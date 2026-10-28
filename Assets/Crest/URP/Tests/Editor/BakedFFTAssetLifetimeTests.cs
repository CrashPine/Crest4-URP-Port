using System;
using System.Collections;
using System.IO;
using Crest;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class BakedFFTAssetLifetimeTests
{
    const string Parent = "Assets/Crest/URP/Tests/Editor";
    string _folder, _guid;
    FFTBakedData _data;
    string Report => $"Evidence/P10-BakedLifetime-{SystemInfo.graphicsDeviceType}.txt";

    void CreateData()
    {
        _folder = Parent + "/BakedLifetime_" + Guid.NewGuid().ToString("N");
        _guid = AssetDatabase.CreateFolder(Parent, Path.GetFileName(_folder));
        _data = ScriptableObject.CreateInstance<FFTBakedData>();
        var frames = new half[16];
        for (int i = 0; i < frames.Length; i++) frames[i] = (half)(i * .125f);
        _data.Initialize(1, 2, 0, 1, 1, 1, (half)0, (half)2, frames);
        AssetDatabase.CreateAsset(_data, _folder + "/Data.asset");
        AssetDatabase.SaveAssets();
        Assert.That(_data._framesFlattenedNative.Length, Is.EqualTo(16));
        Assert.That((float)_data._framesFlattenedNative[15], Is.EqualTo(1.875f));
    }

    static bool Alive(NativeArray<half> buffer)
    {
        try { AtomicSafetyHandle.CheckReadAndThrow(NativeArrayUnsafeUtility.GetAtomicSafetyHandle(buffer)); return true; }
        catch (Exception) { return false; }
    }

    void Observe(string label, NativeArray<half> buffer)
    {
        File.AppendAllText(Report, $"{DateTime.UtcNow:O} {TestContext.CurrentContext.Test.Name}: {label}; allocationAlive={Alive(buffer)}; ownerExists={_data != null}; playing={Application.isPlaying}\n");
    }

    [Test]
    public void SaveUnloadReloadThenUnloadBeforeDeleteReleasesEachAllocation()
    {
        CreateData();
        for (int i = 0; i < 3; i++)
        {
            var previous = _data._framesFlattenedNative;
            Resources.UnloadAsset(_data);
            Observe("unloaded-" + i, previous);
            Assert.That(Alive(previous), Is.False, "Unload must invalidate original allocation.");
            _data = AssetDatabase.LoadAssetAtPath<FFTBakedData>(_folder + "/Data.asset");
            Assert.That((float)_data._framesFlattenedNative[15], Is.EqualTo(1.875f));
        }
        var final = _data._framesFlattenedNative;
        DeleteFolder();
        Observe("deleted", final);
        Assert.That(Alive(final), Is.False, "Delete must invalidate loaded allocation.");
    }

    [UnityTest]
    public IEnumerator CreatedDuringPlayModeReleasesAfterExitAndUnloadBeforeDelete()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        CreateData();
        var original = _data._framesFlattenedNative;
        Observe("before-exit", original);
        yield return new ExitPlayMode();
        Observe("after-exit", original);
        DeleteFolder();
        Observe("after-delete", original);
        Assert.That(Alive(original), Is.False, "Play-created asset deletion must release original allocation.");
    }

    [UnityTest]
    public IEnumerator LoadedBeforePlayModeReleasesAfterExitAndUnloadBeforeDelete()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateData();
        var original = _data._framesFlattenedNative;
        yield return new EnterPlayMode();
        Observe("during-play", original);
        yield return new ExitPlayMode();
        Observe("after-exit", original);
        DeleteFolder();
        Observe("after-delete", original);
        Assert.That(Alive(original), Is.False, "Preloaded asset deletion must release original allocation.");
    }

    void DeleteFolder()
    {
        if (string.IsNullOrEmpty(_folder) || !AssetDatabase.IsValidFolder(_folder)) return;
        Assert.That(Path.GetFullPath(_folder).StartsWith(Path.GetFullPath(Parent) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), Is.True);
        Assert.That(AssetDatabase.AssetPathToGUID(_folder), Is.EqualTo(_guid));
        // DeleteAsset alone leaves the native allocation live in Unity 6000.6.5f1.
        // End all consumers first; unload invokes the asset owner's OnDisable.
        if (_data != null) Resources.UnloadAsset(_data);
        Assert.That(AssetDatabase.DeleteAsset(_folder), Is.True);
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        DeleteFolder();
    }
}
