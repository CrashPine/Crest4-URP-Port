// Owned serialized transfer of InputPersistenceTests, without repeating Editor proof. MIT.
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Crest.URP.Editor
{
    public static partial class ResourceShutdownProbeBuild
    {
        [Serializable] class SavedInputsManifest
        {
            public string scene,sceneGuid,profileGuid,surfaceGuid,profileName,surfaceName;
            public string[] materialGuids,materialNames,shaderNames;
            public string scope="D3: saved scene/profile/material/clock provenance. Player has no AssetDatabase; observe actual references/properties/native fields, not simulated runtime GUID lookup.";
        }
        static GameObject SavedInputPlane(string folder,string label,string shader,float height)
        {
            var owner=GameObject.CreatePrimitive(PrimitiveType.Plane);owner.name="Persisted "+label;owner.transform.localScale=Vector3.one*.8f;owner.transform.position=Vector3.up*height;
            var material=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(material,folder+"/"+label+".mat");owner.GetComponent<Renderer>().sharedMaterial=material;return owner;
        }
        public static void BuildSavedInputs()
        {
            if(Application.unityVersion!="6000.6.5f1")throw new InvalidOperationException("Wrong Unity target");
            bool strict=PlayerSettings.strictShaderVariantMatching;const string parent="Assets";
            string folder=parent+"/CrestSavedInputs_"+Guid.NewGuid().ToString("N"),guid=AssetDatabase.CreateFolder(parent,Path.GetFileName(folder));
            try
            {
                var control=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);string controlPath=folder+"/Control.unity";EditorSceneManager.SaveScene(control,controlPath);
                var scene=EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");var ocean=UnityEngine.Object.FindAnyObjectByType<OceanRenderer>();
                Camera.main.transform.position=new Vector3(0,10,0);foreach(var fft in UnityEngine.Object.FindObjectsByType<ShapeFFT>())fft.enabled=false;
                var surface=new Material(ocean.OceanMaterial);foreach(string keyword in new[]{"_FLOW_ON","_CLIPSURFACE_ON","_ALBEDO_ON"})surface.EnableKeyword(keyword);
                surface.SetFloat("_Flow",1);surface.SetFloat("_ClipSurface",1);surface.SetFloat("_Albedo",1);AssetDatabase.CreateAsset(surface,folder+"/Surface.mat");ocean.OceanMaterial=surface;
                var depth=ScriptableObject.CreateInstance<SimSettingsSeaFloorDepth>();depth._allowVaryingWaterLevel=true;AssetDatabase.CreateAsset(depth,folder+"/Depth.asset");ocean._simSettingsSeaFloorDepth=depth;
                var clock=ocean.gameObject.AddComponent<TimeProviderCustom>();clock._overrideTime=clock._overrideDeltaTime=true;clock._time=.5f;clock._deltaTime=0;
                var fields=new SerializedObject(ocean);foreach(string field in new[]{"_createFlowSim","_createClipSurfaceData","_createAlbedoData"})fields.FindProperty(field).boolValue=true;fields.FindProperty("_timeProvider").objectReferenceValue=clock;fields.ApplyModifiedPropertiesWithoutUndo();
                var flow=SavedInputPlane(folder,"Flow","Crest/Inputs/Flow/Fixed Direction",0);flow.GetComponent<Renderer>().sharedMaterial.SetFloat("_Speed",3);flow.GetComponent<Renderer>().sharedMaterial.SetFloat("_Direction",0);flow.AddComponent<RegisterFlowInput>();
                var albedo=SavedInputPlane(folder,"Albedo","Crest/Inputs/Albedo/Color",0);albedo.GetComponent<Renderer>().sharedMaterial.SetColor("_Color",new Color(1,0,0,.75f));albedo.AddComponent<RegisterAlbedoInput>();
                SavedInputPlane(folder,"Height","Crest/Inputs/Sea Floor Depth/Set Base Water Height Using Geometry",2).AddComponent<RegisterHeightInput>();
                var clip=new GameObject("Persisted Clip");clip.transform.localScale=new Vector3(8,4,8);clip.AddComponent<RegisterClipSurfaceInput>();
                string path=folder+"/Inputs.unity";AssetDatabase.SaveAssets();if(!EditorSceneManager.SaveScene(scene,path))throw new InvalidOperationException("Saved input scene failed");
                var manifest=new SavedInputsManifest{scene=path,sceneGuid=AssetDatabase.AssetPathToGUID(path),profileGuid=AssetDatabase.AssetPathToGUID(folder+"/Depth.asset"),surfaceGuid=AssetDatabase.AssetPathToGUID(folder+"/Surface.mat"),profileName=depth.name,surfaceName=surface.name,materialGuids=new string[3],materialNames=new string[3],shaderNames=new string[3]};
                string[] labels={"Flow","Albedo","Height"};for(int i=0;i<3;i++){var material=AssetDatabase.LoadAssetAtPath<Material>(folder+"/"+labels[i]+".mat");manifest.materialGuids[i]=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(material));manifest.materialNames[i]=material.name;manifest.shaderNames[i]=material.shader.name;}
                AssetDatabase.CreateFolder(folder,"Resources");File.WriteAllText(folder+"/Resources/CrestSavedInputsManifest.json",JsonUtility.ToJson(manifest,true));AssetDatabase.Refresh();AssetDatabase.SaveAssets();
                string snapshot="Evidence/P11-SavedInputs-fixture-"+Path.GetFileName(folder);FileUtil.CopyFileOrDirectory(folder,snapshot);File.Copy(folder+".meta",snapshot+".meta");
                PlayerSettings.strictShaderVariantMatching=true;
                var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{controlPath,path},target=BuildTarget.StandaloneWindows64,locationPathName="Builds/P11-SavedInputs/SavedInputs.exe",options=BuildOptions.Development,extraScriptingDefines=new[]{"CREST_RESOURCE_PROBE"}});
                if(build.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Saved inputs build failed");Debug.Log("SAVED_INPUTS_BUILD_SUCCEEDED bytes="+build.summary.totalSize);
            }
            finally
            {
                PlayerSettings.strictShaderVariantMatching=strict;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                if(Path.GetFullPath(folder).StartsWith(Path.GetFullPath(parent)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)&&AssetDatabase.AssetPathToGUID(folder)==guid)AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
