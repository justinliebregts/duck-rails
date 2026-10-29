Shader "LIV/LCK/StreamingButton"
{
    Properties
    {
        _StreamingColor("Streaming Color", Color) = (1,0,0,1)
        _DefaultColor("Default Color", Color) = (0,0,1,1)
        _ProgressValue("Progress Value", Range(0, 1)) = 0.5
    }
    
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 objectPos : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                UNITY_VERTEX_OUTPUT_STEREO 
            };

            fixed4 _StreamingColor;
            fixed4 _DefaultColor;
            float _ProgressValue;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v); 
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                UNITY_TRANSFER_FOG(o, o.vertex);
                o.objectPos = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float normalizedX = i.objectPos.x + 0.5;

                fixed4 finalColor;

                if (_ProgressValue <= 0.001) {
                    finalColor = _DefaultColor;
                } else if (_ProgressValue >= 0.999) {
                    finalColor = _StreamingColor;
                } else {
                    float blend = step(_ProgressValue, normalizedX);
                    finalColor = lerp(_StreamingColor, _DefaultColor, blend);
                }

                UNITY_APPLY_FOG(i.fogCoord, finalColor);
                return finalColor;
            }
            ENDCG
        }
    }
}
