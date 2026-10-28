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
public class OriginalWhirlpoolTests
{
    RenderTexture _target; string _stem; Vector3 _position; Quaternion _rotation; int _capture;float _scale;
    Vector4[] Read(RenderTexture texture)
    {
        var read=AsyncGPUReadback.Request(texture,0,TextureFormat.RGBAFloat);read.WaitForCompletion();Assert.That(read.hasError,Is.False);Assert.That(read.layerCount,Is.EqualTo(7));
        var values=Enumerable.Range(0,read.layerCount).SelectMany(i=>read.GetData<Vector4>(i).ToArray()).ToArray();Assert.That(values.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)&&float.IsFinite(v.w)),Is.True);return values;
    }
    Color32[] Capture(Camera camera,string state)
    {
        camera.transform.SetPositionAndRotation(_position,_rotation);
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=_target});var previous=RenderTexture.active;var cpu=new Texture2D(640,360,TextureFormat.RGBA32,false);
        try{RenderTexture.active=_target;cpu.ReadPixels(new Rect(0,0,640,360),0,0);cpu.Apply();var pixels=cpu.GetPixels32();File.WriteAllBytes(_stem+"-"+state+".png",cpu.EncodeToPNG());Assert.That(pixels.Count(c=>c.r>245&&c.g<10&&c.b>245),Is.Zero);return pixels;}
        finally{RenderTexture.active=previous;Object.DestroyImmediate(cpu);}
    }
    [UnityTest,Timeout(240000)] public IEnumerator NativeWhirlpoolDisplacementAndFlowInputsChangeAndRestore()
    {
        _capture=Time.captureFramerate;_scale=Time.timeScale;OceanRenderer.EnableGraphicsInBatchMode=true;EditorSceneManager.OpenScene("Assets/Crest/Crest-Examples/Whirlpool/Scenes/whirlpool.unity");
        foreach(var shape in Object.FindObjectsByType<ShapeWaves>())shape.enabled=false;
        var setupOcean=Object.FindObjectsByType<OceanRenderer>().Single();var serialized=new SerializedObject(setupOcean);serialized.FindProperty("_createDynamicWaveSim").boolValue=false;serialized.FindProperty("_createFoamSim").boolValue=false;serialized.ApplyModifiedPropertiesWithoutUndo();
        yield return new EnterPlayMode();Time.captureFramerate=60;Time.timeScale=1;_stem=$"Evidence/OriginalWhirlpool-{SystemInfo.graphicsDeviceType}";
        var ocean=OceanRenderer.Instance;var camera=Camera.main;ocean.ViewCamera=camera;var whirl=Object.FindObjectsByType<Whirlpool>().Single();
        new GameObject("Whirlpool query pacing").AddComponent<BoatProbesQueryFence>();
        for(int i=0;i<60;i++)yield return null;
        var clock=new GameObject("Whirlpool fixed ocean clock").AddComponent<TimeProviderCustom>();clock._overrideTime=clock._overrideDeltaTime=clock._paused=true;clock._time=1;clock._deltaTime=0;ocean.PushTimeProvider(clock);
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var displacement=((GameObject)typeof(Whirlpool).GetField("_displacementInput",flags).GetValue(whirl)).GetComponent<RegisterAnimWavesInput>();
        var flow=((GameObject)typeof(Whirlpool).GetField("_flowInput",flags).GetValue(whirl)).GetComponent<RegisterFlowInput>();Assert.That(displacement.enabled&&flow.enabled,Is.True);
        camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;camera.allowMSAA=false;camera.aspect=16f/9;
        Time.timeScale=0;for(int i=0;i<16;i++)yield return null;_position=camera.transform.position;_rotation=camera.transform.rotation;
        File.WriteAllText(_stem+".txt",$"Original whirlpool.unity; native inputs on/off/on, ocean clock1/delta0 after60 live frames, Unity timeScale0 for captures, capture60Hz/query fence. Camera={_position}; component={whirl.transform.position}; fixed original camera pose for captures; depth on postprocess/MSAA off. Other ShapeWaves and persistent dynamic waves/foam disabled beforePlay to isolate displacement/flow; no standalone/performance/history-decay/dampening acceptance.\n");
        _target=new RenderTexture(640,360,24,RenderTextureFormat.ARGB32);_target.Create();
        var onWaves=Read(ocean._lodDataAnimWaves.DataTexture);var onFlow=Read(ocean._lodDataFlow.DataTexture);var on=Capture(camera,"on");
        displacement.enabled=false;flow.enabled=false;for(int i=0;i<16;i++)yield return null;
        var offWaves=Read(ocean._lodDataAnimWaves.DataTexture);var offFlow=Read(ocean._lodDataFlow.DataTexture);var off=Capture(camera,"off");
        displacement.enabled=true;flow.enabled=true;for(int i=0;i<16;i++)yield return null;
        var restoredWaves=Read(ocean._lodDataAnimWaves.DataTexture);var restoredFlow=Read(ocean._lodDataFlow.DataTexture);var restored=Capture(camera,"restored");
        double waveDelta=onWaves.Zip(offWaves,(a,b)=>Math.Abs(a.y-b.y)).Sum();double flowDelta=onFlow.Zip(offFlow,(a,b)=>Math.Abs(a.x-b.x)+Math.Abs(a.y-b.y)).Sum();
        float waveRestore=onWaves.Zip(restoredWaves,(a,b)=>(a-b).magnitude).Max();float flowRestore=onFlow.Zip(restoredFlow,(a,b)=>(a-b).magnitude).Max();int changed=on.Zip(off,(a,b)=>Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b)>6?1:0).Sum();int restore=on.Zip(restored,(a,b)=>a.Equals(b)?0:1).Sum();
        File.AppendAllText(_stem+".txt",$"Samples={onWaves.Length}; WaveHeightDeltaSum={waveDelta:R}; FlowDeltaSum={flowDelta:R}; WaveRestoreMax={waveRestore:R}; FlowRestoreMax={flowRestore:R}; ChangedPixels={changed}; RestorePixels={restore}\n");
        Assert.That(waveDelta,Is.GreaterThan(1));Assert.That(flowDelta,Is.GreaterThan(1));Assert.That(waveRestore,Is.LessThan(.002));Assert.That(flowRestore,Is.LessThan(.002));Assert.That(changed,Is.GreaterThan(200));Assert.That(restore,Is.Zero);ocean.PopTimeProvider(clock);
    }
    [UnityTest,Timeout(180000)] public IEnumerator OriginalCombinedWhirlpoolSceneRendersWithNativeSimulations()
    {
        _capture=Time.captureFramerate;_scale=Time.timeScale;OceanRenderer.EnableGraphicsInBatchMode=true;EditorSceneManager.OpenScene("Assets/Crest/Crest-Examples/Whirlpool/Scenes/whirlpool.unity");
        yield return new EnterPlayMode();Time.captureFramerate=60;Time.timeScale=1;
        var ocean=OceanRenderer.Instance;var camera=Camera.main;ocean.ViewCamera=camera;
        Assert.That(ocean.CreateDynamicWaveSim&&ocean.CreateFoamSim,Is.True);
        new GameObject("Combined Whirlpool query pacing").AddComponent<BoatProbesQueryFence>();
        for(int i=0;i<120;i++)yield return null;
        _stem=$"Evidence/OriginalWhirlpool-Combined-{SystemInfo.graphicsDeviceType}";_position=camera.transform.position;_rotation=camera.transform.rotation;
        camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;camera.allowMSAA=false;camera.aspect=16f/9;
        var waves=Read(ocean._lodDataAnimWaves.DataTexture);var flow=Read(ocean._lodDataFlow.DataTexture);
        double waveSum=waves.Sum(v=>(double)Math.Abs(v.y));double flowSum=flow.Sum(v=>(double)(Math.Abs(v.x)+Math.Abs(v.y)));Assert.That(waveSum,Is.GreaterThan(1));Assert.That(flowSum,Is.GreaterThan(1));
        _target=new RenderTexture(640,360,24,RenderTextureFormat.ARGB32);_target.Create();var pixels=Capture(camera,"native");int nonbackground=pixels.Count(p=>!p.Equals(pixels[0]));Assert.That(nonbackground,Is.GreaterThan(5000));
        File.WriteAllText(_stem+".txt",$"Original scene combined live120 frames, no source simulation toggles/clock freeze. Source camera depth on/postprocess+MSAA off640x360. All7 layers finite; WaveAbsY={waveSum:R}; FlowAbsXY={flowSum:R}; NonBackground={nonbackground}; Dynamic=True; Foam=True. Pink0. Smoke only, not numerical history/dampening/bake/standalone acceptance.\n");
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        OceanRenderer.EnableGraphicsInBatchMode=false;if(Application.isPlaying)yield return new ExitPlayMode();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Time.captureFramerate=_capture;Time.timeScale=_scale;if(_target!=null){_target.Release();Object.DestroyImmediate(_target);}
    }
}
