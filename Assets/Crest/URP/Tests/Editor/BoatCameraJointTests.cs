using System.Collections;
using System.IO;
using System.Reflection;
using Crest;
using Crest.Examples;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// Passive inputs/outputs around original LerpCam.Update, never moves camera or hull.
[DefaultExecutionOrder(-10000)]
public class BoatCameraJointInputs : MonoBehaviour
{
    public LerpCam CameraLogic;
    public Transform Hull;
    public Vector3 Expected, Look;
    public int Status;
    public float Water;
    static readonly FieldInfo Helper = typeof(LerpCam).GetField("_sampleHeightHelper", BindingFlags.Instance | BindingFlags.NonPublic);
    readonly Vector3[] _point = new Vector3[1];
    readonly float[] _height = new float[1];
    void Update()
    {
        var ocean = OceanRenderer.Instance;
        var helper = (SampleHeightHelper)Helper.GetValue(CameraLogic);
        _point[0] = transform.position;
        Status = ocean.CollisionProvider.Query(helper.GetHashCode(), 0, _point, _height, null, null);
        Water = ocean.CollisionProvider.RetrieveSucceeded(Status) ? _height[0] : ocean.SeaLevel;
        var target = Hull.position;
        target.y = Mathf.Max(target.y, Water + .5f);
        float blend = Mathf.Clamp01(.1f * ocean.DeltaTime * 60);
        Expected = transform.position + (target - transform.position) * blend;
        Look = (Hull.position + Vector3.up * 5 - Expected).normalized;
    }
}

[DefaultExecutionOrder(10000)]
public class BoatCameraJointObservation : MonoBehaviour
{
    public BoatCameraJointInputs Inputs;
    public int Samples, Ready;
    public float MaxPositionError, MaxLookError;
    void Update()
    {
        float pe = Vector3.Distance(transform.position, Inputs.Expected);
        float le = Vector3.Distance(transform.forward, Inputs.Look);
        Assert.That(float.IsFinite(pe) && float.IsFinite(le), Is.True);
        Assert.That(pe, Is.LessThan(.00001f));
        Assert.That(le, Is.LessThan(.00001f));
        MaxPositionError = Mathf.Max(MaxPositionError, pe);
        MaxLookError = Mathf.Max(MaxLookError, le);
        Samples++;
        if (Inputs.Status == 0) Ready++;
    }
}

// Read-only force oracle after controller FixedUpdate, before Unity integrates physics.
[DefaultExecutionOrder(9000)]
public class BoatCameraJointForces : MonoBehaviour
{
    public BoatAlignNormal Boat;
    public Rigidbody Body;
    public int Samples;
    public float MaxError;
    static readonly FieldInfo Height = typeof(BoatAlignNormal).GetField("_sampleHeightHelper", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo Flow = typeof(BoatAlignNormal).GetField("_sampleFlowHelper", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo HP = typeof(SampleHeightHelper).GetField("_queryPos", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo FP = typeof(SampleFlowHelper).GetField("_queryPos", BindingFlags.Instance | BindingFlags.NonPublic);
    readonly Vector3[] _d = new Vector3[1], _n = new Vector3[1], _v = new Vector3[1], _f = new Vector3[1];
    void FixedUpdate()
    {
        var ocean = OceanRenderer.Instance;
        var h = (SampleHeightHelper)Height.GetValue(Boat);
        var f = (SampleFlowHelper)Flow.GetValue(Boat);
        Assert.That(ocean.CollisionProvider.Query(h.GetHashCode(), Boat.ObjectWidth, (Vector3[])HP.GetValue(h), _d, _n, _v), Is.Zero);
        Assert.That(ocean.FlowProvider.Query(f.GetHashCode(), Boat.ObjectWidth, (Vector3[])FP.GetValue(f), _f), Is.Zero);
        float depth = ocean.SeaLevel + _d[0].y - transform.position.y - Boat._bottomH;
        var expected = Vector3.zero;
        if (depth > 0)
        {
            var relative = Body.linearVelocity - (_v[0] + new Vector3(_f[0].x, 0, _f[0].z));
            expected = -Physics.gravity.normalized * Boat._buoyancyCoeff * depth * depth * depth;
            expected += Vector3.up * Vector3.Dot(Vector3.up, -relative) * Boat._dragInWaterUp;
            expected += transform.right * Vector3.Dot(transform.right, -relative) * Boat._dragInWaterRight;
            expected += transform.forward * Vector3.Dot(transform.forward, -relative) * Boat._dragInWaterForward;
            expected *= Body.mass;
        }
        float error = Vector3.Distance(Body.GetAccumulatedForce(), expected);
        Assert.That(float.IsFinite(error) && error < .01f, Is.True, "Actual accumulated force includes wave orbital velocity plus queried flow, not flow alone.");
        MaxError = Mathf.Max(MaxError, error); Samples++;
    }
}

public class BoatCameraJointTests
{
    float _scale;
    int _capture;
    Keyboard _keyboard;
    OceanWaveSpectrum _spectrum;
    Material _oceanMaterial, _flowMaterial;
    BoatAlignNormal _boat;
    Rigidbody _body;
    BoatCameraJointObservation _camera;
    BoatCameraJointForces _forces;
    string _report;
    static readonly FieldInfo HeightHelper = typeof(BoatAlignNormal).GetField("_sampleHeightHelper", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo FlowHelper = typeof(BoatAlignNormal).GetField("_sampleFlowHelper", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo HeightPoints = typeof(SampleHeightHelper).GetField("_queryPos", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo FlowPoints = typeof(SampleFlowHelper).GetField("_queryPos", BindingFlags.Instance | BindingFlags.NonPublic);
    readonly Vector3[] _displacement = new Vector3[1], _normal = new Vector3[1], _velocity = new Vector3[1], _flow = new Vector3[1];

    [UnityTest]
    public IEnumerator OriginalHullDriftsWithReversedFlowAndCameraFollowsOnChoppyWaves()
    {
        _scale = Time.timeScale; _capture = Time.captureFramerate;
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        Time.timeScale = 1; Time.captureFramerate = 60;
        Assert.That(Time.fixedDeltaTime, Is.EqualTo(.02f).Within(.000001f));
        Assert.That(Physics.simulationMode, Is.EqualTo(SimulationMode.FixedUpdate));
        if (Keyboard.current == null) _keyboard = InputSystem.AddDevice<Keyboard>();
        var ocean = OceanRenderer.Instance;
        Camera.main.transform.position = new Vector3(-2, 4, -3);
        foreach (var shape in Object.FindObjectsByType<ShapeWaves>()) shape.enabled = false;
        _oceanMaterial = new Material(ocean.OceanMaterial);
        _oceanMaterial.EnableKeyword("_FLOW_ON"); _oceanMaterial.SetFloat("_Flow", 1);
        ocean.OceanMaterial = _oceanMaterial;
        var oceanFields = new SerializedObject(ocean);
        oceanFields.FindProperty("_createFlowSim").boolValue = true;
        oceanFields.ApplyModifiedPropertiesWithoutUndo();
        new GameObject("Joint numerical query fence").AddComponent<BoatProbesQueryFence>();
        var input = GameObject.CreatePrimitive(PrimitiveType.Plane);
        Object.DestroyImmediate(input.GetComponent<Collider>());
        input.transform.localScale = Vector3.one * 100;
        _flowMaterial = new Material(Shader.Find("Crest/Inputs/Flow/Fixed Direction"));
        _flowMaterial.SetFloat("_Speed", .75f); _flowMaterial.SetFloat("_Direction", 0);
        input.GetComponent<Renderer>().sharedMaterial = _flowMaterial;
        input.AddComponent<RegisterFlowInput>();
        _spectrum = ScriptableObject.CreateInstance<OceanWaveSpectrum>();
        _spectrum._chop = 1.6f; _spectrum._waveDirectionVariance = 0;
        var powers = new SerializedObject(_spectrum);
        for (int i = 0; i < OceanWaveSpectrum.NUM_OCTAVES; i++)
        {
            powers.FindProperty("_powerDisabled").GetArrayElementAtIndex(i).boolValue = i != 9;
            powers.FindProperty("_powerLog").GetArrayElementAtIndex(i).floatValue = -2;
        }
        powers.ApplyModifiedPropertiesWithoutUndo();
        var waveOwner = new GameObject("Original joint Gerstner waves"); waveOwner.SetActive(false);
        var waves = waveOwner.AddComponent<ShapeGerstner>();
        waves._spectrum = _spectrum; waves._randomSeed = 27; waves._componentsPerOctave = 2; waves._resolution = 128;
        waves._overrideGlobalWindSpeed = true; waves._windSpeed = 60;
        waves._overrideGlobalWindDirection = true; waves._waveDirectionHeadingAngle = 0;
        var waveFields = new SerializedObject(waves);
        waveFields.FindProperty("_respectShallowWaterAttenuation").floatValue = 0;
        waveFields.ApplyModifiedPropertiesWithoutUndo(); waveOwner.SetActive(true);
        for (int i = 0; i < 8; i++) yield return null;
        Assert.That(ocean.FlowProvider, Is.Not.Null);
        var hull = new GameObject("Original physical hull and actual camera target"); hull.SetActive(false);
        hull.transform.position = new Vector3(-2, -1.3f, 0);
        hull.AddComponent<BoxCollider>().size = new Vector3(2, 1, 4);
        _body = hull.AddComponent<Rigidbody>(); _body.mass = 1000; _body.angularDamping = 2;
        _body.constraints = RigidbodyConstraints.FreezeAll;
        _boat = hull.AddComponent<BoatAlignNormal>();
        _boat._playerControlled = false; _boat._bottomH = -.5f; _boat._boatWidth = 2;
        _boat._forceHeightOffset = 0; _boat._throttleBias = _boat._steerBias = 0;
        _forces = hull.AddComponent<BoatCameraJointForces>(); _forces.Boat = _boat; _forces.Body = _body; _forces.enabled = false;
        hull.SetActive(true);
        var main = Camera.main;
        var follower = main.gameObject.AddComponent<LerpCam>(); follower.enabled = false;
        var cameraFields = new SerializedObject(follower);
        cameraFields.FindProperty("_targetPos").objectReferenceValue = hull.transform;
        cameraFields.FindProperty("_targetLookatPos").objectReferenceValue = hull.transform;
        cameraFields.FindProperty("_lerpAlpha").floatValue = .1f;
        cameraFields.FindProperty("_lookatOffset").floatValue = 5;
        cameraFields.FindProperty("_minHeightAboveWater").floatValue = .5f;
        cameraFields.ApplyModifiedPropertiesWithoutUndo();
        var cameraInputs = main.gameObject.AddComponent<BoatCameraJointInputs>();
        cameraInputs.CameraLogic = follower; cameraInputs.Hull = hull.transform;
        _camera = main.gameObject.AddComponent<BoatCameraJointObservation>(); _camera.Inputs = cameraInputs;
        follower.enabled = true;
        _report = $"Evidence/BoatCameraJoint-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report, "Original BoatAlignNormal + original LerpCam on actual main Camera, no procedural hull/camera movement after setup. Mass1000kg/hull2x1x4m, original gravity/cubic buoyancy/drag, yaw constrained only after warm-up. Original Gerstner chop1.6/octave9/seed27 and fixed-direction GPU flow .75m/s then reversal. Helper owner queries expose readiness. Before/after Update passive camera recurrence/LookAt checks1e-5. Test-only query fence; not latency/performance/saved-demo visual acceptance.\n");
        int ready = 0; float deadline = Time.realtimeSinceStartup + 15;
        while (ready < 8 && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            ready = Query() && Vector3.Distance(_flow[0], Vector3.right * .75f) < .005f ? ready + 1 : 0;
        }
        Assert.That(ready, Is.EqualTo(8));
        _body.constraints = RigidbodyConstraints.FreezeRotationY;
        _forces.enabled = true;
        yield return Drift("PositiveFlow", .75f);
        _flowMaterial.SetFloat("_Direction", .5f);
        yield return Drift("ReversedFlow", -.75f);
        File.AppendAllText(_report, $"CameraSummary; Samples={_camera.Samples}; Ready={_camera.Ready}; MaxPositionError={_camera.MaxPositionError:R}; MaxLookError={_camera.MaxLookError:R}\n");
        Assert.That(_camera.Ready, Is.GreaterThan(500));
        File.AppendAllText(_report, $"ForceSummary; Samples={_forces.Samples}; MaxForceErrorNewtons={_forces.MaxError:R}\n");
        Assert.That(_forces.Samples, Is.GreaterThan(600));
    }

    bool Query()
    {
        var height = (SampleHeightHelper)HeightHelper.GetValue(_boat);
        var flow = (SampleFlowHelper)FlowHelper.GetValue(_boat);
        int hs = OceanRenderer.Instance.CollisionProvider.Query(height.GetHashCode(), _boat.ObjectWidth,
            (Vector3[])HeightPoints.GetValue(height), _displacement, _normal, _velocity);
        int fs = OceanRenderer.Instance.FlowProvider.Query(flow.GetHashCode(), _boat.ObjectWidth,
            (Vector3[])FlowPoints.GetValue(flow), _flow);
        return hs == 0 && fs == 0;
    }

    IEnumerator Drift(string phase, float expectedFlow)
    {
        float start = Time.fixedTime, last = start, startX = _body.position.x, cameraX = _camera.transform.position.x;
        float minY = float.PositiveInfinity, maxY = float.NegativeInfinity, minSurface = minY, maxSurface = maxY;
        float maxSpeed = 0, maxAngle = 0, tailVelocityError = 0;
        int samples = 0, tail = 0; float deadline = Time.realtimeSinceStartup + 30;
        while (Time.fixedTime - start < 8 && Time.realtimeSinceStartup < deadline)
        {
            yield return new WaitForFixedUpdate();
            if (Time.fixedTime <= last) continue; last = Time.fixedTime;
            Assert.That(Query(), Is.True, phase + " full height/normal/velocity/flow readiness");
            float elapsed = last - start;
            float speed = _body.linearVelocity.magnitude, angle = Vector3.Angle(_body.transform.up, Vector3.up);
            Assert.That(float.IsFinite(speed) && float.IsFinite(angle) && float.IsFinite(_body.position.y), Is.True);
            Assert.That(_body.position.y, Is.InRange(-4, 2));
            Assert.That(speed, Is.LessThan(5)); Assert.That(angle, Is.LessThan(45));
            Assert.That(_boat.InWater, Is.True);
            minY = Mathf.Min(minY, _body.position.y); maxY = Mathf.Max(maxY, _body.position.y);
            minSurface = Mathf.Min(minSurface, _displacement[0].y); maxSurface = Mathf.Max(maxSurface, _displacement[0].y);
            maxSpeed = Mathf.Max(maxSpeed, speed); maxAngle = Mathf.Max(maxAngle, angle);
            if (elapsed > 6)
            {
                Assert.That(Vector3.Distance(_flow[0], Vector3.right * expectedFlow), Is.LessThan(.005f));
                float error = Mathf.Abs(_body.linearVelocity.x - expectedFlow);
                tailVelocityError = Mathf.Max(tailVelocityError, error); tail++;
                // Orbital wave velocity is nonzero: flow-only velocity error is diagnostic, not an equilibrium oracle.
            }
            File.AppendAllText(_report, $"{phase}; T={elapsed:R}; Hull={_body.position:F6}; Velocity={_body.linearVelocity:F6}; Surface={_displacement[0].y:R}; Flow={_flow[0]:F6}; Angle={angle:R}\n");
            samples++;
        }
        float dx = _body.position.x - startX, cx = _camera.transform.position.x - cameraX;
        File.AppendAllText(_report, $"Summary={phase}; Samples={samples}; Tail={tail}; Seconds={last-start:R}; HullDeltaX={dx:R}; CameraDeltaX={cx:R}; HullYSpan={maxY-minY:R}; SurfaceSpan={maxSurface-minSurface:R}; MaxSpeed={maxSpeed:R}; MaxTilt={maxAngle:R}; MaxTailVelocityError={tailVelocityError:R}\n");
        Assert.That(last - start, Is.GreaterThanOrEqualTo(8)); Assert.That(tail, Is.GreaterThanOrEqualTo(95));
        Assert.That(dx * Mathf.Sign(expectedFlow), Is.GreaterThan(3));
        Assert.That(cx * Mathf.Sign(expectedFlow), Is.GreaterThan(3));
        Assert.That(maxY - minY, Is.GreaterThan(.03f)); Assert.That(maxSurface - minSurface, Is.GreaterThan(.03f));
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = _scale; Time.captureFramerate = _capture;
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
        if (_spectrum != null) Object.DestroyImmediate(_spectrum);
        if (_oceanMaterial != null) Object.DestroyImmediate(_oceanMaterial);
        if (_flowMaterial != null) Object.DestroyImmediate(_flowMaterial);
    }
}
