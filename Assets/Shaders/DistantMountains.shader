// Distant mountain silhouettes, one unlit draw.
// Vertices keep their real world position (true parallax from the helicopter)
// but are written at the far end of the depth range, so the camera's far clip
// plane never cuts them and every real object stays in front.
// Drawn just after the skybox (first in the transparent range) without depth
// writes: they paint over sky only, and level pixels reject them early.
// Ridges never depth-test each other: triangle order in the mesh
// (far -> near, see Tools/DistantMountains/build_model.py) puts nearer ridges on top.
// Vertex color: R = ridge layer (0 near, 1 far), G = height (0 base, 1 crest).
Shader "Duckz/Distant Mountains"
{
    Properties
    {
        _NearColor ("Nearest ridge", Color) = (0.012, 0.015, 0.011, 1)
        _FarColor ("Farthest ridge", Color) = (0.05, 0.058, 0.04, 1)
        _BaseDarken ("Base brightness (0 = black at base)", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            // After opaques and the skybox, before other transparents.
            "Queue" = "Transparent-499"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "Mountains"
            Tags { "LightMode" = "UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _NearColor;
                half4 _FarColor;
                half _BaseDarken;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // Pin depth a few depth-buffer steps inside the far plane, so
                // level geometry out to ~1395 m (far = 1400) still wins and
                // distant island silhouettes stay in front of the ridges.
                // This leaves one pixel row uncovered at the sea's far-clipped
                // edge; the storm sky is black below the horizon to hide it.
                // Behind the eye w < 0 and the vertex is clipped as usual.
                #if UNITY_REVERSED_Z
                    positionCS.z = positionCS.w * 1e-7;
                #else
                    positionCS.z = positionCS.w * (1.0 - 1e-7);
                #endif
                output.positionCS = positionCS;
                half3 ridge = lerp(_NearColor.rgb, _FarColor.rgb, input.color.r);
                output.color = ridge * lerp(_BaseDarken, half(1), input.color.g);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return half4(input.color, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
