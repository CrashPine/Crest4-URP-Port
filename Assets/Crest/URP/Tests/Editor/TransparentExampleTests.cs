// Crest 4 URP port. MIT; see LICENSE.Crest.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Crest;
using Crest.URP;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public partial class MaskedExampleTests
{
    Material _transparent;TransparentInputs _transparentInputs;AuxiliaryLightInput _transparentLight;
    readonly Dictionary<string,bool> _transparentKeywords=new Dictionary<string,bool>();
    static readonly string[] TransparentKeys={"CREST_WATER_VOLUME_2D","CREST_WATER_VOLUME_HAS_BACKFACE","CREST_SHADOWS_ON","CREST_SUBSURFACESCATTERING_ON"};
    sealed class TransparentInputs : ICameraGraphPass
    {
        public bool Volume2D,BackFace,Shadow,SSS;
        public Vector3 Density=new Vector3(.06f,.04f,.02f),Diffuse=new Vector3(.1f,.2f,.4f),Grazing=new Vector3(.2f,.3f,.5f),ShadowColour=new Vector3(.015f,.025f,.04f),Ambient=new Vector3(.2f,.3f,.4f),Subsurface=new Vector3(.3f,.4f,.2f);
        public RenderPassEvent Stage=>(RenderPassEvent)((int)RenderPassEvent.BeforeRenderingTransparents-2);
        sealed class Data {public TransparentInputs Input;}
        public void Record(RenderGraph graph,ContextContainer frame)
        {
            using var b=graph.AddUnsafePass<Data>("Test original transparent fog inputs",out var data);data.Input=this;
            b.UseAllGlobalTextures(true);b.AllowGlobalStateModification(true);b.AllowPassCulling(false);
            b.SetRenderFunc((Data p,UnsafeGraphContext c)=>
            {
                var cmd=CommandBufferHelpers.GetNativeCommandBuffer(c.cmd);var s=p.Input;
                bool[] flags={s.Volume2D,s.BackFace,s.Shadow,s.SSS};for(int i=0;i<TransparentKeys.Length;i++){if(flags[i])cmd.EnableShaderKeyword(TransparentKeys[i]);else cmd.DisableShaderKeyword(TransparentKeys[i]);}
                cmd.SetGlobalVector("_CrestDepthFogDensity",s.Density);cmd.SetGlobalVector("_CrestDiffuse",s.Diffuse);cmd.SetGlobalVector("_CrestDiffuseGrazing",s.Grazing);cmd.SetGlobalVector("_CrestDiffuseShadow",s.ShadowColour);cmd.SetGlobalVector("_CrestAmbientLighting",s.Ambient);cmd.SetGlobalVector("_CrestSubSurfaceColour",s.Subsurface);
                cmd.SetGlobalFloat("_CrestSubSurfaceBase",.2f);cmd.SetGlobalFloat("_CrestSubSurfaceSun",.5f);cmd.SetGlobalFloat("_CrestSubSurfaceSunFallOff",2);
            });
        }
    }
    void TransparentSetup(bool controlled)
    {
        Setup("TransparentExamples");_report=$"Evidence/TransparentExamples-{TestContext.CurrentContext.Test.Name}-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report,"Target=320x240 ARGBFloat Linear; RGB interior max.002; original fog law, native pre-fog PBR anchor; no tolerance adjustment after execution.\n");
        foreach(var key in TransparentKeys)_transparentKeywords[key]=Shader.IsKeywordEnabled(key);
        _transparent=Own(new Material(Shader.Find("Crest/Examples/UnderwaterTransparentSurfaceShader")));_transparent.SetColor("_Color",new Color(.6f,.2f,.1f,1));_transparent.SetFloat("_Glossiness",.45f);_transparent.SetFloat("_Metallic",.2f);_transparent.SetFloat("_FogMultiplier",1);
        _quad.GetComponent<Renderer>().sharedMaterial=_transparent;_quad.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        _target.Release();_target=Own(new RenderTexture(W,H,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear));_target.Create();
        if(controlled){_transparentInputs=new TransparentInputs();CameraGraphPassRegistry.Register(_transparentInputs);_publication=new Publication();CameraGraphPassRegistry.Register(_publication);Field("_CrestOceanMaskTexture",(x,y)=>1);Field("_CrestWaterVolumeFrontFaceTexture",(x,y)=>0);Field("_CrestWaterVolumeBackFaceTexture",(x,y)=>0);}
        _transparentLight=new AuxiliaryLightInput();CameraGraphPassRegistry.Register(_transparentLight);
    }
    Color[] TransparentCapture(string name, bool save = true)
    {
        bool enabled=_camera.enabled;_camera.enabled=true;try{RenderPipeline.SubmitRenderRequest(_camera,new UniversalRenderPipeline.SingleCameraRequest{destination=_target});}finally{_camera.enabled=enabled;}
        var previous=RenderTexture.active;var cpu=new Texture2D(W,H,TextureFormat.RGBAFloat,false,true);var preview=new Texture2D(W,H,TextureFormat.RGBA32,false);
        try
        {
            RenderTexture.active=_target;cpu.ReadPixels(new Rect(0,0,W,H),0,0);cpu.Apply();var values=cpu.GetPixels();
            if (save) { preview.SetPixels(values.Select(c=>{var p=c.gamma;p.a=1;return p;}).ToArray());preview.Apply();File.WriteAllBytes($"Evidence/TransparentExamples-{name}-{SystemInfo.graphicsDeviceType}.png",preview.EncodeToPNG()); }
            File.AppendAllText(_report,$"Capture={name}; FogLinear={Shader.IsKeywordEnabled("FOG_LINEAR")}; FogExp={Shader.IsKeywordEnabled("FOG_EXP")}; R0={values[0].r:R}; Centre={values[H/2*W+W/2]}; Alpha={_transparent.GetColor("_Color").a}; Multiplier={_transparent.GetFloat("_FogMultiplier")}\n");return values;
        }
        finally {RenderTexture.active=previous;Object.DestroyImmediate(cpu);Object.DestroyImmediate(preview);}
    }
    void TransparentAgreement(string label,Color[] actual,Color[] expected,bool split=false)
    {
        double max=0,sum=0;int count=0;
        for(int y=2;y<H-2;y++)for(int x=2;x<W-2;x++)
        {
            if(split&&Math.Abs(x-W/2)<=1)continue;int i=y*W+x;
            foreach(float d in new[]{Math.Abs(actual[i].r-expected[i].r),Math.Abs(actual[i].g-expected[i].g),Math.Abs(actual[i].b-expected[i].b)}){Assert.That(float.IsFinite(d),Is.True);max=Math.Max(max,d);sum+=d;count++;}
        }
        File.AppendAllText(_report,$"Reference={label}; RGBFloatMax={max}; Mean={sum/count}; Channels={count}\n");Assert.That(max,Is.LessThanOrEqualTo(.002),label);
    }
    static int TransparentChanged(Color[] a,Color[] b)=>a.Zip(b,(x,y)=>Math.Abs(x.r-y.r)+Math.Abs(x.g-y.g)+Math.Abs(x.b-y.b)>.01?1:0).Sum();
    Material TransparentLitReference()
    {
        var m=Own(new Material(Shader.Find("Universal Render Pipeline/Lit")));m.SetColor("_BaseColor",_transparent.GetColor("_Color"));m.SetTexture("_BaseMap",Texture2D.whiteTexture);m.SetFloat("_Metallic",.2f);m.SetFloat("_Smoothness",.45f);m.SetFloat("_Surface",1);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.SetFloat("_ZWrite",0);m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);m.SetFloat("_SrcBlendAlpha",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlendAlpha",(float)BlendMode.OneMinusSrcAlpha);m.renderQueue=3000;return m;
    }
    [UnityTest,Timeout(180000)]
    public IEnumerator TransparentPbrAlphaAndUnityFogFallbackMatchUrpLit()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();TransparentSetup(true);AuxiliaryLight();
        _camera.enabled=true;for(int i=0;i<4;i++)yield return null;_camera.enabled=false;
        var renderer=_quad.GetComponent<Renderer>();var reference=TransparentLitReference();
        Color[] Compare(string name,float alpha)
        {
            var color=new Color(.6f,.2f,.1f,alpha);_transparent.SetColor("_Color",color);reference.SetColor("_BaseColor",color);
            renderer.sharedMaterial=_transparent;var actual=TransparentCapture(name);renderer.sharedMaterial=reference;var expected=TransparentCapture("Lit"+name);TransparentAgreement(name,actual,expected);renderer.sharedMaterial=_transparent;return actual;
        }
        var opaque=Compare("Opaque",1);var zero=Compare("AlphaZero",0);var half=Compare("AlphaHalf",.5f);var blend=opaque.Zip(zero,(a,b)=>Color.Lerp(b,a,.5f)).ToArray();TransparentAgreement("AlphaBlend",half,blend);
        Assert.That(TransparentChanged(opaque,zero),Is.GreaterThan(1000));
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=0;RenderSettings.fogEndDistance=20;RenderSettings.fogColor=new Color(.1f,.4f,.2f);
        var unityFog=Compare("UnityFogFallback",1);Assert.That(TransparentChanged(unityFog,opaque),Is.GreaterThan(1000));
        Field("_CrestOceanMaskTexture",(x,y)=>-1);_transparent.SetFloat("_FogMultiplier",0);TransparentAgreement("CrestProcessedSuppressesUnityFog",TransparentCapture("CrestZeroMultiplier"),opaque);
        Field("_CrestOceanMaskTexture",(x,y)=>1);RenderSettings.fog=false;_transparent.SetFloat("_FogMultiplier",1);yield return null;var restored=TransparentCapture("PbrRestored");TransparentAgreement("RestoredDiagnostic",restored,opaque);Assert.That(restored,Is.EqualTo(opaque));
        Assert.That(ShaderUtil.GetShaderMessages(_transparent.shader).Where(x=>x.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error),Is.Empty);
    }
    [UnityTest,Timeout(240000)]
    public IEnumerator TransparentFogVolumeShadowAndSssFollowOriginalLaw()
    {
        OceanRenderer.EnableGraphicsInBatchMode=true;EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");yield return new EnterPlayMode();
        foreach(var wave in Object.FindObjectsByType<ShapeWaves>())wave.enabled=false;TransparentSetup(true);var ocean=OceanRenderer.Instance;ocean.ViewCamera=_camera;
        foreach(var r in ocean.Root.GetComponentsInChildren<Renderer>())r.forceRenderingOff=true;
        var config=new SerializedObject(ocean);config.FindProperty("_createShadowData").boolValue=false;config.FindProperty("_createFoamSim").boolValue=false;config.FindProperty("_createClipSurfaceData").boolValue=false;config.ApplyModifiedPropertiesWithoutUndo();
        var sun=AuxiliaryLight();_camera.enabled=true;for(int i=0;i<8;i++)yield return null;_camera.enabled=false;
        var baseline=TransparentCapture("FogBaseline");var initialBaseline=baseline;Field("_CrestOceanMaskTexture",(x,y)=>-1);
        Color[] Model(float distance,float multiplier,bool shadow=false,float shadowValue=0,bool sss=false,bool split=false)
        {
            var result=(Color[])baseline.Clone();var s=_transparentInputs;
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                if(split&&x>=W/2)continue;int i=y*W+x;var position=new Vector3(((x+.5f)/W-.5f)*8,((y+.5f)/H-.5f)*6,10);var view=-position.normalized;
                var scatter=Vector3.Lerp(s.Diffuse,s.Grazing,1-Math.Abs(view.y));float lit=shadow?Mathf.Clamp01(1-shadowValue):1;
                if(shadow)scatter=Vector3.Lerp(s.ShadowColour,scatter,lit);
                if(sss)
                {
                    scatter=Vector3.Scale(scatter,s.Ambient);float towards=Mathf.Pow(Math.Max(0,Vector3.Dot(_transparentLight.Direction.normalized,-view)),2);
                    var colour=(Vector3)(Vector4)_transparentLight.Colour;scatter+=(.2f+.5f*towards)*Vector3.Scale(s.Subsurface,colour)*lit;
                }
                var fog=new Vector3(1-Mathf.Exp(-s.Density.x*distance),1-Mathf.Exp(-s.Density.y*distance),1-Mathf.Exp(-s.Density.z*distance))*multiplier;
                var b=baseline[i];result[i]=new Color(Mathf.Lerp(b.r,scatter.x,fog.x),Mathf.Lerp(b.g,scatter.y,fog.y),Mathf.Lerp(b.b,scatter.z,fog.z),b.a);
            }
            return result;
        }
        var full=TransparentCapture("FogFull");TransparentAgreement("Fullscreen10m",full,Model(10,1));Assert.That(TransparentChanged(full,baseline),Is.GreaterThan(1000));
        _transparent.SetFloat("_FogMultiplier",.5f);TransparentAgreement("MultiplierHalf",TransparentCapture("FogHalf"),Model(10,.5f));_transparent.SetFloat("_FogMultiplier",0);TransparentAgreement("MultiplierZero",TransparentCapture("FogZero"),baseline);_transparent.SetFloat("_FogMultiplier",1);
        _transparentInputs.Volume2D=true;Field("_CrestWaterVolumeFrontFaceTexture",(x,y)=>RawDepth(5));TransparentAgreement("Volume2D5m",TransparentCapture("Volume2D"),Model(5,1));
        Field("_CrestWaterVolumeFrontFaceTexture",(x,y)=>RawDepth(15));TransparentAgreement("BeforeVolume",TransparentCapture("BeforeVolume"),baseline);
        Field("_CrestWaterVolumeFrontFaceTexture",(x,y)=>0);TransparentAgreement("Volume2DMissing",TransparentCapture("VolumeMissing"),baseline);
        _transparentInputs.Volume2D=false;_transparentInputs.BackFace=true;Field("_CrestWaterVolumeFrontFaceTexture",(x,y)=>RawDepth(5));Field("_CrestWaterVolumeBackFaceTexture",(x,y)=>RawDepth(8));TransparentAgreement("BackFace3m",TransparentCapture("BackFace"),Model(3,1));
        Field("_CrestWaterVolumeFrontFaceTexture",(x,y)=>0);TransparentAgreement("InsideVolume8m",TransparentCapture("InsideVolume"),Model(8,1));
        _transparentInputs.BackFace=false;Field("_CrestWaterVolumeBackFaceTexture",(x,y)=>0);Field("_CrestOceanMaskTexture",(x,y)=>x<W/2?-1:1);TransparentAgreement("MaskSplit",TransparentCapture("MaskSplit"),Model(10,1,split:true),true);Field("_CrestOceanMaskTexture",(x,y)=>-1);
        _transparentInputs.SSS=true;TransparentAgreement("SSSBase",TransparentCapture("SSSBase"),Model(10,1,sss:true));
        Field("_CrestOceanMaskTexture",(x,y)=>1);sun.transform.rotation=Quaternion.LookRotation(-Vector3.forward);baseline=TransparentCapture("SSSSunBaseline");Field("_CrestOceanMaskTexture",(x,y)=>-1);TransparentAgreement("SSSSun",TransparentCapture("SSSSun"),Model(10,1,sss:true));
        // Uniform real array overrides only fog shadow input; native ocean cascade metadata remains independent.
        var rt=Own(new RenderTexture(8,8,0,RenderTextureFormat.RFloat,RenderTextureReadWrite.Linear));rt.dimension=TextureDimension.Tex2DArray;rt.volumeDepth=16;rt.Create();var handle=RTHandles.Alloc(rt);_handles.Add(handle);_publication.Textures[Shader.PropertyToID("_LD_TexArray_Shadow")]=handle;
        var cmd=new CommandBuffer();for(int layer=0;layer<16;layer++){cmd.SetRenderTarget(rt,0,CubemapFace.Unknown,layer);cmd.ClearRenderTarget(false,true,new Color(.75f,0,0,0));}Graphics.ExecuteCommandBuffer(cmd);cmd.Release();
        _transparentInputs.Shadow=true;TransparentAgreement("ShadowSSS",TransparentCapture("ShadowSSS"),Model(10,1,true,.75f,true));_transparentInputs.SSS=false;TransparentAgreement("ShadowOnly",TransparentCapture("ShadowOnly"),Model(10,1,true,.75f));
        _transparentInputs.Shadow=false;sun.transform.rotation=Quaternion.identity;baseline=initialBaseline;Assert.That(TransparentCapture("FogRestored"),Is.EqualTo(full));
    }
    [UnityTest,Timeout(240000)]
    public IEnumerator TransparentActualAboveBelowAndVolumeUseCurrentMask()
    {
        OceanRenderer.EnableGraphicsInBatchMode=true;EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");yield return new EnterPlayMode();foreach(var wave in Object.FindObjectsByType<ShapeWaves>())wave.enabled=false;
        TransparentSetup(false);_camera.cullingMask|=1<<4;AuxiliaryLight();var ocean=OceanRenderer.Instance;ocean.ViewCamera=_camera;foreach(var r in ocean.Root.GetComponentsInChildren<Renderer>())r.forceRenderingOff=true;
        var surface=Own(new Material(ocean.OceanMaterial));surface.SetVector("_DepthFogDensity",new Vector4(.08f,.08f,.08f,1));foreach(var key in new[]{"_CAUSTICS_ON","_SHADOWS_ON","_FOAM_ON","_SUBSURFACESCATTERING_ON"})surface.DisableKeyword(key);ocean.OceanMaterial=surface;
        var config=new SerializedObject(ocean);config.FindProperty("_createShadowData").boolValue=false;config.FindProperty("_createFoamSim").boolValue=false;config.FindProperty("_createClipSurfaceData").boolValue=false;config.ApplyModifiedPropertiesWithoutUndo();
        var fog=_camera.gameObject.AddComponent<UnderwaterRenderer>();fog.EnableShaderAPI=true;var settings=new SerializedObject(fog);settings.FindProperty("_meniscus").boolValue=false;settings.ApplyModifiedPropertiesWithoutUndo();
        void Position(float y){_camera.transform.position=new Vector3(0,y,-8);_quad.transform.position=_camera.transform.position+Vector3.forward*10;}
        Position(3);for(int i=0;i<16;i++)yield return null;_transparent.SetFloat("_FogMultiplier",0);var above=TransparentCapture("ActualAboveOff");_transparent.SetFloat("_FogMultiplier",1);Assert.That(TransparentCapture("ActualAboveOn"),Is.EqualTo(above));
        Position(-3);for(int i=0;i<16;i++)yield return null;File.AppendAllText(_report,$"ViewerHeight={ocean.ViewerHeightAboveWater}; Active={fog.IsActive}; Density={Shader.GetGlobalVector("_CrestDepthFogDensity")}\n");Assert.That(fog.IsActive,Is.True);_transparent.SetFloat("_FogMultiplier",0);var below=TransparentCapture("ActualBelowOff");_transparent.SetFloat("_FogMultiplier",1);var enabled=TransparentCapture("ActualBelowOn");Assert.That(TransparentChanged(below,enabled),Is.GreaterThan(1000));
        Position(3);for(int i=0;i<16;i++)yield return null;Assert.That(fog.IsActive,Is.False);_transparent.SetFloat("_FogMultiplier",0);var returned=TransparentCapture("ActualAboveReturnedOff");_transparent.SetFloat("_FogMultiplier",1);Assert.That(TransparentCapture("ActualAboveReturnedOn"),Is.EqualTo(returned),"Inactive above-water camera must not retain a previous underwater mask");
        Position(-3);for(int i=0;i<16;i++)yield return null;
        var volume=Own(GameObject.CreatePrimitive(PrimitiveType.Cube));volume.transform.position=new Vector3(0,-3,0);volume.transform.localScale=new Vector3(4,4,6);volume.GetComponent<Renderer>().enabled=false;fog._mode=UnderwaterRenderer.Mode.Volume;fog._volumeGeometry=volume.GetComponent<MeshFilter>();
        var native=Own(new RenderTexture(W,H,0,RenderTextureFormat.RFloat,RenderTextureReadWrite.Linear));native.Create();var h=RTHandles.Alloc(native);_handles.Add(h);_snapshot=new Snapshot{Destination=h,Probe=Own(new Material(Shader.Find("Hidden/Crest/Tests/MaskTimingProbe")))};CameraGraphPassRegistry.Register(_snapshot);
        _transparent.SetFloat("_FogMultiplier",0);var off=TransparentCapture("ActualVolumeOff");_transparent.SetFloat("_FogMultiplier",1);var on=TransparentCapture("ActualVolumeOn");var mask=Read(native);int changed=0,outside=0;
        for(int y=2;y<H-2;y++)for(int x=2;x<W-2;x++)
        {
            int i=y*W+x;bool edge=false;for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)if((mask[(y+dy)*W+x+dx]<0)!=(mask[i]<0))edge=true;if(edge)continue;
            bool different=Math.Abs(on[i].r-off[i].r)+Math.Abs(on[i].g-off[i].g)+Math.Abs(on[i].b-off[i].b)>.01;if(different){if(mask[i]<0)changed++;else outside++;}
        }
        File.AppendAllText(_report,$"ActualVolumeChangedInterior={changed}; ChangedOutsideNativeMask={outside}\n");Assert.That(changed,Is.GreaterThan(1000));Assert.That(outside,Is.Zero);
        volume.transform.position+=Vector3.right*2;Assert.That(TransparentChanged(on,TransparentCapture("ActualVolumeMoved")),Is.GreaterThan(1000));volume.transform.position-=Vector3.right*2;Assert.That(TransparentCapture("ActualVolumeRestored"),Is.EqualTo(on));
    }
    [UnityTest, Timeout(180000)]
    public IEnumerator TransparentFogRespectsNativePortalAndVolumeFaces()
    {
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        foreach (var wave in Object.FindObjectsByType<ShapeWaves>()) wave.enabled = false;
        TransparentSetup(false);
        _camera.cullingMask |= 1 << 4;
        AuxiliaryLight();
        var ocean = OceanRenderer.Instance;
        ocean.ViewCamera = _camera;
        foreach (var renderer in ocean.Root.GetComponentsInChildren<Renderer>()) renderer.forceRenderingOff = true;
        var surface = Own(new Material(ocean.OceanMaterial));
        surface.SetVector("_DepthFogDensity", new Vector4(.08f, .08f, .08f, 1));
        surface.SetInt("_CullMode", (int)CullMode.Off);
        surface.EnableKeyword("_UNDERWATER_ON");
        surface.SetFloat("_Underwater", 1);
        foreach (string keyword in new[] { "_CAUSTICS_ON", "_SHADOWS_ON", "_FOAM_ON", "_SUBSURFACESCATTERING_ON" })
            surface.DisableKeyword(keyword);
        ocean.OceanMaterial = surface;
        var oceanSettings = new SerializedObject(ocean);
        foreach (string field in new[] { "_createShadowData", "_createFoamSim", "_createClipSurfaceData" })
            oceanSettings.FindProperty(field).boolValue = false;
        oceanSettings.ApplyModifiedPropertiesWithoutUndo();

        _camera.transform.position = new Vector3(0, -3, -8);
        var fog = _camera.gameObject.AddComponent<UnderwaterRenderer>();
        fog.enabled = false;
        fog.EnableShaderAPI = true;
        var fogSettings = new SerializedObject(fog);
        fogSettings.FindProperty("_meniscus").boolValue = false;
        fogSettings.ApplyModifiedPropertiesWithoutUndo();

        _transparent.SetColor("_Color", new Color(.6f, .2f, .1f, .6f));
        var backdrop = Own(GameObject.CreatePrimitive(PrimitiveType.Quad));
        backdrop.layer = 5;
        backdrop.transform.position = new Vector3(0, -3, 14);
        backdrop.transform.localScale = new Vector3(8, 6, 1);
        var backdropMaterial = Own(new Material(Shader.Find("Universal Render Pipeline/Unlit")));
        backdropMaterial.SetColor("_BaseColor", new Color(.55f, .6f, .65f, 1));
        backdrop.GetComponent<Renderer>().sharedMaterial = backdropMaterial;

        var portal = Own(GameObject.CreatePrimitive(PrimitiveType.Quad));
        portal.transform.position = new Vector3(0, -3, -3);
        portal.transform.localScale = new Vector3(4, 4, 1);
        portal.GetComponent<Renderer>().enabled = false;
        var volume = Own(GameObject.CreatePrimitive(PrimitiveType.Cube));
        volume.transform.position = new Vector3(0, -3, 0);
        volume.transform.localScale = new Vector3(4, 4, 6);
        volume.GetComponent<Renderer>().enabled = false;
        int center = H / 2 * W + W / 2;

        foreach (var mode in new[] { UnderwaterRenderer.Mode.Portal, UnderwaterRenderer.Mode.Volume,
                     UnderwaterRenderer.Mode.VolumeFlyThrough })
        {
            fog.enabled = false;
            fog._mode = mode;
            fog._volumeGeometry = (mode == UnderwaterRenderer.Mode.Portal ? portal : volume).GetComponent<MeshFilter>();
            fog.enabled = true;
            for (int frame = 0; frame < 8; frame++) yield return null;

            _quad.GetComponent<Renderer>().enabled = false;
            fog.enabled = false;
            var opaqueOff = TransparentCapture(mode + "-OpaqueOff", false);
            fog.enabled = true;
            var opaqueOn = TransparentCapture(mode + "-OpaqueOn", false);
            CheckVolumeFootprint(mode + "-Opaque", opaqueOff, opaqueOn, expectFog: true);
            _quad.GetComponent<Renderer>().enabled = true;

            CheckVolumeConsumer(mode + "-Before", -4, expectFog: false);
            float inside = CheckVolumeConsumer(mode + "-Inside", 1, expectFog: true, save: true);
            float behind = CheckVolumeConsumer(mode + "-Behind", 6, expectFog: true);
            float farther = CheckVolumeConsumer(mode + "-Farther", 10, expectFog: true);
            Assert.That(behind, Is.GreaterThan(inside + .005f), mode.ToString());
            if (mode == UnderwaterRenderer.Mode.Portal)
                Assert.That(farther, Is.GreaterThan(behind + .005f), "Portal fog continues behind its plane.");
            else
                Assert.That(farther, Is.EqualTo(behind).Within(.002f), "Back face caps the fog thickness.");
        }

        _camera.transform.position = new Vector3(0, -3, 0);
        for (int frame = 0; frame < 8; frame++) yield return null;
        CheckVolumeConsumer("FlyThrough-CameraInside", 2, expectFog: true, checkExterior: false, save: true);

        float CheckVolumeConsumer(string label, float z, bool expectFog, bool checkExterior = true, bool save = false)
        {
            _quad.transform.position = new Vector3(0, -3, z);
            _transparent.SetFloat("_FogMultiplier", 0);
            var off = TransparentCapture(label + "-Off", save);
            _transparent.SetFloat("_FogMultiplier", 1);
            var on = TransparentCapture(label + "-On", save);
            _transparent.SetFloat("_FogMultiplier", 0);
            var restored = TransparentCapture(label + "-Restored", false);
            Assert.That(restored, Is.EqualTo(off), label);
            return CheckVolumeFootprint(label, off, on, expectFog, checkExterior);
        }

        float CheckVolumeFootprint(string label, Color[] off, Color[] on, bool expectFog, bool checkExterior = true)
        {
            int interior = 0, exterior = 0;
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int index = y * W + x;
                if (RgbDelta(on[index], off[index]) <= .01f) continue;
                float horizontal = Mathf.Abs((x + .5f - W * .5f) * 8f / W);
                float vertical = Mathf.Abs((y + .5f - H * .5f) * 6f / H);
                if (horizontal < 1.8f && vertical < 1.8f) interior++;
                if (horizontal > 2.2f || vertical > 2.2f) exterior++;
            }
            float delta = RgbDelta(on[center], off[center]);
            File.AppendAllText(_report, FormattableString.Invariant(
                $"{label}: InteriorChanged={interior}; ExteriorChanged={exterior}; CenterDelta={delta:R}\n"));
            if (expectFog) Assert.That(interior, Is.GreaterThan(1000), label);
            else Assert.That(TransparentChanged(off, on), Is.Zero, label);
            if (checkExterior) Assert.That(exterior, Is.Zero, label);
            return delta;
        }
    }

    static float RgbDelta(Color a, Color b)
        => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

    void TransparentCleanup()
    {
        if(_transparentInputs!=null)CameraGraphPassRegistry.Unregister(_transparentInputs);if(_transparentLight!=null)CameraGraphPassRegistry.Unregister(_transparentLight);_transparentInputs=null;_transparentLight=null;
        foreach(var pair in _transparentKeywords){if(pair.Value)Shader.EnableKeyword(pair.Key);else Shader.DisableKeyword(pair.Key);}_transparentKeywords.Clear();
    }
}
