# Roadside Motel — floor plan study

November 4, 2026. The user approved the motel/transformer-yard drawing set for greyboxing with “do greybox.” The actual motel prefab now contains the installed layout below. Earlier drawing sections retain the history of proposed dimensions; headset scale and gameplay acceptance remain pending.

## Quackville rough materials and edge elevation

November 4 follow-up: user requested the Quackville rough material pass and clarified elevation as “just a little bit around the edges.” Installed worn plaster/olive paint with exposed brick, concrete, timber, dark roofs, asphalt and oxidized steel using the existing Town/Industrial atlases and depot materials. 662 existing surfaces are mapped; chain-link topology and lightmap UVs are retained. Track, original colliders, light settings and gameplay layout are unchanged.

Six collidable grassy banks add subtle perimeter relief: four approach/departure corner patches peak at 0.8–0.9 m; narrow west/east strips peak at 0.3/0.15 m. All stay outside the track corridor and encounter bounds. Their outer rims return to ground +12 mm; the first/last 6 m of the block remain flat. Courtyard, transformer aisles, garage apron and rear lane stay level. These are rough terrain meshes with rectangular grass margins, not final landscaping.

Offline navigation and lighting were rebuilt/exported. Validation passes: 668 mapped meshes with full atlas/lightmap channels, 4,908 bank support samples, maximum bank grade 15%, 176 motel physical/layout checks, 619 catalogue checks and 905 static lightmap receiver bindings. Evidence: [material checks](validation/motel-materials/checks.txt), [bank checks](validation/motel-materials/edge-checks.txt), [layout checks](validation/motel-materials/geometry-checks.txt), [catalogue checks](validation/motel-materials/week3-checks.txt), [lighting checks](validation/motel-materials/lighting-checks.txt). An initial material check incorrectly assumed at least 800 mapped surfaces; it now verifies each eligible surface directly, excluding the retained track and fixtures. An inspection attempted during lighting export found no RailBlock in the companion scene; rerunning after opening the bake scene passed.

Actual Unity review captures: [courtyard](../output/motel-materials/courtyard.png), [transformer yard](../output/motel-materials/transformer-yard.png), [whole block](../output/motel-materials/whole-block.png), [low edge view](../output/motel-materials/edge-banks.png), [upper cutaway](../output/motel-materials/upper-cutaway.png). Review fill and disabled fog are temporary and discarded. Shared editor restored to campground Bake scene after verification.

Use **Duck Rails > Authoring > Motel > Apply Quackville rough materials** to reapply and queue lighting, and **Validate rough materials** to check the saved prefab. Reinstalling greybox resets surface meshes; reapply materials afterward. The mapped instances total 207,052 vertices; target-headset feel and Quest performance remain unverified. This surface/edge follow-up did not repeat the earlier private loading test or claim multiplayer acceptance.

## Installed greybox

`Assets/DuckRails/Blocks/roadside-motel-v1.prefab` contains `Motel greybox v1`, replacing the original room shells, planter and west-side placeholders. The block ID, 96 x 160 m ground, straight track, entry/exit, stop and three spawn variations are retained. `RailWeek3Builder` preserves this authored prefab on catalogue rebuilds.

- Motel: two storeys, five ground guest rooms plus reception/laundry/linen, eight upper guest rooms, bathroom partitions and static bed/desk/furniture blocks. Upper floor +3.6 m, roof underside +7.2 m. The approved 30 x 55 m site envelope is retained.
- Two 2.4 m nominal exterior stairs, 20 risers of 0.18 m each, 6.4 m runs and 2.4 m landings. A joins the east balcony sideways; B joins its north wing. Both connect to the continuous 3 m nominal balcony. Open visual balcony bars use solid collision barriers; stair side guards remain simple solid greybox slabs.
- Six doors are held ajar inward as static geometry. East-wing openings widen from the drawing's 2.4 m to 3 m to retain usable passage beside the leaves; other openings remain 2.4 m. No dynamic door or locked-room mechanic is added. The reception's west exit exists on the ground floor only.
- West office/garage: 20 x 24 m shell, roof +3.6 m, south-facing 6 m garage opening, east office and staff entrances, interior connections and simple props. Garage lintel currently shares the 2.6 m doorway height.
- Transformer yard: six machines in three rows, fin/insulator silhouettes, 32 x 24 m pad, 2.2 m chain-link collision fence, 6 m north gate and 4 m east/west breaches with bent end panels. Mesh is batched per fence span; no electric hazard or power puzzle is added. Van/crates occupy the service apron north of the yard.
- Courtyard car blocks and parking stripes, motel sign, rear lane, dumpsters and boundary fences. Track rails/sleepers remain visual-only.

Resource markers retain their existing identities and counts: six coal per variation (three per side), six enemy candidates, and existing gold markers. Some east coal/gold occupy upper rooms. Enemy navigation remains east of the tracks; the transformer yard remains a player scavenging area. Bounds expand to X=9..45, Z=45..115. The offline graph bake uses origin (8,-2,32), matching the catalogue's 16-cell alignment, with a 38 x 12 x 96 m bake volume. No runtime baking or train navigation is introduced.

Thirty baked visibility fixtures and refreshed probes use the existing lighting companion/export workflow, with 899 static receiver bindings. No production atmosphere settings are changed. The current lights are greybox visibility tuning, not final art/darkness acceptance.

### Validation and review

Compilation, 176 physical/layout checks, 619 Week 3 checks and catalogue lighting export checks pass. Checks cover stair support/headroom, both landings, balcony corridors, all main room entrances, fence breaches, machine aisles, resource support/clearance, ground-only navigation to upper rooms and builder preservation. [Geometry evidence](validation/motel-greybox/geometry-checks.txt), [catalogue evidence](validation/motel-greybox/week3-checks.txt), [lighting evidence](validation/motel-greybox/lighting-checks.txt).

An initial live expedition exposed a graph-origin mismatch (Z=42 rather than Z=32), despite the previous subset-based catalogue check passing. Corrected the authoring bake depth and rebaked navigation, then added catalogue-wide origin alignment assertions for every block. Live retry results are recorded separately below.

The coordinated retry passed at 2026-11-04T06:55:19Z: authenticated private single-editor expedition reached Running/WorldReady, motel first at world Z=150, authored geometry loaded and complete-route lighting ready. [Live evidence](validation/motel-greybox/live-check.txt). The route used motel, lumber yard, convenience store, passenger station and container yard; the concurrently edited campground was deliberately excluded. This is loading/integration evidence, not a natural gameplay run or multiplayer/headset acceptance. Play was stopped, the previous test preset restored and the shared editor returned to the campground task's saved lighting-companion scene.

The successful run logged two MRUK shared-global-context initialization errors, with no new navigation assembly error in the final Console query. [Console notes](validation/motel-greybox/live-console.txt). Initial diagnostics query was sandbox-network-blocked; authorized retry returned ten pre-existing open reports, all left untouched. The new motel chunk-alignment report `23c2ef317421447ba4a6463ca3bdd7b0` was resolved locally and in the backend only after the recorded fix, regression checks and successful live retry. MRUK errors remain separate.

Actual Unity review captures: [courtyard](../output/motel-greybox/courtyard.png), [transformer yard](../output/motel-greybox/transformer-yard.png), [whole block](../output/motel-greybox/whole-block.png), [upper cutaway](../output/motel-greybox/upper-cutaway.png), [ground cutaway](../output/motel-greybox/ground-cutaway.png). Captures temporarily remove fog, add directional review fill and hide roofs/floors for cutaways; these changes are discarded afterward. They show greybox geometry, not final gameplay darkness.

Authoring menus under **Duck Rails > Authoring > Motel**: **Install scaled greybox (resets motel layout)**, **Check greybox**, **Capture greybox**, and **Load motel-first review route**. Install replaces its owned subtree and queues a lighting bake/export; it is not the command for preserving manual edits. Load the review route before a private development expedition and clear it through Test Center's normal-route control when finished.

Pending: worn-headset stairs/guards/door feel, room search pacing, rescues/boarding, multi-client behavior and Quest frame time/memory. Shapes remain greybox objects with the rough materials described above; consolidation and profiling remain later work.

## Drawing set

- **Latest V3:** [Whole block with transformer yard](../output/imagegen/motel-block-layout-v3/whole-block-top-down.png). [Editable source](../output/imagegen/draw-motel-block-layout-v3.ps1). Supersedes V2 site proposal below.

- **Latest:** [Whole-block top-down V2](../output/imagegen/motel-block-layout-v2/whole-block-top-down.png), with courtyard parking, rear lane, office/garage and loading yard across the tracks. [Editable source](../output/imagegen/draw-motel-block-layout-v2.ps1).

- [Scaled site plan](../output/imagegen/motel-floorplans-v1/site-plan.png)
- [Ground floor](../output/imagegen/motel-floorplans-v1/motel-floor-1.png)
- [Upper floor](../output/imagegen/motel-floorplans-v1/motel-floor-2.png)
- [West service building](../output/imagegen/motel-floorplans-v1/service-building.png)
- [Editable PowerShell drawing source](../output/imagegen/draw-motel-floorplans-v1.ps1)

The drawings are metre-scaled diagrams, not generated perspective concepts. North is +Z. Each sheet uses equal horizontal and vertical scales. Nominal footprints include wall allowances that still require a physical greybox pass; furniture is static placeholder geometry. The existing approved nighttime motel study remains the visual reference in `art-concepts.md`.

## Verified base

Whole-block V2 follow-up: the user endorsed the motel and suggested ajar doors, then requested a top-down view of the entire block. V2 incorporates illustrative static inward-open door leaves, parking, a 5 m rear lane at X=40..45 with fence, and a 4 m north connector at Z=109..113. Door angles are schematic; accessible gaps must be measured in greybox. It proposes revising the west building into a south-facing garage (12 x 12 m), east-facing office (8 x 12 m), north storage (12 x 12 m) and staff room (8 x 12 m), retaining its 20 x 24 m footprint. This supersedes the earlier west-room drawing as the current proposal, not as approved implementation. A van and crates occupy the loading yard. Cross-track return paths are diagrammatic and positioned beyond the symbolic stopped-train marker; actual train clearance requires checking. Whole-block PNG rendered and visually reviewed; Unity unchanged.

`Assets/DuckRails/Blocks/roadside-motel-v1.prefab` has a 160 m block length, straight track at X=0 and stop at Z=80. `Assets/Editor/RailWeek3Builder.cs` defines 96 m ground width, and the existing encounter bounds occupy Z=45..115. The existing enemy bounds are X=9..37, Y=-2..10. The proposal retains the block boundary, straight track and quiet approach/departure buffers.

## Proposed layout

### V3 transformer yard addition

The user requested a power substation/transformer area with rows of machinery and chain link broken in several places. The revised drawing places a 32 x 24 m enclosure south of the west garage, world X=-42..-10, Z=47..71. Three north–south rows each contain two nominal 4 x 5 m equipment masses, with 6 m between rows and a 4 m cross aisle. Chain-link perimeter has a 6 m north gate and two 4 m damaged sections on the west/east sides. Bent fence ends angle away from the gaps. The east breach connects toward the train; the north gate connects to a 7 m deep service apron at Z=71..78. Van and crates move to the apron, leaving the garage approach clear. This replaces the large loading yard in V2, while retaining the motel, track and encounter bounds. The requested yard and damaged-fence direction are confirmed; exact footprint, machine count and clearances remain drawing proposals. Equipment is environmental geometry; no power-switch objective or electrical damage mechanic is introduced. This does not replace the catalogue's separate Electrical Substation block. V3 was rendered and visually reviewed; Unity implementation remains pending.

Main motel drawing origin: world X=10, Z=54. Overall envelope 30 x 55 m, with an open courtyard west of the east wing. East guest wing: X=28..40, Z=54..94; north wing: X=10..40, Z=97..109. The north covered walk/balcony occupies Z=94..97 across the full north frontage, connecting to the east walk at X=25..28. This gives every north-wing door direct access without passing through a guest room.

Ground: five 12 x 8 m guest rooms (101–105), plus three 10 x 12 m north-wing spaces for reception, laundry and linen/staff. Reception also opens west. Upper: five east rooms (201–205), three north rooms (206–208), all accessed from the open L-shaped balcony. Guest rooms contain bed/desk and bathroom partitions. Thirteen guest rooms total is a proposed search density, not a requirement; reduce accessible rooms if natural run pacing demands it.

Two exterior stairs connect the courtyard and balcony. In local motel coordinates, A occupies X=12.6..15, Z=0..6.4, with a top landing Z=6.4..8.8 joining the east walk. B occupies X=0..2.4, Z=31.2..37.6, with a top landing Z=37.6..40 joining the north walk. Reserve 2.4 m clear ground landings at each foot (A extends south of the drawing envelope to world Z=51.6). No jumping is required. Upper floor +3.6 m, indicative roof +7.2 m; stairs reserve 6.4 m horizontal runs, 20 rises of 0.18 m and 2.4 m nominal width. Exact treads, wall thickness, landings, headroom and hand clearance require implementation checks. Guards should preserve low-eye visibility while preventing falls; guard design remains unresolved.

West service building: X=-36..-16, Z=78..102, single-storey 20 x 24 m shell with manager office, maintenance, bulk storage and break room. A 3 m central spine and 4 m entry passage connect the east-facing entrance to north/south exits. Proposed roof +3.6 m. Loading pad X=-36..-16, Z=58..74. Replacing the two west placeholder shells with this building and yard is part of the proposed layout, not an implemented change.

Courtyard has two isolated parked-car prop blocks, a clear route toward the train and a motel sign near X=11, Z=49. Main doors nominally 2.4 m wide and 2.6 m high; walks/balcony 3 m wide. Drawing grid is a coordinate aid, not floor geometry. The site train rectangle is only a stop marker, not a measured train footprint. Guest-room search pockets intentionally end inside each room; the exterior circulation, rather than each bedroom, supplies the alternate return route.

## Implementation and acceptance still pending

### Exterior concept follow-up

November 4: in response to the user's question about the other side and request interpreted as concept shots, generated [courtyard parking](../output/imagegen/motel-exterior-study-v1/parking-courtyard.png) and [rear service lane](../output/imagegen/motel-exterior-study-v1/rear-service-lane.png). Proposed treatment: a small guest parking area on the train-facing courtyard, two abandoned cars, and a narrow rear lane with dumpsters/meters and a boundary fence. The east wing at X=40 leaves 8 m to the block edge, allowing a nominal 5–6 m lane plus fence margin. This is a proposal, not an approved site-plan revision. Images are atmosphere studies: room counts, numbering, train design and rear corner geometry are approximate and do not override the scaled plans. Generated with the built-in image tool; [prompts](../output/imagegen/motel-exterior-study-v1/prompts.json). No Unity changes.

- Review drawings before replacing prefab geometry. Preserve stable block/variation identities, track and stop.
- Retain six coal chunks per variation, three per side, and existing gold counts/identities. Place markers after room and route review; no final placements are drawn.
- Extend the east enemy bounds to include X=40 plus appropriate clearance; bake upper walk, stairs and accessible rooms offline. Keep enemies local and off the train. West enemy coverage is not added by this proposal.
- Protect authored geometry from the general block builder; rebake/export lighting using the existing workflow.
- Check real wall/door clearances, stairs and guard collision, room access, navigation and lightmap bindings in the greybox.
- Review both stair routes, low-eye sightlines, room search time, rescuing and boarding in VR, then validate networked play and Quest performance separately.

All four PNGs were rendered and visually inspected. The drawing review caught and corrected insufficient north-wing frontage circulation before delivery. This is drawing verification only, not Unity or headset acceptance.

## Community pool, mini-golf and roller rink — November 12, 2026

User approved the [top-down concept](../output/motel-leisure/approved-concept.png), replacing the transformer yard and west garage with a separate run-down 1980s public pool. Northeast picnic grove is superseded by an outdoor roller rink. User requested more buses than cars.

Installed rough Quackville blockout:
- Existing two-storey motel geometry, balcony, stairs, rooms and courtyard retained.
- West: excavated drained pool (20 x 28 m, floor -1.6 m), broad recovery steps, faded tiles/lane stripes, curved yellow static waterslide and stair tower. Separate fenced public entrance and rail-facing gate.
- Change rooms: roofed open-front access, showers/privacy partitions, benches and 66 open cubbies along the rear wall.
- Northwest: five putting greens, flags, simple obstacles/duck and ticket hut.
- Northeast: concrete roller rink, pastel perimeter stripes, low rails/open entrance, skate rental hut/counter and covered benches.
- South: separate public/motel parking, five closed static coach buses versus two retained courtyard cars, bike racks, signs and baked lamps.
- Four old corner banks removed where they intersected the new destinations. Narrow lateral rises retained; fifteen trees have closed collidable canopies. Flat end seams and railway clearance retained.
- No skating, slide physics or vehicle interaction system added.

Authoring: RailMotelLeisure.Install() is a guarded one-time installation; FinishLayout() is the one-time terrain/scenery follow-up. Edit saved geometry for further changes; do not rerun the old whole-block greybox builder. New assets live in Assets/DuckRails/MotelLeisure.

Validation: 2,114 focused editor assertions pass (loot footprint/clearance, native marker references, rail clearance, UV2, fonts, main entrance headroom, five buses/two cars). All 24 coal/gold candidates pass after local support corrections, including existing upper-room gold height corrections. Offline full-area navigation rebuilt; every enemy marker has ground within 0.8 m. See [checks](validation/motel-leisure/checks.txt) and [navigation evidence](validation/full-area-navigation.txt). Lighting and final captures are tracked below.

Pools remain 18 coal / 6 gold / 18 enemy candidates; per-run scaling/settings unchanged. Displaced west markers were relocated; motel geometry and track preserved. These legacy pools are below eight-player resource capacity (31 coal/52 gold); existing enemy selections fall short in 600/800 crew/seed cases both before and after this pass. This is an existing balance limitation, not accepted spawn capacity. See [comparison](validation/motel-leisure/enemy-selection.txt).

Queued for playtest: natural pool/slide recovery, cubby reach, stairs, room/rink/golf/bus circulation, enemy pursuit, multiplayer/late join, actual night lighting and Quest performance. No Play Mode run for this art pass.

Final editor evidence: lighting exported with 1,737 valid receiver bindings and no missing renderer/texture references; Console error/exception query empty. Final [Unity overview](../output/motel-leisure/pool-and-motel.png) and [top-down](../output/motel-leisure/top-down.png) use temporary review fill/no fog, discarded after capture. Prefab Mode left open for inspection. This is rough blockout, not final art or headset acceptance.

## Screenshot revision batch — November 12, 2026

User explicitly approved executing the accumulated screenshot feedback. The updated prefab supersedes the initial leisure blockout details above.

- Motel sign uses solid extruded CutePixel glyph geometry with white emissive material. Pool has one larger cyan emissive POOL mesh sign; redundant LOCAL/PUBLIC signs removed.
- Two courtyard cars now have colored bodies, glazing, window pillars, wheels, bumpers and lamps. Five buses retained.
- Removed all guest-room number labels. East room openings are 1.5 m wide (formerly 3 m), north openings 1.2 m (formerly 2.4 m). Seven of thirteen guest rooms are fixed closed facades: 103, 105, 201, 203, 205, 206, 208. Their interiors are blocked/removed and their front windows are opaque recesses. The fixed irregular selection is authored, not randomized independently by clients.
- Open rooms have real unglazed front/outside window openings; retained service rooms also have windows. Rooms 101/102 have an internal connecting doorway and ajar leaf. Both motel stair sides now have open handrails/balusters.
- Added two dumpsters north of the motel.
- Moved golf shack to the southeast of the golf area, facing north, clear of the nearest green. Nine holes: three L-shaped, three long, three S-shaped, with varied flag rotations. Removed coplanar overlapping surface patches.
- Trees vary in scale; all canopy mesh colliders are convex and trunks use boxes. Two tree groups moved clear of greens.
- Rink barriers are short white concrete with blue top bands, with north and south entrance gaps. A north rental-building doorway also added.
- Simple metal side fencing uses X +/-47 m (1 m inset) on the outer thirds and short returns to the retained central fences. Existing chain-link geometry retained; no full-length chain-link duplication.
- Pool has six loungers, exterior cubbies only on the left, and a separated right-side food vendor with serving hatch/staff door, freezer, fryer, and shelved/boxed supply room.
- Pool floor slopes from -1.6 m shallow depth to -3.2 m at the north end. Short (1 m) and tall (3 m) static diving boards have stair access. Slide is straight and shifted south to clear the diving boards. Broad shallow-end recovery steps retained.
- Twelve additional 7.5 m streetlights and two 10 m pool floodlight masts. Sixteen new room-front fixtures: eight working/eight broken. Existing interior/other lights retained.

Validation: 2,186 focused editor checks pass after loot support/clearance corrections, including retained native marker references, railway clearance, UV2, fonts, doorway headroom, room/fixture counts, golf, tree colliders and the requested pool/rink changes. All 24 loot candidates pass; closed-room candidates relocated to accessible rooms/features without changing candidate or per-run counts. Full-area navigation rebaked; all 18 enemy candidates have ground within 0.8 m. Existing larger-crew spawn-capacity limitations remain unchanged in scope. Evidence: [revision checks](validation/motel-leisure/revision-checks.txt), [navigation](validation/full-area-navigation.txt).

Visual captures: [motel](../output/motel-leisure/revised-motel.png), [pool](../output/motel-leisure/revised-pool.png), [golf](../output/motel-leisure/revised-golf.png). These use temporary review fill/no fog and do not establish gameplay darkness acceptance. Lighting export evidence recorded separately below. No Play Mode run. All gameplay/headset acceptance is queued for playtest.

Authoring entry: RailMotelRevision.Install is guarded against rerunning on an already revised prefab. Saved prefab is authoritative for final visual clearance adjustments. Do not run the older greybox/leisure installers over these edits.
Interior lighting follow-up: no-fill review exposed a dark vendor. Added three baked fluorescent fixtures for vendor, supply room and skate rental; final check total is 2,186. The final lighting export and captures supersede the first revision bake.
Final revision bake exported 2,154 valid receiver bindings (zero missing references). Final Console error/exception query is empty. Revised images refreshed after final bake; vendor-night.png uses no temporary fill. Saved motel prefab left open, clean. Queued for playtest.

## Detail feedback batch — November 12, 2026

The user authorized the next accumulated screenshot batch. It supersedes the repeated golf shapes and mixed room-front fixture styles above.

- Nine individually authored putting outlines: L bend, T finish, extended zigzag, C loop, long straight, side return, hook finish, offset dogleg and diagonal chicane. A shoot-through house and restored duck occupy separate greens. Golf shack gains a counter and leaning clubs.
- GOLF and SKATE RENTAL use solid extruded CutePixel glyph meshes.
- Closed motel windows receive timber boards. Door leaves receive dirty green pixel materials and handles. Six accessible guest rooms gain dressers/CRTs and bathroom fixtures.
- Both floors use joined wall surface meshes that remove overlapping partition/façade faces; original wall collision and openings remain. Both motel stairs use continuous concrete UVs instead of a repeated texture on each tread side.
- Courtyard cars are light grey and blue. Roof receives three HVAC units and service pipes.
- Keep only the eight white room-front fixtures and place baked lights at them. Remove other room-front fixture styles; interior lighting remains.
- Diving stairs meet the board edges and heights; railing posts sit on the access treads. Rink wall/coping corners use joined extrusions and the three colored strips have mitered corners, retaining north/south entrance gaps.
- New flat materials are replaced with point-filtered pixel textures cropped from the Quackville Town atlas. UVs use a consistent world scale on the added surfaces.

Authoring: `RailMotelFinish.Install` is a guarded one-time detail pass. Saved prefab is authoritative; do not rerun previous whole-block installers. Editor checks, bake and visual-review evidence follow below. Natural traversal, reach, night readability, multiplayer and Quest acceptance remain queued for playtest.

Editor validation: 4,186 checks pass, including all 24 native loot candidates' support/clearance, pixel-material coverage, nine distinct outline meshes, actual house opening, bed/dresser separation, continuous diving stair/board joints and supported rail posts. See [detail checks](validation/motel-leisure/finish-checks.txt). Navigation rebuilt to 27,700 nodes; all 18 enemy candidates have ground within 0.8 m. Candidate pools and per-run settings remain unchanged. The current saved prefab has no waterslide subtree; this detail pass preserves that absence rather than restoring a removed layout element. Visual review prompted moving five dressers away from the beds before the final bake.

Final visual correction: continuous planar lightmap charts remove per-tread bands on both motel stair sides. Bathroom sinks have floor-supported pedestals and shower trays sit under the heads. Final lighting export has 1,954 valid receiver bindings and no missing references; [export evidence](validation/motel-leisure/finish-lighting.txt). Final Console error/exception query is empty. Saved Prefab Mode is open and clean. No Play Mode run; queued for playtest.

Final Unity captures: [motel](../output/motel-leisure/finish-motel.png), [nine greens](../output/motel-leisure/finish-golf.png), [room](../output/motel-leisure/finish-room.png), [stair](../output/motel-leisure/finish-stair-detail.png), [diving boards](../output/motel-leisure/finish-diving-detail.png), [rink](../output/motel-leisure/finish-rink.png). These use temporary review fill/no fog, discarded after capture. The [no-fill motel capture](../output/motel-leisure/finish-night-motel.png) shows the baked lighting with fog disabled; it does not establish headset/night-readability acceptance.

## Face, fixture and spawn correction — November 12, 2026

User authorized this feedback batch: reversed golf/rink sides, floating interior lights, downward whiter streetlights, half-resolution car wheels, six spaced change-room benches, and replacement of all coal/gold/enemy placements.

- Corrected the extrusion side triangle winding in `RailMotelFinish.Extrude` and the 13 saved golf/rink wall/coping meshes. Their top/bottom faces and outlines remain intact. All 198 side triangles now pass outside-facing collider rays with backface hits disabled. This checks the winding defect missed by the earlier detail assertions.
- Mounted all 16 interior motel fixtures flush against the respective ceilings and assigned emissive white pixel material. The 12 tall streetlight spots aim vertically downward with near-white light. Existing authored light/pole positions retained.
- Eight car wheels share a 10-segment, 40-triangle mesh, down from 20 segments/80 triangles per wheel; collider dimensions retained.
- Six timber/metal benches in two columns and three spaced rows replace the change-room seating; clear of shower partitions and vendor space.
- Replaced all previous spawn candidates with 48 coal, 64 gold and 32 enemies. These are this pass's authored pool sizes, not new per-run tuning. Inspector enable/fixed counts remain 6/2/5; crew scaling and selection code are unchanged. Loot covers six accessible guest rooms on both floors, motel services, benches, vendor/freezer/shelves, pool deck/basin, nine greens, golf shack, rink/rental and all five buses. Enemies have eight candidates per quadrant, including pool basin, upper motel walk and room 207 alternatives. Closed rooms are excluded.

Validation: [placement plan](validation/motel-leisure/spawn-plan.txt) was checked for support/clearance before replacement. After saved-prefab reload, all 144 candidate markers retain native prefab references and pass item-envelope support/clearance (including a positive-control physics query); all enemies are within bounds and 0.8 m of baked ground. [2,691 spawn checks](validation/motel-leisure/spawn-revision-checks.txt) pass, including 100 seeds for every crew size 1–8: full scaled resource counts, full 4/8/12 enemy counts, exact quadrant quotas, unique selections and 15 m horizontal enemy spacing. This resolves the historical motel candidate-capacity shortfall recorded above. [50 art checks](validation/motel-leisure/correction-checks.txt) and [outside-face rays](validation/motel-leisure/face-collision-checks.txt) pass. Navigation rebuilt after geometry changes; lighting export and final visual review are recorded below. No Play Mode run; natural reach, bus-roof access, enemy pursuit and headset lighting remain queued for playtest.

Final export: 1,970 valid lightmap receiver bindings, no missing references; saved pools 48/64/32 and inspector settings 6/2/5 verified. Navigation has 27,841 nodes; new enemy markers independently passed proximity checks after placement. [Lighting evidence](validation/motel-leisure/correction-lighting.txt). Compilation/authoring checks succeeded. The final Console query contained one Unity editor service-token exchange error; it did not block local checks or lighting export and was not investigated as a gameplay fault.

Visual review: [golf edges](../output/motel-leisure/correction-golf-edge.png), [rink wall](../output/motel-leisure/correction-rink-edge.png), [six benches](../output/motel-leisure/correction-benches.png), [ceiling fixture](../output/motel-leisure/correction-ceiling-light.png). Captures use temporary fill/no fog; temporary objects/settings discarded and saved motel Prefab Mode left open and clean. Queued for playtest.

Pre-commit crawl-graph rebake (November 12, explicitly requested): 27,836 nodes; all 144 authored markers preserved; all 32 enemy candidates have ground within 0.8 m. The 2,691 spawn checks passed again against the new graph. Saved motel Prefab Mode reopened for inspection. Gameplay/headset checks remain queued for playtest.
