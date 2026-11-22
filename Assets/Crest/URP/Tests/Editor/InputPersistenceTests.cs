using System;
using System.Collections;
using System.IO;
using System.Linq;
using Crest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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

    [UnityTest]
    public IEnumerator SavedWaterBodyRestoresHeightMaterialAndFogAtItsBoundary()
    {
        OceanRenderer.EnableGraphicsInBatchMode = true;
        var scene = EditorSceneManager.OpenScene(BaseScene);
        string name = "WaterBodyRoundTrip_" + Guid.NewGuid().ToString("N");
        _folderGuid = AssetDatabase.CreateFolder(Parent, name);
        _folder = Parent + "/" + name;
        var ocean = Object.FindFirstObjectByType<OceanRenderer>();
        foreach (var waves in Object.FindObjectsByType<ShapeWaves>()) waves.enabled = false;

        var baseMaterial = new Material(ocean.OceanMaterial);
        foreach (string keyword in new[] { "_FOAM_ON", "_TRANSPARENCY_ON", "_CAUSTICS_ON", "_CLIPSURFACE_ON" })
            baseMaterial.DisableKeyword(keyword);
        baseMaterial.EnableKeyword("_UNDERWATER_ON");
        baseMaterial.SetFloat("_Underwater", 1f);
        baseMaterial.SetFloat("_CullMode", (float)CullMode.Off);
        baseMaterial.SetVector("_DepthFogDensity", new Vector4(.01f, .01f, .01f, 1));
        AssetDatabase.CreateAsset(baseMaterial, _folder + "/BaseWater.mat");
        ocean.OceanMaterial = baseMaterial;
        ocean._waterBodyCulling = false;

        var bodyMaterial = new Material(baseMaterial);
        bodyMaterial.SetVector("_DepthFogDensity", new Vector4(.35f, .35f, .35f, 1));
        AssetDatabase.CreateAsset(bodyMaterial, _folder + "/BodyWater.mat");
        string bodyMaterialGuid = AssetDatabase.AssetPathToGUID(_folder + "/BodyWater.mat");
        var bodyObject = new GameObject("Persisted WaterBody");
        bodyObject.transform.localScale = new Vector3(16, 1, 16);
        bodyObject.AddComponent<WaterBody>()._overrideMaterial = bodyMaterial;

        var depth = ScriptableObject.CreateInstance<SimSettingsSeaFloorDepth>();
        depth._allowVaryingWaterLevel = true;
        AssetDatabase.CreateAsset(depth, _folder + "/Depth.asset");
        ocean._simSettingsSeaFloorDepth = depth;
        var clock = ocean.gameObject.AddComponent<TimeProviderCustom>();
        clock._overrideTime = clock._overrideDeltaTime = clock._paused = true;
        clock._time = 1;
        var settings = new SerializedObject(ocean);
        settings.FindProperty("_timeProvider").objectReferenceValue = clock;
        settings.FindProperty("_createFoamSim").boolValue = false;
        settings.FindProperty("_minScale").floatValue = 8;
        settings.FindProperty("_maxScale").floatValue = 8;
        settings.ApplyModifiedPropertiesWithoutUndo();
        var height = CreatePlane("BodyHeight", "Crest/Inputs/Sea Floor Depth/Set Base Water Height Using Geometry", 2);
        height.transform.localScale = Vector3.one * 1.6f;
        height.AddComponent<RegisterHeightInput>();

        var camera = Camera.main;
        camera.transform.SetPositionAndRotation(new Vector3(4, 12, 0), Quaternion.Euler(90, 0, 0));
        camera.allowHDR = camera.allowMSAA = false;
        var cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.requiresDepthTexture = true;
        cameraData.renderPostProcessing = false;
        var fog = camera.GetComponent<UnderwaterRenderer>();
        if (fog == null) fog = camera.gameObject.AddComponent<UnderwaterRenderer>();
        fog.EnableShaderAPI = true;

        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Opaque fog control";
        wall.transform.position = new Vector3(6, -3, 10);
        wall.transform.localScale = new Vector3(40, 4, 1);
        var wallMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        wallMaterial.SetColor("_BaseColor", new Color(.65f, .45f, .25f));
        AssetDatabase.CreateAsset(wallMaterial, _folder + "/Wall.mat");
        wall.GetComponent<Renderer>().sharedMaterial = wallMaterial;

        var scenePath = _folder + "/WaterBody.unity";
        AssetDatabase.SaveAssets();
        Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
        string sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
        _report = $"Evidence/P13-SavedWaterBody-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report, "Body x/z bounds ±8 m, local level +2 m, base fog density 0.01, body density 0.35. Camera crosses the XZ boundary while underwater.\n");

        yield return new EnterPlayMode();
        yield return WaitForOceanUpdates(6);
        var oldOcean = OceanRenderer.Instance;
        var oldBody = GameObject.Find("Persisted WaterBody").GetComponent<WaterBody>();
        var oldTarget = oldOcean._lodDataSeaDepths.DataTexture;
        var before = new Color32[3];
        yield return CheckSavedWaterBody("BeforeReload", bodyMaterialGuid, before);
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));
        yield return WaitForOceanUpdates(6);
        Assert.That(oldOcean == null && oldBody == null && oldTarget == null, Is.True);
        Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(scenePath));
        Assert.That(AssetDatabase.AssetPathToGUID(scenePath), Is.EqualTo(sceneGuid));
        var after = new Color32[3];
        yield return CheckSavedWaterBody("AfterReload", bodyMaterialGuid, after);
        for (int i = 0; i < before.Length; i++)
            Assert.That(ColorDistance(before[i], after[i]), Is.LessThanOrEqualTo(3), $"Reloaded control {i}");
        File.AppendAllText(_report, "Scene GUID preserved, old body and ocean destroyed, old depth target released, fog samples restored.\n");
    }

    IEnumerator CheckSavedWaterBody(string phase, string materialGuid, Color32[] fogSamples)
    {
        var ocean = OceanRenderer.Instance;
        var body = GameObject.Find("Persisted WaterBody").GetComponent<WaterBody>();
        var camera = Camera.main;
        var fog = camera.GetComponent<UnderwaterRenderer>();
        Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(body._overrideMaterial)), Is.EqualTo(materialGuid));
        Assert.That(WaterBody.WaterBodies.Count(entry => entry == body), Is.EqualTo(1));
        Assert.That(body.AABB.Contains(new Vector3(0, body.AABB.center.y, 0)), Is.True);
        Assert.That(body.AABB.Contains(new Vector3(12, body.AABB.center.y, 0)), Is.False);
        var depth = ocean._lodDataSeaDepths.DataTexture;
        var native = AsyncGPUReadback.Request(depth, 0, TextureFormat.RGFloat);
        while (!native.done) yield return null;
        Assert.That(native.hasError, Is.False);
        float insideHeight = native.GetData<Vector2>(0)[WorldIndex(depth, Vector2.zero)].y;
        float outsideHeight = native.GetData<Vector2>(0)[WorldIndex(depth, new Vector2(12, 0))].y;
        Assert.That(insideHeight, Is.EqualTo(2).Within(.01f));
        Assert.That(outsideHeight, Is.Zero);

        var target = new RenderTexture(320, 180, 24, RenderTextureFormat.ARGB32);
        var previousCallback = UnderwaterRenderer.AfterCopyMaterial;
        try
        {
            Assert.That(target.Create(), Is.True);
            camera.aspect = 320f / 180f;
            camera.orthographic = true;
            camera.orthographicSize = 9;
            fog.enabled = false;
            CaptureWaterBody(camera, target, null);
            var tiles = ocean.Root.GetComponentsInChildren<OceanChunkRenderer>();
            int overrides = tiles.Count(tile => tile.Rend.enabled && tile.Rend.sharedMaterial == body._overrideMaterial);
            int defaults = tiles.Count(tile => tile.Rend.enabled && tile.Rend.sharedMaterial == ocean.OceanMaterial);
            File.AppendAllText(_report, $"{phase} height={insideHeight}/{outsideHeight}; tile materials={overrides} body, {defaults} default\n");
            Assert.That(overrides, Is.GreaterThan(0));
            Assert.That(defaults, Is.GreaterThan(0));

            camera.orthographic = false;
            int[] fogChanges = new int[3];
            for (int i = 0; i < fogSamples.Length; i++)
            {
                bool inside = i != 1;
                camera.transform.SetPositionAndRotation(new Vector3(inside ? 0 : 12, -3, 0), Quaternion.identity);
                fog.enabled = false;
                yield return WaitForOceanUpdates(3);
                var control = CaptureWaterBody(camera, target, null);

                Material selected = null;
                UnderwaterRenderer.AfterCopyMaterial = material => selected = material;
                fog.enabled = true;
                yield return WaitForOceanUpdates(3);
                var file = phase == "AfterReload" && i < 2
                    ? $"Evidence/P13-SavedWaterBody-{SystemInfo.graphicsDeviceType}-{(inside ? "Inside" : "Outside")}.png" : null;
                fogSamples[i] = CaptureWaterBody(camera, target, file);
                var expectedMaterial = inside ? body._overrideMaterial : ocean.OceanMaterial;
                Assert.That(selected, Is.SameAs(expectedMaterial), $"{phase} view {i}");
                Vector4 selectedDensity = Shader.GetGlobalVector("_CrestDepthFogDensity");
                float expectedDensity = inside ? .35f : .01f;
                Assert.That(selectedDensity.x, Is.EqualTo(expectedDensity).Within(.0001f), $"{phase} view {i}");
                Assert.That(control.r + control.g + control.b, Is.GreaterThan(50));
                fogChanges[i] = ColorDistance(control, fogSamples[i]);
                File.AppendAllText(_report, $"{phase} view={i}; density={selectedDensity.x:F3}; fog RGB delta={fogChanges[i]}; control={control}; output={fogSamples[i]}\n");
            }
            Assert.That(fogChanges[0], Is.GreaterThan(fogChanges[1] + 30), phase);
            Assert.That(fogChanges[1], Is.GreaterThan(0), phase);
            Assert.That(ColorDistance(fogSamples[0], fogSamples[2]), Is.LessThanOrEqualTo(3), phase);
        }
        finally
        {
            UnderwaterRenderer.AfterCopyMaterial = previousCallback;
            target.Release();
            Object.Destroy(target);
        }
    }

    static Color32 CaptureWaterBody(Camera camera, RenderTexture target, string png)
    {
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        var active = RenderTexture.active;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            if (png != null) File.WriteAllBytes(png, image.EncodeToPNG());
            return image.GetPixel(target.width / 2, target.height / 2);
        }
        finally
        {
            RenderTexture.active = active;
            Object.Destroy(image);
        }
    }

    static int ColorDistance(Color32 first, Color32 second)
        => Mathf.Abs(first.r - second.r) + Mathf.Abs(first.g - second.g) + Mathf.Abs(first.b - second.b);

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
