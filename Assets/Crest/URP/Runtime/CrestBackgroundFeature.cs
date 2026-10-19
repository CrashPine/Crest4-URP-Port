// MIT. Render Graph replacement for Crest's named GrabPass.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Crest.URP
{
    public sealed class CrestBackgroundFeature : ScriptableRendererFeature
    {
        BackgroundPass _pass;
        ShadowPass _shadows;
        UnderwaterPass _mask, _effectBefore, _effectAfter;

        public override void Create()
        {
            _shadows = new ShadowPass { renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.BeforeRenderingTransparents - 3) };
            _pass = new BackgroundPass { renderPassEvent = RenderPassEvent.BeforeRenderingTransparents };
            _mask = new UnderwaterPass(true, true) { renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.BeforeRenderingTransparents - 2) };
            _effectBefore = new UnderwaterPass(false, true) { renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.BeforeRenderingTransparents - 1) };
            _effectAfter = new UnderwaterPass(false, false) { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview) return;
            var target = renderingData.cameraData.camera.targetTexture;
            if (target != null && target.graphicsFormat == UnityEngine.Experimental.Rendering.GraphicsFormat.None) return;
            if (OceanRenderer.Instance?._lodDataShadow != null) renderer.EnqueuePass(_shadows);
            var underwater = UnderwaterRenderer.ForUrpCamera(renderingData.cameraData.camera);
            if (underwater != null && underwater.isActiveAndEnabled)
            {
                renderer.EnqueuePass(_mask);
                renderer.EnqueuePass(underwater.EnableShaderAPI ? _effectBefore : _effectAfter);
            }
            renderer.EnqueuePass(_pass);
        }

        sealed class ShadowPass : ScriptableRenderPass
        {
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
                => OceanRenderer.Instance?._lodDataShadow?.RecordUrpShadows(graph, frameData);
        }

        sealed class UnderwaterPass : ScriptableRenderPass
        {
            readonly bool _mask, _before;
            public UnderwaterPass(bool mask, bool before)
            {
                _mask = mask; _before = before;
                ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
            }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var owner = UnderwaterRenderer.ForUrpCamera(frameData.Get<UniversalCameraData>().camera);
                if (owner == null) return;
                if (_mask) owner.RecordUrpMask(graph, frameData);
                else if (owner.EnableShaderAPI == _before) owner.RecordUrpEffect(graph, frameData);
            }
        }

        sealed class BackgroundPass : ScriptableRenderPass
        {
            static readonly int BackgroundId = Shader.PropertyToID("_BackgroundTexture");
            sealed class PassData { public TextureHandle Source; }

            public BackgroundPass()
            {
                // Forces an intermediate camera color target and requests sampled scene depth.
                ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer)
                {
                    Debug.LogError("Crest requires an intermediate color target for refraction.");
                    return;
                }

                var descriptor = graph.GetTextureDesc(resources.activeColorTexture);
                descriptor.name = "Crest Background";
                descriptor.clearBuffer = false;
                descriptor.depthBufferBits = DepthBits.None;
                descriptor.msaaSamples = MSAASamples.None;
                var destination = graph.CreateTexture(descriptor);

                using var builder = graph.AddRasterRenderPass<PassData>("Crest: Copy background", out var data);
                data.Source = resources.activeColorTexture;
                builder.UseTexture(data.Source, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.SetGlobalTextureAfterPass(destination, BackgroundId);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((PassData pass, RasterGraphContext context) =>
                    Blitter.BlitTexture(context.cmd, pass.Source, new Vector4(1, 1, 0, 0), 0, false));
            }
        }
    }
}
