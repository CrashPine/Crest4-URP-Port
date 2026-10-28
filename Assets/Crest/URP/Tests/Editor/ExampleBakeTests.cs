// Crest 4 URP port. MIT; see LICENSE.Crest.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
public class ExampleBakeTests
{
    const string Folder="Assets/CrestBakeFixture";bool _owned;RenderTexture _target;string _report;
    [UnityTest,Timeout(600000)] public IEnumerator SourceExampleMaterialsReceiveActualBakedLighting()
    {
        Assert.That(AssetDatabase.IsValidFolder(Folder),Is.False,"Do not overwrite an existing fixture folder");AssetDatabase.CreateFolder("Assets","CrestBakeFixture");_owned=true;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        _report=$"Evidence/ExampleBake-{SystemInfo.graphicsDeviceType}.txt";File.WriteAllText(_report,"Disposable saved Editor scene, real Lightmapping.BakeAsync, original source material clones and GI/pass flags. Masked clones use view-mask None for the dry fixture; original GI/pass/colour/texture flags retained. No original scene rebake or original renderer flag acceptance.\n");
        var settings=new LightingSettings();settings.bakedGI=true;settings.realtimeGI=false;settings.lightmapper=LightingSettings.Lightmapper.ProgressiveCPU;settings.lightmapResolution=8;settings.lightmapMaxSize=256;settings.directSampleCount=16;settings.indirectSampleCount=16;settings.environmentSampleCount=16;settings.bounces=1;settings.filteringMode=LightingSettings.FilterMode.None;
        AssetDatabase.CreateAsset(settings,Folder+"/Lighting.asset");Lightmapping.lightingSettings=settings;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.black;RenderSettings.ambientIntensity=0;RenderSettings.skybox=null;RenderSettings.reflectionIntensity=0;
        var renderers=new List<Renderer>();
        foreach(var entry in new[]{("Examples_Material_MaskedWhite",-2f),("Examples_Material_MaskedNavalMine",0f),("Examples_Material_TransparentWithUnderwaterFog",2f)})
        {
            string path="Assets/Crest/Crest-Examples/Examples/Materials/"+entry.Item1+".mat";var source=AssetDatabase.LoadAssetAtPath<Material>(path);Assert.That(source,Is.Not.Null);var material=new Material(source);Assert.That(material.globalIlluminationFlags,Is.EqualTo(source.globalIlluminationFlags));
            if(material.HasProperty("_Mask")){material.DisableKeyword("_MASK_MASKED");material.DisableKeyword("_MASK_FILL");material.EnableKeyword("_MASK_NONE");material.SetFloat("_Mask",0);}
            AssetDatabase.CreateAsset(material,Folder+"/"+entry.Item1+".mat");File.AppendAllText(_report,$"Source={path}; Shader={source.shader.name}; SourceGI={source.globalIlluminationFlags}; CloneGI={material.globalIlluminationFlags}; Meta={material.FindPass("Meta")}; ShadowEnabled={material.GetShaderPassEnabled("ShadowCaster")}\n");
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.name=entry.Item1;cube.transform.position=new Vector3(entry.Item2,.5f,0);var mesh=Object.Instantiate(cube.GetComponent<MeshFilter>().sharedMesh);Unwrapping.GenerateSecondaryUVSet(mesh);AssetDatabase.CreateAsset(mesh,Folder+"/"+entry.Item1+".asset");cube.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=cube.GetComponent<Renderer>();renderer.sharedMaterial=material;GameObjectUtility.SetStaticEditorFlags(cube,StaticEditorFlags.ContributeGI);renderers.Add(renderer);
        }
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Lit floor reference";floor.transform.position=new Vector3(0,-.1f,0);floor.transform.localScale=new Vector3(9,.2f,6);var floorMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));floorMaterial.SetColor("_BaseColor",Color.white);AssetDatabase.CreateAsset(floorMaterial,Folder+"/Floor.mat");floor.GetComponent<Renderer>().sharedMaterial=floorMaterial;var fm=Object.Instantiate(floor.GetComponent<MeshFilter>().sharedMesh);Unwrapping.GenerateSecondaryUVSet(fm);AssetDatabase.CreateAsset(fm,Folder+"/Floor.asset");floor.GetComponent<MeshFilter>().sharedMesh=fm;GameObjectUtility.SetStaticEditorFlags(floor,StaticEditorFlags.ContributeGI);renderers.Add(floor.GetComponent<Renderer>());
        var sun=new GameObject("Baked sun",typeof(Light)).GetComponent<Light>();sun.type=LightType.Directional;sun.intensity=2;sun.lightmapBakeType=LightmapBakeType.Baked;sun.shadows=LightShadows.Hard;sun.transform.rotation=Quaternion.Euler(55,-30,0);RenderSettings.sun=sun;
        var camera=new GameObject("Bake camera",typeof(Camera)).GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.transform.position=new Vector3(6,6,-9);camera.transform.LookAt(new Vector3(0,.5f,0));camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        _target=new RenderTexture(640,360,24);_target.Create();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=_target});
        Assert.That(EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Folder+"/Bake.unity"),Is.True);
        Assert.That(Lightmapping.BakeAsync(),Is.True,"Actual lightmapper must start");while(Lightmapping.isRunning)yield return null;yield return null;
        File.AppendAllText(_report,$"Backend={settings.lightmapper}; Atlases={LightmapSettings.lightmaps.Length}; LightingData={Lightmapping.lightingDataAsset!=null}\n");Assert.That(LightmapSettings.lightmaps.Length,Is.GreaterThan(0));
        foreach(var renderer in renderers){File.AppendAllText(_report,$"Renderer={renderer.name}; LightmapIndex={renderer.lightmapIndex}; ST={renderer.lightmapScaleOffset}; ShadowCasting={renderer.shadowCastingMode}\n");Assert.That(renderer.lightmapIndex,Is.InRange(0,LightmapSettings.lightmaps.Length-1));}
        sun.enabled=false;
        Color32[] Capture(string state,bool save=true)
        {
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=_target});var previous=RenderTexture.active;var cpu=new Texture2D(640,360,TextureFormat.RGBA32,false);
            try{RenderTexture.active=_target;cpu.ReadPixels(new Rect(0,0,640,360),0,0);cpu.Apply();var pixels=cpu.GetPixels32();if(save)File.WriteAllBytes($"Evidence/ExampleBake-{state}-{SystemInfo.graphicsDeviceType}.png",cpu.EncodeToPNG());Assert.That(pixels.Count(c=>c.r>245&&c.g<10&&c.b>245),Is.Zero);return pixels;}
            finally{RenderTexture.active=previous;Object.DestroyImmediate(cpu);}
        }
        var on=Capture("on");var indices=renderers.Select(r=>r.lightmapIndex).ToArray();Color32[] off;
        try{foreach(var renderer in renderers)renderer.lightmapIndex=-1;off=Capture("binding-off");}finally{for(int i=0;i<renderers.Count;i++)renderers[i].lightmapIndex=indices[i];}
        foreach(var renderer in renderers.Take(3))
        {
            int index=renderer.lightmapIndex;Color32[] individual;
            try{renderer.lightmapIndex=-1;individual=Capture(renderer.name+"-map-off",false);}finally{renderer.lightmapIndex=index;}
            int changedOne=on.Zip(individual,(a,b)=>Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b)>6?1:0).Sum();var restoredOne=Capture(renderer.name+"-restored",false);
            File.AppendAllText(_report,$"Individual={renderer.name}; MapOffChanged={changedOne}; RestoreExact={on.SequenceEqual(restoredOne)}\n");Assert.That(changedOne,Is.GreaterThan(20));Assert.That(restoredOne,Is.EqualTo(on));
        }
        var restored=Capture("restored");int changed=on.Zip(off,(a,b)=>Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b)>6?1:0).Sum();int restore=on.Zip(restored,(a,b)=>a.Equals(b)?0:1).Sum();int bright=on.Count(c=>Math.Max(c.r,Math.Max(c.g,c.b))>30);
        File.AppendAllText(_report,$"RealtimeLightOff=True; BackgroundBlack=True; BrightPixels={bright}; LightmapBindingOffChangedPixels={changed}; RestoreChangedPixels={restore}\n");Assert.That(bright,Is.GreaterThan(5000));Assert.That(changed,Is.GreaterThan(1000));Assert.That(restore,Is.Zero);
    }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(Lightmapping.isRunning)Lightmapping.Cancel();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);if(_target!=null){_target.Release();Object.DestroyImmediate(_target);}if(_owned){Assert.That(AssetDatabase.DeleteAsset(Folder),Is.True);_owned=false;}yield return null;
    }
}
