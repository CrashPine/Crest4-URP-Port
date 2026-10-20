using System.Collections;
using System.IO;
using Crest;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

public class UnderwaterVolumeTests
{
    Material _material;
    RenderTexture _target;
    float _timeScale;

    [UnityTest] public IEnumerator PortalRestrictsFogToGeometry() => Check(UnderwaterRenderer.Mode.Portal);
    [UnityTest] public IEnumerator VolumeRestrictsFogToGeometry() => Check(UnderwaterRenderer.Mode.Volume);
    [UnityTest] public IEnumerator FlyThroughRestrictsFogToGeometry() => Check(UnderwaterRenderer.Mode.VolumeFlyThrough);

    IEnumerator Check(UnderwaterRenderer.Mode mode)
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Underwater.unity");
        yield return new EnterPlayMode();
        OceanRenderer.EnableGraphicsInBatchMode = true;
        _timeScale = Time.timeScale;
        var camera = Camera.main;
        // Keep the surface and shoreline outside the view. Volume mode intentionally
        // clips the ocean surface too, which is a separate behavior from fog confinement.
        camera.transform.SetPositionAndRotation(new Vector3(0, -50, -15), Quaternion.identity);
        var underwater = camera.GetComponent<UnderwaterRenderer>();
        underwater.enabled = false;
        var geometry = GameObject.CreatePrimitive(PrimitiveType.Cube);
        geometry.name = "Underwater test volume";
        geometry.transform.position = new Vector3(0, -50, 0);
        geometry.transform.localScale = new Vector3(8, 8, 8);
        geometry.GetComponent<Renderer>().enabled = false;
        underwater._mode = mode;
        underwater._volumeGeometry = geometry.GetComponent<MeshFilter>();
        var backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
        backdrop.transform.position = new Vector3(0, -50, 15);
        backdrop.transform.localScale = Vector3.one * 100;
        _material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        _material.SetColor("_BaseColor", new Color(.7f, .7f, .7f));
        backdrop.GetComponent<Renderer>().sharedMaterial = _material;
        for (int i = 0; i < 10; i++) yield return null;
        Time.timeScale = 0;
        _target = new RenderTexture(320, 180, 24, RenderTextureFormat.ARGB32);
        _target.Create();
        var baseline = Capture(camera, mode + "-Baseline");
        underwater.enabled = true;
        yield return null;
        var effect = Capture(camera, mode + "-Outside");
        int changed = 0;
        int borderChanged = 0;
        for (int y = 0; y < 180; y++)
        for (int x = 0; x < 320; x++)
        {
            int index = y * 320 + x;
            var a = baseline[index];
            var b = effect[index];
            int difference = Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
            if (difference <= 15) continue;
            changed++;
            if (x < 10 || x >= 310 || y < 10 || y >= 170) borderChanged++;
        }
        File.WriteAllText($"Evidence/Underwater-{mode}-{SystemInfo.graphicsDeviceType}.txt",
            $"ChangedPixels={changed}; BorderChangedPixels={borderChanged}\n");
        Assert.That(changed, Is.GreaterThan(500), "Volume must apply visible underwater fog.");
        Assert.That(borderChanged, Is.LessThan(100), "Fog must not escape the projected volume.");
        if (mode == UnderwaterRenderer.Mode.VolumeFlyThrough)
        {
            camera.transform.position = new Vector3(0, -50, 0);
            yield return null;
            var inside = Capture(camera, mode + "-Inside");
            int tinted = 0;
            foreach (var color in inside)
                if (Mathf.Max(color.r, Mathf.Max(color.g, color.b)) - Mathf.Min(color.r, Mathf.Min(color.g, color.b)) > 15) tinted++;
            Assert.That(tinted, Is.GreaterThan(1000), "The effect must remain visible inside the volume.");
        }
    }

    Color32[] Capture(Camera camera, string label)
    {
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = _target });
        var previous = RenderTexture.active;
        var pixels = new Texture2D(320, 180, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = _target;
            pixels.ReadPixels(new Rect(0, 0, 320, 180), 0, 0);
            pixels.Apply();
            Directory.CreateDirectory("Evidence");
            File.WriteAllBytes($"Evidence/Underwater-{label}-{SystemInfo.graphicsDeviceType}.png", pixels.EncodeToPNG());
            return pixels.GetPixels32();
        }
        finally { RenderTexture.active = previous; Object.Destroy(pixels); }
    }

    [UnityTearDown]
    public IEnumerator RestoreState()
    {
        Time.timeScale = _timeScale;
        if (_target != null) { _target.Release(); Object.Destroy(_target); }
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
        if (_material != null) Object.DestroyImmediate(_material);
    }
}
