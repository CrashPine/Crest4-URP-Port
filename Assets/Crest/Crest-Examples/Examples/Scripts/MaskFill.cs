// Crest Ocean System

// This file is subject to the MIT License as seen in the root of this folder structure (LICENSE)

// TODO: This has some lag for deferred because the chosen event happens after opaque.

namespace Crest.Examples
{
    using System.Collections.Generic;
    using Crest.URP;
    using UnityEngine.Rendering.RenderGraphModule;
    using UnityEngine;
    using UnityEngine.Rendering;

    [ExecuteDuringEditMode]
    public class MaskFill : CustomMonoBehaviour, ICameraGraphResources
    {
        [SerializeField]
        [Tooltip("Masked meshes that need filling. Can provide non masked meshes to build a fill mask for open meshes as it uses the back faces.")]
        List<MeshFilter> _meshes = new List<MeshFilter>();

        static class ShaderIDs
        {
            public static readonly int s_FillTexture = Shader.PropertyToID("_FillTexture");
        }

        RTHandle _urpHandle,_urpDepthHandle;
        RenderTexture _urpDepth;RenderTargetIdentifier _urpDepthTarget;
        public void Declare(RenderGraph graph,IUnsafeRenderGraphBuilder builder,RenderTextureDescriptor descriptor)
        {
            descriptor.colorFormat=RenderTextureFormat.RFloat;descriptor.depthBufferBits=0;descriptor.msaaSamples=1;descriptor.enableRandomWrite=false;
            if(_urpDepth==null||!_urpDepth.IsCreated()||_texture.descriptor.width!=descriptor.width||_texture.descriptor.height!=descriptor.height||!_texture.IsCreated()||_texture.descriptor.dimension!=descriptor.dimension||_texture.volumeDepth!=descriptor.volumeDepth||_texture.useDynamicScale!=descriptor.useDynamicScale)
            {
                _urpHandle?.Release();_urpHandle=null;_urpDepthHandle?.Release();_urpDepthHandle=null;
                _texture.Release();_texture.descriptor=descriptor;_texture.Create();
                var depth=descriptor;depth.colorFormat=RenderTextureFormat.Depth;depth.depthBufferBits=24;
                if(_urpDepth!=null)_urpDepth.Release();Helpers.SafeCreateRenderTexture(ref _urpDepth,depth);_urpDepth.Create();
                Helpers.RenderTargetIdentifierXR(ref _urpDepth,ref _urpDepthTarget);
            }
            _urpHandle ??= RTHandles.Alloc(_texture);
            _urpDepthHandle ??= RTHandles.Alloc(_urpDepth);builder.UseTexture(graph.ImportTexture(_urpDepthHandle),AccessFlags.ReadWrite);
            var handle=graph.ImportTexture(_urpHandle);builder.UseTexture(handle,AccessFlags.ReadWrite);
            builder.SetGlobalTextureAfterPass(handle,ShaderIDs.s_FillTexture);
        }
        Material _material;
        RenderTexture _texture;
        RenderTargetIdentifier _target;

        void OnEnable()
        {
            if (_material == null)
            {
                _material = new Material(Shader.Find("Hidden/Crest/Examples/Mask Fill"));
            }

            Helpers.CreateRenderTargetTextureReference(ref _texture, ref _target);
            _texture.name = "_FillTexture";
        }

        void OnDisable()
        {
            _urpHandle?.Release();_urpHandle=null;_urpDepthHandle?.Release();_urpDepthHandle=null;
            if(_urpDepth!=null)_urpDepth.Release();
            if (_texture != null)
            {
                _texture.Release();
            }
        }

        void OnDestroy()
        {
            _urpHandle?.Release();_urpHandle=null;
            _urpDepthHandle?.Release();_urpDepthHandle=null;Helpers.Destroy(_urpDepth);_urpDepth=null;
            Helpers.Destroy(_texture);Helpers.Destroy(_material);_texture=null;_material=null;
        }
        public void ExecuteFillMask(CommandBuffer buffer, Camera camera, RenderTextureDescriptor descriptor)
        {
            if (_meshes.Count == 0 && GraphicsSettings.currentRenderPipeline == null) return;
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                descriptor.colorFormat=RenderTextureFormat.RFloat;descriptor.depthBufferBits=0;descriptor.msaaSamples=1;descriptor.enableRandomWrite=false;
            }

            if (Helpers.RenderTargetTextureNeedsUpdating(_texture, descriptor))
            {
                // RFloat is supported much better than RHalf.
                descriptor.colorFormat = RenderTextureFormat.RFloat;
                descriptor.depthBufferBits = 24;
                _texture.Release();
                _texture.descriptor = descriptor;
            }

            if(GraphicsSettings.currentRenderPipeline!=null&&_urpDepth!=null)buffer.SetRenderTarget(_target,_urpDepthTarget);
            else Helpers.SetRenderTarget(buffer, _target);
            buffer.ClearRenderTarget(true, true, Color.black);
            buffer.SetGlobalTexture(ShaderIDs.s_FillTexture, _target);

            foreach (var meshFilter in _meshes)
            {
                if (meshFilter == null)
                {
                    continue;
                }

                buffer.DrawMesh(meshFilter.sharedMesh, meshFilter.transform.localToWorldMatrix, _material, 0, 0, null);
            }
        }

        public void EnableShadowPassKeyword(CommandBuffer buffer, Camera camera, RenderTextureDescriptor descriptor)
        {
            // Shader Caster pass is used for both depth and shadows, but there is no way to determine which one.
            buffer.EnableShaderKeyword("_SHADOW_PASS");
        }

        public void DisableShadowPassKeyword(CommandBuffer buffer, Camera camera, RenderTextureDescriptor descriptor)
        {
            buffer.DisableShaderKeyword("_SHADOW_PASS");
        }

        public void Clear(CommandBuffer buffer, Camera camera)
        {
            if (_texture != null && _texture.IsCreated())
            {
                if(GraphicsSettings.currentRenderPipeline!=null&&_urpDepth!=null)buffer.SetRenderTarget(_target,_urpDepthTarget);
            else Helpers.SetRenderTarget(buffer, _target);
                buffer.ClearRenderTarget(true, true, Color.black);
            }

            buffer.DisableShaderKeyword("_SHADOW_PASS");
        }
    }
}

