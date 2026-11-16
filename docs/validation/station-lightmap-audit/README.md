# Station lightmap investigation — November 16, 2026

Historical investigation, superseded by the [confirmed ceiling/deck overlap fix](../station-lightmap-fix/README.md). The notes below preserve the earlier inconclusive tests; production is now rebaked/exported with the coplanar geometry separated. Runtime/headset acceptance remains queued.

Verified Duck Rails MCP / StartingStation-bake.unity. Production model, lights and bake inputs were not modified by the diagnostic experiments. Existing dirty station scene was saved before copying; original station reopened afterward. No Play Mode or production bake/export.

Prior UV checks: platform 200 triangles, 245 matched shared edges, zero UV2 discontinuities at those edges; shell triangle-centroid overlap screen found zero hits (not exhaustive overlap proof). Current check: all 204 horizontal shell floor triangles face upward, with correct upward vertex normals. Materials use URP/Lit.

Original bake contains a 2048 and a 1024 color map. Sampled actual color map via current renderer lightmap index, scale/offset, and mesh UV2, reconstructing a top-down floor image. Images are raw encoded RGB previews with a simple display curve; they are NOT HDR-decoded photometric measurements or final camera renders. Use only for comparing spatial patterns.

Controlled bake copies, global resolution 16 / interior scale 2:
1. Original texture spatial pattern: floor.png.
2. All lights shadows=None and lighting settings indirectScale=0: no-shadows.png. Rectangular patches persist.
3. Extract original floor triangles to their own renderer; explicit planar UV2; remove those triangles from shell: planar-floor.png. Patches persist.
4. Replace floor with two simple quads / four triangles, upward normals and planar UV2: simple-floor.png. Patches persist.
5. Reimport diagnostic lightmaps without texture compression: uncompressed.png. Patches persist. Prior format ETC2_RGBA8.

These tests argue against ordinary cast shadows, floor UV2 chart seams or floor subdivisions as the sole cause. They do not isolate filtering, texel validity, encoding/saturation, or other baking-stage behavior. Further investigation must inspect correctly decoded HDR lightmap values and validity/filtering data before another production change.

Console reported 88 objects with overlapping UV texels (Yard, Tunnel, Station_Dressing, Portal_Wall, track rails, ground, deck, sleepers), but not Station_Shell/Rooms/StairsBridge in that report. Those warnings remain separate unresolved issues. BakedTexelValidity view capture was blank gray, so it provided no validity conclusion. Reflection-based internal API access was disallowed by MCP; no workaround attempted.

Diagnostic assets were copied to tmp/station-uv-audit/DiagnosticAssets then removed from Assets; production station reopened. No diagnostic-report inbox or local diagnostic reports inspected.

Final cross-check: decoded-original.png applies the installed URP linear RGBM decode (RGB * 34.493242 * alpha^2.2) before display mapping; the production floor still contains the large rectangles. This is a CPU reconstruction, not a camera capture. The earlier comparison images did not apply RGBM decoding, so interpret their brightness cautiously; they show encoded-data discontinuities rather than calibrated direct illumination. No claim that this fully rules out encoding behavior.
