using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Crest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.XR;
using Object = UnityEngine.Object;

// Read-only observation of actual URP frame data; no production pass replacement.
public class CameraSettingsObservationFeature : ScriptableRendererFeature
{
    public struct Observation
    {
        public Camera Camera;
        public RenderTextureDescriptor Descriptor;
        public float Scale;
        public CameraRenderType Type;
    }
    public static bool Armed;
    public static readonly List<Observation> Records = new List<Observation>();
    class Pass : ScriptableRenderPass
    {
        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            if (!Armed) return;
            var data = frameData.Get<UniversalCameraData>();
            Records.Add(new Observation { Camera = data.camera, Descriptor = data.cameraTargetDescriptor, Scale = data.renderScale, Type = data.renderType });
        }
    }
    Pass _pass;
    public override void Create() { _pass = new Pass { renderPassEvent = RenderPassEvent.AfterRendering }; }
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) { renderer.EnqueuePass(_pass); }
}

public class CameraSettingsTests
{
    readonly List<Object> _owned = new List<Object>();
    Camera _a, _b, _overlay;
    Renderer _probe, _green;
    UniversalRenderPipelineAsset _pipeline;
    UniversalRendererData _renderer;
    RenderPipelineAsset _oldQuality;
    TimeProviderCustom _clock;
    float _timeScale, _drWidth, _drHeight;
    string _report;
    int _requests, _pixels, _descriptorChecks;
    RenderTexture _foamIdentity;
    float[] _foamBefore;
    int _frame, _simFrame;
    RectInt? _dynamicReadViewport;

    [UnityTest, Timeout(90000)]
    public IEnumerator IndependentCamerasAndActualStackKeepBackgroundAndSimulationState()
    {
        Prepare(); yield return new EnterPlayMode();
        yield return Setup("Cameras");
        BeginSimulationGuard();
        var target = Target(256, 256, 1);
        _probe.enabled = false; _a.backgroundColor = Color.magenta;
        var water = Render(_a, target, false, "Surface-control");
        int clear = 0; foreach (var c in water) if (c.r > 240 && c.b > 240 && c.g < 16) clear++;
        Assert.That(clear, Is.Zero, "Actual original opaque ocean must cover the view without the diagnostic probe.");
        _probe.enabled = true;
        _a.backgroundColor = Color.red; _b.backgroundColor = Color.blue;
        CheckCenter(Render(_a, target, false, "A-red-background"), target, Color.cyan);
        CheckCenter(Render(_b, target, false, "B-blue-background"), target, new Color(1, 1, 0));
        CheckCenter(Render(_a, target, false, "A-after-B", false), target, Color.cyan);
        var overlayData = _overlay.GetUniversalAdditionalCameraData();
        overlayData.renderType = CameraRenderType.Overlay;
        Assert.That(overlayData.clearDepth, Is.True);
        _a.GetUniversalAdditionalCameraData().cameraStack.Add(_overlay);
        _overlay.enabled = true;
        _a.cullingMask = 1 << 4; _overlay.cullingMask = 1 << 5;
        _green.enabled = true;
        CheckCenter(Render(_a, target, true, "Base-overlay-stack"), target, Color.magenta);
        Assert.That(CameraSettingsObservationFeature.Records.Count, Is.EqualTo(2));
        Assert.That(CameraSettingsObservationFeature.Records[0].Camera, Is.SameAs(_a));
        Assert.That(CameraSettingsObservationFeature.Records[0].Type, Is.EqualTo(CameraRenderType.Base));
        Assert.That(CameraSettingsObservationFeature.Records[1].Camera, Is.SameAs(_overlay));
        Assert.That(CameraSettingsObservationFeature.Records[1].Type, Is.EqualTo(CameraRenderType.Overlay));
        _a.GetUniversalAdditionalCameraData().cameraStack.Clear();
        _overlay.enabled = false;
        _green.enabled = false; _a.cullingMask = (1 << 4) | (1 << 5);
        CheckCenter(Render(_a, target, true, "Stack-removed", false), target, Color.cyan);
        Assert.That(CameraSettingsObservationFeature.Records.Count, Is.EqualTo(1));
        CheckCenter(Render(_b, target, false, "B-after-stack", false), target, new Color(1, 1, 0));
        EndSimulationGuard(); Summary();
    }

    [UnityTest, Timeout(90000)]
    public IEnumerator UnderwaterViewsAndGeometryOverlayStackIsolateFogWithMsaa()
    {
        Prepare(); yield return new EnterPlayMode();
        yield return Setup("Underwater"); _probe.enabled = false;
        _a.transform.SetPositionAndRotation(new Vector3(0, -3, 0), Quaternion.identity);
        _b.transform.SetPositionAndRotation(new Vector3(0, 3, 0), Quaternion.Euler(90, 0, 0));
        _a.cullingMask = _b.cullingMask = 1 << 4;
        _a.backgroundColor = _b.backgroundColor = new Color(.7f, .7f, .7f);
        _overlay.transform.SetPositionAndRotation(_a.transform.position, _a.transform.rotation);
        _overlay.cullingMask = 1 << 5;
        _overlay.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
        _overlay.gameObject.SetActive(false);
        var overlayFog = _overlay.gameObject.AddComponent<UnderwaterRenderer>();
        overlayFog._overlay = true; overlayFog._layer = 5; overlayFog.EnableShaderAPI = false; overlayFog.enabled = false;
        _overlay.gameObject.SetActive(true);
        var belowFog = _a.GetComponent<UnderwaterRenderer>();
        if (belowFog == null) belowFog = _a.gameObject.AddComponent<UnderwaterRenderer>();
        belowFog.EnableShaderAPI = false; belowFog.enabled = true;
        var aboveFog = _b.gameObject.AddComponent<UnderwaterRenderer>(); aboveFog.EnableShaderAPI = false;
        _green.transform.SetPositionAndRotation(new Vector3(0, -3, 12), Quaternion.Euler(-90, 0, 0));
        _green.transform.localScale = Vector3.one * .4f; _green.sharedMaterial.SetColor("_BaseColor", Color.white);
        _green.enabled = true;
        yield return Updates(24);
        var heightField = typeof(UnderwaterRenderer).GetField("_heightAboveWater", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var aboveSettings = new SerializedObject(aboveFog);
        File.AppendAllText(_report, $"UnderwaterSetup; Playing={Application.isPlaying}; AbovePosition={_b.transform.position}; BelowPosition={_a.transform.position}; AboveEnabled={aboveFog.isActiveAndEnabled}; AbovePrimary={UnderwaterRenderer.Instance == aboveFog}; BelowPrimary={UnderwaterRenderer.Instance == belowFog}; AbovePrivateHeight={heightField.GetValue(aboveFog)}; ViewerHeight={OceanRenderer.Instance.ViewerHeightAboveWater}; AboveMode={aboveFog._mode}; DebugSkipHeight={aboveSettings.FindProperty("_debug").FindPropertyRelative("_disableHeightAboveWaterOptimization").boolValue}\n");
        Assert.That(belowFog.IsActive, Is.True); Assert.That(aboveFog.IsActive, Is.False);
        BeginSimulationGuard();
        foreach (int samples in new[] { 1, 4 })
        foreach (bool beforeTransparents in new[] { false, true })
        {
            _pipeline.msaaSampleCount = samples;
            var target = Target(256, 256, samples);
            belowFog.EnableShaderAPI = false;
            Render(_a, target, true, $"Below-reset-{samples}-before{beforeTransparents}", false);
            aboveFog.enabled = false;
            var aboveControl = Render(_b, target, false, $"Above-control-{samples}-before{beforeTransparents}", false);
            aboveFog.enabled = true;
            belowFog.EnableShaderAPI = beforeTransparents;
            overlayFog.EnableShaderAPI = beforeTransparents;
            var baseOnly = Render(_a, target, true, $"Below-base-{samples}-before{beforeTransparents}", samples == 1);
            var aboveAfter = Render(_b, target, false, $"Above-after-below-{samples}-before{beforeTransparents}", samples == 4);
            int aboveChanged = Differences(aboveControl, aboveAfter);
            Assert.That(aboveChanged, Is.Zero, "Inactive above-water fog must not inherit the submerged camera's composite.");
            _overlay.enabled = true; _a.GetUniversalAdditionalCameraData().cameraStack.Add(_overlay);
            overlayFog.enabled = false;
            var withoutOverlayFog = Render(_a, target, true, $"Overlay-control-{samples}-before{beforeTransparents}", samples == 1);
            overlayFog.enabled = true;
            var withOverlayFog = Render(_a, target, true, $"Underwater-overlay-stack-{samples}-before{beforeTransparents}", true);
            Assert.That(CameraSettingsObservationFeature.Records.Count, Is.EqualTo(2));
            int geometry = 0, geometryChanged = 0, backgroundChanged = 0, outsideBaseChanged = 0, outsideFogChanged = 0;
            var bounds = _green.bounds;
            var min = _overlay.WorldToViewportPoint(bounds.min); var max = _overlay.WorldToViewportPoint(bounds.max);
            var projected = Rect.MinMaxRect(Mathf.Min(min.x, max.x) * target.width - 2, Mathf.Min(min.y, max.y) * target.height - 2,
                Mathf.Max(min.x, max.x) * target.width + 2, Mathf.Max(min.y, max.y) * target.height + 2);
            for (int i = 0; i < baseOnly.Length; i++)
            {
                bool foreground = RgbDistance(baseOnly[i], withoutOverlayFog[i]) > 15;
                bool changed = RgbDistance(withoutOverlayFog[i], withOverlayFog[i]) > 15;
                if (foreground) { geometry++; if (changed) geometryChanged++; }
                else if (changed) backgroundChanged++;
                if (!projected.Contains(new Vector2(i % target.width + .5f, i / target.width + .5f)))
                {
                    if (foreground) outsideBaseChanged++;
                    if (changed) outsideFogChanged++;
                }
            }
            File.AppendAllText(_report, $"UnderwaterStack; MSAA={samples}; BeforeTransparents={beforeTransparents}; AboveChanged={aboveChanged}; Geometry={geometry}; GeometryFogChanged={geometryChanged}; BackgroundFogChanged={backgroundChanged}; ProjectedQuad={projected}; OutsideBaseChanged={outsideBaseChanged}; OutsideFogChanged={outsideFogChanged}\n");
            Assert.That(outsideBaseChanged, Is.LessThan(20), "Adding an overlay must retain the base composite outside the independently projected quad.");
            Assert.That(outsideFogChanged, Is.LessThan(20));
            Assert.That(geometry, Is.GreaterThan(100)); Assert.That(geometryChanged, Is.GreaterThan(100));
            Assert.That(geometry, Is.LessThan(target.width * target.height / 2), "Adding a small overlay quad must not replace the base camera's whole-frame composite.");
            Assert.That(backgroundChanged, Is.LessThan(20), "Geometry-only overlay fog must preserve base-camera background.");
            overlayFog.enabled = false; _overlay.enabled = false; _a.GetUniversalAdditionalCameraData().cameraStack.Clear();
            var restored = Render(_a, target, true, $"Underwater-stack-removed-{samples}-before{beforeTransparents}", false);
            Assert.That(Differences(baseOnly, restored), Is.Zero);
            target.Release();
        }
        EndSimulationGuard(); Summary();
    }

    static int RgbDistance(Color32 a, Color32 b) => Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b);
    static int Differences(Color32[] a, Color32[] b)
    {
        int count = 0; for (int i = 0; i < a.Length; i++) if (RgbDistance(a[i], b[i]) > 15) count++; return count;
    }

    [UnityTest, Timeout(90000)]
    public IEnumerator ResizeMsaaRenderScaleAndDynamicCapabilityUseActualDescriptors()
    {
        Prepare(); yield return new EnterPlayMode();
        yield return Setup("Targets"); BeginSimulationGuard(); _a.backgroundColor = Color.red;
        foreach (var size in new[] { new Vector2Int(256, 256), new Vector2Int(384, 192), new Vector2Int(192, 384), new Vector2Int(128, 128) })
        {
            var target = Target(size.x, size.y, 1);
            CheckCenter(Render(_a, target, false, $"Resize-{size.x}x{size.y}", size.x == 384), target, Color.cyan);
            Descriptor(size.x, size.y, 1, 1);
            target.Release(); Assert.That(target.IsCreated(), Is.False);
        }
        foreach (int requested in new[] { 2, 4, 8 })
        {
            var desc = new RenderTextureDescriptor(256, 192, RenderTextureFormat.ARGB32, 24) { msaaSamples = requested };
            int supported = SystemInfo.GetRenderTextureSupportedMSAASampleCount(desc);
            File.AppendAllText(_report, $"MSAACapability; Requested={requested}; Supported={supported}\n");
            Assert.That(supported, Is.InRange(1, requested));
            if (supported != requested) { File.AppendAllText(_report, "Requested MSAA level NOT TESTED: hardware fallback, not silently accepted.\n"); continue; }
            _pipeline.msaaSampleCount = requested;
            var target = Target(256, 192, requested);
            CheckCenter(Render(_a, target, false, $"MSAA-{requested}", requested == 4), target, Color.cyan);
            Descriptor(256, 192, requested, 1);
            target.Release(); Assert.That(target.IsCreated(), Is.False);
        }
        _pipeline.msaaSampleCount = 1;
        var scaled = Target(256, 192, 1);
        foreach (float scale in new[] { .5f, 1.5f, 1f })
        {
            _pipeline.renderScale = scale;
            CheckCenter(Render(_a, scaled, false, $"Scale-{scale}", scale == .5f), scaled, Color.cyan);
            Descriptor((int)(256 * scale), (int)(192 * scale), 1, scale);
        }
        var displays = new List<XRDisplaySubsystem>(); SubsystemManager.GetSubsystems(displays);
        File.AppendAllText(_report, $"Capabilities; DynamicResolution={SystemInfo.supportsDynamicResolution}; XRDisplays={displays.Count}; XR=NOT TESTED (no XR view rendered)\n");
        if (SystemInfo.supportsDynamicResolution)
        {
            _a.allowDynamicResolution = true;
            ScalableBufferManager.ResizeBuffers(.75f, .75f);
            var dynamicTarget = Target(256, 192, 1, true);
            File.AppendAllText(_report, $"DynamicRequested; WidthScale={ScalableBufferManager.widthScaleFactor:R}; HeightScale={ScalableBufferManager.heightScaleFactor:R}\n");
            var mask = _a.cullingMask; var greenScale = _green.transform.localScale;
            _a.cullingMask = 1 << 5; _probe.enabled = false; _green.enabled = true; _green.transform.localScale = Vector3.one * 4;
            foreach (var feature in _renderer.rendererFeatures) if (!(feature is CameraSettingsObservationFeature)) feature.SetActive(false);
            var control = Render(_a, dynamicTarget, true, "Dynamic-without-Crest-control");
            int green = 0, black = 0, minX = dynamicTarget.width, minY = dynamicTarget.height, maxX = -1, maxY = -1;
            for (int i = 0; i < control.Length; i++)
            {
                var p = control[i];
                if (p.g > 250 && p.r < 5 && p.b < 5) { green++; minX = Math.Min(minX, i % dynamicTarget.width); maxX = Math.Max(maxX, i % dynamicTarget.width); minY = Math.Min(minY, i / dynamicTarget.width); maxY = Math.Max(maxY, i / dynamicTarget.width); }
                if (p.r < 5 && p.g < 5 && p.b < 5) black++;
            }
            Assert.That(green, Is.EqualTo(control.Length), "Uniform DR must fill the entire offscreen control target without cropping.");
            Assert.That(black, Is.Zero);
            _dynamicReadViewport = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            File.AppendAllText(_report, $"DynamicNoCrestControl; Green={green}; Black={black}; Total={control.Length}\n");
            File.AppendAllText(_report, $"DynamicReadbackViewport={_dynamicReadViewport}; ControlWithoutCrest=True; FullOffscreenControl=PASSED; LiveBackbuffer=NOT TESTED\n");
            foreach (var feature in _renderer.rendererFeatures) if (!(feature is CameraSettingsObservationFeature)) feature.SetActive(true);
            _a.cullingMask = mask; _probe.enabled = true; _green.enabled = false; _green.transform.localScale = greenScale;
            CheckCenter(Render(_a, dynamicTarget, true, "Dynamic-resolution", true), dynamicTarget, Color.cyan);
            Assert.That(CameraSettingsObservationFeature.Records[0].Descriptor.useDynamicScale, Is.True);
            File.AppendAllText(_report, $"DynamicActual; WidthScale={ScalableBufferManager.widthScaleFactor:R}; HeightScale={ScalableBufferManager.heightScaleFactor:R}; TargetUseDynamicScale={dynamicTarget.useDynamicScale}\n");
            // Descriptor flag is not proof of effective hardware scaling. Report an unavailable path explicitly.
            if (ScalableBufferManager.widthScaleFactor >= .99f || ScalableBufferManager.heightScaleFactor >= .99f)
                File.AppendAllText(_report, "Effective reduced hardware resolution NOT TESTED: requested factors were not applied.\n");
            _a.allowDynamicResolution = false;
        }
        else File.AppendAllText(_report, "Reduced dynamic resolution NOT TESTED: SystemInfo capability false.\n");
        ScalableBufferManager.ResizeBuffers(_drWidth, _drHeight);
        _dynamicReadViewport = null;
        CheckCenter(Render(_a, scaled, false, "Targets-restored", false), scaled, Color.cyan);
        Descriptor(256, 192, 1, 1);
        EndSimulationGuard(); Summary();
    }

    T Own<T>(T value) where T : Object { _owned.Add(value); return value; }
    void Prepare()
    {
        _timeScale = Time.timeScale; _oldQuality = QualitySettings.renderPipeline;
        _drWidth = ScalableBufferManager.widthScaleFactor; _drHeight = ScalableBufferManager.heightScaleFactor;
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
    }
    IEnumerator Setup(string suffix)
    {
        Assert.That(Application.isPlaying, Is.True, "EnterPlayMode must be yielded by the top-level UnityTest.");
        _requests = _pixels = _descriptorChecks = 0;
        _dynamicReadViewport = null; Time.timeScale = 0;
        var source = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
        _pipeline = Own(Object.Instantiate(source));
        var renderer = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/CrestPortDemos/Renderer.asset")));
        _renderer = renderer;
        var originals = renderer.rendererFeatures.ToArray(); renderer.rendererFeatures.Clear();
        foreach (var feature in originals) renderer.rendererFeatures.Add(Own(Object.Instantiate(feature)));
        renderer.rendererFeatures.Add(Own(ScriptableObject.CreateInstance<CameraSettingsObservationFeature>()));
        var pipelineFields = new SerializedObject(_pipeline); var renderers = pipelineFields.FindProperty("m_RendererDataList"); renderers.arraySize = 1;
        renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer; pipelineFields.FindProperty("m_DefaultRendererIndex").intValue = 0; pipelineFields.ApplyModifiedPropertiesWithoutUndo();
        _pipeline.msaaSampleCount = 1; _pipeline.renderScale = 1; QualitySettings.renderPipeline = _pipeline;
        var ocean = OceanRenderer.Instance;
        foreach (var shape in Object.FindObjectsByType<ShapeWaves>()) shape.enabled = false;
        var surface = Own(new Material(ocean.OceanMaterial)); surface.DisableKeyword("_TRANSPARENCY_ON"); surface.DisableKeyword("_APPLYNORMALMAPPING_ON"); surface.SetFloat("_Transparency", 0); ocean.OceanMaterial = surface;
        var fields = new SerializedObject(ocean); fields.FindProperty("_minScale").floatValue = 8; fields.FindProperty("_maxScale").floatValue = 8; fields.ApplyModifiedPropertiesWithoutUndo();
        var foam = Own(ScriptableObject.CreateInstance<SimSettingsFoam>()); foam._prewarm = false; foam._waveFoamStrength = foam._shorelineFoamStrength = 0; foam._foamFadeRate = .8f; ocean._simSettingsFoam = foam;
        var depth = Own(ScriptableObject.CreateInstance<SimSettingsSeaFloorDepth>()); depth._allowVaryingWaterLevel = false; ocean._simSettingsSeaFloorDepth = depth;
        _a = Camera.main; Configure(_a, Vector3.zero);
        ocean.ViewCamera = _a;
        _b = new GameObject("Independent camera B").AddComponent<Camera>(); Configure(_b, new Vector3(2, 0, 1));
        _overlay = new GameObject("Actual overlay camera").AddComponent<Camera>(); Configure(_overlay, Vector3.zero);
        foreach (var ur in Object.FindObjectsByType<UnderwaterRenderer>()) ur.enabled = false;
        _probe = Plane("Inverted camera background probe", 1, 8, Own(new Material(Shader.Find("Hidden/Crest/Tests/CameraBackgroundProbe"))), 5);
        var greenMaterial = Own(new Material(Shader.Find("Universal Render Pipeline/Unlit"))); greenMaterial.SetColor("_BaseColor", Color.green);
        _green = Plane("Overlay opaque green control", .5f, 6, greenMaterial, 5); _green.enabled = false;
        var sourceMaterial = Own(new Material(Shader.Find("Crest/Inputs/Foam/Override Foam"))); sourceMaterial.SetFloat("_FoamValue", .65f);
        var input = Plane("Simulation guard foam seed", 0, 8, sourceMaterial, 0).gameObject.AddComponent<RegisterFoamInput>();
        yield return Updates(8);
        _clock = ocean.gameObject.AddComponent<TimeProviderCustom>(); _clock._overrideTime = _clock._overrideDeltaTime = true; _clock._time = 1; _clock._deltaTime = 1f / 30; ocean.PushTimeProvider(_clock);
        yield return Updates(8); input.enabled = false; yield return Updates(3);
        _report = $"Evidence/CameraSettings-{suffix}-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report, $"Playing={Application.isPlaying}; Suite={suffix}; Editor-only cloned pipeline/renderer/features; original ocean + inverse-background probe, RGB center tolerance2/255. Actual URP frame-data descriptors. Full native first-layer foam is nonzero with positive delta/fade, source disabled; synchronous same-frame requests must preserve pixels/texture/history stamps. Underwater enabled only in its named suite. XR capability only, not rendered XR acceptance.\n");
    }
    void Configure(Camera camera, Vector3 offset)
    {
        camera.enabled = false; camera.transform.SetPositionAndRotation(new Vector3(0, 12, 0) + offset, Quaternion.Euler(90, 0, 0));
        camera.orthographic = true; camera.orthographicSize = 8; camera.aspect = 1; camera.nearClipPlane = .1f; camera.farClipPlane = 2000;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.red; camera.allowHDR = false; camera.allowMSAA = true;
        camera.cullingMask = (1 << 4) | (1 << 5); camera.GetUniversalAdditionalCameraData().renderPostProcessing = false; camera.GetUniversalAdditionalCameraData().SetRenderer(0);
    }
    Renderer Plane(string name, float height, float width, Material material, int layer)
    {
        var owner = GameObject.CreatePrimitive(PrimitiveType.Plane); owner.name = name; owner.layer = layer; owner.transform.position = Vector3.up * height;
        owner.transform.localScale = Vector3.one * (width / 10); Object.DestroyImmediate(owner.GetComponent<Collider>()); var renderer = owner.GetComponent<Renderer>(); renderer.sharedMaterial = material; return renderer;
    }
    RenderTexture Target(int w, int h, int samples, bool dynamic = false)
    {
        var target = Own(new RenderTexture(new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 24) { msaaSamples = samples, useDynamicScale = dynamic })); target.Create(); return target;
    }
    static IEnumerator Updates(int count)
    {
        int frame = OceanRenderer.FrameCount; float deadline = Time.realtimeSinceStartup + 15;
        while (OceanRenderer.FrameCount < frame + count && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(OceanRenderer.FrameCount - frame, Is.GreaterThanOrEqualTo(count));
    }
    Color32[] Render(Camera camera, RenderTexture target, bool stack, string label, bool save = true)
    {
        var oldTarget = camera.targetTexture; var active = RenderTexture.active; bool invert = GL.invertCulling;
        CameraSettingsObservationFeature.Armed = true;
        void Submit()
        {
            CameraSettingsObservationFeature.Records.Clear();
            if (stack) RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            else RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            _requests++;
        }
        try
        {
            Submit();
            Assert.That(camera.targetTexture, Is.SameAs(oldTarget)); Assert.That(GL.invertCulling, Is.EqualTo(invert));
            Assert.That(CameraSettingsObservationFeature.Records.Count, Is.GreaterThan(0));
            File.AppendAllText(_report, $"Render={label}; Camera={camera.name}; Stack={stack}; Records={CameraSettingsObservationFeature.Records.Count}; Target={target.width}x{target.height}/{target.antiAliasing}\n");
            var resolved = RenderTexture.GetTemporary(target.width, target.height, 0, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(target, resolved); RenderTexture.active = resolved;
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
                if (save) File.WriteAllBytes($"Evidence/CameraSettings-{label}-{SystemInfo.graphicsDeviceType}.png", texture.EncodeToPNG());
                var pixels = texture.GetPixels32();
                if (_probe.enabled)
                {
                    // Graph globals are transient. Measure the sampled texture while the GPU is drawing.
                    _probe.sharedMaterial.SetFloat("_ProbeDimensions", 1);
                    Submit(); Graphics.Blit(target, resolved); RenderTexture.active = resolved;
                    texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
                    var descriptor = CameraSettingsObservationFeature.Records[CameraSettingsObservationFeature.Records.Count - 1].Descriptor;
                    int bgWidth = descriptor.width, bgHeight = descriptor.height;
                    if (descriptor.useDynamicScale) { bgWidth = Mathf.RoundToInt(bgWidth * ScalableBufferManager.widthScaleFactor); bgHeight = Mathf.RoundToInt(bgHeight * ScalableBufferManager.heightScaleFactor); }
                    var encoded = new Color(bgWidth / 1024f, bgHeight / 1024f, 0);
                    if (resolved.sRGB && QualitySettings.activeColorSpace == ColorSpace.Linear) encoded = encoded.gamma;
                    if (label == "Dynamic-resolution") File.WriteAllBytes($"Evidence/CameraSettings-Dynamic-dimensions-{SystemInfo.graphicsDeviceType}.png", texture.EncodeToPNG());
                    File.AppendAllText(_report, $"GPUBackgroundDimensions; Expected={bgWidth}x{bgHeight}; ReadbackSRGB={resolved.sRGB}\n");
                    CheckCenter(texture.GetPixels32(), target, encoded);
                    _probe.sharedMaterial.SetFloat("_ProbeDimensions", 0);
                }
                return pixels;
            }
            finally { Object.DestroyImmediate(texture); RenderTexture.ReleaseTemporary(resolved); }
        }
        finally { if (_probe != null) _probe.sharedMaterial.SetFloat("_ProbeDimensions", 0); CameraSettingsObservationFeature.Armed = false; RenderTexture.active = active; }
    }
    void CheckCenter(Color32[] pixels, RenderTexture target, Color expected)
    {
        var wanted = (Color32)expected; int max = 0;
        var viewport = target.useDynamicScale && _dynamicReadViewport.HasValue ? _dynamicReadViewport.Value : new RectInt(0, 0, target.width, target.height);
        int cx = viewport.x + viewport.width / 2, cy = viewport.y + viewport.height / 2;
        for (int y = cy - 8; y < cy + 8; y++)
        for (int x = cx - 8; x < cx + 8; x++)
        {
            var p = pixels[y * target.width + x]; max = Mathf.Max(max, Math.Abs(p.r - wanted.r), Math.Abs(p.g - wanted.g), Math.Abs(p.b - wanted.b)); _pixels++;
        }
        File.AppendAllText(_report, $"Center; Expected={wanted}; ActualCenter={pixels[cy * target.width + cx]}; Viewport={viewport}; MaxRGBError={max}\n"); Assert.That(max, Is.LessThanOrEqualTo(2));
    }
    void Descriptor(int width, int height, int samples, float scale)
    {
        var data = CameraSettingsObservationFeature.Records[0]; var desc = data.Descriptor;
        File.AppendAllText(_report, $"Descriptor; Actual={desc.width}x{desc.height}/{desc.msaaSamples}; RenderScale={data.Scale:R}; Dynamic={desc.useDynamicScale}\n");
        Assert.That(desc.width, Is.EqualTo(width)); Assert.That(desc.height, Is.EqualTo(height)); Assert.That(desc.msaaSamples, Is.EqualTo(samples)); Assert.That(data.Scale, Is.EqualTo(scale).Within(.001f)); _descriptorChecks++;
    }
    float[] Foam()
    {
        var request = AsyncGPUReadback.Request(OceanRenderer.Instance._lodDataFoam.DataTexture, 0, TextureFormat.RFloat); request.WaitForCompletion(); Assert.That(request.hasError, Is.False); return request.GetData<float>(0).ToArray();
    }
    void BeginSimulationGuard()
    {
        _frame = OceanRenderer.FrameCount; _simFrame = BuildCommandBufferBase._lastUpdateFrame; _foamIdentity = OceanRenderer.Instance._lodDataFoam.DataTexture; _foamBefore = Foam();
        float peak = 0; foreach (float f in _foamBefore) { Assert.That(float.IsFinite(f), Is.True); peak = Mathf.Max(peak, f); }
        Assert.That(peak, Is.GreaterThan(.05f)); Assert.That(OceanRenderer.Instance.DeltaTime, Is.GreaterThan(0));
    }
    void EndSimulationGuard()
    {
        Assert.That(OceanRenderer.FrameCount, Is.EqualTo(_frame)); Assert.That(BuildCommandBufferBase._lastUpdateFrame, Is.EqualTo(_simFrame));
        Assert.That(OceanRenderer.Instance._lodDataFoam.DataTexture, Is.SameAs(_foamIdentity));
        var after = Foam(); float error = 0; for (int i = 0; i < after.Length; i++) error = Mathf.Max(error, Mathf.Abs(after[i] - _foamBefore[i]));
        File.AppendAllText(_report, $"SimulationGuard; Frame={_frame}; PositiveDelta={OceanRenderer.Instance.DeltaTime:R}; NativeFoamSamples={after.Length}; MaxError={error:R}\n"); Assert.That(error, Is.Zero);
    }
    void Summary() { File.AppendAllText(_report, $"Summary; Requests={_requests}; CenterPixels={_pixels}; DescriptorChecks={_descriptorChecks}\n"); }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        CameraSettingsObservationFeature.Armed = false; CameraSettingsObservationFeature.Records.Clear();
        if (_a != null) _a.GetUniversalAdditionalCameraData().cameraStack?.Clear();
        QualitySettings.renderPipeline = _oldQuality; ScalableBufferManager.ResizeBuffers(_drWidth, _drHeight); Time.timeScale = _timeScale;
        yield return null;
        Assert.That(QualitySettings.renderPipeline, Is.SameAs(_oldQuality));
        if (Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); OceanRenderer.EnableGraphicsInBatchMode = false;
        for (int i = _owned.Count - 1; i >= 0; i--) if (_owned[i] != null) { if (_owned[i] is RenderTexture rt) rt.Release(); Object.DestroyImmediate(_owned[i]); } _owned.Clear();
    }
}
