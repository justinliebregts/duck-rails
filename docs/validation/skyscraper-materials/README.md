# Skyscraper material stacks — November 14, 2026

User requested fixing floating layers throughout Skyscraper, varying stack amount,
rotation and placement between floors, repairing the southeast piles, and deleting
the northeast circular concrete pipe. The user subsequently approved this source;
the [new Blender review](../skyscraper-low-poly-atlas/README.md) preserves its
37,184 triangles without subdivision.

## Installed changes

- All **50 tower pallets** are grounded against the actual saved slab heights,
  including the pre-existing slightly raised ground slabs. Layers sit directly
  on the pallet/layer below, with four-corner support verified.
- **One to four layers** per pallet, varied pallet dimensions/yaw and slight layer
  rotations/taper. Total material layers decrease **150 -> 126**. Each of the
  **17 floors has a distinct arrangement**; positions are authored deterministically,
  not randomized at runtime. Real surface/collision checks preserve circulation,
  stair/bridge approaches, shaft openings, lights and remaining loot envelopes.
- All **50 loose square-section pipe pieces** now rest on their floors.
- Southeast delivery timber: four -> three touching layers on the flatbed,
  rotated slightly. Southeast formwork: five -> four touching layers, grounded
  and rotated. Eight shelter stockpiles vary in height, yaw and depth position.
  Combined yard render meshes and their separate box colliders receive identical
  edits; shared source meshes are not overwritten.
- Removed the **northeast pipe pair** near X36 / Z128–131: 24 collider segments
  and their corresponding render triangles. The southeast and northwest pipe
  locations remain.
- **52 loot candidates** move with their supporting materials (50 tower, two
  yard). Counts remain **202 coal / 192 gold / 23 enemies**; per-run counts and
  runtime selection/network behavior are unchanged.
- Existing floor cuts, original skyline, crossing decks, and other tower
  structure are retained. The installer checks unchanged structural transforms.

Current block: **37,184 triangles / 2,789 MeshRenderers / 3,011 colliders**, versus
37,784 / 2,813 / 3,061 before this pass. Source authoring remains unchunked.
No device-performance claim follows from these counts.

## Validation

- [Installation and 50 placements](install.txt).
- [711/711 dressing assertions](dressing-summary.txt): ground/pallet/layer contacts,
  all four layer counts, 17 distinct layouts, loose-pipe grounding, yard render
  vertices matching collider corners, and no residual northeast pipe geometry.
- Offline navigation rebuilt: **45,815 nodes**. Lighting rebaked/exported:
  **2,788 unique valid receiver bindings**. [Route/loot/UV checks](summary.txt):
  **8,702/8,720 pass**. All remaining floors connect via ground navigation;
  stair/crossing/train clearance, candidate footprints and all 800 crew/seed
  selection cases pass. The only failures are the same 18 previously documented
  ground-loot overlaps in B/C/D; no new failures were introduced. Their serialized
  marker records are [unchanged from the input prefab](baseline-overlaps.txt).
- Unity 6000.3.24f1; project target verified as `X:/Work/duck-rails/Assets`.
  Scripts compile and final Console Error/Exception/Assert query is empty.
- Visually inspected [one layer](one-layer.png), [two layers](two-layer.png),
  [four-layer side](four-layer-side.png), [truck](southeast-truck.png),
  [formwork](southeast-formwork.png) and [northeast removal](northeast-pipe-removed.png).
  Captures use temporary fill/no fog to expose contacts, restored afterward;
  they are not nighttime/headset acceptance. No gaps observed in the revised
  stacks. Existing dark faces remain part of the lighting presentation.
- No Play Mode, device/multiplayer run, backend query or diagnostic report access.
  Appearance approval, natural interaction and runtime/headset behavior remain
  **queued for playtest** in the Skyscraper reduced-source entry.

## Continue

Review `Assets/DuckRails/Blocks/skyscraper-works-v1.prefab` in Prefab Mode; use the
Skyscraper bake scene for baked-lighting review. The source prefab is left open.
Authoring and contact checks: `Assets/Editor/RailSkyscraperMaterials.cs`.
`RailSkyscraperReduction.ValidateTo(...)` reuses the previous route checks while
preserving earlier evidence. One-time installers guard their generated folders;
edit the saved result rather than rerunning an old whole-block builder.

Recovery prefab: `output/skyscraper-materials/recovery/before.prefab` (local).
New mesh container: `Assets/DuckRails/SkyscraperWorks/MaterialsRevision/Meshes.asset`.
The old atlas/Blender export still represents obsolete geometry; export fresh
source only after the user has reviewed these changes.
