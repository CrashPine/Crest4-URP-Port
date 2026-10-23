using System.Collections;
using System.IO;
using System.Reflection;
using Crest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class LocalWaveSplineTests
{
    const string Parent = "Assets/Crest/URP/Tests/Editor";
    string _folder, _folderGuid;
    OceanWaveSpectrum _spectrum;
    TimeProviderCustom _clock;
    string _report;
    float _timeScale = 1;
    Vector4[] _snapshot;
    Vector4[][] _rawReference;
    int _width;
    float _texel;
    Vector2 _center;

    [UnityTest]
    public IEnumerator LocalGerstnerSplineWeightsFeatherMovementAndQueries()
    {
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        _timeScale = Time.timeScale;
        Time.timeScale = 0;
        Camera.main.transform.position = new Vector3(0, 4, 0);
        foreach (var shape in Object.FindObjectsByType<ShapeWaves>()) shape.enabled = false;
        yield return WaitForOceanUpdates(4);
        var ocean = OceanRenderer.Instance;
        _clock = ocean.gameObject.AddComponent<TimeProviderCustom>();
        _clock._overrideTime = _clock._overrideDeltaTime = true;
        _clock._time = .5f;
        _clock._deltaTime = 0;
        ocean.PushTimeProvider(_clock);
        _report = $"Evidence/LocalWaveSpline-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report, "Original ShapeGerstner + spline geometry shader; FFT off; fixed seed=13, time=0.5/delta=0, chop=0, straight centered ribbon x=-12..12/width=8. Spectrum octaves 4/5 power=-1, other octaves disabled. Weight tolerance=0.002m, feather tolerance=0.01m, public query tolerance=0.05m/eight matching ocean frames.\n");
        yield return Snapshot("Empty");
        Assert.That(Peak(_snapshot), Is.LessThan(.0001f));
        string folderName = "LocalWaveRoundTrip_" + System.Guid.NewGuid().ToString("N");
        _folderGuid = AssetDatabase.CreateFolder(Parent, folderName);
        _folder = Parent + "/" + folderName;
        _spectrum = ScriptableObject.CreateInstance<OceanWaveSpectrum>();
        AssetDatabase.CreateAsset(_spectrum, _folder + "/Spectrum.asset");
        _spectrum._chop = 0;
        _spectrum._waveDirectionVariance = 0;
        var spectrum = new SerializedObject(_spectrum);
        for (int i = 0; i < OceanWaveSpectrum.NUM_OCTAVES; i++)
        {
            spectrum.FindProperty("_powerLog").GetArrayElementAtIndex(i).floatValue = -1;
            spectrum.FindProperty("_powerDisabled").GetArrayElementAtIndex(i).boolValue = i != 4 && i != 5;
        }
        spectrum.ApplyModifiedPropertiesWithoutUndo();
        var owner = new GameObject("Known local Gerstner ribbon");
        owner.SetActive(false);
        var spline = owner.AddComponent<Crest.Spline.Spline>();
        spline.Radius = 8;
        spline.Subdivisions = 4;
        spline._offset = Crest.Spline.Spline.Offset.Center;
        var points = new Transform[2];
        var data = new SplinePointDataWaves[2];
        for (int i = 0; i < 2; i++)
        {
            points[i] = new GameObject("Wave point " + i).transform;
            points[i].SetParent(owner.transform, false);
            points[i].position = new Vector3(i == 0 ? -12 : 12, 0, 0);
            points[i].gameObject.AddComponent<Crest.Spline.SplinePoint>();
            data[i] = points[i].gameObject.AddComponent<SplinePointDataWaves>();
            data[i].Weight = 1;
        }
        var waves = owner.AddComponent<ShapeGerstner>();
        waves._spectrum = _spectrum;
        waves._randomSeed = 13;
        waves._componentsPerOctave = 8;
        waves._overrideGlobalWindSpeed = true;
        waves._windSpeed = 60;
        waves._overrideGlobalWindDirection = true;
        waves._waveDirectionHeadingAngle = 0;
        waves._respectShallowWaterAttenuation = 0;
        SetFeather(waves, 0);
        owner.SetActive(true);
        spline.UpdateSpline();
        yield return WaitForOceanUpdates(5);
        Assert.That(waves.enabled, Is.True);
        yield return Snapshot("Full", true);
        var reference = _snapshot;
        yield return CheckRawFeather(false);
        Assert.That(Peak(reference), Is.GreaterThan(.01f));
        CheckExterior(Vector2.zero);
        var bounds = new Rect(-2, -2, 4, 4);
        float horizontal = 0, vertical = 0;
        Assert.That(waves.ReportDisplacement(ref bounds, ref horizontal, ref vertical), Is.True);
        Assert.That(horizontal, Is.Zero);
        Assert.That(vertical, Is.GreaterThan(.01f));
        Assert.That(OceanChunkRenderer.DisplacementReporters.Contains(waves), Is.True);
        yield return CheckQueries("Full", Vector2.zero);
        foreach (var point in data) point.Weight = .5f;
        spline.UpdateSpline();
        yield return WaitForOceanUpdates(4);
        yield return Snapshot("HalfPointWeights");
        Compare(reference, .5f, .002f, "HalfPointWeights");
        waves._weight = .5f;
        yield return WaitForOceanUpdates(4);
        yield return Snapshot("QuarterCombinedWeight");
        Compare(reference, .25f, .002f, "QuarterCombinedWeight");
        waves._weight = 1;
        data[0].Weight = 0;
        data[1].Weight = 1;
        spline.UpdateSpline();
        yield return WaitForOceanUpdates(4);
        yield return Snapshot("PointRamp");
        float rampError = 0;
        // The 0/1 point ramp is exactly half only at world X=0, between
        // the two central texel columns. Compare bilinear samples there.
        for (int i = 0; i <= 24; i++)
        {
            var position = new Vector2(0, -3 + i * .25f);
            rampError = Mathf.Max(rampError, Mathf.Abs(Sample(_snapshot, position) - .5f * Sample(reference, position)));
        }
        File.AppendAllText(_report, $"PointRampCenterMaxError={rampError}\n");
        Assert.That(rampError, Is.LessThan(.01f));
        foreach (var point in data) point.Weight = 0;
        spline.UpdateSpline();
        yield return WaitForOceanUpdates(4);
        yield return Snapshot("ZeroPointWeights");
        Assert.That(Peak(_snapshot), Is.LessThan(.0001f));
        foreach (var point in data) point.Weight = 1;
        spline.UpdateSpline();
        waves.enabled = false;
        SetFeather(waves, .5f);
        waves.enabled = true;
        yield return WaitForOceanUpdates(4);
        yield return Snapshot("Feathered", true);
        // Feathering happens before the original multi-LOD combine filters.
        // Validate its exact factor in the raster inputs, not after filtering.
        yield return CheckRawFeather(true);
        _clock._time = 1.75f;
        yield return WaitForOceanUpdates(4);
        var featherReference = _snapshot;
        yield return Snapshot("LaterTime");
        float changed = MaxDifference(featherReference, _snapshot, 1);
        Assert.That(changed, Is.GreaterThan(.01f));
        _clock._time = .5f;
        yield return WaitForOceanUpdates(4);
        yield return Snapshot("TimeRestored");
        Compare(featherReference, 1, .002f, "TimeRestored");
        foreach (var point in points) point.position += Vector3.forward * 8;
        spline.UpdateSpline();
        yield return WaitForOceanUpdates(4);
        yield return Snapshot("Moved", true);
        CheckExterior(new Vector2(0, 8));
        Assert.That(Mathf.Abs(Sample(_snapshot, Vector2.zero)), Is.LessThan(.0001f));
        Assert.That(Peak(_snapshot), Is.GreaterThan(.01f));
        yield return CheckQueries("Moved", new Vector2(0, 8));
        var movedReference = _snapshot;
        waves.enabled = false;
        Assert.That(OceanChunkRenderer.DisplacementReporters.Contains(waves), Is.False);
        yield return WaitForOceanUpdates(4);
        yield return Snapshot("Disabled");
        Assert.That(Peak(_snapshot), Is.LessThan(.0001f));
        yield return CheckQueries("Disabled", new Vector2(0, 8));
        waves.enabled = true;
        Assert.That(OceanChunkRenderer.DisplacementReporters.Contains(waves), Is.True);
        yield return WaitForOceanUpdates(4);
        yield return Snapshot("Reenabled");
        Compare(movedReference, 1, .002f, "Reenabled");
        Assert.That(ocean.TimeProvider, Is.SameAs(_clock));
        string prefabPath = _folder + "/LocalWaves.prefab";
        Assert.That(PrefabUtility.SaveAsPrefabAsset(owner, prefabPath), Is.Not.Null);
        AssetDatabase.SaveAssets();
        string prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
        string spectrumGuid = AssetDatabase.AssetPathToGUID(_folder + "/Spectrum.asset");
        var oldGenerated = waves._waveBuffers;
        Assert.That(oldGenerated, Is.Not.Null);
        var oldField = ocean._lodDataAnimWaves.DataTexture;
        yield return SceneManager.LoadSceneAsync("Ocean_Calm");
        Camera.main.transform.position = new Vector3(0, 4, 0);
        foreach (var shape in Object.FindObjectsByType<ShapeWaves>()) shape.enabled = false;
        yield return WaitForOceanUpdates(4);
        Assert.That(owner == null, Is.True);
        Assert.That(oldGenerated == null, Is.True);
        Assert.That(oldField == null, Is.True);
        ocean = OceanRenderer.Instance;
        _clock = ocean.gameObject.AddComponent<TimeProviderCustom>();
        _clock._overrideTime = _clock._overrideDeltaTime = true;
        _clock._time = .5f; _clock._deltaTime = 0;
        ocean.PushTimeProvider(_clock);
        foreach (string path in new[] { _folder + "/Spectrum.asset", prefabPath })
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        owner = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
        waves = owner.GetComponent<ShapeGerstner>();
        Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(waves._spectrum)), Is.EqualTo(spectrumGuid));
        Assert.That(AssetDatabase.AssetPathToGUID(prefabPath), Is.EqualTo(prefabGuid));
        Assert.That(waves._randomSeed, Is.EqualTo(13));
        Assert.That(waves._componentsPerOctave, Is.EqualTo(8));
        Assert.That(new SerializedObject(waves).FindProperty("_featherWidth").floatValue, Is.EqualTo(.5f));
        Assert.That(owner.GetComponent<Crest.Spline.Spline>().Radius, Is.EqualTo(8));
        var reloadedPoints = owner.GetComponentsInChildren<Crest.Spline.SplinePoint>();
        Assert.That(reloadedPoints.Length, Is.EqualTo(2));
        for (int i = 0; i < 2; i++)
        {
            Assert.That(reloadedPoints[i].transform.position, Is.EqualTo(new Vector3(i == 0 ? -12 : 12, 0, 8)));
            Assert.That(reloadedPoints[i].GetComponent<SplinePointDataWaves>().Weight, Is.EqualTo(1));
        }
        yield return WaitForOceanUpdates(5);
        yield return Snapshot("Reloaded", true);
        Compare(movedReference, 1, .002f, "Reloaded");
        yield return CheckQueries("Reloaded", new Vector2(0, 8));
        File.AppendAllText(_report, "PrefabAndSpectrumGUIDsPreserved=True; PointsAndWeightsRestored=True; OldGeneratedAndCombinedRTsReleased=True; RealSceneReload=True\n");
    }

    static void SetFeather(ShapeGerstner waves, float width)
    {
        var settings = new SerializedObject(waves);
        settings.FindProperty("_featherWidth").floatValue = width;
        settings.FindProperty("_featherWaveStart").floatValue = .001f;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    IEnumerator Snapshot(string phase, bool image = false)
    {
        var ocean = OceanRenderer.Instance;
        var target = ocean._lodDataAnimWaves.DataTexture;
        _width = target.width;
        _texel = 4 * ocean.CalcLodScale(0) / _width;
        var snapped = ocean._lodTransform._renderData[0].Current._posSnapped;
        _center = new Vector2(snapped.x, snapped.z);
        var request = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBAFloat);
        while (!request.done) yield return null;
        Assert.That(request.hasError, Is.False);
        _snapshot = request.GetData<Vector4>(0).ToArray();
        float peak = Peak(_snapshot), horizontal = 0;
        foreach (var value in _snapshot)
        {
            Assert.That(float.IsNaN(value.x) || float.IsInfinity(value.x) || float.IsNaN(value.y) || float.IsInfinity(value.y) || float.IsNaN(value.z) || float.IsInfinity(value.z), Is.False);
            horizontal = Mathf.Max(horizontal, Mathf.Max(Mathf.Abs(value.x), Mathf.Abs(value.z)));
        }
        Assert.That(horizontal, Is.LessThan(.0001f));
        File.AppendAllText(_report, $"Phase={phase}; PeakHeight={peak}; PeakHorizontal={horizontal}; OceanTime={ocean.CurrentTime}\n");
        if (!image) yield break;
        var pixels = new Color[_width * _width];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(.5f + _snapshot[i].y / (2 * peak), .5f - _snapshot[i].y / (2 * peak), .5f, 1);
        var texture = new Texture2D(_width, _width, TextureFormat.RGBA32, false);
        try { texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes($"Evidence/LocalWaveSpline-{phase}-{SystemInfo.graphicsDeviceType}.png", texture.EncodeToPNG()); }
        finally { Object.Destroy(texture); }
    }

    IEnumerator CheckRawFeather(bool compare)
    {
        var ocean = OceanRenderer.Instance;
        // Test-only inspection of the existing raster resource; do not alter
        // the combine algorithm or expose a new production API for this test.
        var field = typeof(LodDataMgrAnimWaves).GetField("_waveBuffers", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        var target = (RenderTexture)field.GetValue(ocean._lodDataAnimWaves);
        var request = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBAFloat);
        while (!request.done) yield return null;
        Assert.That(request.hasError, Is.False);
        if (!compare) _rawReference = new Vector4[request.layerCount][];
        float maximum = 0;
        int samples = 0, activeLayers = 0;
        for (int layer = 0; layer < request.layerCount; layer++)
        {
            var values = request.GetData<Vector4>(layer);
            if (!compare) { _rawReference[layer] = values.ToArray(); continue; }
            if (Peak(_rawReference[layer]) < .01f) continue;
            activeLayers++;
            float texel = 4 * ocean.CalcLodScale(layer) / target.width;
            var snapped = ocean._lodTransform._renderData[layer].Current._posSnapped;
            for (int y = 0; y < target.height; y++)
            for (int x = 0; x < target.width; x++)
            {
                float worldX = (x + .5f - target.width * .5f) * texel + snapped.x;
                float worldZ = (y + .5f - target.height * .5f) * texel + snapped.z;
                if (Mathf.Abs(worldX) > 8 || Mathf.Abs(worldZ) > 3.5f) continue;
                int index = y * target.width + x;
                float expected = _rawReference[layer][index].y * (1 - Mathf.Abs(worldZ) / 4);
                maximum = Mathf.Max(maximum, Mathf.Abs(values[index].y - expected));
                samples++;
            }
        }
        if (!compare) yield break;
        File.AppendAllText(_report, $"RawRasterFeatherMaxError={maximum}; Samples={samples}; ActiveLayers={activeLayers}\n");
        Assert.That(activeLayers, Is.GreaterThan(0));
        Assert.That(samples, Is.GreaterThan(1000));
        Assert.That(maximum, Is.LessThan(.01f));
    }

    Vector2 Position(int x, int y) => _center + new Vector2(x + .5f - _width * .5f, y + .5f - _width * .5f) * _texel;
    static float Peak(Vector4[] values) { float peak = 0; foreach (var value in values) peak = Mathf.Max(peak, Mathf.Abs(value.y)); return peak; }
    static float MaxDifference(Vector4[] reference, Vector4[] actual, float weight)
    { float maximum = 0; for (int i = 0; i < actual.Length; i++) maximum = Mathf.Max(maximum, Mathf.Abs(actual[i].y - reference[i].y * weight)); return maximum; }
    void Compare(Vector4[] reference, float weight, float tolerance, string phase)
    {
        float error = MaxDifference(reference, _snapshot, weight);
        File.AppendAllText(_report, $"Phase={phase}; ExpectedScale={weight}; MaxHeightError={error}\n");
        Assert.That(error, Is.LessThan(tolerance));
    }
    void CheckExterior(Vector2 center)
    {
        float peak = 0;
        for (int y = 0; y < _width; y++)
        for (int x = 0; x < _width; x++)
        {
            var p = Position(x, y) - center;
            if (Mathf.Abs(p.x) < 13 && Mathf.Abs(p.y) < 5) continue;
            peak = Mathf.Max(peak, Mathf.Abs(_snapshot[y * _width + x].y));
        }
        File.AppendAllText(_report, $"ExteriorPeak={peak}\n");
        Assert.That(peak, Is.LessThan(.0001f));
    }
    float Sample(Vector4[] values, Vector2 position)
    {
        var coordinate = (position - _center) / _texel + Vector2.one * (_width * .5f - .5f);
        int x = Mathf.FloorToInt(coordinate.x), y = Mathf.FloorToInt(coordinate.y);
        Assert.That(x, Is.InRange(0, _width - 2)); Assert.That(y, Is.InRange(0, _width - 2));
        float a = Mathf.Lerp(values[y * _width + x].y, values[y * _width + x + 1].y, coordinate.x - x);
        float b = Mathf.Lerp(values[(y + 1) * _width + x].y, values[(y + 1) * _width + x + 1].y, coordinate.x - x);
        return Mathf.Lerp(a, b, coordinate.y - y);
    }
    IEnumerator CheckQueries(string phase, Vector2 center)
    {
        Vector2 best = center;
        float peak = 0;
        for (int y = 0; y < _width; y++)
        for (int x = 0; x < _width; x++)
        {
            var p = Position(x, y);
            if (Mathf.Abs(p.x - center.x) > 8 || Mathf.Abs(p.y - center.y) > 2) continue;
            float height = Mathf.Abs(_snapshot[y * _width + x].y);
            if (height <= peak) continue;
            peak = height; best = p;
        }
        var positions = new[] { best, center + new Vector2(0, -7) };
        var expected = new[] { Sample(_snapshot, positions[0]), Sample(_snapshot, positions[1]) };
        var helpers = new[] { new SampleHeightHelper(), new SampleHeightHelper() };
        var actual = new float[2];
        int matched = 0, previous = -1;
        float deadline = Time.realtimeSinceStartup + 20;
        while (matched < 8 && Time.realtimeSinceStartup < deadline)
        {
            if (OceanRenderer.FrameCount == previous) { yield return null; continue; }
            previous = OceanRenderer.FrameCount;
            bool good = true;
            for (int i = 0; i < 2; i++)
            {
                helpers[i].Init(new Vector3(positions[i].x, 0, positions[i].y));
                good &= helpers[i].Sample(out actual[i]) && !float.IsNaN(actual[i]) && !float.IsInfinity(actual[i]) && Mathf.Abs(actual[i] - expected[i]) < .05f;
            }
            matched = good ? matched + 1 : 0;
            yield return null;
        }
        File.AppendAllText(_report, $"QueryPhase={phase}; Position={best}; Expected={string.Join(",", expected)}; Actual={string.Join(",", actual)}; MatchingFrames={matched}\n");
        Assert.That(matched, Is.EqualTo(8));
    }
    static IEnumerator WaitForOceanUpdates(int count)
    {
        int previous = BuildCommandBufferBase._lastUpdateFrame, completed = 0;
        float deadline = Time.realtimeSinceStartup + 15;
        while (completed < count && Time.realtimeSinceStartup < deadline)
        { yield return null; int frame = BuildCommandBufferBase._lastUpdateFrame; if (frame == previous) continue; previous = frame; completed++; }
        Assert.That(completed, Is.EqualTo(count));
    }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = _timeScale;
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (_spectrum != null && !EditorUtility.IsPersistent(_spectrum)) Object.DestroyImmediate(_spectrum);
        if (!string.IsNullOrEmpty(_folder))
        {
            Assert.That(Path.GetFullPath(_folder), Does.StartWith(Path.GetFullPath(Parent) + Path.DirectorySeparatorChar));
            Assert.That(AssetDatabase.AssetPathToGUID(_folder), Is.EqualTo(_folderGuid));
            Assert.That(AssetDatabase.DeleteAsset(_folder), Is.True);
            _folder = _folderGuid = null;
        }
    }
}
