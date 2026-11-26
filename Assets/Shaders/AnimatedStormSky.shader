// Two samples of one small tileable noise texture, composited in the skybox pass.
// Duck Rails addition: an optional sunrise glow (one dot product and pow) toward
// _SunGlowDirection, driven by RailAtmosphere for pre-dawn and dawn. At zero
// strength the output matches the Duckz source shader.
Shader "Duckz/Animated Storm Sky"
{
    Properties
    {
        [NoScaleOffset] _CloudTex ("Cloud noise (linear, repeating)", 2D) = "gray" {}
        _ZenithColor ("Sky overhead", Color) = (0.08, 0.095, 0.065, 1)
        _HorizonColor ("Horizon haze", Color) = (0.12, 0.135, 0.09, 1)
        _LowerColor ("Sky below horizon", Color) = (0, 0, 0, 1)
        _CloudShadowColor ("Cloud shadow", Color) = (0.018, 0.024, 0.017, 1)
        _CloudEdgeColor ("Cloud edge", Color) = (0.14, 0.155, 0.11, 1)
        _Exposure ("Brightness", Range(0, 2)) = 0.8
        _CloudScale ("Cloud scale (higher = smaller clouds)", Range(0.05, 2)) = 0.6
        _CloudCoverage ("Cloud coverage", Range(0, 1)) = 0.5
        _CloudSoftness ("Cloud edge softness", Range(0.02, 0.4)) = 0.14
        _CloudOpacity ("Cloud opacity", Range(0, 1)) = 0.94
        _CloudWind ("Cloud wind (X/Z texture units per second)", Vector) = (0.0012, 0.00035, 0, 0)
        _SunGlowColor ("Sunrise glow colour", Color) = (1, 0.55, 0.25, 1)
        _SunGlowStrength ("Sunrise glow strength", Range(0, 2)) = 0
        _SunGlowSize ("Sunrise glow tightness (higher = smaller)", Range(1, 64)) = 6
        _SunGlowDirection ("Sunrise glow direction (world, toward sun)", Vector) = (-0.82, 0.05, 0.57, 0)
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "Sky"
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CloudTex);
            SAMPLER(sampler_CloudTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _HorizonColor;
                half4 _LowerColor;
                half4 _CloudShadowColor;
                half4 _CloudEdgeColor;
                float4 _CloudWind;
                float _CloudScale;
                half _Exposure;
                half _CloudCoverage;
                half _CloudSoftness;
                half _CloudOpacity;
                half4 _SunGlowColor;
                half4 _SunGlowDirection;
                half _SunGlowStrength;
                half _SunGlowSize;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
                float4 drift : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                // Wrap the uniform offsets, never the fragment UVs: repeat sampling
                // stays continuous and mip derivatives stay valid at tile edges.
                float2 detailWind = _CloudWind.xy * 0.63 + float2(-_CloudWind.y, _CloudWind.x) * 0.17;
                output.drift = frac(float4(_CloudWind.xy, detailWind) * _Time.y);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 direction = normalize(input.direction);
                half height = saturate(direction.y);
                half skyBlend = smoothstep(half(0), half(0.65), height);
                half3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, skyBlend);

                // Rails sunrise glow: brightest along the horizon toward the sun,
                // fading overhead. Zero strength leaves the Duckz result unchanged.
                half sunAlign = saturate(dot(half3(direction), _SunGlowDirection.xyz));
                half glow = pow(sunAlign, _SunGlowSize) * _SunGlowStrength * (half(1) - skyBlend * half(0.75));
                half3 glowLight = _SunGlowColor.rgb * glow;
                sky += glowLight;

                // Project onto an overhead plane. The offset avoids a horizon
                // singularity; direction-only coordinates avoid a longitude seam
                // or clouds following the helicopter's translation.
                float2 uv = direction.xz * (_CloudScale / (max(direction.y, 0.0) + 0.18));
                half broad = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, uv + input.drift.xy).r;
                float2 detailUV = float2(-uv.y, uv.x) * 2.73 + float2(0.37, 0.61);
                half detail = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, detailUV + input.drift.zw).r;
                half density = broad * half(0.72) + detail * half(0.28);
                // Coverage endpoints are clear and fully overcast respectively.
                half threshold = lerp(half(1), -_CloudSoftness, _CloudCoverage);
                half cloud = smoothstep(threshold, threshold + _CloudSoftness, density);
                cloud *= smoothstep(half(0.015), half(0.18), half(direction.y)) * _CloudOpacity;
                half shade = smoothstep(threshold, threshold + half(0.22), density);
                half3 cloudColor = lerp(_CloudEdgeColor.rgb, _CloudShadowColor.rgb, shade);
                // Thin cloud edges catch the glow; dense cores stay dark.
                cloudColor += glowLight * (half(1) - shade) * half(0.8);
                sky = lerp(sky, cloudColor, cloud);

                // Continuous dark lower hemisphere blends with Duck Z's black fog.
                // Duck Rails drives the lower colour to the current fog colour, so
                // sky seen past the edges of the 96 m-wide blocks matches the fog.
                // Below the horizon the sea and distant mountains cover the sky;
                // the only sky left there is a one-pixel row at the sea's
                // far-clipped edge, so it is fully lower colour under 0.
                half lowerFade = smoothstep(half(-0.18), half(0.015), half(direction.y))
                    * smoothstep(half(-0.0003), half(0), half(direction.y));
                sky = lerp(_LowerColor.rgb, sky, lowerFade);
                return half4(sky * _Exposure, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
