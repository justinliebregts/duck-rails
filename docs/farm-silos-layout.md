# Farm Silos layout

## Northeast paddock polish — November 11, 2026

User approved replacing only the northeast cornfield with the [reviewed pond/playground concept](validation/farm-paddock/approved-concept.png). Installed under `Env/Northeast pond and playground` in the existing Farm prefab; the other two cornfields and all existing buildings remain. Three raised platforms (two roofed towers) connect via two bridges, with a static slide, two climbing ladders, and an empty swing frame (no chains, seats, joints or physics). Hay stacks, broken fencing and a cart dress the paths. The pond is a shallow ground depression, maximum 0.38 m below the surrounding ground, with an inexpensive opaque water surface and six dense horsetail clusters (108 stems). Vegetation has no collision or shadow casting. Water adds no swimming or physics system.

Measured against the user's current saved prefab: removed field **48,132 triangles**, replacement **5,028 triangles / 14 renderers**. Whole Farm **200,317 → 157,213 triangles**, a **43,104 / 21.5% reduction**. This is authored geometry, not measured headset frame time. Existing Quackville materials are reused/adapted; broad opaque surfaces avoid transparent foliage/water overdraw.

Redistributed the field's existing **14 coal and 14 gold candidates** among decks/bridges, hay stacks, cart and pond-edge discoveries. Repositioned its two enemy candidates and appended four, giving **six paddock enemy candidates**. Whole-block pools are **100 coal / 100 gold / 22 enemies**; per-run settings and crew scaling are unchanged. Markers retain native prefab connections and registration. Placement is ready for inspection; it does not guarantee these landmarks are all selected in a run.

Offline navigation rebaked (25,064 nodes); Farm lighting rebaked/exported with 896 valid static receiver bindings. Editor checks passed for all 28 loot footprints, 34 spawn clearance envelopes plus a positive overlap control, six enemy navigation/bounds positions, sampled deck support/headroom and pond depth, 800 enemy selections (all crew sizes, 100 seeds, exact quotas and 15 m spacing), and 200 maximum-crew loot selections. Saved Farm lightmap references and companion build registration pass. The broader catalogue lighting checker stops on its unrelated outdated four-light assumption for the midpoint helipad; it is not reported as passing.

[Unity daylight review](validation/farm-paddock/unity-preview.png) · [geometry counts](validation/farm-paddock/geometry.txt) · [checks and limits](validation/farm-paddock/checks.txt) · [clearance](validation/farm-paddock/clearance.txt) · [preservation](validation/farm-paddock/preservation.txt). Preview uses temporary daylight and is not final night-lighting acceptance. **Queued for playtest:** natural climbing/slide feel, pond traversal and loot recovery, enemy pursuit, late join and Quest performance.

Authoring: `Assets/Editor/RailFarmPaddock.cs` is a one-time scoped installer and refuses to overwrite an installed paddock. Edit the saved prefab for subsequent polish. The old full Farm installer now refuses to erase this pass; corn-only updates skip the replaced field. No other blocks were rebuilt. The user's existing Expedition edits were saved with explicit authorization before scene isolation.

## Spawn placement first pass — November 8, 2026

User requested approximately 100 coal, 100 gold and 18 enemies, with substantial loot hidden inside corn, plus house/buildings/roofs/trees/props. Replaced 42 old markers with exactly 100 coal, 100 gold and 18 enemy prefab candidates. Retained per-run counts 6/2/5 and existing seeded selection.

Each of the three cornfields contains 14 coal and 14 gold, sampled inside its planted area away from the authored path/prop clearings. Both loot types have four room candidates on each house level (basement, ground, upstairs, attic), ten furniture/prop candidates, four hay-climb candidates and six tree-root candidates. Coal additionally has ten building-roof and twelve barn/shed corner candidates; gold has fourteen roof, six barn/shed corner and two silo-base candidates. Enemies cover corn/approach areas, barn/sheds, orchard/departure and a track position.

Verified in the Duck Rails editor: all proposed positions have raycast support and clear spawn envelopes; loot has footprint support and slope-aware clearance. Enemies fit current bounds and lie within 0.8 m of baked ground navigation. Positive overlap control detected a known furniture collider. Saved-reference, 100 seeds per type, bilateral-enemy and navigation checks passed (536 checks). Preserved all 996 environment transforms and the graph reference. No geometry, lighting, runtime logic or run counts changed.

User reviewed and approved the first pass and requested commit/push. Natural VR reachability, settling on pitched roofs, corn search difficulty and actual client/headset acceptance remain pending. No diagnostic reports were inspected.

November 4, 2026. User approved building V3 in 3D and added apple trees around the farmhouse. Rough Quackville geometry is installed; validation is recorded below.

[Current V3 layout and farmhouse section](../output/imagegen/farm-silos-v3/layout.png) | [Editable drawing source](../output/imagegen/draw-farm-silos-v3.ps1) | [Previous hay-climb study](../output/imagegen/farm-silos-v2/layout.png)

## Confirmed direction

November 5 concept follow-up: user likes the character pass, requests corn about twice as tall and dense enough to obstruct sightlines, and suggests relocating the farmhouse west of the railway opposite the barn. [Revised overhead](../output/imagegen/farm-character-v2/top.png), [west-side farmhouse](../output/imagegen/farm-character-v2/house.png), and [dense corn path](../output/imagegen/farm-character-v2/corn.png) explore that arrangement. Northwest house/orchard replaces northwest corn; northeast becomes dense corn. Front porch faces east toward tracks. Generated concepts only, using built-in image_gen; prompts saved beside images. Exact heights (3.6–4 m explored), footprints, track shape and terrain are illustrative, not construction measurements. Retain actual authored track when translating to a measured plan. Unity remains unchanged under the user's images-only instruction; these concepts supersede V4 orientation for discussion, not the installed V3 geometry.

November 5: user requested turning the farmhouse to face the tracks and asked for ideas to make the block less flat/boring. [V4 orientation drawing](../output/imagegen/farm-silos-v4/layout.png) turns the front west, with apple trees and paths rearranged. Unity remains V3 pending review of the revised plan. The basement excavation, internal stairs, exterior cellar stair and probes must rotate with the house; preserve the railway and block seams. Suggested raised orchard, winding drive, shallow drainage ditch/timber crossing, irregular corn edges/scarecrow clearing and barn loft/hoist are proposals only, not approved additions.

Corn rows, pyramid-stacked hay bales that help players climb onto buildings including the barn roof, and a water tower. Keep the farm focal point mainly in the middle third while expanding supporting scenery throughout the whole block. Follow drawing approval with rough Quackville blockout.

## Proposed layout

### V3 farmhouse revision

User requested replacing both top-right (northeast/departure) corn plots with a two-storey farmhouse, plus a basement and attic. The supplied photo establishes the reference: white siding, broad columned front porch, steep roof with a front gable, tall windows and chimney. This is confirmed direction; the following dimensions and room arrangement are proposals.

Proposed house footprint X=14..34, Z=128..146 (20 x 18 m), with a 4 m deep porch facing south toward the central farm. Retain lawn, rear/side paths and the flat seam strip. The farmhouse is a secondary departure destination; barn and silos remain the central focal point. Northeast corn is removed; western and approach corn remains.

Four accessible levels: basement cellar/utility, ground-floor living/kitchen/dining, upper-floor bedrooms and landing, and attic storage beneath the gable. Internal stairs connect every level; exterior rear cellar steps provide a second basement exit. Stair footprints, room partitions, landings, basement excavation and attic headroom need detailed layout before implementation. The section shows connections schematically, not stair dimensions. Preserve existing enemy bounds: adding the farmhouse outside them does not imply enemy navigation there.

### Retained V2 direction

- Retain the 96 x 160 m footprint. Main barn, paired silos, feed barn, workshop and water tower occupy the central third (Z approximately 53.3–106.7). Preserve the existing railway's westward curve, reaching X=-4 at Z=80, and its authored stop/entry/exit. Prefab source inspection confirms length 160, the sampled curve and existing east enemy bounds; no editor validation is claimed.
- East barn footprint X=17..35, Z=66..89, with a low lean-to at Z=60..66. Front and rear doors connect to the ground-level farmyard loop. Two silos stand behind the barn at Z=98.
- H1 hay pyramid reaches the lean-to roof; a smaller upper stack continues to the barn eave. H2 reaches the feed-barn roof across the railway. Use broad, stable stepped bale geometry, with landing space and a reversible descent route. Exact bale/roof heights and hand clearance remain proposals to tune against inherited locomotion; the side study is schematic, not an engineered section. Climbing is optional, with normal ground entrances retained.
- Water tower at X=-24, Z=58 is a central landmark opposite the barn. Tank access is not assumed. Tower form/height and barn interior details remain open for review.
- Approach: corn rows on both sides, cross-paths, wagon, tractor, scattered trees and fence remnants. Departure: farmhouse, western corn, pens, cart shed and additional hay stacks. Scenery extends approximately Z=10..153 while a proposed 6 m perimeter stays flat.
- Corn rows have breaks and perimeter bypasses; aim for enclosing silhouettes at duck eye height without trapping players or hiding the only return route. Corn collision, density, rustle audio and final height require later art/locomotion review; no new vegetation interaction system is implied.
- Retain the earlier low rolling pasture direction at the sides, level building pads and flat railway. Exact contours are deferred; paths and crop plots in this drawing are schematic.

## Approval and acceptance

V3 replaces the earlier generic farm drawing. User approval to build, including apple trees, was received November 4. Preserve stable block identity, six coal per variation (three per side), gold counts and existing east-side enemy coverage. Outer scenery does not authorize broader enemy bounds.

After approval: rough Quackville geometry/materials, actual rail/train clearance checks, measured climb transitions and roof descent, corn path/hand clearance, loose-item recovery, offline navigation and lighting rebakes, runtime loading, then real-client/headset/performance acceptance. Drawing review does not complete any gameplay acceptance.

## Installed rough blockout

Authoring: `Assets/Editor/RailFarmSilos.cs`. Run **Duck Rails > Authoring > Farm Silos > Install approved farm** only in Duck Rails Edit Mode with saved scenes and no bake active. This explicitly rebuilds this farm's authored geometry, preserves track/anchors/variation identities and rebakes its offline navigation and lighting. Catalogue rebuilds preserve the installed farm.

- Main barn and paired silos, water tower, feed barn/workshop and roof-reaching hay pyramids remain central. Barn roof climbs use a 2.7 m lean-to and a second stepped hay stack. Supporting pens, cart shed, wagon, tractor, broken fences, trees and four corn fields extend into the outer thirds.
- Farmhouse: two full floors at Y=0 and 3.2 m, basement at -3.2 m, attic floor at 6.4 m. Three 3 m wide internal stair flights connect them with approximately 0.178 m rises and 0.4 m treads. The ground slab is physically cut out beneath the house and exterior cellar stair. Rear cellar steps provide a second basement exit. Rooms include cellar/utility, kitchen/dining/living areas, upstairs bedrooms and attic storage. The main hall remains clear.
- White weathered boards, red barn boards and dark roofing reuse Quackville Town atlas pixels. Broad columned porch, tall windows, front gable and chimney follow the photo reference. Geometry and props remain intentionally rough.
- Nine apple trees surround the house, with 81 visible decorative apples and low-poly rounded foliage. Apples are static scenery, not new collectible items. Four combined-mesh corn fields have visual stalks/leaves/ears, cross-gaps and perimeter routes; corn is non-colliding scenery in this pass.
- Eighteen baked lights and additional probes cover the house levels and farm. Existing Expedition atmosphere is unchanged. Existing east-side enemy bounds are retained; the farmhouse remains outside enemy coverage.
- Build/capture commands operate on `Assets/DuckRails/Lighting/Blocks/farm-silos-v1/farm-silos-v1-bake.unity`; runtime uses the original `farm-silos-v1.prefab` and its exported lighting companion.

### Validation

First checks caught blocked stair headroom from filled stair risers, a hall bench and two loot/prop overlaps. Replaced the stacked flight blocks with thin overlapping treads, moved the bench out of the hall and repositioned the affected coal markers. The original curved-track stop is X=-4, Z=80 and is retained.

Final evidence: **1,893 physical/layout/UV checks**, **619 catalogue checks**, and all catalogue lighting export checks pass. Farm export contains 18 baked lights and 903 static receiver bindings. An offline Play Mode instance at world Z=320 reached lighting readiness with all 903 texture/index/UV bindings correct and no lighting failure. Evidence lives in `docs/validation/farm-silos/` (`geometry-checks.txt`, `week3-checks.txt`, `lighting-checks.txt`, `runtime-proof.txt`). Repeat geometry checks with **Validate farm geometry**; `RuntimeProof.unity` is an offline inspection scene, not a shipping entry scene or player simulation.

Visual captures were inspected after fixes to stair clearance, attic eave enclosure, farmhouse siding and repeat-install mesh GPU refresh. [Farmhouse and apple trees](../output/farm-silos/farmhouse.png), [barn and hay climbs](../output/farm-silos/barn-and-silos.png), [whole block](../output/farm-silos/whole-farm.png), [basement](../output/farm-silos/basement.png), [attic](../output/farm-silos/attic.png). Capture views use temporary bright review lighting with fog off; they do not establish final gameplay darkness.

Headset hand climbing, roof descent, corn readability, multiplayer and Quest performance remain unverified. The loading proof uses one editor with no authenticated crew and is not locomotion or network evidence.

## V5 approved expansion — November 5

User explicitly approved building the revised 3D layout. This supersedes the old northeast-house, four-small-fields and perimeter-bypass descriptions above.

- Farmhouse and nine apple trees now occupy the raised northwest orchard, west of the railway. The porch faces east toward the tracks. The complete house is rigidly rotated and raised 1.5 m, preserving its basement, both main floors, attic and four stair flights. Terrain excavation follows the relocated basement and exterior cellar stairs.
- Three large corn areas cover 4,404 square metres before passage/prop clearings. Dense overlapping opaque leaves reach approximately four metres high. Winding vegetation gaps replace the old small-plot cross-lanes. Corn remains non-colliding visual scenery, combined into small renderer patches and probe-lit.
- Removed perimeter lanes. Two farmyard links and a winding orchard drive connect buildings; corn reaches within about two metres of side edges. Track geometry, stop, enemy bounds and flat end seams remain unchanged.
- Added orchard elevation, low retaining-stone runs, shallow drainage ditch and timber footbridge, barn loft landing/window and decorative hoist, scarecrow clearing, broken fence, tractor, tire swing and apple crates. These are rough Quackville props, not new interactive systems.
- Offline navigation and lighting were rebuilt and exported. Current checks pass: 1,865 geometry/layout/UV checks and 757 catalogue checks, plus lighting export checks. Runtime proof is recorded separately in runtime-proof.txt.
- Review images are regenerated in output/farm-silos, including top-down.png, whole-farm.png, farmhouse.png and corn-lane.png. Temporary review fill/fog settings are not shipping gameplay lighting.
- Headset climbing, corn performance/readability, loose-item recovery and multiplayer acceptance remain outstanding.

Authoring now also uses Assets/Editor/RailFarmExpansion.cs.

### Corn optimization — November 5

User authorized disabling corn shadow casting and simplifying geometry. Reduced each stalk from nine leaf diamonds to five broader leaves, retaining plant positions, four-metre height, field coverage and 79 culling patches. Corn now has 154,392 triangles / 308,784 vertices, down from 242,616 / 485,232 (36.4%). ShadowCastingMode is Off on all corn patches in the saved prefab and bake scene; future authoring builds preserve it. Corn remains non-static, probe-lit scenery; the 851 baked receiver bindings are retained. No navigation or lighting rebake was required for this visual-only update.

Geometry/catalogue/export checks passed and the regenerated ground-level capture was inspected. Unity emitted scene culling-mask allocation warnings during the combined check/capture command, but evidence files and captures completed; the saved prefab was independently inspected afterwards. Headset GPU timing remains unmeasured. No LODs added in this pass given the short fog visibility; fog itself is not a rendering cull, so distance culling remains a possible measured follow-up.
