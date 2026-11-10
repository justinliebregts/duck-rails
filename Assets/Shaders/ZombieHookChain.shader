Shader "Duckz/Zombie Hook Chain"
{
    Properties
    {
        [MainColor] _BaseColor("Iron", Color) = (0.38,0.4,0.42,1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _SpecColor("Highlight", Color) = (0.2,0.21,0.23,0.35)
        [PerRendererData] _ExposedLength("Exposed Length", Float) = 20
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ChainVertex
            #pragma fragment LitPassFragmentSimple
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #define _SPECULAR_COLOR 1
            #define _RECEIVE_SHADOWS_OFF 1
            #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitForwardPass.hlsl"

            float _ExposedLength;
            Varyings ChainVertex(Attributes input)
            {
                // The mesh starts at the hook shank. Feed the partial link into the wrist
                // while every fully exposed link retains its authored shape.
                input.positionOS.z = min(input.positionOS.z, _ExposedLength);
                return LitPassVertexSimple(input);
            }
            ENDHLSL
        }
    }
}
