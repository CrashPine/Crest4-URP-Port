// Crest 4 URP port. MIT; see LICENSE.Crest.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Crest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
public class OriginalLakesRiversTests
{
    RenderTexture _target; string _stem; Vector3 _position; Quaternion _rotation; int _capture;float _scale;
    Vector4[] Read(RenderTexture texture,bool depth=false)
    {
        var read=AsyncGPUReadback.Request(texture,0,TextureFormat.RGBAFloat);read.WaitForCompletion();Assert.That(read.hasError,Is.False);Assert.That(read.layerCount,Is.EqualTo(7));
        var values=Enumerable.Range(0,read.layerCount).SelectMany(i=>read.GetData<Vector4>(i).ToArray()).ToArray();Assert.That(values.All(v=>(float.IsFinite(v.x)||(depth&&float.IsNegativeInfinity(v.x)))&&float.IsFinite(v.y)&&float.IsFinite(v.z)&&float.IsFinite(v.w)),Is.True);return values;
    }
    Color32[] Capture(Camera camera,string state)
    {
        camera.transform.SetPositionAndRotation(_position,_rotation);
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=_target});var previous=RenderTexture.active;var cpu=new Texture2D(640,360,TextureFormat.RGBA32,false);
        try{RenderTexture.active=_target;cpu.ReadPixels(new Rect(0,0,640,360),0,0);cpu.Apply();var pixels=cpu.GetPixels32();File.WriteAllBytes(_stem+"-"+state+".png",cpu.EncodeToPNG());Assert.That(pixels.Count(c=>c.r>245&&c.g<10&&c.b>245),Is.Zero);return pixels;}
        finally{RenderTexture.active=previous;Object.DestroyImmediate(cpu);}
    }
    [UnityTest,Timeout(240000)] public IEnumerator OriginalRiverHeightAndFlowInputsChangeAndRestore()
    {
        _capture=Time.captureFramerate;_scale=Time.timeScale;OceanRenderer.EnableGraphicsInBatchMode=true;EditorSceneManager.OpenScene("Assets/Crest/Crest-Examples/LakesAndRivers/Scenes/LakesAndRivers.unity");
        foreach(var shape in Object.FindObjectsByType<ShapeWaves>())shape.enabled=false;
        yield return new EnterPlayMode();Time.captureFramerate=60;Time.timeScale=1;_stem=$"Evidence/OriginalLakesRivers-{SystemInfo.graphicsDeviceType}";
        var ocean=OceanRenderer.Instance;var camera=Camera.main;Assert.That(ocean,Is.Not.Null);Assert.That(camera,Is.Not.Null);ocean.ViewCamera=camera;
        new GameObject("River query pacing").AddComponent<BoatProbesQueryFence>();for(int i=0;i<60;i++)yield return null;
        var heights=Object.FindObjectsByType<RegisterHeightInput>();var flows=Object.FindObjectsByType<RegisterFlowInput>();Assert.That(heights.Length,Is.GreaterThan(0));Assert.That(flows.Length,Is.GreaterThan(0));
        var clock=new GameObject("River fixed ocean clock").AddComponent<TimeProviderCustom>();clock._overrideTime=clock._overrideDeltaTime=clock._paused=true;clock._time=1;clock._deltaTime=0;ocean.PushTimeProvider(clock);Time.timeScale=0;
        camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;camera.allowMSAA=false;camera.aspect=16f/9;
        for(int i=0;i<16;i++)yield return null;_position=camera.transform.position;_rotation=camera.transform.rotation;
        File.WriteAllText(_stem+".txt",$"Original LakesAndRivers source camera={_position}; Heights={heights.Length}; Flow={flows.Length}; Terrain={Object.FindObjectsByType<Terrain>().Length}; Bodies={Object.FindObjectsByType<WaterBody>().Length}; Clip={ocean.CreateClipSurfaceData}. Original input names="+string.Join(";",heights.Select(h=>h.name))+" / "+string.Join(";",flows.Select(f=>f.name))+". Other ShapeWaves off only in fixture; original terrain/depth retained. Ocean clock1/delta0 +Unity timeScale0, capture60Hz/query fence, depth on/postprocess/MSAA off.\n");
        _target=new RenderTexture(640,360,24,RenderTextureFormat.ARGB32);_target.Create();
        var onW=Read(ocean._lodDataSeaDepths.DataTexture,true);var onF=Read(ocean._lodDataFlow.DataTexture);var on=Capture(camera,"on");
        foreach(var h in heights)h.enabled=false;foreach(var f in flows)f.enabled=false;for(int i=0;i<16;i++)yield return null;
        var offW=Read(ocean._lodDataSeaDepths.DataTexture,true);var offF=Read(ocean._lodDataFlow.DataTexture);var off=Capture(camera,"off");
        foreach(var h in heights)h.enabled=true;foreach(var f in flows)f.enabled=true;for(int i=0;i<16;i++)yield return null;
        var reW=Read(ocean._lodDataSeaDepths.DataTexture,true);var reF=Read(ocean._lodDataFlow.DataTexture);var restored=Capture(camera,"restored");
        double heightDelta=onW.Zip(offW,(a,b)=>(double)Math.Abs(a.y-b.y)).Sum();double flowDelta=onF.Zip(offF,(a,b)=>(double)(Math.Abs(a.x-b.x)+Math.Abs(a.y-b.y))).Sum();float heightRestore=onW.Zip(reW,(a,b)=>Math.Abs(a.y-b.y)).Max();float flowRestore=onF.Zip(reF,(a,b)=>(a-b).magnitude).Max();int changed=on.Zip(off,(a,b)=>Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b)>6?1:0).Sum();int restore=on.Zip(restored,(a,b)=>a.Equals(b)?0:1).Sum();
        File.AppendAllText(_stem+".txt",$"Samples={onW.Length}; HeightDelta={heightDelta:R}; FlowDelta={flowDelta:R}; HeightRestoreMax={heightRestore:R}; FlowRestoreMax={flowRestore:R}; ChangedPixels={changed}; RestorePixels={restore}\n");
        Assert.That(heightDelta,Is.GreaterThan(1));Assert.That(flowDelta,Is.GreaterThan(1));Assert.That(heightRestore,Is.LessThan(.002));Assert.That(flowRestore,Is.LessThan(.002));Assert.That(changed,Is.GreaterThan(200));Assert.That(restore,Is.Zero);ocean.PopTimeProvider(clock);
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        foreach(var receiver in Object.FindObjectsByType<MonoBehaviour>().Where(m=>m is Crest.Spline.IReceiveSplineChangeMessages).ToArray())Object.DestroyImmediate(receiver);
        OceanRenderer.EnableGraphicsInBatchMode=false;foreach(var h in Object.FindObjectsByType<RegisterHeightInput>())h.enabled=false;foreach(var f in Object.FindObjectsByType<RegisterFlowInput>())f.enabled=false;if(Application.isPlaying)yield return new ExitPlayMode();foreach(var receiver in Object.FindObjectsByType<MonoBehaviour>().Where(m=>m is Crest.Spline.IReceiveSplineChangeMessages).ToArray())Object.DestroyImmediate(receiver);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Time.captureFramerate=_capture;Time.timeScale=_scale;if(_target!=null){_target.Release();Object.DestroyImmediate(_target);}
    }
}
