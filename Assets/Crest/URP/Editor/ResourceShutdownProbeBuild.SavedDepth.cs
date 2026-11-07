// Compiled transfer of an already accepted original depth export; no rebake. MIT.
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crest.URP.Editor
{
    public static partial class ResourceShutdownProbeBuild
    {
        [Serializable] class SavedDepthManifest
        {
            public string scene,sceneGuid,textureGuid,prefabGuid,textureName,exportSHA256,importedFloatSHA256;
            public string source="Evidence/DepthBaked-Direct3D11.exr: accepted original Inspector export, relative-6.5m; no new Editor bake.";
        }
        static string DepthSHA(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        public static void BuildSavedDepth()
        {
            if(Application.unityVersion!="6000.6.5f1")throw new InvalidOperationException("Wrong Unity target");
            bool strict=PlayerSettings.strictShaderVariantMatching;const string parent="Assets";
            string folder=parent+"/CrestSavedDepth_"+Guid.NewGuid().ToString("N"),guid=AssetDatabase.CreateFolder(parent,Path.GetFileName(folder));
            try
            {
                var control=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);string controlPath=folder+"/Control.unity";EditorSceneManager.SaveScene(control,controlPath);
                string exr=folder+"/Depth.exr";var bytes=File.ReadAllBytes("Evidence/DepthBaked-Direct3D11.exr");File.WriteAllBytes(exr,bytes);AssetDatabase.ImportAsset(exr);
                var importer=(TextureImporter)AssetImporter.GetAtPath(exr);importer.textureType=TextureImporterType.SingleChannel;importer.sRGBTexture=false;importer.alphaSource=TextureImporterAlphaSource.None;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;importer.npotScale=TextureImporterNPOTScale.None;
                var textureSettings=new TextureImporterSettings();importer.ReadTextureSettings(textureSettings);textureSettings.singleChannelComponent=TextureImporterSingleChannelComponent.Red;importer.SetTextureSettings(textureSettings);
                var platform=importer.GetDefaultPlatformTextureSettings();platform.format=TextureImporterFormat.RFloat;importer.SetPlatformTextureSettings(platform);importer.SaveAndReimport();
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(exr);
                var readback=AsyncGPUReadback.Request(texture,0,TextureFormat.RFloat);readback.WaitForCompletion();if(readback.hasError)throw new InvalidOperationException("Imported depth readback failed");
                var values=readback.GetData<float>().ToArray();if(texture.width!=64||texture.height!=64||values.Any(v=>!float.IsFinite(v))||Mathf.Abs(values[32*64+32]+6.5f)>.02f)throw new InvalidOperationException("Accepted EXR fixture changed");
                var floatBytes=new byte[values.Length*4];Buffer.BlockCopy(values,0,floatBytes,0,floatBytes.Length);
                var scene=EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");Camera.main.transform.position=new Vector3(0,7,0);
                foreach(var shape in UnityEngine.Object.FindObjectsByType<ShapeWaves>())shape.enabled=false;
                var owner=new GameObject("Saved baked depth cache");owner.SetActive(false);owner.transform.position=Vector3.up*2;owner.transform.localScale=new Vector3(32,1,32);
                var cache=owner.AddComponent<OceanDepthCache>();cache._resolution=64;cache._layers=1<<30;cache._cameraMaxTerrainHeight=20;cache._cameraFarClipPlane=100;
                var fields=new SerializedObject(cache);fields.FindProperty("_type").intValue=(int)OceanDepthCache.OceanDepthCacheType.Baked;fields.FindProperty("_refreshMode").intValue=(int)OceanDepthCache.OceanDepthCacheRefreshMode.OnDemand;fields.FindProperty("_savedCache").objectReferenceValue=texture;fields.FindProperty("_relative").boolValue=true;fields.ApplyModifiedPropertiesWithoutUndo();
                string prefabPath=folder+"/Cache.prefab";var prefab=PrefabUtility.SaveAsPrefabAsset(owner,prefabPath);UnityEngine.Object.DestroyImmediate(owner);var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);instance.SetActive(true);
                var dependency=new GameObject("Cached depth shader dependency",typeof(MeshRenderer)).GetComponent<MeshRenderer>();dependency.enabled=false;var material=new Material(Shader.Find(LodDataMgrSeaFloorDepth.ShaderName));AssetDatabase.CreateAsset(material,folder+"/CacheShader.mat");dependency.sharedMaterial=material;
                string path=folder+"/SavedDepth.unity";EditorSceneManager.SaveScene(scene,path);AssetDatabase.CreateFolder(folder,"Resources");
                var manifest=new SavedDepthManifest{scene=path,sceneGuid=AssetDatabase.AssetPathToGUID(path),textureGuid=AssetDatabase.AssetPathToGUID(exr),prefabGuid=AssetDatabase.AssetPathToGUID(prefabPath),textureName=texture.name,exportSHA256=DepthSHA(bytes),importedFloatSHA256=DepthSHA(floatBytes)};
                File.WriteAllText(folder+"/Resources/CrestSavedDepthManifest.json",JsonUtility.ToJson(manifest,true));AssetDatabase.Refresh();AssetDatabase.SaveAssets();
                string snapshot="Evidence/P11-SavedDepth-fixture-"+Path.GetFileName(folder);FileUtil.CopyFileOrDirectory(folder,snapshot);File.Copy(folder+".meta",snapshot+".meta");
                PlayerSettings.strictShaderVariantMatching=true;
                var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{controlPath,path},target=BuildTarget.StandaloneWindows64,locationPathName="Builds/P11-SavedDepth/SavedDepth.exe",options=BuildOptions.Development,extraScriptingDefines=new[]{"CREST_RESOURCE_PROBE"}});
                if(build.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Saved depth build failed");Debug.Log("SAVED_DEPTH_BUILD_SUCCEEDED bytes="+build.summary.totalSize);
            }
            finally
            {
                PlayerSettings.strictShaderVariantMatching=strict;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                if(Path.GetFullPath(folder).StartsWith(Path.GetFullPath(parent)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)&&AssetDatabase.AssetPathToGUID(folder)==guid)AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
