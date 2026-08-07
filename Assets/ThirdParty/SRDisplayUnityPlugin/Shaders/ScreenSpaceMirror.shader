Shader "Hidden/SRD/ScreenSpaceMirror"
{
    Properties
    {
        _BlitTexture ("Blit Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "MirrorBlitPass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            TEXTURE2D(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // X軸(水平方向)を反転
                uv.x = 1.0 - uv.x;

                float4 color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv);
                return color;
            }
            ENDHLSL
        }
    }
}
