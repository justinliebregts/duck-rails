Shader "Duck Rails/Fountain Pixel Water"
{
    Properties
    {
        [IntRange] _PixelsAcross ("Grid Size (Pixels Across)", Range(8, 256)) = 64
        _Deep ("Deep water", Color) = (0.07,0.17,0.20,1)
        _Shallow ("Lit water", Color) = (0.15,0.32,0.34,1)
        _Glint ("Ripple highlights", Color) = (0.36,0.53,0.53,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Deep, _Shallow, _Glint;
                float _PixelsAcross;
            CBUFFER_END
            struct Input { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Output { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fog : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Output vert(Input v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Output o;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(Output i) : SV_Target
            {
                float pixelsAcross = max(1, round(_PixelsAcross));
                float2 p = (floor(i.uv * pixelsAcross) + 0.5) / pixelsAcross - 0.5;
                float t = _Time.y;
                float drift = sin(p.x * 17 + p.y * 9 + t * 0.7);
                float wave = sin(length(p) * 66 - t * 2.1 + drift * 0.7);
                float crossWave = sin(p.y * 32 + p.x * 7 + drift + t * 0.8);
                float detail = saturate(1 - max(fwidth(i.uv.x), fwidth(i.uv.y)) * 65);
                float tone = lerp(0.42, floor(saturate(0.5 + wave * 0.23 + crossWave * 0.24) * 4) / 3, detail);
                half3 water = lerp(_Deep.rgb, _Shallow.rgb, tone);
                water = lerp(water, _Glint.rgb, detail * smoothstep(0.84,0.99,wave) * smoothstep(0.45,0.9,crossWave));
                return half4(MixFog(water, i.fog), 1);
            }
            ENDHLSL
        }
    }
}
