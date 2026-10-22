using System;
using System.IO;
using Crest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public class EditorPersistenceTests
{
    const string Parent = "Assets/Crest/URP/Tests/Editor";
    string _folder, _guid;
    GameObject _root;

    [Test]
    public void ProfilesSplineReferencesAndUndoSurviveAssetRoundTrip()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        string name = "EditorRoundTrip_" + Guid.NewGuid().ToString("N");
        _guid = AssetDatabase.CreateFolder(Parent, name);
        _folder = Parent + "/" + name;
        Assert.That(_guid, Is.Not.Empty);
        var foam = ScriptableObject.CreateInstance<SimSettingsFoam>();
        foam._prewarm = false;
        foam._foamFadeRate = 1.75f;
        foam._simulationFrequency = 45;
        foam._waveFoamStrength = 2.25f;
        foam._shorelineFoamMaxDepth = 1.5f;
        var foamSettings = new SerializedObject(foam);
        foamSettings.FindProperty("_filterWaves").intValue = 3;
        foamSettings.ApplyModifiedPropertiesWithoutUndo();
        var spectrum = ScriptableObject.CreateInstance<OceanWaveSpectrum>();
        spectrum._multiplier = 1.3f;
        spectrum._chop = .9f;
        spectrum._waveDirectionVariance = 60;
        spectrum._chopScales[3] = 1.4f;
        spectrum._gravityScales[3] = .75f;
        var spectrumSettings = new SerializedObject(spectrum);
        spectrumSettings.FindProperty("_powerLog").GetArrayElementAtIndex(2).floatValue = -2.75f;
        spectrumSettings.FindProperty("_powerDisabled").GetArrayElementAtIndex(4).boolValue = true;
        spectrumSettings.ApplyModifiedPropertiesWithoutUndo();
        var depth = ScriptableObject.CreateInstance<SimSettingsSeaFloorDepth>();
        depth._allowVaryingWaterLevel = false;
        string foamPath = _folder + "/Foam.asset", spectrumPath = _folder + "/Spectrum.asset", depthPath = _folder + "/Depth.asset";
        AssetDatabase.CreateAsset(foam, foamPath);
        AssetDatabase.CreateAsset(spectrum, spectrumPath);
        AssetDatabase.CreateAsset(depth, depthPath);
        _root = new GameObject("Serialized ocean settings");
        _root.SetActive(false);
        var ocean = _root.AddComponent<OceanRenderer>();
        ocean.OceanMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/CrestPortDemos/Ocean_Calm.mat");
        ocean._simSettingsFoam = foam;
        ocean._simSettingsSeaFloorDepth = depth;
        var waves = new GameObject("Wave profile");
        waves.transform.SetParent(_root.transform, false);
        waves.AddComponent<ShapeFFT>()._spectrum = spectrum;
        var river = new GameObject("Saved flow spline");
        river.transform.SetParent(_root.transform, false);
        var spline = river.AddComponent<Crest.Spline.Spline>();
        spline.Radius = 12.5f;
        spline.Subdivisions = 3;
        spline._offset = Crest.Spline.Spline.Offset.Right;
        spline._closed = true;
        var positions = new[] { Vector3.zero, new Vector3(6, 1, 0), new Vector3(6, 2, 8) };
        var velocities = new[] { 3f, -2f, 4.5f };
        for (int i = 0; i < positions.Length; i++)
        {
            var point = new GameObject("Point " + i);
            point.transform.SetParent(river.transform, false);
            point.transform.localPosition = positions[i];
            point.AddComponent<Crest.Spline.SplinePoint>();
            point.AddComponent<SplinePointData>().RadiusMultiplier = 1 + i * .5f;
            point.AddComponent<SplinePointDataFlow>().FlowVelocity = velocities[i];
        }
        var input = river.AddComponent<RegisterFlowInput>();
        input.OverrideSplineSettings = true;
        input.Radius = 7.25f;
        input.Subdivisions = 4;
        string prefabPath = _folder + "/Ocean.prefab";
        Assert.That(PrefabUtility.SaveAsPrefabAsset(_root, prefabPath), Is.Not.Null);
        AssetDatabase.SaveAssets();
        string prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
        Object.DestroyImmediate(_root);
        Resources.UnloadAsset(foam);
        Resources.UnloadAsset(spectrum);
        Resources.UnloadAsset(depth);
        foreach (string path in new[] { foamPath, spectrumPath, depthPath, prefabPath })
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        _root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
        ocean = _root.GetComponent<OceanRenderer>();
        foam = ocean._simSettingsFoam;
        Assert.That(AssetDatabase.GetAssetPath(foam), Is.EqualTo(foamPath));
        Assert.That(foam._prewarm, Is.False);
        Assert.That(foam._foamFadeRate, Is.EqualTo(1.75f));
        Assert.That(foam._simulationFrequency, Is.EqualTo(45));
        Assert.That(foam._waveFoamStrength, Is.EqualTo(2.25f));
        Assert.That(foam._shorelineFoamMaxDepth, Is.EqualTo(1.5f));
        Assert.That(foam.FilterWaves, Is.EqualTo(3));
        Assert.That(AssetDatabase.GetAssetPath(ocean._simSettingsSeaFloorDepth), Is.EqualTo(depthPath));
        Assert.That(ocean._simSettingsSeaFloorDepth._allowVaryingWaterLevel, Is.False);
        spectrum = _root.GetComponentInChildren<ShapeFFT>(true)._spectrum;
        Assert.That(AssetDatabase.GetAssetPath(spectrum), Is.EqualTo(spectrumPath));
        Assert.That(spectrum._multiplier, Is.EqualTo(1.3f));
        Assert.That(spectrum._chop, Is.EqualTo(.9f));
        Assert.That(spectrum._waveDirectionVariance, Is.EqualTo(60));
        Assert.That(spectrum._chopScales[3], Is.EqualTo(1.4f));
        Assert.That(spectrum._gravityScales[3], Is.EqualTo(.75f));
        spectrumSettings = new SerializedObject(spectrum);
        Assert.That(spectrumSettings.FindProperty("_powerLog").GetArrayElementAtIndex(2).floatValue, Is.EqualTo(-2.75f));
        Assert.That(spectrumSettings.FindProperty("_powerDisabled").GetArrayElementAtIndex(4).boolValue, Is.True);
        spline = _root.GetComponentInChildren<Crest.Spline.Spline>(true);
        Assert.That(spline.Radius, Is.EqualTo(12.5f));
        Assert.That(spline.Subdivisions, Is.EqualTo(3));
        Assert.That(spline._closed, Is.True);
        Assert.That(spline._offset, Is.EqualTo(Crest.Spline.Spline.Offset.Right));
        input = spline.GetComponent<RegisterFlowInput>();
        Assert.That(input.OverrideSplineSettings, Is.True);
        Assert.That(input.Radius, Is.EqualTo(7.25f));
        Assert.That(input.Subdivisions, Is.EqualTo(4));
        var points = spline.GetComponentsInChildren<Crest.Spline.SplinePoint>(true);
        Assert.That(points.Length, Is.EqualTo(3));
        for (int i = 0; i < points.Length; i++)
        {
            Assert.That(points[i].transform.localPosition, Is.EqualTo(positions[i]));
            Assert.That(points[i].GetComponent<SplinePointData>().RadiusMultiplier, Is.EqualTo(1 + i * .5f));
            Assert.That(points[i].GetComponent<SplinePointDataFlow>().FlowVelocity, Is.EqualTo(velocities[i]));
        }
        var spectrumEditor = Editor.CreateEditor(spectrum);
        var splineEditor = Editor.CreateEditor(spline);
        try
        {
            Assert.That(spectrumEditor, Is.TypeOf<OceanWaveSpectrumEditor>());
            Assert.That(splineEditor, Is.TypeOf<Crest.Spline.SplineEditor>());
        }
        finally { Object.DestroyImmediate(spectrumEditor); Object.DestroyImmediate(splineEditor); }
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        try
        {
            Undo.RecordObject(spline, "Change spline radius");
            spline.Radius = 20;
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.That(spline.Radius, Is.EqualTo(12.5f));
            Undo.PerformRedo();
            Assert.That(spline.Radius, Is.EqualTo(20));
            Assert.That(PrefabUtility.SaveAsPrefabAsset(_root, prefabPath), Is.Not.Null);
            Assert.That(AssetDatabase.AssetPathToGUID(prefabPath), Is.EqualTo(prefabGuid));
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).GetComponentInChildren<Crest.Spline.Spline>(true).Radius, Is.EqualTo(20));
        }
        finally { Undo.RevertAllDownToGroup(group); }
        File.WriteAllText($"Evidence/EditorPersistence-{SystemInfo.graphicsDeviceType}.txt",
            "Foam/depth/spectrum profiles preserved; private fields and spectrum arrays preserved; profile references resolve after unload/reimport; closed three-point spline, positions/radius multipliers/signed speeds/overrides preserved; original spectrum/spline custom editors resolve; Undo=12.5/Redo=20; edited radius persisted with same prefab GUID.\nInactive prefab used for data verification; this does not establish curved/closed flow rendering or interactive Scene view gestures.\n");
    }

    [TearDown]
    public void Cleanup()
    {
        if (_root != null) Object.DestroyImmediate(_root);
        if (!string.IsNullOrEmpty(_folder))
        {
            Assert.That(Path.GetFullPath(_folder), Does.StartWith(Path.GetFullPath(Parent) + Path.DirectorySeparatorChar));
            Assert.That(AssetDatabase.AssetPathToGUID(_folder), Is.EqualTo(_guid));
            Assert.That(AssetDatabase.DeleteAsset(_folder), Is.True);
            _folder = _guid = null;
        }
    }
}
