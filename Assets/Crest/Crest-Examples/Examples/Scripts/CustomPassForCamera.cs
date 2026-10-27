// Crest Ocean System

// This file is subject to the MIT License as seen in the root of this folder structure (LICENSE)

namespace Crest.Examples
{
    using System.Collections.Generic;
    using System.Linq;
    using Crest.URP;
    using UnityEngine.Rendering.RenderGraphModule;
    using UnityEngine.Rendering.Universal;
    using UnityEngine;
    using UnityEngine.Events;
    using UnityEngine.Rendering;

    [ExecuteDuringEditMode]
    public abstract class CustomPassForCameraBase : CustomMonoBehaviour, ICameraGraphPass
    {
        // Use int to support other RPs. We could use a custom enum to map to each RP in the future.
        protected abstract int Event { get; }

        int _currentEvent;
        CommandBuffer _buffer;
        List<Camera> _cameras = new List<Camera>();

        public virtual RenderPassEvent Stage => CameraGraphPassRegistry.Map((CameraEvent)Event);
        protected virtual IEnumerable<ICameraGraphResources> Resources => System.Array.Empty<ICameraGraphResources>();
        sealed class GraphData
        {
            public CustomPassForCameraBase Owner;public Camera Camera;public RenderTextureDescriptor Descriptor;
            public Matrix4x4 View,Projection;public Rect Viewport;
        }
        public void Record(RenderGraph graph,ContextContainer frameData)
        {
            if (!isActiveAndEnabled) return;
            var cameraData=frameData.Get<UniversalCameraData>();
            var camera=cameraData.camera;
            if (!_cameras.Contains(camera)) _cameras.Add(camera);
            using var builder=graph.AddUnsafePass<GraphData>("Crest example: "+(CameraEvent)Event,out var data);
            data.Owner=this;data.Camera=camera;data.Descriptor=cameraData.cameraTargetDescriptor;
            data.View=cameraData.GetViewMatrix();data.Projection=cameraData.GetProjectionMatrix();data.Viewport=camera.pixelRect;
            foreach(var resource in Resources.Distinct())resource.Declare(graph,builder,data.Descriptor);
            builder.UseAllGlobalTextures(true);builder.AllowGlobalStateModification(true);builder.AllowPassCulling(false);
            builder.SetRenderFunc((GraphData pass,UnsafeGraphContext context)=>
            {
                var cmd=CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                if (pass.Owner==null) return;
                // This callback runs before URP sets up the opaque camera state.
                cmd.SetViewProjectionMatrices(pass.View,pass.Projection);cmd.SetViewport(new Rect(0,0,pass.Descriptor.width,pass.Descriptor.height));
                bool primary=Camera.main==pass.Camera||OceanRenderer.Instance?.ViewCameraExcludingSceneCamera==pass.Camera;
                if (primary||pass.Camera.cameraType==CameraType.SceneView||pass.Camera.name.StartsWithNoAlloc("Preview"))
                    pass.Owner.Execute(cmd,pass.Camera,pass.Descriptor);
                else pass.Owner.Clear(cmd,pass.Camera);
                // Original callbacks can draw offscreen meshes/install matrices. Raster attachments are rebound by RG.
                cmd.SetViewProjectionMatrices(pass.View,pass.Projection);cmd.SetViewport(pass.Viewport);
            });
        }
        void OnEnable()
        {
            if (_buffer == null)
            {
                _buffer = new CommandBuffer()
                {
                    name = "Execute Command Buffer",
                };
            }

            _currentEvent = Event;
            CameraGraphPassRegistry.Register(this);

            Camera.onPreRender -= OnBeforeRender;
            Camera.onPreRender += OnBeforeRender;
        }

        void OnDisable()
        {
            CameraGraphPassRegistry.Unregister(this);
            Camera.onPreRender -= OnBeforeRender;
            CleanCameras();
        }

        void OnDestroy() { _buffer?.Release();_buffer=null; }

        void CleanCameras()
        {
            foreach (var camera in _cameras)
            {
                // This can happen on recompile. Thankfully, command buffers will be removed for us.
                if (camera == null)
                {
                    continue;
                }

                if (GraphicsSettings.currentRenderPipeline == null)
                foreach (CameraEvent @event in System.Enum.GetValues(typeof(CameraEvent)))
                {
                    camera.RemoveCommandBuffer(@event, _buffer);
                }

                Clear(_buffer, camera);
                Graphics.ExecuteCommandBuffer(_buffer);
            }

            _cameras.Clear();
        }

        void OnBeforeRender(Camera camera)
        {
            if (GraphicsSettings.currentRenderPipeline != null) return;
            if (!_cameras.Contains(camera))
            {
                if (_currentEvent != Event)
                {
                    CleanCameras();
                    _currentEvent = Event;
                }

                _cameras.Add(camera);
                camera.AddCommandBuffer((CameraEvent)Event, _buffer);
            }

            // Buffer will be shared by multiple cameras and multiple Execute calls. Clear once for each camera.
            _buffer.Clear();

            // Only execute for main camera and editor only cameras.
            if (Camera.main != camera && camera.cameraType != CameraType.SceneView && !camera.name.StartsWithNoAlloc("Preview"))
            {
                Clear(_buffer, camera);
                return;
            }

            Execute(_buffer, camera, XRHelpers.GetRenderTextureDescriptor(camera));
        }

        protected abstract void Execute(CommandBuffer buffer, Camera camera, RenderTextureDescriptor descriptor);
        protected abstract void Clear(CommandBuffer buffer, Camera camera);
    }

    public class CustomPassForCamera : CustomPassForCameraBase
    {
        // TODO: We need a separate event for deferred as it is very different from forward. But having an event per
        // camera complicates things so will skip for now. Will probably need a dictionary.
        [SerializeField]
        CameraEvent _event;

        [SerializeField]
        UnityEvent<CommandBuffer, Camera, RenderTextureDescriptor> _onExecute = new UnityEvent<CommandBuffer, Camera, RenderTextureDescriptor>();

        [SerializeField]
        UnityEvent<CommandBuffer, Camera> _onClear = new UnityEvent<CommandBuffer, Camera>();

        protected override IEnumerable<ICameraGraphResources> Resources
        {
            get
            {
                for(int i=0;i<_onExecute.GetPersistentEventCount();i++)
                    if(_onExecute.GetPersistentTarget(i) is ICameraGraphResources resource)yield return resource;
            }
        }
        public override RenderPassEvent Stage
        {
            get
            {
                // The fill mask must also be ready for the DepthOnly/DepthNormals prepasses.
                for(int i=0;i<_onExecute.GetPersistentEventCount();i++)
                    if(_onExecute.GetPersistentTarget(i) is MaskFill && _onExecute.GetPersistentMethodName(i)==nameof(MaskFill.ExecuteFillMask))
                        return RenderPassEvent.BeforeRenderingPrePasses;
                return base.Stage;
            }
        }

        protected override int Event
        {
            get
            {
                return (int)_event;
            }
        }

        protected override void Execute(CommandBuffer buffer, Camera camera, RenderTextureDescriptor descriptor)
        {
            _onExecute.Invoke(buffer, camera, descriptor);
        }

        protected override void Clear(CommandBuffer buffer, Camera camera)
        {
            _onClear.Invoke(buffer, camera);
        }
    }
}
