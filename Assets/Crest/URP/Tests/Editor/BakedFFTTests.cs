using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Crest;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class BakedFFTTests
{
    const string Parent = "Assets/Crest/URP/Tests/Editor";
    string _folder, _folderGuid, _report;
    OceanWaveSpectrum _spectrum;
    FFTBakedData _data;
    CollProviderBakedFFT _provider;
    RenderTexture _sentinel;
    float _timeScale;
    readonly Dictionary<int, Color[]> _gpuFrames = new Dictionary<int, Color[]>();

    [UnityTest]
    public IEnumerator RealFFTBakeReloadAndCPUJobsMatchGPUFrames()
    {
        _timeScale = Time.timeScale;
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        Time.timeScale = 0;
        var ocean = OceanRenderer.Instance;
        foreach (var shape in Object.FindObjectsByType<ShapeWaves>()) shape.enabled = false;
        var clock = ocean.gameObject.AddComponent<TimeProviderCustom>();
        clock._overrideTime = clock._overrideDeltaTime = true;
        clock._time = clock._deltaTime = 0;
        ocean.PushTimeProvider(clock);
        _folder = Parent + "/BakedFFT_" + Guid.NewGuid().ToString("N");
        _folderGuid = AssetDatabase.CreateFolder(Parent, Path.GetFileName(_folder));
        _spectrum = ScriptableObject.CreateInstance<OceanWaveSpectrum>();
        _spectrum._chop = 0;
        var spectrumFields = new SerializedObject(_spectrum);
        for (int i = 0; i < OceanWaveSpectrum.NUM_OCTAVES; i++)
        {
            spectrumFields.FindProperty("_powerDisabled").GetArrayElementAtIndex(i).boolValue = i != 7;
            spectrumFields.FindProperty("_powerLog").GetArrayElementAtIndex(i).floatValue = -2;
        }
        spectrumFields.ApplyModifiedPropertiesWithoutUndo();
        var owner = new GameObject("Offline FFT");
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
        _report = $"Evidence/BakedFFT-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report, "Real original FFTCompute -> FFTBake -> RGBAHalf project asset -> unload/reimport -> CPU jobs. Resolution16, period4s,4fps, one spectrum octave, chop0. GPU reference regenerated independently at all16 keyframes; CPU comparisons use spatial/temporal interpolation of those GPU readbacks.\n");
        string asset = _folder + "/Waves.asset";
        _sentinel = new RenderTexture(8, 8, 0);
        _sentinel.Create();
        var previousActive = RenderTexture.active;
        RenderTexture.active = _sentinel;
        try
        {
            _data = FFTBaker.BakeShapeFFT(fft, asset);
            Assert.That(RenderTexture.active, Is.SameAs(_sentinel), "Exporter must restore caller's active render target.");
        }
        finally { RenderTexture.active = previousActive; }
        Assert.That(_data, Is.Not.Null);
        Assert.That(_data._parameters._frameCount, Is.EqualTo(16));
        Assert.That(_data._parameters._lodCount, Is.EqualTo(2));
        Assert.That(_data._parameters._firstLod, Is.EqualTo(7));
        Assert.Throws<ArgumentException>(() => FFTBaker.BakeShapeFFT(fft, asset), "Explicit path must never overwrite existing assets.");
        Assert.Throws<ArgumentException>(() => FFTBaker.BakeShapeFFT(fft, "Assets/../escape.asset"));
        if (Application.isBatchMode) Assert.Throws<InvalidOperationException>(() => FFTBaker.BakeShapeFFT(fft));
        string guid = AssetDatabase.AssetPathToGUID(asset);
        AssetDatabase.SaveAssets();
        Resources.UnloadAsset(_data);
        _data = null;
        AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceUpdate);
        _data = AssetDatabase.LoadAssetAtPath<FFTBakedData>(asset);
        Assert.That(AssetDatabase.AssetPathToGUID(asset), Is.EqualTo(guid));
        Assert.That(_data._framesFlattenedNative.IsCreated, Is.True);
        float peak = 0, maxBakeError = 0;
        for (int frame = 0; frame < 16; frame++)
        {
            using var commands = new CommandBuffer();
            var texture = FFTCompute.GenerateDisplacements(commands, fft._resolution, 4,
                fft.WindTurbulence, fft.WindDirRadForFFT, fft.WindSpeed, frame / 4f, _spectrum, true);
            Graphics.ExecuteCommandBuffer(commands);
            var request = AsyncGPUReadback.Request(texture, 0, TextureFormat.RGBAFloat);
            while (!request.done) yield return null;
            Assert.That(request.hasError, Is.False);
            Assert.That(request.layerCount, Is.EqualTo(texture.volumeDepth));
            var pixels = new Color[texture.width * texture.height * texture.volumeDepth];
            for (int layer = 0; layer < request.layerCount; layer++)
            {
                var layerPixels = request.GetData<Color>(layer);
                Assert.That(layerPixels.Length, Is.EqualTo(256));
                for (int pixel = 0; pixel < layerPixels.Length; pixel++) pixels[layer * 256 + pixel] = layerPixels[pixel];
            }
            _gpuFrames[frame] = pixels;
            var p = _data._parameters;
            for (int lod = 0; lod < p._lodCount; lod++)
            for (int pixel = 0; pixel < p._textureResolution * p._textureResolution; pixel++)
            for (int channel = 0; channel < 3; channel++)
            {
                float gpu = pixels[(p._firstLod + lod) * 256 + pixel][channel];
                float saved = (float)_data._framesFlattenedNative[((frame * p._lodCount + lod) * 256 + pixel) * 4 + channel];
                Assert.That(float.IsFinite(gpu) && float.IsFinite(saved), Is.True);
                peak = Mathf.Max(peak, Mathf.Abs(gpu));
                maxBakeError = Mathf.Max(maxBakeError, Mathf.Abs(saved - gpu));
            }
        }
        File.AppendAllText(_report, $"Bake; Guid={guid}; NativeHalfCount={_data._framesFlattenedNative.Length}; Peak={peak:R}; MaxGPUToAssetError={maxBakeError:R}; ActiveRTRestored=True; Reloaded=True\n");
        Assert.That(peak, Is.GreaterThan(.001f), "Real FFT reference must contain waves.");
        Assert.That(maxBakeError, Is.LessThan(.0025f), "Half-float storage tolerance.");
        _provider = new CollProviderBakedFFT(_data);
        var points = new[] { new Vector3(-21.5f, 0, -3.25f), Vector3.zero, new Vector3(.5f, 0, .5f), new Vector3(4.2f, 0, -7.3f), new Vector3(13.1f, 0, 22.7f) };
        var heights = new float[5];
        var normals = new Vector3[5];
        var velocities = new Vector3[5];
        foreach (float time in new[] { 0f, .125f, 1.375f, 3.875f, 4.125f, -.125f })
        {
            clock._time = time;
            // Complete both double buffers after changing time; Query retrieves the previous job results.
            for (int i = 0; i < 4; i++)
            {
                _provider.Query(GetHashCode(), 0, points, heights, normals, velocities);
                _provider.UpdateQueries();
            }
            int status = _provider.Query(GetHashCode(), 0, points, heights, normals, velocities);
            Assert.That(status, Is.Zero);
            for (int i = 0; i < points.Length; i++)
            {
                var point = points[i];
                float h = Height(point.x, point.z, time);
                var n = new Vector3(h - Height(point.x + .1f, point.z, time), .1f, h - Height(point.x, point.z + .1f, time)).normalized;
                float v = (h - Height(point.x, point.z, time - .06f)) / .06f;
                File.AppendAllText(_report, $"CPU; Time={time:R}; Point={i}; Status={status}; Height={heights[i]:R}; ExpectedHeight={h:R}; NormalError={Vector3.Distance(normals[i], n):R}; Velocity={velocities[i]:F6}; ExpectedVerticalVelocity={v:R}\n");
                Assert.That(heights[i], Is.EqualTo(h + ocean.SeaLevel).Within(.005f));
                Assert.That(Vector3.Distance(normals[i], n), Is.LessThan(.01f));
                Assert.That(Vector3.Distance(velocities[i], Vector3.up * v), Is.LessThan(.02f));
            }
        }
        _provider.CleanUp();
        _provider = null;
        File.AppendAllText(_report, "CPU jobs cleaned up; 30 height/normal/vertical-velocity triples including negative time, period seam and fifth SIMD-tail point passed.\n");
    }

    float Height(float x, float z, float time)
    {
        float frame = Mathf.Repeat(time, 4) * 4;
        int first = Mathf.FloorToInt(frame);
        return Mathf.Lerp(FrameHeight(x, z, first), FrameHeight(x, z, (first + 1) % 16), frame - first);
    }

    float FrameHeight(float x, float z, int frame)
    {
        float height = 0;
        var p = _data._parameters;
        for (int lod = p._firstLod; lod < p._firstLod + p._lodCount; lod++)
        {
            float width = .5f * (1 << lod);
            float u = Mathf.Repeat(x / width * 16 - .5f, 16);
            float v = Mathf.Repeat(z / width * 16 - .5f, 16);
            int x0 = Mathf.FloorToInt(u), z0 = Mathf.FloorToInt(v), x1 = (x0 + 1) % 16, z1 = (z0 + 1) % 16;
            var pixels = _gpuFrames[frame];
            float a = Mathf.Lerp(pixels[lod * 256 + z0 * 16 + x0].g, pixels[lod * 256 + z0 * 16 + x1].g, u - x0);
            float b = Mathf.Lerp(pixels[lod * 256 + z1 * 16 + x0].g, pixels[lod * 256 + z1 * 16 + x1].g, u - x0);
            height += Mathf.Lerp(a, b, v - z0);
        }
        return height;
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (_provider != null) { _provider.CleanUp(); _provider = null; }
        Time.timeScale = _timeScale;
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (_spectrum != null) Object.DestroyImmediate(_spectrum);
        if (_sentinel != null) { _sentinel.Release(); Object.DestroyImmediate(_sentinel); }
        if (!string.IsNullOrEmpty(_folder) && AssetDatabase.IsValidFolder(_folder))
        {
            string root = Path.GetFullPath(Parent) + Path.DirectorySeparatorChar;
            Assert.That(Path.GetFullPath(_folder).StartsWith(root, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(AssetDatabase.AssetPathToGUID(_folder), Is.EqualTo(_folderGuid));
            // Scene/providers are stopped above. DeleteAsset alone does not release
            // loaded FFTBakedData native storage in Unity 6000.6.5f1.
            if (_data != null) Resources.UnloadAsset(_data);
            Assert.That(AssetDatabase.DeleteAsset(_folder), Is.True);
        }
        _gpuFrames.Clear();
    }
}
