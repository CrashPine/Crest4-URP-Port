// Crest 4 URP port. MIT; see LICENSE.Crest.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal;
using UnityEngine;

namespace Crest.URP.Editor
{
    public static class ExampleMaterialAudit
    {
        [Serializable] class MaterialInfo
        {
            public string path, shader, shaderAsset, pipelineTag;
            public bool supported;
            public string[] passes, keywords, compilerErrors;
        }
        [Serializable] class SceneInfo { public string path; public string[] materials; }
        [Serializable] class Report
        {
            public string recordedUtc, unity, scope;
            public MaterialInfo[] materials;
            public SceneInfo[] scenes;
        }
        [Serializable] class Migration
        {
            public string path,guid,beforeShader,afterShader,beforeSha256,afterSha256;
            public Color beforeColor,afterColor;
            public float beforeMode,afterSurface,afterBlend,beforeSmoothness,afterSmoothness;
            public int textureBindingsChecked;
        }
        [Serializable] class MigrationReport {public string recordedUtc,scope;public Migration[] materials;}
        static string Hash(string path)
        {
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","");
        }

        [MenuItem("Crest/URP/Migrate original Standard and terrain materials")]
        public static void MigrateBuiltInMaterials()
        {
            var migrated=new List<Migration>();
            foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/Crest/Crest-Examples"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                string shader=material.shader.name;
                if(shader!="Standard"&&shader!="Nature/Terrain/Diffuse")continue;
                var source=UnityEngine.Object.Instantiate(material);
                var entry=new Migration{path=path,guid=guid,beforeShader=shader,beforeSha256=Hash(path)};
                try
                {
                    if(shader=="Standard")
                    {
                        entry.beforeColor=source.GetColor("_Color");entry.beforeMode=source.GetFloat("_Mode");
                        entry.beforeSmoothness=source.GetTexture("_MetallicGlossMap")!=null?source.GetFloat("_GlossMapScale"):source.GetFloat("_Glossiness");
                        new StandardUpgrader(shader).Upgrade(material,MaterialUpgrader.UpgradeFlags.None);
                        entry.afterColor=material.GetColor("_BaseColor");entry.afterSurface=material.GetFloat("_Surface");entry.afterBlend=material.GetFloat("_Blend");entry.afterSmoothness=material.GetFloat("_Smoothness");
                        if(entry.beforeColor!=entry.afterColor||entry.beforeSmoothness!=entry.afterSmoothness)
                            throw new InvalidOperationException("Colour/smoothness changed during migration: "+path);
                        float expectedSurface=entry.beforeMode>=2?1:0;
                        if(entry.afterSurface!=expectedSurface||(entry.beforeMode==3&&entry.afterBlend!=1))
                            throw new InvalidOperationException("Transparency mode was not preserved: "+path);
                    }
                    else new TerrainUpgrader(shader).Upgrade(material,MaterialUpgrader.UpgradeFlags.None);
                    foreach(string property in source.GetTexturePropertyNames())
                    {
                        string target=property=="_MainTex"&&shader=="Standard"?"_BaseMap":property;
                        if(!material.HasProperty(target))continue;
                        if(source.GetTexture(property)!=material.GetTexture(target))throw new InvalidOperationException("Texture reference changed: "+path+" "+property);
                        // The official converter intentionally transforms detail UVs relative to the base map.
                        if(property!="_DetailAlbedoMap"&&(source.GetTextureScale(property)!=material.GetTextureScale(target)||source.GetTextureOffset(property)!=material.GetTextureOffset(target)))
                            throw new InvalidOperationException("Texture UV transform changed: "+path+" "+property);
                        entry.textureBindingsChecked++;
                    }
                    entry.afterShader=material.shader.name;
                    EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
                    if(AssetDatabase.AssetPathToGUID(path)!=guid)throw new InvalidOperationException("Material GUID changed: "+path);
                    entry.afterSha256=Hash(path);migrated.Add(entry);
                }
                catch {material.shader=source.shader;material.CopyPropertiesFromMaterial(source);throw;}
                finally {UnityEngine.Object.DestroyImmediate(source);}
            }
            Directory.CreateDirectory("Evidence");
            if(migrated.Count==0&&File.Exists("Evidence/P09-ExampleMaterial-migration.json"))
            {
                Debug.Log("CREST_EXAMPLE_MATERIAL_MIGRATION: no remaining built-in materials; retained previous migration evidence.");
                Run();return;
            }
            File.WriteAllText("Evidence/P09-ExampleMaterial-migration.json",JsonUtility.ToJson(new MigrationReport
            {
                recordedUtc=DateTime.UtcNow.ToString("O"),scope="Only original example Standard and Nature/Terrain/Diffuse materials; Unity URP17.6 official upgraders. Per-material SaveAssetIfDirty; no scene/Ocean.mat/lighting save. Texture bindings, UVs, colours, smoothness, transparent surface/premultiply mode and GUIDs checked. Other original Surface Shaders remain pending.",materials=migrated.ToArray()
            },true));
            Debug.Log($"CREST_EXAMPLE_MATERIAL_MIGRATION: {migrated.Count} materials migrated; render acceptance separate.");
            Run();
        }

        [MenuItem("Crest/URP/Audit original example materials")]
        public static void Run()
        {
            var materials=AssetDatabase.FindAssets("t:Material",new[]{"Assets/Crest"})
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(x=>x).Select(path=>
                {
                    var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                    var shader=material!=null?material.shader:null;
                    return new MaterialInfo
                    {
                        path=path,shader=shader!=null?shader.name:"<missing>",
                        shaderAsset=shader!=null?AssetDatabase.GetAssetPath(shader):"",
                        pipelineTag=material!=null?material.GetTag("RenderPipeline",false,""):"",
                        supported=shader!=null&&shader.isSupported,
                        passes=material!=null?Enumerable.Range(0,material.passCount).Select(material.GetPassName).ToArray():Array.Empty<string>(),
                        keywords=material!=null?material.shaderKeywords:Array.Empty<string>(),
                        compilerErrors=shader!=null?ShaderUtil.GetShaderMessages(shader)
                            .Where(x=>x.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                            .Select(x=>x.message).ToArray():new[]{"Missing shader"}
                    };
                }).ToArray();
            var paths=materials.Select(x=>x.path).ToHashSet();
            var scenes=AssetDatabase.FindAssets("t:Scene",new[]{"Assets/Crest/Crest-Examples"})
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(x=>x).Select(path=>new SceneInfo
                {
                    path=path,materials=AssetDatabase.GetDependencies(path,true).Where(paths.Contains).OrderBy(x=>x).ToArray()
                }).ToArray();
            var report=new Report
            {
                recordedUtc=DateTime.UtcNow.ToString("O"),unity=Application.unityVersion,
                scope="Read-only Unity AssetDatabase shader resolution/dependencies. supported=true and empty compilerErrors do not prove URP draw compatibility. No assets or scenes saved.",
                materials=materials,scenes=scenes
            };
            Directory.CreateDirectory("Evidence");
            File.WriteAllText("Evidence/P09-ExampleMaterial-unity-audit.json",JsonUtility.ToJson(report,true));
            Debug.Log($"CREST_EXAMPLE_MATERIAL_AUDIT: {materials.Length} materials, {scenes.Length} scenes; inventory only.");
        }
    }
}
