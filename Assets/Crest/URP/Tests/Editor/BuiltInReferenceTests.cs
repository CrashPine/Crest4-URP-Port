// Crest 4 URP port. MIT; see LICENSE.Crest.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Crest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// Original shader is compiled in memory from the read-only MIT baseline. No source assets are rewritten.
public class BuiltInReferenceTests
{
    RenderPipelineAsset _default, _quality;
    Material _surface, _reference, _heightMaterial;
    Shader _shader;
    Mesh _heightMesh;
    RenderTexture _target;
    Camera _camera;
    OceanRenderer _ocean;
    TimeProviderCustom _clock;
    SimSettingsSeaFloorDepth _depth;
    SimSettingsFoam _foamSettings;
    SimSettingsAnimatedWaves _animated;
    OceanWaveSpectrum _spectrum;
    Material _foamInput;
    Color32[] _previousPhase;
    float _timeScale;
    string _report;
    [Serializable] class Source {public string path; public string sha256;}
    [Serializable] class Manifest {public Source[] originalShaderDependencies;}
    Dictionary<string,string> _expected;
    string _originalRoot;
    readonly HashSet<string> _sources = new HashSet<string>();

    [UnityTest, Timeout(180000)]
    public IEnumerator OriginalBuiltInAndUrpRenderSameFrozenFlatAndCompactHeightSurface()
    {
        _default=GraphicsSettings.defaultRenderPipeline; _quality=QualitySettings.renderPipeline; _timeScale=Time.timeScale;
        OceanRenderer.EnableGraphicsInBatchMode=true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        Assert.That(Application.isPlaying,Is.True);
        _report=$"Evidence/BuiltInReference-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report,"P09 original Built-in shader reference. Original local includes recursively expanded unchanged, only shader name renamed; engine UnityCG/Lighting unchanged. Same generated meshes, simulation bindings, material properties, camera/light/target. Compilation/coverage/repeatability are asserted; scoped RGB tolerance <=2 byte/channel and mean<=.05, URP restoration exact, each feature effect>100 pixels. This is not full visual acceptance.\n");
        string original=Path.GetFullPath("../../work/crest-upstream/crest/Assets/Crest/Crest/Shaders/Ocean.shader");
        _originalRoot=Path.GetDirectoryName(original);
        _expected=JsonUtility.FromJson<Manifest>(File.ReadAllText("Evidence/P09-Reference-inspection.json")).originalShaderDependencies.ToDictionary(x=>x.path,x=>x.sha256);
        string code=Expand(original).Replace("Shader \"Crest/Ocean\"","Shader \"Hidden/Crest/Tests/OriginalBuiltInOcean\"");
        Assert.That(_sources.Count,Is.EqualTo(_expected.Count));
        _shader=ShaderUtil.CreateShaderAsset(code,false);
        Assert.That(_shader,Is.Not.Null); CheckShader();
        File.AppendAllText(_report,$"OriginalFiles={_sources.Count}; ExpandedSourceSHA256={Hash(System.Text.Encoding.UTF8.GetBytes(code))}; ColorSpace={QualitySettings.activeColorSpace}; Playing={Application.isPlaying}\n");
        _ocean=OceanRenderer.Instance; _camera=Camera.main;
        foreach(var camera in Object.FindObjectsByType<Camera>()) camera.enabled=false;
        foreach(var shape in Object.FindObjectsByType<ShapeWaves>()) shape.enabled=false;
        _camera.transform.SetPositionAndRotation(new Vector3(.017f,28,-.033f),Quaternion.Euler(90,0,0));
        _camera.orthographic=true; _camera.orthographicSize=48; _camera.aspect=1;
        _camera.nearClipPlane=.1f; _camera.farClipPlane=2000; _camera.cullingMask=1<<4;
        _camera.clearFlags=CameraClearFlags.SolidColor; _camera.backgroundColor=Color.magenta;
        _camera.allowHDR=false; _camera.allowMSAA=false; _camera.depthTextureMode=DepthTextureMode.Depth;
        _camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        _ocean.ViewCamera=_camera;
        _animated=Object.Instantiate(_ocean._lodDataAnimWaves.Settings);
        _animated.CollisionSource=SimSettingsAnimatedWaves.CollisionSources.None;
        _ocean._simSettingsAnimatedWaves=_animated;
        var settings=new SerializedObject(_ocean);
        settings.FindProperty("_minScale").floatValue=16; settings.FindProperty("_maxScale").floatValue=16;
        settings.ApplyModifiedPropertiesWithoutUndo();
        RenderSettings.fog=false; RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.2f,.2f,.2f);
        _surface=new Material(_ocean.OceanMaterial);
        _surface.DisableKeyword("_TRANSPARENCY_ON"); _surface.SetFloat("_Transparency",0);
        _surface.DisableKeyword("_APPLYNORMALMAPPING_ON"); _surface.SetFloat("_NormalsStrengthOverall",1);
        _surface.SetFloat("_FoamStrength",0); _surface.DisableKeyword("_CAUSTICS_ON");
        _ocean.OceanMaterial=_surface;
        _reference=new Material(_surface){shader=_shader};
        _depth=ScriptableObject.CreateInstance<SimSettingsSeaFloorDepth>(); _depth._allowVaryingWaterLevel=true;
        _ocean._simSettingsSeaFloorDepth=_depth;
        for(int i=0;i<12;i++) yield return null;
        _clock=_ocean.gameObject.AddComponent<TimeProviderCustom>(); _clock._overrideTime=_clock._overrideDeltaTime=true;
        _clock._time=1; _clock._deltaTime=0; _ocean.PushTimeProvider(_clock);
        _target=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32){antiAliasing=1}; _target.Create();
        for(int i=0;i<12;i++) yield return null;
        Assert.That(_ocean.TimeProvider,Is.SameAs(_clock));
        yield return Compare("Flat");
        var go=new GameObject("P09 compact base height reference");
        _heightMesh=Patch(); go.AddComponent<MeshFilter>().sharedMesh=_heightMesh;
        _heightMaterial=new Material(Shader.Find("Crest/Inputs/Sea Floor Depth/Set Base Water Height Using Geometry"));
        go.AddComponent<MeshRenderer>().sharedMaterial=_heightMaterial; go.AddComponent<RegisterHeightInput>();
        for(int i=0;i<8;i++) yield return null;
        yield return Compare("Compact");
        _surface.EnableKeyword("_APPLYNORMALMAPPING_ON"); _surface.SetFloat("_ApplyNormalMapping",1);
        Assert.That(_surface.GetTexture("_Normals"),Is.Not.Null);
        yield return Compare("CompactNormals");
        _surface.EnableKeyword("_PROCEDURALSKY_ON"); _surface.SetFloat("_ProceduralSky",1);
        yield return Compare("CompactProceduralSky");
        _spectrum=ScriptableObject.CreateInstance<OceanWaveSpectrum>(); _spectrum._chop=1.6f; _spectrum._waveDirectionVariance=0;
        var spectrum=new SerializedObject(_spectrum);
        for(int i=0;i<OceanWaveSpectrum.NUM_OCTAVES;i++) {spectrum.FindProperty("_powerDisabled").GetArrayElementAtIndex(i).boolValue=i!=9;spectrum.FindProperty("_powerLog").GetArrayElementAtIndex(i).floatValue=-2;}
        spectrum.ApplyModifiedPropertiesWithoutUndo();
        var waveOwner=new GameObject("Original reference frozen choppy Gerstner"); waveOwner.SetActive(false);
        var waves=waveOwner.AddComponent<ShapeGerstner>(); waves._spectrum=_spectrum; waves._randomSeed=27; waves._componentsPerOctave=2; waves._resolution=64; waves._overrideGlobalWindSpeed=true; waves._windSpeed=60; waves._overrideGlobalWindDirection=true; waves._waveDirectionHeadingAngle=30; var waveConfiguration=new SerializedObject(waves); waveConfiguration.FindProperty("_respectShallowWaterAttenuation").floatValue=0; waveConfiguration.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(waves._resolution,Is.EqualTo(64)); waveOwner.SetActive(true);
        for(int i=0;i<12;i++) yield return null;
        yield return Compare("CompactChoppy");
        _foamSettings=ScriptableObject.CreateInstance<SimSettingsFoam>(); _foamSettings._prewarm=false;
        _foamSettings._waveFoamStrength=_foamSettings._shorelineFoamStrength=_foamSettings._foamFadeRate=0;
        _ocean._simSettingsFoam=_foamSettings;
        var seed=GameObject.CreatePrimitive(PrimitiveType.Plane); seed.transform.localScale=Vector3.one*100;
        _foamInput=new Material(Shader.Find("Crest/Inputs/Foam/Override Foam")); _foamInput.SetFloat("_FoamValue",.65f);
        seed.GetComponent<Renderer>().sharedMaterial=_foamInput; var input=seed.AddComponent<RegisterFoamInput>();
        _clock._deltaTime=1f/30;
        for(int i=0;i<8;i++) yield return null;
        _clock._deltaTime=0; input.enabled=false;
        for(int i=0;i<6;i++) yield return null;
        var foam=AsyncGPUReadback.Request(_ocean._lodDataFoam.DataTexture,0,TextureFormat.RFloat); foam.WaitForCompletion();
        Assert.That(foam.hasError,Is.False); int width=_ocean._lodDataFoam.DataTexture.width;
        Assert.That(foam.GetData<float>(0)[width*(width/2)+width/2],Is.EqualTo(.65f).Within(.005f));
        yield return Compare("CompactFoam");
        File.AppendAllText(_report,"Result=Reference renders completed; visual parity remains subject to inspection and measured differences.\n");
    }

    string Expand(string file)
    {
        file=Path.GetFullPath(file); _sources.Add(file);
        string relative=Path.GetRelativePath(_originalRoot,file).Replace('\\','/');
        Assert.That(_expected.ContainsKey(relative),Is.True,"Unexpected original shader dependency.");
        Assert.That(Hash(File.ReadAllBytes(file)),Is.EqualTo(_expected[relative]),"Original baseline must match recorded provenance.");
        string text=File.ReadAllText(file);
        return Regex.Replace(text,@"(?m)^\s*#include\s+""([^""]+)""[^\r\n]*",m=>
        {
            string dependency=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file),m.Groups[1].Value));
            if(!File.Exists(dependency)) return m.Value;
            return "\n// Original include: "+m.Groups[1].Value+"\n"+Expand(dependency)+"\n";
        });
    }
    static string Hash(byte[] bytes) { using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-",""); }
    void CheckShader()
    {
        var errors=ShaderUtil.GetShaderMessages(_shader).Where(x=>x.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
        File.AppendAllText(_report,string.Join("\n",errors.Select(x=>x.message))+"\n");
        Assert.That(errors,Is.Empty); Assert.That(_shader.isSupported,Is.True);
    }
    IEnumerator Compare(string phase)
    {
        Assert.That(GraphicsSettings.currentRenderPipeline,Is.Not.Null);
        _ocean.OceanMaterial=_surface;
        _reference.CopyPropertiesFromMaterial(_surface);
        string state=State(phase+"-URP");
        var urp=Capture(phase+"-URP",false);
        GraphicsSettings.defaultRenderPipeline=null; QualitySettings.renderPipeline=null;
        yield return null; yield return null;
        Assert.That(GraphicsSettings.currentRenderPipeline,Is.Null);
        _ocean.OceanMaterial=_reference;
        Assert.That(State(phase+"-BuiltIn"),Is.EqualTo(state),"Frozen native fields must match.");
        var birp=Capture(phase+"-BuiltIn",true); CheckShader();
        GraphicsSettings.defaultRenderPipeline=_default; QualitySettings.renderPipeline=_quality;
        yield return null; yield return null;
        Assert.That(GraphicsSettings.currentRenderPipeline,Is.Not.Null);
        _ocean.OceanMaterial=_surface;
        Assert.That(State(phase+"-URP-restored"),Is.EqualTo(state),"Native fields must survive pipeline restoration.");
        var restored=Capture(phase+"-URP-restored",false);
        long difference=0; int changed=0, repeat=0, max=0, maxChannel=0, effect=0;
        for(int i=0;i<urp.Length;i++)
        {
            int delta=Math.Abs(urp[i].r-birp[i].r)+Math.Abs(urp[i].g-birp[i].g)+Math.Abs(urp[i].b-birp[i].b);
            difference+=delta; if(delta>6) changed++; max=Math.Max(max,delta);
            maxChannel=Math.Max(maxChannel,Math.Max(Math.Abs(urp[i].r-birp[i].r),Math.Max(Math.Abs(urp[i].g-birp[i].g),Math.Abs(urp[i].b-birp[i].b))));
            if(_previousPhase!=null && Math.Abs(urp[i].r-_previousPhase[i].r)+Math.Abs(urp[i].g-_previousPhase[i].g)+Math.Abs(urp[i].b-_previousPhase[i].b)>6) effect++;
            if(!urp[i].Equals(restored[i])) repeat++;
        }
        File.AppendAllText(_report,$"Phase={phase}; MeanAbsoluteRGBByteDifference={(double)difference/(urp.Length*3):R}; PixelsOverSixRGBByteDifference={changed}; MaxSummedRGBDifference={max}; UrpRestoreChangedPixels={repeat}; MaxChannelDifference={maxChannel}; ChangedFromPreviousPhase={effect}\n");
        Assert.That(repeat,Is.Zero,"Pipeline round-trip must preserve frozen surface exactly.");
        Assert.That(maxChannel,Is.LessThanOrEqualTo(2),"Original reference RGB tolerance is two quantization steps per channel.");
        Assert.That((double)difference/(urp.Length*3),Is.LessThanOrEqualTo(.05),"Mean reference RGB byte error.");
        if(_previousPhase!=null) Assert.That(effect,Is.GreaterThan(100),"Each enabled feature must visibly affect the surface.");
        _previousPhase=urp;
    }
    string State(string label)
    {
        var waves=AsyncGPUReadback.Request(_ocean._lodDataAnimWaves.DataTexture,0); waves.WaitForCompletion();
        var depth=AsyncGPUReadback.Request(_ocean._lodDataSeaDepths.DataTexture,0); depth.WaitForCompletion();
        var foam=AsyncGPUReadback.Request(_ocean._lodDataFoam.DataTexture,0); foam.WaitForCompletion();
        Assert.That(waves.hasError||depth.hasError||foam.hasError,Is.False);
        File.AppendAllText(_report,$"State={label}; WaveHash={Hash(waves.GetData<byte>().ToArray())}; DepthHash={Hash(depth.GetData<byte>().ToArray())}; OceanTime={_ocean.CurrentTime:R}; Scale={_ocean.Scale:R}; FoamHash={Hash(foam.GetData<byte>().ToArray())}; ViewerAlpha={_ocean.ViewerAltitudeLevelAlpha:R}; MeshAlpha={Shader.GetGlobalFloat("_MeshScaleLerp"):R}\n");
        Assert.That(_ocean.CurrentTime,Is.EqualTo(1)); Assert.That(_ocean.DeltaTime,Is.Zero);
        Assert.That(Shader.GetGlobalFloat("_MeshScaleLerp"),Is.Zero);
        return Hash(waves.GetData<byte>().ToArray())+Hash(depth.GetData<byte>().ToArray())+Hash(foam.GetData<byte>().ToArray());
    }
    Color32[] Capture(string label,bool builtIn)
    {
        if(builtIn) { _camera.targetTexture=_target; try {_camera.Render();} finally {_camera.targetTexture=null;} }
        else RenderPipeline.SubmitRenderRequest(_camera,new UniversalRenderPipeline.SingleCameraRequest{destination=_target});
        var previous=RenderTexture.active; var cpu=new Texture2D(512,512,TextureFormat.RGBA32,false);
        try
        {
            RenderTexture.active=_target; cpu.ReadPixels(new Rect(0,0,512,512),0,0); cpu.Apply();
            File.WriteAllBytes($"Evidence/BuiltInReference-{label}-{SystemInfo.graphicsDeviceType}.png",cpu.EncodeToPNG());
            var pixels=cpu.GetPixels32(); int clear=pixels.Count(x=>x.r>240&&x.b>240&&x.g<16);
            File.AppendAllText(_report,$"Capture={label}; ClearPixels={clear}; Pipeline={GraphicsSettings.currentRenderPipeline?.name??"BuiltIn"}\n");
            Assert.That(clear,Is.Zero,"Actual surface must cover target; error pink also fails."); return pixels;
        }
        finally {RenderTexture.active=previous; Object.DestroyImmediate(cpu);}
    }
    static Mesh Patch()
    {
        const int n=33; var vertices=new Vector3[n*n]; var triangles=new List<int>();
        for(int z=0;z<n;z++) for(int x=0;x<n;x++)
        {
            float wx=-40+2.5f*x,wz=-40+2.5f*z;
            float fx=Mathf.Max(0,1-wx*wx/1600),fz=Mathf.Max(0,1-wz*wz/1600);
            vertices[z*n+x]=new Vector3(wx,4*fx*fx*fz*fz,wz);
            if(x<n-1&&z<n-1) {int i=z*n+x; triangles.AddRange(new[]{i,i+n,i+1,i+1,i+n,i+n+1});}
        }
        var mesh=new Mesh{name="Original reference compact height"}; mesh.vertices=vertices;mesh.triangles=triangles.ToArray();mesh.RecalculateBounds();return mesh;
    }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        GraphicsSettings.defaultRenderPipeline=_default; QualitySettings.renderPipeline=_quality;
        Time.timeScale=_timeScale; OceanRenderer.EnableGraphicsInBatchMode=false;
        if(Application.isPlaying) {yield return null;yield return new ExitPlayMode();}
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        if(_target!=null) {_target.Release();Object.DestroyImmediate(_target);}
        foreach(var obj in new Object[]{_surface,_reference,_shader,_heightMesh,_heightMaterial,_depth,_spectrum,_foamSettings,_foamInput,_animated}) if(obj!=null) Object.DestroyImmediate(obj);
    }
}
