#ifndef DUCK_GRAYSCALE_PREVIEW_INPUT_INCLUDED
#define DUCK_GRAYSCALE_PREVIEW_INPUT_INCLUDED
#define InitializeStandardLitSurfaceData InitializeUntintedDuckSurfaceData
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
#undef InitializeStandardLitSurfaceData
half4 _DuckTint;
half _DuckTintEnabled;
half _DuckTintStrength;
inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surface)
{
    InitializeUntintedDuckSurfaceData(uv, surface);
    half gray = dot(surface.albedo, half3(0.2126h, 0.7152h, 0.0722h));
    half3 colored = lerp(gray.xxx, gray * _DuckTint.rgb, saturate(_DuckTintStrength));
    // The original yellow duck bypasses grayscale and tint entirely.
    surface.albedo = lerp(surface.albedo, colored, saturate(_DuckTintEnabled));
}
#endif
