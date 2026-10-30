// Explicit refresh CPU/resource observations, not presented frame/GPU timings. MIT.
#if CREST_RESOURCE_PROBE
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Crest.URP.Diagnostics
{
    public sealed partial class ResourceShutdownProbe
    {
        [Serializable] class ReflectionCostResult
        {
            public string unity,api;public bool reflection;
            public int errors;public long requests,mainRenders,reflectionRenders,outsideRenders;
            public double seconds;public Snapshot before,after;public Metric refreshCpu,mainCpu;
        }
        IEnumerator ReflectionCost()
        {
            bool enabled=Argument("-crestReflectionEnabled","1")=="1";
            var camera=Camera.main;var ocean=OceanRenderer.Instance;
            if(ocean==null)throw new InvalidOperationException("Ocean mode required");
            QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
            var surface=new Material(ocean.OceanMaterial);surface.EnableKeyword("_PLANARREFLECTIONS_ON");ocean.OceanMaterial=surface;
            var reflection=camera.gameObject.AddComponent<OceanPlanarReflection>();
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var last=typeof(OceanPlanarReflection).GetField("_lastRefreshOnFrame",flags);
            typeof(OceanPlanarReflection).GetField("RefreshPerFrames",flags).SetValue(reflection,int.MaxValue);
            typeof(OceanPlanarReflection).GetField("_frameRefreshOffset",flags).SetValue(reflection,int.MaxValue-1);
            last.SetValue(reflection,long.MaxValue);
            reflection.enabled=enabled;
            // Let the original Start initialize viewpoint/skybox before explicit refresh.
            yield return null;
            var result=new ReflectionCostResult {unity=Application.unityVersion,api=SystemInfo.graphicsDeviceType.ToString(),reflection=enabled};
            int phase=0;
            void Count(ScriptableRenderContext context,Camera rendered)
            {
                if(phase==0){result.outsideRenders++;return;}
                if(rendered==camera)result.mainRenders++;
                else if(rendered==reflection.ReflectionCamera)result.reflectionRenders++;
                else result.outsideRenders++;
            }
            RenderPipelineManager.endCameraRendering+=Count;
            var refresh=new Accumulator("Explicit RefreshReflection CPU","Milliseconds",.05);
            var main=new Accumulator("Main Submit CPU","Milliseconds",.05);
            var target=new RenderTexture(960,540,24,RenderTextureFormat.ARGB32);target.Create();
            var request=new UniversalRenderPipeline.SingleCameraRequest {destination=target};
            double begin=Time.realtimeSinceStartupAsDouble;bool measuring=false;
            try
            {
                while(true)
                {
                    if(!measuring&&Time.realtimeSinceStartupAsDouble-begin>=5)
                    {
                        result.before=Capture("before-reflection-cost");
                        result.mainRenders=result.reflectionRenders=result.outsideRenders=0;
                        measuring=true;begin=Time.realtimeSinceStartupAsDouble;
                    }
                    if(measuring&&Time.realtimeSinceStartupAsDouble-begin>=10)break;
                    phase=1;long start=System.Diagnostics.Stopwatch.GetTimestamp();
                    if(enabled)reflection.RefreshReflection();
                    long end=System.Diagnostics.Stopwatch.GetTimestamp();
                    // This sentinel suppresses automatic LateUpdate even if renderedFrameCount is0.
                    // Public RefreshReflection explicitly sets -1 before invoking the original path.
                    last.SetValue(reflection,long.MaxValue);
                    if(measuring)refresh.Add((end-start)*1000.0/System.Diagnostics.Stopwatch.Frequency);
                    start=System.Diagnostics.Stopwatch.GetTimestamp();
                    RenderPipeline.SubmitRenderRequest(camera,request);
                    end=System.Diagnostics.Stopwatch.GetTimestamp();phase=0;
                    if(measuring){main.Add((end-start)*1000.0/System.Diagnostics.Stopwatch.Frequency);result.requests++;}
                    var done=AsyncGPUReadback.Request(target,0,0,1,0,1,0,1);
                    while(!done.done)yield return null;
                    if(done.hasError)_errors++;
                    yield return null;
                }
                result.seconds=Time.realtimeSinceStartupAsDouble-begin;result.after=Capture("after-reflection-cost");
            }
            finally {RenderPipelineManager.endCameraRendering-=Count;target.Release();Destroy(target);}
            if(result.requests<20||result.mainRenders!=result.requests||result.reflectionRenders!=(enabled?result.requests:0)||result.outsideRenders!=0)_errors++;
            result.refreshCpu=refresh.Finish("EXPLICIT_CALL_ONLY");result.mainCpu=main.Finish("EXPLICIT_CALL_ONLY");result.errors=_errors;
            File.WriteAllText(Argument("-crestProbeOutput","ReflectionCost.json"),JsonUtility.ToJson(result,true));
            Debug.Log("REFLECTION_COST_COMPLETED errors="+_errors);Application.Quit(_errors==0?0:1);
        }
    }
}
#endif
