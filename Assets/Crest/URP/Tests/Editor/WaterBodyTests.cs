using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Crest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class WaterBodyTests
{
    const int Size = 256;
    readonly List<Object> _owned = new List<Object>();
    Camera _camera;
    RenderTexture _target;
    WaterBody _a, _b;
    Material _surface;
    TimeProviderCustom _clock;
    float _oldScale;
    string _report;
    int _states, _tileChecks, _fieldChecks, _pixelChecks;

    [UnityTest, Timeout(120000)]
    public IEnumerator BoundsTileCullingMaterialDominanceAndIncludedAreasRestore()
    {
        _oldScale = Time.timeScale;
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        Time.timeScale = 0;
        var ocean = OceanRenderer.Instance;
        _camera = Camera.main;
        _camera.transform.SetPositionAndRotation(new Vector3(0, 12, 0), Quaternion.Euler(90, 0, 0));
        _camera.orthographic = true; _camera.orthographicSize = 16; _camera.aspect = 1;
        _camera.nearClipPlane = .1f; _camera.farClipPlane = 2000;
        _camera.cullingMask = 1 << 4; _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = Color.magenta; _camera.allowHDR = _camera.allowMSAA = false;
        _camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        var underwater = _camera.GetComponent<UnderwaterRenderer>();
        if (underwater != null) underwater.enabled = false;
        RenderSettings.fog = false;
        foreach (var shape in Object.FindObjectsByType<ShapeWaves>()) shape.enabled = false;
        _surface = Own(new Material(ocean.OceanMaterial));
        foreach (var keyword in new[] { "_FOAM_ON", "_TRANSPARENCY_ON", "_APPLYNORMALMAPPING_ON", "_CAUSTICS_ON", "_CLIPSURFACE_ON" }) _surface.DisableKeyword(keyword);
        _surface.SetFloat("_Foam", 0); _surface.SetFloat("_Transparency", 0); _surface.SetFloat("_ApplyNormalMapping", 0);
        ocean.OceanMaterial = _surface;
        var configuration = new SerializedObject(ocean);
        configuration.FindProperty("_minScale").floatValue = 8;
        configuration.FindProperty("_maxScale").floatValue = 8;
        configuration.FindProperty("_createFoamSim").boolValue = false;
        configuration.FindProperty("_createClipSurfaceData").boolValue = false;
        configuration.ApplyModifiedPropertiesWithoutUndo();
        var depth = Own(ScriptableObject.CreateInstance<SimSettingsSeaFloorDepth>());
        depth._allowVaryingWaterLevel = false; ocean._simSettingsSeaFloorDepth = depth;
        _target = Own(new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32)); _target.Create();
        yield return Updates(6);
        _clock = ocean.gameObject.AddComponent<TimeProviderCustom>();
        _clock._overrideTime = _clock._overrideDeltaTime = true; _clock._time = _clock._deltaTime = 0;
        ocean.PushTimeProvider(_clock);
        _report = $"Evidence/WaterBodies-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report, "Original WaterBody/OceanRenderer, fixed scale8, flat water, actual renderer states and URP surface. Independent corner bounds/positive intersection area reference; source bounds tolerance .0001m. Movement uses disable/change/re-enable (no private bounds refresh); live Editor transform changes are not a Player API. Clip field tolerance .005, interior mask excludes .35m edge band. No performance/underwater-volume acceptance.\n");
        Assert.That(WaterBody.WaterBodies.Count, Is.Zero);
        yield return Settle(); CheckCulling("NoBodies", false);
        Assert.That(CaptureMask("Baseline", false), Is.EqualTo(Size * Size));

        _a = Body("A", new Vector3(-2, 0, 0), new Vector3(16, 1, 16));
        yield return Settle(); CheckCulling("OneBody", true);
        _a._overrideMaterial = Own(new Material(_surface));
        _b = Body("B", new Vector3(4, 0, 0), new Vector3(12, 1, 12));
        _b._overrideMaterial = Own(new Material(_surface));
        yield return Settle(); CheckCulling("OverlappingMaterials", true, true);
        _a.enabled = false;
        yield return Settle(); CheckCulling("OnlyB", true);
        _b.enabled = false;
        yield return Settle(); CheckCulling("BothDisabled", false);
        CaptureMask("Restored", false, false);
        foreach (var tile in Tiles())
            if (_camera.WorldToViewportPoint(tile.Rend.bounds.center).z > 0 && GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(_camera), tile.Rend.bounds))
                Assert.That(tile.Rend.sharedMaterial, Is.SameAs(_surface), "Visible tiles restore the default material through actual render binding.");

        MoveInactive(_a, new Vector3(-4, 0, 3), new Vector3(8, 2, 14)); _a.enabled = true;
        _camera.transform.position = new Vector3(5, 12, 3);
        yield return Settle(); CheckCulling("MovedAndCameraMoved", true);
        _a.enabled = false;
        var edgeTile = Array.Find(Tiles(), t => t.Rend.bounds.size.x < 12 && t.Rend.bounds.size.z < 12);
        Assert.That(edgeTile, Is.Not.Null);
        var edge = edgeTile.Rend.bounds;
        MoveInactive(_a, new Vector3(edge.max.x + 2, 0, edge.center.z), new Vector3(4, 1, 4)); _a.enabled = true;
        yield return Settle(); CheckCulling("ExactTouch", true);
        Assert.That(_a.AABB.min.x, Is.EqualTo(edge.max.x));
        Assert.That(edgeTile.Rend.enabled, Is.False, "A zero-area boundary touch does not overlap.");
        ocean._waterBodyCulling = false;
        yield return Settle(); CheckCulling("CullingOff", false);
        _a.enabled = false; ocean._waterBodyCulling = true;
        yield return Settle(); CheckCulling("CullingRestored", false);

        _surface.EnableKeyword("_CLIPSURFACE_ON"); _surface.SetFloat("_ClipSurface", 1);
        ocean._defaultClippingState = OceanRenderer.DefaultClippingState.EverythingClipped;
        configuration = new SerializedObject(ocean);
        configuration.FindProperty("_createClipSurfaceData").boolValue = true;
        configuration.ApplyModifiedPropertiesWithoutUndo();
        yield return Updates(8); ocean.PushTimeProvider(_clock);
        _camera.transform.position = new Vector3(0, 12, 0);
        yield return Settle(); CheckField("NoIncludedAreas");
        Assert.That(CaptureMask("Empty", true), Is.Zero);
        MoveInactive(_a, new Vector3(-6, 0, 0), new Vector3(12, 1, 12));
        MoveInactive(_b, new Vector3(4, 0, 0), new Vector3(12, 1, 8));
        _a._overrideMaterial = _b._overrideMaterial = null;
        _a.enabled = _b.enabled = true;
        yield return Settle(); CheckCulling("TwoIncludedAreas", true); CheckField("TwoIncludedAreas");
        Assert.That(CaptureMask("TwoIncludedAreas", true), Is.GreaterThan(1000));
        _b.enabled = false;
        yield return Settle(); CheckField("OnlyAIncluded"); CaptureMask("OnlyAIncluded", true);
        _a.enabled = false;
        MoveInactive(_a, new Vector3(5, 0, 1), new Vector3(6, 1, 12)); _a.enabled = true;
        _camera.transform.position = new Vector3(4, 12, 2);
        yield return Settle(); CheckCulling("MovedIncludedArea", true); CheckField("MovedIncludedArea"); CaptureMask("MovedIncludedArea", true);
        _a.enabled = false;
        yield return Settle(); CheckField("IncludedAreasDisabled");
        Assert.That(CaptureMask("Disabled", true, false), Is.Zero);
        ocean._defaultClippingState = OceanRenderer.DefaultClippingState.NothingClipped;
        yield return Settle(); CheckField("DefaultVisibleRestored");
        Assert.That(CaptureMask("DefaultVisibleRestored", false, false), Is.EqualTo(Size * Size));
        ocean._defaultClippingState = OceanRenderer.DefaultClippingState.EverythingClipped;
        yield return Settle(); CheckField("DefaultHiddenRestored");
        Assert.That(CaptureMask("DefaultHiddenRestored", true, false), Is.Zero);
        ocean._defaultClippingState = OceanRenderer.DefaultClippingState.NothingClipped;
        yield return Settle(); CheckField("FinalVisibleRestored");
        Assert.That(CaptureMask("FinalVisibleRestored", false, false), Is.EqualTo(Size * Size));
        Assert.That(ocean.TimeProvider, Is.SameAs(_clock));
        File.AppendAllText(_report, $"Summary; CullingStates={_states}; TileChecks={_tileChecks}; FieldChecks={_fieldChecks}; InteriorPixelChecks={_pixelChecks}; ActiveBodies={WaterBody.WaterBodies.Count}\n");
    }

    T Own<T>(T value) where T : Object { _owned.Add(value); return value; }
    WaterBody Body(string name, Vector3 position, Vector3 scale)
    {
        var owner = new GameObject("Water body fixture " + name); owner.SetActive(false);
        owner.transform.position = position; owner.transform.localScale = scale;
        var body = owner.AddComponent<WaterBody>(); owner.SetActive(true); return body;
    }
    static void MoveInactive(WaterBody body, Vector3 position, Vector3 scale)
    {
        Assert.That(body.enabled, Is.False);
        body.transform.position = position; body.transform.localScale = scale;
    }
    static OceanChunkRenderer[] Tiles() => OceanRenderer.Instance.Root.GetComponentsInChildren<OceanChunkRenderer>();
    static IEnumerator Updates(int count)
    {
        int start = OceanRenderer.FrameCount; float deadline = Time.realtimeSinceStartup + 15;
        while (OceanRenderer.FrameCount < start + count && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(OceanRenderer.FrameCount - start, Is.GreaterThanOrEqualTo(count));
    }
    static IEnumerator Settle() => Updates(Tiles().Length + 3); // Every original time-sliced tile updates its unexpanded bounds.
    static Rect CornerRect(Transform owner, Bounds local)
    {
        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int x = -1; x <= 1; x += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            var p = owner.TransformPoint(local.center + new Vector3(x * local.extents.x, 0, z * local.extents.z));
            min = Vector2.Min(min, new Vector2(p.x, p.z)); max = Vector2.Max(max, new Vector2(p.x, p.z));
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
    static float Area(Rect a, Rect b) => Mathf.Max(0, Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin)) * Mathf.Max(0, Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin));
    static Rect XZ(Bounds b) => Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
    void CheckCulling(string phase, bool requireMixed, bool requireBothMaterials = false)
    {
        foreach (var body in WaterBody.WaterBodies)
        {
            var reference = CornerRect(body.transform, new Bounds(Vector3.zero, new Vector3(1, 0, 1)));
            Assert.That(float.IsFinite(body.AABB.center.x + body.AABB.center.y + body.AABB.center.z + body.AABB.size.sqrMagnitude), Is.True);
            Assert.That(Vector2.Distance(reference.min, XZ(body.AABB).min) + Vector2.Distance(reference.max, XZ(body.AABB).max), Is.LessThan(.0001f));
            Assert.That(body.AABB.center.y, Is.EqualTo(body.transform.position.y)); Assert.That(body.AABB.extents.y, Is.Zero);
        }
        int enabled = 0, aMaterials = 0, bMaterials = 0;
        foreach (var tile in Tiles())
        {
            bool overlaps = false; float largest = 0; Material material = null;
            var raw = CornerRect(tile.transform, tile._boundsLocal);
            foreach (var body in WaterBody.WaterBodies)
            {
                overlaps |= Area(XZ(tile.Rend.bounds), XZ(body.AABB)) > 0;
                float area = Area(raw, XZ(body.AABB));
                if (body._overrideMaterial != null && area > largest) { largest = area; material = body._overrideMaterial; }
            }
            bool expected = !OceanRenderer.Instance._waterBodyCulling || WaterBody.WaterBodies.Count == 0 || overlaps;
            Assert.That(tile.Rend.enabled, Is.EqualTo(expected), phase + " tile " + tile.name);
            Assert.That(tile.MaterialOverridden, Is.EqualTo(material != null));
            if (material != null) Assert.That(tile.Rend.sharedMaterial, Is.SameAs(material));
            if (_a != null && material != null && material == _a._overrideMaterial) aMaterials++;
            if (_b != null && material != null && material == _b._overrideMaterial) bMaterials++;
            if (expected) enabled++; _tileChecks++;
        }
        if (requireMixed) { Assert.That(enabled, Is.GreaterThan(0)); Assert.That(enabled, Is.LessThan(Tiles().Length)); }
        if (requireBothMaterials) { Assert.That(aMaterials, Is.GreaterThan(0)); Assert.That(bMaterials, Is.GreaterThan(0)); }
        File.AppendAllText(_report, $"State={phase}; Bodies={WaterBody.WaterBodies.Count}; Enabled={enabled}; Total={Tiles().Length}; MaterialA={aMaterials}; MaterialB={bMaterials}\n"); _states++;
    }
    bool Included(Vector2 p) => WaterBody.WaterBodies.Exists(body => XZ(body.AABB).Contains(p));
    bool NearEdge(Vector2 p, float margin)
    {
        foreach (var body in WaterBody.WaterBodies)
        {
            var r = XZ(body.AABB);
            if ((p.y >= r.yMin - margin && p.y <= r.yMax + margin && Mathf.Min(Mathf.Abs(p.x - r.xMin), Mathf.Abs(p.x - r.xMax)) < margin) ||
                (p.x >= r.xMin - margin && p.x <= r.xMax + margin && Mathf.Min(Mathf.Abs(p.y - r.yMin), Mathf.Abs(p.y - r.yMax)) < margin)) return true;
        }
        return false;
    }
    void CheckField(string phase)
    {
        var ocean = OceanRenderer.Instance; var rt = ocean._lodDataClipSurface.DataTexture;
        var request = AsyncGPUReadback.Request(rt, 0, TextureFormat.RFloat); request.WaitForCompletion(); Assert.That(request.hasError, Is.False);
        var values = request.GetData<float>(0); var data = ocean._lodTransform._renderData[0].Current;
        int inside = 0, outside = 0; float maxError = 0;
        for (int y = 0; y < rt.height; y += 3)
        for (int x = 0; x < rt.width; x += 3)
        {
            var p = new Vector2(data._posSnapped.x, data._posSnapped.z) + (new Vector2(x + .5f, y + .5f) - Vector2.one * (rt.width * .5f)) * data._texelWidth;
            if (NearEdge(p, 2 * data._texelWidth)) continue;
            bool included = Included(p); float expected = ocean._defaultClippingState == OceanRenderer.DefaultClippingState.NothingClipped || included ? 0 : 1;
            float value = values[y * rt.width + x]; Assert.That(float.IsFinite(value), Is.True);
            maxError = Mathf.Max(maxError, Mathf.Abs(value - expected));
            if (included) inside++; else outside++; _fieldChecks++;
        }
        File.AppendAllText(_report, $"Field={phase}; Inside={inside}; Outside={outside}; MaxError={maxError:R}\n");
        Assert.That(maxError, Is.LessThan(.005f)); Assert.That(outside, Is.GreaterThan(100));
        if (WaterBody.WaterBodies.Count > 0) Assert.That(inside, Is.GreaterThan(100));
    }
    int CaptureMask(string phase, bool clipped, bool save = true)
    {
        RenderPipeline.SubmitRenderRequest(_camera, new UniversalRenderPipeline.SingleCameraRequest { destination = _target });
        var previous = RenderTexture.active; var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = _target; texture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); texture.Apply();
            if (save) File.WriteAllBytes($"Evidence/WaterBodies-{phase}-{SystemInfo.graphicsDeviceType}.png", texture.EncodeToPNG());
            var pixels = texture.GetPixels32(); int visible = 0, checkedInside = 0, checkedOutside = 0, mismatches = 0;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                var pixel = pixels[y * Size + x]; bool covered = !(pixel.r > 240 && pixel.b > 240 && pixel.g < 16);
                if (covered) visible++;
                if (!clipped) continue;
                var ray = _camera.ViewportPointToRay(new Vector3((x + .5f) / Size, (y + .5f) / Size, 0));
                var world = ray.origin + ray.direction * (-ray.origin.y / ray.direction.y); var p = new Vector2(world.x, world.z);
                if (NearEdge(p, .35f)) continue;
                bool expected = Included(p); if (expected) checkedInside++; else checkedOutside++;
                if (covered != expected) mismatches++; _pixelChecks++;
            }
            File.AppendAllText(_report, $"Image={phase}; Visible={visible}; InteriorInside={checkedInside}; InteriorOutside={checkedOutside}; Mismatches={mismatches}\n");
            if (clipped)
            {
                Assert.That(checkedOutside, Is.GreaterThan(100));
                if (WaterBody.WaterBodies.Count > 0) Assert.That(checkedInside, Is.GreaterThan(100));
            }
            Assert.That(mismatches, Is.Zero); return visible;
        }
        finally { RenderTexture.active = previous; Object.DestroyImmediate(texture); }
    }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = _oldScale; OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (_target != null) _target.Release();
        foreach (var item in _owned) if (item != null) Object.DestroyImmediate(item); _owned.Clear();
    }
}
