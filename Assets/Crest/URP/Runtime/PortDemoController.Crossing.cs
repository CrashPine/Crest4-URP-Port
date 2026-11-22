// MIT. Fixed-frame controls for the large-waves Player route.
#if DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Crest.URP
{
    public sealed partial class PortDemoController
    {
        const int CrossingWidth = 640;
        const int CrossingHeight = 360;
        int _crossingStart = -1;
        bool _entryPassed, _exitPassed, _submerged, _abovePassed;
        float _previousCameraY;
        RenderTexture _crossingTarget;
        Texture2D _crossingPixels;
        FieldInfo _meniscusField;

        void CheckWaveCrossing()
        {
            try
            {
                var camera = Camera.main;
                var ocean = OceanRenderer.Instance;
                if (camera == null || ocean == null)
                    throw new InvalidOperationException("Missing large-waves camera or ocean.");

                var fog = camera.GetComponent<UnderwaterRenderer>();
                if (_crossingStart < 0)
                {
                    Directory.CreateDirectory(_evidence);
                    var route = camera.GetComponent<LargeWaveCameraRoute>();
                    if (route == null || fog == null || !fog.IsMeniscusEnabled)
                        throw new InvalidOperationException("The scene needs its enabled camera route and meniscus.");
                    _meniscusField = typeof(UnderwaterRenderer).GetField("_meniscus", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (_meniscusField == null) throw new MissingFieldException("_meniscus");
                    _crossingStart = Time.frameCount;
                    Time.captureFramerate = 60;
                    camera.aspect = (float)CrossingWidth / CrossingHeight;
                    route.enabled = false;
                    route.enabled = true;
                    _previousCameraY = camera.transform.position.y;
                    _crossingTarget = new RenderTexture(CrossingWidth, CrossingHeight, 24, RenderTextureFormat.ARGB32);
                    if (!_crossingTarget.Create()) throw new InvalidOperationException("Crossing render target creation failed.");
                    _crossingPixels = new Texture2D(CrossingWidth, CrossingHeight, TextureFormat.RGB24, false);
                    _results.Add($"Unity={Application.unityVersion}; API={SystemInfo.graphicsDeviceType}; GPU={SystemInfo.graphicsDeviceName}");
                    return;
                }

                float height = ocean.ViewerHeightAboveWater;
                float cameraY = camera.transform.position.y;
                bool rising = cameraY > _previousCameraY;
                _previousCameraY = cameraY;
                if (BuildCommandBufferBase._lastUpdateFrame != OceanRenderer.FrameCount)
                    throw new InvalidOperationException("Crossing capture preceded the ocean update.");

                if (!_submerged && height < -2)
                    _submerged = CompareCrossing(camera, fog, height, "Submerged", meniscusOnly: false);
                if (_submerged && !_abovePassed && rising && height > 3)
                    _abovePassed = CompareCrossing(camera, fog, height, "Above", meniscusOnly: false);

                if (Mathf.Abs(height) < .8f)
                {
                    if (!_entryPassed && !_submerged && !rising)
                        _entryPassed = CompareCrossing(camera, fog, height, "Entry", meniscusOnly: true);
                    if (!_exitPassed && _submerged && rising)
                        _exitPassed = CompareCrossing(camera, fog, height, "Exit", meniscusOnly: true);
                }

                if (_entryPassed && _exitPassed && _submerged && _abovePassed)
                    EndWaveCrossing();
                else if (Time.frameCount - _crossingStart >= 1080)
                    throw new InvalidOperationException($"Incomplete route: entry={_entryPassed}; exit={_exitPassed}; submerged={_submerged}; above={_abovePassed}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EndWaveCrossing();
            }
        }

        bool CompareCrossing(Camera camera, UnderwaterRenderer fog, float height, string label, bool meniscusOnly)
        {
            var before = ReadCrossing(camera);
            Color32[] control;
            try
            {
                if (meniscusOnly) _meniscusField.SetValue(fog, false);
                else fog.enabled = false;
                control = ReadCrossing(camera);
            }
            finally
            {
                if (meniscusOnly) _meniscusField.SetValue(fog, true);
                else fog.enabled = true;
            }
            var restored = ReadCrossing(camera);
            int changed = 0, brighter = 0, restoration = 0, pink = 0;
            for (int i = 0; i < before.Length; i++)
            {
                var a = before[i];
                var b = control[i];
                if (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) > 6) changed++;
                if (a.r + a.g + a.b > b.r + b.g + b.b + 6) brighter++;
                if (!a.Equals(restored[i])) restoration++;
                if (a.r > 245 && a.g < 10 && a.b > 245) pink++;
            }
            if (restoration != 0 || pink != 0)
                throw new InvalidOperationException($"{label}: restoration={restoration}; error-pink={pink}");

            bool passed = meniscusOnly ? changed > 20 && changed < before.Length / 6 && brighter == 0
                : label == "Above" ? changed == 0 : changed > 1000;
            if (!passed) return false;

            _results.Add(FormattableString.Invariant(
                $"{label}: frame={Time.frameCount}; oceanTime={OceanRenderer.Instance.CurrentTime:F3}; relativeHeight={height:F4}; changed={changed}; brighter={brighter}; restoration={restoration}; errorPink={pink}"));
            SaveCrossing(label + "-on", before);
            if (label != "Above") SaveCrossing(label + "-control", control);
            return true;
        }

        Color32[] ReadCrossing(Camera camera)
        {
            var previous = RenderTexture.active;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = _crossingTarget });
                RenderTexture.active = _crossingTarget;
                _crossingPixels.ReadPixels(new Rect(0, 0, CrossingWidth, CrossingHeight), 0, 0);
                _crossingPixels.Apply();
                return _crossingPixels.GetPixels32();
            }
            finally { RenderTexture.active = previous; }
        }

        void SaveCrossing(string label, Color32[] pixels)
        {
            _crossingPixels.SetPixels32(pixels);
            _crossingPixels.Apply();
            File.WriteAllBytes(Path.Combine(_evidence, label + ".png"), _crossingPixels.EncodeToPNG());
        }

        void EndWaveCrossing()
        {
            _checkWaveCrossing = false;
            if (_crossingTarget != null)
            {
                _crossingTarget.Release();
                Destroy(_crossingTarget);
            }
            if (_crossingPixels != null) Destroy(_crossingPixels);
            _results.Add($"Errors={_errors}");
            File.WriteAllLines(Path.Combine(_evidence, "crossing.txt"), _results);
            Application.Quit(_errors == 0 ? 0 : 1);
        }
    }
}
#endif
