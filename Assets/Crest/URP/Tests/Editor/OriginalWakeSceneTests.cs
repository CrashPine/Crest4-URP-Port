// Crest 4 URP port. MIT; see LICENSE.Crest.
using System.Collections;
using System.IO;
using System.Linq;
using Crest;
using Crest.Examples;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
public class OriginalWakeSceneTests
{
    int _capture; float _scale; RenderTexture _target;
    [UnityTest,Timeout(300000)]
    public IEnumerator OriginalWakeRigGeneratesNativeWavesWithSourceOffControl()
    {
        _capture=Time.captureFramerate;_scale=Time.timeScale;
        string report=$"Evidence/OriginalWakes-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(report,"Original BoatWakes.unity. Independent PlayMode lifecycles off/on/off; actual SimpleMotion and five SphereWaterInteraction. Live native simulation, capture60Hz/query fence; not performance or exact API parity.\n");
        foreach(bool active in new[]{false,true,false})
        {
            OceanRenderer.EnableGraphicsInBatchMode=true;EditorSceneManager.OpenScene("Assets/Crest/Crest-Examples/BoatDev/Scenes/BoatWakes.unity");
            var sources=Object.FindObjectsByType<SphereWaterInteraction>();Assert.That(sources.Length,Is.EqualTo(8));Assert.That(sources.Count(x=>x.transform.parent.name=="WakeRig"),Is.EqualTo(5));File.AppendAllText(report,"Sources="+string.Join(";",sources.Select(x=>x.name+":"+x._radius+":"+x._weight))+"\n");
            foreach(var source in sources)source.enabled=active;
            yield return new EnterPlayMode();Time.captureFramerate=60;Time.timeScale=1;
            var ocean=OceanRenderer.Instance;Assert.That(ocean,Is.Not.Null);Assert.That(ocean._lodDataDynWaves,Is.Not.Null);
            new GameObject("Wake test GPU query pacing").AddComponent<BoatProbesQueryFence>();
            var rig=Object.FindObjectsByType<SimpleMotion>().Single(x=>x.name=="WakeRig");var start=rig.transform.position;
            var camera=Camera.main;Assert.That(camera,Is.Not.Null);ocean.ViewCamera=camera;
            camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;camera.allowMSAA=false;camera.aspect=16f/9;
            for(int i=0;i<120;i++)yield return null;
            float distance=Vector3.Distance(start,rig.transform.position);Assert.That(distance,Is.GreaterThan(10));
            var request=AsyncGPUReadback.Request(ocean._lodDataDynWaves.DataTexture,0,TextureFormat.RGFloat);request.WaitForCompletion();Assert.That(request.hasError,Is.False);
            double energy=0;int nonzero=0;
            for(int layer=0;layer<request.layerCount;layer++)
            {
                double sum=0;int count=0;
                foreach(var value in request.GetData<Vector2>(layer)){Assert.That(float.IsFinite(value.x)&&float.IsFinite(value.y),Is.True);sum+=System.Math.Abs(value.x)+System.Math.Abs(value.y);if(value.sqrMagnitude>1e-12f)count++;}
                energy+=sum;nonzero+=count;File.AppendAllText(report,$"Enabled={active}; Layer={layer}; AbsSum={sum:R}; Nonzero={count}\n");
            }
            File.AppendAllText(report,$"Enabled={active}; Distance={distance:R}; Layers={request.layerCount}; AbsSum={energy:R}; Nonzero={nonzero}\n");
            Assert.That(request.layerCount,Is.EqualTo(7));
            if(active){Assert.That(energy,Is.GreaterThan(.001));Assert.That(nonzero,Is.GreaterThan(100));}
            else Assert.That(energy,Is.LessThan(1e-8),"No dynamic wave output without the original sphere inputs");
            _target=new RenderTexture(640,360,24,RenderTextureFormat.ARGB32);_target.Create();
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=_target});
            var previous=RenderTexture.active;var cpu=new Texture2D(640,360,TextureFormat.RGBA32,false);
            try{RenderTexture.active=_target;cpu.ReadPixels(new Rect(0,0,640,360),0,0);cpu.Apply();int pink=cpu.GetPixels32().Count(c=>c.r>245&&c.g<10&&c.b>245);File.WriteAllBytes($"Evidence/OriginalWakes-{active}-{SystemInfo.graphicsDeviceType}.png",cpu.EncodeToPNG());File.AppendAllText(report,$"Pink={pink}\n");Assert.That(pink,Is.Zero);}
            finally{RenderTexture.active=previous;Object.DestroyImmediate(cpu);_target.Release();Object.DestroyImmediate(_target);_target=null;}
            yield return new ExitPlayMode();
        }
    }
    [UnityTest,Timeout(180000)]
    public IEnumerator SourceCameraGeometryVisibilityAndRestoration()
    {
        _capture=Time.captureFramerate;_scale=Time.timeScale;
        for(int cycle=0;cycle<3;cycle++)
        {
        OceanRenderer.EnableGraphicsInBatchMode=true;
        EditorSceneManager.OpenScene("Assets/Crest/Crest-Examples/BoatDev/Scenes/BoatWakes.unity");
        foreach(var source in Object.FindObjectsByType<SphereWaterInteraction>())source.enabled=cycle==1;
        yield return new EnterPlayMode();Time.captureFramerate=60;Time.timeScale=1;
        var camera=Camera.main;var ocean=OceanRenderer.Instance;ocean.ViewCamera=camera;
        new GameObject("Visibility query pacing").AddComponent<BoatProbesQueryFence>();
        string stem=$"Evidence/OriginalWakes-Visibility-toggle-cycle{cycle}-{SystemInfo.graphicsDeviceType}";
        File.WriteAllText(stem+".txt",$"StartCamera={camera.transform.position}; Rotation={camera.transform.eulerAngles}; Components="+string.Join(";",camera.GetComponents<MonoBehaviour>().Select(c=>c.GetType().Name))+"\n");
        camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;camera.allowMSAA=false;camera.aspect=16f/9;
        for(int i=0;i<120;i++)yield return null;
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var renderers=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Renderer>()).Where(r=>r.GetComponentInParent<OceanRenderer>()==null).ToArray();
        var planes=GeometryUtility.CalculateFrustumPlanes(camera);
        File.AppendAllText(stem+".txt",$"EndCamera={camera.transform.position}; Rotation={camera.transform.eulerAngles}; Mask={camera.cullingMask}; Near={camera.nearClipPlane}; Far={camera.farClipPlane}\n");
        foreach(var r in renderers)File.AppendAllText(stem+".txt",$"Renderer={r.name}; Enabled={r.enabled}; ForceOff={r.forceRenderingOff}; Layer={r.gameObject.layer}; Bounds={r.bounds}; InFrustum={GeometryUtility.TestPlanesAABB(planes,r.bounds)}\n");
        _target=new RenderTexture(640,360,24,RenderTextureFormat.ARGB32);_target.Create();
        Color32[] Capture(string state)
        {
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=_target});
            var old=RenderTexture.active;var cpu=new Texture2D(640,360,TextureFormat.RGBA32,false);
            try{RenderTexture.active=_target;cpu.ReadPixels(new Rect(0,0,640,360),0,0);cpu.Apply();var pixels=cpu.GetPixels32();File.WriteAllBytes(stem+"-"+state+".png",cpu.EncodeToPNG());Assert.That(pixels.Count(c=>c.r>245&&c.g<10&&c.b>245),Is.Zero);return pixels;}
            finally{RenderTexture.active=old;Object.DestroyImmediate(cpu);}
        }
        var on=Capture("on");var states=renderers.Select(r=>r.forceRenderingOff).ToArray();Color32[] off;
        try{foreach(var r in renderers)r.forceRenderingOff=true;off=Capture("off");}
        finally{for(int i=0;i<renderers.Length;i++)renderers[i].forceRenderingOff=states[i];}
        var restored=Capture("restored");int changed=on.Zip(off,(a,b)=>System.Math.Abs(a.r-b.r)+System.Math.Abs(a.g-b.g)+System.Math.Abs(a.b-b.b)>6?1:0).Sum();int restore=on.Zip(restored,(a,b)=>a.Equals(b)?0:1).Sum();
        File.AppendAllText(stem+".txt",$"GeometryChanged={changed}; RestoreChanged={restore}\n");Assert.That(changed,Is.GreaterThan(200));Assert.That(restore,Is.Zero);
        _target.Release();Object.DestroyImmediate(_target);_target=null;yield return new ExitPlayMode();
        }
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        OceanRenderer.EnableGraphicsInBatchMode=false;if(Application.isPlaying)yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Time.captureFramerate=_capture;Time.timeScale=_scale;
        if(_target!=null){_target.Release();Object.DestroyImmediate(_target);}
    }
}
