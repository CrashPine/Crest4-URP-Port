// Crest 4 URP port. MIT; see LICENSE.Crest.
Shader "Hidden/Crest/Tests/DualPipelineDepthProbe"
{
 Properties {_Mode("Background mode",Float)=0 _UseUrp("URP screen",Float)=1 _Offset("Sample offset",Vector)=(0,0,0,0)}
 SubShader
 {
  Tags {"RenderType"="Transparent" "Queue"="Overlay"}
  Pass
  {
   ZWrite Off ZTest Always Cull Off
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _CameraDepthTexture;sampler2D _BackgroundTexture;float4 _Offset;float _Mode;float _UseUrp;
   struct v2f {float4 pos:SV_POSITION;float4 screen:TEXCOORD0;float4 grab:TEXCOORD1;};
   v2f vert(appdata_base v) {v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.screen=ComputeScreenPos(o.pos);o.grab=ComputeGrabScreenPos(o.pos);return o;}
   float4 frag(v2f i):SV_Target {float2 uv=i.screen.xy/i.screen.w;if(_Mode>2){float4 screen=lerp(i.grab,i.screen,_UseUrp);return float4(screen.xy/screen.w, i.screen.xy/i.screen.w);}if(_Mode>0){float4 screen=lerp(i.grab,i.screen,_UseUrp);return float4(tex2D(_BackgroundTexture,screen.xy/screen.w+_Offset.xy).rgb,1);}float raw=SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,uv+_Offset.xy);return float4(raw,LinearEyeDepth(raw),uv);}
   ENDCG
  }
 }
}
