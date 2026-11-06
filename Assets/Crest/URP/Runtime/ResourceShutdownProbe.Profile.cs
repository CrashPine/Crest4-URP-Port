// Opt-in constant-storage profiling. Excluded from normal builds. MIT.
#if CREST_RESOURCE_PROBE
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Crest.URP.Diagnostics
{
    public sealed partial class ResourceShutdownProbe
    {
        [Serializable] class Metric
        {
            public string name, unit, status;
            public long count, positiveCount;
            public double min, max, mean, p50Upper, p95Upper, p99Upper, histogramStep;
            public int[] bins;
            public long[] frequencies;
        }
        sealed class Accumulator
        {
            public readonly string name, unit;
            readonly double _step;
            readonly long[] _histogram;
            long _count, _positive;
            double _sum, _min=double.PositiveInfinity, _max=double.NegativeInfinity;
            public Accumulator(string name,string unit,double step=0)
            {
                this.name=name;this.unit=unit;_step=step;
                if(step>0)_histogram=new long[10001];
            }
            public void Add(double value)
            {
                if(double.IsNaN(value)||double.IsInfinity(value)||value<0)return;
                _count++;if(value>0)_positive++;_sum+=value;_min=Math.Min(_min,value);_max=Math.Max(_max,value);
                if(_histogram!=null)_histogram[Math.Min(10000,(int)(value/_step))]++;
            }
            double Percentile(double fraction)
            {
                if(_histogram==null||_count==0)return -1;
                long required=(long)Math.Ceiling(_count*fraction), total=0;
                for(int i=0;i<_histogram.Length;i++){total+=_histogram[i];if(total>=required)return i==10000?_max:(i+1)*_step;}
                return _max;
            }
            public Metric Finish(string status)
            {
                var indices=_histogram==null?new int[0]:Enumerable.Range(0,_histogram.Length).Where(i=>_histogram[i]>0).ToArray();
                return new Metric {name=name,unit=unit,status=_count==0?"NO_SAMPLES":(name=="GPU Frame Time"&&_positive==0?"NO_POSITIVE_GPU_TIMINGS":status),count=_count,positiveCount=_positive,
                    min=_count==0?-1:_min,max=_count==0?-1:_max,mean=_count==0?-1:_sum/_count,
                    p50Upper=Percentile(.5),p95Upper=Percentile(.95),p99Upper=Percentile(.99),histogramStep=_step,
                    bins=indices,frequencies=indices.Select(i=>_histogram[i]).ToArray()};
            }
        }
        [Serializable] class CounterInfo { public string name,category,unit,dataType; }
        sealed class Counter
        {
            public ProfilerRecorder recorder;
            public bool floating,renderCounter;
            public Accumulator values;
        }
        [Serializable] class ProfileResult
        {
            public string unity,api,gpu,cpu,mode,pipeline,quality,colorSpace;
            public string method="Offscreen SingleCameraRequest + one-pixel completion readback; no presentation/FPS claim";
            public bool development,batch,frameTimingEnabled;
            public int width,height,msaa,vsync,targetFrameRate,ramMB,graphicsMemoryMB,errors;
            public int oceanResolution,lodCount,fftResolution;
            public float windKPH,renderScale;
            public double warmSeconds,sampleSeconds;
            public long sampleRequests,updateTicksWhileWaiting,mainCameraRenders;
            public long mainThreadAllocatedBytes,rawThreadApiDelta,gen0Collections,gen1Collections,gen2Collections,gcCounterResets;
            public string allocationStatus="System.GC thread API failed known-allocation calibration; use native Submit metric";
            public Snapshot before,after;
            public CounterInfo[] availableCounters;
            public string[] missingCounters;
            public Metric[] metrics;
            public string costWorkload;
            public WorkloadCost beforeCost,afterCost;
            public long[] underwaterGcBytes,underwaterGcCalls,gpuNameCalibration;
        }
        [Serializable] class CapabilitySample
        {
            public int frame;public double[] last,current;public ulong timestamp;public bool freshTiming;public double gpuMs,cpuMs;
        }
        [Serializable] class CapabilityPhase
        {
            public string name;public int enabledControls,visibleControls;public long cameraRenders;
            public double seconds;public Metric[] counters;public CapabilitySample[] samples;
        }
        [Serializable] class CapabilityResult
        {
            public string unity,api,gpu,mode;public bool batch,development,gpuRecorderSupported,frameTimingEnabled,cameraEnabled,featureDisabled;
            public ulong gpuTimerFrequency;public int width,height,errors,vsync,targetFrameRate;
            public CounterInfo[] availableCounters,selectedCounters;public CapabilityPhase[] phases;
        }
        IEnumerator CounterCapability(string mode)
        {
            if(Application.isBatchMode)throw new InvalidOperationException("Counter capability requires ordinary non-batch camera rendering");
            QualitySettings.vSyncCount=0;Application.targetFrameRate=60;
            _profileCamera=Camera.main;_profileCamera.enabled=true;
            _profileCamera.transform.position=new Vector3(0,5,-15);_profileCamera.transform.LookAt(new Vector3(0,2,0));
            _profileCamera.aspect=(float)Screen.width/Screen.height;
            var controls=new GameObject("Counter positive control");controls.SetActive(false);
            var shader=Shader.Find("Universal Render Pipeline/Unlit");
            if(shader==null)throw new InvalidOperationException("Counter control shader not retained");
            var material=new Material(shader){enableInstancing=false};
            var renderers=new List<Renderer>();
            for(int i=0;i<64;i++)
            {
                var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.transform.SetParent(controls.transform);
                cube.transform.position=new Vector3((i%8-3.5f)*.6f,2.5f,(i/8-3.5f)*.6f);
                cube.transform.localScale=Vector3.one*.4f;
                var renderer=cube.GetComponent<Renderer>();renderer.sharedMaterial=material;renderers.Add(renderer);
            }
            var handles=new List<ProfilerRecorderHandle>();ProfilerRecorderHandle.GetAvailable(handles);
            var available=handles.Select(h=>ProfilerRecorderHandle.GetDescription(h)).ToArray();
            string[] names={"GPU Frame Time","Draw Calls Count","Batches Count","SetPass Calls Count","Triangles Count","Vertices Count","Dynamic Batches Count","Static Batches Count","Instanced Batches Count","SRP Batcher Draw Calls Count"};
            var counters=new List<Counter>();var selected=new List<CounterInfo>();
            foreach(var name in names)
            {
                int index=Array.FindIndex(available,d=>d.Name==name&&d.Category.Name=="Render");if(index<0)continue;
                var d=available[index];var recorder=new ProfilerRecorder(handles[index],1,ProfilerRecorderOptions.Default);recorder.Start();
                counters.Add(new Counter{recorder=recorder,floating=d.DataType.ToString().Contains("Double")||d.DataType.ToString().Contains("Float")});
                selected.Add(new CounterInfo{name=d.Name,category=d.Category.Name,unit=d.UnitType.ToString(),dataType=d.DataType.ToString()});
            }
            var phases=new List<CapabilityPhase>();var timings=new FrameTiming[1];ulong lastTimestamp=0;
            RenderPipelineManager.endCameraRendering+=CountRender;
            try
            {
                for(int phase=0;phase<3;phase++)
                {
                    controls.SetActive(phase==1);
                    double warm=Time.realtimeSinceStartupAsDouble;
                    while(Time.realtimeSinceStartupAsDouble-warm<2){FrameTimingManager.CaptureFrameTimings();yield return null;}
                    var planes=GeometryUtility.CalculateFrustumPlanes(_profileCamera);
                    int visible=phase==1?renderers.Count(r=>GeometryUtility.TestPlanesAABB(planes,r.bounds)):0;
                    var samples=new List<CapabilitySample>();
                    var metrics=selected.Select(d=>new Accumulator(d.name,d.unit)).ToArray();
                    long before=_mainRenders;double start=Time.realtimeSinceStartupAsDouble;
                    while(Time.realtimeSinceStartupAsDouble-start<3)
                    {
                        FrameTimingManager.CaptureFrameTimings();yield return null;
                        var sample=new CapabilitySample{frame=Time.frameCount,last=new double[counters.Count],current=new double[counters.Count]};
                        for(int c=0;c<counters.Count;c++)
                        {
                            var counter=counters[c];sample.last[c]=sample.current[c]=-1;
                            if(!counter.recorder.Valid||counter.recorder.Count==0)continue;
                            sample.last[c]=counter.floating?counter.recorder.LastValueAsDouble:counter.recorder.LastValue;
                            sample.current[c]=counter.floating?-1:counter.recorder.CurrentValue;metrics[c].Add(sample.last[c]);
                        }
                        if(FrameTimingManager.GetLatestTimings(1,timings)>0)
                        {
                            sample.timestamp=timings[0].frameStartTimestamp;sample.gpuMs=timings[0].gpuFrameTime;sample.cpuMs=timings[0].cpuFrameTime;
                            sample.freshTiming=sample.timestamp!=lastTimestamp;lastTimestamp=sample.timestamp;
                        }
                        if(samples.Count>=4096)throw new InvalidOperationException("Capability sample bound exceeded");samples.Add(sample);
                    }
                    phases.Add(new CapabilityPhase{name=phase==0?"baseline":phase==1?"positive64":"restored",enabledControls=phase==1?64:0,visibleControls=visible,
                        cameraRenders=_mainRenders-before,seconds=Time.realtimeSinceStartupAsDouble-start,counters=metrics.Select(m=>m.Finish("Observed regular camera LastValue")).ToArray(),samples=samples.ToArray()});
                    Debug.Log("COUNTER_CAPABILITY_PHASE "+phases.Last().name+" renders="+phases.Last().cameraRenders);
                }
            }
            finally {RenderPipelineManager.endCameraRendering-=CountRender;foreach(var c in counters)c.recorder.Dispose();Destroy(controls);Destroy(material);}
            var result=new CapabilityResult{unity=Application.unityVersion,api=SystemInfo.graphicsDeviceType.ToString(),gpu=SystemInfo.graphicsDeviceName,mode=mode,
                batch=Application.isBatchMode,development=Debug.isDebugBuild,gpuRecorderSupported=SystemInfo.supportsGpuRecorder,frameTimingEnabled=FrameTimingManager.IsFeatureEnabled(),
                gpuTimerFrequency=FrameTimingManager.GetGpuTimerFrequency(),cameraEnabled=_profileCamera.enabled,featureDisabled=Array.IndexOf(Environment.GetCommandLineArgs(),"-crestProbeDisableFeature")>=0,
                width=Screen.width,height=Screen.height,errors=_errors,vsync=QualitySettings.vSyncCount,targetFrameRate=Application.targetFrameRate,
                availableCounters=available.Select(d=>new CounterInfo{name=d.Name,category=d.Category.Name,unit=d.UnitType.ToString(),dataType=d.DataType.ToString()}).ToArray(),selectedCounters=selected.ToArray(),phases=phases.ToArray()};
            File.WriteAllText(Argument("-crestProbeOutput","CounterCapability.json"),JsonUtility.ToJson(result,true));
            Debug.Log("COUNTER_CAPABILITY_COMPLETED errors="+_errors);Application.Quit(_errors==0?0:1);
        }

        sealed class StableTime : ITimeProvider
        {
            public float CurrentTime => 123.5f;
            public float DeltaTime => 0;
            public float DeltaTimeDynamics => 0;
        }
        [Serializable] class StableSample
        {
            public int frame;public long utcTicks,renders;public bool focused,freshTiming;
            public double[] counters;public ulong timestamp;public double gpuMs,cpuMs;
        }
        [Serializable] class StablePhase
        {
            public string name;public bool surfaceVisible;public double startUnixSeconds,endUnixSeconds;
            public int width,height,tiles;public StableSample[] samples;
        }
        [Serializable] class StableResult
        {
            public string unity,api,gpu,method;public int errors,width,height,vsync,targetFrameRate,lods,resolution;
            public float fixedTime;public CounterInfo[] counters;public StablePhase[] phases;
        }
        IEnumerator StableGpuWindow(string mode)
        {
            if(Application.isBatchMode||mode!="Ocean")throw new InvalidOperationException("Stable GPU window requires visible non-batch Ocean mode");
            QualitySettings.vSyncCount=0;Application.targetFrameRate=60;
            foreach(var controller in FindObjectsByType<PortDemoController>(FindObjectsSortMode.None))controller.enabled=false;
            _profileCamera=Camera.main;_profileCamera.enabled=true;
            _profileCamera.transform.SetPositionAndRotation(new Vector3(0,5,-15),Quaternion.LookRotation(new Vector3(0,-3,15)));
            _profileCamera.aspect=(float)Screen.width/Screen.height;
            var ocean=OceanRenderer.Instance;ocean.ViewCamera=_profileCamera;
            var time=new StableTime();ocean.PushTimeProvider(time);
            var renderers=ocean.Root.GetComponentsInChildren<Renderer>(true);
            var handles=new List<ProfilerRecorderHandle>();ProfilerRecorderHandle.GetAvailable(handles);
            var available=handles.Select(h=>ProfilerRecorderHandle.GetDescription(h)).ToArray();
            var counters=new List<Counter>();var selected=new List<CounterInfo>();
            foreach(var name in new[]{"GPU Frame Time","Draw Calls Count","Triangles Count","Vertices Count"})
            {
                int i=Array.FindIndex(available,d=>d.Name==name&&d.Category.Name=="Render");
                if(i<0)throw new InvalidOperationException("Missing accepted Render counter: "+name);
                var d=available[i];var recorder=new ProfilerRecorder(handles[i],1,ProfilerRecorderOptions.Default);recorder.Start();
                counters.Add(new Counter{recorder=recorder,floating=d.DataType.ToString().Contains("Double")||d.DataType.ToString().Contains("Float")});
                selected.Add(new CounterInfo{name=d.Name,category=d.Category.Name,unit=d.UnitType.ToString(),dataType=d.DataType.ToString()});
            }
            var phases=new List<StablePhase>();var timings=new FrameTiming[1];ulong lastTimestamp=0;
            RenderPipelineManager.endCameraRendering+=CountRender;
            try
            {
                // Prewarm the visible surface before starting either baseline.
                double warm=Time.realtimeSinceStartupAsDouble;
                while(Time.realtimeSinceStartupAsDouble-warm<20){FrameTimingManager.CaptureFrameTimings();yield return null;}
                for(int phase=0;phase<3;phase++)
                {
                    bool visible=phase==1;
                    foreach(var renderer in renderers)renderer.forceRenderingOff=!visible;
                    warm=Time.realtimeSinceStartupAsDouble;
                    while(Time.realtimeSinceStartupAsDouble-warm<5){FrameTimingManager.CaptureFrameTimings();yield return null;}
                    var samples=new List<StableSample>(1024);double start=Time.realtimeSinceStartupAsDouble;
                    var data=new StablePhase{name=phase==0?"A0_hidden":phase==1?"B_surface":"A1_hidden",surfaceVisible=visible,
                        startUnixSeconds=(DateTime.UtcNow.Ticks-621355968000000000L)/10000000.0,width=Screen.width,height=Screen.height,tiles=renderers.Length};
                    while(Time.realtimeSinceStartupAsDouble-start<10)
                    {
                        FrameTimingManager.CaptureFrameTimings();yield return null;
                        var sample=new StableSample{frame=Time.frameCount,utcTicks=DateTime.UtcNow.Ticks,focused=Application.isFocused,renders=_mainRenders,counters=new double[counters.Count]};
                        for(int i=0;i<counters.Count;i++)
                        {
                            var c=counters[i];sample.counters[i]=!c.recorder.Valid||c.recorder.Count==0?-1:c.floating?c.recorder.LastValueAsDouble:c.recorder.LastValue;
                        }
                        if(FrameTimingManager.GetLatestTimings(1,timings)>0)
                        {
                            sample.timestamp=timings[0].frameStartTimestamp;sample.gpuMs=timings[0].gpuFrameTime;sample.cpuMs=timings[0].cpuFrameTime;
                            sample.freshTiming=sample.timestamp!=lastTimestamp;lastTimestamp=sample.timestamp;
                        }
                        if(samples.Count>=4096)throw new InvalidOperationException("Stable window sample bound exceeded");samples.Add(sample);
                    }
                    data.endUnixSeconds=(DateTime.UtcNow.Ticks-621355968000000000L)/10000000.0;data.samples=samples.ToArray();phases.Add(data);
                    Debug.Log("STABLE_GPU_PHASE "+data.name+" samples="+samples.Count);
                }
            }
            finally
            {
                RenderPipelineManager.endCameraRendering-=CountRender;
                foreach(var counter in counters)counter.recorder.Dispose();foreach(var renderer in renderers)if(renderer!=null)renderer.forceRenderingOff=false;
                ocean.PopTimeProvider(time);
            }
            var result=new StableResult{unity=Application.unityVersion,api=SystemInfo.graphicsDeviceType.ToString(),gpu=SystemInfo.graphicsDeviceName,
                method="Ordinary camera frames; frozen Calm; surface hidden-visible-hidden;20s prewarm/5s settle/10s samples per phase; simulation retained",
                errors=_errors,width=Screen.width,height=Screen.height,vsync=QualitySettings.vSyncCount,targetFrameRate=Application.targetFrameRate,
                lods=ocean.CurrentLodCount,resolution=ocean.LodDataResolution,fixedTime=123.5f,counters=selected.ToArray(),phases=phases.ToArray()};
            File.WriteAllText(Argument("-crestProbeOutput","StableGpu.json"),JsonUtility.ToJson(result,true));
            Debug.Log("STABLE_GPU_COMPLETED errors="+_errors);Application.Quit(_errors==0?0:1);
        }

        static long ThreadAllocated()
        {
            try{return GC.GetAllocatedBytesForCurrentThread();}
            catch(NotSupportedException){return -1;}
            catch(NotImplementedException){return -1;}
        }
        [Serializable] class AllocationObservation
        {
            public bool allocationMarkerEnabled;
            public long expectedDataBytes=262144,threadDelta,heapUsedDelta,currentCounterDelta,lastCounter,allocationEvents;
        }
        [Serializable] class AllocationCheck
        {
            public string unity,api;
            public AllocationObservation[] observations;
        }
        IEnumerator CheckAllocationCounters()
        {
            var observations=new List<AllocationObservation>();
            var memory=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);
            var marker=default(ProfilerRecorder);
            try
            {
                for(int pass=0;pass<2;pass++)
                {
                    if(pass==1)marker=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC.Alloc",1);
                    yield return null;yield return null;
                    var blocks=new byte[4][];
                    long threadBefore=ThreadAllocated(),heapBefore=UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong(),currentBefore=memory.CurrentValue;
                    for(int i=0;i<4;i++)blocks[i]=new byte[65536];
                    long threadAfter=ThreadAllocated(),heapAfter=UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong(),currentAfter=memory.CurrentValue;
                    yield return null;
                    observations.Add(new AllocationObservation {allocationMarkerEnabled=pass==1,
                        threadDelta=threadBefore<0||threadAfter<0?-1:threadAfter-threadBefore,heapUsedDelta=heapAfter-heapBefore,
                        currentCounterDelta=currentAfter-currentBefore,lastCounter=memory.Count>0?memory.LastValue:-1,
                        allocationEvents=marker.Valid&&marker.Count>0?marker.GetSample(0).Count:-1});
                    GC.KeepAlive(blocks);
                }
            }
            finally {memory.Dispose();if(marker.Valid)marker.Dispose();}
            var result=new AllocationCheck {unity=Application.unityVersion,api=SystemInfo.graphicsDeviceType.ToString(),observations=observations.ToArray()};
            string path=Argument("-crestProbeOutput","AllocationCheck.json");Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path,JsonUtility.ToJson(result,true));Debug.Log("ALLOCATION_COUNTER_CHECK_COMPLETED");Application.Quit(0);
        }
        Camera _profileCamera;
        long _mainRenders;
        void CountRender(ScriptableRenderContext context,Camera camera) {if(camera==_profileCamera)_mainRenders++;}
        IEnumerator Profile(string mode)
        {
            if(Application.isBatchMode || mode=="OceanUnload")throw new InvalidOperationException("Profiling requires non-batch Empty or Ocean.");
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-crestProfileGCCheck")>=0)
            {
                yield return CheckAllocationCounters();yield break;
            }
            double seconds=double.Parse(Argument("-crestProfileSeconds","60"),CultureInfo.InvariantCulture);
            if(seconds<1||seconds>600)throw new ArgumentOutOfRangeException("-crestProfileSeconds");
            Debug.Log("PROFILE_STAGE setup");
            QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
            _profileCamera=Camera.main;
            yield return SetupCost(mode);
            RenderPipelineManager.endCameraRendering+=CountRender;
            var target=new RenderTexture(Screen.width,Screen.height,24,RenderTextureFormat.ARGB32);
            target.antiAliasing=_costSamples;
            target.Create();
            var request=new UniversalRenderPipeline.SingleCameraRequest {destination=target};
            RenderPipeline.SubmitRenderRequest(_profileCamera,request);
            var initial=AsyncGPUReadback.Request(target,0,0,1,0,1,0,1);
            while(!initial.done)yield return null;
            if(initial.hasError)_errors++;
            var handles=new List<ProfilerRecorderHandle>();ProfilerRecorderHandle.GetAvailable(handles);
            Debug.Log("PROFILE_STAGE discovered handles="+handles.Count);
            var available=handles.Select(h=>ProfilerRecorderHandle.GetDescription(h)).ToArray();
            string[] names={"Main Thread","Render Thread","GPU Frame Time","GC Allocated In Frame","GC Used Memory","GC Reserved Memory",
                "Draw Calls Count","Batches Count","SetPass Calls Count","Triangles Count","Vertices Count","Gfx Used Memory","Gfx Reserved Memory",
                "Render Textures Count","Render Textures Bytes","Vertex Buffer Bytes","Index Buffer Bytes","Compute Buffer Count","Compute Buffer Bytes"};
            var missing=new List<string>();var counters=new List<Counter>();
            foreach(var name in names)
            {
                int index=Array.FindIndex(available,d=>d.Name==name);
                if(index<0){missing.Add(name);continue;}
                var description=available[index];string unit=description.UnitType.ToString();
                var recorder=new ProfilerRecorder(handles[index],1,ProfilerRecorderOptions.Default);
                recorder.Start();
                counters.Add(new Counter {recorder=recorder,floating=description.DataType.ToString().Contains("Double")||description.DataType.ToString().Contains("Float"),renderCounter=name=="Draw Calls Count"||name=="Batches Count"||name=="SetPass Calls Count"||name=="Triangles Count"||name=="Vertices Count",values=new Accumulator(name,unit,unit.Contains("Nanosecond")?50000:0)});
            }
            Debug.Log("PROFILE_STAGE counters="+counters.Count);
            var allocationCounter=counters.FirstOrDefault(c=>c.values.name=="GC Allocated In Frame");
            if(Argument("-crestGcSites","0")=="1")
            {
                if(allocationCounter==null)throw new InvalidOperationException("Native GC counter unavailable");
                UnderwaterRenderer.DiagnosticAllocated=()=>allocationCounter.recorder.CurrentValue;
            }
            var siteBytes=new long[10];var siteCalls=new long[10];
            var submitGc=new Accumulator("GC bytes during SubmitRenderRequest","Bytes");
            var gpu=new Accumulator("FrameTiming unique GPU","Milliseconds",.05);
            var cpu=new Accumulator("FrameTiming unique CPU","Milliseconds",.05);
            var present=new Accumulator("FrameTiming unique present wait","Milliseconds",.05);
            var delta=new Accumulator("Offscreen request interval","Milliseconds",.05);
            var submitCpu=new Accumulator("SubmitRenderRequest CPU","Milliseconds",.05);
            var completion=new Accumulator("One-pixel completion latency","Milliseconds",.05);
            var timing=new FrameTiming[1];ulong lastTiming=0;
            var result=new ProfileResult {unity=Application.unityVersion,api=SystemInfo.graphicsDeviceType.ToString(),gpu=SystemInfo.graphicsDeviceName,
                cpu=SystemInfo.processorType,ramMB=SystemInfo.systemMemorySize,graphicsMemoryMB=SystemInfo.graphicsMemorySize,
                mode=mode,pipeline=GraphicsSettings.currentRenderPipeline.name,quality=QualitySettings.names[QualitySettings.GetQualityLevel()],
                colorSpace=QualitySettings.activeColorSpace.ToString(),development=Debug.isDebugBuild,batch=Application.isBatchMode,
                width=Screen.width,height=Screen.height,msaa=((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).msaaSampleCount,
                renderScale=((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).renderScale,
                vsync=QualitySettings.vSyncCount,targetFrameRate=Application.targetFrameRate,frameTimingEnabled=FrameTimingManager.IsFeatureEnabled(),
                availableCounters=available.Select(d=>new CounterInfo {name=d.Name,category=d.Category.Name,unit=d.UnitType.ToString(),dataType=d.DataType.ToString()}).ToArray(),
                missingCounters=missing.ToArray()};
            var ocean=OceanRenderer.Instance;
            if(ocean!=null){result.oceanResolution=ocean.LodDataResolution;result.lodCount=ocean.CurrentLodCount;result.windKPH=ocean.WindSpeedKPH;}
            var fft=Resources.FindObjectsOfTypeAll<ShapeFFT>();if(fft.Length>0)result.fftResolution=fft[0]._resolution;
            try
            {
                double warm=Time.realtimeSinceStartupAsDouble;
                while(Time.realtimeSinceStartupAsDouble-warm<5)
                {
                    RenderPipeline.SubmitRenderRequest(_profileCamera,request);
                    var done=AsyncGPUReadback.Request(target,0,0,1,0,1,0,1);
                    while(!done.done)yield return null;
                    if(done.hasError)_errors++;
                    FrameTimingManager.CaptureFrameTimings();yield return null;
                }
                result.warmSeconds=Time.realtimeSinceStartupAsDouble-warm;
                Debug.Log("PROFILE_STAGE warm-complete");
                result.before=Capture("before-profile");
                if(Argument("-crestGcSites","0")=="1")
                {
                    string cachedName=SystemInfo.graphicsDeviceName,sink=null;bool flag=false;
                    Action[] controls={()=>{sink=SystemInfo.graphicsDeviceName;},()=>{sink=cachedName.ToLowerInvariant();},
                        ()=>{flag=Crest.Helpers.IsIntelGPU();},()=>{flag=cachedName.IndexOf("intel",StringComparison.OrdinalIgnoreCase)>=0;}};
                    result.gpuNameCalibration=new long[4];
                    for(int c=0;c<4;c++)
                    {
                        controls[c](); // Prime outside measurement.
                        long before=allocationCounter.recorder.CurrentValue;
                        for(int i=0;i<200;i++)controls[c]();
                        result.gpuNameCalibration[c]=allocationCounter.recorder.CurrentValue-before;
                    }
                    GC.KeepAlive(sink);GC.KeepAlive(flag);
                }
                // Snapshot allocation and recorder priming must be outside the sampling window.
                for(int i=0;i<8;i++) {FrameTimingManager.CaptureFrameTimings();yield return null;}
                Debug.Log("PROFILE_STAGE sample-start");
                yield return null;
                Array.Copy(UnderwaterRenderer.DiagnosticGcBytes,siteBytes,10);Array.Copy(UnderwaterRenderer.DiagnosticGcCalls,siteCalls,10);
                long renderStart=_mainRenders;
                long allocatedStart=ThreadAllocated();
                int gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2);
                double start=Time.realtimeSinceStartupAsDouble,last=start;
                result.costWorkload=_costWorkload;result.beforeCost=ReadCost(target);
                while(Time.realtimeSinceStartupAsDouble-start<seconds)
                {
                    long gcBefore=allocationCounter==null?-1:allocationCounter.recorder.CurrentValue;
                    long cpuStart=System.Diagnostics.Stopwatch.GetTimestamp();
                    RenderPipeline.SubmitRenderRequest(_profileCamera,request);
                    long cpuEnd=System.Diagnostics.Stopwatch.GetTimestamp();
                    long gcAfter=allocationCounter==null?-1:allocationCounter.recorder.CurrentValue;
                    if(gcBefore>=0&&gcAfter>=gcBefore)submitGc.Add(gcAfter-gcBefore);
                    else result.gcCounterResets++;
                    submitCpu.Add((cpuEnd-cpuStart)*1000.0/System.Diagnostics.Stopwatch.Frequency);
                    foreach(var counter in counters)
                        if(counter.renderCounter&&counter.recorder.Valid)counter.values.Add(counter.floating?counter.recorder.CurrentValueAsDouble:counter.recorder.CurrentValue);
                    double waitStart=Time.realtimeSinceStartupAsDouble;
                    var done=AsyncGPUReadback.Request(target,0,0,1,0,1,0,1);
                    while(!done.done){result.updateTicksWhileWaiting++;yield return null;}
                    if(done.hasError)_errors++;
                    completion.Add((Time.realtimeSinceStartupAsDouble-waitStart)*1000);
                    FrameTimingManager.CaptureFrameTimings();
                    yield return null;
                    double now=Time.realtimeSinceStartupAsDouble;delta.Add((now-last)*1000);last=now;result.sampleRequests++;
                    foreach(var counter in counters)
                        if(!counter.renderCounter&&counter.recorder.Valid&&counter.recorder.Count>0)counter.values.Add(counter.floating?counter.recorder.LastValueAsDouble:counter.recorder.LastValue);
                    if(FrameTimingManager.GetLatestTimings(1,timing)>0 && timing[0].frameStartTimestamp!=lastTiming)
                    {
                        lastTiming=timing[0].frameStartTimestamp;
                        if(timing[0].gpuFrameTime>0)gpu.Add(timing[0].gpuFrameTime);
                        if(timing[0].cpuFrameTime>0)cpu.Add(timing[0].cpuFrameTime);
                        present.Add(timing[0].cpuMainThreadPresentWaitTime);
                    }
                }
                result.sampleSeconds=Time.realtimeSinceStartupAsDouble-start;result.mainCameraRenders=_mainRenders-renderStart;
                result.afterCost=ReadCost(target);
                result.underwaterGcBytes=new long[10];result.underwaterGcCalls=new long[10];
                for(int i=0;i<10;i++){result.underwaterGcBytes[i]=UnderwaterRenderer.DiagnosticGcBytes[i]-siteBytes[i];result.underwaterGcCalls[i]=UnderwaterRenderer.DiagnosticGcCalls[i]-siteCalls[i];}
                long allocatedEnd=ThreadAllocated();result.rawThreadApiDelta=allocatedStart<0||allocatedEnd<0?-1:allocatedEnd-allocatedStart;result.mainThreadAllocatedBytes=-1;
                result.gen0Collections=GC.CollectionCount(0)-gc0;result.gen1Collections=GC.CollectionCount(1)-gc1;result.gen2Collections=GC.CollectionCount(2)-gc2;
            }
            finally
            {
                UnderwaterRenderer.DiagnosticAllocated=null;
                RenderPipelineManager.endCameraRendering-=CountRender;
                foreach(var counter in counters)counter.recorder.Dispose();
                result.after=Capture("after-profile");
                target.Release();Destroy(target);
            }
            result.metrics=counters.Select(c=>c.values.Finish(c.renderCounter?"CURRENT_AFTER_SUBMIT":"LAST_COMPLETED_UPDATE_AFTER_READBACK")).Concat(new[]{gpu.Finish("UNIQUE_TIMINGS"),cpu.Finish("UNIQUE_TIMINGS"),present.Finish("UNIQUE_TIMINGS"),delta.Finish("SAMPLED"),submitGc.Finish("NATIVE_CURRENT_DELTA_AROUND_SUBMIT"),submitCpu.Finish("SAMPLED"),completion.Finish("INCLUDES_DRIVER_AND_COPY_NOT_GPU_FRAME_TIME")}).ToArray();
            if(result.mainCameraRenders<1 || result.mainCameraRenders!=result.sampleRequests || result.before.oceans!=(mode=="Empty"?0:1))_errors++;
            ValidateCost(result);
            result.errors=_errors;
            string path=Argument("-crestProbeOutput","Profile.json");Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path,JsonUtility.ToJson(result,true));
            Debug.Log("RESOURCE_PROFILE_COMPLETED "+mode+" samples="+result.sampleRequests+" renders="+result.mainCameraRenders+" errors="+_errors);
            Application.Quit(_errors==0?0:1);
        }
    }
}
#endif
