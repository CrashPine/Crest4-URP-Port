// Crest 4 URP port. MIT; see LICENSE.Crest.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Crest;
using Crest.Examples;
using Crest.URP;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public partial class MaskedExampleTests
{
    const int W=320,H=240;
    readonly List<Object> _owned=new List<Object>();
    Camera _camera;Material _material;GameObject _quad;RenderTexture _target;
    Publication _publication;Snapshot _snapshot;string _report;
    readonly List<RTHandle> _handles=new List<RTHandle>();
    T Own<T>(T x) where T:Object {_owned.Add(x);return x;}
    sealed class Publication : ICameraGraphPass
    {
        public readonly Dictionary<int,RTHandle> Textures=new Dictionary<int,RTHandle>();
        public RenderPassEvent Stage=>(RenderPassEvent)((int)RenderPassEvent.BeforeRenderingPrePasses-1);
        sealed class Data {}
        public void Record(RenderGraph graph,ContextContainer frame)
        {
            using var b=graph.AddUnsafePass<Data>("Test known mask publication",out var data);
            foreach(var item in Textures){var h=graph.ImportTexture(item.Value);b.UseTexture(h,AccessFlags.Read);b.SetGlobalTextureAfterPass(h,item.Key);}
            b.AllowPassCulling(false);b.SetRenderFunc((Data p,UnsafeGraphContext c)=>{});
        }
    }
    sealed class Snapshot : ICameraGraphPass
    {
        public RTHandle Destination;public Material Probe;public RenderPassEvent Stage=>RenderPassEvent.BeforeRenderingPrePasses;
        sealed class Data {public TextureHandle Destination;public Material Probe;}
        public void Record(RenderGraph graph,ContextContainer frame)
        {
            using var b=graph.AddUnsafePass<Data>("Test native mask before opaque",out var data);
            data.Destination=graph.ImportTexture(Destination);data.Probe=Probe;b.UseTexture(data.Destination,AccessFlags.Write);b.UseAllGlobalTextures(true);b.AllowGlobalStateModification(true);b.AllowPassCulling(false);
            b.SetRenderFunc((Data p,UnsafeGraphContext c)=>{var cmd=CommandBufferHelpers.GetNativeCommandBuffer(c.cmd);cmd.SetRenderTarget(p.Destination);cmd.DrawProcedural(Matrix4x4.identity,p.Probe,0,MeshTopology.Triangles,3);});
        }
    }
    sealed class PassOutput : ICameraGraphPass
    {
        public RTHandle Colour,Depth;public Material Material;public Renderer Renderer;public int Pass;
        public RenderPassEvent Stage=>RenderPassEvent.AfterRenderingOpaques;
        sealed class Data {public TextureHandle Colour,Depth;public PassOutput Owner;public Matrix4x4 View,Projection;public int Width,Height;}
        public void Record(RenderGraph graph,ContextContainer frame)
        {
            var camera=frame.Get<UniversalCameraData>();
            using var b=graph.AddUnsafePass<Data>("Test masked depth/normal output",out var data);
            data.Owner=this;data.Colour=graph.ImportTexture(Colour);data.Depth=graph.ImportTexture(Depth);
            data.View=camera.GetViewMatrix();data.Projection=camera.GetProjectionMatrix();data.Width=Colour.rt.width;data.Height=Colour.rt.height;
            b.UseTexture(data.Colour,AccessFlags.Write);b.UseTexture(data.Depth,AccessFlags.Write);b.UseAllGlobalTextures(true);b.AllowGlobalStateModification(true);b.AllowPassCulling(false);
            b.SetRenderFunc((Data p,UnsafeGraphContext c)=>
            {
                var cmd=CommandBufferHelpers.GetNativeCommandBuffer(c.cmd);cmd.SetRenderTarget(p.Colour,p.Depth);
                // CommandBuffer depth clear uses canonical far=1; Unity handles reversed Z.
                cmd.SetViewport(new Rect(0,0,p.Width,p.Height));cmd.ClearRenderTarget(true,true,Color.clear,1);
                cmd.SetViewProjectionMatrices(p.View,p.Projection);cmd.DrawRenderer(p.Owner.Renderer,p.Owner.Material,0,p.Owner.Pass);
            });
        }
    }
    void Setup(string reportPrefix="MaskedExamples")
    {
        _report=$"Evidence/{reportPrefix}-{TestContext.CurrentContext.Test.Name}-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report,"Fixture start: "+TestContext.CurrentContext.Test.Name+"; Playing="+Application.isPlaying+"\n");
        foreach(var camera in Object.FindObjectsByType<Camera>())camera.enabled=false;
        _camera=Own(new GameObject("Masked example camera",typeof(Camera))).GetComponent<Camera>();_camera.tag="MainCamera";_camera.enabled=false;
        _camera.orthographic=true;_camera.orthographicSize=3;_camera.aspect=4f/3;_camera.nearClipPlane=.1f;_camera.farClipPlane=100;
        _camera.cullingMask=1<<5;_camera.clearFlags=CameraClearFlags.SolidColor;_camera.backgroundColor=new Color(.02f,.03f,.04f);_camera.allowHDR=_camera.allowMSAA=false;
        _camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;_camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.black;RenderSettings.ambientProbe=new SphericalHarmonicsL2();RenderSettings.reflectionIntensity=0;RenderSettings.skybox=null;
        foreach(var light in Object.FindObjectsByType<Light>())light.intensity=0;
        _quad=Own(GameObject.CreatePrimitive(PrimitiveType.Quad));_quad.layer=5;_quad.transform.position=new Vector3(0,0,10);_quad.transform.localScale=new Vector3(8,6,1);
        _material=Own(new Material(Shader.Find("Crest/Examples/Masked")));_material.SetColor("_Color",new Color(.8f,.02f,.01f,1));_material.SetColor("_EmissionColor",Color.white);_material.EnableKeyword("_EMISSION_ON");_material.SetFloat("_Emission",1);_material.SetTexture("_Albedo",Texture2D.whiteTexture);_material.SetFloat("_Cutoff",.5f);
        _quad.GetComponent<Renderer>().sharedMaterial=_material;
        _target=Own(new RenderTexture(W,H,24,RenderTextureFormat.ARGB32));_target.Create();
    }
    void Mode(string mode){foreach(var key in new[]{"_MASK_NONE","_MASK_MASKED","_MASK_FILL"})_material.DisableKeyword(key);_material.EnableKeyword(mode);_material.SetFloat("_Mask",mode=="_MASK_NONE"?0:mode=="_MASK_MASKED"?1:2);}
    RTHandle Field(string name,Func<int,int,float> value)
    {
        var rt=Own(new RenderTexture(W,H,0,RenderTextureFormat.RFloat,RenderTextureReadWrite.Linear));rt.Create();
        var cpu=Own(new Texture2D(W,H,TextureFormat.RFloat,false,true));var values=new float[W*H];for(int y=0;y<H;y++)for(int x=0;x<W;x++)values[y*W+x]=value(x,y);
        cpu.SetPixelData(values,0);cpu.Apply();Graphics.Blit(cpu,rt);var handle=RTHandles.Alloc(rt);_handles.Add(handle);_publication.Textures[Shader.PropertyToID(name)]=handle;return handle;
    }
    float RawDepth(float distance)
    {
        var p=GL.GetGPUProjectionMatrix(_camera.projectionMatrix,true)*new Vector4(0,0,-distance,1);return p.z/p.w;
    }
    Color32[] Capture(string name,string prefix="MaskedExamples")
    {
        bool enabled=_camera.enabled;_camera.enabled=true;File.AppendAllText(_report,$"Request={name}; MainCameraMatches={Camera.main==_camera}; PixelRect={_camera.pixelRect}\n");
        try {RenderPipeline.SubmitRenderRequest(_camera,new UniversalRenderPipeline.SingleCameraRequest{destination=_target});}
        finally {_camera.enabled=enabled;}
        var previous=RenderTexture.active;var cpu=new Texture2D(_target.width,_target.height,TextureFormat.RGBA32,false);
        try {RenderTexture.active=_target;cpu.ReadPixels(new Rect(0,0,_target.width,_target.height),0,0);cpu.Apply();File.WriteAllBytes($"Evidence/{prefix}-{name}-{SystemInfo.graphicsDeviceType}.png",cpu.EncodeToPNG());var pixels=cpu.GetPixels32();
            File.AppendAllText(_report,$"Capture={name}; Centre={pixels[pixels.Length/2+_target.width/2]}; Keywords={string.Join(",",_material.shaderKeywords)}; MaskValue={(_material.HasProperty("_Mask")?_material.GetFloat("_Mask"):float.NaN)}; EmissionValue={(_material.HasProperty("_Emission")?_material.GetFloat("_Emission"):float.NaN)}; Pipeline={RenderPipelineManager.currentPipeline?.GetType().Name}\n");return pixels;}
        finally {RenderTexture.active=previous;Object.DestroyImmediate(cpu);}
    }
    static bool Red(Color32 c)=>c.r>100&&c.r>c.g*3&&c.r>c.b*3;
    float[] Read(RenderTexture rt)
    {
        var previous=RenderTexture.active;var cpu=new Texture2D(rt.width,rt.height,TextureFormat.RFloat,false,true);
        try {RenderTexture.active=rt;cpu.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);cpu.Apply();return cpu.GetPixelData<float>(0).ToArray();}
        finally {RenderTexture.active=previous;Object.DestroyImmediate(cpu);}
    }
    void Check(string label,Color32[] pixels,Func<int,int,bool> expected,bool knownSplits=false)
    {
        int wrong=0,visible=0;for(int y=2;y<_target.height-2;y++)for(int x=2;x<_target.width-2;x++)
        {if(knownSplits&&(Math.Abs(x-W/4)<=1||Math.Abs(x-W/3)<=1||Math.Abs(x-W/2)<=1))continue;bool actual=Red(pixels[y*_target.width+x]);if(actual)visible++;if(actual!=expected(x,y))wrong++;}
        File.AppendAllText(_report,$"Phase={label}; Visible={visible}; InteriorClassificationErrors={wrong}\n");Assert.That(wrong,Is.Zero,label);
    }
    [UnityTest,Timeout(180000)]
    public IEnumerator ColourDepthNormalsAlphaAndEmissionUseDeclaredMasks()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();Setup();_camera.enabled=true;for(int warm=0;warm<4;warm++)yield return null;_camera.enabled=false;
        _publication=new Publication();CameraGraphPassRegistry.Register(_publication);
        Field("_CrestOceanMaskTexture",(x,y)=>x<W/2?1:0);Field("_CrestWaterVolumeFrontFaceTexture",(x,y)=>0);Field("_CrestWaterVolumeBackFaceTexture",(x,y)=>0);Field("_FillTexture",(x,y)=>x>=W/2?1:0);
        Mode("_MASK_NONE");Check("None",Capture("None"),(x,y)=>true);
        Mode("_MASK_MASKED");Check("MaskedLeft",Capture("MaskedLeft"),(x,y)=>x<W/2,true);
        Mode("_MASK_FILL");Check("FillRight",Capture("FillRight"),(x,y)=>x>=W/2,true);
        Mode("_MASK_MASKED");
        Field("_CrestWaterVolumeFrontFaceTexture",(x,y)=>x<W/4?RawDepth(20):0);
        Field("_CrestWaterVolumeBackFaceTexture",(x,y)=>x>=W/4&&x<W/3?RawDepth(5):0);
        Check("VolumeDepth",Capture("VolumeDepth"),(x,y)=>x>=W/3&&x<W/2,true);
        // A different mask is published without yielding a frame: no previous-camera/previous-frame result is accepted.
        Field("_CrestOceanMaskTexture",(x,y)=>1);Field("_CrestWaterVolumeFrontFaceTexture",(x,y)=>0);Field("_CrestWaterVolumeBackFaceTexture",(x,y)=>0);
        Check("CurrentMask",Capture("CurrentMask"),(x,y)=>true);
        _material.SetFloat("_Emission",0);_material.DisableKeyword("_EMISSION_ON");Check("EmissionOff",Capture("EmissionOff"),(x,y)=>false);_material.SetFloat("_Emission",1);_material.EnableKeyword("_EMISSION_ON");
        _material.SetColor("_Color",new Color(.8f,.02f,.01f,.25f));Check("AlphaCutoff",Capture("AlphaCutoff"),(x,y)=>false);_material.SetColor("_Color",new Color(.8f,.02f,.01f,1));
        foreach(string pass in new[]{"ForwardLit","DepthNormals","DepthOnly"})
        {
            Field("_CrestOceanMaskTexture",(x,y)=>x<W/2?1:0);Capture("Before"+pass);
            var rt=Own(new RenderTexture(W,H,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear));rt.Create();
            var depth=Own(new RenderTexture(W,H,24,RenderTextureFormat.Depth));depth.Create();
            var colourHandle=RTHandles.Alloc(rt);var depthHandle=RTHandles.Alloc(depth);_handles.Add(colourHandle);_handles.Add(depthHandle);
            ShaderUtil.CompilePass(_material,_material.FindPass(pass),true);
            var output=new PassOutput{Colour=colourHandle,Depth=depthHandle,Material=_material,Renderer=_quad.GetComponent<Renderer>(),Pass=_material.FindPass(pass)};
            CameraGraphPassRegistry.Register(output);
            try {Capture("PassOutput"+pass);}finally {CameraGraphPassRegistry.Unregister(output);}
            var previous=RenderTexture.active;var cpu=new Texture2D(W,H,TextureFormat.RGBAFloat,false,true);int visible=0,wrong=0;
            try
            {
                RenderTexture.active=rt;cpu.ReadPixels(new Rect(0,0,W,H),0,0);cpu.Apply();var values=cpu.GetPixels();File.AppendAllText(_report,$"PassDiagnostic={pass}; Index={_material.FindPass(pass)}; Left={values[H/2*W+W/4]}; Right={values[H/2*W+3*W/4]}; MaxRGB={values.Max(c=>Math.Abs(c.r)+Math.Abs(c.g)+Math.Abs(c.b))}; Colour={_material.GetColor("_Color")}\n");
                for(int y=2;y<H-2;y++)for(int x=2;x<W-2;x++){if(Math.Abs(x-W/2)<=1)continue;var c=values[y*W+x];bool actual=Math.Abs(c.r)+Math.Abs(c.g)+Math.Abs(c.b)>.001f;if(actual)visible++;if(actual!=(x<W/2))wrong++;}
            }
            finally {RenderTexture.active=previous;Object.DestroyImmediate(cpu);}
            File.AppendAllText(_report,$"Pass={pass}; Visible={visible}; MaskClassificationErrors={wrong}\n");Assert.That(visible,Is.GreaterThan(1000));Assert.That(wrong,Is.Zero);
        }
        Assert.That(ShaderUtil.GetShaderMessages(_material.shader).Where(x=>x.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error),Is.Empty);
    }
    void NativeMaskAgreement(string label,Color32[] colour,float[] mask,Func<float,bool> predicate)
    {
        int wrong=0,interior=0,boundary=0;int w=_target.width,h=_target.height;
        for(int y=2;y<h-2;y++)for(int x=2;x<w-2;x++)
        {
            int i=y*w+x;bool expected=predicate(mask[i]);bool edge=false;
            for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)if(predicate(mask[(y+dy)*w+x+dx])!=expected)edge=true;
            if(edge){boundary++;continue;}interior++;if(Red(colour[i])!=expected)wrong++;
        }
        File.AppendAllText(_report,$"Native={label}; Interior={interior}; PredefinedOnePixelBoundary={boundary}; ClassificationErrors={wrong}\n");Assert.That(interior,Is.GreaterThan(1000));Assert.That(wrong,Is.Zero);
    }
    [UnityTest,Timeout(180000)]
    public IEnumerator LegacyFillCallbackRunsInGraphAndClearsOnDisableResizeAndEmptyList()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();Setup();_camera.enabled=true;for(int warm=0;warm<4;warm++)yield return null;_camera.enabled=false;Mode("_MASK_FILL");
        _publication=new Publication();CameraGraphPassRegistry.Register(_publication);Field("_CrestWaterVolumeFrontFaceTexture",(x,y)=>RawDepth(5));
        var proxy=Own(GameObject.CreatePrimitive(PrimitiveType.Cube));proxy.transform.position=new Vector3(0,0,5);proxy.transform.localScale=Vector3.one*4;proxy.GetComponent<Renderer>().forceRenderingOff=true;
        var owner=Own(new GameObject("Original fill callbacks"));var fill=owner.AddComponent<MaskFill>();var meshes=new SerializedObject(fill).FindProperty("_meshes");meshes.arraySize=1;meshes.GetArrayElementAtIndex(0).objectReferenceValue=proxy.GetComponent<MeshFilter>();meshes.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        var callback=owner.AddComponent<CustomPassForCamera>();var so=new SerializedObject(callback);so.FindProperty("_event").intValue=(int)CameraEvent.BeforeForwardOpaque;so.ApplyModifiedPropertiesWithoutUndo();
        var field=typeof(CustomPassForCamera).GetField("_onExecute",BindingFlags.Instance|BindingFlags.NonPublic);var execute=(UnityEvent<CommandBuffer,Camera,RenderTextureDescriptor>)field.GetValue(callback);UnityEventTools.AddPersistentListener(execute,fill.ExecuteFillMask);
        var clear=(UnityEvent<CommandBuffer,Camera>)typeof(CustomPassForCamera).GetField("_onClear",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(callback);UnityEventTools.AddPersistentListener(clear,fill.Clear);
        RenderTexture Texture()=> (RenderTexture)typeof(MaskFill).GetField("_texture",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(fill);
        var a=Capture("NativeFill");var native=Read(Texture());File.AppendAllText(_report,$"FillNativeNonzero={native.Count(x=>x!=0)}; Max={native.Max()}\n");Assert.That(a.Count(Red),Is.GreaterThan(1000));NativeMaskAgreement("Fill",a,native,x=>x!=0);
        proxy.transform.position+=Vector3.right*2;var moved=Capture("MovedFill");NativeMaskAgreement("MovedFill",moved,Read(Texture()),x=>x!=0);Assert.That(a.Zip(moved,(x,y)=>x.Equals(y)?0:1).Sum(),Is.GreaterThan(1000));
        callback.enabled=false;Check("CallbackDisabled",Capture("CallbackDisabled"),(x,y)=>false);callback.enabled=true;proxy.transform.position-=Vector3.right*2;
        var restored=Capture("FillRestored");Assert.That(a,Is.EqualTo(restored));
        _target.Release();_target.width=192;_target.height=128;_target.Create();_camera.aspect=1.5f;var resized=Capture("FillResized");Assert.That(Texture().width,Is.EqualTo(192));Assert.That(Texture().height,Is.EqualTo(128));NativeMaskAgreement("Resized",resized,Read(Texture()),x=>x!=0);
        meshes=new SerializedObject(fill).FindProperty("_meshes");meshes.ClearArray();meshes.serializedObject.ApplyModifiedPropertiesWithoutUndo();Check("EmptyFill",Capture("EmptyFill"),(x,y)=>false);
        // Camera.GetCommandBuffers is unsupported under SRP and cannot establish this contract at runtime.
    }
    [UnityTest,Timeout(180000)]
    public IEnumerator OpaqueMaskedGeometryReadsActualOceanMaskBeforeDepthAndColour()
    {
        OceanRenderer.EnableGraphicsInBatchMode=true;EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");yield return new EnterPlayMode();
        foreach(var wave in Object.FindObjectsByType<ShapeWaves>())wave.enabled=false;
        Setup();var ocean=OceanRenderer.Instance;ocean.ViewCamera=_camera;_camera.cullingMask|=1<<4;_camera.transform.position=new Vector3(0,-1,-8);
        foreach(var renderer in ocean.Root.GetComponentsInChildren<Renderer>())renderer.forceRenderingOff=true;
        var surface=Own(new Material(ocean.OceanMaterial));surface.SetVector("_DepthFogDensity",Vector4.zero);foreach(var key in new[]{"_CAUSTICS_ON","_SHADOWS_ON","_FOAM_ON","_SUBSURFACESCATTERING_ON"})surface.DisableKeyword(key);ocean.OceanMaterial=surface;
        var config=new SerializedObject(ocean);config.FindProperty("_createFoamSim").boolValue=false;config.FindProperty("_createClipSurfaceData").boolValue=false;config.FindProperty("_createShadowData").boolValue=false;config.ApplyModifiedPropertiesWithoutUndo();
        var volume=Own(GameObject.CreatePrimitive(PrimitiveType.Cube));volume.transform.position=new Vector3(0,-1,0);volume.transform.localScale=new Vector3(4,4,6);volume.GetComponent<Renderer>().enabled=false;
        var fog=_camera.gameObject.AddComponent<UnderwaterRenderer>();fog._mode=UnderwaterRenderer.Mode.Volume;fog._volumeGeometry=volume.GetComponent<MeshFilter>();fog.EnableShaderAPI=true;var fogSettings=new SerializedObject(fog);fogSettings.FindProperty("_meniscus").boolValue=false;fogSettings.ApplyModifiedPropertiesWithoutUndo();
        Mode("_MASK_MASKED");for(int i=0;i<16;i++)yield return null;
        var rt=Own(new RenderTexture(W,H,0,RenderTextureFormat.RFloat,RenderTextureReadWrite.Linear));rt.Create();var handle=RTHandles.Alloc(rt);_handles.Add(handle);_snapshot=new Snapshot{Destination=handle,Probe=Own(new Material(Shader.Find("Hidden/Crest/Tests/MaskTimingProbe")))};CameraGraphPassRegistry.Register(_snapshot);
        void Follow(){_quad.transform.SetPositionAndRotation(_camera.transform.position+_camera.transform.forward*10,_camera.transform.rotation);}
        Follow();var a=Capture("ActualMask");var first=Read(rt);Assert.That(a.Count(Red),Is.GreaterThan(1000));NativeMaskAgreement("Actual",a,first,x=>x!=0);
        volume.transform.position+=Vector3.right*2;Follow();var moved=Capture("ActualMaskTurned");NativeMaskAgreement("ActualTurned",moved,Read(rt),x=>x!=0);Assert.That(a.Zip(moved,(x,y)=>x.Equals(y)?0:1).Sum(),Is.GreaterThan(1000));
        volume.transform.position-=Vector3.right*2;Follow();var restored=Capture("ActualMaskRestored");Assert.That(a,Is.EqualTo(restored));
    }
    [UnityTest,Timeout(180000)]
    public IEnumerator MaskedShadowCasterIgnoresViewMaskButPreservesAlphaCutoff()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();Setup();
        var receiver=Own(new Material(Shader.Find("Universal Render Pipeline/Lit")));receiver.SetColor("_BaseColor",Color.white);receiver.SetFloat("_Smoothness",0);
        _quad.GetComponent<Renderer>().sharedMaterial=receiver;_quad.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        var caster=Own(GameObject.CreatePrimitive(PrimitiveType.Cube));caster.layer=5;caster.transform.position=new Vector3(-1,0,6);caster.transform.localScale=Vector3.one*1.5f;
        var renderer=caster.GetComponent<Renderer>();renderer.sharedMaterial=_material;renderer.receiveShadows=false;
        var light=Own(new GameObject("Masked shadow light",typeof(Light))).GetComponent<Light>();light.type=LightType.Directional;light.intensity=1;light.shadows=LightShadows.Hard;light.shadowStrength=1;light.shadowBias=.01f;light.shadowNormalBias=0;light.transform.rotation=Quaternion.LookRotation(new Vector3(.3f,0,1));RenderSettings.sun=light;
        _camera.enabled=true;for(int warm=0;warm<4;warm++)yield return null;_camera.enabled=false;
        _publication=new Publication();CameraGraphPassRegistry.Register(_publication);Field("_CrestOceanMaskTexture",(x,y)=>0);Field("_CrestWaterVolumeFrontFaceTexture",(x,y)=>0);Field("_CrestWaterVolumeBackFaceTexture",(x,y)=>0);Mode("_MASK_MASKED");
        var shadow=Capture("HiddenCasterShadow");Assert.That(shadow.Count(Red),Is.Zero,"View mask hides the caster colour");
        renderer.shadowCastingMode=ShadowCastingMode.Off;var noShadow=Capture("CasterShadowOff");
        int changed=shadow.Zip(noShadow,(a,b)=>Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b)>20?1:0).Sum();
        File.AppendAllText(_report,$"ShadowReceiverChangedPixels={changed}\n");Assert.That(changed,Is.GreaterThan(1000),"View-clipped geometry must still cast its shadow");
        renderer.shadowCastingMode=ShadowCastingMode.On;Assert.That(Capture("CasterShadowRestored"),Is.EqualTo(shadow));
        _material.SetColor("_Color",new Color(.8f,.02f,.01f,.25f));Assert.That(Capture("CasterAlphaCutoff"),Is.EqualTo(noShadow),"Alpha cutoff also removes shadow fragments");
        _material.SetColor("_Color",new Color(.8f,.02f,.01f,1));Assert.That(Capture("CasterAlphaRestored"),Is.EqualTo(shadow));
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        TransparentCleanup();
        if(_publication!=null)CameraGraphPassRegistry.Unregister(_publication);if(_snapshot!=null)CameraGraphPassRegistry.Unregister(_snapshot);
        OceanRenderer.EnableGraphicsInBatchMode=false;if(Application.isPlaying)yield return new ExitPlayMode();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        foreach(var handle in _handles)handle.Release();_handles.Clear();foreach(var obj in _owned)if(obj!=null){if(obj is RenderTexture rt)rt.Release();Object.DestroyImmediate(obj);}_owned.Clear();
    }
}
