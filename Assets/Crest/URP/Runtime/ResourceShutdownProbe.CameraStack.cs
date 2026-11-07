// Finite compiled transfer of CameraSettingsTests underwater stack controls. MIT.
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
        [Serializable] class StackDescriptor
        {
            public string camera,type,color,depth;public int width,height,samples;public float scale;
        }
        [Serializable] class StackRow
        {
            public int samples,aboveChanged,geometry,geometryFogChanged,backgroundChanged,outsideBaseChanged,outsideFogChanged,restoration;
            public bool beforeTransparents;public string projected;
            public int frame,lodFrame,foamSamples;public float delta,foamPeak,foamError;
            public StackDescriptor[] cameras;
        }
        [Serializable] class StackReport
        {
            public string unity,api,gpu,scope,failure;public int errors;public List<StackRow> rows=new List<StackRow>();
        }
        sealed class StackObserver : ICameraGraphPass
        {
            public RenderPassEvent Stage=>RenderPassEvent.AfterRendering;
            public bool Armed;public readonly List<StackDescriptor> Records=new List<StackDescriptor>();
            public void Record(RenderGraph graph,ContextContainer context)
            {
                if(!Armed)return;var data=context.Get<UniversalCameraData>();var d=data.cameraTargetDescriptor;
                Records.Add(new StackDescriptor{camera=data.camera.name,type=data.renderType.ToString(),width=d.width,height=d.height,samples=d.msaaSamples,scale=data.renderScale,color=d.graphicsFormat.ToString(),depth=d.depthStencilFormat.ToString()});
            }
        }
        sealed class StackClock : ITimeProvider
        {
            public float CurrentTime=>1;public float DeltaTime=>1f/30;public float DeltaTimeDynamics=>1f/30;
        }
        StackReport _stackReport;StackObserver _stackObserver;
        void SaveStack()
        {
            if(_stackReport==null)return;_stackReport.errors=_errors;
            File.WriteAllText(Argument("-crestProbeOutput","CameraStack.json"),JsonUtility.ToJson(_stackReport,true));
        }
        void ConfigureStackCamera(Camera camera,Vector3 position,Quaternion rotation)
        {
            camera.enabled=false;camera.transform.SetPositionAndRotation(position,rotation);
            camera.orthographic=true;camera.orthographicSize=8;camera.aspect=1;camera.nearClipPlane=.1f;camera.farClipPlane=2000;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.7f,.7f,.7f);camera.allowHDR=false;camera.allowMSAA=true;camera.cullingMask=1<<4;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;camera.GetUniversalAdditionalCameraData().SetRenderer(0);
        }
        Color32[] ReadStack(Camera camera,RenderTexture target,bool stack,string image=null)
        {
            var oldTarget=camera.targetTexture;var active=RenderTexture.active;bool invert=GL.invertCulling;
            _stackObserver.Records.Clear();_stackObserver.Armed=true;
            try
            {
                if(stack)RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});
                else RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                CheckExample(camera.targetTexture==oldTarget&&GL.invertCulling==invert,"Stack request altered camera target/culling");
                CheckExample(_stackObserver.Records.Count>0,"Missing actual stack camera records");
                foreach(var d in _stackObserver.Records)CheckExample(d.width==256&&d.height==256&&d.samples==target.antiAliasing&&d.scale==1,"Unexpected actual stack descriptor");
                var resolved=RenderTexture.GetTemporary(256,256,0,RenderTextureFormat.ARGB32);var cpu=new Texture2D(256,256,TextureFormat.RGBA32,false);
                try
                {
                    Graphics.Blit(target,resolved);RenderTexture.active=resolved;cpu.ReadPixels(new Rect(0,0,256,256),0,0);cpu.Apply();
                    if(image!=null)File.WriteAllBytes(Path.Combine(Argument("-crestProbeImages","."),image+".png"),cpu.EncodeToPNG());
                    var pixels=cpu.GetPixels32();CheckExample(!pixels.Any(p=>p.r>245&&p.g<10&&p.b>245),"Stack error-pink");return pixels;
                }
                finally{RenderTexture.active=active;Destroy(cpu);RenderTexture.ReleaseTemporary(resolved);}
            }
            finally{_stackObserver.Armed=false;RenderTexture.active=active;}
        }
        static int StackDifferences(Color32[] a,Color32[] b)=>a.Zip(b,(x,y)=>ModeDistance(x,y)>15?1:0).Sum();
        float[] StackFoam()
        {
            var request=AsyncGPUReadback.Request(OceanRenderer.Instance._lodDataFoam.DataTexture,0,TextureFormat.RFloat);request.WaitForCompletion();
            CheckExample(!request.hasError,"Stack foam readback failed");return request.GetData<float>(0).ToArray();
        }
        IEnumerator CameraStackPlayer()
        {
            Directory.CreateDirectory(Argument("-crestProbeImages","."));
            _stackReport=new StackReport{unity=Application.unityVersion,api=SystemInfo.graphicsDeviceType.ToString(),gpu=SystemInfo.graphicsDeviceName,
                scope="P11.3-C2: four geometry-overlay controls/API, before/after transparents x1x/4x; actual base/overlay descriptors, independent above camera, same-frame positive-delta nonzero native foam guard. Not original Built-in reference/XR/performance/all stacks."};
            yield return SceneManager.LoadSceneAsync(1);
            var ocean=OceanRenderer.Instance;CheckExample(ocean!=null,"Missing stack fixture ocean");
            var previousPipeline=QualitySettings.renderPipeline;var pipeline=Instantiate((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline);
            pipeline.msaaSampleCount=1;pipeline.renderScale=1;QualitySettings.renderPipeline=pipeline;
            foreach(var shape in FindObjectsByType<ShapeWaves>())shape.enabled=false;
            foreach(var ur in FindObjectsByType<UnderwaterRenderer>())ur.enabled=false;
            var surface=new Material(ocean.OceanMaterial);surface.DisableKeyword("_TRANSPARENCY_ON");surface.DisableKeyword("_APPLYNORMALMAPPING_ON");surface.SetFloat("_Transparency",0);ocean.OceanMaterial=surface;
            SceneSet(ocean,"_minScale",8f);SceneSet(ocean,"_maxScale",8f);
            var foam=ScriptableObject.CreateInstance<SimSettingsFoam>();foam._prewarm=false;foam._waveFoamStrength=foam._shorelineFoamStrength=0;foam._foamFadeRate=.8f;ocean._simSettingsFoam=foam;
            var depth=ScriptableObject.CreateInstance<SimSettingsSeaFloorDepth>();depth._allowVaryingWaterLevel=false;ocean._simSettingsSeaFloorDepth=depth;
            var a=Camera.main;ConfigureStackCamera(a,new Vector3(0,-3,0),Quaternion.identity);a.name="Stack base below";ocean.ViewCamera=a;
            var b=new GameObject("Independent above").AddComponent<Camera>();ConfigureStackCamera(b,new Vector3(0,3,0),Quaternion.Euler(90,0,0));
            var overlay=new GameObject("Geometry overlay").AddComponent<Camera>();ConfigureStackCamera(overlay,a.transform.position,a.transform.rotation);overlay.cullingMask=1<<5;overlay.GetUniversalAdditionalCameraData().renderType=CameraRenderType.Overlay;
            var belowFog=a.GetComponent<UnderwaterRenderer>();if(belowFog==null)belowFog=a.gameObject.AddComponent<UnderwaterRenderer>();belowFog._mode=UnderwaterRenderer.Mode.FullScreen;belowFog.EnableShaderAPI=false;belowFog.enabled=true;
            var aboveFog=b.gameObject.AddComponent<UnderwaterRenderer>();aboveFog.EnableShaderAPI=false;
            overlay.gameObject.SetActive(false);var overlayFog=overlay.gameObject.AddComponent<UnderwaterRenderer>();overlayFog._overlay=true;overlayFog._layer=5;overlayFog.EnableShaderAPI=false;overlayFog.enabled=false;overlay.gameObject.SetActive(true);
            var quad=GameObject.CreatePrimitive(PrimitiveType.Plane);quad.name="Overlay opaque geometry control";quad.layer=5;quad.transform.SetPositionAndRotation(new Vector3(0,-3,12),Quaternion.Euler(-90,0,0));quad.transform.localScale=Vector3.one*.4f;
            var white=new Material(Shader.Find("Universal Render Pipeline/Unlit"));white.SetColor("_BaseColor",Color.white);var green=quad.GetComponent<Renderer>();green.sharedMaterial=white;
            var seed=GameObject.CreatePrimitive(PrimitiveType.Plane);seed.name="Nonzero native foam guard seed";seed.transform.localScale=Vector3.one*.8f;
            var source=new Material(Shader.Find("Crest/Inputs/Foam/Override Foam"));source.SetFloat("_FoamValue",.65f);seed.GetComponent<Renderer>().sharedMaterial=source;var input=seed.AddComponent<RegisterFoamInput>();
            for(int i=0;i<8;i++)yield return null;
            Time.timeScale=0;var clock=new StackClock();ocean.PushTimeProvider(clock);
            for(int i=0;i<8;i++)yield return null;input.enabled=false;
            for(int i=0;i<27;i++)yield return null;
            CheckExample(belowFog.IsActive&&!aboveFog.IsActive,"Stack camera height optimizations not active as expected");
            _stackObserver=new StackObserver();CameraGraphPassRegistry.Register(_stackObserver);
            yield return ModeCapture(()=>
            {
                foreach(int samples in new[]{1,4})foreach(bool before in new[]{false,true})
                {
                    pipeline.msaaSampleCount=samples;var desc=new RenderTextureDescriptor(256,256,RenderTextureFormat.ARGB32,24){msaaSamples=samples};
                    CheckExample(SystemInfo.GetRenderTextureSupportedMSAASampleCount(desc)==samples,"Required stack MSAA unsupported; no fallback acceptance");
                    var target=new RenderTexture(desc);target.Create();CheckExample(target.antiAliasing==samples,"Actual stack target MSAA mismatch");
                    try
                    {
                        int frame=OceanRenderer.FrameCount,lodFrame=BuildCommandBufferBase._lastUpdateFrame;var identity=ocean._lodDataFoam.DataTexture;var native=StackFoam();
                        CheckExample(native.All(float.IsFinite)&&native.Max()>.05f&&ocean.DeltaTime>0&&frame==lodFrame,"Stack guard requires current positive-delta nonzero native history");
                        belowFog.EnableShaderAPI=false;ReadStack(a,target,true);aboveFog.enabled=false;var aboveControl=ReadStack(b,target,false);aboveFog.enabled=true;
                        belowFog.EnableShaderAPI=overlayFog.EnableShaderAPI=before;
                        var baseOnly=ReadStack(a,target,true);var aboveAfter=ReadStack(b,target,false);
                        overlay.enabled=true;a.GetUniversalAdditionalCameraData().cameraStack.Add(overlay);overlayFog.enabled=false;
                        var without=ReadStack(a,target,true);overlayFog.enabled=true;
                        var with=ReadStack(a,target,true,samples==4&&before?"Overlay-4x-before":null);
                        CheckExample(_stackObserver.Records.Count==2&&_stackObserver.Records[0].camera==a.name&&_stackObserver.Records[0].type=="Base"&&_stackObserver.Records[1].camera==overlay.name&&_stackObserver.Records[1].type=="Overlay","Actual camera stack order/type");
                        var row=new StackRow{samples=samples,beforeTransparents=before,aboveChanged=StackDifferences(aboveControl,aboveAfter),cameras=_stackObserver.Records.ToArray(),frame=frame,lodFrame=lodFrame,delta=ocean.DeltaTime,foamPeak=native.Max(),foamSamples=native.Length};
                        var min=overlay.WorldToViewportPoint(green.bounds.min);var max=overlay.WorldToViewportPoint(green.bounds.max);
                        var projected=Rect.MinMaxRect(Mathf.Min(min.x,max.x)*256-2,Mathf.Min(min.y,max.y)*256-2,Mathf.Max(min.x,max.x)*256+2,Mathf.Max(min.y,max.y)*256+2);row.projected=projected.ToString("R");
                        for(int i=0;i<with.Length;i++)
                        {
                            bool foreground=ModeDistance(baseOnly[i],without[i])>15,changed=ModeDistance(without[i],with[i])>15;
                            if(foreground){row.geometry++;if(changed)row.geometryFogChanged++;}else if(changed)row.backgroundChanged++;
                            if(!projected.Contains(new Vector2(i%256+.5f,i/256+.5f))){if(foreground)row.outsideBaseChanged++;if(changed)row.outsideFogChanged++;}
                        }
                        overlayFog.enabled=false;overlay.enabled=false;a.GetUniversalAdditionalCameraData().cameraStack.Clear();
                        row.restoration=StackDifferences(baseOnly,ReadStack(a,target,true));var after=StackFoam();
                        row.foamError=native.Zip(after,(x,y)=>Mathf.Abs(x-y)).Max();_stackReport.rows.Add(row);SaveStack();
                        CheckExample(row.aboveChanged==0&&row.geometry>100&&row.geometry<256*256/2&&row.geometryFogChanged>100&&row.backgroundChanged<20&&row.outsideBaseChanged<20&&row.outsideFogChanged<20&&row.restoration==0,"Stack geometry/exclusion/restoration control");
                        CheckExample(OceanRenderer.FrameCount==frame&&BuildCommandBufferBase._lastUpdateFrame==lodFrame&&ocean._lodDataFoam.DataTexture==identity&&after.Length==native.Length&&after.All(float.IsFinite)&&row.foamError==0,"Camera requests advanced native simulation");
                    }
                    finally{if(RenderTexture.active==target)RenderTexture.active=null;target.Release();Destroy(target);}
                }
            });
            CameraGraphPassRegistry.Unregister(_stackObserver);ocean.PopTimeProvider(clock);QualitySettings.renderPipeline=previousPipeline;
            Destroy(pipeline);Destroy(b.gameObject);Destroy(overlay.gameObject);Destroy(quad);Destroy(seed);Destroy(surface);Destroy(white);Destroy(source);Destroy(foam);Destroy(depth);
            SaveStack();Debug.Log("CAMERA_STACK_COMPLETED errors="+_errors);Application.Quit(_errors==0?0:1);
        }
    }
}
#endif
