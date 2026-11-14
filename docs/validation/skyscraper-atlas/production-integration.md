# Approved Skyscraper production integration — November 14, 2026

User approved the appearance and explicitly chose to keep the existing triangle
count. Integrated on main into `Assets/DuckRails/Blocks/skyscraper-works-v1.prefab`.
Production GUID **2a747f5f81583b74a873a4c2702813af** and metadata are unchanged.
The normal catalogue/route references now resolve the optimized prefab.

## Result and preservation

- 394 approved chunks under `Optimized Skyscraper Works atlas`, one shared
  scenery material, and the original CutePixel sign: **395 enabled renderers**.
  Scenery triangles remain **228,866**. No triangle-reduction variant was built.
- All 3,403 replaced source renderers are disabled in place. Original objects,
  meshes, materials, collision and gameplay are retained for recovery.
- [Object identity audit](production-file-ids.txt): all **23,068 original local
  object IDs** retained. Only 3,403 original renderer enabled flags, the root's
  appended visual child and the lighting component's appended bindings changed.
  Every other original serialized record is byte-identical to the pre-integration
  snapshot. The source's pre-existing differences from HEAD were whitespace only.
- [Approved-review comparison](production-preservation.txt): 4,902 transforms,
  15,973 compared serialized components, all **3,649 colliders** and **3,797
  lighting bindings** match the approved review, normalizing only prefab identity
  and the generated visual-root name. All 3,403 original lightmap bindings remain.
  This includes spawn pools/per-run settings, navigation, lights, text/font,
  active states and current layout.
- [Dependency check](production-dependencies.txt): **353 pre-existing files and
  metadata unchanged**, excluding only the intentionally updated production prefab.
  Bake scene, lighting/probe companion, lightmaps, graph, source meshes/materials
  and production metadata are unchanged. No rebakes.
- Material, three textures and mesh container moved with `AssetDatabase.MoveAsset`
  to **Assets/DuckRails/SkyscraperAtlas**, preserving GUIDs and mesh subasset IDs.
  The approved review still references these same assets. Keep this production
  folder when cleaning review artifacts.

## Recovery and future authoring

`ArtSource/SkyscraperAtlas/pre-integration.prefab` and
`pre-integration.prefab.meta.txt` preserve the exact prior production files.
Original source/Blender snapshots and disabled renderers are also retained.

Before subsequent production edits, a full rollback can restore the pre-integration
prefab contents in place, retain the existing `.meta`, and archive/remove
`production-state.json`. Retain shared atlas assets while the review uses them.
If later edits exist, reconcile them instead of overwriting: re-enable the 3,403
original renderers and remove only the generated root and its 394 appended bindings.

`RailSkyscraperAtlasProduction` fingerprints rendering geometry/settings,
materials and baked bindings. Its build guard rejects stale derived scenery.
The standard catalogue lighting Prepare/Export/Bake Open paths reject integrated
Skyscraper before mutation. Restore original rendering/bindings and reconcile the
bake scene before changing geometry/materials or rebaking; regenerate the atlas
and validate before accepting a new fingerprint. Direct Unity Bake is not a
supported shortcut. Other blocks' behavior is unchanged.

## Validation and remaining acceptance

[Guard checks](production-guards.txt) pass: saved production accepted, a disposable
geometry edit invalidates the fingerprint, and lighting Prepare is rejected before
scene mutation. Compilation succeeds; final Error/Exception/Assert Console is empty.
The approved review remains open with original baked lighting; production matches it.

Runtime/headset checks remain **queued for playtest** under **Skyscraper Works
production atlas integration**. No Play Mode, network run, comparison APK, device
capture or diagnostic report inspection. No new performance claim.
