// Diagnostic-only Player entry point. Excluded from normal builds. MIT.
#if CREST_RESOURCE_PROBE
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Crest.URP.Diagnostics
{
    public sealed partial class ResourceShutdownProbe : MonoBehaviour
    {
        [Serializable] class TextureInfo { public string name,id,format,depthFormat,flags; public int width,height,layers,msaa; public bool created; public long nativeBytes; }
        [Serializable] class Snapshot { public string stage; public int oceans; public TextureInfo[] textures; }
        [Serializable] class Result
        {
            public string unity, api, mode, pipeline, gpu;
            public bool batch;
            public int width, height, frames, errors, crestFeatures, activeCrestFeatures;
            public bool submit, readPixels, featureDisabled;
            public float warmSeconds;
            public Snapshot warm, drained;
        }
        int _errors;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartProbe()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-crestResourceProbe") < 0) return;
            Application.runInBackground = true;
            var owner = new GameObject("Resource shutdown diagnostic");
            DontDestroyOnLoad(owner);
            owner.AddComponent<ResourceShutdownProbe>();
        }
        string Argument(string name, string fallback)
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }
        void Error(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errors++;
        }
        Snapshot Capture(string stage)
        {
            return new Snapshot { stage = stage,
                oceans = Resources.FindObjectsOfTypeAll<OceanRenderer>().Length,
                textures = Resources.FindObjectsOfTypeAll<RenderTexture>().Select(t => new TextureInfo {
                    name=t.name,id=t.GetEntityId().ToString(),format=t.graphicsFormat.ToString(),depthFormat=t.depthStencilFormat.ToString(),flags=t.hideFlags.ToString(),msaa=t.antiAliasing,created=t.IsCreated(), width=t.width, height=t.height, layers=t.volumeDepth,
                    nativeBytes=Profiler.GetRuntimeMemorySizeLong(t) }).ToArray() };
        }
        IEnumerator Start()
        {
            Application.logMessageReceived += Error;
            string mode = Argument("-crestProbeMode", "Empty");
            if (mode != "Empty" && mode != "Ocean" && mode != "OceanUnload")
                throw new ArgumentException("Unknown resource diagnostic mode: " + mode);
            bool disableFeature=Array.IndexOf(Environment.GetCommandLineArgs(), "-crestProbeDisableFeature") >= 0;
            bool submit=Array.IndexOf(Environment.GetCommandLineArgs(), "-crestProbeSubmit") >= 0;
            bool readPixels=Array.IndexOf(Environment.GetCommandLineArgs(), "-crestProbeReadPixels") >= 0;
            // Force renderer construction before counting/optionally disabling features.
            ((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).GetRenderer(0);
            var features=Resources.FindObjectsOfTypeAll<CrestBackgroundFeature>();
            if (disableFeature) foreach (var feature in features) feature.SetActive(false);
            OceanRenderer.EnableGraphicsInBatchMode = true;
            if (mode != "Empty") yield return SceneManager.LoadSceneAsync(1);
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-crestReflectionCost") >= 0)
            {
                yield return ReflectionCost();yield break;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-crestColdBindings") >= 0)
            {
                yield return ColdBindings(mode);yield break;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-crestOwnership") >= 0)
            {
                yield return Ownership(mode);yield break;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-crestProfile") >= 0)
            {
                yield return Profile(mode);
                yield break;
            }
            int frames = 0; float begin = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - begin < 3 || frames < 120) { frames++; yield return null; }
            if (submit)
            {
                var target=new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
                var previous=RenderTexture.active;
                Texture2D pixels=null;
                try
                {
                    target.Create();
                    RenderPipeline.SubmitRenderRequest(Camera.main, new UniversalRenderPipeline.SingleCameraRequest { destination=target });
                    if (readPixels)
                    {
                        RenderTexture.active=target;
                        pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
                        pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();
                    }
                    var done=AsyncGPUReadback.Request(target);
                    while(!done.done) yield return null;
                    if(done.hasError) _errors++;
                }
                finally
                {
                    RenderTexture.active=previous;
                    target.Release();Destroy(target);
                    if(pixels!=null)Destroy(pixels);
                }
                yield return null;yield return null;
            }
            var result = new Result { unity=Application.unityVersion, api=SystemInfo.graphicsDeviceType.ToString(),
                gpu=SystemInfo.graphicsDeviceName, mode=mode, batch=Application.isBatchMode,
                pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name,
                width=Screen.width, height=Screen.height, frames=frames, warmSeconds=Time.realtimeSinceStartup-begin,
                warm=Capture("warm"), submit=submit, readPixels=readPixels, featureDisabled=disableFeature,
                crestFeatures=features.Length, activeCrestFeatures=features.Count(f=>f.isActive) };
            if (result.warm.oceans != (mode == "Empty" ? 0 : 1)) _errors++;
            if (mode == "OceanUnload")
            {
                yield return SceneManager.LoadSceneAsync(0);
                yield return Resources.UnloadUnusedAssets();
                GC.Collect();
                for (int i = 0; i < 60; i++) yield return null;
                result.drained = Capture("after-ocean-unload");
                if (result.drained.oceans != 0) _errors++;
            }
            result.errors=_errors;
            string output=Argument("-crestProbeOutput", "ResourceProbe.json");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output, JsonUtility.ToJson(result, true));
            Debug.Log("RESOURCE_PROBE_COMPLETED " + mode + " errors=" + _errors);
            Application.Quit(_errors == 0 ? 0 : 1);
        }
        void OnDestroy() { Application.logMessageReceived -= Error; }
    }
}
#endif
