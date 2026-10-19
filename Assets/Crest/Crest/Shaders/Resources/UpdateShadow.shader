// Crest Ocean System. MIT; see LICENSE.
// URP sampling adapter. The original temporal/jittered accumulation lives in UpdateShadow.hlsl.
Shader "Hidden/Crest/Simulation/Update Shadow"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #define _SURFACE_TYPE_TRANSPARENT 1
            #define CREST_SAMPLE_SHADOW_HARD
            #include "../Helpers/URP/Core.hlsl"
            #include "../ShaderLibrary/UpdateShadow.hlsl"

            half CrestSampleShadows(const float4 positionWS)
            {
                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                float4 coordinates = TransformWorldToShadowCoord(positionWS.xyz);
                // Crest applies its own world-space jitter and temporal softening.
                half4 parameters = GetMainLightShadowParams();
                parameters.y = 0;
                return SampleShadowmap(TEXTURE2D_SHADOW_ARGS(_MainLightShadowmapTexture, sampler_LinearClampCompare),
                    coordinates, GetMainLightShadowSamplingData(), parameters, false);
                #else
                return 1;
                #endif
            }
            half CrestComputeShadowFade(const float4 positionWS)
            {
                return GetMainLightShadowFade(positionWS.xyz);
            }
            ENDHLSL
        }
    }
}
