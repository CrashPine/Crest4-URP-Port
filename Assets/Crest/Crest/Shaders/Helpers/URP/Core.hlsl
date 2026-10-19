// Crest 4 URP port. MIT; see LICENSE.Crest.
#ifndef CREST_URP_CORE_INCLUDED
#define CREST_URP_CORE_INCLUDED
#define CREST_URP 1
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#define UNITY_INITIALIZE_OUTPUT(type, value) ZERO_INITIALIZE(type, value)
#define UNITY_DECLARE_SCREENSPACE_TEXTURE(name) TEXTURE2D_X(name); SAMPLER(sampler##name)
#define UNITY_SAMPLE_SCREENSPACE_TEXTURE(name, uv) SAMPLE_TEXTURE2D_X(name, sampler##name, UnityStereoTransformScreenSpaceTex(uv))
#define _LightColor0 (GetMainLight().color)
#define _ScreenSize float4(_ScaledScreenParams.xy, rcp(_ScaledScreenParams.xy))
#ifndef SAMPLE_DEPTH_TEXTURE
#define SAMPLE_DEPTH_TEXTURE(name, samplerName, uv) SAMPLE_TEXTURE2D(name, samplerName, uv).r
#endif
#endif
