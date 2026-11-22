// Builds an isolated diagnostic Player without replacing deliverable builds. MIT.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Crest.URP.Editor
{
    public static partial class ResourceShutdownProbeBuild
    {
        public static void Build() => BuildImpl(0);
        public static void BuildOriginalExamples() => BuildImpl(1);
        public static void BuildOriginalScenes() => BuildImpl(2);
        public static void BuildRenderModes() => BuildImpl(3);
        public static void BuildCameraStack() => BuildImpl(4);
        public static void BuildReflectionModes() => BuildImpl(5);
        public static void BuildNavigationInput() => BuildImpl(6);
        public static void BuildDebugGui() => BuildImpl(7);
        public static void BuildDepthCacheLifecycle() => BuildImpl(8);
        public static void BuildLocalProbes() => BuildImpl(9);
        static void BuildImpl(int mode)
        {
            if (Application.unityVersion != "6000.6.5f1") throw new InvalidOperationException("Wrong Unity target.");
            var arguments = Environment.GetCommandLineArgs();
            bool composedRiver = mode == 2 && Array.IndexOf(arguments, "-crestBuildComposedRiver") >= 0;
            bool activateProbeBox = mode == 9 && Array.IndexOf(arguments, "-crestProbeBoxOnly") >= 0;
            bool activateProbeAtlas = mode == 9 && Array.IndexOf(arguments, "-crestProbeAtlasOnly") >= 0;
            bool restoreQualityPipeline = mode != 9 || activateProbeBox || activateProbeAtlas;
            var qualityPipeline=QualitySettings.renderPipeline;
            var preloadedAssets=PlayerSettings.GetPreloadedAssets();
            bool timing=PlayerSettings.enableFrameTimingStats;
            bool strict=PlayerSettings.strictShaderVariantMatching;
            const string parent="Assets";
            string folder=parent+"/CrestResourceProbe_"+Guid.NewGuid().ToString("N");
            string guid=AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            try
            {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camera=new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                camera.tag="MainCamera";
                camera.backgroundColor=Color.gray;
                camera.clearFlags=CameraClearFlags.SolidColor;
                camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;
                // Cost probes enable this exact Calm variant at runtime. Retain it by a
                // serialized dependency; strict shader matching must remain enabled.
                var underwater=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/CrestPortDemos/Ocean_Calm.mat"));
                underwater.EnableKeyword("_UNDERWATER_ON");underwater.SetFloat("_Underwater",1);
                AssetDatabase.CreateAsset(underwater,folder+"/DiagnosticUnderwater.mat");
                var retention=new GameObject("Diagnostic shader variant dependency",typeof(MeshRenderer)).GetComponent<MeshRenderer>();
                retention.enabled=false;retention.sharedMaterial=underwater;
                var planar=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/CrestPortDemos/Ocean_Calm.mat"));
                planar.EnableKeyword("_PLANARREFLECTIONS_ON");
                AssetDatabase.CreateAsset(planar,folder+"/DiagnosticPlanar.mat");
                var counterControl=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(counterControl,folder+"/DiagnosticCounterControl.mat");
                retention.sharedMaterials=new[]{underwater,planar,counterControl};
                if(mode==4)
                {
                    var flat=new Material(underwater);flat.DisableKeyword("_TRANSPARENCY_ON");flat.DisableKeyword("_APPLYNORMALMAPPING_ON");flat.SetFloat("_Transparency",0);
                    AssetDatabase.CreateAsset(flat,folder+"/StackFlatUnderwater.mat");
                    var flatAbove=new Material(flat);flatAbove.DisableKeyword("_UNDERWATER_ON");flatAbove.SetFloat("_Underwater",0);
                    AssetDatabase.CreateAsset(flatAbove,folder+"/StackFlatAbove.mat");
                    var foam=new Material(Shader.Find("Crest/Inputs/Foam/Override Foam"));AssetDatabase.CreateAsset(foam,folder+"/StackFoamSeed.mat");
                    retention.sharedMaterials=new[]{underwater,planar,counterControl,flat,flatAbove,foam};
                }
                if(mode is 5 or 9)
                {
                    var original=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/CrestPortDemos/Renderer.asset");
                    var forward=UnityEngine.Object.Instantiate(original);forward.renderingMode=RenderingMode.Forward;AssetDatabase.CreateAsset(forward,folder+"/Forward.asset");
                    var clustered=UnityEngine.Object.Instantiate(original);clustered.renderingMode=RenderingMode.ForwardPlus;AssetDatabase.CreateAsset(clustered,folder+"/ForwardPlus.asset");
                    var deferred=UnityEngine.Object.Instantiate(original);deferred.renderingMode=RenderingMode.Deferred;AssetDatabase.CreateAsset(deferred,folder+"/Deferred.asset");
                    var pipeline=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/CrestPortDemos/Pipeline.asset"));
                    var serialized=new SerializedObject(pipeline);var renderers=serialized.FindProperty("m_RendererDataList");renderers.arraySize=3;
                    renderers.GetArrayElementAtIndex(0).objectReferenceValue=forward;renderers.GetArrayElementAtIndex(1).objectReferenceValue=clustered;renderers.GetArrayElementAtIndex(2).objectReferenceValue=deferred;
                    if (mode == 9)
                    {
                        serialized.FindProperty("m_ReflectionProbeBlending").boolValue = true;
                        serialized.FindProperty("m_ReflectionProbeBoxProjection").boolValue = false;
                        serialized.FindProperty("m_ReflectionProbeAtlas").boolValue = true;
                        AssetDatabase.CreateFolder(folder, "Resources");
                        var surface = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/CrestPortDemos/Ocean_Calm.mat"));
                        ConfigureLocalProbeMaterial(surface);
                        AssetDatabase.CreateAsset(surface, folder + "/Resources/CrestLocalProbeSurface.mat");
                        var lit = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        lit.SetFloat("_Metallic", 1);
                        lit.SetFloat("_Smoothness", 0.95f);
                        AssetDatabase.CreateAsset(lit, folder + "/Resources/CrestLocalProbeLit.mat");
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.CreateAsset(pipeline, folder + (mode == 9 ? "/Resources/CrestLocalProbeAtlasPipeline.asset" : "/ReflectionModesPipeline.asset"));
                    if (mode == 9)
                    {
                        var boxPipeline = UnityEngine.Object.Instantiate(pipeline);
                        var boxSettings = new SerializedObject(boxPipeline);
                        boxSettings.FindProperty("m_ReflectionProbeBlending").boolValue = false;
                        boxSettings.FindProperty("m_ReflectionProbeBoxProjection").boolValue = true;
                        boxSettings.FindProperty("m_ReflectionProbeAtlas").boolValue = false;
                        var boxRenderers = boxSettings.FindProperty("m_RendererDataList");
                        boxRenderers.arraySize = 1;
                        boxRenderers.GetArrayElementAtIndex(0).objectReferenceValue = forward;
                        boxSettings.ApplyModifiedPropertiesWithoutUndo();
                        AssetDatabase.CreateAsset(boxPipeline, folder + "/Resources/CrestLocalProbeBoxPipeline.asset");
                        var includer = GraphicsSettings.GetRenderPipelineSettings<IncludeAdditionalRPAssets>();
                        if (includer == null || !includer.includeAssetsByLabel || string.IsNullOrEmpty(includer.labelToInclude))
                            throw new InvalidOperationException("The diagnostic requires URP's additional pipeline asset label.");
                        // Windows has an explicit PC pipeline override, so URP skips the default fallback when collecting variants.
                        AssetDatabase.SetLabels(pipeline, new[] { includer.labelToInclude });
                        AssetDatabase.SetLabels(boxPipeline, new[] { includer.labelToInclude });
                        var included = new List<UniversalRenderPipelineAsset>();
                        if (!BuildTarget.StandaloneWindows64.TryGetRenderPipelineAssets(included) ||
                            !included.Contains(pipeline) || !included.Contains(boxPipeline))
                            throw new InvalidOperationException("URP's Windows build inventory is missing a diagnostic pipeline.");
                        if (activateProbeBox) QualitySettings.renderPipeline = boxPipeline;
                        if (activateProbeAtlas) QualitySettings.renderPipeline = pipeline;
                    }
                    else
                    {
                        QualitySettings.renderPipeline = pipeline;
                    }
                    AssetDatabase.SaveAssets();
                }
                string path=folder+"/Resource_Control.unity";
                EditorSceneManager.SaveScene(scene,path);
                string localProbeScene = mode == 9 ? SaveLocalProbeScene(folder) : null;
                string projectedProbeScene = null;
                if (activateProbeBox)
                {
                    GameObject.Find("Crest Local Probe Primary").GetComponent<ReflectionProbe>().boxProjection = true;
                    projectedProbeScene = folder + "/LocalProbeProjected.unity";
                    if (!EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), projectedProbeScene, saveAsCopy: true))
                        throw new InvalidOperationException("Could not save the projected probe scene.");
                }
                string[] localProbeScenes = activateProbeAtlas ? SaveLocalProbeAtlasScenes(folder, path, localProbeScene) :
                    activateProbeBox ? new[] { path, localProbeScene, projectedProbeScene } : new[] { path, localProbeScene };
                PlayerSettings.enableFrameTimingStats=true;
                if(mode != 0)PlayerSettings.strictShaderVariantMatching=true;
                var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes=mode is 3 or 8?new[]{path,"Assets/CrestPortDemos/Ocean_Underwater.unity"}:mode==9?localProbeScenes:mode==7?new[]{path,"Assets/CrestPortDemos/Ocean_Calm.unity"}:mode==6?new[]{path,"Assets/Crest/Crest-Examples/Examples/Scenes/Examples.unity"}:mode==5?new[]{path,"Assets/CrestPortDemos/Ocean_Reflections.unity"}:mode==4?new[]{path,"Assets/CrestPortDemos/Ocean_Calm.unity"}:mode==2?new[]{path,
                        "Assets/Crest/Crest-Examples/Main/Scenes/main.unity",
                        "Assets/Crest/Crest-Examples/BoatDev/Scenes/boat.unity",
                        "Assets/Crest/Crest-Examples/BoatDev/Scenes/BoatWakes.unity",
                        "Assets/Crest/Crest-Examples/BoatDev/Scenes/threeboats.unity",
                        "Assets/Crest/Crest-Examples/LakesAndRivers/Scenes/LakesAndRivers.unity",
                        "Assets/Crest/Crest-Examples/Whirlpool/Scenes/whirlpool.unity"}:
                        mode==1?new[]{path,"Assets/Crest/Crest-Examples/Examples/Scenes/Examples.unity"}:new[] {path,"Assets/CrestPortDemos/Ocean_Calm.unity","Assets/CrestPortDemos/Ocean_Interactions.unity"},
                    target=BuildTarget.StandaloneWindows64,
                    locationPathName=composedRiver?"Builds/P13-RiverComposition/OriginalScenes.exe":mode==9?"Builds/P12-LocalProbes/LocalProbes.exe":mode==8?"Builds/P11-DepthCacheLifecycle/DepthCacheLifecycle.exe":mode==7?"Builds/P11-DebugGui/DebugGui.exe":mode==6?"Builds/P11-NavigationInput/NavigationInput.exe":mode==5?"Builds/P11-ReflectionModes/ReflectionModes.exe":mode==4?"Builds/P11-CameraStack/CameraStack.exe":mode==3?"Builds/P11-RenderModes/RenderModes.exe":mode==2?"Builds/P11-OriginalScenes/OriginalScenes.exe":mode==1?"Builds/P11-OriginalExamples/OriginalExamples.exe":"Builds/P10-ResourceProbe/ResourceProbe.exe",
                    options=BuildOptions.Development,
                    extraScriptingDefines=new[] {"CREST_RESOURCE_PROBE"}
                });
                if(build.summary.result!=BuildResult.Succeeded) throw new InvalidOperationException("Diagnostic build failed.");
                Debug.Log("RESOURCE_PROBE_BUILD_SUCCEEDED bytes="+build.summary.totalSize);
            }
            finally
            {
                if (restoreQualityPipeline) QualitySettings.renderPipeline=qualityPipeline;
                PlayerSettings.SetPreloadedAssets(preloadedAssets);
                PlayerSettings.enableFrameTimingStats=timing;
                PlayerSettings.strictShaderVariantMatching=strict;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if(Path.GetFullPath(folder).StartsWith(Path.GetFullPath(parent)+Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    && AssetDatabase.AssetPathToGUID(folder)==guid)
                {
                    if (activateProbeBox || activateProbeAtlas) AssetDatabase.SaveAssets();
                    AssetDatabase.DeleteAsset(folder);
                }
            }
        }

        static string SaveLocalProbeScene(string folder)
        {
            var scene = EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
            var primary = new GameObject("Crest Local Probe Primary").AddComponent<ReflectionProbe>();
            var secondary = new GameObject("Crest Local Probe Secondary").AddComponent<ReflectionProbe>();
            primary.gameObject.layer = secondary.gameObject.layer = 4;
            primary.mode = secondary.mode = ReflectionProbeMode.Custom;
            primary.transform.position = new Vector3(12, 0, 0);
            primary.size = new Vector3(80, 50, 160);
            secondary.size = new Vector3(160, 64, 160);
            primary.customBakedTexture = SaveLocalProbeCubemap(folder, "Directional red-green", Color.red, true);
            secondary.customBakedTexture = SaveLocalProbeCubemap(folder, "Solid green", Color.green);
            secondary.enabled = false;
            SaveLocalProbeCubemap(folder, "Solid red", Color.red);
            string path = folder + "/LocalProbeOcean.unity";
            if (!EditorSceneManager.SaveScene(scene, path, saveAsCopy: true))
                throw new InvalidOperationException("Could not save the owned local-probe scene.");
            EditorSceneManager.OpenScene(path);
            return path;
        }

        static Cubemap SaveLocalProbeCubemap(string folder, string name, Color color, bool directional = false)
        {
            var cube = new Cubemap(16, TextureFormat.RGBA32, true) { name = name };
            var face = new Color[16 * 16];
            for (int i = 0; i < 6; i++)
            {
                Array.Fill(face, directional && i == (int)CubemapFace.PositiveX ? Color.green : color);
                cube.SetPixels(face, (CubemapFace)i);
            }
            cube.Apply();
            AssetDatabase.CreateAsset(cube, folder + "/Resources/" + name + ".asset");
            return cube;
        }

        static string[] SaveLocalProbeAtlasScenes(string folder, string control, string liveScene)
        {
            var scene = EditorSceneManager.GetActiveScene();
            var primary = GameObject.Find("Crest Local Probe Primary").GetComponent<ReflectionProbe>();
            var secondary = GameObject.Find("Crest Local Probe Secondary").GetComponent<ReflectionProbe>();
            primary.transform.position = Vector3.zero;
            primary.customBakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(folder + "/Resources/Solid red.asset");
            primary.size = secondary.size = new Vector3(160, 64, 160);
            primary.blendDistance = secondary.blendDistance = 64;
            string red = folder + "/LocalProbeRed.unity";
            string overlap = folder + "/LocalProbeOverlap.unity";
            string green = folder + "/LocalProbeGreen.unity";
            bool redSaved = EditorSceneManager.SaveScene(scene, red, saveAsCopy: true);
            secondary.enabled = true;
            bool overlapSaved = EditorSceneManager.SaveScene(scene, overlap, saveAsCopy: true);
            primary.enabled = false;
            if (!EditorSceneManager.SaveScene(scene, green, saveAsCopy: true) || !overlapSaved || !redSaved)
                throw new InvalidOperationException("Could not save all atlas probe scenes.");
            return new[] { control, liveScene, red, overlap, green };
        }

        static void ConfigureLocalProbeMaterial(Material surface)
        {
            foreach (string keyword in new[] { "_PLANARREFLECTIONS_ON", "_OVERRIDEREFLECTIONCUBEMAP_ON",
                         "_PROCEDURALSKY_ON", "_COMPUTEDIRECTIONALLIGHT_ON", "_TRANSPARENCY_ON",
                         "_APPLYNORMALMAPPING_ON", "_FOAM_ON" })
                surface.DisableKeyword(keyword);
            surface.SetFloat("_Specular", 1);
        }
    }
}
