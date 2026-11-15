// Compiled transfer of the two optional local-probe controls. MIT.
#if CREST_RESOURCE_PROBE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
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
            public int boxPixels, atlasPixels, boxRestore, atlasRestore, boxProbeResponse, errors;
            public int savedBoxPixels, savedBoxRestore, savedAtlasPixels, savedAtlasRestore;
            public bool passed;
            public bool automaticCamera, refreshProbeCulling;
            public List<LocalProbeCapture> captures = new List<LocalProbeCapture>();
        }

        [Serializable]
        sealed class LocalProbeCapture
        {
            public string state, assignedTexture, nativeTexture;
            public int probeLayer;
            public int observerCalls, width, height, nonClearPixels;
            public List<string> probes = new List<string>();
        }

        sealed class LocalProbeObserver : ICameraGraphPass
        {
            public RenderPassEvent Stage => RenderPassEvent.AfterRendering;
            public Camera Camera;
            public ReflectionProbe Probe;
            public LocalProbeCapture Capture;

            public void Record(RenderGraph graph, ContextContainer context)
            {
                var camera = context.Get<UniversalCameraData>();
                if (Capture == null || camera.camera != Camera) return;
                Capture.observerCalls++;
                Capture.width = camera.cameraTargetDescriptor.width;
                Capture.height = camera.cameraTargetDescriptor.height;
                foreach (var probe in context.Get<UniversalRenderingData>().cullResults.visibleReflectionProbes)
                {
                    var texture = probe.texture;
                    var component = probe.reflectionProbe;
                    Capture.probes.Add($"{component?.name}: texture={texture?.name}, box={probe.isBoxProjection}");
                }
            }
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
            var arguments = Environment.GetCommandLineArgs();
            _localProbes = new LocalProbeReport
            {
                api = SystemInfo.graphicsDeviceType.ToString(), unity = Application.unityVersion,
                automaticCamera = Array.IndexOf(arguments, "-crestProbeAutomaticCamera") >= 0,
                refreshProbeCulling = Array.IndexOf(arguments, "-crestProbeRefreshCulling") >= 0,
            };
            bool runBox = Array.IndexOf(arguments, "-crestProbeAtlasOnly") < 0;
            bool runAtlas = Array.IndexOf(arguments, "-crestProbeBoxOnly") < 0;
            yield return SceneManager.LoadSceneAsync(1);
            var camera = Camera.main;
            var settings = camera.GetUniversalAdditionalCameraData();
            var qualityPipeline = QualitySettings.renderPipeline;
            var atlasPipeline = Resources.Load<UniversalRenderPipelineAsset>("CrestLocalProbeAtlasPipeline");
            var boxPipeline = Resources.Load<UniversalRenderPipelineAsset>("CrestLocalProbeBoxPipeline");
            CheckExample(atlasPipeline != null && atlasPipeline.reflectionProbeBlending && atlasPipeline.reflectionProbeAtlas && !atlasPipeline.reflectionProbeBoxProjection
                && boxPipeline != null && boxPipeline.reflectionProbeBoxProjection && !boxPipeline.reflectionProbeBlending,
                "Missing distinct serialized probe pipeline settings");
            var ocean = OceanRenderer.Instance;
            var original = ocean.OceanMaterial;
            var surface = Resources.Load<Material>("CrestLocalProbeSurface");
            CheckExample(surface != null, "Missing packed probe surface material");
            var clock = new ExampleFixedTime();

            var target = new RenderTexture(320, 180, 24, RenderTextureFormat.ARGB32);
            var originalTarget = camera.targetTexture;
            var pixels = new Texture2D(320, 180, TextureFormat.RGBA32, false);
            var red = Resources.Load<Cubemap>("Directional red-green");
            var green = Resources.Load<Cubemap>("Solid green");
            var solidRed = Resources.Load<Cubemap>("Solid red");
            var primary = GameObject.Find("Crest Local Probe Primary");
            var secondary = GameObject.Find("Crest Local Probe Secondary");
            CheckExample(primary != null && secondary != null && red != null && green != null && solidRed != null,
                "Missing saved local probes or cubemap assets");
            var probe = primary.GetComponent<ReflectionProbe>();
            var second = secondary.GetComponent<ReflectionProbe>();
            target.Create();
            ConfigureLocalProbeCamera(camera, target, surface, clock);
            var observer = new LocalProbeObserver { Camera = camera, Probe = probe };
            CameraGraphPassRegistry.Register(observer);
            GameObject litControl = null;
            try
            {
                Color32[] savedBaseline = null;
                second.enabled = false;
                Color32[] baseline = null, restored = null;
                if (runBox)
                {
                    QualitySettings.renderPipeline = boxPipeline;
                    settings.SetRenderer(0);
                    probe.customBakedTexture = red;
                    probe.transform.position = new Vector3(12, 0, 0);
                    probe.size = new Vector3(80, 50, 160);
                    probe.blendDistance = 1;
                    for (int i = 0; i < 12; i++) yield return null;
                    Color32[] changed = null, probeOff = null;
                    yield return CaptureLocalState(camera, target, pixels, "Forward", value => baseline = value, observer, "BoxOff");
                    savedBaseline = baseline;
                    probe.boxProjection = true;
                    yield return null;
                    yield return CaptureLocalState(camera, target, pixels, "Forward", value => changed = value, observer, "BoxOn");
                    probe.boxProjection = false;
                    yield return null;
                    yield return CaptureLocalState(camera, target, pixels, "Forward", value => restored = value, observer, "BoxRestored");
                    probe.enabled = false;
                    yield return null;
                    yield return CaptureLocalState(camera, target, pixels, "Forward", value => probeOff = value, observer, "ProbeOff");
                    probe.enabled = true;
                    _localProbes.forwardMode = ActualProbeRenderer(settings);
                    for (int i = 0; i < baseline.Length; i++)
                    {
                        if (ProbeDifference(baseline[i], changed[i]) > 12) _localProbes.boxPixels++;
                        if (ProbeDifference(baseline[i], restored[i]) != 0) _localProbes.boxRestore++;
                        if (ProbeDifference(baseline[i], probeOff[i]) > 12) _localProbes.boxProbeResponse++;
                    }
                    var lit = Resources.Load<Material>("CrestLocalProbeLit");
                    CheckExample(lit != null, "Missing packed URP/Lit control");
                    litControl = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    litControl.name = "URP Lit probe control";
                    litControl.layer = 4;
                    litControl.transform.position = new Vector3(0, 1, -5);
                    litControl.GetComponent<Renderer>().sharedMaterial = lit;
                    probe.customBakedTexture = solidRed;
                    yield return null;
                    yield return CaptureLocalState(camera, target, pixels, "Forward", _ => { }, observer, "LitRed");
                    probe.customBakedTexture = green;
                    yield return null;
                    yield return CaptureLocalState(camera, target, pixels, "Forward", _ => { }, observer, "LitGreen");
                    litControl.SetActive(false);
                    SaveLocalProbes();
                }
                if (runAtlas)
                {
                    QualitySettings.renderPipeline = atlasPipeline;
                    settings.SetRenderer(1);
                    probe.transform.position = Vector3.zero;
                    probe.customBakedTexture = solidRed;
                    probe.size = second.size = new Vector3(160, 64, 160);
                    probe.blendDistance = second.blendDistance = 64;
                    second.customBakedTexture = green;
                    Color32[] both = null, onlyGreen = null;
                    for (int i = 0; i < 4; i++) yield return null;
                    yield return CaptureLocalState(camera, target, pixels, "ForwardPlus", value => baseline = value, observer, "AtlasRed");
                    second.enabled = true;
                    yield return null;
                    yield return CaptureLocalState(camera, target, pixels, "ForwardPlus", value => both = value, observer, "AtlasOverlap");
                    probe.enabled = false;
                    yield return null;
                    yield return CaptureLocalState(camera, target, pixels, "ForwardPlus", value => onlyGreen = value, observer, "AtlasGreen");
                    probe.enabled = true;
                    second.enabled = false;
                    yield return null;
                    yield return CaptureLocalState(camera, target, pixels, "ForwardPlus", value => restored = value, observer, "AtlasRestored");
                    _localProbes.clusterMode = ActualProbeRenderer(settings);
                    for (int i = 0; i < both.Length; i++)
                    {
                        if (baseline[i].r - both[i].r > 5 && both[i].r - onlyGreen[i].r > 5 &&
                            onlyGreen[i].g - both[i].g > 5 && both[i].g - baseline[i].g > 5) _localProbes.atlasPixels++;
                        if (ProbeDifference(baseline[i], restored[i]) != 0) _localProbes.atlasRestore++;
                    }
                    SaveLocalProbes();
                }
                if (runBox)
                {
                    if (SceneManager.sceneCountInBuildSettings > 2)
                    {
                        Color32[] projected = null, reloaded = null;
                        yield return CaptureSavedProbe(2, 0, "SavedBoxOn", target, pixels, surface, clock, observer, value => projected = value);
                        yield return CaptureSavedProbe(1, 0, "SavedBoxOffReloaded", target, pixels, surface, clock, observer, value => reloaded = value);
                        for (int i = 0; i < savedBaseline.Length; i++)
                        {
                            if (ProbeDifference(savedBaseline[i], projected[i]) > 12) _localProbes.savedBoxPixels++;
                            if (ProbeDifference(savedBaseline[i], reloaded[i]) != 0) _localProbes.savedBoxRestore++;
                        }
                        SaveLocalProbes();
                    }
                    CheckExample(_localProbes.boxProbeResponse > 500, "Compiled Forward surface did not respond to probe registration");
                    CheckExample(_localProbes.boxPixels > 500 && _localProbes.boxRestore == 0, "Compiled box projection did not change and restore the surface");
                }
                if (runAtlas)
                {
                    if (SceneManager.sceneCountInBuildSettings == 5)
                    {
                        Color32[] redOnly = null, overlap = null, greenOnly = null, redReloaded = null;
                        yield return CaptureSavedProbe(2, 1, "SavedRed", target, pixels, surface, clock, observer, value => redOnly = value);
                        yield return CaptureSavedProbe(3, 1, "SavedOverlap", target, pixels, surface, clock, observer, value => overlap = value);
                        yield return CaptureSavedProbe(4, 1, "SavedGreen", target, pixels, surface, clock, observer, value => greenOnly = value);
                        yield return CaptureSavedProbe(2, 1, "SavedRedReloaded", target, pixels, surface, clock, observer, value => redReloaded = value);
                        for (int i = 0; i < overlap.Length; i++)
                        {
                            if (redOnly[i].r - overlap[i].r > 5 && overlap[i].r - greenOnly[i].r > 5 &&
                                greenOnly[i].g - overlap[i].g > 5 && overlap[i].g - redOnly[i].g > 5) _localProbes.savedAtlasPixels++;
                            if (ProbeDifference(redOnly[i], redReloaded[i]) != 0) _localProbes.savedAtlasRestore++;
                        }
                        SaveLocalProbes();
                    }
                    CheckExample(_localProbes.atlasPixels > 500 && _localProbes.atlasRestore == 0, "Compiled atlas did not blend and restore the surface");
                }
                CheckExample(_errors == 0, "Local probe Player logged errors");
                _localProbes.passed = true;
            }
            finally
            {
                CameraGraphPassRegistry.Unregister(observer);
                Destroy(litControl);
                QualitySettings.renderPipeline = qualityPipeline;
                if (ocean != null)
                {
                    ocean.PopTimeProvider(clock);
                    ocean.OceanMaterial = original;
                }
                Destroy(pixels);
                if (camera != null) camera.targetTexture = originalTarget;
                target.Release();
                Destroy(target);
            }
            SaveLocalProbes();
            Application.Quit(_errors == 0 ? 0 : 1);
        }

        static void ConfigureLocalProbeCamera(Camera camera, RenderTexture target, Material surface, ITimeProvider clock)
        {
            camera.transform.SetPositionAndRotation(new Vector3(0, 4, -12), Quaternion.Euler(16, 0, 0));
            camera.cullingMask = 1 << 4;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.blue;
            camera.allowHDR = camera.allowMSAA = false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            camera.targetTexture = target;
            foreach (var shape in FindObjectsByType<ShapeWaves>()) shape.enabled = false;
            var ocean = OceanRenderer.Instance;
            ocean.OceanMaterial = surface;
            ocean.PushTimeProvider(clock);
        }

        IEnumerator CaptureSavedProbe(int sceneIndex, int renderer, string state, RenderTexture target, Texture2D pixels, Material surface,
            ITimeProvider clock, LocalProbeObserver observer, Action<Color32[]> save)
        {
            yield return SceneManager.LoadSceneAsync(sceneIndex);
            var camera = Camera.main;
            var ocean = OceanRenderer.Instance;
            var original = ocean.OceanMaterial;
            var previousTarget = camera.targetTexture;
            try
            {
                ConfigureLocalProbeCamera(camera, target, surface, clock);
                camera.GetUniversalAdditionalCameraData().SetRenderer(renderer);
                observer.Camera = camera;
                observer.Probe = GameObject.Find("Crest Local Probe Primary").GetComponent<ReflectionProbe>();
                for (int i = 0; i < 12; i++) yield return null;
                yield return CaptureLocalState(camera, target, pixels, renderer == 0 ? "Forward" : "ForwardPlus", save, observer, state);
            }
            finally
            {
                ocean.PopTimeProvider(clock);
                ocean.OceanMaterial = original;
                camera.targetTexture = previousTarget;
            }
        }

        static string ActualProbeRenderer(UniversalAdditionalCameraData settings)
        {
            var renderer = settings.scriptableRenderer;
            return renderer.GetType().GetProperty("renderingModeActual", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(renderer)?.ToString();
        }

        IEnumerator CaptureLocalState(Camera camera, RenderTexture target, Texture2D pixels, string expectedRenderer,
            Action<Color32[]> save, LocalProbeObserver observer, string state)
        {
            CheckExample(ActualProbeRenderer(camera.GetUniversalAdditionalCameraData()) == expectedRenderer, "Unexpected actual probe renderer");
            var probe = observer.Probe;
            observer.Capture = new LocalProbeCapture
            {
                state = state, probeLayer = probe.gameObject.layer,
                assignedTexture = probe.customBakedTexture?.name, nativeTexture = probe.texture?.name,
            };
            try
            {
                if (_localProbes.automaticCamera)
                {
                    float deadline = Time.realtimeSinceStartup + 10;
                    while (observer.Capture.observerCalls == 0 && Time.realtimeSinceStartup < deadline)
                        yield return null;
                }
                else
                {
                    yield return ModeCapture(() =>
                    {
                        // Keep the explicit request aligned with probe changes made in the player loop.
                        if (_localProbes.refreshProbeCulling) ReflectionProbe.UpdateCachedState();
                        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    });
                }
                CheckExample(observer.Capture.observerCalls > 0, "No probe camera frame was recorded");
                save(ReadLocalProbeState(target, pixels, observer.Capture));
            }
            finally { observer.Capture = null; }
        }

        Color32[] ReadLocalProbeState(RenderTexture target, Texture2D pixels, LocalProbeCapture capture)
        {
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                var result = pixels.GetPixels32();
                foreach (var pixel in result)
                    if (ProbeDifference(pixel, new Color32(0, 0, 255, 255)) > 12) capture.nonClearPixels++;
                _localProbes.captures.Add(capture);
                var directory = Path.GetDirectoryName(Path.GetFullPath(Argument("-crestProbeOutput", "LocalProbes.json")));
                File.WriteAllBytes(Path.Combine(directory, capture.state + ".png"), pixels.EncodeToPNG());
                return result;
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        static int ProbeDifference(Color32 a, Color32 b) =>
            Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
    }
}
#endif
