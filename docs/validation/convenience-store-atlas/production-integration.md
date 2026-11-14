# Approved Convenience Store production integration — November 14, 2026

User approved the full-block appearance and requested production integration.
Installed into `Assets/DuckRails/Blocks/convenience-store-v1.prefab` on main.
The production GUID remains **a5c880cc3c274084d9ea80f6cb60cd0b**; metadata is
byte-identical. No replacement of the prefab asset or catalogue reference.

## Result and preservation

- Approved atlas visuals copied under `Optimized Convenience Store atlas`.
  1,139 original renderers are disabled in place; 285 atlas chunks enabled.
  No new runtime switching component, mesh generation or lighting bake.
- 516 enabled MeshRenderers total, including all 231 exceptions. Whole-block
  MeshFilter triangles remain 127,134. The retained 109 HDR-neon renderers, 91
  brush renderers/seven spin scripts, eight cooler panes, seven CutePixel labels
  and 16 incomplete-UV rooftop renderers match the approved review.
- [Exact YAML/object identity audit](production-file-ids.txt): **all 11,745 original
  local object IDs remain**. Of the old serialized records, only **1,139 renderer
  enabled flags**, the root's appended child and the lighting component's appended
  bindings changed. Every other original record is byte-identical.
- [Approved-review comparison](production-preservation.txt): all 3,494 transforms
  and 6,422 compared serialized components match, normalizing only prefab identity
  and the new visual-root name. All **1,420 colliders** and **1,557 lightmap bindings**
  match, including all 1,272 originals. Original gameplay, transform/active states,
  spawns/per-run settings, navigation, brush collision, fonts, lights and user edits
  remain intact.
- [Dependency checks](production-dependencies.txt): **1,377 pre-existing files and
  metadata unchanged**. Only production prefab content is intentionally modified.
  Original bake scene, lighting/probe companion, lightmaps, navigation, source
  meshes/materials and production `.meta` are unchanged.

The atlas material, two textures and mesh container were moved with
`AssetDatabase.MoveAsset` into **Assets/DuckRails/ConvenienceStoreAtlas**. Their
GUIDs and mesh subasset IDs are preserved; the approved review continues using
the same assets. Do not delete this folder when cleaning review artifacts.

## Recovery and subsequent authoring

`ArtSource/ConvenienceStoreAtlas/pre-integration.prefab` and
`pre-integration.prefab.meta.txt` preserve the exact pre-integration production
files. Earlier source/Blender/reference snapshots are also retained. Original
objects, geometry, materials and collision remain in the production prefab itself.

For an immediate full rollback **before any subsequent production edits**, restore
the pre-integration prefab content in place, leave its existing `.meta` intact,
and archive/remove `production-state.json` so the build guard no longer expects
the atlas. Retain shared assets while the review uses them. If later edits exist,
do not overwrite them with the snapshot: reconcile the changes, restore the 1,139
source renderer flags, remove only the generated visual root and its 285 appended
lighting bindings, and retain all original objects and bindings.

Further geometry/material/lighting edits require a fresh source export/atlas pass.
`RailConvenienceStoreAtlasProduction` records a stable fingerprint of rendering
geometry/settings, asset identities/dependencies and baked bindings. Its build
guard rejects changed or missing derived scenery, rather than shipping stale UVs.
The standard catalogue lighting Prepare/Export/Bake Open paths reject an integrated
Convenience Store before mutation. Restore the original render/binding setup and
reconcile the bake scene before baking/re-exporting; regenerate and validate the
atlas afterward. Direct Unity Bake is not a supported shortcut around this workflow.
Other blocks' authoring behavior is unchanged. The existing catalogue builder
preserves the Convenience Store authored root; normal route references retain the
same prefab GUID. Runtime visibility uses `forceRenderingOff`, not enabled flags.

## Checks and limits

[Guard checks](production-guards.txt) pass: valid production state accepted,
disposable geometry edit changes the fingerprint, and the catalogue Prepare guard
stops before switching/mutating a scene. Import/integration editor code compiles;
the final Unity Error/Exception/Assert Console query is empty. Existing review
appearance is the user-approved visual result; production matches its serialized
rendering state and uses the same assets.

No Play Mode, network run, APK, device capture, lighting or navigation rebake.
Actual production-route loading/readiness, translated lightmap binding, visibility
hide/show, brush animation/stationary collision, replay/late join, headset
minification and performance remain **queued for playtest** under **Convenience
Store production atlas integration**. No routine Quest A/B is required.
