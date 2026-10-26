// Crest 4 URP port. MIT; see LICENSE.Crest.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using Crest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public class OpticalReferenceTests
{
 const int Width=512,Height=384;
 readonly List<Object> _owned=new List<Object>();
 RenderPipelineAsset _default,_quality;
 Camera _camera;OceanRenderer _ocean;Material _surface,_reference;RenderTexture _target;
 readonly List<string> _errors=new List<string>();
 Material _backgroundProbe;
 Color32[] _geometry;
 TimeProviderCustom _clock;string _report;Color32[] _last;
 T Own<T>(T x) where T:Object {_owned.Add(x);return x;}
 [UnityTest,Timeout(180000)]
 public IEnumerator OriginalPerspectiveOpaqueControlTransparencyRefractionScatteringAndCaustics()
 {
  _default=GraphicsSettings.defaultRenderPipeline;_quality=QualitySettings.renderPipeline;
  OceanRenderer.EnableGraphicsInBatchMode=true;EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
  yield return new EnterPlayMode();Assert.That(Application.isPlaying,Is.True);
  _report=$"Evidence/OpticalReference-{SystemInfo.graphicsDeviceType}.txt";
  File.WriteAllText(_report,"P09.3 perspective optical reference; actual original Built-in vs URP with synchronous pipeline switch/no simulation tick.512x384 Linear/ARGB32/1x, camera(0,8,-12) looking at(0,-2,4), FOV60, sea floor-6. Independent opaque-control shader works in both pipelines and has a ShadowCaster for Built-in camera depth. RGB tolerance max2 byte/channel/mean.05; exact URP restoration, each feature effect>100 pixels.\n");
  _ocean=OceanRenderer.Instance;_camera=Camera.main;
  foreach(var cam in Object.FindObjectsByType<Camera>()) cam.enabled=false;
  foreach(var shape in Object.FindObjectsByType<ShapeWaves>()) shape.enabled=false;
  _camera.transform.SetPositionAndRotation(new Vector3(0,8,-12),Quaternion.LookRotation(new Vector3(0,-10,16)));
  _camera.orthographic=false;_camera.fieldOfView=60;_camera.aspect=(float)Width/Height;
  _camera.nearClipPlane=.1f;_camera.farClipPlane=1000;_camera.cullingMask=(1<<4)|(1<<5);
  _camera.clearFlags=CameraClearFlags.SolidColor;_camera.backgroundColor=new Color(.13f,.21f,.31f);
  _camera.allowHDR=_camera.allowMSAA=false;_camera.depthTextureMode=DepthTextureMode.Depth;
  var cameraData=_camera.GetUniversalAdditionalCameraData();cameraData.renderPostProcessing=false;cameraData.requiresDepthTexture=true;cameraData.renderShadows=false;cameraData.taaSettings.mipBias=0;
  _ocean.ViewCamera=_camera;RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.2f,.2f,.2f);
  _surface=Own(new Material(_ocean.OceanMaterial));
  foreach(string key in new[]{"_FOAM_ON","_TRANSPARENCY_ON","_APPLYNORMALMAPPING_ON","_CAUSTICS_ON","_CLIPSURFACE_ON","_SHADOWS_ON"}) _surface.DisableKeyword(key);
  _surface.SetFloat("_Foam",0);_surface.SetFloat("_Transparency",0);_surface.SetFloat("_ApplyNormalMapping",0);_surface.SetFloat("_Caustics",0);
  _surface.SetVector("_DepthFogDensity",new Vector4(.12f,.07f,.035f,1));_surface.SetFloat("_RefractionStrength",0);
  _ocean.OceanMaterial=_surface;
  var config=new SerializedObject(_ocean);config.FindProperty("_minScale").floatValue=8;config.FindProperty("_maxScale").floatValue=8;
  config.FindProperty("_createFoamSim").boolValue=false;config.FindProperty("_createClipSurfaceData").boolValue=false;config.ApplyModifiedPropertiesWithoutUndo();
  var animated=Own(Object.Instantiate(_ocean._lodDataAnimWaves.Settings));animated.CollisionSource=SimSettingsAnimatedWaves.CollisionSources.None;_ocean._simSettingsAnimatedWaves=animated;
  var depth=Own(ScriptableObject.CreateInstance<SimSettingsSeaFloorDepth>());depth._allowVaryingWaterLevel=false;_ocean._simSettingsSeaFloorDepth=depth;
  Geometry(PrimitiveType.Plane,new Vector3(0,-6,0),new Vector3(20,1,20),new Color(.65f,.5f,.3f));
  Geometry(PrimitiveType.Cube,new Vector3(-3,-3,4),new Vector3(3,4,3),new Color(.85f,.18f,.07f));
  Geometry(PrimitiveType.Cube,new Vector3(3,-4,9),new Vector3(3,3,3),new Color(.1f,.7f,.3f));
  Geometry(PrimitiveType.Cube,new Vector3(0,0,16),new Vector3(2,4,2),new Color(.1f,.2f,.9f));
  var input=GameObject.CreatePrimitive(PrimitiveType.Plane);input.name="Known original sea floor input";input.transform.position=new Vector3(0,-6,0);input.transform.localScale=new Vector3(20,1,20);
  input.GetComponent<Renderer>().sharedMaterial=Own(new Material(Shader.Find("Crest/Inputs/Depth/Ocean Depth From Geometry")));input.AddComponent<RegisterSeaFloorDepthInput>();
  for(int i=0;i<12;i++) yield return null;
  _clock=_ocean.gameObject.AddComponent<TimeProviderCustom>();_clock._overrideTime=_clock._overrideDeltaTime=true;_clock._time=1;_clock._deltaTime=0;_ocean.PushTimeProvider(_clock);
  for(int i=0;i<8;i++) yield return null;
  var floorReadback=AsyncGPUReadback.Request(_ocean._lodDataSeaDepths.DataTexture,0,TextureFormat.RGFloat);floorReadback.WaitForCompletion();
  Assert.That(floorReadback.hasError,Is.False);int floorWidth=_ocean._lodDataSeaDepths.DataTexture.width;
  var floorValue=floorReadback.GetData<Vector2>(0)[floorWidth*(floorWidth/2)+floorWidth/2];
  File.AppendAllText(_report,$"NativeSeaFloorCentre={floorValue}; ExpectedHeight=-6\n");Assert.That(floorValue.x,Is.EqualTo(-6).Within(.001f));
  _target=Own(new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32));_target.Create();
  var original=Own(BuiltInReferenceTests.CreateVerifiedOriginalShader(_report));_reference=Own(new Material(_surface){shader=original});
  var tiles=_ocean.Root.GetComponentsInChildren<MeshRenderer>();var states=tiles.Select(x=>x.forceRenderingOff).ToArray();
  foreach(var tile in tiles) tile.forceRenderingOff=true;
  try {Compare("OpaqueGeometryControl",false);DepthControl();}
  finally {for(int i=0;i<tiles.Length;i++) tiles[i].forceRenderingOff=states[i];}
  _last=null;
  var background=GameObject.CreatePrimitive(PrimitiveType.Quad);background.layer=5;background.transform.position=_camera.transform.position+_camera.transform.forward;background.transform.rotation=_camera.transform.rotation;
  float quadHeight=2*Mathf.Tan(_camera.fieldOfView*.5f*Mathf.Deg2Rad);background.transform.localScale=new Vector3(quadHeight*_camera.aspect,quadHeight,1);
  _backgroundProbe=Own(new Material(Shader.Find("Hidden/Crest/Tests/DualPipelineDepthProbe")));_backgroundProbe.SetFloat("_Mode",1);background.GetComponent<Renderer>().sharedMaterial=_backgroundProbe;
  Compare("BackgroundZero",false);_backgroundProbe.SetVector("_Offset",new Vector4(.33f/Width,.2f/Height,0,0));Compare("BackgroundSubpixel",false);
  background.SetActive(false);_backgroundProbe=null;_last=null;
  Compare("OpaqueWater",false);
  _surface.EnableKeyword("_TRANSPARENCY_ON");_surface.SetFloat("_Transparency",1);Compare("TransparentZeroOffset",true);
  _surface.EnableKeyword("_APPLYNORMALMAPPING_ON");_surface.SetFloat("_ApplyNormalMapping",1);
  Compare("NormalsZeroOffset",false);
  _surface.SetFloat("_RefractionStrength",.5f);Compare("NormalRefraction",true);OceanInternals();
  var originalNormal=_surface.GetTexture("_Normals");
  var constant=Own(new Texture2D(4,4,TextureFormat.RGBA32,false,true));constant.SetPixels(Enumerable.Repeat(new Color(.75f,.5f,0,1),16).ToArray());constant.Apply();
  _surface.SetTexture("_Normals",constant);Compare("ConstantNormalRefraction",false);_surface.SetTexture("_Normals",originalNormal);Compare("NormalRefractionRestored",false);
  _surface.EnableKeyword("_SUBSURFACESCATTERING_ON");_surface.SetFloat("_SubSurfaceScattering",1);_surface.EnableKeyword("_SUBSURFACESHALLOWCOLOUR_ON");_surface.SetFloat("_SubSurfaceShallowColour",1);_surface.SetColor("_SubSurfaceShallowCol",new Color(.1f,.55f,.15f));_surface.SetFloat("_SubSurfaceDepthMax",20);Compare("ShallowScattering",true);
  _surface.EnableKeyword("_CAUSTICS_ON");_surface.SetFloat("_Caustics",1);_surface.SetFloat("_CausticsFocalDepth",6);_surface.SetFloat("_CausticsDepthOfField",10);_surface.SetFloat("_CausticsStrength",3.2f);Compare("Caustics",true);
  Assert.That(_errors,Is.Empty,"All measured reference tolerances must pass.");
 }
 void DepthControl()
 {
  var probe=GameObject.CreatePrimitive(PrimitiveType.Quad);probe.layer=5;probe.transform.position=_camera.transform.position+_camera.transform.forward;probe.transform.rotation=_camera.transform.rotation;
  float height=2*Mathf.Tan(_camera.fieldOfView*.5f*Mathf.Deg2Rad);probe.transform.localScale=new Vector3(height*_camera.aspect,height,1);
  var material=Own(new Material(Shader.Find("Hidden/Crest/Tests/DualPipelineDepthProbe")));probe.GetComponent<Renderer>().sharedMaterial=material;
  var target=Own(new RenderTexture(Width,Height,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear));target.Create();
  foreach(var offset in new[]{Vector2.zero,new Vector2(.33f/Width,.2f/Height),Vector2.one})
  {
   string phase=offset==Vector2.one?"ScreenGrabUv":offset==Vector2.zero?"DepthZero":"DepthSubpixel";material.SetVector("_Offset",offset);material.SetFloat("_Mode",offset==Vector2.one?3:0);material.SetFloat("_UseUrp",1);
   RenderPipeline.SubmitRenderRequest(_camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});var a=ReadDepth(target,phase+"-URP");Color[] b=null;
   try {GraphicsSettings.defaultRenderPipeline=null;QualitySettings.renderPipeline=null;material.SetFloat("_UseUrp",0);_camera.targetTexture=target;try {_camera.Render();} finally {_camera.targetTexture=null;}b=ReadDepth(target,phase+"-BuiltIn");}
   finally {GraphicsSettings.defaultRenderPipeline=_default;QualitySettings.renderPipeline=_quality;}
   float raw=0,eye=0,uv=0;int changed=0;
   for(int i=0;i<a.Length;i++) {raw=Mathf.Max(raw,Mathf.Abs(a[i].r-b[i].r));eye=Mathf.Max(eye,Mathf.Abs(a[i].g-b[i].g));uv=Mathf.Max(uv,Mathf.Abs(a[i].b-b[i].b),Mathf.Abs(a[i].a-b[i].a));if(Mathf.Abs(a[i].g-b[i].g)>.01f)changed++;}
   File.AppendAllText(_report,$"DepthControl={phase}; MaxRawDifference={raw:R}; MaxEyeMetresDifference={eye:R}; EyeDifferenceOverCm={changed}; MaxUvDifference={uv:R}\n");
  }
  probe.SetActive(false);target.Release();
 }
 Color[] ReadDepth(RenderTexture target,string phase)
 {
  var previous=RenderTexture.active;var cpu=new Texture2D(Width,Height,TextureFormat.RGBAFloat,false,true);
  try
  {
   RenderTexture.active=target;cpu.ReadPixels(new Rect(0,0,Width,Height),0,0);cpu.Apply();
   using(var file=File.Create($"Evidence/OpticalReference-{phase}-{SystemInfo.graphicsDeviceType}.rgba-f32.gz"))
   using(var gzip=new GZipStream(file,System.IO.Compression.CompressionLevel.Optimal)) {var bytes=cpu.GetRawTextureData<byte>().ToArray();gzip.Write(bytes,0,bytes.Length);}
   return cpu.GetPixels();
  }
  finally {RenderTexture.active=previous;Object.DestroyImmediate(cpu);}
 }
 string ExpandProbe(string file)
 {
  return Regex.Replace(File.ReadAllText(file),@"(?m)^\s*#include\s+""([^""]+)""[^\r\n]*",m=>
  {
   string child=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file),m.Groups[1].Value));
   return File.Exists(child)?"\n"+ExpandProbe(child)+"\n":m.Value;
  });
 }
 Shader InternalProbe(bool original)
 {
  string file=Path.GetFullPath(original?"../../work/crest-upstream/crest/Assets/Crest/Crest/Shaders/Ocean.shader":"Assets/Crest/Crest/Shaders/Ocean.shader");
  string code=ExpandProbe(file).Replace("Shader \"Crest/Ocean\"","Shader \"Hidden/Crest/Tests/OpticalInternal"+(original?"BuiltIn":"URP")+"\"");
  code=code.Replace("half4 Frag(","float4 Frag(");
  code=code.Replace("Blend SrcAlpha OneMinusSrcAlpha","Blend Off");
  // Named GrabPass caches once per frame: isolate the float diagnostic from prior 8-bit renders.
  int start=code.IndexOf("#include");code=code.Insert(start,"float _CrestOpticalProbe;\n");
  string needle="return half4(col, reflAlpha);";Assert.That(code.Contains(needle),Is.True);
  code=code.Replace(needle,@"
   float2 probeOffset = _RefractionStrength * n_pixel.xz * min(1.0, .5*(sceneZ-pixelZ))/sceneZ;
   float probeRaw = CREST_SAMPLE_SCENE_DEPTH_X(uvDepth+probeOffset);
   float2 probeBg = input.grabPos.xy/input.grabPos.w + (probeRaw < input.positionCS.z ? probeOffset : 0);
   if(_CrestOpticalProbe==1) return float4(n_pixel,pixelZ);
   if(_CrestOpticalProbe==2) return float4(input.grabPos.xy/input.grabPos.w,uvDepth);
   if(_CrestOpticalProbe==3) return float4(probeOffset,sceneZ,pixelZ);
   if(_CrestOpticalProbe==4) return float4(probeRaw,CrestLinearEyeDepth(probeRaw),probeBg);
   if(_CrestOpticalProbe==5)
   {
#if defined(CREST_URP)
    return float4(CrestSampleBackground(probeBg),CrestLinearEyeDepth(probeRaw)-pixelZ);
#else
    return float4(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_BackgroundTexture,probeBg).rgb,CrestLinearEyeDepth(probeRaw)-pixelZ);
#endif
   }
   if(_CrestOpticalProbe==8) return float4(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_BackgroundTexture,probeBg).rgb,0);
   if(_CrestOpticalProbe>=6)
   {
#if defined(CREST_URP)
    if(_CrestOpticalProbe==6) return float4(SAMPLE_TEXTURE2D_X_LOD(_BackgroundTexture,sampler_LinearClamp,probeBg,0).rgb,0);
    return float4(SAMPLE_TEXTURE2D_X_LOD(_BackgroundTexture,sampler_PointClamp,probeBg,0).rgb,0);
#else
    return float4(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_BackgroundTexture,probeBg).rgb,0);
#endif
   }
   return half4(col,reflAlpha);
  ");
  if(original)code=code.Replace("_BackgroundTexture","_CrestOpticalInternalBackground");
  var shader=Own(ShaderUtil.CreateShaderAsset(code,false));
  var errors=ShaderUtil.GetShaderMessages(shader).Where(x=>x.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).Select(x=>x.message).ToArray();
  File.AppendAllText(_report,"DiagnosticCompile="+string.Join(";",errors)+"\n");Assert.That(errors,Is.Empty);return shader;
 }
 void OceanInternals()
 {
  var port=Own(new Material(_surface){shader=InternalProbe(false)});var builtin=Own(new Material(_surface){shader=InternalProbe(true)});
  port.CopyPropertiesFromMaterial(_surface);builtin.CopyPropertiesFromMaterial(_surface);
  Assert.That(port.IsKeywordEnabled("_APPLYNORMALMAPPING_ON")&&builtin.IsKeywordEnabled("_APPLYNORMALMAPPING_ON"),Is.True);
  var target=Own(new RenderTexture(Width,Height,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear));target.Create();
  try
  {
   for(int mode=1;mode<=8;mode++)
   {
    Shader.SetGlobalFloat("_CrestOpticalProbe",mode);_ocean.OceanMaterial=port;
    RenderPipeline.SubmitRenderRequest(_camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});var a=ReadDepth(target,"Internal"+mode+"-URP");Color[] b=null;
    try {GraphicsSettings.defaultRenderPipeline=null;QualitySettings.renderPipeline=null;_ocean.OceanMaterial=builtin;_camera.targetTexture=target;try {_camera.Render();}finally {_camera.targetTexture=null;}b=ReadDepth(target,"Internal"+mode+"-BuiltIn");}
    finally {GraphicsSettings.defaultRenderPipeline=_default;QualitySettings.renderPipeline=_quality;}
    Assert.That(ShaderUtil.GetShaderMessages(builtin.shader).Any(x=>x.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error),Is.False);
    float r=0,g=0,blue=0,alpha=0;int changed=0;
    for(int i=0;i<a.Length;i++){r=Mathf.Max(r,Mathf.Abs(a[i].r-b[i].r));g=Mathf.Max(g,Mathf.Abs(a[i].g-b[i].g));blue=Mathf.Max(blue,Mathf.Abs(a[i].b-b[i].b));alpha=Mathf.Max(alpha,Mathf.Abs(a[i].a-b[i].a));if(a[i]!=b[i])changed++;}
    File.AppendAllText(_report,$"OceanInternal={mode}; MaxR={r:R}; MaxG={g:R}; MaxB={blue:R}; MaxA={alpha:R}; Changed={changed}\n");
   }
  }
  finally {_ocean.OceanMaterial=_surface;Shader.SetGlobalFloat("_CrestOpticalProbe",0);target.Release();}
 }
 void Geometry(PrimitiveType type,Vector3 position,Vector3 scale,Color color)
 {
  var go=GameObject.CreatePrimitive(type);go.layer=5;go.transform.position=position;go.transform.localScale=scale;
  var material=Own(new Material(Shader.Find("Hidden/Crest/Tests/DualPipelineOpaque")));material.SetColor("_Color",color);go.GetComponent<Renderer>().sharedMaterial=material;
 }
 void Compare(string phase,bool effectRequired)
 {
  Assert.That(_ocean.TimeProvider,Is.SameAs(_clock));Assert.That(_ocean.CurrentTime,Is.EqualTo(1));Assert.That(_ocean.DeltaTime,Is.Zero);
  if(_backgroundProbe!=null)_backgroundProbe.SetFloat("_UseUrp",1);
  int frame=OceanRenderer.FrameCount;_reference.CopyPropertiesFromMaterial(_surface);_ocean.OceanMaterial=_surface;
  RenderPipeline.SubmitRenderRequest(_camera,new UniversalRenderPipeline.SingleCameraRequest{destination=_target});var urp=Read(phase+"-URP");
  Color32[] original=null;
  try
  {
   GraphicsSettings.defaultRenderPipeline=null;QualitySettings.renderPipeline=null;Assert.That(GraphicsSettings.currentRenderPipeline,Is.Null);
   if(_backgroundProbe!=null)_backgroundProbe.SetFloat("_UseUrp",0);
   _ocean.OceanMaterial=_reference;_camera.targetTexture=_target;try {_camera.Render();} finally {_camera.targetTexture=null;}
   original=Read(phase+"-BuiltIn");
  }
  finally {GraphicsSettings.defaultRenderPipeline=_default;QualitySettings.renderPipeline=_quality;_ocean.OceanMaterial=_surface;if(_backgroundProbe!=null)_backgroundProbe.SetFloat("_UseUrp",1);}
  RenderPipeline.SubmitRenderRequest(_camera,new UniversalRenderPipeline.SingleCameraRequest{destination=_target});var restored=Read(phase+"-URP-restored");
  int max=0,changed=0,repeat=0,effect=0,opaqueGeometry=0;long total=0;
  for(int i=0;i<urp.Length;i++)
  {
   int r=Math.Abs(urp[i].r-original[i].r),g=Math.Abs(urp[i].g-original[i].g),b=Math.Abs(urp[i].b-original[i].b);max=Math.Max(max,Math.Max(r,Math.Max(g,b)));total+=r+g+b;if(r+g+b>6)changed++;
   if(!urp[i].Equals(restored[i]))repeat++;
   if(_last!=null && Math.Abs(urp[i].r-_last[i].r)+Math.Abs(urp[i].g-_last[i].g)+Math.Abs(urp[i].b-_last[i].b)>6)effect++;
   if(urp[i].r>urp[i].g*1.2f&&urp[i].r>urp[i].b+20)opaqueGeometry++;
  }
  double mean=(double)total/(urp.Length*3);
  File.AppendAllText(_report,$"Phase={phase}; MaxChannelDifference={max}; MeanRgbByteDifference={mean:R}; PixelsOverSix={changed}; RestoreChangedPixels={repeat}; FeatureChangedPixels={effect}; OpaqueControlColorPixels={opaqueGeometry}; FrameCount={frame}; GlobalMipBias={Shader.GetGlobalVector("_GlobalMipBias")}\n");
  Assert.That(OceanRenderer.FrameCount,Is.EqualTo(frame));Assert.That(repeat,Is.Zero);
  if(max>2||mean>.05)_errors.Add($"{phase}: max={max}, mean={mean:R}");
  if(phase=="OpaqueGeometryControl")_geometry=urp;
  if(phase=="BackgroundZero")
  {
   int control=0;for(int i=0;i<urp.Length;i++)if(Math.Abs(urp[i].r-_geometry[i].r)+Math.Abs(urp[i].g-_geometry[i].g)+Math.Abs(urp[i].b-_geometry[i].b)>3)control++;
   File.AppendAllText(_report,$"BackgroundMatchesOpaqueControlChangedPixels={control}\n");Assert.That(control,Is.Zero);
  }
  if(effectRequired)Assert.That(effect,Is.GreaterThan(100));if(phase=="OpaqueGeometryControl")Assert.That(opaqueGeometry,Is.GreaterThan(5000));_last=urp;
 }
 Color32[] Read(string phase)
 {
  var previous=RenderTexture.active;var cpu=new Texture2D(Width,Height,TextureFormat.RGBA32,false);
  try {var depth=Shader.GetGlobalTexture("_CameraDepthTexture");File.AppendAllText(_report,$"PostRenderDepth={phase}; Type={depth?.GetType().Name}; Size={depth?.width}x{depth?.height}; Format={depth?.graphicsFormat}; Filter={depth?.filterMode}; TexelSize={Shader.GetGlobalVector("_CameraDepthTexture_TexelSize")}\n");RenderTexture.active=_target;cpu.ReadPixels(new Rect(0,0,Width,Height),0,0);cpu.Apply();File.WriteAllBytes($"Evidence/OpticalReference-{phase}-{SystemInfo.graphicsDeviceType}.png",cpu.EncodeToPNG());return cpu.GetPixels32();}
  finally {RenderTexture.active=previous;Object.DestroyImmediate(cpu);}
 }
 [UnityTearDown]
 public IEnumerator Cleanup()
 {
  GraphicsSettings.defaultRenderPipeline=_default;QualitySettings.renderPipeline=_quality;OceanRenderer.EnableGraphicsInBatchMode=false;
  if(Application.isPlaying)yield return new ExitPlayMode();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  if(_target!=null)_target.Release();foreach(var obj in _owned)if(obj!=null)Object.DestroyImmediate(obj);_owned.Clear();
 }
}
