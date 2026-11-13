// Compiled original-scene navigation/material controls. Diagnostic-only; MIT.
#if CREST_RESOURCE_PROBE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Crest.URP.Diagnostics
{
    [DefaultExecutionOrder(300)]
    public sealed partial class ResourceShutdownProbe
    {
        [Serializable] class ExamplePlayerRow
        {
            public string name,camera,renderer;public int index,pink,nonBackground,missingScripts,activeChildren,frame,lodFrame;
            public string[] materials;public int fillChanged=-1,restoration=-1;
        }
        [Serializable] class ExamplePlayerReport
        {
            public string unity,api,gpu,scene,quality,scope;public int errors,msaa;public float renderScale;
            public bool nextWrap,previousWrap;public List<ExamplePlayerRow> rows=new List<ExamplePlayerRow>();
        }
        sealed class ExampleFixedTime : ITimeProvider { public float CurrentTime=>1;public float DeltaTime=>0;public float DeltaTimeDynamics=>0; }
        Action _examplesCapture;
        string _examplesFailure;
        RenderTexture _examplesTarget;
        ExamplePlayerReport _examplesReport;
        void LateUpdate()
        {
            ObserveNavigationInput();
            if(_originalScenePacing) AsyncGPUReadback.WaitAllRequests();
            if(_examplesCapture==null)return;
            var capture=_examplesCapture;_examplesCapture=null;
            try{capture();}
            catch(Exception error){_examplesFailure=error.ToString();Debug.LogException(error);}
        }
        static void CheckExample(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        void SaveExamples()
        {
            _examplesReport.errors=_errors;
            File.WriteAllText(Argument("-crestProbeOutput","OriginalExamples.json"),JsonUtility.ToJson(_examplesReport,true));
        }
        IEnumerator CaptureExample(Action capture)
        {
            _examplesFailure=null;_examplesCapture=capture;
            float until=Time.realtimeSinceStartup+20;
            while(_examplesCapture!=null&&Time.realtimeSinceStartup<until)yield return null;
            if(_examplesCapture!=null)_examplesFailure="Capture did not execute at LateUpdate300";
            if(_examplesFailure!=null){SaveExamples();Application.Quit(1);yield break;}
        }
        Color32[] ReadExample(Camera camera,string image=null)
        {
            CheckExample(BuildCommandBufferBase._lastUpdateFrame==OceanRenderer.FrameCount,"Capture before current ocean update");
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=_examplesTarget});
            var old=RenderTexture.active;var cpu=new Texture2D(640,360,TextureFormat.RGBA32,false);
            try
            {
                RenderTexture.active=_examplesTarget;cpu.ReadPixels(new Rect(0,0,640,360),0,0);cpu.Apply();
                if(image!=null)File.WriteAllBytes(Path.Combine(Argument("-crestProbeImages","."),image),cpu.EncodeToPNG());
                var pixels=cpu.GetPixels32();CheckExample(pixels.Count(p=>p.r>245&&p.g<10&&p.b>245)==0,"Original scene error-pink");return pixels;
            }
            finally{RenderTexture.active=old;Destroy(cpu);}
        }
        IEnumerator OriginalExamplesPlayer()
        {
            Directory.CreateDirectory(Argument("-crestProbeImages","."));
            var pipeline=(UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            _examplesReport=new ExamplePlayerReport{unity=Application.unityVersion,api=SystemInfo.graphicsDeviceType.ToString(),gpu=SystemInfo.graphicsDeviceName,scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                quality=QualitySettings.names[QualitySettings.GetQualityLevel()],msaa=pipeline.msaaSampleCount,renderScale=pipeline.renderScale,
                scope="Compiled original24 scene navigation/material smoke +3 fill causal controls at LateUpdate300 after ocean200. Public Next/Previous via reflection avoids Crest.Examples assembly cycle. Not keyboard/GUI/all-feature/performance acceptance."};
            Func<MonoBehaviour,bool> isController=c=>{for(var t=c.GetType();t!=null;t=t.BaseType)if(t.FullName=="Crest.Examples.ExamplesController")return true;return false;};
            var controllers=FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(c=>c!=null&&isController(c)).ToArray();
            var controller=controllers.Single(c=>c.transform.childCount==24&&c.transform.GetChild(0).name=="Albedo");
            // Original input controllers stay active: absent devices must be safe too.
            foreach(Transform child in controller.transform)child.gameObject.SetActive(false);
            var next=controller.GetType().GetMethod("Next");var previous=controller.GetType().GetMethod("Previous");
            _examplesTarget=new RenderTexture(640,360,24,RenderTextureFormat.ARGB32);_examplesTarget.Create();
            for(int i=0;i<24;i++)
            {
                next.Invoke(controller,null);var config=controller.transform.GetChild(i);
                CheckExample(config.gameObject.activeInHierarchy,"Next selected wrong child: "+i);
                int active=controller.transform.Cast<Transform>().Count(c=>c.gameObject.activeSelf);CheckExample(active==1,"Multiple active configurations");
                for(int warm=0;warm<45;warm++)yield return null;
                var ocean=OceanRenderer.Instance;var camera=Camera.main;CheckExample(ocean!=null&&camera!=null,"Missing ocean/camera");ocean.ViewCamera=camera;
                var cameraData=camera.GetUniversalAdditionalCameraData();cameraData.requiresDepthTexture=true;cameraData.renderPostProcessing=false;camera.allowMSAA=false;camera.aspect=16f/9;
                for(int warm=0;warm<2;warm++)yield return null;
                var row=new ExamplePlayerRow{index=i,name=config.name,activeChildren=active,camera=camera.transform.position.ToString("R"),renderer=cameraData.scriptableRenderer.GetType().Name,
                    missingScripts=config.GetComponentsInChildren<Transform>(true).Sum(c=>c.GetComponents<MonoBehaviour>().Count(m=>m==null))};
                yield return CaptureExample(()=>
                {
                    var pixels=ReadExample(camera);row.pink=0;row.nonBackground=pixels.Count(p=>!p.Equals(pixels[0]));row.frame=OceanRenderer.FrameCount;row.lodFrame=BuildCommandBufferBase._lastUpdateFrame;
                    row.materials=FindObjectsByType<Renderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Select(m=>m.name+":"+m.shader.name+":"+m.shader.isSupported).Distinct().OrderBy(x=>x).ToArray();
                    CheckExample(row.missingScripts==0&&row.nonBackground>5000,"Missing scripts/empty scene: "+row.name);
                    CheckExample(!row.materials.Any(m=>m.Contains("Hidden/InternalErrorShader")||m.EndsWith(":False")),"Unsupported material in "+row.name);
                });
                if(_examplesFailure!=null)yield break;
                if(new[]{"River","DynamicWavesReflections","VaryingWaterLevel"}.Contains(config.name))
                {
                    var animator=camera.GetComponent<Animator>();bool animate=animator!=null&&animator.enabled;if(animator!=null)animator.enabled=false;
                    float scale=Time.timeScale;Time.timeScale=0;var clock=new ExampleFixedTime();ocean.PushTimeProvider(clock);
                    for(int warm=0;warm<8;warm++)yield return null;
                    yield return CaptureExample(()=>
                    {
                        var callbacks=config.GetComponentsInChildren<MonoBehaviour>(true).Where(c=>c!=null&&c.GetType().FullName=="Crest.Examples.CustomPassForCamera").Where(c=>
                        {
                            var ev=(UnityEventBase)c.GetType().GetField("_onExecute",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c);
                            return Enumerable.Range(0,ev.GetPersistentEventCount()).Any(n=>ev.GetPersistentMethodName(n)=="ExecuteFillMask");
                        }).ToArray();CheckExample(callbacks.Length==1,"Missing original fill callback");
                        bool images=config.name=="River";var on=ReadExample(camera,images?"River-on.png":null);Color32[] off;
                        try{callbacks[0].enabled=false;off=ReadExample(camera,images?"River-off.png":null);}finally{callbacks[0].enabled=true;}
                        var restored=ReadExample(camera,images?"River-restored.png":null);
                        row.fillChanged=on.Zip(off,(a,b)=>Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b)>6?1:0).Sum();row.restoration=on.Zip(restored,(a,b)=>a.Equals(b)?0:1).Sum();
                        CheckExample(row.fillChanged>200&&row.restoration==0,"Original fill causal control: "+config.name);
                    });
                    ocean.PopTimeProvider(clock);Time.timeScale=scale;if(animator!=null)animator.enabled=animate;if(_examplesFailure!=null)yield break;
                }
                _examplesReport.rows.Add(row);SaveExamples();Debug.Log("ORIGINAL_EXAMPLES_CASE "+i+" "+config.name+" pixels="+row.nonBackground);
            }
            next.Invoke(controller,null);_examplesReport.nextWrap=controller.transform.GetChild(0).gameObject.activeSelf;previous.Invoke(controller,null);_examplesReport.previousWrap=controller.transform.GetChild(23).gameObject.activeSelf;
            CheckExample(_examplesReport.nextWrap&&_examplesReport.previousWrap,"Original navigation wrap");SaveExamples();_examplesTarget.Release();Destroy(_examplesTarget);
            Debug.Log("ORIGINAL_EXAMPLES_COMPLETED errors="+_errors);Application.Quit(_errors==0?0:1);
        }
    }
}
#endif
