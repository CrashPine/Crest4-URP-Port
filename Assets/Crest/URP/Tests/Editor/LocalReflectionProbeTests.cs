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

public class LocalReflectionProbeTests
{
    [UnityTest]
    public IEnumerator LocalProbeChangesWaterAndRestoresAfterReenable() => CheckProbe(false);

    [UnityTest]
    public IEnumerator OverlappingProbesBlendBothCubemaps() => CheckProbe(true);

    static IEnumerator CheckProbe(bool overlap)
    {
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        var ocean = OceanRenderer.Instance;
        var camera = Camera.main;
        camera.enabled = false;
        camera.transform.SetPositionAndRotation(new Vector3(0, 4, -12), Quaternion.Euler(16, 0, 0));
        camera.cullingMask = 1 << 4;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.blue;
        camera.allowHDR = camera.allowMSAA = false;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        foreach (var shape in Object.FindObjectsByType<ShapeWaves>()) shape.enabled = false;

        var sourceMaterial = ocean.OceanMaterial;
        var surface = new Material(sourceMaterial);
        foreach (string keyword in new[] { "_PLANARREFLECTIONS_ON", "_OVERRIDEREFLECTIONCUBEMAP_ON",
                     "_PROCEDURALSKY_ON", "_COMPUTEDIRECTIONALLIGHT_ON", "_TRANSPARENCY_ON",
                     "_APPLYNORMALMAPPING_ON", "_FOAM_ON" })
            surface.DisableKeyword(keyword);
        surface.SetFloat("_Specular", 1);
        ocean.OceanMaterial = surface;

        var clock = ocean.gameObject.AddComponent<TimeProviderCustom>();
        clock._overrideTime = clock._overrideDeltaTime = true;
        clock._time = 1;
        ocean.PushTimeProvider(clock);
        var owner = new GameObject("Local reflection reference");
        var probe = owner.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Custom;
        probe.size = Vector3.one * 160;
        var red = SolidCubemap(Color.red);
        var green = SolidCubemap(Color.green);
        var target = new RenderTexture(320, 180, 24, RenderTextureFormat.ARGB32);
        target.Create();
        var previousPipeline = QualitySettings.renderPipeline;
        UniversalRenderPipelineAsset pipeline = null;
        GameObject secondOwner = null;
        try
        {
            probe.customBakedTexture = red;
            ReflectionProbe second = null;
            if (overlap)
            {
                pipeline = Object.Instantiate((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline);
                var settings = new SerializedObject(pipeline);
                settings.FindProperty("m_ReflectionProbeBlending").boolValue = true;
                settings.ApplyModifiedPropertiesWithoutUndo();
                QualitySettings.renderPipeline = pipeline;
                secondOwner = new GameObject("Second local reflection reference");
                second = secondOwner.AddComponent<ReflectionProbe>();
                second.mode = ReflectionProbeMode.Custom;
                second.size = probe.size;
                second.customBakedTexture = green;
                second.enabled = false;
            }
            for (int i = 0; i < 12; i++) yield return null;
            var redPixels = Capture(camera, target);
            if (overlap)
            {
                second.enabled = true;
                yield return null;
                var both = Capture(camera, target);
                probe.enabled = false;
                yield return null;
                var onlyGreen = Capture(camera, target);
                probe.enabled = true;
                second.enabled = false;
                yield return null;
                var redAgain = Capture(camera, target);
                int blended = 0, restoredRed = 0;
                for (int i = 0; i < both.Length; i++)
                {
                    if (redPixels[i].r - both[i].r > 5 && both[i].r - onlyGreen[i].r > 5 &&
                        onlyGreen[i].g - both[i].g > 5 && both[i].g - redPixels[i].g > 5)
                        blended++;
                    if (ColorDifference(redPixels[i], redAgain[i]) != 0) restoredRed++;
                }
                string result = $"BlendedPixels={blended}; RestoreDifference={restoredRed}; Pixels={both.Length}";
                File.WriteAllText($"Evidence/P11-OverlapReflection-{SystemInfo.graphicsDeviceType}.txt", result + System.Environment.NewLine);
                Assert.That(blended, Is.GreaterThan(500), result);
                Assert.That(restoredRed, Is.Zero, result);
            }
            else
            {
                probe.customBakedTexture = green;
                yield return null;
                var greenPixels = Capture(camera, target);
                probe.enabled = false;
                yield return null;
                var offPixels = Capture(camera, target);
                probe.enabled = true;
                probe.customBakedTexture = red;
                yield return null;
                var restored = Capture(camera, target);

                int colorResponse = 0, registrationResponse = 0, restoration = 0;
                for (int i = 0; i < redPixels.Length; i++)
                {
                    var a = redPixels[i];
                    var b = greenPixels[i];
                    if (a.r - b.r > 8 && b.g - a.g > 8) colorResponse++;
                    if (ColorDifference(a, offPixels[i]) > 12) registrationResponse++;
                    if (ColorDifference(a, restored[i]) != 0) restoration++;
                }
                string report = $"RedGreenResponse={colorResponse}; ProbeOffResponse={registrationResponse}; RestoreDifference={restoration}; Pixels={redPixels.Length}";
                File.WriteAllText($"Evidence/P11-LocalReflection-{SystemInfo.graphicsDeviceType}.txt", report + System.Environment.NewLine);
                Assert.That(colorResponse, Is.GreaterThan(500), report);
                Assert.That(registrationResponse, Is.GreaterThan(500), report);
                Assert.That(restoration, Is.Zero, report);
            }
        }
        finally
        {
            ocean.PopTimeProvider(clock);
            ocean.OceanMaterial = sourceMaterial;
            if (pipeline != null)
            {
                QualitySettings.renderPipeline = previousPipeline;
                Object.Destroy(pipeline);
            }
            if (secondOwner != null) Object.Destroy(secondOwner);
            probe.enabled = false;
            Object.Destroy(owner);
            Object.Destroy(red);
            Object.Destroy(green);
            Object.Destroy(target);
            Object.Destroy(surface);
        }
        yield return new ExitPlayMode();
    }

    static Cubemap SolidCubemap(Color color)
    {
        const int size = 16;
        var cube = new Cubemap(size, TextureFormat.RGBA32, true);
        var pixels = new Color[size * size];
        System.Array.Fill(pixels, color);
        for (int i = 0; i < 6; i++) cube.SetPixels(pixels, (CubemapFace)i);
        cube.Apply();
        return cube;
    }

    static int ColorDifference(Color32 a, Color32 b) =>
        Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

    static Color32[] Capture(Camera camera, RenderTexture target)
    {
        var previous = RenderTexture.active;
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        try
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            return pixels.GetPixels32();
        }
        finally
        {
            RenderTexture.active = previous;
            Object.Destroy(pixels);
        }
    }
}
