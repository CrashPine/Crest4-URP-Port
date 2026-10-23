using System.Collections;
using System.IO;
using System.Reflection;
using Crest;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

// Updates before OceanRenderer.LateUpdate so the geometry and query timestamp
// refer to the same Unity clock. No production query or compute code is changed.
public class QueryPlaneMotion : MonoBehaviour
{
    public float StartTime, Speed;
    public void Update() => transform.position = Vector3.up * (2 + Speed * (Time.time - StartTime));
}

public class QueryContractTests
{
    Material _heightMaterial;
    SimSettingsSeaFloorDepth _settings;
    float _timeScale;
    int _captureFramerate;
    string _report;
    static readonly FieldInfo ResultTime = typeof(QueryBase).GetField("_queryResultsTime", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo PreviousTime = typeof(QueryBase).GetField("_queryResultsTimeLast", BindingFlags.Instance | BindingFlags.NonPublic);

    [UnityTest]
    public IEnumerator SlopedNormalsMovingVelocityAndReadbackLatency()
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
        _heightMaterial = new Material(Shader.Find("Crest/Inputs/Sea Floor Depth/Set Base Water Height Using Geometry"));
        var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.transform.localScale = Vector3.one * 1.2f;
        plane.transform.SetPositionAndRotation(Vector3.up * 2, Quaternion.Euler(0, 0, Mathf.Atan(.25f) * Mathf.Rad2Deg));
        plane.GetComponent<Renderer>().sharedMaterial = _heightMaterial;
        plane.AddComponent<RegisterHeightInput>();
        var motion = plane.AddComponent<QueryPlaneMotion>();
        motion.StartTime = Time.time;
        _report = $"Evidence/QueryContract-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report, "Plane y=2+0.25*x+speed*(Unity Time.time-start); no waves. Direct Query status must be zero, two positive distinct result timestamps required. Twelve distinct GPU results per phase. Tolerances: height .05m, normal vector .015, velocity .08m/s. Latency is measured in Unity clock (captureFramerate=60), not GPU execution milliseconds.\n");
        yield return CheckPhase(ocean, motion, "Static", 0);
        motion.StartTime = Time.time;
        motion.Speed = 2;
        motion.Update();
        yield return CheckPhase(ocean, motion, "Moving", 2);
    }

    IEnumerator CheckPhase(OceanRenderer ocean, QueryPlaneMotion motion, string phase, float speed)
    {
        var points = new[] { new Vector3(-2, 0, 0), Vector3.zero, new Vector3(2, 0, 0) };
        var heights = new float[3];
        var normals = new Vector3[3];
        var velocities = new Vector3[3];
        int owner = GetHashCode(), accepted = 0, attempts = 0;
        float lastAccepted = -1, deadline = Time.realtimeSinceStartup + 15;
        float maxHeightError = 0, maxNormalError = 0, maxVelocityError = 0, maxAge = 0;
        var normal = new Vector3(-.25f, 1, 0).normalized;
        while (accepted < 12 && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            int status = ocean.CollisionProvider.Query(owner, 0, points, heights, normals, velocities);
            attempts++;
            float timestamp = (float)ResultTime.GetValue(ocean.CollisionProvider);
            float previous = (float)PreviousTime.GetValue(ocean.CollisionProvider);
            if (status != 0 || timestamp <= lastAccepted || previous < motion.StartTime + .1f) continue;
            Assert.That(timestamp - previous, Is.GreaterThan(.0001f));
            float age = Time.time - timestamp;
            Assert.That(age, Is.GreaterThanOrEqualTo(-.0001f));
            Assert.That(age, Is.LessThan(5), "Readback exceeds the fixture's 5 simulated second recovery budget.");
            maxAge = Mathf.Max(maxAge, age);
            for (int i = 0; i < points.Length; i++)
            {
                float expected = 2 + .25f * points[i].x + speed * (timestamp - motion.StartTime);
                float he = Mathf.Abs(heights[i] - expected);
                float ne = Vector3.Distance(normals[i], normal);
                float ve = Vector3.Distance(velocities[i], Vector3.up * speed);
                Assert.That(float.IsFinite(he) && float.IsFinite(ne) && float.IsFinite(ve), Is.True);
                maxHeightError = Mathf.Max(maxHeightError, he);
                maxNormalError = Mathf.Max(maxNormalError, ne);
                maxVelocityError = Mathf.Max(maxVelocityError, ve);
                File.AppendAllText(_report, $"{phase}; Result={accepted}; Point={i}; Status={status}; Now={Time.time:R}; Timestamp={timestamp:R}; Previous={previous:R}; Age={age:R}; Height={heights[i]:R}; Expected={expected:R}; Normal={normals[i]:F6}; Velocity={velocities[i]:F6}\n");
                Assert.That(he, Is.LessThan(.05f), phase + " height at request timestamp");
                Assert.That(ne, Is.LessThan(.015f), phase + " analytic normal");
                Assert.That(ve, Is.LessThan(.08f), phase + " finite difference velocity");
            }
            lastAccepted = timestamp;
            accepted++;
        }
        File.AppendAllText(_report, $"Summary={phase}; Accepted={accepted}; Attempts={attempts}; MaxHeightError={maxHeightError:R}; MaxNormalError={maxNormalError:R}; MaxVelocityError={maxVelocityError:R}; MaxSimulatedAge={maxAge:R}\n");
        Assert.That(accepted, Is.EqualTo(12), "Require fresh successful results, not repeated cached samples.");
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = _timeScale;
        Time.captureFramerate = _captureFramerate;
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (_heightMaterial != null) Object.DestroyImmediate(_heightMaterial);
        if (_settings != null) Object.DestroyImmediate(_settings);
    }
}
