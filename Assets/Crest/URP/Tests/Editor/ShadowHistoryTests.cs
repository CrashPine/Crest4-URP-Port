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
    enum Control { History, Penumbra, CascadeTransfer, Override }

    [UnityTest]
    public IEnumerator OcclusionHistoryAccumulatesAndDecaysAtOriginalWeights()
        => ValidateShadowHistory(Control.History);

    [UnityTest]
    public IEnumerator SoftJitterWidensTheShadowEdgeAndRestores()
        => ValidateShadowHistory(Control.Penumbra);

    [UnityTest]
    public IEnumerator ScaleChangesReadTheMatchingPreviousShadowCascade()
        => ValidateShadowHistory(Control.CascadeTransfer);

    [UnityTest]
    public IEnumerator GeometryOverridesBothShadowChannels()
        => ValidateShadowHistory(Control.Override);

    IEnumerator ValidateShadowHistory(Control control)
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
        var report = new StringBuilder();
        string reportName = control switch
        {
            Control.Penumbra => "ShadowPenumbra",
            Control.CascadeTransfer => "ShadowCascadeTransfer",
            Control.Override => "ShadowOverride",
            _ => "ShadowHistory",
        };
        try
        {
            Time.captureFramerate = 60;
            camera.enabled = false;
            camera.transform.SetPositionAndRotation(new Vector3(0, control == Control.CascadeTransfer ? 12 : 24, 0), Quaternion.Euler(90, 0, 0));
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
            serialized.FindProperty("_maxScale").floatValue = control == Control.CascadeTransfer ? 32 : 8;
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
            if (control == Control.Penumbra)
            {
                caster.SetActive(true);
                yield return CheckPenumbra(ocean, camera, target, recorder, shadows, shadowSettings, report);
            }
            else if (control == Control.CascadeTransfer)
            {
                yield return CheckCascadeTransfer(ocean, camera, target, recorder, shadows, shadowSettings, report);
            }
            else if (control == Control.Override)
            {
                yield return CheckOverride(ocean, camera, target, recorder, shadows, shadowSettings, report);
            }
            else
            {
                yield return CheckHistory(camera, target, caster, recorder, shadows, shadowSettings, report);
            }
        }
        finally
        {
            string milestone = control == Control.Override ? "P13" : "P12";
            File.WriteAllText($"Evidence/{milestone}-{reportName}-{SystemInfo.graphicsDeviceType}.txt", report.ToString());
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

    static IEnumerator CheckOverride(OceanRenderer ocean, Camera camera, RenderTexture target,
        ShadowHistoryCapture recorder, LodDataMgrShadow shadows, SimSettingsShadow settings, StringBuilder report)
    {
        // A full weight returns to the current unoccluded atlas in one update.
        settings._currentFrameWeightSoft = settings._currentFrameWeightHard = 1;
        settings._jitterDiameterSoft = settings._jitterDiameterHard = 0;
        var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.transform.localScale = Vector3.one * .8f;
        var shader = Shader.Find("Crest/Inputs/Shadows/Override Shadows");
        Assert.That(shader, Is.Not.Null);
        var material = new Material(shader);
        try
        {
            plane.GetComponent<Renderer>().sharedMaterial = material;
            var input = plane.AddComponent<RegisterShadowInput>();
            input.enabled = false;
            report.AppendLine("LOD0 RG8, unoccluded atlas, full current-frame weight, zero jitter. Four scheduled camera renders.");
            foreach (var (phase, value) in new[] { ("Baseline", 0f), ("Quarter", .25f), ("ThreeQuarters", .75f), ("Disabled", 0f) })
            {
                material.SetFloat("_ShadowValue", value);
                input.enabled = value > 0;
                // Let registration and the input's Renderer state settle before submitting a camera request.
                yield return null;
                Exception failure = null;
                bool completed = false;
                recorder.Capture = () =>
                {
                    try
                    {
                        Assert.That(BuildCommandBufferBase._lastUpdateFrame, Is.EqualTo(OceanRenderer.FrameCount));
                        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                        var field = shadows.DataTexture;
                        var lod = ocean._lodTransform._renderData[0].Current;
                        var readback = AsyncGPUReadback.Request(field, 0);
                        readback.WaitForCompletion();
                        Assert.That(readback.hasError, Is.False);
                        var bytes = readback.GetData<byte>(0);
                        foreach (float x in new[] { -2f, 2f, 7f })
                        {
                            int column = Mathf.FloorToInt((x - lod._posSnapped.x) / lod._texelWidth + field.width * .5f);
                            int row = Mathf.FloorToInt(-lod._posSnapped.z / lod._texelWidth + field.height * .5f);
                            int offset = (row * field.width + column) * 2;
                            int expected = Mathf.Abs(x) < 4 ? Mathf.RoundToInt(value * 255) : 0;
                            report.AppendLine($"{phase} x={x} soft={bytes[offset]} hard={bytes[offset + 1]} expected={expected}");
                            Assert.That(bytes[offset], Is.EqualTo(expected), $"{phase} soft at {x}");
                            Assert.That(bytes[offset + 1], Is.EqualTo(expected), $"{phase} hard at {x}");
                        }
                        completed = true;
                    }
                    catch (Exception exception) { failure = exception; }
                };
                yield return null;
                if (failure != null) throw failure;
                Assert.That(completed, Is.True, phase);
            }
        }
        finally
        {
            Object.Destroy(plane);
            Object.Destroy(material);
        }
    }

    static IEnumerator CheckCascadeTransfer(OceanRenderer ocean, Camera camera, RenderTexture target,
        ShadowHistoryCapture recorder, LodDataMgrShadow shadows, SimSettingsShadow settings, StringBuilder report)
    {
        float[] heights = { 24, 40, 12, 12 };
        float[] scales = { 16, 32, 8, 8 };
        int[] changes = { 1, 1, -2, 0 };
        Assert.That(ocean.Scale, Is.EqualTo(8f));
        report.AppendLine("Each prior RG8 slice has a distinct uniform value. The caster stays off so valid history decays by the original weights.");
        report.AppendLine("height,scale,delta,layer,sourceLayer,sourceSoft,sourceHard,actualSoft,actualHard,maxError");
        for (int state = 0; state < heights.Length; state++)
        {
            var source = shadows.DataTexture;
            using (var seed = new CommandBuffer { name = "Seed distinct shadow history slices" })
            {
                for (int slice = 0; slice < source.volumeDepth; slice++)
                {
                    var value = CascadeSignature(slice);
                    seed.SetRenderTarget(source, 0, CubemapFace.Unknown, slice);
                    seed.ClearRenderTarget(false, true, new Color(value.x / 255f, value.y / 255f, 0f, 1f));
                }
                Graphics.ExecuteCommandBuffer(seed);
            }
            camera.transform.position = new Vector3(0, heights[state], 0);
            int transition = state;
            bool rendered = false;
            Exception failure = null;
            recorder.Capture = () =>
            {
                try
                {
                    Assert.That(BuildCommandBufferBase._lastUpdateFrame, Is.EqualTo(OceanRenderer.FrameCount));
                    Assert.That(ocean.Scale, Is.EqualTo(scales[transition]));
                    int change = changes[transition];
                    Assert.That(ocean._lodTransform.ScaleDifferencePow2, Is.EqualTo(change));
                    Assert.That(Shader.GetGlobalFloat(OceanRenderer.sp_CrestLodChange), Is.EqualTo(change));
                    Assert.That(shadows.GetDataTexture(1), Is.SameAs(source));
                    Assert.That(shadows.DataTexture, Is.Not.SameAs(source));
                    Assert.That(Time.deltaTime, Is.EqualTo(1f / 60f).Within(.00001f));
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    var texture = shadows.DataTexture;
                    var readback = AsyncGPUReadback.Request(texture, 0);
                    readback.WaitForCompletion();
                    Assert.That(readback.hasError, Is.False);
                    int center = 2 * (texture.height / 2 * texture.width + texture.width / 2);
                    // Test only layers with a valid, same-extent previous slice.
                    for (int layer = 2; layer <= 4; layer++)
                    {
                        int previousLayer = layer + change;
                        var lod = ocean._lodTransform._renderData[layer].Current;
                        var prior = ocean._lodTransform._renderData[previousLayer].Previous(1);
                        Assert.That(lod._texelWidth, Is.EqualTo(prior._texelWidth));
                        Assert.That(lod._posSnapped, Is.EqualTo(prior._posSnapped));
                        var value = CascadeSignature(previousLayer);
                        var bytes = readback.GetData<byte>(layer);
                        float soft = value.x * (1f - settings._currentFrameWeightSoft);
                        float hard = value.y * (1f - settings._currentFrameWeightHard);
                        float error = Mathf.Max(Mathf.Abs(soft - bytes[center]), Mathf.Abs(hard - bytes[center + 1]));
                        report.AppendLine(FormattableString.Invariant($"{heights[transition]},{ocean.Scale},{change},{layer},{previousLayer},{value.x},{value.y},{bytes[center]},{bytes[center + 1]},{error:F3}"));
                        Assert.That(error, Is.LessThanOrEqualTo(1f), "History must come from the same world-space scale.");
                    }
                    rendered = true;
                }
                catch (Exception exception) { failure = exception; }
            };
            yield return null;
            File.WriteAllText($"Evidence/P12-ShadowCascadeTransfer-{SystemInfo.graphicsDeviceType}.txt", report.ToString());
            if (failure != null) throw failure;
            Assert.That(rendered, Is.True, "The transition must be measured on the same frame.");
        }
    }

    static Vector2Int CascadeSignature(int slice) => new Vector2Int(32 + 24 * slice, 224 - 24 * slice);

    static IEnumerator CheckHistory(Camera camera, RenderTexture target, GameObject caster, ShadowHistoryCapture recorder,
        LodDataMgrShadow shadows, SimSettingsShadow shadowSettings, StringBuilder report)
    {
        report.AppendLine("RG8 shadow history at LOD0 center. Caster off/on/off, 60 Hz. Default jitter and frame weights.");
        report.AppendLine("step,frame,caster,soft,hard,expectedSoft,expectedHard,maxByteError");
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

    static IEnumerator CheckPenumbra(OceanRenderer ocean, Camera camera, RenderTexture target, ShadowHistoryCapture recorder,
        LodDataMgrShadow shadows, SimSettingsShadow settings, StringBuilder report)
    {
        const int lodIndex = 1;
        float originalJitter = settings._jitterDiameterSoft;
        settings._jitterDiameterHard = 0f;
        var profiles = new Vector2[3][];
        float[] positions = null;
        for (int phase = 0; phase < profiles.Length; phase++)
        {
            settings._jitterDiameterSoft = phase == 1 ? originalJitter : 0f;
            shadows.ClearLodData();
            int samples = 0;
            var profile = new Vector2[shadows.DataTexture.width];
            profiles[phase] = profile;
            for (int frame = 0; frame < 128; frame++)
            {
                bool measure = frame >= 112;
                Exception failure = null;
                bool rendered = false;
                recorder.Capture = () =>
                {
                    try
                    {
                        Assert.That(BuildCommandBufferBase._lastUpdateFrame, Is.EqualTo(OceanRenderer.FrameCount));
                        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                        rendered = true;
                        if (measure) SumShadowProfile(shadows.DataTexture, lodIndex, profile);
                    }
                    catch (Exception exception) { failure = exception; }
                };
                yield return null;
                if (failure != null) throw failure;
                Assert.That(rendered, Is.True, "No current-frame shadow render.");
                if (measure) samples++;
            }
            for (int x = 0; x < profile.Length; x++) profile[x] /= samples;
            var lod = ocean._lodTransform._renderData[lodIndex].Current;
            var phasePositions = new float[profile.Length];
            for (int x = 0; x < profile.Length; x++)
                phasePositions[x] = lod._posSnapped.x + (x + .5f - profile.Length * .5f) * lod._texelWidth;
            if (positions == null) positions = phasePositions;
            else Assert.That(phasePositions, Is.EqualTo(positions), "All profiles must use the same world-space samples.");
        }
        report.AppendLine(FormattableString.Invariant($"LOD{lodIndex} center-row average of the last 16 of 128 frames per phase; soft jitter 0/{originalJitter:R}/0 m; hard jitter 0 m."));
        report.AppendLine("worldX,softOff,softOn,softRestored,hardOff,hardOn,hardRestored");
        int offTransition = 0, onTransition = 0, inside = 0, outside = 0;
        float hardDifference = 0f, restoreDifference = 0f, centerOcclusion = 255f, outsideOcclusion = 0f;
        for (int x = 0; x < positions.Length; x++)
        {
            float distance = Mathf.Abs(positions[x]);
            var baseline = profiles[0][x];
            var jittered = profiles[1][x];
            var restored = profiles[2][x];
            report.AppendLine(FormattableString.Invariant($"{positions[x]:F4},{baseline.x:F2},{jittered.x:F2},{restored.x:F2},{baseline.y:F2},{jittered.y:F2},{restored.y:F2}"));
            if (distance > 22f) continue;
            hardDifference = Mathf.Max(hardDifference, Mathf.Abs(baseline.y - jittered.y));
            restoreDifference = Mathf.Max(restoreDifference, (baseline - restored).magnitude);
            if (distance < 1f)
            {
                inside++;
                centerOcclusion = Mathf.Min(centerOcclusion, jittered.x);
            }
            if (distance > 18f)
            {
                outside++;
                outsideOcclusion = Mathf.Max(outsideOcclusion, jittered.x);
            }
            if (distance < 3f || distance > 18f) continue;
            if (baseline.x is > 25f and < 225f) offTransition++;
            if (jittered.x is > 25f and < 225f) onTransition++;
        }
        report.AppendLine(FormattableString.Invariant($"TransitionTexels={offTransition}->{onTransition}; HardMaxDelta={hardDifference:R}; RestoreMaxDelta={restoreDifference:R}; CenterMin={centerOcclusion:R}; ExteriorMax={outsideOcclusion:R}"));
        File.WriteAllText($"Evidence/P12-ShadowPenumbra-{SystemInfo.graphicsDeviceType}.txt", report.ToString());
        Assert.That(inside, Is.GreaterThan(0));
        Assert.That(outside, Is.GreaterThan(0));
        Assert.That(centerOcclusion, Is.GreaterThan(225f));
        Assert.That(outsideOcclusion, Is.LessThan(5f));
        Assert.That(onTransition, Is.GreaterThan(offTransition + 8));
        Assert.That(hardDifference, Is.LessThanOrEqualTo(1f));
        Assert.That(restoreDifference, Is.LessThanOrEqualTo(1f));
    }

    static void SumShadowProfile(RenderTexture texture, int lodIndex, Vector2[] profile)
    {
        var readback = AsyncGPUReadback.Request(texture, 0);
        readback.WaitForCompletion();
        Assert.That(readback.hasError, Is.False);
        var bytes = readback.GetData<byte>(lodIndex);
        int row = texture.height / 2 * texture.width * 2;
        for (int x = 0; x < profile.Length; x++)
            profile[x] += new Vector2(bytes[row + x * 2], bytes[row + x * 2 + 1]);
    }

    [UnityTearDown]
    public IEnumerator ExitFailedPlayMode()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
