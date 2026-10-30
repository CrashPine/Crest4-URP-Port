// Builds an isolated diagnostic Player without replacing deliverable builds. MIT.
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Crest.URP.Editor
{
    public static class ResourceShutdownProbeBuild
    {
        public static void Build()
        {
            if (Application.unityVersion != "6000.6.5f1") throw new InvalidOperationException("Wrong Unity target.");
            var preloadedAssets=PlayerSettings.GetPreloadedAssets();
            bool timing=PlayerSettings.enableFrameTimingStats;
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
                retention.sharedMaterials=new[]{underwater,planar};
                string path=folder+"/Resource_Control.unity";
                EditorSceneManager.SaveScene(scene,path);
                PlayerSettings.enableFrameTimingStats=true;
                var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes=new[] {path,"Assets/CrestPortDemos/Ocean_Calm.unity"},
                    target=BuildTarget.StandaloneWindows64,
                    locationPathName="Builds/P10-ResourceProbe/ResourceProbe.exe",
                    options=BuildOptions.Development,
                    extraScriptingDefines=new[] {"CREST_RESOURCE_PROBE"}
                });
                if(build.summary.result!=BuildResult.Succeeded) throw new InvalidOperationException("Diagnostic build failed.");
                Debug.Log("RESOURCE_PROBE_BUILD_SUCCEEDED bytes="+build.summary.totalSize);
            }
            finally
            {
                PlayerSettings.SetPreloadedAssets(preloadedAssets);
                PlayerSettings.enableFrameTimingStats=timing;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if(Path.GetFullPath(folder).StartsWith(Path.GetFullPath(parent)+Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    && AssetDatabase.AssetPathToGUID(folder)==guid) AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
