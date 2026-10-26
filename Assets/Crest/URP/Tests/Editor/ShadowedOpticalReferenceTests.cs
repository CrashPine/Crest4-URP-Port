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

public class ShadowedOpticalReferenceTests
{
 const int Width=320,Height=240;
 const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 [Serializable] class Source {public string path;public string sha256;}
 [Serializable] class Manifest {public Source[] files;}
 readonly List<Object> _owned=new List<Object>();
 RenderPipelineAsset _pipeline,_quality;
 OceanRenderer _ocean;Camera _camera;UnderwaterRenderer _fog;TimeProviderCustom _clock;
 Material _surface,_originalSurface;Shader _originalMask,_originalEffect,_originalShadow;
 RenderTexture _target;string _report,_root,_includeRoot;Dictionary<string,string> _sources;
 bool _referenceActive;int _callbacks;
 T Own<T>(T obj) where T:Object {_owned.Add(obj);return obj;}
 static FieldInfo Field(string name)=>typeof(UnderwaterRenderer).GetField(name,Private);
 Material Material(string field)=>((PropertyWrapperMaterial)Field(field).GetValue(_fog)).material;
 void Call(string name)=>typeof(UnderwaterRenderer).GetMethod(name,Private).Invoke(_fog,null);

 UniversalRenderPipelineAsset _testPipeline;LodDataMgrShadow _shadow;GameObject _caster;Light _sun;
 int _captureRate;UnityEngine.ShadowQuality _oldShadows;ShadowProjection _oldProjection;UnityEngine.ShadowResolution _oldResolution;float _oldDistance;int _oldCascades;
 readonly List<string> _errors=new List<string>();bool _below;
 Renderer _receiver;Material _receiverOriginal,_receiverUrp;
 Bounds CasterBounds=>new Bounds(_caster.transform.position,_caster.transform.localScale);
 void ShadowCall(string name)=>typeof(LodDataMgrShadow).GetMethod(name,Private).Invoke(_shadow,null);
 void ShadowPreCull(Camera camera) {if(_referenceActive&&camera==_camera){ShadowCall("AddCommandBufferToPrimaryLight");_shadow.BuildCommandBuffer(_ocean,_shadow.BufCopyShadowMap);}}
 void ShadowPostRender(Camera camera) {if(_referenceActive&&camera==_camera)ShadowCall("RemoveCommandBufferFromPrimaryLight");}

 [UnityTest,Timeout(240000)]
 public IEnumerator OriginalShadowedCausticsAndUnderwaterLightingMatchInteriors()
 {
  _pipeline=GraphicsSettings.defaultRenderPipeline;_quality=QualitySettings.renderPipeline;
  _captureRate=Time.captureFramerate;_oldShadows=QualitySettings.shadows;_oldProjection=QualitySettings.shadowProjection;_oldResolution=QualitySettings.shadowResolution;_oldDistance=QualitySettings.shadowDistance;_oldCascades=QualitySettings.shadowCascades;
  OceanRenderer.EnableGraphicsInBatchMode=true;EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
  yield return new EnterPlayMode();Assert.That(Application.isPlaying,Is.True);
  _report=$"Evidence/ShadowedOpticalReference-{SystemInfo.graphicsDeviceType}.txt";
  File.WriteAllText(_report,"P09.4 shadowed original surface/underwater comparison. Independently cleared/rendered BIRP native shadow map, screen mask and original UpdateShadow LOD shaders vs URP graph/atlas. Matching opaque receiver colour with BIRP ForwardBase shadow receiver (receive strength0); caustic shadow occlusion remains active in original Crest. Independent BIRP render starts with original white screen-shadow fallback, native collector overwrites it when active. Below view uses independently rendered above-water native LOD history copied to previous buffer (same cascade metadata, no cross-pipeline shadow data). Predefined interior max2/mean.05 RGB byte tolerance, exact URP restoration. Analytic caster XZ margin1m, viewport margin.025; both floor and water shadow boundaries excluded for above-water interiors. Whole-frame metrics retained. Hard light, one cascade/distance100/2048, zero jitter/weights1, Time.deltaTime1/60, ocean time1/delta0, flat water, refraction strength0.\n");
  _root=Path.GetFullPath("../../work/crest-upstream/crest/Assets/Crest/Crest/Shaders");
  _sources=JsonUtility.FromJson<Manifest>(File.ReadAllText("Evidence/P09-ShadowedOpticalReference-sources.json")).files.ToDictionary(x=>x.path,x=>x.sha256);
  _testPipeline=Own(Object.Instantiate((UniversalRenderPipelineAsset)(_quality!=null?_quality:_pipeline)));
  _testPipeline.shadowDistance=100;_testPipeline.shadowCascadeCount=1;_testPipeline.mainLightShadowmapResolution=2048;
  GraphicsSettings.defaultRenderPipeline=_testPipeline;QualitySettings.renderPipeline=_testPipeline;
  Time.captureFramerate=60;QualitySettings.shadows=UnityEngine.ShadowQuality.All;QualitySettings.shadowProjection=ShadowProjection.CloseFit;QualitySettings.shadowResolution=UnityEngine.ShadowResolution.VeryHigh;QualitySettings.shadowDistance=100;QualitySettings.shadowCascades=0;
  _ocean=OceanRenderer.Instance;_camera=Camera.main;
  foreach(var cam in Object.FindObjectsByType<Camera>())cam.enabled=false;
  foreach(var shape in Object.FindObjectsByType<ShapeWaves>())shape.enabled=false;
  _camera.fieldOfView=60;_camera.aspect=(float)Width/Height;_camera.orthographic=false;_camera.nearClipPlane=.1f;_camera.farClipPlane=150;_camera.cullingMask=(1<<4)|(1<<5);
  _camera.clearFlags=CameraClearFlags.SolidColor;_camera.backgroundColor=new Color(.17f,.24f,.35f);_camera.allowHDR=_camera.allowMSAA=false;_camera.depthTextureMode=DepthTextureMode.Depth;_camera.renderingPath=RenderingPath.Forward;
  var data=_camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=false;data.renderShadows=true;data.requiresDepthTexture=true;data.taaSettings.mipBias=0;
  _ocean.ViewCamera=_camera;RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.2f,.2f,.2f);
  _sun=RenderSettings.sun;_ocean._primaryLight=_sun;_sun.transform.rotation=Quaternion.Euler(55,-30,0);_sun.shadows=LightShadows.Hard;_sun.shadowStrength=1;_sun.shadowCustomResolution=2048;_sun.shadowBias=_sun.shadowNormalBias=0;_sun.cullingMask=_camera.cullingMask;_sun.intensity=1;_sun.color=Color.white;
  _surface=Own(new Material(_ocean.OceanMaterial));
  foreach(var key in new[]{"_FOAM_ON","_CAUSTICS_ON","_APPLYNORMALMAPPING_ON","_CLIPSURFACE_ON"})_surface.DisableKeyword(key);
  _surface.EnableKeyword("_UNDERWATER_ON");_surface.EnableKeyword("_SHADOWS_ON");_surface.EnableKeyword("_TRANSPARENCY_ON");
  _surface.SetFloat("_Underwater",1);_surface.SetFloat("_Shadows",1);_surface.SetFloat("_Transparency",1);_surface.SetFloat("_RefractionStrength",0);
  _surface.SetFloat("_Foam",0);_surface.SetFloat("_Caustics",0);_surface.SetFloat("_ApplyNormalMapping",0);
  _surface.SetVector("_DepthFogDensity",new Vector4(.06f,.035f,.02f,1));_surface.SetColor("_DiffuseShadow",new Color(.025f,.04f,.055f));
  _surface.SetFloat("_CausticsFocalDepth",8);_surface.SetFloat("_CausticsDepthOfField",10);_surface.SetFloat("_CausticsStrength",3.2f);_ocean.OceanMaterial=_surface;
  var settings=new SerializedObject(_ocean);settings.FindProperty("_minScale").floatValue=8;settings.FindProperty("_maxScale").floatValue=8;settings.FindProperty("_createShadowData").boolValue=true;
  settings.FindProperty("_createFoamSim").boolValue=false;settings.FindProperty("_createClipSurfaceData").boolValue=false;settings.ApplyModifiedPropertiesWithoutUndo();
  var animated=Own(Object.Instantiate(_ocean._lodDataAnimWaves.Settings));animated.CollisionSource=SimSettingsAnimatedWaves.CollisionSources.None;_ocean._simSettingsAnimatedWaves=animated;
  var depth=Own(ScriptableObject.CreateInstance<SimSettingsSeaFloorDepth>());depth._allowVaryingWaterLevel=false;_ocean._simSettingsSeaFloorDepth=depth;
  var shadowSettings=Own(ScriptableObject.CreateInstance<SimSettingsShadow>());shadowSettings._jitterDiameterSoft=shadowSettings._jitterDiameterHard=0;shadowSettings._currentFrameWeightSoft=shadowSettings._currentFrameWeightHard=1;_ocean._simSettingsShadow=shadowSettings;
  var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.layer=5;floor.transform.position=new Vector3(0,-8,0);floor.transform.localScale=Vector3.one*20;
  var receiver=Own(new Material(Shader.Find("Hidden/Crest/Tests/BuiltInShadowReceiver")));receiver.SetColor("_Color",new Color(.65f,.5f,.3f));receiver.SetFloat("_ReceiveShadowStrength",0);_receiver=floor.GetComponent<Renderer>();_receiver.shadowCastingMode=ShadowCastingMode.Off;_receiverOriginal=receiver;
  _receiverUrp=Own(new Material(Shader.Find("Hidden/Crest/Tests/DualPipelineOpaque")));_receiverUrp.SetColor("_Color",receiver.GetColor("_Color"));_receiver.sharedMaterial=_receiverUrp;
  _caster=GameObject.CreatePrimitive(PrimitiveType.Cube);_caster.layer=5;_caster.transform.position=new Vector3(0,5,0);_caster.transform.localScale=new Vector3(8,2,8);_caster.GetComponent<Renderer>().sharedMaterial=_receiverUrp;_caster.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.ShadowsOnly;_caster.SetActive(false);
  var input=GameObject.CreatePrimitive(PrimitiveType.Plane);input.transform.position=new Vector3(0,-8,0);input.transform.localScale=Vector3.one*20;input.GetComponent<Renderer>().sharedMaterial=Own(new Material(Shader.Find("Crest/Inputs/Depth/Ocean Depth From Geometry")));input.AddComponent<RegisterSeaFloorDepthInput>();
  SetView(false);for(int i=0;i<12;i++)yield return null;
  _clock=_ocean.gameObject.AddComponent<TimeProviderCustom>();_clock._overrideTime=_clock._overrideDeltaTime=true;_clock._time=1;_clock._deltaTime=0;_ocean.PushTimeProvider(_clock);
  _target=Own(new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32));_target.Create();
  _fog=_camera.gameObject.AddComponent<UnderwaterRenderer>();_fog.EnableShaderAPI=false;_fog._copyOceanMaterialParamsEachFrame=true;
  _originalSurface=Own(new Material(_surface){shader=Load("Ocean.shader","Surface")});_originalSurface.CopyPropertiesFromMaterial(_surface);
  _originalMask=Load("Underwater/Resources/UnderwaterMask.shader","Mask");_originalEffect=Load("Underwater/Resources/UnderwaterEffect.shader","Effect");_originalShadow=Load("Resources/UpdateShadow.shader","Shadow");
  Camera.onPreRender+=PrepareOriginal;Camera.onPreCull+=ShadowPreCull;Camera.onPostRender+=ShadowPostRender;
  for(int i=0;i<8;i++)yield return null;_shadow=_ocean._lodDataShadow;Assert.That(_shadow,Is.Not.Null);
  foreach(bool below in new[]{false,true})
  {
   SetView(below);for(int i=0;i<12;i++)yield return null;
   string view=below?"Below":"Above";_surface.EnableKeyword("_SHADOWS_ON");_surface.DisableKeyword("_CAUSTICS_ON");_caster.SetActive(false);
   var baseline=Compare(view+"-NoCaster-NoCaustics",true);
   _caster.SetActive(true);var shadow=Compare(view+"-Caster-NoCaustics",false);
   _surface.EnableKeyword("_CAUSTICS_ON");_surface.SetFloat("_Caustics",1);var caustics=Compare(view+"-Caster-Caustics",false);
   _caster.SetActive(false);var lit=Compare(view+"-NoCaster-Caustics",true);
   Controls(view,baseline,shadow,caustics,lit);
   _surface.DisableKeyword("_SHADOWS_ON");var noShadows=Compare(view+"-NoCaster-ShadowKeywordOff",true);
   Assert.That(Different(lit.urp,noShadows.urp),Is.Zero);Assert.That(Different(lit.original,noShadows.original),Is.Zero);
  }
  Assert.That(_errors,Is.Empty,"All predefined original-reference comparisons and feature controls must pass.");
 }
 void SetView(bool below)
 {
  _below=below;
  Vector3 position=below?new Vector3(-1.75052f,-3,3.032f):new Vector3(0,8,-16);
  Vector3 lookAt=below?new Vector3(-4.55f,-8,7.88f):new Vector3(0,-8,4);
  _camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(lookAt-position));
 }
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
  _includeRoot=Path.GetDirectoryName(Path.Combine(_root,file));string code=Expand(Path.Combine(_root,file));code=new Regex(@"(?m)^Shader\s+""[^""]+""").Replace(code,"Shader \"Hidden/Crest/Tests/OriginalShadowedOptical"+label+"\"",1);
  var shader=Own(ShaderUtil.CreateShaderAsset(code,false));Check(shader);return shader;
 }
 void Check(Shader shader)
 {
  var errors=ShaderUtil.GetShaderMessages(shader).Where(x=>x.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).Select(x=>x.message).ToArray();
  File.AppendAllText(_report,"ShaderErrors="+string.Join(";",errors)+"\n");Assert.That(errors,Is.Empty);
 }
 void PrepareOriginal(Camera camera)
 {
  if(!_referenceActive||camera!=_camera||!_fog.enabled)return;
  _callbacks++;
  if(!_fog.IsActive)
  {
   foreach(string name in new[]{"_oceanMaskCommandBuffer","_underwaterEffectCommandBuffer"})((CommandBuffer)Field(name).GetValue(_fog)).Clear();return;
  }
  Field("_gpuInverseViewProjectionMatrix").SetValue(_fog,(GL.GetGPUProjectionMatrix(_camera.projectionMatrix,false)*_camera.worldToCameraMatrix).inverse);
  Field("_cameraFrustumPlanes").SetValue(_fog,GeometryUtility.CalculateFrustumPlanes(_camera));
  Call("UpdateOceanRendererStateForCamera");Call("OnPreRenderOceanMask");Call("OnPreRenderUnderwaterEffect");
 }

 (Color32[] urp,Color32[] original) Compare(string phase,bool wholeRequired)
 {
  int frame=OceanRenderer.FrameCount;Assert.That(_ocean.TimeProvider,Is.SameAs(_clock));Assert.That(_ocean.CurrentTime,Is.EqualTo(1));Assert.That(_ocean.DeltaTime,Is.Zero);
  _originalSurface.CopyPropertiesFromMaterial(_surface);var urp=Capture(phase+"-URP",false);
  var mask=Material("_oceanMaskMaterial");var effect=Material("_underwaterEffectMaterial");var maskShader=mask.shader;var effectShader=effect.shader;
  var wrappers=(PropertyWrapperMaterial[])typeof(LodDataMgrShadow).GetField("_renderMaterial",Private).GetValue(_shadow);var shadowShaders=wrappers.Select(x=>x.material.shader).ToArray();
  Color32[] original=null;int calls=_callbacks;
  try
  {
   _fog.enabled=false;ShadowCall("CleanUpShadowCommandBuffers");GraphicsSettings.defaultRenderPipeline=null;QualitySettings.renderPipeline=null;
   ShadowCall("SetUpShadowCommandBuffers");for(int i=0;i<wrappers.Length;i++)wrappers[i].material.shader=_originalShadow;
   _receiver.sharedMaterial=_receiverOriginal;_caster.GetComponent<Renderer>().sharedMaterial=_receiverOriginal;
   _ocean.OceanMaterial=_originalSurface;_fog.enabled=true;mask.shader=_originalMask;effect.shader=_originalEffect;
   _referenceActive=true;original=Capture(phase+"-BuiltIn",true);_referenceActive=false;Check(_receiverOriginal.shader);Check(_receiverUrp.shader);Check(_originalSurface.shader);Check(_originalMask);Check(_originalEffect);Check(_originalShadow);Assert.That(_callbacks-calls,Is.EqualTo(1));
  }
  finally
  {
   _referenceActive=false;if(GraphicsSettings.currentRenderPipeline==null)_fog.enabled=false;
   ShadowCall("RemoveCommandBufferFromPrimaryLight");ShadowCall("CleanUpShadowCommandBuffers");
   for(int i=0;i<wrappers.Length;i++)wrappers[i].material.shader=shadowShaders[i];mask.shader=maskShader;effect.shader=effectShader;_ocean.OceanMaterial=_surface;
   _receiver.sharedMaterial=_receiverUrp;_caster.GetComponent<Renderer>().sharedMaterial=_receiverUrp;
   GraphicsSettings.defaultRenderPipeline=_testPipeline;QualitySettings.renderPipeline=_testPipeline;ShadowCall("SetUpShadowCommandBuffers");_fog.enabled=true;
  }
  foreach(var ev in new[]{CameraEvent.BeforeDepthTexture,CameraEvent.BeforeGBuffer,CameraEvent.BeforeForwardAlpha,CameraEvent.AfterForwardAlpha})Assert.That(_camera.GetCommandBuffers(ev),Is.Empty);
  foreach(var ev in new[]{LightEvent.BeforeScreenspaceMask,LightEvent.AfterScreenspaceMask,LightEvent.AfterShadowMap})Assert.That(_sun.GetCommandBuffers(ev),Is.Empty);
  var restored=Capture(phase+"-URP-restored",false);int max=0,interiorMax=0,repeat=0,count=0,changed=0,boundaryChanged=0;long total=0,interiorTotal=0;
  for(int i=0;i<urp.Length;i++)
  {
   int r=Math.Abs(urp[i].r-original[i].r),g=Math.Abs(urp[i].g-original[i].g),b=Math.Abs(urp[i].b-original[i].b),m=Math.Max(r,Math.Max(g,b));max=Math.Max(max,m);total+=r+g+b;
   bool inside=Interior(i,out bool occluded);if(inside){count++;interiorMax=Math.Max(interiorMax,m);interiorTotal+=r+g+b;}else if(r+g+b>6)boundaryChanged++;
   if(r+g+b>6)changed++;if(!urp[i].Equals(restored[i]))repeat++;
  }
  double mean=(double)total/(urp.Length*3),interiorMean=(double)interiorTotal/Math.Max(1,count*3);
  File.AppendAllText(_report,$"Phase={phase}; FogActive={_fog.IsActive}; WholeMax={max}; WholeMean={mean:R}; WholeChangedOverSix={changed}; InteriorPixels={count}; InteriorMax={interiorMax}; InteriorMean={interiorMean:R}; OutsideInteriorChangedOverSix={boundaryChanged}; RestoreChangedPixels={repeat}; FrameCount={frame}\n");
  Assert.That(OceanRenderer.FrameCount,Is.EqualTo(frame));Assert.That(repeat,Is.Zero);Assert.That(count,Is.GreaterThan(1000));
  if(interiorMax>2||interiorMean>.05)_errors.Add($"{phase}: interior max={interiorMax}, mean={interiorMean:R}");
  if(wholeRequired&&(max>2||mean>.05))_errors.Add($"{phase}: whole max={max}, mean={mean:R}");
  return(urp,original);
 }
 bool Interior(int index,out bool shadowed)
 {
  shadowed=false;int x=index%Width,y=index/Width;float u=(x+.5f)/Width,v=(y+.5f)/Height;
  if(u<.025f||u>.975f||v<.025f||v>.975f)return false;
  var ray=_camera.ViewportPointToRay(new Vector3(u,v));if(ray.direction.y>=0)return false;
  Vector3 floor=ray.origin+ray.direction*((-8-ray.origin.y)/ray.direction.y);
  if(Math.Abs(floor.x)>98||Math.Abs(floor.z)>98)return false;
  var inner=CasterBounds;inner.Expand(new Vector3(-2,0,-2));var outer=CasterBounds;outer.Expand(new Vector3(2,0,2));
  var lightRay=new Ray(floor,-_sun.transform.forward);shadowed=inner.IntersectRay(lightRay);bool lit=!outer.IntersectRay(lightRay);if(!shadowed&&!lit)return false;
  if(!_below)
  {
   Vector3 water=ray.origin+ray.direction*(-ray.origin.y/ray.direction.y);lightRay=new Ray(water,-_sun.transform.forward);
   if(!inner.IntersectRay(lightRay)&&outer.IntersectRay(lightRay))return false;
  }
  return true;
 }
 void Controls(string view,(Color32[] urp,Color32[] original) baseline,(Color32[] urp,Color32[] original) shadow,(Color32[] urp,Color32[] original) caustics,(Color32[] urp,Color32[] original) lit)
 {
  foreach(bool original in new[]{false,true})
  {
   var a=original?baseline.original:baseline.urp;var b=original?shadow.original:shadow.urp;var c=original?caustics.original:caustics.urp;var d=original?lit.original:lit.urp;
   int shadowPixels=Different(a,b),causticPixels=0,occluded=0,occludedChanged=0,litChanged=0;
   for(int i=0;i<a.Length;i++)if(Interior(i,out bool inside))
   {
    int effect=Math.Abs(c[i].r-b[i].r)+Math.Abs(c[i].g-b[i].g)+Math.Abs(c[i].b-b[i].b);
    if(inside){occluded++;if(effect>6)occludedChanged++;}
    else {if(effect>6)causticPixels++;if(Math.Abs(d[i].r-a[i].r)+Math.Abs(d[i].g-a[i].g)+Math.Abs(d[i].b-a[i].b)>6)litChanged++;}
   }
   File.AppendAllText(_report,$"Controls={view}; Original={original}; ShadowChangedPixels={shadowPixels}; LitCausticChangedPixels={causticPixels}; NoCasterLitCausticChangedPixels={litChanged}; OccludedInteriorPixels={occluded}; OccludedCausticChangedPixels={occludedChanged}\n");
   if(shadowPixels<=100||causticPixels<=100||litChanged<=100||occluded<=100||occludedChanged!=0)_errors.Add($"{view} original={original}: feature controls failed");
  }
 }
 void RenderCamera(bool builtin)
 {
  if(builtin)
  {
   // A new independent render starts from original white/no-shadow fallback.
   // Original AfterScreenspaceMask overrides this when the native collector executes.
   LodDataMgrShadow.BindScreenSpaceNullToGraphicsShaders(_camera);
   _camera.targetTexture=_target;try {_camera.Render();}finally {_camera.targetTexture=null;}
  }
  else RenderPipeline.SubmitRenderRequest(_camera,new UniversalRenderPipeline.SingleCameraRequest{destination=_target});
 }
 Color32[] Capture(string label,bool builtin)
 {
  _shadow.ClearLodData();_shadow.BufCopyShadowMap.Clear();
  if(_below)
  {
   // The original collector only updates visible water positions. Seed history from
   // the same native pipeline's above-water view, never from the other pipeline.
   var position=_camera.transform.position;var rotation=_camera.transform.rotation;bool enabled=_fog.enabled;
   _fog.enabled=false;_camera.transform.SetPositionAndRotation(new Vector3(0,8,-16),Quaternion.LookRotation(new Vector3(0,-16,20)));
   try {RenderCamera(builtin);Graphics.CopyTexture(_shadow.DataTexture,_shadow.GetDataTexture(1));}
   finally {_camera.transform.SetPositionAndRotation(position,rotation);_fog.enabled=enabled;}
   _shadow.BufCopyShadowMap.Clear();
  }
  RenderCamera(builtin);
  var prev=RenderTexture.active;var cpu=new Texture2D(Width,Height,TextureFormat.RGBA32,false);
  try {RenderTexture.active=_target;cpu.ReadPixels(new Rect(0,0,Width,Height),0,0);cpu.Apply();File.WriteAllBytes($"Evidence/ShadowedOpticalReference-{label}-{SystemInfo.graphicsDeviceType}.png",cpu.EncodeToPNG());return cpu.GetPixels32();}
  finally {RenderTexture.active=prev;Object.DestroyImmediate(cpu);}
 }
 static int Different(Color32[] a,Color32[] b)=>a.Zip(b,(x,y)=>Math.Abs(x.r-y.r)+Math.Abs(x.g-y.g)+Math.Abs(x.b-y.b)>6?1:0).Sum();
 [UnityTearDown] public IEnumerator Cleanup()
 {
  _referenceActive=false;Camera.onPreRender-=PrepareOriginal;Camera.onPreCull-=ShadowPreCull;Camera.onPostRender-=ShadowPostRender;
  if(_fog!=null)_fog.enabled=false;GraphicsSettings.defaultRenderPipeline=_pipeline;QualitySettings.renderPipeline=_quality;
  Time.captureFramerate=_captureRate;QualitySettings.shadows=_oldShadows;QualitySettings.shadowProjection=_oldProjection;QualitySettings.shadowResolution=_oldResolution;QualitySettings.shadowDistance=_oldDistance;QualitySettings.shadowCascades=_oldCascades;
  OceanRenderer.EnableGraphicsInBatchMode=false;if(Application.isPlaying)yield return new ExitPlayMode();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  if(_target!=null)_target.Release();foreach(var obj in _owned)if(obj!=null)Object.DestroyImmediate(obj);_owned.Clear();
 }
}
