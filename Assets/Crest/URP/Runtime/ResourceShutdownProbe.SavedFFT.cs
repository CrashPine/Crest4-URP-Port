// Saved-scene compiled FFT transfer; scalar reference follows BakedFFTIntegrationTests. MIT.
#if CREST_RESOURCE_PROBE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Crest.URP.Diagnostics
{
    public sealed partial class ResourceShutdownProbe
    {
        [Serializable] class SavedFFTManifest
        {
            public string scene,sceneGuid,settingsGuid,dataGuid,spectrumGuid,settingsName,dataName,rawHalfSHA256;
            public int rawHalfCount;
        }
        [Serializable] class SavedFFTRow
        {
            public string state,provider,rawHalfSHA256;public int samples,readyPackets,releasedGPUBufferCount,releasedCPUPositionCount;
            public float maxHeightError,maxNormalError,maxVelocityError,maxDisplacementError,horizontalPeak;
            public bool serializedBinding,oldOceanDestroyed,velocityChecked;
        }
        [Serializable] class SavedFFTReport
        {
            public string unity,api,gpu,scope,failure;public int errors;public SavedFFTManifest provenance;
            public List<SavedFFTRow> rows=new List<SavedFFTRow>();
        }
        SavedFFTReport _savedFFTReport;FFTBakedData _savedData;float[] _savedRaw;Camera _savedCamera;
        void SaveFFTReport()
        {
            if(_savedFFTReport==null)return;_savedFFTReport.errors=_errors;
            File.WriteAllText(Argument("-crestProbeOutput","SavedFFT.json"),JsonUtility.ToJson(_savedFFTReport,true));
        }
        SavedFFTRow FFTState(string label)
        {
            var row=new SavedFFTRow{state=label,provider=OceanRenderer.Instance.CollisionProvider.GetType().Name};_savedFFTReport.rows.Add(row);SaveFFTReport();return row;
        }
        void VerifySavedFFTBinding(SavedFFTRow row)
        {
            var ocean=OceanRenderer.Instance;var manifest=_savedFFTReport.provenance;
            var settings=ocean._simSettingsAnimatedWaves;_savedData=settings._bakedFFTData;
            CheckExample(SceneManager.GetActiveScene().path==manifest.scene&&settings.name==manifest.settingsName&&_savedData.name==manifest.dataName,"Actual saved scene/profile/data binding");
            var clock=ocean.GetComponent<TimeProviderCustom>();
            CheckExample(ocean.TimeProvider==clock&&ocean.DeltaTime==0&&_savedCamera.transform.position==new Vector3(0,4,0),"Saved clock/camera reference");
            var p=_savedData._parameters;CheckExample(p._frameCount==16&&p._textureResolution==16&&p._lodCount==2&&p._firstLod==7&&p._period==4,"Serialized bake parameters changed");
            var raw=(Array)SceneGet(_savedData,"_framesFlattened");CheckExample(raw.Length==manifest.rawHalfCount,"Serialized raw-half count");
            var halfType=raw.GetType().GetElementType();var valueField=halfType.GetField("value");
            var conversion=halfType.GetMethods(BindingFlags.Static|BindingFlags.Public).Single(m=>m.Name=="op_Implicit"&&m.ReturnType==typeof(float)&&m.GetParameters().Length==1&&m.GetParameters()[0].ParameterType==halfType);
            byte[] bytes=new byte[raw.Length*2];_savedRaw=new float[raw.Length];
            for(int i=0;i<raw.Length;i++){object half=raw.GetValue(i);ushort bits=(ushort)valueField.GetValue(half);bytes[i*2]=(byte)bits;bytes[i*2+1]=(byte)(bits>>8);_savedRaw[i]=(float)conversion.Invoke(null,new[]{half});}
            using(var sha=SHA256.Create())row.rawHalfSHA256=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
            row.serializedBinding=row.rawHalfSHA256==manifest.rawHalfSHA256;SaveFFTReport();
            CheckExample(row.serializedBinding&&_savedRaw.All(float.IsFinite),"Serialized baked half bytes do not match Editor provenance");
            // Editor GUIDs are provenance from the archived serialized fixture, not a
            // simulated AssetDatabase lookup in Player. Binding and bytes are observed here.
        }
        IEnumerator WaitSavedProvider(Type type)
        {
            float deadline=Time.realtimeSinceStartup+15;
            do{yield return null;if(OceanRenderer.Instance.CollisionProvider?.GetType()==type)yield break;}while(Time.realtimeSinceStartup<deadline);
            CheckExample(false,"Automatic saved settings rebuild did not select "+type.Name);
        }
        ComputeBuffer[] SavedGPUOwners(ICollProvider provider)
        {
            var buffers=new List<ComputeBuffer>();
            for(var type=provider.GetType();type!=null;type=type.BaseType)
                foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly))
                    if(field.FieldType==typeof(ComputeBuffer)&&field.GetValue(provider) is ComputeBuffer buffer)buffers.Add(buffer);
            CheckExample(buffers.Count>0&&buffers.All(b=>b.IsValid()),"GPU provider must own valid buffers before switching");return buffers.ToArray();
        }
        Array[] SavedCPUOwners(ICollProvider provider)
        {
            var arrays=new List<Array>();
            foreach(string field in new[]{"_queryDataHeights","_queryDataDisps","_queryDataNorms","_queryDataVels"})
            {
                var query=SceneGet(provider,field);foreach(string positions in new[]{"_queryPositionQuadsX","_queryPositionQuadsZ"})arrays.Add((Array)SceneGet(query,positions));
            }
            CheckExample(arrays.All(a=>Enumerable.Range(0,a.Length).All(i=>SavedNativeCreated(a.GetValue(i)))),"CPU native positions must exist before release");return arrays.ToArray();
        }
        static bool SavedNativeCreated(object value)=>(bool)value.GetType().GetProperty("IsCreated").GetValue(value);
        int AssertSavedCPUReleased(Array[] arrays)
        {
            int count=0;foreach(var array in arrays)for(int i=0;i<array.Length;i++){CheckExample(!SavedNativeCreated(array.GetValue(i)),"Old CPU native position array still created");count++;}return count;
        }
        IEnumerator CheckSavedFlatQueries(SavedFFTRow row)
        {
            var ocean=OceanRenderer.Instance;var points=new[]{Vector3.zero,new Vector3(2,0,1),new Vector3(-1,0,-3)};var heights=new float[3];var normals=new Vector3[3];var velocities=new Vector3[3];var displacements=new Vector3[3];
            // GPU velocity requires advancing Unity timestamps; the CPU ocean-time
            // derivative below deliberately uses timeScale0. Do not conflate them.
            row.velocityChecked=ocean.CollisionProvider is CollProviderNull;if(!row.velocityChecked)row.maxVelocityError=-1;
            float deadline=Time.realtimeSinceStartup+15;int ready=0;
            while(ready<4&&Time.realtimeSinceStartup<deadline)
            {
                yield return null;
                int hs=ocean.CollisionProvider.Query(9101,0,points,heights,normals,row.velocityChecked?velocities:null),ds=ocean.CollisionProvider.Query(9102,0,points,displacements,null,null);
                if(hs!=0||ds!=0){ready=0;continue;}ready++;
                for(int i=0;i<3;i++)
                {
                    CheckExample(float.IsFinite(heights[i])&&float.IsFinite(normals[i].x)&&float.IsFinite(normals[i].y)&&float.IsFinite(normals[i].z)&&float.IsFinite(velocities[i].sqrMagnitude)&&float.IsFinite(displacements[i].sqrMagnitude),"Recovered public query returned nonfinite output");
                    row.maxHeightError=Mathf.Max(row.maxHeightError,Mathf.Abs(heights[i]));row.maxNormalError=Mathf.Max(row.maxNormalError,Vector3.Distance(normals[i],Vector3.up));if(row.velocityChecked)row.maxVelocityError=Mathf.Max(row.maxVelocityError,velocities[i].magnitude);row.maxDisplacementError=Mathf.Max(row.maxDisplacementError,displacements[i].magnitude);row.samples++;
                }
            }
            row.readyPackets=ready;SaveFFTReport();CheckExample(ready==4&&row.maxHeightError<.01f&&row.maxNormalError<.02f&&row.maxVelocityError<.1f&&row.maxDisplacementError<.0001f,"Recovered source-off GPU/None public queries");
        }
        IEnumerator CheckSavedCPU(SavedFFTRow row,float[] times)
        {
            row.velocityChecked=true;
            var ocean=OceanRenderer.Instance;var clock=ocean.GetComponent<TimeProviderCustom>();
            CheckExample(((CollProviderBakedFFT)ocean.CollisionProvider)._data==_savedData,"Selected CPU provider did not use serialized baked data");
            var points=new[]{new Vector3(-21.5f,0,-3.25f),Vector3.zero,new Vector3(.5f,0,.5f),new Vector3(4.2f,0,-7.3f),new Vector3(13.1f,0,22.7f)};
            var heights=new float[5];var normals=new Vector3[5];var velocities=new Vector3[5];var displacements=new Vector3[5];
            foreach(float time in times)
            {
                clock._time=time;int ready=0;float deadline=Time.realtimeSinceStartup+15;
                while(ready<4&&Time.realtimeSinceStartup<deadline)
                {
                    yield return null;
                    int hs=ocean.CollisionProvider.Query(9201,0,points,heights,normals,velocities),ds=ocean.CollisionProvider.Query(9202,0,points,displacements,null,null);ready=hs==0&&ds==0?ready+1:0;
                }
                CheckExample(ready==4,"Saved CPU queries did not become ready");row.readyPackets+=ready;
                for(int i=0;i<5;i++)
                {
                    var point=new Vector2(points[i].x,points[i].z);float h=SavedHeight(point,time);
                    var normal=new Vector3(h-SavedHeight(point+Vector2.right*.1f,time),.1f,h-SavedHeight(point+Vector2.up*.1f,time)).normalized;
                    float velocity=(h-SavedHeight(point,time-.06f))/.06f;var displacement=SavedDisplacement(point,time);
                    float he=Mathf.Abs(heights[i]-h-ocean.SeaLevel),ne=Vector3.Distance(normals[i],normal),ve=Vector3.Distance(velocities[i],Vector3.up*velocity),de=Vector3.Distance(displacements[i],displacement);
                    row.samples++;row.maxHeightError=Mathf.Max(row.maxHeightError,he);row.maxNormalError=Mathf.Max(row.maxNormalError,ne);row.maxVelocityError=Mathf.Max(row.maxVelocityError,ve);row.maxDisplacementError=Mathf.Max(row.maxDisplacementError,de);row.horizontalPeak=Mathf.Max(row.horizontalPeak,new Vector2(displacements[i].x,displacements[i].z).magnitude);SaveFFTReport();
                    CheckExample(float.IsFinite(he)&&float.IsFinite(ne)&&float.IsFinite(ve)&&float.IsFinite(de)&&he<.01f&&ne<.02f&&ve<.1f&&de<.0001f,"Saved CPU query differs from independent raw-frame scalar/Newton reference");
                }
            }
            CheckExample(row.horizontalPeak>.01f,"Saved choppy fixture must exercise nonzero horizontal displacement");SaveFFTReport();
        }
    float SavedHeight(Vector2 position, float time)
    {
        var p = _savedData._parameters;
        float frame = Mathf.Repeat(time, p._period) * p._frameCount / p._period;
        int first = Mathf.FloorToInt(frame);
        return Mathf.Lerp(SavedInvertedFrameHeight(position, first), SavedInvertedFrameHeight(position, (first + 1) % p._frameCount), frame - first);
    }

    float SavedInvertedFrameHeight(Vector2 position, int frame)
    {
        var undisplaced = position;
        for (int iteration = 0; iteration < 12; iteration++)
        {
            var displacement = SavedFrameDisplacement(undisplaced, frame);
            var error = undisplaced + new Vector2(displacement.x, displacement.z) - position;
            if (error.magnitude < .000005f) break;
            const float epsilon = .001f;
            var dx = (SavedFrameDisplacement(undisplaced + Vector2.right * epsilon, frame) - displacement) / epsilon;
            var dz = (SavedFrameDisplacement(undisplaced + Vector2.up * epsilon, frame) - displacement) / epsilon;
            float a = 1 + dx.x, b = dz.x, c = dx.z, d = 1 + dz.z;
            float determinant = a * d - b * c;
            CheckExample(Mathf.Abs(determinant)>.1f,"Reference mapping must remain locally invertible");
            undisplaced -= new Vector2(d * error.x - b * error.y, -c * error.x + a * error.y) / determinant;
        }
        var final = SavedFrameDisplacement(undisplaced, frame);
        CheckExample((undisplaced+new Vector2(final.x,final.z)-position).magnitude<.00005f,"Scalar Newton reference failed convergence");
        return final.y;
    }

    Vector3 SavedDisplacement(Vector2 point, float time)
    {
        var p = _savedData._parameters;
        float frame = Mathf.Repeat(time, p._period) * p._frameCount / p._period;
        int first = Mathf.FloorToInt(frame);
        return Vector3.Lerp(SavedFrameDisplacement(point, first), SavedFrameDisplacement(point, (first + 1) % p._frameCount), frame - first);
    }

    Vector3 SavedFrameDisplacement(Vector2 point, int frame)
    {
        var p = _savedData._parameters;
        Vector3 result = Vector3.zero;
        for (int lod = 0; lod < p._lodCount; lod++)
        {
            float width = .5f * (1 << (p._firstLod + lod));
            float u = Mathf.Repeat(point.x / width * p._textureResolution - .5f, p._textureResolution);
            float v = Mathf.Repeat(point.y / width * p._textureResolution - .5f, p._textureResolution);
            int x = Mathf.FloorToInt(u), z = Mathf.FloorToInt(v);
            int x1 = (x + 1) % p._textureResolution, z1 = (z + 1) % p._textureResolution;
            for (int channel = 0; channel < 3; channel++)
            {
                int offset = ((frame * p._lodCount + lod) * p._textureResolution * p._textureResolution) * 4;
                float a = _savedRaw[offset + (z * p._textureResolution + x) * 4 + channel];
                float b = _savedRaw[offset + (z * p._textureResolution + x1) * 4 + channel];
                float c = _savedRaw[offset + (z1 * p._textureResolution + x) * 4 + channel];
                float d = _savedRaw[offset + (z1 * p._textureResolution + x1) * 4 + channel];
                result[channel] += Mathf.Lerp(Mathf.Lerp(a, b, u - x), Mathf.Lerp(c, d, u - x), v - z);
            }
        }
        return result;
    }

        IEnumerator SavedFFTPlayer()
        {
            _savedFFTReport=new SavedFFTReport{unity=Application.unityVersion,api=SystemInfo.graphicsDeviceType.ToString(),gpu=SystemInfo.graphicsDeviceName,
                scope="P11.3-D1 compiled saved FFT/profile/clock/scene; raw-half provenance, original bounded scalar/Newton CPU reference, GPU/CPU/None rebuild/release/public recovery. Source-off flat GPU control; no rendered FFT/GPU visual parity, saturation, arbitrary strong-fold/performance acceptance."};
            var text=Resources.Load<TextAsset>("CrestSavedFFTManifest");CheckExample(text!=null,"Missing serialized fixture provenance");_savedFFTReport.provenance=JsonUtility.FromJson<SavedFFTManifest>(text.text);SaveFFTReport();
            Time.timeScale=0;yield return SceneManager.LoadSceneAsync(1);_savedCamera=Camera.main;_savedCamera.enabled=false;
            yield return WaitSavedProvider(typeof(QueryDisplacements));var ocean=OceanRenderer.Instance;var settings=ocean._simSettingsAnimatedWaves;
            var initial=FFTState("SavedGPU");VerifySavedFFTBinding(initial);yield return CheckSavedFlatQueries(initial);
            var oldGPU=SavedGPUOwners(ocean.CollisionProvider);settings.CollisionSource=SimSettingsAnimatedWaves.CollisionSources.BakedFFT;yield return WaitSavedProvider(typeof(CollProviderBakedFFT));
            var cpu=FFTState("CPU-six-times");CheckExample(oldGPU.All(b=>!b.IsValid()),"Old saved GPU query buffer retained");cpu.releasedGPUBufferCount=oldGPU.Length;
            VerifySavedFFTBinding(cpu);yield return CheckSavedCPU(cpu,new[]{0f,.125f,1f,3.875f,4.125f,-.125f});
            var oldCPU=SavedCPUOwners(ocean.CollisionProvider);settings.CollisionSource=SimSettingsAnimatedWaves.CollisionSources.None;yield return WaitSavedProvider(typeof(CollProviderNull));
            var none=FFTState("None");none.releasedCPUPositionCount=AssertSavedCPUReleased(oldCPU);yield return CheckSavedFlatQueries(none);
            settings.CollisionSource=SimSettingsAnimatedWaves.CollisionSources.ComputeShaderQueries;yield return WaitSavedProvider(typeof(QueryDisplacements));var returnedGPU=FFTState("RecoveredGPU");yield return CheckSavedFlatQueries(returnedGPU);
            oldGPU=SavedGPUOwners(ocean.CollisionProvider);settings.CollisionSource=SimSettingsAnimatedWaves.CollisionSources.BakedFFT;yield return WaitSavedProvider(typeof(CollProviderBakedFFT));
            var returnedCPU=FFTState("ReturnedCPU-sea125");CheckExample(oldGPU.All(b=>!b.IsValid()),"Returned GPU buffers retained");returnedCPU.releasedGPUBufferCount=oldGPU.Length;
            ocean.transform.position+=Vector3.up*1.25f;yield return null;CheckExample(Mathf.Abs(ocean.SeaLevel-1.25f)<.001f,"Sea-level state");yield return CheckSavedCPU(returnedCPU,new[]{.125f});
            oldCPU=SavedCPUOwners(ocean.CollisionProvider);var previous=ocean;
            // Restore serialized source mode before reload; no profile/data reassignment.
            settings.CollisionSource=SimSettingsAnimatedWaves.CollisionSources.ComputeShaderQueries;
            yield return SceneManager.LoadSceneAsync(1);_savedCamera=Camera.main;_savedCamera.enabled=false;yield return WaitSavedProvider(typeof(QueryDisplacements));
            var reloaded=FFTState("Reloaded-saved-binding");reloaded.oldOceanDestroyed=previous==null;CheckExample(reloaded.oldOceanDestroyed,"Saved scene did not destroy old ocean");reloaded.releasedCPUPositionCount=AssertSavedCPUReleased(oldCPU);VerifySavedFFTBinding(reloaded);yield return CheckSavedFlatQueries(reloaded);
            ocean=OceanRenderer.Instance;settings=ocean._simSettingsAnimatedWaves;oldGPU=SavedGPUOwners(ocean.CollisionProvider);
            settings.CollisionSource=SimSettingsAnimatedWaves.CollisionSources.BakedFFT;yield return WaitSavedProvider(typeof(CollProviderBakedFFT));
            var reloadCPU=FFTState("ReloadedCPU");CheckExample(oldGPU.All(b=>!b.IsValid()),"Reload GPU buffers retained");reloadCPU.releasedGPUBufferCount=oldGPU.Length;VerifySavedFFTBinding(reloadCPU);yield return CheckSavedCPU(reloadCPU,new[]{.125f});
            SaveFFTReport();Debug.Log("SAVED_FFT_COMPLETED errors="+_errors);Application.Quit(_errors==0?0:1);
        }
    }
}
#endif
