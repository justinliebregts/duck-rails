# Midpoint helipad fencing — November 14, 2026

Implemented after referenced Skyscraper task completed and pushed `d448e4d27`.
Scope: `Assets/DuckRails/Blocks/midpoint-helipad-v1.prefab` only, plus its mesh,
offline crawlgraph and lighting artifacts. Unity 6000.3.24f1, verified project
`X:/Work/duck-rails/Assets`, Edit Mode throughout.

- Ground measured at 96 x 60 m. Side centerlines X +/-47, 1 m inside lateral
  edges; 2 m high, Z0..60. Post outer faces leave 0.9 m to the ground edge.
- Existing timber material, 0.2 m posts, two rails at Y0.6/1.4; 16 posts per side.
  Two renderers share one 216-triangle mesh (432 triangles total, +60 compared
  with the removed 31-piece west fence). Each cube remains 12 triangles.
- World-size UV0, unit object scale and separate UV1. Two continuous Environment
  box colliders extend to ground and both seams; existing timber AudioSurface.
- Retain rear decorative fence, track/anchors, helicopter and shop, invisible
  boundaries and all 42 loot markers (21 coal, 21 gold; 6/2 selected per run).
  Original helicopter prefab-instance overrides restored exactly after Unity
  introduced redundant rotation serialization during authoring.
- Navigation rebuilt to 8,023 nodes; all 42 marker poses preserved. Lighting bake
  and companion export completed; 216 valid renderer bindings include both fences.
- [Saved-prefab checks](checks.txt) pass; error/exception Console returned zero.
  [Serialized preservation check](preservation.txt) confirms only the old west
  fence objects were removed and all other original object IDs remain. Final
  saved bake-scene reload passes both fence lightmap/audio references.
  No runtime scripts changed. The installer/check scripts in this directory are
  explicit MCP authoring evidence, not runtime components.
- [Geometry preview](geometry-preview.png) inspected for placement and rail/shop
  clearance. Preview temporarily uses unlit materials with fog disabled; original
  materials/settings restored and saved bake scene reopened. It is not nighttime
  visual acceptance. The editor is left on the helipad bake scene.

**Queued for playtest:** natural fence contact, railway/block seam transitions,
nighttime and headset readability, translated lighting and real-client loading.
No Play Mode, client or headset acceptance was run.
