Shader "Custom/PointCloudGPU"
{
    Properties
    {
        _PointSize("Point Size", Float) = 0.01
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<float4> _Positions;
            StructuredBuffer<float4> _Colors;
            float _PointSize;

            struct appdata
            {
                float3 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            v2f vert(appdata v, uint id : SV_InstanceID)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);

                float4 pos = _Positions[id];
                float4 color = _Colors[id];

                // メッシュスケール + 位置
                float4 worldPos = float4(pos.xyz, 1.0);
                o.vertex = TransformObjectToHClip(v.vertex * _PointSize + worldPos);
                o.color = color;

                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                return i.color;
            }
            ENDHLSL
        }
    }
}

