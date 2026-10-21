// Crest Ocean System. MIT; see LICENSE.
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Crest
{
    public partial class OceanPlanarReflection
    {
        static OceanPlanarReflection s_UrpRequestOwner;
        static Scene s_PersistentScene;
        static readonly ProfilerMarker s_DistanceCulling = new ProfilerMarker("Crest.Reflection.DistanceCulling");
        readonly List<GameObject> _sceneRoots = new List<GameObject>();
        readonly List<Renderer> _sceneRenderers = new List<Renderer>();
        readonly List<Terrain> _sceneTerrains = new List<Terrain>();
        readonly List<CanvasRenderer> _sceneCanvases = new List<CanvasRenderer>();
        readonly List<Renderer> _distanceHiddenRenderers = new List<Renderer>();
        readonly List<CanvasRenderer> _distanceHiddenCanvases = new List<CanvasRenderer>();
        readonly List<TerrainState> _distanceHiddenTerrains = new List<TerrainState>();
        readonly Vector3[] _canvasCorners = new Vector3[4];

        struct TerrainState
        {
            public Terrain Terrain;
            public bool Heightmap, Trees;
        }

        /// <summary>Per-camera lighting policy, valid only during a synchronous URP reflection request.</summary>
        public static bool UsesVertexLightingForUrpCamera(Camera camera)
            => s_UrpRequestOwner != null && s_UrpRequestOwner._camReflections == camera && s_UrpRequestOwner._disablePixelLights;

        void ConfigureUrpRenderer(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset pipeline,
            UnityEngine.Rendering.Universal.UniversalAdditionalCameraData reflectionData)
        {
            // Camera.renderingPath is ignored by SRP. URP chooses the path through
            // a renderer asset; clustered/deferred lighting cannot use vertex lights.
            if (_forceForwardRenderingPath || _disablePixelLights)
            {
                var renderers = pipeline.rendererDataList;
                for (int index = 0; index < renderers.Length; index++)
                {
                    if (renderers[index] is UnityEngine.Rendering.Universal.UniversalRendererData renderer &&
                        renderer.renderingMode == UnityEngine.Rendering.Universal.RenderingMode.Forward)
                    {
                        reflectionData.SetRenderer(index);
                        return;
                    }
                }
                throw new System.InvalidOperationException("Crest: planar force-forward/vertex-light reflection requires a Forward Universal Renderer in the active URP asset renderer list, with CrestBackgroundFeature enabled.");
            }

            var viewpointData = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(_camViewpoint);
            var viewpointRenderer = viewpointData.scriptableRenderer;
            if (pipeline.GetRenderer(-1) == viewpointRenderer)
            {
                reflectionData.SetRenderer(-1);
                return;
            }
            for (int index = 0; index < pipeline.rendererDataList.Length; index++)
            {
                if (pipeline.GetRenderer(index) != viewpointRenderer) continue;
                reflectionData.SetRenderer(index);
                return;
            }
            throw new System.InvalidOperationException("Crest: viewpoint renderer is not registered in the active URP asset.");
        }

        void BeginUrpDistanceCulling()
        {
            using var marker = s_DistanceCulling.Auto();
            // Unity's Camera layer-distance planes are derived from the oblique projection.
            // Their spherical switch is a Built-in-only API. Apply the same conservative
            // bounding-sphere distance policy for this synchronous request and restore it.
            for (int index = 0; index < SceneManager.sceneCount; index++)
                CullScene(SceneManager.GetSceneAt(index));
            if (Application.isPlaying)
            {
                if (!s_PersistentScene.IsValid())
                {
                    // SceneManager does not enumerate DontDestroyOnLoad. Discover its public
                    // Scene handle once; subsequent requests reuse lists and allocate no probe.
                    var probe = new GameObject("Crest reflection scene discovery") { hideFlags = HideFlags.HideAndDontSave };
                    DontDestroyOnLoad(probe);
                    s_PersistentScene = probe.scene;
                    Destroy(probe);
                }
                CullScene(s_PersistentScene);
            }
        }

        void CullScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            if (_sceneRoots.Capacity < scene.rootCount) _sceneRoots.Capacity = scene.rootCount;
            _sceneRoots.Clear();
            scene.GetRootGameObjects(_sceneRoots);
            foreach (var root in _sceneRoots)
            {
                if (!root.activeInHierarchy) continue;
                _sceneRenderers.Clear();
                root.GetComponentsInChildren(false, _sceneRenderers);
                foreach (var renderer in _sceneRenderers)
                {
                    if (!renderer.enabled || renderer.forceRenderingOff || !InReflectionLayer(renderer.gameObject)) continue;
                    if (!OutsideReflectionDistance(renderer.bounds)) continue;
                    _distanceHiddenRenderers.Add(renderer);
                    renderer.forceRenderingOff = true;
                }
                _sceneTerrains.Clear();
                root.GetComponentsInChildren(false, _sceneTerrains);
                foreach (var terrain in _sceneTerrains)
                {
                    if (!terrain.enabled || terrain.terrainData == null || !InReflectionLayer(terrain.gameObject)) continue;
                    // Do not record an already-hidden terrain again if hierarchy/scene
                    // enumeration exposes it twice during the synchronous request.
                    if (!terrain.drawHeightmap && !terrain.drawTreesAndFoliage) continue;
                    var local = terrain.terrainData.bounds;
                    var matrix = terrain.transform.localToWorldMatrix;
                    var x = matrix.MultiplyVector(Vector3.right * local.extents.x);
                    var y = matrix.MultiplyVector(Vector3.up * local.extents.y);
                    var z = matrix.MultiplyVector(Vector3.forward * local.extents.z);
                    var extent = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                        Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
                    if (!OutsideReflectionDistance(new Bounds(matrix.MultiplyPoint3x4(local.center), extent * 2))) continue;
                    _distanceHiddenTerrains.Add(new TerrainState { Terrain = terrain, Heightmap = terrain.drawHeightmap, Trees = terrain.drawTreesAndFoliage });
                    terrain.drawHeightmap = false;
                    terrain.drawTreesAndFoliage = false;
                }
                _sceneCanvases.Clear();
                root.GetComponentsInChildren(false, _sceneCanvases);
                foreach (var renderer in _sceneCanvases)
                {
                    if (renderer.cull || !InReflectionLayer(renderer.gameObject)) continue;
                    var canvas = renderer.GetComponentInParent<Canvas>();
                    if (canvas == null || canvas.rootCanvas.renderMode != RenderMode.WorldSpace) continue;
                    var rectangle = renderer.transform as RectTransform;
                    if (rectangle == null) continue;
                    rectangle.GetWorldCorners(_canvasCorners);
                    var bounds = new Bounds(_canvasCorners[0], Vector3.zero);
                    for (int index = 1; index < 4; index++) bounds.Encapsulate(_canvasCorners[index]);
                    if (!OutsideReflectionDistance(bounds)) continue;
                    _distanceHiddenCanvases.Add(renderer);
                    renderer.cull = true;
                }
            }
        }

        bool InReflectionLayer(GameObject owner) => (_reflectionLayers.value & (1 << owner.layer)) != 0;

        bool OutsideReflectionDistance(Bounds bounds)
        {
            float distance = _farClipPlane > 0 ? _farClipPlane : _camViewpoint.farClipPlane;
            float radius = distance + bounds.extents.magnitude;
            return (bounds.center - _camReflections.transform.position).sqrMagnitude > radius * radius;
        }

        void EndUrpDistanceCulling()
        {
            foreach (var renderer in _distanceHiddenRenderers) if (renderer != null) renderer.forceRenderingOff = false;
            foreach (var renderer in _distanceHiddenCanvases) if (renderer != null) renderer.cull = false;
            foreach (var state in _distanceHiddenTerrains)
            {
                if (state.Terrain == null) continue;
                state.Terrain.drawHeightmap = state.Heightmap;
                state.Terrain.drawTreesAndFoliage = state.Trees;
            }
            _distanceHiddenRenderers.Clear();
            _distanceHiddenCanvases.Clear();
            _distanceHiddenTerrains.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetUrpReflectionState()
        {
            s_UrpRequestOwner = null;
            s_PersistentScene = default;
        }
    }
}
