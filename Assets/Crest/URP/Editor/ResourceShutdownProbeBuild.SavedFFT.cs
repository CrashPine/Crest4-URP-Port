// Owned saved-scene fixture for the finite compiled FFT integration transfer. MIT.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Crest.URP.Editor
{
    public static partial class ResourceShutdownProbeBuild
    {
        [Serializable] class SavedFFTManifest
        {
            public string scene,sceneGuid,settingsGuid,dataGuid,spectrumGuid,settingsName,dataName,rawHalfSHA256;
            public int rawHalfCount;public string scope="D1: real GPU FFT bake saved into serialized scene/profile/data; source-off GPU queries are flat controls, not baked/GPU visual parity.";
        }
        public static void BuildSavedFFT() => BuildSavedFFT(false);
        public static void BuildHeadlessFFT() => BuildSavedFFT(true);

        static void BuildSavedFFT(bool headless)
        {
            if(Application.unityVersion!="6000.6.5f1")throw new InvalidOperationException("Wrong Unity target");
            bool graphics=OceanRenderer.EnableGraphicsInBatchMode,strict=PlayerSettings.strictShaderVariantMatching;
            const string parent="Assets";string folder=parent+"/CrestSavedFFT_"+Guid.NewGuid().ToString("N");
            string guid=AssetDatabase.CreateFolder(parent,Path.GetFileName(folder));FFTBakedData data=null;
            try
            {
                var control=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                string controlPath=folder+"/Control.unity";EditorSceneManager.SaveScene(control,controlPath);
                OceanRenderer.EnableGraphicsInBatchMode=true;
                var scene=EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
                var ocean=UnityEngine.Object.FindFirstObjectByType<OceanRenderer>();
                // CustomMonoBehaviour schedules this supported Unity property via Invoke.
                // Synchronous executeMethod preparation precedes that callback.
                if(ocean!=null)ocean.runInEditMode=true;
                if(ocean==null||OceanRenderer.Instance!=ocean)throw new InvalidOperationException("Offline bake requires initialized scene ocean");
                Camera.main.transform.position=new Vector3(0,4,0);
                foreach(var shape in UnityEngine.Object.FindObjectsByType<ShapeWaves>())shape.enabled=false;
                ocean._globalWindZone=null;ocean._globalWindSpeed=60;
                var spectrum=ScriptableObject.CreateInstance<OceanWaveSpectrum>();spectrum.name="Persisted choppy spectrum";spectrum._chop=1.6f;
                var fields=new SerializedObject(spectrum);
                for(int i=0;i<OceanWaveSpectrum.NUM_OCTAVES;i++){fields.FindProperty("_powerDisabled").GetArrayElementAtIndex(i).boolValue=i!=7;fields.FindProperty("_powerLog").GetArrayElementAtIndex(i).floatValue=-2;}
                fields.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.CreateAsset(spectrum,folder+"/Spectrum.asset");
                var owner=new GameObject("Offline FFT source (disabled)");owner.SetActive(false);var fft=owner.AddComponent<ShapeFFT>();
                fft._spectrum=spectrum;fft._resolution=16;fft._enableBakedCollision=true;fft._timeLoopLength=4;fft._timeResolution=4;fft._smallestWavelengthRequired=8;fft._overrideGlobalWindSpeed=true;fft._windSpeed=60;
                data=FFTBaker.BakeShapeFFT(fft,folder+"/Choppy.asset");if(data==null)throw new InvalidOperationException("Real FFT bake failed");
                data.name="Persisted choppy FFT";EditorUtility.SetDirty(data);
                var settings=ScriptableObject.CreateInstance<SimSettingsAnimatedWaves>();settings.name="Persisted FFT profile";settings.CollisionSource=SimSettingsAnimatedWaves.CollisionSources.ComputeShaderQueries;settings._bakedFFTData=data;
                if (headless) settings.CollisionSource = SimSettingsAnimatedWaves.CollisionSources.BakedFFT;
                AssetDatabase.CreateAsset(settings,folder+"/Settings.asset");ocean._simSettingsAnimatedWaves=settings;
                var clock=ocean.gameObject.AddComponent<TimeProviderCustom>();clock._overrideTime=clock._overrideDeltaTime=true;clock._time=.125f;clock._deltaTime=0;
                var oceanFields=new SerializedObject(ocean);oceanFields.FindProperty("_timeProvider").objectReferenceValue=clock;oceanFields.ApplyModifiedPropertiesWithoutUndo();
                string path=folder+"/SavedFFT.unity";if(!EditorSceneManager.SaveScene(scene,path))throw new InvalidOperationException("Could not save owned scene");
                AssetDatabase.CreateFolder(folder,"Resources");
                var raw=(Array)typeof(FFTBakedData).GetField("_framesFlattened",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(data);
                var halfValue=raw.GetType().GetElementType().GetField("value");byte[] bytes=new byte[raw.Length*2];
                for(int i=0;i<raw.Length;i++){ushort value=(ushort)halfValue.GetValue(raw.GetValue(i));bytes[i*2]=(byte)value;bytes[i*2+1]=(byte)(value>>8);}
                string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
                var manifest=new SavedFFTManifest{scene=path,sceneGuid=AssetDatabase.AssetPathToGUID(path),settingsGuid=AssetDatabase.AssetPathToGUID(folder+"/Settings.asset"),dataGuid=AssetDatabase.AssetPathToGUID(folder+"/Choppy.asset"),spectrumGuid=AssetDatabase.AssetPathToGUID(folder+"/Spectrum.asset"),settingsName=settings.name,dataName=data.name,rawHalfSHA256=hash,rawHalfCount=raw.Length};
                File.WriteAllText(folder+"/Resources/CrestSavedFFTManifest.json",JsonUtility.ToJson(manifest,true));
                AssetDatabase.Refresh();AssetDatabase.SaveAssets();
                // Keep exact serialized source/GUIDs and bake bytes outside Assets before cleanup.
                string snapshot = (headless ? "Evidence/P13-HeadlessFFT-fixture-" : "Evidence/P11-SavedFFT-fixture-") + Path.GetFileName(folder);
                FileUtil.CopyFileOrDirectory(folder,snapshot);File.Copy(folder+".meta",snapshot+".meta");
                PlayerSettings.strictShaderVariantMatching=true;
                string executable = headless ? "Builds/P13-HeadlessFFT/HeadlessFFT.exe" : "Builds/P11-SavedFFT/SavedFFT.exe";
                var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{controlPath,path},target=BuildTarget.StandaloneWindows64,locationPathName=executable,options=BuildOptions.Development,extraScriptingDefines=new[]{"CREST_RESOURCE_PROBE"}});
                if(build.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Saved FFT diagnostic build failed");
                Debug.Log("SAVED_FFT_BUILD_SUCCEEDED bytes="+build.summary.totalSize+" rawHalfSHA256="+hash);
            }
            finally
            {
                PlayerSettings.strictShaderVariantMatching=strict;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                if(data!=null)Resources.UnloadAsset(data);
                OceanRenderer.EnableGraphicsInBatchMode=graphics;
                if(Path.GetFullPath(folder).StartsWith(Path.GetFullPath(parent)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)&&AssetDatabase.AssetPathToGUID(folder)==guid)AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
