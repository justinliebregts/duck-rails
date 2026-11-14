# Approved low-poly Skyscraper integration — November 14, 2026

The user approved the new conversion ("This is way better"), requested the
strategy be documented, and authorized commit/push. The production catalogue
prefab now uses that accepted conversion: **37,184 triangles, 649 renderers**,
including the original CutePixel sign. This supersedes the retired 228,866-triangle
version and the temporary unchunked source revision.

`Assets/DuckRails/Blocks/skyscraper-works-v1.prefab` retains GUID
`2a747f5f81583b74a873a4c2702813af`. Shared atlas assets moved, with their existing
GUIDs, to `Assets/DuckRails/SkyscraperLowPolyAtlas`; the review scene/prefab still
reference the same meshes and materials. Original source objects remain in place
with their 2,788 replaced renderers disabled. The generated root is
`Optimized Skyscraper Works atlas`.

## Validation

- [Object identity audit](production-file-ids.txt): all 19,064 pre-integration
  local IDs retained; only renderer enabled flags, root children and appended
  lighting bindings changed among original records. Production metadata unchanged.
- [Approved-review comparison](production-preservation.txt): 4,410 transforms,
  13,671 serialized components, 3,011 colliders and 3,436 lighting bindings match
  the reviewed version. 648 chunks use five materials; no triangle growth.
- [Dependencies](production-dependencies.txt): all 303 pre-existing dependency
  files other than the intentionally changed production prefab remain unchanged.
- [Guards](production-guards.txt): saved production validation passes; changing
  disposable source geometry invalidates its fingerprint. Lighting preparation
  rejects derived atlas UVs before mutating the scene. No build or bake was run.
- Unity compilation succeeds; final Error/Exception/Assert Console empty.

## Recovery and future editing

`ArtSource/SkyscraperAtlasLowPoly/pre-integration.prefab` and its `.meta.txt` retain
the approved unchunked source immediately before integration. `original.prefab`,
source JSON, dependency hashes and Blender file retain the conversion inputs.
`production-state.json` records the current build fingerprint. The prior high-poly
state remains archived in `ArtSource/SkyscraperAtlas/production-state-before-reduction.json`.

For a geometry or lighting revision, restore original renderer/binding ownership,
make the requested source change, and re-export the conversion. Do not overwrite
derived UV2 bindings with a fresh lighting export or rerun old installers over
the current prefab. Preserve unrelated edits and the production prefab GUID.

Runtime/headset acceptance remains **queued for playtest**. No Play Mode, APK,
device comparison or multiplayer verification was performed for this integration.
The known 18 source ground-loot overlaps are unchanged by the render conversion.
