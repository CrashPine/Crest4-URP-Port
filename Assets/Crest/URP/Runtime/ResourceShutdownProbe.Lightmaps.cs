// Compiled lightmap binding controls matching ExampleBakeTests. MIT.
#if CREST_RESOURCE_PROBE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Crest.URP.Diagnostics
{
    public sealed partial class ResourceShutdownProbe
    {
        [Serializable] class LightmapReceiverSource{public string name,materialGuid,shader,giFlags;public int index,metaPass;public Vector4 st;public bool shadowPass;}
        [Serializable] class LightmapSceneSource{public string scene,sceneGuid,mode,backend,lightingDataGuid;public int modeValue;public string[] colorGuids,directionGuids;public List<LightmapReceiverSource> receivers;}
        [Serializable] class LightmapSourceManifest{public string unity,scope;public List<LightmapSceneSource> scenes;}
        [Serializable] class LightmapReceiverResult{public string name,material,shader,giFlags;public int index,changed,restored;public Vector4 st;}
        [Serializable] class LightmapPlayerRow
        {
            public string scene,mode,colorFormat,depthFormat;public int modeValue;public int width,height,samples,atlases,bright,allChanged,restored,pink,enabledLights;
            public float renderScale;public bool directionalityMatches,serializedBindings;public string[] colors,directions;
            public List<LightmapReceiverResult> receivers=new List<LightmapReceiverResult>();
        }
        [Serializable] class LightmapPlayerReport{public string unity,api,gpu,scope,failure;public int errors;public LightmapSourceManifest provenance;public List<LightmapPlayerRow> rows=new List<LightmapPlayerRow>();}
        LightmapPlayerReport _lightmapReport;ModeObserver _lightmapObserver;RenderTexture _lightmapTarget;
        void SaveLightmapReport(){if(_lightmapReport==null)return;_lightmapReport.errors=_errors;File.WriteAllText(Argument("-crestProbeOutput","Lightmaps.json"),JsonUtility.ToJson(_lightmapReport,true));}
        Color32[] CaptureLightmaps(Camera camera,string image=null)
        {
            var previous=RenderTexture.active;var cpu=new Texture2D(640,360,TextureFormat.RGBA32,false);_lightmapObserver.Count=0;_lightmapObserver.Armed=true;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=_lightmapTarget});
                CheckExample(_lightmapObserver.Count==1,"Missing actual lightmap camera observation");var d=_lightmapObserver.Descriptor;
                CheckExample(d.width==640&&d.height==360&&d.msaaSamples==1&&_lightmapObserver.Scale==1,"Unexpected actual lightmap camera descriptor");
                RenderTexture.active=_lightmapTarget;cpu.ReadPixels(new Rect(0,0,640,360),0,0);cpu.Apply();var pixels=cpu.GetPixels32();
                if(image!=null)File.WriteAllBytes(Path.Combine(Argument("-crestProbeImages","."),image+".png"),cpu.EncodeToPNG());
                CheckExample(!pixels.Any(p=>p.r>245&&p.g<10&&p.b>245),"Lightmap error-pink output");return pixels;
            }
            finally{_lightmapObserver.Armed=false;RenderTexture.active=previous;Destroy(cpu);}
        }
        static int LightmapPixelChanges(Color32[] a,Color32[] b)=>a.Zip(b,(x,y)=>Math.Abs(x.r-y.r)+Math.Abs(x.g-y.g)+Math.Abs(x.b-y.b)>6?1:0).Sum();
        static int LightmapRestoreChanges(Color32[] a,Color32[] b)=>a.Zip(b,(x,y)=>x.Equals(y)?0:1).Sum();
        void MeasureLightmapScene(Camera camera,LightmapSceneSource source)
        {
            var maps=LightmapSettings.lightmaps;var row=new LightmapPlayerRow{scene=SceneManager.GetActiveScene().path,mode=LightmapSettings.lightmapsMode.ToString(),modeValue=(int)LightmapSettings.lightmapsMode,atlases=maps.Length,enabledLights=FindObjectsByType<Light>().Count(l=>l.isActiveAndEnabled),colors=maps.Select(m=>m.lightmapColor!=null?m.lightmapColor.name+":"+m.lightmapColor.graphicsFormat:"null").ToArray(),directions=maps.Select(m=>m.lightmapDir!=null?m.lightmapDir.name+":"+m.lightmapDir.graphicsFormat:"null").ToArray()};
            _lightmapReport.rows.Add(row);SaveLightmapReport();
            CheckExample(row.scene==source.scene&&row.modeValue==source.modeValue&&row.atlases>0&&row.atlases==source.colorGuids.Length,"Saved lightmap mode/atlas binding");
            bool directional=source.modeValue==(int)LightmapsMode.CombinedDirectional;row.directionalityMatches=maps.All(m=>m.lightmapColor!=null&&(directional?m.lightmapDir!=null:m.lightmapDir==null));
            CheckExample(row.directionalityMatches&&row.enabledLights==0&&RenderSettings.ambientMode==AmbientMode.Flat&&RenderSettings.ambientLight==Color.black&&RenderSettings.ambientIntensity==0&&RenderSettings.reflectionIntensity==0&&RenderSettings.skybox==null,"Baked-only lighting or actual direction texture contract");
            var renderers=source.receivers.Select(s=>GameObject.Find(s.name).GetComponent<Renderer>()).ToArray();CheckExample(renderers.Length==4,"Missing saved receivers");
            for(int i=0;i<renderers.Length;i++)
            {
                var r=renderers[i];var s=source.receivers[i];CheckExample(r.lightmapIndex==s.index&&r.lightmapScaleOffset.Equals(s.st)&&r.sharedMaterial.shader.name==s.shader&&r.sharedMaterial.globalIlluminationFlags.ToString()==s.giFlags&&r.sharedMaterial.GetShaderPassEnabled("ShadowCaster")==s.shadowPass,"Serialized receiver/material GI/pass/index/ST changed");
            }
            row.serializedBindings=true;var on=CaptureLightmaps(camera,source.mode+"-on");var d=_lightmapObserver.Descriptor;row.width=d.width;row.height=d.height;row.samples=d.msaaSamples;row.renderScale=_lightmapObserver.Scale;row.colorFormat=d.graphicsFormat.ToString();row.depthFormat=d.depthStencilFormat.ToString();row.bright=on.Count(p=>Math.Max(p.r,Math.Max(p.g,p.b))>30);
            var indices=renderers.Select(r=>r.lightmapIndex).ToArray();Color32[] off;
            try{foreach(var r in renderers)r.lightmapIndex=-1;off=CaptureLightmaps(camera,source.mode+"-all-off");}
            finally{for(int i=0;i<renderers.Length;i++)renderers[i].lightmapIndex=indices[i];}
            row.allChanged=LightmapPixelChanges(on,off);SaveLightmapReport();
            foreach(var receiver in renderers.Take(3))
            {
                var result=new LightmapReceiverResult{name=receiver.name,material=receiver.sharedMaterial.name,shader=receiver.sharedMaterial.shader.name,giFlags=receiver.sharedMaterial.globalIlluminationFlags.ToString(),index=receiver.lightmapIndex,st=receiver.lightmapScaleOffset};row.receivers.Add(result);
                Color32[] individual;int index=receiver.lightmapIndex;
                try{receiver.lightmapIndex=-1;individual=CaptureLightmaps(camera);}finally{receiver.lightmapIndex=index;}
                result.changed=LightmapPixelChanges(on,individual);result.restored=LightmapRestoreChanges(on,CaptureLightmaps(camera));SaveLightmapReport();
                CheckExample(result.changed>20&&result.restored==0,"Individual compiled lightmap contribution/restoration");
            }
            row.restored=LightmapRestoreChanges(on,CaptureLightmaps(camera));SaveLightmapReport();CheckExample(row.bright>5000&&row.allChanged>1000&&row.restored==0,"Compiled total lightmap contribution/restoration");
        }
        IEnumerator LightmapsPlayer()
        {
            Directory.CreateDirectory(Argument("-crestProbeImages","."));_lightmapReport=new LightmapPlayerReport{unity=Application.unityVersion,api=SystemInfo.graphicsDeviceType.ToString(),gpu=SystemInfo.graphicsDeviceName,scope="P11.3-F finite compiled NonDirectional/CombinedDirectional CPU-baked fixture; three source material clones and individual/all lightmap binding controls, serialized bindings, strict matching. No original scene flags/rebake, all lighting/XR/performance/leak-free acceptance."};
            var text=Resources.Load<TextAsset>("CrestLightmapManifest");CheckExample(text!=null,"Missing saved bake manifest");_lightmapReport.provenance=JsonUtility.FromJson<LightmapSourceManifest>(text.text);SaveLightmapReport();CheckExample(_lightmapReport.provenance.scenes.Count==2,"Missing two baked modes");
            _lightmapTarget=new RenderTexture(640,360,24,RenderTextureFormat.ARGB32);_lightmapTarget.Create();Time.timeScale=0;
            for(int i=0;i<2;i++)
            {
                yield return SceneManager.LoadSceneAsync(i+1);var camera=Camera.main;camera.enabled=false;camera.aspect=16f/9;_lightmapObserver=new ModeObserver{Target=camera};CameraGraphPassRegistry.Register(_lightmapObserver);
                for(int warm=0;warm<4;warm++)yield return null;
                yield return ModeCapture(()=>MeasureLightmapScene(camera,_lightmapReport.provenance.scenes[i]));CameraGraphPassRegistry.Unregister(_lightmapObserver);
            }
            if(RenderTexture.active==_lightmapTarget)RenderTexture.active=null;_lightmapTarget.Release();Destroy(_lightmapTarget);SaveLightmapReport();Debug.Log("LIGHTMAPS_COMPLETED errors="+_errors);Application.Quit(_errors==0?0:1);
        }
    }
}
#endif
