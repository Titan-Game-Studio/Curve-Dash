Shader "Hidden/CurveDash/PickupOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 0.8, 0, 1)
        _OutlineWidth ("Outline Width", Float) = 2.0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        // Pass 0: SolidMask
        Pass
        {
            Name "SolidMask"
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return half4(1, 1, 1, 1);
            }
            ENDHLSL
        }

        // Pass 1: OutlineComposite
        Pass
        {
            Name "OutlineComposite"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha // Alpha Blending so we don't need to read Camera Color

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D_X(_SilhouetteMask);
            SAMPLER(sampler_SilhouetteMask);

            float4 _SilhouetteMask_TexelSize;
            float4 _OutlineColor;
            float _OutlineWidth;

            Varyings vert(Attributes input)
            {
                Varyings output;
                // Generate fullscreen triangle
                float x = -1.0 + float((input.vertexID & 1) << 2);
                float y = -1.0 + float((input.vertexID & 2) << 1);
                output.positionCS = float4(x, y, 0.0, 1.0);
                output.uv = float2((x + 1.0) * 0.5, (y + 1.0) * 0.5);
                
                #if UNITY_UV_STARTS_AT_TOP
                output.uv.y = 1.0 - output.uv.y;
                #endif

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.uv;

                float maskCenter = SAMPLE_TEXTURE2D_X(_SilhouetteMask, sampler_SilhouetteMask, uv).r;

                // Do not draw outline inside the object
                if (maskCenter > 0.5) return half4(0, 0, 0, 0);

                float edge = 0;
                float2 offset = _SilhouetteMask_TexelSize.xy * _OutlineWidth;

                edge += SAMPLE_TEXTURE2D_X(_SilhouetteMask, sampler_SilhouetteMask, uv + float2(offset.x, 0)).r;
                edge += SAMPLE_TEXTURE2D_X(_SilhouetteMask, sampler_SilhouetteMask, uv - float2(offset.x, 0)).r;
                edge += SAMPLE_TEXTURE2D_X(_SilhouetteMask, sampler_SilhouetteMask, uv + float2(0, offset.y)).r;
                edge += SAMPLE_TEXTURE2D_X(_SilhouetteMask, sampler_SilhouetteMask, uv - float2(0, offset.y)).r;
                
                edge += SAMPLE_TEXTURE2D_X(_SilhouetteMask, sampler_SilhouetteMask, uv + float2(offset.x, offset.y)).r;
                edge += SAMPLE_TEXTURE2D_X(_SilhouetteMask, sampler_SilhouetteMask, uv + float2(-offset.x, offset.y)).r;
                edge += SAMPLE_TEXTURE2D_X(_SilhouetteMask, sampler_SilhouetteMask, uv + float2(offset.x, -offset.y)).r;
                edge += SAMPLE_TEXTURE2D_X(_SilhouetteMask, sampler_SilhouetteMask, uv + float2(-offset.x, -offset.y)).r;

                if (edge > 0.01)
                {
                    return _OutlineColor;
                }

                return half4(0, 0, 0, 0); // Transparent where no outline
            }
            ENDHLSL
        }
    }
}
