using System;
using System.IO;
using System.Linq;
using Crest;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Crest.URP.Editor
{
    public static class PortAutomation
    {
        public static readonly string[] SceneNames = {
            "Ocean_Calm", "Ocean_Waves", "Ocean_Storm", "Ocean_Buoyancy",
            "Ocean_Interactions", "Ocean_Underwater", "Ocean_Reflections"
        };
        const string Root = "Assets/CrestPortDemos";

        [MenuItem("Crest/URP/Generate validation scenes")]
        public static void GenerateScenes()
        {
            Directory.CreateDirectory(Root);
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            renderer.name = "Crest Renderer";
            var feature = ScriptableObject.CreateInstance<CrestBackgroundFeature>();
            feature.name = "Crest Background";
            renderer.rendererFeatures.Add(feature);
            AssetDatabase.CreateAsset(renderer, Root + "/Renderer.asset");
            AssetDatabase.AddObjectToAsset(feature, renderer);
            var pipeline = UniversalRenderPipelineAsset.Create(renderer);
            pipeline.name = "Crest URP";
            pipeline.supportsCameraDepthTexture = true;
            pipeline.supportsCameraOpaqueTexture = true;
            pipeline.msaaSampleCount = 1;
            AssetDatabase.CreateAsset(pipeline, Root + "/Pipeline.asset");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            var previousQuality = QualitySettings.GetQualityLevel();
            try
            {
                for (var quality = 0; quality < QualitySettings.names.Length; quality++)
                {
                    QualitySettings.SetQualityLevel(quality, false);
                    QualitySettings.renderPipeline = pipeline;
                }
            }
            finally { QualitySettings.SetQualityLevel(previousQuality, false); }
            var referenceMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(referenceMaterial, Root + "/Reference.mat");

            for (var index = 0; index < SceneNames.Length; index++)
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                camera.tag = "MainCamera";
                camera.transform.SetPositionAndRotation(new Vector3(0, 7, -25), Quaternion.Euler(12, 0, 0));
                camera.farClipPlane = 10000;
                camera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
                var sun = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.3f;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(45, -30, 0);
                RenderSettings.sun = sun;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.3f, .4f, .5f);
                var oceanObject = new GameObject("Ocean");
                oceanObject.SetActive(false);
                var ocean = oceanObject.AddComponent<OceanRenderer>();
                var original = AssetDatabase.LoadAssetAtPath<Material>("Assets/Crest/Crest/Materials/Ocean.mat");
                if (original == null) throw new InvalidOperationException("Original ocean material missing.");
                var material = new Material(original) { name = SceneNames[index] };
                AssetDatabase.CreateAsset(material, Root + "/" + SceneNames[index] + ".mat");
                ocean.OceanMaterial = material;
                ocean.ViewCamera = camera;
                ocean._primaryLight = sun;
                ocean._globalWindSpeed = index == 0 ? 12 : index == 2 ? 100 : 40;
                var settings = new SerializedObject(ocean);
                settings.FindProperty("_createFoamSim").boolValue = true;
                settings.FindProperty("_createDynamicWaveSim").boolValue = index == 4;
                settings.ApplyModifiedPropertiesWithoutUndo();
                material.EnableKeyword("_FOAM_ON");
                material.EnableKeyword("_TRANSPARENCY_ON");
                var waves = new GameObject("FFT Waves");
                waves.transform.SetParent(oceanObject.transform);
                waves.AddComponent<ShapeFFT>();
                oceanObject.SetActive(true);

                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Visible refraction reference floor";
                floor.transform.position = new Vector3(0, -8, 20);
                floor.transform.localScale = new Vector3(100, 1, 100);
                floor.GetComponent<Renderer>().sharedMaterial = referenceMaterial;

                if (index == 3 || index == 4)
                {
                    for (var i = 0; i < 3; i++)
                    {
                        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        body.name = "Buoyancy " + i;
                        body.transform.position = new Vector3((i - 1) * 7, 2, 0);
                        body.transform.localScale = Vector3.one * (i + 1);
                        body.GetComponent<Renderer>().sharedMaterial = referenceMaterial;
                        body.AddComponent<Rigidbody>().mass = 10 * Mathf.Pow(10, i);
                        body.AddComponent<SimpleFloatingObject>()._objectWidth = i + 1;
                        if (index == 4) body.AddComponent<SphereWaterInteraction>();
                    }
                }
                if (index == 5)
                {
                    camera.transform.position = new Vector3(0, -2, -25);
                    camera.gameObject.AddComponent<UnderwaterRenderer>();
                    material.EnableKeyword("_UNDERWATER_ON");
                    material.SetInt("_CullMode", (int)CullMode.Off);
                }
                if (index == 6)
                {
                    camera.gameObject.AddComponent<OceanPlanarReflection>();
                    material.EnableKeyword("_PLANARREFLECTIONS_ON");
                    var reference = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    reference.name = "Reflection reference";
                    reference.transform.position = new Vector3(0, 4, 20);
                    reference.transform.localScale = new Vector3(5, 8, 5);
                    reference.GetComponent<Renderer>().sharedMaterial = referenceMaterial;
                }
                EditorSceneManager.SaveScene(scene, Root + "/" + SceneNames[index] + ".unity");
            }
            EditorBuildSettings.scenes = SceneNames.Select(name => new EditorBuildSettingsScene(Root + "/" + name + ".unity", true)).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("CREST_SCENES_GENERATED: fixtures created; rendering is NOT TESTED.");
        }

        public static void ValidateAssets()
        {
            int failures = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Crest" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                { Debug.LogError("Missing material or shader: " + path); failures++; }
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Shader", new[] { "Assets/Crest" }))
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var message in ShaderUtil.GetShaderMessages(shader))
                {
                    if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                    { Debug.LogError(shader.name + ": " + message.message); failures++; }
                }
            }
            if (failures != 0) throw new InvalidOperationException($"Crest asset validation: {failures} errors.");
            Debug.Log("CREST_ASSET_VALIDATION: no reported asset errors. GPU execution/variants NOT TESTED.");
        }

        public static void BuildDevelopment() => Build(BuildOptions.Development, "Builds/Development/CrestPort.exe");
        public static void BuildRelease() => Build(BuildOptions.None, "Builds/Release/CrestPort.exe");
        static void Build(BuildOptions options, string path)
        {
            if (Application.unityVersion != "6000.6.5f1")
                throw new InvalidOperationException("Acceptance builds require Unity 6000.6.5f1.");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                target = BuildTarget.StandaloneWindows64, locationPathName = path, options = options
            });
            if (result.summary.result != BuildResult.Succeeded) throw new Exception("Windows build failed.");
        }
    }
}

