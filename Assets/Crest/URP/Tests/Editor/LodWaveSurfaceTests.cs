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
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

[DefaultExecutionOrder(12000)]
public class LodWaveSurfaceObservation : MonoBehaviour
{
    public Action<int> Observe;
    public int Version, LastVersion = -1, Tick;
    public Exception Failure;
    void LateUpdate()
    {
        if (Observe == null) return;
        if (LastVersion != Version) { LastVersion = Version; Tick = 0; }
        try { Observe(Tick++); } catch (Exception error) { Failure = error; Observe = null; }
    }
}

// Capture the three central cascades used by this fixture after actual dispatch.
[DefaultExecutionOrder(11000)]
public class LodWaveFieldCapture : MonoBehaviour
{
    public class Frame
    {
        public Color[][] Pixels = new Color[3][];
        public Vector2[] Centers = new Vector2[3];
        public float[] Texels = new float[3];
        public Vector2 Root;
        public float Scale, Alpha;
        public int Width;
        Vector3 Sample(Vector2 point)
        {
            float distance = Mathf.Max(Mathf.Abs(point.x - Root.x), Mathf.Abs(point.y - Root.y));
            float number = Mathf.Log(Mathf.Max(distance / Scale, 1), 2);
            int slice = Mathf.FloorToInt(number);
            Assert.That(slice, Is.InRange(0, 1), "Fixture's independent reference uses three central cascades only.");
            float alpha = Mathf.Clamp01((number - slice - .15f) / .7f);
            if (slice == 0) alpha = Mathf.Min(alpha + Alpha, 1);
            Vector3 Layer(int index)
            {
                float u = (point.x - Centers[index].x) / Texels[index] + Width * .5f - .5f;
                float v = (point.y - Centers[index].y) / Texels[index] + Width * .5f - .5f;
                int x = Mathf.FloorToInt(u), y = Mathf.FloorToInt(v);
                Assert.That(x, Is.InRange(0, Width - 2)); Assert.That(y, Is.InRange(0, Width - 2));
                var p = Pixels[index];
                var a = Color.Lerp(p[y * Width + x], p[y * Width + x + 1], u - x);
                var b = Color.Lerp(p[(y + 1) * Width + x], p[(y + 1) * Width + x + 1], u - x);
                var c = Color.Lerp(a, b, v - y);
                return new Vector3(c.r, c.g, c.b);
            }
            return Vector3.Lerp(Layer(slice), Layer(slice + 1), alpha);
        }
        public Vector3 Invert(Vector3 world)
        {
            var target = new Vector2(world.x, world.z); var u = target;
            for (int iteration = 0; iteration < 16; iteration++)
            {
                var d = Sample(u); var error = u + new Vector2(d.x, d.z) - target;
                const float epsilon = .01f;
                var x = (Sample(u + Vector2.right * epsilon) - Sample(u - Vector2.right * epsilon)) / (2 * epsilon);
                var z = (Sample(u + Vector2.up * epsilon) - Sample(u - Vector2.up * epsilon)) / (2 * epsilon);
                float a = 1 + x.x, b = z.x, c = x.z, e = 1 + z.z, determinant = a * e - b * c;
                Assert.That(determinant, Is.GreaterThan(.1f));
                if (error.magnitude < .00001f) break;
                u -= new Vector2(e * error.x - b * error.y, a * error.y - c * error.x) / determinant;
            }
            var result = Sample(u);
            Assert.That((u + new Vector2(result.x, result.z) - target).magnitude, Is.LessThan(.00005f));
            return result;
        }
    }
    public readonly Dictionary<float, Frame> Frames = new Dictionary<float, Frame>();
    void LateUpdate()
    {
        var ocean = OceanRenderer.Instance; var texture = ocean._lodDataAnimWaves.DataTexture;
        var request = AsyncGPUReadback.Request(texture, 0, TextureFormat.RGBAFloat); request.WaitForCompletion();
        Assert.That(request.hasError, Is.False);
        var frame = new Frame { Width = texture.width, Scale = ocean.Scale, Alpha = Shader.GetGlobalFloat("_MeshScaleLerp"), Root = new Vector2(ocean.Root.position.x, ocean.Root.position.z) };
        for (int i = 0; i < 3; i++)
        {
            frame.Pixels[i] = request.GetData<Color>(i).ToArray();
            var center = ocean._lodTransform._renderData[i].Current._posSnapped;
            frame.Centers[i] = new Vector2(center.x, center.z); frame.Texels[i] = 4 * ocean.CalcLodScale(i) / texture.width;
        }
        Frames[Time.time] = frame;
        if (Frames.Count > 16) { float oldest = float.PositiveInfinity; foreach (float key in Frames.Keys) oldest = Mathf.Min(oldest, key); Frames.Remove(oldest); }
    }
}

public class LodWaveSurfaceTests
{
    const int Size = 512;
    Camera _camera;
    RenderTexture _target;
    OceanWaveSpectrum _spectrum;
    SimSettingsSeaFloorDepth _depth;
    Material _surface;
    TimeProviderCustom _clock;
    LodWaveSurfaceObservation _observer;
    LodWaveFieldCapture _field;
    float _scale;
    int _capture, _state, _fresh, _ready, _unready, _images, _minVisibleLods = 100;
    float _expectedScale, _lastTime = -1, _maxPhaseError, _maxSameScaleError, _maxReferenceError, _horizontalPeak;
    Vector3[] _baseline;
    readonly Vector3[] _points = { new Vector3(-10, 0, -3), new Vector3(-5, 0, 1), Vector3.zero, new Vector3(5, 0, -1), new Vector3(10, 0, 3) };
    readonly Vector3[] _displacements = new Vector3[5];
    string _report;
    static readonly FieldInfo ResultTime = typeof(QueryBase).GetField("_queryResultsTime", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo LodIndex = typeof(OceanChunkRenderer).GetField("_lodIndex", BindingFlags.Instance | BindingFlags.NonPublic);

    [UnityTest]
    public IEnumerator FrozenChoppyWorldPhaseAndRenderedCoverageSurviveLodTransitions()
    {
        _scale = Time.timeScale; _capture = Time.captureFramerate;
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        Time.timeScale = 1; Time.captureFramerate = 60;
        var ocean = OceanRenderer.Instance;
        _camera = Camera.main;
        _camera.transform.SetPositionAndRotation(new Vector3(.017f, 14, -.033f), Quaternion.Euler(90, 0, 0));
        _camera.orthographic = true; _camera.orthographicSize = 48; _camera.aspect = 1;
        _camera.nearClipPlane = .1f; _camera.farClipPlane = 2000;
        _camera.clearFlags = CameraClearFlags.SolidColor; _camera.backgroundColor = Color.magenta;
        _camera.cullingMask = 1 << 4; _camera.allowHDR = false; _camera.allowMSAA = false;
        _camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        RenderSettings.fog = false;
        foreach (var shape in Object.FindObjectsByType<ShapeWaves>()) shape.enabled = false;
        _surface = new Material(ocean.OceanMaterial);
        _surface.DisableKeyword("_TRANSPARENCY_ON"); _surface.SetFloat("_Transparency", 0);
        ocean.OceanMaterial = _surface;
        _depth = ScriptableObject.CreateInstance<SimSettingsSeaFloorDepth>(); _depth._allowVaryingWaterLevel = false;
        ocean._simSettingsSeaFloorDepth = _depth;
        new GameObject("LOD wave numerical query fence").AddComponent<BoatProbesQueryFence>();
        _spectrum = ScriptableObject.CreateInstance<OceanWaveSpectrum>();
        _spectrum._chop = 1.6f; _spectrum._waveDirectionVariance = 0;
        var powers = new SerializedObject(_spectrum);
        for (int i = 0; i < OceanWaveSpectrum.NUM_OCTAVES; i++)
        {
            powers.FindProperty("_powerDisabled").GetArrayElementAtIndex(i).boolValue = i != 9;
            powers.FindProperty("_powerLog").GetArrayElementAtIndex(i).floatValue = -2;
        }
        powers.ApplyModifiedPropertiesWithoutUndo();
        var owner = new GameObject("Original frozen choppy Gerstner"); owner.SetActive(false);
        var waves = owner.AddComponent<ShapeGerstner>();
        waves._spectrum = _spectrum; waves._randomSeed = 27; waves._componentsPerOctave = 2; waves._resolution = 128;
        waves._overrideGlobalWindSpeed = true; waves._windSpeed = 60;
        waves._overrideGlobalWindDirection = true; waves._waveDirectionHeadingAngle = 0;
        var waveFields = new SerializedObject(waves);
        waveFields.FindProperty("_respectShallowWaterAttenuation").floatValue = 0;
        waveFields.ApplyModifiedPropertiesWithoutUndo(); owner.SetActive(true);
        for (int i = 0; i < 8; i++) yield return null;
        _clock = ocean.gameObject.AddComponent<TimeProviderCustom>();
        _clock._overrideTime = _clock._overrideDeltaTime = true; _clock._time = 1; _clock._deltaTime = 0;
        ocean.PushTimeProvider(_clock);
        for (int i = 0; i < 8; i++) yield return null;
        _target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32); _target.Create();
        _report = $"Evidence/LodWaveSurface-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report, "Original surface shader/mesh/vertex morph on frozen original Gerstner chop1.6/octave9/seed27, ocean time1. Camera top-down, opaque surface clone to isolate geometry coverage from refraction. Solid-magenta clear control, postprocessing/fog/MSAA off. Five camera states at scales8/16/32, translated/returned; capture first and sixth actual post-ocean updates. Every status0 displacement at five FIXED world points compared with independent RGB bilinear three-cascade sampling/Newton inverse at actual dispatch timestamp, tolerance .01m/residual .00005m/determinant>.1. Same-scale translation/return compared with initial frozen field, tolerance .025m. Cross-scale baseline differences recorded, not assumed invariant because representation is filtered. No private readiness gate. Each 512x512 capture must have zero magenta-clear pixels and at least three visible LOD indices; no claim about subpixel cracks, height/normal seam continuity, transparency or local-height/input edges. Numerical fence, not performance/latency.\n");
        var chunks = ocean.Root.GetComponentsInChildren<Renderer>();
        var enabled = new bool[chunks.Length];
        try
        {
            for (int i = 0; i < chunks.Length; i++) { enabled[i] = chunks[i].enabled; chunks[i].enabled = false; }
            int control = Capture("ClearControl");
            Assert.That(control, Is.EqualTo(Size * Size), "Detector must find the known uncovered background.");
        }
        finally { for (int i = 0; i < chunks.Length; i++) chunks[i].enabled = enabled[i]; }
        _field = ocean.gameObject.AddComponent<LodWaveFieldCapture>();
        _observer = ocean.gameObject.AddComponent<LodWaveSurfaceObservation>();
        _observer.Observe = Observe;
        yield return State(new Vector2(.017f, -.033f), 8);
        yield return State(new Vector2(8.017f, -8.033f), 8);
        yield return State(new Vector2(8.017f, -8.033f), 16);
        yield return State(new Vector2(-8.017f, 8.033f), 32);
        yield return State(new Vector2(.017f, -.033f), 8);
        _observer.Observe = null;
        float min = float.PositiveInfinity, max = float.NegativeInfinity;
        foreach (var d in _baseline) { min = Mathf.Min(min, d.y); max = Mathf.Max(max, d.y); }
        File.AppendAllText(_report, $"Summary; States={_state}; Ready={_ready}; Unready={_unready}; Captures={_images}; MinVisibleLods={_minVisibleLods}; BaselineHeightSpan={max-min:R}; HorizontalPeak={_horizontalPeak:R}; MaxCrossScaleBaselineDifference={_maxPhaseError:R}; MaxSameScaleDisplacementError={_maxSameScaleError:R}; MaxIndependentReferenceError={_maxReferenceError:R}\n");
        Assert.That(max - min, Is.GreaterThan(.03f)); Assert.That(_horizontalPeak, Is.GreaterThan(.01f));
        Assert.That(_images, Is.EqualTo(11));
    }

    IEnumerator State(Vector2 xz, float scale)
    {
        _state++; _expectedScale = scale; _fresh = 0; _lastTime = -1;
        _camera.transform.position = new Vector3(xz.x, 4 + scale * 1.25f, xz.y);
        _camera.orthographicSize = 6 * scale;
        _observer.Version = _state;
        float deadline = Time.realtimeSinceStartup + 15;
        while ((_observer.LastVersion != _state || _observer.Tick < 8 || _fresh < 6) && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            if (_observer.Failure != null) throw _observer.Failure;
        }
        if (_observer.Failure != null) throw _observer.Failure;
        Assert.That(_fresh, Is.GreaterThanOrEqualTo(6));
        Assert.That(_observer.LastVersion, Is.EqualTo(_state));
        Assert.That(_observer.Tick, Is.GreaterThanOrEqualTo(8));
    }

    void Observe(int tick)
    {
        if (_state == 0) return;
        var ocean = OceanRenderer.Instance;
        Assert.That(ocean.TimeProvider, Is.SameAs(_clock)); Assert.That(ocean.CurrentTime, Is.EqualTo(1));
        Assert.That(ocean.Scale, Is.EqualTo(_expectedScale));
        int status = ocean.CollisionProvider.Query(GetHashCode(), 0, _points, _displacements, null, null);
        if (status == 0)
        {
            float time = (float)ResultTime.GetValue(ocean.CollisionProvider);
            Assert.That(_field.Frames.TryGetValue(time, out var frame), Is.True);
            if (_baseline == null) _baseline = (Vector3[])_displacements.Clone();
            for (int i = 0; i < _displacements.Length; i++)
            {
                float error = Vector3.Distance(_displacements[i], _baseline[i]);
                File.AppendAllText(_report, $"Query; State={_state}; Tick={tick}; Point={i}; Displacement={_displacements[i]:F7}; Error={error:R}\n");
                Assert.That(float.IsFinite(error), Is.True);
                var reference = frame.Invert(_points[i]);
                float referenceError = Vector3.Distance(_displacements[i], reference);
                File.AppendAllText(_report, $"Reference; State={_state}; Tick={tick}; Point={i}; Time={time:R}; Scale={frame.Scale:R}; Error={referenceError:R}\n");
                Assert.That(float.IsFinite(referenceError) && referenceError < .01f, Is.True, "Independent Newton inverse of actual filtered cascade field.");
                _maxReferenceError = Mathf.Max(_maxReferenceError, referenceError);
                if (_expectedScale == 8 && frame.Scale == 8)
                {
                    Assert.That(error, Is.LessThan(.025f), "Same-scale translation/return preserves frozen world phase.");
                    _maxSameScaleError = Mathf.Max(_maxSameScaleError, error);
                }
                _maxPhaseError = Mathf.Max(_maxPhaseError, error);
                _horizontalPeak = Mathf.Max(_horizontalPeak, new Vector2(_displacements[i].x, _displacements[i].z).magnitude);
            }
            if (time > _lastTime) { _fresh++; _lastTime = time; }
            _ready++;
        }
        else { Assert.That(status & (int)QueryBase.QueryStatus.PostFailed, Is.Zero); _unready++; }
        if (tick == 0 || tick == 5)
        {
            var indices = new HashSet<int>(); var frustum = GeometryUtility.CalculateFrustumPlanes(_camera);
            foreach (var chunk in ocean.Root.GetComponentsInChildren<OceanChunkRenderer>())
            {
                var renderer = chunk.GetComponent<Renderer>();
                if (renderer.enabled && GeometryUtility.TestPlanesAABB(frustum, renderer.bounds)) indices.Add((int)LodIndex.GetValue(chunk));
            }
            _minVisibleLods = Mathf.Min(_minVisibleLods, indices.Count);
            int holes = Capture($"State{_state}-Tick{tick}");
            File.AppendAllText(_report, $"Coverage; State={_state}; Tick={tick}; Scale={ocean.Scale:R}; ShaderAlpha={Shader.GetGlobalFloat("_MeshScaleLerp"):R}; CurrentAlpha={ocean.ViewerAltitudeLevelAlpha:R}; VisibleLods={string.Join(",",indices)}; ClearPixels={holes}\n");
            Assert.That(indices.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(holes, Is.Zero, "No clear-background pixels across the rendered LOD rings at this sampled resolution.");
        }
    }

    int Capture(string label)
    {
        RenderPipeline.SubmitRenderRequest(_camera, new UniversalRenderPipeline.SingleCameraRequest { destination = _target });
        var active = RenderTexture.active; var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = _target; texture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); texture.Apply();
            File.WriteAllBytes($"Evidence/LodWaveSurface-{label}-{SystemInfo.graphicsDeviceType}.png", texture.EncodeToPNG());
            int magenta = 0;
            foreach (var pixel in texture.GetPixels32()) if (pixel.r > 240 && pixel.b > 240 && pixel.g < 16) magenta++;
            _images++;
            File.AppendAllText(_report, $"Capture={label}; Magenta={magenta}; Total={Size*Size}\n");
            return magenta;
        }
        finally { RenderTexture.active = active; Object.DestroyImmediate(texture); }
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (_observer != null) _observer.Observe = null;
        if (_field != null) { _field.enabled = false; _field.Frames.Clear(); }
        Time.timeScale = _scale; Time.captureFramerate = _capture;
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (_target != null) { _target.Release(); Object.DestroyImmediate(_target); }
        if (_surface != null) Object.DestroyImmediate(_surface);
        if (_spectrum != null) Object.DestroyImmediate(_spectrum);
        if (_depth != null) Object.DestroyImmediate(_depth);
    }
}
