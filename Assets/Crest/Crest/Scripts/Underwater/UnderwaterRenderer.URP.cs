// Crest Ocean System. MIT; see LICENSE.
// Graph scheduling reuses the original mask, volume, artifact correction and effect algorithms.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Crest
{
    public partial class UnderwaterRenderer
    {
#if CREST_RESOURCE_PROBE
        // Diagnostic Player only: CPU graph registration/command recording, not GPU timestamps.
        public static long DiagnosticCompositeRecords, DiagnosticCompositeExecutions;
        public static long DiagnosticPreserveRecords, DiagnosticPreserveExecutions;
        public static int DiagnosticWidth, DiagnosticHeight, DiagnosticSamples, DiagnosticColorFormat;
#endif
        public static UnderwaterRenderer ForUrpCamera(Camera camera) => Get(camera);

        sealed class GraphTextures : ContextItem
        {
            public TextureHandle Mask, Depth, Front, Back, EmptyFaces;
            public override void Reset() { Mask = Depth = Front = Back = EmptyFaces = TextureHandle.nullHandle; }
        }

        sealed class MaskPassData
        {
            public UnderwaterRenderer Owner;
            public Camera Camera;
            public RenderTextureDescriptor Descriptor;
            public TextureHandle Mask, Depth, Front, Back, EmptyFaces;
        }

        sealed class EffectPassData
        {
            public UnderwaterRenderer Owner;
            public Camera Camera;
            public TextureHandle Source, Destination, SceneDepth, Stencil;
        }

        sealed class RestoreCameraTargetData
        {
            public TextureHandle Source;
        }

        MaterialPropertyBlock _urpEffectProperties;

        public void RecordUrpMask(RenderGraph graph, ContextContainer frameData)
        {
            if (!isActiveAndEnabled || !IsActive) return;
            var cameraData = frameData.Get<UniversalCameraData>();
            var camera = cameraData.camera;
            if (!Helpers.MaskIncludesLayer(camera.cullingMask, _layer)) return;
            SetupOceanMask();
            SetUpMaterials();
            SetUpFixMaskArtefactsShader();
            _cameraFrustumPlanes ??= new Plane[6];
            XRHelpers.Update(camera);
            var descriptor = cameraData.cameraTargetDescriptor;
            var textures = frameData.GetOrCreate<GraphTextures>();
            textures.Mask = UniversalRenderer.CreateRenderGraphTexture(graph, GetMaskColorRTD(descriptor), "Crest Underwater Mask", false);
            textures.Depth = UniversalRenderer.CreateRenderGraphTexture(graph, GetMaskDepthRTD(GetMaskColorRTD(descriptor)), "Crest Underwater Depth", false);
            if (_mode != Mode.FullScreen && _volumeGeometry != null)
            {
                var depthDescriptor = GetMaskDepthRTD(GetMaskColorRTD(descriptor));
                textures.Front = UniversalRenderer.CreateRenderGraphTexture(graph, depthDescriptor, "Crest Volume Front", false);
                textures.Back = UniversalRenderer.CreateRenderGraphTexture(graph, depthDescriptor, "Crest Volume Back", false);
            }
            else
            {
                // TextureXR's static defaults need separate initialization which URP does
                // not guarantee. Use a declared graph resource, with the camera's dimension.
                textures.EmptyFaces = descriptor.dimension == TextureDimension.Tex2DArray
                    ? graph.CreateTexture(new TextureDesc(1, 1) {
                        name = "Crest Empty Volume Faces", dimension = TextureDimension.Tex2DArray,
                        slices = descriptor.volumeDepth,
                        colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm,
                        clearBuffer = true, clearColor = Color.black })
                    : graph.defaultResources.blackTexture;
            }

            using var builder = graph.AddUnsafePass<MaskPassData>("Crest: Underwater mask and volumes", out var data);
            data.Owner = this; data.Camera = camera; data.Descriptor = descriptor;
            data.Mask = textures.Mask; data.Depth = textures.Depth;
            data.Front = textures.Front; data.Back = textures.Back;
            data.EmptyFaces = textures.EmptyFaces;
            builder.UseTexture(data.Mask, AccessFlags.ReadWrite);
            builder.UseTexture(data.Depth, AccessFlags.ReadWrite);
            if (data.Front.IsValid())
            {
                builder.UseTexture(data.Front, AccessFlags.ReadWrite);
                builder.UseTexture(data.Back, AccessFlags.ReadWrite);
                builder.SetGlobalTextureAfterPass(data.Front, ShaderIDs.s_CrestWaterVolumeFrontFaceTexture);
                builder.SetGlobalTextureAfterPass(data.Back, ShaderIDs.s_CrestWaterVolumeBackFaceTexture);
            }
            else
            {
                builder.UseTexture(data.EmptyFaces, AccessFlags.Read);
                // Reset stale volume bindings before opaque masked examples draw.
                builder.SetGlobalTextureAfterPass(data.EmptyFaces, ShaderIDs.s_CrestWaterVolumeFrontFaceTexture);
                builder.SetGlobalTextureAfterPass(data.EmptyFaces, ShaderIDs.s_CrestWaterVolumeBackFaceTexture);
            }
            builder.SetGlobalTextureAfterPass(data.Mask, ShaderIDs.s_CrestOceanMaskTexture);
            builder.SetGlobalTextureAfterPass(data.Depth, ShaderIDs.s_CrestOceanMaskDepthTexture);
            builder.AllowGlobalStateModification(true);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc((MaskPassData pass, UnsafeGraphContext context) =>
            {
                var owner = pass.Owner;
                var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                owner.UpdateOceanRendererStateForCamera();
                owner.SetUpVolume(owner._oceanMaskMaterial.material);
                if (pass.Front.IsValid())
                {
                    owner.PopulateVolumeFront(cmd, pass.Front, pass.Back);
                    owner.PopulateVolumeBack(cmd, pass.Front, pass.Back);
                    cmd.CopyTexture(owner._mode == Mode.Portal ? pass.Front : pass.Back, pass.Depth);
                    Helpers.Blit(cmd, pass.Depth, Helpers.UtilityMaterial, (int)Helpers.UtilityPass.ClearDepth);
                }
                owner.SetUpMask(cmd, pass.Mask, pass.Depth);
                PopulateOceanMask(cmd, pass.Camera, OceanRenderer.Instance.Tiles, owner._cameraFrustumPlanes,
                    owner._oceanMaskMaterial.material, owner._farPlaneMultiplier, owner._enableShaderAPI, owner._debug._disableOceanMask);
                var maskDescriptor = owner.GetMaskColorRTD(pass.Descriptor);
                owner.FixMaskArtefacts(cmd, maskDescriptor, pass.Mask);
            });
        }

        public void RecordUrpEffect(RenderGraph graph, ContextContainer frameData)
        {
            if (!isActiveAndEnabled || !IsActive) return;
            var masks = frameData.GetOrCreate<GraphTextures>();
            if (!masks.Mask.IsValid()) return;
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer) return;
            var cameraData = frameData.Get<UniversalCameraData>();
            SetupUnderwaterEffect();
            _urpEffectProperties ??= new MaterialPropertyBlock();
            var cameraColor = resources.activeColorTexture;
            var descriptor = graph.GetTextureDesc(cameraColor);
            // Subsequent stack cameras import URP's persistent target. MSAA also needs
            // its original sample count to remain compatible with later depth attachments.
            var preserveCameraTarget = !cameraData.resolveFinalTarget || descriptor.msaaSamples != MSAASamples.None;
#if CREST_RESOURCE_PROBE
            DiagnosticCompositeRecords++;
            DiagnosticWidth = descriptor.width; DiagnosticHeight = descriptor.height;
            DiagnosticSamples = (int)descriptor.msaaSamples; DiagnosticColorFormat = (int)descriptor.colorFormat;
#endif
            descriptor.name = "Crest Underwater Color";
            descriptor.clearBuffer = false;
            descriptor.depthBufferBits = DepthBits.None;
            descriptor.msaaSamples = MSAASamples.None;
            var destination = graph.CreateTexture(descriptor);
            var stencil = TextureHandle.nullHandle;
            if (UseStencilBufferOnEffect)
                stencil = UniversalRenderer.CreateRenderGraphTexture(graph,
                    GetMaskDepthRTD(GetMaskColorRTD(cameraData.cameraTargetDescriptor)), "Crest Effect Stencil", false);

            using (var builder = graph.AddUnsafePass<EffectPassData>("Crest: Underwater composite", out var data))
            {
                data.Owner = this; data.Camera = cameraData.camera;
                data.Source = cameraColor; data.Destination = destination;
                data.SceneDepth = resources.cameraDepthTexture; data.Stencil = stencil;
                builder.UseTexture(data.Source, AccessFlags.Read);
                builder.UseTexture(data.Destination, AccessFlags.Write);
                builder.UseTexture(data.SceneDepth, AccessFlags.Read);
                if (resources.mainShadowsTexture.IsValid()) builder.UseTexture(resources.mainShadowsTexture, AccessFlags.Read);
                builder.UseTexture(masks.Mask, AccessFlags.Read);
                builder.UseTexture(masks.Depth, AccessFlags.Read);
                if (masks.Front.IsValid()) { builder.UseTexture(masks.Front, AccessFlags.Read); builder.UseTexture(masks.Back, AccessFlags.Read); }
                if (stencil.IsValid()) builder.UseTexture(stencil, AccessFlags.ReadWrite);
                builder.UseAllGlobalTextures(true);
                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((EffectPassData pass, UnsafeGraphContext context) =>
                {
#if CREST_RESOURCE_PROBE
                    DiagnosticCompositeExecutions++;
#endif
                    var owner = pass.Owner;
                    var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    UpdatePostProcessMaterial(owner, owner._mode, pass.Camera, owner._underwaterEffectMaterial,
                        owner._sphericalHarmonicsData, owner._meniscus, owner._firstRender || owner._copyOceanMaterialParamsEachFrame,
                        owner._debug._viewOceanMask, owner._debug._viewStencil, owner._filterOceanData,
                        ref owner._currentOceanMaterial, owner._enableShaderAPI, owner._overlay);
                    // Preserve pixels where a volume clips the effect. Source and destination never alias.
                    Blitter.BlitCameraTexture(cmd, pass.Source, pass.Destination);
                    cmd.SetGlobalTexture("_CameraDepthTexture", pass.SceneDepth);
                    if (pass.Stencil.IsValid())
                    {
                        Helpers.Blit(cmd, pass.Stencil, Helpers.UtilityMaterial, (int)Helpers.UtilityPass.CopyDepth);
                        cmd.SetRenderTarget(pass.Destination, pass.Stencil);
                    }
                    else cmd.SetRenderTarget(pass.Destination);
                    owner._urpEffectProperties.Clear();
                    owner._urpEffectProperties.SetTexture(ShaderIDs.s_CrestCameraColorTexture, (Texture)pass.Source);
                    owner.ExecuteEffect(cmd, owner._underwaterEffectMaterial.material, owner._urpEffectProperties);
                    owner._firstRender = false;
                });
            }

            if (preserveCameraTarget)
            {
#if CREST_RESOURCE_PROBE
                DiagnosticPreserveRecords++;
#endif
                using var builder = graph.AddRasterRenderPass<RestoreCameraTargetData>("Crest: Preserve underwater camera target", out var data);
                data.Source = destination;
                builder.UseTexture(data.Source, AccessFlags.Read);
                builder.SetRenderAttachment(cameraColor, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);
#if CREST_RESOURCE_PROBE
                builder.SetRenderFunc((RestoreCameraTargetData pass, RasterGraphContext context) =>
                {
                    DiagnosticPreserveExecutions++;
                    Blitter.BlitTexture(context.cmd, pass.Source, new Vector4(1, 1, 0, 0), 0, false);
                });
#else
                builder.SetRenderFunc((RestoreCameraTargetData pass, RasterGraphContext context) =>
                    Blitter.BlitTexture(context.cmd, pass.Source, new Vector4(1, 1, 0, 0), 0, false));
#endif
            }
            else resources.cameraColor = destination;
        }
    }
}
