// Crest 4 URP port. MIT; see LICENSE.Crest.
using System.Collections;
using System.IO;
using System.Linq;
using Crest;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

public class RenderWireFrameTests
{
    const int Size = 128;
    RenderTexture _target;
    Texture2D _pixels;
    Material _material;

    [UnityTest, Timeout(60000)]
    public IEnumerator WireframeDrawsEdgesOnlyAndRestoresOtherCameras()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var camera = CreateCamera("Wireframe camera");
        var otherCamera = CreateCamera("Control camera");
        var wireframe = camera.gameObject.AddComponent<RenderWireFrame>();
        wireframe._backgroundColor = Color.black;
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.transform.localScale = Vector3.one * 2;
        _material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        _material.SetColor("_BaseColor", Color.white);
        quad.GetComponent<Renderer>().sharedMaterial = _material;
        _target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
        _target.Create();
        _pixels = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        yield return null;
        var filled = Capture(camera, "filled");
        wireframe._enable = true;
        yield return null;
        var lines = Capture(camera, "wire");
        bool flagLeaked = GL.wireframe;
        var control = Capture(otherCamera, "other");
        wireframe._enable = false;
        yield return null;
        var restored = Capture(camera, "restored");

        int filledCount = filled.Count(IsWhite);
        int lineCount = lines.Count(IsWhite);
        bool unrelatedUnchanged = filled.SequenceEqual(control);
        bool restoredExactly = filled.SequenceEqual(restored);
        File.WriteAllText(Evidence("txt"), $"FilledPixels={filledCount}; WirePixels={lineCount}; OtherExact={unrelatedUnchanged}; RestoredExact={restoredExactly}; FlagLeaked={flagLeaked}\n");
        Assert.That(filledCount, Is.GreaterThan(3000), "The quad must occupy a filled region.");
        Assert.That(lineCount, Is.InRange(50, filledCount / 4), "The wireframe capture must contain edges with most of the interior empty.");
        Assert.That(unrelatedUnchanged, Is.True);
        Assert.That(restoredExactly, Is.True);
        Assert.That(flagLeaked, Is.False);
    }

    static Camera CreateCamera(string name)
    {
        var camera = new GameObject(name).AddComponent<Camera>();
        camera.enabled = false;
        camera.transform.position = Vector3.back * 5;
        camera.orthographic = true;
        camera.orthographicSize = 2;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.allowMSAA = false;
        return camera;
    }

    static bool IsWhite(Color32 pixel) => pixel.r > 245 && pixel.g > 245 && pixel.b > 245;

    static string Evidence(string suffix) => $"Evidence/P13-WireFrame-{SystemInfo.graphicsDeviceType}.{suffix}";

    Color32[] Capture(Camera camera, string label)
    {
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = _target });
        var previous = RenderTexture.active;
        try
        {
            RenderTexture.active = _target;
            _pixels.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            _pixels.Apply();
            File.WriteAllBytes(Evidence(label + ".png"), _pixels.EncodeToPNG());
            return _pixels.GetPixels32();
        }
        finally
        {
            RenderTexture.active = previous;
        }
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        GL.wireframe = false;
        if (_target != null) { _target.Release(); Object.DestroyImmediate(_target); }
        if (_pixels != null) Object.DestroyImmediate(_pixels);
        if (_material != null) Object.DestroyImmediate(_material);
        if (Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }
}
