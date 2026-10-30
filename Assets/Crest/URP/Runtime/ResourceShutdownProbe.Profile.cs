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
