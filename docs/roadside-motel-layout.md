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
