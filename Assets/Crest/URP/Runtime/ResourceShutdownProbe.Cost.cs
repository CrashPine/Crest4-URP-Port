// Scoped workload setup for the existing offscreen profiler. Diagnostic Player only. MIT.
#if CREST_RESOURCE_PROBE
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Crest.URP.Diagnostics
{
    public sealed partial class ResourceShutdownProbe
    {
        [Serializable] class WorkloadCost
        {
            public long compositeRecorded, compositeExecuted, preserveRecorded, preserveExecuted;
            public int colorWidth, colorHeight, colorSamples, colorFormat, targetSamples;
            public bool underwaterEnabled, underwaterActive;
            public float viewerHeight;
        }
        Material _costSurface;
        UnderwaterRenderer _costUnderwater;
        string _costWorkload;
        int _costSamples=1;

        IEnumerator SetupCost(string mode)
        {
            _costWorkload=Argument("-crestCost","None");
            if(_costWorkload=="None")yield break;
            if(mode!="Ocean"||(_costWorkload!="UnderwaterOff"&&_costWorkload!="Underwater1"&&_costWorkload!="Underwater4"))
                throw new ArgumentException("Unknown scoped cost workload, or mode is not Ocean");
            _costSamples=_costWorkload=="Underwater4"?4:1;
            ((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).msaaSampleCount=_costSamples;
            _profileCamera.allowMSAA=true;
            _profileCamera.transform.SetPositionAndRotation(new Vector3(0,-3,0),Quaternion.identity);
            _profileCamera.cullingMask=~0;
            var ocean=OceanRenderer.Instance; ocean.ViewCamera=_profileCamera;
            _costSurface=new Material(ocean.OceanMaterial);
            _costSurface.EnableKeyword("_UNDERWATER_ON");_costSurface.SetFloat("_Underwater",1);
            ocean.OceanMaterial=_costSurface;
            _costUnderwater=_profileCamera.gameObject.GetComponent<UnderwaterRenderer>();
            if(_costUnderwater==null)_costUnderwater=_profileCamera.gameObject.AddComponent<UnderwaterRenderer>();
            _costUnderwater.EnableShaderAPI=false;
            _costUnderwater.enabled=_costWorkload!="UnderwaterOff";
            // Original viewer-height/LOD state must settle after moving the camera.
            for(int i=0;i<24;i++)yield return null;
        }

        WorkloadCost ReadCost(RenderTexture target)
        {
            return new WorkloadCost {
                compositeRecorded=UnderwaterRenderer.DiagnosticCompositeRecords,
                compositeExecuted=UnderwaterRenderer.DiagnosticCompositeExecutions,
                preserveRecorded=UnderwaterRenderer.DiagnosticPreserveRecords,
                preserveExecuted=UnderwaterRenderer.DiagnosticPreserveExecutions,
                colorWidth=UnderwaterRenderer.DiagnosticWidth,colorHeight=UnderwaterRenderer.DiagnosticHeight,
                colorSamples=UnderwaterRenderer.DiagnosticSamples,colorFormat=UnderwaterRenderer.DiagnosticColorFormat,
                targetSamples=target.antiAliasing,
                underwaterEnabled=_costUnderwater!=null&&_costUnderwater.isActiveAndEnabled,
                underwaterActive=_costUnderwater!=null&&_costUnderwater.IsActive,
                viewerHeight=OceanRenderer.Instance!=null?OceanRenderer.Instance.ViewerHeightAboveWater:0
            };
        }

        void ValidateCost(ProfileResult result)
        {
            if(_costWorkload=="None")return;
            var a=result.beforeCost;var b=result.afterCost;long expected=_costWorkload=="UnderwaterOff"?0:result.sampleRequests;
            long preserve=_costWorkload=="Underwater4"?expected:0;
            if(result.sampleRequests<20||b.compositeRecorded-a.compositeRecorded!=expected||b.compositeExecuted-a.compositeExecuted!=expected
                ||b.preserveRecorded-a.preserveRecorded!=preserve||b.preserveExecuted-a.preserveExecuted!=preserve
                ||a.targetSamples!=_costSamples||b.targetSamples!=_costSamples||Mathf.Abs(b.viewerHeight+3)>1)
                _errors++;
            if(expected>0&&(!b.underwaterEnabled||!b.underwaterActive||b.colorSamples!=_costSamples||b.colorWidth!=result.width||b.colorHeight!=result.height))_errors++;
        }
    }
}
#endif
