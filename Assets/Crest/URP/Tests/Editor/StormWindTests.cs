// Crest 4 URP port. MIT; see LICENSE.Crest.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System;
using Object = UnityEngine.Object;
using Crest;
using Crest.URP;
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

    [UnityTest, Timeout(180000)]
    public IEnumerator LargeWavesSpectrumMeetsMeasuredHeightTarget()
    {
        bool previousGraphics = OceanRenderer.EnableGraphicsInBatchMode;
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_LargeWaves.unity");
        yield return new EnterPlayMode();
        var ocean = OceanRenderer.Instance;
        var fft = Object.FindAnyObjectByType<ShapeFFT>();
        var camera = Camera.main;
        camera.GetComponent<LargeWaveCameraRoute>().enabled = false;
        camera.transform.position = new Vector3(0, 7, 0);
        var clock = ocean.gameObject.AddComponent<TimeProviderCustom>();
        clock._overrideTime = clock._overrideDeltaTime = clock._paused = true;
        ocean.PushTimeProvider(clock);
        try
        {
            Assert.That(fft._spectrum, Is.Not.Null);
            Assert.That(ocean._lodDataFoam, Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<BoatProbes>(), Is.Not.Null);
            for (int frame = 0; frame < 12; frame++) yield return null;

            const float interval = .25f;
            const int steps = 960;
            var points = new[] { new Vector2(-6, -5), Vector2.zero, new Vector2(6, 5) };
            var heights = points.Select(_ => new float[steps + 1]).ToArray();
            var target = ocean._lodDataAnimWaves.DataTexture;
            var lod = ocean._lodTransform._renderData[0].Current;
            var origin = new Vector2(lod._posSnapped.x, lod._posSnapped.z) - Vector2.one * (target.width * .5f * lod._texelWidth);
            var pixels = points.Select(point => Vector2Int.FloorToInt((point - origin) / lod._texelWidth)).ToArray();
            int left = pixels.Min(pixel => pixel.x), bottom = pixels.Min(pixel => pixel.y);
            int width = pixels.Max(pixel => pixel.x) - left + 1, height = pixels.Max(pixel => pixel.y) - bottom + 1;
            Assert.That(left, Is.GreaterThanOrEqualTo(0));
            Assert.That(bottom, Is.GreaterThanOrEqualTo(0));
            Assert.That(left + width, Is.LessThanOrEqualTo(target.width));
            Assert.That(bottom + height, Is.LessThanOrEqualTo(target.height));

            for (int step = 0; step <= steps; step++)
            {
                clock._time = step * interval;
                yield return null;
                Assert.That(BuildCommandBufferBase._lastUpdateFrame, Is.EqualTo(OceanRenderer.FrameCount - 1).Or.EqualTo(OceanRenderer.FrameCount));
                var request = AsyncGPUReadback.Request(target, 0, left, width, bottom, height, 0, 1, TextureFormat.RGBAFloat);
                request.WaitForCompletion();
                Assert.That(request.hasError, Is.False);
                var data = request.GetData<Vector4>();
                for (int point = 0; point < points.Length; point++)
                {
                    int index = (pixels[point].y - bottom) * width + pixels[point].x - left;
                    float displacement = data[index].y;
                    Assert.That(float.IsFinite(displacement), Is.True);
                    heights[point][step] = displacement;
                }
            }

            var report = new StringBuilder();
            report.AppendLine(FormattableString.Invariant(
                $"Wind={fft.WindSpeed * 3.6f:R} km/h; Heading={ocean.WindDirectionAngle:R}; SpectrumMultiplier={fft._spectrum._multiplier:R}; Duration={steps * interval:R}s; Interval={interval:R}s"));
            report.AppendLine("Fixed undisplaced LOD0 texel positions. Upcrossings of each series mean bound full waves. H1/3 is the mean of the highest third.");
            var pooled = new List<float>();
            for (int point = 0; point < points.Length; point++)
            {
                var waveHeights = CompletedWaves(heights[point], interval, out var periods);
                Assert.That(waveHeights.Count, Is.GreaterThanOrEqualTo(10));
                pooled.AddRange(waveHeights);
                var position = origin + (new Vector2(pixels[point].x, pixels[point].y) + Vector2.one * .5f) * lod._texelWidth;
                report.AppendLine(FormattableString.Invariant(
                    $"Point={position}; Waves={waveHeights.Count}; H1/3={HighestThird(waveHeights):F3}m; MinPeriod={periods.Min():F2}s; MaxPeriod={periods.Max():F2}s"));
            }
            float significantHeight = HighestThird(pooled);
            report.AppendLine(FormattableString.Invariant($"PooledWaves={pooled.Count}; PooledH1/3={significantHeight:F3}m"));
            string output = $"Evidence/P13-LargeWaves-{SystemInfo.graphicsDeviceType}";
            File.WriteAllText(output + ".txt", report.ToString());
            File.WriteAllLines(output + ".csv", new[] { "time_s,height_1_m,height_2_m,height_3_m" }.Concat(
                Enumerable.Range(0, steps + 1).Select(step => FormattableString.Invariant(
                    $"{step * interval:R},{heights[0][step]:R},{heights[1][step]:R},{heights[2][step]:R}"))));
            Assert.That(significantHeight, Is.InRange(4f, 6f), report.ToString());
        }
        finally
        {
            ocean.PopTimeProvider(clock);
            Object.Destroy(clock);
            OceanRenderer.EnableGraphicsInBatchMode = previousGraphics;
        }
        yield return new ExitPlayMode();
    }

    [UnityTest, Timeout(180000)]
    public IEnumerator LargeWavesCameraRouteShowsBoatFoamAndLodChanges()
    {
        bool previousGraphics = OceanRenderer.EnableGraphicsInBatchMode;
        int previousCaptureRate = Time.captureFramerate;
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_LargeWaves.unity");
        yield return new EnterPlayMode();
        var ocean = OceanRenderer.Instance;
        var camera = Camera.main;
        var route = camera.GetComponent<LargeWaveCameraRoute>();
        var boat = Object.FindAnyObjectByType<BoatProbes>();
        var target = new RenderTexture(640, 360, 24, RenderTextureFormat.ARGB32);
        var fence = new GameObject("Boat query pacing").AddComponent<BoatProbesQueryFence>();
        string prefix = $"Evidence/P13-LargeWaves-Route-{SystemInfo.graphicsDeviceType}";
        try
        {
            Time.captureFramerate = 60;
            camera.aspect = 16f / 9f;
            Assert.That(target.Create(), Is.True);
            route.enabled = false;
            route.enabled = true;
            float start = Time.time;
            float minimumBoat = float.PositiveInfinity, maximumBoat = float.NegativeInfinity;
            float lowestView = float.PositiveInfinity, highestView = float.NegativeInfinity;
            float smallestScale = float.PositiveInfinity, largestScale = 0;
            float foamPeak = 0;
            int wetFrames = 0, captures = 0;
            float[] captureTimes = { 1, 7, 14 };
            string[] labels = { "NearSurface", "Underwater", "Overview" };

            while (Time.time - start < 15f)
            {
                yield return null;
                float elapsed = Time.time - start;
                minimumBoat = Mathf.Min(minimumBoat, boat.transform.position.y);
                maximumBoat = Mathf.Max(maximumBoat, boat.transform.position.y);
                lowestView = Mathf.Min(lowestView, ocean.ViewerHeightAboveWater);
                highestView = Mathf.Max(highestView, ocean.ViewerHeightAboveWater);
                smallestScale = Mathf.Min(smallestScale, ocean.Scale);
                largestScale = Mathf.Max(largestScale, ocean.Scale);
                if (boat.InWater) wetFrames++;

                if (captures >= captureTimes.Length || elapsed < captureTimes[captures]) continue;
                CaptureLargeWaves(camera, target, $"{prefix}-{labels[captures]}.png");
                if (captures < 2)
                {
                    var foam = AsyncGPUReadback.Request(ocean._lodDataFoam.DataTexture, 0, TextureFormat.RFloat);
                    foam.WaitForCompletion();
                    Assert.That(foam.hasError, Is.False);
                    foreach (float value in foam.GetData<float>(0))
                    {
                        Assert.That(float.IsFinite(value), Is.True);
                        foamPeak = Mathf.Max(foamPeak, value);
                    }
                }
                captures++;
            }

            File.WriteAllText(prefix + ".txt", FormattableString.Invariant(
                $"BoatRange={minimumBoat:F3}..{maximumBoat:F3} m; WetFrames={wetFrames}; ViewerRelativeHeight={lowestView:F3}..{highestView:F3} m; LodScale={smallestScale:R}..{largestScale:R}; FoamLod0Peak={foamPeak:R}; Captures={captures}\n"));
            Assert.That(captures, Is.EqualTo(3));
            Assert.That(lowestView, Is.LessThan(-1f));
            Assert.That(highestView, Is.GreaterThan(40f));
            Assert.That(largestScale, Is.GreaterThan(smallestScale));
            Assert.That(wetFrames, Is.GreaterThan(30));
            Assert.That(maximumBoat - minimumBoat, Is.GreaterThan(.5f));
            Assert.That(Mathf.Max(Mathf.Abs(minimumBoat), Mathf.Abs(maximumBoat)), Is.LessThan(12f));
            Assert.That(foamPeak, Is.GreaterThan(.01f));
        }
        finally
        {
            target.Release();
            Object.Destroy(target);
            Object.Destroy(fence.gameObject);
            Time.captureFramerate = previousCaptureRate;
            OceanRenderer.EnableGraphicsInBatchMode = previousGraphics;
        }
        yield return new ExitPlayMode();
    }

    static void CaptureLargeWaves(Camera camera, RenderTexture target, string path)
    {
        RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest
        {
            destination = target,
        });
        var previous = RenderTexture.active;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            Assert.That(image.GetPixels32().Count(pixel => pixel.r > 245 && pixel.g < 10 && pixel.b > 245), Is.Zero);
        }
        finally
        {
            RenderTexture.active = previous;
            Object.Destroy(image);
        }
    }

    static List<float> CompletedWaves(float[] samples, float interval, out List<float> periods)
    {
        var waves = new List<float>();
        periods = new List<float>();
        float mean = samples.Average();
        float start = -1, minimum = mean, maximum = mean;
        for (int index = 1; index < samples.Length; index++)
        {
            float current = samples[index];
            float previous = samples[index - 1];
            if (previous <= mean && current > mean)
            {
                float crossing = (index - 1 + (mean - previous) / (current - previous)) * interval;
                if (start >= 0)
                {
                    waves.Add(maximum - minimum);
                    periods.Add(crossing - start);
                }
                start = crossing;
                minimum = maximum = mean;
            }
            minimum = Mathf.Min(minimum, current);
            maximum = Mathf.Max(maximum, current);
        }
        return waves;
    }

    static float HighestThird(List<float> heights)
        => heights.OrderByDescending(height => height).Take(Mathf.CeilToInt(heights.Count / 3f)).Average();

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
