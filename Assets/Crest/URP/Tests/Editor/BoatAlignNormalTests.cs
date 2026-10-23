using System.Collections;
using System.IO;
using System.Reflection;
using Crest;
using Crest.Examples;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

public class BoatAlignNormalTests
{
    readonly BoatAlignNormal[] _boats = new BoatAlignNormal[2];
    readonly Rigidbody[] _bodies = new Rigidbody[2];
    readonly BoatControlForceObserver[] _observers = new BoatControlForceObserver[2];
    Keyboard _ownedKeyboard;
    Material _material;
    SimSettingsSeaFloorDepth _settings;
    float _scale;
    int _capture;
    string _report;
    static readonly FieldInfo WidthHelper = typeof(BoatAlignNormal).GetField("_sampleHeightHelper", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo LengthHelper = typeof(BoatAlignNormal).GetField("_sampleHeightHelperLengthwise", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo Points = typeof(SampleHeightHelper).GetField("_queryPos", BindingFlags.Instance | BindingFlags.NonPublic);

    void Open()
    {
        _scale = Time.timeScale;
        _capture = Time.captureFramerate;
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
    }

    void Setup(string reportName, float y, bool held)
    {
        Assert.That(Application.isPlaying, Is.True);
        Assert.That(Physics.simulationMode, Is.EqualTo(SimulationMode.FixedUpdate));
        Assert.That(Time.fixedDeltaTime, Is.EqualTo(.02f).Within(1e-6f));
        Time.timeScale = 1;
        Time.captureFramerate = 60;
        Camera.main.transform.position = new Vector3(0, 4, 0);
        foreach (var shape in Object.FindObjectsByType<ShapeWaves>()) shape.enabled = false;
        new GameObject("Test-only boat readback fence").AddComponent<BoatProbesQueryFence>();
        // The original component reads raw input even with playerControlled=false.
        // Supply a device only if batch mode has none; do not alter production input.
        if (Keyboard.current == null) _ownedKeyboard = InputSystem.AddDevice<Keyboard>();
        _report = $"Evidence/{reportName}-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report, $"Original BoatAlignNormal; masses1000/5000kg, widths2/4m, lengths4/8m. Actual Start/FixedUpdate/physics. Test-only readback fence and passive force observer; no performance claim. Owned test keyboard={_ownedKeyboard != null}; playerControlled=false, no injected keys.\n");
        for (int i = 0; i < 2; i++)
        {
            var owner = new GameObject("Original BoatAlignNormal " + i);
            owner.SetActive(false);
            owner.transform.position = new Vector3(i == 0 ? -3 : 3, y, 0);
            owner.AddComponent<BoxCollider>().size = new Vector3(i == 0 ? 2 : 4, 1, i == 0 ? 4 : 8);
            var body = _bodies[i] = owner.AddComponent<Rigidbody>();
            body.mass = i == 0 ? 1000 : 5000;
            body.linearDamping = 0;
            body.angularDamping = 2;
            body.useGravity = !held;
            body.constraints = held ? RigidbodyConstraints.FreezePosition : RigidbodyConstraints.FreezeAll;
            var boat = _boats[i] = owner.AddComponent<BoatAlignNormal>();
            boat._playerControlled = false;
            boat._bottomH = held ? 0 : -.5f;
            boat._boatWidth = i == 0 ? 2 : 4;
            boat._boatLength = i == 0 ? 4 : 8;
            boat._useBoatLength = i == 1;
            if (held)
            {
                boat._dragInWaterUp = boat._dragInWaterRight = boat._dragInWaterForward = 0;
                boat._forceHeightOffset = 0;
            }
            _observers[i] = owner.AddComponent<BoatControlForceObserver>();
            _observers[i].Body = body;
            owner.SetActive(true);
        }
    }

    IEnumerator Warm()
    {
        int ready = 0;
        float deadline = Time.realtimeSinceStartup + 15;
        while (ready < 8 && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            bool all = true;
            for (int i = 0; i < 2; i++)
            {
                all &= CheckHelper(_boats[i], WidthHelper, _boats[i]._boatWidth);
                if (_boats[i]._useBoatLength) all &= CheckHelper(_boats[i], LengthHelper, _boats[i]._boatLength);
            }
            ready = all ? ready + 1 : 0;
        }
        File.AppendAllText(_report, $"WarmReady={ready}\n");
        Assert.That(ready, Is.EqualTo(8), "Actual controller helpers need full positive-dt query readiness");
    }

    bool CheckHelper(BoatAlignNormal boat, FieldInfo field, float width)
    {
        var helper = (SampleHeightHelper)field.GetValue(boat);
        var points = (Vector3[])Points.GetValue(helper);
        var disps = new Vector3[1];
        var normals = new Vector3[1];
        var velocities = new Vector3[1];
        int status = OceanRenderer.Instance.CollisionProvider.Query(helper.GetHashCode(), width, points, disps, normals, velocities);
        if (status != 0) return false;
        Assert.That(disps[0].magnitude, Is.LessThan(.0001f));
        Assert.That(Vector3.Distance(normals[0], Vector3.up), Is.LessThan(.0001f));
        Assert.That(velocities[0].magnitude, Is.LessThan(.0001f));
        return true;
    }

    [UnityTest]
    public IEnumerator MassIndependentDraftRightingAndBothNormalModesFollowOriginalForces()
    {
        Open();
        yield return new EnterPlayMode();
        Setup("BoatAlign", -.5f, false);
        yield return Warm();
        // Hold translation/yaw to isolate vertical buoyancy and pitch/roll.
        // Neither restoring torque nor vertical motion is constrained.
        foreach (var body in _bodies) body.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationY;
        yield return Settle("FlatDraft", 8, false);
        Assert.That(_bodies[0].position.y, Is.EqualTo(_bodies[1].position.y).Within(.01f), "Acceleration buoyancy retains mass-independent draft");
        foreach (var body in _bodies)
        {
            body.rotation = Quaternion.Euler(12, 0, 18);
            body.linearVelocity = body.angularVelocity = Vector3.zero;
        }
        yield return Settle("Righting", 10, false);
        _bodies[0].mass = 2000;
        OceanRenderer.Instance.transform.position += Vector3.up * .75f;
        yield return Settle("MassDoubledSeaLevelRaised", 8, false);
        _boats[0]._useBoatLength = true;
        _boats[1]._useBoatLength = false;
        OceanRenderer.Instance.transform.position = Vector3.zero;
        _settings = ScriptableObject.CreateInstance<SimSettingsSeaFloorDepth>();
        _settings._allowVaryingWaterLevel = true;
        OceanRenderer.Instance._simSettingsSeaFloorDepth = _settings;
        _material = new Material(Shader.Find("Crest/Inputs/Sea Floor Depth/Set Base Water Height Using Geometry"));
        var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        Object.DestroyImmediate(plane.GetComponent<Collider>());
        plane.transform.localScale = Vector3.one * 2;
        plane.transform.SetPositionAndRotation(Vector3.up * 2, Quaternion.Euler(0, 0, Mathf.Atan(.25f) * Mathf.Rad2Deg));
        plane.GetComponent<Renderer>().sharedMaterial = _material;
        plane.AddComponent<RegisterHeightInput>();
        yield return Settle("SlopedHeightBothModes", 12, true);
    }

    IEnumerator Settle(string phase, float seconds, bool slope)
    {
        float start = Time.fixedTime, last = start, deadline = Time.realtimeSinceStartup + 30;
        int samples = 0;
        float maxYError = 0, maxAngle = 0, maxSpeed = 0, maxAngular = 0;
        while (Time.fixedTime - start < seconds && Time.realtimeSinceStartup < deadline)
        {
            yield return new WaitForFixedUpdate();
            if (Time.fixedTime <= last) continue;
            last = Time.fixedTime;
            bool tail = last - start >= seconds - 2;
            if (tail) samples++;
            for (int i = 0; i < 2; i++)
            {
                var body = _bodies[i];
                var normal = slope ? new Vector3(-.25f, 1, 0).normalized : Vector3.up;
                // On this X slope/yaw0 the length normal loses its X component.
                // Original additive torques align to their sum, not to normal alone.
                if (_boats[i]._useBoatLength) normal = (normal + new Vector3(0, normal.y, 0)).normalized;
                float surface = slope ? 2 + .25f * body.position.x : OceanRenderer.Instance.SeaLevel;
                float expectedY = surface - _boats[i]._bottomH - Mathf.Pow(Physics.gravity.magnitude / _boats[i]._buoyancyCoeff, 1f / 3);
                float error = Mathf.Abs(body.position.y - expectedY);
                float angle = Vector3.Angle(body.rotation * Vector3.up, normal);
                float speed = body.linearVelocity.magnitude, angular = body.angularVelocity.magnitude;
                Assert.That(float.IsFinite(body.position.y) && float.IsFinite(angle) && float.IsFinite(speed) && float.IsFinite(angular), Is.True);
                Assert.That(body.position.y, Is.InRange(-5, 5));
                Assert.That(speed, Is.LessThan(15));
                Assert.That(angular, Is.LessThan(10));
                if (!tail) continue;
                File.AppendAllText(_report, $"{phase}; T={last - start:R}; Boat={i}; Mass={body.mass:R}; LengthMode={_boats[i]._useBoatLength}; Y={body.position.y:R}; ExpectedY={expectedY:R}; Up={body.transform.up:F6}; ExpectedUp={normal:F6}; AngleError={angle:R}; Speed={speed:R}; Angular={angular:R}; InWater={_boats[i].InWater}\n");
                maxYError = Mathf.Max(maxYError, error);
                maxAngle = Mathf.Max(maxAngle, angle);
                maxSpeed = Mathf.Max(maxSpeed, speed);
                maxAngular = Mathf.Max(maxAngular, angular);
                Assert.That(error, Is.LessThan(.03f), phase + " cubic buoyancy balance");
                Assert.That(angle, Is.LessThan(1), phase + " original torque equilibrium");
                Assert.That(speed, Is.LessThan(.12f));
                Assert.That(angular, Is.LessThan(.1f));
                Assert.That(_boats[i].InWater, Is.True);
            }
        }
        File.AppendAllText(_report, $"Summary={phase}; TailSamples={samples}; Seconds={Time.fixedTime - start:R}; MaxYError={maxYError:R}; MaxAngleError={maxAngle:R}; MaxSpeed={maxSpeed:R}; MaxAngular={maxAngular:R}\n");
        Assert.That(Time.fixedTime - start, Is.GreaterThanOrEqualTo(seconds));
        Assert.That(samples, Is.GreaterThanOrEqualTo(95));
    }

    [UnityTest]
    public IEnumerator AccelerationCapDryStateAndProgrammaticControlsRetainOriginalUnits()
    {
        Open();
        yield return new EnterPlayMode();
        Setup("BoatAlignForces", -2, true);
        yield return Warm();
        yield return Forces("Unlimited", Mathf.Infinity, 0, 0, 12, true);
        yield return Forces("CapAcceleration3", 3, 0, 0, 3, true);
        yield return Forces("ZeroCap", 0, 0, 0, 0, true);
        yield return Forces("ForwardAndSteer", 0, .5f, .4f, 0, true);
        yield return Forces("ReverseAndSteer", 0, -.5f, -.4f, 0, true);
        MoveHeld(1);
        yield return Forces("DryAboveWater", Mathf.Infinity, .5f, .4f, 0, false);
        MoveHeld(0);
        yield return Forces("ExactSurfaceIsDry", Mathf.Infinity, .5f, .4f, 0, false);
        MoveHeld(-.01f);
        yield return Forces("JustSubmerged", Mathf.Infinity, 0, 0, 1.5e-6f, true);
    }

    void MoveHeld(float y)
    {
        // The controller samples Transform.position in FixedUpdate. Synchronise
        // the teleported Transform and physics pose before measuring the next step.
        foreach (var body in _bodies) body.transform.position = new Vector3(body.position.x, y, 0);
        Physics.SyncTransforms();
        foreach (var body in _bodies)
        {
            Assert.That(body.position.y, Is.EqualTo(y).Within(1e-6f));
            Assert.That(body.transform.position.y, Is.EqualTo(y).Within(1e-6f));
        }
    }

    IEnumerator Forces(string phase, float cap, float throttle, float steer, float accelerationY, bool wet)
    {
        foreach (var boat in _boats)
        {
            boat._maximumBuoyancyForce = cap;
            boat._throttleBias = throttle;
            boat._steerBias = steer;
        }
        int samples = 0, last = _observers[0].Samples;
        float deadline = Time.realtimeSinceStartup + 15, maxForceError = 0, maxAlphaError = 0;
        while (samples < 8 && Time.realtimeSinceStartup < deadline)
        {
            yield return new WaitForFixedUpdate();
            if (_observers[0].Samples <= last) continue;
            last = _observers[0].Samples;
            samples++;
            for (int i = 0; i < 2; i++)
            {
                var observed = _observers[i];
                var expected = _bodies[i].mass * (Vector3.up * accelerationY +
                    observed.Rotation * Vector3.forward * (wet ? _boats[i]._enginePower * throttle : 0));
                float forceError = Vector3.Distance(observed.Force, expected);
                var local = Quaternion.Inverse(observed.InertiaRotation) * observed.Torque;
                var alpha = observed.InertiaRotation * new Vector3(local.x / observed.Inertia.x, local.y / observed.Inertia.y, local.z / observed.Inertia.z);
                float alphaError = Vector3.Distance(alpha, Vector3.up * (wet ? _boats[i]._turnPower * steer : 0));
                File.AppendAllText(_report, $"{phase}; Sample={samples}; Boat={i}; Mass={_bodies[i].mass:R}; InWater={_boats[i].InWater}; Force={observed.Force:F6}; Expected={expected:F6}; Torque={observed.Torque:F6}; Inertia={observed.Inertia:F6}; DerivedAlpha={alpha:F6}; ForceError={forceError:R}; AlphaError={alphaError:R}\n");
                maxForceError = Mathf.Max(maxForceError, forceError);
                maxAlphaError = Mathf.Max(maxAlphaError, alphaError);
                Assert.That(_boats[i].InWater, Is.EqualTo(wet));
                Assert.That(float.IsFinite(forceError) && float.IsFinite(alphaError), Is.True, "Reject NaN comparisons explicitly");
                Assert.That(Mathf.Min(observed.Inertia.x, observed.Inertia.y, observed.Inertia.z), Is.GreaterThan(0));
                Assert.That(forceError, Is.LessThan(.05f), "Cap is acceleration, so Newton force scales with mass");
                Assert.That(alphaError, Is.LessThan(.002f));
            }
        }
        File.AppendAllText(_report, $"Summary={phase}; Samples={samples}; MaxForceError={maxForceError:R}; MaxAlphaError={maxAlphaError:R}\n");
        Assert.That(samples, Is.EqualTo(8));
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = _scale;
        Time.captureFramerate = _capture;
        if (_ownedKeyboard != null && _ownedKeyboard.added) InputSystem.RemoveDevice(_ownedKeyboard);
        _ownedKeyboard = null;
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (_material != null) Object.DestroyImmediate(_material);
        if (_settings != null) Object.DestroyImmediate(_settings);
    }
}
