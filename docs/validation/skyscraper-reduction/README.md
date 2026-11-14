# Skyscraper source geometry revision — November 14, 2026

Later screenshot follow-up: stack grounding/variation and northeast pipe removal
reduce the current source to 37,184 triangles. This document preserves the earlier
floor-reduction evidence; see [current dressing revision](../skyscraper-materials/README.md).
User subsequently approved this source; the [new Blender review](../skyscraper-low-poly-atlas/README.md)
preserves 37,184 triangles exactly.

User approved all four proposed optimizations and specifically requested the
northwest top floor and southeast top two floors removed, with the tall framing
retained. The requested source review is now complete and the new atlas review
is linked above. The existing tower footprints and
crossings remain the approved layout; this is a vertical revision to it.

## Installed result

- Production retains prefab GUID `2a747f5f81583b74a873a4c2702813af`.
- Restored the original repeating-material meshes in place and removed the 394
  generated atlas chunks/bindings. All old atlas/Blender review assets remain as
  historical recovery material, not the current block.
- NW / Tower C: floors at 0/4/8/12 m. SE / Tower B: 0/4/8 m.
  SW / A and NE / D retain 0/4/8/12/16 m. Total **17 playable floors**.
- Removed outgoing stairs, removed-floor shafts/props/guards and unsupported
  overhead fixtures. All **60 original skyline objects** and **five crossing
  decks** retain their exact transforms. Four SE corner pillars survive as open
  framing, corner supports close former slab gaps, and 12 narrow tie beams replace
  the removed slabs' perimeter framing. No new full floor surfaces.
- Simplified upper-floor exterior railings to top rails and all existing posts;
  shaft, bridge and stair rails remain intact. Upper loose-pipe groups retain two
  of four pipes. Removed 88 detail cubes / **1,056 triangles**.
- Removed **3,120 fully buried side triangles** across 520 adjoining slabs.
  Tops, exposed undersides, UV channels and BoxCollider shapes are retained.
- Moved NW/SE rooftop lamps, housings and tripod stands down 4/8 m to the new top
  floors. Reverb boxes follow the reduced playable heights. 52 baked lights remain.
- Removed candidates belonging to the deleted floors: **202 coal / 192 gold /
  23 enemies** remain, versus 270/270/26. Per-run tuning and runtime selection
  rules are unchanged. No replacement scatter pass was performed.

Triangle progression: **228,866 atlas -> 47,996 original -> 41,960 floor/frame
revision -> 40,904 detail simplification -> 37,784 final**. This is 83.5% below
the atlas version and 21.3% below the original. The unchunked review has **2,813
MeshRenderers**, including the CutePixel sign; draw-call optimization is deferred.
Triangle reduction is not a measured device frame-time improvement.

## Evidence and limitations

- [Installation counts and preservation](install.txt).
- Unity 6000.3.24f1, verified `X:/Work/duck-rails/Assets`, Edit Mode only.
  Scripts compile; final Console Error/Exception/Assert query is empty.
  Offline navigation rebuilt to **46,182 nodes**; lighting
  rebaked/exported with **2,812 valid unique bindings**, three lightmaps and
  439 probes. Original production prefab and graph GUIDs remain unchanged.
- [Focused checks](summary.txt): **8,726 of 8,744 assertions pass**. All 17
  floors connect using ground-only navigation; removed-floor samples have no
  ground nodes. Stair support/headroom, crossing support, train clearance,
  loot footprints, UV channels and all 800 crew/seed selection cases pass.
- The 18 failures are the **previously documented ground-floor loot overlaps**
  in Towers B/C/D, not new placements. [Baseline comparison](baseline-overlaps.txt)
  confirms every implicated marker record and the original ground slab transforms/
  colliders are unchanged. They remain open in the existing playtest entry.
- [Separate tripod check](tripod-support.txt): all 12 rooftop feet are supported,
  excluding each foot's own collider from the query.
- Visual inspection: [NW](northwest.png), [SE](southeast.png),
  [overview](overview.png), [railway](railway-night.png). Tower captures use a
  temporary directional fill and no fog; railway uses baked lighting with fog
  disabled. No missing slab undersides or floating rooftop stands observed.
  The railway view retains dark regions; these captures are not headset evidence.
- No Play Mode, multiplayer, device capture or diagnostic report access.
  User appearance review, natural traversal/loot reach and runtime/headset
  performance remain **queued for playtest**.

## Review and next atlas pass

Open the production prefab in Prefab Mode for geometry editing, or
`Assets/DuckRails/Lighting/Blocks/skyscraper-works-v1/skyscraper-works-v1-bake.unity`
for baked-lighting review. Authoring/check helper: `Assets/Editor/RailSkyscraperReduction.cs`.
Its one-time installer must not be rerun over further edits. Validation intentionally
still reports the 18 baseline overlap failures rather than silently accepting them.

Recovery snapshots are in `output/skyscraper-reduction/recovery/`; original atlas
source/review remains in `ArtSource/SkyscraperAtlas`. The retired fingerprint is
`production-state-before-reduction.json`; no current production atlas marker exists.
Never restore an old fingerprint to bless these different meshes. Do not rerun the
original whole-block builder or old atlas integration/export against this revision.

After user review, export fresh source and lighting. Keep spatial chunking, but use
repeating materials or a deliberately validated repeating-texture shader/array
approach rather than tessellating every texture repeat. Preserve simple cube faces,
UV2/lightmap bindings and collision. The old atlas's 4.77x triangle growth must not
be reintroduced as an incidental cost of consolidation.
