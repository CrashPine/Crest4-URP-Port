using System;
using System.IO;
using Crest;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Reproducible fixture for real Inspector/Scene view verification. The menu
// records state; it does not implement the edits being verified.
[InitializeOnLoad]
public static class CrestEditorInteractionProbe
{
    const string Parent = "Assets/Crest/URP/Tests/Editor";
    const string OwnerPath = "Evidence/EditorInteraction-owner.json";
    const string Report = "Evidence/EditorInteraction.jsonl";
    const string Errors = "Evidence/EditorInteraction-errors.txt";
    [Serializable] public class Owner { public string folder, folderGuid, sceneGuid, spectrumGuid; public bool liveOcean; }
    [Serializable] public class Snapshot
    {
        public string phase, utc, sceneGuid, spectrumGuid, selected;
        public float radius, spectrumMultiplier;
        public bool closed, sceneDirty, spectrumDirty;
        public Vector3[] positions;
        public float[] speeds;
        public bool[] hasFlowData;
        public string api, flowHash, wavesHash;
        public int oceanFrame, lodFrame, collisionSamples;
        public float sampleTime, flowPeak, wavePeak, collisionMin, collisionMax;
    }
    static string _lastSignature;
    static double _nextObservation;
    static string _pendingSignature;
    static int _pendingFrame;
    static CrestEditorInteractionProbe()
    {
        EditorApplication.update += ObserveChanges;
        Application.logMessageReceived += RecordError;
    }
    static void ObserveChanges()
    {
        if (EditorApplication.timeSinceStartup < _nextObservation) return;
        _nextObservation = EditorApplication.timeSinceStartup + .3;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(OwnerPath)) return;
        var owner = ReadOwner();
        if (SceneManager.GetActiveScene().path != owner.folder + "/Editor.unity") return;
        if (Object.FindFirstObjectByType<Crest.Spline.Spline>() == null) return;
        Record("Observed", true);
    }
    static Owner ReadOwner()
    {
        var owner = JsonUtility.FromJson<Owner>(File.ReadAllText(OwnerPath));
        var parent = Path.GetFullPath(Parent) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(owner.folder).StartsWith(parent, StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(owner.folder).StartsWith("EditorInteraction_", StringComparison.Ordinal)
            || AssetDatabase.AssetPathToGUID(owner.folder) != owner.folderGuid)
            throw new InvalidOperationException("Editor fixture ownership check failed.");
        return owner;
    }
    public static void Prepare() => Prepare(false);
    public static void PrepareLiveOcean() => Prepare(true);

    static void Prepare(bool liveOcean)
    {
        if (File.Exists(OwnerPath)) throw new InvalidOperationException("Clean up the previous owned Editor fixture before preparing another.");
        if (liveOcean) EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        string name = "EditorInteraction_" + Guid.NewGuid().ToString("N");
        var owner = new Owner { folderGuid = AssetDatabase.CreateFolder(Parent, name), folder = Parent + "/" + name, liveOcean = liveOcean };
        var root = new GameObject("Crest UI Spline");
        var spline = root.AddComponent<Crest.Spline.Spline>();
        spline.Radius = 8;
        spline.Subdivisions = 3;
        spline._offset = Crest.Spline.Spline.Offset.Center;
        for (int i = 0; i < 3; i++)
        {
            var point = new GameObject("Point " + i);
            point.transform.SetParent(root.transform, false);
            point.transform.localPosition = new Vector3(-8 + i * 8, 0, i == 1 ? 2 : 0);
            point.AddComponent<Crest.Spline.SplinePoint>();
            point.AddComponent<SplinePointDataFlow>().FlowVelocity = 3;
        }
        var spectrum = ScriptableObject.CreateInstance<OceanWaveSpectrum>();
        AssetDatabase.CreateAsset(spectrum, owner.folder + "/Spectrum.asset");
        if (liveOcean) ConfigureLiveOcean(root, spectrum);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), owner.folder + "/Editor.unity");
        AssetDatabase.SaveAssets();
        owner.sceneGuid = AssetDatabase.AssetPathToGUID(owner.folder + "/Editor.unity");
        owner.spectrumGuid = AssetDatabase.AssetPathToGUID(owner.folder + "/Spectrum.asset");
        File.WriteAllText(OwnerPath, JsonUtility.ToJson(owner, true));
        File.WriteAllText(liveOcean ? "Evidence/P11-LiveEditor.jsonl" : Report, "");
        File.WriteAllText(liveOcean ? "Evidence/P11-LiveEditor-errors.txt" : Errors, "");
        Selection.activeGameObject = root;
        var view = SceneView.GetWindow<SceneView>();
        view.LookAt(Vector3.zero, Quaternion.Euler(90, 0, 0), 15, true, true);
        Tools.current = Tool.Move;
        Record("Prepared");
    }

    static void ConfigureLiveOcean(GameObject splineRoot, OceanWaveSpectrum spectrum)
    {
        var ocean = Object.FindFirstObjectByType<OceanRenderer>();
        var fft = Object.FindFirstObjectByType<ShapeFFT>();
        fft._spectrum = spectrum;
        var clock = ocean.gameObject.AddComponent<TimeProviderCustom>();
        clock._overrideTime = clock._overrideDeltaTime = true;
        clock._time = 1;
        var settings = new SerializedObject(ocean);
        settings.FindProperty("_timeProvider").objectReferenceValue = clock;
        settings.FindProperty("_createFlowSim").boolValue = true;
        settings.ApplyModifiedPropertiesWithoutUndo();
        ocean.PushTimeProvider(clock);
        var material = new Material(ocean.OceanMaterial);
        material.EnableKeyword("_FLOW_ON");
        material.SetFloat("_Flow", 1);
        string materialPath = AssetDatabase.GetAssetPath(spectrum).Replace("Spectrum.asset", "Ocean.mat");
        AssetDatabase.CreateAsset(material, materialPath);
        ocean.OceanMaterial = material;
        splineRoot.AddComponent<RegisterFlowInput>();
        var display = new GameObject("Crest Collision Debug").AddComponent<VisualiseCollisionArea>();
        var debug = new SerializedObject(display);
        debug.FindProperty("_steps").intValue = 5;
        debug.FindProperty("_stepSize").floatValue = 2;
        debug.ApplyModifiedPropertiesWithoutUndo();
    }
    static void RecordError(string message, string stack, LogType type)
    {
        if (!File.Exists(OwnerPath)) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            File.AppendAllText(ReadOwner().liveOcean ? "Evidence/P11-LiveEditor-errors.txt" : Errors, type + ": " + message + "\n" + stack + "\n");
    }
    [MenuItem("Crest/Port Validation/Record Editor Checkpoint")]
    public static void RecordCheckpoint() => Record("Checkpoint");
    static void Record(string phase, bool onlyChanges = false)
    {
        var owner = ReadOwner();
        var spline = Object.FindFirstObjectByType<Crest.Spline.Spline>();
        var spectrum = AssetDatabase.LoadAssetAtPath<OceanWaveSpectrum>(owner.folder + "/Spectrum.asset");
        var points = spline.GetComponentsInChildren<Crest.Spline.SplinePoint>();
        var state = new Snapshot
        {
            phase = phase, radius = spline.Radius, closed = spline._closed,
            sceneGuid = AssetDatabase.AssetPathToGUID(SceneManager.GetActiveScene().path),
            spectrumGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(spectrum)),
            sceneDirty = SceneManager.GetActiveScene().isDirty, spectrumDirty = EditorUtility.IsDirty(spectrum),
            spectrumMultiplier = spectrum._multiplier, selected = Selection.activeObject ? Selection.activeObject.name : "",
            positions = new Vector3[points.Length], speeds = new float[points.Length], hasFlowData = new bool[points.Length]
        };
        for (int i = 0; i < points.Length; i++)
        {
            state.positions[i] = points[i].transform.position;
            state.hasFlowData[i] = points[i].TryGetComponent<SplinePointDataFlow>(out var flow);
            state.speeds[i] = flow != null ? flow.FlowVelocity : 0;
        }
        string signature = JsonUtility.ToJson(state);
        if (onlyChanges && signature == _lastSignature) return;
        if (owner.liveOcean && phase != "Prepared")
        {
            int frame = OceanRenderer.FrameCount;
            if (signature != _pendingSignature)
            {
                _pendingSignature = signature;
                _pendingFrame = frame;
                return;
            }
            if (frame < _pendingFrame + 5 || !ReadLiveOcean(state)) return;
        }
        _lastSignature = signature;
        state.utc = DateTime.UtcNow.ToString("o");
        File.AppendAllText(owner.liveOcean ? "Evidence/P11-LiveEditor.jsonl" : Report, JsonUtility.ToJson(state) + "\n");
    }

    static bool ReadLiveOcean(Snapshot state)
    {
        var ocean = OceanRenderer.Instance;
        if (ocean?._lodDataFlow?.DataTexture == null || ocean._lodDataAnimWaves?.DataTexture == null
            || BuildCommandBufferBase._lastUpdateFrame != OceanRenderer.FrameCount)
            return false;

        state.api = SystemInfo.graphicsDeviceType.ToString();
        state.oceanFrame = OceanRenderer.FrameCount;
        state.lodFrame = BuildCommandBufferBase._lastUpdateFrame;
        state.sampleTime = ocean.CurrentTime;
        (state.flowHash, state.flowPeak) = ReadField(ocean._lodDataFlow.DataTexture, TextureFormat.RGFloat, 2);
        (state.wavesHash, state.wavePeak) = ReadField(ocean._lodDataAnimWaves.DataTexture, TextureFormat.RGBAFloat, 4);
        var display = Object.FindFirstObjectByType<VisualiseCollisionArea>();
        var heights = typeof(VisualiseCollisionArea)
            .GetField("_resultHeights", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .GetValue(display) as float[];
        if (heights == null) return true;
        state.collisionSamples = heights.Length;
        state.collisionMin = float.PositiveInfinity;
        state.collisionMax = float.NegativeInfinity;
        foreach (float height in heights)
        {
            if (!float.IsFinite(height)) throw new InvalidOperationException("Non-finite collision debug height.");
            state.collisionMin = Mathf.Min(state.collisionMin, height);
            state.collisionMax = Mathf.Max(state.collisionMax, height);
        }
        return true;
    }

    static (string hash, float peak) ReadField(RenderTexture texture, TextureFormat format, int components)
    {
        var request = AsyncGPUReadback.Request(texture, 0, format);
        request.WaitForCompletion();
        if (request.hasError) throw new InvalidOperationException("Could not read the live Editor field.");
        var values = request.GetData<float>(0).ToArray();
        float peak = 0;
        for (int i = 0; i < values.Length; i++)
        {
            if (!float.IsFinite(values[i])) throw new InvalidOperationException("Non-finite live Editor field.");
            if (components != 4 || i % 4 != 3) peak = Mathf.Max(peak, Mathf.Abs(values[i]));
        }
        var bytes = new byte[values.Length * sizeof(float)];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        using var hash = System.Security.Cryptography.SHA256.Create();
        return (BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", ""), peak);
    }
    [MenuItem("Crest/Port Validation/Select Spline")]
    public static void SelectSpline() => Selection.activeGameObject = Object.FindFirstObjectByType<Crest.Spline.Spline>().gameObject;
    [MenuItem("Crest/Port Validation/Select First Point")]
    public static void SelectFirstPoint() => Selection.activeGameObject = Object.FindFirstObjectByType<Crest.Spline.Spline>().transform.GetChild(0).gameObject;
    [MenuItem("Crest/Port Validation/Select Spectrum")]
    public static void SelectSpectrum() => Selection.activeObject = AssetDatabase.LoadAssetAtPath<OceanWaveSpectrum>(ReadOwner().folder + "/Spectrum.asset");
    [MenuItem("Crest/Port Validation/Reload Saved Fixture")]
    public static void ReloadSavedFixture()
    {
        var owner = ReadOwner();
        AssetDatabase.ImportAsset(owner.folder + "/Spectrum.asset", ImportAssetOptions.ForceUpdate);
        EditorSceneManager.OpenScene(owner.folder + "/Editor.unity");
        SelectSpline();
        Record("Reloaded");
    }
    [MenuItem("Crest/Port Validation/Record Editor Checkpoint", true)]
    [MenuItem("Crest/Port Validation/Select Spline", true)]
    [MenuItem("Crest/Port Validation/Select First Point", true)]
    [MenuItem("Crest/Port Validation/Select Spectrum", true)]
    [MenuItem("Crest/Port Validation/Reload Saved Fixture", true)]
    static bool HasFixture() => File.Exists(OwnerPath);
    public static void Cleanup()
    {
        var owner = ReadOwner();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (!AssetDatabase.DeleteAsset(owner.folder)) throw new InvalidOperationException("Owned Editor fixture cleanup failed.");
        File.Delete(OwnerPath);
    }
}
