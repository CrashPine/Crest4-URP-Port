// Crest Ocean System

// This file is subject to the MIT License as seen in the root of this folder structure (LICENSE)

Shader "Crest/Underwater Meniscus"
{
	Properties
	{
		_MeniscusWidth("Meniscus Width", Range(0.0, 100.0)) = 1.0
	}

	SubShader
	{
		Tags{ "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }

		Pass
		{
			Tags { "LightMode" = "UniversalForwardOnly" }

			// Could turn this off, and it would allow the ocean surface to render through it
			ZWrite Off
			//Blend SrcAlpha OneMinusSrcAlpha
			Blend DstColor Zero

			HLSLPROGRAM
			#pragma target 4.5
			#define CREST_URP 1
			#pragma vertex Vert
			#pragma fragment Frag

			#include "UnderwaterMeniscus.hlsl"
			ENDHLSL
		}
	}

	SubShader
	{
		Tags{ "LightMode" = "ForwardBase" "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }

		Pass
		{
			// Could turn this off, and it would allow the ocean surface to render through it
			ZWrite Off
			//Blend SrcAlpha OneMinusSrcAlpha
			Blend DstColor Zero

			CGPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

			#include "UnderwaterMeniscus.hlsl"
			ENDCG
		}
	}
}
