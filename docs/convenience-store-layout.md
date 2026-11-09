# Convenience Store layout

## V5 implementation — November 5

User approved the arcade/streetlight revision for 3D. Installed on the existing convenience-store prefab under `Convenience store authored v5`, using `RailConvenienceStore` plus `RailConvenienceStoreV5`. Prior V2 implementation/evidence below is historical.

New content: west-facing 18 x 24 m Quack Mart with stocked aisles, fridges, freezer, checkout and stockroom; east-facing 18 x 20 m arcade with eight cabinets, some illuminated screens, prize machine, counter, air-hockey prop and rear service exit; offset four-pump gas canopy/pylon; open-ended car wash; north restaurant strip; eight warm streetlights; emissive shop/arcade/gas/car-wash/restaurant signs with baked local spill. Grass verges, rest-stop picnic/vending/bus shelters, abandoned parking, utilities, fence fragments and trees populate the outer thirds. Gates stay raised and arcade/pump props do not add gameplay systems.

Track, stop, stable ID and spawn counts remain. Enemy bounds extend from Z=45..115 to Z=45..125 on the east only (X=9..43); restaurant frontage gains coverage while rear kitchens, car wash and the west arcade remain outside enemy bounds. Offline graph covers the expanded eastern geometry, retaining the shared chunk origin; runtime bounds still restrict enemy reach. This retains at least 80 m between this block's encounter end and the next standard Z=45 encounter at 4 m/s. Loot markers are moved to supported clear positions.

Drainage is represented by shallow dry-lined surface channels with low curbs and timber crossings/approaches rather than excavating the retained ground beneath the railway. Storefront windows have clear framed openings backed by collision panes; no opaque glass hides the interior. Shared Quackville atlases and cropped grass/foliage are reused. Thirty environment lights are baked; moving lights and expedition atmosphere remain unchanged.

Existing unsaved Expedition edits were backed up to `Temp/store-v5-backup/Expedition-unsaved.unity` and saved before switching to authoring. Unity compilation and offline navigation/lighting export passed. Validation:

- [1,328 physical/layout/UV/navigation assertions](validation/convenience-store-v5/geometry-checks.txt), including clear routes, supported loot, flat seams, railway clearance and catalogue rebuild preservation.
- [757 catalogue checks](validation/convenience-store-v5/week3-checks.txt) and [lighting export checks](validation/convenience-store-v5/lighting-checks.txt); store has 30 baked environment lights and 756 receiver bindings.
- [42 offline Play Mode assertions across two assembled routes](validation/convenience-store-v5/runtime/runtime-proof.txt), including readiness, translated native probes, renderer bindings, route replacement and teardown. Seeds 11 and 43.
- Actual Unity previews: [street](../output/convenience-store-v5/street.png), [arcade](../output/convenience-store-v5/arcade.png), [store interior](../output/convenience-store-v5/store-interior.png), [whole block](../output/convenience-store-v5/whole-block.png). Capture fill light and disabled fog improve inspection; these are not final expedition-atmosphere evidence.

Headset traversal, low-eye material/lighting readability, enemy pursuit, real-client behavior and Quest performance acceptance remain open. Arcade machines, fuel pumps, wash equipment and raised gates are scenery.

## V5 arcade revision — current review plan

November 5: user selected an abandoned arcade instead of the western garage and explicitly requested streetlights along the road. [Updated site and arcade interior](../output/imagegen/convenience-store-v5/top-down.png) | [Editable drawing](../output/imagegen/draw-convenience-store-v5.ps1). This supersedes the V4 garage proposal. The drawing was subsequently approved for 3D and installed as recorded above; the proposal notes below preserve the design history.

The arcade retains X=-37..-19, Z=66..86 (18 x 20 m), with a 3 m east-facing entrance at Z=77..80 toward the railway and west rear exit at Z=69..72. Proposed interior: two banks of four arcade cabinets, prize machine/display, counter, air-hockey table and rear service/storage strip. A broad central entry route turns behind the cabinets to the rear exit. Glowing ARCADE fascia and a few emissive screens establish its nighttime identity. Machines are environmental props; playable minigames are not part of this layout.

Replace the tow-truck apron with an entry plaza/vending feature; reinterpret the former wreck yard as abandoned customer parking with damaged cars and two fence gaps. Retain store/gas canopy, car wash, restaurant strip, picnic/bus-stop area, utilities and outer scenery from V4, including its store interior inset.

Streetlights are confirmed; proposed placement is eight warm lamps, at X=-36,-18,18,36 on each road sidewalk (Z=103 and Z=113). Keep poles outside crossings and the railway envelope; favor baked fixture illumination. The previously suggested dead lamps are not a confirmed requirement. Exact heights, intensity and spacing remain subject to layout/lighting review and headset performance validation.

User approved this drawing with “let's try in 3D now.” V5 replaces the installed V2 block. Northern restaurants and east-only enemy navigation are reconciled in the implementation notes above.

## V4 top-down revision — November 5

User endorsed the wide concept details and requested this quick coordinated plan before rebuilding. [Site and store interior sheet](../output/imagegen/convenience-store-v4/top-down.png) | [Editable drawing](../output/imagegen/draw-convenience-store-v4.ps1). Concept contents are selected; exact footprints below remain proposed for layout approval. No Unity geometry changes.

Retain the 96 x 160 m block, straight X=0 railway and stop at Z=80. Keep a clear +/-4 m rail corridor and 3 m walking strips at X=-7..-4 and X=4..7. Seams remain flat. Railway crossing road shifts north to Z=104..112, beyond the stopped train; crossing gates remain raised scenery.

- **Store:** X=22..40, Z=62..86, 18 m deep with 24 m frontage. Main entrance on WEST wall at Z=77..80 faces the tracks. The roof-off inset has the same orientation: checkout near front, three 7 x 1.5 m shelf runs separated by 3 m, north-wall fridges, chest freezer and a 4 m-deep east stockroom. Rear service exit at X=40, Z=79..82. Exact wall thickness/clear widths must be checked in 3D.
- **Gas forecourt:** canopy X=10..20, Z=88..100; two islands/four pump faces. Northward offset leaves the direct train-to-shop route open at Z=78.5. Four parking bays south of that route, glowing GAS pylon near road, pink storefront fascia, entrance ice/vending and north delivery van. No fuel gameplay implied.
- **Service/car wash:** 4 m nominal east lane with side dumpsters, south/north store links; car-wash shell X=28..40, Z=38..52 with 6 m-wide open-ended passage and an exterior bypass. Car wash has its own pink sign.
- **Restaurants:** three 10 x 12 m units at X=10..40, Z=120..132 across the road. Glowing individual signs, south/front dining, rear kitchens, front/rear exits and perimeter walks. Detailed kitchen partitions follow this footprint review.
- **Garage:** X=-37..-19, Z=66..86. East-facing open bay toward tracks at Z=76..82, adjacent closed bay, workbench/storage and west rear door. Amber sign and tow-truck apron. Wreck lot X=-39..-12, Z=90..101 has two fence gaps.
- **Outer areas:** west picnic/rest stop X=-37..-12, Z=28..56 with tables, vending shelter and bus stop; northwest utility pad X=-38..-14, Z=120..136. Shallow off-track drains near Z=19..22 with 4 m bridges and dry bypasses, plus trees/fence remnants toward the outer edges. Drain depth, banks and item recovery remain to detail; do not excavate beneath railway or break flat seams.

This revision moves restaurants beyond the old east enemy Z=45..115 envelope. Before implementation, explicitly reconcile the east-only bounds/crawlgraph and resource placements with these new footprints; do not accidentally leave enemies in walls or silently add west-side enemy coverage. Preserve spawn counts and stable IDs. Rebuild offline navigation/lighting and verify route loading after implementation; historical V2 checks below do not validate this new plan.

## November 5 revision — rail-facing storefront and glowing signs

Follow-up: user likes the concept details and requested a wider shot showing the other rail side. [Both-sides wide concept](../output/imagegen/convenience-store-v3/both-sides-wide.png) explores a west auto-repair garage with glowing amber signage, partly open bay, tow truck and fenced wreck lot, plus a picnic/vending/bus-stop area, utility yard and drainage crossings. These new west-side additions remain proposals. [Wide prompt](../output/imagegen/convenience-store-v3/wide-prompt.json), generated with built-in image_gen using the establishing study as reference. Exact frontage rotation, dimensions and trackside circulation must be resolved in the revised top-down plan; the perspective concept is not a surveyed layout. No Unity changes.

User finds the installed blockout basic/boring and requests new concept renders. Confirmed new direction: turn the shopfront/main entrance toward the tracks and make the business signs glow. A gas station is exploratory (user: "Maybe"); exact orientation, footprint and revised circulation need a new top-down layout review before another 3D pass.

Two built-in image_gen studies: [elevated establishing view](../output/imagegen/convenience-store-v3/establishing.png), [low-eye arrival view](../output/imagegen/convenience-store-v3/arrival.png). [Exact prompts](../output/imagegen/convenience-store-v3/prompts.json). These are separate generated interpretations, not geometrically matched views of one model or Unity captures.

Proposed treatment: rail-facing glazed store with warm interior light, pink QUACK MART fascia, teal gas pylon/canopy, offset pump islands that preserve the view of the entrance, and individually glowing abandoned restaurant signs across the perpendicular road. Emissive surfaces plus local light spill should give the signs presence; rendered bloom/wet reflections are illustrative, not approved headset lighting targets.

Suggested supporting additions: small abandoned car-wash passage with an exterior return route, delivery alley, ice freezer/vending machine beside the entrance, damaged bus shelter, broken planted curbs and clustered roadside clutter. Retain readable train access and scenery through the outer thirds. No new fuel mechanic, active traffic or functional car-wash system is implied. No Unity assets changed for these studies. Historical installed-block evidence below applies to V2 only.

November 4, 2026. User approved the expanded drawing and explicitly requested implementation. The approved plan is installed with rough Quackville materials; validation evidence and remaining acceptance are recorded below.

[Top-down site and interior drawing](../output/imagegen/convenience-store-v2/layout.png) | [Editable drawing source](../output/imagegen/draw-convenience-store-v2.ps1)

## Confirmed direction

Small/medium convenience store with front parking, aisles, fridges/freezers and checkout. Large neon sign in the parking lot, side dumpsters, and a strip plaza of abandoned restaurants across the road. Traffic scenery and a gated rail crossing are candidate details.

For all blocks: top-down layout first, user confirmation next, then blockout with rough Quackville materials. The featured destination can occupy the middle third, but supporting scenery should extend through the outer thirds toward the edges rather than leaving empty approach/departure areas.

## Proposed layout

- Retain the 96 x 160 m footprint, straight railway at X=0 and stop at Z=80. Unity MCP verified the project path, length, track and anchors on November 4; inspection was read-only.
- Store footprint X=14..38, Z=58..76: 24 x 18 m including a rear stockroom. Three 1.5 m shelf runs with nominal 3 m gaps, front/rear cross-aisles, east-wall fridges, separate freezer and checkout near the front entrance. Dimensions remain review proposals.
- Six parking bays and two abandoned cars in front; neon pylon near the road entrance. Two side dumpsters with bypass space and a rear service exit reconnecting around the store.
- Two-lane road at Z=90..98 crosses the railway beyond the schematic stopped train. Three 10 x 12 m restaurant units across the road at Z=102..114, with front dining areas, rear kitchens and rear exits. Restaurant interior detailing follows layout review.
- Crosswalk, faded paint, signs, lamps and cones. Crossing gates are proposed static scenery initially; active animation or traffic simulation is not approved. Preserve pedestrian bypasses and train clearance.
- West overflow lot with wrecks and bus shelter provides a secondary scavenging area. Approach: abandoned car lot, utility pad, trees, fence remnants and paths. Departure: shuttered auto shop, delivery lot, more trees and broken fences. Scenery reaches roughly Z=10..154 while the outer 6 m seam strip stays flat. Density and exact prop positions remain proposals.

## Implementation gate and acceptance

Layout confirmed by the user on November 4. The updated drawing supersedes the earlier generic store/garage study. Preserve stable block identity, six coal per variation with three per rail side, gold counts and current enemy coverage. Supporting western scenery does not imply expanded enemy bounds.

After approval: build rough Quackville geometry/materials, check doors and full hand clearance, train/road crossing clearance and loose-item recovery, rebake offline navigation and lighting, and verify runtime loading. Headset traversal, material readability, multiplayer and performance remain separate acceptance work. The drawing is not evidence for any of these checks.

## Installed blockout

Installed on `Assets/DuckRails/Blocks/convenience-store-v1.prefab` under `Convenience store authored v2`. The dedicated authoring command is **Duck Rails > Authoring > Convenience Store > Install approved layout** (`RailConvenienceStore.cs`). It rebuilds only this block's owned geometry, relocates existing resource/enemy markers, bakes the crawlgraph and queues the existing block lighting exporter. The catalogue builder preserves the authored root.

- 24 x 18 m store, glazed front, wide entrance, three stocked shelf runs, five fridge cabinets, chest freezer, checkout/register and rear stockroom with service exit.
- Six parking bays, abandoned vehicles, large pink emissive QUACK MART pylon, two dumpsters and walkable side/rear loops.
- Three restaurant interiors with tables, counters, kitchens, front/rear exits and perimeter bypasses.
- Road, crosswalk, markings, stop signs, static raised rail gates, bus shelter and traffic bollards. No active traffic or gate animation.
- Outer thirds: old car lot, utility cabinets, closed auto shop, delivery van lot, paths, fence fragments and 26 reused low-poly pine silhouettes. The auto shop is intentionally shuttered; its apron is accessible.
- Shared Quackville Town/Industrial materials and metre-scaled UVs; campground foliage meshes/material reused. Dedicated simple materials for glass, fixture lenses and emissive neon. Sixteen baked lights; 652 static receiver bindings exported.

Track samples, stop, stable IDs, all three spawn arrays, six coal/two gold per variation and original east-side enemy bounds are retained. Loot positions were moved away from new walls; the western areas remain player-only scavenging. Gate posts are outside a clear +/-4 m rail corridor; railway graphics retain no colliders. Flat ground and perimeter joins are retained.

## Editor verification

- Unity compilation passed; offline crawlgraph and lighting bake/export completed.
- [1,087 checks](validation/convenience-store/geometry-checks.txt) pass: physical door/aisle/service routes, resource support/clearance, seam and railway collider envelopes, mesh UVs, ground navigation from the forecourt to store/stockroom/restaurants, and catalogue regeneration protection.
- [619 catalogue checks](validation/convenience-store/week3-checks.txt) and [all block lighting exports](validation/convenience-store/lighting-checks.txt) pass.
- [Authenticated single-editor runtime check](validation/convenience-store/live-check.txt) passed: private expedition reached Running/WorldReady with the store first at world Z=150, its authored root present, 652 store bindings and complete-route lighting ready. Test route: store then lumber yard. Play Mode stopped and previous Test Block Order settings restored. This verifies loading/integration, not natural gameplay or multiplayer acceptance.
- Initial checks caught two resource-wall overlaps and fence fragments encroaching on the seam margin; marker/fence placement corrected, graph/lighting rebaked, then the checks passed.
- Actual Unity views: [street](../output/convenience-store/street.png), [store front](../output/convenience-store/store-front.png), [interior](../output/convenience-store/store-interior.png), [roof-off plan](../output/convenience-store/store-cutaway.png). Captures use temporary review fill and disabled fog, restored afterward; they do not establish final darkness.
- [Whole-block overhead](../output/convenience-store/whole-block.png) shows the installed outer-third coverage. Post-run Unity Console error/exception query returned zero entries.

Target-headset scale, full hand locomotion, natural loose-item recovery, multiplayer/late joining and Quest frame-time/memory remain unverified. Static gates and simple opaque fridge/shop glass are deliberate rough-blockout choices.

## Enemy quadrant coverage — November 8, 2026

User requested enough candidates for spawning-only crew tiers. Added eight enemy
prefab markers through approach/departure areas, two per quadrant. Preserved the
19 existing enemy candidates and all 124 coal/101 gold candidates. Enemy pool is
now 27. Existing environment and marker transforms, navigation and lighting were
preserved; no rebake. All crew sizes across 100 seeds reach their 4/8/12 targets
with 15 m horizontal spacing. User inspection and natural headset/multiplayer
acceptance remain pending. See [placement evidence](enemy-spawn-balance.md#requested-marker-additions).
