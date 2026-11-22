using System.Collections;
using System.IO;
using System.Collections.Generic;
using Crest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

public class SplineGeometryTests
{
    Material _surface, _heightMaterial, _markerMaterial;
    Mesh _verificationMesh;
    SimSettingsSeaFloorDepth _depthSettings;
    float _timeScale = 1;
    string _report;

    void OpenScene()
    {
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
    }

    IEnumerator Configure(bool flow)
    {
        _timeScale = Time.timeScale;
        Time.timeScale = 0;
        var ocean = OceanRenderer.Instance;
        Camera.main.transform.position = new Vector3(0, 4, 0);
        foreach (var fft in Object.FindObjectsByType<ShapeFFT>()) fft.enabled = false;
        _surface = new Material(ocean.OceanMaterial);
        if (flow) { _surface.EnableKeyword("_FLOW_ON"); _surface.SetFloat("_Flow", 1); }
        ocean.OceanMaterial = _surface;
        _depthSettings = ScriptableObject.CreateInstance<SimSettingsSeaFloorDepth>();
        _depthSettings._allowVaryingWaterLevel = true;
        ocean._simSettingsSeaFloorDepth = _depthSettings;
        var configuration = new SerializedObject(ocean);
        configuration.FindProperty("_createFlowSim").boolValue = flow;
        configuration.ApplyModifiedPropertiesWithoutUndo();
        yield return WaitForOceanUpdates(4);
        var clock = ocean.gameObject.AddComponent<TimeProviderCustom>();
        clock._overrideTime = clock._overrideDeltaTime = true;
        clock._time = clock._deltaTime = 0;
        ocean.PushTimeProvider(clock);
    }

    [UnityTest]
    public IEnumerator ClosedSplineInterpolatesPointSpeedAndWidthAcrossTheSeam()
    {
        OpenScene();
        yield return new EnterPlayMode();
        yield return Configure(true);
        var owner = new GameObject("Variable flow loop");
        var spline = owner.AddComponent<Crest.Spline.Spline>();
        spline.Radius = 2;
        spline.Subdivisions = 6;
        spline._offset = Crest.Spline.Spline.Offset.Center;
        spline._closed = true;
        float[] speeds = { 1, 3, 5, 2 };
        float[] widths = { 1, 1.5f, 1, 1.5f };
        for (int i = 0; i < speeds.Length; i++)
        {
            var point = new GameObject($"Knot {i}");
            point.transform.SetParent(owner.transform, false);
            var radial = Polar(i * 90) * 8;
            point.transform.position = new Vector3(radial.x, 0, radial.y);
            point.AddComponent<Crest.Spline.SplinePoint>();
            point.AddComponent<SplinePointDataFlow>().FlowVelocity = speeds[i];
            point.AddComponent<SplinePointData>().RadiusMultiplier = widths[i];
        }

        owner.AddComponent<RegisterFlowInput>();
        spline.UpdateSpline();
        yield return WaitForOceanUpdates(4);

        Vector3[] vertices = null;
        Assert.That(ShapeGerstnerSplineHandling.GenerateMeshFromSpline<SplinePointDataFlow>(
            spline, owner.transform, spline.Subdivisions, spline.Radius, Vector2.zero,
            ref _verificationMesh, out _, out _, ref vertices), Is.True);
        var data = new List<Vector2>();
        _verificationMesh.GetUVs(2, data);
        float firstWidth = Vector3.Distance(vertices[0], vertices[1]);
        float lastWidth = Vector3.Distance(vertices[^2], vertices[^1]);
        string report = $"Evidence/P13-ClosedSpline-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(report, $"StartSpeed={data[0].x:R}; EndSpeed={data[^1].x:R}; StartWidth={firstWidth:R}; EndWidth={lastWidth:R}\n");

        var request = ReadFlow();
        while (!request.done) yield return null;
        Assert.That(request.hasError, Is.False);
        var target = OceanRenderer.Instance._lodDataFlow.DataTexture;
        var field = request.GetData<Vector2>(0);
        float maximumError = 0;
        foreach (float angle in new[] { -3f, 0, 3, 45, 90, 135, 180, 225, 270, 315 })
        {
            float segment = Mathf.Repeat(angle, 360) / 90;
            int knot = Mathf.FloorToInt(segment);
            float speed = Mathf.Lerp(speeds[knot], speeds[(knot + 1) % speeds.Length], Mathf.SmoothStep(0, 1, segment - knot));
            var radial = Polar(angle);
            var expected = new Vector2(-radial.y, radial.x) * speed;
            var actual = Sample(field, target, radial * 8);
            float error = Vector2.Distance(expected, actual);
            maximumError = Mathf.Max(maximumError, error);
            File.AppendAllText(report, $"Angle={angle:R}; ExpectedSpeed={speed:R}; ActualSpeed={actual.magnitude:R}; VectorError={error:R}\n");
        }

        Assert.That(data[0].x, Is.EqualTo(speeds[0]));
        Assert.That(data[^1].x, Is.EqualTo(speeds[0]), "The closing segment must return to knot zero.");
        Assert.That(firstWidth, Is.EqualTo(2).Within(.03f));
        Assert.That(lastWidth, Is.EqualTo(firstWidth).Within(.03f));
        Assert.That(maximumError, Is.LessThan(.15f));
    }

    [UnityTest]
    public IEnumerator CurvedClosedFlowHasTangentVectorsAndNoSeamGap()
    {
        OpenScene();
        yield return new EnterPlayMode();
        yield return Configure(true);
        _report = $"Evidence/CurvedFlow-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report, "Four cardinal control points at radius 8m; original cubic hull tm=0.39 approximates a circle. Constant speed=3m/s, centered width=2m, subdivisions=5. 64 angular probes plus +/-1 degree around seam; vector tolerance=0.12m/s.\n");
        var owner = new GameObject("Curved flow loop");
        var spline = owner.AddComponent<Crest.Spline.Spline>();
        spline.Radius = 2;
        spline.Subdivisions = 5;
        spline._offset = Crest.Spline.Spline.Offset.Center;
        spline._closed = true;
        var points = new Transform[4];
        var speeds = new SplinePointDataFlow[4];
        for (int i = 0; i < 4; i++)
        {
            points[i] = new GameObject("Loop point " + i).transform;
            points[i].SetParent(owner.transform, false);
            float angle = i * Mathf.PI / 2;
            points[i].position = new Vector3(8 * Mathf.Cos(angle), 0, 8 * Mathf.Sin(angle));
            points[i].gameObject.AddComponent<Crest.Spline.SplinePoint>();
            speeds[i] = points[i].gameObject.AddComponent<SplinePointDataFlow>();
            speeds[i].FlowVelocity = 3;
        }
        var input = owner.AddComponent<RegisterFlowInput>();
        spline.UpdateSpline();
        yield return WaitForOceanUpdates(4);
        yield return CheckLoop("Closed", Vector2.zero, 3);
        spline._closed = false;
        spline.UpdateSpline();
        yield return WaitForOceanUpdates(4);
        var open = ReadFlow();
        while (!open.done) yield return null;
        Assert.That(open.hasError, Is.False);
        var openValues = open.GetData<Vector2>(0);
        var arc = Polar(135);
        var actual = Sample(openValues, OceanRenderer.Instance._lodDataFlow.DataTexture, arc * 8);
        Assert.That(Vector2.Distance(actual, new Vector2(-arc.y, arc.x) * 3), Is.LessThan(.12f));
        var gap = Sample(openValues, OceanRenderer.Instance._lodDataFlow.DataTexture, Polar(315) * 8);
        Assert.That(gap.magnitude, Is.LessThan(.0001f));
        File.AppendAllText(_report, $"OpenArc135={actual}; MissingClosingArc315={gap}\n");
        SaveFlowImage(openValues, "Open");
        spline._closed = true;
        foreach (var point in points) point.position += Vector3.right * 3;
        spline.UpdateSpline();
        yield return WaitForOceanUpdates(4);
        yield return CheckLoop("Moved", new Vector2(3, 0), 3);
        foreach (var speed in speeds) speed.FlowVelocity = -2;
        spline.UpdateSpline();
        yield return WaitForOceanUpdates(4);
        yield return CheckLoop("Reversed", new Vector2(3, 0), -2);
        input.enabled = false;
        yield return WaitForOceanUpdates(4);
        var empty = ReadFlow();
        while (!empty.done) yield return null;
        Assert.That(empty.hasError, Is.False);
        float peak = 0;
        foreach (var value in empty.GetData<Vector2>(0)) peak = Mathf.Max(peak, value.magnitude);
        Assert.That(peak, Is.Zero);
        File.AppendAllText(_report, $"DisabledPeak={peak}\n");
        input.enabled = true;
        yield return WaitForOceanUpdates(4);
        yield return CheckLoop("Reenabled", new Vector2(3, 0), -2);
    }

    static Vector2 Polar(float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
    static AsyncGPUReadbackRequest ReadFlow() => AsyncGPUReadback.Request(OceanRenderer.Instance._lodDataFlow.DataTexture, 0, TextureFormat.RGFloat);

    IEnumerator CheckLoop(string phase, Vector2 center, float speed)
    {
        var target = OceanRenderer.Instance._lodDataFlow.DataTexture;
        var request = ReadFlow();
        while (!request.done) yield return null;
        Assert.That(request.hasError, Is.False);
        var values = request.GetData<Vector2>(0);
        float peak = 0, minimum = float.MaxValue, maxError = 0;
        foreach (var value in values)
        {
            Assert.That(float.IsNaN(value.x) || float.IsInfinity(value.x) || float.IsNaN(value.y) || float.IsInfinity(value.y), Is.False);
            peak = Mathf.Max(peak, value.magnitude);
        }
        for (int i = 0; i < 67; i++)
        {
            float angle = i < 64 ? i * 360f / 64 : (i - 65);
            var radial = Polar(angle);
            var actual = Sample(values, target, center + 8 * radial);
            var expected = speed * new Vector2(-radial.y, radial.x);
            minimum = Mathf.Min(minimum, actual.magnitude);
            maxError = Mathf.Max(maxError, Vector2.Distance(actual, expected));
            Assert.That(Vector2.Distance(actual, expected), Is.LessThan(.12f), phase + " angle " + angle);
        }
        Assert.That(peak, Is.LessThanOrEqualTo(Mathf.Abs(speed) + .01f));
        Assert.That(Sample(values, target, center).magnitude, Is.Zero);
        Assert.That(Sample(values, target, center + new Vector2(12, 0)).magnitude, Is.Zero);
        if (center.x != 0) Assert.That(Sample(values, target, new Vector2(-8, 0)).magnitude, Is.Zero);
        File.AppendAllText(_report, $"Phase={phase}; Samples=67; MinSampleSpeed={minimum}; MaxVectorError={maxError}; PeakSpeed={peak}; CenterAndOutside=0\n");
        SaveFlowImage(values, phase);
    }

    void SaveFlowImage(Unity.Collections.NativeArray<Vector2> values, string phase)
    {
        int width = OceanRenderer.Instance._lodDataFlow.DataTexture.width;
        var pixels = new Color[width * width];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(.5f + values[i].x / 6, .5f + values[i].y / 6, .5f, 1);
        var texture = new Texture2D(width, width, TextureFormat.RGBA32, false);
        try { texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes($"Evidence/CurvedFlow-{phase}-{SystemInfo.graphicsDeviceType}.png", texture.EncodeToPNG()); }
        finally { Object.Destroy(texture); }
    }

    static Vector2 Sample(Unity.Collections.NativeArray<Vector2> values, RenderTexture target, Vector2 position)
    {
        int index = Index(target, position);
        Vector2 result = Vector2.zero;
        for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++) result += values[index + y * target.width + x] / 9;
        return result;
    }

    static int Index(RenderTexture target, Vector2 position)
    {
        var ocean = OceanRenderer.Instance;
        var snapped = ocean._lodTransform._renderData[0].Current._posSnapped;
        float texel = 4 * ocean.CalcLodScale(0) / target.width;
        int x = Mathf.FloorToInt((position.x - snapped.x) / texel + target.width * .5f);
        int y = Mathf.FloorToInt((position.y - snapped.z) / texel + target.height * .5f);
        Assert.That(x, Is.InRange(1, target.width - 2));
        Assert.That(y, Is.InRange(1, target.height - 2));
        return y * target.width + x;
    }

    [UnityTest]
    public IEnumerator SlopedGeometryAndSplineMatchPlaneAndPublicHeightQueries()
    {
        OpenScene();
        yield return new EnterPlayMode();
        yield return Configure(false);
        _report = $"Evidence/SlopedHeight-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report, "World plane y=2+0.25*x, then collinear three-point height spline; RGFloat Y offset tolerance=0.01m at actual texel center; public query tolerance=0.05m, eight consecutive matching ocean frames.\n");
        _heightMaterial = new Material(Shader.Find("Crest/Inputs/Sea Floor Depth/Set Base Water Height Using Geometry"));
        var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = "Sloped local water";
        plane.transform.localScale = Vector3.one * 1.2f;
        plane.transform.SetPositionAndRotation(Vector3.up * 2, Quaternion.Euler(0, 0, Mathf.Atan(.25f) * Mathf.Rad2Deg));
        plane.GetComponent<Renderer>().sharedMaterial = _heightMaterial;
        var input = plane.AddComponent<RegisterHeightInput>();
        yield return WaitForOceanUpdates(4);
        yield return CheckSlope("Geometry", input);
        var bounds = new Rect(-4, -1, 8, 2);
        float minimum = 0, maximum = 0;
        Assert.That(input.ReportHeight(ref bounds, ref minimum, ref maximum), Is.True);
        float range = 6 * Mathf.Sin(Mathf.Atan(.25f));
        Assert.That(minimum, Is.EqualTo(2 - range).Within(.001f));
        Assert.That(maximum, Is.EqualTo(2 + range).Within(.001f));
        File.AppendAllText(_report, $"GeometryBoundsMin={minimum}; Max={maximum}\n");
        input.enabled = false;
        var owner = new GameObject("Sloped height spline");
        var spline = owner.AddComponent<Crest.Spline.Spline>();
        spline.Radius = 4;
        spline.Subdivisions = 4;
        spline._offset = Crest.Spline.Spline.Offset.Center;
        for (int i = 0; i < 3; i++)
        {
            var point = new GameObject("Height point " + i);
            point.transform.SetParent(owner.transform, false);
            point.transform.position = new Vector3(-8 + i * 8, i * 2, 0);
            point.AddComponent<Crest.Spline.SplinePoint>();
        }
        input = owner.AddComponent<RegisterHeightInput>();
        spline.UpdateSpline();
        yield return WaitForOceanUpdates(4);
        yield return CheckSlope("Spline", input);
        Assert.That(input.ReportHeight(ref bounds, ref minimum, ref maximum), Is.True);
        Assert.That(minimum, Is.EqualTo(0).Within(.001f));
        Assert.That(maximum, Is.EqualTo(4).Within(.001f));
        OceanRenderer.Instance.transform.position = Vector3.up;
        yield return WaitForOceanUpdates(4);
        yield return CheckSlope("SplineSeaLevelOne", input);
        input.enabled = false;
        yield return WaitForOceanUpdates(4);
        yield return CheckSlope("Disabled", input, false);
        input.enabled = true;
        yield return WaitForOceanUpdates(4);
        yield return CheckSlope("Reenabled", input);
    }

    IEnumerator CheckSlope(string phase, RegisterHeightInput input, bool enabled = true)
    {
        var ocean = OceanRenderer.Instance;
        Assert.That(OceanChunkRenderer.HeightReporters.Contains(input), Is.EqualTo(enabled));
        var positions = new[] { new Vector2(-4, 0), Vector2.zero, new Vector2(4, 0), new Vector2(0, 8) };
        var target = ocean._lodDataSeaDepths.DataTexture;
        var snapped = ocean._lodTransform._renderData[0].Current._posSnapped;
        float texel = 4 * ocean.CalcLodScale(0) / target.width;
        var request = AsyncGPUReadback.Request(target, 0, TextureFormat.RGFloat);
        while (!request.done) yield return null;
        Assert.That(request.hasError, Is.False);
        var values = request.GetData<Vector2>(0);
        var helpers = new SampleHeightHelper[positions.Length];
        var expected = new float[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            int index = Index(target, positions[i]);
            float x = (index % target.width + .5f - target.width * .5f) * texel + snapped.x;
            expected[i] = enabled && i < 3 ? 2 + .25f * positions[i].x : ocean.SeaLevel;
            float offset = enabled && i < 3 ? 2 + .25f * x - ocean.SeaLevel : 0;
            File.AppendAllText(_report, $"Phase={phase}; Position={positions[i]}; ExpectedOffset={offset}; ActualOffset={values[index].y}\n");
            Assert.That(values[index].y, Is.EqualTo(offset).Within(.01f));
            helpers[i] = new SampleHeightHelper();
        }
        foreach (var value in values)
        {
            Assert.That(float.IsNaN(value.y) || float.IsInfinity(value.y), Is.False);
            if (!enabled) Assert.That(value.y, Is.Zero);
        }
        int matching = 0, previous = -1;
        float deadline = Time.realtimeSinceStartup + 20;
        var heights = new float[positions.Length];
        while (matching < 8 && Time.realtimeSinceStartup < deadline)
        {
            if (OceanRenderer.FrameCount == previous) { yield return null; continue; }
            previous = OceanRenderer.FrameCount;
            bool good = true;
            for (int i = 0; i < positions.Length; i++)
            {
                helpers[i].Init(new Vector3(positions[i].x, 0, positions[i].y));
                good &= helpers[i].Sample(out heights[i]) && !float.IsNaN(heights[i]) && !float.IsInfinity(heights[i]) && Mathf.Abs(heights[i] - expected[i]) < .05f;
            }
            matching = good ? matching + 1 : 0;
            yield return null;
        }
        File.AppendAllText(_report, $"Phase={phase}; ExpectedHeights={string.Join(",", expected)}; QueryHeights={string.Join(",", heights)}; MatchingFrames={matching}\n");
        Assert.That(matching, Is.EqualTo(8));
        yield return CheckSlopeVisibility(phase, positions, expected);
    }

    IEnumerator CheckSlopeVisibility(string phase, Vector2[] positions, float[] heights)
    {
        const int size = 192;
        var ocean = OceanRenderer.Instance;
        var camera = Camera.main;
        camera.transform.SetPositionAndRotation(new Vector3(0, 10, 0), Quaternion.Euler(90, 0, 0));
        camera.orthographic = true;
        camera.orthographicSize = 12;
        // The square request target must match WorldToViewportPoint after URP restores the camera.
        camera.aspect = 1;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        var underwater = camera.GetComponent<UnderwaterRenderer>();
        if (underwater != null) underwater.enabled = false;
        RenderSettings.fog = false;
        _surface.DisableKeyword("_TRANSPARENCY_ON");
        _surface.SetFloat("_Transparency", 0);
        if (_markerMaterial == null)
        {
            _markerMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            _markerMaterial.SetColor("_BaseColor", Color.magenta);
        }
        var markers = new GameObject[positions.Length * 2];
        var target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var previous = RenderTexture.active;
        try
        {
            for (int i = 0; i < positions.Length; i++)
            for (int side = 0; side < 2; side++)
            {
                var marker = markers[i * 2 + side] = GameObject.CreatePrimitive(PrimitiveType.Plane);
                marker.name = "Slope surface occlusion marker";
                marker.transform.localScale = Vector3.one * .08f;
                marker.transform.position = new Vector3(positions[i].x, heights[i] + (side == 0 ? -.25f : .25f), positions[i].y + (side == 0 ? -.7f : .7f));
                marker.GetComponent<Renderer>().sharedMaterial = _markerMaterial;
            }
            target.Create();
            yield return WaitForOceanUpdates(4);
            Assert.That(ocean.Root.GetComponentsInChildren<OceanChunkRenderer>().Length, Is.GreaterThan(0));
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            texture.Apply();
            File.WriteAllBytes($"Evidence/SlopedHeightSurface-{phase}-{SystemInfo.graphicsDeviceType}.png", texture.EncodeToPNG());
            var pixels = texture.GetPixels32();
            for (int i = 0; i < markers.Length; i++)
            {
                var viewport = camera.WorldToViewportPoint(markers[i].transform.position);
                int x = Mathf.FloorToInt(viewport.x * size), y = Mathf.FloorToInt(viewport.y * size);
                Assert.That(x, Is.InRange(1, size - 2));
                Assert.That(y, Is.InRange(1, size - 2));
                int magenta = 0;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var color = pixels[(y + dy) * size + x + dx];
                    if (color.r > 200 && color.b > 200 && color.g < 50) magenta++;
                }
                int expected = i % 2 == 0 ? 0 : 9;
                File.AppendAllText(_report, $"SurfacePhase={phase}; Marker={i}; Height={markers[i].transform.position.y}; MagentaPixels={magenta}; Expected={expected}\n");
                Assert.That(magenta, Is.EqualTo(expected), phase + " marker " + i + " must be occluded below the original ocean surface and visible above it.");
            }
        }
        finally
        {
            RenderTexture.active = previous;
            foreach (var marker in markers) if (marker != null) Object.Destroy(marker);
            target.Release(); Object.Destroy(target); Object.Destroy(texture);
        }
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
        Time.timeScale = _timeScale;
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (_surface != null) Object.DestroyImmediate(_surface);
        if (_heightMaterial != null) Object.DestroyImmediate(_heightMaterial);
        if (_markerMaterial != null) Object.DestroyImmediate(_markerMaterial);
        if (_verificationMesh != null) Object.DestroyImmediate(_verificationMesh);
        if (_depthSettings != null) Object.DestroyImmediate(_depthSettings);
    }
}
