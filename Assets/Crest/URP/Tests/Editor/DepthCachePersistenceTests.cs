using System;
using System.Collections;
using System.IO;
using System.Linq;
using Crest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class DepthCachePersistenceTests
{
    const string Parent = "Assets/Crest/URP/Tests/Editor";
    string _folder, _folderGuid;
    RenderTexture _sentinel;

    [UnityTest]
    public IEnumerator BakedDepthSurvivesDiskPrefabReloadAndRelativeMovement()
    {
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        string name = "CacheRoundTrip_" + Guid.NewGuid().ToString("N");
        _folderGuid = AssetDatabase.CreateFolder(Parent, name);
        _folder = Parent + "/" + name;
        Assert.That(_folderGuid, Is.Not.Empty);
        yield return new EnterPlayMode();
        foreach (var fft in Object.FindObjectsByType<ShapeFFT>()) fft.enabled = false;
        Camera.main.transform.position = new Vector3(0, 7, 0);
        yield return WaitForOceanUpdates(4);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Temporary source floor";
        floor.layer = 30;
        floor.transform.position = new Vector3(0, -3.6f, 0);
        floor.transform.localScale = new Vector3(100, .2f, 100);
        floor.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/CrestPortDemos/Reference.mat");
        var owner = new GameObject("Persistent baked cache");
        owner.transform.position = Vector3.up * 2;
        owner.transform.localScale = new Vector3(32, 1, 32);
        var cache = owner.AddComponent<OceanDepthCache>();
        cache._resolution = 64;
        cache._layers = 1 << 30;
        cache._cameraMaxTerrainHeight = 20;
        cache._cameraFarClipPlane = 100;
        var configuration = new SerializedObject(cache);
        configuration.FindProperty("_refreshMode").intValue = (int)OceanDepthCache.OceanDepthCacheRefreshMode.OnDemand;
        configuration.FindProperty("_relative").boolValue = false;
        configuration.ApplyModifiedPropertiesWithoutUndo();
        _sentinel = new RenderTexture(8, 8, 0);
        _sentinel.Create();
        var previousTarget = RenderTexture.active;
        try
        {
            RenderTexture.active = _sentinel;
            cache.PopulateCache();
            Assert.That(RenderTexture.active, Is.SameAs(_sentinel), "Populating the realtime cache must restore the caller's active render texture.");
        }
        finally
        {
            RenderTexture.active = previousTarget;
        }
        string report = $"Evidence/DepthPersistence-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(report, "Known floor=-3.5m, owner=2m; original Inspector export method; EXR disk import and prefab round-trip; tolerance=0.02m\n");
        yield return CheckTexture(cache.CacheTexture, -3.5f, "LegacyRealtime", report);

        RenderTexture.active = _sentinel;
        string path = _folder + "/Depth.exr";
        Assert.That(OceanDepthCacheEditor.SaveCacheToFile(cache, path), Is.EqualTo(path));
        AssertExportCleanup();
        Assert.That(cache.Type, Is.EqualTo(OceanDepthCache.OceanDepthCacheType.Baked));
        Assert.That(AssetDatabase.GetAssetPath(cache.SavedCache), Is.EqualTo(path));
        Assert.That(new SerializedObject(cache).FindProperty("_relative").boolValue, Is.True);
        Assert.That(new FileInfo(path).Length, Is.GreaterThan(100));
        string textureGuid = AssetDatabase.AssetPathToGUID(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.SingleChannel));
        Assert.That(importer.sRGBTexture, Is.False);
        Assert.That(importer.mipmapEnabled, Is.False);
        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        Assert.That(importer.GetDefaultPlatformTextureSettings().format, Is.EqualTo(TextureImporterFormat.RFloat));
        yield return CheckTexture(cache.SavedCache, -5.5f, "FirstBakeRelative", report);

        // Exercise the real export failure path after the CPU texture is created.
        RenderTexture.active = _sentinel;
        Assert.Throws<DirectoryNotFoundException>(() => OceanDepthCacheEditor.SaveCacheToFile(cache, _folder + "/Absent/Depth.exr"));
        AssertExportCleanup();
        Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(textureGuid));
        floor.transform.position -= Vector3.up;
        RenderTexture.active = _sentinel;
        Assert.That(OceanDepthCacheEditor.SaveCacheToFile(cache), Is.EqualTo(path));
        AssertExportCleanup();
        Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(textureGuid), "Rebake must preserve the saved texture reference.");
        yield return CheckTexture(cache.SavedCache, -6.5f, "RebakedRelative", report);
        File.Copy(path, $"Evidence/DepthBaked-{SystemInfo.graphicsDeviceType}.exr", true);

        // Serialize only the cache configuration, not its generated DontSave quad.
        var savedOwner = new GameObject("Baked cache prefab");
        savedOwner.SetActive(false);
        savedOwner.transform.SetPositionAndRotation(owner.transform.position, owner.transform.rotation);
        savedOwner.transform.localScale = owner.transform.localScale;
        var savedCache = savedOwner.AddComponent<OceanDepthCache>();
        EditorUtility.CopySerialized(cache, savedCache);
        string prefabPath = _folder + "/Cache.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(savedOwner, prefabPath);
        Assert.That(prefab, Is.Not.Null);
        var originalTarget = cache.CacheTexture;
        Object.Destroy(owner);
        Object.Destroy(savedOwner);
        Object.Destroy(floor);
        yield return WaitForOceanUpdates(2);
        Assert.That(originalTarget == null, Is.True);
        yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Ocean_Calm");
        Camera.main.transform.position = new Vector3(0, 7, 0);
        Assert.That(Object.FindObjectsByType<Transform>().Any(t => t.gameObject.layer == 30), Is.False);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var reloaded = instance.GetComponent<OceanDepthCache>();
        Assert.That(reloaded.Type, Is.EqualTo(OceanDepthCache.OceanDepthCacheType.Baked));
        Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(reloaded.SavedCache)), Is.EqualTo(textureGuid));
        Assert.That(reloaded._resolution, Is.EqualTo(64));
        Assert.That(reloaded._layers.value, Is.EqualTo(1 << 30));
        Assert.That(new SerializedObject(reloaded).FindProperty("_relative").boolValue, Is.True);
        instance.SetActive(true);
        yield return WaitForOceanUpdates(4);
        Assert.That(reloaded.CacheTexture, Is.Null, "Baked mode must use the imported asset without allocating a realtime cache.");
        yield return CheckTexture(reloaded.SavedCache, -6.5f, "DiskReloadRelative", report);
        yield return CheckWorldDepth(-4.5f, "ReloadWorldHeight", report);
        instance.transform.position = Vector3.up * 5;
        yield return WaitForOceanUpdates(4);
        yield return CheckWorldDepth(-1.5f, "MovedOwnerWorldHeight", report);
        reloaded.enabled = false;
        yield return WaitForOceanUpdates(4);
        yield return CheckWorldDepth(float.NegativeInfinity, "Disabled", report);
        reloaded.enabled = true;
        yield return WaitForOceanUpdates(4);
        yield return CheckWorldDepth(-1.5f, "Reenabled", report);
        File.AppendAllText(report, "TextureGUIDPreserved=True; PrefabReferenceReloaded=True; SourceGeometryAbsent=True; OldRealtimeRTReleased=True; ExportActiveTargetRestored=True; ExportCPUTextureReleased=True; FailurePathCleanup=True\n");
    }

    void AssertExportCleanup()
    {
        Assert.That(RenderTexture.active, Is.SameAs(_sentinel));
        Assert.That(Resources.FindObjectsOfTypeAll<Texture2D>().Any(t => t.name == "Crest Depth Cache Export"), Is.False);
    }
    static IEnumerator CheckTexture(Texture texture, float expected, string phase, string report)
    {
        var request = AsyncGPUReadback.Request(texture, 0, TextureFormat.RFloat);
        while (!request.done) yield return null;
        Assert.That(request.hasError, Is.False);
        float value = request.GetData<float>(0)[texture.width * (texture.height / 2) + texture.width / 2];
        File.AppendAllText(report, $"Phase={phase}; Expected={expected}; Actual={value}\n");
        Assert.That(value, Is.EqualTo(expected).Within(.02f));
    }
    static IEnumerator CheckWorldDepth(float expected, string phase, string report)
    {
        var target = OceanRenderer.Instance._lodDataSeaDepths.DataTexture;
        var transform = OceanRenderer.Instance._lodTransform._renderData[0].Current;
        float texel = 4 * OceanRenderer.Instance.CalcLodScale(0) / target.width;
        int x = Mathf.FloorToInt(-transform._posSnapped.x / texel + target.width * .5f);
        int y = Mathf.FloorToInt(-transform._posSnapped.z / texel + target.height * .5f);
        Assert.That(x, Is.InRange(0, target.width - 1));
        Assert.That(y, Is.InRange(0, target.height - 1));
        var request = AsyncGPUReadback.Request(target, 0, TextureFormat.RGFloat);
        while (!request.done) yield return null;
        Assert.That(request.hasError, Is.False);
        float height = request.GetData<Vector2>(0)[target.width * y + x].x;
        File.AppendAllText(report, $"Phase={phase}; Expected={expected}; Actual={height}\n");
        if (float.IsNegativeInfinity(expected)) Assert.That(height, Is.LessThan(-10000));
        else Assert.That(height, Is.EqualTo(expected).Within(.02f));
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
    public IEnumerator Restore()
    {
        RenderTexture.active = null;
        if (_sentinel != null) { _sentinel.Release(); Object.Destroy(_sentinel); }
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
        if (!string.IsNullOrEmpty(_folder))
        {
            Assert.That(Path.GetFullPath(_folder), Does.StartWith(Path.GetFullPath(Parent) + Path.DirectorySeparatorChar));
            Assert.That(AssetDatabase.AssetPathToGUID(_folder), Is.EqualTo(_folderGuid));
            Assert.That(AssetDatabase.DeleteAsset(_folder), Is.True);
            Assert.That(AssetDatabase.IsValidFolder(_folder), Is.False);
            _folder = _folderGuid = null;
        }
    }
}
