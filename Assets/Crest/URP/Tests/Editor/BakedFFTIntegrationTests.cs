using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Crest;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class BakedFFTIntegrationTests
{
    const string Parent = "Assets/Crest/URP/Tests/Editor";
    string _folder, _guid, _report;
    OceanWaveSpectrum _spectrum;
    FFTBakedData _data;
    SimSettingsAnimatedWaves _settings;
    float _scale;

    [UnityTest]
    public IEnumerator SettingsSelectCPUChoppyHeightInvertsAndSwitchesReleaseProviders()
    {
        _scale = Time.timeScale;
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        Time.timeScale = 0;
        var ocean = OceanRenderer.Instance;
        Camera.main.transform.position = new Vector3(0, 4, 0);
        foreach (var shape in Object.FindObjectsByType<ShapeWaves>()) shape.enabled = false;
        ocean._globalWindZone = null;
        ocean._globalWindSpeed = 60;
        _folder = Parent + "/BakedIntegration_" + Guid.NewGuid().ToString("N");
        _guid = AssetDatabase.CreateFolder(Parent, Path.GetFileName(_folder));
        _spectrum = ScriptableObject.CreateInstance<OceanWaveSpectrum>();
        _spectrum._chop = 1.6f;
        var powers = new SerializedObject(_spectrum);
        for (int i = 0; i < OceanWaveSpectrum.NUM_OCTAVES; i++)
        {
            powers.FindProperty("_powerDisabled").GetArrayElementAtIndex(i).boolValue = i != 7;
            powers.FindProperty("_powerLog").GetArrayElementAtIndex(i).floatValue = -2;
        }
        powers.ApplyModifiedPropertiesWithoutUndo();
        var owner = new GameObject("Choppy offline FFT");
        owner.SetActive(false);
        var fft = owner.AddComponent<ShapeFFT>();
        fft._spectrum = _spectrum;
        fft._resolution = 16;
        fft._enableBakedCollision = true;
        fft._timeLoopLength = 4;
        fft._timeResolution = 4;
        fft._smallestWavelengthRequired = 8;
        fft._overrideGlobalWindSpeed = true;
        fft._windSpeed = 60;
        _data = FFTBaker.BakeShapeFFT(fft, _folder + "/Choppy.asset");
        Assert.That(_data, Is.Not.Null);
        _settings = ScriptableObject.CreateInstance<SimSettingsAnimatedWaves>();
        _settings.CollisionSource = SimSettingsAnimatedWaves.CollisionSources.BakedFFT;
        _settings._bakedFFTData = _data;
        string settingsPath = _folder + "/Settings.asset";
        AssetDatabase.CreateAsset(_settings, settingsPath);
        AssetDatabase.SaveAssets();
        string settingsGuid = AssetDatabase.AssetPathToGUID(settingsPath);
        string dataGuid = AssetDatabase.AssetPathToGUID(_folder + "/Choppy.asset");
        Resources.UnloadAsset(_settings);
        AssetDatabase.ImportAsset(settingsPath, ImportAssetOptions.ForceUpdate);
        _settings = AssetDatabase.LoadAssetAtPath<SimSettingsAnimatedWaves>(settingsPath);
        Assert.That(AssetDatabase.AssetPathToGUID(settingsPath), Is.EqualTo(settingsGuid));
        Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_settings._bakedFFTData)), Is.EqualTo(dataGuid));
        _data = _settings._bakedFFTData;
        _report = $"Evidence/BakedIntegration-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report, $"Real FFT bake chop1.6, settings asset unload/reimport, automatic OceanRenderer provider selection. SettingsGuid={settingsGuid}; DataGuid={dataGuid}. CPU reference uses scalar raw baked frames and independent Newton inversion, never FFTBakedData.SampleHeightXZT. Time.timeScale=0 is valid for the CPU provider's ocean-time derivative, not GPU velocity.\n");
        var oldGPU = ocean.CollisionProvider as QueryDisplacements;
        Assert.That(oldGPU, Is.Not.Null);
        var oldQueries = (ComputeBuffer)typeof(QueryBase).GetField("_computeBufQueries", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(oldGPU);
        ocean._simSettingsAnimatedWaves = _settings;
        yield return WaitForProvider(typeof(CollProviderBakedFFT));
        Assert.That(oldQueries.IsValid(), Is.False, "Settings rebuild must release the old GPU provider.");
        Assert.That(((CollProviderBakedFFT)ocean.CollisionProvider)._data, Is.SameAs(_data));
        File.AppendAllText(_report, "Selection=CPU; OldGPUBufferReleased=True\n");
        yield return CheckCPU("InitialCPU", new[] { 0f, .125f, 1f, 3.875f, 4.125f, -.125f });
        var oldCPU = ocean.CollisionProvider;
        var queryData = oldCPU.GetType().GetField("_queryDataHeights", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(oldCPU);
        var oldPositions = (NativeArray<float4>[])queryData.GetType().GetField("_queryPositionQuadsX").GetValue(queryData);
        _settings.CollisionSource = SimSettingsAnimatedWaves.CollisionSources.None;
        yield return WaitForProvider(typeof(CollProviderNull));
        foreach (var positions in oldPositions) Assert.That(positions.IsCreated, Is.False);
        File.AppendAllText(_report, "Selection=None; OldCPUPositionArraysReleased=True\n");
        _settings.CollisionSource = SimSettingsAnimatedWaves.CollisionSources.ComputeShaderQueries;
        yield return WaitForProvider(typeof(QueryDisplacements));
        File.AppendAllText(_report, "Selection=GPU; QueryDisplacementsCreated=True\n");
        _settings.CollisionSource = SimSettingsAnimatedWaves.CollisionSources.BakedFFT;
        yield return WaitForProvider(typeof(CollProviderBakedFFT));
        ocean.transform.position += Vector3.up * 1.25f;
        yield return null;
        Assert.That(ocean.SeaLevel, Is.EqualTo(1.25f).Within(.001f));
        yield return CheckCPU("ReturnedCPUSeaLevel125", new[] { .125f });
        var previousOcean = ocean;
        yield return SceneManager.LoadSceneAsync("Ocean_Calm");
        Assert.That(previousOcean == null, Is.True);
        ocean = OceanRenderer.Instance;
        Camera.main.transform.position = new Vector3(0, 4, 0);
        foreach (var shape in Object.FindObjectsByType<ShapeWaves>()) shape.enabled = false;
        ocean._globalWindZone = null;
        ocean._globalWindSpeed = 60;
        ocean._simSettingsAnimatedWaves = _settings;
        yield return WaitForProvider(typeof(CollProviderBakedFFT));
        Assert.That(((CollProviderBakedFFT)ocean.CollisionProvider)._data, Is.SameAs(_data));
        yield return CheckCPU("ReloadedOcean", new[] { .125f });
        File.AppendAllText(_report, "Ocean reloaded and settings reassigned; reference asset preserved. This is not saved-scene binding acceptance.\n");
    }

    IEnumerator WaitForProvider(Type type)
    {
        float deadline = Time.realtimeSinceStartup + 15;
        while (Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            if (OceanRenderer.Instance.CollisionProvider?.GetType() == type) yield break;
        }
        Assert.Fail("Automatic collision settings rebuild did not select " + type.Name);
    }

    IEnumerator CheckCPU(string phase, float[] times)
    {
        var ocean = OceanRenderer.Instance;
        var clock = ocean.gameObject.AddComponent<TimeProviderCustom>();
        clock._overrideTime = clock._overrideDeltaTime = true;
        clock._deltaTime = 0;
        ocean.PushTimeProvider(clock);
        var points = new[] { new Vector3(-21.5f, 0, -3.25f), Vector3.zero, new Vector3(.5f, 0, .5f), new Vector3(4.2f, 0, -7.3f), new Vector3(13.1f, 0, 22.7f) };
        var heights = new float[5];
        var normals = new Vector3[5];
        var velocities = new Vector3[5];
        var displacements = new Vector3[5];
        float maxHeightError = 0, maxNormalError = 0, maxVelocityError = 0, maxDisplacementError = 0, horizontalPeak = 0;
        foreach (float time in times)
        {
            clock._time = time;
            int ready = 0;
            float deadline = Time.realtimeSinceStartup + 15;
            while (ready < 4 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                int hs = ocean.CollisionProvider.Query(GetHashCode(), 0, points, heights, normals, velocities);
                int ds = ocean.CollisionProvider.Query(~GetHashCode(), 0, points, displacements, null, null);
                ready = hs == 0 && ds == 0 ? ready + 1 : 0;
            }
            Assert.That(ready, Is.EqualTo(4));
            for (int i = 0; i < points.Length; i++)
            {
                var point = new Vector2(points[i].x, points[i].z);
                float h = Height(point, time);
                var expectedNormal = new Vector3(h - Height(point + Vector2.right * .1f, time), .1f, h - Height(point + Vector2.up * .1f, time)).normalized;
                float expectedVelocity = (h - Height(point, time - .06f)) / .06f;
                var expectedDisplacement = Displacement(point, time);
                float he = Mathf.Abs(heights[i] - (h + ocean.SeaLevel));
                float ne = Vector3.Distance(normals[i], expectedNormal);
                float ve = Vector3.Distance(velocities[i], Vector3.up * expectedVelocity);
                float de = Vector3.Distance(displacements[i], expectedDisplacement);
                File.AppendAllText(_report, $"{phase}; Time={time:R}; Point={i}; Height={heights[i]:R}; ExpectedHeight={h + ocean.SeaLevel:R}; HeightError={he:R}; NormalError={ne:R}; VelocityError={ve:R}; DisplacementError={de:R}\n");
                Assert.That(float.IsFinite(he) && float.IsFinite(ne) && float.IsFinite(ve) && float.IsFinite(de), Is.True);
                Assert.That(he, Is.LessThan(.01f));
                Assert.That(ne, Is.LessThan(.02f));
                Assert.That(ve, Is.LessThan(.1f));
                Assert.That(de, Is.LessThan(.0001f));
                maxHeightError = Mathf.Max(maxHeightError, he);
                maxNormalError = Mathf.Max(maxNormalError, ne);
                maxVelocityError = Mathf.Max(maxVelocityError, ve);
                maxDisplacementError = Mathf.Max(maxDisplacementError, de);
                horizontalPeak = Mathf.Max(horizontalPeak, new Vector2(displacements[i].x, displacements[i].z).magnitude);
            }
        }
        File.AppendAllText(_report, $"Summary={phase}; Samples={times.Length * 5}; MaxHeightError={maxHeightError:R}; MaxNormalError={maxNormalError:R}; MaxVelocityError={maxVelocityError:R}; MaxDisplacementError={maxDisplacementError:R}; HorizontalDisplacementPeak={horizontalPeak:R}\n");
        Assert.That(horizontalPeak, Is.GreaterThan(.01f), "Choppy inversion fixture must have nonzero horizontal displacement.");
    }

    float Height(Vector2 position, float time)
    {
        var p = _data._parameters;
        float frame = Mathf.Repeat(time, p._period) * p._frameCount / p._period;
        int first = Mathf.FloorToInt(frame);
        return Mathf.Lerp(InvertedFrameHeight(position, first), InvertedFrameHeight(position, (first + 1) % p._frameCount), frame - first);
    }

    float InvertedFrameHeight(Vector2 position, int frame)
    {
        var undisplaced = position;
        for (int iteration = 0; iteration < 12; iteration++)
        {
            var displacement = FrameDisplacement(undisplaced, frame);
            var error = undisplaced + new Vector2(displacement.x, displacement.z) - position;
            if (error.magnitude < .000005f) break;
            const float epsilon = .001f;
            var dx = (FrameDisplacement(undisplaced + Vector2.right * epsilon, frame) - displacement) / epsilon;
            var dz = (FrameDisplacement(undisplaced + Vector2.up * epsilon, frame) - displacement) / epsilon;
            float a = 1 + dx.x, b = dz.x, c = dx.z, d = 1 + dz.z;
            float determinant = a * d - b * c;
            Assert.That(Mathf.Abs(determinant), Is.GreaterThan(.1f), "Reference mapping must remain locally invertible.");
            undisplaced -= new Vector2(d * error.x - b * error.y, -c * error.x + a * error.y) / determinant;
        }
        var final = FrameDisplacement(undisplaced, frame);
        Assert.That((undisplaced + new Vector2(final.x, final.z) - position).magnitude, Is.LessThan(.00005f));
        return final.y;
    }

    Vector3 Displacement(Vector2 point, float time)
    {
        var p = _data._parameters;
        float frame = Mathf.Repeat(time, p._period) * p._frameCount / p._period;
        int first = Mathf.FloorToInt(frame);
        return Vector3.Lerp(FrameDisplacement(point, first), FrameDisplacement(point, (first + 1) % p._frameCount), frame - first);
    }

    Vector3 FrameDisplacement(Vector2 point, int frame)
    {
        var p = _data._parameters;
        Vector3 result = Vector3.zero;
        for (int lod = 0; lod < p._lodCount; lod++)
        {
            float width = .5f * (1 << (p._firstLod + lod));
            float u = Mathf.Repeat(point.x / width * p._textureResolution - .5f, p._textureResolution);
            float v = Mathf.Repeat(point.y / width * p._textureResolution - .5f, p._textureResolution);
            int x = Mathf.FloorToInt(u), z = Mathf.FloorToInt(v);
            int x1 = (x + 1) % p._textureResolution, z1 = (z + 1) % p._textureResolution;
            for (int channel = 0; channel < 3; channel++)
            {
                int offset = ((frame * p._lodCount + lod) * p._textureResolution * p._textureResolution) * 4;
                float a = (float)_data._framesFlattenedNative[offset + (z * p._textureResolution + x) * 4 + channel];
                float b = (float)_data._framesFlattenedNative[offset + (z * p._textureResolution + x1) * 4 + channel];
                float c = (float)_data._framesFlattenedNative[offset + (z1 * p._textureResolution + x) * 4 + channel];
                float d = (float)_data._framesFlattenedNative[offset + (z1 * p._textureResolution + x1) * 4 + channel];
                result[channel] += Mathf.Lerp(Mathf.Lerp(a, b, u - x), Mathf.Lerp(c, d, u - x), v - z);
            }
        }
        return result;
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = _scale;
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (_spectrum != null) Object.DestroyImmediate(_spectrum);
        if (!string.IsNullOrEmpty(_folder) && AssetDatabase.IsValidFolder(_folder))
        {
            string root = Path.GetFullPath(Parent) + Path.DirectorySeparatorChar;
            Assert.That(Path.GetFullPath(_folder).StartsWith(root, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(AssetDatabase.AssetPathToGUID(_folder), Is.EqualTo(_guid));
            // Scene/providers are stopped above. DeleteAsset alone does not release
            // loaded FFTBakedData native storage in Unity 6000.6.5f1.
            if (_data != null) Resources.UnloadAsset(_data);
            Assert.That(AssetDatabase.DeleteAsset(_folder), Is.True);
        }
    }
}
