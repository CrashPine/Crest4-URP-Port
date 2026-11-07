// Finite compiled transfer of existing underwater volume controls. MIT.
#if CREST_RESOURCE_PROBE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Crest.URP.Diagnostics
{
    public sealed partial class ResourceShutdownProbe
    {
        [Serializable] class ModeRow
        {
            public string mode,quality,camera,colorFormat,depthFormat,targetDepth;
            public int width,height,samples,changed,borderChanged,tinted,restoration,frame,lodFrame;
            public float renderScale;public long effectExecutions;
        }
        [Serializable] class ModeReport
        {
            public string unity,api,gpu,scope,failure,qualityStatus;public string[] availableQualities;public int errors;public bool qualityUsesSamePipeline,qualityRestored;
            public int qualityChanged=-1,qualityRestoration=-1;public List<ModeRow> rows=new List<ModeRow>();
        }
        sealed class ModeObserver : ICameraGraphPass
        {
            public RenderPassEvent Stage=>RenderPassEvent.AfterRendering;
            public Camera Target;public bool Armed;public int Count;public RenderTextureDescriptor Descriptor;public float Scale;
            public void Record(RenderGraph graph,ContextContainer context)
            {
                var data=context.Get<UniversalCameraData>();if(!Armed||data.camera!=Target)return;
                Count++;Descriptor=data.cameraTargetDescriptor;Scale=data.renderScale;
            }
        }
        ModeReport _modeReport;ModeObserver _modeObserver;RenderTexture _modeTarget;
        void SaveModes()
        {
            if(_modeReport==null)return;_modeReport.errors=_errors;
            File.WriteAllText(Argument("-crestProbeOutput","RenderModes.json"),JsonUtility.ToJson(_modeReport,true));
        }
        IEnumerator ModeCapture(Action capture)
        {
            _examplesFailure=null;_examplesCapture=capture;float end=Time.realtimeSinceStartup+20;
            while(_examplesCapture!=null&&Time.realtimeSinceStartup<end)yield return null;
            CheckExample(_examplesCapture==null&&_examplesFailure==null,"Mode capture: "+_examplesFailure);
        }
        Color32[] ReadMode(Camera camera,string image=null)
        {
            CheckExample(BuildCommandBufferBase._lastUpdateFrame==OceanRenderer.FrameCount,"Mode capture before ocean update");
            _modeObserver.Armed=true;_modeObserver.Count=0;
            try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=_modeTarget});}
            finally{_modeObserver.Armed=false;}
            CheckExample(_modeObserver.Count==1,"Actual camera frame-data observer not invoked once");
            var desc=_modeObserver.Descriptor;
            CheckExample(desc.width==320&&desc.height==180&&desc.msaaSamples==1&&_modeObserver.Scale==1,"Unexpected actual URP descriptor");
            var old=RenderTexture.active;var cpu=new Texture2D(320,180,TextureFormat.RGBA32,false);
            try
            {
                RenderTexture.active=_modeTarget;cpu.ReadPixels(new Rect(0,0,320,180),0,0);cpu.Apply();var pixels=cpu.GetPixels32();
                if(image!=null)File.WriteAllBytes(Path.Combine(Argument("-crestProbeImages","."),image+".png"),cpu.EncodeToPNG());
                CheckExample(!pixels.Any(p=>p.r>245&&p.g<10&&p.b>245),"Mode error-pink");return pixels;
            }
            finally{RenderTexture.active=old;Destroy(cpu);}
        }
        static int ModeDistance(Color32 a,Color32 b)=>Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b);
        IEnumerator RenderModesPlayer()
        {
            Directory.CreateDirectory(Argument("-crestProbeImages","."));
            _modeReport=new ModeReport{unity=Application.unityVersion,api=SystemInfo.graphicsDeviceType.ToString(),gpu=SystemInfo.graphicsDeviceName,
                scope="P11.3-C1 only: existing volume controls in compiled Player,320x180/1x/Forward;5 mode/view states plus Windows quality inventory (Mobile excluded by source platform settings). Not overlay/MSAA/reflection/XR or complete original-renderer parity."};
            yield return SceneManager.LoadSceneAsync(1);
            var camera=Camera.main;var ocean=OceanRenderer.Instance;CheckExample(camera!=null&&ocean!=null,"Missing fixture ocean/camera");
            camera.transform.SetPositionAndRotation(new Vector3(0,-50,-15),Quaternion.identity);camera.aspect=16f/9;camera.allowMSAA=false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var underwater=camera.GetComponent<UnderwaterRenderer>();CheckExample(underwater!=null,"Missing original underwater component");underwater.enabled=false;
            var volume=GameObject.CreatePrimitive(PrimitiveType.Cube);volume.name="Compiled volume control";volume.transform.position=new Vector3(0,-50,0);volume.transform.localScale=Vector3.one*8;volume.GetComponent<Renderer>().enabled=false;
            underwater._volumeGeometry=volume.GetComponent<MeshFilter>();underwater.EnableShaderAPI=false;
            var backdrop=GameObject.CreatePrimitive(PrimitiveType.Quad);backdrop.name="Opaque depth/backdrop control";backdrop.transform.position=new Vector3(0,-50,15);backdrop.transform.localScale=Vector3.one*100;
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.SetColor("_BaseColor",new Color(.7f,.7f,.7f));backdrop.GetComponent<Renderer>().sharedMaterial=material;
            for(int i=0;i<10;i++)yield return null;
            Time.timeScale=0;var clock=new ExampleFixedTime();ocean.PushTimeProvider(clock);ocean.ViewCamera=camera;
            _modeTarget=new RenderTexture(320,180,24,RenderTextureFormat.ARGB32);_modeTarget.Create();
            CheckExample(_modeTarget.depthStencilFormat.ToString().Contains("S8"),"Actual target must carry stencil");
            _modeObserver=new ModeObserver{Target=camera};CameraGraphPassRegistry.Register(_modeObserver);
            var modes=new[]{UnderwaterRenderer.Mode.FullScreen,UnderwaterRenderer.Mode.Portal,UnderwaterRenderer.Mode.Volume,UnderwaterRenderer.Mode.VolumeFlyThrough,UnderwaterRenderer.Mode.VolumeFlyThrough};
            for(int state=0;state<modes.Length;state++)
            {
                bool inside=state==4;string label=modes[state]+(inside?"-inside":"-outside");underwater.enabled=false;underwater._mode=modes[state];
                camera.transform.position=inside?new Vector3(0,-50,0):new Vector3(0,-50,-15);for(int i=0;i<2;i++)yield return null;
                Color32[] off=null;yield return ModeCapture(()=>off=ReadMode(camera,state==1?label+"-off":null));
                underwater.enabled=true;yield return null;
                yield return ModeCapture(()=>
                {
                    long before=UnderwaterRenderer.DiagnosticCompositeExecutions;var on=ReadMode(camera,state==1||inside?label+"-on":null);
                    long calls=UnderwaterRenderer.DiagnosticCompositeExecutions-before;CheckExample(calls>0,"Effect GPU graph pass did not execute");
                    var desc=_modeObserver.Descriptor;
                    underwater.enabled=false;var restored=ReadMode(camera,state==1?label+"-restored":null);
                    int changed=0,border=0,tinted=0;
                    for(int i=0;i<on.Length;i++)
                    {
                        if(ModeDistance(off[i],on[i])>15){changed++;int x=i%320,y=i/320;if(x<10||x>=310||y<10||y>=170)border++;}
                        var p=on[i];if(Math.Max(p.r,Math.Max(p.g,p.b))-Math.Min(p.r,Math.Min(p.g,p.b))>15)tinted++;
                    }
                    var row=new ModeRow{mode=label,quality=QualitySettings.names[QualitySettings.GetQualityLevel()],camera=camera.transform.position.ToString("R"),
                        width=desc.width,height=desc.height,samples=desc.msaaSamples,colorFormat=desc.graphicsFormat.ToString(),depthFormat=desc.depthStencilFormat.ToString(),targetDepth=_modeTarget.depthStencilFormat.ToString(),renderScale=_modeObserver.Scale,
                        changed=changed,borderChanged=border,tinted=tinted,restoration=off.Zip(restored,(a,b)=>a.Equals(b)?0:1).Sum(),effectExecutions=calls,frame=OceanRenderer.FrameCount,lodFrame=BuildCommandBufferBase._lastUpdateFrame};
                    _modeReport.rows.Add(row);SaveModes();
                    CheckExample(changed>500&&row.restoration==0,"Underwater positive/restoration control");
                    if(state>0&&!inside)CheckExample(border<100,"Volume fog escaped projected geometry");
                    if(inside||state==0)CheckExample(tinted>1000,"Fullscreen/inside fog must remain visible");
                });
            }
            camera.transform.position=new Vector3(0,-50,-15);underwater._mode=UnderwaterRenderer.Mode.FullScreen;underwater.enabled=true;yield return null;
            _modeReport.availableQualities=QualitySettings.names;
            int mobileIndex=Array.IndexOf(QualitySettings.names,"Mobile");
            if(mobileIndex<0)
            {
                CheckExample(QualitySettings.names.Length==1&&QualitySettings.names[0]=="PC","Unexpected Windows quality inventory");
                _modeReport.qualityStatus="NOT APPLICABLE: Mobile excluded from Standalone; runtime PC only. No quality-switch acceptance.";
            }
            else
            {
                int previous=QualitySettings.GetQualityLevel();var pipeline=GraphicsSettings.currentRenderPipeline;Color32[] pc=null,mobile=null,restoredPc=null;
                yield return ModeCapture(()=>pc=ReadMode(camera));QualitySettings.SetQualityLevel(mobileIndex,true);yield return null;
                _modeReport.qualityUsesSamePipeline=GraphicsSettings.currentRenderPipeline==pipeline;CheckExample(_modeReport.qualityUsesSamePipeline,"Quality changed pipeline unexpectedly");
                yield return ModeCapture(()=>mobile=ReadMode(camera));QualitySettings.SetQualityLevel(previous,true);yield return null;
                yield return ModeCapture(()=>restoredPc=ReadMode(camera));
                _modeReport.qualityChanged=pc.Zip(mobile,(a,b)=>a.Equals(b)?0:1).Sum();_modeReport.qualityRestoration=pc.Zip(restoredPc,(a,b)=>a.Equals(b)?0:1).Sum();_modeReport.qualityRestored=QualitySettings.GetQualityLevel()==previous;
                CheckExample(_modeReport.qualityRestoration==0&&_modeReport.qualityRestored,"Quality restoration");_modeReport.qualityStatus="Same-pipeline roundtrip checked";
            }
            SaveModes();
            CameraGraphPassRegistry.Unregister(_modeObserver);ocean.PopTimeProvider(clock);_modeTarget.Release();Destroy(_modeTarget);Destroy(material);Destroy(volume);Destroy(backdrop);
            Debug.Log("RENDER_MODES_COMPLETED errors="+_errors);Application.Quit(_errors==0?0:1);
        }
    }
}
#endif
