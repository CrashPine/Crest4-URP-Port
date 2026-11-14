// Compiled transfer of the two optional local-probe controls. MIT.
#if CREST_RESOURCE_PROBE
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Crest.URP.Diagnostics
{
    public sealed partial class ResourceShutdownProbe
    {
        [Serializable]
        sealed class LocalProbeReport
        {
            public string api, unity, failure, forwardMode, clusterMode;
            public int boxPixels, atlasPixels, boxRestore, atlasRestore, errors;
            public bool passed;
        }

        LocalProbeReport _localProbes;

        void SaveLocalProbes(string failure = "")
        {
            _localProbes.failure = failure;
            _localProbes.errors = _errors;
            File.WriteAllText(Argument("-crestProbeOutput", "LocalProbes.json"), JsonUtility.ToJson(_localProbes, true));
        }

        IEnumerator LocalProbesPlayer()
        {
            _localProbes = new LocalProbeReport { api = SystemInfo.graphicsDeviceType.ToString(), unity = Application.unityVersion };
            yield return SceneManager.LoadSceneAsync(1);
            var camera = Camera.main;
            var settings = camera.GetUniversalAdditionalCameraData();
            var pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            CheckExample(pipeline.reflectionProbeBlending && pipeline.reflectionProbeBoxProjection && pipeline.reflectionProbeAtlas,
                "Missing serialized probe pipeline settings");
            camera.enabled = false;
            camera.transform.SetPositionAndRotation(new Vector3(0, 4, -12), Quaternion.Euler(16, 0, 0));
            camera.cullingMask = 1 << 4;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.blue;
            camera.allowHDR = camera.allowMSAA = false;
            settings.renderPostProcessing = false;
            foreach (var shape in FindObjectsByType<ShapeWaves>()) shape.enabled = false;
            var ocean = OceanRenderer.Instance;
            var original = ocean.OceanMaterial;
            var surface = Resources.Load<Material>("CrestLocalProbeSurface");
            CheckExample(surface != null, "Missing packed probe surface material");
            ocean.OceanMaterial = surface;
            var clock = new ExampleFixedTime();
            ocean.PushTimeProvider(clock);

            var target = new RenderTexture(320, 180, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(320, 180, TextureFormat.RGBA32, false);
            var red = ProbeCubemap(Color.red, true);
            var green = ProbeCubemap(Color.green);
            var solidRed = ProbeCubemap(Color.red);
            var probe = new GameObject("Box projection probe").AddComponent<ReflectionProbe>();
            var second = new GameObject("Atlas second probe").AddComponent<ReflectionProbe>();
            target.Create();
            try
            {
                settings.SetRenderer(0);
                probe.mode = ReflectionProbeMode.Custom;
                probe.customBakedTexture = red;
                probe.transform.position = new Vector3(12, 0, 0);
                probe.size = new Vector3(80, 50, 160);
                probe.blendDistance = 1;
                second.enabled = false;
                for (int i = 0; i < 12; i++) yield return null;
                Color32[] baseline = null, changed = null, restored = null;
                yield return ModeCapture(() => baseline = CaptureProbe(camera, target, pixels, "Forward"));
                probe.boxProjection = true;
                yield return ModeCapture(() => changed = CaptureProbe(camera, target, pixels, "Forward"));
                probe.boxProjection = false;
                yield return ModeCapture(() => restored = CaptureProbe(camera, target, pixels, "Forward"));
                _localProbes.forwardMode = ActualProbeRenderer(settings);
                for (int i = 0; i < baseline.Length; i++)
                {
                    if (ProbeDifference(baseline[i], changed[i]) > 12) _localProbes.boxPixels++;
                    if (ProbeDifference(baseline[i], restored[i]) != 0) _localProbes.boxRestore++;
                }
                SaveLocalProbes();
                CheckExample(_localProbes.boxPixels > 500 && _localProbes.boxRestore == 0, "Compiled box projection did not change and restore the surface");

                settings.SetRenderer(1);
                probe.transform.position = Vector3.zero;
                probe.customBakedTexture = solidRed;
                probe.size = second.size = new Vector3(160, 64, 160);
                probe.blendDistance = second.blendDistance = 64;
                second.mode = ReflectionProbeMode.Custom;
                second.customBakedTexture = green;
                Color32[] both = null, onlyGreen = null;
                for (int i = 0; i < 4; i++) yield return null;
                yield return ModeCapture(() => baseline = CaptureProbe(camera, target, pixels, "ForwardPlus"));
                second.enabled = true;
                yield return ModeCapture(() => both = CaptureProbe(camera, target, pixels, "ForwardPlus"));
                probe.enabled = false;
                yield return ModeCapture(() => onlyGreen = CaptureProbe(camera, target, pixels, "ForwardPlus"));
                probe.enabled = true;
                second.enabled = false;
                yield return ModeCapture(() => restored = CaptureProbe(camera, target, pixels, "ForwardPlus"));
                _localProbes.clusterMode = ActualProbeRenderer(settings);
                for (int i = 0; i < both.Length; i++)
                {
                    if (baseline[i].r - both[i].r > 5 && both[i].r - onlyGreen[i].r > 5 &&
                        onlyGreen[i].g - both[i].g > 5 && both[i].g - baseline[i].g > 5) _localProbes.atlasPixels++;
                    if (ProbeDifference(baseline[i], restored[i]) != 0) _localProbes.atlasRestore++;
                }
                CheckExample(_localProbes.atlasPixels > 500 && _localProbes.atlasRestore == 0, "Compiled atlas did not blend and restore the surface");
                CheckExample(_errors == 0, "Local probe Player logged errors");
                _localProbes.passed = true;
            }
            finally
            {
                ocean.PopTimeProvider(clock);
                ocean.OceanMaterial = original;
                Destroy(probe.gameObject);
                Destroy(second.gameObject);
                Destroy(red);
                Destroy(green);
                Destroy(solidRed);
                Destroy(pixels);
                target.Release();
                Destroy(target);
            }
            SaveLocalProbes();
            Application.Quit(_errors == 0 ? 0 : 1);
        }

        static string ActualProbeRenderer(UniversalAdditionalCameraData settings)
        {
            var renderer = settings.scriptableRenderer;
            return renderer.GetType().GetProperty("renderingModeActual", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(renderer)?.ToString();
        }

        static Color32[] CaptureProbe(Camera camera, RenderTexture target, Texture2D pixels, string expectedRenderer)
        {
            CheckExample(ActualProbeRenderer(camera.GetUniversalAdditionalCameraData()) == expectedRenderer, "Unexpected actual probe renderer");
            var previous = RenderTexture.active;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                return pixels.GetPixels32();
            }
            finally { RenderTexture.active = previous; }
        }

        static Cubemap ProbeCubemap(Color color, bool directional = false)
        {
            var cube = new Cubemap(16, TextureFormat.RGBA32, true);
            var face = new Color[16 * 16];
            for (int i = 0; i < 6; i++)
            {
                Array.Fill(face, directional && i == (int)CubemapFace.PositiveX ? Color.green : color);
                cube.SetPixels(face, (CubemapFace)i);
            }
            cube.Apply();
            return cube;
        }

        static int ProbeDifference(Color32 a, Color32 b) =>
            Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
    }
}
#endif
