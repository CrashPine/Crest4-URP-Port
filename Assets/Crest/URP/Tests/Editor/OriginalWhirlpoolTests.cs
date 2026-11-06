// Crest 4 URP port. MIT; see LICENSE.Crest.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Crest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
public class OriginalWhirlpoolTests
{
    RenderTexture _target; string _stem; Vector3 _position; Quaternion _rotation; int _capture;float _scale;
    Vector4[] Read(RenderTexture texture)
    {
        var read=AsyncGPUReadback.Request(texture,0,TextureFormat.RGBAFloat);read.WaitForCompletion();Assert.That(read.hasError,Is.False);Assert.That(read.layerCount,Is.EqualTo(7));
        var values=Enumerable.Range(0,read.layerCount).SelectMany(i=>read.GetData<Vector4>(i).ToArray()).ToArray();Assert.That(values.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)&&float.IsFinite(v.w)),Is.True);return values;
    }
    Color32[] Capture(Camera camera,string state)
    {
        camera.transform.SetPositionAndRotation(_position,_rotation);
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=_target});var previous=RenderTexture.active;var cpu=new Texture2D(640,360,TextureFormat.RGBA32,false);
        try{RenderTexture.active=_target;cpu.ReadPixels(new Rect(0,0,640,360),0,0);cpu.Apply();var pixels=cpu.GetPixels32();File.WriteAllBytes(_stem+"-"+state+".png",cpu.EncodeToPNG());Assert.That(pixels.Count(c=>c.r>245&&c.g<10&&c.b>245),Is.Zero);return pixels;}
        finally{RenderTexture.active=previous;Object.DestroyImmediate(cpu);}
    }
    [UnityTest,Timeout(240000)] public IEnumerator NativeWhirlpoolDisplacementAndFlowInputsChangeAndRestore()
    {
        _capture=Time.captureFramerate;_scale=Time.timeScale;OceanRenderer.EnableGraphicsInBatchMode=true;EditorSceneManager.OpenScene("Assets/Crest/Crest-Examples/Whirlpool/Scenes/whirlpool.unity");
        foreach(var shape in Object.FindObjectsByType<ShapeWaves>())shape.enabled=false;
        var setupOcean=Object.FindObjectsByType<OceanRenderer>().Single();var serialized=new SerializedObject(setupOcean);serialized.FindProperty("_createDynamicWaveSim").boolValue=false;serialized.FindProperty("_createFoamSim").boolValue=false;serialized.ApplyModifiedPropertiesWithoutUndo();
        yield return new EnterPlayMode();Time.captureFramerate=60;Time.timeScale=1;_stem=$"Evidence/OriginalWhirlpool-{SystemInfo.graphicsDeviceType}";
        var ocean=OceanRenderer.Instance;var camera=Camera.main;ocean.ViewCamera=camera;var whirl=Object.FindObjectsByType<Whirlpool>().Single();
        new GameObject("Whirlpool query pacing").AddComponent<BoatProbesQueryFence>();
        for(int i=0;i<60;i++)yield return null;
        var clock=new GameObject("Whirlpool fixed ocean clock").AddComponent<TimeProviderCustom>();clock._overrideTime=clock._overrideDeltaTime=clock._paused=true;clock._time=1;clock._deltaTime=0;ocean.PushTimeProvider(clock);
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var displacement=((GameObject)typeof(Whirlpool).GetField("_displacementInput",flags).GetValue(whirl)).GetComponent<RegisterAnimWavesInput>();
        var flow=((GameObject)typeof(Whirlpool).GetField("_flowInput",flags).GetValue(whirl)).GetComponent<RegisterFlowInput>();Assert.That(displacement.enabled&&flow.enabled,Is.True);
        camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;camera.allowMSAA=false;camera.aspect=16f/9;
        Time.timeScale=0;for(int i=0;i<16;i++)yield return null;_position=camera.transform.position;_rotation=camera.transform.rotation;
        File.WriteAllText(_stem+".txt",$"Original whirlpool.unity; native inputs on/off/on, ocean clock1/delta0 after60 live frames, Unity timeScale0 for captures, capture60Hz/query fence. Camera={_position}; component={whirl.transform.position}; fixed original camera pose for captures; depth on postprocess/MSAA off. Other ShapeWaves and persistent dynamic waves/foam disabled beforePlay to isolate displacement/flow; no standalone/performance/history-decay/dampening acceptance.\n");
        _target=new RenderTexture(640,360,24,RenderTextureFormat.ARGB32);_target.Create();
        var onWaves=Read(ocean._lodDataAnimWaves.DataTexture);var onFlow=Read(ocean._lodDataFlow.DataTexture);var on=Capture(camera,"on");
        displacement.enabled=false;flow.enabled=false;for(int i=0;i<16;i++)yield return null;
        var offWaves=Read(ocean._lodDataAnimWaves.DataTexture);var offFlow=Read(ocean._lodDataFlow.DataTexture);var off=Capture(camera,"off");
        displacement.enabled=true;flow.enabled=true;for(int i=0;i<16;i++)yield return null;
        var restoredWaves=Read(ocean._lodDataAnimWaves.DataTexture);var restoredFlow=Read(ocean._lodDataFlow.DataTexture);var restored=Capture(camera,"restored");
        double waveDelta=onWaves.Zip(offWaves,(a,b)=>Math.Abs(a.y-b.y)).Sum();double flowDelta=onFlow.Zip(offFlow,(a,b)=>Math.Abs(a.x-b.x)+Math.Abs(a.y-b.y)).Sum();
        float waveRestore=onWaves.Zip(restoredWaves,(a,b)=>(a-b).magnitude).Max();float flowRestore=onFlow.Zip(restoredFlow,(a,b)=>(a-b).magnitude).Max();int changed=on.Zip(off,(a,b)=>Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b)>6?1:0).Sum();int restore=on.Zip(restored,(a,b)=>a.Equals(b)?0:1).Sum();
        File.AppendAllText(_stem+".txt",$"Samples={onWaves.Length}; WaveHeightDeltaSum={waveDelta:R}; FlowDeltaSum={flowDelta:R}; WaveRestoreMax={waveRestore:R}; FlowRestoreMax={flowRestore:R}; ChangedPixels={changed}; RestorePixels={restore}\n");
        Assert.That(waveDelta,Is.GreaterThan(1));Assert.That(flowDelta,Is.GreaterThan(1));Assert.That(waveRestore,Is.LessThan(.002));Assert.That(flowRestore,Is.LessThan(.002));Assert.That(changed,Is.GreaterThan(200));Assert.That(restore,Is.Zero);ocean.PopTimeProvider(clock);
    }
    [UnityTest,Timeout(180000)] public IEnumerator OriginalCombinedWhirlpoolSceneRendersWithNativeSimulations()
    {
        _capture=Time.captureFramerate;_scale=Time.timeScale;OceanRenderer.EnableGraphicsInBatchMode=true;EditorSceneManager.OpenScene("Assets/Crest/Crest-Examples/Whirlpool/Scenes/whirlpool.unity");
        yield return new EnterPlayMode();Time.captureFramerate=60;Time.timeScale=1;
        var ocean=OceanRenderer.Instance;var camera=Camera.main;ocean.ViewCamera=camera;
        Assert.That(ocean.CreateDynamicWaveSim&&ocean.CreateFoamSim,Is.True);
        new GameObject("Combined Whirlpool query pacing").AddComponent<BoatProbesQueryFence>();
        for(int i=0;i<120;i++)yield return null;
        _stem=$"Evidence/OriginalWhirlpool-Combined-{SystemInfo.graphicsDeviceType}";_position=camera.transform.position;_rotation=camera.transform.rotation;
        camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;camera.allowMSAA=false;camera.aspect=16f/9;
        var waves=Read(ocean._lodDataAnimWaves.DataTexture);var flow=Read(ocean._lodDataFlow.DataTexture);
        double waveSum=waves.Sum(v=>(double)Math.Abs(v.y));double flowSum=flow.Sum(v=>(double)(Math.Abs(v.x)+Math.Abs(v.y)));Assert.That(waveSum,Is.GreaterThan(1));Assert.That(flowSum,Is.GreaterThan(1));
        _target=new RenderTexture(640,360,24,RenderTextureFormat.ARGB32);_target.Create();var pixels=Capture(camera,"native");int nonbackground=pixels.Count(p=>!p.Equals(pixels[0]));Assert.That(nonbackground,Is.GreaterThan(5000));
        File.WriteAllText(_stem+".txt",$"Original scene combined live120 frames, no source simulation toggles/clock freeze. Source camera depth on/postprocess+MSAA off640x360. All7 layers finite; WaveAbsY={waveSum:R}; FlowAbsXY={flowSum:R}; NonBackground={nonbackground}; Dynamic=True; Foam=True. Pink0. Smoke only, not numerical history/dampening/bake/standalone acceptance.\n");
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        OceanRenderer.EnableGraphicsInBatchMode=false;if(Application.isPlaying)yield return new ExitPlayMode();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Time.captureFramerate=_capture;Time.timeScale=_scale;if(_target!=null){_target.Release();Object.DestroyImmediate(_target);}
    }
}


public class WhirlpoolDampingTests
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    int _capture;float _scale;OceanRenderer _ocean;RipplePulseClock _clock;
    RippleStepObserver _steps;Material _material;float _strength;RegisterDynWavesInput _input;
    string _report;
    [UnityTest,Timeout(180000)]
    public IEnumerator CombinedSceneDampingAttenuatesMatchedNonzeroHistory()
    {
        _capture=Time.captureFramerate;_scale=Time.timeScale;OceanRenderer.EnableGraphicsInBatchMode=true;
        EditorSceneManager.OpenScene("Assets/Crest/Crest-Examples/Whirlpool/Scenes/whirlpool.unity");
        yield return new EnterPlayMode();Time.captureFramerate=60;Time.timeScale=1;
        _ocean=OceanRenderer.Instance;var whirl=Object.FindObjectsByType<Whirlpool>().Single();
        var camera=Camera.main;camera.transform.position=whirl.transform.position+Vector3.up*4;_ocean.ViewCamera=camera;
        new GameObject("Combined damping query pacing").AddComponent<BoatProbesQueryFence>();
        for(int i=0;i<120;i++)yield return null;
        Assert.That(_ocean.CreateDynamicWaveSim&&_ocean.CreateFoamSim&&_ocean._lodDataFlow!=null,Is.True);
        Assert.That(Object.FindObjectsByType<ShapeWaves>().Any(w=>w.enabled),Is.True);
        foreach(string field in new[]{"_displacementInput","_flowInput"})
            Assert.That(((GameObject)typeof(Whirlpool).GetField(field,Private).GetValue(whirl)).GetComponent<RegisterLodDataInputBase>().enabled,Is.True);
        _input=((GameObject)typeof(Whirlpool).GetField("_dynamicWavesInput",Private).GetValue(whirl)).GetComponent<RegisterDynWavesInput>();
        _material=_input.GetComponent<Renderer>().sharedMaterial;_strength=_material.GetFloat("_DampenStrength");float radius=_material.GetFloat("_Radius");
        Assert.That(radius,Is.GreaterThan(0));Assert.That(_strength,Is.GreaterThan(0));
        Time.timeScale=0;_clock=new RipplePulseClock{Time=123.5f,Frequency=_ocean._lodDataDynWaves.Settings._simulationFrequency};_ocean.PushTimeProvider(_clock);
        _steps=_ocean.gameObject.AddComponent<RippleStepObserver>();for(int i=0;i<32;i++)yield return null;
        var replay=_ocean.gameObject.AddComponent<WhirlpoolSphereInputReplay>();replay.Capture();
        _report="Evidence/P11-Whirlpool-"+SystemInfo.graphicsDeviceType;
        File.WriteAllText(_report+".txt",$"Original combined scene, all original wave/flow/displacement/dynamic/foam systems enabled. After120 live frames, fixed clock123.5/Unity timeScale0; each trial resets all3 dynamic RTs to RG(.25,.5), consumes exactly1 positive original substep. MatchedSphereSources={replay.SphereCount}; Radius={radius:R}; NativeStrength={_strength:R}; QuadScale={_input.transform.lossyScale}; Frequency={_clock.Frequency:R}. Not a long-term temporal/restoration/performance claim.\n");
        var results=new Vector2[4][];
        for(int trial=0;trial<4;trial++)
        {
            _input.enabled=trial==1||trial==2;_material.SetFloat("_DampenStrength",trial==2?.5f:_strength);
            for(int i=0;i<4;i++)yield return null;
            Seed();int before=_steps.TotalSubsteps;
            _clock.MinimumFrame=BuildCommandBufferBase._lastUpdateFrame;_clock.ActiveFrame=-1;_clock.Armed=true;_steps.CaptureNext=true;
            float deadline=Time.realtimeSinceStartup+20;
            while((_clock.Armed||_steps.LastFrame<_clock.ActiveFrame)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(_clock.Armed,Is.False);Assert.That(_steps.TotalSubsteps-before,Is.EqualTo(1));Assert.That(_steps.ReadError||_steps.CaptureNext,Is.False);
            _clock.ActiveFrame=-1;Assert.That(_steps.Captured.Length,Is.EqualTo(7));
            foreach(var layer in _steps.Captured)Assert.That(layer.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)),Is.True);
            results[trial]=_steps.Captured[0];
            using(var writer=new BinaryWriter(File.Create(_report+$"-trial{trial}-lod0.bin")))foreach(var v in results[trial]){writer.Write(v.x);writer.Write(v.y);}
            File.AppendAllText(_report+".txt",$"Trial={trial}; DampingEnabled={_input.enabled}; Strength={_material.GetFloat("_DampenStrength"):R}; ActualSteps={_steps.TotalSubsteps-before}; PeakR={results[trial].Max(v=>Math.Abs(v.x)):R}; PeakG={results[trial].Max(v=>Math.Abs(v.y)):R}\n");
        }
        int width=_ocean._lodDataDynWaves.DataTexture.width;
        var correction=(Vector3)typeof(RegisterLodDataInputBase).GetField("_displacement",Private).GetValue(_input);
        var center=_input.transform.position;center.x-=correction.x;center.z-=correction.z;
        var vp=_ocean._lodTransform.GetProjectionMatrix(0)*_ocean._lodTransform.GetWorldToCameraMatrix(0);
        Vector2 Screen(Vector3 world){var p=vp*new Vector4(world.x,world.y,world.z,1);return new Vector2((p.x/p.w*.5f+.5f)*width,(p.y/p.w*.5f+.5f)*width);}
        var lo=Screen(center-new Vector3(radius,0,radius));var hi=Screen(center+new Vector3(radius,0,radius));
        Assert.That(Math.Min(lo.x,hi.x),Is.GreaterThan(4));Assert.That(Math.Max(lo.x,hi.x),Is.LessThan(width-4));
        float[] errors=new float[2];float outsideError=0,repeatError=0,defaultDelta=0;int interior=0,outside=0;
        for(int y=0;y<width;y++)for(int x=0;x<width;x++)
        {
            int i=y*width+x;float nx=2*(x+.5f-lo.x)/(hi.x-lo.x)-1,nz=2*(y+.5f-lo.y)/(hi.y-lo.y)-1;
            float r2=nx*nx+nz*nz;float falloff=r2<1?Mathf.Pow(1-r2,2):0;
            for(int t=0;t<2;t++)
            {
                float alpha=falloff*(t==0?_strength:.5f);var expected=results[0][i]*(1-alpha);var actual=results[t+1][i];
                errors[t]=Mathf.Max(errors[t],Math.Abs(actual.x-expected.x),Math.Abs(actual.y-expected.y));
            }
            repeatError=Mathf.Max(repeatError,(results[3][i]-results[0][i]).magnitude);
            if(r2<.25f){interior++;defaultDelta=Mathf.Max(defaultDelta,(results[1][i]-results[0][i]).magnitude);}
            if(r2>1.2f){outside++;outsideError=Mathf.Max(outsideError,(results[2][i]-results[0][i]).magnitude);}
        }
        File.AppendAllText(_report+".txt",$"ProjectionLow={lo:F8}; High={hi:F8}; DisplacementCorrection={correction:F8}; Interior={interior}; Outside={outside}; NativeRGError={errors[0]:R}; StrongRGError={errors[1]:R}; NativeInteriorDelta={defaultDelta:R}; OutsideDelta={outsideError:R}; MatchedOffRepeatError={repeatError:R}\n");
        Assert.That(interior,Is.GreaterThan(20));Assert.That(outside,Is.GreaterThan(1000));Assert.That(defaultDelta,Is.GreaterThan(.001f));
        Assert.That(errors[0],Is.LessThan(.0006f));Assert.That(errors[1],Is.LessThan(.0006f));Assert.That(outsideError,Is.LessThan(.0006f));Assert.That(repeatError,Is.LessThan(.0006f));
        _input.enabled=true;_material.SetFloat("_DampenStrength",_strength);
    }
    void Seed()
    {
        var sim=_ocean._lodDataDynWaves;var temporary=(RenderTexture)typeof(LodDataMgrPersistent).GetField("_sources",Private).GetValue(sim);
        using(var cmd=new CommandBuffer())
        {
            foreach(var texture in new[]{sim.GetDataTexture(0),sim.GetDataTexture(1),temporary}.Distinct())
                for(int layer=0;layer<texture.volumeDepth;layer++){cmd.SetRenderTarget(texture,0,CubemapFace.Unknown,layer);cmd.ClearRenderTarget(false,true,new Color(.25f,.5f,0,0));}
            Graphics.ExecuteCommandBuffer(cmd);
        }
        typeof(LodDataMgrPersistent).GetField("_timeToSimulate",Private).SetValue(sim,0f);
        typeof(LodDataMgrPersistent).GetField("_substepDtPrevious",Private).SetValue(sim,1/_clock.Frequency);
    }
    [UnityTearDown]public IEnumerator Cleanup()
    {
        if(_material!=null)_material.SetFloat("_DampenStrength",_strength);
        if(_ocean!=null&&_clock!=null)_ocean.PopTimeProvider(_clock);
        OceanRenderer.EnableGraphicsInBatchMode=false;
        if(Application.isPlaying)yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Time.captureFramerate=_capture;Time.timeScale=_scale;
    }
}


// Test-only matched forcing: original sphere components/registration remain active.
// Replay one settled CPU input snapshot just before OceanRenderer (order200).
[DefaultExecutionOrder(199)]
public class WhirlpoolSphereInputReplay : MonoBehaviour
{
    readonly System.Collections.Generic.List<(FieldInfo field,object target,object value)> _fields=new System.Collections.Generic.List<(FieldInfo,object,object)>();
    readonly System.Collections.Generic.List<(System.Collections.IList list,object[] values)> _lists=new System.Collections.Generic.List<(System.Collections.IList,object[])>();
    public int SphereCount;
    public void Capture()
    {
        var type=typeof(SphereWaterInteraction);const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        var spheres=Object.FindObjectsByType<SphereWaterInteraction>();SphereCount=spheres.Length;
        foreach(var sphere in spheres)foreach(string name in new[]{"_velocity","_weightThisFrame","_renderMatrix"})
        {var field=type.GetField(name,flags);_fields.Add((field,sphere,field.GetValue(sphere)));}
        foreach(string name in new[]{"s_RelativeVelocityProperties","s_RadiusProperties","s_InnerSphereOffsetProperties","s_InnerSphereMultiplierProperties","s_LargeWaveMultiplierProperties","s_DisplacementAtInputPositionProperties"})
        {var list=(System.Collections.IList)type.GetField(name,flags).GetValue(null);var values=new object[list.Count];list.CopyTo(values,0);_lists.Add((list,values));}
    }
    void LateUpdate()
    {
        foreach(var item in _fields)item.field.SetValue(item.target,item.value);
        foreach(var item in _lists){item.list.Clear();foreach(var value in item.values)item.list.Add(value);}
    }
}
