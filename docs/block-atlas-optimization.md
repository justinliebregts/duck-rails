# Blender atlas optimization workflow

## Confirmed direction — November 13, 2026

The user accepted the Motel optimization and authorized applying the approach to
the other blocks, **Convenience Store next**. Work one block at a time from the
current saved art, preserving layout and gameplay. This is render optimization,
not level redesign.

Do not repeat the wing pilot or full Quest A/B for every block. Routine work
includes export/import checks, compile/Console checks, saved-asset preservation
checks and visual inspection. Leave a concrete Unity result for user review and
queue remaining runtime/headset checks. Do not launch Play Mode, build/install an
APK or start a device capture unless requested. New material/geometry exceptions
require inspection/adaptation, not automatically another benchmarking cycle.

This supersedes earlier recommendations to finish all Motel visual fixes or
profile every block before broader conversion. Those Motel visual issues remain
separate. Its full-map atlas is a review/benchmark asset, **not the production
catalogue prefab**. Track conversion, review, integration and runtime validation
separately. Preserve recoverable originals.

## Evidence and reference implementation

Motel: 1,964 compatible source renderers / 41 materials became 305 spatial chunks
sharing one material, plus ten original renderers. Total 315 versus 767 under the
earlier production consolidation. Repeated-UV clipping increased converted triangles
83,344 -> 168,243. Three 1024-square maps retained native source texture resolution.

The accepted 120 m Quest pair showed 24–70% fewer whole-frame draws, 0.1–0.6 ms
better median CPU and maximum-view CPU p95 12.5 -> 11.8 ms. GPU improved in three
views; forward GPU p95 increased 10.0 -> 10.4 ms. Both met the sampled 72 Hz p95
budget. Pose differed 0.176 m / 0.23 degrees, and both alternatives were resident.
These are Motel results, not measured gains or shipping-memory savings for every
block. [Full results](validation/motel-full-atlas/results.md).

Reuse/adapt these references:

- `ArtSource/MotelAtlas/export-source.cs.txt`: saved Unity geometry, source keys,
  renderer settings, materials/textures and baked-lighting export.
- `ArtSource/MotelAtlas/build_atlas.py`: Blender atlas packing, repeated-UV clipping,
  spatial grouping, surface-area checks and explicit Unity-coordinate export.
- `ArtSource/MotelAtlas/Motel-FullMap-Atlas.blend`, `manifest.json` and `README.md`:
  editable source, counts and limits.
- `Assets/Editor/RailMotelFullAtlasReview.cs`: isolated Unity importer/review.
- `docs/validation/motel-full-atlas/import-checks.cs.txt` and `import-checks.txt`:
  saved-asset preservation comparison and evidence.

These are Motel-specific scripts, **not a generic installer**. Adapt paths, root,
coordinate origin, output names, counts and material handling. Never run them
unchanged against another block or overwrite Motel artifacts. Reuse small proven
functions; a broad tooling rewrite is not a prerequisite.

## Per-block procedure

### 1. Inspect and snapshot

Read AGENTS.md, game design, execution plan, target layout notes and playtest queue.
Work on main and preserve unrelated edits. Verify Unity MCP targets
`X:/Work/duck-rails/Assets`, is in Edit Mode and is not baking. Inspect Blender's
connection/current scene and preserve user work.

Record saved prefab dependency/file hashes. Inventory renderers/material properties,
UVs/readability, baked bindings, colliders, transforms, gameplay components, spawn
pools and navigation references. Current saved assets override historical counts.
Identify existing atlas/generated roots and runtime switches; do not export both
source scenery and its generated duplicate. Never rerun old whole-block installers,
relocate spawns or rebake navigation for this render-only task. No new top-down
layout is needed when retaining the existing layout.

### 2. Select compatible static scenery

Use enabled opaque static MeshRenderers with readable meshes, valid normals/UV0/
UV2 and known baked bindings. Exclude TMP, transparency, animated/skinned meshes,
moving/scripted parents, physics-driven objects, property-block overrides and
unsupported shaders/material properties. Preserve excluded renderers intact and
report them. One shared material is the target for compatible scenery; exceptions
do not need to be flattened to force the entire block into that material.

Motel allowed URP Lit with no keywords except `_EMISSION`, and harmless AudioSurface
components. That whitelist alone is insufficient: its exporter handles base texture/
tint and **constant** emission/metallic/smoothness. It does not sample an existing
emission texture, normal map or other property texture. Inspect actual shader inputs;
retain special materials or extend export to preserve them. Never silently drop maps.

Use stable root-relative source keys. Persistent prefab assets may report
`activeInHierarchy=false`; inspect ancestor `activeSelf`. Export positions in
block-root space and normals with inverse-transpose. Do not assume root identity.

### 3. Build the Blender asset

Create block-specific ArtSource output and a separate Blender scene. Pack padded
base/property maps, retaining native resolution, texture scale/offset, tint,
color-space handling and HDR emission. Motel used skyline packing, four-pixel
padding, Point sampling and an emission multiplier of eight. Choose atlas size and
HDR range from actual inputs; do not force 1024 or eight when data would be lost.

Clip repeated UV triangles at tile boundaries before atlas remapping. Interpolate
position, normal and UV2; preserve winding and texture repetition. Check per-part
surface area and record triangle/vertex growth. Weld only matching position, normal
and both UVs. Do not decimate, alter silhouettes or replace collision geometry.

Group by spatial cell, baked-lightmap identity and compatible renderer settings.
Start with Motel's 12 x 6 x 12 m cells; retain oversized sources separately and
sensible room/floor/culling boundaries. One material does not imply one giant mesh.
Fold original lightmap scale/offset into UV2 and retain each lightmap asset identity.

Save .blend, source snapshot, atlas maps, manifest and unity-meshes.json. Retain a
hidden original-material reference and visually inspect before import. Do not delete
existing Blender scenes or regenerate over user edits.

### 4. Import and present for review

Create a block-specific review prefab/scene from the existing prefab and bake scene.
Verify the source dependency hash before using source keys; resolve keys before
changing sibling indices. Retain source objects, colliders and gameplay. Disable
only replaced source renderers. Remove superseded generated-render switches/roots
from the clone so they cannot double-render or restore the wrong version.

Import explicit positions/normals/UV0/UV2/indices and renderer settings. Use one mesh
container with subassets or batched AssetDatabase operations: individually importing
hundreds of meshes was slow. Preserve GUIDs/references; generate unique metadata for
new assets. Use 32-bit indices only when necessary.

Use URP Lit and correct keywords. Base is sRGB; emission and surface data are linear.
Unity expects metallic in red and smoothness in alpha; convert Blender roughness.
Motel's constant-per-material emission/surface maps use **no mipmaps**: averaged
atlas tiles made distant trees glow with neighboring sign colors. Base-color mips
remain enabled. Do not blindly apply this constant-map rule to newly textured
emission; inspect padding/minification. Keep compression changes separate from the
initial conversion; Motel review textures are uncompressed.

Retain original bindings, lighting companion and probe data. Append chunk bindings
with identity scale/offset, and explicitly connect actual lightmap indices in the
review scene. No lighting rebake is needed for preserved geometry/UV2. Avoid
overlapping review scenes and never discard dirty user scenes. Leave the result
visible in Unity with baked lighting and a useful view.

### 5. Check and record

Use bounded authoring checks on reloaded saved assets:

- Original colliders, serialized shapes/settings and references unchanged.
- Original transforms/active states, gameplay components, spawns/counts, navigation,
  lights, text/fonts and interactions preserved.
- Normalize copied prefab identity when comparing references; keep local object
  file IDs and external references meaningful. Expected differences are renderer
  enabled flags, generated visuals and appended lighting bindings.
- Every replaced source disabled once; exceptions retained; no missing meshes,
  materials or lightmaps, duplicate render roots or stale runtime switch.
- Chunk counts, indices/normals/UV2 and identity lightmap transforms match export;
  compilation succeeds and Console is inspected.
- Visually inspect interiors, close surfaces, emission, seams and minification.
  Record visible defects honestly; editor checks are not headset acceptance.

Update the target layout notes, this rollout status and playtest queue with actual
counts, exceptions, paths, checks and open issues. Say "queued for playtest" for
unexercised live behavior. **Routine per-block Quest comparisons are not required.**

Retain the established visual-review step before replacing production appearance.
Conversion authorization does not make unreviewed visuals approved. When integrating
a reviewed result, preserve the production prefab GUID/gameplay references, retain
recoverable originals and reconcile existing generated-render/build guards. Track
integration separately; Motel full-map production integration is still pending.

## Convenience Store implementation

Target `Assets/DuckRails/Blocks/convenience-store-v1.prefab`; read
`docs/convenience-store-layout.md`, especially later sparse-store/collision and
user-edit preservation notes. Its bake scene is
`Assets/DuckRails/Lighting/Blocks/convenience-store-v1/convenience-store-v1-bake.unity`.

Preserve rotating car-wash visuals and stationary collision. Inspect scripted
parents before batching. Its existing emissive atlas requires texture/HDR handling
beyond Motel's constant-emission exporter, or must remain an explicit exception.
Keep CutePixel labels, user-moved trailer/props, sparse shop/empty cooler shelves,
stocked inventory room, spawns and navigation. Do not restore deleted clutter or
bundle art fixes. Start with the full block; no wing pilot or comparison APK needed.

## Rollout status

- Motel: full-map review and scoped Quest pair complete; optimization approach
  accepted. Visual fixes and full-map production integration pending.
- Convenience Store: full-block Blender asset and separate Unity review prefab/scene
  imported November 14. 1,139 compatible source renderers -> 285 shared-material
  chunks; 231 retained exceptions; total 1,370 -> 516 enabled mesh renderers.
  Original HDR textured emission retained. Converted triangles 50,538 -> 121,970;
  whole-block MeshFilter triangles 55,702 -> 127,134. Saved preservation, exact-mesh,
  dependency-hash and seven-view authoring checks pass. User approved the visuals;
  integrated into the original-GUID production prefab November 14, retaining all
  original object IDs/gameplay/collision and baked lighting. Runtime/headset
  validation queued for playtest. [Integration and recovery](validation/convenience-store-atlas/production-integration.md).
- Remaining blocks authorized for subsequent passes, not converted here: Bridge
  Finale, Canyon, Container Yard, Electrical Substation, Farm Silos, Hospital
  District, Lakeside Campground, Lumber Yard, Maintenance Depot, Midpoint Helipad,
  Nuclear Power Plant, Passenger Station, Quarry, Scrapyard, Skyscraper Works and
  Tunnel Works. Later ordering is not fixed. Preserve special finale/vehicle/gameplay
  objects; shared train/player/shop assets outside the target block are not an
  implicit extension of scenery conversion.

## If further performance testing is requested

Reuse the existing runner. Build-scene substitution requires **CleanBuildCache**:
Unity initially reused the wing scene for the full-map build. Assert/log the injected
prefab and requested/applied runtime variant before trusting results. A two-minute
Unity MCP timeout does not stop the build; inspect
`output/quest-performance/build-status.txt` before retrying. Never install a stale APK.

Match route/settings/pose, verify tracking, collect CPU/GPU median/p95 and draws/
triangles, and restore recording/proximity state. Both-variant fixtures cannot measure
shipping memory/loading savings. Details: `docs/quest-performance.md`.

Realtime per-pixel lighting remains unchanged. Static fixtures are already baked;
vertex-lighting conversion is a separate experiment. Never inspect diagnostic
reports unless explicitly requested.
