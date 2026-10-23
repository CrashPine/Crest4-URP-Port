using System.Collections;
using System.IO;
using System.Reflection;
using Crest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

// Observes the original controller after its FixedUpdate and before simulation.
// Adds no forces and does not replace the production physics loop.
[DefaultExecutionOrder(10000)]
public class BoatControlForceObserver : MonoBehaviour
{
    public Rigidbody Body;
    public int Samples;
    public float Stamp;
    public Vector3 Force, Torque, Velocity, Inertia;
    public Quaternion Rotation, InertiaRotation;
    void FixedUpdate()
    {
        Force = Body.GetAccumulatedForce();
        Torque = Body.GetAccumulatedTorque();
        Velocity = Body.linearVelocity;
        Rotation = Body.rotation;
        InertiaRotation = Body.rotation * Body.inertiaTensorRotation;
        Inertia = Body.inertiaTensor;
        Stamp = Time.fixedTime;
        Samples++;
    }
}

public class BoatProbesControlsTests
{
    BoatProbes _boat;
    Rigidbody _body;
    BoatControlForceObserver _observer;
    float _scale;
    int _capture;
    string _report;
    static readonly FieldInfo Points = typeof(BoatProbes).GetField("_queryPoints", BindingFlags.Instance | BindingFlags.NonPublic);

    void OpenScene()
    {
        _scale = Time.timeScale;
        _capture = Time.captureFramerate;
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
    }

    IEnumerator Setup(string name, float height)
    {
        Assert.That(Application.isPlaying, Is.True, "EnterPlayMode must be yielded by the top-level UnityTest.");
        Time.timeScale = 1;
        Camera.main.transform.position = new Vector3(0, 4, 0);
        Assert.That(Time.fixedDeltaTime, Is.EqualTo(.02f).Within(.000001f), "Use the configured physics step without round-tripping Unity rational project settings through float.");
        Time.captureFramerate = 60;
        foreach (var shape in Object.FindObjectsByType<ShapeWaves>()) shape.enabled = false;
        _report = $"Evidence/{name}-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report, "Original BoatProbes, automatic FixedUpdate physics, flat GPU water, no image assertions. Observer reads accumulated force/torque after controller and adds no force.\n");
        var owner = new GameObject(name);
        owner.SetActive(false);
        owner.transform.position = new Vector3(0, height, 0);
        owner.AddComponent<BoxCollider>().size = new Vector3(2, .6f, 4);
        _body = owner.AddComponent<Rigidbody>();
        _body.mass = 1000;
        _body.linearDamping = 0;
        _body.angularDamping = 2;
        _body.constraints = RigidbodyConstraints.FreezeAll;
        _boat = owner.AddComponent<BoatProbes>();
        _boat._playerControlled = false;
        _boat._minSpatialLength = 2;
        var fields = new SerializedObject(_boat);
        fields.FindProperty("_centerOfMass").vector3Value = new Vector3(0, -.5f, 0);
        var points = fields.FindProperty("_forcePoints");
        points.arraySize = 4;
        for (int i = 0; i < 4; i++)
        {
            var point = points.GetArrayElementAtIndex(i);
            point.FindPropertyRelative("_weight").floatValue = 1;
            point.FindPropertyRelative("_offsetPosition").vector3Value = new Vector3(i % 2 == 0 ? -.8f : .8f, 0, i < 2 ? -1.6f : 1.6f);
        }
        fields.ApplyModifiedPropertiesWithoutUndo();
        _observer = owner.AddComponent<BoatControlForceObserver>();
        _observer.Body = _body;
        owner.SetActive(true);
        yield return null;
        int ready = 0;
        float deadline = Time.realtimeSinceStartup + 15;
        while (ready < 8 && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            var locations = Points.GetValue(_boat) as Vector3[];
            if (locations == null) continue;
            var displacements = new Vector3[locations.Length];
            var velocities = new Vector3[locations.Length];
            int status = OceanRenderer.Instance.CollisionProvider.Query(_boat.GetHashCode(), 2, locations, displacements, null, velocities);
            ready = status == 0 ? ready + 1 : 0;
            if (status != 0) continue;
            foreach (var value in displacements) Assert.That(value.magnitude, Is.LessThan(.0001f));
            foreach (var value in velocities) Assert.That(value.magnitude, Is.LessThan(.0001f));
        }
        Assert.That(ready, Is.EqualTo(8));
    }

    [UnityTest]
    public IEnumerator ThrottleCoastReverseAndSignedSteeringKeepOriginalForces()
    {
        OpenScene();
        yield return new EnterPlayMode();
        yield return Setup("BoatControls", .4f);
        _body.constraints = RigidbodyConstraints.FreezeRotation;
        yield return Propulsion("Forward", .5f, 6, 3.5f, .04f);
        yield return Propulsion("Coast", 0, 4, 0, .08f);
        _body.mass = 2000;
        yield return Propulsion("ReverseAtDoubleMass", -.5f, 6, -3.5f, .04f);
        yield return Propulsion("StopBeforeTurn", 0, 4, 0, .08f);
        _body.constraints = RigidbodyConstraints.None;
        _body.angularVelocity = Vector3.zero;
        _boat._turningHeel = .35f;
        _boat._turnBias = .4f;
        int before = _observer.Samples;
        while (_observer.Samples <= before) yield return new WaitForFixedUpdate();
        var localTorque = Quaternion.Inverse(_observer.InertiaRotation) * _observer.Torque;
        var alpha = _observer.InertiaRotation * new Vector3(localTorque.x / _observer.Inertia.x, localTorque.y / _observer.Inertia.y, localTorque.z / _observer.Inertia.z);
        var expected = _observer.Rotation * new Vector3(0, .2f, .07f);
        File.AppendAllText(_report, $"SteeringFirstStep; Torque={_observer.Torque:F6}; Inertia={_observer.Inertia:F6}; DerivedAngularAcceleration={alpha:F6}; Expected={expected:F6}\n");
        Assert.That(Vector3.Distance(alpha, expected), Is.LessThan(.002f), "Original yaw + turningHeel angular acceleration before restoring torques develop.");
        yield return Steering("TurnPositive", .4f, 4, 1);
        yield return Steering("TurnNegative", -.4f, 4, -1);
        yield return Steering("TurnReleased", 0, 4, 0);
    }

    IEnumerator Propulsion(string phase, float bias, float seconds, float target, float tolerance)
    {
        _boat._engineBias = bias;
        float started = Time.fixedTime, deadline = Time.realtimeSinceStartup + 30;
        int last = _observer.Samples, ticks = 0;
        float maxForceError = 0, startZ = _body.position.z;
        while (Time.fixedTime - started < seconds && Time.realtimeSinceStartup < deadline)
        {
            yield return new WaitForFixedUpdate();
            if (_observer.Samples <= last) continue;
            last = _observer.Samples; ticks++;
            float expectedForce = _body.mass * (_boat._enginePower * bias - _boat._dragInWaterForward * _observer.Velocity.z);
            float error = Mathf.Abs(_observer.Force.z - expectedForce);
            maxForceError = Mathf.Max(maxForceError, error);
            if (ticks % 25 == 0) File.AppendAllText(_report, $"{phase}; Time={Time.fixedTime - started:R}; Mass={_body.mass:R}; Velocity={_body.linearVelocity:F6}; ForceZ={_observer.Force.z:R}; ExpectedForceZ={expectedForce:R}\n");
            Assert.That(error, Is.LessThan(.5f));
            CheckFinite();
        }
        File.AppendAllText(_report, $"Summary={phase}; Ticks={ticks}; SimulatedSeconds={Time.fixedTime - started:R}; FinalVelocityZ={_body.linearVelocity.z:R}; Target={target:R}; MaxForceErrorN={maxForceError:R}; DeltaZ={_body.position.z - startZ:R}\n");
        Assert.That(Time.fixedTime - started, Is.GreaterThanOrEqualTo(seconds));
        Assert.That(_body.linearVelocity.z, Is.EqualTo(target).Within(tolerance));
        if (bias > 0) Assert.That(_body.position.z - startZ, Is.GreaterThan(5));
        if (bias < 0) Assert.That(_body.position.z - startZ, Is.LessThan(-5));
    }

    IEnumerator Steering(string phase, float bias, float seconds, int sign)
    {
        _boat._turnBias = bias;
        float started = Time.fixedTime, deadline = Time.realtimeSinceStartup + 30;
        int last = _observer.Samples, ticks = 0;
        var forward = _body.rotation * Vector3.forward;
        float maxTilt = 0;
        while (Time.fixedTime - started < seconds && Time.realtimeSinceStartup < deadline)
        {
            yield return new WaitForFixedUpdate();
            if (_observer.Samples <= last) continue;
            last = _observer.Samples; ticks++;
            float tilt = Vector3.Angle(_body.rotation * Vector3.up, Vector3.up);
            maxTilt = Mathf.Max(maxTilt, tilt);
            CheckFinite();
            Assert.That(tilt, Is.LessThan(10));
        }
        float yaw = Vector3.SignedAngle(forward, _body.rotation * Vector3.forward, Vector3.up);
        File.AppendAllText(_report, $"Summary={phase}; Ticks={ticks}; YawChangeDegrees={yaw:R}; AngularVelocity={_body.angularVelocity:F6}; MaxTilt={maxTilt:R}\n");
        Assert.That(Time.fixedTime - started, Is.GreaterThanOrEqualTo(seconds));
        if (sign != 0)
        {
            Assert.That(yaw * sign, Is.GreaterThan(8));
            Assert.That(_body.angularVelocity.y * sign, Is.GreaterThan(.05f));
        }
        else Assert.That(Mathf.Abs(_body.angularVelocity.y), Is.LessThan(.005f));
    }

    [UnityTest]
    public IEnumerator BuoyancyCapIsPerProbeAndCanBeZeroOrInfinite()
    {
        OpenScene();
        yield return new EnterPlayMode();
        yield return Setup("BoatBuoyancyCap", -2);
        _boat._dragInWaterUp = _boat._dragInWaterRight = _boat._dragInWaterForward = 0;
        yield return CheckCap("FourCappedProbes", 1000, 4000);
        yield return CheckCap("ZeroCap", 0, 0);
        float expected = 10 * 1000 * Mathf.Abs(Physics.gravity.y) * 2.5f;
        yield return CheckCap("Unlimited", Mathf.Infinity, expected);
        // Unequal weights must retain normalization when the cap is removed.
        var fields = new SerializedObject(_boat);
        for (int i = 0; i < 4; i++) fields.FindProperty("_forcePoints").GetArrayElementAtIndex(i).FindPropertyRelative("_weight").floatValue = i + 1;
        fields.ApplyModifiedPropertiesWithoutUndo();
        yield return CheckCap("UnequalWeightsUnlimited", Mathf.Infinity, expected);
        yield return CheckCap("UnequalWeightsCapped", 1000, 4000);
    }

    IEnumerator CheckCap(string phase, float cap, float expected)
    {
        _boat._maximumBuoyancyForce = cap;
        int last = _observer.Samples, accepted = 0;
        float deadline = Time.realtimeSinceStartup + 10;
        while (accepted < 8 && Time.realtimeSinceStartup < deadline)
        {
            yield return new WaitForFixedUpdate();
            if (_observer.Samples <= last) continue;
            last = _observer.Samples;
            File.AppendAllText(_report, $"{phase}; Sample={accepted}; CapPerProbe={cap:R}; Force={_observer.Force:F6}; ExpectedY={expected:R}\n");
            Assert.That(_observer.Force.y, Is.EqualTo(expected).Within(.5f));
            Assert.That(Mathf.Abs(_observer.Force.x) + Mathf.Abs(_observer.Force.z), Is.LessThan(.01f));
            accepted++;
        }
        Assert.That(accepted, Is.EqualTo(8));
    }

    void CheckFinite()
    {
        Assert.That(float.IsFinite(_body.position.x) && float.IsFinite(_body.position.y) && float.IsFinite(_body.position.z) && float.IsFinite(_body.linearVelocity.magnitude) && float.IsFinite(_body.angularVelocity.magnitude), Is.True);
        Assert.That(_body.position.y, Is.InRange(-3, 3));
        Assert.That(_body.linearVelocity.magnitude, Is.LessThan(15));
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
