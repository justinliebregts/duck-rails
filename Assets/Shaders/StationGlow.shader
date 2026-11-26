// Unlit emissive glow for station tubes, signs and screens.
// _Flicker > 0 makes each object blink on its own random timing (seeded by its world position).
// The baked light around it stays steady, so use flicker on dim or dying fixtures.
Shader "DuckRails/StationGlow"
{
    Properties
    {
        _BaseColor ("Glow Color", Color) = (0.8, 0.9, 1, 1)
        _Intensity ("Intensity", Range(0, 8)) = 1.5
        _Flicker ("Flicker Chance", Range(0, 1)) = 0
        _Rate ("Burst Checks Per Second", Range(0.05, 4)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

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

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Intensity;
                half _Flicker;
                half _Rate;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half brightness : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash(float n) { return frac(sin(n) * 43758.5453); }

            // Mostly steady. Every 1/_Rate seconds a fixture may start a short burst of blinks.
            half Brightness(float3 origin)
            {
                if (_Flicker <= 0.0001) return 1;
                float seed = dot(floor(origin * 2.0), float3(12.9898, 78.233, 37.719));
                float slot = floor(_Time.y * _Rate + Hash(seed) * 17.0);
                if (Hash(slot + seed * 0.37) > _Flicker) return 1;
                float phase = frac(_Time.y * _Rate + Hash(seed) * 17.0);
                if (phase > 0.35) return 1;
                float blink = Hash(floor(_Time.y * 23.0) + seed);
                return blink > 0.45 ? 0.85 : 0.04;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // Per-object seed keeps a whole tube in sync. Glow renderers are not statically batched.
                output.brightness = Brightness(TransformObjectToWorld(float3(0, 0, 0)));
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(_BaseColor.rgb * _Intensity * input.brightness, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half frag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
