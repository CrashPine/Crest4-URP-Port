// Crest 4 URP port. MIT; see LICENSE.Crest.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Crest;
using Crest.Examples;
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
