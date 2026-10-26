// Crest 4 URP port. MIT; see LICENSE.Crest.
Shader "Hidden/Crest/Tests/BuiltInShadowReceiver"
{
 Properties {_Color("Color",Color)=(1,1,1,1) _ReceiveShadowStrength("Receive shadow strength",Range(0,1))=0}
 SubShader
 {
  Tags {"RenderType"="Opaque" "Queue"="Geometry"}
  // The uniform keeps the actual BIRP receiver/shadow variant live. Strength0 isolates
  // Crest caustic occlusion from the opaque receiver's own shading in RGB comparisons.
  Pass
  {
   Tags {"LightMode"="ForwardBase"}
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fwdbase
   #include "UnityCG.cginc"
   #include "AutoLight.cginc"
   float4 _Color;float _ReceiveShadowStrength;
   struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;SHADOW_COORDS(1)};
   v2f vert(appdata_base v) {v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;TRANSFER_SHADOW(o);return o;}
   float4 frag(v2f i):SV_Target {float checker=fmod(abs(floor(i.world.x)+floor(i.world.z)),2);return float4(_Color.rgb*lerp(.35,1,checker)*lerp(1,SHADOW_ATTENUATION(i),_ReceiveShadowStrength),1);}
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
