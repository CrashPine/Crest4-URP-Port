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

public class UnderwaterBoundaryTests
{
    const int Width = 320, Height = 180;
    readonly List<Material> _materials = new List<Material>();
    RenderTexture _target;
    Camera _camera;
    UnderwaterRenderer _underwater;
    float _timeScale = 1;

    void Setup(float height)
    {
        Assert.That(Application.isPlaying, Is.True);
        OceanRenderer.EnableGraphicsInBatchMode = true;
        _timeScale = Time.timeScale;
        Time.timeScale = 0;
        foreach (var fft in Object.FindObjectsByType<ShapeFFT>(FindObjectsSortMode.None)) fft.enabled = false;
        var ocean = OceanRenderer.Instance;
        var time = ocean.gameObject.AddComponent<TimeProviderCustom>();
        time._overrideTime = true;
        time._time = 0;
        time._overrideDeltaTime = true;
        time._deltaTime = 0;
        ocean.PushTimeProvider(time);
        _camera = Camera.main;
        _camera.transform.SetPositionAndRotation(new Vector3(0, height, -15), Quaternion.identity);
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = new Color(.7f, .7f, .7f);
        _underwater = _camera.GetComponent<UnderwaterRenderer>();
        _underwater.enabled = false;
        _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        _target.Create();
    }

    [UnityTest]
    public IEnumerator SurfaceCrossingAndMeniscusRespondAtFixedTime()
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Underwater.unity");
        yield return new EnterPlayMode();
        Setup(3);
        for (int i = 0; i < 30; i++) yield return null;
        Quad("Opaque backdrop", new Vector3(0, 0, 20), new Vector3(100, 100, 1), new Color(.7f, .7f, .7f));
        for (int i = 0; i < 5; i++) yield return null;
        var aboveBase = Capture("Above-baseline");
        _underwater.enabled = true;
        for (int i = 0; i < 20; i++) yield return null;
        var above = Capture("Above");
        Assert.That(_underwater.IsActive, Is.False, "The above-water optimization must follow camera height.");
        Assert.That(Different(aboveBase, above), Is.LessThan(20));

        _camera.transform.position = new Vector3(0, -3, -15);
        for (int i = 0; i < 30; i++) yield return null;
        var below = Capture("Below");
        Assert.That(_underwater.IsActive, Is.True);
        _underwater.enabled = false;
        yield return null;
        var belowBase = Capture("Below-baseline");
        int belowChanged = Different(belowBase, below);
        Assert.That(belowChanged, Is.GreaterThan(10000), "Submerged camera must receive the effect.");

        _camera.transform.position = new Vector3(0, .1f, -15);
        _underwater.enabled = true;
        for (int i = 0; i < 30; i++) yield return null;
        SetMeniscus(false);
        var without = Capture("Waterline-without-meniscus");
        SetMeniscus(true);
        var with = Capture("Waterline-with-meniscus");
        int meniscusChanged = Different(without, with, 2);
        File.WriteAllText($"Evidence/Boundary-{SystemInfo.graphicsDeviceType}.txt",
            $"BelowChangedPixels={belowChanged}; MeniscusChangedPixels={meniscusChanged}\nFixed ocean time=0; FFT disabled\n");
        Assert.That(meniscusChanged, Is.GreaterThan(20), "Meniscus must affect the visible waterline.");
        Assert.That(meniscusChanged, Is.LessThan(5000), "Meniscus must remain a narrow boundary.");
        _camera.transform.position = new Vector3(0, 3, -15);
        for (int i = 0; i < 30; i++) yield return null;
        var returned = Capture("Returned-above");
        Assert.That(_underwater.IsActive, Is.False);
        Assert.That(Different(aboveBase, returned), Is.LessThan(20), "Surfacing must restore the original above-water image.");
    }

    [UnityTest]
    public IEnumerator OverlayFogsGeometryAndPreservesBackground()
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Underwater.unity");
        yield return new EnterPlayMode();
        Setup(-50);
        for (int i = 0; i < 30; i++) yield return null;
        HideExistingGeometry();
        _camera.cullingMask = 1; // Test foreground only; exclude the independently shaded ocean surface.
        _underwater._layer = 0;
        Quad("Overlay foreground", new Vector3(-3, -50, 0), new Vector3(6, 8, 1), new Color(.7f, .7f, .7f));
        yield return null;
        var baseline = Capture("Overlay-baseline");
        _underwater._overlay = true;
        _underwater.enabled = true;
        for (int i = 0; i < 10; i++) yield return null;
        var overlay = Capture("Overlay");
        int foreground = Different(baseline, overlay, 15, 0, Width / 2);
        int background = Different(baseline, overlay, 15, Width * 3 / 4, Width);
        File.WriteAllText($"Evidence/Overlay-{SystemInfo.graphicsDeviceType}.txt",
            $"ForegroundChangedPixels={foreground}; BackgroundChangedPixels={background}\n");
        Assert.That(foreground, Is.GreaterThan(500));
        Assert.That(background, Is.LessThan(20), "Overlay must discard pixels without scene depth.");
    }

    [UnityTest]
    public IEnumerator EffectOrderingPreservesUnfoggedTransparentShaderBeforeTransparents()
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Underwater.unity");
        yield return new EnterPlayMode();
        Setup(-50);
        for (int i = 0; i < 30; i++) yield return null;
        HideExistingGeometry();
        Quad("Opaque backdrop", new Vector3(0, -50, 20), new Vector3(100, 100, 1), new Color(.7f, .7f, .7f));
        var transparent = Quad("Transparent reference", new Vector3(-3, -50, 0), new Vector3(6, 8, 1), new Color(1, 0, 0, .9f));
        transparent.SetFloat("_Surface", 1);
        transparent.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        transparent.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        transparent.SetFloat("_ZWrite", 0);
        transparent.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        transparent.renderQueue = (int)RenderQueue.Transparent;
        _underwater.EnableShaderAPI = false;
        _underwater.enabled = true;
        for (int i = 0; i < 10; i++) yield return null;
        var after = Capture("After-transparents");
        _underwater.enabled = false;
        _underwater.EnableShaderAPI = true;
        _underwater.enabled = true;
        for (int i = 0; i < 10; i++) yield return null;
        var before = Capture("Before-transparents");
        int transparentChanged = Different(after, before, 15, 0, Width / 2);
        int opaqueChanged = Different(after, before, 15, Width * 3 / 4, Width);
        var reference = before[Height / 2 * Width + Width / 3];
        File.WriteAllText($"Evidence/UnderwaterOrder-{SystemInfo.graphicsDeviceType}.txt",
            $"TransparentChangedPixels={transparentChanged}; OpaqueChangedPixels={opaqueChanged}; BeforeReference={reference}\n");
        Assert.That(transparentChanged, Is.GreaterThan(500));
        Assert.That(opaqueChanged, Is.LessThan(20), "Opaque fog must be independent of transparent scheduling.");
        Assert.That(reference.r, Is.GreaterThan(180), "A shader without the Crest fog API renders after the before-transparent composite.");
    }

    Material Quad(string name, Vector3 position, Vector3 scale, Color color)
    {
        var owner = GameObject.CreatePrimitive(PrimitiveType.Quad);
        owner.name = name;
        owner.transform.position = position;
        owner.transform.localScale = scale;
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.SetColor("_BaseColor", color);
        owner.GetComponent<Renderer>().sharedMaterial = material;
        _materials.Add(material);
        return material;
    }

    static void HideExistingGeometry()
    {
        // An empty-background assertion needs pixels without ocean/floor/pier geometry.
        // The mask's fullscreen horizon still classifies this deep camera as submerged.
        foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            renderer.forceRenderingOff = true;
    }

    void SetMeniscus(bool enabled)
    {
        var settings = new SerializedObject(_underwater);
        settings.FindProperty("_meniscus").boolValue = enabled;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    Color32[] Capture(string label)
    {
        RenderPipeline.SubmitRenderRequest(_camera, new UniversalRenderPipeline.SingleCameraRequest { destination = _target });
        var previous = RenderTexture.active;
        var pixels = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = _target;
            pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            pixels.Apply();
            Directory.CreateDirectory("Evidence");
            File.WriteAllBytes($"Evidence/Boundary-{label}-{SystemInfo.graphicsDeviceType}.png", pixels.EncodeToPNG());
            return pixels.GetPixels32();
        }
        finally { RenderTexture.active = previous; Object.Destroy(pixels); }
    }

    static int Different(Color32[] a, Color32[] b, int threshold = 15, int startX = 0, int endX = Width)
    {
        int count = 0;
        for (int y = 0; y < Height; y++)
        for (int x = startX; x < endX; x++)
        {
            int i = y * Width + x;
            if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) > threshold) count++;
        }
        return count;
    }

    [UnityTearDown]
    public IEnumerator RestoreState()
    {
        Time.timeScale = _timeScale;
        if (_target != null) { _target.Release(); Object.Destroy(_target); }
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
        foreach (var material in _materials) if (material != null) Object.DestroyImmediate(material);
        _materials.Clear();
    }
}
