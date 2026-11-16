# Farm Silos screenshot fixes — November 15, 2026

Applied the user's eight screenshot fixes plus the requested pond collider to the
current saved Farm prefab. Original prefab GUID retained. No whole-block installer
was rerun. All 244 spawn candidate poses/order and per-run settings are retained.

- Aligned the slide deck and both side colliders to the existing +0.196 m visual
  offset. The colliders existed but were displaced from the visible sides.
- Trimmed all five silo ramp decks to meet the turning/door landings, eliminating
  intersecting top faces. Matching ramp MeshColliders use the repaired geometry.
- Removed the central silo lantern and detached south cart-shed lantern, including
  their emissive geometry and baked Lights. Kept the silo entrance lantern.
- Added 14 small metal supports connecting retained warm lanterns to nearby walls
  or ceilings. Pond lanterns already sit on their posts. Lowered the south barn
  doorway lantern 0.35 m. Reconciled the retained shed light with its visible lamp.
- Removed the outdoor abandoned tractor; retained the separate indoor dismantled
  tractor and other scenery.
- Removed overlapping triangle footprints within the north farmyard path and
  orchard drive, including their intersection. Existing user placement retained.
- Added a non-trigger pond-shaped MeshCollider 0.025 m below the current water
  mesh (world Y approximately -0.29 m). Its GameObject is named
  `Pond underside collider - assign Water AudioSurface`; Water AudioSurface is
  intentionally left for the user's assignment.

Offline Farm navigation and lighting were rebuilt/exported. The source objects
remain beneath the new optimized visual root for future authoring. Blender 5.3
ran in background mode because the live Blender MCP connection was unavailable.
The existing Blender session and previous .blend files were not overwritten.

## Atlas and checks

`ArtSource/FarmSilosPolish` contains the before snapshot, corrected/baked source,
exporter, editable `FarmSilos-LowPoly-Atlas.blend`, builders and independent checks.
The accepted triangle-preserving workflow converts **59,326 -> 59,326 triangles**:
907 source renderers become 209 chunks (115 atlas, 94 repeating-material exception
chunks), with 54 retained probe-lit/scripted renderers: **263 enabled renderers**.
The old conversion used 171 renderers but tessellated the source; renderer counts
are not a measured performance claim. The new output uses 12 shared materials and
one 1024-square atlas set. 459 twelve-triangle source parts stay twelve triangles.

- [69 collision/reference checks plus three spawn-pool comparisons](editor-checks.txt)
  pass. All 105 coal, 117 gold and 22 enemy candidate transforms/order preserved.
- [Independent Blender corner checks](../../../ArtSource/FarmSilosPolish/geometry-checks.json)
  pass for every triangle: position, normal, UV2, material/lightmap identity and
  texture phase/gradients. Maximum UV phase error 0.0000301 repeats is float32
  export rounding (under 0.008 source texel), not texture rescaling.
- Eight Unity source/atlas pairs inspected. [Pixel comparison](visual-comparison.json):
  mean RGB byte difference 0.0015–0.150; at most 0.011% of pixels exceed eight bytes.
  Temporary fill and fog-off captures are visual checks, not gameplay darkness.
- Production guard passes: 209 chunks, 907 disabled originals, 1,116 valid bindings.
  1,429 colliders and 39 lights. Compilation succeeds; final Console errors empty.

[Slide](atlas-slide.png) · [Ramp](atlas-ramps.png) · [Silo](atlas-silo.png) ·
[Lanterns](atlas-west-lanterns.png) · [Barn](atlas-barn-south.png) · [Path](atlas-path.png).

Open **Duck Rails > Authoring > Farm Silos > Open polished Blender review**.
The old full-map review menu now routes to this current review. Review-only baked
fixture suppression prevents duplicate realtime preview lighting; runtime prefab
lights remain enabled. Review lightmap bindings restore after scene reload.

The pending Compound scene edits were saved before scene switching, with both the
previous disk file and unsaved scene snapshot backed up under `Temp/farm-fixes-*`.
Other working-tree changes were not reverted, staged, committed or pushed.

**Queued for playtest:** natural slide/edge climbing, ramp landing traversal,
moving-view z-fighting, pond collision/audio after user assignment, night fixture
appearance, loose items, enemy navigation and real-client/late-join agreement.
No Play Mode, multiplayer, APK, headset test or diagnostic report inspection ran.

## Future edits

Use `ArtSource/FarmSilosPolish/original.prefab` only as a reference after subsequent
edits. Reconcile source renderers and remove the generated root/bindings before a
new bake, then re-export/rebuild/import. Do not rerun the one-time screenshot
migration. Current production fingerprint/payload are in `FarmSilosPolish`, and
`RailFarmSilosAtlasProduction` validates those when its production-state exists.
The earlier FarmSilosAtlas review/source is historical and uses the previous bake.

## Ditch water follow-up

Added a 3.3 x 0.1 x 37 m solid Environment BoxCollider beneath the narrow
ditch water, with Water AudioSurface assigned. Three direct contact rays and
Water lookup pass; Farm navigation rebuilt. Visual meshes/lightmaps unchanged,
production atlas guard passes. Listening remains queued for playtest.

