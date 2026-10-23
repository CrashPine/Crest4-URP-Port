using System.Collections;
using System.IO;
using Crest;
using Crest.Examples;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

public class RipplePulseClock : ITimeProvider
{
    public float Time = 0, Frequency = 60;
    public int MinimumFrame = -1, ActiveFrame = -1;
    public bool Armed;
    public float CurrentTime => Time;
    public float DeltaTime
    {
        get
        {
            int frame = OceanRenderer.FrameCount;
            if (Armed && frame > MinimumFrame) { ActiveFrame = frame; Armed = false; }
            return frame == ActiveFrame ? 1 / Frequency : 0;
        }
    }
    public float DeltaTimeDynamics => DeltaTime;
}

// Counts actual consumed simulation substeps after the original ocean update.
[DefaultExecutionOrder(10000)]
public class RippleStepObserver : MonoBehaviour
{
    public int LastFrame = -1, TotalSubsteps;
    public bool CaptureNext, ReadError;
    public Vector2[][] Captured;
    void LateUpdate()
    {
        var ocean = OceanRenderer.Instance;
        int frame = BuildCommandBufferBase._lastUpdateFrame;
        if (ocean == null || ocean._lodDataDynWaves == null || frame <= LastFrame) return;
        TotalSubsteps += ocean._lodDataDynWaves.LastUpdateSubstepCount;
        if (CaptureNext && ocean._lodDataDynWaves.LastUpdateSubstepCount > 0)
        {
            var request = AsyncGPUReadback.Request(ocean._lodDataDynWaves.DataTexture, 0, TextureFormat.RGFloat);
            request.WaitForCompletion();
            ReadError = request.hasError;
            Captured = new Vector2[request.layerCount][];
            if (!ReadError) for (int layer = 0; layer < Captured.Length; layer++) Captured[layer] = request.GetData<Vector2>(layer).ToArray();
            CaptureNext = false;
        }
        LastFrame = frame;
    }
}

public class RippleGeneratorTests
{
    SimSettingsWave _settings;
    Material _material;
    RippleGenerator _generator;
    RegisterDynWavesInput _input;
    Renderer _renderer;
    RipplePulseClock _clock;
    RippleStepObserver _steps;
    readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
    float _scale;
    int _capture;
    string _report;
    const float Amplitude = 12, Radius = 1.5f;
    static readonly int Marker = Shader.PropertyToID("_RippleTestMarker");

    [UnityTest]
    public IEnumerator OriginalGateAndGPUImpulseMatchShaderThenPropagateWithoutSource()
    {
        _scale = Time.timeScale;
        _capture = Time.captureFramerate;
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        Assert.That(Application.isPlaying, Is.True);
        Time.timeScale = 1;
        Time.captureFramerate = 60;
        Camera.main.transform.position = new Vector3(0, 4, 0);
        var ocean = OceanRenderer.Instance;
        foreach (var waves in Object.FindObjectsByType<ShapeWaves>()) waves.enabled = false;
        _settings = ScriptableObject.CreateInstance<SimSettingsWave>();
        _settings._simulationFrequency = 60;
        _settings._horizDisplace = 0;
        _settings._attenuationInShallows = 0;
        ocean._simSettingsDynamicWaves = _settings;
        var fields = new SerializedObject(ocean);
        fields.FindProperty("_createDynamicWaveSim").boolValue = true;
        fields.ApplyModifiedPropertiesWithoutUndo();
        for (int i = 0; i < 12; i++) yield return null;
        Assert.That(ocean._lodDataDynWaves, Is.Not.Null);
        _clock = new RipplePulseClock();
        ocean.PushTimeProvider(_clock);
        _steps = ocean.gameObject.AddComponent<RippleStepObserver>();
        var owner = GameObject.CreatePrimitive(PrimitiveType.Plane);
        owner.SetActive(false);
        Object.DestroyImmediate(owner.GetComponent<Collider>());
        owner.name = "Original ripple input";
        _material = new Material(Shader.Find("Crest/Inputs/Dynamic Waves/Add Bump"));
        _material.SetFloat("_Amplitude", Amplitude);
        _material.SetFloat("_Radius", Radius);
        _renderer = owner.GetComponent<Renderer>();
        _renderer.sharedMaterial = _material;
        _input = owner.AddComponent<RegisterDynWavesInput>();
        _input.enabled = false;
        _generator = owner.AddComponent<RippleGenerator>();
        _generator._warmUp = 3;
        _generator._onTime = .25f;
        _generator._period = 1;
        _block.SetFloat(RippleGenerator.ShaderIDs.s_SimCount, 123);
        _block.SetFloat(LodDataMgrPersistent.sp_SimDeltaTime, .125f);
        _block.SetFloat(Marker, 7.125f);
        _renderer.SetPropertyBlock(_block);
        owner.SetActive(true);
        _report = $"Evidence/RippleGenerator-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report, "Original RippleGenerator/registration/Add Bump shader. Gate uses explicit ocean times; simulation advances only through consumed single-step pulses. Cold impulse velocity reference=dt*12/simCount*(1-rSquared)^.1, radius1.5m, LOD0 interior. Both RG channels checked, no query result used as expected. Test-only clock/step observer/readback sync, not performance.\n");
        yield return Gate("WarmDisabled", 0, false, false, true);
        _input.enabled = true;
        yield return Gate("WarmRetainsEnabled", 2.5f, true, true, true);
        _input.enabled = false;
        yield return Gate("OnWindow", 3.125f, true);
        yield return Gate("ExactOffBoundary", 3.25f, false);
        yield return Gate("OffWindow", 3.5f, false);
        yield return Gate("PeriodWrapOn", 4.125f, true);
        _generator._animate = false;
        _input.enabled = false;
        yield return Gate("ManualDisabled", 4.125f, false);
        _input.enabled = true;
        yield return Gate("ManualEnabled", 3.5f, true);
        _generator._animate = true;
        yield return Gate("AnimationRestoredOff", 3.5f, false);
        Assert.That(Peak(Snapshot()), Is.LessThan(1e-6f), "No simulation dt means no forces submitted, even while gate is enabled");

        yield return ColdImpulse(60);
        yield return ColdImpulse(30);
    }

    IEnumerator Gate(string phase, float time, bool enabled, bool? registration = null, bool warm = false)
    {
        _clock.Time = time;
        for (int i = 0; i < 3; i++) yield return null;
        _renderer.GetPropertyBlock(_block);
        bool registered = false;
        foreach (var entry in RegisterLodDataInputBase.GetRegistrar(typeof(LodDataMgrDynWaves))) registered |= ReferenceEquals(entry.Value, _input);
        int count = SimCount();
        float expectedCount = warm ? 123 : count;
        float expectedDt = warm ? .125f : 1 / _settings._simulationFrequency;
        File.AppendAllText(_report, $"Gate={phase}; OceanTime={time:R}; Enabled={_input.enabled}; Registered={registered}; SimCount={_block.GetFloat(RippleGenerator.ShaderIDs.s_SimCount):R}; ExpectedCount={expectedCount:R}; Dt={_block.GetFloat(LodDataMgrPersistent.sp_SimDeltaTime):R}; ExpectedDt={expectedDt:R}\n");
        Assert.That(_generator.enabled, Is.True);
        Assert.That(_input.enabled, Is.EqualTo(enabled));
        Assert.That(registered, Is.EqualTo(registration ?? enabled));
        Assert.That(_block.GetFloat(RippleGenerator.ShaderIDs.s_SimCount), Is.EqualTo(expectedCount));
        Assert.That(_block.GetFloat(LodDataMgrPersistent.sp_SimDeltaTime), Is.EqualTo(expectedDt).Within(1e-7f));
        Assert.That(_block.GetFloat(Marker), Is.EqualTo(7.125f));
    }

    int SimCount()
    {
        var p = _generator.transform.position;
        int lod = LodDataMgrAnimWaves.SuggestDataLOD(new Rect(new Vector2(p.x, p.z), Vector2.zero));
        Assert.That(lod, Is.GreaterThanOrEqualTo(0));
        LodDataMgrDynWaves.CountWaveSims(lod, out int present, out int active);
        Assert.That(present, Is.GreaterThan(0));
        Assert.That(active, Is.GreaterThan(0));
        return active;
    }

    IEnumerator Advance(int requested, bool capture = false)
    {
        Assert.That(OceanRenderer.Instance.TimeProvider, Is.SameAs(_clock));
        float deadline = Time.realtimeSinceStartup + 20;
        try
        {
            for (int i = 0; i < requested; i++)
            {
                int before = _steps.TotalSubsteps;
                _steps.CaptureNext = capture && i == requested - 1;
                _clock.MinimumFrame = BuildCommandBufferBase._lastUpdateFrame;
                _clock.ActiveFrame = -1;
                _clock.Armed = true;
                while ((_clock.Armed || _steps.LastFrame < _clock.ActiveFrame) && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(_clock.Armed, Is.False);
                Assert.That(_clock.ActiveFrame, Is.GreaterThan(_clock.MinimumFrame));
                Assert.That(_steps.TotalSubsteps - before, Is.EqualTo(1), "Exactly one real dynamic-wave substep per pulse");
                _clock.ActiveFrame = -1;
            }
        }
        finally { _clock.Armed = false; _clock.ActiveFrame = -1; }
    }

    Vector2[] Snapshot()
    {
        var request = AsyncGPUReadback.Request(OceanRenderer.Instance._lodDataDynWaves.DataTexture, 0, TextureFormat.RGFloat);
        request.WaitForCompletion();
        Assert.That(request.hasError, Is.False);
        for (int layer = 0; layer < request.layerCount; layer++) foreach (var value in request.GetData<Vector2>(layer))
            Assert.That(float.IsFinite(value.x) && float.IsFinite(value.y), Is.True, "All cascades finite");
        return request.GetData<Vector2>(0).ToArray();
    }

    Vector2[] CapturedSnapshot()
    {
        Assert.That(_steps.CaptureNext || _steps.ReadError, Is.False);
        Assert.That(_steps.Captured, Is.Not.Null);
        foreach (var layer in _steps.Captured) foreach (var value in layer)
            Assert.That(float.IsFinite(value.x) && float.IsFinite(value.y), Is.True);
        return _steps.Captured[0];
    }

    static float Peak(Vector2[] values)
    {
        float peak = 0;
        foreach (var value in values) peak = Mathf.Max(peak, Mathf.Abs(value.x), Mathf.Abs(value.y));
        return peak;
    }

    IEnumerator ColdImpulse(float frequency)
    {
        _settings._simulationFrequency = _clock.Frequency = frequency;
        yield return Gate("PulseOn" + frequency, 3.125f, true);
        var ocean = OceanRenderer.Instance;
        ocean._lodDataDynWaves.ClearLodData();
        for (int i = 0; i < 3; i++) yield return null;
        Assert.That(Peak(Snapshot()), Is.LessThan(1e-6f));
        yield return Advance(1, true);
        var pulse = CapturedSnapshot();
        int width = ocean._lodDataDynWaves.DataTexture.width, count = SimCount();
        var grid = ocean._lodTransform._renderData[0].Current;
        float half = width * grid._texelWidth * .5f;
        Assert.That(Radius + Mathf.Max(Mathf.Abs(grid._posSnapped.x), Mathf.Abs(grid._posSnapped.z)), Is.LessThan(half * .8f), "Source is entirely outside the cascade feather zone");
        // The original draw camera is10000m above the surface. Its floating-point
        // view rotation is not an exact90 degrees. Reconstruct fragment varyings
        // through the actual projection rather than assuming ideal world texels.
        var vp = ocean._lodTransform.GetProjectionMatrix(0) * ocean._lodTransform.GetWorldToCameraMatrix(0);
        Vector2 Screen(Vector3 world)
        {
            var clip = vp * new Vector4(world.x, world.y, world.z, 1);
            return new Vector2((clip.x / clip.w * .5f + .5f) * width, (clip.y / clip.w * .5f + .5f) * width);
        }
        var low = Screen(new Vector3(-Radius, 0, -Radius));
        var high = Screen(new Vector3(Radius, 0, Radius));
        File.AppendAllText(_report, $"RasterProjection; Low={low:F8}; High={high:F8}\n");
        float maxError = 0, peakVelocity = 0;
        Vector2 worstPosition = Vector2.zero, worstValue = Vector2.zero;
        float worstExpected = 0;
        int nonzero = 0;
        for (int y = 0; y < width; y++) for (int x = 0; x < width; x++)
        {
            float wx = grid._posSnapped.x + (x + .5f - width * .5f) * grid._texelWidth;
            float wz = grid._posSnapped.z + (y + .5f - width * .5f) * grid._texelWidth;
            float nx = 2 * (x + .5f - low.x) / (high.x - low.x) - 1;
            float nz = 2 * (y + .5f - low.y) / (high.y - low.y) - 1;
            float r2 = nx * nx + nz * nz;
            float expected = r2 < 1 ? Amplitude / frequency / count * Mathf.Pow(1 - r2, .1f) : 0;
            var value = pulse[y * width + x];
            float error = Mathf.Max(Mathf.Abs(value.y - expected), Mathf.Abs(value.x));
            if (error > maxError) { maxError = error; worstPosition = new Vector2(wx, wz); worstValue = value; worstExpected = expected; }
            peakVelocity = Mathf.Max(peakVelocity, Mathf.Abs(value.y));
            if (expected > 0) nonzero++;
        }
        File.AppendAllText(_report, $"Impulse; Frequency={frequency:R}; ActualSteps=1; SimCount={count}; Grid={grid._texelWidth:R}; Width={width}; NonzeroReference={nonzero}; PeakVelocity={peakVelocity:R}; MaxRGError={maxError:R}; WorstPosition={worstPosition:F8}; WorstRG={worstValue:F8}; ExpectedG={worstExpected:R}\n");
        Assert.That(nonzero, Is.GreaterThan(20));
        Assert.That(peakVelocity, Is.GreaterThan(.001f));
        Assert.That(maxError, Is.LessThan(.0001f), "Original analytic radial impulse and zero initial height");
        yield return Gate("SourceOff" + frequency, 3.5f, false);
        var beforeFirst = Snapshot();
        yield return Advance(1, true);
        var first = CapturedSnapshot();
        float firstError = 0, heightPeak = 0;
        for (int i = 0; i < first.Length; i++)
        {
            float v = beforeFirst[i].y * (1 - _settings._damping / frequency);
            firstError = Mathf.Max(firstError, Mathf.Abs(first[i].y - v), Mathf.Abs(first[i].x - v / frequency));
            heightPeak = Mathf.Max(heightPeak, Mathf.Abs(first[i].x));
        }
        File.AppendAllText(_report, $"FirstUnforced; Frequency={frequency:R}; ActualSteps=1; HeightPeak={heightPeak:R}; MaxRGError={firstError:R}\n");
        Assert.That(heightPeak, Is.GreaterThan(.0001f));
        Assert.That(firstError, Is.LessThan(.0001f), "Zero-height initial Laplacian; damp velocity and integrate with no new source");
        yield return Advance((int)frequency - 1);
        var propagated = Snapshot();
        float outsidePeak = 0, finalHeight = 0;
        for (int y = 0; y < width; y++) for (int x = 0; x < width; x++)
        {
            float wx = grid._posSnapped.x + (x + .5f - width * .5f) * grid._texelWidth;
            float wz = grid._posSnapped.z + (y + .5f - width * .5f) * grid._texelWidth;
            float h = Mathf.Abs(propagated[y * width + x].x);
            finalHeight = Mathf.Max(finalHeight, h);
            if (wx * wx + wz * wz > (Radius + .1f) * (Radius + .1f)) outsidePeak = Mathf.Max(outsidePeak, h);
        }
        File.AppendAllText(_report, $"Propagation; Frequency={frequency:R}; UnforcedSteps={frequency}; HeightPeak={finalHeight:R}; OutsideRadiusPlusPoint1={outsidePeak:R}; InputEnabled={_input.enabled}\n");
        Assert.That(_input.enabled, Is.False);
        Assert.That(finalHeight, Is.InRange(.0001f, 1));
        Assert.That(outsidePeak, Is.GreaterThan(.000001f), "Real wave propagation extends beyond original source footprint");
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
