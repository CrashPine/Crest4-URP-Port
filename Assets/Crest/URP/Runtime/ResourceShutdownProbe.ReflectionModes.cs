// Finite compiled transfer of PlanarReflectionTests renderer/target controls. MIT.
#if CREST_RESOURCE_PROBE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Crest.URP.Diagnostics
{
    public sealed partial class ResourceShutdownProbe
    {
        [Serializable] class ReflectionRow
        {
            public string state,configuredMode,actualMode,format,color,depth;public int viewRenderer,reflectionRenderer,width,height,samples,descriptorSamples,magenta,callbacks;
            public bool stableTarget,newTarget,allowHDR,allowMSAA;
        }
        [Serializable] class ReflectionReport
        {
            public string unity,api,gpu,scope,failure;public int errors,boundChunks;public List<ReflectionRow> rows=new List<ReflectionRow>();
        }
        sealed class ReflectionObserver : ICameraGraphPass
        {
            public RenderPassEvent Stage=>RenderPassEvent.AfterRendering;
            public OceanPlanarReflection Owner;public bool Armed;public int Count;public RenderTextureDescriptor Descriptor;public string ActualMode;
            public void Record(RenderGraph graph,ContextContainer context)
            {
                var data=context.Get<UniversalCameraData>();if(!Armed||data.camera!=Owner.ReflectionCamera)return;
                Count++;Descriptor=data.cameraTargetDescriptor;
                var renderer=data.camera.GetUniversalAdditionalCameraData().scriptableRenderer;
                var property=renderer.GetType().GetProperty("renderingModeActual",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                CheckExample(property!=null,"Missing read-only actual renderer-mode observation");ActualMode=property.GetValue(renderer).ToString();
            }
        }
        ReflectionReport _reflectionReport;ReflectionObserver _reflectionObserver;
        void SaveReflectionModes()
        {
            if(_reflectionReport==null)return;_reflectionReport.errors=_errors;
            File.WriteAllText(Argument("-crestProbeOutput","ReflectionModes.json"),JsonUtility.ToJson(_reflectionReport,true));
        }
        int ReflectionMagenta(RenderTexture target,string image=null)
        {
            var active=RenderTexture.active;var resolved=RenderTexture.GetTemporary(target.width,target.height,0,RenderTextureFormat.ARGB32);
            var cpu=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            try
            {
                Graphics.Blit(target,resolved);RenderTexture.active=resolved;cpu.ReadPixels(new Rect(0,0,target.width,target.height),0,0);cpu.Apply();
                if(image!=null)File.WriteAllBytes(Path.Combine(Argument("-crestProbeImages","."),image+".png"),cpu.EncodeToPNG());
                return cpu.GetPixels32().Count(p=>p.r>180&&p.b>180&&p.g<80);
            }
            finally{RenderTexture.active=active;Destroy(cpu);RenderTexture.ReleaseTemporary(resolved);}
        }
        ReflectionRow CaptureReflection(OceanPlanarReflection reflection,UniversalRenderPipelineAsset pipeline,Camera view,string label,int viewIndex,int reflectionIndex,RenderTexture previous=null,bool save=false)
        {
            _reflectionObserver.Count=0;_reflectionObserver.Armed=true;
            try{reflection.RefreshReflection();}finally{_reflectionObserver.Armed=false;}
            var camera=reflection.ReflectionCamera;var target=camera.targetTexture;var d=_reflectionObserver.Descriptor;
            CheckExample(_reflectionObserver.Count==1,"Reflection camera must execute exactly once");
            CheckExample(view.GetUniversalAdditionalCameraData().scriptableRenderer==pipeline.GetRenderer(viewIndex)&&camera.GetUniversalAdditionalCameraData().scriptableRenderer==pipeline.GetRenderer(reflectionIndex),"Compiled reflection renderer selection");
            CheckExample(d.width==target.width&&d.height==target.height&&d.msaaSamples==target.antiAliasing,"Actual reflection descriptor mismatch");
            var row=new ReflectionRow{state=label,viewRenderer=viewIndex,reflectionRenderer=reflectionIndex,configuredMode=((UniversalRendererData)pipeline.rendererDataList[reflectionIndex]).renderingMode.ToString(),actualMode=_reflectionObserver.ActualMode,
                width=target.width,height=target.height,samples=target.antiAliasing,descriptorSamples=d.msaaSamples,format=target.format.ToString(),color=d.graphicsFormat.ToString(),depth=d.depthStencilFormat.ToString(),magenta=ReflectionMagenta(target,save?label:null),callbacks=_reflectionObserver.Count,
                newTarget=previous!=null&&target!=previous,allowHDR=camera.allowHDR,allowMSAA=camera.allowMSAA};
            _reflectionReport.rows.Add(row);SaveReflectionModes();CheckExample(row.magenta>30,"Known reflected geometry must be visible");return row;
        }
        IEnumerator ReflectionModesPlayer()
        {
            Directory.CreateDirectory(Argument("-crestProbeImages","."));
            _reflectionReport=new ReflectionReport{unity=Application.unityVersion,api=SystemInfo.graphicsDeviceType.ToString(),gpu=SystemInfo.graphicsDeviceName,
                scope="P11.3-C3 finite compiled renderer selection4 branches plus LDR/HDR-stencil/MSAA-resize/reuse/surface binding. Serialized owned three-renderer pipeline; actual URP descriptors/mode observed. Deferred may fall back internally for reflection cameras. Not all features/XR/performance/original-renderer reference."};
            yield return SceneManager.LoadSceneAsync(1);
            var pipeline=(UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            CheckExample(pipeline.rendererDataList.Length==3&&((UniversalRendererData)pipeline.rendererDataList[0]).renderingMode==RenderingMode.Forward&&((UniversalRendererData)pipeline.rendererDataList[1]).renderingMode==RenderingMode.ForwardPlus&&((UniversalRendererData)pipeline.rendererDataList[2]).renderingMode==RenderingMode.Deferred,"Missing serialized diagnostic renderer dependencies");
            var view=Camera.main;view.enabled=false;view.transform.position=new Vector3(0,5,-15);view.transform.LookAt(new Vector3(0,2,0));view.fieldOfView=60;view.aspect=16f/9;
            var reflection=view.GetComponent<OceanPlanarReflection>();CheckExample(reflection!=null,"Missing original reflection component");
            GameObject.Find("Reflection reference").GetComponent<Renderer>().enabled=false;
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.name="Renderer mode geometry reference";cube.transform.position=new Vector3(0,2,0);cube.transform.localScale=Vector3.one*2;
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.SetColor("_BaseColor",Color.magenta);cube.GetComponent<Renderer>().sharedMaterial=material;
            SceneSet(reflection,"_hdr",false);SceneSet(reflection,"_stencil",false);SceneSet(reflection,"_allowMSAA",false);SceneSet(reflection,"_textureSize",128);
            var viewData=view.GetUniversalAdditionalCameraData();viewData.SetRenderer(1);reflection.DisablePixelLights=false;SceneSet(reflection,"_forceForwardRenderingPath",true);
            for(int i=0;i<8;i++)yield return null;
            Time.timeScale=0;var ocean=OceanRenderer.Instance;var clock=new ExampleFixedTime();ocean.PushTimeProvider(clock);
            _reflectionObserver=new ReflectionObserver{Owner=reflection};CameraGraphPassRegistry.Register(_reflectionObserver);
            yield return ModeCapture(()=>
            {
                CaptureReflection(reflection,pipeline,view,"Forced-Forward",1,0);
                SceneSet(reflection,"_forceForwardRenderingPath",false);CaptureReflection(reflection,pipeline,view,"Inherited-ForwardPlus",1,1);
                viewData.SetRenderer(2);CaptureReflection(reflection,pipeline,view,"Inherited-Deferred",2,2,save:true);
                reflection.DisablePixelLights=true;CaptureReflection(reflection,pipeline,view,"Vertex-Forward",2,0);
                viewData.SetRenderer(0);var ldr=CaptureReflection(reflection,pipeline,view,"LDR-reuse",0,0);var first=reflection.ReflectionCamera.targetTexture;
                reflection.RefreshReflection();ldr.stableTarget=reflection.ReflectionCamera.targetTexture==first;
                CheckExample(first.width==128&&first.format==RenderTextureFormat.ARGB32&&first.antiAliasing==1&&ldr.stableTarget,"LDR128 target/reuse");
                SceneSet(reflection,"_hdr",true);SceneSet(reflection,"_stencil",true);var hdr=CaptureReflection(reflection,pipeline,view,"HDR-stencil",0,0,first);var hdrTarget=reflection.ReflectionCamera.targetTexture;
                CheckExample(hdr.newTarget&&hdrTarget.format==RenderTextureFormat.ARGBHalf&&hdrTarget.depth>=24,"HDR/stencil recreation");
                pipeline.msaaSampleCount=4;SceneSet(reflection,"_allowMSAA",true);SceneSet(reflection,"_textureSize",256);
                var msaa=CaptureReflection(reflection,pipeline,view,"MSAA-resized",0,0,hdrTarget,true);var resized=reflection.ReflectionCamera.targetTexture;
                int supported=SystemInfo.GetRenderTextureSupportedMSAASampleCount(new RenderTextureDescriptor(256,256,RenderTextureFormat.ARGBHalf,24){msaaSamples=4});
                reflection.RefreshReflection();msaa.stableTarget=reflection.ReflectionCamera.targetTexture==resized;
                CheckExample(supported>1&&resized.width==256&&resized.antiAliasing==supported&&msaa.newTarget&&msaa.stableTarget&&msaa.allowHDR&&msaa.allowMSAA,"Actual MSAA/resize/reuse control");
                var mainTarget=new RenderTexture(256,256,24,RenderTextureFormat.ARGBHalf);mainTarget.Create();
                try
                {
                    RenderPipeline.SubmitRenderRequest(view,new UniversalRenderPipeline.SingleCameraRequest{destination=mainTarget});
                    var block=new MaterialPropertyBlock();foreach(var chunk in ocean.Root.GetComponentsInChildren<OceanChunkRenderer>())
                    {
                        if(chunk.Rend==null)continue;chunk.Rend.GetPropertyBlock(block);var bound=block.GetTexture("_ReflectionTex");if(bound==null||bound==Texture2D.blackTexture)continue;
                        CheckExample(bound==resized,"Actual ocean draw retained an obsolete reflection target");_reflectionReport.boundChunks++;
                    }
                    CheckExample(_reflectionReport.boundChunks>0,"No actual ocean chunk binds resized reflection");
                }
                finally{if(RenderTexture.active==mainTarget)RenderTexture.active=null;mainTarget.Release();Destroy(mainTarget);}
                SaveReflectionModes();
            });
            CameraGraphPassRegistry.Unregister(_reflectionObserver);ocean.PopTimeProvider(clock);Destroy(cube);Destroy(material);
            SaveReflectionModes();Debug.Log("REFLECTION_MODES_COMPLETED errors="+_errors);Application.Quit(_errors==0?0:1);
        }
    }
}
#endif
