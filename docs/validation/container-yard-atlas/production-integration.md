# Approved Container Yard production integration — November 14, 2026

User approved the review and requested integration, commit and push on main.
Integrated into `Assets/DuckRails/Blocks/container-yard-v1.prefab` with original
GUID **cb078f55ab7a62f4ab6be7fd46786e01** and byte-identical metadata. Existing
catalogue/route references now select the optimized visuals.

## Result and preservation

- 246 approved chunks under `Optimized Container Yard atlas`, one shared scenery
  material and three original CutePixel labels: **249 enabled renderers**.
  Scenery triangles remain **202,488**. Original layout and appearance retained.
- All 1,191 replaced source renderers remain disabled in place, retaining original
  objects, meshes, materials, collision and gameplay for recovery.
- [Object identity audit](production-file-ids.txt): all **8,147 original local
  object IDs** retained. Only 1,191 original renderer enabled flags, the root's
  appended visual child and the lighting component's appended bindings changed.
  Every other original serialized record matches the pre-integration snapshot.
  Source prefab's pre-existing differences from HEAD were whitespace only.
- [Approved-review comparison](production-preservation.txt): 2,607 transforms,
  4,602 compared serialized components, all **663 colliders** and **1,437 lighting
  bindings** match the approved review, normalizing prefab identity and generated
  visual-root name. This preserves all source transforms, spawn pools/per-run
  settings, navigation, lights/reverb, text/fonts and active states.
- [Dependencies](production-dependencies.txt): **353 pre-existing files/metadata
  unchanged**, excluding only the intentionally updated production prefab. Bake
  scene, probe/lighting companion, lightmaps, graph, source meshes/materials and
  production metadata are unchanged; no rebake.
- Material, two textures and mesh container moved with `AssetDatabase.MoveAsset`
  to **Assets/DuckRails/ContainerYardAtlas**, preserving their GUIDs and subasset
  IDs. The approved review still uses these exact assets. Retain the production
  folder when cleaning review artifacts. This block has no emission textures.

## Recovery and future authoring

`ArtSource/ContainerYardAtlas/pre-integration.prefab` and
`pre-integration.prefab.meta.txt` preserve the exact prior production files.
Original source/Blender snapshots and disabled renderers are also retained.

Before later production edits, rollback can restore the pre-integration prefab
contents in place, keep the existing `.meta`, and archive/remove
`production-state.json`. Keep shared atlas assets while the review uses them.
If later edits exist, reconcile them instead of overwriting: re-enable the 1,191
source renderers and remove only the generated root and its 246 appended bindings.

`RailContainerYardAtlasProduction` fingerprints rendering geometry/settings,
materials and baked bindings. Its build guard rejects stale derived scenery.
Standard catalogue lighting Prepare/Export/Bake Open reject integrated Container
Yard before mutation. Restore original rendering/bindings and reconcile the bake
scene before geometry/material changes or a new bake; regenerate and validate the
atlas before accepting a new fingerprint. Direct Unity Bake is not a supported
shortcut. Other blocks' behavior is unchanged.

## Validation and remaining acceptance

[Guard checks](production-guards.txt) pass: production accepted; a disposable
geometry edit invalidates the fingerprint; lighting Prepare rejects before scene
mutation. Compilation succeeds and final Error/Exception/Assert Console is empty.
Approved review remains open with original baked lighting; production matches it.

Runtime/headset checks are **queued for playtest** under **Container Yard
production atlas integration**. No Play Mode, network run, comparison APK, device
capture or diagnostic report inspection. No new performance claim.
