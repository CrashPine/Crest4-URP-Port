// Owned async ProgressiveCPU inputs for compiled lightmap retention checks. MIT.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Crest.URP.Editor
{
    public static partial class ResourceShutdownProbeBuild
    {
        [Serializable] class LightmapReceiverSource
        {
            public string name,materialGuid,shader,giFlags;public int index,metaPass;public Vector4 st;public bool shadowPass;
        }
        [Serializable] class LightmapSceneSource
        {
            public string scene,sceneGuid,mode,backend,lightingDataGuid;public int modeValue;public string[] colorGuids,directionGuids;
            public List<LightmapReceiverSource> receivers=new List<LightmapReceiverSource>();
        }
        [Serializable] class LightmapSourceManifest
        {
            public string unity,scope="F finite two-mode compiled retention fixture; original material flags retained, owned ContributeGI/UV2 meshes. No original scene rebake or full lighting acceptance.";
            public List<LightmapSceneSource> scenes=new List<LightmapSceneSource>();
        }
        static string _lmFolder,_lmGuid,_lmControl,_lmScene,_lmPhase;
        static int _lmMode,_lmWarm;static double _lmDeadline;
        static bool _lmStrict;static RenderTexture _lmTarget;static Camera _lmCamera;static Light _lmSun;
        static List<Renderer> _lmReceivers;static LightmapSourceManifest _lmManifest;
        static void LightmapRequire(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        public static void BuildLightmaps()
        {
            _lmStrict=PlayerSettings.strictShaderVariantMatching;
            try
            {
                LightmapRequire(Application.unityVersion=="6000.6.5f1","Wrong Unity target");
                _lmStrict=PlayerSettings.strictShaderVariantMatching;_lmFolder="Assets/CrestLightmaps_"+Guid.NewGuid().ToString("N");_lmGuid=AssetDatabase.CreateFolder("Assets",Path.GetFileName(_lmFolder));
                var control=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);_lmControl=_lmFolder+"/Control.unity";EditorSceneManager.SaveScene(control,_lmControl);
                _lmManifest=new LightmapSourceManifest{unity=Application.unityVersion};_lmMode=0;PrepareLightmapScene();EditorApplication.update+=TickLightmapBuild;
            }
            catch(Exception error){FinishLightmapBuild(error);}
        }
        // A diagnostic-only assertion fix must not repeat the two real bakes.
        public static void RebuildLightmapsFromFixture()
        {
            _lmStrict=PlayerSettings.strictShaderVariantMatching;var preloads=PlayerSettings.GetPreloadedAssets();Exception failure=null;
            try
            {
                LightmapRequire(Application.unityVersion=="6000.6.5f1","Wrong Unity target");
                _lmManifest=JsonUtility.FromJson<LightmapSourceManifest>(File.ReadAllText("Evidence/P11-Lightmaps-bake-manifest.json"));LightmapRequire(_lmManifest.scenes.Count==2,"Missing saved bake source");
                _lmFolder=Path.GetDirectoryName(Path.GetDirectoryName(_lmManifest.scenes[0].scene)).Replace('\\','/');
                LightmapRequire(Path.GetFullPath(_lmFolder).StartsWith(Path.GetFullPath("Assets")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(_lmFolder).StartsWith("CrestLightmaps_",StringComparison.Ordinal)&&!Directory.Exists(_lmFolder)&&!File.Exists(_lmFolder+".meta"),"Refuse to overwrite existing fixture");
                string snapshot="Evidence/P11-Lightmaps-fixture-"+Path.GetFileName(_lmFolder);LightmapRequire(Directory.Exists(snapshot)&&File.Exists(snapshot+".meta"),"Exact serialized baked fixture unavailable");
                var check=System.Text.RegularExpressions.Regex.Match(File.ReadAllText(snapshot+".meta"),@"(?m)^guid: ([a-f0-9]{32})");LightmapRequire(check.Success,"Missing original fixture GUID");_lmGuid=check.Groups[1].Value;
                FileUtil.CopyFileOrDirectory(snapshot,_lmFolder);File.Copy(snapshot+".meta",_lmFolder+".meta");AssetDatabase.Refresh();LightmapRequire(AssetDatabase.AssetPathToGUID(_lmFolder)==_lmGuid,"Restored source GUID changed");
                foreach(var source in _lmManifest.scenes)
                {
                    var mode=(LightmapsMode)Enum.Parse(typeof(LightmapsMode),source.mode);source.modeValue=(int)mode;
                    LightmapRequire(mode==LightmapsMode.NonDirectional||mode==LightmapsMode.CombinedDirectional,"Unexpected archived enum value");source.mode=mode==LightmapsMode.NonDirectional?nameof(LightmapsMode.NonDirectional):nameof(LightmapsMode.CombinedDirectional);
                    LightmapRequire(AssetDatabase.AssetPathToGUID(source.scene)==source.sceneGuid,"Restored scene GUID changed");
                }
                string manifest=_lmFolder+"/Resources/CrestLightmapManifest.json";File.WriteAllText(manifest,JsonUtility.ToJson(_lmManifest,true));AssetDatabase.ImportAsset(manifest);AssetDatabase.SaveAssets();File.WriteAllText("Evidence/P11-Lightmaps-rebuild-manifest.json",JsonUtility.ToJson(_lmManifest,true));
                string rebuilt="Evidence/P11-Lightmaps-reused-"+Path.GetFileName(_lmFolder);LightmapRequire(!Directory.Exists(rebuilt),"Do not overwrite prior reuse evidence");FileUtil.CopyFileOrDirectory(_lmFolder,rebuilt);File.Copy(_lmFolder+".meta",rebuilt+".meta");
                PlayerSettings.strictShaderVariantMatching=true;
                var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{_lmFolder+"/Control.unity"}.Concat(_lmManifest.scenes.Select(s=>s.scene)).ToArray(),target=BuildTarget.StandaloneWindows64,locationPathName="Builds/P11-Lightmaps/Lightmaps.exe",options=BuildOptions.Development,extraScriptingDefines=new[]{"CREST_RESOURCE_PROBE"}});
                LightmapRequire(build.summary.result==BuildResult.Succeeded,"Reused lightmap fixture build failed");Debug.Log("LIGHTMAP_REUSE_BUILD_SUCCEEDED bytes="+build.summary.totalSize);
            }
            catch(Exception error){failure=error;Debug.LogException(error);File.WriteAllText("Evidence/P11-Lightmaps-rebuild-failure.txt",error.ToString());}
            finally{PlayerSettings.SetPreloadedAssets(preloads);FinishLightmapBuild(failure);}
        }
        static void PrepareLightmapScene()
        {
            string name=_lmMode==0?"NonDirectional":"CombinedDirectional";AssetDatabase.CreateFolder(_lmFolder,name);string folder=_lmFolder+"/"+name;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var settings=new LightingSettings{bakedGI=true,realtimeGI=false,lightmapper=LightingSettings.Lightmapper.ProgressiveCPU,lightmapResolution=8,lightmapMaxSize=256,directSampleCount=16,indirectSampleCount=16,environmentSampleCount=16,bounces=1,filteringMode=LightingSettings.FilterMode.None,directionalityMode=_lmMode==0?LightmapsMode.NonDirectional:LightmapsMode.CombinedDirectional};
            AssetDatabase.CreateAsset(settings,folder+"/Lighting.asset");Lightmapping.lightingSettings=settings;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.black;RenderSettings.ambientIntensity=0;RenderSettings.skybox=null;RenderSettings.reflectionIntensity=0;
            _lmReceivers=new List<Renderer>();
            foreach(var entry in new[]{("Examples_Material_MaskedWhite",-2f),("Examples_Material_MaskedNavalMine",0f),("Examples_Material_TransparentWithUnderwaterFog",2f)})
            {
                var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/Crest/Crest-Examples/Examples/Materials/"+entry.Item1+".mat");LightmapRequire(source!=null,"Missing source material");
                var material=new Material(source);LightmapRequire(material.globalIlluminationFlags==source.globalIlluminationFlags&&material.GetShaderPassEnabled("ShadowCaster")==source.GetShaderPassEnabled("ShadowCaster"),"Source GI/pass flags changed");
                if(material.HasProperty("_Mask")){material.DisableKeyword("_MASK_MASKED");material.DisableKeyword("_MASK_FILL");material.EnableKeyword("_MASK_NONE");material.SetFloat("_Mask",0);}
                AssetDatabase.CreateAsset(material,folder+"/"+entry.Item1+".mat");
                AddLightmapCube(folder,entry.Item1,new Vector3(entry.Item2,.5f,0),Vector3.one,material);
            }
            var floorMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));floorMaterial.SetColor("_BaseColor",Color.white);AssetDatabase.CreateAsset(floorMaterial,folder+"/Floor.mat");AddLightmapCube(folder,"Lit floor reference",new Vector3(0,-.1f,0),new Vector3(9,.2f,6),floorMaterial);
            _lmSun=new GameObject("Baked sun",typeof(Light)).GetComponent<Light>();_lmSun.type=LightType.Directional;_lmSun.intensity=2;_lmSun.lightmapBakeType=LightmapBakeType.Baked;_lmSun.shadows=LightShadows.Hard;_lmSun.transform.rotation=Quaternion.Euler(55,-30,0);RenderSettings.sun=_lmSun;
            _lmCamera=new GameObject("Bake camera",typeof(Camera)).GetComponent<Camera>();_lmCamera.tag="MainCamera";_lmCamera.clearFlags=CameraClearFlags.SolidColor;_lmCamera.backgroundColor=Color.black;_lmCamera.allowMSAA=false;_lmCamera.transform.position=new Vector3(6,6,-9);_lmCamera.transform.LookAt(new Vector3(0,.5f,0));_lmCamera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            _lmTarget=new RenderTexture(640,360,24);_lmTarget.Create();_lmScene=folder+"/Bake.unity";LightmapRequire(EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),_lmScene),"Failed to save owned bake scene");
            _lmWarm=0;_lmPhase="warm";_lmDeadline=EditorApplication.timeSinceStartup+600;File.WriteAllText("Evidence/P11-Lightmaps-bake-state.json",JsonUtility.ToJson(new LightmapSceneSource{scene=_lmScene,mode=name,backend=settings.lightmapper.ToString()},true));
        }
        static void AddLightmapCube(string folder,string name,Vector3 position,Vector3 scale,Material material)
        {
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.name=name;cube.transform.position=position;cube.transform.localScale=scale;
            var mesh=UnityEngine.Object.Instantiate(cube.GetComponent<MeshFilter>().sharedMesh);Unwrapping.GenerateSecondaryUVSet(mesh);AssetDatabase.CreateAsset(mesh,folder+"/"+name+".asset");cube.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=cube.GetComponent<Renderer>();renderer.sharedMaterial=material;GameObjectUtility.SetStaticEditorFlags(cube,StaticEditorFlags.ContributeGI);_lmReceivers.Add(renderer);
        }
        static void TickLightmapBuild()
        {
            try
            {
                LightmapRequire(EditorApplication.timeSinceStartup<_lmDeadline,"CPU bake preparation exceeded600s per scene");
                if(_lmPhase=="warm")
                {
                    RenderPipeline.SubmitRenderRequest(_lmCamera,new UniversalRenderPipeline.SingleCameraRequest{destination=_lmTarget});
                    if(++_lmWarm<3){EditorApplication.QueuePlayerLoopUpdate();return;}
                    LightmapRequire(RenderPipelineManager.currentPipeline is UniversalRenderPipeline,"URP not initialized for Meta bake");
                    LightmapRequire(Lightmapping.BakeAsync(),"Actual ProgressiveCPU bake did not start");_lmPhase="baking";return;
                }
                if(_lmPhase!="baking"||Lightmapping.isRunning)return;
                var expected=_lmMode==0?LightmapsMode.NonDirectional:LightmapsMode.CombinedDirectional;
                LightmapRequire(Lightmapping.lightingSettings.lightmapper==LightingSettings.Lightmapper.ProgressiveCPU&&LightmapSettings.lightmapsMode==expected&&Lightmapping.lightingDataAsset!=null&&LightmapSettings.lightmaps.Length>0,"CPU bake did not produce expected serialized lighting");
                var source=new LightmapSceneSource{scene=_lmScene,sceneGuid=AssetDatabase.AssetPathToGUID(_lmScene),mode=expected.ToString(),modeValue=(int)expected,backend=Lightmapping.lightingSettings.lightmapper.ToString(),lightingDataGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset)),colorGuids=new string[LightmapSettings.lightmaps.Length],directionGuids=new string[LightmapSettings.lightmaps.Length]};
                for(int i=0;i<LightmapSettings.lightmaps.Length;i++){var map=LightmapSettings.lightmaps[i];LightmapRequire(map.lightmapColor!=null&&(expected==LightmapsMode.NonDirectional?map.lightmapDir==null:map.lightmapDir!=null),"Baked atlas directionality mismatch");source.colorGuids[i]=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(map.lightmapColor));source.directionGuids[i]=map.lightmapDir!=null?AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(map.lightmapDir)):"";}
                foreach(var renderer in _lmReceivers){LightmapRequire(renderer.lightmapIndex>=0&&renderer.lightmapIndex<LightmapSettings.lightmaps.Length,"Unbound baked receiver");source.receivers.Add(new LightmapReceiverSource{name=renderer.name,index=renderer.lightmapIndex,st=renderer.lightmapScaleOffset,materialGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(renderer.sharedMaterial)),shader=renderer.sharedMaterial.shader.name,giFlags=renderer.sharedMaterial.globalIlluminationFlags.ToString(),shadowPass=renderer.sharedMaterial.GetShaderPassEnabled("ShadowCaster"),metaPass=renderer.sharedMaterial.FindPass("Meta")});}
                _lmSun.enabled=false;EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();_lmManifest.scenes.Add(source);File.WriteAllText("Evidence/P11-Lightmaps-bake-manifest.json",JsonUtility.ToJson(_lmManifest,true));
                _lmTarget.Release();UnityEngine.Object.DestroyImmediate(_lmTarget);_lmTarget=null;
                if(++_lmMode<2){PrepareLightmapScene();return;}
                EditorApplication.update-=TickLightmapBuild;_lmPhase="building";
                AssetDatabase.CreateFolder(_lmFolder,"Resources");File.WriteAllText(_lmFolder+"/Resources/CrestLightmapManifest.json",JsonUtility.ToJson(_lmManifest,true));AssetDatabase.Refresh();AssetDatabase.SaveAssets();
                string snapshot="Evidence/P11-Lightmaps-fixture-"+Path.GetFileName(_lmFolder);FileUtil.CopyFileOrDirectory(_lmFolder,snapshot);File.Copy(_lmFolder+".meta",snapshot+".meta");
                PlayerSettings.strictShaderVariantMatching=true;
                var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{_lmControl}.Concat(_lmManifest.scenes.Select(s=>s.scene)).ToArray(),target=BuildTarget.StandaloneWindows64,locationPathName="Builds/P11-Lightmaps/Lightmaps.exe",options=BuildOptions.Development,extraScriptingDefines=new[]{"CREST_RESOURCE_PROBE"}});
                LightmapRequire(build.summary.result==BuildResult.Succeeded,"Lightmap diagnostic build failed");Debug.Log("LIGHTMAP_BUILD_SUCCEEDED bytes="+build.summary.totalSize);FinishLightmapBuild(null);
            }
            catch(Exception error){FinishLightmapBuild(error);}
        }
        static void FinishLightmapBuild(Exception error)
        {
            EditorApplication.update-=TickLightmapBuild;
            if(error!=null){Debug.LogException(error);File.WriteAllText("Evidence/P11-Lightmaps-build-failure.txt",error.ToString());}
            try
            {
                if(Lightmapping.isRunning)Lightmapping.Cancel();if(_lmTarget!=null){_lmTarget.Release();UnityEngine.Object.DestroyImmediate(_lmTarget);_lmTarget=null;}
                PlayerSettings.strictShaderVariantMatching=_lmStrict;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                if(!string.IsNullOrEmpty(_lmFolder)&&AssetDatabase.IsValidFolder(_lmFolder))
                {
                    string backup="Evidence/P11-Lightmaps-retained-"+Path.GetFileName(_lmFolder);if(error!=null&&!Directory.Exists(backup)){FileUtil.CopyFileOrDirectory(_lmFolder,backup);File.Copy(_lmFolder+".meta",backup+".meta");}
                    if(Path.GetFullPath(_lmFolder).StartsWith(Path.GetFullPath("Assets")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)&&AssetDatabase.AssetPathToGUID(_lmFolder)==_lmGuid)AssetDatabase.DeleteAsset(_lmFolder);
                }
            }
            catch(Exception cleanup){Debug.LogException(cleanup);error=error??cleanup;}
            EditorApplication.Exit(error==null?0:1);
        }
    }
}
