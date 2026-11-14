# Full Motel atlas review

Current rollout decision: user accepts this optimization approach and authorizes
the other blocks, Convenience Store next, without repeating full Quest A/Bs for
each block. Use `docs/block-atlas-optimization.md` for the reusable procedure and
block-specific exceptions. These scripts remain Motel-specific. Motel visual fixes
and full-map production integration are still pending.

## Requested performance comparison

User subsequently authorized Quest performance testing before fixing remaining
visual issues. **Duck Rails > Tests > Quest Performance > Build Motel Full Map
Comparison** builds the isolated `MotelFullAtlasBenchmark` fixture. Use the PC
helper's `-MotelFullMapVariant control` / `atlas`; it records requested/applied
full-map variants separately from the older wing test. The exact production
consolidation meshes/materials form the control. Both alternatives are resident,
so this compares rendering cost, not shipping memory savings. Normal builds and
saved production assets remain unchanged. Completed same-build pair and build-cache
correction are recorded in `docs/validation/motel-full-atlas/results.md`: 24–70%
fewer draws, improved CPU medians/p95, mixed forward GPU tail. All four views valid;
0.176 m / 0.23-degree pose difference retained as a limitation. The user requested
performance before visual fixes; those fixes remain untouched and pending.

## Review source

November 13, 2026: after approving the east-wing appearance and accepting the
scoped Quest comparison, the user authorized the same approach across the rest
of Motel. This is a separate full-map review, with the existing layout retained.

Open **Duck Rails > Authoring > Motel > Open full-map Blender review** in Unity.
The scene/prefab are under `Assets/DuckRails/MotelAtlasReview`. The source file is
`Motel-FullMap-Atlas.blend`, active scene `Motel full map - atlas review`.
Earlier wing/draft scenes are retained in the Blender file. The visible optimized
collection has 305 objects; the hidden reference collection uses original materials.
Blender coordinates are (Unity X, Unity Z - 80, Unity Y).

## Conversion

- 1,964 compatible opaque static renderers / 41 materials become 305 chunks sharing
  one URP Lit material. Ten original mesh renderers remain (315 total, compared
  with 1,974 original or 767 under the earlier production consolidation).
- Chunks group by 12 x 6 x 12 m spatial cell, existing lightmap and renderer settings.
  Oversized surfaces retain separate bounds. Dynamic/scripted objects and TMP text
  are excluded; CutePixel text stays intact.
- Three 1024-square Unity maps: tinted base color, emission, metallic/smoothness.
  Native source texture resolution is preserved with four-pixel edge padding and
  Point/clamp sampling. Review textures are uncompressed. Base color has mipmaps;
  emission and surface maps do not, because they contain constant values per tile
  and mip averaging visibly leaked emissive-sign color onto trees. That observed
  issue was corrected before review. Base-color minification/seams and texture
  compression still need headset review.
- Repeating UVs are clipped at tile boundaries to preserve texture repetition.
  Triangles increase from 83,344 nondegenerate source triangles to 168,243;
  355,049 exported vertices. Lower renderer counts do not establish a speedup.
  Per-source-part surface area is conserved (maximum relative error 3.07e-13).
- Original UV2/lightmap identities are retained, with source lightmap scale/offset
  baked into chunk UV2. New bindings use identity scale/offset. Existing baked
  lighting is reused, without rebaking.

## Isolation and evidence

`RailMotelFullAtlasReview` copies the production prefab and bake scene. It removes
the old render-consolidation component/generated root from the clone, disables only
the selected source MeshRenderers, then adds atlas renderers and lightmap bindings.
Original source objects remain available. No production prefab, catalogue, gameplay
scene, navigation, lighting assets or build settings are changed by this pass.

Saved-asset authoring checks in `docs/validation/motel-full-atlas/import-checks.txt`
confirm 2,688 original transforms, 8,003 original serialized components (allowing
only renderer-enabled differences), 1,900 colliders and 1,964 original lightmap
bindings retained. The review has 2,269 nonnull bindings including all 305 chunks.
Local prefab GUIDs are normalized when comparing copied references; object file
IDs and external references remain compared. Importer compiles. No Play Mode,
device capture, build or performance test was run for the full-map pass.

User visual review is pending. Runtime translation, lighting rebinding, block
visibility/re-enable, interiors/combat, minification, memory/loading and Quest
performance remain **queued for playtest**. Production integration is pending.
The wing result does not establish full-map acceptance.

## Reproduction

`export-source.cs.txt` runs read-only against the saved production prefab using the
verified Duck Rails Unity MCP. It produces `source.json` and `textures/`.
Run `build_atlas.py` in Blender to create a new review scene and packed source file;
it does not delete existing Blender scenes. It emits `unity-meshes.json`, maps and
`manifest.json`. The importer guards against a changed source dependency hash and
refuses to overwrite an existing review folder. Meshes use one native Unity asset
with 305 mesh subassets, preserving explicit positions/normals/UVs instead of an
FBX conversion. Do not regenerate over user review edits.
