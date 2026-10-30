// Three cold requests, binding-isolation controls and raw warning stages. Diagnostic builds only. MIT.
#if CREST_RESOURCE_PROBE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Crest.URP.Diagnostics
{
    public sealed partial class ResourceShutdownProbe
    {
        [Serializable] class ColdWarning {public int request;public string message;}
        [Serializable] class ColdStage {public int request;public long composites,preserves;public Snapshot snapshot;}
        [Serializable] class ColdResult
        {
            public string unity,api,workload;public int control,errors;public long renders;
            public ColdWarning[] warnings;public ColdStage[] stages;
        }
        IEnumerator ColdBindings(string mode)
        {
            var warnings=new List<ColdWarning>();var stages=new List<ColdStage>();int current=-1;
            void Log(string message,string stack,LogType type)
            {
                if(type==LogType.Warning&&warnings.Count<32)warnings.Add(new ColdWarning {request=current,message=message});
            }
            Application.logMessageReceived+=Log;
            _profileCamera=Camera.main;
            RenderPipelineManager.endCameraRendering+=CountRender;
            // Historical omission controls exist only in the archived diagnosis build.
            if(Argument("-crestBindingControl","0")!="0")throw new ArgumentOutOfRangeException("control");
            yield return SetupCost(mode);
            var target=new RenderTexture(960,540,24,RenderTextureFormat.ARGB32){antiAliasing=_costSamples};target.Create();
            try
            {
                for(current=0;current<3;current++)
                {
                    Debug.Log("COLD_REQUEST_BEGIN "+current);
                    RenderPipeline.SubmitRenderRequest(_profileCamera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
                    var done=AsyncGPUReadback.Request(target,0,0,1,0,1,0,1);
                    while(!done.done)yield return null;
                    if(done.hasError)_errors++;
                    stages.Add(new ColdStage {request=current,composites=UnderwaterRenderer.DiagnosticCompositeExecutions,
                        preserves=UnderwaterRenderer.DiagnosticPreserveExecutions,snapshot=Capture("cold"+current)});
                    yield return null;
                }
            }
            finally
            {
                Application.logMessageReceived-=Log;RenderPipelineManager.endCameraRendering-=CountRender;
                target.Release();Destroy(target);
            }
            if(_mainRenders!=3)_errors++;
            long expected=_costWorkload=="UnderwaterOff"?0:3;
            if(warnings.Count!=0||UnderwaterRenderer.DiagnosticCompositeExecutions!=expected
                ||UnderwaterRenderer.DiagnosticPreserveExecutions!=(_costSamples==4?expected:0))_errors++;
            var result=new ColdResult {unity=Application.unityVersion,api=SystemInfo.graphicsDeviceType.ToString(),workload=_costWorkload,
                control=0,errors=_errors,renders=_mainRenders,warnings=warnings.ToArray(),stages=stages.ToArray()};
            string output=Argument("-crestProbeOutput","ColdBindings.json");Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output,JsonUtility.ToJson(result,true));Debug.Log("COLD_BINDINGS_COMPLETED errors="+_errors);Application.Quit(_errors==0?0:1);
        }
    }
}
#endif
