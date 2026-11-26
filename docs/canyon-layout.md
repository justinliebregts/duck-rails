# Canyon

## Connecting gallery, November 26, 2026

The west and east gallery arms now meet the under-river passage in one faceted sandstone junction. The two arms span 30 m between the winch workings and the east mine. A separate outer skin blocks light around the bends. The interior collision mesh contains 724 triangles. The lanterns and timber frames retain their authored positions. Compare the baked player-height views of the [west arm before](../output/canyon-survey/gallery-west-before.jpg) and [after](../output/canyon-survey/gallery-west-after.jpg), and the [east arm before](../output/canyon-survey/gallery-east-before.jpg) and [after](../output/canyon-survey/gallery-east-after.jpg).

The rebuilt crawlgraph has 32,140 nodes. Ground routes from the southwest shaft reached all 42 samples along the gallery and both mine exits. All 959 baked lighting bindings resolved, including the gallery. [Geometry and seam checks](validation/canyon-survey/geometry-checks.txt)

## Under-river cavern and survey camp corrections, November 26, 2026

Between Z=46 and Z=106, the under-river passage and three side chambers share a low-poly sandstone mesh. Its outer skin blocks light, and only the inner surface has collision. The floor and ceiling meet the original shaft and gallery openings. The lightmaps were rebaked with a larger allocation for the cavern. [Before](../output/canyon-survey/cavern-before.jpg) · [After, from the same camera](../output/canyon-survey/cavern-after.jpg)

The sandstone box beside the northwest survey tent moved 9 m. Minimum horizontal clearance from the three tents, including their guy lines, is 3.53 m for the moved box and 13.50 m for the nearest cactus. [Tent entrance](../output/canyon-survey/tent-clearance.jpg)

The saved pool now contains 93 coal, 64 gold and 26 enemy candidates. Fourteen new coal sites are underground and eleven are at surface landmarks. All three selectors passed for crews 1–8 over 100 seeds each. The rebuilt crawlgraph contains 32,276 nodes. Floor routes from the southwest shaft reached all 45 sampled locations, including the side chambers and both mine exits. The Canyon geometry suite passed all 4,770 checks and all 961 baked lighting bindings resolved. [Spawn and clearance checks](validation/canyon-survey/camp-and-spawns.txt) · [Geometry checks](validation/canyon-survey/geometry-checks.txt)

## Survey camp, November 26, 2026

The three south-rim tents use the Lakeside Campground A-frame mesh and its canvas atlas. Each tent stands 2.25 m tall and faces the shared camp clearing. The ground and mine lanterns use an emission mask on the survey atlas so the amber and blue glass glow while the iron frames stay dark. All 12 cacti stand on level rim terrain. The closest measured clearance from a cactus, including its arms, to surrounding scenery was 5.13 m. [Individual cactus measurements](validation/canyon-survey/cactus-clearance.txt)

At the end of this earlier pass the crawlgraph contained 32,696 nodes, and the outstanding failure was eight-player coal capacity. The subsequent cavern and camp correction above filled that shortfall. [Survey tents](../output/canyon-survey/lakeside-survey-tents.jpg) · [Mine lantern](../output/canyon-survey/lit-mine-lantern.jpg)

## Connected mine workings, November 23, 2026

The southwest shaft has a visual-only cover of leaf litter with scrub around the lip. The leaf cover has a textured underside. Five small rocks sit in two uneven groups beside it. Five staggered sandstone ledges descend below it. The rim boulders, scrub and survey stake were moved west onto solid ground, and the trail through the cable-reel shelter was removed.

The northwest shaft drops past sandstone ledges into a timber-framed mine. The chamber under the winch holds an ore cart on a short siding, an air compressor seated on two steel saddles spanning grounded timber skids and sandstone boulders with loose rubble along the walls. See the [equipment views under inspection lighting](../output/canyon-survey/winch-mine-equipment.jpg). Its uphill exit passes through an offset rockfall. A narrow gallery joins the two north workings. The east mouth reaches the dry bank at Z=85. On the south rim a second open shaft near X=-11, Z=41 drops to Y=-18. Follow the winding passage beneath the creek to reach the gallery. Broken mining equipment and scattered rock sit beside this route. Three dim blue ground lanterns mark the under-river passage at Z=51, 78 and 103. Two blue-green lanterns mark opposite arms of the gallery junction. Three small rounded side rooms alternate west, east and west at Z=61, 80 and 98; each extends roughly four metres beyond the corridor wall. A weak cool lantern sits against the southwest shaft wall at the bottom, clear of the drop. Twelve uneven clusters of bedded sandstone break up the long canyon rims. Ten additional rock outcrops sit partway down the side walls and six boulders mark the switchback shoulders. The sandstone has [varied boulder silhouettes](../output/canyon-survey/rim-boulders.jpg), with chipped corners, asymmetric crowns and a mix of squat and upright forms. The rocks use closed indexed shells, padded survey-atlas UVs and a higher baked lightmap scale. See the [baked camp and cliff views](../output/canyon-survey/baked-rocks.jpg). Two 2 m timber fences run the full 160 m at X = +/-47, following the [1 m side-fence standard](side-fences.md). Both shaft drops, the caves and all three side rooms share strong [mine reverb](block-reverb.md). The two Survey Camp sign posts sit 22 cm behind the front face of the board.

The north winch retains its overhead reel. The pit cable and north cross-rim path were removed. The shed has a solid roof, cable spools, a generator and oil drums. Both slopes follow the gable pitch through the overhang and bed into the wall tops. The [roof finish](../output/canyon-survey/winch-roof.jpg) uses a point-filtered corrugated metal tile in the shared Canyon Survey atlas. Thirteen ground lanterns light the exterior. The rail bridge stairs ascend toward opposite ends, the wider platforms replace sections of walkway, and the footbridge planks meet raised end landings. The cactus arms have varied headings and revised joints.

The [Edit Mode geometry checks](validation/canyon-survey/geometry-checks.txt) sample both drops, the connecting routes, bridge floors, clearance and the existing spawn pool. The [review views](../output/canyon-survey/mine-review.jpg) use temporary inspection lighting. Review darkness, rock seams, bridge flicker and hand clearance on headset.

## Earlier survey camp and shaft works, November 23, 2026

The south approach pairs three pitched tents and a signed camp on the east with a cable-reel yard and maintenance shelter on the west. In the central gorge, bedded sandstone follows the playable terraces, an iron truss runs under the rail trestle and the east pedestrian bridge crosses above the creek. On the north rim, the lattice headframe carries a winch and a fixed cable down the shaft. A corrugated winch shed, eastern bridge anchors, worklights, desert scrub and sandstone outcrops complete the departure. The shaft drop, cave exit and six exploration landings retain their collision geometry. The detailed model and shared atlas are editable in [Canyon Survey.blend](../ArtSource/CanyonSurvey/Canyon%20Survey.blend).

The [Unity whole-block capture](../output/canyon-survey/whole-block.png), [south camp](../output/canyon-survey/survey-camp.png) and [north shaft](../output/canyon-survey/shaft-and-anchors.png) use an orthographic review camera and a temporary directional light. The block itself uses seventeen baked fixtures. The model follows the [finished layout render](../output/full-block-finished-reviews/canyon.png), with rock piles, spools and worklights shifted away from the bridge landings, deck, traversable shaft and flat seams.

The saved block uses a single candidate pool with 18 coal, 18 enemy and six gold candidates. Its [geometry and navigation checks](validation/canyon-survey/geometry-checks.txt) cover physical routes, collision and headroom, candidate support, connectivity, visual-only rails, artwork and lightmap UVs. The [offline export](../output/canyon-survey/export.txt) records the graph, lightmaps, probe count and lighting companion. Headset review is queued for camp and shaft readability from the train, carrying items over the bridges, dropping into the shaft and walking out, and frame timing in the gorge.

## Approved 3D work — November 4, 2026

User approved building V3 in 3D and requested several plateaus/landings along the descending cliffs for exploration and enemy encounters. Implementation uses rough Quackville materials. Six broad landings (three on each wall, approximately 10 x 6 m) occupy Y=-4, -8 and -12, with full-width switchbacks between them. The new block is `canyon-v1`; catalogue selection remains five distinct stops plus the existing finale.

The railway and separate east pedestrian suspension crossing are static. The broken worker walkway has deliberate gaps and deck access stairs. Hanging-rope mechanics remain unimplemented pending source inspection; the crossing's suspension cables are structural visuals, not grabbable ropes.

The shaft cave floor is now approximately Y=-16 beneath the northwest rim, rising along its exit passage to Y=-12 at the canyon. This deeper chamber preserves roof clearance below the descending surface paths. The east cave also grades down under the north wall. These are implementation dimensions chosen during collision checking, superseding the earlier flat cave-floor proposal. The creek bed is at most 0.35 m below the canyon floor, with visual water above the bed; no swimming/current mechanic is added.

Authoring: `Assets/Editor/RailCanyon.cs`; focused physical/navigation checks: `Assets/Editor/RailCanyonChecks.cs`. The rough block is installed and editor validation is recorded below. Headset, multiplayer and performance acceptance remain separate.

## Installed rough block and validation

- Prefab: `Assets/DuckRails/Blocks/canyon-v1.prefab`; graph: `canyon-v1.bytes`; authoring scene: `Assets/DuckRails/Lighting/Blocks/canyon-v1/canyon-v1-bake.unity`. Canyon is registered once in the saved Expedition catalogue, now thirteen locations. It is available in the existing Test Block Order prefab picker. Normal runs still choose five distinct stops.
- Three spawn variations each contain six coal markers (three per side), six plateau enemy candidates and two gold markers. Existing enemy selection chooses 4–6 of these candidates. Six landings are connected by ground-only navigation; the upper railway corridor is excluded. The enemy bounds remain Z=45..115, preserving separation from adjacent encounters.
- Static railway trestle, two broken lower catwalks with deck access stairs, east suspension-style pedestrian bridge with handrails, two cave routes, northwest drop shaft, shallow creek, cactus, rock blocks and maintenance shelters are authored with Quackville-derived rough materials. Bridge physics and grabbable hanging ropes are not implemented.
- The shaft chamber is Y=-16 and its passage rises to Y=-12; the east cave descends to Y=-18 and returns along its graded entrance. Surface switchbacks retain headroom above the cave shells. Cave gradients and natural fall recovery still need headset review.
- [3,699 physical/navigation/UV checks](validation/canyon/geometry-checks.txt) passed. Coverage includes full-width switchbacks, six landings, train deck, pedestrian span, cave support/headroom, shaft opening, worker gaps, spawn support, ground-only graph connectivity and rail exclusion.
- [665 catalogue checks](validation/canyon/catalogue-checks.txt) passed, including graph grid alignment, markers, translated graph assembly and route serialization. [Saved catalogue registration](validation/canyon/catalogue-registration.txt) passed.
- [Lighting export checks](validation/canyon/lighting-checks.txt) passed: twelve baked fixtures, 753 static receiver bindings and an enabled additive lighting companion. Navigation and lighting were baked offline.
- [42 offline Play Mode assertions](validation/canyon/runtime-proof.txt) passed across seeds 0 and 1 with Canyon, Quarry, Farm Silos, Maintenance Depot and Lakeside Campground. The actual route builder loaded Canyon at different route positions; bindings, translated probe samples, readiness, route replacement and teardown passed. This is not authenticated multiplayer or player traversal evidence.
- Compilation and final Console error query passed (no reported errors). Rebuild preservation was added for Canyon catalogue inclusion, authored plateau markers and lighting companion build entries; the full destructive Week 3 rebuild was not run.

Actual Unity review captures: [overview](../output/canyon/overview.png), [both bridges](../output/canyon/bridges.png), [cliff landings](../output/canyon/plateaus.png), [shaft](../output/canyon/shaft.png), [cave interior](../output/canyon/cave-exit.png). Captures use temporary review lighting; the saved gameplay atmosphere is unchanged. This remains rough geometry, with final rock shaping, lighting readability and target-device performance pending.

To repeat checks, open the Canyon bake scene and run **Duck Rails > Authoring > Canyon > Check physical layout and graph**. The saved `Assets/DuckRails/Canyon/RuntimeProof.unity` exercises the loading test when played. For player review, use Canyon in **Duck Rails Tests > Test Block Order**, then test descents, plateau encounters, dropped items, shaft landing/exit and both bridge routes with the shared rig and target headsets.

## Drawing history

November 4, 2026. New block requested by the user. Planning only: no prefab, catalogue registration, rope implementation or Unity validation yet. Review the top-down drawing before rough Quackville blockout, per AGENTS.md.

[Concept sheet](../output/imagegen/canyon-v1/canyon-layout.png). Generated with built-in imagegen; [generation prompt](../output/imagegen/canyon-v1/prompt.txt). Visually reviewed: the overhead panel is an elevated illustrative view rather than a measured orthographic plan, and the section exaggerates depth. Use the written dimensions as proposals, not image measurements.

## Requested direction

V3 follow-up: user requested a pedestrian suspension bridge across the canyon on the right/east side. Add a separate rim-to-rim foot crossing, parallel to and east of the railway bridge, with walking connections at both ends. Exact placement, width and construction remain drawing proposals; a suspension appearance does not imply simulated sway or breakage.

[V3 top-down drawing](../output/imagegen/canyon-v3/canyon-top-down.png) | [Exact built-in imagegen edit prompt](../output/imagegen/canyon-v3/prompt.txt). Visually reviewed: the pedestrian span and both rim connections are shown. The generated revision omits part of the previous south creek-to-rim switchback; retain that continuous descent/recovery route in the eventual measured layout. Bridge span and anchoring positions are illustrative, not construction dimensions. No Unity changes.

V2 follow-up: user liked the initial concept and requested a true top-down view, suggesting a shaft on one side that drops into a cave with a walking exit into the canyon. Incorporate that route in the revised drawing; exact placement, geometry and fall behavior remain proposals. This is not an instruction to start Unity blockout.

- A canyon crossed by a rickety railway bridge.
- Steep walls with paths down into the canyon.
- Bridge scaffolding and a worker walkway underneath the rails, with broken sections.
- A shallow creek along the canyon bottom and cactus in the landscape.
- Possible caves off the main canyon and hanging ropes from the bridge. Reusing Raider Legends' rope mechanic is an idea to investigate, not verified compatibility or an approved import boundary.

## Proposed layout

Use the existing 160 m block length and a provisional 96 m width. Keep entry/exit track level, straight and aligned along +Z. The canyon runs across the block in its middle third, approximately Z=55..105; the train crosses on a fixed trestle. This is proposed as an additional scavenging location, separate from the existing bridge finale and its finish logic.

Proposed rail level is Y=0, worker catwalk approximately Y=-3 m and canyon floor approximately Y=-12 m. These are review dimensions, not measured traversal geometry. The rail deck remains structurally continuous for the train: broken sections belong to the lower worker route. Use worn timber, steel bracing and scaffold platforms for the rickety appearance; collapse or bridge simulation is not proposed.

Two switchback routes, one on each canyon wall, link rim landings with lower ledges and the creek. Reserve broad passing/turning landings and a continuous walking return from every accessible lower area. For a 12 m drop at a provisional 1:10 grade, each route needs at least 120 m of graded run plus landings; the drawing is schematic and detailed fitting is required before building. Ropes must not be the only way to retrieve players or loose supplies.

Place two modest cave pockets off lower ledges, with visible entrances and dry floors above the creek. Their number and depth remain proposals. Keep the creek shallow and crossable with gently graded banks; swimming, currents and water damage are not requested. Define exact water depth and item recovery during blockout.

Worker stairs/access at the bridge ends lead to scaffold landings and the partial catwalk. Broken spans create optional traversal choices, with recovery below; do not force a rope jump for the core scavenging route. Proposed ropes hang outside the train clearance envelope to intermediate platforms and canyon-floor landings. Whether these support climbing, swinging or both depends on inspecting the source mechanic and user review.

Populate approach and departure thirds with cactus, scrub, rock outcrops, maintenance supplies and a small tool shelter. Preserve flat seams, unobstructed railway passage and clear boarding landings. Keep the main canyon spectacle central without leaving the rest of the block empty. Canyon mouths toward lateral edges require deliberate boundary geometry; do not expose a walkable route into the void.

## Historical proposals and remaining acceptance

### V2 shaft route proposal

[V2 overhead plan](../output/imagegen/canyon-v2/canyon-top-down.png) | [Illustrated shaft section study](../output/imagegen/canyon-v2/canyon-shaft-study.png). Both generated with built-in imagegen; exact prompts saved alongside as `top-down-prompt.txt` and `illustrated-prompt.txt`. Visually reviewed: the final map uses flat contours and underground overlays; the earlier study retains oblique cliff faces and is supplemental only. Neither is a measured construction plan. The shaft/chamber connection is schematic: build the landing directly below the opening, with a dry walking exit above the creek waterline, regardless of the diagram's label spacing.

Place an open rock shaft on the northwest rim, away from the railway and the main walking path. A vertical drop reaches a dry cave landing chamber; a short underground passage exits through the canyon-facing wall onto a lower ledge connected to the creek path and return switchbacks. The passage is roofed below solid rim terrain, not an open trench. Draw it as a dashed underground overlay in the overhead plan. Northwest placement is a proposed default, not a user-selected side.

The intended sequence is fall into cave, regain footing, then walk out into the canyon without needing to climb the shaft or use ropes. Dimensions, landing clearance and fall handling must be checked against existing player behavior; no new fall-damage, teleport or respawn mechanic is implied. Keep the landing and exit usable for loose-item retrieval and teammate rescue. Validate shaft collision, cave roof/ground separation, natural hand locomotion, low-eye exit visibility and real-client/headset recovery before acceptance.

### Remaining work

- Include the east pedestrian suspension bridge in the revised layout review. Proposed timber deck, cable handrails and anchored rim landings connect to upper walking routes on both banks. Preserve the secondary cave below and keep all supports outside train clearance. During approved blockout, verify full-span deck collision, landing joins, passing/hand clearance, fall recovery and headset traversal. Dynamic bridge physics are not requested or verified.

- Drawing approval and rough blockout are complete; headset scale and traversal review remain pending.
- Locate and inspect Raider Legends rope source read-only; verify project boundary, dependencies, hand locomotion integration and Photon behavior before importing anything. No source project has been changed or inspected for this proposal.
- Stable block ID, catalogue registration, three spawn variations, counts and offline graph/lighting are installed as recorded above.
- Complete natural hand locomotion, falls/rescue and loose-item recovery, creek traversal and low-eye route readability on headsets and real clients. The editor checks do not close those acceptance items.
- If ropes are adopted, verify natural grab/release, two-hand behavior, multiple users, late joining, authority changes and moving-train separation across real clients and target headsets. Editor-only evidence is insufficient.
- Profile geometry, lighting and any rope simulation on target hardware. No feature acceptance is claimed by the concept sheet.

A caged amber lantern hangs from the center of each creek-facing mine roof lip. The west opening is at X=-27, Z=92 and the east opening at X=14, Z=91. Their warm light is baked into the approaches.

Canyon contact audio uses Sand on the rim, Stone in the caves and cliffs, Dirt on ballast, and Wood, Metal or Foliage on their respective surfaces. Mixed survey props use collider triangles grouped by atlas material. Tent canvas uses the soft Dirt contact. The creek audio follows its visible banks over the full 80 m. Five shallow pools at passage Z52, 63, 77, 89 and 99 use the creek material and matching water footprints, with dry stone between pools.

Edit Mode checked all 659 solid environment colliders, exact triangle coverage on 32 mixed survey meshes, 405 creek shoreline samples, floor contacts in all six water volumes and four dry gaps. Both player sound sets resolve water to HandHit_Water. The Canyon physical layout and graph checks passed.

## Canyon scavenging and ambient audio, November 23

The saved pools contain 68 coal candidates (46 underground), 64 gold candidates (48 underground) and 26 enemy candidates (9 underground). The 42 prototype markers were removed. Six new exterior coal locations cover the survey supplies, cable reel yard, west switchback landing, footbridge approach, winch stores and east cable yard. Three replacement enemy locations cover the survey approach and footbridge. Mine loot follows the under-river passage, its three side pockets, the connecting gallery, both inclines and the shaft landings. Per-run selection uses the existing crew scaling.

A full crawlgraph bake produced 32,761 nodes and 324,738 edges. All nine mine enemy markers have a ground node within 0.8 m connected to the exterior landings. Edit Mode checked 100 seeds for each crew size from one through eight. All 800 selections met the loot counts, enemy quadrant quotas and 15 m enemy spacing. An underground enemy was selected in 600 cases. All saved markers retain their nested prefab references.

The camp and creek have localized insect and water ambience. The trestle, headframe and winch mine have intermittent creaks, with drip sources under the river and in the eastern gallery. Entering the southwest shaft canopy plays the Farm corn rustle once for the local listener. The cover remains pass-through. The saved audio references and canopy bounds passed Edit Mode checks. Headset listening, pickup reach and live encounters are queued for playtest.

## Canyon terrain triangle count, November 23

The original terrain used 35,288 triangles. A first pass merged coplanar 2 m patches and reached 16,052. The coarser pass fits rectangular faces up to 16 m across the rim and up to 8 m along cliffs, mine floors and ceilings. Each face stays within its chunk, material and opening boundaries. Short faces follow grade changes and rock edges.

The saved terrain uses 136 rim triangles, 3,118 cliff triangles and 1,400 cave triangles in each of its render and collision representations, totaling 4,654. A comparison of 74,551 half-metre grid samples against the first pass found matching surface coverage and a maximum height difference of 1.8 cm.

The lighting rebake bound all 95 terrain renderers. The 32,753-node crawlgraph connects the canyon floor to all nine underground encounter locations. The Edit Mode suite recorded 4,439 passes and one separate capacity failure. The revised eight-player coal target is 93 and the Canyon has 68 coal locations. Headset traversal and frame timing are queued for playtest.

The optimized rim initially disappeared in Unity's shaded view despite valid collision and CPU triangle data. Rebuilding its draw buffers through the Mesh API restored all 44 chunks. The layout check now renders the rim from above and samples the center of all 136 triangles against a contrasting background. It passed after reimport and scene reload. Reopening the prefab also confirmed a visible ground pixel that returned to the background color when its renderer was hidden.
