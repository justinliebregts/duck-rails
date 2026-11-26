# Nuclear power plant — layout and implementation

## November 25 — spiral ramp loot and plant acoustics

Each of the four turns inside each cooling tower gained three coal and two gold candidates. The saved test-only block totals 137 coal, 89 gold and 33 enemy candidates. All 7,523 plant checks passed, including support and clearance and 100 seeds for every crew size. [Current spawn map](../output/nuclear-full-block/spawn-map.png)

Seven box reverb volumes cover both towers, the turbine hall, containment, receiving office, spill workshop and pump hut. Cooling towers have the longest decay at 2.3 seconds. The outdoor railway stays dry. Eleven listener areas select machinery, wind, electrical hum, leaks and water beds with occasional directional creaks and drips. Edit Mode verified lookup and playable clip references. Headset mix and transitions are queued for playtest.

## November 25 — scavenging and enemy placement

Quackwatt is ready for a headset playtest in B through Test Block Order. Its prefab holds 113 coal, 73 gold and 33 enemy candidates. The existing marker instances were reused. Crowded variation markers were distributed around the plant, including repeated gold positions. The three rail patrol locations remain available.

Coal follows the coach stop, delivery racks, barrel yard, offices, equipment and service walks. Gold includes accessible interior finds and climb rewards in the turbine gallery, on the roof and along both cooling tower ramps up to their lookouts. The pump hut, settling pond banks, transformer apron and flume landing extend the search into the northern third.

The enemy pools contain 6 southwest, 7 southeast, 10 northwest and 10 northeast candidates. Across 100 seeds for each crew size from one to eight, the saved prefab filled each resource budget and selected 4, 8 or 12 enemies with equal quadrant quotas and at least 15 m horizontal separation. The B checkout selects 31 coal and 52 gold for eight players. The coal pool also exceeds the 93-candidate project target.

The full crawlgraph bake produced 40,467 nodes and 377,148 edges. All 7,323 Edit Mode checks passed, including supported marker footprints, enemy proximity to baked ground and graph connectivity to the tower lookouts and turbine hall roof. All 529 prefab lightmap bindings passed export verification. Headset reach, runtime pursuit and multiplayer pickup are queued for playtest.

[Spawn map](../output/nuclear-full-block/spawn-map.png) · [Placement and selector checks](validation/nuclear-full-block/geometry-checks.txt) · [Baked asset checks](../output/nuclear-full-block/editor-checks.txt)

## November 25 — waste and water lighting

Three green baked point lights illuminate the pavement and barrels around the waste spills. Four blue-aqua points light the settling pond and four turquoise points light the stepped discharge flume. Unity confirmed all eleven points contributed to the bake and all 529 exported prefab lightmap bindings were valid.

[Waste yard at night](../output/nuclear-full-block/waste-green-night.png) · [Both water areas at night](../output/nuclear-full-block/ponds-aqua-night.png)

## November 25 — control gallery and facade lighting

Ten bracket-mounted fixtures line the four interior walls. Only two work, one on the west gallery wall and one near the north end of the east turbine wall. The other eight have black, non-emissive lenses and no light components. The six suspended lamp blocks and the floating CONTROL sign assembly were removed. Three warm spotlights under the track-facing nameboard canopy aim down the facade and doorway. The eight tripod work lights stand on level paving outside the two tower ramp approaches.

The refreshed navigation and lighting exports passed 3,916 Edit Mode checks, including wall mount positions, spotlight directions, tripod clearance and support at each foot. Review glare at headset eye height and the facade from the moving train during the queued playtest.

[Gallery at night](../output/nuclear-full-block/control-gallery-lighting.png) · [Facade at night](../output/nuclear-full-block/turbine-facade-night.png) · [West ramp entrance](../output/nuclear-full-block/west-tripod-clearance.png) · [East ramp entrance](../output/nuclear-full-block/east-tripod-clearance.png)

## November 25 — dry towers, four turbines and the pond pump

Both spiral decks keep their four turns, with rectangular rails and horizontal brackets seated inside the shell. The undercroft uses grounded supports where the shell is open. Removing diagonal braces and simplifying the rails reduced each ramp group from 15,428 to 7,112 triangles. Both cooling towers have dry floors and clear entrances. The old floating tower signs, light blocks and overhead fill assembly are removed.

Four matching turbines sit in two rows with a through aisle to the gallery stairs. The north pair clears the existing enemy spawn. The gallery railing sits inside the deck edge, including its end posts. The west-side floating pipe run is gone. The southwest parking paint sits on the asphalt, the truck bed meets its chassis, and the warning beacon seats on the reactor crown.

The pump hut has a solid pond-facing wall. A coupled electric motor and pump share a skid, with flanged suction and discharge pipes visible from the entrance. Four mounted floodlights aim across the settling pond.

The B editor passed 3,876 Edit Mode checks. The lightmaps, runtime lighting companion and crawlgraph were rebuilt. The tower routes, roof access, gallery post footprints, parking paint and retained spawn clearances passed. Headset traversal and moving-view lighting are queued for playtest.

[Pump hut](../output/nuclear-full-block/pump-hut.png) · [Pond lighting](../output/nuclear-full-block/pond-corners.png) · [Turbine hall](../output/nuclear-full-block/four-turbines.png) · [West tower floor](../output/nuclear-full-block/west-tower-floor.png) · [Edit Mode evidence](../output/nuclear-full-block/editor-checks.txt)

## November 25 — yard lighting and cooling tower entrances

The plant's original 148 m paths had square cube-face lightmap islands, and the thin pavement boxes overlapped the ground. The exposed paving uses world-proportioned planar UVs, with distinct material draw ranges for concrete, paths and aggregate on one receiver. The bus pad and the two transformer apron sections occupy cutouts. Concrete covers the service yard, with aggregate reserved for the ballast and designated work aprons. The turbine hall roof also has a continuous upper surface around the stair opening. Its landing meets the roof and last tread edge to edge.

Three pole-mounted floods illuminate the northeast transformer yard. Wall-mounted lights serve the bus lot and workshop entrance. A separate warm floodlight on a mast lights the loading apron and drums. Thirty-six striped bollards mark track verges and service walk edges, with openings at crossings. The waste yard has 65 independently oriented drums. The truck has four octagonal rubber tires, a cab seated on the chassis and sideboards seated on the deck.

Both tower entrances are clear of the floating transverse manifolds. The last overhead bridge now sits at Z=95 m, where its capped headers finish at the downcomer fittings. The lower collared supply pipes terminate at Z=90 m above their last support.

The B editor passed 3,835 Edit Mode checks. The nighttime comparison uses the same camera and the plant’s baked illumination with distance fog hidden for visibility. The lighting companion and authored navigation graph were exported. Moving train and headset inspection are queued for playtest.

[Before lighting repair](../output/nuclear-full-block/night-before.png) · [After lighting repair](../output/nuclear-full-block/night-after.png) · [Transformer yard](../output/nuclear-full-block/transformer-floodlights.png) · [Truck and drums](../output/nuclear-full-block/waste-yard.png) · [Roof landing](../output/nuclear-full-block/roof-landing-night.png) · [Edit Mode and bake evidence](../output/nuclear-full-block/editor-checks.txt)

## Track-facing sign and northern flume — November 25

The turbine hall nameboard, hood and its three lamps now face the train above the east entrance. Both original ground-to-gallery stair flights have their four sloped handrails removed. The gallery edge remains guarded. The roof opening is enclosed by a metal stair headhouse with a sloping overhanging roof and a clear south-facing exit.

Seventy-eight faceted cast fittings join the overhead crossings, hall and annex branches, and pool pipework. The straight tubes stop inside the fittings. The three turbine hall lines have separate centerlines and risers. The perimeter fence uses the compound’s double-sided alpha-cutout material at one texture repeat per metre. Six two-triangle panels span the two sides between concrete walls, with separate collision.

The east flume has full-depth stepped concrete sidewalls, a smooth ramp from grade to +4.12 m, and a rear deck spanning 26.74 m. The incline, landing and platform have distinct walking surfaces. The eight yard posts and four streetlamps are rooted in the verge. The west pond ramp and east flume ramp are clear.

The B editor passed 3,741 Edit Mode checks, including ramp and deck support and headroom, headhouse coverage and exit clearance, sign orientation, perimeter collision, lamp positions, and the previous tower and reactor routes. Navigation and lighting were rebaked. Review perceived pipe or water shimmer while moving, sign readability from the train, fence aliasing and headset traversal during the queued playtest.

[Track-facing sign](../output/nuclear-full-block/hall-facade.png) · [Pipe fittings](../output/nuclear-full-block/pipe-joints.png) · [Flume ramp and deck](../output/nuclear-full-block/flume-ramp.png) · [Roof headhouse](../output/nuclear-full-block/roof-headhouse.png) · [Saved bake evidence](../output/nuclear-full-block/editor-checks.txt)

## Playable towers and refueling hall — November 25 feedback pass

Enter each cooling tower at grade. A 2.23 m wide maintenance ramp follows the shell for nearly four turns, with level rest sections and a track-facing lookout at 35.5 m. The center remains open. Six baked blue-white uplights surround each shell. The west turbine gallery now has a 30-step roof stair and a cut-through landing. Its solid south facade has concrete pilasters and an illuminated **TURBINE HALL** sign.

The containment dome moved 4 m east. The west entrance opens into a refueling hall with a blue-lit pool, a walk-around metal deck, steps to the crane controls, an overhead service crane and connected pipework. The pipe headers beside the tracks clear the shell and the southern feed pipes still enter it.

The gatehouse records a **02:10** drum delivery with departure left blank. The maintenance shed has a drum repair bench, tool cart and open spill cabinet. The gatehouse, maintenance shed and service annex have open-air windows. Three grounded pipe cradles carry nested rows of hollow pipe. The yard has 65 textured drums, including the six tipped drums, with varied stack heights. The waste-storage placard is mounted on posts, the entrance sign has rear supports, the shift bus roof meets its window frames and the pump hut sign fits the doorway.

Both long edges use concrete walls and the production motel chain-link mesh on the project's 1 m side setback. The tree geometry has been removed. The northeast crossing has a separate walking surface between two gravel apron pieces.

The B editor passed **3,174 Edit Mode checks**. They cover support and headroom at 320 ramp positions, foot-to-lookout graph connectivity, every roof stair tread, gallery-to-roof connectivity, pool circulation, crane access, open windows, side barriers, retained markers and railway clearance. Baked lighting and probe export details are in [the export record](../output/nuclear-full-block/editor-checks.txt). The previous section below describes the first November 25 layout. Headset comfort and multiplayer appearance are queued for playtest.

[Whole site](../output/nuclear-full-block/whole-block.png) · [Tower interior](../output/nuclear-full-block/tower-descent.png) · [Refueling hall](../output/nuclear-full-block/refueling-hall.png) · [Gatehouse](../output/nuclear-full-block/gatehouse.png) · [Workshop](../output/nuclear-full-block/maintenance.png) · [Waste yard](../output/nuclear-full-block/waste-yard.png) · [Geometry results](validation/nuclear-full-block/geometry-checks.txt)

## Quackwatt Nuclear Generating Station — November 25

The south entrance faces a 17 m station sign. The shift bus and gatehouse sit west of the tracks, opposite the delivery racks, truck and waste yard. Concrete covers the working areas and gravel covers the remaining ground. Three overhead pipe bridges cross the railway at Z 48, 81 and 98 m. The turbine hall and its playable gallery stand west, opposite the containment dome and service annex. The damp and drained cooling towers face one another at X −26 and +26 m, Z 103 m. At the north end a maintenance ramp reaches the raised settling pond on the west, and steps climb beside the four-stage discharge flume on the east.

All 130 drums share one eight-sided, 60-triangle mesh and a texture with painted hoops and radiation markings. The previous barrel body alone used 576 triangles, with five more mesh renderers per drum for the markings. Five irregular green emissive puddles sit around the spilled drums. They are static scenery.

[Whole block](../output/nuclear-full-block/whole-block.png) · [North-up plan](../output/nuclear-full-block/blueprint.png) · [Entrance sign](../output/nuclear-full-block/entrance.png) · [Waste yard](../output/nuclear-full-block/waste-yard.png) · [Northern waterworks](../output/nuclear-full-block/north-waterworks.png). These are Edit Mode review captures with an inspection light.

The B editor passed 2,587 geometry, clearance, marker, navigation and UV checks. The exported prefab has 722 valid receiver bindings across three atlases, 589 baked probes and a 37,477-node crawlgraph. The existing 18 coal, 6 gold and 18 enemy markers followed the revised structures. Crew-size capacity is outside this layout review. Headset traversal, in-game lighting and Quest performance are queued for playtest. [Edit Mode results](validation/nuclear-full-block/geometry-checks.txt) · [Export evidence](../output/nuclear-full-block/editor-checks.txt)

Open the nuclear bake scene and use **Duck Rails → Authoring → Nuclear Plant → Check open full site** or **Capture open full site**. The editable model source is [build_site.py](../ArtSource/NuclearFullBlock/build_site.py). The November 5 geometry and acceptance figures below describe the previous layout.

## Full plant site — November 23

The current 96 × 160 m expedition prefab follows the finished render. The two hollow cooling towers stand east of the tracks. The turbine hall and sealed containment stand west. This supersedes the orientation shown in the November 5 proposal.

- **South approach.** The employee bus, fleet car and striped turnaround face four pipe delivery racks across the railway. The gatehouse, maintenance shed, delivery truck and 130 stacked or spilled drums carry the approach into the plant.
- **Middle.** The tower manifolds, collared supply pipes, shell bands and portable lamps frame both 22 m cooling towers. The damp and drained interiors retain their crosswalks and undercroft entrances. The hall keeps its 28 × 36 m floor, two stair flights and control gallery. Clerestory windows, roof equipment and CutePixel signs identify its entrances.
- **North departure.** A reed-lined settling pond and pump hut sit west of the line. Four transformer banks and a stepped discharge flume finish the east side. Faceted pines, verge planting and baked lamps continue toward the seams.

[North-up blueprint](../output/nuclear-full-block/blueprint.png) · [Whole-block Unity render](../output/nuclear-full-block/whole-block.png) · [South approach](../output/nuclear-full-block/south-arrival.png) · [North waterworks](../output/nuclear-full-block/north-waterworks.png). The Unity captures use the baked prefab with fog disabled and a temporary directional review light.

The finished site reuses the original playable structures, colliders and spawn marker identities. The gallery, tower markers and corresponding probes moved with the mirrored structures. The prefab contains 18 coal, 6 gold and 18 enemy candidate markers. The runtime determines the population for each expedition.

**Edit Mode evidence.** The freshly baked crawlgraph has 33,392 nodes and one connected component. All 3,210 geometry, navigation and UV checks passed, including the approach lanes, tower entrances, hall circulation, rails and seams. The production prefab has 41 baked fixtures, 313 probes and 1,353 valid receiver bindings across three atlases. All 56 new site receivers have lightmap bindings. The companion remains enabled in the build. The main Duck Rails editor loaded the delivered prefab, graph, markers and bindings. Headset traversal and multiplayer route appearance are queued for playtest. [Geometry results](validation/nuclear-full-block/geometry-checks.txt) · [Lighting and graph summary](../output/nuclear-full-block/editor-checks.txt)

The prefab is `Assets/DuckRails/Blocks/nuclear-power-plant-v1.prefab`. Its mesh, materials and original Quackville texture blends live in `Assets/DuckRails/NuclearPlant/FullBlock`. [Blender source](../ArtSource/NuclearFullBlock/build_site.py) generates the FBX, surface textures and fixture manifest. [Plan source](../ArtSource/NuclearFullBlock/draw_plan.ps1) draws the measured site diagram. To recapture the installed prefab, open the nuclear bake scene by itself and choose **Duck Rails → Authoring → Nuclear Plant → Capture open full site**.

## Installed rough blockout — November 5

User approved V2 for 3D and requested closer barrel spacing and stacked drums. Installed `nuclear-power-plant-v1` as a new sixteenth scavenging location, with repeatable authoring in `Assets/Editor/RailNuclearPlant.cs`. Existing prototype rebuilds preserve this additional block. V1/V2 proposal sections below are historical design context; this section records actual implementation.

- Two 22 m-diameter, 35 m-high hollow cooling shells, open undercrofts, designated east/west approaches, internal supports, crosswalks and perimeter circulation. Tower A has shallow water dressing and partial overhead fill; Tower B has a dry floor and fallen panels. Both have a clear central view up through the shell. The shell bases are open between supports, not enclosed rooms with only two door holes.
- A 28 x 36 m turbine hall with two machinery units, opposite ground-level doors, an 8 m-wide gallery at +3 m, two open stairwells and 3 m-wide stairs, green console displays and workbench. Reactor containment is an exterior landmark with a sealed base.
- **130 static waste drums:** three tight clusters containing 117 drums, four on the truck bed, six tipped behind the truck and three other loose-positioned upright drums. Cluster spacing is 1.02 m for roughly 0.84 m bodies; 45 base drums, 45 second-tier drums and 27 third-tier drums. Every barrel has a radiation mark. Truck, lowered tailgate and two small spills are scenery, without new damage or moving-physics mechanics.
- Gatehouse, employee parking, maintenance shed, cooling basin, pump hut, transformers, fences, trees and utilities fill the approach/departure thirds. Flat seams and railway clearance retained. Water is a shallow visual surface above supported floors, not a swimming or deep-basin system.
- Rough Quackville materials, 29 baked environment lights, dedicated authoring/companion scenes and probes. The normal Expedition atmosphere is unchanged. Enemy bounds cover both plant sides at Z=45..115; a 14 m central navigation exclusion keeps enemies off the railway. Three spawn variations retain six coal, six enemy candidates and two gold candidates each.

### Validation

- [3,185 physical/layout/navigation/UV assertions](validation/nuclear-plant/geometry-checks.txt): tower access/rings, open shell tops, stair support/headroom, gallery circulation, barrel stack support and aisles, flat seams, railway clearance, spawn placement and connected graph routes on each side.
- [803 catalogue checks](validation/nuclear-plant/week3-checks.txt), including the new sixteenth location, coal separation, route serialization and graph-grid alignment.
- [Lighting export validation](validation/nuclear-plant/lighting-checks.txt): 29 baked lights, 1,344 static receiver bindings and enabled companion build entry.
- [Offline Play Mode route proof](validation/nuclear-plant/runtime/runtime-proof.txt): seeds 11 and 43 passed readiness, atlas bindings, translated probe comparison, finale relocation, route replacement and complete teardown with the plant plus four existing locations. The retained log contains two completed repetitions of the two-seed workflow, 42 PASS lines per repetition (84 total), with 12 lightmaps and 2,334 probes per assembled route. This is single-editor runtime evidence, not multiplayer or headset acceptance.

Actual Unity review captures: [whole block](../output/nuclear-plant/overview.png), [dense barrel yard](../output/nuclear-plant/barrel-yard.png), [tower interior](../output/nuclear-plant/tower-interior.png), [open tower sky](../output/nuclear-plant/tower-sky.png), [turbine hall](../output/nuclear-plant/turbine-hall.png), [control gallery](../output/nuclear-plant/control-gallery.png). These use a temporary fill light and disabled fog to inspect geometry; they are not final gameplay-atmosphere or headset evidence. No capture overrides were saved to the bake scene.

The initial visual pass caught coplanar ground/interior floor overlap; interior floors are now slightly raised and rechecked. The first offline proof attempt incorrectly supplied only one catalogue entry to the five-stop selection harness; it was stopped and the setup changed to five existing locations, including the plant. That aborted attempt is not acceptance evidence. During the corrected run, a tool inspection showed an idle test object with no route; explicitly starting its coroutine produced a second completed repetition in the retained log. The proof itself needs no runtime-gameplay changes. Editor Multiplayer Play Mode persistent-settings and pooled-audio warnings appeared during test startup; no related project-wide behavior changes were made.

Headset traversal, low-eye readability, enemy pursuit/rescue, real-client behavior and Quest performance remain open. The blockout does not satisfy those milestone acceptance items.

### Reproduce / review

Open `Assets/DuckRails/Lighting/Blocks/nuclear-power-plant-v1/nuclear-power-plant-v1-bake.unity`, or include **Nuclear Power Plant** in Duck Rails Tests > Test Block Order. Authoring entry point: **Duck Rails > Authoring > Nuclear Plant > Install approved block**. After geometry edits, rerun `RailNuclearPlantChecks.Run()`, bake/export via **Lighting > All Blocks > Bake Open Block**, and validate the catalogue export. `RailNuclearPlantChecks.PrepareProof()` creates the offline assembled-route proof; then enter Play Mode. `Register()` adds the prefab to Expedition without rebuilding other blocks.

## V2 — enterable cooling towers and waste-drum yard

November 5: user requested a top-down floor plan and explicitly wants to enter the cooling towers. They also requested many oil-style barrels representing nuclear waste outdoors, possibly spilled from a truck. These requirements supersede V1's predominantly scenic tower treatment; exact layouts remain proposals.

[Top-down site and interior sheet](../output/imagegen/nuclear-power-plant-v2/floor-plan.png) | [Editable drawing](../output/imagegen/draw-nuclear-floorplan-v2.ps1)

The drawing uses deterministic scaled footprints rather than image-generation perspective. Whole site is 96 x 160 m, north/+Z up. Central railway remains at X=0, with X=-4..4 clear and 3 m walking verges outside it; flat 6 m seams. Towers are centered at (-26,67) and (-26,94), both 22 m diameter, within the middle third. East turbine hall occupies X=12..40, Z=55..91; rounded reactor shell centered (26,103), radius 10 m, remains exterior-only in this proposal and extends slightly into the departure third.

### Enterable tower interiors

Both towers have east/west 3 m access openings between base supports, a nominal 3 m perimeter walk, broad crosswalks and connected exterior return routes. Four interior support clusters sit inside the basin footprint, away from crosswalks. These are scaled-down fictional game towers, not engineering replicas.

- **A / damp:** shallow decorative basin quadrants, proposed water depth 0.1 m, dry crosswalks and perimeter route. Partial fill and water-distribution pipework remain overhead at +4.5 m or higher; damaged central fill opens a view upward.
- **B / drained:** level dry basin floor, fallen fill panels outside the main paths, mostly missing overhead fill and a clear view up the hollow shell. No pit or mandatory jump is proposed. Both exits remain accessible from the interior.

Real natural-draft wet cooling towers contain cooling fill and water-distribution equipment above a collection basin; warm moist air rises through the tower. Sources: [USGS tower description](https://www.usgs.gov/media/images/natural-draft-cooling-tower) and [NRC steam/power conversion description](https://www.nrc.gov/sites/default/files/doc_library/cdn/legacy/reading-rm/doc-collections/nuregs/staff/sr1793/initial/chapter10.pdf). The drained floors, broad routes and missing fill are deliberate abandoned-site gameplay adaptations. No operating-tower access realism or radiation mechanic is implied.

### Hall and outdoor waste scene

Turbine hall has two 6 x 8 m machinery footprints, a rail-facing west entrance and east service exit at Z=71..75, and an unobstructed cross-hall route. An 8 m-wide east gallery at +3 m has consoles against the east wall, a clear aisle and two 3 m-wide, 9 m-long stair flights with end landings. Ground and upper plans share orientation and footprints. Stair headroom, railings and low-eye visibility require 3D validation.

The southeast waste yard occupies X=11..40, Z=16..34, next to the maintenance shed at X=27..40, Z=37..49. The site plan proposes 42 upright drums plus seven scattered drums; counts are drawing defaults. A parked truck, barrels fallen from its rear, some tipped/split barrels and small stylized spills establish the requested accident scene. Use radiation markings on barrel sides, dull yellow/rusted steel and restrained spill color. Keep a broad accessible yard route; spills are scenery rather than new damage or cleanup mechanics. The enlarged truck inset is schematic, not a second exact barrel-placement plan.

Gatehouse, parking, cooling basin, pump hut, transformer yard, fences and vegetation continue through the outer thirds. Reactor shell is exterior-only for this revision; no interior is promised without a further layout.

### Validation and next step

PNG rendered and visually inspected for legible labels, matching north-up plans, entrances, clear railway and full-block distribution. No Unity edits or runtime validation. User review of this sheet precedes rough Quackville blockout. Before implementation reconcile east/west enemy coverage, bake authored navigation and lighting, check all stairs/openings and ensure loose items can be recovered from basins. Headset, multiplayer and performance acceptance remain separate.

## V1 — historical concept

November 5, 2026. User requested a nuclear power plant block concept. This is an unapproved layout and atmosphere study; no Unity assets or gameplay have changed.

[Concept sheet](../output/imagegen/nuclear-power-plant-v1/concept.png) | [Generation prompt](../output/imagegen/nuclear-power-plant-v1/prompt.txt)

## Proposed direction

A deserted plant that still appears partly alive: enormous cooling towers, faint machinery hum, amber maintenance lights and an elevated control room glowing green. Use simple faceted forms and coarse Quackville-style pixel textures. The warm train remains the obvious return landmark.

Use the standard 96 x 160 m layout as a starting envelope, with a straight central railway and the stop at the midpoint. Preserve the clear railway corridor, walking verges and flat seams. Exact dimensions and geometry await a measured top-down revision and user confirmation before rough blockout.

- Middle third, west: two cooling towers with a pipe court and paths around their bases. The towers primarily provide scale and silhouette; full-height climbing is not proposed.
- Middle third, east: turbine hall with large machinery, a broad ground-floor circulation loop and an elevated control-room gallery reached by two stairs. Provide a rail-facing entrance and a second exit connected back around the building. A separate rounded reactor containment building provides a secondary landmark.
- Approach third: gatehouse and abandoned employee parking west; maintenance shed, loading apron and service truck east.
- Departure third: cooling-water basin and pump hut west; fenced transformer yard east. Paths, broken fence openings, utility infrastructure and vegetation distribute supporting scenery toward the outer edges.

Quick scavenging sits near rail-facing maintenance spaces; a longer excursion leads through the turbine hall and gallery. Reuse established coal/equipment rules, with coal presented as railway maintenance supplies rather than plant fuel. Exact spawn placements and enemy coverage require a later layout pass; the concept does not change counts or authorize new enemy behavior.

The horror comes from obscured views behind machinery, distant silhouettes and the contrast between apparently active controls and abandoned rooms. Radiation damage, meltdown timers and power-restoration puzzles are not part of this proposal.

## Drawing limitations and next step

Generated with the built-in image-generation tool and visually inspected. Labels and major areas are readable. The overhead panel is illustrative rather than a measured orthographic drawing: tower sidewalls are visible, the towers are arranged along the railway rather than side by side, and the main structures occupy somewhat more than the strict middle third. The perspective panel also invents train details; it is not a train redesign or a Unity capture.

Review the location concept, then resolve exact footprints, middle-third fit, gallery stairs, basin recovery routes, railway clearance and enemy/navigation boundaries in a measured top-down plan before implementation. No headset, multiplayer or performance validation is claimed.
