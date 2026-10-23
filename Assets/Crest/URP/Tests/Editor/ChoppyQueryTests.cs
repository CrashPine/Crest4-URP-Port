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

public class ChoppyQueryTests
{
    OceanWaveSpectrum _spectrum;
    float _scale;
    int _capture;
    static readonly FieldInfo ResultTime = typeof(QueryBase).GetField("_queryResultsTime", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo PreviousTime = typeof(QueryBase).GetField("_queryResultsTimeLast", BindingFlags.Instance | BindingFlags.NonPublic);

    // Scalar RGB bilinear sampling, independent of the production query compute.
    static Vector3 Sample(GerstnerQueryCapture.Snapshot frame, Vector2 point)
    {
        Assert.That(Mathf.Max(Mathf.Abs(point.x - frame.OceanCenter.x), Mathf.Abs(point.y - frame.OceanCenter.y)), Is.LessThan(frame.Scale));
        float Channel(int layer, int channel)
        {
            float u = (point.x - frame.Centers[layer].x) / frame.Texels[layer] + frame.Width * .5f - .5f;
            float v = (point.y - frame.Centers[layer].y) / frame.Texels[layer] + frame.Width * .5f - .5f;
            int x = Mathf.FloorToInt(u), y = Mathf.FloorToInt(v);
            Assert.That(x, Is.InRange(0, frame.Width - 2));
            Assert.That(y, Is.InRange(0, frame.Width - 2));
            var pixels = frame.Layers[layer];
            float a = Mathf.Lerp(pixels[y * frame.Width + x][channel], pixels[y * frame.Width + x + 1][channel], u - x);
            float b = Mathf.Lerp(pixels[(y + 1) * frame.Width + x][channel], pixels[(y + 1) * frame.Width + x + 1][channel], u - x);
            return Mathf.Lerp(a, b, v - y);
        }
        return new Vector3(Mathf.Lerp(Channel(0, 0), Channel(1, 0), frame.Alpha),
            Mathf.Lerp(Channel(0, 1), Channel(1, 1), frame.Alpha), Mathf.Lerp(Channel(0, 2), Channel(1, 2), frame.Alpha));
    }

    // Newton solves the inverse map; Crest deliberately retains its four fixed-point iterations.
    static Vector3 Invert(GerstnerQueryCapture.Snapshot frame, Vector3 world)
    {
        var target = new Vector2(world.x, world.z);
        var u = target;
        for (int i = 0; i < 16; i++)
        {
            var d = Sample(frame, u);
            var error = u + new Vector2(d.x, d.z) - target;
            const float eps = .01f;
            var x = (Sample(frame, u + Vector2.right * eps) - Sample(frame, u - Vector2.right * eps)) / (2 * eps);
            var z = (Sample(frame, u + Vector2.up * eps) - Sample(frame, u - Vector2.up * eps)) / (2 * eps);
            float a = 1 + x.x, b = z.x, c = x.z, e = 1 + z.z;
            float determinant = a * e - b * c;
            Assert.That(determinant, Is.GreaterThan(.1f), "Reference restricted to a locally invertible wave field.");
            if (error.magnitude < .00001f) break;
            u -= new Vector2(e * error.x - b * error.y, a * error.y - c * error.x) / determinant;
        }
        var result = Sample(frame, u);
        Assert.That((u + new Vector2(result.x, result.z) - target).magnitude, Is.LessThan(.00005f), "Independent inverse residual.");
        return result;
    }

    [UnityTest]
    public IEnumerator ChoppyDisplacementNormalHeightAndVelocityMatchIndependentInverse()
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
        _spectrum._chop = 1.6f;
        _spectrum._waveDirectionVariance = 0;
        var powers = new SerializedObject(_spectrum);
        for (int i = 0; i < OceanWaveSpectrum.NUM_OCTAVES; i++)
        {
            powers.FindProperty("_powerDisabled").GetArrayElementAtIndex(i).boolValue = i != 9;
            powers.FindProperty("_powerLog").GetArrayElementAtIndex(i).floatValue = -2;
        }
        powers.ApplyModifiedPropertiesWithoutUndo();
        var owner = new GameObject("Independent choppy query reference");
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
        var capture = ocean.gameObject.AddComponent<GerstnerQueryCapture>();
        capture.Waves = waves;
        capture.Capture = true;
        string report = $"Evidence/ChoppyQuery-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(report, "Original global Gerstner, chop1.6/octave9/seed27/two components/heading0. Independent RGB RT sampling + Newton inverse; original displacement-based normal and dispatch-time vector velocity. Central LOD0/1, locally invertible only; no fold/seam acceptance. Test-only GPU fence, not latency/performance. Fixed tolerances displacement/height .01m, normal .02, velocity .08m/s. Every status0 retrieval with available snapshots validated; no private layout gates.\n");
        var points = new[] { new Vector3(-1, 0, -.5f), Vector3.zero, new Vector3(1, 0, 1), new Vector3(-.5f, 0, 1), new Vector3(.5f, 0, -1) };
        var displacements = new Vector3[points.Length];
        var normals = new Vector3[points.Length];
        var velocities = new Vector3[points.Length];
        var heights = new float[points.Length];
        int displacementOwner = GetHashCode(), heightOwner = displacementOwner ^ 0x123456;
        int accepted = 0, checkedFrames = 0, attempts = 0, heightChecks = 0;
        float last = -1, min = float.PositiveInfinity, max = float.NegativeInfinity;
        float maxD = 0, maxN = 0, maxV = 0, maxH = 0, horizontal = 0;
        deadline = Time.realtimeSinceStartup + 40;
        while (accepted < 32 && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            int status = ocean.CollisionProvider.Query(displacementOwner, 0, points, displacements, normals, velocities);
            int heightStatus = ocean.CollisionProvider.Query(heightOwner, 0, points, heights, null, null);
            attempts++;
            float time = (float)ResultTime.GetValue(ocean.CollisionProvider);
            float previous = (float)PreviousTime.GetValue(ocean.CollisionProvider);
            bool available = capture.Frames.TryGetValue(time, out var frame);
            if (heightStatus == 0)
            {
                Assert.That(available, Is.True, "Successful height query must have a captured dispatch frame.");
                foreach (int i in System.Linq.Enumerable.Range(0, points.Length))
                {
                    float error = Mathf.Abs(heights[i] - ocean.SeaLevel - Invert(frame, points[i]).y);
                    File.AppendAllText(report, $"Height; Time={time:R}; Point={i}; Error={error:R}\n");
                    Assert.That(float.IsFinite(error) && error < .01f, Is.True);
                    maxH = Mathf.Max(maxH, error);
                    heightChecks++;
                }
            }
            if (status == 0)
            {
                Assert.That(available && capture.Frames.ContainsKey(previous), Is.True, "Successful vector query must have both captured dispatch frames.");
                Assert.That(time - previous, Is.GreaterThan(.0001f));
                for (int i = 0; i < points.Length; i++)
                {
                    var expected = Invert(frame, points[i]);
                    var px = Invert(frame, points[i] + Vector3.right * .1f) - Vector3.right * .1f;
                    var pz = Invert(frame, points[i] + Vector3.forward * .1f) - Vector3.forward * .1f;
                    var normal = Vector3.Cross(expected - px, expected - pz).normalized;
                    normal.y *= -1;
                    var velocity = (expected - Invert(capture.Frames[previous], points[i])) / (time - previous);
                    float de = Vector3.Distance(displacements[i], expected), ne = Vector3.Distance(normals[i], normal), ve = Vector3.Distance(velocities[i], velocity);
                    File.AppendAllText(report, $"Vector; Time={time:R}; Previous={previous:R}; Point={i}; DisplacementError={de:R}; NormalError={ne:R}; VelocityError={ve:R}\n");
                    Assert.That(float.IsFinite(de) && float.IsFinite(ne) && float.IsFinite(ve), Is.True);
                    Assert.That(de, Is.LessThan(.01f));
                    Assert.That(ne, Is.LessThan(.02f));
                    Assert.That(ve, Is.LessThan(.08f));
                    maxD = Mathf.Max(maxD, de); maxN = Mathf.Max(maxN, ne); maxV = Mathf.Max(maxV, ve);
                    horizontal = Mathf.Max(horizontal, new Vector2(expected.x, expected.z).magnitude);
                    min = Mathf.Min(min, expected.y); max = Mathf.Max(max, expected.y);
                }
                checkedFrames++;
                if (time >= last + .1f) { accepted++; last = time; }
            }
            // Keep a bounded history including recent packets; current/previous are never pruned.
            foreach (float key in new List<float>(capture.Frames.Keys))
                if (key < Time.time - 5 && key != time && key != previous) capture.Frames.Remove(key);
        }
        capture.Capture = false;
        capture.Frames.Clear();
        File.AppendAllText(report, $"Summary; Accepted={accepted}; CheckedFrames={checkedFrames}; HeightChecks={heightChecks}; Attempts={attempts}; HeightSpan={max-min:R}; HorizontalPeak={horizontal:R}; MaxDisplacementError={maxD:R}; MaxHeightError={maxH:R}; MaxNormalError={maxN:R}; MaxVelocityError={maxV:R}\n");
        Assert.That(accepted, Is.EqualTo(32));
        Assert.That(heightChecks, Is.GreaterThan(0));
        Assert.That(horizontal, Is.GreaterThan(.01f));
        Assert.That(max - min, Is.GreaterThan(.03f));
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
    }
}
