using System.Collections;
using System.IO;
using System.Reflection;
using Crest;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public class QueryLayoutTests
{
    Material _material;
    SimSettingsSeaFloorDepth _settings;
    float _timeScale;
    int _capture;
    string _report;
    int _postedCount;
    bool _postedNormals;
    static readonly FieldInfo ResultTime = typeof(QueryBase).GetField("_queryResultsTime", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly Vector3 Normal = new Vector3(-.25f, 1, 0).normalized;

    [UnityTest]
    public IEnumerator OwnersCountsAndOptionalOutputsKeepResultsSeparated()
    {
        _timeScale = Time.timeScale;
        _capture = Time.captureFramerate;
        _postedCount = 0;
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        Assert.That(Application.isPlaying, Is.True);
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
        new GameObject("Test-only readback fence").AddComponent<BoatProbesQueryFence>();
        _report = $"Evidence/QueryLayout-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report, "Static y=2+.25*x. Every public successful retrieval is checked, including immediately after a layout change. Eight distinct successful timestamps per phase. Test-only fence; no latency claim. Height tolerance .05m; normal .015; velocity .05m/s.\n");
        // Let the height input reach the simulation before registering either owner.
        for (int i = 0; i < 8; i++) yield return null;
        yield return Phase(ocean, "Warm", 1, true, true, true, false);
        // Equal packet length: one point plus three normal samples -> four points.
        yield return Phase(ocean, "SamePacketDifferentLayout", 4, true, false, true, false);
        yield return Phase(ocean, "Grow", 5, true, true, true, false);
        yield return Phase(ocean, "Shrink", 2, true, true, true, false);
        yield return Phase(ocean, "NormalsOnly", 2, false, true, false, false);
        yield return Phase(ocean, "HeightsOnly", 2, true, false, false, false);
        yield return Phase(ocean, "VelocityOnly", 2, false, false, true, false);
        yield return Phase(ocean, "VelocityOnlyGrow", 5, false, false, true, false);
        yield return Phase(ocean, "Displacements", 2, true, true, true, true);
        ((QueryBase)ocean.CollisionProvider).RemoveQueryPoints(71001);
        for (int i = 0; i < 12; i++) yield return null;
        yield return Phase(ocean, "OwnerReadded", 5, true, true, true, false);
    }

    IEnumerator Phase(OceanRenderer ocean, string phase, int count, bool positions, bool normals, bool velocities, bool displacements)
    {
        bool layoutChanged = _postedCount > 0 && (_postedCount != count || _postedNormals != normals);
        var points = new Vector3[count];
        for (int i = 0; i < count; i++) points[i] = new Vector3(-2 + i, 0, 0);
        var heights = positions && !displacements ? new float[count] : null;
        var disps = positions && displacements ? new Vector3[count] : null;
        var norms = normals ? new Vector3[count] : null;
        var vels = velocities ? new Vector3[count] : null;
        var otherPoints = new[] { new Vector3(2.5f, 0, 0) };
        var otherHeights = new float[1];
        int accepted = 0, attempts = 0, notReady = 0, otherReady = 0;
        float maxHeightError = 0, maxNormalError = 0, maxVelocity = 0;
        float last = -1, deadline = Time.realtimeSinceStartup + 15;
        while (accepted < 8 && Time.realtimeSinceStartup < deadline)
        {
            // No yield between registration and checking: stale successful status must not be hidden.
            int otherStatus = ocean.CollisionProvider.Query(71002, 0, otherPoints, otherHeights, null, null);
            if (ocean.CollisionProvider.RetrieveSucceeded(otherStatus))
            {
                otherReady++;
                Assert.That(otherHeights[0], Is.EqualTo(2.625f).Within(.05f), phase + " second owner");
            }
            int status = displacements
                ? ocean.CollisionProvider.Query(71001, 0, points, disps, norms, vels)
                : ocean.CollisionProvider.Query(71001, 0, points, heights, norms, vels);
            float timestamp = (float)ResultTime.GetValue(ocean.CollisionProvider);
            attempts++;
            File.AppendAllText(_report, $"Phase={phase}; Attempt={attempts}; Status={status}; Timestamp={timestamp:R}; Count={count}\n");
            if (attempts == 1 && layoutChanged)
                Assert.That(ocean.CollisionProvider.RetrieveSucceeded(status), Is.False,
                    phase + " must not report the previous layout as ready before a new packet can be dispatched");
            if (ocean.CollisionProvider.RetrieveSucceeded(status))
            {
                for (int i = 0; i < count; i++)
                {
                    float expected = 2 + .25f * points[i].x;
                    if (heights != null) maxHeightError = Mathf.Max(maxHeightError, Mathf.Abs(heights[i] - expected));
                    if (disps != null) maxHeightError = Mathf.Max(maxHeightError, Vector3.Distance(disps[i], Vector3.up * expected));
                    if (heights != null) Assert.That(heights[i], Is.EqualTo(expected).Within(.05f), phase + " height " + i);
                    if (disps != null) Assert.That(Vector3.Distance(disps[i], Vector3.up * expected), Is.LessThan(.05f), phase + " displacement " + i);
                    if (norms != null)
                    {
                        maxNormalError = Mathf.Max(maxNormalError, Vector3.Distance(norms[i], Normal));
                        File.AppendAllText(_report, $"Point={i}; Normal={norms[i]:F8}; Expected={Normal:F8}\n");
                        Assert.That(Vector3.Distance(norms[i], Normal), Is.LessThan(.015f), phase + " normal " + i);
                    }
                }
            }
            else notReady++;
            if (status == 0)
            {
                if (vels != null) foreach (var velocity in vels)
                {
                    maxVelocity = Mathf.Max(maxVelocity, velocity.magnitude);
                    Assert.That(velocity.magnitude, Is.LessThan(.05f), phase + " static surface velocity");
                }
                if (timestamp > last) { accepted++; last = timestamp; }
            }
            yield return null;
        }
        File.AppendAllText(_report, $"Summary={phase}; Accepted={accepted}; Attempts={attempts}; NotReady={notReady}; OtherReady={otherReady}; MaxHeightError={maxHeightError:R}; MaxNormalError={maxNormalError:R}; MaxVelocity={maxVelocity:R}\n");
        Assert.That(accepted, Is.EqualTo(8), phase + " fresh successful results");
        Assert.That(otherReady, Is.GreaterThanOrEqualTo(8), phase + " other owner remains ready");
        _postedCount = count;
        _postedNormals = normals;
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
