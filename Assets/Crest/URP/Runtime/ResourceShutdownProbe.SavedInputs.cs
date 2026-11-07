// Finite compiled native input persistence controls. MIT.
#if CREST_RESOURCE_PROBE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Crest.URP.Diagnostics
{
    public sealed partial class ResourceShutdownProbe
    {
        [Serializable] class SavedInputsManifest{public string scene,sceneGuid,profileGuid,surfaceGuid,profileName,surfaceName,scope;public string[] materialGuids,materialNames,shaderNames;}
        [Serializable] class SavedInputsRow
        {
            public string state;public bool bindings,heightReporter,oldOceanDestroyed,oldHeightDestroyed;public int oldTargetsDestroyed,flowSamples,heightSamples,clipSamples,albedoSamples,frame,lodFrame;
            public Vector2 flowCenter,flowOutside;public float heightCenter,heightOutside,clipCenter,clipExpected,clipOutside;public Color albedoCenter,albedoOutside;
            public bool disabledFieldsZero;public string[] materialIdentities;
        }
        [Serializable] class SavedInputsReport{public string unity,api,gpu,scope,failure;public int errors;public SavedInputsManifest provenance;public List<SavedInputsRow> rows=new List<SavedInputsRow>();}
        SavedInputsReport _savedInputsReport;Camera _inputsCamera;
        void SaveInputsReport(){if(_savedInputsReport==null)return;_savedInputsReport.errors=_errors;File.WriteAllText(Argument("-crestProbeOutput","SavedInputs.json"),JsonUtility.ToJson(_savedInputsReport,true));}
        IEnumerator WaitSavedInputs(int count)
        {
            int previous=BuildCommandBufferBase._lastUpdateFrame,done=0;float deadline=Time.realtimeSinceStartup+15;
            while(done<count&&Time.realtimeSinceStartup<deadline){yield return null;int frame=BuildCommandBufferBase._lastUpdateFrame;if(frame!=previous){previous=frame;done++;}}
            CheckExample(done==count,"Saved inputs require actual ocean updates");
        }
        int SavedInputIndex(RenderTexture target,Vector2 position)
        {
            var ocean=OceanRenderer.Instance;var snapped=ocean._lodTransform._renderData[0].Current._posSnapped;float texel=4*ocean.CalcLodScale(0)/target.width;
            int x=Mathf.FloorToInt((position.x-snapped.x)/texel+target.width*.5f),y=Mathf.FloorToInt((position.y-snapped.z)/texel+target.height*.5f);
            CheckExample(x>=0&&x<target.width&&y>=0&&y<target.height,"Saved input probe outside cascade");return y*target.width+x;
        }
        T[] ReadSavedInput<T>(RenderTexture target,TextureFormat format) where T:struct
        {
            var request=AsyncGPUReadback.Request(target,0,format);request.WaitForCompletion();CheckExample(!request.hasError,"Native saved input readback");return request.GetData<T>(0).ToArray();
        }
        void ToggleSavedInputs(bool enabled)
        {
            GameObject.Find("Persisted Flow").GetComponent<RegisterFlowInput>().enabled=enabled;
            GameObject.Find("Persisted Albedo").GetComponent<RegisterAlbedoInput>().enabled=enabled;
            GameObject.Find("Persisted Height").GetComponent<RegisterHeightInput>().enabled=enabled;
            GameObject.Find("Persisted Clip").GetComponent<RegisterClipSurfaceInput>().enabled=enabled;
        }
        void MeasureSavedInputs(string label,bool enabled,bool oldOcean=false,bool oldHeight=false,int oldTargets=0)
        {
            var ocean=OceanRenderer.Instance;var manifest=_savedInputsReport.provenance;
            var row=new SavedInputsRow{state=label,oldOceanDestroyed=oldOcean,oldHeightDestroyed=oldHeight,oldTargetsDestroyed=oldTargets,frame=OceanRenderer.FrameCount,lodFrame=BuildCommandBufferBase._lastUpdateFrame};_savedInputsReport.rows.Add(row);SaveInputsReport();
            CheckExample(row.frame==row.lodFrame&&SceneManager.GetActiveScene().path==manifest.scene,"Saved scene/capture frame mismatch");
            CheckExample(_inputsCamera.transform.position==new Vector3(0,10,0)&&ReferenceEquals(ocean.TimeProvider,ocean.GetComponent<TimeProviderCustom>())&&ocean.CurrentTime==.5f&&ocean.DeltaTime==0,"Serialized camera/clock binding");
            CheckExample(ocean._simSettingsSeaFloorDepth.name==manifest.profileName&&ocean._simSettingsSeaFloorDepth._allowVaryingWaterLevel&&ocean.OceanMaterial.name==manifest.surfaceName,"Saved profile/surface binding");
            foreach(string keyword in new[]{"_FLOW_ON","_CLIPSURFACE_ON","_ALBEDO_ON"})CheckExample(ocean.OceanMaterial.IsKeywordEnabled(keyword),"Saved surface keyword missing");
            string[] labels={"Flow","Albedo","Height"};row.materialIdentities=new string[3];
            for(int i=0;i<3;i++){var material=GameObject.Find("Persisted "+labels[i]).GetComponent<Renderer>().sharedMaterial;CheckExample(material.name==manifest.materialNames[i]&&material.shader.name==manifest.shaderNames[i],"Serialized input material/shader binding");row.materialIdentities[i]=material.GetEntityId().ToString();}
            var flowMaterial=GameObject.Find("Persisted Flow").GetComponent<Renderer>().sharedMaterial;var albedoMaterial=GameObject.Find("Persisted Albedo").GetComponent<Renderer>().sharedMaterial;
            CheckExample(flowMaterial.GetFloat("_Speed")==3&&flowMaterial.GetFloat("_Direction")==0&&albedoMaterial.GetColor("_Color")==new Color(1,0,0,.75f),"Saved material values");row.bindings=true;
            row.heightReporter=OceanChunkRenderer.HeightReporters.Contains(GameObject.Find("Persisted Height").GetComponent<RegisterHeightInput>());CheckExample(row.heightReporter==enabled,"Height reporter registration");
            var target=ocean._lodDataFlow.DataTexture;var flow=ReadSavedInput<Vector2>(target,TextureFormat.RGFloat);row.flowSamples=flow.Length;row.flowCenter=flow[SavedInputIndex(target,Vector2.zero)];row.flowOutside=flow[SavedInputIndex(target,new Vector2(10,0))];
            CheckExample(Mathf.Abs(row.flowCenter.x-(enabled?3:0))<.01f&&Mathf.Abs(row.flowCenter.y)<.01f&&row.flowOutside.x==0&&row.flowOutside.y==0&&flow.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)),"Native saved flow control");
            target=ocean._lodDataSeaDepths.DataTexture;var height=ReadSavedInput<Vector2>(target,TextureFormat.RGFloat);row.heightSamples=height.Length;row.heightCenter=height[SavedInputIndex(target,Vector2.zero)].y;row.heightOutside=height[SavedInputIndex(target,new Vector2(10,0))].y;
            CheckExample(Mathf.Abs(row.heightCenter-(enabled?2:0))<.01f&&row.heightOutside==0&&height.All(v=>float.IsFinite(v.y)),"Native saved height control");
            // Depth R retains the original no-depth -infinity sentinel; height G must be finite.
            target=ocean._lodDataClipSurface.DataTexture;int center=SavedInputIndex(target,Vector2.zero);var clip=ReadSavedInput<float>(target,TextureFormat.RFloat);row.clipSamples=clip.Length;row.clipCenter=clip[center];row.clipOutside=clip[SavedInputIndex(target,new Vector2(10,0))];
            float texel=4*ocean.CalcLodScale(0)/target.width;var snapped=ocean._lodTransform._renderData[0].Current._posSnapped;float x=(center%target.width+.5f-target.width*.5f)*texel+snapped.x,z=(center/target.width+.5f-target.height*.5f)*texel+snapped.z;
            row.clipExpected=enabled?Mathf.Clamp01(1-Mathf.Max(Mathf.Max(Mathf.Abs(x),Mathf.Abs(z))/8,2f/4)):0;
            CheckExample(Mathf.Abs(row.clipCenter-row.clipExpected)<.005f&&row.clipOutside==0&&clip.All(float.IsFinite),"Saved clip SDF/displaced-height control");
            target=ocean._lodDataAlbedo.DataTexture;var albedo=ReadSavedInput<Color>(target,TextureFormat.RGBAFloat);row.albedoSamples=albedo.Length;row.albedoCenter=albedo[SavedInputIndex(target,Vector2.zero)];row.albedoOutside=albedo[SavedInputIndex(target,new Vector2(10,0))];var expected=enabled?new Color(.75f,0,0,.5625f):Color.clear;
            for(int c=0;c<4;c++){CheckExample(Mathf.Abs(row.albedoCenter[c]-expected[c])<=2f/255&&row.albedoOutside[c]==0&&albedo.All(v=>float.IsFinite(v[c])),"Native saved albedo control");}
            if(!enabled){row.disabledFieldsZero=flow.All(v=>v.x==0&&v.y==0)&&height.All(v=>v.y==0)&&clip.All(v=>v==0)&&albedo.All(v=>v.r==0&&v.g==0&&v.b==0&&v.a==0);CheckExample(row.disabledFieldsZero,"Disabled saved input fields did not clear completely");}
            SaveInputsReport();
        }
        IEnumerator SavedInputsPlayer()
        {
            _savedInputsReport=new SavedInputsReport{unity=Application.unityVersion,api=SystemInfo.graphicsDeviceType.ToString(),gpu=SystemInfo.graphicsDeviceName,scope="P11.3-D3: existing InputPersistence fixture in compiled Player, four saved-scene/reload/toggle states. Native LOD0 fields/profile/material/clock/reporter binding and old4RT release. Original tolerances retained; not all materials/rendered optics/other LODs/physical input/performance/leak-free acceptance."};
            var text=Resources.Load<TextAsset>("CrestSavedInputsManifest");CheckExample(text!=null,"Missing saved input manifest");_savedInputsReport.provenance=JsonUtility.FromJson<SavedInputsManifest>(text.text);SaveInputsReport();Time.timeScale=0;
            yield return SceneManager.LoadSceneAsync(1);_inputsCamera=Camera.main;_inputsCamera.enabled=false;yield return WaitSavedInputs(6);yield return ModeCapture(()=>MeasureSavedInputs("Initial",true));
            var oldOcean=OceanRenderer.Instance;var oldHeight=GameObject.Find("Persisted Height").GetComponent<RegisterHeightInput>();var oldTargets=new[]{oldOcean._lodDataFlow.DataTexture,oldOcean._lodDataClipSurface.DataTexture,oldOcean._lodDataAlbedo.DataTexture,oldOcean._lodDataSeaDepths.DataTexture};
            yield return SceneManager.LoadSceneAsync(1);_inputsCamera=Camera.main;_inputsCamera.enabled=false;yield return WaitSavedInputs(6);
            CheckExample(oldOcean==null&&oldHeight==null&&oldTargets.All(t=>t==null),"Saved reload retained old ocean/input/native RT");yield return ModeCapture(()=>MeasureSavedInputs("Reloaded",true,oldOcean==null,oldHeight==null,oldTargets.Count(t=>t==null)));
            ToggleSavedInputs(false);yield return WaitSavedInputs(4);yield return ModeCapture(()=>MeasureSavedInputs("Disabled",false));
            ToggleSavedInputs(true);yield return WaitSavedInputs(4);yield return ModeCapture(()=>MeasureSavedInputs("Reenabled",true));
            SaveInputsReport();Debug.Log("SAVED_INPUTS_COMPLETED errors="+_errors);Application.Quit(_errors==0?0:1);
        }
    }
}
#endif
