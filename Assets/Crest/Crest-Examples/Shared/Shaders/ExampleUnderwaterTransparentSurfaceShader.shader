// Crest Ocean System. MIT; see LICENSE.
// URP adapter retaining the original material and final-colour fog contract.
Shader "Crest/Examples/UnderwaterTransparentSurfaceShader"
{
	Properties
	{
		_Color ("Color", Color) = (1,1,1,1)
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Metallic ("Metallic", Range(0,1)) = 0.0
		_FogMultiplier ("Fog Multiplier", Range(0, 1)) = 1.0
	}
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        HLSLINCLUDE
        #pragma target 4.5
        #pragma multi_compile_instancing
        #define _BaseColor _Color
        #define _Smoothness _Glossiness
        #define _SURFACE_TYPE_TRANSPARENT 1
        #include "../../../Crest/Shaders/Helpers/URP/Core.hlsl"
        #define InitializeStandardLitSurfaceData CrestUnusedStandardSurfaceData
        #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
        #undef InitializeStandardLitSurfaceData
        half _FogMultiplier;
        void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surface)
        {
            surface=(SurfaceData)0;
            surface.albedo=_Color.rgb;
            surface.alpha=_Color.a;
            surface.metallic=_Metallic;
            surface.smoothness=_Glossiness;
            surface.normalTS=half3(0,0,1);
            surface.occlusion=1;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags {"LightMode"="UniversalForwardOnly"}
            HLSLPROGRAM
            #pragma vertex LitPassVertex
            #pragma fragment CrestTransparentFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fog
            #pragma multi_compile _ CREST_WATER_VOLUME_2D CREST_WATER_VOLUME_HAS_BACKFACE
            #pragma multi_compile _ CREST_SUBSURFACESCATTERING_ON
            #pragma multi_compile _ CREST_SHADOWS_ON
            #include "../../../Crest/Shaders/Underwater/UnderwaterEffectIncludes.hlsl"
            // The package PBR pass must leave fog to the original final-colour callback.
            #define MixFog(colour, factor) colour
            #define LitPassFragment CrestUnfoggedLitFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            #undef LitPassFragment
            #undef MixFog
            void CrestTransparentFragment(Varyings input, out half4 colour : SV_Target0)
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                CrestUnfoggedLitFragment(input,colour);
                float2 positionNDC=GetNormalizedScreenSpaceUV(input.positionCS);
                if(!CrestApplyUnderwaterFog(positionNDC,input.positionWS,input.positionCS.z,_FogMultiplier,colour.rgb))
                {
                    InputData data;
                    InitializeInputData(input,half3(0,0,1),data);
                    colour.rgb=MixFog(colour.rgb,data.fogCoord);
                }
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags {"LightMode"="ShadowCaster"}
            Blend One Zero
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            // The original Diffuse fallback provided an opaque caster for this alpha-blend surface.
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "Meta"
            Tags {"LightMode"="Meta"}
            Blend One Zero
            Cull Off
            HLSLPROGRAM
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaLit
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"
            ENDHLSL
        }
    }
}
