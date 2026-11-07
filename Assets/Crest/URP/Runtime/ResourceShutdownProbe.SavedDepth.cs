// Original exported depth in a serialized prefab/scene without source geometry. MIT.
#if CREST_RESOURCE_PROBE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Crest.URP.Diagnostics
{
    public sealed partial class ResourceShutdownProbe
    {
        [Serializable] class SavedDepthManifest{public string scene,sceneGuid,textureGuid,prefabGuid,textureName,exportSHA256,importedFloatSHA256,source;}
        [Serializable] class SavedDepthRow
        {
            public string state,textureSHA256,format,actual;public float ownerY,expected;public bool sentinel,binding,noRealtimeCache,sourceGeometryAbsent,oldOceanDestroyed,oldDepthTargetDestroyed;
        }
        [Serializable] class SavedDepthReport
        {
            public string unity,api,gpu,scope,failure;public int errors;public SavedDepthManifest provenance;public List<SavedDepthRow> rows=new List<SavedDepthRow>();
        }
        SavedDepthReport _savedDepthReport;
        void SaveDepthReport(){if(_savedDepthReport==null)return;_savedDepthReport.errors=_errors;File.WriteAllText(Argument("-crestProbeOutput","SavedDepth.json"),JsonUtility.ToJson(_savedDepthReport,true));}
        IEnumerator WaitSavedDepthUpdates()
        {
            int previous=BuildCommandBufferBase._lastUpdateFrame,done=0;float deadline=Time.realtimeSinceStartup+15;
            while(done<4&&Time.realtimeSinceStartup<deadline){yield return null;int frame=BuildCommandBufferBase._lastUpdateFrame;if(frame!=previous){previous=frame;done++;}}
            CheckExample(done==4,"Saved depth needs actual ocean updates");
        }
        void MeasureSavedDepth(OceanDepthCache cache,string label,float expected,bool sentinel=false,bool oldOcean=false,bool oldTarget=false)
        {
            var ocean=OceanRenderer.Instance;var manifest=_savedDepthReport.provenance;var texture=cache.SavedCache;
            var row=new SavedDepthRow{state=label,ownerY=cache.transform.position.y,expected=expected,sentinel=sentinel,noRealtimeCache=cache.CacheTexture==null,sourceGeometryAbsent=!FindObjectsByType<Transform>().Any(t=>t.gameObject.layer==30),oldOceanDestroyed=oldOcean,oldDepthTargetDestroyed=oldTarget,format=texture.graphicsFormat.ToString()};
            _savedDepthReport.rows.Add(row);SaveDepthReport();
            CheckExample(SceneManager.GetActiveScene().path==manifest.scene&&cache.Type==OceanDepthCache.OceanDepthCacheType.Baked&&cache._resolution==64&&cache._layers.value==1<<30&&(bool)SceneGet(cache,"_relative"),"Saved cache prefab configuration changed");
            CheckExample(row.noRealtimeCache&&row.sourceGeometryAbsent&&texture.name==manifest.textureName&&texture.width==64&&texture.height==64&&texture.format==TextureFormat.RFloat&&texture.mipmapCount==1,"Saved texture/source exclusion/realtime cache");
            var read=AsyncGPUReadback.Request(texture,0,TextureFormat.RFloat);read.WaitForCompletion();CheckExample(!read.hasError,"Saved texture readback");var values=read.GetData<float>().ToArray();var bytes=new byte[values.Length*4];Buffer.BlockCopy(values,0,bytes,0,bytes.Length);
            using(var sha=SHA256.Create())row.textureSHA256=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();row.binding=row.textureSHA256==manifest.importedFloatSHA256;
            CheckExample(row.binding&&values.All(float.IsFinite)&&Mathf.Abs(values[32*64+32]+6.5f)<.02f,"Imported baked pixel data differs from archived source");
            var target=ocean._lodDataSeaDepths.DataTexture;var snapped=ocean._lodTransform._renderData[0].Current._posSnapped;float texel=4*ocean.CalcLodScale(0)/target.width;
            int x=Mathf.FloorToInt(-snapped.x/texel+target.width*.5f),y=Mathf.FloorToInt(-snapped.z/texel+target.height*.5f);CheckExample(x>=0&&x<target.width&&y>=0&&y<target.height,"Depth world origin outside sampled cascade");
            var native=AsyncGPUReadback.Request(target,0,TextureFormat.RGFloat);native.WaitForCompletion();CheckExample(!native.hasError,"Native depth readback");float value=native.GetData<Vector2>()[y*target.width+x].x;
            row.actual=value.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
            if(sentinel)CheckExample(value< -10000,"Disabled cache failed to deregister");else CheckExample(float.IsFinite(value)&&Mathf.Abs(value-expected)<.02f,"Saved native depth does not match relative owner height");
            SaveDepthReport();
        }
        IEnumerator SavedDepthPlayer()
        {
            _savedDepthReport=new SavedDepthReport{unity=Application.unityVersion,api=SystemInfo.graphicsDeviceType.ToString(),gpu=SystemInfo.graphicsDeviceName,scope="P11.3-D2 compiled existing EXR/prefab/scene; no rebake/source geometry. Owner movement, deregistration/recovery, actual saved scene reload and old ocean RT release. .02m original tolerance. Not all cache modes/terrain/export GUI/performance/leak-free acceptance."};
            var text=Resources.Load<TextAsset>("CrestSavedDepthManifest");CheckExample(text!=null,"Missing saved depth manifest");_savedDepthReport.provenance=JsonUtility.FromJson<SavedDepthManifest>(text.text);SaveDepthReport();
            Time.timeScale=0;yield return SceneManager.LoadSceneAsync(1);var camera=Camera.main;camera.enabled=false;yield return WaitSavedDepthUpdates();
            var cache=FindAnyObjectByType<OceanDepthCache>();CheckExample(cache!=null,"Missing serialized cache");MeasureSavedDepth(cache,"SavedPrefab",-4.5f);
            cache.transform.position=Vector3.up*5;yield return WaitSavedDepthUpdates();MeasureSavedDepth(cache,"MovedOwner",-1.5f);
            cache.enabled=false;yield return WaitSavedDepthUpdates();MeasureSavedDepth(cache,"Disabled",0,true);
            cache.enabled=true;yield return WaitSavedDepthUpdates();MeasureSavedDepth(cache,"Reenabled",-1.5f);
            var oldOcean=OceanRenderer.Instance;var oldTarget=oldOcean._lodDataSeaDepths.DataTexture;
            yield return SceneManager.LoadSceneAsync(1);camera=Camera.main;camera.enabled=false;yield return WaitSavedDepthUpdates();cache=FindAnyObjectByType<OceanDepthCache>();
            CheckExample(oldOcean==null&&oldTarget==null,"Reload retained previous ocean/depth target");MeasureSavedDepth(cache,"ReloadedSavedPrefab",-4.5f,false,oldOcean==null,oldTarget==null);
            SaveDepthReport();Debug.Log("SAVED_DEPTH_COMPLETED errors="+_errors);Application.Quit(_errors==0?0:1);
        }
    }
}
#endif
