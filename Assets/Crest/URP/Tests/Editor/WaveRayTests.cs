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
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// Snapshot the positions actually present before OceanRenderer dispatch, independently
// of when the Editor's UnityTest coroutine resumes within the frame.
[DefaultExecutionOrder(-10000)]
public class WaveRayDispatchPosts : MonoBehaviour
{
    public Vector3[] Points;
    public readonly Dictionary<float, Vector3[]> Frames = new Dictionary<float, Vector3[]>();
    void LateUpdate()
    {
        if (Points != null) Frames[Time.time] = (Vector3[])Points.Clone();
    }
}

public class WaveRayTests
{
    const float Step = .05f, Length = 5;
    OceanWaveSpectrum _spectrum;
    float _scale;
    int _capture;
    string _report;
    GerstnerQueryCapture _field;
    static readonly FieldInfo ResultTime = typeof(QueryBase).GetField("_queryResultsTime", BindingFlags.Instance | BindingFlags.NonPublic);
    WaveRayDispatchPosts _posts;
    RayTraceHelper _ray;
    Vector3[] _points, _results;
    int _ready, _unready, _hits, _misses, _stale;
    float _maxHeightError, _maxDistanceError, _maxPositionGap;
    float _minDistance = float.PositiveInfinity, _maxDistance = float.NegativeInfinity;

    [UnityTest]
    public IEnumerator OriginalRayTracksWavePacketsAndRecoversAfterEndpointMotion()
    {
        _scale = Time.timeScale;
        _capture = Time.captureFramerate;
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        Time.timeScale = 1;
        Time.captureFramerate = 60;
        var ocean = OceanRenderer.Instance;
        Camera.main.transform.position = new Vector3(0, 4, 0);
        foreach (var shape in Object.FindObjectsByType<ShapeWaves>()) shape.enabled = false;
        _spectrum = ScriptableObject.CreateInstance<OceanWaveSpectrum>();
        _spectrum._chop = 0;
        _spectrum._waveDirectionVariance = 0;
        var powers = new SerializedObject(_spectrum);
        for (int i = 0; i < OceanWaveSpectrum.NUM_OCTAVES; i++)
        {
            powers.FindProperty("_powerDisabled").GetArrayElementAtIndex(i).boolValue = i != 9;
            powers.FindProperty("_powerLog").GetArrayElementAtIndex(i).floatValue = -2;
        }
        powers.ApplyModifiedPropertiesWithoutUndo();
        var owner = new GameObject("Original Gerstner for wave ray reference");
        owner.SetActive(false);
        var waves = owner.AddComponent<ShapeGerstner>();
        waves._spectrum = _spectrum;
        waves._randomSeed = 27;
        waves._componentsPerOctave = 2;
        waves._resolution = 128;
        waves._overrideGlobalWindSpeed = true;
        waves._windSpeed = 60;
        waves._overrideGlobalWindDirection = true;
        waves._waveDirectionHeadingAngle = 0;
        var fields = new SerializedObject(waves);
        fields.FindProperty("_respectShallowWaterAttenuation").floatValue = 0;
        fields.ApplyModifiedPropertiesWithoutUndo();
        owner.SetActive(true);
        float deadline = Time.realtimeSinceStartup + 15;
        while ((waves._waveBuffers == null || !waves._waveBuffers.IsCreated()) && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(waves._waveBuffers, Is.Not.Null);
        yield return null;
        _field = ocean.gameObject.AddComponent<GerstnerQueryCapture>();
        _field.Waves = waves;
        _field.Capture = true;
        _posts = ocean.gameObject.AddComponent<WaveRayDispatchPosts>();
        _ray = new RayTraceHelper(Length, Step);
        int count = Mathf.CeilToInt(Length / Step) + 1;
        Assert.That(count, Is.LessThanOrEqualTo(128));
        _points = new Vector3[count];
        _results = new Vector3[count];
        _report = $"Evidence/WaveRay-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report, "Original RayTraceHelper + original Gerstner chop0/octave9/seed27/two components. Independent combined RT scalar bilinear height reference at actual dispatch timestamp and posted positions. Central LOD0/1 and minSlice0 asserted for minLength=step*4; length5/step.05. Packet heights combined with current ray Y reproduces inherited asynchronous endpoint semantics. All ready results checked; no private readiness/layout gates. Tolerances height .002m, distance .005m. Test-only GPU fence, no latency/performance/fold/seam acceptance.\n");
        yield return Phase("FixedDown", new Vector3(-1, 2, 0), Vector3.down, 24, true);
        Observe("OriginImmediate", new Vector3(1, 2, .5f), Vector3.down, true);
        yield return Phase("OriginRecovery", new Vector3(1, 2, .5f), Vector3.down, 16, true);
        var oblique = new Vector3(.6f, -.8f, 0);
        Observe("DirectionImmediate", new Vector3(-1, 2, 0), oblique, true);
        yield return Phase("DirectionRecovery", new Vector3(-1, 2, 0), oblique, 16, true);
        for (int frame = 0; frame < 64; frame++)
        {
            yield return null;
            var origin = new Vector3(1.5f * Mathf.Sin(Time.time * 3), 2, .5f * Mathf.Cos(Time.time * 2));
            var direction = new Vector3(.25f * Mathf.Sin(Time.time * 4), -1, 0).normalized;
            Observe("ContinuousMotion", origin, direction, false);
        }
        yield return Phase("MotionRecovery", new Vector3(-1, 2, 0), Vector3.down, 16, true);
        yield return Phase("UpwardMiss", new Vector3(0, 2, 0), Vector3.up, 8, false);
        yield return Phase("FromBelow", new Vector3(0, -2, 0), Vector3.up, 8, true);
        File.AppendAllText(_report, $"Summary; Ready={_ready}; Unready={_unready}; Hits={_hits}; Misses={_misses}; StalePositionPackets={_stale}; MaxHeightError={_maxHeightError:R}; MaxDistanceError={_maxDistanceError:R}; MaxSynchronousPositionGap={_maxPositionGap:R}; FixedDistanceSpan={_maxDistance-_minDistance:R}\n");
        Assert.That(_unready, Is.GreaterThan(0), "Explicit initial unready path observed.");
        Assert.That(_stale, Is.GreaterThan(0), "Moving endpoint responses include old positions.");
        Assert.That(_maxPositionGap, Is.GreaterThan(.001f), "Disclose a measurable difference from synchronous current-position sampling.");
        Assert.That(_maxDistance - _minDistance, Is.GreaterThan(.03f), "Fixed ray intersects actual evolving waves.");
        Assert.That(_misses, Is.GreaterThan(0));
        _field.Capture = false;
        _field.Frames.Clear();
    }

    IEnumerator Phase(string phase, Vector3 origin, Vector3 direction, int required, bool hit)
    {
        float start = Time.time, last = -1, deadline = Time.realtimeSinceStartup + 20;
        int fresh = 0, attempts = 0;
        while (fresh < required && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            bool ready = Observe(phase, origin, direction, false);
            attempts++;
            if (!ready) continue;
            float time = (float)ResultTime.GetValue(OceanRenderer.Instance.CollisionProvider);
            if (time < start || time < last + .1f) continue;
            Assert.That(_lastHit, Is.EqualTo(hit), phase + " fresh hit/miss");
            last = time; fresh++;
        }
        File.AppendAllText(_report, $"Phase={phase}; Fresh={fresh}; Required={required}; Attempts={attempts}\n");
        Assert.That(fresh, Is.EqualTo(required));
    }

    bool _lastHit;
    bool Observe(string phase, Vector3 origin, Vector3 direction, bool requireReady)
    {
        _ray.Init(origin, direction);
        for (int i = 0; i < _points.Length; i++) _points[i] = origin + i * Step * direction;
        var posted = (Vector3[])typeof(RayTraceHelper).GetField("_queryPos", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_ray);
        for (int i = 0; i < _points.Length; i++) Assert.That(Vector3.Distance(posted[i], _points[i]), Is.LessThan(.000001f));
        bool hit = _ray.Trace(out float distance);
        int status = OceanRenderer.Instance.CollisionProvider.Query(_ray.GetHashCode(), Step * 4, _points, _results, null, null);
        _posts.Points = (Vector3[])_points.Clone();
        if (requireReady) Assert.That(status, Is.Zero, phase + " old packet remains publicly ready immediately after movement");
        if (status != 0)
        {
            Assert.That(hit, Is.False);
            Assert.That(distance, Is.EqualTo(-1));
            Assert.That(status & (int)QueryBase.QueryStatus.PostFailed, Is.Zero);
            _unready++;
            File.AppendAllText(_report, $"Unready={phase}; Status={status}\n");
            return false;
        }
        float time = (float)ResultTime.GetValue(OceanRenderer.Instance.CollisionProvider);
        Assert.That(_posts.Frames.TryGetValue(time, out var oldPoints), Is.True, "Returned packet must map to actual posted positions.");
        Assert.That(_field.Frames.TryGetValue(time, out var field), Is.True, "Returned packet must map to captured field.");
        Assert.That(Mathf.Floor(Mathf.Log(Mathf.Max(Step / field.Texels[0], 1), 2)), Is.Zero, "Ray minGrid=step must select minSlice0.");
        var heights = new double[_points.Length];
        var currentHeights = new double[_points.Length];
        bool stale = false;
        for (int i = 0; i < _points.Length; i++)
        {
            heights[i] = field.Height(oldPoints[i]) + OceanRenderer.Instance.SeaLevel;
            currentHeights[i] = field.Height(_points[i]) + OceanRenderer.Instance.SeaLevel;
            float error = Mathf.Abs(_results[i].y + OceanRenderer.Instance.SeaLevel - (float)heights[i]);
            if (!float.IsFinite(error) || error >= .002f) File.AppendAllText(_report, $"HeightFailure={phase}; Time={time:R}; Now={Time.time:R}; Point={i}; Posted={oldPoints[i]:F6}; Actual={_results[i].y:R}; Expected={heights[i]:R}; Error={error:R}\n");
            Assert.That(float.IsFinite(error) && error < .002f, Is.True, phase + " packet height");
            _maxHeightError = Mathf.Max(_maxHeightError, error);
            stale |= Vector3.Distance(oldPoints[i], _points[i]) > .00001f;
        }
        double expected = Intersection(heights), synchronous = Intersection(currentHeights);
        Assert.That(hit, Is.EqualTo(expected >= 0), phase + " packet-aware crossing");
        float distanceError = Mathf.Abs(distance - (float)expected);
        File.AppendAllText(_report, $"Result={phase}; Time={time:R}; Status={status}; Hit={hit}; Distance={distance:R}; PacketReference={expected:R}; CurrentPositionReference={synchronous:R}; DistanceError={distanceError:R}; StalePositions={stale}\n");
        Assert.That(float.IsFinite(distanceError) && distanceError < .005f, Is.True, phase + " discrete scalar intersection");
        _maxDistanceError = Mathf.Max(_maxDistanceError, distanceError);
        if (stale) _stale++;
        if (expected >= 0 && synchronous >= 0) _maxPositionGap = Mathf.Max(_maxPositionGap, (float)Math.Abs(expected - synchronous));
        if (phase == "FixedDown" && hit) { _minDistance = Mathf.Min(_minDistance, distance); _maxDistance = Mathf.Max(_maxDistance, distance); }
        _lastHit = hit; _ready++;
        if (hit) _hits++; else _misses++;
        foreach (float key in new List<float>(_field.Frames.Keys)) if (key < Time.time - 2 && key != time) _field.Frames.Remove(key);
        foreach (float key in new List<float>(_posts.Frames.Keys)) if (key < Time.time - 2 && key != time) _posts.Frames.Remove(key);
        return true;
    }

    double Intersection(double[] heights)
    {
        for (int i = 1; i < heights.Length; i++)
        {
            double a = heights[i-1] - _points[i-1].y, b = heights[i] - _points[i].y;
            if ((a >= 0) == (b >= 0)) continue;
            return (i - 1 + Math.Abs(a) / (Math.Abs(a) + Math.Abs(b))) * Step;
        }
        return -1;
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = _scale;
        Time.captureFramerate = _capture;
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (_spectrum != null) Object.DestroyImmediate(_spectrum);
        if (_posts != null) _posts.Frames.Clear();
    }
}
