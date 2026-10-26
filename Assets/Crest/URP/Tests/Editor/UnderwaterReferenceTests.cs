// Crest 4 URP port. MIT; see LICENSE.Crest.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
using Object=UnityEngine.Object;

public class UnderwaterReferenceTests
{
 const int Width=320,Height=240;
 const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 [Serializable] class Source {public string path;public string sha256;}
 [Serializable] class Manifest {public Source[] files;}
 readonly List<Object> _owned=new List<Object>();
 RenderPipelineAsset _pipeline,_quality;
 OceanRenderer _ocean;Camera _camera;UnderwaterRenderer _fog;TimeProviderCustom _clock;
 Material _surface,_originalSurface;Shader _originalMask,_originalEffect;
 RenderTexture _target;string _report,_root,_includeRoot;Dictionary<string,string> _sources;
 bool _referenceActive;int _callbacks;
 T Own<T>(T obj) where T:Object {_owned.Add(obj);return obj;}
 static FieldInfo Field(string name)=>typeof(UnderwaterRenderer).GetField(name,Private);
 Material Material(string field)=>((PropertyWrapperMaterial)Field(field).GetValue(_fog)).material;
 void Call(string name)=>typeof(UnderwaterRenderer).GetMethod(name,Private).Invoke(_fog,null);

 [UnityTest,Timeout(180000)]
 public IEnumerator OriginalFullscreenUnderwaterMaskFogAndMeniscusMatchUrp()
 {
  _pipeline=GraphicsSettings.defaultRenderPipeline;_quality=QualitySettings.renderPipeline;
  OceanRenderer.EnableGraphicsInBatchMode=true;EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
  yield return new EnterPlayMode();Assert.That(Application.isPlaying,Is.True);
  _report=$"Evidence/UnderwaterReference-{SystemInfo.graphicsDeviceType}.txt";
  File.WriteAllText(_report,"P09.4 original fullscreen BIRP mask/effect callbacks and shader reference. Actual legacy CameraEvent mask/depth/color resources; no URP mask substituted. Batch Camera.onPreRender shim bypasses only Editor focus guard and sets original inverse VP/global state before invoking unchanged mask/effect methods.320x240/1x Linear, frozen flat ocean time1/delta0; unshadowed only. Predefined RGB max2/mean.05, exact URP restoration, visible fog/meniscus effects.\n");
  _root=Path.GetFullPath("../../work/crest-upstream/crest/Assets/Crest/Crest/Shaders");
  _sources=JsonUtility.FromJson<Manifest>(File.ReadAllText("Evidence/P09-UnderwaterReference-sources.json")).files.ToDictionary(x=>x.path,x=>x.sha256);
  _ocean=OceanRenderer.Instance;_camera=Camera.main;
  foreach(var cam in Object.FindObjectsByType<Camera>())cam.enabled=false;
  foreach(var shape in Object.FindObjectsByType<ShapeWaves>())shape.enabled=false;
  _camera.transform.SetPositionAndRotation(new Vector3(0,-3,0),Quaternion.identity);
  _camera.fieldOfView=60;_camera.aspect=(float)Width/Height;_camera.orthographic=false;
  _camera.nearClipPlane=.1f;_camera.farClipPlane=100;_camera.cullingMask=(1<<4)|(1<<5);
  _camera.clearFlags=CameraClearFlags.SolidColor;_camera.backgroundColor=new Color(.17f,.24f,.35f);
  _camera.allowHDR=_camera.allowMSAA=false;_camera.depthTextureMode=DepthTextureMode.Depth;
  var cameraData=_camera.GetUniversalAdditionalCameraData();cameraData.renderPostProcessing=false;cameraData.renderShadows=false;cameraData.requiresDepthTexture=true;cameraData.taaSettings.mipBias=0;
  _ocean.ViewCamera=_camera;RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.2f,.2f,.2f);
  if(RenderSettings.sun!=null) {RenderSettings.sun.shadows=LightShadows.None;RenderSettings.sun.intensity=1;RenderSettings.sun.color=Color.white;}
  _surface=Own(new Material(_ocean.OceanMaterial));
  foreach(var key in new[]{"_FOAM_ON","_SHADOWS_ON","_CAUSTICS_ON","_APPLYNORMALMAPPING_ON","_CLIPSURFACE_ON"})_surface.DisableKeyword(key);
  _surface.EnableKeyword("_UNDERWATER_ON");_surface.SetFloat("_Underwater",1);
  _surface.SetFloat("_Foam",0);_surface.SetFloat("_Caustics",0);_surface.SetFloat("_ApplyNormalMapping",0);
  _surface.SetVector("_DepthFogDensity",new Vector4(.12f,.07f,.035f,1));_ocean.OceanMaterial=_surface;
  var settings=new SerializedObject(_ocean);settings.FindProperty("_minScale").floatValue=8;settings.FindProperty("_maxScale").floatValue=8;
  settings.FindProperty("_createFoamSim").boolValue=false;settings.FindProperty("_createClipSurfaceData").boolValue=false;settings.ApplyModifiedPropertiesWithoutUndo();
  var animated=Own(Object.Instantiate(_ocean._lodDataAnimWaves.Settings));animated.CollisionSource=SimSettingsAnimatedWaves.CollisionSources.None;_ocean._simSettingsAnimatedWaves=animated;
  var depth=Own(ScriptableObject.CreateInstance<SimSettingsSeaFloorDepth>());depth._allowVaryingWaterLevel=false;_ocean._simSettingsSeaFloorDepth=depth;
  Geometry(PrimitiveType.Plane,new Vector3(0,-8,12),new Vector3(10,1,10),new Color(.65f,.5f,.3f));
  Geometry(PrimitiveType.Cube,new Vector3(-3,-3,12),new Vector3(3,4,3),new Color(.85f,.18f,.07f));
  Geometry(PrimitiveType.Cube,new Vector3(3,-4,17),new Vector3(3,4,3),new Color(.1f,.7f,.3f));
  Geometry(PrimitiveType.Cube,new Vector3(0,-2,30),new Vector3(2,12,2),new Color(.15f,.2f,.85f));
  for(int i=0;i<12;i++)yield return null;
  _clock=_ocean.gameObject.AddComponent<TimeProviderCustom>();_clock._overrideTime=_clock._overrideDeltaTime=true;_clock._time=1;_clock._deltaTime=0;_ocean.PushTimeProvider(_clock);
  _target=Own(new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32));_target.Create();
  _fog=_camera.gameObject.AddComponent<UnderwaterRenderer>();_fog.EnableShaderAPI=false;_fog._copyOceanMaterialParamsEachFrame=true;
  _originalSurface=Own(new Material(_surface){shader=Load("Ocean.shader","Surface")});_originalSurface.CopyPropertiesFromMaterial(_surface);
  _originalMask=Load("Underwater/Resources/UnderwaterMask.shader","Mask");_originalEffect=Load("Underwater/Resources/UnderwaterEffect.shader","Effect");
  Camera.onPreRender+=PrepareOriginal;
  for(int i=0;i<8;i++)yield return null;
  Assert.That(_fog.IsActive,Is.True);
  _fog.enabled=false;var control=Capture("Below-no-fog",false);_fog.enabled=true;
  var below=Compare("Below-effect");Assert.That(Different(control,below),Is.GreaterThan(1000));
  DebugMask(true);Compare("Below-mask");DebugMask(false);
  _camera.transform.position=new Vector3(0,.1f,0);for(int i=0;i<12;i++)yield return null;
  Assert.That(_fog.IsActive,Is.True);DebugMask(true);Compare("Near-mask");DebugMask(false);
  Meniscus(false);var noMeniscus=Compare("Near-no-meniscus");Meniscus(true);var meniscus=Compare("Near-meniscus");
  int edge=Different(noMeniscus,meniscus);File.AppendAllText(_report,$"MeniscusChangedPixels={edge}\n");Assert.That(edge,Is.GreaterThan(50));
  _camera.transform.position=new Vector3(0,3,0);for(int i=0;i<12;i++)yield return null;
  Assert.That(_fog.IsActive,Is.False);Compare("Above-optimised");
 }
 void Geometry(PrimitiveType type,Vector3 pos,Vector3 scale,Color color)
 {
  var go=GameObject.CreatePrimitive(type);go.layer=5;go.transform.position=pos;go.transform.localScale=scale;
  var material=Own(new Material(Shader.Find("Hidden/Crest/Tests/DualPipelineOpaque")));material.SetColor("_Color",color);go.GetComponent<Renderer>().sharedMaterial=material;
 }
 void DebugMask(bool value) {var s=new SerializedObject(_fog);s.FindProperty("_debug").FindPropertyRelative("_viewOceanMask").boolValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
 void Meniscus(bool value) {var s=new SerializedObject(_fog);s.FindProperty("_meniscus").boolValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
 static string Hash(byte[] bytes) {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}
 string Expand(string file)
 {
  file=Path.GetFullPath(file);string relative=Path.GetRelativePath(_root,file).Replace('\\','/');
  Assert.That(_sources.ContainsKey(relative),Is.True);Assert.That(Hash(File.ReadAllBytes(file)),Is.EqualTo(_sources[relative]));
  return Regex.Replace(File.ReadAllText(file),@"(?m)^\s*#include\s+""([^""]+)""[^\r\n]*",m=>
  {
   string child=new[]{Path.Combine(Path.GetDirectoryName(file),m.Groups[1].Value),Path.Combine(_includeRoot,m.Groups[1].Value),Path.Combine(_root,m.Groups[1].Value)}.Select(Path.GetFullPath).FirstOrDefault(File.Exists);return child!=null?"\n"+Expand(child)+"\n":m.Value;
  });
 }
 Shader Load(string file,string label)
 {
  _includeRoot=Path.GetDirectoryName(Path.Combine(_root,file));string code=Expand(Path.Combine(_root,file));code=new Regex(@"(?m)^Shader\s+""[^""]+""").Replace(code,"Shader \"Hidden/Crest/Tests/OriginalUnderwater"+label+"\"",1);
  var shader=Own(ShaderUtil.CreateShaderAsset(code,false));Check(shader);return shader;
 }
 void Check(Shader shader)
 {
  var errors=ShaderUtil.GetShaderMessages(shader).Where(x=>x.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).Select(x=>x.message).ToArray();
  File.AppendAllText(_report,"ShaderErrors="+string.Join(";",errors)+"\n");Assert.That(errors,Is.Empty);
 }
 void PrepareOriginal(Camera camera)
 {
  if(!_referenceActive||camera!=_camera)return;
  _callbacks++;
  if(!_fog.IsActive)
  {
   foreach(string name in new[]{"_oceanMaskCommandBuffer","_underwaterEffectCommandBuffer"})((CommandBuffer)Field(name).GetValue(_fog)).Clear();return;
  }
  Field("_gpuInverseViewProjectionMatrix").SetValue(_fog,(GL.GetGPUProjectionMatrix(_camera.projectionMatrix,false)*_camera.worldToCameraMatrix).inverse);
  Field("_cameraFrustumPlanes").SetValue(_fog,GeometryUtility.CalculateFrustumPlanes(_camera));
  Call("UpdateOceanRendererStateForCamera");Call("OnPreRenderOceanMask");Call("OnPreRenderUnderwaterEffect");
 }
 Color32[] Compare(string phase)
 {
  int frame=OceanRenderer.FrameCount;int calls=_callbacks;Assert.That(_ocean.TimeProvider,Is.SameAs(_clock));Assert.That(_ocean.CurrentTime,Is.EqualTo(1));Assert.That(_ocean.DeltaTime,Is.Zero);
  var urp=Capture(phase+"-URP",false);
  var mask=Material("_oceanMaskMaterial");var effect=Material("_underwaterEffectMaterial");var maskShader=mask.shader;var effectShader=effect.shader;
  Color32[] original=null;
  try
  {
   _fog.enabled=false;GraphicsSettings.defaultRenderPipeline=null;QualitySettings.renderPipeline=null;
   _ocean.OceanMaterial=_originalSurface;_fog.enabled=true;mask.shader=_originalMask;effect.shader=_originalEffect;
   _referenceActive=true;original=Capture(phase+"-BuiltIn",true);_referenceActive=false;
   Check(_originalMask);Check(_originalEffect);Assert.That(_callbacks,Is.GreaterThan(calls));
  }
  finally
  {
   _referenceActive=false;if(GraphicsSettings.currentRenderPipeline==null)_fog.enabled=false;
   mask.shader=maskShader;effect.shader=effectShader;_ocean.OceanMaterial=_surface;
   GraphicsSettings.defaultRenderPipeline=_pipeline;QualitySettings.renderPipeline=_quality;_fog.enabled=true;
  }
  foreach(var ev in new[]{CameraEvent.BeforeDepthTexture,CameraEvent.BeforeGBuffer,CameraEvent.BeforeForwardAlpha,CameraEvent.AfterForwardAlpha})Assert.That(_camera.GetCommandBuffers(ev),Is.Empty);
  var restored=Capture(phase+"-URP-restored",false);int max=0,repeat=0;long total=0;
  for(int i=0;i<urp.Length;i++) {int r=Math.Abs(urp[i].r-original[i].r),g=Math.Abs(urp[i].g-original[i].g),b=Math.Abs(urp[i].b-original[i].b);max=Math.Max(max,Math.Max(r,Math.Max(g,b)));total+=r+g+b;if(!urp[i].Equals(restored[i]))repeat++;}
  double mean=(double)total/(urp.Length*3);
  File.AppendAllText(_report,$"Phase={phase}; Active={_fog.IsActive}; Camera={_camera.transform.position}; MaxChannelDifference={max}; MeanRgbByteDifference={mean:R}; RestoredChangedPixels={repeat}; OriginalCallbacks={_callbacks-calls}; FrameCount={frame}\n");
  Assert.That(OceanRenderer.FrameCount,Is.EqualTo(frame));Assert.That(repeat,Is.Zero);Assert.That(max,Is.LessThanOrEqualTo(2));Assert.That(mean,Is.LessThanOrEqualTo(.05));return urp;
 }
 Color32[] Capture(string label,bool builtin)
 {
  if(builtin) {_camera.targetTexture=_target;try {_camera.Render();}finally {_camera.targetTexture=null;}}
  else RenderPipeline.SubmitRenderRequest(_camera,new UniversalRenderPipeline.SingleCameraRequest{destination=_target});
  var prev=RenderTexture.active;var cpu=new Texture2D(Width,Height,TextureFormat.RGBA32,false);
  try {RenderTexture.active=_target;cpu.ReadPixels(new Rect(0,0,Width,Height),0,0);cpu.Apply();File.WriteAllBytes($"Evidence/UnderwaterReference-{label}-{SystemInfo.graphicsDeviceType}.png",cpu.EncodeToPNG());return cpu.GetPixels32();}
  finally {RenderTexture.active=prev;Object.DestroyImmediate(cpu);}
 }
 static int Different(Color32[] a,Color32[] b)=>a.Zip(b,(x,y)=>Math.Abs(x.r-y.r)+Math.Abs(x.g-y.g)+Math.Abs(x.b-y.b)>6?1:0).Sum();
 [UnityTearDown]
 public IEnumerator Cleanup()
 {
  _referenceActive=false;Camera.onPreRender-=PrepareOriginal;
  if(_fog!=null)_fog.enabled=false;GraphicsSettings.defaultRenderPipeline=_pipeline;QualitySettings.renderPipeline=_quality;
  OceanRenderer.EnableGraphicsInBatchMode=false;if(Application.isPlaying)yield return new ExitPlayMode();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  if(_target!=null)_target.Release();foreach(var obj in _owned)if(obj!=null)Object.DestroyImmediate(obj);_owned.Clear();
 }
}
