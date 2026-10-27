// Crest 4 URP port. MIT; see LICENSE.Crest.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Crest.URP
{
    // Optional example/integration passes register without making the ocean depend on example/gameplay assemblies.
    public interface ICameraGraphPass
    {
        RenderPassEvent Stage { get; }
        void Record(RenderGraph graph, ContextContainer frameData);
    }
    public interface ICameraGraphResources
    {
        void Declare(RenderGraph graph,IUnsafeRenderGraphBuilder builder,RenderTextureDescriptor descriptor);
    }
    public static class CameraGraphPassRegistry
    {
        static readonly List<ICameraGraphPass> s_Passes=new List<ICameraGraphPass>();
        public static void Register(ICameraGraphPass pass) {if(!s_Passes.Contains(pass))s_Passes.Add(pass);}
        public static void Unregister(ICameraGraphPass pass)=>s_Passes.Remove(pass);
        public static RenderPassEvent[] Stages=>s_Passes.Select(x=>x.Stage).Distinct().OrderBy(x=>(int)x).ToArray();
        public static void Record(RenderPassEvent stage,RenderGraph graph,ContextContainer frameData)
        {
            foreach(var pass in s_Passes.ToArray())if(pass.Stage==stage)pass.Record(graph,frameData);
        }
        public static RenderPassEvent Map(CameraEvent stage)
        {
            switch(stage)
            {
                case CameraEvent.BeforeDepthTexture:case CameraEvent.BeforeDepthNormalsTexture:return RenderPassEvent.BeforeRenderingPrePasses;
                case CameraEvent.AfterDepthTexture:case CameraEvent.AfterDepthNormalsTexture:return RenderPassEvent.AfterRenderingPrePasses;
                case CameraEvent.BeforeGBuffer:return RenderPassEvent.BeforeRenderingGbuffer;
                case CameraEvent.AfterGBuffer:return RenderPassEvent.AfterRenderingGbuffer;
                case CameraEvent.BeforeLighting:case CameraEvent.BeforeReflections:return RenderPassEvent.BeforeRenderingDeferredLights;
                case CameraEvent.AfterLighting:case CameraEvent.AfterReflections:return RenderPassEvent.AfterRenderingDeferredLights;
                case CameraEvent.BeforeForwardOpaque:return RenderPassEvent.BeforeRenderingOpaques;
                case CameraEvent.AfterForwardOpaque:case CameraEvent.BeforeImageEffectsOpaque:return RenderPassEvent.AfterRenderingOpaques;
                case CameraEvent.AfterImageEffectsOpaque:case CameraEvent.BeforeSkybox:return RenderPassEvent.BeforeRenderingSkybox;
                case CameraEvent.AfterSkybox:return RenderPassEvent.AfterRenderingSkybox;
                case CameraEvent.BeforeForwardAlpha:return RenderPassEvent.BeforeRenderingTransparents;
                case CameraEvent.AfterForwardAlpha:return RenderPassEvent.AfterRenderingTransparents;
                case CameraEvent.BeforeFinalPass:case CameraEvent.BeforeImageEffects:case CameraEvent.BeforeHaloAndLensFlares:return RenderPassEvent.BeforeRenderingPostProcessing;
                case CameraEvent.AfterFinalPass:case CameraEvent.AfterImageEffects:case CameraEvent.AfterHaloAndLensFlares:return RenderPassEvent.AfterRenderingPostProcessing;
                default:return RenderPassEvent.AfterRendering;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()=>s_Passes.Clear();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void RestoreLiveComponents()
        {
            // ExecuteAlways examples may already be alive when entering Play with scene reload disabled.
            foreach(var component in Object.FindObjectsByType<MonoBehaviour>())
                if(component.isActiveAndEnabled && component is ICameraGraphPass pass)Register(pass);
        }
    }
}
