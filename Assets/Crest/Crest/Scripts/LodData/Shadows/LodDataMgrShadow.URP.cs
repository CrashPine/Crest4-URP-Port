// Crest Ocean System. MIT; see LICENSE.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Crest
{
    public partial class LodDataMgrShadow
    {
        readonly RTHandle[] _urpHandles = new RTHandle[4];
        sealed class ShadowPassData
        {
            public LodDataMgrShadow Owner;
            public Matrix4x4 View, Projection;
            public Rect Viewport;
        }

        TextureHandle Import(RenderGraph graph, int slot, RenderTexture texture)
        {
            if (_urpHandles[slot] == null || _urpHandles[slot].rt != texture)
            {
                _urpHandles[slot]?.Release();
                _urpHandles[slot] = RTHandles.Alloc(texture);
            }
            return graph.ImportTexture(_urpHandles[slot]);
        }

        void ReleaseUrpHandles()
        {
            for (int i = 0; i < _urpHandles.Length; i++)
            {
                _urpHandles[i]?.Release();
                _urpHandles[i] = null;
            }
        }

        public void RecordUrpShadows(RenderGraph graph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            var camera = cameraData.camera;
            if (!enabled || !s_processData || BufCopyShadowMap == null || camera != _ocean.ViewCamera ||
                !Helpers.MaskIncludesLayer(camera.cullingMask, _ocean.Layer)) return;
            var lights = frameData.Get<UniversalLightData>();
            // Crest's primary light must match the atlas provided by URP for this camera.
            if (lights.mainLightIndex < 0 || lights.visibleLights[lights.mainLightIndex].light != _mainLight) return;
            var resources = frameData.Get<UniversalResourceData>();
            if (!resources.mainShadowsTexture.IsValid()) return;
            using var builder = graph.AddUnsafePass<ShadowPassData>("Crest: Temporal shadow accumulation", out var data);
            data.Owner = this;
            data.View = cameraData.GetViewMatrix();
            data.Projection = cameraData.GetProjectionMatrix();
            data.Viewport = camera.pixelRect;
            builder.UseTexture(resources.mainShadowsTexture, AccessFlags.Read);
            builder.UseTexture(Import(graph, 0, _targets.Current), AccessFlags.ReadWrite);
            builder.UseTexture(Import(graph, 1, _targets.Previous(1)), AccessFlags.Read);
            if (_ocean._lodDataAnimWaves != null)
                builder.UseTexture(Import(graph, 2, _ocean._lodDataAnimWaves.DataTexture), AccessFlags.Read);
            if (_ocean._lodDataSeaDepths != null)
                builder.UseTexture(Import(graph, 3, _ocean._lodDataSeaDepths.DataTexture), AccessFlags.Read);
            builder.UseAllGlobalTextures(true);
            builder.AllowGlobalStateModification(true);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc((ShadowPassData pass, UnsafeGraphContext context) =>
            {
                var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                var camera = pass.Owner._ocean.ViewCamera;
                bool stereo = camera.stereoEnabled && XRHelpers.IsSinglePass;
                if (stereo) cmd.DisableShaderKeyword("STEREO_INSTANCING_ON");
                pass.Owner.BuildCommandBuffer(pass.Owner._ocean, cmd);
                // Registered LOD inputs install orthographic simulation matrices. URP does
                // not restore those after an unsafe custom pass as the Built-in light pass did.
                cmd.SetViewProjectionMatrices(pass.View, pass.Projection);
                cmd.SetViewport(pass.Viewport);
                if (stereo) cmd.EnableShaderKeyword("STEREO_INSTANCING_ON");
            });
        }
    }
}
