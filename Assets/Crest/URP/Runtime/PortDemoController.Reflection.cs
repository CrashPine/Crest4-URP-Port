// Standalone regression for reflection options and retained URP lighting variants. MIT.
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Crest.URP
{
    public sealed partial class PortDemoController
    {
        IEnumerator ReflectionOptions()
        {
            yield return SceneManager.LoadSceneAsync("Ocean_Reflections");
            var camera = Camera.main;
            camera.transform.position = new Vector3(0, 5, -15);
            camera.transform.LookAt(new Vector3(0, 2, 0));
            camera.fieldOfView = 60;
            camera.aspect = 16f / 9;
            var reflection = camera.GetComponent<OceanPlanarReflection>();
            var source = GameObject.Find("Reflection reference").GetComponent<Renderer>();
            source.enabled = false;
            // This scene material directly references URP/Lit, retaining the shader in Player.
            var material = new Material(source.sharedMaterial);
            material.SetFloat("_Smoothness", 0);
            material.SetColor("_BaseColor", Color.white);
            var near = GameObject.CreatePrimitive(PrimitiveType.Cube);
            near.transform.position = new Vector3(-2, 2, 0);
            near.transform.localScale = Vector3.one * 2;
            near.GetComponent<Renderer>().sharedMaterial = material;
            var far = GameObject.CreatePrimitive(PrimitiveType.Cube);
            far.transform.position = new Vector3(13, 2, 3);
            far.transform.localScale = Vector3.one * 2;
            far.GetComponent<Renderer>().sharedMaterial = material;
            reflection.ReflectionDistance = 100;
            for (int i = 0; i < 8; i++) yield return null;
            var distant = ReadReflection(reflection, "Distance-100");
            reflection.ReflectionDistance = 20;
            for (int i = 0; i < 8; i++) yield return null;
            var restricted = ReadReflection(reflection, "Distance-20");
            int farChanges = ReflectionRegionChanges(distant, restricted, reflection.ReflectionCamera, far.transform.position);
            int nearChanges = ReflectionRegionChanges(distant, restricted, reflection.ReflectionCamera, near.transform.position);
            if (farChanges < 64 || nearChanges != 0 || far.GetComponent<Renderer>().forceRenderingOff) _errors++;
            near.SetActive(false);
            far.SetActive(false);
            reflection.ReflectionDistance = 100;
            if (RenderSettings.sun != null) RenderSettings.sun.enabled = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.reflectionIntensity = 0;
            var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panel.transform.position = new Vector3(0, 2, 0);
            panel.transform.localScale = new Vector3(10, 8, 1);
            panel.GetComponent<Renderer>().sharedMaterial = material;
            material.SetColor("_BaseColor", Color.gray);
            var light = new GameObject("Reflection point light").AddComponent<Light>();
            light.type = LightType.Point;
            light.transform.position = new Vector3(0, 2, -2);
            light.range = 4;
            light.intensity = 15;
            var pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            var originalMode = pipeline.additionalLightsRenderingMode;
            reflection.DisablePixelLights = false;
            for (int i = 0; i < 8; i++) yield return null;
            float pixel = ReflectionRegionMean(ReadReflection(reflection, "Pixel-lights"), reflection.ReflectionCamera, panel.transform.position);
            reflection.DisablePixelLights = true;
            for (int i = 0; i < 8; i++) yield return null;
            float vertex = ReflectionRegionMean(ReadReflection(reflection, "Vertex-lights"), reflection.ReflectionCamera, panel.transform.position);
            if (pixel <= 5 || vertex >= pixel * .25f || pipeline.additionalLightsRenderingMode != originalMode) _errors++;
            string result = $"ReflectionOptions: FarChanged={farChanges}; NearChanged={nearChanges}; PixelMean={pixel}; VertexMean={vertex}; PipelineMode={originalMode}";
            _results.Add(result);
            File.WriteAllText(Path.Combine(_evidence, "reflection-options.txt"), result);
            Destroy(material);
        }

        Color32[] ReadReflection(OceanPlanarReflection owner, string label)
        {
            owner.RefreshReflection();
            var target = owner.ReflectionCamera.targetTexture;
            var previous = RenderTexture.active;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(_evidence, "ReflectionOptions-" + label + ".png"), pixels.EncodeToPNG());
                return pixels.GetPixels32();
            }
            finally { RenderTexture.active = previous; Destroy(pixels); }
        }

        static int ReflectionRegionStart(Camera camera, Vector3 position)
        {
            var projected = camera.WorldToViewportPoint(position);
            int width = camera.targetTexture.width, height = camera.targetTexture.height;
            int x = Mathf.RoundToInt(projected.x * width), y = Mathf.RoundToInt(projected.y * height);
            if (projected.z <= 0 || x < 8 || y < 8 || x + 8 > width || y + 8 > height)
                throw new System.InvalidOperationException("Reflection marker region must be fully visible.");
            return (y - 8) * width + x - 8;
        }

        static float ReflectionRegionMean(Color32[] colors, Camera camera, Vector3 position)
        {
            int start = ReflectionRegionStart(camera, position), width = camera.targetTexture.width;
            double sum = 0;
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                var color = colors[start + y * width + x];
                sum += color.r + color.g + color.b;
            }
            return (float)(sum / (256 * 3));
        }

        static int ReflectionRegionChanges(Color32[] first, Color32[] second, Camera camera, Vector3 position)
        {
            int start = ReflectionRegionStart(camera, position), width = camera.targetTexture.width, changed = 0;
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                var a = first[start + y * width + x];
                var b = second[start + y * width + x];
                if (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) > 30) changed++;
            }
            return changed;
        }
    }
}
