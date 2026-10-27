// Crest port test. MIT; see LICENSE.Crest.
Shader "Hidden/Crest/Tests/MaskTimingProbe"
{
 SubShader { Pass {
  ZWrite Off ZTest Always Cull Off
  HLSLPROGRAM
  #pragma vertex Vert
  #pragma fragment Frag
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  TEXTURE2D_X(_CrestOceanMaskTexture);
  struct V {float4 positionCS:SV_POSITION;};
  V Vert(uint id:SV_VertexID){V o;o.positionCS=GetFullScreenTriangleVertexPosition(id);return o;}
  float Frag(V i):SV_Target {return LOAD_TEXTURE2D_X(_CrestOceanMaskTexture,int2(i.positionCS.xy)).r;}
  ENDHLSL
 } }
}
