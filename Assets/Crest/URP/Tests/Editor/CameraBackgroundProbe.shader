Shader "Hidden/Crest/Tests/CameraBackgroundProbe"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+500" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D_X(_BackgroundTexture);
            float _ProbeDimensions;
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                if (_ProbeDimensions > .5)
                {
                    uint width, height;
                    _BackgroundTexture.GetDimensions(width, height);
                    return half4(width / 1024.0, height / 1024.0, 0, 1);
                }
                return half4(1 - SAMPLE_TEXTURE2D_X(_BackgroundTexture, sampler_LinearClamp, uv).rgb, 1);
            }
            ENDHLSL
        }
    }
}
