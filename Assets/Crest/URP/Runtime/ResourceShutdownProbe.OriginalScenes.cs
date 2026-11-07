// Original serialized scene controls in a compiled Player. Diagnostic-only; MIT.
#if CREST_RESOURCE_PROBE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Crest.URP.Diagnostics
{
    public sealed partial class ResourceShutdownProbe
    {
        [Serializable] class SceneMetric { public string name; public double value; }
        [Serializable] class OriginalSceneReport
        {
            public string unity,api,gpu,scene,test,scope,failure,quality,camera;
            public bool keyboardPresent,focused; public int errors,missingScripts,frame=-1,lodFrame=-1,changed=-1,restoration=-1;
            public List<SceneMetric> metrics=new List<SceneMetric>();
        }
        OriginalSceneReport _originalSceneReport;
        bool _originalScenePacing;
        const BindingFlags SceneFlags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        static FieldInfo SceneField(Type type,string name)
        {
            for(;type!=null;type=type.BaseType){var field=type.GetField(name,SceneFlags|BindingFlags.DeclaredOnly);if(field!=null)return field;}
            throw new MissingFieldException(name);
        }
        static void SceneSet(object obj,string field,object value)=>SceneField(obj.GetType(),field).SetValue(obj,value);
        static object SceneGet(object obj,string field)=>SceneField(obj.GetType(),field).GetValue(obj);
        static MonoBehaviour[] SceneComponents(string name)=>FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(c=>c!=null&&c.GetType().FullName==name).ToArray();
        void SceneMeasure(string name,double value){CheckExample(!double.IsNaN(value)&&!double.IsInfinity(value),"Nonfinite "+name);_originalSceneReport.metrics.Add(new SceneMetric{name=name,value=value});}
        void SaveOriginalScene()
        {
            if(_originalSceneReport==null)return;
            _originalSceneReport.errors=_errors;
            File.WriteAllText(Argument("-crestProbeOutput","OriginalScene.json"),JsonUtility.ToJson(_originalSceneReport,true));
        }
        // Flatten child routines so a failed assertion saves the partial report and exits, rather than hanging.
        IEnumerator GuardOriginalScene(IEnumerator root)
        {
            var stack=new Stack<IEnumerator>();stack.Push(root);
            while(stack.Count>0)
            {
                object item=null;bool next=false;Exception failure=null;
                try{next=stack.Peek().MoveNext();if(next)item=stack.Peek().Current;}
                catch(Exception error){failure=error;}
                if(failure!=null){Debug.LogException(failure);if(_originalSceneReport!=null)_originalSceneReport.failure=failure.ToString();SaveOriginalScene();Application.Quit(1);yield break;}
                if(!next){stack.Pop();continue;}
                if(item is IEnumerator child)stack.Push(child);else yield return item;
            }
        }
        IEnumerator SceneCapture(Action capture)
        {
            _examplesFailure=null;_examplesCapture=()=>
            {
                capture();_originalSceneReport.frame=OceanRenderer.FrameCount;_originalSceneReport.lodFrame=BuildCommandBufferBase._lastUpdateFrame;
                if(Camera.main!=null)_originalSceneReport.camera=Camera.main.transform.position.ToString("R");
            };
            float end=Time.realtimeSinceStartup+20;
            while(_examplesCapture!=null&&Time.realtimeSinceStartup<end)yield return null;
            CheckExample(_examplesCapture==null&&_examplesFailure==null,"Scene capture failed: "+_examplesFailure);
        }
        Camera SceneCamera()
        {
            var camera=Camera.main;CheckExample(camera!=null&&OceanRenderer.Instance!=null,"Missing source camera/ocean");
            OceanRenderer.Instance.ViewCamera=camera;camera.aspect=16f/9;camera.allowMSAA=false;
            var data=camera.GetUniversalAdditionalCameraData();data.requiresDepthTexture=true;data.renderPostProcessing=false;
            _originalSceneReport.camera=camera.transform.position.ToString("R");return camera;
        }
        IEnumerator SceneVisibility(Camera camera,Renderer[] renderers,Terrain[] terrains=null)
        {
            yield return SceneCapture(()=>
            {
                var states=renderers.Select(r=>r.forceRenderingOff).ToArray();var ts=terrains?.Select(t=>t.enabled).ToArray();
                var on=ReadExample(camera,"on.png");Color32[] off;
                try{foreach(var r in renderers)r.forceRenderingOff=true;if(terrains!=null)foreach(var t in terrains)t.enabled=false;off=ReadExample(camera,"off.png");}
                finally{for(int i=0;i<renderers.Length;i++)renderers[i].forceRenderingOff=states[i];if(terrains!=null)for(int i=0;i<terrains.Length;i++)terrains[i].enabled=ts[i];}
                var restored=ReadExample(camera,"restored.png");
                _originalSceneReport.changed=on.Zip(off,(a,b)=>Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b)>6?1:0).Sum();
                _originalSceneReport.restoration=on.Zip(restored,(a,b)=>a.Equals(b)?0:1).Sum();
                _originalSceneReport.frame=OceanRenderer.FrameCount;_originalSceneReport.lodFrame=BuildCommandBufferBase._lastUpdateFrame;
                SceneMeasure("nonBackground",on.Count(p=>!p.Equals(on[0])));
                CheckExample(_originalSceneReport.changed>200&&_originalSceneReport.restoration==0,"Source geometry visibility/restoration");
            });
        }
        static Vector4[] SceneRead(RenderTexture target,bool depth=false)
        {
            CheckExample(target!=null&&target.IsCreated(),"Missing native field");
            var read=AsyncGPUReadback.Request(target,0,TextureFormat.RGBAFloat);read.WaitForCompletion();
            CheckExample(!read.hasError&&read.layerCount==7,"Native readback/layers");
            var values=Enumerable.Range(0,read.layerCount).SelectMany(i=>read.GetData<Vector4>(i).ToArray()).ToArray();
            CheckExample(values.All(v=>(float.IsFinite(v.x)||(depth&&float.IsNegativeInfinity(v.x)))&&float.IsFinite(v.y)&&float.IsFinite(v.z)&&float.IsFinite(v.w)),"Nonfinite native field");return values;
        }
        IEnumerator OriginalScenePlayer()
        {
            string test=Argument("-crestOriginalSceneCase","main");
            int scene=test=="main"?1:test=="boat"||test=="input"?2:test.StartsWith("wakes")?3:test=="threeboats"?4:test=="rivers"?5:6;
            Directory.CreateDirectory(Argument("-crestProbeImages","."));
            _originalSceneReport=new OriginalSceneReport{unity=Application.unityVersion,api=SystemInfo.graphicsDeviceType.ToString(),gpu=SystemInfo.graphicsDeviceName,test=test,
                quality=QualitySettings.names[QualitySettings.GetQualityLevel()],focused=Application.isFocused,
                keyboardPresent=Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem")?.GetProperty("current",BindingFlags.Static|BindingFlags.Public)?.GetValue(null)!=null,
                scope="P11.3-B finite original scene transfer; Forward/PC/1x/640x360. Capture60Hz/query fence and fixed clocks are explicit test controls, not performance/physical-input acceptance."};
            Time.captureFramerate=60;Time.timeScale=1;_originalScenePacing=true;
            void Configure(Scene loaded,LoadSceneMode mode)
            {
                foreach(var b in SceneComponents("Crest.Examples.BoatAlignNormal")){SceneSet(b,"_playerControlled",false);if(test=="boat"||test=="input"){SceneSet(b,"_throttleBias",0f);SceneSet(b,"_steerBias",0f);}}
                foreach(var b in FindObjectsByType<BoatProbes>(FindObjectsSortMode.None))b._playerControlled=false;
                if(test.StartsWith("wakes"))foreach(var s in FindObjectsByType<SphereWaterInteraction>(FindObjectsSortMode.None))s.enabled=test=="wakes-on";
                if(test=="rivers"||test=="whirl-isolated")foreach(var shape in FindObjectsByType<ShapeWaves>(FindObjectsSortMode.None))shape.enabled=false;
                if(test=="whirl-isolated"){SceneSet(OceanRenderer.Instance,"_createDynamicWaveSim",false);SceneSet(OceanRenderer.Instance,"_createFoamSim",false);}
            }
            SceneManager.sceneLoaded+=Configure;
            try{yield return SceneManager.LoadSceneAsync(scene);}finally{SceneManager.sceneLoaded-=Configure;}
            _originalSceneReport.scene=SceneManager.GetActiveScene().path;
            _originalSceneReport.missingScripts=SceneManager.GetActiveScene().GetRootGameObjects().Sum(r=>r.GetComponentsInChildren<Transform>(true).Sum(t=>t.GetComponents<MonoBehaviour>().Count(c=>c==null)));
            CheckExample(_originalSceneReport.missingScripts==0,"Missing scripts");
            _examplesTarget=new RenderTexture(640,360,24,RenderTextureFormat.ARGB32);_examplesTarget.Create();
            int warm=(test=="boat"||test=="threeboats"||test.StartsWith("wakes")||test=="whirl-combined")?0:60;
            for(int i=0;i<warm;i++)yield return null;
            var camera=SceneCamera();var ocean=OceanRenderer.Instance;
            if(test=="input")
            {
                SceneMeasure("sourceAlignNormalCount",SceneComponents("Crest.Examples.BoatAlignNormal").Length);
                var owner=new GameObject("Owned BoatProbes input preflight",typeof(Rigidbody));var probe=owner.AddComponent<BoatProbes>();probe.enabled=false;
                SceneSet(probe,"_rb",owner.GetComponent<Rigidbody>());probe._playerControlled=true;probe._engineBias=1;probe._turnBias=.5f;
                try{typeof(BoatProbes).GetMethod("FixedUpdateEngine",SceneFlags).Invoke(probe,null);}catch(Exception error){Debug.LogException(error);}
                SceneMeasure("probeEngineInvoked",1);
                var force=owner.GetComponent<Rigidbody>().GetAccumulatedForce(Time.fixedDeltaTime);var torque=owner.GetComponent<Rigidbody>().GetAccumulatedTorque(Time.fixedDeltaTime);
                SceneMeasure("probeBiasForce",force.z);SceneMeasure("probeBiasTorque",torque.magnitude);
                CheckExample(force.z>0&&torque.magnitude>0,"Missing keyboard must retain native bias forces");
                var gui=FindObjectsByType<OceanDebugGUI>(FindObjectsSortMode.None).FirstOrDefault();if(gui==null)gui=owner.AddComponent<OceanDebugGUI>();
                try{typeof(OceanDebugGUI).GetMethod("Update",SceneFlags).Invoke(gui,null);}catch(Exception error){Debug.LogException(error);}
                SceneMeasure("debugGuiInvoked",1);Destroy(owner);
            }
            else if(test=="boat"||test=="threeboats")yield return SceneBoats(camera,test=="threeboats");
            else if(test.StartsWith("wakes"))yield return SceneWakes(camera,test=="wakes-on");
            else if(test=="rivers"||test=="whirl-isolated")yield return SceneFields(camera,test=="rivers");
            else
            {
                if(test=="whirl-combined")
                {
                    CheckExample(ocean.CreateDynamicWaveSim&&ocean.CreateFoamSim,"Combined source systems must stay enabled");
                    for(int i=0;i<120;i++)yield return null;
                    double waves=SceneRead(ocean._lodDataAnimWaves.DataTexture).Sum(v=>(double)Math.Abs(v.y));double flow=SceneRead(ocean._lodDataFlow.DataTexture).Sum(v=>(double)(Math.Abs(v.x)+Math.Abs(v.y)));
                    SceneMeasure("waveSum",waves);SceneMeasure("flowSum",flow);CheckExample(waves>1&&flow>1,"Combined whirlpool fields");
                    yield return SceneCapture(()=>{var pixels=ReadExample(camera,"combined.png");SceneMeasure("nonBackground",pixels.Count(p=>!p.Equals(pixels[0])));CheckExample(pixels.Count(p=>!p.Equals(pixels[0]))>5000,"Empty combined scene");});
                }
                else yield return SceneVisibility(camera,FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r=>r.GetComponentInParent<OceanRenderer>()==null).ToArray(),FindObjectsByType<Terrain>(FindObjectsSortMode.None));
            }
            SaveOriginalScene();_examplesTarget.Release();Destroy(_examplesTarget);_originalScenePacing=false;
            Debug.Log("ORIGINAL_SCENE_COMPLETED "+test+" errors="+_errors);Application.Quit(_errors==0?0:1);
        }
        IEnumerator SceneBoats(Camera camera,bool three)
        {
            var boats=SceneComponents("Crest.Examples.BoatAlignNormal").OrderBy(b=>b.name).ToArray();
            if(!three)boats=new[]{boats.Single(b=>b.name=="BoatAlignNormalPlayer")};
            CheckExample(boats.Length==(three?3:1),"Boat count");
            for(int i=0;i<(three?200:150);i++)yield return new WaitForFixedUpdate();
            int steps=three?150:100;var starts=boats.Select(b=>b.transform.position).ToArray();var rotations=boats.Select(b=>b.transform.rotation).ToArray();var wet=new int[boats.Length];
            for(int i=0;i<steps;i++){yield return new WaitForFixedUpdate();for(int n=0;n<boats.Length;n++){if(((FloatingObjectBase)boats[n]).InWater)wet[n]++;CheckExample(Mathf.Abs(boats[n].transform.position.y-OceanRenderer.Instance.SeaLevel)<10,"Unbounded hull");}}
            for(int n=0;n<boats.Length;n++)
            {
                SceneMeasure(boats[n].name+".wet",wet[n]);CheckExample(wet[n]>(three?75:50),"Hull not in water");
                if(three){double moved=Vector3.ProjectOnPlane(boats[n].transform.position-starts[n],Vector3.up).magnitude,turn=Quaternion.Angle(rotations[n],boats[n].transform.rotation);SceneMeasure(boats[n].name+".metres",moved);SceneMeasure(boats[n].name+".turn",turn);CheckExample(moved>2&&turn>5,"Three boats native movement");}
            }
            yield return SceneVisibility(camera,boats.SelectMany(b=>b.GetComponentsInChildren<Renderer>()).ToArray());
            if(!three)
            {
                var boat=boats[0];var start=boat.transform.position;var heading=Vector3.ProjectOnPlane(boat.transform.forward,Vector3.up).normalized;SceneSet(boat,"_throttleBias",1f);int count=0;
                for(int i=0;i<150;i++){yield return new WaitForFixedUpdate();if(((FloatingObjectBase)boat).InWater)count++;}
                SceneSet(boat,"_throttleBias",0f);float forward=Vector3.Dot(boat.transform.position-start,heading);SceneMeasure("throttleWet",count);SceneMeasure("forwardMetres",forward);CheckExample(count>75&&forward>2,"Native throttle movement");
            }
        }
        IEnumerator SceneWakes(Camera camera,bool on)
        {
            var sources=FindObjectsByType<SphereWaterInteraction>(FindObjectsSortMode.None);CheckExample(sources.Length==8,"Original8 wake sources");
            var rig=SceneComponents("Crest.Examples.SimpleMotion").Single(b=>b.name=="WakeRig");var start=rig.transform.position;
            for(int i=0;i<120;i++)yield return null;
            var values=SceneRead(OceanRenderer.Instance._lodDataDynWaves.DataTexture);double energy=values.Sum(v=>(double)(Math.Abs(v.x)+Math.Abs(v.y)));int nonzero=values.Count(v=>v.x*v.x+v.y*v.y>1e-12f);float distance=Vector3.Distance(start,rig.transform.position);
            SceneMeasure("energy",energy);SceneMeasure("nonzero",nonzero);SceneMeasure("rigDistance",distance);CheckExample(distance>10,"Wake rig movement");
            CheckExample(on?energy>.001&&nonzero>100:energy<1e-8,"Wake source contribution");
            if(on)yield return SceneVisibility(camera,FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r=>r.GetComponentInParent<OceanRenderer>()==null).ToArray());
        }
        IEnumerator SceneFields(Camera camera,bool rivers)
        {
            var ocean=OceanRenderer.Instance;var clock=new ExampleFixedTime();ocean.PushTimeProvider(clock);Time.timeScale=0;
            var heights=FindObjectsByType<RegisterHeightInput>(FindObjectsSortMode.None).Cast<Behaviour>().ToArray();var flows=FindObjectsByType<RegisterFlowInput>(FindObjectsSortMode.None).Cast<Behaviour>().ToArray();
            Behaviour[] inputs;
            if(rivers){CheckExample(heights.Length>0&&flows.Length>0,"Missing river inputs");inputs=heights.Concat(flows).ToArray();}
            else{var whirl=SceneComponents("Crest.Whirlpool").Single();inputs=new Behaviour[]{((GameObject)SceneGet(whirl,"_displacementInput")).GetComponent<RegisterAnimWavesInput>(),((GameObject)SceneGet(whirl,"_flowInput")).GetComponent<RegisterFlowInput>()};}
            CheckExample(inputs.All(i=>i.enabled),"Source inputs disabled");
            var fixedPosition=camera.transform.position;var fixedRotation=camera.transform.rotation;
            var fields=new Vector4[3][][];var images=new Color32[3][];
            for(int phase=0;phase<3;phase++)
            {
                foreach(var input in inputs)input.enabled=phase!=1;for(int i=0;i<16;i++)yield return null;
                fields[phase]=new[]{SceneRead(rivers?ocean._lodDataSeaDepths.DataTexture:ocean._lodDataAnimWaves.DataTexture,rivers),SceneRead(ocean._lodDataFlow.DataTexture)};
                int captured=phase;yield return SceneCapture(()=>{camera.transform.SetPositionAndRotation(fixedPosition,fixedRotation);images[captured]=ReadExample(camera,new[]{"on.png","off.png","restored.png"}[captured]);});
            }
            double first=fields[0][0].Zip(fields[1][0],(a,b)=>(double)Math.Abs(a.y-b.y)).Sum(),flow=fields[0][1].Zip(fields[1][1],(a,b)=>(double)(Math.Abs(a.x-b.x)+Math.Abs(a.y-b.y))).Sum();
            float restoreFirst=fields[0][0].Zip(fields[2][0],(a,b)=>rivers?Math.Abs(a.y-b.y):(a-b).magnitude).Max(),restoreFlow=fields[0][1].Zip(fields[2][1],(a,b)=>(a-b).magnitude).Max();
            _originalSceneReport.changed=images[0].Zip(images[1],(a,b)=>Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b)>6?1:0).Sum();_originalSceneReport.restoration=images[0].Zip(images[2],(a,b)=>a.Equals(b)?0:1).Sum();
            SceneMeasure("heightDelta",first);SceneMeasure("flowDelta",flow);SceneMeasure("heightRestore",restoreFirst);SceneMeasure("flowRestore",restoreFlow);
            CheckExample(first>1&&flow>1&&restoreFirst<.002&&restoreFlow<.002&&_originalSceneReport.changed>200&&_originalSceneReport.restoration==0,"Source fields/control restoration");ocean.PopTimeProvider(clock);
        }
    }
}
#endif
