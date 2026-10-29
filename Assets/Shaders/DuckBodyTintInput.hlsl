#ifndef DUCK_BODY_TINT_INPUT_INCLUDED
#define DUCK_BODY_TINT_INPUT_INCLUDED
// Retain URP's surface setup, lighting, fog, shadows and stereo rendering.
#define InitializeStandardLitSurfaceData InitializeUntintedDuckSurfaceData
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
#undef InitializeStandardLitSurfaceData

half4 _DuckTint;
half _DuckTintEnabled;
half _DuckTintStrength;

inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surface)
{
    InitializeUntintedDuckSurfaceData(uv, surface);
    // Max-channel value preserves painted shading without the yellow hue.
    // Slot zero bypasses recoloring entirely, preserving the original appearance.
    half value = max(surface.albedo.r, max(surface.albedo.g, surface.albedo.b));
    half strength = saturate(_DuckTintEnabled) * saturate(_DuckTintStrength);
    surface.albedo = lerp(surface.albedo, value * _DuckTint.rgb, strength);
}
#endif
