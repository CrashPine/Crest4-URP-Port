// Crest 4 URP port. MIT; see LICENSE.Crest.
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

#pragma warning disable CS0618 // Intentionally exercises the legacy serialized prefabs.

public class LegacyUnderwaterTests
{
    const int Width = 320, Height = 180;

    [UnityTest]
    public IEnumerator OriginalBuiltInCurtainChangesUnderwaterView() => CompareCurtain(true);

    [UnityTest]
    public IEnumerator UrpCurtainChangesUnderwaterView() => CompareCurtain(false);

    static IEnumerator CompareCurtain(bool builtIn)
    {
        bool previousGraphics = OceanRenderer.EnableGraphicsInBatchMode;
        var previousDefault = GraphicsSettings.defaultRenderPipeline;
        var previousQuality = QualitySettings.renderPipeline;
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        var ocean = OceanRenderer.Instance;
        var camera = Camera.main;
        var originalOcean = ocean.OceanMaterial;
        var oceanMaterial = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/CrestPortDemos/Ocean_Underwater.mat"));
        var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var panelMaterial = new Material(Shader.Find("Hidden/Crest/Tests/DualPipelineOpaque"));
        GameObject curtain = null, meniscus = null;
        Material curtainMaterial = null, meniscusMaterial = null;
        string report = $"Evidence/P13-LegacyUnderwater-{(builtIn ? "BuiltIn" : "URP")}-{SystemInfo.graphicsDeviceType}";
        try
        {
            if (builtIn)
            {
                QualitySettings.renderPipeline = null;
                GraphicsSettings.defaultRenderPipeline = null;
            }
            camera.enabled = false;
            camera.transform.SetPositionAndRotation(new Vector3(0, -3f, -6f), Quaternion.identity);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.75f, .55f, .35f);
            camera.cullingMask = 1 << ocean.Layer;
            camera.depthTextureMode = DepthTextureMode.Depth;
            camera.farClipPlane = 100;
            camera.allowHDR = camera.allowMSAA = false;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.requiresDepthTexture = true;
            cameraData.renderPostProcessing = false;
            ocean.ViewCamera = camera;
            oceanMaterial.EnableKeyword("_UNDERWATER_ON");
            oceanMaterial.SetFloat("_Underwater", 1f);
            oceanMaterial.DisableKeyword("_CAUSTICS_ON");
            oceanMaterial.SetFloat("_Caustics", 0f);
            ocean.OceanMaterial = oceanMaterial;
            panel.layer = ocean.Layer;
            panel.transform.position = new Vector3(0, -3, 8);
            panel.transform.localScale = new Vector3(12, 7, 1);
            panelMaterial.SetColor("_Color", new Color(.2f, .65f, .35f));
            panel.GetComponent<Renderer>().sharedMaterial = panelMaterial;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.35f, .35f, .35f);
            foreach (var wave in Object.FindObjectsByType<ShapeWaves>()) wave.enabled = false;
            curtain = InstantiatePrefab("UnderWaterCurtainGeom", camera, ocean.Layer, out curtainMaterial);
            meniscus = InstantiatePrefab("UnderWaterMeniscus", camera, ocean.Layer, out meniscusMaterial);
            curtain.SetActive(false);
            meniscus.SetActive(false);
            Assert.That(target.Create(), Is.True);
            var clock = ocean.gameObject.AddComponent<TimeProviderCustom>();
            clock._overrideTime = clock._overrideDeltaTime = true;
            clock._time = 1f;
            clock._deltaTime = 0f;
            ocean.PushTimeProvider(clock);
            for (int frame = 0; frame < 12; frame++) yield return null;
            // Isolate the curtain from the port's URP-only ocean surface in the Built-in comparison.
            foreach (var tile in ocean.Tiles) tile.gameObject.layer = (ocean.Layer + 1) % 32;
            var baseline = Capture(camera, target, builtIn, report + "-Off.png");
            curtain.SetActive(true);
            meniscus.SetActive(true);
            yield return null;
            Capture(camera, target, builtIn, null);
            for (int frame = 0; frame < 12; frame++) yield return null;
            var underwater = Capture(camera, target, builtIn, report + "-On.png");
            curtain.SetActive(false);
            meniscus.SetActive(false);
            yield return null;
            var restored = Capture(camera, target, builtIn, report + "-Restored.png");
            int changed = ChangedPixels(baseline, underwater, 12);
            int restoreDifference = ChangedPixels(baseline, restored, 0);
            int nonBlack = 0;
            foreach (var pixel in underwater)
                if (pixel.r + pixel.g + pixel.b > 12) nonBlack++;
            File.WriteAllText(report + ".txt", $"ChangedPixels={changed}; RestoreDifferentPixels={restoreDifference}; NonBlackOn={nonBlack}; Total={baseline.Length}\n");
            Assert.That(nonBlack, Is.GreaterThan(baseline.Length / 2), "A black frame does not establish underwater rendering.");
            Assert.That(changed, Is.GreaterThan(500), "The original Curtain/Meniscus workflow must visibly change the underwater view.");
            Assert.That(restoreDifference, Is.Zero, "Disabling the legacy prefabs must restore the original frame.");

            camera.transform.position = new Vector3(0, ocean.SeaLevel, -6f);
            // Use a wider serialized setting to measure the thin effect with a 180 px target.
            meniscusMaterial.SetFloat("_MeniscusWidth", 30);
            yield return null;
            var waterlineBaseline = Capture(camera, target, builtIn, report + "-WaterlineOff.png");
            meniscus.SetActive(true);
            yield return null;
            var waterline = Capture(camera, target, builtIn, report + "-WaterlineOn.png");
            int meniscusPixels = ChangedPixels(waterlineBaseline, waterline, 12);
            File.AppendAllText(report + ".txt", $"MeniscusPixels={meniscusPixels}\n");
            Assert.That(meniscusPixels, Is.InRange(Width, Width * 20), "The isolated meniscus must affect a narrow band.");

            camera.transform.position = new Vector3(0, ocean.SeaLevel + 3f, -6f);
            curtain.SetActive(true);
            yield return null;
            yield return null;
            Assert.That(curtain.GetComponent<Renderer>().enabled, Is.False);
            Assert.That(meniscus.GetComponent<Renderer>().enabled, Is.False);
            var above = Capture(camera, target, builtIn, report + "-AboveOn.png");
            curtain.SetActive(false);
            meniscus.SetActive(false);
            yield return null;
            var aboveBaseline = Capture(camera, target, builtIn, report + "-AboveOff.png");
            int aboveDifference = ChangedPixels(aboveBaseline, above, 0);
            File.AppendAllText(report + ".txt", $"AboveDifferentPixels={aboveDifference}\n");
            Assert.That(aboveDifference, Is.Zero, "The legacy effect must clear after returning above water.");

            if (!builtIn)
            {
                camera.transform.position = new Vector3(0, ocean.SeaLevel - 3f, -6f);
                var modern = camera.gameObject.AddComponent<UnderwaterRenderer>();
                for (int frame = 0; frame < 12; frame++) yield return null;
                Capture(camera, target, false, null);
                yield return null;
                var modernOnly = Capture(camera, target, false, report + "-ModernOnly.png");
                Assert.That(ChangedPixels(baseline, modernOnly, 12), Is.GreaterThan(500),
                    "The modern-only reference must contain a visible underwater effect.");
                curtain.SetActive(true);
                meniscus.SetActive(true);
                yield return null;
                yield return null;
                Assert.That(curtain.GetComponent<Renderer>().enabled, Is.False);
                Assert.That(meniscus.GetComponent<Renderer>().enabled, Is.False);
                var combined = Capture(camera, target, false, report + "-ModernWithLegacy.png");
                int combinedDifference = ChangedPixels(modernOnly, combined, 0);
                File.AppendAllText(report + ".txt", $"ModernWithLegacyDifferentPixels={combinedDifference}\n");
                Assert.That(combinedDifference, Is.Zero, "The modern renderer must take precedence.");
                modern.enabled = false;
                yield return null;
                yield return null;
                Assert.That(curtain.GetComponent<Renderer>().enabled, Is.True);

                camera.transform.position = new Vector3(0, ocean.SeaLevel - .2f, -6f);
                yield return null;
                yield return null;
                var isolated = Capture(camera, target, false, report + "-IsolatedNearSurface.png");
                var curtainRenderer = curtain.GetComponent<Renderer>();
                Assert.That(curtainMaterial.renderQueue, Is.EqualTo(oceanMaterial.renderQueue));
                Assert.That(meniscusMaterial.renderQueue, Is.GreaterThan(oceanMaterial.renderQueue));
                foreach (var tile in ocean.Tiles)
                {
                    tile.gameObject.layer = ocean.Layer;
                    Assert.That(tile.GetComponent<Renderer>().sortingOrder, Is.GreaterThan(curtainRenderer.sortingOrder));
                }
                var withSurface = Capture(camera, target, false, report + "-WithSurface.png");
                curtain.SetActive(false);
                meniscus.SetActive(false);
                yield return null;
                var surfaceOnly = Capture(camera, target, false, report + "-SurfaceOnly.png");
                int surfacePixels = ChangedPixels(isolated, withSurface, 12);
                int legacyPixels = ChangedPixels(surfaceOnly, withSurface, 12);
                File.AppendAllText(report + ".txt", $"SurfacePixels={surfacePixels}; LegacyPixels={legacyPixels}\n");
                Assert.That(surfacePixels, Is.GreaterThan(500), "The water surface must be visible over the curtain.");
                Assert.That(legacyPixels, Is.GreaterThan(500), "Submerged geometry must still be fogged with the surface visible.");
            }
        }
        finally
        {
            if (curtain != null) Object.Destroy(curtain);
            if (meniscus != null) Object.Destroy(meniscus);
            Object.Destroy(curtainMaterial);
            Object.Destroy(meniscusMaterial);
            Object.Destroy(panel);
            Object.Destroy(panelMaterial);
            ocean.OceanMaterial = originalOcean;
            Object.Destroy(oceanMaterial);
            target.Release();
            Object.Destroy(target);
            GraphicsSettings.defaultRenderPipeline = previousDefault;
            QualitySettings.renderPipeline = previousQuality;
            OceanRenderer.EnableGraphicsInBatchMode = previousGraphics;
        }
        yield return new ExitPlayMode();
    }

    static GameObject InstantiatePrefab(string name, Camera camera, int layer, out Material material)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Crest/Crest/Data/{name}.prefab");
        Assert.That(prefab, Is.Not.Null, name);
        var instance = Object.Instantiate(prefab, camera.transform, false);
        instance.layer = layer;
        var renderer = instance.GetComponent<Renderer>();
        material = new Material(renderer.sharedMaterial);
        renderer.sharedMaterial = material;
        return instance;
    }

    static Color32[] Capture(Camera camera, RenderTexture target, bool builtIn, string path)
    {
        var previousActive = RenderTexture.active;
        var pixels = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        try
        {
            if (builtIn)
            {
                var previous = camera.targetTexture;
                camera.targetTexture = target;
                try { camera.Render(); }
                finally { camera.targetTexture = previous; }
            }
            else RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            pixels.Apply();
            if (path != null) File.WriteAllBytes(path, pixels.EncodeToPNG());
            return pixels.GetPixels32();
        }
        finally
        {
            RenderTexture.active = previousActive;
            Object.Destroy(pixels);
        }
    }

    static int ChangedPixels(Color32[] baseline, Color32[] current, int minimumChange)
    {
        int count = 0;
        for (int i = 0; i < baseline.Length; i++)
        {
            var a = baseline[i];
            var b = current[i];
            if (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) > minimumChange) count++;
        }
        return count;
    }

    [UnityTearDown]
    public IEnumerator ExitFailedPlayMode()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
