using System;
using System.IO;
using Crest;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
    [Serializable] public class Owner { public string folder, folderGuid, sceneGuid, spectrumGuid; }
    [Serializable] public class Snapshot
    {
        public string phase, utc, sceneGuid, spectrumGuid, selected;
        public float radius, spectrumMultiplier;
        public bool closed, sceneDirty, spectrumDirty;
        public Vector3[] positions;
        public float[] speeds;
        public bool[] hasFlowData;
    }
    static string _lastSignature;
    static double _nextObservation;
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
    public static void Prepare()
    {
        if (File.Exists(OwnerPath)) throw new InvalidOperationException("Clean up the previous owned Editor fixture before preparing another.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        string name = "EditorInteraction_" + Guid.NewGuid().ToString("N");
        var owner = new Owner { folderGuid = AssetDatabase.CreateFolder(Parent, name), folder = Parent + "/" + name };
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
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), owner.folder + "/Editor.unity");
        AssetDatabase.SaveAssets();
        owner.sceneGuid = AssetDatabase.AssetPathToGUID(owner.folder + "/Editor.unity");
        owner.spectrumGuid = AssetDatabase.AssetPathToGUID(owner.folder + "/Spectrum.asset");
        File.WriteAllText(OwnerPath, JsonUtility.ToJson(owner, true));
        File.WriteAllText(Report, "");
        File.WriteAllText("Evidence/EditorInteraction-errors.txt", "");
        Selection.activeGameObject = root;
        var view = SceneView.GetWindow<SceneView>();
        view.LookAt(Vector3.zero, Quaternion.Euler(90, 0, 0), 15, true, true);
        Tools.current = Tool.Move;
        Record("Prepared");
    }
    static void RecordError(string message, string stack, LogType type)
    {
        if (!File.Exists(OwnerPath)) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            File.AppendAllText("Evidence/EditorInteraction-errors.txt", type + ": " + message + "\n" + stack + "\n");
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
        _lastSignature = signature;
        state.utc = DateTime.UtcNow.ToString("o");
        File.AppendAllText(Report, JsonUtility.ToJson(state) + "\n");
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
