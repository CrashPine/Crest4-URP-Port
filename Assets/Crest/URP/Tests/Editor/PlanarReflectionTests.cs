using System.Collections;
using System.IO;
using Crest;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

public class PlanarReflectionTests
{
    Material _material;

    [UnityTest]
    public IEnumerator ReflectionContainsVisibleObjectAndUpdatesWhenHidden()
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Reflections.unity");
        yield return new EnterPlayMode();
        OceanRenderer.EnableGraphicsInBatchMode = true;
        var camera = Camera.main;
        camera.transform.position = new Vector3(0, 5, -15);
        camera.transform.LookAt(new Vector3(0, 2, 0));
        var cube = GameObject.Find("Reflection reference");
        cube.transform.position = new Vector3(0, 2, 0);
        cube.transform.localScale = new Vector3(3, 3, 3);
        _material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        _material.SetColor("_BaseColor", Color.magenta);
        var renderer = cube.GetComponent<Renderer>();
        renderer.sharedMaterial = _material;
        var reflection = camera.GetComponent<OceanPlanarReflection>();
        for (int i = 0; i < 8; i++) yield return null;
        Assert.That(reflection.ReflectionCamera, Is.Not.Null);
        var target = reflection.ReflectionCamera.targetTexture;
        Assert.That(target, Is.Not.Null);
        var visible = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32);
        while (!visible.done) yield return null;
        Assert.That(visible.hasError, Is.False);
        int visiblePixels = CountMagenta(visible);
        Save(visible, target, "Visible");
        renderer.enabled = false;
        for (int i = 0; i < 8; i++) yield return null;
        var hidden = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32);
        while (!hidden.done) yield return null;
        Assert.That(hidden.hasError, Is.False);
        int hiddenPixels = CountMagenta(hidden);
        Save(hidden, target, "Hidden");
        File.WriteAllText("Evidence/PlanarReflection-" + SystemInfo.graphicsDeviceType + ".txt",
            $"Unity={Application.unityVersion}\nVisibleMagentaPixels={visiblePixels}; HiddenMagentaPixels={hiddenPixels}\n");
        Assert.That(visiblePixels, Is.GreaterThan(100), "The reflected object must be present in the reflection target.");
        Assert.That(hiddenPixels, Is.LessThan(visiblePixels / 10), "The reflection must update when the source object disappears.");
        Assert.That(OceanRenderer.Instance.Root.gameObject.activeSelf, Is.True);
    }

    static int CountMagenta(AsyncGPUReadbackRequest request)
    {
        int count = 0;
        foreach (var color in request.GetData<Color32>())
            if (color.r > 180 && color.b > 180 && color.g < 80) count++;
        return count;
    }

    static void Save(AsyncGPUReadbackRequest request, RenderTexture target, string state)
    {
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        try
        {
            pixels.LoadRawTextureData(request.GetData<byte>());
            pixels.Apply();
            Directory.CreateDirectory("Evidence");
            File.WriteAllBytes($"Evidence/PlanarReflection-{SystemInfo.graphicsDeviceType}-{state}.png", pixels.EncodeToPNG());
        }
        finally { Object.Destroy(pixels); }
    }

    [UnityTearDown]
    public IEnumerator RestoreState()
    {
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
        if (_material != null) Object.DestroyImmediate(_material);
    }
}
