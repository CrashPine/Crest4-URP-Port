using System.Collections;
using System.IO;
using System.Text;
using Crest;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class ResourceLifecycleTests
{
    RenderTexture _capture;

    [UnityTest]
    public IEnumerator RepeatedUnderwaterSceneLoadsKeepTextureFootprintStable()
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Underwater.unity");
        yield return new EnterPlayMode();
        OceanRenderer.EnableGraphicsInBatchMode = true;
        _capture = new RenderTexture(640, 360, 24);
        _capture.Create();
        var report = new StringBuilder($"Unity={Application.unityVersion}; API={SystemInfo.graphicsDeviceType}\n");
        int baselineCount = 0;
        long baselineBytes = 0;
        for (int cycle = 0; cycle < 12; cycle++)
        {
            yield return SceneManager.LoadSceneAsync("Ocean_Underwater");
            var sample = new SampleHeightHelper();
            int ready = 0;
            for (int frame = 0; frame < 60; frame++)
            {
                yield return new WaitForSeconds(.02f);
                sample.Init(Vector3.zero, 1, true);
                if (sample.Sample(out float height) && !float.IsNaN(height) && !float.IsInfinity(height)) ready++;
            }
            Assert.That(ready, Is.GreaterThan(0), "Queries must recover after scene reload.");
            RenderPipeline.SubmitRenderRequest(Camera.main,
                new UniversalRenderPipeline.SingleCameraRequest { destination = _capture });
            var completion = AsyncGPUReadback.Request(_capture);
            while (!completion.done) yield return null;
            Assert.That(completion.hasError, Is.False);
            yield return null; // Flush deferred destruction without unloading unused assets.
            int count = 0;
            long bytes = 0;
            foreach (var texture in Resources.FindObjectsOfTypeAll<RenderTexture>())
            {
                if (!IsCrestTexture(texture.name)) continue;
                count++;
                bytes += Profiler.GetRuntimeMemorySizeLong(texture);
            }
            report.AppendLine($"Cycle={cycle}; Textures={count}; NativeTextureBytes={bytes}; Queries={ready}");
            File.WriteAllText("Evidence/ResourceLifecycle-" + SystemInfo.graphicsDeviceType + ".txt", report.ToString());
            if (cycle < 3)
            {
                baselineCount = Mathf.Max(baselineCount, count);
                baselineBytes = System.Math.Max(baselineBytes, bytes);
            }
            else
            {
                Assert.That(count, Is.LessThanOrEqualTo(baselineCount), report.ToString());
                Assert.That(bytes, Is.LessThanOrEqualTo(baselineBytes), report.ToString());
            }
            Assert.That(count, Is.GreaterThan(0));
        }
    }

    static bool IsCrestTexture(string name)
    {
        return name.StartsWith("Crest") || name == "FFTCascades" || name.Contains("_oceanDepth_") ||
            name.StartsWith("AnimatedWaves") || name.StartsWith("DynamicWaves") ||
            name.StartsWith("Foam") || name.StartsWith("Flow") || name.StartsWith("Shadow") ||
            name.StartsWith("SeaFloorDepth") || name.StartsWith("ClipSurface") || name.StartsWith("Albedo");
    }

    [UnityTearDown]
    public IEnumerator RestoreState()
    {
        if (_capture != null) { _capture.Release(); Object.Destroy(_capture); }
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
