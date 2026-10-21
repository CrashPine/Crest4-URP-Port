using System.Collections;
using System.IO;
using System.Collections.Generic;
using Crest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

public class PlanarReflectionTests
{
    Material _material;
    readonly List<Material> _extraMaterials = new List<Material>();

    [UnityTest]
    public IEnumerator ReflectionDistanceCullsWorldCanvasAndRestoresTerrainFlags()
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Reflections.unity");
        yield return new EnterPlayMode();
        OceanRenderer.EnableGraphicsInBatchMode = true;
        var reflection = ConfigureCamera().GetComponent<OceanPlanarReflection>();
        GameObject.Find("Reflection reference").GetComponent<Renderer>().enabled = false;
        var canvas = new GameObject("World reflection markers", typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var near = CanvasMarker(canvas, new Vector3(-2, 2, 0), Color.green);
        var far = CanvasMarker(canvas, new Vector3(13, 2, 3), Color.magenta);
        var terrainData = new TerrainData { heightmapResolution = 33, size = new Vector3(4, 1, 4) };
        var terrainOwner = Terrain.CreateTerrainGameObject(terrainData);
        terrainOwner.transform.position = new Vector3(100, 2, 0);
        var terrain = terrainOwner.GetComponent<Terrain>();
        terrain.drawHeightmap = true;
        terrain.drawTreesAndFoliage = false;
        try
        {
            reflection.ReflectionDistance = 100;
            for (int i = 0; i < 8; i++) yield return null;
            Canvas.ForceUpdateCanvases();
            reflection.RefreshReflection();
            int before = Count(Read(reflection, "Canvas-100"), false);
            Assert.That(before, Is.GreaterThan(30), "Far world-space UI marker must initially be visible.");
            reflection.ReflectionDistance = 20;
            bool farCulled = false, nearKept = false, terrainCulled = false;
            System.Action<ScriptableRenderContext, Camera> callback = (context, camera) =>
            {
                if (camera != reflection.ReflectionCamera) return;
                farCulled = far.canvasRenderer.cull;
                nearKept = !near.canvasRenderer.cull;
                terrainCulled = !terrain.drawHeightmap && !terrain.drawTreesAndFoliage;
            };
            RenderPipelineManager.beginCameraRendering += callback;
            try { reflection.RefreshReflection(); }
            finally { RenderPipelineManager.beginCameraRendering -= callback; }
            var after = Read(reflection, "Canvas-20");
            int remaining = Count(after, false), green = Count(after, true);
            Assert.That(farCulled && nearKept && terrainCulled, Is.True);
            Assert.That(remaining, Is.LessThan(5));
            Assert.That(green, Is.GreaterThan(30));
            Assert.That(far.canvasRenderer.cull, Is.False);
            Assert.That(terrain.drawHeightmap, Is.True);
            Assert.That(terrain.drawTreesAndFoliage, Is.False);
            File.WriteAllText($"Evidence/ReflectionCanvasTerrain-{SystemInfo.graphicsDeviceType}.txt",
                $"FarCanvasBefore={before}; FarCanvasAfter={remaining}; NearCanvasGreen={green}; TerrainCulledInCallback={terrainCulled}; OriginalHeightmap=True; OriginalTrees=False; FlagsRestored=True\n");
        }
        finally { Object.Destroy(terrainOwner); Object.Destroy(terrainData); }
    }

    static UnityEngine.UI.RawImage CanvasMarker(Canvas canvas, Vector3 position, Color color)
    {
        var image = new GameObject("Canvas marker", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.RawImage))
            .GetComponent<UnityEngine.UI.RawImage>();
        image.transform.SetParent(canvas.transform, false);
        image.transform.position = position;
        image.rectTransform.sizeDelta = Vector2.one * 2;
        image.color = color;
        return image;
    }

    [UnityTest]
    public IEnumerator ReflectionSelectsForwardOrInheritsViewpointRenderer()
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Reflections.unity");
        yield return new EnterPlayMode();
        OceanRenderer.EnableGraphicsInBatchMode = true;
        var camera = ConfigureCamera();
        var reflection = camera.GetComponent<OceanPlanarReflection>();
        GameObject.Find("Reflection reference").GetComponent<Renderer>().enabled = false;
        ColoredCube("Renderer mode reference", new Vector3(0, 2, 0), Color.magenta);
        var original = QualitySettings.renderPipeline;
        var primary = (UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
        var forward = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>("Assets/CrestPortDemos/Renderer.asset");
        var clustered = Object.Instantiate(forward);
        clustered.renderingMode = UnityEngine.Rendering.Universal.RenderingMode.ForwardPlus;
        var deferred = Object.Instantiate(forward);
        deferred.renderingMode = UnityEngine.Rendering.Universal.RenderingMode.Deferred;
        var pipeline = Object.Instantiate(primary);
        var serialized = new SerializedObject(pipeline);
        var renderers = serialized.FindProperty("m_RendererDataList");
        renderers.arraySize = 3;
        renderers.GetArrayElementAtIndex(0).objectReferenceValue = forward;
        renderers.GetArrayElementAtIndex(1).objectReferenceValue = clustered;
        renderers.GetArrayElementAtIndex(2).objectReferenceValue = deferred;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var viewData = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(camera);
        try
        {
            QualitySettings.renderPipeline = pipeline;
            viewData.SetRenderer(1);
            for (int i = 0; i < 4; i++) yield return null;
            reflection.RefreshReflection();
            var reflectionData = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(reflection.ReflectionCamera);
            Assert.That(viewData.scriptableRenderer, Is.SameAs(pipeline.GetRenderer(1)));
            Assert.That(reflectionData.scriptableRenderer, Is.SameAs(pipeline.GetRenderer(0)), "Force-forward must select a Forward renderer, not Camera.renderingPath.");
            int forcedPixels = Count(Read(reflection, "Renderer-forced-Forward"), false);
            Assert.That(forcedPixels, Is.GreaterThan(30));
            SetOption(reflection, "_forceForwardRenderingPath", false);
            reflection.DisablePixelLights = false;
            reflection.RefreshReflection();
            Assert.That(reflectionData.scriptableRenderer, Is.SameAs(pipeline.GetRenderer(1)), "Per-pixel reflection inherits Forward+ from the viewpoint.");
            int clusteredPixels = Count(Read(reflection, "Renderer-inherited-ForwardPlus"), false);
            Assert.That(clusteredPixels, Is.GreaterThan(30));
            viewData.SetRenderer(2);
            reflection.RefreshReflection();
            Assert.That(reflectionData.scriptableRenderer, Is.SameAs(pipeline.GetRenderer(2)), "Renderer inheritance must include a non-default Deferred renderer.");
            int deferredPixels = Count(Read(reflection, "Renderer-inherited-Deferred"), false);
            Assert.That(deferredPixels, Is.GreaterThan(30));
            reflection.DisablePixelLights = true;
            reflection.RefreshReflection();
            Assert.That(reflectionData.scriptableRenderer, Is.SameAs(pipeline.GetRenderer(0)), "Vertex lights require Forward even when the viewpoint uses Deferred.");
            int vertexPixels = Count(Read(reflection, "Renderer-vertex-Forward"), false);
            Assert.That(vertexPixels, Is.GreaterThan(30));
            Assert.That(viewData.scriptableRenderer, Is.SameAs(pipeline.GetRenderer(2)));
            File.WriteAllText($"Evidence/ReflectionRenderer-{SystemInfo.graphicsDeviceType}.txt",
                $"Forward+ viewpoint -> forced Forward reflection; per-pixel inherits Forward+ and Deferred; vertex option routes to Forward; viewpoint renderer unchanged. Magenta pixels: forced={forcedPixels}, ForwardPlus={clusteredPixels}, Deferred={deferredPixels}, vertexForward={vertexPixels}. Deferred reflection rendering may fall back internally according to URP camera support.\n");
        }
        finally
        {
            viewData.SetRenderer(-1);
            QualitySettings.renderPipeline = original;
            Object.Destroy(pipeline);
            Object.Destroy(clustered);
            Object.Destroy(deferred);
        }
    }

    [UnityTest]
    public IEnumerator ReflectionShadowOptionChangesReceiverWithoutChangingMainView()
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Reflections.unity");
        yield return new EnterPlayMode();
        OceanRenderer.EnableGraphicsInBatchMode = true;
        var camera = ConfigureCamera();
        var reflection = camera.GetComponent<OceanPlanarReflection>();
        GameObject.Find("Reflection reference").GetComponent<Renderer>().enabled = false;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.reflectionIntensity = 0;
        var sun = RenderSettings.sun;
        Assert.That(sun, Is.Not.Null);
        sun.transform.rotation = Quaternion.LookRotation(new Vector3(0, -.5f, 1));
        sun.intensity = 1;
        sun.color = Color.white;
        sun.shadows = LightShadows.Hard;
        sun.shadowBias = 0;
        sun.shadowNormalBias = 0;
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", Color.gray);
        material.SetFloat("_Smoothness", 0);
        _extraMaterials.Add(material);
        var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
        panel.transform.position = new Vector3(0, 2, 0);
        panel.transform.localScale = new Vector3(10, 8, 1);
        panel.GetComponent<Renderer>().sharedMaterial = material;
        var caster = GameObject.CreatePrimitive(PrimitiveType.Cube);
        caster.transform.position = new Vector3(0, 3, -2);
        caster.transform.localScale = Vector3.one * 2;
        caster.GetComponent<Renderer>().sharedMaterial = material;
        SetOption(reflection, "_disableShadows", false);
        for (int i = 0; i < 8; i++) yield return null;
        float shadow = MeanAtPanel(Read(reflection, "Shadows-on"), reflection.ReflectionCamera, panel.transform.position);
        float mainBefore = ReadPrimaryMean(camera, panel.transform.position, "Main-shadow-before");
        SetOption(reflection, "_disableShadows", true);
        for (int i = 0; i < 8; i++) yield return null;
        float unshadowed = MeanAtPanel(Read(reflection, "Shadows-off"), reflection.ReflectionCamera, panel.transform.position);
        float mainAfter = ReadPrimaryMean(camera, panel.transform.position, "Main-shadow-after");
        File.WriteAllText($"Evidence/ReflectionShadows-{SystemInfo.graphicsDeviceType}.txt",
            $"ShadowMean={shadow}; UnshadowedMean={unshadowed}; MainBefore={mainBefore}; MainAfter={mainAfter}\n");
        Assert.That(unshadowed, Is.GreaterThan(20));
        Assert.That(shadow, Is.LessThan(unshadowed * .5f), "Known caster must darken the reflected panel.");
        Assert.That(mainAfter, Is.EqualTo(mainBefore).Within(.1f));
        Assert.That(UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(camera).renderShadows, Is.True);
    }

    [UnityTest]
    public IEnumerator ReflectionRestoresStateAfterCameraCallbackException()
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Reflections.unity");
        yield return new EnterPlayMode();
        OceanRenderer.EnableGraphicsInBatchMode = true;
        var reflection = ConfigureCamera().GetComponent<OceanPlanarReflection>();
        reflection.ReflectionDistance = 30;
        ColoredCube("Exception far marker", new Vector3(100, 2, 0), Color.white);
        var far = GameObject.Find("Exception far marker").GetComponent<Renderer>();
        ColoredCube("Persistent far marker", new Vector3(100, 3, 0), Color.white);
        var persistent = GameObject.Find("Persistent far marker");
        var persistentRenderer = persistent.GetComponent<Renderer>();
        Object.DontDestroyOnLoad(persistent);
        for (int i = 0; i < 8; i++) yield return null;
        bool originalInvert = GL.invertCulling;
        int originalPixelLights = QualitySettings.pixelLightCount;
        var originalShadows = QualitySettings.shadows;
        int callbacks = 0;
        bool culled = false, persistentCulled = false, rootHidden = false;
        const string message = "Crest fixture: injected reflection callback failure";
        System.Action<ScriptableRenderContext, Camera> callback = (context, camera) =>
        {
            if (camera != reflection.ReflectionCamera) return;
            callbacks++;
            culled = far.forceRenderingOff;
            persistentCulled = persistentRenderer.forceRenderingOff;
            rootHidden = !OceanRenderer.Instance.Root.gameObject.activeSelf;
            throw new System.InvalidOperationException(message);
        };
        bool propagated = false;
        RenderPipelineManager.beginCameraRendering += callback;
        try
        {
            try { reflection.RefreshReflection(); }
            catch (System.InvalidOperationException exception) when (exception.Message == message) { propagated = true; }
        }
        finally { RenderPipelineManager.beginCameraRendering -= callback; }
        // Native render-request boundaries may log the managed exception instead of propagating it.
        if (!propagated) LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex(message));
        try
        {
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(culled && persistentCulled && rootHidden, Is.True, "Failure must occur inside the mutated reflection state.");
            Assert.That(far.forceRenderingOff, Is.False);
            Assert.That(persistentRenderer.forceRenderingOff, Is.False);
            Assert.That(OceanRenderer.Instance.Root.gameObject.activeSelf, Is.True);
            Assert.That(GL.invertCulling, Is.EqualTo(originalInvert));
            Assert.That(QualitySettings.pixelLightCount, Is.EqualTo(originalPixelLights));
            Assert.That(QualitySettings.shadows, Is.EqualTo(originalShadows));
            Assert.That(OceanPlanarReflection.UsesVertexLightingForUrpCamera(reflection.ReflectionCamera), Is.False);
            int recoveryCallbacks = 0;
            System.Action<ScriptableRenderContext, Camera> recovery = (context, camera) =>
            { if (camera == reflection.ReflectionCamera) recoveryCallbacks++; };
            RenderPipelineManager.beginCameraRendering += recovery;
            try { reflection.RefreshReflection(); }
            finally { RenderPipelineManager.beginCameraRendering -= recovery; }
            Assert.That(recoveryCallbacks, Is.EqualTo(1), "An exception must not leave the recursion guard locked.");
            File.WriteAllText($"Evidence/ReflectionException-{SystemInfo.graphicsDeviceType}.txt",
                $"InjectedCallbacks={callbacks}; Propagated={propagated}; SceneRendererRestored=True; PersistentRendererRestored=True; RootRestored=True; GLRestored=True; RecoveryCallbacks={recoveryCallbacks}\n");
        }
        finally { Object.Destroy(persistent); }
    }

    [UnityTest]
    public IEnumerator ReflectionTargetTracksFormatSamplesAndSizeWithoutRepeatedAllocation()
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Reflections.unity");
        yield return new EnterPlayMode();
        OceanRenderer.EnableGraphicsInBatchMode = true;
        var reflection = ConfigureCamera().GetComponent<OceanPlanarReflection>();
        var pipeline = (UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
        int previousSamples = pipeline.msaaSampleCount;
        for (int i = 0; i < 8; i++) yield return null;
        try
        {
            SetOption(reflection, "_hdr", false);
            SetOption(reflection, "_stencil", false);
            SetOption(reflection, "_allowMSAA", false);
            SetOption(reflection, "_textureSize", 128);
            reflection.RefreshReflection();
            var first = reflection.ReflectionCamera.targetTexture;
            Assert.That(first.width, Is.EqualTo(128));
            Assert.That(first.format, Is.EqualTo(RenderTextureFormat.ARGB32));
            Assert.That(first.antiAliasing, Is.EqualTo(1));
            reflection.RefreshReflection();
            Assert.That(reflection.ReflectionCamera.targetTexture, Is.SameAs(first));
            SetOption(reflection, "_hdr", true);
            SetOption(reflection, "_stencil", true);
            reflection.RefreshReflection();
            var hdr = reflection.ReflectionCamera.targetTexture;
            Assert.That(hdr, Is.Not.SameAs(first));
            Assert.That(hdr.format, Is.EqualTo(RenderTextureFormat.ARGBHalf));
            Assert.That(hdr.depth, Is.GreaterThanOrEqualTo(24));
            pipeline.msaaSampleCount = 4;
            SetOption(reflection, "_allowMSAA", true);
            SetOption(reflection, "_textureSize", 256);
            reflection.RefreshReflection();
            var multisampled = reflection.ReflectionCamera.targetTexture;
            var descriptor = new RenderTextureDescriptor(256, 256, RenderTextureFormat.ARGBHalf, 24) { msaaSamples = 4 };
            int supported = SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor);
            Assert.That(supported, Is.GreaterThan(1), "This GPU fixture must actually exercise MSAA.");
            Assert.That(multisampled.width, Is.EqualTo(256));
            Assert.That(multisampled.antiAliasing, Is.EqualTo(supported));
            reflection.RefreshReflection();
            Assert.That(reflection.ReflectionCamera.targetTexture, Is.SameAs(multisampled), "Stable descriptors must reuse the target.");
            Assert.That(reflection.ReflectionCamera.allowHDR, Is.True);
            Assert.That(reflection.ReflectionCamera.allowMSAA, Is.True);
            ReadPrimaryMean(Camera.main, new Vector3(0, 2, 0), "Resized-main");
            int boundChunks = 0;
            var block = new MaterialPropertyBlock();
            foreach (var chunk in OceanRenderer.Instance.Root.GetComponentsInChildren<OceanChunkRenderer>())
            {
                if (chunk.Rend == null) continue;
                chunk.Rend.GetPropertyBlock(block);
                var bound = block.GetTexture("_ReflectionTex");
                if (bound == null || bound == Texture2D.blackTexture) continue;
                Assert.That(bound, Is.SameAs(multisampled), "Surface draw must bind the resized target, not the cached released texture.");
                boundChunks++;
            }
            Assert.That(boundChunks, Is.GreaterThan(0));
            File.WriteAllText($"Evidence/ReflectionTarget-{SystemInfo.graphicsDeviceType}.txt",
                $"LDR=128/ARGB32/1; HDR=ARGBHalf/stencil; resized=256; MSAA={supported}; stable target reused; SurfaceChunksBound={boundChunks}\n");
        }
        finally { pipeline.msaaSampleCount = previousSamples; }
    }

    [UnityTest]
    public IEnumerator ReflectionClipsSubmergedObjectsAndRestoresCullingAfterRecursiveCallback()
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Reflections.unity");
        yield return new EnterPlayMode();
        OceanRenderer.EnableGraphicsInBatchMode = true;
        var reflection = ConfigureCamera().GetComponent<OceanPlanarReflection>();
        GameObject.Find("Reflection reference").GetComponent<Renderer>().enabled = false;
        ColoredCube("Above plane", new Vector3(-2, 2, 0), Color.green);
        ColoredCube("Below plane", new Vector3(2, -3, 0), Color.magenta);
        ColoredCube("Far reference", new Vector3(100, 2, 0), Color.white);
        var far = GameObject.Find("Far reference").GetComponent<Renderer>();
        ColoredCube("Already hidden", new Vector3(100, 3, 0), Color.white);
        var hidden = GameObject.Find("Already hidden").GetComponent<Renderer>();
        hidden.forceRenderingOff = true;
        reflection.ReflectionDistance = 30;
        for (int i = 0; i < 8; i++) yield return null;
        int callbacks = 0;
        bool wasCulled = false;
        bool originalInvert = GL.invertCulling;
        System.Action<ScriptableRenderContext, Camera> callback = (context, camera) =>
        {
            if (camera != reflection.ReflectionCamera) return;
            callbacks++;
            wasCulled = far.forceRenderingOff;
            reflection.RefreshReflection();
        };
        RenderPipelineManager.beginCameraRendering += callback;
        try { reflection.RefreshReflection(); }
        finally { RenderPipelineManager.beginCameraRendering -= callback; }
        var pixels = Read(reflection, "Clipping");
        Assert.That(Count(pixels, true), Is.GreaterThan(30));
        Assert.That(Count(pixels, false), Is.LessThan(5), "Objects below the reflection plane must be clipped.");
        Assert.That(callbacks, Is.EqualTo(1), "Recursive refresh must not submit another camera request.");
        Assert.That(wasCulled, Is.True);
        Assert.That(far.forceRenderingOff, Is.False);
        Assert.That(hidden.forceRenderingOff, Is.True);
        Assert.That(GL.invertCulling, Is.EqualTo(originalInvert));
        Assert.That(OceanRenderer.Instance.Root.gameObject.activeSelf, Is.True);
        Assert.That(OceanPlanarReflection.UsesVertexLightingForUrpCamera(reflection.ReflectionCamera), Is.False);
    }

    [UnityTest]
    public IEnumerator ReflectionDistanceCullsOffAxisObjectsAndPreservesViewCamera()
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Reflections.unity");
        yield return new EnterPlayMode();
        OceanRenderer.EnableGraphicsInBatchMode = true;
        var camera = ConfigureCamera();
        var reflection = camera.GetComponent<OceanPlanarReflection>();
        SetOption(reflection, "_farClipPlane", 100f);
        var reference = GameObject.Find("Reflection reference");
        reference.GetComponent<Renderer>().enabled = false;
        ColoredCube("Near green reference", new Vector3(-2, 2, 0), Color.green);
        ColoredCube("Off-axis far magenta reference", new Vector3(13, 2, 3), Color.magenta);
        float viewFar = camera.farClipPlane;
        var viewProjection = camera.projectionMatrix;
        for (int i = 0; i < 8; i++) yield return null;
        var unrestricted = Read(reflection, "Distance-100");
        int initialMagenta = Count(unrestricted, false);
        Assert.That(initialMagenta, Is.GreaterThan(30), "The distant marker must first be visible.");
        SetOption(reflection, "_farClipPlane", 20f);
        for (int i = 0; i < 8; i++) yield return null;
        var restricted = Read(reflection, "Distance-20");
        int remainingMagenta = Count(restricted, false);
        int nearGreen = Count(restricted, true);
        File.WriteAllText($"Evidence/ReflectionDistance-{SystemInfo.graphicsDeviceType}.txt",
            $"UnrestrictedMagenta={initialMagenta}; RestrictedMagenta={remainingMagenta}; NearGreen={nearGreen}\n");
        Assert.That(nearGreen, Is.GreaterThan(30));
        Assert.That(remainingMagenta, Is.LessThan(5), "Spherical distance must cull an off-axis object beyond 20 m.");
        Assert.That(camera.farClipPlane, Is.EqualTo(viewFar));
        Assert.That(camera.projectionMatrix, Is.EqualTo(viewProjection));
    }

    [UnityTest]
    public IEnumerator PixelLightOptionChangesReflectionWithoutChangingPipelineAsset()
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Reflections.unity");
        yield return new EnterPlayMode();
        OceanRenderer.EnableGraphicsInBatchMode = true;
        var camera = ConfigureCamera();
        var reflection = camera.GetComponent<OceanPlanarReflection>();
        GameObject.Find("Reflection reference").GetComponent<Renderer>().enabled = false;
        if (RenderSettings.sun != null) RenderSettings.sun.enabled = false;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.reflectionIntensity = 0;
        var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
        panel.transform.position = new Vector3(0, 2, 0);
        panel.transform.localScale = new Vector3(10, 8, 1);
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", Color.gray);
        material.SetFloat("_Smoothness", 0);
        panel.GetComponent<Renderer>().sharedMaterial = material;
        _extraMaterials.Add(material);
        var light = new GameObject("Reflection point light").AddComponent<Light>();
        light.type = LightType.Point;
        light.transform.position = new Vector3(0, 2, -2);
        light.range = 4;
        light.intensity = 15;
        var pipeline = (UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
        var mode = pipeline.additionalLightsRenderingMode;
        SetOption(reflection, "_disablePixelLights", false);
        for (int i = 0; i < 8; i++) yield return null;
        float pixelMean = MeanAtPanel(Read(reflection, "Pixel-lights"), reflection.ReflectionCamera, panel.transform.position);
        float mainBefore = ReadPrimaryMean(camera, panel.transform.position, "Main-pixel-before");
        SetOption(reflection, "_disablePixelLights", true);
        for (int i = 0; i < 8; i++) yield return null;
        float vertexMean = MeanAtPanel(Read(reflection, "Vertex-lights"), reflection.ReflectionCamera, panel.transform.position);
        float mainAfter = ReadPrimaryMean(camera, panel.transform.position, "Main-pixel-after");
        File.WriteAllText($"Evidence/ReflectionLights-{SystemInfo.graphicsDeviceType}.txt",
            $"PixelMean={pixelMean}; VertexMean={vertexMean}; MainBefore={mainBefore}; MainAfter={mainAfter}; PipelineMode={mode}\nRegion: 16x16 pixels at projected panel center; excludes empty background.\n");
        Assert.That(pixelMean, Is.GreaterThan(5), "The small point light must illuminate the panel in pixel mode.");
        Assert.That(vertexMean, Is.LessThan(pixelMean * .25f), "Panel vertices are outside light range, so per-vertex lighting must be dimmer.");
        Assert.That(pipeline.additionalLightsRenderingMode, Is.EqualTo(mode));
        Assert.That(mainBefore, Is.GreaterThan(5));
        Assert.That(mainAfter, Is.EqualTo(mainBefore).Within(.1f), "The main camera must retain its per-pixel lighting.");
    }

    static Camera ConfigureCamera()
    {
        var camera = Camera.main;
        camera.transform.position = new Vector3(0, 5, -15);
        camera.transform.LookAt(new Vector3(0, 2, 0));
        camera.fieldOfView = 60;
        camera.aspect = 16f / 9;
        return camera;
    }

    void ColoredCube(string name, Vector3 position, Color color)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.position = position;
        cube.transform.localScale = Vector3.one * 2;
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.SetColor("_BaseColor", color);
        cube.GetComponent<Renderer>().sharedMaterial = material;
        _extraMaterials.Add(material);
    }

    static void SetOption(OceanPlanarReflection owner, string name, object value)
    {
        var serialized = new SerializedObject(owner);
        if (value is bool flag) serialized.FindProperty(name).boolValue = flag;
        else if (value is int number) serialized.FindProperty(name).intValue = number;
        else serialized.FindProperty(name).floatValue = (float)value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static Color32[] Read(OceanPlanarReflection owner, string label)
    {
        return ReadTarget(owner.ReflectionCamera.targetTexture, label);
    }

    static float ReadPrimaryMean(Camera camera, Vector3 center, string label)
    {
        var previous = camera.targetTexture;
        var target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGBHalf);
        try
        {
            target.Create();
            camera.targetTexture = target;
            RenderPipeline.SubmitRenderRequest(camera,
                new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
            return MeanAtPanel(ReadTarget(target, label), camera, center);
        }
        finally { camera.targetTexture = previous; target.Release(); Object.Destroy(target); }
    }

    static Color32[] ReadTarget(RenderTexture target, string label)
    {
        var previous = RenderTexture.active;
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            Directory.CreateDirectory("Evidence");
            File.WriteAllBytes($"Evidence/ReflectionOptions-{label}-{SystemInfo.graphicsDeviceType}.png", texture.EncodeToPNG());
            return texture.GetPixels32();
        }
        finally { RenderTexture.active = previous; Object.Destroy(texture); }
    }

    static int Count(Color32[] colors, bool green)
    {
        int count = 0;
        foreach (var c in colors)
            if (green ? c.g > 180 && c.r < 80 && c.b < 80 : c.r > 180 && c.b > 180 && c.g < 80) count++;
        return count;
    }

    static float MeanAtPanel(Color32[] colors, Camera camera, Vector3 center)
    {
        var projected = camera.WorldToViewportPoint(center);
        int width = camera.targetTexture.width, height = camera.targetTexture.height;
        int cx = Mathf.RoundToInt(projected.x * width), cy = Mathf.RoundToInt(projected.y * height);
        double sum = 0;
        int count = 0;
        for (int y = Mathf.Max(0, cy - 8); y < Mathf.Min(height, cy + 8); y++)
        for (int x = Mathf.Max(0, cx - 8); x < Mathf.Min(width, cx + 8); x++)
        {
            var c = colors[y * width + x];
            sum += c.r + c.g + c.b;
            count++;
        }
        Assert.That(count, Is.EqualTo(256), "Panel center must be in view for the light comparison.");
        return (float)(sum / (count * 3));
    }

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
        foreach (var material in _extraMaterials) if (material != null) Object.DestroyImmediate(material);
        _extraMaterials.Clear();
    }
}
