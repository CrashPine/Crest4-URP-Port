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

public class FlowSurfaceTests
{
    const int Width = 320, Height = 180;
    readonly List<Material> _materials = new List<Material>();
    SimSettingsFoam _settings;
    Material _surface;
    RenderTexture _target;
    Camera _camera;
    float _timeScale = 1;

    [UnityTest]
    public IEnumerator FlowChangesSurfaceNormalsAtFixedTime()
    {
        // Opt in before scene creation: a headless-created root has no meshes,
        // and changing feature toggles need not rebuild its geometry.
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        yield return CheckSurface(false);
    }

    [UnityTest]
    public IEnumerator FlowChangesSurfaceFoamAtFixedTime()
    {
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        yield return CheckSurface(true);
    }

    IEnumerator CheckSurface(bool foam)
    {
        OceanRenderer.EnableGraphicsInBatchMode = true;
        _timeScale = Time.timeScale;
        Time.timeScale = 0;
        var ocean = OceanRenderer.Instance;
        foreach (var fft in Object.FindObjectsByType<ShapeFFT>(FindObjectsSortMode.None)) fft.enabled = false;
        _camera = Camera.main;
        var underwater = _camera.GetComponent<UnderwaterRenderer>();
        if (underwater != null) underwater.enabled = false;
        _camera.transform.SetPositionAndRotation(new Vector3(0, 4, -10), Quaternion.Euler(20, 0, 0));
        _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        _target.Create();
        var surface = Track(new Material(ocean.OceanMaterial));
        _surface = surface;
        ocean.OceanMaterial = surface;
        foreach (string keyword in new[] { "_CAUSTICS_ON", "_SHADOWS_ON", "_TRANSPARENCY_ON", "_FOAM3DLIGHTING_ON" }) surface.DisableKeyword(keyword);
        SetKeyword(surface, "_FOAM_ON", foam);
        SetKeyword(surface, "_APPLYNORMALMAPPING_ON", !foam);
        surface.SetFloat("_Foam", foam ? 1 : 0);
        surface.SetFloat("_ApplyNormalMapping", foam ? 0 : 1);
        if (!foam)
        {
            // A uniform/saturated environment cannot reveal changes in reflected
            // normals. Use Crest's original procedural sky as a known gradient.
            surface.EnableKeyword("_PROCEDURALSKY_ON");
            surface.SetFloat("_ProceduralSky", 1);
            surface.SetColor("_SkyBase", new Color(.02f, .1f, .3f));
            surface.SetColor("_SkyTowardsSun", Color.white);
            surface.SetColor("_SkyAwayFromSun", new Color(.1f, .6f, .2f));
            surface.SetFloat("_SkyDirectionality", .2f);
            surface.SetFloat("_NormalsStrength", .36f);
            surface.SetFloat("_DirectionalLightBoost", 0);
        }
        surface.EnableKeyword("_FLOW_ON");
        surface.SetFloat("_Flow", 1);
        Assert.That(surface.GetTexture(foam ? "_FoamTexture" : "_Normals"), Is.Not.Null);
        var configuration = new SerializedObject(ocean);
        configuration.FindProperty("_createFlowSim").boolValue = true;
        configuration.FindProperty("_createFoamSim").boolValue = foam;
        configuration.ApplyModifiedPropertiesWithoutUndo();
        _settings = ScriptableObject.CreateInstance<SimSettingsFoam>();
        _settings._prewarm = false;
        _settings._waveFoamStrength = _settings._shorelineFoamStrength = _settings._foamFadeRate = 0;
        ocean._simSettingsFoam = _settings;
        var flowOwner = GameObject.CreatePrimitive(PrimitiveType.Plane);
        flowOwner.transform.localScale = Vector3.one * 100;
        var flow = Track(new Material(Shader.Find("Crest/Inputs/Flow/Fixed Direction")));
        flow.SetFloat("_Speed", 0);
        flow.SetFloat("_Direction", 0);
        flowOwner.GetComponent<Renderer>().sharedMaterial = flow;
        flowOwner.AddComponent<RegisterFlowInput>();
        yield return WaitForOceanUpdates(4);
        var clock = ocean.gameObject.AddComponent<TimeProviderCustom>();
        clock._overrideTime = clock._overrideDeltaTime = true;
        clock._time = .5f;
        clock._deltaTime = foam ? 1f / 30 : 0;
        ocean.PushTimeProvider(clock);
        if (foam)
        {
            var seed = GameObject.CreatePrimitive(PrimitiveType.Plane);
            seed.transform.localScale = Vector3.one * 100;
            var material = Track(new Material(Shader.Find("Crest/Inputs/Foam/Override Foam")));
            material.SetFloat("_FoamValue", .65f);
            seed.GetComponent<Renderer>().sharedMaterial = material;
            var input = seed.AddComponent<RegisterFoamInput>();
            yield return WaitForOceanUpdates(4);
            clock._deltaTime = 0;
            input.enabled = false;
        }
        yield return WaitForOceanUpdates(6);
        Assert.That(ocean.TimeProvider, Is.SameAs(clock));
        string mode = foam ? "Foam" : "Normals";
        if (foam)
        {
            var data = AsyncGPUReadback.Request(ocean._lodDataFoam.DataTexture, 0, TextureFormat.RFloat);
            while (!data.done) yield return null;
            Assert.That(data.hasError, Is.False);
            int width = ocean._lodDataFoam.DataTexture.width;
            Assert.That(data.GetData<float>(0)[width * (width / 2) + width / 2], Is.EqualTo(.65f).Within(.005f));
        }
        var baseline = Capture(mode + "-Zero");
        Assert.That(flowOwner.GetComponent<Renderer>().enabled, Is.False);
        int chunks = 0;
        var frustum = GeometryUtility.CalculateFrustumPlanes(_camera);
        foreach (var chunk in ocean.Root.GetComponentsInChildren<OceanChunkRenderer>())
        {
            var renderer = chunk.GetComponent<Renderer>();
            // Crest updates the surface material in OnWillRenderObject, so
            // off-screen chunks may legitimately retain the previous material.
            if (!renderer.enabled || !GeometryUtility.TestPlanesAABB(frustum, renderer.bounds)) continue;
            Assert.That(renderer.sharedMaterial, Is.SameAs(surface), "Capture must use the configured surface material.");
            chunks++;
        }
        Assert.That(chunks, Is.GreaterThan(0));
        Assert.That(surface.IsKeywordEnabled("_APPLYNORMALMAPPING_ON"), Is.EqualTo(!foam));
        Assert.That(surface.IsKeywordEnabled("_FOAM_ON"), Is.EqualTo(foam));
        flow.SetFloat("_Speed", 3);
        yield return WaitForOceanUpdates(4);
        var vectorData = AsyncGPUReadback.Request(ocean._lodDataFlow.DataTexture, 0, TextureFormat.RGFloat);
        while (!vectorData.done) yield return null;
        Assert.That(vectorData.hasError, Is.False);
        int flowWidth = ocean._lodDataFlow.DataTexture.width;
        var vector = vectorData.GetData<Vector2>(0)[flowWidth * (flowWidth / 2) + flowWidth / 2];
        Assert.That(Vector2.Distance(vector, new Vector2(3, 0)), Is.LessThan(.05f));
        var moving = Capture(mode + "-Flow");
        int affected = Different(baseline, moving);
        flow.SetFloat("_Speed", 0);
        yield return WaitForOceanUpdates(4);
        int restored = Different(baseline, Capture(mode + "-Restored"));
        surface.DisableKeyword("_FLOW_ON");
        surface.SetFloat("_Flow", 0);
        yield return WaitForOceanUpdates(3);
        var disabled = Capture(mode + "-Disabled-zero");
        flow.SetFloat("_Speed", 3);
        yield return WaitForOceanUpdates(4);
        int control = Different(disabled, Capture(mode + "-Disabled-flow"));
        File.WriteAllText($"Evidence/FlowSurface-{mode}-{SystemInfo.graphicsDeviceType}.txt",
            $"OceanTime=0.5; Delta=0; Flow={vector}; ChangedPixels={affected}; RestoredChangedPixels={restored}; KeywordOffChangedPixels={control}; DifferenceThreshold=12\nNormalsKeyword={surface.IsKeywordEnabled("_APPLYNORMALMAPPING_ON")}; FoamKeyword={surface.IsKeywordEnabled("_FOAM_ON")}; NormalStrength={surface.GetFloat("_NormalsStrength")}; CurrentTime={ocean.CurrentTime}; Root={ocean.Root.position}; Scale={ocean.Scale}\nOriginal surface shader, normal/foam textures and flow input; FFT off, foam amount held constant.\n");
        Assert.That(affected, Is.GreaterThan(100), "Flow must change the isolated surface detail at fixed ocean time.");
        Assert.That(restored, Is.LessThan(20), "Returning flow to zero must restore the original image.");
        Assert.That(control, Is.LessThan(20), "With the flow keyword disabled, the input must not change the image.");
        Assert.That(ocean.CurrentTime, Is.EqualTo(.5f));
        Assert.That(ocean.DeltaTime, Is.EqualTo(0));
    }

    Material Track(Material material) { _materials.Add(material); return material; }
    static void SetKeyword(Material material, string keyword, bool enabled)
    {
        if (enabled) material.EnableKeyword(keyword); else material.DisableKeyword(keyword);
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

    Color32[] Capture(string label)
    {
        RenderPipeline.SubmitRenderRequest(_camera, new UniversalRenderPipeline.SingleCameraRequest { destination = _target });
        var previous = RenderTexture.active;
        var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = _target;
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            texture.Apply();
            File.WriteAllBytes($"Evidence/FlowSurface-{label}-{SystemInfo.graphicsDeviceType}.png", texture.EncodeToPNG());
            return texture.GetPixels32();
        }
        finally { RenderTexture.active = previous; Object.Destroy(texture); }
    }

    static int Different(Color32[] a, Color32[] b)
    {
        int result = 0;
        for (int i = 0; i < a.Length; i++)
            if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) > 12) result++;
        return result;
    }

    [UnityTearDown]
    public IEnumerator Restore()
    {
        Time.timeScale = _timeScale;
        // The negative control deliberately disables the surface flow keyword
        // while retaining nonzero flow data. Restore a consistent configuration
        // before ExitPlayMode changes the batch graphics state and rebuilds.
        if (_surface != null) { _surface.EnableKeyword("_FLOW_ON"); _surface.SetFloat("_Flow", 1); }
        if (_target != null) { _target.Release(); Object.Destroy(_target); }
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
        if (_settings != null) Object.DestroyImmediate(_settings);
        foreach (var material in _materials) if (material != null) Object.DestroyImmediate(material);
        _materials.Clear();
    }
}
