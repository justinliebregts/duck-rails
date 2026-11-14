# Container Yard atlas review — November 14, 2026

Implemented after the referenced Skyscraper integration completed. Unity
6000.3.24f1, verified `X:/Work/duck-rails/Assets`, Edit Mode, no bake in progress.
Blender 5.3.0, existing work backed up and preserved in separate scenes.

Open **Duck Rails > Authoring > Container Yard > Open full-map Blender review**.
Prefab and scene:
`Assets/DuckRails/ContainerYardAtlasReview/ContainerYard-FullAtlas-Review.prefab`
and `.unity`. Editable Blender source:
`ArtSource/ContainerYardAtlas/ContainerYard-FullMap-Atlas.blend`.
The review evidence below records the conversion stage. The user subsequently
approved it and the visuals are now integrated into the original production prefab,
preserving its GUID, all original IDs and existing edits. Shared atlas assets moved
with GUIDs intact to `Assets/DuckRails/ContainerYardAtlas`.
[Production validation and recovery](production-integration.md).

## Geometry and exclusions

- 1,191 compatible static MeshRenderers / 17 materials -> 246 chunks / one shared
  material. Three CutePixel labels retained: repair shed and two propane warnings.
  Whole-block enabled renderers: 1,194 -> 249, down 79.1%.
- Scenery and total MeshFilter triangles: 108,336 -> 202,488, up 86.9% to preserve
  repeating textures. TMP geometry is not included in these MeshFilter counts.
- 12 x 6 x 12 m cells, separate groups for lightmaps/renderer settings. Eleven
  oversized sources remain separate. 381,523 exported vertices; two original
  lightmap assets. Maximum per-part surface-area relative error 1.14e-8.
- 1024-square padded atlas maps retain native source resolution, tint, texture
  scale/offset and surface values, including the ground's 0.25 scale/12 m repeats.
  Base is sRGB with mips; metallic/smoothness linear without mips; Point filtering,
  uncompressed review textures. No emission map/keyword is needed: inventory found
  no emissive materials, textured emission or animated brushes in Container Yard.
  No unsupported property texture was dropped. Other blocks remain untouched.

## Passed authoring checks

- [Saved preservation](import-checks.txt): 2,360 original transforms, 4,110
  serialized components (only replaced Renderer.enabled differs), all 663
  colliders and their settings/references preserved. Navigation, all spawn settings,
  interactions, native prefab references, lights and fonts unchanged.
- All 1,191 original baked bindings/lighting companion preserved; 246 identity-ST
  bindings appended, 1,437 valid bindings total. Source objects remain recoverable.
  Replaced source renderers disabled exactly once; no stale switch or duplicate
  generated root. Three labels remain enabled and use CutePixel SDF.
- [Exact mesh/lightmap checks](detail-checks.txt): saved positions/normals/UV0/UV2/
  indices equal Blender payload; finite unit normals and valid indices. All 1,437
  actual review-scene lightmap indices/STs point to the original intended assets.
  168 coal / 142 gold / 32 enemy candidates, 40 lights and four reverb zones remain.
  No missing components. Candidate counts are not per-run spawn counts.
- [Dependency hashes](dependency-checks.txt): all 354 original dependency files and
  metadata unchanged, including saved production prefab/bake and their local edits.
  Checked inside Unity because package paths are virtual to ordinary shell I/O.
- [GUID checks](guid-checks.txt): eight new asset metadata GUIDs are unique across
  Assets. [Editor state](editor-state.txt): compilation succeeds, final error/
  assert/exception Console query empty, Edit Mode, no bake.
- [Visual comparison](visual-comparison.txt): ten identical-camera source/atlas
  pairs covering whole block, railway, container interior, rooftop route, gantry,
  repair shed, propane tanks, trailer interior, close ground and fence. Mean RGB
  byte differences 0.009–0.114; at most 0.32% of pixels differ by >8 average bytes.
  Visually inspected [pair sheet 1](comparison-1.png) and
  [pair sheet 2](comparison-2.png), plus full-resolution close views and Blender.
  No new missing faces, lighting drift or texture seams observed in these samples.
  Captures disable fog; the overview adds temporary fill. Other views retain baked
  lighting, including the source's dark north yard/trailer and roof areas.

## Remaining work and recovery

User visual approval and production integration are now complete; see the separate
production evidence above.
Full moving-view/headset inspection, distant minification, natural traversal,
loose-item settling and runtime loading/rebinding/visibility/peer agreement are
**queued for playtest**. Static comparisons do not establish those or actual
CPU/GPU/memory benefits. Existing dark areas are preserved, not newly lit or rebaked.
No pilot, Play Mode, APK, Quest capture, navigation bake or lighting bake was run.

Recoverable originals, source hashes, material inventory and deterministic export/
build/import helpers are in [ArtSource](../../../ArtSource/ContainerYardAtlas/README.md).
The review importer guards the original dependency hash and refuses overwrites.
Do not rerun whole-block installers; use the documented recovery/regeneration path
for subsequent render-authoring changes.
