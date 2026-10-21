using System.Collections;
using System.Collections.Generic;
using System.IO;
using Crest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

public class SurfaceInputTests
{
    const int Size = 192;
    readonly List<Material> _materials = new List<Material>();
    RenderTexture _target;
    Camera _camera;
    float _timeScale = 1;

    [UnityTest]
    public IEnumerator CubeSphereAndGeometryClipSurfaceAndDeregisterCleanly()
    {
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        _timeScale = Time.timeScale;
        Time.timeScale = 0;
        var ocean = OceanRenderer.Instance;
        foreach (var fft in Object.FindObjectsByType<ShapeFFT>(FindObjectsSortMode.None)) fft.enabled = false;
        _camera = Camera.main;
        _camera.transform.SetPositionAndRotation(new Vector3(0, 10, 0), Quaternion.Euler(90, 0, 0));
        _camera.orthographic = true;
        _camera.orthographicSize = 12;
        var underwater = _camera.GetComponent<UnderwaterRenderer>();
        if (underwater != null) underwater.enabled = false;
        _target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
        _target.Create();
        var surface = Track(new Material(ocean.OceanMaterial));
        foreach (string keyword in new[] { "_APPLYNORMALMAPPING_ON", "_FOAM_ON", "_TRANSPARENCY_ON", "_CAUSTICS_ON" }) surface.DisableKeyword(keyword);
        surface.SetFloat("_Foam", 0);
        surface.SetFloat("_ApplyNormalMapping", 0);
        surface.EnableKeyword("_CLIPSURFACE_ON");
        surface.SetFloat("_ClipSurface", 1);
        ocean.OceanMaterial = surface;
        var configuration = new SerializedObject(ocean);
        configuration.FindProperty("_createFoamSim").boolValue = false;
        configuration.FindProperty("_createClipSurfaceData").boolValue = true;
        configuration.ApplyModifiedPropertiesWithoutUndo();
        var floor = GameObject.Find("Visible refraction reference floor");
        var floorMaterial = Track(new Material(Shader.Find("Universal Render Pipeline/Unlit")));
        floorMaterial.SetColor("_BaseColor", Color.magenta);
        floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
        yield return WaitForOceanUpdates(5);
        var clock = ocean.gameObject.AddComponent<TimeProviderCustom>();
        clock._overrideTime = clock._overrideDeltaTime = true;
        clock._time = clock._deltaTime = 0;
        ocean.PushTimeProvider(clock);
        yield return WaitForOceanUpdates(4);
        Assert.That(ocean.Root.GetComponentsInChildren<OceanChunkRenderer>().Length, Is.GreaterThan(0));
        var baseline = Capture("Baseline");
        Assert.That(Magenta(baseline), Is.EqualTo(0), "Opaque ocean must initially cover the reference floor.");
        string report = $"Evidence/ClipSurface-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(report, "Fixed ocean time/delta=0; FFT off; 192x192 top-down orthographic view; magenta floor under opaque ocean; clip tolerance=0.005; primitives store signed-distance gradients, surface clips at >0.5\n");
        var owner = new GameObject("Known clip primitive");
        owner.transform.localScale = new Vector3(8, 4, 8);
        var input = owner.AddComponent<RegisterClipSurfaceInput>();
        for (int mode = 0; mode < 3; mode++)
        {
            string label = mode == 0 ? "Cube" : mode == 1 ? "Sphere" : "Geometry";
            if (mode == 1)
            {
                var primitive = new SerializedObject(input);
                primitive.FindProperty("_primitive").intValue = (int)RegisterClipSurfaceInput.Primitive.Sphere;
                primitive.ApplyModifiedPropertiesWithoutUndo();
                input.enabled = true;
            }
            else if (mode == 2)
            {
                owner = GameObject.CreatePrimitive(PrimitiveType.Plane);
                owner.name = "Known clip geometry";
                owner.transform.localScale = Vector3.one * .8f;
                owner.GetComponent<Renderer>().sharedMaterial = Track(new Material(Shader.Find("Crest/Inputs/Clip Surface/Remove Area")));
                input = owner.AddComponent<RegisterClipSurfaceInput>();
                var geometry = new SerializedObject(input);
                geometry.FindProperty("_mode").intValue = (int)RegisterClipSurfaceInput.Mode.Geometry;
                geometry.ApplyModifiedPropertiesWithoutUndo();
            }
            yield return WaitForOceanUpdates(4);
            var target = ocean._lodDataClipSurface.DataTexture;
            var data = AsyncGPUReadback.Request(target, 0, TextureFormat.RFloat);
            while (!data.done) yield return null;
            Assert.That(data.hasError, Is.False);
            float center = ClipSample(data, Vector2.zero, out var centerPosition);
            float corner = ClipSample(data, new Vector2(3, 3), out var cornerPosition);
            float outside = ClipSample(data, new Vector2(8, 0), out var outsidePosition);
            float expectedCenter = ExpectedClip(mode, centerPosition);
            float expectedCorner = ExpectedClip(mode, cornerPosition);
            float expectedOutside = ExpectedClip(mode, outsidePosition);
            var clipped = Capture(label);
            int hole = Magenta(clipped), changed = Different(baseline, clipped);
            File.AppendAllText(report, $"Mode={label}; Center={center}; Corner={corner}; Outside={outside}; MagentaPixels={hole}; ChangedPixels={changed}\n");
            File.AppendAllText(report, $"ExpectedCenter={expectedCenter}; ExpectedCorner={expectedCorner}; ExpectedOutside={expectedOutside}; CornerWorldTexel={cornerPosition}\n");
            Assert.That(center, Is.EqualTo(expectedCenter).Within(.005f));
            Assert.That(corner, Is.EqualTo(expectedCorner).Within(.005f));
            Assert.That(outside, Is.EqualTo(expectedOutside).Within(.005f));
            Assert.That(hole, Is.InRange(1000, 8000));
            Assert.That(changed, Is.InRange(1000, 8000));
            Assert.That(Different(baseline, clipped, borderOnly: true), Is.EqualTo(0));
            input.enabled = false;
            yield return WaitForOceanUpdates(4);
            var cleared = AsyncGPUReadback.Request(ocean._lodDataClipSurface.DataTexture, 0, TextureFormat.RFloat);
            while (!cleared.done) yield return null;
            Assert.That(cleared.hasError, Is.False);
            float peak = 0;
            foreach (float value in cleared.GetData<float>(0)) peak = Mathf.Max(peak, Mathf.Abs(value));
            int restored = Different(baseline, Capture(label + "-Disabled"));
            File.AppendAllText(report, $"Mode={label}; DisabledPeak={peak}; RestoredChangedPixels={restored}\n");
            Assert.That(peak, Is.LessThan(.0001f));
            Assert.That(restored, Is.LessThan(20));
        }
    }

    static float ExpectedClip(int mode, Vector2 position)
    {
        if (mode == 2) return Mathf.Abs(position.x) < 4 && Mathf.Abs(position.y) < 4 ? 1 : 0;
        float distance = mode == 0 ? Mathf.Max(Mathf.Abs(position.x), Mathf.Abs(position.y)) / 8 : position.magnitude / 8;
        return Mathf.Clamp01(1 - distance);
    }

    static float ClipSample(AsyncGPUReadbackRequest request, Vector2 position, out Vector2 texelPosition)
    {
        var ocean = OceanRenderer.Instance;
        int width = ocean._lodDataClipSurface.DataTexture.width;
        var transform = ocean._lodTransform._renderData[0].Current;
        var center = new Vector2(transform._posSnapped.x, transform._posSnapped.z);
        var uv = (position - center) / transform._texelWidth + Vector2.one * (width * .5f);
        int x = Mathf.FloorToInt(uv.x), y = Mathf.FloorToInt(uv.y);
        Assert.That(x, Is.InRange(0, width - 1));
        Assert.That(y, Is.InRange(0, width - 1));
        texelPosition = center + (new Vector2(x + .5f, y + .5f) - Vector2.one * (width * .5f)) * transform._texelWidth;
        float value = request.GetData<float>(0)[y * width + x];
        Assert.That(float.IsNaN(value) || float.IsInfinity(value), Is.False);
        return value;
    }

    static IEnumerator WaitForOceanUpdates(int count)
    {
        int previous = BuildCommandBufferBase._lastUpdateFrame, completed = 0;
        float deadline = Time.realtimeSinceStartup + 15;
        while (completed < count && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            int current = BuildCommandBufferBase._lastUpdateFrame;
            if (current == previous) continue;
            previous = current;
            completed++;
        }
        Assert.That(completed, Is.EqualTo(count));
    }

    Material Track(Material material) { _materials.Add(material); return material; }
    Color32[] Capture(string label)
    {
        RenderPipeline.SubmitRenderRequest(_camera, new UniversalRenderPipeline.SingleCameraRequest { destination = _target });
        var previous = RenderTexture.active;
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = _target;
            texture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            texture.Apply();
            File.WriteAllBytes($"Evidence/ClipSurface-{label}-{SystemInfo.graphicsDeviceType}.png", texture.EncodeToPNG());
            return texture.GetPixels32();
        }
        finally { RenderTexture.active = previous; Object.Destroy(texture); }
    }

    static int Magenta(Color32[] pixels)
    {
        int count = 0;
        foreach (var pixel in pixels) if (pixel.r > 200 && pixel.b > 200 && pixel.g < 50) count++;
        return count;
    }
    static int Different(Color32[] a, Color32[] b, bool borderOnly = false)
    {
        int count = 0;
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            if (borderOnly && x >= 16 && x < Size - 16 && y >= 16 && y < Size - 16) continue;
            int i = y * Size + x;
            if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) > 12) count++;
        }
        return count;
    }

    [UnityTearDown]
    public IEnumerator Restore()
    {
        Time.timeScale = _timeScale;
        if (_target != null) { _target.Release(); Object.Destroy(_target); }
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
        foreach (var material in _materials) if (material != null) Object.DestroyImmediate(material);
        _materials.Clear();
    }
}
