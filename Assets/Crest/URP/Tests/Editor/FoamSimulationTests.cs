using System.Collections;
using System.IO;
using Crest;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

public class FoamSimulationTests
{
    SimSettingsFoam _settings;

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
    }
}
