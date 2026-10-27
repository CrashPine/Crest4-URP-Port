// Crest Ocean System. MIT; see LICENSE.
Shader "Crest/Examples/DepthOnly"
{
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Geometry-1"}
  ColorMask 0
  ZWrite On
  Pass
  {
   HLSLPROGRAM
   #pragma vertex Vertex
   #pragma fragment Fragment
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct Attributes {float3 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct Varyings {float4 positionCS:SV_POSITION;UNITY_VERTEX_OUTPUT_STEREO};
   Varyings Vertex(Attributes i) {Varyings o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(i.positionOS);return o;}
   half4 Fragment(Varyings i):SV_Target {return 0;}
   ENDHLSL
  }
 }
}
