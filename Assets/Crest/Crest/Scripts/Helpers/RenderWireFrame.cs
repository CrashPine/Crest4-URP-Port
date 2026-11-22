// Crest Ocean System

// This file is subject to the MIT License as seen in the root of this folder structure (LICENSE)

using UnityEngine;
using UnityEngine.Rendering;

namespace Crest
{
    /// <summary>
    /// Triggers the scene render to happen in wireframe.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu(Crest.Internal.Constants.MENU_PREFIX_DEBUG + "Render Wire Frame")]
    public class RenderWireFrame : MonoBehaviour
    {
        public bool _enable;
        public CameraClearFlags _clearFlags = CameraClearFlags.SolidColor;
        public Color _backgroundColor = new Color(0.1921569f, 0.3019608f, 0.4745098f, 0f);
        public LayerMask _cullingMask = ~0;
        public static bool _wireFrame = false;

        Camera _cam;
        CameraClearFlags _oldClearFlags;
        Color _oldBackgroundColor;
        LayerMask _oldCullingMask;
        bool _ownsWireframe;
        bool _previousWireframe;

        public bool Active => _wireFrame || _enable;

        void OnEnable()
        {
            _cam = GetComponent<Camera>();
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            RestoreWireframe();
        }

        void Start()
        {
            _oldClearFlags = _cam.clearFlags;
            _oldBackgroundColor = _cam.backgroundColor;
            _oldCullingMask = _cam.cullingMask;
        }

        void Update()
        {
            _cam.clearFlags = Active ? _clearFlags : _oldClearFlags;
            _cam.backgroundColor = Active ? _backgroundColor : _oldBackgroundColor;
            _cam.cullingMask = Active ? _cullingMask : _oldCullingMask;
        }

        void OnPreRender()
        {
            if (GraphicsSettings.currentRenderPipeline == null) ApplyWireframe();
        }

        void OnPostRender()
        {
            if (GraphicsSettings.currentRenderPipeline == null) RestoreWireframe();
        }

        void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == _cam) ApplyWireframe();
        }

        void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == _cam) RestoreWireframe();
        }

        void ApplyWireframe()
        {
            if (!Active || _ownsWireframe) return;
            _previousWireframe = GL.wireframe;
            _ownsWireframe = true;
            GL.wireframe = true;
        }

        void RestoreWireframe()
        {
            if (!_ownsWireframe) return;
            GL.wireframe = _previousWireframe;
            _ownsWireframe = false;
        }
    }
}
