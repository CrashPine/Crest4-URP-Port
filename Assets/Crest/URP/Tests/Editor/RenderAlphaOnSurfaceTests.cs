// Crest 4 URP port. MIT; see LICENSE.Crest.
using System.Collections;
using System.IO;
using Crest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

public class RenderAlphaOnSurfaceTests
{
    const int Size = 128;
    Mesh _mesh;
    Material _overlayMaterial;
    Material _heightMaterial;
    RenderTexture _target;
    Texture2D _readback;
    bool _previousBatch;

    [UnityTest, Timeout(60000)]
    public IEnumerator OverlayFollowsDisplacementAndKeepsSharedMeshBounds()
    {
        _previousBatch = OceanRenderer.EnableGraphicsInBatchMode;
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        var ocean = OceanRenderer.Instance;
        Camera.main.transform.position = new Vector3(0, 4, 0);
        foreach (var shape in Object.FindObjectsByType<ShapeWaves>()) shape.enabled = false;
        ocean.transform.position = Vector3.up;

        var marker = GameObject.CreatePrimitive(PrimitiveType.Plane);
        marker.layer = 2;
        marker.transform.position = Vector3.up * 3;
        marker.transform.localScale = Vector3.one * .2f;
        var filter = marker.GetComponent<MeshFilter>();
        _mesh = Object.Instantiate(filter.sharedMesh);
        filter.sharedMesh = _mesh;
        var original = _mesh.bounds;
        var renderer = marker.GetComponent<Renderer>();
        _overlayMaterial = new Material(Shader.Find("Crest/Ocean Surface Alpha"));
        _overlayMaterial.SetFloat("_Alpha", .5f);
        renderer.sharedMaterial = _overlayMaterial;
        var overlay = marker.AddComponent<RenderAlphaOnSurface>();
        var peer = new GameObject("Unmodified shared mesh", typeof(MeshFilter), typeof(MeshRenderer));
        peer.GetComponent<MeshFilter>().sharedMesh = _mesh;

        var source = GameObject.CreatePrimitive(PrimitiveType.Plane);
        source.transform.position = Vector3.up * 3;
        source.transform.localScale = Vector3.one * 4;
        _heightMaterial = new Material(Shader.Find("Crest/Inputs/Animated Waves/Set Water Height Using Geometry"));
        source.GetComponent<Renderer>().sharedMaterial = _heightMaterial;
        var input = source.AddComponent<RegisterAnimWavesInput>();
        input.enabled = false;
        var settings = new SerializedObject(input);
        settings.FindProperty("_maxDisplacementVertical").floatValue = 2;
        settings.ApplyModifiedPropertiesWithoutUndo();

        var camera = new GameObject("Overlay capture").AddComponent<Camera>();
        camera.enabled = false;
        camera.cullingMask = 1 << 2;
        camera.orthographic = true;
        camera.orthographicSize = 4;
        camera.aspect = 1;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.allowMSAA = false;
        camera.transform.SetPositionAndRotation(new Vector3(0, 7, -7), Quaternion.Euler(45, 0, 0));
        _target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
        _target.Create();
        _readback = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        yield return Updates();
        var flat = Capture(camera, "Flat");
        input.enabled = true;
        yield return Updates();
        var lifted = Capture(camera, "Displaced");
        var expanded = renderer.localBounds;
        var shared = _mesh.bounds;
        var other = peer.GetComponent<Renderer>().localBounds;
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        int innerLod = block.GetInt(LodDataMgr.sp_LD_SliceIndex);
        var x = ocean._lodTransform._renderData[0].Current.RectXZ.xMax + .5f;
        marker.transform.position = new Vector3(x, 5, 0);
        yield return Updates();
        renderer.GetPropertyBlock(block);
        int outerLod = block.GetInt(LodDataMgr.sp_LD_SliceIndex);
        overlay.enabled = false;

        float expectedShift = (camera.WorldToViewportPoint(Vector3.up * 3).y - camera.WorldToViewportPoint(Vector3.up).y) * Size;
        string stem = $"Evidence/P13-SurfaceAlpha-{SystemInfo.graphicsDeviceType}";
        File.WriteAllText(stem + ".txt", $"FlatPixels={flat.count}; LiftedPixels={lifted.count}; FlatRed={flat.red}; LiftedRed={lifted.red}; Shift={lifted.y - flat.y:R}; ExpectedShift={expectedShift:R}; InnerLOD={innerLod}; OuterLOD={outerLod}; ExpandedY={expanded.extents.y:R}; SharedY={shared.extents.y:R}; PeerY={other.extents.y:R}; RestoredY={renderer.localBounds.extents.y:R}\n");
        Assert.That(flat.count, Is.GreaterThan(200));
        Assert.That(lifted.count, Is.GreaterThan(200));
        Assert.That(flat.red, Is.InRange(110, 220), "The overlay alpha must blend over the black background.");
        Assert.That(lifted.y - flat.y, Is.EqualTo(expectedShift).Within(1.25f));
        Assert.That(marker.transform.position.y, Is.EqualTo(1).Within(.001f));
        Assert.That(innerLod, Is.Zero);
        Assert.That(outerLod, Is.GreaterThan(innerLod));
        Assert.That(expanded.extents.y, Is.GreaterThanOrEqualTo(1.9f));
        Assert.That(shared, Is.EqualTo(original), "Expanding renderer bounds must not mutate its shared mesh.");
        Assert.That(other, Is.EqualTo(original));
        Assert.That(renderer.localBounds, Is.EqualTo(original));
    }

    static IEnumerator Updates()
    {
        for (int i = 0; i < 8; i++) yield return null;
    }

    (int count, float y, int red) Capture(Camera camera, string phase)
    {
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = _target });
        var previous = RenderTexture.active;
        try
        {
            RenderTexture.active = _target;
            _readback.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            _readback.Apply();
            File.WriteAllBytes($"Evidence/P13-SurfaceAlpha-{SystemInfo.graphicsDeviceType}-{phase}.png", _readback.EncodeToPNG());
            int count = 0, rowSum = 0, red = 0;
            var pixels = _readback.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].r < 90 || pixels[i].g < 90 || pixels[i].b < 90) continue;
                count++;
                rowSum += i / Size;
                red = Mathf.Max(red, pixels[i].r);
            }
            return (count, (float)rowSum / Mathf.Max(1, count), red);
        }
        finally { RenderTexture.active = previous; }
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        OceanRenderer.EnableGraphicsInBatchMode = _previousBatch;
        if (Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (_target != null) { _target.Release(); Object.DestroyImmediate(_target); }
        if (_readback != null) Object.DestroyImmediate(_readback);
        if (_mesh != null) Object.DestroyImmediate(_mesh);
        if (_overlayMaterial != null) Object.DestroyImmediate(_overlayMaterial);
        if (_heightMaterial != null) Object.DestroyImmediate(_heightMaterial);
    }
}
