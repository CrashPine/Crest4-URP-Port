// Standalone demo navigation and real-player smoke evidence. MIT.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Crest.URP
{
    // OceanRenderer's preserved script execution order is200.
    [DefaultExecutionOrder(300)]
    public sealed partial class PortDemoController : MonoBehaviour
    {
        string _pendingCapture;
        bool _smoke;
#if DEVELOPMENT_BUILD
        bool _checkWaveCrossing;
#else
        const bool _checkWaveCrossing = false;
#endif
        int _errors;
        string _evidence;
        readonly List<string> _results = new List<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Application.isEditor || !SceneManager.GetActiveScene().name.StartsWith("Ocean_")) return;
            var owner = new GameObject("Crest demonstration controls");
            DontDestroyOnLoad(owner);
            owner.AddComponent<PortDemoController>();
        }

        void Awake()
        {
            var args = Environment.GetCommandLineArgs();
#if DEVELOPMENT_BUILD
            _checkWaveCrossing = Array.IndexOf(args, "-crestWaveCrossing") >= 0;
#endif
            _smoke = Array.IndexOf(args, "-crestSmoke") >= 0 || _checkWaveCrossing;
            var index = Array.IndexOf(args, "-crestEvidence");
            _evidence = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.Combine(Application.persistentDataPath, "CrestEvidence");
            if (_smoke)
            {
                OceanRenderer.EnableGraphicsInBatchMode = true;
                Application.logMessageReceived += RecordError;
                if (!_checkWaveCrossing) StartCoroutine(Smoke());
            }
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= RecordError;
            if (_smoke) OceanRenderer.EnableGraphicsInBatchMode = false;
        }

        void RecordError(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            _errors++;
            _results.Add(type + ": " + message + "\n" + stack);
        }

        IEnumerator Smoke()
        {
            Directory.CreateDirectory(_evidence);
            _results.Add($"Unity={Application.unityVersion}; API={SystemInfo.graphicsDeviceType}; GPU={SystemInfo.graphicsDeviceName}");
            for (int scene = 0; scene < SceneManager.sceneCountInBuildSettings; scene++)
            {
                yield return SceneManager.LoadSceneAsync(scene);
                var query = new SampleHeightHelper();
                int ready = 0;
                float minimum = float.PositiveInfinity, maximum = float.NegativeInfinity;
                var until = Time.realtimeSinceStartup + 5;
                while (Time.realtimeSinceStartup < until)
                {
                    query.Init(Vector3.zero, 1, true);
                    if (query.Sample(out float height) && !float.IsNaN(height) && !float.IsInfinity(height))
                    {
                        ready++;
                        minimum = Mathf.Min(minimum, height);
                        maximum = Mathf.Max(maximum, height);
                    }
                    yield return null;
                }
                var name = SceneManager.GetActiveScene().name;
                _results.Add($"{name}: ready={ready}; minimum={minimum}; maximum={maximum}");
                if (ready < 30 || maximum - minimum < .001f) _errors++;
                // Coroutines resume before LateUpdate; submit only after this frame's ocean update.
                _pendingCapture = Path.Combine(_evidence, name + ".png");
                while (_pendingCapture != null) yield return null;
                yield return null;
                yield return null;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-crestReflectionOptions") >= 0)
                yield return ReflectionOptions();
            _results.Add($"Errors={_errors}. Smoke checks do not establish full visual or performance acceptance.");
            File.WriteAllLines(Path.Combine(_evidence, "player-smoke.txt"), _results);
            Application.Quit(_errors == 0 ? 0 : 1);
        }

        void LateUpdate()
        {
#if DEVELOPMENT_BUILD
            if (_checkWaveCrossing)
            {
                CheckWaveCrossing();
                return;
            }
#endif
            if (_pendingCapture == null) return;
            try
            {
                var ocean = OceanRenderer.Instance;
                int lodFrame = ocean._lodTransform._renderData[0].Current._frame;
                _results.Add($"CaptureFrame: scene={SceneManager.GetActiveScene().name}; frame={Time.frameCount}; lodFrame={lodFrame}");
                if (lodFrame != Time.frameCount) _errors++;
                CaptureCamera(_pendingCapture);
            }
            finally { _pendingCapture = null; }
        }

        void OnGUI()
        {
            if (_smoke) return;
            GUILayout.BeginArea(new Rect(15, 15, 320, 260), GUI.skin.box);
            GUILayout.Label("Crest 4 / Unity " + Application.unityVersion);
            GUILayout.Label(SceneManager.GetActiveScene().name);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Previous")) Navigate(-1);
            if (GUILayout.Button("Next")) Navigate(1);
            GUILayout.EndHorizontal();
            var ocean = OceanRenderer.Instance;
            if (ocean != null)
            {
                GUILayout.Label("Wind: " + ocean._globalWindSpeed.ToString("F0") + " km/h");
                ocean._globalWindSpeed = GUILayout.HorizontalSlider(ocean._globalWindSpeed, 0, 120);
                GUILayout.Label("Sea level: " + ocean.SeaLevel.ToString("F1") + " m");
                var position = ocean.transform.position;
                position.y = GUILayout.HorizontalSlider(position.y, -5, 5);
                ocean.transform.position = position;
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Camera up") && Camera.main != null) Camera.main.transform.position += Vector3.up * 2;
            if (GUILayout.Button("Camera down") && Camera.main != null) Camera.main.transform.position -= Vector3.up * 2;
            GUILayout.EndHorizontal();
            GUILayout.Label("Wind changes use the original Crest spectrum controls.");
            GUILayout.EndArea();
        }

        static void CaptureCamera(string path)
        {
            var target = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(960, 540, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                target.Create();
                RenderPipeline.SubmitRenderRequest(Camera.main,
                    new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                Destroy(target);
                Destroy(pixels);
            }
        }

        void Navigate(int direction)
        {
            int count = SceneManager.sceneCountInBuildSettings;
            SceneManager.LoadScene((SceneManager.GetActiveScene().buildIndex + direction + count) % count);
        }
    }
}
