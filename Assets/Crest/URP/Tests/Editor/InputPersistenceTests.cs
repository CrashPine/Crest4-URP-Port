using System;
using System.Collections;
using System.IO;
using Crest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class InputPersistenceTests
{
    const string Parent = "Assets/Crest/URP/Tests/Editor";
    const string BaseScene = "Assets/CrestPortDemos/Ocean_Calm.unity";
    string _folder, _folderGuid, _report;
    readonly string[] _materials = { "Flow", "Albedo", "Height" };
    string[] _materialGuids;

    [UnityTest]
    public IEnumerator SavedSceneRestoresInputFieldsReferencesAndRegistration()
    {
        OceanRenderer.EnableGraphicsInBatchMode = true;
        var scene = EditorSceneManager.OpenScene(BaseScene);
        string name = "InputRoundTrip_" + Guid.NewGuid().ToString("N");
        _folderGuid = AssetDatabase.CreateFolder(Parent, name);
        _folder = Parent + "/" + name;
        var ocean = Object.FindFirstObjectByType<OceanRenderer>();
        Assert.That(ocean, Is.Not.Null);
        Camera.main.transform.position = new Vector3(0, 10, 0);
        foreach (var fft in Object.FindObjectsByType<ShapeFFT>()) fft.enabled = false;
        var surface = new Material(ocean.OceanMaterial);
        foreach (string keyword in new[] { "_FLOW_ON", "_CLIPSURFACE_ON", "_ALBEDO_ON" }) surface.EnableKeyword(keyword);
        surface.SetFloat("_Flow", 1);
        surface.SetFloat("_ClipSurface", 1);
        surface.SetFloat("_Albedo", 1);
        AssetDatabase.CreateAsset(surface, _folder + "/Surface.mat");
        ocean.OceanMaterial = surface;
        var depth = ScriptableObject.CreateInstance<SimSettingsSeaFloorDepth>();
        depth._allowVaryingWaterLevel = true;
        AssetDatabase.CreateAsset(depth, _folder + "/Depth.asset");
        ocean._simSettingsSeaFloorDepth = depth;
        var clock = ocean.gameObject.AddComponent<TimeProviderCustom>();
        clock._overrideTime = clock._overrideDeltaTime = true;
        clock._time = .5f;
        clock._deltaTime = 0;
        var settings = new SerializedObject(ocean);
        foreach (string field in new[] { "_createFlowSim", "_createClipSurfaceData", "_createAlbedoData" })
            settings.FindProperty(field).boolValue = true;
        settings.FindProperty("_timeProvider").objectReferenceValue = clock;
        settings.ApplyModifiedPropertiesWithoutUndo();
        var flow = CreatePlane("Flow", "Crest/Inputs/Flow/Fixed Direction", 0);
        flow.GetComponent<Renderer>().sharedMaterial.SetFloat("_Speed", 3);
        flow.GetComponent<Renderer>().sharedMaterial.SetFloat("_Direction", 0);
        flow.AddComponent<RegisterFlowInput>();
        var albedo = CreatePlane("Albedo", "Crest/Inputs/Albedo/Color", 0);
        albedo.GetComponent<Renderer>().sharedMaterial.SetColor("_Color", new Color(1, 0, 0, .75f));
        albedo.AddComponent<RegisterAlbedoInput>();
        CreatePlane("Height", "Crest/Inputs/Sea Floor Depth/Set Base Water Height Using Geometry", 2).AddComponent<RegisterHeightInput>();
        var clip = new GameObject("Persisted Clip");
        clip.transform.localScale = new Vector3(8, 4, 8);
        clip.AddComponent<RegisterClipSurfaceInput>();
        _materialGuids = new string[_materials.Length];
        for (int i = 0; i < _materials.Length; i++)
            _materialGuids[i] = AssetDatabase.AssetPathToGUID(_folder + "/" + _materials[i] + ".mat");
        string scenePath = _folder + "/Inputs.unity";
        AssetDatabase.SaveAssets();
        Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
        string sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
        _report = $"Evidence/InputPersistence-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report, "Saved scene with material/profile/time-provider references; actual scene reload; original input shaders. Vector tolerance=0.01, clip=0.005, RGBA=2/255, height=0.01m.\n");
        yield return new EnterPlayMode();
        yield return WaitForOceanUpdates(6);
        yield return CheckFields("BeforeReload", true);
        var oldOcean = OceanRenderer.Instance;
        var oldTargets = new[] { oldOcean._lodDataFlow.DataTexture, oldOcean._lodDataClipSurface.DataTexture,
            oldOcean._lodDataAlbedo.DataTexture, oldOcean._lodDataSeaDepths.DataTexture };
        var oldHeight = GameObject.Find("Persisted Height").GetComponent<RegisterHeightInput>();
        foreach (string asset in new[] { "Flow.mat", "Albedo.mat", "Height.mat", "Surface.mat", "Depth.asset", "Inputs.unity" })
            AssetDatabase.ImportAsset(_folder + "/" + asset, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));
        yield return WaitForOceanUpdates(6);
        Assert.That(oldOcean == null, Is.True);
        Assert.That(oldHeight == null, Is.True);
        foreach (var target in oldTargets) Assert.That(target == null, Is.True, "Reload must destroy the previous simulation target.");
        Assert.That(AssetDatabase.AssetPathToGUID(scenePath), Is.EqualTo(sceneGuid));
        Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(scenePath));
        yield return CheckFields("AfterReload", true);
        SetInputsEnabled(false);
        yield return WaitForOceanUpdates(4);
        yield return CheckFields("DisabledAfterReload", false);
        SetInputsEnabled(true);
        yield return WaitForOceanUpdates(4);
        yield return CheckFields("ReenabledAfterReload", true);
        File.AppendAllText(_report, "OldOceanDestroyed=True; OldHeightInputDestroyed=True; FourOldRTsReleased=True; SceneGUIDPreserved=True\n");
    }

    GameObject CreatePlane(string label, string shaderName, float height)
    {
        var owner = GameObject.CreatePrimitive(PrimitiveType.Plane);
        owner.name = "Persisted " + label;
        owner.transform.localScale = Vector3.one * .8f;
        owner.transform.position = Vector3.up * height;
        var shader = Shader.Find(shaderName);
        Assert.That(shader, Is.Not.Null);
        var material = new Material(shader);
        AssetDatabase.CreateAsset(material, _folder + "/" + label + ".mat");
        owner.GetComponent<Renderer>().sharedMaterial = material;
        return owner;
    }

    static void SetInputsEnabled(bool enabled)
    {
        GameObject.Find("Persisted Flow").GetComponent<RegisterFlowInput>().enabled = enabled;
        GameObject.Find("Persisted Albedo").GetComponent<RegisterAlbedoInput>().enabled = enabled;
        GameObject.Find("Persisted Height").GetComponent<RegisterHeightInput>().enabled = enabled;
        GameObject.Find("Persisted Clip").GetComponent<RegisterClipSurfaceInput>().enabled = enabled;
    }

    IEnumerator CheckFields(string phase, bool enabled)
    {
        var ocean = OceanRenderer.Instance;
        Assert.That(Camera.main.transform.position, Is.EqualTo(new Vector3(0, 10, 0)));
        Assert.That(ocean.TimeProvider, Is.SameAs(ocean.GetComponent<TimeProviderCustom>()));
        Assert.That(ocean.CurrentTime, Is.EqualTo(.5f));
        Assert.That(ocean.DeltaTime, Is.Zero);
        Assert.That(AssetDatabase.GetAssetPath(ocean._simSettingsSeaFloorDepth), Is.EqualTo(_folder + "/Depth.asset"));
        Assert.That(ocean._simSettingsSeaFloorDepth._allowVaryingWaterLevel, Is.True);
        Assert.That(AssetDatabase.GetAssetPath(ocean.OceanMaterial), Is.EqualTo(_folder + "/Surface.mat"));
        for (int i = 0; i < _materials.Length; i++)
        {
            var material = GameObject.Find("Persisted " + _materials[i]).GetComponent<Renderer>().sharedMaterial;
            Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(material)), Is.EqualTo(_materialGuids[i]));
        }
        Assert.That(GameObject.Find("Persisted Flow").GetComponent<Renderer>().sharedMaterial.GetFloat("_Speed"), Is.EqualTo(3));
        Assert.That(GameObject.Find("Persisted Flow").GetComponent<Renderer>().sharedMaterial.GetFloat("_Direction"), Is.Zero);
        Assert.That(GameObject.Find("Persisted Albedo").GetComponent<Renderer>().sharedMaterial.GetColor("_Color"), Is.EqualTo(new Color(1, 0, 0, .75f)));
        Assert.That(OceanChunkRenderer.HeightReporters.Contains(GameObject.Find("Persisted Height").GetComponent<RegisterHeightInput>()), Is.EqualTo(enabled));
        File.AppendAllText(_report, $"Phase={phase}; MaterialGUIDsAndProfileReferencesPreserved=True; SerializedClock=0.5/0; HeightReporterRegistered={enabled}\n");
        yield return CheckVector(ocean._lodDataFlow.DataTexture, phase + "/Flow", enabled ? new Vector2(3, 0) : Vector2.zero, false);
        yield return CheckVector(ocean._lodDataSeaDepths.DataTexture, phase + "/Height", enabled ? new Vector2(0, 2) : Vector2.zero, true);
        var target = ocean._lodDataClipSurface.DataTexture;
        int center = WorldIndex(target, Vector2.zero), outside = WorldIndex(target, new Vector2(10, 0));
        var clip = AsyncGPUReadback.Request(target, 0, TextureFormat.RFloat);
        while (!clip.done) yield return null;
        Assert.That(clip.hasError, Is.False);
        var clipValues = clip.GetData<float>(0);
        // Original cube SDF samples the displaced water height (2m here) and texel-center XZ.
        float texel = 4 * ocean.CalcLodScale(0) / target.width;
        var snapped = ocean._lodTransform._renderData[0].Current._posSnapped;
        float x = (center % target.width + .5f - target.width * .5f) * texel + snapped.x;
        float z = (center / target.width + .5f - target.height * .5f) * texel + snapped.z;
        float expectedClip = enabled ? Mathf.Clamp01(1 - Mathf.Max(Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) / 8, 2f / 4)) : 0;
        Assert.That(clipValues[center], Is.EqualTo(expectedClip).Within(.005f));
        Assert.That(clipValues[outside], Is.Zero);
        File.AppendAllText(_report, $"ClipCenter={clipValues[center]}; Expected={expectedClip}; Outside={clipValues[outside]}\n");
        if (!enabled) foreach (float value in clipValues) Assert.That(value, Is.Zero);
        target = ocean._lodDataAlbedo.DataTexture;
        center = WorldIndex(target, Vector2.zero); outside = WorldIndex(target, new Vector2(10, 0));
        var albedo = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBAFloat);
        while (!albedo.done) yield return null;
        Assert.That(albedo.hasError, Is.False);
        var colors = albedo.GetData<Color>(0);
        Color expectedColor = enabled ? new Color(.75f, 0, 0, .5625f) : Color.clear;
        for (int channel = 0; channel < 4; channel++)
        {
            Assert.That(colors[center][channel], Is.EqualTo(expectedColor[channel]).Within(2f / 255));
            Assert.That(colors[outside][channel], Is.Zero);
        }
        File.AppendAllText(_report, $"AlbedoCenter={colors[center]}; Expected={expectedColor}; Outside={colors[outside]}\n");
        if (!enabled) foreach (var color in colors) Assert.That(color, Is.EqualTo(Color.clear));
    }

    IEnumerator CheckVector(RenderTexture target, string phase, Vector2 expected, bool heightOnly)
    {
        int center = WorldIndex(target, Vector2.zero), outside = WorldIndex(target, new Vector2(10, 0));
        var request = AsyncGPUReadback.Request(target, 0, TextureFormat.RGFloat);
        while (!request.done) yield return null;
        Assert.That(request.hasError, Is.False);
        var values = request.GetData<Vector2>(0);
        Assert.That(values[center].y, Is.EqualTo(expected.y).Within(.01f));
        Assert.That(values[outside].y, Is.Zero);
        if (!heightOnly)
        {
            Assert.That(values[center].x, Is.EqualTo(expected.x).Within(.01f));
            Assert.That(values[outside].x, Is.Zero);
        }
        File.AppendAllText(_report, $"Phase={phase}; Center={values[center]}; Outside={values[outside]}\n");
        foreach (var value in values)
        {
            Assert.That(float.IsNaN(value.y) || float.IsInfinity(value.y), Is.False);
            if (!heightOnly) Assert.That(float.IsNaN(value.x) || float.IsInfinity(value.x), Is.False);
            if (expected == Vector2.zero)
            {
                Assert.That(value.y, Is.Zero);
                if (!heightOnly) Assert.That(value.x, Is.Zero);
            }
        }
    }

    static int WorldIndex(RenderTexture target, Vector2 position)
    {
        var ocean = OceanRenderer.Instance;
        var snapped = ocean._lodTransform._renderData[0].Current._posSnapped;
        float texel = 4 * ocean.CalcLodScale(0) / target.width;
        int x = Mathf.FloorToInt((position.x - snapped.x) / texel + target.width * .5f);
        int y = Mathf.FloorToInt((position.y - snapped.z) / texel + target.height * .5f);
        Assert.That(x, Is.InRange(0, target.width - 1));
        Assert.That(y, Is.InRange(0, target.height - 1));
        return y * target.width + x;
    }

    static IEnumerator WaitForOceanUpdates(int count)
    {
        int previous = BuildCommandBufferBase._lastUpdateFrame, completed = 0;
        float deadline = Time.realtimeSinceStartup + 15;
        while (completed < count && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            int frame = BuildCommandBufferBase._lastUpdateFrame;
            if (frame == previous) continue;
            previous = frame; completed++;
        }
        Assert.That(completed, Is.EqualTo(count));
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        OceanRenderer.EnableGraphicsInBatchMode = false;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (!string.IsNullOrEmpty(_folder))
        {
            Assert.That(Path.GetFullPath(_folder), Does.StartWith(Path.GetFullPath(Parent) + Path.DirectorySeparatorChar));
            Assert.That(AssetDatabase.AssetPathToGUID(_folder), Is.EqualTo(_folderGuid));
            Assert.That(AssetDatabase.DeleteAsset(_folder), Is.True);
            _folder = _folderGuid = null;
        }
    }
}
