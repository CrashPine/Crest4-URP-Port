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

public class ShadowReferenceTests
{
 const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 readonly List<Object> _owned=new List<Object>();
 RenderPipelineAsset _pipeline,_quality;UniversalRenderPipelineAsset _testPipeline;OceanRenderer _ocean;Camera _camera;Light _sun;
 RenderTexture _target,_mask;CommandBuffer _copy;LodDataMgrShadow _shadow;
 int _captureRate,_callbacks;bool _original;string _report,_root;
 UnityEngine.ShadowQuality _shadowQuality;ShadowProjection _projection;UnityEngine.ShadowResolution _resolution;float _distance;int _cascades;
 [Serializable] class Source {public string path;public string sha256;}
 [Serializable] class Manifest {public Source[] files;}
 Dictionary<string,string> _sources;
 T Own<T>(T obj) where T:Object {_owned.Add(obj);return obj;}
 void Call(string name)=>typeof(LodDataMgrShadow).GetMethod(name,Private).Invoke(_shadow,null);
 static string Hash(byte[] data) {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","");}

 [UnityTest,Timeout(180000)]
 public IEnumerator OriginalLightEventsProduceIndependentMaskAndLodOcclusion()
 {
  _pipeline=GraphicsSettings.defaultRenderPipeline;_quality=QualitySettings.renderPipeline;
  _captureRate=Time.captureFramerate;_shadowQuality=QualitySettings.shadows;_projection=QualitySettings.shadowProjection;
  _resolution=QualitySettings.shadowResolution;_distance=QualitySettings.shadowDistance;_cascades=QualitySettings.shadowCascades;
  OceanRenderer.EnableGraphicsInBatchMode=true;EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
  yield return new EnterPlayMode();Assert.That(Application.isPlaying,Is.True);
  _report=$"Evidence/ShadowReference-{SystemInfo.graphicsDeviceType}.txt";
  File.WriteAllText(_report,"P09.4 independent original Built-in shadow INPUT validation, not final URP visual parity. Standard opaque receiver; separate ShadowsOnly caster; original UpdateShadow shader and LightEvent command buffers. Batch pre-cull shim bypasses focus/update guards only. No URP shadow atlas or LOD content reused. Hard directional, no jitter, frame weights1, captureFramerate60; target320x240, one cascade/distance100/2048 shadow map in both pipelines; different engine atlas fitting is not assumed identical.\n");
  _root=Path.GetFullPath("../../work/crest-upstream/crest/Assets/Crest/Crest/Shaders");
  _sources=JsonUtility.FromJson<Manifest>(File.ReadAllText("Evidence/P09-ShadowReference-sources.json")).files.ToDictionary(x=>x.path,x=>x.sha256);
  _testPipeline=Own(Object.Instantiate((UniversalRenderPipelineAsset)(_quality!=null?_quality:_pipeline)));
  _testPipeline.shadowDistance=100;_testPipeline.shadowCascadeCount=1;_testPipeline.mainLightShadowmapResolution=2048;
  Assert.That(_testPipeline.supportsMainLightShadows,Is.True);
  GraphicsSettings.defaultRenderPipeline=_testPipeline;QualitySettings.renderPipeline=_testPipeline;
  Time.captureFramerate=60;QualitySettings.shadows=UnityEngine.ShadowQuality.All;QualitySettings.shadowProjection=ShadowProjection.CloseFit;
  QualitySettings.shadowResolution=UnityEngine.ShadowResolution.VeryHigh;QualitySettings.shadowDistance=100;QualitySettings.shadowCascades=0;
  _ocean=OceanRenderer.Instance;_camera=Camera.main;
  foreach(var cam in Object.FindObjectsByType<Camera>())cam.enabled=false;
  foreach(var shape in Object.FindObjectsByType<ShapeWaves>())shape.enabled=false;
  _camera.transform.SetPositionAndRotation(new Vector3(0,24,0),Quaternion.Euler(90,0,0));
  _camera.orthographic=true;_camera.orthographicSize=20;_camera.aspect=4f/3;_camera.nearClipPlane=.1f;_camera.farClipPlane=100;
  _camera.cullingMask=(1<<4)|(1<<5);_camera.allowHDR=_camera.allowMSAA=false;_camera.renderingPath=RenderingPath.Forward;
  _camera.depthTextureMode=DepthTextureMode.Depth;_ocean.ViewCamera=_camera;
  _sun=RenderSettings.sun;Assert.That(_sun,Is.Not.Null);_ocean._primaryLight=_sun;
  _sun.transform.rotation=Quaternion.Euler(55,-30,0);_sun.shadows=LightShadows.Hard;_sun.shadowStrength=1;_sun.shadowCustomResolution=2048;_sun.shadowBias=_sun.shadowNormalBias=0;
  _sun.cullingMask=_camera.cullingMask;
  var surface=Own(new Material(_ocean.OceanMaterial));surface.EnableKeyword("_SHADOWS_ON");_ocean.OceanMaterial=surface;
  var settings=new SerializedObject(_ocean);settings.FindProperty("_createShadowData").boolValue=true;
  settings.FindProperty("_minScale").floatValue=8;settings.FindProperty("_maxScale").floatValue=8;settings.ApplyModifiedPropertiesWithoutUndo();
  var shadowSettings=Own(ScriptableObject.CreateInstance<SimSettingsShadow>());
  shadowSettings._jitterDiameterSoft=shadowSettings._jitterDiameterHard=0;
  shadowSettings._currentFrameWeightSoft=shadowSettings._currentFrameWeightHard=1;_ocean._simSettingsShadow=shadowSettings;
  var receiver=GameObject.CreatePrimitive(PrimitiveType.Plane);receiver.layer=5;receiver.transform.position=new Vector3(0,-6,0);receiver.transform.localScale=Vector3.one*10;
  receiver.GetComponent<Renderer>().sharedMaterial=Own(new Material(Shader.Find("Standard")));
  var caster=GameObject.CreatePrimitive(PrimitiveType.Cube);caster.layer=5;caster.transform.position=new Vector3(0,5,0);caster.transform.localScale=new Vector3(8,2,8);
  caster.GetComponent<Renderer>().sharedMaterial=receiver.GetComponent<Renderer>().sharedMaterial;
  caster.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.ShadowsOnly;
  caster.SetActive(false);
  for(int i=0;i<12;i++)yield return null;
  _shadow=_ocean._lodDataShadow;Assert.That(_shadow,Is.Not.Null);Assert.That(Time.deltaTime,Is.EqualTo(1f/60).Within(.00001));
  var shader=Own(ShaderUtil.CreateShaderAsset(new Regex(@"(?m)^Shader\s+""[^""]+""").Replace(Expand(Path.Combine(_root,"Resources/UpdateShadow.shader")),"Shader \"Hidden/Crest/Tests/OriginalUpdateShadow\"",1),false));
  var wrappers=(PropertyWrapperMaterial[])typeof(LodDataMgrShadow).GetField("_renderMaterial",Private).GetValue(_shadow);
  var previous=wrappers.Select(x=>x.material.shader).ToArray();
  _target=Own(new RenderTexture(320,240,24,RenderTextureFormat.ARGB32));_target.Create();
  _mask=Own(new RenderTexture(320,240,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear));_mask.Create();
  Camera.onPreCull+=PrepareOriginal;Camera.onPostRender+=FinishOriginal;
  byte[] originalOn=null;
  try
  {
   Call("CleanUpShadowCommandBuffers");GraphicsSettings.defaultRenderPipeline=null;QualitySettings.renderPipeline=null;
   Call("SetUpShadowCommandBuffers");for(int i=0;i<wrappers.Length;i++)wrappers[i].material.shader=shader;
   _copy=new CommandBuffer{name="P09 capture native Built-in screen shadow mask"};_copy.Blit(BuiltinRenderTextureType.CurrentActive,_mask);
   _sun.AddCommandBuffer(LightEvent.AfterScreenspaceMask,_copy);_original=true;
   // Hide water but preserve its layer in the camera mask, which is required by the original LOD collector.
   foreach(var chunk in _ocean.Root.GetComponentsInChildren<Renderer>())chunk.enabled=false;
   var off=Capture("Caster-off");caster.SetActive(true);var on=Capture("Caster-on");caster.SetActive(false);var restored=Capture("Caster-restored-off");
   originalOn=on.lod;
   var errors=ShaderUtil.GetShaderMessages(shader).Where(x=>x.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).Select(x=>x.message).ToArray();
   File.AppendAllText(_report,"ShaderErrors="+string.Join(";",errors)+"\n");Assert.That(errors,Is.Empty);
   int maskChanged=off.mask.Zip(on.mask,(a,b)=>Math.Abs(a.r-b.r)>32?1:0).Sum();
   int maskRestored=off.mask.Zip(restored.mask,(a,b)=>a.Equals(b)?0:1).Sum();
   int lodChanged=off.lod.Zip(on.lod,(a,b)=>b>a+32?1:0).Sum();
   int lodRestored=off.lod.Zip(restored.lod,(a,b)=>a==b?0:1).Sum();
   File.AppendAllText(_report,$"MaskChangedPixels={maskChanged}; MaskRestoredChangedPixels={maskRestored}; LodChangedChannels={lodChanged}; LodRestoredChangedChannels={lodRestored}; OriginalCallbacks={_callbacks}\n");
   Assert.That(maskChanged,Is.GreaterThan(100));Assert.That(lodChanged,Is.GreaterThan(100));
   Assert.That(maskRestored,Is.Zero);Assert.That(lodRestored,Is.Zero);Assert.That(_callbacks,Is.EqualTo(3));
  }
  finally
  {
   _original=false;if(_copy!=null){_sun.RemoveCommandBuffer(LightEvent.AfterScreenspaceMask,_copy);_copy.Release();_copy=null;}
   Call("RemoveCommandBufferFromPrimaryLight");Call("CleanUpShadowCommandBuffers");
   for(int i=0;i<wrappers.Length;i++)wrappers[i].material.shader=previous[i];
   GraphicsSettings.defaultRenderPipeline=_testPipeline;QualitySettings.renderPipeline=_testPipeline;
  }
  foreach(var ev in new[]{LightEvent.BeforeScreenspaceMask,LightEvent.AfterScreenspaceMask,LightEvent.AfterShadowMap})Assert.That(_sun.GetCommandBuffers(ev),Is.Empty);
  // Fresh URP atlas and cleared LOD targets, with an actual URP receiver. No original field is used as input.
  var receiverMaterial=Own(new Material(Shader.Find("Universal Render Pipeline/Lit")));
  receiver.GetComponent<Renderer>().sharedMaterial=receiverMaterial;caster.GetComponent<Renderer>().sharedMaterial=receiverMaterial;
  _camera.GetUniversalAdditionalCameraData().renderShadows=true;Call("SetUpShadowCommandBuffers");
  var urpOff=CaptureUrp("Caster-off");caster.SetActive(true);var urpOn=CaptureUrp("Caster-on");caster.SetActive(false);var urpRestored=CaptureUrp("Caster-restored-off");
  Assert.That(urpOff.All(x=>x==0),Is.True);Assert.That(urpRestored,Is.EqualTo(urpOff));
  ValidateInterior(originalOn,urpOn,new Bounds(caster.transform.position,caster.transform.localScale));
 }
 string Expand(string file)
 {
  file=Path.GetFullPath(file);string relative=Path.GetRelativePath(_root,file).Replace('\\','/');
  Assert.That(_sources.ContainsKey(relative),Is.True);Assert.That(Hash(File.ReadAllBytes(file)),Is.EqualTo(_sources[relative]));
  return Regex.Replace(File.ReadAllText(file),@"(?m)^\s*#include\s+""([^""]+)""[^\r\n]*",m=>
  {var child=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file),m.Groups[1].Value));return File.Exists(child)?"\n"+Expand(child)+"\n":m.Value;});
 }
 void PrepareOriginal(Camera camera)
 {
  if(!_original||camera!=_camera)return;_callbacks++;
  Call("AddCommandBufferToPrimaryLight");_shadow.BuildCommandBuffer(_ocean,_shadow.BufCopyShadowMap);
 }
 void FinishOriginal(Camera camera) {if(_original&&camera==_camera)Call("RemoveCommandBufferFromPrimaryLight");}
 byte[] CaptureUrp(string label)
 {
  _shadow.ClearLodData();_shadow.BufCopyShadowMap.Clear();
  RenderPipeline.SubmitRenderRequest(_camera,new UniversalRenderPipeline.SingleCameraRequest{destination=_target});
  var request=AsyncGPUReadback.Request(_shadow.DataTexture,0);request.WaitForCompletion();Assert.That(request.hasError,Is.False);
  Assert.That(request.layerCount,Is.EqualTo(_shadow.DataTexture.volumeDepth));
  var bytes=Enumerable.Range(0,request.layerCount).SelectMany(layer=>request.GetData<byte>(layer).ToArray()).ToArray();File.WriteAllBytes($"Evidence/ShadowReference-{label}-URP-lod-{SystemInfo.graphicsDeviceType}.bin",bytes);
  if(label=="Caster-on")SaveLodImage(bytes,"URP");
  File.AppendAllText(_report,$"URPPhase={label}; LodMax={bytes.Max()}; LodNonzero={bytes.Count(x=>x>0)}; LodSha256={Hash(bytes)}\n");return bytes;
 }
 void SaveLodImage(byte[] bytes,string pipeline)
 {
  int w=_shadow.DataTexture.width,h=_shadow.DataTexture.height;var cpu=new Texture2D(w,h,TextureFormat.RGBA32,false,true);
  try {var colors=new Color32[w*h];for(int i=0;i<colors.Length;i++)colors[i]=new Color32(bytes[i*2],bytes[i*2+1],0,255);cpu.SetPixels32(colors);cpu.Apply();File.WriteAllBytes($"Evidence/ShadowReference-Caster-on-{pipeline}-lod0-{SystemInfo.graphicsDeviceType}.png",cpu.EncodeToPNG());}
  finally {Object.DestroyImmediate(cpu);}
 }
 void ValidateInterior(byte[] original,byte[] urp,Bounds caster)
 {
  Assert.That(urp.Length,Is.EqualTo(original.Length));int width=_shadow.DataTexture.width,height=_shadow.DataTexture.height;
  Assert.That(original.Length,Is.EqualTo(width*height*_shadow.DataTexture.volumeDepth*2));
  var inner=caster;inner.Expand(new Vector3(-2,0,-2));var outer=caster;outer.Expand(new Vector3(2,0,2));
  int inside=0,outside=0,boundary=0,errors=0,pairChanged=0,maxDifference=0,interiorChanged=0;
  for(int lod=0;lod<_shadow.DataTexture.volumeDepth;lod++)for(int y=0;y<height;y++)for(int x=0;x<width;x++)
  {
   var data=_ocean._lodTransform._renderData[lod].Current;
   var pos=data._posSnapped+new Vector3(((x+.5f)/width-.5f)*_ocean.CalcLodScale(lod)*4,0,((y+.5f)/height-.5f)*_ocean.CalcLodScale(lod)*4);pos.y=0;
   var screen=_camera.WorldToViewportPoint(pos);if(screen.z<=0||screen.x<.025f||screen.x>.975f||screen.y<.025f||screen.y>.975f)continue;
   var ray=new Ray(pos,-_sun.transform.forward);bool occluded=inner.IntersectRay(ray);bool lit=!outer.IntersectRay(ray);
   if(occluded)inside++;else if(lit)outside++;else boundary++;
   for(int channel=0;channel<2;channel++)
   {
    int index=((lod*height+y)*width+x)*2+channel;int a=original[index],b=urp[index];
    if(a!=b){pairChanged++;if(occluded||lit)interiorChanged++;}maxDifference=Math.Max(maxDifference,Math.Abs(a-b));
    if((occluded&&(a<250||b<250))||(lit&&(a>5||b>5)))errors++;
   }
  }
  File.AppendAllText(_report,$"InteriorShadowSamples={inside}; InteriorLitSamples={outside}; BoundarySamples={boundary}; InteriorClassificationErrors={errors}; InteriorChangedChannels={interiorChanged}; WholeViewChangedChannels={pairChanged}; WholeViewMaxDifference={maxDifference}; AnalyticMarginXZ=1m; CameraViewportMargin=.025; ExpectedShadowMin=250; ExpectedLitMax=5\n");
  Assert.That(inside,Is.GreaterThan(100));Assert.That(outside,Is.GreaterThan(100));Assert.That(boundary,Is.GreaterThan(0));
  Assert.That(interiorChanged,Is.Zero);
  Assert.That(errors,Is.Zero,"Independently rendered fields must agree with analytic caster interiors; boundary differences are reported separately.");
 }
 (Color32[] mask,byte[] lod) Capture(string label)
 {
  _shadow.ClearLodData();_shadow.BufCopyShadowMap.Clear();
  var previous=RenderTexture.active;RenderTexture.active=_mask;GL.Clear(false,true,Color.magenta);RenderTexture.active=previous;
  _camera.targetTexture=_target;try {_camera.Render();}finally {_camera.targetTexture=null;}
  var cpu=new Texture2D(320,240,TextureFormat.RGBA32,false,true);
  try
  {
   RenderTexture.active=_mask;cpu.ReadPixels(new Rect(0,0,320,240),0,0);cpu.Apply();var pixels=cpu.GetPixels32();
   File.WriteAllBytes($"Evidence/ShadowReference-{label}-mask-{SystemInfo.graphicsDeviceType}.png",cpu.EncodeToPNG());
   Assert.That(pixels.Count(x=>x.r>250&&x.b>250&&x.g<5),Is.Zero,"LightEvent did not overwrite sentinel mask.");
   var request=AsyncGPUReadback.Request(_shadow.DataTexture,0);request.WaitForCompletion();Assert.That(request.hasError,Is.False);
   Assert.That(request.layerCount,Is.EqualTo(_shadow.DataTexture.volumeDepth));
  var bytes=Enumerable.Range(0,request.layerCount).SelectMany(layer=>request.GetData<byte>(layer).ToArray()).ToArray();File.WriteAllBytes($"Evidence/ShadowReference-{label}-lod-{SystemInfo.graphicsDeviceType}.bin",bytes);
   if(label=="Caster-on")SaveLodImage(bytes,"BuiltIn");
   File.AppendAllText(_report,$"Phase={label}; MaskMin={pixels.Min(x=>x.r)}; MaskMax={pixels.Max(x=>x.r)}; LodMax={bytes.Max()}; LodNonzero={bytes.Count(x=>x>0)}; LodSha256={Hash(bytes)}; DeltaTime={Time.deltaTime:R}\n");
   return(pixels,bytes);
  }
  finally {RenderTexture.active=previous;Object.DestroyImmediate(cpu);}
 }
 [UnityTearDown] public IEnumerator Cleanup()
 {
  _original=false;Camera.onPreCull-=PrepareOriginal;Camera.onPostRender-=FinishOriginal;
  if(_copy!=null){if(_sun!=null)_sun.RemoveCommandBuffer(LightEvent.AfterScreenspaceMask,_copy);_copy.Release();_copy=null;}
  GraphicsSettings.defaultRenderPipeline=_pipeline;QualitySettings.renderPipeline=_quality;Time.captureFramerate=_captureRate;
  QualitySettings.shadows=_shadowQuality;QualitySettings.shadowProjection=_projection;QualitySettings.shadowResolution=_resolution;QualitySettings.shadowDistance=_distance;QualitySettings.shadowCascades=_cascades;
  OceanRenderer.EnableGraphicsInBatchMode=false;if(Application.isPlaying)yield return new ExitPlayMode();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  foreach(var obj in _owned)if(obj!=null){if(obj is RenderTexture rt)rt.Release();Object.DestroyImmediate(obj);}_owned.Clear();
 }
}
