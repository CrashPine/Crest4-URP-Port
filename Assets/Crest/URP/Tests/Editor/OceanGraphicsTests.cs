using System.Collections;
using System.IO;
using Crest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

public class OceanGraphicsTests
{
    int? _previousCaptureRate;
    [UnityTest]
    public IEnumerator WavesProduceChangingGpuQueriesAndCapture()
        => CaptureAndQuery("Ocean_Waves");

    [UnityTest]
    public IEnumerator UnderwaterRendersAndQueries()
        => CaptureAndQuery("Ocean_Underwater");

    [UnityTest]
    public IEnumerator ReflectionsRenderAndQuery()
        => CaptureAndQuery("Ocean_Reflections");

    [UnityTest]
    public IEnumerator DepthCacheCapturesKnownHeight()
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Waves.unity");
        yield return new EnterPlayMode();
        OceanRenderer.EnableGraphicsInBatchMode = true;
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.layer = 30;
        cube.transform.position = new Vector3(0, 2, 0);
        cube.transform.localScale = new Vector3(10, 2, 10);
        cube.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/CrestPortDemos/Reference.mat");
        var owner = new GameObject("Depth cache test");
        owner.transform.localScale = new Vector3(20, 1, 20);
        var cache = owner.AddComponent<OceanDepthCache>();
        cache._resolution = 64;
        cache._layers = 1 << 30;
        cache._cameraMaxTerrainHeight = 20;
        cache._cameraFarClipPlane = 100;
        cache.PopulateCache();
        var request = AsyncGPUReadback.Request(cache.CacheTexture, 0, TextureFormat.RFloat);
        while (!request.done) yield return null;
        Assert.That(request.hasError, Is.False);
        var height = request.GetData<float>()[32 * 64 + 32];
        Directory.CreateDirectory("Evidence");
        File.WriteAllText("Evidence/DepthCache-" + SystemInfo.graphicsDeviceType + ".txt", $"Unity={Application.unityVersion}\nExpected=3\nMeasured={height}\n");
        Assert.That(height, Is.EqualTo(3).Within(.05f));
        Object.Destroy(owner);
        Object.Destroy(cube);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ShadowAtlasAccumulatesOcclusion()
        => CaptureAndQuery("Ocean_Reflections", true);

    IEnumerator CaptureAndQuery(string scene, bool shadows = false)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            Assert.Ignore("NOT TESTED: graphics device unavailable.");
        Assert.That(File.Exists("Assets/CrestPortDemos/" + scene + ".unity"), Is.True,
            "Generate validation scenes before running graphics tests.");
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/" + scene + ".unity");
        yield return new EnterPlayMode();
        OceanRenderer.EnableGraphicsInBatchMode = true;
        if (shadows)
        {
            // The original temporal filter uses Time.deltaTime and an R8 target.
            // Unbounded batch frame rates can quantize each contribution to zero.
            _previousCaptureRate = Time.captureFramerate;
            Time.captureFramerate = 60;
            var caster = GameObject.Find("Reflection reference");
            caster.transform.position = new Vector3(0, 6, 0);
            caster.transform.localScale = new Vector3(12, 12, 12);
            var ocean = Object.FindFirstObjectByType<OceanRenderer>();
            ocean.OceanMaterial = new Material(ocean.OceanMaterial);
            ocean.OceanMaterial.EnableKeyword("_SHADOWS_ON");
            var settings = new SerializedObject(ocean);
            settings.FindProperty("_createShadowData").boolValue = true;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
        var sample = new SampleHeightHelper();
        var startFrame = Time.frameCount;
        var startTime = Time.time;
        int samples = 0;
        float minimum = float.PositiveInfinity, maximum = float.NegativeInfinity;
        for (int frame = 0; frame < 180; frame++)
        {
            sample.Init(Vector3.zero, 1, true);
            if (sample.Sample(out float height))
            {
                Assert.That(float.IsNaN(height) || float.IsInfinity(height), Is.False);
                minimum = Mathf.Min(minimum, height);
                maximum = Mathf.Max(maximum, height);
                samples++;
            }
            // A batch Editor has no visible Game View. Allow real player-loop time for
            // asynchronous readback instead of exhausting editor ticks in a tight loop.
            yield return new WaitForSeconds(0.02f);
        }

        var camera = Camera.main;
        Assert.That(camera, Is.Not.Null);
        var target = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
        var pixels = new Texture2D(960, 540, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            target.Create();
            RenderPipeline.SubmitRenderRequest(camera,
                new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            if (shadows)
            {
                Assert.That(OceanRenderer.Instance._lodDataShadow, Is.Not.Null);
                var readback = AsyncGPUReadback.Request(OceanRenderer.Instance._lodDataShadow.DataTexture, 0);
                readback.WaitForCompletion();
                Assert.That(readback.hasError, Is.False);
                byte peak = 0;
                foreach (var value in readback.GetData<byte>()) peak = System.Math.Max(peak, value);
                Directory.CreateDirectory("Evidence");
                File.WriteAllText("Evidence/Shadows-" + SystemInfo.graphicsDeviceType + ".txt", $"Unity={Application.unityVersion}\nPeakOcclusionByte={peak}\nCaptureFramerate=60; original temporal weights and R8 target retained.\n");
                Assert.That(peak, Is.GreaterThan(0), "Shadow LOD array contains no accumulated occlusion.");
            }
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            pixels.Apply();
            Directory.CreateDirectory("Evidence");
            var captureName = shadows ? "Ocean_Shadows" : scene;
            File.WriteAllBytes("Evidence/" + captureName + "-" + SystemInfo.graphicsDeviceType + ".png", pixels.EncodeToPNG());
            File.WriteAllText("Evidence/" + captureName + "-" + SystemInfo.graphicsDeviceType + "-queries.txt",
                $"Unity={Application.unityVersion}\nAPI={SystemInfo.graphicsDeviceType}\nSamples={samples}\nMinimum={minimum}\nMaximum={maximum}\nStartFrame={startFrame}\nEndFrame={Time.frameCount}\nStartTime={startTime}\nEndTime={Time.time}\nProvider={OceanRenderer.Instance?.CollisionProvider?.GetType().Name}\nVisual acceptance requires manual image inspection.\n");
        }
        finally
        {
            RenderTexture.active = previous;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(pixels);
            OceanRenderer.EnableGraphicsInBatchMode = false;
        }
        yield return new ExitPlayMode();
        Assert.That(samples, Is.GreaterThan(30), "GPU queries did not become ready.");
        Assert.That(maximum - minimum, Is.GreaterThan(.001f), "FFT surface did not evolve at the sample point.");
    }

    [UnityTearDown]
    public IEnumerator RestorePlayMode()
    {
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (_previousCaptureRate.HasValue)
        {
            Time.captureFramerate = _previousCaptureRate.Value;
            _previousCaptureRate = null;
        }
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
