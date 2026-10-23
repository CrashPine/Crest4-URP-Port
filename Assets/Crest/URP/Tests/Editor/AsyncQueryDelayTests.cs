using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Crest;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

// Records the frame containing OceanRenderer's dispatch, without any GPU readback/fence.
[DefaultExecutionOrder(10000)]
public class QueryDispatchClock : MonoBehaviour
{
    public struct Stamp { public double Wall; public int Frame; }
    public readonly Dictionary<float, Stamp> Frames = new Dictionary<float, Stamp>();
    public int MaxRequests;
    void LateUpdate()
    {
        Frames[Time.time] = new Stamp { Wall = Time.realtimeSinceStartupAsDouble, Frame = Time.frameCount };
        MaxRequests = Mathf.Max(MaxRequests, ((QueryBase)OceanRenderer.Instance.CollisionProvider).RequestCount);
    }
}

public class AsyncQueryDelayTests
{
    Material _material;
    SimSettingsSeaFloorDepth _settings;
    float _scale;
    int _capture;
    string _report;
    static readonly FieldInfo ResultTime = typeof(QueryBase).GetField("_queryResultsTime", BindingFlags.Instance | BindingFlags.NonPublic);
    readonly int[] _counts = { 1, 3, 7, 13 };
    Vector3[][] _points, _normals;
    float[][] _heights;

    [UnityTest]
    public IEnumerator UnfencedMultipleOwnersRemainBoundedAndRecoverAfterIdle()
    {
        _scale = Time.timeScale;
        _capture = Time.captureFramerate;
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        Time.timeScale = 1;
        Time.captureFramerate = 0;
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
        var clock = ocean.gameObject.AddComponent<QueryDispatchClock>();
        _points = new Vector3[4][]; _normals = new Vector3[4][]; _heights = new float[4][];
        for (int owner = 0; owner < 4; owner++)
        {
            _points[owner] = new Vector3[_counts[owner]];
            _normals[owner] = new Vector3[_counts[owner]];
            _heights[owner] = new float[_counts[owner]];
            for (int i = 0; i < _counts[owner]; i++) _points[owner][i] = new Vector3(-2 + .2f * i, 0, -.75f + owner * .5f);
        }
        _report = $"Evidence/AsyncQueryDelay-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report, "No test-only GPU requests, WaitAllRequests, wait-for-completion, fixed capture clock or artificial GPU stall. Four stable owners with1/3/7/13 points and normals (96 GPU samples). Plane y=2+.25x validates every status0 response. Wall age measured from post-dispatch LateUpdate observer to caller retrieval; CPU scheduling/Editor/render/readback included, not GPU execution time. Budget5 wall seconds per fresh packet; production profiling and forced queue saturation excluded.\n");
        yield return Phase(ocean, clock, "Initial");
        double deadline = Time.realtimeSinceStartupAsDouble + 5;
        int idleFrames = 0;
        while (idleFrames < 24 || OwnedResults((QueryBase)ocean.CollisionProvider) != 0)
        {
            Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline), "Idle owners must expire within five wall seconds; OceanRenderer keeps its camera query active.");
            yield return null;
            idleFrames++;
            Assert.That(((QueryBase)ocean.CollisionProvider).RequestCount, Is.InRange(0, 7));
        }
        File.AppendAllText(_report, $"Idle; Frames={idleFrames}; Requests={((QueryBase)ocean.CollisionProvider).RequestCount}; CachedResultGuids={((QueryBase)ocean.CollisionProvider).ResultGuidCount}; OwnedResultGuids={OwnedResults((QueryBase)ocean.CollisionProvider)}\n");
        // Original provider retains its last result after idle. Do not misreport this cache as a new packet.
        yield return Phase(ocean, clock, "Recovery");
        Assert.That(clock.MaxRequests, Is.InRange(1, 7));
        File.AppendAllText(_report, $"QueueSummary; MaximumObservedRequests={clock.MaxRequests}; SourceCapacity=7; SaturationNotRequired=True\n");
    }

    int OwnedResults(QueryBase query)
    {
        // Read-only inspection distinguishes expired test owners from OceanRenderer's live viewer query.
        var segments = (IDictionary)typeof(QueryBase).GetField("_resultSegments", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(query);
        int count = 0;
        for (int owner = 0; owner < 4; owner++) if (segments != null && segments.Contains(GetHashCode() ^ (0x13257 + owner * 7919))) count++;
        return count;
    }

    IEnumerator Phase(OceanRenderer ocean, QueryDispatchClock clock, string phase)
    {
        var query = (QueryBase)ocean.CollisionProvider;
        int[] fresh = new int[4], ready = new int[4], notReady = new int[4];
        float[] last = { -1, -1, -1, -1 };
        double start = Time.realtimeSinceStartupAsDouble, lastProgress = start;
        float startTime = Time.time;
        int startFrame = Time.frameCount, frames = 0;
        double maxWall = 0;
        int maxFrames = 0;
        float maxHeight = 0, maxNormal = 0;
        while (fresh[0] < 64 || fresh[1] < 64 || fresh[2] < 64 || fresh[3] < 64)
        {
            Assert.That(Time.realtimeSinceStartupAsDouble - lastProgress, Is.LessThan(5), "Fresh unfenced results must recover within five wall seconds.");
            Assert.That(Time.realtimeSinceStartupAsDouble - start, Is.LessThan(30));
            for (int owner = 0; owner < 4; owner++)
            {
                int status = ocean.CollisionProvider.Query(GetHashCode() ^ (0x13257 + owner * 7919), 0, _points[owner], _heights[owner], _normals[owner], null);
                Assert.That(status & (int)QueryBase.QueryStatus.PostFailed, Is.Zero);
                if (status != 0) { notReady[owner]++; continue; }
                ready[owner]++;
                for (int i = 0; i < _counts[owner]; i++)
                {
                    float he = Mathf.Abs(_heights[owner][i] - (2 + .25f * _points[owner][i].x));
                    float ne = Vector3.Distance(_normals[owner][i], new Vector3(-.25f, 1, 0).normalized);
                    Assert.That(float.IsFinite(he) && float.IsFinite(ne), Is.True);
                    Assert.That(he, Is.LessThan(.05f));
                    Assert.That(ne, Is.LessThan(.015f));
                    maxHeight = Mathf.Max(maxHeight, he); maxNormal = Mathf.Max(maxNormal, ne);
                }
                float timestamp = (float)ResultTime.GetValue(query);
                // Cached success is validated above, but cannot count towards post-idle recovery.
                if (timestamp < startTime || timestamp <= last[owner]) continue;
                Assert.That(clock.Frames.TryGetValue(timestamp, out var stamp), Is.True);
                double age = Time.realtimeSinceStartupAsDouble - stamp.Wall;
                int frameAge = Time.frameCount - stamp.Frame;
                Assert.That(age, Is.InRange(0d, 5d));
                Assert.That(frameAge, Is.GreaterThanOrEqualTo(0));
                if (fresh[owner] == 0) File.AppendAllText(_report, $"First={phase}; Owner={owner}; WallFromPost={Time.realtimeSinceStartupAsDouble-start:R}; FramesFromPost={Time.frameCount-startFrame}\n");
                File.AppendAllText(_report, $"Packet={phase}; Owner={owner}; Time={timestamp:R}; WallAgeSeconds={age:R}; FrameAge={frameAge}; Requests={query.RequestCount}\n");
                fresh[owner]++; last[owner] = timestamp; lastProgress = Time.realtimeSinceStartupAsDouble;
                maxWall = System.Math.Max(maxWall, age); maxFrames = Mathf.Max(maxFrames, frameAge);
            }
            Assert.That(query.RequestCount, Is.InRange(0, 7));
            frames++;
            yield return null;
        }
        for (int owner = 0; owner < 4; owner++) File.AppendAllText(_report, $"OwnerSummary={phase}; Owner={owner}; Fresh={fresh[owner]}; Ready={ready[owner]}; NotReady={notReady[owner]}\n");
        File.AppendAllText(_report, $"Summary={phase}; Frames={frames}; WallDurationSeconds={Time.realtimeSinceStartupAsDouble-start:R}; MaxObservedWallAgeSeconds={maxWall:R}; MaxFrameAge={maxFrames}; MaxHeightError={maxHeight:R}; MaxNormalError={maxNormal:R}\n");
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = _scale;
        Time.captureFramerate = _capture;
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (_material != null) Object.DestroyImmediate(_material);
        if (_settings != null) Object.DestroyImmediate(_settings);
    }
}
