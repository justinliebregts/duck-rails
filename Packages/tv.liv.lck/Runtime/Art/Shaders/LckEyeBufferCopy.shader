Shader "LIV/LCK/EyeBufferCopy"
{
    Properties
    {
        _MainTex ("Source", 2DArray) = "" {}
        _SliceIndex ("Eye Slice Index", Float) = 0
        _ScaleOffset ("Scale (xy) Offset (zw)", Vector) = (1, 1, 0, 0)
        _SourceScaleOffset ("Source Scale (xy) Offset (zw)", Vector) = (1, 1, 0, 0)
        _BlitScaleBias ("Blit Scale (xy) Bias (zw)", Vector) = (1, 1, 0, 0)
        _FlipY ("Flip Y", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        ZWrite Off
        ZTest Always
        Cull Off

        // Pass 0: Legacy path used by CommandBuffer.Blit (endCameraRendering / built-in pipeline).
        // Receives _MainTex from cmd.Blit and mesh-based vertex input.
        Pass
        {
            Name "EyeBufferCopy"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma require 2darray

            #include "UnityCG.cginc"

            UNITY_DECLARE_TEX2DARRAY(_MainTex);

            float _SliceIndex;
            float4 _ScaleOffset;
            float4 _BlitScaleBias;
            float _FlipY;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                // Map destination UV to source UV via scale/offset
                float2 uv = i.uv * _ScaleOffset.xy + _ScaleOffset.zw;

                // Conditional Y-flip (controlled by C# based on SystemInfo.graphicsUVStartsAtTop)
                uv.y = lerp(uv.y, 1.0 - uv.y, _FlipY);

                // Black bars for out-of-range UVs
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return half4(0, 0, 0, 1);

                uv = uv * _BlitScaleBias.xy + _BlitScaleBias.zw;
                return UNITY_SAMPLE_TEX2DARRAY(_MainTex, float3(uv, _SliceIndex));
            }
            ENDCG
        }

        // Pass 1: Render Graph path used by AddBlitPass (URP ScriptableRenderPass).
        // Receives _BlitTexture from the Blitter and uses SV_VertexID for fullscreen triangle.
        Pass
        {
            Name "EyeBufferCopy_RenderGraph"

            CGPROGRAM
            #pragma vertex vert_blit
            #pragma fragment frag_blit
            #pragma require 2darray

            #include "UnityCG.cginc"

            UNITY_DECLARE_TEX2DARRAY(_BlitTexture);

            float _SliceIndex;
            float4 _ScaleOffset;
            float4 _BlitScaleBias;
            float _FlipY;

            struct appdata_blit
            {
                uint vertexID : SV_VertexID;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert_blit(appdata_blit v)
            {
                v2f o;

                uint vertexID = v.vertexID;
                // Fullscreen triangle from vertex ID (0, 1, 2)
                o.uv = float2((vertexID << 1) & 2, vertexID & 2);
                o.vertex = float4(o.uv * 2.0 - 1.0, 0.0, 1.0);
                return o;
            }

            half4 frag_blit(v2f i) : SV_Target
            {
                float2 uv = i.uv * _ScaleOffset.xy + _ScaleOffset.zw;

                // _FlipY < 0 is a sentinel meaning "auto-detect via _ProjectionParams.x",
                // used only by old compatibility paths. Modern URP paths set _FlipY
                // explicitly from the active source target state.
                float flipY;
                if (_FlipY < 0.0)
                {
                    // On D3D/Vulkan/Metal the fullscreen triangle's UV(0,0) maps to the
                    // bottom of the output, but texture UV(0,0) maps to the top of the
                    // source, so a Y-flip is needed. However, when the pipeline already
                    // flipped the projection (rendering to an intermediate), that cancels
                    // the mismatch and we must NOT flip.
                #if UNITY_UV_STARTS_AT_TOP
                    flipY = _ProjectionParams.x < 0.0 ? 0.0 : 1.0;
                #else
                    flipY = 0.0;
                #endif
                }
                else
                {
                    flipY = _FlipY;
                }
                uv.y = lerp(uv.y, 1.0 - uv.y, flipY);

                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return half4(0, 0, 0, 1);

                uv = uv * _BlitScaleBias.xy + _BlitScaleBias.zw;
                return UNITY_SAMPLE_TEX2DARRAY(_BlitTexture, float3(uv, _SliceIndex));
            }
            ENDCG
        }

        // Pass 2: 2D source path used when the active camera color target is not a texture array
        // (multi-pass, double-wide single-pass, or non-XR fallback).
        Pass
        {
            Name "EyeBufferCopy_2D"

            CGPROGRAM
            #pragma vertex vert_blit
            #pragma fragment frag_blit_2d

            #include "UnityCG.cginc"

            UNITY_DECLARE_TEX2D(_BlitTexture);

            float4 _ScaleOffset;
            float4 _SourceScaleOffset;
            float4 _BlitScaleBias;
            float _FlipY;

            struct appdata_blit
            {
                uint vertexID : SV_VertexID;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert_blit(appdata_blit v)
            {
                v2f o;

                uint vertexID = v.vertexID;
                o.uv = float2((vertexID << 1) & 2, vertexID & 2);
                o.vertex = float4(o.uv * 2.0 - 1.0, 0.0, 1.0);
                return o;
            }

            half4 frag_blit_2d(v2f i) : SV_Target
            {
                float2 uv = i.uv * _ScaleOffset.xy + _ScaleOffset.zw;

                float flipY;
                if (_FlipY < 0.0)
                {
                #if UNITY_UV_STARTS_AT_TOP
                    flipY = _ProjectionParams.x < 0.0 ? 0.0 : 1.0;
                #else
                    flipY = 0.0;
                #endif
                }
                else
                {
                    flipY = _FlipY;
                }
                uv.y = lerp(uv.y, 1.0 - uv.y, flipY);

                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return half4(0, 0, 0, 1);

                uv = uv * _SourceScaleOffset.xy + _SourceScaleOffset.zw;
                uv = uv * _BlitScaleBias.xy + _BlitScaleBias.zw;
                return UNITY_SAMPLE_TEX2D(_BlitTexture, uv);
            }
            ENDCG
        }
    }
}
