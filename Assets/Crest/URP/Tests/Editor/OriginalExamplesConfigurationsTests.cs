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
using UnityEngine.Rendering.RenderGraphModule;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public partial class OriginalExampleSceneTests
{
    [Serializable] sealed class OriginalConfiguration
    {
        public int index;public string name,cameraPosition,cameraRotation;public float oceanScale,viewerHeight;
        public int activeRenderers,errorPinkPixels,nonBackgroundPixels,missingScripts;
        public string[] materials,callbacks;public string capture;
    }
    [Serializable] sealed class OriginalConfigurationsReport
    {
        public string unity,api,scene,scope;public List<OriginalConfiguration> configurations=new List<OriginalConfiguration>();
    }
    static string OriginalPath(Transform obj)
    {
        var path=obj.name;while(obj.parent!=null){obj=obj.parent;path=obj.name+"/"+path;}return path;
    }
    static string[] OriginalCallbacks(GameObject config)
    {
        var result=new List<string>();
        foreach(var component in config.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if(component==null)continue;
            for(var type=component.GetType();type!=typeof(MonoBehaviour)&&type!=null;type=type.BaseType)
            foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
            {
                if(!(field.GetValue(component) is UnityEventBase ev))continue;
                for(int i=0;i<ev.GetPersistentEventCount();i++)
                {
                    var target=ev.GetPersistentTarget(i);result.Add(OriginalPath(component.transform)+":"+field.Name+" -> "+(target==null?"MISSING":target.GetType().Name+":"+target.name)+"."+ev.GetPersistentMethodName(i)+" ["+ev.GetPersistentListenerState(i)+"]");
                }
            }
        }
        return result.ToArray();
    }
    [UnityTest,Timeout(300000)]
    public IEnumerator ExamplesConfigurationsCycleAndRenderWithoutErrorShader()
    {
        const string scene="Assets/Crest/Crest-Examples/Examples/Scenes/Examples.unity";
        OceanRenderer.EnableGraphicsInBatchMode=true;EditorSceneManager.OpenScene(scene);
        var controllers=Object.FindObjectsByType<ExamplesController>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        var controller=controllers.Single(x=>x.transform.childCount==24&&x.transform.GetChild(0).name=="Albedo");
        // Exercise the original Next API. Physical keyboard polling is outside this automated fixture.
        foreach(var c in controllers)c.enabled=false;
        yield return new EnterPlayMode();Assert.That(Application.isPlaying,Is.True);
        controller=Object.FindObjectsByType<ExamplesController>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(x=>x.transform.childCount==24&&x.transform.GetChild(0).name=="Albedo");
        foreach(Transform child in controller.transform)child.gameObject.SetActive(false);
        var report=new OriginalConfigurationsReport{unity=Application.unityVersion,api=SystemInfo.graphicsDeviceType.ToString(),scene=scene,scope="Original24 configurations selected through public Next, source camera/events/materials, PlayMode640x360. No shader-error smoke is not feature acceptance; physical input/postprocess/standalone/baked output not accepted."};
        string reportPath=$"Evidence/OriginalExamples-{SystemInfo.graphicsDeviceType}.json";string folder=$"Evidence/OriginalExamples-{SystemInfo.graphicsDeviceType}";Directory.CreateDirectory(folder);
        _target=new RenderTexture(640,360,24,RenderTextureFormat.ARGB32);_target.Create();
        for(int index=0;index<24;index++)
        {
            controller.Next();var config=controller.transform.GetChild(index);
            Assert.That(config.gameObject.activeInHierarchy,Is.True,index+":"+config.name);
            Assert.That(controller.transform.Cast<Transform>().Count(x=>x.gameObject.activeSelf),Is.EqualTo(1));
            for(int warm=0;warm<45;warm++)yield return null;
            var ocean=OceanRenderer.Instance;var camera=Camera.main;Assert.That(ocean,Is.Not.Null);Assert.That(camera,Is.Not.Null);ocean.ViewCamera=camera;
            camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;camera.allowMSAA=false;camera.aspect=16f/9;
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=_target});
            var previous=RenderTexture.active;var cpu=new Texture2D(640,360,TextureFormat.RGBA32,false);
            try
            {
                RenderTexture.active=_target;cpu.ReadPixels(new Rect(0,0,640,360),0,0);cpu.Apply();var pixels=cpu.GetPixels32();
                var renderers=Object.FindObjectsByType<Renderer>();string path=folder+"/"+index.ToString("00")+"-"+config.name+".png";File.WriteAllBytes(path,cpu.EncodeToPNG());
                var row=new OriginalConfiguration{index=index,name=config.name,cameraPosition=camera.transform.position.ToString("R"),cameraRotation=camera.transform.eulerAngles.ToString("R"),oceanScale=ocean.Scale,viewerHeight=ocean.ViewerHeightAboveWater,activeRenderers=renderers.Length,errorPinkPixels=pixels.Count(x=>x.r>245&&x.g<10&&x.b>245),nonBackgroundPixels=pixels.Count(x=>!x.Equals(pixels[0])),missingScripts=config.GetComponentsInChildren<Transform>(true).Sum(x=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(x.gameObject)),materials=renderers.SelectMany(x=>x.sharedMaterials).Where(x=>x!=null).Select(x=>x.name+":"+x.shader.name+":supported="+x.shader.isSupported).Distinct().OrderBy(x=>x).ToArray(),callbacks=OriginalCallbacks(config.gameObject),capture=path};report.configurations.Add(row);File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));
                Assert.That(row.missingScripts,Is.Zero,config.name);Assert.That(row.errorPinkPixels,Is.Zero,config.name+" contains error-pink pixels; inspect capture/material inventory");Assert.That(row.nonBackgroundPixels,Is.GreaterThan(5000),config.name+" must render a meaningful original scene");
            }
            finally {RenderTexture.active=previous;Object.DestroyImmediate(cpu);}
        }
        controller.Next();Assert.That(controller.transform.GetChild(0).gameObject.activeSelf,Is.True,"Original Next wraps to first configuration");controller.Previous();Assert.That(controller.transform.GetChild(23).gameObject.activeSelf,Is.True,"Original Previous wraps to last configuration");
    }
    Color32[] OriginalConfigurationCapture(Camera camera,string name)
    {
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=_target});var previous=RenderTexture.active;var cpu=new Texture2D(640,360,TextureFormat.RGBA32,false);
        try{RenderTexture.active=_target;cpu.ReadPixels(new Rect(0,0,640,360),0,0);cpu.Apply();File.WriteAllBytes($"Evidence/OriginalExamples-Control-{name}-{SystemInfo.graphicsDeviceType}.png",cpu.EncodeToPNG());var pixels=cpu.GetPixels32();Assert.That(pixels.Count(x=>x.r>245&&x.g<10&&x.b>245),Is.Zero,name);return pixels;}
        finally{RenderTexture.active=previous;Object.DestroyImmediate(cpu);}
    }
    static int OriginalChanged(Color32[] a,Color32[] b)=>a.Zip(b,(x,y)=>Math.Abs(x.r-y.r)+Math.Abs(x.g-y.g)+Math.Abs(x.b-y.b)>6?1:0).Sum();
    [UnityTest,Timeout(300000)]
    public IEnumerator ExamplesOriginalFillCallbacksAndTransparentFogChangeVisibleOutput()
    {
        OceanRenderer.EnableGraphicsInBatchMode=true;EditorSceneManager.OpenScene("Assets/Crest/Crest-Examples/Examples/Scenes/Examples.unity");
        foreach(var c in Object.FindObjectsByType<ExamplesController>(FindObjectsInactive.Include,FindObjectsSortMode.None))c.enabled=false;
        yield return new EnterPlayMode();var controller=Object.FindObjectsByType<ExamplesController>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(x=>x.transform.childCount==24&&x.transform.GetChild(0).name=="Albedo");
        string report=$"Evidence/OriginalExamples-Controls-{SystemInfo.graphicsDeviceType}.txt";File.WriteAllText(report,"Original source scene/config callbacks; fixed ocean clock1/delta0; same-frame off/on controls. Threshold changed>200, exact restoration. Transparent lower-camera probe is explicit, not default pose.\n");
        _target=new RenderTexture(640,360,24,RenderTextureFormat.ARGB32);_target.Create();
        var clock=new GameObject("Original examples fixed test clock").AddComponent<TimeProviderCustom>();clock._overrideTime=clock._overrideDeltaTime=clock._paused=true;clock._time=1;clock._deltaTime=0;
        foreach(var name in new[]{"River","DynamicWavesReflections","VaryingWaterLevel","UnderwaterLighting","TransparentObjectUnderwater"})
        {
            foreach(Transform child in controller.transform)child.gameObject.SetActive(false);
            var config=controller.transform.Cast<Transform>().Single(x=>x.name==name);config.gameObject.SetActive(true);for(int i=0;i<45;i++)yield return null;
            var ocean=OceanRenderer.Instance;var camera=Camera.main;Assert.That(camera,Is.Not.Null);ocean.ViewCamera=camera;ocean.PushTimeProvider(clock);
            camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;camera.allowMSAA=false;camera.aspect=16f/9;
            for(int i=0;i<8;i++)yield return null;Assert.That(ocean.TimeProvider,Is.SameAs(clock));
            var ur=UnderwaterRenderer.ForUrpCamera(camera);File.AppendAllText(report,$"State={name}; UnderwaterMode={ur?._mode}; Active={ur?.IsActive}; API={ur?.EnableShaderAPI}; Camera={camera.transform.position}\n");
            if(name!="TransparentObjectUnderwater"&&name!="UnderwaterLighting")
            {
                var callbacks=config.GetComponentsInChildren<CustomPassForCamera>(true).Where(c=>
                {
                    var ev=(UnityEventBase)typeof(CustomPassForCamera).GetField("_onExecute",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c);
                    return Enumerable.Range(0,ev.GetPersistentEventCount()).Any(i=>ev.GetPersistentMethodName(i)==nameof(MaskFill.ExecuteFillMask));
                }).ToArray();Assert.That(callbacks.Length,Is.EqualTo(1),name);
                var before=OriginalConfigurationCapture(camera,name+"-on");var stable=OriginalConfigurationCapture(camera,name+"-repeat");
                int repeat=before.Zip(stable,(a,b)=>a.Equals(b)?0:1).Sum();File.AppendAllText(report,$"Case={name}; BaselineRepeatChanged={repeat}\n");Assert.That(repeat,Is.Zero,name+" baseline must be stable before control");
                Color32[] off;var states=callbacks.Select(c=>c.enabled).ToArray();
                try{foreach(var callback in callbacks)callback.enabled=false;off=OriginalConfigurationCapture(camera,name+"-fill-off");}
                finally{for(int i=0;i<callbacks.Length;i++)callbacks[i].enabled=states[i];}
                var restored=OriginalConfigurationCapture(camera,name+"-restored");int changed=OriginalChanged(before,off);int restoration=before.Zip(restored,(a,b)=>a.Equals(b)?0:1).Sum();File.AppendAllText(report,$"Case={name}; FillOffChangedPixels={changed}; RestoreChangedPixels={restoration}\n");Assert.That(changed,Is.GreaterThan(200),name+" fill must be visible");Assert.That(restoration,Is.Zero,name);
            }
            else if(name=="UnderwaterLighting")
            {
                var environment=camera.GetComponent<UnderwaterEnvironmentalLighting>();Assert.That(environment,Is.Not.Null);Assert.That(environment.enabled,Is.True);
                var animator=camera.GetComponent<Animator>();if(animator!=null)animator.enabled=false;var position=camera.transform.position;camera.transform.position=new Vector3(position.x,-3,position.z);
                for(int settle=0;settle<16;settle++)yield return null;
                var type=typeof(UnderwaterEnvironmentalLighting);const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var light=(Light)type.GetField("_primaryLight",flags).GetValue(environment);float baseIntensity=(float)type.GetField("_lightIntensity",flags).GetValue(environment);float weight=(float)type.GetField("_weight",flags).GetValue(environment);var density=ocean.UnderwaterDepthFogDensity;
                float expected=baseIntensity*Mathf.Exp((density.x+density.y+density.z)/3*Mathf.Min(ocean.ViewerHeightAboveWater*.25f,0)*weight);float actual=light.intensity;
                Assert.That(ocean.ViewerHeightAboveWater,Is.LessThan(-2));Assert.That(actual,Is.EqualTo(expected).Within(.00001f));Assert.That(actual,Is.LessThan(baseIntensity));
                var before=OriginalConfigurationCapture(camera,"UnderwaterLighting-attenuated");environment.enabled=false;var off=OriginalConfigurationCapture(camera,"UnderwaterLighting-environment-off");Assert.That(light.intensity,Is.EqualTo(baseIntensity));environment.enabled=true;type.GetMethod("LateUpdate",flags).Invoke(environment,null);var restored=OriginalConfigurationCapture(camera,"UnderwaterLighting-environment-restored");
                int changed=OriginalChanged(before,off);int restoration=before.Zip(restored,(a,b)=>a.Equals(b)?0:1).Sum();File.AppendAllText(report,$"Case={name}; ProbeCamera={camera.transform.position}; ViewerHeight={ocean.ViewerHeightAboveWater}; BaseLight={baseIntensity}; AttenuatedLight={actual}; ExpectedLight={expected}; EnvironmentOffChangedPixels={changed}; RestoreChangedPixels={restoration}; FillDefaultAndInsideProbeUnobservable=True\n");Assert.That(changed,Is.GreaterThan(200));Assert.That(restoration,Is.Zero);
            }
            else
            {
                var renderer=config.GetComponentsInChildren<Renderer>().Single(r=>r.sharedMaterial!=null&&r.sharedMaterial.shader.name=="Crest/Examples/UnderwaterTransparentSurfaceShader");var original=renderer.sharedMaterial;var material=new Material(original);var animator=camera.GetComponent<Animator>();bool animate=animator!=null&&animator.enabled;var position=camera.transform.position;var rotation=camera.transform.rotation;var spherePosition=renderer.transform.position;
                try
                {
                    renderer.sharedMaterial=material;if(animator!=null)animator.enabled=false;camera.transform.position=new Vector3(position.x,-1,position.z);
                    var volume=UnderwaterRenderer.ForUrpCamera(camera)._volumeGeometry;var centre=volume.transform.TransformPoint(volume.sharedMesh.bounds.center);renderer.transform.position=centre;camera.transform.LookAt(renderer.bounds.center);
                    File.AppendAllText(report,$"Transparent source sphere position={spherePosition}; probe inside original volume centre={centre}; volume local bounds={volume.sharedMesh.bounds}\n");
                    material.SetFloat("_FogMultiplier",1);var on=OriginalConfigurationCapture(camera,"Transparent-on");material.SetFloat("_FogMultiplier",0);var off=OriginalConfigurationCapture(camera,"Transparent-fog-off");material.SetFloat("_FogMultiplier",1);var restored=OriginalConfigurationCapture(camera,"Transparent-restored");
                    int changed=OriginalChanged(on,off);int restoration=on.Zip(restored,(a,b)=>a.Equals(b)?0:1).Sum();File.AppendAllText(report,$"Case={name}; SourceCamera={position}; ProbeCamera={camera.transform.position}; FogOffChangedPixels={changed}; RestoreChangedPixels={restoration}\n");Assert.That(changed,Is.GreaterThan(200));Assert.That(restoration,Is.Zero);
                }
                finally{renderer.sharedMaterial=original;Object.DestroyImmediate(material);camera.transform.position=position;camera.transform.rotation=rotation;renderer.transform.position=spherePosition;if(animator!=null)animator.enabled=animate;}
            }
            ocean.PopTimeProvider(clock);
        }
    }

}


public class UnderwaterLightingFillTests
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    RenderTexture _target;float _scale;int _capture;
    Material _referenceMaterial,_probeMaterial;Shader _referenceShader;ReferencePass _reference;
    GameObject _positive;List<MeshFilter> _fillMeshes;MeshFilter[] _originalMeshes;
    readonly List<RenderTexture> _resources=new List<RenderTexture>();
    sealed class ReferencePass : ICameraGraphPass
    {
        public Camera Camera;public RTHandle Colour,Depth,Front;public Material Material,Probe;public List<MeshFilter> Meshes;
        public RenderPassEvent Stage=>(RenderPassEvent)((int)RenderPassEvent.BeforeRenderingPrePasses+1);
        sealed class Data{public TextureHandle Colour,Depth,Front;public Material Material,Probe;public MeshFilter[] Meshes;public Matrix4x4 View,Projection;public int Width,Height;}
        public void Record(RenderGraph graph,ContextContainer frame)
        {
            var camera=frame.Get<UniversalCameraData>();if(camera.camera!=Camera)return;
            using var b=graph.AddUnsafePass<Data>("Test original fill shader reference",out var d);
            d.Colour=graph.ImportTexture(Colour);d.Depth=graph.ImportTexture(Depth);d.Front=graph.ImportTexture(Front);d.Material=Material;d.Probe=Probe;d.Meshes=Meshes.ToArray();
            d.View=camera.GetViewMatrix();d.Projection=camera.GetProjectionMatrix();d.Width=Colour.rt.width;d.Height=Colour.rt.height;
            b.UseTexture(d.Colour,AccessFlags.Write);b.UseTexture(d.Depth,AccessFlags.Write);b.UseTexture(d.Front,AccessFlags.Write);b.UseAllGlobalTextures(true);b.AllowGlobalStateModification(true);b.AllowPassCulling(false);
            b.SetRenderFunc((Data data,UnsafeGraphContext context)=>
            {
                var cmd=CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);cmd.SetViewport(new Rect(0,0,data.Width,data.Height));
                cmd.SetRenderTarget(data.Front);cmd.DrawProcedural(Matrix4x4.identity,data.Probe,0,MeshTopology.Triangles,3);
                cmd.SetRenderTarget(data.Colour,data.Depth);cmd.ClearRenderTarget(true,true,Color.black);cmd.SetViewProjectionMatrices(data.View,data.Projection);
                foreach(var mesh in data.Meshes)if(mesh!=null)cmd.DrawMesh(mesh.sharedMesh,mesh.transform.localToWorldMatrix,data.Material,0,0);
                cmd.SetViewProjectionMatrices(data.View,data.Projection);
            });
        }
    }
    string ExpandReference(string file)
    {
        string text=File.ReadAllText(file);
        return System.Text.RegularExpressions.Regex.Replace(text,@"(?m)^\s*#include\s+""([^""]+)""[^\r\n]*",m=>
        {string path=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file),m.Groups[1].Value));return File.Exists(path)?ExpandReference(path):m.Value;});
    }
    RenderTexture Resource(RenderTextureFormat format,int depth)
    {var t=new RenderTexture(640,360,depth,format,RenderTextureReadWrite.Linear);t.Create();_resources.Add(t);return t;}
    static float[] ReadMask(RenderTexture texture)
    {var r=AsyncGPUReadback.Request(texture,0,TextureFormat.RFloat);r.WaitForCompletion();Assert.That(r.hasError,Is.False);var values=r.GetData<float>().ToArray();Assert.That(values.All(float.IsFinite),Is.True);return values;}
    static int Changed(Color32[] a,Color32[] b)=>a.Zip(b,(x,y)=>Math.Abs(x.r-y.r)+Math.Abs(x.g-y.g)+Math.Abs(x.b-y.b)>6?1:0).Sum();
    [UnityTest,Timeout(180000)]
    public IEnumerator OriginalUnderwaterLightingFillProducerAndConsumers()
    {
        _scale=Time.timeScale;_capture=Time.captureFramerate;OceanRenderer.EnableGraphicsInBatchMode=true;
        EditorSceneManager.OpenScene("Assets/Crest/Crest-Examples/Examples/Scenes/Examples.unity");
        foreach(var c in Object.FindObjectsByType<ExamplesController>(FindObjectsInactive.Include,FindObjectsSortMode.None))c.enabled=false;
        yield return new EnterPlayMode();
        var controller=Object.FindObjectsByType<ExamplesController>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(c=>c.transform.childCount==24&&c.transform.GetChild(0).name=="Albedo");
        foreach(Transform child in controller.transform)child.gameObject.SetActive(false);
        var config=controller.transform.Cast<Transform>().Single(c=>c.name=="UnderwaterLighting");config.gameObject.SetActive(true);
        for(int i=0;i<45;i++)yield return null;
        var ocean=OceanRenderer.Instance;var camera=Camera.main;ocean.ViewCamera=camera;
        var animator=camera.GetComponent<Animator>();if(animator!=null)animator.enabled=false;
        var clock=new GameObject("Fill reference fixed clock").AddComponent<TimeProviderCustom>();clock._overrideTime=clock._overrideDeltaTime=clock._paused=true;clock._time=1;clock._deltaTime=0;ocean.PushTimeProvider(clock);
        Time.timeScale=0;camera.allowMSAA=false;camera.aspect=16f/9;camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        for(int i=0;i<16;i++)yield return null;
        string report="Evidence/P11-Fill-"+SystemInfo.graphicsDeviceType;
        var callbacks=config.GetComponentsInChildren<CustomPassForCamera>(true).Where(c=>
        {
            var ev=(UnityEventBase)typeof(CustomPassForCamera).GetField("_onExecute",Private).GetValue(c);
            return Enumerable.Range(0,ev.GetPersistentEventCount()).Any(i=>ev.GetPersistentMethodName(i)==nameof(MaskFill.ExecuteFillMask));
        }).ToArray();Assert.That(callbacks.Length,Is.EqualTo(1));
        var fill=config.GetComponentInChildren<MaskFill>(true);Assert.That(fill,Is.Not.Null);
        var meshes=(System.Collections.Generic.List<MeshFilter>)typeof(MaskFill).GetField("_meshes",Private).GetValue(fill);
        File.WriteAllText(report+".txt",$"Only original UnderwaterLighting config; callbackStage={callbacks[0].Stage}; FillMeshes={meshes.Count}; Camera={camera.transform.position}; Original fill shader reference +native front-depth/consumer control; four views including exact historical inside pose and explicit positive backface probe.\n");
        foreach(var mesh in meshes)File.AppendAllText(report+".txt",$"FillMesh={mesh.name}; Position={mesh.transform.position}; Bounds={mesh.sharedMesh.bounds}; Scale={mesh.transform.lossyScale}\n");
        foreach(var r in Object.FindObjectsByType<Renderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy))
            foreach(var m in r.sharedMaterials.Where(m=>m!=null))File.AppendAllText(report+".txt",$"Renderer={r.name}; Material={m.name}; Shader={m.shader.name}; Mask={(m.HasProperty("_Mask")?m.GetFloat("_Mask"):-1)}; FillKeyword={m.IsKeywordEnabled("_MASK_FILL")}; Bounds={r.bounds}\n");
        _target=new RenderTexture(640,360,24,RenderTextureFormat.ARGB32);_target.Create();
        string sourcePath=Path.GetFullPath("../../work/crest-upstream/crest/Assets/Crest/Crest-Examples/Examples/Shaders/Resources/MaskFill.shader");
        string code=ExpandReference(sourcePath).Replace("Shader \"Hidden/Crest/Examples/Mask Fill\"","Shader \"Hidden/Crest/Tests/OriginalFillReference\"");
        File.WriteAllText(report+"-reference.shader.txt",code);_referenceShader=ShaderUtil.CreateShaderAsset(code,false);_referenceMaterial=new Material(_referenceShader);ShaderUtil.CompilePass(_referenceMaterial,0,true);
        Assert.That(ShaderUtil.GetShaderMessages(_referenceShader).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error),Is.Empty);
        _probeMaterial=new Material(Shader.Find("Hidden/Crest/Tests/MaskTimingProbe"));_probeMaterial.SetInt("_ProbeField",1);
        _reference=new ReferencePass{Camera=camera,Colour=RTHandles.Alloc(Resource(RenderTextureFormat.RFloat,0)),Depth=RTHandles.Alloc(Resource(RenderTextureFormat.Depth,24)),Front=RTHandles.Alloc(Resource(RenderTextureFormat.RFloat,0)),Material=_referenceMaterial,Probe=_probeMaterial,Meshes=meshes};CameraGraphPassRegistry.Register(_reference);
        var originalPosition=camera.transform.position;var originalRotation=camera.transform.rotation;
        for(int pose=0;pose<4;pose++)
        {
            if(pose==3)camera.transform.position=new Vector3(0,-1,-3);
            else if(pose>0)camera.transform.position=new Vector3(camera.transform.position.x,pose==1?-3:-12,camera.transform.position.z);
            for(int i=0;i<8;i++)yield return null;
            var on=Capture(camera);var texture=(RenderTexture)typeof(MaskFill).GetField("_texture",Private).GetValue(fill);
            var data=AsyncGPUReadback.Request(texture,0,TextureFormat.RFloat);data.WaitForCompletion();Assert.That(data.hasError,Is.False);var values=data.GetData<float>().ToArray();
            int nonzero=values.Count(v=>v>0),ones=values.Count(v=>v==1);
            var reference=ReadMask(_reference.Colour.rt);var front=ReadMask(_reference.Front.rt);float error=values.Zip(reference,(a,b)=>Math.Abs(a-b)).Max();
            File.AppendAllText(report+".txt",$"ReferencePose={pose}; ReferenceNonzero={reference.Count(v=>v>0)}; NativeReferenceMaxError={error:R}; FrontNonzero={front.Count(v=>v>0)}\n");
            Assert.That(error,Is.LessThan(.000001f));
            if(pose<3)Assert.That(front.Count(v=>v>0),Is.GreaterThan(1000));
            else{Assert.That(UnderwaterRenderer.ForUrpCamera(camera)._mode,Is.EqualTo(UnderwaterRenderer.Mode.Volume));Assert.That(front.Count(v=>v>0),Is.Zero,"Volume mode has no front face when viewed from inside");}
            callbacks[0].enabled=false;var off=Capture(camera);callbacks[0].enabled=true;var restored=Capture(camera);
            int changed=on.Zip(off,(a,b)=>Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b)>6?1:0).Sum();
            File.AppendAllText(report+".txt",$"Pose={pose}; Camera={camera.transform.position}; NonzeroFill={nonzero}; Ones={ones}; FillMax={values.Max():R}; Changed={changed}; Restore={on.Zip(restored,(a,b)=>a.Equals(b)?0:1).Sum()}\n");
        }
        _fillMeshes=meshes;_originalMeshes=meshes.ToArray();
        _positive=GameObject.CreatePrimitive(PrimitiveType.Quad);_positive.name="Fill source-reference positive control";_positive.GetComponent<Renderer>().enabled=false;
        _positive.transform.position=Vector3.zero;_positive.transform.rotation=Quaternion.Euler(0,180,0);_positive.transform.localScale=Vector3.one*8;
        camera.transform.SetPositionAndRotation(originalPosition,originalRotation);meshes.Clear();meshes.Add(_positive.GetComponent<MeshFilter>());
        for(int i=0;i<8;i++)yield return null;
        var positive=Capture(camera);var native=ReadMask((RenderTexture)typeof(MaskFill).GetField("_texture",Private).GetValue(fill));var expected=ReadMask(_reference.Colour.rt);
        float positiveError=native.Zip(expected,(a,b)=>Math.Abs(a-b)).Max();int positivePixels=native.Count(v=>v>0);
        callbacks[0].enabled=false;var positiveOff=Capture(camera);callbacks[0].enabled=true;var positiveRestore=Capture(camera);
        int colourDelta=Changed(positive,positiveOff),restore=positive.Zip(positiveRestore,(a,b)=>a.Equals(b)?0:1).Sum();
        int deepOutsideChanges=0,interiorCompared=0;
        for(int y=1;y<359;y++)for(int x=1;x<639;x++)
        {
            int i=y*640+x;bool deepOutside=true;
            for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)if(native[(y+dy)*640+x+dx]!=0)deepOutside=false;
            if(!deepOutside)continue;interiorCompared++;
            if(Math.Abs(positive[i].r-positiveOff[i].r)+Math.Abs(positive[i].g-positiveOff[i].g)+Math.Abs(positive[i].b-positiveOff[i].b)>6)deepOutsideChanges++;
        }
        File.AppendAllText(report+".txt",$"OutsideMaskInteriorCompared={interiorCompared}; OutsideMaskChanged={deepOutsideChanges}\n");Assert.That(deepOutsideChanges,Is.Zero);
        File.AppendAllText(report+".txt",$"PositiveBackfaceMask={positivePixels}; ReferenceMaxError={positiveError:R}; FillConsumerChanged={colourDelta}; RestoredPixels={restore}\n");
        Assert.That(positivePixels,Is.GreaterThan(1000));Assert.That(positiveError,Is.LessThan(.000001f));Assert.That(colourDelta,Is.GreaterThan(200));Assert.That(restore,Is.Zero);
        meshes.Clear();meshes.AddRange(_originalMeshes);ocean.PopTimeProvider(clock);
    }
    Color32[] Capture(Camera camera)
    {
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=_target});var old=RenderTexture.active;var cpu=new Texture2D(640,360,TextureFormat.RGBA32,false);
        try{RenderTexture.active=_target;cpu.ReadPixels(new Rect(0,0,640,360),0,0);cpu.Apply();return cpu.GetPixels32();}
        finally{RenderTexture.active=old;Object.DestroyImmediate(cpu);}
    }
    [UnityTearDown]public IEnumerator Cleanup()
    {
        if(_fillMeshes!=null){_fillMeshes.Clear();_fillMeshes.AddRange(_originalMeshes.Where(m=>m!=null));}
        if(_reference!=null){CameraGraphPassRegistry.Unregister(_reference);_reference.Colour.Release();_reference.Depth.Release();_reference.Front.Release();}
        OceanRenderer.EnableGraphicsInBatchMode=false;if(Application.isPlaying)yield return new ExitPlayMode();
        foreach(var t in _resources)if(t!=null){t.Release();Object.DestroyImmediate(t);}_resources.Clear();
        if(_positive!=null)Object.DestroyImmediate(_positive);if(_referenceMaterial!=null)Object.DestroyImmediate(_referenceMaterial);if(_probeMaterial!=null)Object.DestroyImmediate(_probeMaterial);if(_referenceShader!=null)Object.DestroyImmediate(_referenceShader);
        if(_target!=null){_target.Release();Object.DestroyImmediate(_target);}EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Time.timeScale=_scale;Time.captureFramerate=_capture;
    }
}
