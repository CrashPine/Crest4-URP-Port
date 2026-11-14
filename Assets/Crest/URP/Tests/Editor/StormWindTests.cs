// Crest 4 URP port. MIT; see LICENSE.Crest.
using System.Collections;
using System.IO;
using System;
using Object = UnityEngine.Object;
using Crest;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

public class StormWindTests
{
    [UnityTest]
    public IEnumerator StormWindChangesNativeDisplacementAndRestores()
    {
        bool previousGraphics = OceanRenderer.EnableGraphicsInBatchMode;
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Storm.unity");
        yield return new EnterPlayMode();
        var ocean = OceanRenderer.Instance;
        var fft = Object.FindAnyObjectByType<ShapeFFT>();
        Assert.That(fft, Is.Not.Null);
        Assert.That(fft._overrideGlobalWindSpeed, Is.False);
        Assert.That(ocean._lodDataFoam, Is.Not.Null, "Use the original Storm simulation configuration.");
        float originalWind = ocean._globalWindSpeed;
        var clock = ocean.gameObject.AddComponent<TimeProviderCustom>();
        clock._overrideTime = clock._overrideDeltaTime = true;
        clock._time = 1f;
        ocean.PushTimeProvider(clock);
        try
        {
            Assert.That(originalWind, Is.EqualTo(100f));
            for (int frame = 0; frame < 12; frame++) yield return null;
            var baseline = ReadDisplacement(ocean, fft, originalWind);

            ocean._globalWindSpeed = 25f;
            for (int frame = 0; frame < 12; frame++) yield return null;
            var reduced = ReadDisplacement(ocean, fft, 25f);

            ocean._globalWindSpeed = originalWind;
            for (int frame = 0; frame < 12; frame++) yield return null;
            var restored = ReadDisplacement(ocean, fft, originalWind);
            Assert.That(reduced.Length, Is.EqualTo(baseline.Length));
            Assert.That(restored.Length, Is.EqualTo(baseline.Length));

            float originalPeak = 0f, reducedPeak = 0f, maxChange = 0f, maxRestore = 0f;
            for (int i = 0; i < baseline.Length; i++)
            {
                originalPeak = Mathf.Max(originalPeak, Mathf.Abs(baseline[i].y));
                reducedPeak = Mathf.Max(reducedPeak, Mathf.Abs(reduced[i].y));
                maxChange = Mathf.Max(maxChange, ((Vector3)(baseline[i] - reduced[i])).magnitude);
                maxRestore = Mathf.Max(maxRestore, ((Vector3)(baseline[i] - restored[i])).magnitude);
            }
            string result = FormattableString.Invariant($"API={SystemInfo.graphicsDeviceType}; samples={baseline.Length}; time=1; wind={originalWind}->25->{originalWind}; originalHeightPeak={originalPeak:R}; reducedHeightPeak={reducedPeak:R}; maxDisplacementChange={maxChange:R}; maxRestore={maxRestore:R}");
            File.WriteAllText($"Evidence/P12-StormWind-{SystemInfo.graphicsDeviceType}.txt", result + "\n");
            Assert.That(originalPeak, Is.GreaterThan(.01f), result);
            Assert.That(reducedPeak, Is.LessThan(originalPeak), result);
            Assert.That(maxChange, Is.GreaterThan(.01f), result);
            Assert.That(maxRestore, Is.LessThanOrEqualTo(.001f), result);
        }
        finally
        {
            ocean._globalWindSpeed = originalWind;
            ocean.PopTimeProvider(clock);
            Object.Destroy(clock);
            OceanRenderer.EnableGraphicsInBatchMode = previousGraphics;
        }
        yield return new ExitPlayMode();
    }

    static Vector4[] ReadDisplacement(OceanRenderer ocean, ShapeFFT fft, float wind)
    {
        Assert.That(fft.WindSpeed, Is.EqualTo(wind / 3.6f).Within(.0001f));
        Assert.That(ocean.CurrentTime, Is.EqualTo(1f));
        var readback = AsyncGPUReadback.Request(ocean._lodDataAnimWaves.DataTexture, 0, TextureFormat.RGBAFloat);
        readback.WaitForCompletion();
        Assert.That(readback.hasError, Is.False);
        var samples = readback.GetData<Vector4>(0).ToArray();
        Assert.That(samples.Length, Is.GreaterThan(0));
        foreach (var sample in samples)
            if (!float.IsFinite(sample.x) || !float.IsFinite(sample.y) || !float.IsFinite(sample.z))
                Assert.Fail("Storm FFT generated non-finite displacement.");
        return samples;
    }
}
