# Maintenance Depot — prototype study

November 2, 2026. The user selected Maintenance Depot and requested rough, lower-resolution renders plus a labeled top-down view before greyboxing, followed by a first art pass after the greybox. The V3 blueprint revision is now approved and installed for further VR review.

## V3 greybox implementation

### Rough material previews — November 4

The user subsequently approved this direction for a basic material pass while deferring further model work. `RailDepotMaterials` supplies **Duck Rails > Authoring > Depot > Apply rough material pass**, targeting only the existing depot surfaces. It uses unchanged Town/Industrial atlases with URP Lit materials, metre-scaled atlas UVs, faded olive/plaster wall bands, localized exposed-brick patches, concrete floors, oxidized metal, dark roofing, timber crates/pallets and yellow stair-edge markings. Surface mesh subdivisions carry material changes; silhouettes and colliders remain the existing greybox. The original cube's lightmap UV charts are retained. New materials/UV meshes live in `Assets/DuckRails/DepotMaterials`; there is no new texture atlas or runtime material-generation code.

The pass preserves current light placement and intensity, then rebakes/re-exports the depot lighting for the new surface response. It does not retune the global night/fog atmosphere. General Week 3 generation preserves the authored depot; explicitly reinstalling the greybox resets its appearance, so reapply the material pass afterward if that reset is intended. Further prop modeling and final lighting remain separate work.

Implementation uses nine shared material families across 446 surface renderers and 210 shared UV meshes. TextMeshPro room labels and lamp lenses retain their own materials. Atlas files/import settings are unchanged. Surface subdivisions add vertices (188,592 across mapped instances), so target-headset profiling and later mesh optimization are still required; there is no Quest performance acceptance from this pass.

Validation: compilation, atlas UV bounds (including the rectangular Industrial atlas), complete lightmap UVs, unchanged collision/track, 268 depot geometry checks, 607 Week 3 checks and lighting export checks passed. Reapplication updates both CPU and GPU mesh buffers; the first captures exposed a stale-buffer issue and an atlas-aspect mistake, both corrected before delivery. [Evidence](validation/depot-materials/checks.txt). [Actual Unity exterior](../output/depot-materials/exterior.png), [interior](../output/depot-materials/interior.png), [overview](../output/depot-materials/overview.png) use temporary directional review fill removed after capture; they are not final gameplay darkness previews. The rough surfaces follow the approved palette/material direction; exact wear placement differs from the generated concepts. No new headset or multiplayer claim is made.

The user requested renders for review before building the rough material pass. Built-in image generation produced [exterior](../output/imagegen/depot-material-study-v1/exterior.png) and [interior](../output/imagegen/depot-material-study-v1/interior.png) concept paintovers of the user's actual greybox screenshots. Both use the existing Quackville Town atlas as primary reference and the existing Industrial atlas as a supplemental reference. Direction: gritty pixelated stained plaster, faded olive lower wall paint, exposed brick patches, rusted dark steel, worn concrete and restrained oil stains. Lighting remains readable for material review; near-pitch-dark gameplay tuning is separate.

These were generated material concepts, not implemented Unity screenshots or proof of exact atlas mapping. No Unity assets were changed during that preview stage. The user then approved the implementation recorded above. [Prompt/reference notes](../output/imagegen/depot-material-study-v1/prompt-notes.txt).

The user approved the drawings with “That looks awesome. let's grey box it.” The actual `Assets/DuckRails/Blocks/maintenance-depot-v1.prefab` now contains `Depot greybox v3`. The track curve, 96 x 160 m boundary, stop, entry/exit and block ID are retained.

- Parts store: 26.4 x 21.6 m, roof 7.2 m, full upper floor at 3.2 m. Bulk stock, returns and picking hall below; archive, office, staff room, sorting and overflow stock above.
- Workshop: 19.2 x 21.6 m, roof 6.8 m, gallery at 3.2 m. Tool cage, office and repair benches below; supervisor room and continuous gallery around a 7 x 5.6 m opening above.
- Repair shed: 24 x 36 m, roof 8.4 m, mezzanine at 4 m around a 14 x 14 m opening. Dispatch, tool and compressor rooms below; spares, control, shift office and break room above.
- Two stairs per building: 2.4 m nominal shaft width, 6 m runs in the western buildings and 7.2 m runs in the shed. Physical risers are at most 18 cm; shafts have actual floor openings and 1.1 m solid greybox guards. Main interior doors are 2.4 m wide and 2.6 m high. The shed bay retains its 8 m width and 4 m height. Exterior wall thickness is contained within each footprint.
- Props are static collision blocks for tables, racks, machinery and repair stands. They add routes and occlusion without introducing new interactable systems. Floor labels identify major rooms.
- Service passage moves to Z=53..57. The shed's north doorway connects to a new 4 m gap in the yard's south fence. Existing western loading space and track-facing facades remain in place.

Coal and gold marker identities, counts and three variations are preserved. Some markers now occupy upper floors to motivate traversal; coal remains three west/three east and at least eight metres apart. Enemy navigation stays east of the tracks. Its authored bounds expand to cover the larger shed and upper deck while preserving the encounter's Z=45..115 limits. Navigation was rebaked offline. Both shed stair landings and the west gallery are reachable through ground-only graph links from outside.

Lighting is a rough visibility pass: six site fixtures plus 33 baked interior fixtures, with no additional realtime lights. Updated light probes and 685 static receiver bindings were exported to the existing additive lighting companion. Roofs remain installed in gameplay; cutaway captures temporarily hide them and add review fill without saving those changes.

Validation: Unity compilation, 268 physical geometry checks (stair capsule support, 1.95 m stair headroom, blueprint shell dimensions, shed mezzanine connectivity and doorway/outdoor connector clearance), all 607 Week 3 checks, and catalogue lighting export checks passed. These are editor checks, not worn-headset or multiplayer acceptance. The diagnostic backend returned ten existing open reports after retrying outside the network-restricted sandbox; none was resolved by this work.

Evidence: [geometry](validation/depot-greybox/v3/geometry-checks.txt), [block checks](validation/depot-greybox/v3/week3-checks.txt), [lighting](validation/depot-greybox/v3/lighting-checks.txt), [Unity cutaway](../output/depot-greybox/v3/overview-cutaway.png). Per-floor Unity captures are in `output/depot-greybox/v3/`. Run **Duck Rails > Authoring > Depot > Check two-floor greybox** for the physical/editor checks. Use **Load depot-first review route**, then a private development expedition, for headset review. **Install scaled greybox (resets depot layout)** regenerates V3 and overwrites manual changes under its owned subtree; the general Week 3 builder preserves the authored depot.

An authenticated single-editor private expedition also reached Running/WorldReady with V3 first at world Z=150, all six stairs present and native route lighting ready. No Console errors were reported. [Live evidence](validation/depot-greybox/v3/live-check.txt), [live shed approach](../output/depot-greybox/v3/live-shed-approach.png). The sandbox was stopped and the previous route preset and Login scene restored afterward.

Next acceptance: walk both stair routes and every room in VR, review duck-eye sightlines and prop spacing, and assess lighting/performance on the target headset. The first art pass has not started.

## Installed greybox — November 2

### November 3 review: v3 drawings only

Historical drawing stage: after testing the greybox, the user requested buildings 20% larger, a taller repair shed, a parts store extended farther west away from the tracks, and two-floor blueprint studies with rooms and props. The user explicitly selected **drawings first for everything**. No Unity changes were made during that drawing stage. Subsequent approval authorized the V3 implementation above.

Interpretation for review: 20% means each horizontal dimension is multiplied by 1.2 (44% larger footprint area), rather than only 20% more area. The parts store gets a further 7.2 m of rear depth beyond that baseline. All v3 measurements, rooms, stairs and props are proposals awaiting review.

- [Revised scaled site plan](../output/imagegen/depot-floorplans-v3/site-plan-v3.png): retain 96 x 160 m block and verified track curve. Near-track facades stay at X=-16/+16.
- [Parts store — floors 1 and 2](../output/imagegen/depot-floorplans-v3/parts-store-floors-v3.png): 26.4 x 21.6 m, X=-42.4..-16, Z=90.2..111.8. A rear bulk-stock/returns wing, staggered picking racks and counter downstairs; archive, office, staff room and overflow sorting upstairs. Two aligned stair flights, upper floor +3.2 m; proposed roof 7.2 m.
- [Workshop — floors 1 and 2](../output/imagegen/depot-floorplans-v3/workshop-floors-v3.png): 19.2 x 21.6 m, X=-35.2..-16, Z=54.2..75.8. Tool cage, office, benches and machine below; supervisor room, landing and inspection gallery above a central void. Two corner stair flights, upper gallery +3.2 m; proposed roof 6.8 m.
- [Repair shed — floors 1 and 2](../output/imagegen/depot-floorplans-v3/repair-shed-floors-v3.png): 24 x 36 m, X=16..40, Z=59..95. Tall repair bay, dispatch room, tools and compressor room downstairs; spare parts, control room, shift office and break room around a mezzanine above. West bay, south exit and proposed north yard exit. Mezzanine +4.0 m, roof 8.4 m (6 m x 1.2, plus 1.2 m extra height).

Space implications: the parts-store rear leaves 5.6 m to the western block boundary; the shed leaves 8 m to the east. Yard remains X=16..36, Z=98..114, so its gap from the shed decreases to 3 m. Shift service passage south to Z=53..57 and extend it to X=16..40. Loading remains X=-32..-16, Z=78..88; it has 2.2 m separation from each enlarged western building. The 45 m approach buffers are preserved.

Blueprint conventions: north/+Z is up; each drawing uses local coordinates from the southwest corner of its building and identical scale on both axes. Floor 1 is ground level. Cyan thresholds are open doorways, filled rectangles are prop footprints, and hatching means no upper floor. Stair A/B positions match across levels; 2.4 m stair widths, 6 m runs for store/workshop and 7.2 m runs for the shed reserve approximately 2.4 m clear landings. Main routes/doors target 2.4 m clear, secondary aisles 1.8 m. These are nominal design dimensions; wall thickness, headroom, stair/hand locomotion, prop collision and upper-floor enemy navigation require future greybox validation. Guard all open gallery edges. Rooms remain connected and no mandatory jumping is proposed. Props are layout placeholders, not new interactable systems or changes to loot quantities.

The small buildings need taller shells than a uniform 20% height increase to support two usable levels; 7.2/6.8 m are explicit proposals. The original single-level design constraint is superseded for these requested drawings, not yet for Unity implementation.

Sources: measured v2 prefab/track recorded above, user's annotated footprint reference and VR feedback. Editable diagram generators: [floor plans](../output/imagegen/draw-depot-floorplans-v3.ps1), [site plan](../output/imagegen/draw-depot-site-v3.ps1). Drawings were rendered and visually checked; no gameplay or headset acceptance is claimed.

Historical V2 implementation: the user authorized the scaled v2 layout on the actual block prefab for VR scale/distance review. `Assets/DuckRails/Blocks/maintenance-depot-v1.prefab` then contained `Depot greybox v2`, replacing the earlier placeholder buildings. The 96 x 160 m ground, 81 track samples, 4 m curve, entry/exit, stop, stable block ID and three spawn variations were retained.

The three shells use the exact v2 footprints below. Initial heights are 4 m for the store/workshop and 6 m for the repair shed. Store/workshop entrances are 6 m wide and 3 m high; the shed bay is 8 m wide and 4 m high, with a 4 m wide, 3 m high south exit. These vertical dimensions are greybox tuning, not headset-approved measurements. Thin floor/path markings have no colliders or raised thresholds. Basic 1.4 m post-and-rail fences outline the yard, loading space and service passage, with a 6 m yard gate.

Six simple baked lights occupy the diagram's fixture areas. L4's pole moves from Z=106 to Z=108 to clear the walking line. No realtime environment lights were added. The offline crawlgraph and block lighting/probe companion were rebaked; existing coal/enemy/gold marker references and quantities were preserved but positions moved clear of the new walls. Enemy navigation remains east of the railway. There are no detailed props or first-art-pass meshes yet.

Authoring and review:

- Open the prefab normally, or its existing `Assets/DuckRails/Lighting/Blocks/maintenance-depot-v1/maintenance-depot-v1-bake.unity` scene for lighting-aware edits. Do not press Play directly in the isolated bake scene; launch normal gameplay from Compound/Login.
- **Duck Rails > Authoring > Depot > Load depot-first review route** sets the next development private expedition to visit the depot first. Then use **Duck Rails > Test Center > Manual sandbox** or a normal development private run. The depot stop is about 160 m of train travel from the starting position. For headset review, use the established shared rig/Quest Link flow.
- **Test Center > Restore normal random routes** clears the saved review preset. The menu affects private development routes only, not normal public departures.
- The full Week 3 builder now preserves an installed authored depot. **Install scaled greybox (resets depot layout)** intentionally reapplies this layout and queues a bake; do not use it after manual edits unless resetting them is intended. Other marker-placement utilities still overwrite manual spawn placement.

Validation: Unity compilation, 607 Week 3 checks, footprint/door/path-clearance checks and catalogue lighting export checks passed. The depot exports 310 static renderer bindings. A real authenticated solo private expedition reached Running/WorldReady with the depot first at world Z=150, all route lighting ready, the new geometry present and the final yard path clear. [Live check](validation/depot-greybox/live-check.txt), [live view from the track](../output/depot-greybox/live-track-eye.png). The temporary sandbox was stopped and the prior route preset restored afterward. Startup logged the known XR form-factor-unavailable error while no headset was in use; gameplay subsequently loaded successfully. Diagnostic backend access succeeded after retrying outside the network-restricted sandbox and returned ten open reports; none was resolved. VR scale, actual hand locomotion feel, multiplayer encounter balance and target-device performance remain user/playtest acceptance.

Evidence: [block checks](validation/depot-greybox/week3-checks.txt), [geometry checks](validation/depot-greybox/geometry-checks.txt), [lighting checks](validation/depot-greybox/lighting-checks.txt). [Unity overview](../output/depot-greybox/overview.png) and [top view](../output/depot-greybox/top.png) use a temporary directional review fill solely for the captures; it is not saved in the prefab or scene. [Track-eye view](../output/depot-greybox/track-eye.png) shows the isolated bake lighting without Expedition's moonlight.

## Proposed contents

### Scaled plan v2 — current proposal

The user clarified that existing prefab buildings can be ignored; retain the actual block size and track curve while designing the rest together. [Scaled site plan](../output/imagegen/maintenance-depot-scaled-plan-v2.png) supersedes the earlier unscaled diagram. [Reproducible drawing script](../output/imagegen/draw-depot-scaled-plan.ps1).

Unity MCP verified `Application.dataPath` is `X:/Work/duck-rails/Assets`, then read `maintenance-depot-v1.prefab` without scene changes. Ground is 96 x 160 m (X -48..48, Z 0..160). Entry/exit are (0,0)/(0,160). Track samples follow X=-4*sin²(pi*Z/160), every 2 m; the suggested stop is (-4,80). The diagram uses the smooth formula to depict that sampled curve. Encounter Z 45..115 preserves 45 m buffers at both ends. Diagram north means +Z, not geographic north.

All building coordinates below are new proposals, expressed as X/Z footprint extents:

- Parts store: X -32..-16, Z 92..110; 16 x 18 m.
- Workshop: X -32..-16, Z 56..74; 16 x 18 m.
- Repair shed: X 16..36, Z 62..92; 20 x 30 m, west-facing bay and south side exit.
- Work yard: X 16..36, Z 98..114; 20 x 16 m, with 6 m between yard and shed.
- Loading space: X -32..-16, Z 78..88; 16 x 10 m.
- Service passage: X 16..36, Z 56..60; 4 m wide, connected to the shed exit.

Walking paths are proposed at 3 m wide. The pale corridor extends 5 m either side of track center; it is reserved clear space, not a measured train footprint. The stop is a point marker, not the train's length. Final boarding, crossings and travel distances require greybox review.

Six proposed baked fixtures are shown as point symbols, not illumination coverage: L1 (-17,101) parts-store entrance; L2 (-17,65) workshop entrance; L3 (16,80) shed bay; L4 (18,106) work-yard entrance; L5 (30,76) shed interior; L6 (26,62) side exit. Heights, ranges, intensities and probe placement remain for the lighting pass. Existing four sample fixtures do not constrain this design.

No existing building footprint, interior prop or enemy-bound shape is a design constraint beyond the retained encounter spacing. Rebuild the offline graph and reconcile enemy bounds to the accepted layout during greyboxing. This diagram does not change Unity assets or claim navigation/lighting validation.

Track runs south to north, with the train facing north. Left/right mean west/east on the overhead study.

1. **Train stop / boarding corridor:** central return landmark; keep both sides and rear usable, with no scenery blocking boarding.
2. **Repair shed:** east of the track, large west-facing bay, stepped roof, sparse repair stands and shelving. A second exit on the south wall connects to an outside service passage. Keep a loop around equipment for escape and rescue.
3. **Work yard:** north of the repair shed, fenced with broad openings toward the track, sparse pallets and cable reels.
4. **Service passage:** along the shed's south side, reconnecting its second exit to the forecourt without a dead end.
5. **Parts store:** northwest of the train, shelves and a broad track-facing entrance. This is scenery/scavenging space, not a new functioning equipment shop.
6. **Small workshop:** southwest of the train, workbench and repair supplies, broad track-facing entrance.
7. **Loading space:** open ground between the two western buildings, a third western scavenging area.

Illustrative pickup distribution: three coal points west (parts store, workshop, loading space) and three east (shed, yard, service passage). These illustrate one variation only; retain six chunks per variation, three variations, and existing fuel values. Final marker positions must respect spacing, collision and gameplay navigation. No additional loot type or reward tuning is authorized by this study.

## Layout boundaries for greyboxing

- Retain the existing 160 m block, straight entry/exit anchors and translation-only placement. Concentrate the encounter in local Z 45–115 to preserve travel spacing.
- Superseded by the user's v2 clarification: existing building footprints and east enemy bounds do not constrain the new layout. Retain the block boundary, track curve and approach spacing; reconcile navigation to the accepted design.
- Preserve the clear track corridor. Enemy routes remain local and off the train. The current western buildings have no enemy patrol; adding western patrol is a separate gameplay decision.
- V3 supersedes the original single-level constraint with two floors and guarded stairs/galleries. Retain flat exterior thresholds and review openings, sightlines and hand clearance at the duck's low eye line.
- Bake navigation offline after geometry changes; no runtime baking.
- Preserve manual art edits against builder regeneration. The full Week 3 builder now preserves the authored depot subtree.

## Review and implementation sequence

1. Review the rough arrival, interior and labeled overhead images for contents, layout and atmosphere. Generated perspectives are illustrative and may vary; this written layout is the intended relationship between areas.
2. Once the layout is settled, greyblock it in Unity with simple geometry and inspect approach, scavenging loop, exits, rescue routes and boarding. Check diagnostics before gameplay testing and confirm MCP targets Duck Rails.
3. Validate scale and navigation in the shared rig, then with real clients and target headsets. A screenshot is not gameplay acceptance.
4. After the greybox is accepted, make the first art pass: modular walls/roof, pixel textures, sparse industrial props and localized lighting. Recheck navigation and profile lighting on the target headset.

The original concept deliverable did not change Unity assets. The installed greybox above supersedes that historical state; headset scale and art acceptance remain pending.

## Concept files

- [Labeled top-down plan](../output/imagegen/maintenance-depot-layout-plan-v1.png) — deterministic diagram, not to scale. Two generated overhead attempts failed with network errors; this plan supplies the requested location labels and pickup/escape layout.
- [Arrival overview](../output/imagegen/maintenance-depot-arrival-rough-v1.png)
- [Low-eye interior](../output/imagegen/maintenance-depot-interior-rough-v1.png)
- [Generation prompts](../output/imagegen/maintenance-depot-prototype-v1-prompts.json) — built-in image generation.
- [Overhead retry prompt](../output/imagegen/maintenance-depot-overhead-v1-retry-prompt.json) — failed network request, no rendered overhead output.

The arrival image depicts an illustrative train and coal bins; these do not replace the approved train or prescribe pickup placement. The written distribution above governs the proposed six pickup areas. Review images for massing and mood, not exact measurements or matching projections from a shared model.
