// Field-reachable Crest resources and operation-isolated texture snapshots. MIT.
#if CREST_RESOURCE_PROBE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Crest.URP.Diagnostics
{
    public sealed partial class ResourceShutdownProbe
    {
        [Serializable] class BufferInfo {public string id,kind,owner;public bool valid;public int count,stride;public long logicalBytes;}
        sealed class TrackedBuffer {public object reference;public BufferInfo description;}
        [Serializable] class TextureOwner {public string id;public string[] paths;}
        [Serializable] class OwnershipStage
        {
            public string stage,utc;
            public Snapshot snapshot;
            public BufferInfo[] reachableBuffers,trackedBufferState;
            public TextureOwner[] textureOwners;
            public string[] walkLimits;
            public int walkedNodes;
#if UNITY_STANDALONE_WIN
            public DriverMemory driverMemory;
#endif
        }
        [Serializable] class OwnershipResult {public string unity,api,mode,readback;public int errors;public bool hdr,featureDisabled;public OwnershipStage[] stages;}
        sealed class References : IEqualityComparer<object>
        {
            public new bool Equals(object a,object b)=>ReferenceEquals(a,b);
            public int GetHashCode(object o)=>RuntimeHelpers.GetHashCode(o);
        }
        readonly List<TrackedBuffer> _trackedBuffers=new List<TrackedBuffer>();
        bool _fftCacheObserved;
        bool BufferValid(object buffer)=>buffer is ComputeBuffer c?c.IsValid():((GraphicsBuffer)buffer).IsValid();
        BufferInfo DescribeBuffer(TrackedBuffer item)
        {
            var d=item.description;
            return new BufferInfo {id=d.id,kind=d.kind,owner=d.owner,count=d.count,stride=d.stride,logicalBytes=d.logicalBytes,valid=BufferValid(item.reference)};
        }
        OwnershipStage Observe(string label,RenderTexture target)
        {
            var seen=new HashSet<object>(new References());var found=new HashSet<object>(new References());
            var owners=new Dictionary<string,List<string>>();var limits=new List<string>();int nodes=0;
            Action<object,string,int> walk=null;
            walk=(value,path,depth)=>
            {
                if(value==null)return;
                if(value is RenderTexture texture)
                {
                    if(texture!=null){string id=texture.GetEntityId().ToString();if(!owners.TryGetValue(id,out var paths))owners[id]=paths=new List<string>();paths.Add(path);}return;
                }
                if(value is ComputeBuffer||value is GraphicsBuffer)
                {
                    found.Add(value);
                    if(!_trackedBuffers.Any(b=>ReferenceEquals(b.reference,value)))
                    {
                        bool valid=BufferValid(value);int count=valid?(value is ComputeBuffer c?c.count:((GraphicsBuffer)value).count):0;
                        int stride=valid?(value is ComputeBuffer b?b.stride:((GraphicsBuffer)value).stride):0;
                        _trackedBuffers.Add(new TrackedBuffer {reference=value,description=new BufferInfo {id="B"+(_trackedBuffers.Count+1),kind=value.GetType().Name,owner=path,count=count,stride=stride,logicalBytes=(long)count*stride,valid=valid}});
                    }return;
                }
                var type=value.GetType();if(type.IsPrimitive||type.IsEnum||value is string||value is Type)return;
                if(depth>12||nodes>30000){limits.Add(path+": limit");return;}
                if(!seen.Add(value))return;nodes++;
                if(value is Array array){for(int i=0;i<Math.Min(array.Length,1024);i++)walk(array.GetValue(i),path+"["+i+"]",depth+1);if(array.Length>1024)limits.Add(path+": array truncated");return;}
                if(value is IDictionary dictionary){int i=0;foreach(DictionaryEntry entry in dictionary){if(i>=1024){limits.Add(path+": dictionary truncated");break;}walk(entry.Value,path+".value["+i+++"]",depth+1);}return;}
                if(value is IList list){for(int i=0;i<Math.Min(list.Count,1024);i++)walk(list[i],path+"["+i+"]",depth+1);if(list.Count>1024)limits.Add(path+": list truncated");return;}
                if(value is RTHandle handle){walk(handle.rt,path+".rt",depth+1);return;}
                for(var declaring=type;declaring!=null&&declaring.Namespace!=null&&declaring.Namespace.StartsWith("Crest",StringComparison.Ordinal);declaring=declaring.BaseType)
                    foreach(var field in declaring.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
                    {
                        try{walk(field.GetValue(value),path+"."+field.Name,depth+1);}
                        catch(Exception e){limits.Add(path+"."+field.Name+": "+e.GetType().Name);}
                    }
            };
            foreach(var root in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
                if(root!=null&&root.GetType().Namespace!=null&&root.GetType().Namespace.StartsWith("Crest",StringComparison.Ordinal)&&!root.GetType().Namespace.Contains(".Diagnostics"))
                    walk(root,root.GetType().Name+"#"+root.GetEntityId(),0);
            foreach(var root in Resources.FindObjectsOfTypeAll<CrestBackgroundFeature>())walk(root,"CrestBackgroundFeature#"+root.GetEntityId(),0);
            if(OceanRenderer.Instance!=null)_fftCacheObserved=true;
            // Only the FFT cache known to have been activated by this scene is read.
            // Never enumerate arbitrary static fields/cctors of unloaded Crest subsystems.
            if(_fftCacheObserved)walk(typeof(FFTCompute).GetField("_generators",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null),"FFTCompute._generators",0);
            if(target!=null)walk(target,"Probe.target",0);
            var stage=new OwnershipStage {stage=label,utc=DateTime.UtcNow.ToString("O"),snapshot=Capture(label),walkedNodes=nodes,walkLimits=limits.ToArray(),
                reachableBuffers=_trackedBuffers.Where(b=>found.Contains(b.reference)).Select(DescribeBuffer).ToArray(),
                trackedBufferState=_trackedBuffers.Select(DescribeBuffer).ToArray(),textureOwners=owners.Select(p=>new TextureOwner {id=p.Key,paths=p.Value.ToArray()}).ToArray()
#if UNITY_STANDALONE_WIN
                ,driverMemory=ReadDriverMemory()
#endif
            };
            File.WriteAllText(Argument("-crestProbeOutput","Ownership.json")+".phase.json",JsonUtility.ToJson(stage));
            return stage;
        }
        IEnumerator Ownership(string mode)
        {
            string readback=Argument("-crestOwnershipReadback","Crop");
            if(readback!="None"&&readback!="Full"&&readback!="Crop")throw new ArgumentException("Unknown readback mode");
            var stages=new List<OwnershipStage>();int cycles=mode=="Empty"?1:3;
            for(int cycle=0;cycle<cycles;cycle++)
            {
                if(cycle>0)yield return SceneManager.LoadSceneAsync(1);
                for(int i=0;i<4;i++)yield return null;
                string prefix="cycle"+cycle+"-";
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"-crestOwnershipNoHDR")>=0)Camera.main.allowHDR=false;
                stages.Add(Observe(prefix+"setup",null));
                var target=new RenderTexture(960,540,24,RenderTextureFormat.ARGB32){name="Probe ownership target"};target.Create();
                try
                {
                    stages.Add(Observe(prefix+"target-created",target));
                    var request=new UniversalRenderPipeline.SingleCameraRequest {destination=target};
                    RenderPipeline.SubmitRenderRequest(Camera.main,request);
                    stages.Add(Observe(prefix+"submitted",target));
                    if(readback!="None")
                    {
                        var done=readback=="Crop"?AsyncGPUReadback.Request(target,0,0,1,0,1,0,1):AsyncGPUReadback.Request(target);
                        stages.Add(Observe(prefix+"readback-requested",target));
                        while(!done.done)yield return null;
                        if(done.hasError)_errors++;
                        stages.Add(Observe(prefix+"readback-completed",target));
                    }
                    yield return new WaitForSecondsRealtime(2);
                    stages.Add(Observe(prefix+"held",target));
                }
                finally {target.Release();Destroy(target);}
                for(int i=0;i<4;i++)yield return null;
                stages.Add(Observe(prefix+"target-released",null));
                if(mode!="Empty")
                {
                    if(!_trackedBuffers.Any(b=>BufferValid(b.reference)))_errors++;
                    yield return SceneManager.LoadSceneAsync(0);
                    yield return Resources.UnloadUnusedAssets();
                    for(int i=0;i<4;i++)yield return null;
                    var unloaded=Observe(prefix+"ocean-unloaded",null);stages.Add(unloaded);
                    if(unloaded.snapshot.oceans!=0||unloaded.trackedBufferState.Any(b=>b.valid))_errors++;
                    yield return new WaitForSecondsRealtime(2);
                    stages.Add(Observe(prefix+"unloaded-held",null));
                }
            }
            var result=new OwnershipResult {unity=Application.unityVersion,api=SystemInfo.graphicsDeviceType.ToString(),mode=mode,readback=readback,hdr=Array.IndexOf(Environment.GetCommandLineArgs(),"-crestOwnershipNoHDR")<0,featureDisabled=Array.IndexOf(Environment.GetCommandLineArgs(),"-crestProbeDisableFeature")>=0,errors=_errors,stages=stages.ToArray()};
            string output=Argument("-crestProbeOutput","Ownership.json");Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output,JsonUtility.ToJson(result,true));Debug.Log("OWNERSHIP_PROBE_COMPLETED "+mode+" "+readback+" errors="+_errors);Application.Quit(_errors==0?0:1);
        }
    }
}
#endif
