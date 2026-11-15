# Helipad jagged rocks — November 14, 2026

## Random enlargement — November 15, 2026

User requested random 2–4x sizes to distinguish scenery from coal. All 14 rocks
now have fixed authored uniform scale factors from a seeded random draw, actual
range 2.19–3.65x. Grounding retains a 10 cm embed; eleven X/Z positions are unchanged,
two move 0.75 m and one moves 0.5 m to keep existing loot clear. No marker changes.
Meshes, materials, fences and 328 total triangles are retained.

- [Exact scales and shifts](scale-variation-2026-11-15.csv).
- [Saved scene/scale/grounding/clearance checks](resize-checks-2026-11-15.txt) and
  [mesh/reference checks](scaled-checks-2026-11-15.txt) pass.
- Offline navigation rebuilt: 8,233 nodes, all 42 marker poses preserved.
  Lighting rebuilt/exported: 216 valid bindings; saved scene reload passes.
- [Enlarged layout preview](enlarged-preview.png) inspected with temporary unlit
  materials; original rendering restored and saved bake scene reopened cleanly.
- Console contains an LCK graphics-API compatibility error; no rock-specific
  errors observed in the final checks. LCK configuration was not changed.

Queued with the existing helipad entry for natural traversal/contact, loot reach
and nighttime/headset coal-versus-scenery readability. No Play Mode was run.
The original installation measurements below describe the smaller November 14 rocks.

## Original jagged geometry

User requested angular low-poly rocks after approving the fences. Updated only
the 14 rocks in `midpoint-helipad-v1.prefab`, their three new mesh assets and the
block's offline navigation/lighting outputs. No runtime script changes.

- Three asymmetric convex forms with offset ridges and flat triangle normals,
  22–24 triangles each; 328 total versus 14 x 768 = 10,752 (96.9% reduction).
- Existing positions, rotations, 1.5 x 0.9 x 1.3 m scale, stone material and audio
  retained. Ground seating remains slightly embedded. New convex MeshColliders
  match each rock instead of the old sphere collision.
- Meshes have outward-facing closed manifold hulls, UV0 and separate lightmap UV1.
- [Preservation check](preservation.txt): transforms and nested prefab instances
  unchanged; only 14 sphere collider components replaced. Fences and all 42 loot
  candidates remain; 6 coal / 2 gold per-run settings retained.
- Navigation rebuilt to 7,976 nodes with marker poses preserved. Lighting rebuilt
  and exported; all 216 renderer bindings valid, including the 14 revised rocks.
- [Editor checks](checks.txt) pass in verified Duck Rails Unity 6000.3.24f1.
  Saved bake-scene reload and final Console checks also pass. No Play Mode run.
- [Three-shape preview](variants.png) inspected. Isolated preview lighting is for
  geometry inspection, not nighttime acceptance. A temporary render-target cleanup
  error from the first preview was corrected by detaching the camera before cleanup;
  the corrected capture reran successfully. No preview objects were saved.

**Queued for playtest:** natural hand/body/item contact, ground seating and
nighttime/headset appearance, under the existing midpoint fence entry.
