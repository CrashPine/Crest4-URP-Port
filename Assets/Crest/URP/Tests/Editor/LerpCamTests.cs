using System.Collections;
using System.IO;
using System.Reflection;
using Crest;
using Crest.Examples;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

// Drives only the clock and target, then records inputs before original Update.
[DefaultExecutionOrder(-10000)]
public class LerpCamInputs : MonoBehaviour
{
    public TimeProviderCustom Clock;
    public Transform Target, Look;
    public float Delta = 1f / 60, TargetY = -2, Alpha = .1f;
    public int Version;
    public Vector3 Before, Expected, ExpectedLook, TargetAtSample;
    public float AppliedDelta;
    public int AppliedVersion;
    void Update()
    {
        Clock._overrideDeltaTime = true;
        Clock._deltaTime = Delta;
        Target.position = new Vector3(Target.position.x + Time.deltaTime, TargetY, Target.position.z);
        Before = transform.position;
        TargetAtSample = Target.position;
        var goal = TargetAtSample;
        goal.y = Mathf.Max(goal.y, OceanRenderer.Instance.SeaLevel + .5f);
        AppliedDelta = OceanRenderer.Instance.DeltaTime;
        Expected = Before + (goal - Before) * Mathf.Clamp01(Alpha * AppliedDelta * 60);
        ExpectedLook = (Look.position + Vector3.up * 5 - Expected).normalized;
        AppliedVersion = Version;
    }
}

// Observes after the unmodified LerpCam.Update; never moves the follower.
[DefaultExecutionOrder(10000)]
public class LerpCamObservation : MonoBehaviour
{
    public LerpCamInputs Inputs;
    public int Samples, Version;
    public float PositionError, LookError, Movement, Delta;
    public Vector3 Position, Target;
    void Update()
    {
        Position = transform.position;
        Target = Inputs.TargetAtSample;
        PositionError = Vector3.Distance(Position, Inputs.Expected);
        LookError = Vector3.Distance(transform.forward, Inputs.ExpectedLook);
        Movement = Vector3.Distance(Position, Inputs.Before);
        Delta = Inputs.AppliedDelta;
        Version = Inputs.AppliedVersion;
        Samples++;
    }
}

public class LerpCamTests
{
    float _scale;
    int _capture;
    string _report;
    LerpCam _follower;
    LerpCamInputs _inputs;
    LerpCamObservation _observation;
    static readonly FieldInfo Helper = typeof(LerpCam).GetField("_sampleHeightHelper", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo Points = typeof(SampleHeightHelper).GetField("_queryPos", BindingFlags.Instance | BindingFlags.NonPublic);

    [UnityTest]
    public IEnumerator ActualUpdateFollowsMovingTargetClampsHeightAndRespectsOceanDelta()
    {
        _scale = Time.timeScale;
        _capture = Time.captureFramerate;
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        Assert.That(Application.isPlaying, Is.True);
        Time.timeScale = 1;
        Time.captureFramerate = 60;
        var ocean = OceanRenderer.Instance;
        Camera.main.transform.position = new Vector3(0, 4, 0);
        foreach (var shape in Object.FindObjectsByType<ShapeWaves>()) shape.enabled = false;
        ocean.transform.position = Vector3.up * 1.25f;
        new GameObject("Test-only camera query fence").AddComponent<BoatProbesQueryFence>();
        var clock = ocean.gameObject.AddComponent<TimeProviderCustom>();
        yield return null;
        ocean.PushTimeProvider(clock);
        var target = new GameObject("Procedural moving boat target").transform;
        target.position = new Vector3(0, -2, 4);
        var owner = new GameObject("Original follow camera logic");
        owner.SetActive(false);
        owner.transform.position = new Vector3(-2, 4, -3);
        _follower = owner.AddComponent<LerpCam>();
        var fields = new SerializedObject(_follower);
        fields.FindProperty("_targetPos").objectReferenceValue = target;
        fields.FindProperty("_targetLookatPos").objectReferenceValue = target;
        fields.FindProperty("_lerpAlpha").floatValue = .1f;
        fields.FindProperty("_lookatOffset").floatValue = 5;
        fields.FindProperty("_minHeightAboveWater").floatValue = .5f;
        fields.ApplyModifiedPropertiesWithoutUndo();
        _inputs = owner.AddComponent<LerpCamInputs>();
        _inputs.Clock = clock;
        _inputs.Target = _inputs.Look = target;
        _observation = owner.AddComponent<LerpCamObservation>();
        _observation.Inputs = _inputs;
        owner.SetActive(true);
        _report = $"Evidence/LerpCam-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report, "Unmodified LerpCam.Update, moving procedural target1m/s, flat GPU water at1.25m. Early component drives only target/clock and records inputs; late observer never moves follower. Height query must be ready through original helper owner. Auxiliary Transform only, not multi-camera rendering acceptance. No private lifecycle Invoke; test-only readback fence. Position/look-vector tolerances .00001.\n");
        yield return Phase("LowTargetHeightClamp", -2, 1f / 60, 80, 1.75f);
        yield return Phase("HighTargetHalfDelta", 4, 1f / 120, 100, 4);
        yield return Phase("ZeroDeltaMovingTarget", 4, 0, 24, null);
        yield return Phase("ResumedHeightClamp", -2, 1f / 60, 80, 1.75f);
        ocean.PopTimeProvider(clock);
    }

    IEnumerator Phase(string phase, float targetY, float delta, int count, float? finalY)
    {
        _inputs.TargetY = targetY;
        _inputs.Delta = delta;
        int version = ++_inputs.Version;
        int last = _observation.Samples, accepted = 0;
        float maxPositionError = 0, maxLookError = 0, maxMove = 0;
        float startTargetX = _inputs.Target.position.x, deadline = Time.realtimeSinceStartup + 20;
        var heights = new float[1];
        while (accepted < count && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            if (_observation.Samples <= last || _observation.Version != version) continue;
            last = _observation.Samples;
            var helper = (SampleHeightHelper)Helper.GetValue(_follower);
            int status = OceanRenderer.Instance.CollisionProvider.Query(helper.GetHashCode(), 0,
                (Vector3[])Points.GetValue(helper), heights, null, null);
            if (accepted == 0 && status != 0) continue;
            File.AppendAllText(_report, $"{phase}; Sample={accepted}; Status={status}; QueryHeight={heights[0]:R}; Position={_observation.Position:F8}; Target={_observation.Target:F8}; Delta={_observation.Delta:R}; PositionError={_observation.PositionError:R}; LookError={_observation.LookError:R}; Movement={_observation.Movement:R}\n");
            Assert.That(status, Is.Zero);
            Assert.That(heights[0], Is.EqualTo(1.25f).Within(.0001f));
            Assert.That(float.IsFinite(_observation.PositionError) && float.IsFinite(_observation.LookError), Is.True);
            Assert.That(_observation.Delta, Is.EqualTo(delta));
            Assert.That(_observation.PositionError, Is.LessThan(.00001f), "Original frame position versus independent linear recurrence");
            Assert.That(_observation.LookError, Is.LessThan(.00001f), "LookAt original target plus offset");
            if (delta == 0) Assert.That(_observation.Movement, Is.Zero);
            maxPositionError = Mathf.Max(maxPositionError, _observation.PositionError);
            maxLookError = Mathf.Max(maxLookError, _observation.LookError);
            maxMove = Mathf.Max(maxMove, _observation.Movement);
            accepted++;
        }
        float targetDistance = _inputs.Target.position.x - startTargetX;
        File.AppendAllText(_report, $"Summary={phase}; Accepted={accepted}; FinalY={_observation.Position.y:R}; TargetMovedX={targetDistance:R}; MaxPositionError={maxPositionError:R}; MaxLookError={maxLookError:R}; MaxMovement={maxMove:R}\n");
        Assert.That(accepted, Is.EqualTo(count));
        Assert.That(targetDistance, Is.GreaterThan(.3f));
        if (finalY.HasValue) Assert.That(_observation.Position.y, Is.EqualTo(finalY.Value).Within(.025f));
        if (delta > 0) Assert.That(maxMove, Is.GreaterThan(.005f));
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = _scale;
        Time.captureFramerate = _capture;
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }
}
