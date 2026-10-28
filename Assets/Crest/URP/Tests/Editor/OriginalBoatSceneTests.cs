// Crest 4 URP port. MIT; see LICENSE.Crest.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Crest;
using Crest.Examples;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public class OriginalBoatSceneTests
{
    RenderTexture _target; Keyboard _keyboard; int _capture; float _scale; string _report;
    Color32[] Capture(Camera camera,string state,string prefix="OriginalBoat")
    {
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=_target});
        var previous=RenderTexture.active;var cpu=new Texture2D(640,360,TextureFormat.RGBA32,false);
        try
        {
            RenderTexture.active=_target;cpu.ReadPixels(new Rect(0,0,640,360),0,0);cpu.Apply();
            var pixels=cpu.GetPixels32();int pink=pixels.Count(x=>x.r>245&&x.g<10&&x.b>245);
            File.WriteAllBytes($"Evidence/{prefix}-{state}-{SystemInfo.graphicsDeviceType}.png",cpu.EncodeToPNG());
            File.AppendAllText(_report,$"Capture={state}; Pink={pink}; Camera={camera.transform.position}\n");
            Assert.That(pink,Is.Zero,state);return pixels;
        }
        finally{RenderTexture.active=previous;Object.DestroyImmediate(cpu);}
    }
    [UnityTest,Timeout(240000)]
    public IEnumerator OriginalBoatRendersFloatsAndMovesWithNativeThrottle()
    {
        _capture=Time.captureFramerate;_scale=Time.timeScale;OceanRenderer.EnableGraphicsInBatchMode=true;
        EditorSceneManager.OpenScene("Assets/Crest/Crest-Examples/BoatDev/Scenes/boat.unity");
        foreach(var b in Object.FindObjectsByType<BoatAlignNormal>())b._playerControlled=false;
        foreach(var b in Object.FindObjectsByType<BoatProbes>())b._playerControlled=false;
        if(Keyboard.current==null)_keyboard=InputSystem.AddDevice<Keyboard>();
        yield return new EnterPlayMode();Time.captureFramerate=60;Time.timeScale=1;
        _report=$"Evidence/OriginalBoat-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report,"Original boat.unity, actual hull/materials/ocean/camera. Native automatic throttle bias; physical keyboard disabled. Live waves/physics, capture60Hz +test-only GPU query fence, not performance/standalone/wake numerical acceptance.\n");
        new GameObject("Original boat test query pacing").AddComponent<BoatProbesQueryFence>();
        var ocean=OceanRenderer.Instance;var camera=Camera.main;Assert.That(ocean,Is.Not.Null);Assert.That(camera,Is.Not.Null);ocean.ViewCamera=camera;
        var boats=Object.FindObjectsByType<BoatAlignNormal>();var boat=boats.Single(x=>x.name=="BoatAlignNormalPlayer");var rb=boat.GetComponent<Rigidbody>();
        foreach(var b in boats){b._playerControlled=false;b._throttleBias=0;b._steerBias=0;}
        camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;camera.allowMSAA=false;camera.aspect=16f/9;
        _target=new RenderTexture(640,360,24,RenderTextureFormat.ARGB32);_target.Create();
        int missing=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Sum(root=>root.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
        File.AppendAllText(_report,$"Boats={boats.Length}; ProbeBoats={Object.FindObjectsByType<BoatProbes>().Length}; MissingScripts={missing}; Follower={camera.GetComponent<LerpCam>()!=null}; DynamicWaves={ocean._lodDataDynWaves!=null}; Foam={ocean._lodDataFoam!=null}\n");Assert.That(missing,Is.Zero);
        int wet=0;float minY=float.PositiveInfinity,maxY=float.NegativeInfinity;
        for(int i=0;i<250;i++)
        {
            yield return new WaitForFixedUpdate();
            if(i>=150){if(boat.InWater)wet++;minY=Mathf.Min(minY,rb.position.y);maxY=Mathf.Max(maxY,rb.position.y);}
        }
        File.AppendAllText(_report,$"SettledWet={wet}/100; HeightRange={minY:R},{maxY:R}; Position={rb.position}; Velocity={rb.linearVelocity}\n");
        Assert.That(wet,Is.GreaterThan(50));Assert.That(minY,Is.GreaterThan(ocean.SeaLevel-10));Assert.That(maxY,Is.LessThan(ocean.SeaLevel+10));
        var on=Capture(camera,"on");var repeat=Capture(camera,"repeat");Assert.That(on.SequenceEqual(repeat),Is.True,"Same-frame baseline");
        var renderers=boat.GetComponentsInChildren<Renderer>();Assert.That(renderers.Length,Is.GreaterThan(0));var states=renderers.Select(r=>r.forceRenderingOff).ToArray();Color32[] hidden;
        try{foreach(var r in renderers)r.forceRenderingOff=true;hidden=Capture(camera,"hull-off");}
        finally{for(int i=0;i<renderers.Length;i++)renderers[i].forceRenderingOff=states[i];}
        var restored=Capture(camera,"restored");int visible=on.Zip(hidden,(a,b)=>Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b)>6?1:0).Sum();
        File.AppendAllText(_report,$"HullVisiblePixels={visible}; RestoreChanged={on.Zip(restored,(a,b)=>a.Equals(b)?0:1).Sum()}; Materials="+string.Join(";",renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Select(m=>m.name+":"+m.shader.name).Distinct())+"\n");
        Assert.That(visible,Is.GreaterThan(200));Assert.That(on.SequenceEqual(restored),Is.True);
        var start=rb.position;var heading=Vector3.ProjectOnPlane(boat.transform.forward,Vector3.up).normalized;boat._throttleBias=1;int movingWet=0;
        for(int i=0;i<150;i++){yield return new WaitForFixedUpdate();if(boat.InWater)movingWet++;}
        boat._throttleBias=0;var displacement=rb.position-start;float forward=Vector3.Dot(displacement,heading);
        File.AppendAllText(_report,$"Throttle=1; Steps=150; Wet={movingWet}; Displacement={displacement}; ForwardMetres={forward:R}; FinalVelocity={rb.linearVelocity}; Camera={camera.transform.position}\n");
        Capture(camera,"after-throttle");Assert.That(movingWet,Is.GreaterThan(75));Assert.That(forward,Is.GreaterThan(2),"Native throttle moves the actual source hull forwards");
    }
    [UnityTest,Timeout(240000)]
    public IEnumerator ThreeOriginalBoatsFloatMoveAndRenderWithSavedBiases()
    {
        _capture=Time.captureFramerate;_scale=Time.timeScale;OceanRenderer.EnableGraphicsInBatchMode=true;
        EditorSceneManager.OpenScene("Assets/Crest/Crest-Examples/BoatDev/Scenes/threeboats.unity");
        foreach(var b in Object.FindObjectsByType<BoatAlignNormal>())b._playerControlled=false;
        if(Keyboard.current==null)_keyboard=InputSystem.AddDevice<Keyboard>();
        yield return new EnterPlayMode();Time.captureFramerate=60;Time.timeScale=1;
        _report=$"Evidence/OriginalThreeBoats-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report,"Original threeboats.unity; native automatic throttle/steer preserved, physical input off; capture60Hz/query pacing, source camera depth on, postprocess/MSAA off. Not performance/bake/standalone/input acceptance.\n");
        new GameObject("Three boats query pacing").AddComponent<BoatProbesQueryFence>();
        var ocean=OceanRenderer.Instance;var camera=Camera.main;Assert.That(ocean,Is.Not.Null);Assert.That(camera,Is.Not.Null);ocean.ViewCamera=camera;
        var boats=Object.FindObjectsByType<BoatAlignNormal>().OrderBy(b=>b.name).ToArray();Assert.That(boats.Length,Is.EqualTo(3));
        foreach(var b in boats){b._playerControlled=false;Assert.That(b._throttleBias,Is.EqualTo(1));File.AppendAllText(_report,$"Boat={b.name}; Throttle={b._throttleBias}; Steer={b._steerBias}; Start={b.transform.position}\n");}
        camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;camera.allowMSAA=false;camera.aspect=16f/9;
        int missing=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Sum(root=>root.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));Assert.That(missing,Is.Zero);
        for(int i=0;i<200;i++)yield return new WaitForFixedUpdate();
        var starts=boats.Select(b=>b.transform.position).ToArray();var rotations=boats.Select(b=>b.transform.rotation).ToArray();int[] wet=new int[3];
        for(int i=0;i<150;i++)
        {
            yield return new WaitForFixedUpdate();
            for(int j=0;j<3;j++){if(boats[j].InWater)wet[j]++;Assert.That(float.IsFinite(boats[j].transform.position.y),Is.True);Assert.That(Mathf.Abs(boats[j].transform.position.y-ocean.SeaLevel),Is.LessThan(10));}
        }
        for(int j=0;j<3;j++)
        {
            float moved=Vector3.ProjectOnPlane(boats[j].transform.position-starts[j],Vector3.up).magnitude;float turned=Quaternion.Angle(rotations[j],boats[j].transform.rotation);
            File.AppendAllText(_report,$"Boat={boats[j].name}; Wet={wet[j]}/150; HorizontalMetres={moved:R}; RotationDegrees={turned:R}; Final={boats[j].transform.position}\n");Assert.That(wet[j],Is.GreaterThan(75));Assert.That(moved,Is.GreaterThan(2));Assert.That(turned,Is.GreaterThan(5));
        }
        _target=new RenderTexture(640,360,24,RenderTextureFormat.ARGB32);_target.Create();
        var on=Capture(camera,"on","OriginalThreeBoats");var repeat=Capture(camera,"repeat","OriginalThreeBoats");Assert.That(on.SequenceEqual(repeat),Is.True);
        var hulls=boats.SelectMany(b=>b.GetComponentsInChildren<Renderer>()).ToArray();var states=hulls.Select(r=>r.forceRenderingOff).ToArray();Color32[] off;
        try{foreach(var r in hulls)r.forceRenderingOff=true;off=Capture(camera,"hulls-off","OriginalThreeBoats");}finally{for(int i=0;i<hulls.Length;i++)hulls[i].forceRenderingOff=states[i];}
        var restored=Capture(camera,"restored","OriginalThreeBoats");int changed=on.Zip(off,(a,b)=>Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b)>6?1:0).Sum();int restore=on.Zip(restored,(a,b)=>a.Equals(b)?0:1).Sum();
        File.AppendAllText(_report,$"MissingScripts={missing}; Hulls={hulls.Length}; CollectiveHullPixels={changed}; RestoreChanged={restore}; Camera={camera.transform.position}\n");Assert.That(changed,Is.GreaterThan(200));Assert.That(restore,Is.Zero);
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        OceanRenderer.EnableGraphicsInBatchMode=false;
        if(Application.isPlaying)yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        Time.captureFramerate=_capture;Time.timeScale=_scale;
        if(_keyboard!=null&&_keyboard.added)InputSystem.RemoveDevice(_keyboard);
        if(_target!=null){_target.Release();Object.DestroyImmediate(_target);}
    }
}
