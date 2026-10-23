using System.Collections;
using System.IO;
using System.Reflection;
using Crest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Rendering;

// Paces the numerical wave fixture so unthrottled CPU simulation cannot run
// seconds ahead of its GPU. This is intentionally not a performance/latency test.
[DefaultExecutionOrder(10000)]
public class BoatProbesQueryFence : MonoBehaviour
{
    void LateUpdate() => AsyncGPUReadback.WaitAllRequests();
}

public class BoatProbesTests
{
    readonly BoatProbes[] _boats = new BoatProbes[2];
    readonly Rigidbody[] _bodies = new Rigidbody[2];
    float _timeScale, _fixedDelta;
    int _captureRate;
    string _report;
    OceanWaveSpectrum _stormSpectrum;
    static readonly FieldInfo QueryPoints = typeof(BoatProbes).GetField("_queryPoints", BindingFlags.Instance | BindingFlags.NonPublic);

    [UnityTest]
    public IEnumerator DifferentMassBoatsSettleRightThemselvesAndFollowSeaLevel()
    {
        _timeScale = Time.timeScale;
        _fixedDelta = Time.fixedDeltaTime;
        _captureRate = Time.captureFramerate;
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        Time.timeScale = 1;
        Time.fixedDeltaTime = .02f;
        Time.captureFramerate = 60;
        Assert.That(Physics.simulationMode, Is.EqualTo(SimulationMode.FixedUpdate));
        var ocean = OceanRenderer.Instance;
        Camera.main.transform.position = new Vector3(0, 4, 0);
        foreach (var shape in Object.FindObjectsByType<ShapeWaves>()) shape.enabled = false;
        _report = $"Evidence/BoatProbes-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report, "Original BoatProbes and automatic FixedUpdate physics; flat GPU water. Mass1000/5000kg, hull width2/4m, length4/8m, four equal-weight corner probes at COM.y=-.5m. Force multiplier10, angular damping2, original water drag. Expected root Y=seaLevel+.5-mass/(1000*forceMultiplier). No snapshots/render assertions: numerical buoyancy/righting tests only.\n");
        CreateBoat(0, 1000, 2, 4, -8);
        CreateBoat(1, 5000, 4, 8, 8);
        yield return null;
        yield return WaitForFlatQueries(ocean);
        foreach (var body in _bodies) body.constraints = RigidbodyConstraints.None;
        yield return Settle("Initial", 8);
        float lightDraft = ocean.SeaLevel + .5f - _bodies[0].position.y;
        float heavyDraft = ocean.SeaLevel + .5f - _bodies[1].position.y;
        Assert.That(heavyDraft / lightDraft, Is.EqualTo(5).Within(.1f), "Original ForceMode.Force buoyancy must retain mass-dependent draft.");
        foreach (var body in _bodies)
        {
            body.rotation = Quaternion.Euler(12, 0, 18);
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            Assert.That(Vector3.Angle(body.rotation * Vector3.up, Vector3.up), Is.GreaterThan(20));
        }
        yield return Settle("RightingFromPitch12Roll18", 10);
        _bodies[0].mass = 2000;
        ocean.transform.position += Vector3.up * .75f;
        yield return null;
        Assert.That(ocean.SeaLevel, Is.EqualTo(.75f).Within(.001f));
        yield return Settle("MassDoubledAndSeaLevelRaised", 8);
        File.AppendAllText(_report, "All three phases passed; 26 simulated physics seconds, no original force/query code modified.\n");
    }

    [UnityTest]
    public IEnumerator BoatsRemainStableWithWavesWindSpectrumAndFrozenTime()
    {
        _timeScale = Time.timeScale;
        _fixedDelta = Time.fixedDeltaTime;
        _captureRate = Time.captureFramerate;
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        Time.timeScale = 1;
        Time.fixedDeltaTime = .02f;
        Time.captureFramerate = 60;
        var ocean = OceanRenderer.Instance;
        Camera.main.transform.position = new Vector3(0, 4, 0);
        foreach (var shape in Object.FindObjectsByType<ShapeWaves>()) shape.enabled = false;
        ocean.gameObject.AddComponent<BoatProbesQueryFence>();
        _report = $"Evidence/BoatProbesWaves-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report, "Original BoatProbes on original FFT/choppy waves; masses1000/5000kg, widths2/4m. Test-only LateUpdate GPU readback fence paces capture60 physics; not a wall-clock performance or unpaced latency test. 12s waves +12s changed public wind/spectrum +6s frozen ocean time.\n");
        CreateBoat(0, 1000, 2, 4, -8);
        CreateBoat(1, 5000, 4, 8, 8);
        yield return null;
        yield return WaitForFlatQueries(ocean);
        _stormSpectrum = ScriptableObject.CreateInstance<OceanWaveSpectrum>();
        _stormSpectrum._chop = 1;
        _stormSpectrum._multiplier = 2;
        var powers = new SerializedObject(_stormSpectrum);
        for (int i = 0; i < OceanWaveSpectrum.NUM_OCTAVES; i++)
        {
            powers.FindProperty("_powerDisabled").GetArrayElementAtIndex(i).boolValue = i != 9;
            powers.FindProperty("_powerLog").GetArrayElementAtIndex(i).floatValue = -1;
        }
        powers.ApplyModifiedPropertiesWithoutUndo();
        ocean._globalWindZone = null;
        ocean._globalWindSpeed = 60;
        ocean._globalWindDirectionAngle = 0;
        var owner = new GameObject("Controlled storm FFT");
        owner.SetActive(false);
        var fft = owner.AddComponent<ShapeFFT>();
        fft._resolution = 64;
        fft._spectrum = _stormSpectrum;
        var configuration = new SerializedObject(fft);
        configuration.FindProperty("_spectrumFixedAtRuntime").boolValue = false;
        configuration.ApplyModifiedPropertiesWithoutUndo();
        owner.SetActive(true);
        foreach (var body in _bodies) body.constraints = RigidbodyConstraints.None;
        yield return ObserveWaves("Waves", 12, false);
        ocean._globalWindSpeed = 90;
        ocean._globalWindDirectionAngle = 60;
        _stormSpectrum._multiplier = 2.5f;
        Assert.That(fft.WindSpeed, Is.EqualTo(25).Within(.001f));
        Assert.That(fft.WaveDirectionHeadingAngle, Is.EqualTo(60));
        yield return ObserveWaves("ChangedWindAndSpectrum", 12, false);
        var clock = ocean.gameObject.AddComponent<TimeProviderCustom>();
        clock._overrideTime = clock._overrideDeltaTime = true;
        clock._time = ocean.CurrentTime;
        clock._deltaTime = 0;
        ocean.PushTimeProvider(clock);
        yield return ObserveWaves("FrozenOceanTime", 6, true);
    }

    IEnumerator ObserveWaves(string phase, float seconds, bool frozen)
    {
        float started = Time.fixedTime, last = started, deadline = Time.realtimeSinceStartup + 45;
        int ticks = 0, ready = 0;
        var wet = new int[2];
        var tiltMax = new float[2];
        var speedMax = new float[2];
        float minHeight = float.PositiveInfinity, maxHeight = float.NegativeInfinity, maxFrozenVelocity = 0;
        var location = new[] { Vector3.zero };
        var heights = new float[1];
        var velocities = new Vector3[1];
        int owner = phase.GetHashCode();
        var displacementsField = typeof(BoatProbes).GetField("_queryResultDisps", BindingFlags.Instance | BindingFlags.NonPublic);
        while (Time.fixedTime - started < seconds && Time.realtimeSinceStartup < deadline)
        {
            yield return new WaitForFixedUpdate();
            if (Time.fixedTime <= last) continue;
            last = Time.fixedTime;
            ticks++;
            float elapsed = Time.fixedTime - started;
            int status = OceanRenderer.Instance.CollisionProvider.Query(owner, 0, location, heights, null, velocities);
            if (status == 0 && elapsed > 2)
            {
                ready++;
                minHeight = Mathf.Min(minHeight, heights[0]);
                maxHeight = Mathf.Max(maxHeight, heights[0]);
                if (frozen) maxFrozenVelocity = Mathf.Max(maxFrozenVelocity, velocities[0].magnitude);
            }
            for (int i = 0; i < 2; i++)
            {
                var body = _bodies[i];
                float tilt = Vector3.Angle(body.rotation * Vector3.up, Vector3.up);
                float speed = body.linearVelocity.magnitude;
                Assert.That(float.IsFinite(body.position.x) && float.IsFinite(body.position.y) && float.IsFinite(body.position.z) && float.IsFinite(speed) && float.IsFinite(body.angularVelocity.magnitude), Is.True);
                Assert.That(body.position.y - OceanRenderer.Instance.SeaLevel, Is.InRange(-10, 10), phase + " bounded vertical position");
                Assert.That(body.position.magnitude, Is.LessThan(100));
                Assert.That(tilt, Is.LessThan(65), phase + " bounded pitch/roll");
                Assert.That(speed, Is.LessThan(20));
                Assert.That(body.angularVelocity.magnitude, Is.LessThan(15));
                var points = QueryPoints.GetValue(_boats[i]) as Vector3[];
                var displacements = displacementsField.GetValue(_boats[i]) as Vector3[];
                bool inWater = false;
                for (int point = 0; point < 4; point++)
                    inWater |= OceanRenderer.Instance.SeaLevel + displacements[point].y > points[point].y;
                if (inWater) wet[i]++;
                tiltMax[i] = Mathf.Max(tiltMax[i], tilt);
                speedMax[i] = Mathf.Max(speedMax[i], speed);
                if (ticks % 25 == 0) File.AppendAllText(_report, $"{phase}; Time={elapsed:R}; Boat={i}; Position={body.position:F6}; Tilt={tilt:R}; Speed={speed:R}; Wet={inWater}; ReferenceStatus={status}; ReferenceHeight={heights[0]:R}\n");
            }
        }
        File.AppendAllText(_report, $"Phase={phase}; Ticks={ticks}; SimulatedSeconds={Time.fixedTime - started:R}; Ready={ready}; ReferenceMin={minHeight:R}; ReferenceMax={maxHeight:R}; Span={maxHeight - minHeight:R}; MaxFrozenVelocity={maxFrozenVelocity:R}\n");
        Assert.That(Time.fixedTime - started, Is.GreaterThanOrEqualTo(seconds));
        Assert.That(ready, Is.GreaterThan(100));
        if (frozen)
        {
            Assert.That(maxHeight - minHeight, Is.LessThan(.005f), "Fixed world query must stay on the frozen field.");
            Assert.That(maxFrozenVelocity, Is.LessThan(.005f), "Unity query timestamps still advance while ocean time is frozen.");
        }
        else Assert.That(maxHeight - minHeight, Is.GreaterThan(1), "Require measured metre-scale waves, not a nominal storm setting.");
        for (int i = 0; i < 2; i++)
        {
            File.AppendAllText(_report, $"Summary={phase}; Boat={i}; WetTicks={wet[i]}; TotalTicks={ticks}; MaxTilt={tiltMax[i]:R}; MaxSpeed={speedMax[i]:R}\n");
            Assert.That(wet[i] / (float)ticks, Is.GreaterThan(.4f), "Require actual immersed probes; BoatProbes.InWater is always true in the original.");
        }
    }

    void CreateBoat(int index, float mass, float width, float length, float x)
    {
        var owner = new GameObject("Probe boat " + index);
        owner.SetActive(false);
        owner.transform.position = new Vector3(x, 1.5f, 0);
        owner.AddComponent<BoxCollider>().size = new Vector3(width, .6f, length);
        var body = owner.AddComponent<Rigidbody>();
        body.mass = mass;
        body.angularDamping = 2;
        body.linearDamping = 0;
        body.constraints = RigidbodyConstraints.FreezeAll;
        var boat = owner.AddComponent<BoatProbes>();
        boat._playerControlled = false;
        boat._forceMultiplier = 10;
        boat._minSpatialLength = width;
        var fields = new SerializedObject(boat);
        fields.FindProperty("_centerOfMass").vector3Value = new Vector3(0, -.5f, 0);
        var points = fields.FindProperty("_forcePoints");
        points.arraySize = 4;
        for (int i = 0; i < 4; i++)
        {
            var point = points.GetArrayElementAtIndex(i);
            point.FindPropertyRelative("_weight").floatValue = 1;
            point.FindPropertyRelative("_offsetPosition").vector3Value = new Vector3((i % 2 == 0 ? -1 : 1) * width * .4f, 0, (i < 2 ? -1 : 1) * length * .4f);
        }
        fields.ApplyModifiedPropertiesWithoutUndo();
        _boats[index] = boat;
        _bodies[index] = body;
        owner.SetActive(true);
    }

    IEnumerator WaitForFlatQueries(OceanRenderer ocean)
    {
        float deadline = Time.realtimeSinceStartup + 15;
        int consecutive = 0;
        while (consecutive < 8 && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            bool ready = true;
            foreach (var boat in _boats)
            {
                var points = QueryPoints.GetValue(boat) as Vector3[];
                if (points == null) { ready = false; continue; }
                var displacements = new Vector3[points.Length];
                var velocities = new Vector3[points.Length];
                int status = ocean.CollisionProvider.Query(boat.GetHashCode(), boat.ObjectWidth, points, displacements, null, velocities);
                if (status != 0) { ready = false; continue; }
                foreach (var value in displacements) Assert.That(value.magnitude, Is.LessThan(.0001f));
                foreach (var value in velocities) Assert.That(value.magnitude, Is.LessThan(.0001f));
            }
            consecutive = ready ? consecutive + 1 : 0;
        }
        File.AppendAllText(_report, $"Warmup; FullStatusZeroConsecutive={consecutive}; SeaLevel={ocean.SeaLevel:R}\n");
        Assert.That(consecutive, Is.EqualTo(8));
    }

    IEnumerator Settle(string phase, float seconds)
    {
        float started = Time.fixedTime, last = started, deadline = Time.realtimeSinceStartup + 30;
        int ticks = 0, windowSamples = 0;
        var errors = new float[2];
        var tilts = new float[2];
        var speeds = new float[2];
        var angularSpeeds = new float[2];
        while (Time.fixedTime - started < seconds && Time.realtimeSinceStartup < deadline)
        {
            yield return new WaitForFixedUpdate();
            if (Time.fixedTime <= last) continue;
            last = Time.fixedTime;
            ticks++;
            float elapsed = Time.fixedTime - started;
            bool window = elapsed >= seconds - 2;
            if (window) windowSamples++;
            for (int i = 0; i < _bodies.Length; i++)
            {
                var body = _bodies[i];
                float expected = OceanRenderer.Instance.SeaLevel + .5f - body.mass / (1000 * _boats[i]._forceMultiplier);
                float error = Mathf.Abs(body.position.y - expected);
                float tilt = Vector3.Angle(body.rotation * Vector3.up, Vector3.up);
                float speed = body.linearVelocity.magnitude, angular = body.angularVelocity.magnitude;
                Assert.That(float.IsFinite(body.position.x) && float.IsFinite(body.position.y) && float.IsFinite(body.position.z) && float.IsFinite(speed) && float.IsFinite(angular), Is.True);
                Assert.That(body.position.y, Is.InRange(-3, 4), "Boat escaped the known water column.");
                Assert.That(speed, Is.LessThan(15));
                Assert.That(angular, Is.LessThan(15));
                if (window)
                {
                    errors[i] = Mathf.Max(errors[i], error);
                    tilts[i] = Mathf.Max(tilts[i], tilt);
                    speeds[i] = Mathf.Max(speeds[i], speed);
                    angularSpeeds[i] = Mathf.Max(angularSpeeds[i], angular);
                }
                if (ticks % 25 == 0) File.AppendAllText(_report, $"{phase}; Time={elapsed:R}; Boat={i}; Mass={body.mass:R}; Y={body.position.y:R}; ExpectedY={expected:R}; Tilt={tilt:R}; Speed={speed:R}; AngularSpeed={angular:R}\n");
            }
        }
        File.AppendAllText(_report, $"Phase={phase}; FixedTicks={ticks}; SimulatedSeconds={Time.fixedTime - started:R}; WindowSamples={windowSamples}\n");
        Assert.That(Time.fixedTime - started, Is.GreaterThanOrEqualTo(seconds));
        Assert.That(windowSamples, Is.GreaterThanOrEqualTo(95));
        for (int i = 0; i < _bodies.Length; i++)
        {
            File.AppendAllText(_report, $"Summary={phase}; Boat={i}; MaxFinal2sHeightError={errors[i]:R}; MaxFinal2sTilt={tilts[i]:R}; MaxFinal2sSpeed={speeds[i]:R}; MaxFinal2sAngularSpeed={angularSpeeds[i]:R}\n");
            Assert.That(errors[i], Is.LessThan(.03f), phase + " analytic equilibrium");
            Assert.That(tilts[i], Is.LessThan(1.5f), phase + " restore upright pitch/roll");
            Assert.That(speeds[i], Is.LessThan(.12f), phase + " settled linear velocity");
            Assert.That(angularSpeeds[i], Is.LessThan(.1f), phase + " settled angular velocity");
        }
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = _timeScale;
        Time.fixedDeltaTime = _fixedDelta;
        Time.captureFramerate = _captureRate;
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (_stormSpectrum != null) Object.DestroyImmediate(_stormSpectrum);
    }
}
