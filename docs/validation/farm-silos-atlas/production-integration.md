# Approved Farm Silos production integration — November 14, 2026

User approved the visual review and requested integration, commit and push on main.
Integrated into Assets/DuckRails/Blocks/farm-silos-v1.prefab, preserving original
GUID **db60e2d8d31a6a04f9c8df3897557881** and byte-identical metadata. Existing
catalogue/route references now select the approved optimized visuals.

## Result and preservation

- 117 approved chunks under Optimized Farm Silos atlas, sharing one material,
  plus 54 unchanged probe-lit corn/pond/horsetail renderers: **171 enabled renderers**.
  Converted triangles **104,040**; whole-block triangles **194,784**.
- All 899 replaced original renderers remain disabled in place with their source
  objects, meshes, materials, collision and gameplay available for recovery.
- [Identity audit](production-file-ids.txt): all **8,738 original local object IDs**
  retained. Only 899 original renderer enabled flags, the root's appended child
  and lighting component's appended bindings changed. Every other original
  serialized record matches the pre-integration snapshot. Pre-existing Farm
  differences from HEAD were whitespace only.
- [Approved-review comparison](production-preservation.txt): 2,479 transforms,
  4,737 serialized components, **1,434 colliders** and **1,016 lighting bindings**
  match the approved review, normalizing prefab identity/visual-root name.
  This includes navigation, 105 coal / 117 gold / 22 enemy candidates, per-run
  settings, original lights, water interaction, six reverb zones and active states.
- [Dependencies](production-dependencies.txt): all **733 pre-existing dependency
  files/metadata** unchanged, excluding only the intentionally updated production
  prefab. Original bake scene, LightingData, probes/companion, lightmaps, navigation,
  meshes/materials and production metadata remain unchanged; no rebake.
- Material, three textures (including HDR emission) and mesh container moved with
  AssetDatabase.MoveAsset to **Assets/DuckRails/FarmSilosAtlas**, retaining GUIDs and
  subasset IDs. The approved review uses the same assets. Retain this production
  folder when removing optional review artifacts.

## Review versus runtime lighting

The approved review scene's labelled EditorOnly overview fill and baked-fixture
preview suppression remain confined to that scene. They are not part of the review
prefab and were not copied into production. All 41 production prefab lights retain
their original serialized settings. Original lightmap companion and 899 bindings
remain; 117 chunk bindings use identity scale/offset.

The Farm review's editor-only lightmap rebind helper targets only its named review
scene. Runtime production still uses RailBakedBlockLighting/RailBlockLightingLoader;
translated loading, visibility and late joining remain queued, not editor-verified.

## Recovery and future authoring

ArtSource/FarmSilosAtlas/pre-integration.prefab and pre-integration.prefab.meta.txt
preserve the exact preceding production files. Original source/Blender snapshots
and disabled originals are retained.

Before later production edits, rollback can restore pre-integration.prefab contents
in place, retain the existing .meta, and archive/remove production-state.json.
Keep atlas assets while the review references them. After later edits, reconcile
instead of overwriting: re-enable the 899 source renderers and remove only the
generated visual root and its 117 appended bindings.

RailFarmSilosAtlasProduction fingerprints rendering geometry/settings, materials
and baked bindings. Its build guard rejects stale derived visuals. Catalogue
lighting Prepare/Export/Bake Open reject integrated Farm before mutation.
Restore original rendering/bindings and reconcile the bake scene before geometry/
material changes or a new bake; regenerate the atlas before accepting a new
fingerprint. Direct Unity Bake is not a supported shortcut.

## Validation and remaining acceptance

[Guard checks](production-guards.txt) pass: production accepted, a disposable
geometry edit changes the fingerprint, lighting Prepare rejects before modifying
the scene. Compilation succeeds; final Error/Exception/Assert Console is empty.
Approved full-block review remains open.

**Queued for playtest: Farm Silos production atlas integration.** No Play Mode,
multiplayer run, APK, Quest capture or diagnostic report inspection. No new device
performance claim. Existing seven roof/hay loot failures remain separately tracked.
