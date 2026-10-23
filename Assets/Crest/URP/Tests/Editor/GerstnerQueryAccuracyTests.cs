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
using UnityEngine.Rendering;

// Capture after simulation/query dispatch; fence only this controlled numerical fixture.
[DefaultExecutionOrder(10000)]
public class GerstnerQueryCapture : MonoBehaviour
{
    public class Snapshot
    {
        public Color[][] Layers;
        public Vector2[] Centers;
        public float[] Texels;
        public float Alpha, Scale;
        public int Width;
        public Vector2 OceanCenter;
        public Dictionary<int, float> Source = new Dictionary<int, float>();
        public float Height(Vector3 position)
        {
            Assert.That(Mathf.Max(Mathf.Abs(position.x - OceanCenter.x), Mathf.Abs(position.z - OceanCenter.y)), Is.LessThan(Scale), "Reference restricted to central LOD0/1 region.");
            float Sample(int layer)
            {
                float u = (position.x - Centers[layer].x) / Texels[layer] + Width * .5f - .5f;
                float v = (position.z - Centers[layer].y) / Texels[layer] + Width * .5f - .5f;
                int x = Mathf.FloorToInt(u), y = Mathf.FloorToInt(v);
                Assert.That(x, Is.InRange(0, Width - 2));
                Assert.That(y, Is.InRange(0, Width - 2));
                var pixels = Layers[layer];
                float a = Mathf.Lerp(pixels[y * Width + x].g, pixels[y * Width + x + 1].g, u - x);
                float b = Mathf.Lerp(pixels[(y + 1) * Width + x].g, pixels[(y + 1) * Width + x + 1].g, u - x);
                return Mathf.Lerp(a, b, v - y);
            }
            return Mathf.Lerp(Sample(0), Sample(1), Alpha);
        }
    }
    public ShapeGerstner Waves;
    public bool Capture;
    public readonly HashSet<int> Cascades = new HashSet<int>();
    public readonly Dictionary<float, Snapshot> Frames = new Dictionary<float, Snapshot>();
    void LateUpdate()
    {
        if (!Capture) return;
        var ocean = OceanRenderer.Instance;
        var texture = ocean._lodDataAnimWaves.DataTexture;
        var field = AsyncGPUReadback.Request(texture, 0, TextureFormat.RGBAFloat);
        var source = AsyncGPUReadback.Request(Waves._waveBuffers, 0, TextureFormat.RGBAFloat);
        AsyncGPUReadback.WaitAllRequests();
        Assert.That(field.hasError || source.hasError, Is.False);
        var snapshot = new Snapshot { Layers = new Color[2][], Centers = new Vector2[2], Texels = new float[2], Width = texture.width,
            Alpha = Shader.GetGlobalFloat("_MeshScaleLerp"), Scale = ocean.CalcLodScale(0), OceanCenter = new Vector2(ocean.Root.position.x, ocean.Root.position.z) };
        for (int i = 0; i < 2; i++)
        {
            snapshot.Layers[i] = field.GetData<Color>(i).ToArray();
            var center = ocean._lodTransform._renderData[i].Current._posSnapped;
            snapshot.Centers[i] = new Vector2(center.x, center.z);
            snapshot.Texels[i] = 4 * ocean.CalcLodScale(i) / texture.width;
        }
        foreach (int cascade in Cascades) snapshot.Source[cascade] = source.GetData<Color>(cascade)[4 * Waves._waveBuffers.width + 3].g;
        Frames[Time.time] = snapshot;
    }
}

public class GerstnerQueryAccuracyTests
{
    struct Mode { public float K, Dx, Dz, Omega, Phase, Phase2, Amp, Amp2, Texel; public int Cascade; }
    readonly List<Mode> _modes = new List<Mode>();
    OceanWaveSpectrum _spectrum;
    float _scale;
    int _capture;
    string _report;
    static readonly FieldInfo ResultTime = typeof(QueryBase).GetField("_queryResultsTime", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo PreviousTime = typeof(QueryBase).GetField("_queryResultsTimeLast", BindingFlags.Instance | BindingFlags.NonPublic);

    [UnityTest]
    public IEnumerator GPUHeightNormalAndVelocityMatchOriginalGerstnerModes()
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
        var owner = new GameObject("Analytic Gerstner reference");
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
        var packets = (System.Array)typeof(ShapeGerstner).GetField("_waveData", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(waves);
        var cascades = (System.Array)typeof(ShapeGerstner).GetField("_cascadeParams", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(waves);
        for (int cascade = 0; cascade < cascades.Length - 1; cascade++)
        {
            int Start(int index) => (int)cascades.GetValue(index).GetType().GetField("_startIndex").GetValue(cascades.GetValue(index));
            for (int index = Start(cascade); index < Start(cascade + 1); index++)
            {
                var packet = packets.GetValue(index);
                var type = packet.GetType();
                Vector4 Field(string name) => (Vector4)type.GetField(name).GetValue(packet);
                for (int lane = 0; lane < 4; lane++)
                {
                    float amp = Field("_amp")[lane], amp2 = Field("_amp2")[lane];
                    if (Mathf.Abs(amp) + Mathf.Abs(amp2) < .000001f) continue;
                    _modes.Add(new Mode { K = Field("_twoPiOverWavelength")[lane], Dx = Field("_waveDirX")[lane], Dz = Field("_waveDirZ")[lane], Omega = Field("_omega")[lane], Phase = Field("_phase")[lane], Phase2 = Field("_phase2")[lane], Amp = amp, Amp2 = amp2, Cascade = cascade, Texel = .5f * (1 << cascade) / waves._waveBuffers.width });
                }
            }
        }
        Assert.That(_modes.Count, Is.GreaterThan(0));
        var capture = ocean.gameObject.AddComponent<GerstnerQueryCapture>();
        capture.Waves = waves;
        foreach (var mode in _modes) capture.Cascades.Add(mode.Cascade);
        capture.Capture = true;
        _report = $"Evidence/GerstnerQuery-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report, $"Original ShapeGerstner global, octave9, seed27, two components/octave, chop0, heading0. Active scalar modes={_modes.Count}. Two independent stages: source generator texels versus double cosine sum, then public query versus scalar bilinear sampling of combined ocean RT readbacks with dispatch timestamps. Test-only GPU fence, not performance/latency acceptance. Normal uses original .1m forward difference, velocity uses distinct request timestamps.\n");
        var points = new[] { new Vector3(-1, 0, -.5f), Vector3.zero, new Vector3(1, 0, 1) };
        var heights = new float[3];
        var normals = new Vector3[3];
        var velocities = new Vector3[3];
        float last = -1, min = float.PositiveInfinity, max = float.NegativeInfinity;
        float maxHeightError = 0, maxNormalError = 0, maxVelocityError = 0, maxAge = 0;
        int accepted = 0, attempts = 0;
        deadline = Time.realtimeSinceStartup + 20;
        while (accepted < 32 && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            int status = ocean.CollisionProvider.Query(GetHashCode(), 0, points, heights, normals, velocities);
            attempts++;
            float time = (float)ResultTime.GetValue(ocean.CollisionProvider);
            float previous = (float)PreviousTime.GetValue(ocean.CollisionProvider);
            if (status != 0 || time < last + .1f || !capture.Frames.TryGetValue(time, out var snapshot) || !capture.Frames.TryGetValue(previous, out var previousSnapshot)) continue;
            foreach (var pair in snapshot.Source)
            {
                double expectedSource = 0;
                foreach (var mode in _modes)
                {
                    if (mode.Cascade != pair.Key) continue;
                    double angle = mode.K * (mode.Dx * 3.5 * mode.Texel + mode.Dz * 4.5 * mode.Texel);
                    expectedSource += mode.Amp * System.Math.Cos(angle + mode.Phase - mode.Omega * time) + mode.Amp2 * System.Math.Cos(angle + mode.Phase2 + mode.Omega * time);
                }
                File.AppendAllText(_report, $"Source; Timestamp={time:R}; Cascade={pair.Key}; Actual={pair.Value:R}; Expected={expectedSource:R}; Error={System.Math.Abs(pair.Value - expectedSource):R}\n");
                Assert.That(System.Math.Abs(pair.Value - expectedSource), Is.LessThan(.002), "Original generator texel versus independent cosine sum.");
            }
            Assert.That(time - previous, Is.GreaterThan(.0001f));
            Assert.That(ocean.CurrentTime, Is.EqualTo(Time.time).Within(.0001f));
            float age = Time.time - time;
            Assert.That(age, Is.InRange(0, 5));
            maxAge = Mathf.Max(maxAge, age);
            for (int i = 0; i < points.Length; i++)
            {
                float expected = snapshot.Height(points[i]);
                var expectedNormal = new Vector3(expected - snapshot.Height(points[i] + Vector3.right * .1f), .1f, expected - snapshot.Height(points[i] + Vector3.forward * .1f)).normalized;
                float expectedVelocity = (expected - previousSnapshot.Height(points[i])) / (time - previous);
                float he = Mathf.Abs(heights[i] - expected - ocean.SeaLevel);
                float ne = Vector3.Distance(normals[i], expectedNormal);
                float ve = Vector3.Distance(velocities[i], Vector3.up * expectedVelocity);
                File.AppendAllText(_report, $"Result={accepted}; Point={i}; Status={status}; Timestamp={time:R}; Previous={previous:R}; Age={age:R}; Height={heights[i]:R}; Expected={expected:R}; HeightError={he:R}; NormalError={ne:R}; VelocityError={ve:R}\n");
                Assert.That(float.IsFinite(he) && float.IsFinite(ne) && float.IsFinite(ve), Is.True);
                Assert.That(he, Is.LessThan(.025f));
                Assert.That(ne, Is.LessThan(.015f));
                Assert.That(ve, Is.LessThan(.05f));
                maxHeightError = Mathf.Max(maxHeightError, he);
                maxNormalError = Mathf.Max(maxNormalError, ne);
                maxVelocityError = Mathf.Max(maxVelocityError, ve);
                min = Mathf.Min(min, heights[i]); max = Mathf.Max(max, heights[i]);
            }
            accepted++; last = time;
        }
        File.AppendAllText(_report, $"Summary; Accepted={accepted}; Attempts={attempts}; HeightSpan={max - min:R}; MaxHeightError={maxHeightError:R}; MaxNormalError={maxNormalError:R}; MaxVelocityError={maxVelocityError:R}; MaxSimulatedReadbackAge={maxAge:R}\n");
        Assert.That(accepted, Is.EqualTo(32));
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
        _modes.Clear();
    }
}
