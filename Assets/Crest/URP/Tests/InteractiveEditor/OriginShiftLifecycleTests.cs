using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Crest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class OriginShiftReceiver : MonoBehaviour, IShiftingOrigin
{
    public int Calls;
    public Vector3 Last;
    public void SetOrigin(Vector3 origin) { Calls++; Last=origin; }
}

[DefaultExecutionOrder(-10000)]
public class OriginShiftBefore : MonoBehaviour
{
    public OriginShiftObservation Observation;
    void FixedUpdate() { Observation.Before(); }
}

[DefaultExecutionOrder(10000)]
public class OriginShiftObservation : MonoBehaviour
{
    public bool Armed, FixedDone, LateDone, LateShiftFlag;
    public Exception Failure;
    public Transform Camera, Root, Child;
    public Rigidbody Near, Far;
    public ParticleSystem[] Particles;
    public OriginShiftReceiver Receiver;
    public Vector3 Expected, Cumulative;
    public int Frame, HistoryChecks, NativeChecks;
    Vector3 _camera,_root,_child;
    Vector3[][] _centres,_particles;
    int _calls;
    bool[] _playing,_paused;
    bool _before;
    public void Before()
    {
        if(!Armed||_before||Failure!=null) return;
        _before=true; _camera=Camera.position; _root=Root.position; _child=Child.position; _calls=Receiver.Calls;
        var ocean=OceanRenderer.Instance; _centres=new Vector3[ocean.CurrentLodCount][];
        for(int i=0;i<_centres.Length;i++) { var b=ocean._lodTransform._renderData[i]; _centres[i]=new Vector3[b.Size]; for(int f=0;f<b.Size;f++) _centres[i][f]=b.Previous(f)._posSnapped; }
        _particles=new Vector3[Particles.Length][]; _playing=new bool[Particles.Length]; _paused=new bool[Particles.Length];
        for(int i=0;i<Particles.Length;i++)
        {
            var data=new ParticleSystem.Particle[8]; int n=Particles[i].GetParticles(data); _particles[i]=new Vector3[n];
            for(int p=0;p<n;p++) _particles[i][p]=data[p].position;
            _playing[i]=Particles[i].isPlaying; _paused[i]=Particles[i].isPaused;
        }
    }
    void FixedUpdate()
    {
        if(!Armed||!_before||Failure!=null) return;
        try
        {
            bool shift=Expected!=Vector3.zero;
            Assert.That(ShiftingOrigin.HasTeleportedThisFrame,Is.EqualTo(shift));
            Assert.That(ShiftingOrigin.TeleportOriginThisFrame,Is.EqualTo(Expected));
            Assert.That(Vector3.Distance(Camera.position,_camera-Expected),Is.LessThan(.001f));
            Assert.That(Vector3.Distance(Root.position,_root-Expected),Is.LessThan(.001f));
            Assert.That(Vector3.Distance(Child.position,_child-Expected),Is.LessThan(.001f));
            Assert.That(Vector3.Distance(Child.position-Root.position,_child-_root),Is.LessThan(.001f),"Root and child must not be shifted twice.");
            Assert.That(Receiver.Calls,Is.EqualTo(_calls+(shift?1:0))); if(shift) Assert.That(Receiver.Last,Is.EqualTo(Expected));
            var ocean=OceanRenderer.Instance;
            for(int i=0;i<_centres.Length;i++) for(int f=0;f<_centres[i].Length;f++) { Assert.That(Vector3.Distance(ocean._lodTransform._renderData[i].Previous(f)._posSnapped,_centres[i][f]-Expected),Is.LessThan(.001f)); HistoryChecks++; }
            for(int i=0;i<Particles.Length;i++)
            {
                var data=new ParticleSystem.Particle[8]; int n=Particles[i].GetParticles(data); Assert.That(n,Is.EqualTo(_particles[i].Length)); Assert.That(n,Is.EqualTo(2));
                Vector3 delta=Particles[i].main.simulationSpace==ParticleSystemSimulationSpace.World?Expected:Vector3.zero;
                for(int p=0;p<n;p++) Assert.That(Vector3.Distance(data[p].position,_particles[i][p]-delta),Is.LessThan(.001f));
                Assert.That(Particles[i].isPlaying,Is.EqualTo(_playing[i])); Assert.That(Particles[i].isPaused,Is.EqualTo(_paused[i]));
            }
            if(shift)
            {
                Assert.That(Near.sleepThreshold,Is.EqualTo(.14f)); Assert.That(Far.sleepThreshold,Is.EqualTo(float.MaxValue));
                Assert.That(Vector3.Distance(Shader.GetGlobalVector(ShiftingOrigin.sp_CrestFloatingOriginOffset),Cumulative),Is.LessThan(.001f));
            }
            else { Assert.That(Near.sleepThreshold,Is.EqualTo(.33f)); Assert.That(Far.sleepThreshold,Is.EqualTo(.44f)); }
            Frame=Time.frameCount; FixedDone=true; Armed=false;
        }
        catch(Exception e) { Failure=e; Armed=false; }
    }
    void LateUpdate()
    {
        if(!FixedDone||LateDone||Failure!=null) return;
        try
        {
            var ocean=OceanRenderer.Instance; LateShiftFlag=ShiftingOrigin.HasTeleportedThisFrame;
            Assert.That(LateShiftFlag,Is.EqualTo(Expected!=Vector3.zero));
            foreach(var name in new[]{"_bufCascadeDataTgt","_bufCascadeDataSrc"})
            {
                var buffer=(ComputeBuffer)typeof(OceanRenderer).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ocean);
                var data=new OceanRenderer.CascadeParams[buffer.count]; buffer.GetData(data); int f=name.EndsWith("Src")?1:0;
                for(int i=0;i<ocean.CurrentLodCount;i++)
                {
                    var center=ocean._lodTransform._renderData[i].Previous(f)._posSnapped;
                    Assert.That(Vector2.Distance(data[i]._posSnapped,new Vector2(center.x,center.z)),Is.LessThan(.001f)); NativeChecks++;
                }
            }
            LateDone=true;
        }
        catch(Exception e) { Failure=e; }
    }
    public void Arm(Vector3 origin,Vector3 cumulative)
    {
        Expected=origin; Cumulative=cumulative; FixedDone=LateDone=LateShiftFlag=_before=false; Failure=null; Armed=true;
    }
}

public class OriginShiftLifecycleTests
{
    float _scale;
    int _capture;
    string _report;
    OriginShiftObservation _observation;
    int _rendered;
    Camera _camera;
    void Rendered(ScriptableRenderContext context,Camera camera) { if(camera==_camera) _rendered++; }

    [UnityTest,Timeout(45000)]
    public IEnumerator NonBatchGameViewRunsOriginalShiftAndEndOfFrameReset()
    {
        _scale=Time.timeScale; _capture=Time.captureFramerate;
        Assert.That(Application.isBatchMode,Is.False,"Run using -Interactive; this test must never be reported as a batch EOF pass.");
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        var game=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")); game.Show(); game.Focus();
        yield return new EnterPlayMode();
        Time.timeScale=1; Time.captureFramerate=60;
        var ocean=OceanRenderer.Instance; _camera=Camera.main;
        foreach(var waves in Object.FindObjectsByType<ShapeWaves>()) waves.enabled=false;
        RenderPipelineManager.endCameraRendering+=Rendered;
        _camera.transform.position=new Vector3(0,4,0);
        for(int i=0;i<5;i++) yield return null;
        yield return new WaitForEndOfFrame();
        Assert.That(_rendered,Is.GreaterThan(0),"Actual main GameView camera rendered; no manual SubmitRenderRequest/EOF emulation.");
        var root=new GameObject("Original shift root marker").transform;
        var child=new GameObject("Child marker must not move twice").transform; child.SetParent(root,false); child.localPosition=new Vector3(2,3,4);
        var receiver=ocean.gameObject.AddComponent<OriginShiftReceiver>();
        var near=new GameObject("Near physics threshold").AddComponent<Rigidbody>(); near.isKinematic=true; near.useGravity=false;
        var far=new GameObject("Far physics threshold").AddComponent<Rigidbody>(); far.isKinematic=true; far.useGravity=false;
        var particles=new[]{Particle(root,ParticleSystemSimulationSpace.World,false),Particle(root,ParticleSystemSimulationSpace.World,true),Particle(root,ParticleSystemSimulationSpace.Local,false)};
        _observation=_camera.gameObject.AddComponent<OriginShiftObservation>();
        _observation.Camera=_camera.transform; _observation.Root=root; _observation.Child=child; _observation.Near=near; _observation.Far=far; _observation.Particles=particles; _observation.Receiver=receiver;
        _camera.gameObject.AddComponent<OriginShiftBefore>().Observation=_observation;
        var shift=_camera.gameObject.AddComponent<ShiftingOrigin>(); shift._threshold=2048;
        var settings=new SerializedObject(shift); settings.FindProperty("_physicsThreshold").floatValue=100; settings.ApplyModifiedPropertiesWithoutUndo();
        _report=$"Evidence/OriginLifecycle-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report,"NON-BATCH Editor, actual focused GameView/render events and actual WaitForEndOfFrame. Original ShiftingOrigin.FixedUpdate/Start, no private MoveOrigin invocation or flag resets. Exact/+/-/multi-axis/Y cases at threshold2048; root/child transforms, world-playing/world-paused/local particles, notification, physics sleep thresholds, all buffered LOD centres before LateUpdate and native target/source buffers after LateUpdate. Tolerance .001m. Flat waves only: no phase/compatibility/FFT/dynamic-history-transport claim.\n");
        Vector3 cumulative=Vector3.zero;
        var cases=new[]{new Vector3(2048,4,0),new Vector3(-2048,4,0),new Vector3(2048.25f,4,0),new Vector3(-2048.25f,4,0),new Vector3(0,4,2048.25f),new Vector3(0,4,-2048.25f),new Vector3(4096.25f,4,-4096.25f),new Vector3(0,2048.25f,0)};
        for(int index=0;index<cases.Length;index++)
        {
            var position=cases[index]; Vector3 origin=Vector3.zero;
            for(int axis=0;axis<3;axis++) if(Mathf.Abs(position[axis])>2048) origin[axis]=(float)(Math.Floor((double)position[axis]/2048)*2048);
            cumulative-=origin;
            _camera.transform.position=position; root.position=position+new Vector3(7,1,5);
            near.transform.position=origin+new Vector3(10,4,10); far.transform.position=origin+new Vector3(300,4,0); near.sleepThreshold=.33f; far.sleepThreshold=.44f;
            foreach(var ps in particles) Seed(ps,position);
            Physics.SyncTransforms(); _observation.Arm(origin,cumulative);
            float deadline=Time.realtimeSinceStartup+8;
            while(!_observation.FixedDone&&_observation.Failure==null&&Time.realtimeSinceStartup<deadline) yield return new WaitForFixedUpdate();
            if(_observation.Failure!=null) throw _observation.Failure;
            Assert.That(_observation.FixedDone,Is.True);
            yield return new WaitForEndOfFrame();
            if(_observation.Failure!=null) throw _observation.Failure;
            Assert.That(_observation.LateDone,Is.True);
            Assert.That(ShiftingOrigin.HasTeleportedThisFrame,Is.False,"Original Start resets flag at actual EOF.");
            Assert.That(ShiftingOrigin.TeleportOriginThisFrame,Is.EqualTo(Vector3.zero));
            File.AppendAllText(_report,$"Case={index}; Before={position:F4}; Shift={origin:F4}; After={_camera.transform.position:F4}; Cumulative={cumulative:F4}; FixedFrame={_observation.Frame}; EofFrame={Time.frameCount}; LateFlag={_observation.LateShiftFlag}; Reset={ShiftingOrigin.HasTeleportedThisFrame}; RenderEvents={_rendered}\n");
        }
        File.AppendAllText(_report,$"Summary; Cases={cases.Length}; ReceiverCalls={receiver.Calls}; HistoryChecks={_observation.HistoryChecks}; NativeBufferChecks={_observation.NativeChecks}; RenderEvents={_rendered}; Batch={Application.isBatchMode}\n");
        Assert.That(receiver.Calls,Is.EqualTo(6));
        shift.enabled=false; Assert.That(Shader.IsKeywordEnabled("CREST_FLOATING_ORIGIN"),Is.False); Assert.That((Vector3)Shader.GetGlobalVector(ShiftingOrigin.sp_CrestFloatingOriginOffset),Is.EqualTo(Vector3.zero));
    }
    static ParticleSystem Particle(Transform root,ParticleSystemSimulationSpace space,bool pause)
    {
        var owner=new GameObject("Shift particle "+space+" paused="+pause); owner.transform.SetParent(root,false);
        var ps=owner.AddComponent<ParticleSystem>(); ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main; main.playOnAwake=false; main.simulationSpace=space; main.simulationSpeed=0; main.maxParticles=8; main.startLifetime=1000; main.startSpeed=0;
        var emission=ps.emission; emission.enabled=false; ps.Play(); if(pause) ps.Pause(); return ps;
    }
    static void Seed(ParticleSystem ps,Vector3 camera)
    {
        var particles=new ParticleSystem.Particle[2];
        for(int i=0;i<2;i++) particles[i]=new ParticleSystem.Particle {position=(ps.main.simulationSpace==ParticleSystemSimulationSpace.World?camera:Vector3.zero)+new Vector3(i+1,2,3),velocity=Vector3.zero,startLifetime=1000,remainingLifetime=1000,startSize=.1f};
        ps.SetParticles(particles,2);
    }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if(_observation!=null) _observation.Armed=false;
        RenderPipelineManager.endCameraRendering-=Rendered;
        Time.timeScale=_scale; Time.captureFramerate=_capture;
        if(Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    }
}
