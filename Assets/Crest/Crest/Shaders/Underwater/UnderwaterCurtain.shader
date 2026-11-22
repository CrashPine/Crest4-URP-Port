// Crest Ocean System

// This file is subject to the MIT License as seen in the root of this folder structure (LICENSE)

Shader "Crest/Underwater Curtain"
{
	Properties
	{
		// Most properties are copied over from the main ocean material on startup

		// Shader features need to be statically configured - it works to dynamically configure them in editor, but in standalone
		// builds they need to be preconfigured. This is a pitfall unfortunately - the settings need to be manually matched.
		[Toggle] _Shadows("Shadowing", Float) = 0
		[Toggle] _SubSurfaceScattering("Sub-Surface Scattering", Float) = 1
		[Toggle] _SubSurfaceShallowColour("Sub-Surface Shallow Colour", Float) = 1
		[Toggle] _Transparency("Transparency", Float) = 1
		[Toggle] _Caustics("Caustics", Float) = 1
	}

	SubShader
	{
		Tags{ "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+510" "IgnoreProjector" = "True" "RenderType" = "Transparent" }

		Pass
		{
			Tags { "LightMode" = "UniversalForwardOnly" }

			// The ocean surface will render after the skirt, and overwrite the pixels
			ZWrite Off
			ZTest Always

			HLSLPROGRAM
			#pragma target 4.5
			#define CREST_URP 1
			#pragma vertex Vert
			#pragma fragment Frag

			// Use multi_compile because these keywords are copied over from the ocean material. With shader_feature,
			// the keywords would be stripped from builds. Unused shader variants are stripped using a build processor.
			#pragma multi_compile_local __ _SUBSURFACESCATTERING_ON
			#pragma multi_compile_local __ _SUBSURFACESHALLOWCOLOUR_ON
			#pragma multi_compile_local __ _TRANSPARENCY_ON
			#pragma multi_compile_local __ _CAUSTICS_ON
			#pragma multi_compile_local __ _SHADOWS_ON

			#include "UnderwaterCurtain.hlsl"
			ENDHLSL
		}
	}

	SubShader
	{
		Tags{ "LightMode" = "ForwardBase" "Queue" = "Geometry+510" "IgnoreProjector" = "True" "RenderType" = "Opaque" }

		GrabPass
		{
			"_BackgroundTexture"
		}

		Pass
		{
			// The ocean surface will render after the skirt, and overwrite the pixels
			ZWrite Off
			ZTest Always

			CGPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

			// Use multi_compile because these keywords are copied over from the ocean material. With shader_feature,
			// the keywords would be stripped from builds. Unused shader variants are stripped using a build processor.
			#pragma multi_compile_local __ _SUBSURFACESCATTERING_ON
			#pragma multi_compile_local __ _SUBSURFACESHALLOWCOLOUR_ON
			#pragma multi_compile_local __ _TRANSPARENCY_ON
			#pragma multi_compile_local __ _CAUSTICS_ON
			#pragma multi_compile_local __ _SHADOWS_ON

			#include "UnderwaterCurtain.hlsl"
			ENDCG
		}
	}
}
