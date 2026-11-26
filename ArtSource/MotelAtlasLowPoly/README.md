# Motel low-poly atlas source

November 17, 2026. Fresh triangle-preserving conversion of the saved
`Assets/DuckRails/Blocks/roadside-motel-v1.prefab` after the 2026-11-17 light fixes
and rebake (commits ba8d8871b, eb4e8553d). It replaces the retired
triangle-clipped review in `../MotelAtlas` (83,344 -> 168,243 triangles, stale lighting).
**79,948 -> 79,948 triangles; 1,921 scenery renderers -> 298 chunks.**
User approved the review; it is **integrated into production** (see below).

- Snapshot: `source.json`, `original.prefab`, `original-bake.unity`,
  `dependency-sha256.tsv`, `exclusions.txt`, `textures/`.
- Export: `export-source.cs.txt` (Skyscraper exporter with Motel paths), run read-only
  through the verified Duck Rails Unity MCP. Refuses to overwrite a snapshot.
- Build: `build_atlas.py`, run headless (`blender -b --factory-startup --python
  build_atlas.py`, Blender 5.3). Writes `Motel-LowPoly-Atlas.blend` (scene
  `Motel low-poly - atlas review`, hidden original-material reference collection),
  atlas maps, `unity-meshes.json`, `part-checks.json`, `manifest.json`. Refuses to
  overwrite an existing `.blend`.
- Verify: `verify_export.py` (same headless command) writes `geometry-checks.json`.
- Import: `Assets/Editor/RailMotelLowPolyAtlasReview.cs`, **Duck Rails > Authoring >
  Motel > Create / Open isolated low-poly Blender review**, into
  `Assets/DuckRails/MotelLowPolyAtlasReview/`.

## Conversion

- 1,974 mesh parts (1,921 renderers, 38 URP Lit materials, all with 64 or 256 px base
  textures, none emissive). 51 renderers stay original: 43 neon/ceiling signs whose
  materials use emission maps, 8 without a baked binding (`exclusions.txt`). The
  retained CutePixel/TMP text is outside this pass.
- One **2048 x 1024** atlas (the eleven 256 px textures alone fill a 1024 square).
  Repeats per material come from the actual UV spans, capped at 256 atlas pixels per
  axis; four-pixel wrap padding, Point sampling, native texel density.
- 1,876 parts use the atlas (247 chunks, 12 x 6 x 12 m cells). 98 small parts whose
  UVs repeat further keep their original material and UVs (51 chunks, 48 x 12 x 48 m
  cells, 13 materials, 1,348 triangles).
- No triangle clipping or subdivision. Source lightmap scale/offset folded into UV2;
  identity bindings on both original lightmaps.
- No source material is emissive, so the Unity atlas material uses no emission map or
  keyword. Base color has mipmaps; the metallic/smoothness map does not. Uncompressed
  review textures.

## Checks (2026-11-17)

- `geometry-checks.json`: PASS. All 79,948 triangle corners compared with the source:
  max position error 3.8e-6, normal 8.9e-8, lightmap UV 3.0e-8, repeat phase 1.4e-6
  (one integer offset per triangle). 1,437 twelve-triangle parts preserved. Surface
  area relative error 2.0e-8. 119,870 exported vertices.
- Saved review prefab: 298 chunks / 79,948 triangles, no missing meshes or materials;
  1,921 originals disabled; 1,900 colliders and 75 lights match production; the old
  `RailSceneryChunks` component and generated root are removed from the review copy
  only; 2,262 bindings (1,964 original + 298 chunks).
- Enabled mesh renderers: 1,972 originals (production uses 698 generated chunks at
  runtime) -> 349 in the review.
- Paired Scene view at the pool/motel (production bake scene vs review, same camera):
  no visible difference. Compiles; Console has no errors.
- Review stage did not include Play Mode or headset/runtime checks. Production
  integration followed the user's approval (below).

## Production integration (2026-11-17, user-approved)

`RailMotelAtlasProduction.Integrate()` (`Assets/Editor/RailMotelAtlasProduction.cs`):

- Copied the reviewed chunks into the original-GUID production prefab as the last
  root child `Optimized Motel atlas`, appended 298 identity bindings
  (1,964 -> 2,262) and disabled the 1,921 replaced originals in place.
- Removed `RailSceneryChunks` and its inactive `Generated Motel render chunks` root.
  `RailMotelRenderChunks.Generate` now refuses; its build guard is a no-op without
  the component. `Assets/DuckRails/MotelRenderChunks/Meshes.asset` is now unused; the
  old Motel wing/full-map benchmark fixtures that relied on it are historical.
- Moved the atlas material, two textures and mesh container, GUIDs intact, to
  `Assets/DuckRails/MotelLowPolyAtlas/`; the review prefab uses the same assets.
- `pre-integration.prefab` (+ `.meta.txt`) is the exact pre-integration production
  prefab for rollback. `production-state.json` records GUID and fingerprint
  (`5361389c…`).
- Wired `RequireOriginalLightingWorkflow` into Prepare/Export/Bake Open and added
  the Motel to the Block Lighting Tool's atlas blocks (lights-only rebake). Its
  Motel render-chunk buttons were removed.

Checks: `ValidateSaved` passes (298 chunks, 14 materials, 79,948 triangles, 2,262
bindings, no duplicate source rendering); the other four atlas guards still pass;
Bake Open Block and chunk regeneration refuse the Motel. Object-ID diff of the
prefab: only the 698 old chunks, their root and `RailSceneryChunks` removed, 298
chunks and the visual root added; all 13,170 other IDs retained; 1,900 colliders,
75 lights and the GUID unchanged. Production bake scene preview binds all 298
chunks and matches the review view. Bake scene and lighting assets unchanged.
Runtime/multiplayer/headset appearance is queued for playtest.
