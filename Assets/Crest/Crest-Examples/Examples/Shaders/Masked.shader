// Crest Ocean System. MIT; see LICENSE.
// URP adapter; original material properties and clipping/emission equations retained.
Shader "Crest/Examples/Masked"
{
	Properties
	{
		_Color("Color", Color) = (1.0, 1.0, 1.0, 1.0)
		[Toggle] _Emission("Emission", Float) = 0.0
		[HDR] _EmissionColor("Color", Color) = (0.0, 0.0, 0.0)
		_Albedo("Albedo (RGB)", 2D) = "white" {}
		_Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
		_Metallic("Metallic", Range(0.0, 1.0)) = 0.0
		_Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
		[KeywordEnum(None, Masked, Fill)] _Mask("Masked", Float) = 1.0
	}

    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
        ZWrite On

    HLSLINCLUDE
    #pragma target 4.5
    #pragma multi_compile_instancing
    #pragma shader_feature_local _MASK_NONE _MASK_MASKED _MASK_FILL
    #pragma shader_feature_local_fragment _EMISSION_ON
    // Preserve original material names while reusing the installed URP PBR/lighting passes.
    #define _BaseMap _Albedo
    #define sampler_BaseMap sampler_Albedo
    #define _BaseMap_ST _Albedo_ST
    #define _BaseMap_TexelSize _Albedo_TexelSize
    #define _BaseColor _Color
    #define _ALPHATEST_ON 1
    #define InitializeStandardLitSurfaceData CrestUnusedStandardSurfaceData
    #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
    #undef InitializeStandardLitSurfaceData
    TEXTURE2D_X(_CrestOceanMaskTexture);
    TEXTURE2D_X(_CrestWaterVolumeFrontFaceTexture);
    TEXTURE2D_X(_CrestWaterVolumeBackFaceTexture);
    TEXTURE2D_X(_FillTexture);
    void CrestExampleClip(float4 positionCS)
    {
        float2 uv = positionCS.xy / _ScaledScreenParams.xy;
    #if defined(_MASK_MASKED)
        float front = SAMPLE_TEXTURE2D_X_LOD(_CrestWaterVolumeFrontFaceTexture, sampler_LinearClamp, uv, 0).r;
        float back = SAMPLE_TEXTURE2D_X_LOD(_CrestWaterVolumeBackFaceTexture, sampler_LinearClamp, uv, 0).r;
        if ((front < positionCS.z && front > 0.0) || (back > positionCS.z && back > 0.0)) discard;
        if (SAMPLE_TEXTURE2D_X_LOD(_CrestOceanMaskTexture, sampler_LinearClamp, uv, 0).r == 0.0) discard;
    #elif defined(_MASK_FILL)
        if (SAMPLE_TEXTURE2D_X_LOD(_FillTexture, sampler_LinearClamp, uv, 0).r == 0.0) discard;
    #endif
    }
    void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surface)
    {
        half4 albedo = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, uv);
        half4 colour = albedo * _Color;
        clip(colour.a - _Cutoff);
        surface = (SurfaceData)0;
        surface.albedo = colour.rgb;
        surface.alpha = colour.a;
        surface.normalTS = half3(0,0,1);
        surface.metallic = _Metallic;
        surface.smoothness = _Smoothness;
        surface.occlusion = 1;
    #if defined(_EMISSION_ON)
        surface.emission = colour.rgb * albedo.a * _EmissionColor.rgb;
    #endif
    }
    ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags {"LightMode"="UniversalForwardOnly"}
            HLSLPROGRAM
            #pragma vertex LitPassVertex
            #pragma fragment CrestExampleFragment
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
            #define LitPassFragment CrestUnmaskedLitFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            #undef LitPassFragment
            void CrestExampleFragment(Varyings input, out half4 colour : SV_Target0)
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                CrestExampleClip(input.positionCS);
                CrestUnmaskedLitFragment(input, colour);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags {"LightMode"="DepthOnly"}
            ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment CrestExampleDepth
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            half CrestExampleDepth(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                CrestExampleClip(input.positionCS);
                return DepthOnlyFragment(input);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags {"LightMode"="DepthNormalsOnly"}
            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment CrestExampleNormals
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            void CrestExampleNormals(Varyings input, out half4 normal : SV_Target0)
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                CrestExampleClip(input.positionCS);
                DepthNormalsFragment(input, normal);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags {"LightMode"="ShadowCaster"}
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            // A shadow camera must not sample the view camera's screen-space clipping mask.
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "Meta"
            Tags {"LightMode"="Meta"}
            Cull Off
            HLSLPROGRAM
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaLit
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"
            ENDHLSL
        }
    }
}
