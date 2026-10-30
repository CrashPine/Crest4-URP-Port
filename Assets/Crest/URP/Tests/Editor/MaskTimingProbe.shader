// Crest port test. MIT; see LICENSE.Crest.
Shader "Hidden/Crest/Tests/MaskTimingProbe"
{
 Properties { [HideInInspector] _ProbeField("Field", Int) = 0 }
 SubShader { Pass {
  ZWrite Off ZTest Always Cull Off
  HLSLPROGRAM
  #pragma vertex Vert
  #pragma fragment Frag
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  TEXTURE2D_X(_CrestOceanMaskTexture);
  TEXTURE2D_X(_CrestWaterVolumeFrontFaceTexture);
  TEXTURE2D_X(_CrestWaterVolumeBackFaceTexture);
  int _ProbeField;
  struct V {float4 positionCS:SV_POSITION;};
  V Vert(uint id:SV_VertexID){V o;o.positionCS=GetFullScreenTriangleVertexPosition(id);return o;}
  float Frag(V i):SV_Target {
   if(_ProbeField==1)return LOAD_TEXTURE2D_X(_CrestWaterVolumeFrontFaceTexture,int2(i.positionCS.xy)).r;
   if(_ProbeField==2)return LOAD_TEXTURE2D_X(_CrestWaterVolumeBackFaceTexture,int2(i.positionCS.xy)).r;
   return LOAD_TEXTURE2D_X(_CrestOceanMaskTexture,int2(i.positionCS.xy)).r;
  }
  ENDHLSL
 } }
}
