using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Runtime.InteropServices;
using Crest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class LodHeightSeamTests
{
    [StructLayout(LayoutKind.Sequential)] struct Input { public Matrix4x4 Matrix; public Vector4 VertexLod; }
    [StructLayout(LayoutKind.Sequential)] struct Output { public Vector4 Base, Displaced, Normal; }
    struct Owner { public int Tile, Lod; }
    readonly Dictionary<Mesh,int[]> _boundaries = new Dictionary<Mesh,int[]>();
    readonly List<Mesh> _meshes = new List<Mesh>();
    Material _inputMaterial, _surface, _originalMaterial;
    Shader _originalShader;
    SimSettingsSeaFloorDepth _depth;
    OceanWaveSpectrum _spectrum;
    ShapeGerstner _waves;
    MeshFilter _input;
    Camera _camera;
    RenderTexture _target;
    ComputeShader _probe;
    TimeProviderCustom _clock;
    float _scale;
    int _capture, _phases, _pairs, _vertices, _captures;
    float _maxGap, _maxNormalGap, _maxPlaneHeightError, _maxPlaneNormalError;
    string _report;
    static readonly FieldInfo Lod = typeof(OceanChunkRenderer).GetField("_lodIndex",BindingFlags.Instance|BindingFlags.NonPublic);
    static ComputeBuffer Buffer(OceanRenderer ocean,string name) => (ComputeBuffer)typeof(OceanRenderer).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ocean);

    [UnityTest]
    public IEnumerator ActualTileBoundaryVerticesWeldAcrossLocalSlopeSmoothEdgeAndChoppyWaves()
    {
        _scale=Time.timeScale; _capture=Time.captureFramerate;
        OceanRenderer.EnableGraphicsInBatchMode=true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        Time.timeScale=1; Time.captureFramerate=60;
        var ocean=OceanRenderer.Instance;
        _camera=Camera.main;
        _camera.transform.SetPositionAndRotation(new Vector3(.017f,14,-.033f),Quaternion.Euler(90,0,0));
        _camera.orthographic=true; _camera.aspect=1; _camera.orthographicSize=48;
        _camera.nearClipPlane=.1f; _camera.farClipPlane=2000; _camera.clearFlags=CameraClearFlags.SolidColor;
        _camera.backgroundColor=Color.magenta; _camera.cullingMask=1<<4; _camera.allowHDR=false; _camera.allowMSAA=false;
        _camera.GetUniversalAdditionalCameraData().renderPostProcessing=false; RenderSettings.fog=false;
        foreach(var shape in Object.FindObjectsByType<ShapeWaves>()) shape.enabled=false;
        _surface=new Material(ocean.OceanMaterial); _surface.DisableKeyword("_TRANSPARENCY_ON"); _surface.SetFloat("_Transparency",0);
        _surface.DisableKeyword("_APPLYNORMALMAPPING_ON"); _surface.SetFloat("_NormalsStrengthOverall",1);
        ocean.OceanMaterial=_surface;
        _depth=ScriptableObject.CreateInstance<SimSettingsSeaFloorDepth>(); _depth._allowVaryingWaterLevel=true; ocean._simSettingsSeaFloorDepth=_depth;
        var owner=new GameObject("Original geometry height input at actual LOD seams");
        _input=owner.AddComponent<MeshFilter>();
        _input.sharedMesh=Plane();
        _inputMaterial=new Material(Shader.Find("Crest/Inputs/Sea Floor Depth/Set Base Water Height Using Geometry"));
        owner.AddComponent<MeshRenderer>().sharedMaterial=_inputMaterial; owner.AddComponent<RegisterHeightInput>();
        new GameObject("Seam numerical query fence").AddComponent<BoatProbesQueryFence>();
        _probe=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Crest/URP/Tests/Editor/LodSeamProbe.compute"); Assert.That(_probe,Is.Not.Null);
        for(int i=0;i<8;i++) yield return null;
        _clock=ocean.gameObject.AddComponent<TimeProviderCustom>(); _clock._overrideTime=_clock._overrideDeltaTime=true; _clock._time=1; _clock._deltaTime=0;
        ocean.PushTimeProvider(_clock);
        _target=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32); _target.Create();
        _report=$"Evidence/LodHeightSeams-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report,"Editor-only compute probe calls unchanged OceanVertHelpers/OceanHelpersNew on boundary vertices extracted from ACTUAL generated tile meshes/local-to-world matrices, actual cascade/instance buffers and native wave/depth textures. Position/geometry-normal before tiled normal maps. Independent affine reference y=4+.01x-.015z, then C1 compact smooth height patch, then original choppy waves. Adjacent LOD0/1 and1/2 pairs matched within .001m undisplaced XZ; world gap<.003m, normal vector gap<.002. Plane height/normal errors<.002. Every selected boundary vertex finite; all four seam directions required. Shared-vertex conformance plus actual opaque surface captures, not a standalone/transparent/subpixel raster proof.\n");
        yield return Phase("Affine",new Vector3(.017f,14,-.033f),8,true,true);
        yield return Phase("AffineTranslated",new Vector3(12.017f,14,-8.033f),8,true,false);
        yield return Phase("AffineScale16",new Vector3(12.017f,28,-8.033f),16,true,false);
        _input.sharedMesh=Patch();
        yield return Phase("SmoothEdge",new Vector3(12.017f,28,-8.033f),16,false,true);
        yield return Phase("SmoothEdgeOpposite",new Vector3(-12.017f,28,8.033f),16,false,false);
        SetupWaves();
        yield return Phase("SmoothEdgeChoppy",new Vector3(.017f,14,-.033f),8,false,true);
        yield return Phase("SmoothEdgeChoppyScale16",new Vector3(12.017f,28,-8.033f),16,false,false);
        File.AppendAllText(_report,$"Summary; Phases={_phases}; Vertices={_vertices}; AdjacentLodPairs={_pairs}; MaxWorldGap={_maxGap:R}; MaxNormalGap={_maxNormalGap:R}; MaxAffineHeightError={_maxPlaneHeightError:R}; MaxAffineNormalError={_maxPlaneNormalError:R}; Captures={_captures}\n");
        Assert.That(_phases,Is.EqualTo(7)); Assert.That(_pairs,Is.GreaterThan(500));
    }

    Mesh Plane()
    {
        var mesh=new Mesh { name="Known affine height input" };
        var vertices=new[]{new Vector3(-512,0,-512),new Vector3(-512,0,512),new Vector3(512,0,-512),new Vector3(512,0,512)};
        for(int i=0;i<vertices.Length;i++) vertices[i].y=4+.01f*vertices[i].x-.015f*vertices[i].z;
        mesh.vertices=vertices; mesh.triangles=new[]{0,1,2,2,1,3}; mesh.RecalculateBounds(); _meshes.Add(mesh); return mesh;
    }
    Mesh Patch()
    {
        const int count=33; var vertices=new Vector3[count*count]; var triangles=new List<int>();
        for(int z=0;z<count;z++) for(int x=0;x<count;x++)
        {
            float wx=-40+2.5f*x,wz=-40+2.5f*z;
            float fx=Mathf.Max(0,1-wx*wx/1600),fz=Mathf.Max(0,1-wz*wz/1600);
            vertices[z*count+x]=new Vector3(wx,4*fx*fx*fz*fz,wz);
            if(x<count-1&&z<count-1) { int i=z*count+x; triangles.AddRange(new[]{i,i+count,i+1,i+1,i+count,i+count+1}); }
        }
        var mesh=new Mesh { name="C1 compact height patch with zero boundary" }; mesh.vertices=vertices; mesh.triangles=triangles.ToArray(); mesh.RecalculateBounds(); _meshes.Add(mesh); return mesh;
    }
    void SetupWaves()
    {
        _spectrum=ScriptableObject.CreateInstance<OceanWaveSpectrum>(); _spectrum._chop=1.6f; _spectrum._waveDirectionVariance=0;
        var powers=new SerializedObject(_spectrum);
        for(int i=0;i<OceanWaveSpectrum.NUM_OCTAVES;i++) { powers.FindProperty("_powerDisabled").GetArrayElementAtIndex(i).boolValue=i!=9; powers.FindProperty("_powerLog").GetArrayElementAtIndex(i).floatValue=-2; }
        powers.ApplyModifiedPropertiesWithoutUndo();
        var owner=new GameObject("Original choppy seam waves"); owner.SetActive(false); _waves=owner.AddComponent<ShapeGerstner>();
        _waves._spectrum=_spectrum; _waves._randomSeed=27; _waves._componentsPerOctave=2; _waves._resolution=128;
        _waves._overrideGlobalWindSpeed=true; _waves._windSpeed=60; _waves._overrideGlobalWindDirection=true; _waves._waveDirectionHeadingAngle=0;
        var fields=new SerializedObject(_waves); fields.FindProperty("_respectShallowWaterAttenuation").floatValue=0; fields.ApplyModifiedPropertiesWithoutUndo(); owner.SetActive(true);
    }
    IEnumerator Phase(string name,Vector3 camera,float scale,bool affine,bool capture)
    {
        _camera.transform.position=camera; _camera.orthographicSize=6*scale;
        int start=OceanRenderer.FrameCount; float deadline=Time.realtimeSinceStartup+10;
        while(OceanRenderer.FrameCount<start+8&&Time.realtimeSinceStartup<deadline) yield return null;
        var ocean=OceanRenderer.Instance; Assert.That(ocean.TimeProvider,Is.SameAs(_clock)); Assert.That(ocean.Scale,Is.EqualTo(scale));
        Check(name,affine);
        if(capture) Capture(name);
        _phases++;
    }
    int[] Boundary(Mesh mesh)
    {
        if(_boundaries.TryGetValue(mesh,out var cached)) return cached;
        var edges=new Dictionary<(int,int),int>(); var triangles=mesh.triangles;
        void Edge(int a,int b) { var key=a<b?(a,b):(b,a); edges.TryGetValue(key,out int n); edges[key]=n+1; }
        for(int i=0;i<triangles.Length;i+=3) { Edge(triangles[i],triangles[i+1]); Edge(triangles[i+1],triangles[i+2]); Edge(triangles[i+2],triangles[i]); }
        var boundary=new HashSet<int>(); foreach(var edge in edges) if(edge.Value==1) { boundary.Add(edge.Key.Item1); boundary.Add(edge.Key.Item2); }
        cached=new int[boundary.Count]; boundary.CopyTo(cached); _boundaries.Add(mesh,cached); return cached;
    }
    void Check(string phase,bool affine)
    {
        var ocean=OceanRenderer.Instance; var inputs=new List<Input>(); var owners=new List<Owner>(); int tile=0;
        foreach(var chunk in ocean.Root.GetComponentsInChildren<OceanChunkRenderer>())
        {
            int lod=(int)Lod.GetValue(chunk); if(lod<0||lod>2) continue;
            var mesh=chunk.GetComponent<MeshFilter>().sharedMesh; var vertices=mesh.vertices;
            foreach(int index in Boundary(mesh)) { inputs.Add(new Input{Matrix=chunk.transform.localToWorldMatrix,VertexLod=new Vector4(vertices[index].x,vertices[index].y,vertices[index].z,lod)}); owners.Add(new Owner{Tile=tile,Lod=lod}); }
            tile++;
        }
        using(var input=new ComputeBuffer(inputs.Count,Marshal.SizeOf<Input>())) using(var output=new ComputeBuffer(inputs.Count,Marshal.SizeOf<Output>()))
        {
            int kernel=_probe.FindKernel("Probe"); input.SetData(inputs); _probe.SetInt("_ProbeCount",inputs.Count);
            _probe.SetBuffer(kernel,"_ProbeInputs",input); _probe.SetBuffer(kernel,"_ProbeOutputs",output);
            _probe.SetBuffer(kernel,"_CrestCascadeData",Buffer(ocean,"_bufCascadeDataTgt")); _probe.SetBuffer(kernel,"_CrestPerCascadeInstanceData",Buffer(ocean,"_bufPerCascadeInstanceData"));
            _probe.SetTexture(kernel,"_LD_TexArray_AnimatedWaves",ocean._lodDataAnimWaves.DataTexture); _probe.SetTexture(kernel,"_LD_TexArray_SeaFloorDepth",ocean._lodDataSeaDepths.DataTexture);
            _probe.SetVector("_OceanCenterPosWorld",ocean.Root.position); _probe.SetVector("_ProbeCamera",_camera.transform.position);
            _probe.SetFloat("_CrestLodAlphaBlackPointFade",Shader.GetGlobalFloat("_CrestLodAlphaBlackPointFade")); _probe.SetFloat("_CrestLodAlphaBlackPointWhitePointFade",Shader.GetGlobalFloat("_CrestLodAlphaBlackPointWhitePointFade"));
            _probe.Dispatch(kernel,(inputs.Count+63)/64,1,1); var results=new Output[inputs.Count]; output.GetData(results);
            var cells=new Dictionary<(int,int),List<int>>(); int pairs=0; int[] bands=new int[2],sides=new int[4]; float gapMax=0,normalMax=0,planeMax=0,planeNormalMax=0,offsetPeak=0,waveXzPeak=0,waveYPeak=0; int planeChecks=0,insidePairs=0,outsidePairs=0,edgePairs=0;
            for(int i=0;i<results.Length;i++)
            {
                var r=results[i]; Vector3 position=r.Displaced,normal=r.Normal;
                Assert.That(float.IsFinite(position.x)&&float.IsFinite(position.y)&&float.IsFinite(position.z)&&float.IsFinite(normal.x)&&float.IsFinite(normal.y)&&float.IsFinite(normal.z),Is.True);
                Assert.That(normal.magnitude,Is.EqualTo(1).Within(.0001f));
                offsetPeak=Mathf.Max(offsetPeak,Mathf.Abs(r.Displaced.w));
                waveXzPeak=Mathf.Max(waveXzPeak,Vector2.Distance(new Vector2(r.Base.x,r.Base.z),new Vector2(position.x,position.z)));
                waveYPeak=Mathf.Max(waveYPeak,Mathf.Abs(position.y-r.Base.y-r.Displaced.w));
                if(!affine && Mathf.Max(Mathf.Abs(position.x),Mathf.Abs(position.z))>41) Assert.That(Mathf.Abs(r.Displaced.w),Is.LessThan(.002f),"Outside compact input, local offset clears to zero.");
                if(affine&&Mathf.Abs(r.Base.x)<128&&Mathf.Abs(r.Base.z)<128)
                {
                    float he=Mathf.Abs(position.y-(4+.01f*position.x-.015f*position.z)); float ne=Vector3.Distance(normal,new Vector3(-.01f,1,.015f).normalized);
                    planeMax=Mathf.Max(planeMax,he); planeNormalMax=Mathf.Max(planeNormalMax,ne); planeChecks++;
                    Assert.That(he,Is.LessThan(.002f)); Assert.That(ne,Is.LessThan(.002f));
                }
                int x=Mathf.FloorToInt(r.Base.x/.001f),z=Mathf.FloorToInt(r.Base.z/.001f);
                for(int dz=-1;dz<=1;dz++) for(int dx=-1;dx<=1;dx++) if(cells.TryGetValue((x+dx,z+dz),out var candidates)) foreach(int j in candidates)
                {
                    if(owners[i].Tile==owners[j].Tile||Math.Abs(owners[i].Lod-owners[j].Lod)!=1) continue;
                    var q=results[j]; if(Vector2.Distance(new Vector2(r.Base.x,r.Base.z),new Vector2(q.Base.x,q.Base.z))>=.001f) continue;
                    float gap=Vector3.Distance(position,(Vector3)q.Displaced),ng=Vector3.Distance(normal,(Vector3)q.Normal);
                    File.AppendAllText(_report,$"Pair={phase}; A={owners[i].Lod}; B={owners[j].Lod}; Base=({r.Base.x:R},{r.Base.z:R}); Gap={gap:R}; NormalGap={ng:R}\n");
                    Assert.That(gap,Is.LessThan(.003f)); Assert.That(ng,Is.LessThan(.002f));
                    gapMax=Mathf.Max(gapMax,gap); normalMax=Mathf.Max(normalMax,ng); pairs++; bands[Math.Min(owners[i].Lod,owners[j].Lod)]++;
                    float extent=Mathf.Max(Mathf.Abs(position.x),Mathf.Abs(position.z));
                    if(extent<38) insidePairs++; if(extent>41) outsidePairs++; if(Mathf.Abs(extent-40)<2.5f) edgePairs++;
                    float rx=r.Base.x-ocean.Root.position.x,rz=r.Base.z-ocean.Root.position.z; sides[Mathf.Abs(rx)>Mathf.Abs(rz)?(rx>0?0:1):(rz>0?2:3)]++;
                }
                if(!cells.TryGetValue((x,z),out var list)) cells[(x,z)]=list=new List<int>(); list.Add(i);
            }
            File.AppendAllText(_report,$"Phase={phase}; Tiles={tile}; Vertices={inputs.Count}; Pairs={pairs}; Bands={string.Join(",",bands)}; Sides={string.Join(",",sides)}; MaxGap={gapMax:R}; MaxNormalGap={normalMax:R}; AffineChecks={planeChecks}; MaxAffineHeightError={planeMax:R}; MaxAffineNormalError={planeNormalMax:R}; InsidePairs={insidePairs}; OutsidePairs={outsidePairs}; NearInputEdgePairs={edgePairs}; LocalOffsetPeak={offsetPeak:R}; WaveXzPeak={waveXzPeak:R}; WaveYPeak={waveYPeak:R}\n");
            Assert.That(pairs,Is.GreaterThan(80)); foreach(int n in bands) Assert.That(n,Is.GreaterThan(20)); foreach(int n in sides) Assert.That(n,Is.GreaterThan(10));
            if(affine) Assert.That(planeChecks,Is.GreaterThan(100));
            else
            {
                Assert.That(offsetPeak,Is.GreaterThan(.1f));
                if(ocean.Scale==16) { Assert.That(edgePairs,Is.GreaterThan(10)); Assert.That(insidePairs,Is.GreaterThan(10)); Assert.That(outsidePairs,Is.GreaterThan(10)); }
            }
            if(_waves!=null) { Assert.That(waveXzPeak,Is.GreaterThan(.01f)); Assert.That(waveYPeak,Is.GreaterThan(.03f)); }
            _pairs+=pairs; _vertices+=inputs.Count; _maxGap=Mathf.Max(_maxGap,gapMax); _maxNormalGap=Mathf.Max(_maxNormalGap,normalMax); _maxPlaneHeightError=Mathf.Max(_maxPlaneHeightError,planeMax); _maxPlaneNormalError=Mathf.Max(_maxPlaneNormalError,planeNormalMax);
        }
    }
    void Capture(string phase)
    {
        RenderPipeline.SubmitRenderRequest(_camera,new UniversalRenderPipeline.SingleCameraRequest{destination=_target});
        var previous=RenderTexture.active; var texture=new Texture2D(512,512,TextureFormat.RGBA32,false);
        try
        {
            RenderTexture.active=_target; texture.ReadPixels(new Rect(0,0,512,512),0,0); texture.Apply(); File.WriteAllBytes($"Evidence/LodHeightSeams-{phase}-{SystemInfo.graphicsDeviceType}.png",texture.EncodeToPNG());
            int holes=0; foreach(var pixel in texture.GetPixels32()) if(pixel.r>240&&pixel.b>240&&pixel.g<16) holes++;
            File.AppendAllText(_report,$"Capture={phase}; ClearPixels={holes}\n"); Assert.That(holes,Is.Zero); _captures++;
            CompareOriginal(phase,texture.GetPixels32());
        }
        finally { RenderTexture.active=previous; Object.DestroyImmediate(texture); }
    }
    void CompareOriginal(string phase,Color32[] expected)
    {
        // No yield or simulation update between the three renders: retain the exact P08 camera/LOD state.
        if(_originalShader==null) _originalShader=BuiltInReferenceTests.CreateVerifiedOriginalShader(_report);
        if(_originalMaterial==null) _originalMaterial=new Material(_surface){shader=_originalShader};
        _originalMaterial.CopyPropertiesFromMaterial(_surface);
        var pipeline=GraphicsSettings.defaultRenderPipeline; var quality=QualitySettings.renderPipeline;
        var ocean=OceanRenderer.Instance; int frame=OceanRenderer.FrameCount;
        string fields=NativeFields(ocean);
        Color32[] original=null,restored=null;
        try
        {
            GraphicsSettings.defaultRenderPipeline=null; QualitySettings.renderPipeline=null;
            Assert.That(GraphicsSettings.currentRenderPipeline,Is.Null);
            ocean.OceanMaterial=_originalMaterial;
            _camera.targetTexture=_target;
            try {_camera.Render();} finally {_camera.targetTexture=null;}
            original=ReadReference(phase+"-BuiltIn");
        }
        finally
        {
            GraphicsSettings.defaultRenderPipeline=pipeline; QualitySettings.renderPipeline=quality;
            ocean.OceanMaterial=_surface;
        }
        RenderPipeline.SubmitRenderRequest(_camera,new UniversalRenderPipeline.SingleCameraRequest{destination=_target});
        restored=ReadReference(phase+"-URP-restored");
        Assert.That(OceanRenderer.FrameCount,Is.EqualTo(frame));
        Assert.That(NativeFields(ocean),Is.EqualTo(fields),"Exact original P08 native wave/depth fields must remain frozen.");
        long total=0;int max=0,repeat=0;
        for(int i=0;i<expected.Length;i++)
        {
            int r=Math.Abs(expected[i].r-original[i].r),g=Math.Abs(expected[i].g-original[i].g),b=Math.Abs(expected[i].b-original[i].b);
            total+=r+g+b;max=Math.Max(max,Math.Max(r,Math.Max(g,b)));
            if(!expected[i].Equals(restored[i])) repeat++;
        }
        double mean=(double)total/(expected.Length*3);
        File.AppendAllText(_report,$"OriginalReference={phase}; Camera={_camera.transform.position}; OrthoSize={_camera.orthographicSize:R}; Scale={ocean.Scale:R}; ViewerAlpha={ocean.ViewerAltitudeLevelAlpha:R}; NativeFields={fields}; FrameCount={frame}; MaxChannelDifference={max}; MeanAbsoluteRgbByteDifference={mean:R}; RestoredPixelsChanged={repeat}\n");
        Assert.That(max,Is.LessThanOrEqualTo(2)); Assert.That(mean,Is.LessThanOrEqualTo(.05)); Assert.That(repeat,Is.Zero);
    }
    static string NativeFields(OceanRenderer ocean)
    {
        var wave=AsyncGPUReadback.Request(ocean._lodDataAnimWaves.DataTexture,0);wave.WaitForCompletion();
        var depth=AsyncGPUReadback.Request(ocean._lodDataSeaDepths.DataTexture,0);depth.WaitForCompletion();
        Assert.That(wave.hasError||depth.hasError,Is.False);
        using(var hash=System.Security.Cryptography.SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(wave.GetData<byte>().ToArray()))+"/"+BitConverter.ToString(hash.ComputeHash(depth.GetData<byte>().ToArray()));
    }
    Color32[] ReadReference(string phase)
    {
        var previous=RenderTexture.active;var cpu=new Texture2D(512,512,TextureFormat.RGBA32,false);
        try
        {
            RenderTexture.active=_target;cpu.ReadPixels(new Rect(0,0,512,512),0,0);cpu.Apply();
            File.WriteAllBytes($"Evidence/LodHeightSeams-{phase}-{SystemInfo.graphicsDeviceType}.png",cpu.EncodeToPNG());
            return cpu.GetPixels32();
        }
        finally {RenderTexture.active=previous;Object.DestroyImmediate(cpu);}
    }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale=_scale; Time.captureFramerate=_capture; OceanRenderer.EnableGraphicsInBatchMode=false;
        if(Application.isPlaying) yield return new ExitPlayMode(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        if(_target!=null) { _target.Release(); Object.DestroyImmediate(_target); }
        foreach(var mesh in _meshes) if(mesh!=null) Object.DestroyImmediate(mesh); _meshes.Clear(); _boundaries.Clear();
        if(_inputMaterial!=null) Object.DestroyImmediate(_inputMaterial); if(_surface!=null) Object.DestroyImmediate(_surface);
        if(_originalMaterial!=null) Object.DestroyImmediate(_originalMaterial); if(_originalShader!=null) Object.DestroyImmediate(_originalShader);
        if(_depth!=null) Object.DestroyImmediate(_depth); if(_spectrum!=null) Object.DestroyImmediate(_spectrum);
    }
}
