# Canyon — approved layout and blockout

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
