// Crest 4 URP port. MIT; see LICENSE.Crest.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
public class OriginalRendererFlagsTests
{
    static string Node(string text,long id)
    {
        var match=Regex.Match(text,@"(?ms)^--- !u!\d+ &"+id+@"(?: stripped)?\r?\n.*?(?=^--- !u!|\z)");Assert.That(match.Success,Is.True,"Original serialized node "+id);return match.Value;
    }
    static int Number(string text,string name)
    {
        var match=Regex.Match(text,@"(?m)^  "+name+@": (\d+)");Assert.That(match.Success,Is.True,name);return unchecked((int)uint.Parse(match.Groups[1].Value));
    }
    [Test] public void ResolvedOriginalExampleRendererFlagsMatchUnchangedSourcePrefabFields()
    {
        string report="Evidence/OriginalRendererFlags.txt";File.WriteAllText(report,"Resolved original Examples scene inactive+active renderer bindings; non-overridden flags compared to readonly original prefab YAML. Overrides reported, not silently compared to base. No asset/scene save.\n");
        var names=new[]{"Examples_Material_MaskedWhite","Examples_Material_MaskedNavalMine","Examples_Material_TransparentWithUnderwaterFog"};var materials=new Dictionary<string,Material>();
        string upstream=Path.GetFullPath("../../work/crest-upstream/crest/Assets/Crest");
        foreach(var name in names)
        {
            string path="Assets/Crest/Crest-Examples/Examples/Materials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);materials[name]=mat;string source=File.ReadAllText(Path.Combine(upstream,path.Substring("Assets/Crest/".Length)));
            int gi=Number(source,"m_LightmapFlags");File.AppendAllText(report,$"Material={name}; GI={(int)mat.globalIlluminationFlags}; SourceGI={gi}; ShadowPass={mat.GetShaderPassEnabled("ShadowCaster")}; MetaPass={mat.FindPass("Meta")}\n");Assert.That((int)mat.globalIlluminationFlags,Is.EqualTo(gi));Assert.That(mat.FindPass("Meta"),Is.GreaterThanOrEqualTo(0));Assert.That(mat.GetShaderPassEnabled("ShadowCaster"),Is.True,"Original selected materials have no disabled shader passes");
        }
        EditorSceneManager.OpenScene("Assets/Crest/Crest-Examples/Examples/Scenes/Examples.unity");
        int fields=0,overrides=0,renderers=0;var counts=names.ToDictionary(n=>n,n=>0);
        foreach(var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
        {
            var selected=renderer.sharedMaterials.Where(m=>m!=null&&materials.Values.Contains(m)).ToArray();if(selected.Length==0)continue;
            renderers++;foreach(var m in selected)counts[m.name]++;
            var source=PrefabUtility.GetCorrespondingObjectFromOriginalSource(renderer);Assert.That(source,Is.Not.Null,renderer.name);string path=AssetDatabase.GetAssetPath(source);Assert.That(path.StartsWith("Assets/Crest/"),Is.True,path);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out string guid,out long id),Is.True);
            string yaml=File.ReadAllText(Path.Combine(upstream,path.Substring("Assets/Crest/".Length)));string node=Node(yaml,id);var serialized=new SerializedObject(renderer);
            string objectPath=renderer.name;for(var parent=renderer.transform.parent;parent!=null;parent=parent.parent)objectPath=parent.name+"/"+objectPath;
            File.AppendAllText(report,$"Renderer={objectPath}; Active={renderer.gameObject.activeInHierarchy}; Enabled={renderer.enabled}; Source={path}:{id}; Materials={string.Join(",",selected.Select(m=>m.name))}\n");
            foreach(var flag in new[]{"m_CastShadows","m_ReceiveShadows","m_ReceiveGI"})
            {
                var prop=serialized.FindProperty(flag);if(prop==null){File.AppendAllText(report,"NotApplicable="+flag+"\n");continue;}
                if(!Regex.IsMatch(node,@"(?m)^  "+flag+@": ")){File.AppendAllText(report,$"LegacyFieldNotSerialized={flag}; Resolved={prop.intValue}; NoSourceValueComparison=True\n");continue;}
                int original=Number(node,flag);File.AppendAllText(report,$"  Flag={flag}; Resolved={prop.intValue}; OriginalBase={original}; Override={prop.prefabOverride}\n");
                if(prop.prefabOverride){overrides++;continue;}Assert.That(prop.intValue,Is.EqualTo(original),objectPath+":"+flag);fields++;
            }
            var goId=Regex.Match(node,@"m_GameObject: \{fileID: (\d+)");Assert.That(goId.Success,Is.True);int originalStatic=Number(Node(yaml,long.Parse(goId.Groups[1].Value)),"m_StaticEditorFlags");var staticProp=new SerializedObject(renderer.gameObject).FindProperty("m_StaticEditorFlags");int actualStatic=(int)GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
            File.AppendAllText(report,$"  Static={actualStatic}; OriginalBase={originalStatic}; Override={staticProp.prefabOverride}\n");if(staticProp.prefabOverride)overrides++;else{Assert.That(actualStatic,Is.EqualTo(originalStatic),objectPath+":Static");fields++;}
        }
        File.AppendAllText(report,$"Renderers={renderers}; ComparedFields={fields}; OverriddenFields={overrides}; "+string.Join(";",counts.Select(p=>p.Key+"="+p.Value))+"\n");
        foreach(var count in counts)Assert.That(count.Value,Is.GreaterThan(0),count.Key);Assert.That(fields,Is.GreaterThan(0));
    }
    [UnityTearDown] public IEnumerator Cleanup(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return null;}
}
