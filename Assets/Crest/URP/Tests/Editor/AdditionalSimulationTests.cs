using System.Collections;
using System.IO;
using Crest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Rendering;

public class AdditionalSimulationTests
{
    Material _oceanMaterial, _inputMaterial, _originalMaterial;
    int? _previousCaptureRate;

    [UnityTest]
    public IEnumerator MovingSphereInjectsDynamicWaveEnergy()
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Waves.unity");
        yield return new EnterPlayMode();
        OceanRenderer.EnableGraphicsInBatchMode = true;
        _previousCaptureRate = Time.captureFramerate;
        Time.captureFramerate = 60;
        foreach (var fft in Object.FindObjectsByType<ShapeFFT>(FindObjectsSortMode.None)) fft.enabled = false;
        var ocean = Object.FindFirstObjectByType<OceanRenderer>();
        var settings = new SerializedObject(ocean);
        settings.FindProperty("_createDynamicWaveSim").boolValue = true;
        settings.ApplyModifiedPropertiesWithoutUndo();
        for (int i = 0; i < 30; i++) yield return new WaitForSeconds(.016f);
        Assert.That(ocean._lodDataDynWaves, Is.Not.Null);
        var baseline = AsyncGPUReadback.Request(ocean._lodDataDynWaves.DataTexture, 0, TextureFormat.RGFloat);
        while (!baseline.done) yield return null;
        Assert.That(baseline.hasError, Is.False);
        float before = Peak(baseline);
        var owner = new GameObject("Moving sphere input");
        owner.transform.position = new Vector3(-3, -.5f, 0);
        var sphere = owner.AddComponent<SphereWaterInteraction>();
        sphere._radius = 2;
        for (int frame = 0; frame < 120; frame++)
        {
            owner.transform.position = new Vector3(-3 + frame * .05f, -.5f, 0);
            yield return new WaitForSeconds(.016f);
        }
        var disturbed = AsyncGPUReadback.Request(ocean._lodDataDynWaves.DataTexture, 0, TextureFormat.RGFloat);
        while (!disturbed.done) yield return null;
        Assert.That(disturbed.hasError, Is.False);
        float after = Peak(disturbed);
        Directory.CreateDirectory("Evidence");
        File.WriteAllText("Evidence/DynamicWaves-" + SystemInfo.graphicsDeviceType + ".txt",
            $"Unity={Application.unityVersion}\nPeakBefore={before}; PeakAfter={after}\nFFT disabled; sphere radius=2 m, speed=3 m/s\n");
        Assert.That(before, Is.LessThan(.0001f));
        Assert.That(after, Is.GreaterThan(.001f));
        Object.Destroy(owner);
    }

    static float Peak(AsyncGPUReadbackRequest request)
    {
        float peak = 0;
        for (int layer = 0; layer < request.layerCount; layer++)
        {
            var values = request.GetData<Vector2>(layer);
            foreach (var value in values)
            {
                Assert.That(float.IsNaN(value.x) || float.IsInfinity(value.x), Is.False);
                Assert.That(float.IsNaN(value.y) || float.IsInfinity(value.y), Is.False);
                peak = Mathf.Max(peak, Mathf.Abs(value.x));
            }
        }
        return peak;
    }

    [UnityTest]
    public IEnumerator FlowQueriesMatchKnownInputAndRuntimeDirectionChange()
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Waves.unity");
        yield return new EnterPlayMode();
        OceanRenderer.EnableGraphicsInBatchMode = true;
        var ocean = Object.FindFirstObjectByType<OceanRenderer>();
        _originalMaterial = ocean.OceanMaterial;
        _oceanMaterial = new Material(_originalMaterial);
        _oceanMaterial.EnableKeyword("_FLOW_ON");
        _oceanMaterial.SetFloat("_Flow", 1);
        ocean.OceanMaterial = _oceanMaterial;
        var settings = new SerializedObject(ocean);
        settings.FindProperty("_createFlowSim").boolValue = true;
        settings.ApplyModifiedPropertiesWithoutUndo();

        var input = GameObject.CreatePrimitive(PrimitiveType.Plane);
        input.name = "Known flow input";
        input.transform.localScale = Vector3.one * 100;
        _inputMaterial = new Material(Shader.Find("Crest/Inputs/Flow/Fixed Direction"));
        _inputMaterial.SetFloat("_Speed", 3);
        input.GetComponent<Renderer>().sharedMaterial = _inputMaterial;
        input.AddComponent<RegisterFlowInput>();
        var sample = new SampleFlowHelper();
        var report = "Unity=" + Application.unityVersion + "\n";
        for (int direction = 0; direction < 2; direction++)
        {
            _inputMaterial.SetFloat("_Direction", direction * .25f);
            int matched = 0;
            Vector2 last = Vector2.zero;
            var expected = direction == 0 ? new Vector2(3, 0) : new Vector2(0, 3);
            // Readback latency is measured in real time: EditMode can run hundreds of
            // player-loop ticks before the GPU has completed an earlier direction.
            float deadline = Time.realtimeSinceStartup + 5;
            while (Time.realtimeSinceStartup < deadline && matched < 120)
            {
                yield return new WaitForSeconds(.02f);
                sample.Init(Vector3.zero, 1);
                if (sample.Sample(out last) && Vector2.Distance(last, expected) < .1f) matched++;
            }
            report += $"Expected={expected}; Last={last}; Matched={matched}\n";
            Assert.That(matched, Is.GreaterThan(100), report);
        }
        Directory.CreateDirectory("Evidence");
        File.WriteAllText("Evidence/Flow-" + SystemInfo.graphicsDeviceType + ".txt", report);
        Object.Destroy(input);
    }

    [UnityTest]
    public IEnumerator GerstnerProducesChangingHeightsWithoutFft()
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Waves.unity");
        yield return new EnterPlayMode();
        OceanRenderer.EnableGraphicsInBatchMode = true;
        _previousCaptureRate = Time.captureFramerate;
        Time.captureFramerate = 60;
        foreach (var fft in Object.FindObjectsByType<ShapeFFT>(FindObjectsSortMode.None))
            fft.enabled = false;
        var owner = new GameObject("Gerstner only test");
        var waves = owner.AddComponent<ShapeGerstner>();
        waves._overrideGlobalWindSpeed = true;
        waves._windSpeed = 60;
        var sample = new SampleHeightHelper();
        float minimum = float.PositiveInfinity, maximum = float.NegativeInfinity;
        int ready = 0;
        for (int frame = 0; frame < 240; frame++)
        {
            yield return new WaitForSeconds(.02f);
            sample.Init(Vector3.zero, 1, true);
            if (frame > 30 && sample.Sample(out float height))
            {
                Assert.That(float.IsNaN(height) || float.IsInfinity(height), Is.False);
                minimum = Mathf.Min(minimum, height);
                maximum = Mathf.Max(maximum, height);
                ready++;
            }
        }
        Assert.That(ready, Is.GreaterThan(100));
        Assert.That(maximum - minimum, Is.GreaterThan(.01f));
        Directory.CreateDirectory("Evidence");
        File.WriteAllText("Evidence/Gerstner-" + SystemInfo.graphicsDeviceType + ".txt",
            $"Unity={Application.unityVersion}\nFFT disabled\nSamples={ready}; Minimum={minimum}; Maximum={maximum}\n");
        Object.Destroy(owner);
    }

    [UnityTearDown]
    public IEnumerator RestoreState()
    {
        if (_previousCaptureRate.HasValue) Time.captureFramerate = _previousCaptureRate.Value;
        _previousCaptureRate = null;
        var ocean = Object.FindFirstObjectByType<OceanRenderer>();
        if (ocean != null && _originalMaterial != null)
        {
            ocean.enabled = false;
            ocean.OceanMaterial = _originalMaterial;
        }
        if (_oceanMaterial != null) Object.Destroy(_oceanMaterial);
        if (_inputMaterial != null) Object.Destroy(_inputMaterial);
        _oceanMaterial = _inputMaterial = _originalMaterial = null;
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
