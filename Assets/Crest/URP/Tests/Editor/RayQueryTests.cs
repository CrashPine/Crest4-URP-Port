using System.Collections;
using System.IO;
using System.Reflection;
using Crest;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public class RayQueryTests
{
    Material _material;
    SimSettingsSeaFloorDepth _settings;
    float _timeScale;
    int _captureFramerate;
    string _report;
    static readonly FieldInfo ResultTime = typeof(QueryBase).GetField("_queryResultsTime", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo Segments = typeof(QueryBase).GetField("_resultSegments", BindingFlags.Instance | BindingFlags.NonPublic);

    [UnityTest]
    public IEnumerator RaysIntersectKnownSlopedAndMovingWater()
    {
        _timeScale = Time.timeScale;
        _captureFramerate = Time.captureFramerate;
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        Time.timeScale = 1;
        Time.captureFramerate = 60;
        var ocean = OceanRenderer.Instance;
        Camera.main.transform.position = new Vector3(0, 4, 0);
        foreach (var shape in Object.FindObjectsByType<ShapeWaves>()) shape.enabled = false;
        _settings = ScriptableObject.CreateInstance<SimSettingsSeaFloorDepth>();
        _settings._allowVaryingWaterLevel = true;
        ocean._simSettingsSeaFloorDepth = _settings;
        _material = new Material(Shader.Find("Crest/Inputs/Sea Floor Depth/Set Base Water Height Using Geometry"));
        var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.transform.localScale = Vector3.one * 1.2f;
        plane.transform.SetPositionAndRotation(Vector3.up * 2, Quaternion.Euler(0, 0, Mathf.Atan(.25f) * Mathf.Rad2Deg));
        plane.GetComponent<Renderer>().sharedMaterial = _material;
        plane.AddComponent<RegisterHeightInput>();
        var motion = plane.AddComponent<QueryPlaneMotion>();
        motion.StartTime = Time.time;
        _report = $"Evidence/RayQuery-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report, "Original RayTraceHelper on y=2+0.25*x+speed*(Time.time-start). Normalized ray directions. Eight distinct owner-ready result timestamps per case; no-hit must also have ready data. Distance tolerance .05m. Non-integral requested length retains original ceil-sampling overshoot, characterized explicitly.\n");
        yield return Check("Down", new Vector3(0, 6, 0), Vector3.down, 8, .5f, true, motion);
        yield return Check("Oblique", new Vector3(-2, 5, 0), new Vector3(.6f, -.8f, 0), 6, .25f, true, motion);
        yield return Check("FromBelow", Vector3.zero, Vector3.up, 4, .5f, true, motion);
        yield return Check("ParallelMiss", new Vector3(0, 6, -2), Vector3.forward, 4, .5f, false, motion);
        yield return Check("TooShort", new Vector3(0, 6, 0), Vector3.down, 1, .25f, false, motion);
        // Legacy sampling goes to ceil(length/step)*step: 4m here, not 3.1m.
        yield return Check("LegacyCeilOvershoot", new Vector3(0, 5.5f, 0), Vector3.down, 3.1f, 2, true, motion);
        motion.StartTime = Time.time;
        motion.Speed = .5f;
        motion.Update();
        yield return Check("MovingDown", new Vector3(0, 6, 0), Vector3.down, 8, .5f, true, motion);
        yield return Check("MovingOblique", new Vector3(-2, 5, 0), new Vector3(.6f, -.8f, 0), 6, .25f, true, motion);
    }

    IEnumerator Check(string phase, Vector3 origin, Vector3 direction, float length, float step, bool expectedHit, QueryPlaneMotion motion)
    {
        var ray = new RayTraceHelper(length, step);
        float started = Time.time, last = -1, deadline = Time.realtimeSinceStartup + 15, maxError = 0;
        int accepted = 0, attempts = 0;
        while (accepted < 8 && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            ray.Init(origin, direction);
            bool hit = ray.Trace(out float distance);
            attempts++;
            var provider = OceanRenderer.Instance.CollisionProvider;
            float timestamp = (float)ResultTime.GetValue(provider);
            var segments = Segments.GetValue(provider) as IDictionary;
            if (segments == null || !segments.Contains(ray.GetHashCode()) || timestamp < started || timestamp <= last) continue;
            float expected = (2 + .25f * origin.x + motion.Speed * (timestamp - motion.StartTime) - origin.y) / (direction.y - .25f * direction.x);
            File.AppendAllText(_report, $"{phase}; Result={accepted}; Timestamp={timestamp:R}; Now={Time.time:R}; Hit={hit}; Distance={distance:R}; Expected={expected:R}; RequestedLength={length:R}; Step={step:R}\n");
            Assert.That(hit, Is.EqualTo(expectedHit), phase + " with owner-ready GPU results");
            if (expectedHit)
            {
                float error = Mathf.Abs(distance - expected);
                Assert.That(float.IsFinite(error), Is.True);
                Assert.That(error, Is.LessThan(.05f), phase + " analytic intersection at request time");
                maxError = Mathf.Max(maxError, error);
                if (phase == "LegacyCeilOvershoot") Assert.That(distance, Is.GreaterThan(length), "Disclose original ceil sampling behavior rather than claiming a strict maximum length.");
                else Assert.That(distance, Is.InRange(0, length));
            }
            else Assert.That(distance, Is.EqualTo(-1));
            last = timestamp;
            accepted++;
        }
        File.AppendAllText(_report, $"Summary={phase}; Accepted={accepted}; Attempts={attempts}; MaxDistanceError={maxError:R}\n");
        Assert.That(accepted, Is.EqualTo(8), "False before readback is not evidence of a miss.");
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = _timeScale;
        Time.captureFramerate = _captureFramerate;
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (_material != null) Object.DestroyImmediate(_material);
        if (_settings != null) Object.DestroyImmediate(_settings);
    }
}
