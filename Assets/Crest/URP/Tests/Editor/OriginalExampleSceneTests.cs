// Crest 4 URP port. MIT; see LICENSE.Crest.
using System.Collections;
using System.IO;
using System.Linq;
using Crest;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public class OriginalExampleSceneTests
{
    RenderTexture _target;
    [UnityTest,Timeout(180000)]
    public IEnumerator MainExampleRendersVisibleGeometryWithoutErrorShader()
    {
        OceanRenderer.EnableGraphicsInBatchMode=true;
        EditorSceneManager.OpenScene("Assets/Crest/Crest-Examples/Main/Scenes/main.unity");
        yield return new EnterPlayMode();
        Assert.That(Application.isPlaying,Is.True);
        for(int i=0;i<90;i++)yield return null;
        var ocean=OceanRenderer.Instance;var camera=Camera.main;
        Assert.That(ocean,Is.Not.Null);Assert.That(camera,Is.Not.Null);
        ocean.ViewCamera=camera;
        foreach(var cam in Object.FindObjectsByType<Camera>())cam.enabled=false;
        camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        camera.allowMSAA=false;camera.aspect=16f/9;
        _target=new RenderTexture(640,360,24,RenderTextureFormat.ARGB32);_target.Create();
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=_target});
        var previous=RenderTexture.active;var cpu=new Texture2D(640,360,TextureFormat.RGBA32,false);
        try
        {
            RenderTexture.active=_target;cpu.ReadPixels(new Rect(0,0,640,360),0,0);cpu.Apply();
            var pixels=cpu.GetPixels32();int pink=pixels.Count(x=>x.r>245&&x.g<10&&x.b>245);
            int nonBackground=pixels.Count(x=>x.r!=pixels[0].r||x.g!=pixels[0].g||x.b!=pixels[0].b);
            var renderers=Object.FindObjectsByType<Renderer>();
            var shaderNames=renderers.SelectMany(x=>x.sharedMaterials).Where(x=>x!=null).Select(x=>x.name+":"+x.shader.name).Distinct().OrderBy(x=>x).ToArray();
            string stem=$"Evidence/OriginalMain-{SystemInfo.graphicsDeviceType}";
            File.WriteAllBytes(stem+".png",cpu.EncodeToPNG());
            File.WriteAllText(stem+".txt",$"Unity={Application.unityVersion}; Playing={Application.isPlaying}; Frame={Time.frameCount}; Renderers={renderers.Length}; CameraPosition={camera.transform.position}; OceanScale={ocean.Scale}; ErrorPinkPixels={pink}; NonBackgroundPixels={nonBackground}\n"+string.Join("\n",shaderNames)+"\n");
            Assert.That(nonBackground,Is.GreaterThan(10000),"Capture must contain visible original scene geometry/ocean.");
            Assert.That(pink,Is.Zero,"Original main scene contains the URP error-shader colour.");
            // Prove that the migrated original island is visible, not merely absent.
            var states=renderers.Select(x=>x.forceRenderingOff).ToArray();Color32[] hidden;
            try
            {
                foreach(var renderer in renderers)renderer.forceRenderingOff=true;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=_target});
                RenderTexture.active=_target;cpu.ReadPixels(new Rect(0,0,640,360),0,0);cpu.Apply();hidden=cpu.GetPixels32();
                File.WriteAllBytes(stem+"-geometry-off.png",cpu.EncodeToPNG());
            }
            finally {for(int i=0;i<renderers.Length;i++)renderers[i].forceRenderingOff=states[i];}
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=_target});
            RenderTexture.active=_target;cpu.ReadPixels(new Rect(0,0,640,360),0,0);cpu.Apply();var restored=cpu.GetPixels32();
            int visible=pixels.Zip(hidden,(a,b)=>System.Math.Abs(a.r-b.r)+System.Math.Abs(a.g-b.g)+System.Math.Abs(a.b-b.b)>6?1:0).Sum();
            int repeat=pixels.Zip(restored,(a,b)=>a.Equals(b)?0:1).Sum();
            File.AppendAllText(stem+".txt",$"VisibleOriginalGeometryChangedPixels={visible}; GeometryRestoredChangedPixels={repeat}\n");
            Assert.That(visible,Is.GreaterThan(1000));Assert.That(repeat,Is.Zero);

        }
        finally {RenderTexture.active=previous;Object.DestroyImmediate(cpu);}
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        OceanRenderer.EnableGraphicsInBatchMode=false;
        if(Application.isPlaying)yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        if(_target!=null){_target.Release();Object.DestroyImmediate(_target);}
    }
}
