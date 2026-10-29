Shader "Duckz/TvScreen"
{
    Properties
    {
        _BaseMap ("Feed", 2D) = "gray" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Scanlines ("Scanlines", Range(40, 320)) = 140
        _ScanStrength ("Scan Strength", Range(0, 1)) = 0.42
        _ScanSpeed ("Scan Speed", Range(0, 30)) = 4
        _Vignette ("Vignette", Range(0, 2)) = 1.1
        _Curve ("Glass Curve", Range(0, 0.15)) = 0.045
        _CornerColor ("Corner Color", Color) = (0.015, 0.015, 0.018, 1)
        _Flicker ("Flicker", Range(0, 0.08)) = 0.025
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Scanlines;
                half _ScanStrength;
                half _ScanSpeed;
                half _Vignette;
                half _Curve;
                half4 _CornerColor;
                half _Flicker;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 centered = input.uv * 2.0 - 1.0;
                float bulge = dot(centered, centered);
                centered *= 1.0 + _Curve * bulge;
                float2 uv = centered * 0.5 + 0.5;

                if (any(uv < 0.0) || any(uv > 1.0))
                    return _CornerColor;

                half3 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb * _BaseColor.rgb;

                float row = uv.y * _Scanlines - _Time.y * _ScanSpeed;
                float width = max(fwidth(row), 0.0001);
                float stripe = smoothstep(0.15, 0.85, abs(frac(row) - 0.5) * 2.0);
                float visible = 1.0 - saturate((width - 0.4) * 2.5);
                color *= lerp(1.0, lerp(1.0 - _ScanStrength, 1.0, stripe), visible);

                float2 fromCenter = uv - 0.5;
                color *= saturate(1.0 - dot(fromCenter, fromCenter) * _Vignette * 2.4);

                color *= 1.0 - _Flicker * (0.5 + 0.5 * sin(_Time.y * 18.0));

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
