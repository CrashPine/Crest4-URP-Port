// Observes controls on the original OceanDebugGUI in an isolated Player. MIT.
#if CREST_RESOURCE_PROBE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Crest.URP.Diagnostics
{
    public sealed partial class ResourceShutdownProbe
    {
        [Serializable]
        sealed class DebugGuiRow
        {
            public string state;
            public bool visible, showSimData, focused;
            public float wind, timeScale, sampleTime, maxFieldChange, viewerSpeedKph;
            public int frame, lodFrame, fieldSamples;
        }

        [Serializable]
        sealed class DebugGuiReport
        {
            public string unity, api, gpu, phase, failure;
            public int errors;
            public List<DebugGuiRow> rows = new List<DebugGuiRow>();
            public List<DebugGuiKeyRow> keys = new List<DebugGuiKeyRow>();
        }

        [Serializable]
        sealed class DebugGuiKeyRow
        {
            public string key, layout;
            public bool native, focused, visible;
            public float timeScale;
            public int frame;
        }

        DebugGuiReport _debugGuiReport;
        OceanDebugGUI _debugGuiKeyboardTarget;
        PropertyInfo _debugGuiCurrentKeyboard;

        void ObserveDebugGuiKeyboard()
        {
            if (_debugGuiKeyboardTarget == null || _debugGuiReport.phase != "keyboard-ready") return;
            var keyboard = _debugGuiCurrentKeyboard.GetValue(null);
            foreach (string key in new[] { "g", "f" })
            {
                if (NavigationProperty(NavigationProperty(keyboard, key + "Key"), "wasPressedThisFrame") is not true)
                    continue;
                int step = _debugGuiReport.keys.Count;
                var row = new DebugGuiKeyRow
                {
                    key = key, layout = NavigationProperty(keyboard, "layout")?.ToString(),
                    native = NavigationProperty(keyboard, "native") is true, focused = Application.isFocused,
                    visible = _debugGuiKeyboardTarget._guiVisible, timeScale = Time.timeScale, frame = Time.frameCount,
                };
                _debugGuiReport.keys.Add(row);
                bool valid = row.native && row.focused && key == (step < 2 ? "g" : "f") &&
                    row.visible == (step != 0) && row.timeScale == (step == 2 ? 0f : 1f);
                if (!valid)
                    SaveDebugGui("keyboard-failed", "Unexpected native input sequence or GUI state");
                else
                    SaveDebugGui(step == 3 ? "keyboard-complete" : "keyboard-ready");
                return;
            }
        }
        static readonly System.Reflection.FieldInfo s_DebugViewerVelocity =
            typeof(OceanDebugGUI).GetField("_viewerVel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        void SaveDebugGui(string phase, string failure = null)
        {
            _debugGuiReport.phase = phase;
            _debugGuiReport.failure = failure;
            _debugGuiReport.errors = _errors;
            File.WriteAllText(Argument("-crestProbeOutput", "DebugGui.json"), JsonUtility.ToJson(_debugGuiReport, true));
        }

        void RecordDebugGui(string state, OceanDebugGUI gui, OceanRenderer ocean, Vector4[] field = null, float delta = 0)
        {
            CheckExample(Application.isFocused && gui._guiVisible, "Debug GUI requires a focused Player and visible controls");
            float viewerSpeedKph = 3.6f * ((Vector3)s_DebugViewerVelocity.GetValue(gui)).magnitude;
            CheckExample(float.IsFinite(viewerSpeedKph), "Debug GUI displays non-finite viewer speed");
            _debugGuiReport.rows.Add(new DebugGuiRow
            {
                state = state, visible = gui._guiVisible, showSimData = gui._showOceanData,
                focused = Application.isFocused, wind = ocean.WindSpeedKPH, timeScale = Time.timeScale,
                sampleTime = ocean.CurrentTime, viewerSpeedKph = viewerSpeedKph, frame = OceanRenderer.FrameCount,
                lodFrame = BuildCommandBufferBase._lastUpdateFrame, fieldSamples = field?.Length ?? 0, maxFieldChange = delta
            });
            SaveDebugGui(state);
        }

        static Vector4[] ReadDebugGuiField(OceanRenderer ocean)
        {
            CheckExample(BuildCommandBufferBase._lastUpdateFrame == OceanRenderer.FrameCount, "Animated field must be read after the current ocean update");
            var request = AsyncGPUReadback.Request(ocean._lodDataAnimWaves.DataTexture, 0, TextureFormat.RGBAFloat);
            request.WaitForCompletion();
            CheckExample(!request.hasError, "Animated waves LOD0 readback failed");
            var values = request.GetData<Vector4>(0).ToArray();
            foreach (var value in values)
                CheckExample(float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z), "Non-finite animated displacement");
            return values;
        }

        static IEnumerator WaitForDebugGui(Func<bool> condition, string control)
        {
            float deadline = Time.realtimeSinceStartup + 50;
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;
            CheckExample(condition(), "No observed change from " + control);
        }

        IEnumerator DebugGuiPlayer()
        {
            _debugGuiReport = new DebugGuiReport
            {
                unity = Application.unityVersion, api = SystemInfo.graphicsDeviceType.ToString(), gpu = SystemInfo.graphicsDeviceName
            };
            SaveDebugGui("loading");
            CheckExample(!Application.isBatchMode, "Debug GUI requires an ordinary Player");
            yield return SceneManager.LoadSceneAsync(1);
            var ocean = OceanRenderer.Instance;
            CheckExample(ocean != null, "Calm ocean not loaded");
            var gui = ocean.GetComponent<OceanDebugGUI>() ?? ocean.gameObject.AddComponent<OceanDebugGUI>();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-crestDebugGuiKeyboard") >= 0)
            {
                yield return DebugGuiKeyboard(gui);
                yield break;
            }
            var clock = new ExampleFixedTime();
            ocean.PushTimeProvider(clock);
            try
            {
                for (int i = 0; i < 30; i++) yield return null;
                yield return WaitForDebugGui(() => Application.isFocused, "Player focus");
                CheckExample(gui._showOceanData && Time.timeScale == 1, "Expected initial GUI state");
                Vector4[] initial = null;
                yield return ModeCapture(() =>
                {
                    initial = ReadDebugGuiField(ocean);
                    RecordDebugGui("ready", gui, ocean, initial);
                });
                yield return WaitForDebugGui(() => !gui._showOceanData, "Show sim data off");
                RecordDebugGui("sim-off", gui, ocean);
                yield return WaitForDebugGui(() => gui._showOceanData, "Show sim data on");
                RecordDebugGui("sim-on", gui, ocean);
                float wind = ocean.WindSpeedKPH;
                yield return WaitForDebugGui(() => Mathf.Abs(ocean.WindSpeedKPH - wind) >= 5, "Global Wind Speed slider");
                for (int i = 0; i < 20; i++) yield return null;
                yield return ModeCapture(() =>
                {
                    var changed = ReadDebugGuiField(ocean);
                    CheckExample(changed.Length == initial.Length && ocean.CurrentTime == clock.CurrentTime, "Animated field sample layout or fixed time changed");
                    float maxChange = 0;
                    for (int i = 0; i < initial.Length; i++)
                    {
                        Vector4 d = changed[i] - initial[i];
                        maxChange = Mathf.Max(maxChange, Mathf.Max(Mathf.Abs(d.x), Mathf.Max(Mathf.Abs(d.y), Mathf.Abs(d.z))));
                    }
                    CheckExample(maxChange > .001f, "Wind slider did not change native animated displacement");
                    RecordDebugGui("wind-changed", gui, ocean, changed, maxChange);
                });
                yield return WaitForDebugGui(() => Time.timeScale == 0, "Freeze time on");
                yield return ModeCapture(() => RecordDebugGui("paused", gui, ocean));
                yield return WaitForDebugGui(() => Time.timeScale == 1, "Freeze time off");
                yield return ModeCapture(() => RecordDebugGui("resumed", gui, ocean));
                SaveDebugGui("complete");
                Application.Quit(_errors == 0 ? 0 : 1);
            }
            finally
            {
                ocean.PopTimeProvider(clock);
            }
        }

        IEnumerator DebugGuiKeyboard(OceanDebugGUI gui)
        {
            _debugGuiCurrentKeyboard = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("UnityEngine.InputSystem.Keyboard"))
                .FirstOrDefault(type => type != null)?.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
            CheckExample(_debugGuiCurrentKeyboard != null, "Native keyboard type unavailable");
            yield return WaitForDebugGui(() => Application.isFocused && gui._guiVisible, "focused visible GUI");
            CheckExample(Time.timeScale == 1f, "Expected normal time before keyboard input");
            _debugGuiKeyboardTarget = gui;
            SaveDebugGui("keyboard-ready");
            float deadline = Time.realtimeSinceStartup + 120;
            try
            {
                while (_debugGuiReport.phase == "keyboard-ready" && Time.realtimeSinceStartup < deadline)
                    yield return null;
                CheckExample(_debugGuiReport.phase == "keyboard-complete" && _debugGuiReport.keys.Count == 4,
                    "Native G/G/F/F controls incomplete: " + _debugGuiReport.failure);
                Application.Quit(_errors == 0 ? 0 : 1);
            }
            finally
            {
                _debugGuiKeyboardTarget = null;
                Time.timeScale = 1f;
            }
        }
    }
}
#endif
