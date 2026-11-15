// Crest 4 URP port. MIT; see LICENSE.Crest.
using System;
using System.Collections;
using System.IO;
using System.Text;
using Crest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

[DefaultExecutionOrder(300)]
public sealed class ShadowHistoryCapture : MonoBehaviour
{
    public Action Capture;

    void LateUpdate()
    {
        var capture = Capture;
        Capture = null;
        capture?.Invoke();
    }
}

public class ShadowHistoryTests
{
    [UnityTest]
    public IEnumerator OcclusionHistoryAccumulatesAndDecaysAtOriginalWeights()
    {
        bool graphics = OceanRenderer.EnableGraphicsInBatchMode;
        int captureRate = Time.captureFramerate;
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        var ocean = OceanRenderer.Instance;
        var camera = Camera.main;
        var shadowSettings = ScriptableObject.CreateInstance<SimSettingsShadow>();
        var originalSurface = ocean.OceanMaterial;
        var surface = new Material(originalSurface);
        var target = new RenderTexture(320, 240, 24);
        var caster = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var receiver = GameObject.CreatePrimitive(PrimitiveType.Plane);
        var recorder = camera.gameObject.AddComponent<ShadowHistoryCapture>();
        var report = new StringBuilder("RG8 shadow history at LOD0 center. Caster off/on/off, 60 Hz. Default jitter and frame weights.\nstep,frame,caster,soft,hard,expectedSoft,expectedHard,maxByteError\n");
        try
        {
            Time.captureFramerate = 60;
            camera.enabled = false;
            camera.transform.SetPositionAndRotation(new Vector3(0, 24, 0), Quaternion.Euler(90, 0, 0));
            camera.orthographic = true;
            camera.orthographicSize = 20;
            camera.aspect = 4f / 3f;
            camera.allowHDR = camera.allowMSAA = false;
            camera.cullingMask = (1 << ocean.Layer) | (1 << 5);
            ocean.ViewCamera = camera;
            foreach (var shape in Object.FindObjectsByType<ShapeWaves>()) shape.enabled = false;
            var sun = RenderSettings.sun;
            sun.transform.rotation = Quaternion.Euler(90, 0, 0);
            sun.shadows = LightShadows.Hard;
            sun.shadowStrength = 1f;
            sun.shadowBias = sun.shadowNormalBias = 0f;
            ocean._primaryLight = sun;
            ocean._simSettingsShadow = shadowSettings;
            surface.EnableKeyword("_SHADOWS_ON");
            surface.SetFloat("_Shadows", 1f);
            ocean.OceanMaterial = surface;
            var serialized = new SerializedObject(ocean);
            serialized.FindProperty("_createShadowData").boolValue = true;
            serialized.FindProperty("_minScale").floatValue = 8;
            serialized.FindProperty("_maxScale").floatValue = 8;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            caster.layer = receiver.layer = 5;
            receiver.transform.position = new Vector3(0, -3, 0);
            receiver.transform.localScale = Vector3.one * 10;
            caster.transform.position = new Vector3(0, 5, 0);
            caster.transform.localScale = new Vector3(20, 2, 20);
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/CrestPortDemos/Reference.mat");
            receiver.GetComponent<Renderer>().sharedMaterial = material;
            var casterRenderer = caster.GetComponent<Renderer>();
            casterRenderer.sharedMaterial = material;
            casterRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            caster.SetActive(false);
            Assert.That(target.Create(), Is.True);
            for (int frame = 0; frame < 12; frame++) yield return null;

            var shadows = ocean._lodDataShadow;
            Assert.That(shadows, Is.Not.Null);
            Assert.That(shadows.DataTexture.graphicsFormat, Is.EqualTo(GraphicsFormat.R8G8_UNorm));
            var previous = Vector2Int.zero;
            var peak = Vector2Int.zero;
            float maximumError = 0;
            int previousFrame = -1;
            for (int step = 0; step < 65; step++)
            {
                bool shadowed = step > 0 && step <= 32;
                caster.SetActive(shadowed);
                var actual = Vector2Int.zero;
                int sampleFrame = -1;
                float delta = 0;
                Exception failure = null;
                recorder.Capture = () =>
                {
                    try
                    {
                        Assert.That(BuildCommandBufferBase._lastUpdateFrame, Is.EqualTo(OceanRenderer.FrameCount));
                        sampleFrame = Time.frameCount;
                        delta = Time.deltaTime;
                        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                        var texture = shadows.DataTexture;
                        var readback = AsyncGPUReadback.Request(texture, 0);
                        readback.WaitForCompletion();
                        Assert.That(readback.hasError, Is.False);
                        var bytes = readback.GetData<byte>(0);
                        int center = 2 * (texture.height / 2 * texture.width + texture.width / 2);
                        actual = new Vector2Int(bytes[center], bytes[center + 1]);
                    }
                    catch (Exception exception) { failure = exception; }
                };
                yield return null;
                if (failure != null) throw failure;
                Assert.That(sampleFrame, Is.GreaterThanOrEqualTo(0));
                Assert.That(delta, Is.EqualTo(1f / 60f).Within(.00001f));
                if (step > 0) Assert.That(sampleFrame, Is.EqualTo(previousFrame + 1));
                previousFrame = sampleFrame;
                float destination = shadowed ? 255 : 0;
                float soft = Mathf.Lerp(previous.x, destination, shadowSettings._currentFrameWeightSoft * delta * 60);
                float hard = Mathf.Lerp(previous.y, destination, shadowSettings._currentFrameWeightHard * delta * 60);
                float error = Mathf.Max(Mathf.Abs(soft - actual.x), Mathf.Abs(hard - actual.y));
                maximumError = Mathf.Max(maximumError, error);
                report.AppendLine(FormattableString.Invariant($"{step},{sampleFrame},{shadowed},{actual.x},{actual.y},{soft:F3},{hard:F3},{error:F3}"));
                peak = Vector2Int.Max(peak, actual);
                previous = actual;
            }
            report.AppendLine(FormattableString.Invariant($"Peak={peak}; final={previous}; maxByteError={maximumError:R}"));
            Assert.That(peak.x, Is.GreaterThan(100));
            Assert.That(peak.y, Is.GreaterThan(220));
            Assert.That(previous.x, Is.LessThan(peak.x));
            Assert.That(previous.y, Is.LessThan(peak.y));
            Assert.That(maximumError, Is.LessThanOrEqualTo(1), "Both channels must match the original recurrence within one UNorm byte.");
        }
        finally
        {
            File.WriteAllText($"Evidence/P12-ShadowHistory-{SystemInfo.graphicsDeviceType}.txt", report.ToString());
            target.Release();
            Object.Destroy(target);
            var serialized = new SerializedObject(ocean);
            serialized.FindProperty("_createShadowData").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            ocean.OceanMaterial = originalSurface;
            Object.Destroy(surface);
            Object.Destroy(shadowSettings);
            Time.captureFramerate = captureRate;
            OceanRenderer.EnableGraphicsInBatchMode = graphics;
        }
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator ExitFailedPlayMode()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
