// Crest 4 URP port. MIT; see LICENSE.Crest.
Shader "Hidden/Crest/Tests/DualPipelineOpaque"
{
 Properties {_Color("Color",Color)=(1,1,1,1)}
 SubShader
 {
  Tags {"RenderType"="Opaque" "Queue"="Geometry"}
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   float4 _Color;
   struct v2f {float4 position:SV_POSITION;float3 world:TEXCOORD0;};
   v2f vert(appdata_base v) {v2f o;o.position=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o;}
   float4 frag(v2f i):SV_Target {float checker=fmod(abs(floor(i.world.x)+floor(i.world.z)),2);return float4(_Color.rgb*lerp(.35,1,checker),1);}
   ENDCG
  }
  Pass
  {
   Tags {"LightMode"="ShadowCaster"}
   ZWrite On ZTest LEqual
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_shadowcaster
   #include "UnityCG.cginc"
   struct v2f {V2F_SHADOW_CASTER;};
   v2f vert(appdata_base v) {v2f o;TRANSFER_SHADOW_CASTER_NORMALOFFSET(o);return o;}
   float4 frag(v2f i):SV_Target {SHADOW_CASTER_FRAGMENT(i)}
   ENDCG
  }
 }
}
