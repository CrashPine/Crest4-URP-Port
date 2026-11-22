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
    [Test]
    public void FreshSphereClipMaterialSelectsItsRequiredVariant()
    {
        var owner = new GameObject("Fresh primitive variant"); owner.SetActive(false);
        var input = owner.AddComponent<RegisterClipSurfaceInput>();
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var primitive = typeof(RegisterClipSurfaceInput).GetField("_primitive", flags);
        var material = typeof(RegisterClipSurfaceInput).GetField("_signedDistancedMaterial", flags);
        var initialize = typeof(RegisterClipSurfaceInput).GetMethod("InitializeSignedDistanceMaterial", flags);
        try
        {
            primitive.SetValue(input, RegisterClipSurfaceInput.Primitive.Sphere);
            for (int lifetime = 0; lifetime < 2; lifetime++)
            {
                initialize.Invoke(input, null); var current = (Material)material.GetValue(input);
                Assert.That(current.IsKeywordEnabled("_SPHERE"), Is.True, "Fresh/recreated sphere must select a compiled variant");
                Assert.That(current.IsKeywordEnabled("_CUBE"), Is.False);
                Object.DestroyImmediate(current); material.SetValue(input, null);
            }
            primitive.SetValue(input, RegisterClipSurfaceInput.Primitive.Cube);
            initialize.Invoke(input, null); var cube = (Material)material.GetValue(input);
            Assert.That(cube.IsKeywordEnabled("_CUBE"), Is.True);
            Assert.That(cube.IsKeywordEnabled("_SPHERE"), Is.False);
        }
        finally
        {
            var current = (Material)material.GetValue(input); if (current != null) Object.DestroyImmediate(current);
            Object.DestroyImmediate(owner);
        }
    }

    const int Size = 192;
    readonly List<Material> _materials = new List<Material>();
    RenderTexture _target;
    Camera _camera;
    float _timeScale = 1;
    Color32[] _albedoImage;
    Color _albedoSample;
    SimSettingsSeaFloorDepth _depthSettings;

    [UnityTest]
    public IEnumerator LocalWaterHeightUpdatesGpuQueriesAndHeightBounds()
    {
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        _timeScale = Time.timeScale;
        Time.timeScale = 0;
        var ocean = OceanRenderer.Instance;
        Camera.main.transform.position = new Vector3(0, 10, 0);
        foreach (var fft in Object.FindObjectsByType<ShapeFFT>(FindObjectsSortMode.None)) fft.enabled = false;
        _depthSettings = ScriptableObject.CreateInstance<SimSettingsSeaFloorDepth>();
        _depthSettings._allowVaryingWaterLevel = true;
        ocean._simSettingsSeaFloorDepth = _depthSettings;
        yield return WaitForOceanUpdates(5);
        var clock = ocean.gameObject.AddComponent<TimeProviderCustom>();
        clock._overrideTime = clock._overrideDeltaTime = true;
        clock._time = clock._deltaTime = 0;
        ocean.PushTimeProvider(clock);
        var owner = GameObject.CreatePrimitive(PrimitiveType.Plane);
        owner.name = "Known local water level";
        owner.transform.localScale = Vector3.one * .8f;
        owner.transform.position = Vector3.up * 2;
        var material = Track(new Material(Shader.Find("Crest/Inputs/Sea Floor Depth/Set Base Water Height Using Geometry")));
        owner.GetComponent<Renderer>().sharedMaterial = material;
        var input = owner.AddComponent<RegisterHeightInput>();
        var helpers = new[] { new SampleHeightHelper(), new SampleHeightHelper(), new SampleHeightHelper() };
        string report = $"Evidence/LocalHeight-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(report, "Original RegisterHeightInput; RGFloat depth offset and public SampleHeightHelper; offset tolerance=0.01m; height tolerance=0.05m; eight consecutive matching query frames\n");
        yield return WaitForOceanUpdates(4);
        Assert.That(ocean._lodDataSeaDepths.DataTexture.graphicsFormat,
            Is.EqualTo(UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32_SFloat));
        yield return CheckLocalHeight("Raised", new[] { Vector2.zero, new Vector2(-8, 0) }, new[] { 2f, 0f }, helpers);
        var bounds = new Rect(-2, -2, 4, 4);
        float minimum = 0, maximum = 0;
        Assert.That(input.ReportHeight(ref bounds, ref minimum, ref maximum), Is.True);
        Assert.That(minimum, Is.EqualTo(2).Within(.001f));
        Assert.That(maximum, Is.EqualTo(2).Within(.001f));
        File.AppendAllText(report, $"RaisedBoundsMin={minimum}; Max={maximum}\n");
        owner.transform.position = Vector3.down;
        yield return WaitForOceanUpdates(4);
        yield return CheckLocalHeight("Lowered", new[] { Vector2.zero, new Vector2(-8, 0) }, new[] { -1f, 0f }, helpers);
        Assert.That(input.ReportHeight(ref bounds, ref minimum, ref maximum), Is.True);
        Assert.That(minimum, Is.EqualTo(-1).Within(.001f));
        Assert.That(maximum, Is.EqualTo(-1).Within(.001f));
        owner.transform.position += Vector3.right * 6;
        yield return WaitForOceanUpdates(4);
        yield return CheckLocalHeight("Moved", new[] { Vector2.zero, new Vector2(6, 0), new Vector2(-8, 0) }, new[] { 0f, -1f, 0f }, helpers);
        ocean.transform.position = Vector3.up * 3;
        yield return WaitForOceanUpdates(4);
        yield return CheckLocalHeight("SeaLevelThree", new[] { Vector2.zero, new Vector2(6, 0), new Vector2(-8, 0) }, new[] { 3f, -1f, 3f }, helpers);
        Assert.That(ocean.SeaLevel, Is.EqualTo(3));
        input.enabled = false;
        Assert.That(OceanChunkRenderer.HeightReporters.Contains(input), Is.False);
        yield return WaitForOceanUpdates(4);
        yield return CheckLocalHeight("Disabled", new[] { Vector2.zero, new Vector2(6, 0), new Vector2(-8, 0) }, new[] { 3f, 3f, 3f }, helpers, requireEmpty: true);
        input.enabled = true;
        Assert.That(OceanChunkRenderer.HeightReporters.Contains(input), Is.True);
        yield return WaitForOceanUpdates(4);
        yield return CheckLocalHeight("Reenabled", new[] { new Vector2(6, 0), new Vector2(-8, 0) }, new[] { -1f, 3f }, helpers);
        Assert.That(ocean.TimeProvider, Is.SameAs(clock));
    }

    static IEnumerator CheckLocalHeight(string phase, Vector2[] positions, float[] expectedHeights, SampleHeightHelper[] helpers, bool requireEmpty = false)
    {
        var ocean = OceanRenderer.Instance;
        var target = ocean._lodDataSeaDepths.DataTexture;
        var transform = ocean._lodTransform._renderData[0].Current;
        var center = new Vector2(transform._posSnapped.x, transform._posSnapped.z);
        float texel = 4 * ocean.CalcLodScale(0) / target.width;
        var request = AsyncGPUReadback.Request(target, 0, TextureFormat.RGFloat);
        while (!request.done) yield return null;
        Assert.That(request.hasError, Is.False);
        var values = request.GetData<Vector2>(0);
        float peakOffset = 0;
        for (int i = 0; i < target.width * target.height; i++)
        {
            // X legitimately contains Crest's -infinity sentinel without floor
            // inputs. The varying water-level offset is exclusively channel Y.
            float offset = values[i].y;
            Assert.That(float.IsNaN(offset) || float.IsInfinity(offset), Is.False);
            peakOffset = Mathf.Max(peakOffset, Mathf.Abs(offset));
        }
        string report = $"Evidence/LocalHeight-{SystemInfo.graphicsDeviceType}.txt";
        File.AppendAllText(report, $"Phase={phase}; SeaLevel={ocean.SeaLevel}; PeakOffset={peakOffset}\n");
        for (int i = 0; i < positions.Length; i++)
        {
            var coordinate = (positions[i] - center) / texel + Vector2.one * (target.width * .5f);
            int x = Mathf.FloorToInt(coordinate.x), y = Mathf.FloorToInt(coordinate.y);
            Assert.That(x, Is.InRange(0, target.width - 1));
            Assert.That(y, Is.InRange(0, target.height - 1));
            float offset = values[y * target.width + x].y;
            float expectedOffset = expectedHeights[i] - ocean.SeaLevel;
            File.AppendAllText(report, $"Position={positions[i]}; ExpectedOffset={expectedOffset}; Offset={offset}\n");
            Assert.That(offset, Is.EqualTo(expectedOffset).Within(.01f));
        }
        if (requireEmpty) Assert.That(peakOffset, Is.LessThan(.0001f));
        var heights = new float[positions.Length];
        int consecutive = 0, previousFrame = -1;
        float deadline = Time.realtimeSinceStartup + 20;
        while (consecutive < 8 && Time.realtimeSinceStartup < deadline)
        {
            int frame = OceanRenderer.FrameCount;
            if (frame == previousFrame) { yield return null; continue; }
            previousFrame = frame;
            bool matching = true;
            for (int i = 0; i < positions.Length; i++)
            {
                helpers[i].Init(new Vector3(positions[i].x, 0, positions[i].y));
                matching &= helpers[i].Sample(out heights[i]) &&
                    !float.IsNaN(heights[i]) && !float.IsInfinity(heights[i]) && Mathf.Abs(heights[i] - expectedHeights[i]) < .05f;
            }
            consecutive = matching ? consecutive + 1 : 0;
            yield return null;
        }
        File.AppendAllText(report, $"QueryHeights={string.Join(", ", heights)}; ConsecutiveMatchingFrames={consecutive}\n");
        Assert.That(consecutive, Is.EqualTo(8), phase + " must propagate local and global water level changes through the original GPU query provider.");
    }

    [UnityTest]
    public IEnumerator AlbedoColorBlendCutoffAndRegistrationAffectSurface()
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
        _camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        RenderSettings.fog = false;
        var underwater = _camera.GetComponent<UnderwaterRenderer>();
        if (underwater != null) underwater.enabled = false;
        _target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
        _target.Create();
        var surface = Track(new Material(ocean.OceanMaterial));
        foreach (string keyword in new[] { "_APPLYNORMALMAPPING_ON", "_FOAM_ON", "_TRANSPARENCY_ON", "_CAUSTICS_ON" }) surface.DisableKeyword(keyword);
        surface.SetFloat("_Foam", 0);
        surface.SetFloat("_ApplyNormalMapping", 0);
        surface.EnableKeyword("_ALBEDO_ON");
        surface.SetFloat("_Albedo", 1);
        ocean.OceanMaterial = surface;
        var configuration = new SerializedObject(ocean);
        configuration.FindProperty("_createFoamSim").boolValue = false;
        configuration.FindProperty("_createAlbedoData").boolValue = true;
        configuration.ApplyModifiedPropertiesWithoutUndo();
        yield return WaitForOceanUpdates(5);
        var clock = ocean.gameObject.AddComponent<TimeProviderCustom>();
        clock._overrideTime = clock._overrideDeltaTime = true;
        clock._time = clock._deltaTime = 0;
        ocean.PushTimeProvider(clock);
        yield return WaitForOceanUpdates(4);
        Assert.That(ocean.Root.GetComponentsInChildren<OceanChunkRenderer>().Length, Is.GreaterThan(0));
        var baseline = Capture("Baseline", "Albedo");
        File.WriteAllText($"Evidence/Albedo-{SystemInfo.graphicsDeviceType}.txt",
            "Original Albedo/Color shader; SrcAlpha/OneMinusSrcAlpha blend; RGBA tolerance=2/255; cutoff=0.5; fixed ocean time/delta=0\n");
        var owner = GameObject.CreatePrimitive(PrimitiveType.Plane);
        owner.name = "Known albedo patch";
        owner.transform.localScale = Vector3.one * .8f;
        var material = Track(new Material(Shader.Find("Crest/Inputs/Albedo/Color")));
        material.SetColor("_Color", Color.red);
        owner.GetComponent<Renderer>().sharedMaterial = material;
        var input = owner.AddComponent<RegisterAlbedoInput>();
        Assert.That(material.GetInt("_BlendModeSource"), Is.EqualTo((int)BlendMode.SrcAlpha));
        Assert.That(material.GetInt("_BlendModeTarget"), Is.EqualTo((int)BlendMode.OneMinusSrcAlpha));
        yield return WaitForOceanUpdates(4);
        yield return CheckAlbedo("Red", new[] { Vector2.zero, new Vector2(8, 0) }, new[] { Color.red, Color.clear });
        Assert.That(CountColor(_albedoImage, red: true), Is.InRange(1000, 8000));
        material.SetColor("_Color", Color.blue);
        yield return WaitForOceanUpdates(4);
        yield return CheckAlbedo("Blue", new[] { Vector2.zero }, new[] { Color.blue });
        Assert.That(CountColor(_albedoImage, red: false), Is.InRange(1000, 8000));
        owner.transform.position = Vector3.right * 6;
        yield return WaitForOceanUpdates(4);
        yield return CheckAlbedo("Moved", new[] { Vector2.zero, new Vector2(6, 0) }, new[] { Color.clear, Color.blue });
        int sample = ColorCentroid(_albedoImage, red: false);
        Assert.That(CountColor(_albedoImage, red: false), Is.InRange(1000, 8000));
        Assert.That(RgbError(_albedoImage[Size * (Size / 2) + Size / 2], baseline[Size * (Size / 2) + Size / 2]), Is.LessThan(.01f));
        material.SetColor("_Color", new Color(1, 0, 0, .75f));
        yield return WaitForOceanUpdates(4);
        // The original input shader uses the same blend factors for RGBA:
        // red=alpha and stored alpha=alpha squared over a clear target.
        yield return CheckAlbedo("Blended", new[] { new Vector2(6, 0) }, new[] { new Color(.75f, 0, 0, .75f * .75f) });
        Color baseColor = baseline[sample];
        if (_target.sRGB) baseColor = baseColor.linear;
        Color expectedSurface = Color.Lerp(baseColor, _albedoSample, _albedoSample.a);
        if (_target.sRGB) expectedSurface = expectedSurface.gamma;
        Color actualSurface = _albedoImage[sample];
        File.AppendAllText($"Evidence/Albedo-{SystemInfo.graphicsDeviceType}.txt",
            $"BlendSurfaceExpected={expectedSurface}; Actual={actualSurface}; TargetSRGB={_target.sRGB}; Pixel={sample}; RGBTolerance=0.03\n");
        Assert.That(RgbError(actualSurface, expectedSurface), Is.LessThan(.03f), "Surface must composite the measured albedo field over the baseline in linear space.");
        material.SetColor("_Color", new Color(1, 0, 0, .25f));
        yield return WaitForOceanUpdates(4);
        yield return CheckAlbedo("BelowCutoff", new[] { new Vector2(6, 0) }, new[] { Color.clear }, requireEmpty: true);
        Assert.That(Different(baseline, _albedoImage), Is.EqualTo(0));
        material.SetColor("_Color", Color.blue);
        input.enabled = false;
        yield return WaitForOceanUpdates(4);
        yield return CheckAlbedo("Disabled", new[] { new Vector2(6, 0) }, new[] { Color.clear }, requireEmpty: true);
        Assert.That(Different(baseline, _albedoImage), Is.EqualTo(0));
        input.enabled = true;
        yield return WaitForOceanUpdates(4);
        yield return CheckAlbedo("Reenabled", new[] { new Vector2(6, 0) }, new[] { Color.blue });
        Assert.That(CountColor(_albedoImage, red: false), Is.InRange(1000, 8000));
        Assert.That(ocean.TimeProvider, Is.SameAs(clock));
    }

    IEnumerator CheckAlbedo(string phase, Vector2[] positions, Color[] expected, bool requireEmpty = false)
    {
        var ocean = OceanRenderer.Instance;
        var target = ocean._lodDataAlbedo.DataTexture;
        var transform = ocean._lodTransform._renderData[0].Current;
        var center = new Vector2(transform._posSnapped.x, transform._posSnapped.z);
        // Albedo may override resolution independently of the ocean simulation.
        float texel = 4 * ocean.CalcLodScale(0) / target.width;
        var request = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32);
        while (!request.done) yield return null;
        Assert.That(request.hasError, Is.False);
        var values = request.GetData<Color32>(0);
        string report = $"Evidence/Albedo-{SystemInfo.graphicsDeviceType}.txt";
        File.AppendAllText(report, $"Phase={phase}; Resolution={target.width}; TexelMetres={texel}\n");
        for (int i = 0; i < positions.Length; i++)
        {
            var coordinate = (positions[i] - center) / texel + Vector2.one * (target.width * .5f);
            int x = Mathf.FloorToInt(coordinate.x), y = Mathf.FloorToInt(coordinate.y);
            Assert.That(x, Is.InRange(0, target.width - 1));
            Assert.That(y, Is.InRange(0, target.height - 1));
            _albedoSample = values[y * target.width + x];
            File.AppendAllText(report, $"Position={positions[i]}; Expected={expected[i]}; Actual={_albedoSample}\n");
            Assert.That(RgbError(_albedoSample, expected[i]), Is.LessThanOrEqualTo(2f / 255));
            Assert.That(Mathf.Abs(_albedoSample.a - expected[i].a), Is.LessThanOrEqualTo(2f / 255));
        }
        if (requireEmpty)
        {
            foreach (var value in values) Assert.That(value, Is.EqualTo(new Color32(0, 0, 0, 0)));
        }
        _albedoImage = Capture(phase, "Albedo");
        File.AppendAllText(report, $"SurfaceRedPixels={CountColor(_albedoImage, true)}; SurfaceBluePixels={CountColor(_albedoImage, false)}; RequireEmpty={requireEmpty}\n");
    }

    static float RgbError(Color a, Color b) => Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b));
    static int CountColor(Color32[] pixels, bool red)
    {
        int count = 0;
        foreach (var p in pixels) if (red ? p.r > 200 && p.g < 50 && p.b < 50 : p.b > 200 && p.r < 50 && p.g < 50) count++;
        return count;
    }
    static int ColorCentroid(Color32[] pixels, bool red)
    {
        int count = 0; long sumX = 0, sumY = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            var p = pixels[i];
            if (!(red ? p.r > 200 && p.g < 50 && p.b < 50 : p.b > 200 && p.r < 50 && p.g < 50)) continue;
            sumX += i % Size; sumY += i / Size; count++;
        }
        Assert.That(count, Is.GreaterThan(1000));
        return (int)(sumY / count) * Size + (int)(sumX / count);
    }

    [UnityTest]
    public IEnumerator CubeSphereAndGeometryClipSurfaceAndDeregisterCleanly()
    {
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        yield return ConfigureClipScene();
        var ocean = OceanRenderer.Instance;
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

    [UnityTest]
    public IEnumerator AdvancedClipInputsComposeAndRestore()
    {
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        yield return ConfigureClipScene();

        const string prefix = "P13-AdvancedClip";
        string report = $"Evidence/{prefix}-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(report, "Flat water, fixed time, LOD0 native clip and top-down surface controls.\n");
        var baseline = Capture("Baseline", prefix);
        Assert.That(Magenta(baseline), Is.Zero);

        var hull = CreateClipGeometry(PrimitiveType.Cube, "Convex Hull", new Vector3(8, 4, 8));
        yield return CheckAdvancedClip("Hull", baseline, report, new Vector3(1, 1, 0));
        hull.transform.position = Vector3.up * 3;
        yield return CheckAdvancedClip("HullAbove", baseline, report, Vector3.zero);
        hull.enabled = false;

        var mask = new Texture2D(2, 1, TextureFormat.RGBA32, false, true)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        try
        {
            mask.SetPixels(new[] { Color.red, Color.cyan });
            mask.Apply();
            var textured = CreateClipGeometry(PrimitiveType.Plane, "Remove Area Texture", Vector3.one * .8f);
            textured.GetComponent<Renderer>().sharedMaterial.mainTexture = mask;
            yield return CheckAdvancedClip("Texture", baseline, report, new Vector3(0, 1, 0));
            textured.enabled = false;
            yield return CheckAdvancedClip("TextureDisabled", baseline, report, Vector3.zero);
        }
        finally
        {
            Object.Destroy(mask);
        }

        var add = CreateClipPrimitive(-2, false, 0);
        var subtract = CreateClipPrimitive(2, true, 1);
        yield return CheckAdvancedClip("OverlappingSdf", baseline, report, new Vector3(.875f, .125f, 0), sdf: true);
        subtract.enabled = false;
        yield return CheckAdvancedClip("AddOnly", baseline, report, new Vector3(.875f, .375f, 0), sdf: true);
        add.enabled = false;
        yield return CheckAdvancedClip("SdfDisabled", baseline, report, Vector3.zero);
    }

    IEnumerator ConfigureClipScene()
    {
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
    }

    RegisterClipSurfaceInput CreateClipGeometry(PrimitiveType primitive, string shaderName, Vector3 scale)
    {
        var owner = GameObject.CreatePrimitive(primitive);
        owner.SetActive(false);
        owner.transform.localScale = scale;
        var shader = Shader.Find("Crest/Inputs/Clip Surface/" + shaderName);
        Assert.That(shader, Is.Not.Null);
        owner.GetComponent<Renderer>().sharedMaterial = Track(new Material(shader));
        var input = owner.AddComponent<RegisterClipSurfaceInput>();
        var serialized = new SerializedObject(input);
        serialized.FindProperty("_mode").intValue = (int)RegisterClipSurfaceInput.Mode.Geometry;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        owner.SetActive(true);
        return input;
    }

    static RegisterClipSurfaceInput CreateClipPrimitive(float x, bool inverted, int order)
    {
        var owner = new GameObject(inverted ? "Restore water SDF" : "Clip water SDF");
        owner.SetActive(false);
        owner.transform.position = Vector3.right * x;
        owner.transform.localScale = new Vector3(8, 4, 8);
        var input = owner.AddComponent<RegisterClipSurfaceInput>();
        var serialized = new SerializedObject(input);
        serialized.FindProperty("_inverted").boolValue = inverted;
        serialized.FindProperty("_order").intValue = order;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        owner.SetActive(true);
        return input;
    }

    IEnumerator CheckAdvancedClip(string label, Color32[] baseline, string report, Vector3 expected, bool sdf = false)
    {
        yield return WaitForOceanUpdates(4);
        var data = AsyncGPUReadback.Request(OceanRenderer.Instance._lodDataClipSurface.DataTexture, 0, TextureFormat.RFloat);
        while (!data.done) yield return null;
        Assert.That(data.hasError, Is.False);
        float left = ClipSample(data, new Vector2(-3, 0), out var leftTexel);
        float right = ClipSample(data, new Vector2(3, 0), out var rightTexel);
        float outside = ClipSample(data, new Vector2(11, 0), out _);
        var frame = Capture(label, "P13-AdvancedClip");
        int hole = Magenta(frame);
        int changes = Different(baseline, frame);
        File.AppendAllText(report, $"{label}: left={left}, right={right}, outside={outside}, hole={hole}, changed={changes}\n");
        if (sdf)
        {
            expected.x = ExpectedClip(0, leftTexel + Vector2.right * 2);
            if (label == "OverlappingSdf") expected.x = Mathf.Min(expected.x, 1 - ExpectedClip(0, leftTexel - Vector2.right * 2));
            expected.y = label == "OverlappingSdf"
                ? 1 - ExpectedClip(0, rightTexel - Vector2.right * 2)
                : ExpectedClip(0, rightTexel + Vector2.right * 2);
        }
        Assert.That(left, Is.EqualTo(expected.x).Within(.005f), label);
        Assert.That(right, Is.EqualTo(expected.y).Within(.005f), label);
        Assert.That(outside, Is.EqualTo(expected.z).Within(.005f), label);
        Assert.That(Different(baseline, frame, borderOnly: true), Is.Zero, label);
        if (expected.x > .5f || expected.y > .5f)
            Assert.That(hole, Is.GreaterThan(500), label);
        else
            Assert.That(changes, Is.LessThan(20), label);
        if (label == "OverlappingSdf")
        {
            float overlap = ClipSample(data, Vector2.zero, out var texel);
            float expectedOverlap = 1 - ExpectedClip(0, texel - Vector2.right * 2);
            File.AppendAllText(report, $"Overlap={overlap}, expected={expectedOverlap}\n");
            Assert.That(overlap, Is.EqualTo(expectedOverlap).Within(.005f));
            Assert.That(overlap, Is.LessThan(.5f));
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
    Color32[] Capture(string label, string prefix = "ClipSurface")
    {
        RenderPipeline.SubmitRenderRequest(_camera, new UniversalRenderPipeline.SingleCameraRequest { destination = _target });
        var previous = RenderTexture.active;
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = _target;
            texture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            texture.Apply();
            File.WriteAllBytes($"Evidence/{prefix}-{label}-{SystemInfo.graphicsDeviceType}.png", texture.EncodeToPNG());
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
        if (_depthSettings != null) Object.DestroyImmediate(_depthSettings);
        foreach (var material in _materials) if (material != null) Object.DestroyImmediate(material);
        _materials.Clear();
    }
}
