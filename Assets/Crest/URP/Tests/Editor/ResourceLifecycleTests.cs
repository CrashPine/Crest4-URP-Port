using System.Collections;
using System.IO;
using System.Text;
using Crest;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class ResourceLifecycleTests
{
    RenderTexture _capture;

    [UnityTest]
    public IEnumerator RepeatedUnderwaterSceneLoadsKeepTextureFootprintStable()
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Underwater.unity");
        yield return new EnterPlayMode();
        OceanRenderer.EnableGraphicsInBatchMode = true;
        _capture = new RenderTexture(640, 360, 24);
        _capture.Create();
        var report = new StringBuilder($"Unity={Application.unityVersion}; API={SystemInfo.graphicsDeviceType}\n");
        int baselineCount = 0;
        long baselineBytes = 0;
        for (int cycle = 0; cycle < 12; cycle++)
        {
            yield return SceneManager.LoadSceneAsync("Ocean_Underwater");
            var sample = new SampleHeightHelper();
            int ready = 0, attempts = 0, consecutive = 0;
            float started = Time.realtimeSinceStartup;
            // Simulated WaitForSeconds ticks are not a wall-clock GPU deadline
            // in an unthrottled Editor. Keep the original 60 attempts and also
            // require sustained recovery before measuring resources.
            float deadline = started + 10;
            while ((attempts < 60 || consecutive < 8) && Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForSeconds(.02f);
                sample.Init(Vector3.zero, 1, true);
                attempts++;
                bool valid = sample.Sample(out float height) && !float.IsNaN(height) && !float.IsInfinity(height);
                if (valid) ready++;
                consecutive = valid ? consecutive + 1 : 0;
            }
            float elapsed = Time.realtimeSinceStartup - started;
            File.WriteAllText("Evidence/ResourceLifecycle-QueryRecovery-" + SystemInfo.graphicsDeviceType + ".txt",
                $"Cycle={cycle}; Attempts={attempts}; Ready={ready}; Consecutive={consecutive}; WallSeconds={elapsed}\n");
            Assert.That(ready, Is.GreaterThan(0), "Queries must recover after scene reload.");
            Assert.That(attempts, Is.GreaterThanOrEqualTo(60));
            Assert.That(consecutive, Is.GreaterThanOrEqualTo(8), "Query recovery must persist across eight successive samples.");
            RenderPipeline.SubmitRenderRequest(Camera.main,
                new UniversalRenderPipeline.SingleCameraRequest { destination = _capture });
            var completion = AsyncGPUReadback.Request(_capture);
            while (!completion.done) yield return null;
            Assert.That(completion.hasError, Is.False);
            yield return null; // Flush deferred destruction without unloading unused assets.
            int count = 0;
            long bytes = 0;
            foreach (var texture in Resources.FindObjectsOfTypeAll<RenderTexture>())
            {
                if (!IsCrestTexture(texture.name)) continue;
                count++;
                bytes += Profiler.GetRuntimeMemorySizeLong(texture);
            }
            report.AppendLine($"Cycle={cycle}; Textures={count}; NativeTextureBytes={bytes}; Queries={ready}; Attempts={attempts}; Consecutive={consecutive}; WallSeconds={elapsed}");
            File.WriteAllText("Evidence/ResourceLifecycle-" + SystemInfo.graphicsDeviceType + ".txt", report.ToString());
            if (cycle < 3)
            {
                baselineCount = Mathf.Max(baselineCount, count);
                baselineBytes = System.Math.Max(baselineBytes, bytes);
            }
            else
            {
                Assert.That(count, Is.LessThanOrEqualTo(baselineCount), report.ToString());
                Assert.That(bytes, Is.LessThanOrEqualTo(baselineBytes), report.ToString());
            }
            Assert.That(count, Is.GreaterThan(0));
        }
    }

    static bool IsCrestTexture(string name)
    {
        return name.StartsWith("Crest") || name == "FFTCascades" || name.Contains("_oceanDepth_") ||
            name.StartsWith("AnimatedWaves") || name.StartsWith("DynamicWaves") ||
            name.StartsWith("Foam") || name.StartsWith("Flow") || name.StartsWith("Shadow") ||
            name.StartsWith("SeaFloorDepth") || name.StartsWith("ClipSurface") || name.StartsWith("Albedo");
    }

    [UnityTearDown]
    public IEnumerator RestoreState()
    {
        if (_capture != null) { _capture.Release(); Object.Destroy(_capture); }
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}


public class OceanGeometryLifetimeTests
{
    const System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
    OceanRenderer _editorOcean;
    readonly System.Collections.Generic.List<Material> _proxyMaterials = new System.Collections.Generic.List<Material>();
    static Mesh[] Meshes() => (Mesh[])typeof(OceanBuilder).GetField("s_Meshes", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
    static void Set(OceanRenderer ocean, string name, object value) => typeof(OceanRenderer).GetField(name, Private).SetValue(ocean, value);
    static void Call(OceanRenderer ocean, string name) => typeof(OceanRenderer).GetMethod(name, Private).Invoke(ocean, null);

    [UnityTest]
    public IEnumerator GeneratedMeshesAndTilesAreDestroyedAcrossDensityAndLodRebuilds()
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        OceanRenderer.EnableGraphicsInBatchMode = true;
        for (int i=0; i<12; i++) yield return null;
        var ocean = OceanRenderer.Instance;
        var report = new StringBuilder($"Unity={Application.unityVersion}; API={SystemInfo.graphicsDeviceType}\n");
        Mesh[] previous=null; GameObject oldRoot=null; GameObject[] oldTiles=null;
        int[] initialVertices=null;
        for (int state=0; state<3; state++)
        {
            if (state>0)
            {
                Set(ocean,"_geometryDownSampleFactor",state==1?4:2);
                Set(ocean,"_lodCount",state==1?4:7);
                for(int i=0;i<8;i++)yield return null;
                Assert.That(System.Array.TrueForAll(previous,m=>m==null),Is.True,"All prior patch meshes, including unused ones, must be destroyed");
                Assert.That(oldRoot==null,Is.True);
                Assert.That(System.Array.TrueForAll(oldTiles,t=>t==null),Is.True);
            }
            var meshes=(Mesh[])Meshes().Clone();
            Assert.That(meshes.Length,Is.EqualTo(10));
            Assert.That(System.Array.TrueForAll(meshes,m=>m!=null&&m.vertexCount>0),Is.True);
            var vertices=System.Array.ConvertAll(meshes,m=>m.vertexCount);
            if(state==0)initialVertices=vertices;
            if(state==1)for(int i=0;i<meshes.Length;i++)Assert.That(vertices[i],Is.LessThan(initialVertices[i]));
            if(state==2)CollectionAssert.AreEqual(initialVertices,vertices);
            var filters=ocean.Root.GetComponentsInChildren<MeshFilter>(true);
            Assert.That(filters.Length,Is.EqualTo(12*(state==1?4:7)+4));
            var used=new System.Collections.Generic.HashSet<Mesh>();
            foreach(var f in filters)
            {
                Assert.That(System.Array.IndexOf(meshes,f.sharedMesh),Is.EqualTo(-1),"Tiles own clones, not the template mesh");
                Assert.That(System.Array.Exists(meshes,m=>m.name==f.sharedMesh.name&&m.vertexCount==f.sharedMesh.vertexCount),Is.True);
                Assert.That(used.Add(f.sharedMesh),Is.True,"Each tile owns a distinct mesh clone");
            }
            int unused=meshes.Length;
            report.AppendLine($"State={state}; Meshes={meshes.Length}; UnassignedMeshes={unused}; Tiles={filters.Length}; Vertices={string.Join(",",vertices)}; PriorObjectsDestroyed=True");
            previous=System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Concat(meshes,used));oldRoot=ocean.Root.gameObject;oldTiles=System.Array.ConvertAll(filters,f=>f.gameObject);
        }
        Object.Destroy(ocean.gameObject);for(int i=0;i<4;i++)yield return null;
        bool meshesGone=System.Array.TrueForAll(previous,m=>m==null),tilesGone=System.Array.TrueForAll(oldTiles,t=>t==null);
        report.AppendLine($"FinalMeshesDestroyed={meshesGone}; FinalTilesDestroyed={tilesGone}; FinalRootDestroyed={oldRoot==null}");
        File.WriteAllText("Evidence/P10-Geometry-"+SystemInfo.graphicsDeviceType+".txt",report.ToString());
        Assert.That(meshesGone&&tilesGone&&oldRoot==null,Is.True);
    }

    [UnityTest]
    public IEnumerator EditorProxyOwnedMaterialAndPlaneAreReleasedOnCleanup()
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        OceanRenderer.EnableGraphicsInBatchMode=true;
        _editorOcean=System.Linq.Enumerable.Single(Object.FindObjectsByType<OceanRenderer>());
        if(OceanRenderer.Instance==null)Call(_editorOcean,"OnEnable");
        var report=new StringBuilder($"PlayMode={Application.isPlaying}\n");
        for(int cycle=0;cycle<2;cycle++)
        {
            if(cycle>0)Call(_editorOcean,"OnEnable");
            Set(_editorOcean,"_showOceanProxyPlane",true);Call(_editorOcean,"OnDrawGizmos");
            var plane=(GameObject)typeof(OceanRenderer).GetField("_proxyPlane",Private).GetValue(_editorOcean);
            Assert.That(plane,Is.Not.Null);
            var material=plane.GetComponent<Renderer>().sharedMaterial;_proxyMaterials.Add(material);
            Assert.That(material,Is.Not.Null);Assert.That(UnityEditor.AssetDatabase.Contains(material),Is.False);
            var sharedPlaneMesh=plane.GetComponent<MeshFilter>().sharedMesh;
            Call(_editorOcean,"OnDisable");yield return null;
            bool materialGone=material==null,planeGone=plane==null;
            report.AppendLine($"Cycle={cycle}; ProxyMaterialDestroyed={materialGone}; ProxyPlaneDestroyed={planeGone}; BorrowedPlaneMeshPreserved={sharedPlaneMesh!=null}");
            File.WriteAllText("Evidence/P10-Geometry-Proxy-"+SystemInfo.graphicsDeviceType+".txt",report.ToString());
            Assert.That(materialGone,Is.True,"The proxy's runtime-created material must not outlive ocean cleanup");
            Assert.That(planeGone,Is.True);Assert.That(sharedPlaneMesh,Is.Not.Null,"Built-in shared primitive mesh is borrowed");
        }
    }

    [UnityTearDown]
    public IEnumerator Restore()
    {
        if(_editorOcean!=null&&!Application.isPlaying)Call(_editorOcean,"OnDisable");
        foreach(var material in _proxyMaterials)if(material!=null)Object.DestroyImmediate(material);
        _proxyMaterials.Clear();_editorOcean=null;
        OceanRenderer.EnableGraphicsInBatchMode=false;
        if(Application.isPlaying)yield return new ExitPlayMode();
    }
}
