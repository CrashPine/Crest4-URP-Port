using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Crest;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

// Observe the actual ocean update; never calls UpdateTransforms or changes its buffers.
[DefaultExecutionOrder(11000)]
public class LodTransitionObservation : MonoBehaviour
{
    struct Previous { public Vector3 Center; public float Texel, Wavelength; public int Frame; }
    Previous[] _previous;
    float _previousScale, _previousAlpha;
    public bool Record;
    public int Version, SeenVersion, Samples, Up, Down, LargeJumps, LaggedShaderFrames;
    public float MaxSnapError, MaxHistoryError;
    public string Report;

    void LateUpdate()
    {
        if (!Record) return;
        var ocean = OceanRenderer.Instance;
        var camera = ocean.ViewCamera;
        float level = Mathf.Clamp(Mathf.Abs(camera.transform.position.y - ocean.SeaLevel) - 4, 8, 1.99f * 256);
        double exponent = Math.Floor(Math.Log(level, 2));
        float expectedScale = (float)Math.Pow(2, exponent);
        Assert.That(ocean.Scale, Is.EqualTo(expectedScale), "Flat-water camera band and configured min/max scale.");
        Assert.That(ocean.ViewerAltitudeLevelAlpha, Is.EqualTo((float)(Math.Log(level, 2) - exponent)).Within(.00001f));
        Assert.That(ocean.Root.localScale, Is.EqualTo(new Vector3(expectedScale, 1, expectedScale)));
        Assert.That(ocean.ScaleCouldIncrease, Is.EqualTo(expectedScale < 256 * .99f));
        Assert.That(ocean.ScaleCouldDecrease, Is.EqualTo(expectedScale > 8 * 1.01f));
        var expectedRoot = camera.transform.position;
        expectedRoot.y = ocean.SeaLevel;
        if (Math.Abs(expectedRoot.x * 60 - Math.Round(expectedRoot.x * 60)) < .001) expectedRoot.x += .002f;
        if (Math.Abs(expectedRoot.z * 60 - Math.Round(expectedRoot.z * 60)) < .001) expectedRoot.z += .002f;
        Assert.That(Vector3.Distance(ocean.Root.position, expectedRoot), Is.LessThan(.000001f), "Original grid-precision nudge contract.");
        int difference = _previous == null ? 0 : (int)Math.Round(Math.Log(expectedScale / _previousScale, 2));
        if (_previous != null)
        {
            Assert.That(ocean._lodTransform.ScaleDifferencePow2, Is.EqualTo(difference));
            Assert.That(Shader.GetGlobalFloat("_CrestLodChange"), Is.EqualTo(difference));
            if (difference > 0) Up++; if (difference < 0) Down++; if (Math.Abs(difference) > 1) LargeJumps++;
            float shaderAlpha = Shader.GetGlobalFloat("_MeshScaleLerp");
            // Original global is set before LateUpdateScale, using the previous update's altitude/scale.
            float expectedShaderAlpha = _previousScale < 256 * .99f ? _previousAlpha : 0;
            Assert.That(shaderAlpha, Is.EqualTo(expectedShaderAlpha).Within(.00001f));
            if (Mathf.Abs(shaderAlpha - (ocean.ScaleCouldIncrease ? ocean.ViewerAltitudeLevelAlpha : 0)) > .001f) LaggedShaderFrames++;
        }
        var next = new Previous[ocean.CurrentLodCount];
        for (int lod = 0; lod < next.Length; lod++)
        {
            var buffer = ocean._lodTransform._renderData[lod];
            var current = buffer.Current;
            float scale = expectedScale * (1 << lod);
            float texel = 4 * scale / ocean.LodDataResolution;
            Assert.That(current._texelWidth, Is.EqualTo(texel).Within(.000001f));
            Assert.That(current._textureRes, Is.EqualTo(ocean.LodDataResolution));
            Assert.That(current._maxWavelength, Is.EqualTo(4 * texel).Within(.000001f));
            Assert.That(current._frame, Is.EqualTo(OceanRenderer.FrameCount));
            var snapped = new Vector3((float)(Math.Floor((double)expectedRoot.x / texel) * texel), expectedRoot.y,
                (float)(Math.Floor((double)expectedRoot.z / texel) * texel));
            float error = Vector3.Distance(current._posSnapped, snapped);
            Assert.That(float.IsFinite(error) && error < .0001f, Is.True, "Independent floor-to-texel centre, including negative coordinates.");
            MaxSnapError = Mathf.Max(MaxSnapError, error);
            Assert.That(current.RectXZ.width, Is.EqualTo(4 * scale).Within(.0001f));
            Assert.That(current.RectXZ.height, Is.EqualTo(4 * scale).Within(.0001f));
            if (_previous != null)
            {
                Assert.That(buffer.Size, Is.GreaterThanOrEqualTo(2));
                var previous = buffer.Previous(1);
                float historyError = Vector3.Distance(previous._posSnapped, _previous[lod].Center);
                MaxHistoryError = Mathf.Max(MaxHistoryError, historyError);
                Assert.That(historyError, Is.LessThan(.000001f));
                Assert.That(previous._texelWidth, Is.EqualTo(_previous[lod].Texel));
                Assert.That(previous._maxWavelength, Is.EqualTo(_previous[lod].Wavelength));
                Assert.That(previous._frame, Is.EqualTo(_previous[lod].Frame));
            }
            var center = ocean._lodTransform.GetProjectionMatrix(lod) * ocean._lodTransform.GetWorldToCameraMatrix(lod) * new Vector4(current._posSnapped.x, current._posSnapped.y, current._posSnapped.z, 1);
            Assert.That(float.IsFinite(center.x) && float.IsFinite(center.y) && float.IsFinite(center.z), Is.True);
            Assert.That(Mathf.Abs(center.x / center.w), Is.LessThan(.0001f));
            Assert.That(Mathf.Abs(center.y / center.w), Is.LessThan(.0001f));
            next[lod] = new Previous { Center = current._posSnapped, Texel = current._texelWidth, Wavelength = current._maxWavelength, Frame = current._frame };
        }
        _previous = next; _previousScale = expectedScale; _previousAlpha = ocean.ViewerAltitudeLevelAlpha;
        SeenVersion = Version; Samples++;
        File.AppendAllText(Report, $"Update; Version={Version}; Frame={OceanRenderer.FrameCount}; Camera={camera.transform.position:F7}; Root={ocean.Root.position:F7}; Scale={ocean.Scale:R}; DifferencePow2={difference}; Alpha={ocean.ViewerAltitudeLevelAlpha:R}; ShaderAlpha={Shader.GetGlobalFloat("_MeshScaleLerp"):R}\n");
    }
}

public class LodTransitionTests
{
    float _scale;
    int _capture;
    LodTransitionObservation _observation;
    SimSettingsSeaFloorDepth _depthSettings;
    string _report;
    int _states, _queryFrames, _texelChecks;
    readonly HashSet<float> _scales = new HashSet<float>();
    readonly Vector3[] _points = new Vector3[3], _normals = new Vector3[3], _velocities = new Vector3[3];
    readonly float[] _heights = new float[3];
    static readonly FieldInfo ResultTime = typeof(QueryBase).GetField("_queryResultsTime", BindingFlags.Instance | BindingFlags.NonPublic);

    [UnityTest]
    public IEnumerator CameraSnapScaleHistoryAndFlatQueriesSurviveSignedBoundaryCrossings()
    {
        _scale = Time.timeScale; _capture = Time.captureFramerate;
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        Time.timeScale = 1; Time.captureFramerate = 60;
        var ocean = OceanRenderer.Instance;
        Camera.main.transform.position = new Vector3(.017377f, 4, -.021331f);
        foreach (var shape in UnityEngine.Object.FindObjectsByType<ShapeWaves>()) shape.enabled = false;
        Assert.That(ocean.SeaLevel, Is.Zero);
        Assert.That((float)typeof(OceanRenderer).GetField("_minScale", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ocean), Is.EqualTo(8));
        Assert.That((float)typeof(OceanRenderer).GetField("_maxScale", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ocean), Is.EqualTo(256));
        _depthSettings = ScriptableObject.CreateInstance<SimSettingsSeaFloorDepth>();
        _depthSettings._allowVaryingWaterLevel = false;
        ocean._simSettingsSeaFloorDepth = _depthSettings;
        new GameObject("LOD numerical query fence").AddComponent<BoatProbesQueryFence>();
        for (int i = 0; i < 8; i++) yield return null;
        Assert.That(ocean._lodDataSeaDepths == null || !ocean._lodDataSeaDepths.Settings._allowVaryingWaterLevel, Is.True, "Isolate scale selection from local-level smoothing.");
        _report = $"Evidence/LodTransitions-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report, "Original OceanRenderer/LodTransform, flat known-zero field after disabling wave generators. MinScale8/max256/resolution unchanged. Passive post-ocean observer validates signed floor snapping, original root nudge, powers-of-two/clamps/band4m, Current/Previous history and scale delta. Global MeshScaleLerp retains original previous-update phase; this is disclosed, not changed. Every ready public height/normal/velocity query checked; requires fresh recovery per state. Native GPU RGB finite/zero on every cascade per state. Test-only readback fence, not latency, nonzero-wave history transport or visual seam acceptance.\n");
        _observation = ocean.gameObject.AddComponent<LodTransitionObservation>();
        _observation.Report = _report; _observation.Record = true;
        float t = 32f / ocean.LodDataResolution;
        foreach (float boundary in new[] { -1.001f, -1f, -.999f, -.001f, 0f, .001f, .999f, 1f, 1.001f })
            yield return State(new Vector3(boundary * t, 4, -boundary * t), "SignedSnap");
        foreach (float height in new[] { 19.99f, 20, 20.01f, 35.99f, 36, 36.01f, 67.99f, 68, 132, 516, -516, -260, -68, -36, -20, -19.99f, 4 })
            yield return State(new Vector3(.017377f, height, -.021331f), "HeightScale");
        File.AppendAllText(_report, $"Summary; States={_states}; ObservedUpdates={_observation.Samples}; Scales={string.Join(",", _scales)}; ScaleUps={_observation.Up}; ScaleDowns={_observation.Down}; MultiOctaveJumps={_observation.LargeJumps}; OriginalShaderAlphaLagFrames={_observation.LaggedShaderFrames}; QueryFrames={_queryFrames}; CheckedNativeTexels={_texelChecks}; MaxSnapError={_observation.MaxSnapError:R}; MaxHistoryError={_observation.MaxHistoryError:R}\n");
        Assert.That(_states, Is.EqualTo(26));
        Assert.That(_scales.SetEquals(new[] { 8f, 16, 32, 64, 128, 256 }), Is.True);
        Assert.That(_observation.Up, Is.GreaterThanOrEqualTo(4));
        Assert.That(_observation.Down, Is.GreaterThanOrEqualTo(4));
        Assert.That(_observation.LargeJumps, Is.GreaterThan(0));
        Assert.That(_observation.LaggedShaderFrames, Is.GreaterThan(0));
        _observation.Record = false;
    }

    IEnumerator State(Vector3 position, string phase)
    {
        var ocean = OceanRenderer.Instance;
        Camera.main.transform.position = position;
        int version = ++_observation.Version;
        int start = _observation.Samples, ready = 0;
        float after = Time.time, last = -1, deadline = Time.realtimeSinceStartup + 10;
        for (int i = 0; i < 3; i++) _points[i] = new Vector3(position.x + i - 1, 0, position.z);
        while (ready < 4 && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            int status = ocean.CollisionProvider.Query(GetHashCode(), 0, _points, _heights, _normals, _velocities);
            if (status != 0) continue;
            for (int i = 0; i < 3; i++)
            {
                Assert.That(float.IsFinite(_heights[i]) && Mathf.Abs(_heights[i]) < .0001f, Is.True);
                Assert.That(Vector3.Distance(_normals[i], Vector3.up), Is.LessThan(.0001f));
                Assert.That(_velocities[i].magnitude, Is.LessThan(.0001f));
            }
            _queryFrames++;
            float time = (float)ResultTime.GetValue(ocean.CollisionProvider);
            if (_observation.SeenVersion == version && _observation.Samples > start + 1 && time >= after && time > last) { ready++; last = time; }
        }
        Assert.That(ready, Is.EqualTo(4), "Fresh positive-dt query recovery after camera transition.");
        _scales.Add(ocean.Scale);
        var request = AsyncGPUReadback.Request(ocean._lodDataAnimWaves.DataTexture, 0, TextureFormat.RGBAFloat);
        request.WaitForCompletion();
        Assert.That(request.hasError, Is.False);
        for (int lod = 0; lod < ocean.CurrentLodCount; lod++)
        {
            bool finite = true; float peak = 0;
            foreach (var pixel in request.GetData<Color>(lod))
            {
                finite &= float.IsFinite(pixel.r) && float.IsFinite(pixel.g) && float.IsFinite(pixel.b);
                peak = Mathf.Max(peak, Mathf.Abs(pixel.r), Mathf.Abs(pixel.g), Mathf.Abs(pixel.b));
                _texelChecks++;
            }
            Assert.That(finite, Is.True);
            Assert.That(peak, Is.LessThan(.0001f));
        }
        _states++;
        File.AppendAllText(_report, $"State={phase}; Position={position:F7}; Scale={ocean.Scale:R}; Fresh={ready}; FieldLayers={ocean.CurrentLodCount}\n");
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (_observation != null) _observation.Record = false;
        Time.timeScale = _scale; Time.captureFramerate = _capture;
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (_depthSettings != null) UnityEngine.Object.DestroyImmediate(_depthSettings);
    }
}
