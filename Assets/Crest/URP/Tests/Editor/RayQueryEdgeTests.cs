using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Crest;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public class RayQueryEdgeTests
{
    Material _material;
    SimSettingsSeaFloorDepth _settings;
    float _timeScale;
    int _capture;
    string _report;
    static readonly FieldInfo ResultTime = typeof(QueryBase).GetField("_queryResultsTime", BindingFlags.Instance | BindingFlags.NonPublic);

    // The extra public Query posts exactly the same owner/positions within the
    // same call, without a dispatch between calls. It exposes readiness because
    // Trace's bool alone cannot distinguish an unavailable packet from a miss.
    class Probe
    {
        public readonly RayTraceHelper Ray;
        readonly Vector3[] _points, _results;
        readonly float _step;

        public Probe(float length, float requestedStep)
        {
            int count = Mathf.CeilToInt(length / requestedStep) + 1;
            _step = requestedStep;
            if (count > 128)
            {
                count = 128;
                _step = length / 127;
                LogAssert.Expect(LogType.Warning, new Regex(@"^Crest: RayTraceHelper: ray steps exceed maximum \(128\), step size increased to .+ to reduce step count\.$"));
            }
            Ray = new RayTraceHelper(length, requestedStep);
            var posted = (Vector3[])typeof(RayTraceHelper).GetField("_queryPos", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Ray);
            Assert.That(posted.Length, Is.EqualTo(count), "Original sample count/cap");
            _points = new Vector3[count];
            _results = new Vector3[count];
        }

        public int Trace(Vector3 origin, Vector3 direction, out bool hit, out float distance)
        {
            Ray.Init(origin, direction);
            for (int i = 0; i < _points.Length; i++) _points[i] = origin + i * _step * direction;
            var posted = (Vector3[])typeof(RayTraceHelper).GetField("_queryPos", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Ray);
            for (int i = 0; i < posted.Length; i++)
                Assert.That(Vector3.Distance(posted[i], _points[i]), Is.LessThan(1e-6f), "Sample spacing and endpoint");
            hit = Ray.Trace(out distance);
            return OceanRenderer.Instance.CollisionProvider.Query(Ray.GetHashCode(), _step * 4, _points, _results, null, null);
        }
    }

    [UnityTest]
    public IEnumerator EndpointsSampleCapAndMovedRaysRetainOriginalSemantics()
    {
        _timeScale = Time.timeScale;
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
        new GameObject("Test-only ray readback fence").AddComponent<BoatProbesQueryFence>();
        _report = $"Evidence/RayEdges-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report, "Original RayTraceHelper: flat exact endpoints, cap128, then changed origin/direction on y=2+.25*x. Readiness observed with identical public Query, not private owner gates. Eight distinct successful timestamps per state. Distance tolerance .005m. Test-only fence; no latency/performance claim.\n");
        for (int i = 0; i < 8; i++) yield return null;
        float sea = ocean.SeaLevel;
        yield return Check(new Probe(4, .5f), "StartDown", Vector3.up * sea, Vector3.down, false, -1);
        yield return Check(new Probe(4, .5f), "StartUp", Vector3.up * sea, Vector3.up, true, 0);
        yield return Check(new Probe(4, .5f), "EndDown", Vector3.up * (sea + 4), Vector3.down, true, 4);
        yield return Check(new Probe(4, .5f), "EndUp", Vector3.up * (sea - 4), Vector3.up, false, -1);
        yield return Check(new Probe(4, .5f), "TangentOnSurface", Vector3.up * sea, Vector3.forward, false, -1);
        yield return Check(new Probe(4, .5f), "JustInsideDown", Vector3.up * (sea + 3.99f), Vector3.down, true, 3.99f);
        yield return Check(new Probe(4, .5f), "JustOutsideDown", Vector3.up * (sea + 4.01f), Vector3.down, false, -1);
        yield return Check(new Probe(4, .5f), "JustInsideUp", Vector3.up * (sea - 3.99f), Vector3.up, true, 3.99f);
        yield return Check(new Probe(4, .5f), "JustOutsideUp", Vector3.up * (sea - 4.01f), Vector3.up, false, -1);
        yield return Check(new Probe(8, .01f), "CappedDown", Vector3.up * (sea + 3), Vector3.down, true, 3);
        yield return Check(new Probe(8, .01f), "CappedUp", Vector3.up * (sea - 3), Vector3.up, true, 3);

        _settings = ScriptableObject.CreateInstance<SimSettingsSeaFloorDepth>();
        _settings._allowVaryingWaterLevel = true;
        ocean._simSettingsSeaFloorDepth = _settings;
        _material = new Material(Shader.Find("Crest/Inputs/Sea Floor Depth/Set Base Water Height Using Geometry"));
        var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.transform.localScale = Vector3.one * 1.2f;
        plane.transform.SetPositionAndRotation(Vector3.up * 2, Quaternion.Euler(0, 0, Mathf.Atan(.25f) * Mathf.Rad2Deg));
        plane.GetComponent<Renderer>().sharedMaterial = _material;
        plane.AddComponent<RegisterHeightInput>();
        for (int i = 0; i < 8; i++) yield return null;
        var moved = new Probe(8, .5f);
        yield return Check(moved, "BeforeMove", new Vector3(-2, 6, 0), Vector3.down, true, 4.5f);
        CheckImmediateOldPacket(moved, "OriginChangedImmediate", new Vector3(2, 6, 0), Vector3.down, 4.5f, 3.5f);
        yield return Check(moved, "OriginChangedRecovery", new Vector3(2, 6, 0), Vector3.down, true, 3.5f, 4.5f);
        var oblique = new Vector3(.6f, -.8f, 0);
        float intersection = (1.5f - 6) / (-.8f - .25f * .6f);
        // Returned samples still all have height2.5 from the previous vertical ray.
        CheckImmediateOldPacket(moved, "DirectionChangedImmediate", new Vector3(-2, 6, 0), oblique, 3.5f / .8f, intersection);
        yield return Check(moved, "DirectionChangedRecovery", new Vector3(-2, 6, 0), oblique, true, intersection, 3.5f / .8f);
    }

    void CheckImmediateOldPacket(Probe probe, string phase, Vector3 origin, Vector3 direction, float oldPacketDistance, float currentIntersection)
    {
        int status = probe.Trace(origin, direction, out bool hit, out float distance);
        File.AppendAllText(_report, $"{phase}; Status={status}; Hit={hit}; Distance={distance:R}; ExpectedOldPacket={oldPacketDistance:R}; CurrentIntersection={currentIntersection:R}\n");
        Assert.That(status, Is.Zero, phase + " old packet remains publicly ready");
        Assert.That(hit, Is.True);
        Assert.That(distance, Is.EqualTo(oldPacketDistance).Within(.005f), "Retain original asynchronous position semantics");
        Assert.That(Mathf.Abs(distance - currentIntersection), Is.GreaterThan(.3f), "Disclose stale-position distance rather than claiming synchronous intersection");
    }

    IEnumerator Check(Probe probe, string phase, Vector3 origin, Vector3 direction, bool expectedHit, float expected, float? previousPacketDistance = null)
    {
        float last = -1, deadline = Time.realtimeSinceStartup + 15, maxError = 0;
        int accepted = 0, attempts = 0, stale = 0;
        while (accepted < 8 && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            int status = probe.Trace(origin, direction, out bool hit, out float distance);
            attempts++;
            float timestamp = (float)ResultTime.GetValue(OceanRenderer.Instance.CollisionProvider);
            File.AppendAllText(_report, $"{phase}; Attempt={attempts}; Status={status}; Timestamp={timestamp:R}; Hit={hit}; Distance={distance:R}; Expected={expected:R}\n");
            if (!OceanRenderer.Instance.CollisionProvider.RetrieveSucceeded(status)) continue;
            Assert.That(status, Is.Zero);
            Assert.That(hit, Is.EqualTo(expectedHit), phase + " ready packet");
            if (!expectedHit) Assert.That(distance, Is.EqualTo(-1));
            else
            {
                Assert.That(float.IsFinite(distance), Is.True);
                float error = Mathf.Abs(distance - expected);
                // During a changed ray every successful result must match either
                // the known old packet or the new analytic plane intersection.
                if (error >= .005f && previousPacketDistance.HasValue)
                {
                    Assert.That(distance, Is.EqualTo(previousPacketDistance.Value).Within(.005f), phase + " known old layout positions");
                    stale++;
                    continue;
                }
                Assert.That(error, Is.LessThan(.005f), phase + " analytic distance");
                maxError = Mathf.Max(maxError, error);
            }
            if (timestamp > last) { accepted++; last = timestamp; }
        }
        File.AppendAllText(_report, $"Summary={phase}; Accepted={accepted}; Attempts={attempts}; Stale={stale}; MaxDistanceError={maxError:R}\n");
        Assert.That(accepted, Is.EqualTo(8), phase + " needs fresh ready data, not unready misses");
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = _timeScale;
        Time.captureFramerate = _capture;
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (_material != null) Object.DestroyImmediate(_material);
        if (_settings != null) Object.DestroyImmediate(_settings);
    }
}
