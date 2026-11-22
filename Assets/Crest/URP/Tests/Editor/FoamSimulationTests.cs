using System.Collections;
using System.Collections.Generic;
using System.IO;
using Crest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

public class FoamSimulationTests
{
    SimSettingsFoam _settings;
    readonly List<Material> _materials = new List<Material>();

    internal sealed class StepClock : ITimeProvider
    {
        public int MinimumFrame = -1, ActiveFrame = -1;
        public bool Armed;
        public float CurrentTime => 0;
        public float DeltaTime
        {
            get
            {
                int frame = OceanRenderer.FrameCount;
                if (Armed && frame > MinimumFrame)
                {
                    ActiveFrame = frame;
                    Armed = false;
                }
                return frame == ActiveFrame ? 1f / 30 : 0;
            }
        }
        public float DeltaTimeDynamics => DeltaTime;
    }

    internal static IEnumerator AdvanceExactSteps(StepClock clock, int requested)
    {
        Assert.That(OceanRenderer.Instance.TimeProvider, Is.SameAs(clock), "Fixture clock must survive configuration before scheduling steps.");
        float deadline = Time.realtimeSinceStartup + 20;
        try
        {
            for (int step = 0; step < requested; step++)
            {
                int previous = BuildCommandBufferBase._lastUpdateFrame;
                clock.MinimumFrame = previous;
                clock.ActiveFrame = -1;
                clock.Armed = true;
                while ((clock.Armed || BuildCommandBufferBase._lastUpdateFrame <= previous ||
                    BuildCommandBufferBase._lastUpdateFrame < clock.ActiveFrame) && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(clock.Armed, Is.False, "The time pulse must be consumed by the ocean.");
                Assert.That(clock.ActiveFrame, Is.GreaterThan(previous));
                Assert.That(BuildCommandBufferBase._lastUpdateFrame, Is.GreaterThanOrEqualTo(clock.ActiveFrame));
                clock.ActiveFrame = -1;
            }
        }
        finally { clock.Armed = false; clock.ActiveFrame = -1; }
    }

    [UnityTest]
    public IEnumerator KnownFlowTransportsFoamPatchByThreeMetres()
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        OceanRenderer.EnableGraphicsInBatchMode = true;
        var ocean = OceanRenderer.Instance;
        foreach (var fft in Object.FindObjectsByType<ShapeFFT>(FindObjectsSortMode.None)) fft.enabled = false;
        _settings = ScriptableObject.CreateInstance<SimSettingsFoam>();
        _settings._prewarm = false;
        _settings._waveFoamStrength = 0;
        _settings._shorelineFoamStrength = 0;
        _settings._foamFadeRate = 0;
        _settings._simulationFrequency = 30;
        ocean._simSettingsFoam = _settings;
        var clock = new StepClock();
        ocean.PushTimeProvider(clock);
        var oceanMaterial = new Material(ocean.OceanMaterial);
        _materials.Add(oceanMaterial);
        oceanMaterial.EnableKeyword("_FLOW_ON");
        oceanMaterial.SetFloat("_Flow", 1);
        ocean.OceanMaterial = oceanMaterial;
        var configuration = new SerializedObject(ocean);
        configuration.FindProperty("_createFlowSim").boolValue = true;
        configuration.ApplyModifiedPropertiesWithoutUndo();
        var flowOwner = GameObject.CreatePrimitive(PrimitiveType.Plane);
        flowOwner.name = "Uniform 3 m per second flow";
        flowOwner.transform.localScale = Vector3.one * 100;
        var flowMaterial = new Material(Shader.Find("Crest/Inputs/Flow/Fixed Direction"));
        _materials.Add(flowMaterial);
        flowMaterial.SetFloat("_Speed", 3);
        flowMaterial.SetFloat("_Direction", 0);
        flowOwner.GetComponent<Renderer>().sharedMaterial = flowMaterial;
        flowOwner.AddComponent<RegisterFlowInput>();
        yield return WaitForOceanUpdates(4);
        // Serialized feature changes can rebuild OceanRenderer and reset its time
        // stack. Install the fixture clock after configuration has settled.
        ocean.PushTimeProvider(clock);
        Assert.That(ocean.TimeProvider, Is.SameAs(clock));
        Assert.That(ocean._lodDataFlow, Is.Not.Null);
        var flow = AsyncGPUReadback.Request(ocean._lodDataFlow.DataTexture, 0, TextureFormat.RGFloat);
        while (!flow.done) yield return null;
        Assert.That(flow.hasError, Is.False);
        int width = ocean._lodDataFlow.DataTexture.width;
        var vector = flow.GetData<Vector2>(0)[width * (width / 2) + width / 2];
        Assert.That(Vector2.Distance(vector, new Vector2(3, 0)), Is.LessThan(.05f));
        ocean._lodDataFoam.ClearLodData();
        var patch = GameObject.CreatePrimitive(PrimitiveType.Plane);
        patch.name = "Local foam seed";
        patch.transform.position = ocean.Root.position;
        patch.transform.localScale = Vector3.one * .4f;
        var foamMaterial = new Material(Shader.Find("Crest/Inputs/Foam/Override Foam"));
        _materials.Add(foamMaterial);
        foamMaterial.SetFloat("_FoamValue", 1);
        patch.GetComponent<Renderer>().sharedMaterial = foamMaterial;
        var input = patch.AddComponent<RegisterFoamInput>();
        yield return AdvanceExactSteps(clock, 1);
        input.enabled = false;
        // Current can be flipped before the corresponding commands execute. At zero
        // dt, drain both ping-pong buffers before taking a stable GPU snapshot.
        yield return WaitForOceanUpdates(3);
        var before = AsyncGPUReadback.Request(ocean._lodDataFoam.DataTexture, 0, TextureFormat.RFloat);
        while (!before.done) yield return null;
        Assert.That(before.hasError, Is.False);
        var initial = FoamMomentsAndCapture(before, width, "Before");
        Assert.That(initial.z, Is.GreaterThan(10), "Seed must write a measurable patch through the original input shader.");
        float texel = 4 * ocean.CalcLodScale(0) / width;
        var root = ocean.Root.position;
        float scale = ocean.Scale;
        yield return AdvanceExactSteps(clock, 30);
        yield return WaitForOceanUpdates(3);
        var after = AsyncGPUReadback.Request(ocean._lodDataFoam.DataTexture, 0, TextureFormat.RFloat);
        while (!after.done) yield return null;
        Assert.That(after.hasError, Is.False);
        var transported = FoamMomentsAndCapture(after, width, "After");
        float distanceX = (transported.x - initial.x) * texel;
        float distanceZ = (transported.y - initial.y) * texel;
        float ratio = transported.z / initial.z;
        File.WriteAllText($"Evidence/FoamFlow-{SystemInfo.graphicsDeviceType}.txt",
            $"Flow={vector}; TexelMetres={texel}; SeedMass={initial.z}; FinalMass={transported.z}; MassRatio={ratio}; ShiftX={distanceX}; ShiftZ={distanceZ}; ExpectedX=3; Steps=30; Delta=1/30; PositionTolerance=0.1; MassTolerance=5%\n");
        Assert.That(ocean.Root.position, Is.EqualTo(root));
        Assert.That(ocean.Scale, Is.EqualTo(scale));
        Assert.That(distanceX, Is.EqualTo(3).Within(.1f));
        Assert.That(distanceZ, Is.EqualTo(0).Within(.1f));
        Assert.That(ratio, Is.InRange(.95f, 1.05f), "With no generators or decay, advection should preserve patch mass within R16/filter error.");
        flowMaterial.SetFloat("_Direction", .25f);
        yield return WaitForOceanUpdates(3);
        var changedFlow = AsyncGPUReadback.Request(ocean._lodDataFlow.DataTexture, 0, TextureFormat.RGFloat);
        while (!changedFlow.done) yield return null;
        Assert.That(changedFlow.hasError, Is.False);
        var turnedVector = changedFlow.GetData<Vector2>(0)[width * (width / 2) + width / 2];
        Assert.That(Vector2.Distance(turnedVector, new Vector2(0, 3)), Is.LessThan(.05f));
        yield return AdvanceExactSteps(clock, 30);
        yield return WaitForOceanUpdates(3);
        var turned = AsyncGPUReadback.Request(ocean._lodDataFoam.DataTexture, 0, TextureFormat.RFloat);
        while (!turned.done) yield return null;
        Assert.That(turned.hasError, Is.False);
        var final = FoamMomentsAndCapture(turned, width, "Turned");
        float turnX = (final.x - transported.x) * texel, turnZ = (final.y - transported.y) * texel;
        float finalMassRatio = final.z / initial.z;
        File.AppendAllText($"Evidence/FoamFlow-{SystemInfo.graphicsDeviceType}.txt",
            $"ChangedFlow={turnedVector}; SecondLegX={turnX}; SecondLegZ={turnZ}; ExpectedSecondZ=3; FinalMassRatio={finalMassRatio}\nSnapshots stabilized over three zero-delta ocean updates on each leg; original input shaders and compute retained.\n");
        Assert.That(turnX, Is.EqualTo(0).Within(.1f));
        Assert.That(turnZ, Is.EqualTo(3).Within(.1f));
        Assert.That(finalMassRatio, Is.InRange(.95f, 1.05f));
        Assert.That(ocean.Root.position, Is.EqualTo(root));
        Assert.That(ocean.Scale, Is.EqualTo(scale));
    }

    static Vector3 FoamMomentsAndCapture(AsyncGPUReadbackRequest request, int width, string label)
    {
        var values = request.GetData<float>(0);
        double mass = 0, momentX = 0, momentY = 0;
        var pixels = new Color32[width * width];
        for (int y = 0; y < width; y++)
        for (int x = 0; x < width; x++)
        {
            float value = values[y * width + x];
            Assert.That(float.IsNaN(value) || float.IsInfinity(value), Is.False);
            Assert.That(value, Is.InRange(0f, 1.001f));
            byte grey = (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255);
            pixels[y * width + x] = new Color32(grey, grey, grey, 255);
            if (x < 16 || y < 16 || x >= width - 16 || y >= width - 16) continue;
            mass += value;
            momentX += (x + .5) * value;
            momentY += (y + .5) * value;
        }
        var texture = new Texture2D(width, width, TextureFormat.RGBA32, false);
        try
        {
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes($"Evidence/FoamFlow-{label}-{SystemInfo.graphicsDeviceType}.png", texture.EncodeToPNG());
        }
        finally { Object.Destroy(texture); }
        Assert.That(mass, Is.GreaterThan(0));
        return new Vector3((float)(momentX / mass), (float)(momentY / mass), (float)mass);
    }

    [UnityTest]
    public IEnumerator KnownShallowDepthGeneratesFoamAndToggleRecreatesEmptyTargets()
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        OceanRenderer.EnableGraphicsInBatchMode = true;
        var ocean = OceanRenderer.Instance;
        foreach (var fft in Object.FindObjectsByType<ShapeFFT>(FindObjectsSortMode.None)) fft.enabled = false;
        _settings = ScriptableObject.CreateInstance<SimSettingsFoam>();
        _settings._prewarm = false;
        _settings._waveFoamStrength = 0;
        _settings._shorelineFoamStrength = 1;
        _settings._shorelineFoamMaxDepth = 1;
        _settings._foamFadeRate = 0;
        _settings._simulationFrequency = 30;
        ocean._simSettingsFoam = _settings;
        var clock = ocean.gameObject.AddComponent<TimeProviderCustom>();
        clock._overrideTime = true;
        clock._time = 0;
        clock._overrideDeltaTime = true;
        clock._deltaTime = 0;
        ocean.PushTimeProvider(clock);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Known shallow floor";
        floor.layer = 30;
        floor.transform.position = new Vector3(0, -.6f, 0);
        floor.transform.localScale = new Vector3(1000, .2f, 1000);
        floor.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/CrestPortDemos/Reference.mat");
        var owner = new GameObject("Shoreline reference depth cache");
        owner.transform.localScale = new Vector3(1000, 1, 1000);
        var cache = owner.AddComponent<OceanDepthCache>();
        cache._resolution = 64;
        cache._layers = 1 << 30;
        cache._cameraMaxTerrainHeight = 20;
        cache._cameraFarClipPlane = 100;
        cache.PopulateCache();
        var depth = AsyncGPUReadback.Request(cache.CacheTexture, 0, TextureFormat.RFloat);
        while (!depth.done) yield return null;
        Assert.That(depth.hasError, Is.False);
        float measuredHeight = depth.GetData<float>()[32 * 64 + 32];
        Assert.That(measuredHeight, Is.EqualTo(-.5f).Within(.01f));
        yield return WaitForOceanUpdates(4);
        Assert.That(ocean._lodDataSeaDepths, Is.Not.Null);
        Assert.That(ocean._lodDataFoam, Is.Not.Null);
        ocean._lodDataFoam.ClearLodData();
        yield return AdvanceSteps(ocean, clock, 30);
        var readback = AsyncGPUReadback.Request(ocean._lodDataFoam.DataTexture, 0, TextureFormat.RFloat);
        while (!readback.done) yield return null;
        Assert.That(readback.hasError, Is.False);
        float generated = InteriorPeak(readback, ocean._lodDataFoam.DataTexture.width, out _);
        File.WriteAllText($"Evidence/FoamShoreline-{SystemInfo.graphicsDeviceType}.txt",
            $"FloorHeight={measuredHeight}; ExpectedDepth=0.5; ShorelineMaxDepth=1; Strength=1; Fade=0; Steps=30; Delta=1/30; ExpectedFoam=0.5; MeasuredFoam={generated}\n");
        Assert.That(generated, Is.EqualTo(.5f).Within(.005f));
        var originalTarget = ocean._lodDataFoam.DataTexture;
        _settings._shorelineFoamStrength = 0;
        var configuration = new SerializedObject(ocean);
        configuration.FindProperty("_createFoamSim").boolValue = false;
        configuration.ApplyModifiedPropertiesWithoutUndo();
        yield return WaitForOceanUpdates(2);
        Assert.That(ocean._lodDataFoam, Is.Null);
        Assert.That(originalTarget == null, Is.True, "Disabled simulation must release its owned target.");
        configuration.Update();
        configuration.FindProperty("_createFoamSim").boolValue = true;
        configuration.ApplyModifiedPropertiesWithoutUndo();
        yield return WaitForOceanUpdates(2);
        Assert.That(ocean._lodDataFoam, Is.Not.Null);
        var recreated = AsyncGPUReadback.Request(ocean._lodDataFoam.DataTexture, 0, TextureFormat.RFloat);
        while (!recreated.done) yield return null;
        Assert.That(recreated.hasError, Is.False);
        float restarted = InteriorPeak(recreated, ocean._lodDataFoam.DataTexture.width, out _);
        Assert.That(restarted, Is.LessThan(.0001f));
        File.AppendAllText($"Evidence/FoamShoreline-{SystemInfo.graphicsDeviceType}.txt",
            $"ToggleDisabled=True; OldTargetReleased=True; NewTargetCreated=True; RestartedFoam={restarted}\n");
    }

    [UnityTest]
    public IEnumerator ShorelinePrewarmAtZeroDeltaSurvivesSceneReload()
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        OceanRenderer.EnableGraphicsInBatchMode = true;
        float previousTimeScale = Time.timeScale;
        RenderTexture previousFoam = null, previousDepth = null;
        string report = $"Evidence/FoamPrewarm-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(report, "ExpectedFoam=0.25; FloorHeight=-0.5; MaxDepth=1; ShorelineStrength=1; Fade=2; Delta=0; Tolerance=0.005\n");
        try
        {
            // A feature rebuild resets the temporary provider stack. Keep Unity's
            // fallback clock paused too, including the first frame after reload.
            Time.timeScale = 0;
            for (int cycle = 0; cycle < 2; cycle++)
            {
                if (cycle > 0)
                {
                    yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Ocean_Calm");
                    Assert.That(previousFoam == null, Is.True, "Reload must destroy the old foam target.");
                    Assert.That(previousDepth == null, Is.True, "Reload must destroy the old cache target.");
                    Object.DestroyImmediate(_settings);
                }
                var ocean = OceanRenderer.Instance;
                Assert.That(ocean, Is.Not.Null);
                foreach (var fft in Object.FindObjectsByType<ShapeFFT>(FindObjectsSortMode.None)) fft.enabled = false;
                _settings = ScriptableObject.CreateInstance<SimSettingsFoam>();
                _settings._prewarm = false;
                _settings._waveFoamStrength = 0;
                _settings._shorelineFoamStrength = 1;
                _settings._shorelineFoamMaxDepth = 1;
                _settings._foamFadeRate = 2;
                _settings._simulationFrequency = 30;
                ocean._simSettingsFoam = _settings;
                SetFoamEnabled(ocean, false);
                yield return WaitForOceanUpdates(3);
                Assert.That(ocean._lodDataFoam, Is.Null);

                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Prewarm reference floor";
                floor.layer = 30;
                floor.transform.position = new Vector3(0, -.6f, 0);
                floor.transform.localScale = new Vector3(1000, .2f, 1000);
                floor.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/CrestPortDemos/Reference.mat");
                var owner = new GameObject("Prewarm reference cache");
                owner.transform.localScale = new Vector3(1000, 1, 1000);
                var cache = owner.AddComponent<OceanDepthCache>();
                cache._resolution = 64;
                cache._layers = 1 << 30;
                cache._cameraMaxTerrainHeight = 20;
                cache._cameraFarClipPlane = 100;
                cache.PopulateCache();
                yield return WaitForOceanUpdates(4);
                var depth = AsyncGPUReadback.Request(cache.CacheTexture, 0, TextureFormat.RFloat);
                while (!depth.done) yield return null;
                Assert.That(depth.hasError, Is.False);
                float height = depth.GetData<float>()[32 * 64 + 32];
                Assert.That(height, Is.EqualTo(-.5f).Within(.01f));

                // Negative control: depth alone cannot generate foam at dt=0.
                SetFoamEnabled(ocean, true);
                yield return WaitForOceanUpdates(4);
                var clock = new StepClock();
                ocean.PushTimeProvider(clock);
                var control = AsyncGPUReadback.Request(ocean._lodDataFoam.DataTexture, 0, TextureFormat.RFloat);
                while (!control.done) yield return null;
                Assert.That(control.hasError, Is.False);
                float unprewarmed = InteriorPeak(control, ocean._lodDataFoam.DataTexture.width, out _);
                Assert.That(unprewarmed, Is.LessThan(.0001f));
                SetFoamEnabled(ocean, false);
                yield return WaitForOceanUpdates(3);
                _settings._prewarm = true;
                SetFoamEnabled(ocean, true);
                yield return WaitForOceanUpdates(4);
                ocean.PushTimeProvider(clock);
                Assert.That(ocean.TimeProvider, Is.SameAs(clock));
                Assert.That(ocean.DeltaTime, Is.EqualTo(0));
                var warm = AsyncGPUReadback.Request(ocean._lodDataFoam.DataTexture, 0, TextureFormat.RFloat);
                while (!warm.done) yield return null;
                Assert.That(warm.hasError, Is.False);
                float prewarmed = InteriorPeak(warm, ocean._lodDataFoam.DataTexture.width, out _);
                File.AppendAllText(report, $"Cycle={cycle}; FloorHeight={height}; Unprewarmed={unprewarmed}; Prewarmed={prewarmed}\n");
                Assert.That(prewarmed, Is.EqualTo(.25f).Within(.005f));
                yield return WaitForOceanUpdates(8);
                var held = AsyncGPUReadback.Request(ocean._lodDataFoam.DataTexture, 0, TextureFormat.RFloat);
                while (!held.done) yield return null;
                Assert.That(held.hasError, Is.False);
                float retained = InteriorPeak(held, ocean._lodDataFoam.DataTexture.width, out _);
                Assert.That(retained, Is.EqualTo(prewarmed).Within(.0001f), "Prewarm must run once, not accumulate on paused frames.");
                File.AppendAllText(report, $"Cycle={cycle}; HeldAfterEightUpdates={retained}; OldReloadTargetsReleased={(cycle > 0)}\n");
                previousFoam = ocean._lodDataFoam.DataTexture;
                previousDepth = cache.CacheTexture;
            }
        }
        finally { Time.timeScale = previousTimeScale; }
    }

    static void SetFoamEnabled(OceanRenderer ocean, bool enabled)
    {
        var configuration = new SerializedObject(ocean);
        configuration.FindProperty("_createFoamSim").boolValue = enabled;
        configuration.ApplyModifiedPropertiesWithoutUndo();
    }

    static IEnumerator WaitForOceanUpdates(int requested)
    {
        int last = BuildCommandBufferBase._lastUpdateFrame, completed = 0;
        float deadline = Time.realtimeSinceStartup + 15;
        while (completed < requested && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            int current = BuildCommandBufferBase._lastUpdateFrame;
            if (current == last) continue;
            last = current;
            completed++;
        }
        Assert.That(completed, Is.EqualTo(requested));
    }

    [UnityTest]
    public IEnumerator WaveFoamGeneratesAndDecaysAtOriginalDiscreteRate()
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        OceanRenderer.EnableGraphicsInBatchMode = true;
        var ocean = OceanRenderer.Instance;
        foreach (var fft in Object.FindObjectsByType<ShapeFFT>(FindObjectsSortMode.None)) fft.enabled = false;
        _settings = ScriptableObject.CreateInstance<SimSettingsFoam>();
        _settings._prewarm = false;
        _settings._simulationFrequency = 30;
        _settings._waveFoamCoverage = 1;
        _settings._waveFoamStrength = 0;
        _settings._shorelineFoamStrength = 0;
        _settings._foamFadeRate = 20;
        ocean._simSettingsFoam = _settings;
        var clock = ocean.gameObject.AddComponent<TimeProviderCustom>();
        clock._overrideTime = true;
        clock._time = 0;
        clock._overrideDeltaTime = true;
        clock._deltaTime = 1f / 30;
        ocean.PushTimeProvider(clock);
        yield return AdvanceSteps(ocean, clock, 30);
        Assert.That(ocean._lodDataFoam, Is.Not.Null);
        clock._deltaTime = 0;
        var baseline = AsyncGPUReadback.Request(ocean._lodDataFoam.DataTexture, 0, TextureFormat.RFloat);
        while (!baseline.done) yield return null;
        Assert.That(baseline.hasError, Is.False);
        float initial = InteriorPeak(baseline, ocean._lodDataFoam.DataTexture.width, out _);
        Assert.That(initial, Is.LessThan(.0001f), "No wave/shoreline generators must leave no foam after clearing.");

        var waves = new GameObject("Foam generation Gerstner reference").AddComponent<ShapeGerstner>();
        waves._overrideGlobalWindSpeed = true;
        waves._windSpeed = 60;
        _settings._foamFadeRate = 2;
        _settings._waveFoamStrength = 5;
        clock._deltaTime = 1f / 30;
        yield return AdvanceSteps(ocean, clock, 90);
        clock._deltaTime = 0;
        var generated = AsyncGPUReadback.Request(ocean._lodDataFoam.DataTexture, 0, TextureFormat.RFloat);
        while (!generated.done) yield return null;
        Assert.That(generated.hasError, Is.False);
        float peak = InteriorPeak(generated, ocean._lodDataFoam.DataTexture.width, out int sampleIndex);
        Assert.That(peak, Is.GreaterThan(.2f), "Original Gerstner compression must generate whitecaps.");

        _settings._waveFoamStrength = 0;
        clock._deltaTime = 1f / 30;
        yield return AdvanceSteps(ocean, clock, 30);
        clock._deltaTime = 0;
        var decayed = AsyncGPUReadback.Request(ocean._lodDataFoam.DataTexture, 0, TextureFormat.RFloat);
        while (!decayed.done) yield return null;
        Assert.That(decayed.hasError, Is.False);
        InteriorPeak(decayed, ocean._lodDataFoam.DataTexture.width, out _);
        float actual = decayed.GetData<float>(0)[sampleIndex];
        float expected = peak * Mathf.Pow(1f - 2f / 30, 30);
        File.WriteAllText($"Evidence/Foam-{SystemInfo.graphicsDeviceType}.txt",
            $"Baseline={initial}; GeneratedPeak={peak}; DecayedSample={actual}; Expected={expected}; SampleIndex={sampleIndex}; Steps=30; Delta=1/30; FadeRate=2; Tolerance=0.005\nFFT disabled, fixed Gerstner phase, zero flow and shoreline generation; interior slice-0 sample, original UpdateFoam recurrence.\n");
        Assert.That(actual, Is.EqualTo(expected).Within(.005f));
    }

    static float InteriorPeak(AsyncGPUReadbackRequest request, int width, out int index)
    {
        var values = request.GetData<float>(0);
        float peak = 0;
        index = 0;
        for (int y = 16; y < width - 16; y++)
        for (int x = 16; x < width - 16; x++)
        {
            int current = y * width + x;
            float value = values[current];
            Assert.That(float.IsNaN(value) || float.IsInfinity(value), Is.False);
            Assert.That(value, Is.InRange(0f, 1.001f));
            if (value <= peak) continue;
            peak = value;
            index = current;
        }
        return peak;
    }

    static IEnumerator AdvanceSteps(OceanRenderer ocean, TimeProviderCustom clock, int requested)
    {
        int last = BuildCommandBufferBase._lastUpdateFrame, completed = 0;
        float deadline = Time.realtimeSinceStartup + 15;
        clock._deltaTime = 1f / 30;
        try
        {
            while (completed < requested && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                int current = BuildCommandBufferBase._lastUpdateFrame;
                if (current == last) continue;
                last = current;
                if (ocean._lodDataFoam != null) completed += ocean._lodDataFoam.LastUpdateSubstepCount;
            }
            Assert.That(completed, Is.EqualTo(requested), "Count executed ocean substeps, not EditMode enumerator ticks.");
        }
        finally { clock._deltaTime = 0; }
    }

    [UnityTearDown]
    public IEnumerator Restore()
    {
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
        if (_settings != null) Object.DestroyImmediate(_settings);
        foreach (var material in _materials) if (material != null) Object.DestroyImmediate(material);
        _materials.Clear();
    }
}
