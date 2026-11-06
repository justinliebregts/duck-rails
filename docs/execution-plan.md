# Duck Rails — execution plan

Handoff date: October 28, 2026. Read `game-design.md` for decision status and scope.

## November 6 — manual two-client launch and keyboard throttle

- [x] Apply saved enemy invincibility to all editor test expeditions, including public quick-start and automated suites; synchronize the main editor's toggle to its additional clients. Two authenticated clients passed ON/OFF/ON and rejected 24 enemy contacts each. Full automated-suite and headset reruns remain separate. See [invincibility evidence](testing-tools.md#enemy-invincibility-in-test-center).
- [x] Replace the Test Center coal/handoff button with a manual two-client expedition launch using the existing scenario and quick-start routing; no fixture or automatic timeout. Both authenticated editor clients reached the same running expedition with readiness published; stopping restored Edit Mode and cleared the runner. See [testing tools](testing-tools.md).
- [x] Make keyboard T toggle GO/STOP, retaining shared drive ownership and clearing the toggle on focus loss, blocked input, downing and input-source changes. Unity compilation and shared-rig, drive and quick-start checks passed. Physical lever behavior is unchanged. See [shared rig](shared-rig.md#toggle-t-shortcut--november-6).
- [ ] Retest physical keyboard presses and headset interaction; injected toggle checks and a real two-client launch do not establish full multiplayer driving acceptance.

## November 6 — minion side openings

- [x] Use the rear and both existing side railing gaps for bidirectional minion transfers, with connected ground landings, capsule clearance and target-aware route selection.
- [x] Actual motel Play Mode: both side exits/reboarding, blocked-nearest alternate route, all-openings-blocked hold, retry after clearing; rear/carry/speed/knock-off regression also passed. Expanded graph/packet suite: 489 checks, plus 125 track-crossing checks and navigation regression.
- [x] Two authenticated Photon clients: left exit/natural reboarding and right exit, plus moving carry, host migration, non-owner knock-off and moving leave/rejoin. See [enemy boarding](enemy-boarding.md).
- [ ] Target-headset review of side transfer presentation, crowded gaps and performance.

## November 6 — enemy track crossings

- [x] Remove permanent railway exclusions and rebake all 17 block graphs offline; preserve outer block boundaries.
- [x] Verify ground connections between both rail sides in every block and motel crossing paths; add train-relative occupied-node filtering and movement/shortcut checks.
- [x] Actual motel/minion Play Mode: natural empty-track pursuit, parked-train detour, reverse pursuit after departure, and a stale path replanning around newly occupied track. 125 graph/avoidance checks and existing boarding/navigation regressions pass. See [track crossings](track-crossing.md).
- [ ] Gameplay acceptance for incoming-train timing, multiple enemies, real-client cross-track pursuit and target-headset performance.

## November 6 — stopped-train dismount (report 4d54)

- [x] Add the missing train-to-block rear transfer, including clearance/ground checks, continued pursuit and reboarding.
- [x] Actual motel Play Mode: voluntary exit, renewed ground chase, reboarding, blocked-exit rejection, fast-train rejection and melee knock-off. Existing navigation, graph/packet and diagnostic regressions pass.
- [x] Add bounded per-enemy transfer state and transition events to future diagnostic reports.
- [x] Two authenticated clients on the reported motel route: committed voluntary exit, replicated block-graph pose, and diagnostic snapshot fields. Backend and local report 4d54 resolved with evidence; broader canyon transport tolerance failure is recorded separately in enemy-boarding.md.
- [ ] Headset review of dismount presentation and pursuit around crowded/obstructed exits.

## November 6 — minion train boarding

- [x] Author a separate train-local crawlgraph and opt the minion into collision-checked rear boarding; retain offline baking.
- [x] Prove natural approach/boarding, train-relative carry through acceleration/yaw/slope, deck pursuit and real melee knock-off in isolated Play Mode. See [enemy boarding](enemy-boarding.md).
- [x] Two real Photon clients: stationary/moving train-relative synchronization, moving host migration, non-owner melee knock-off and moving leave/rejoin. Transport fixture places minion aboard; natural two-client boarding remains acceptance work.
- [x] Isolated Play Mode: physical obstacle rejects approach, fast train rejects boarding, departure interrupts active transfer. Graph/packet/authoring suite: 390 checks; existing navigation regression passed.
- [ ] Validate hostile contact/revival aboard, multiple minions, natural two-client boarding, repeated runs, recovery in another block and adverse network conditions.
- [ ] Review boarding animation/readability, rescue balance, natural bat interactions and performance on target headsets. Desktop tests do not close this acceptance.
- [ ] Extend the proven moving-graph approach to spider wall/ceiling movement and stalking; minion-first implementation is authorized.

- [x] November 6: broadcast confirmed gold reward coin effects to other clients, using local watch and remote body targets; Unity compilation and existing economy checks pass.
- [ ] Verify on real clients that midpoint deposits show streams to every credited recipient for every observer, including simultaneous deposits, capped wallets and headset performance.

- [x] November 5: enable inherited held-coal locomotion in prefab and builder; verify imported configuration through Unity MCP.
- [ ] Verify coal ground-pushing/vaulting feel on headset and behavior with real multiplayer clients.

- [x] Prepare [nuclear power plant concept](nuclear-power-plant-layout.md) with a whole-site layout and nighttime atmosphere illustration. Concept deliverable only.
- [x] Draw [plant V2 floor plan](nuclear-power-plant-layout.md#v2--enterable-cooling-towers-and-waste-drum-yard): enterable cooling towers, hall/gallery and requested waste drums with a spilled truck load. Scaled drawing only; no implementation acceptance.
- [x] User approved plant V2 for 3D, adding tighter barrel groups and stacks. Install two enterable towers, turbine hall/gallery, utility yards and 130 drums with rough Quackville materials; bake navigation/lighting and register the sixteenth location. 3,185 geometry/navigation/UV checks, 803 catalogue checks, lighting export and two-seed offline route workflow pass. See [plant evidence](nuclear-power-plant-layout.md#installed-rough-blockout--november-5).
- [ ] Review plant tower/gallery traversal, low-eye lighting, loose-item recovery, enemy pursuit/rescue and real-client behavior on target headsets; measure Quest performance. Editor and offline checks do not close this acceptance.

- [x] Record user's rejection of rigid/widely spaced V5 container rows and prepare [V6 playground revision](container-yard-layout.md#v6--denser-playground-revision): double-depth clusters, offset stacks, tighter lanes and roof landings.
- [x] User approved V6 with the crane on the south side. [Rebuilt the yard](container-yard-layout.md#installed-v6-playground), repositioned loot/enemy markers and rebaked offline navigation/lighting. 2,215 geometry assertions, 757 catalogue checks, lighting export validation and 42 assertions across two route-loader Play Mode rounds pass. V6 evidence is separate from historical V5 evidence; headset/multiplayer acceptance remains below.

- [x] Produce [simplified Container Yard V3](container-yard-layout.md#v3--simplified-alternative-current-review-proposal) following user concern about build complexity: repeated modules, six ground interiors, two fallen containers, low roof routes and four-high backdrops. Drawing only; review before implementation.

- [x] Prepare the [Container Yard maze drawing](container-yard-layout.md), recording requested open/closed/fallen containers, climbable interiors and tall stacks. Drawing only; module dimensions and four-to-five-high heights remain proposals.
- [x] User approved Container Yard V5 for 3D; install wider six-row-per-side rough Quackville maze, hollow/fallen containers, low roof climbs, static gantry and reach stacker. Offline graph/lighting, 1,660 geometry/navigation/UV checks, 757 catalogue checks and 42 assertions across two route-loader Play Mode rounds pass. See [installed yard](container-yard-layout.md#installed-rough-blockout).
- [ ] Review container-yard natural hand climbing/descent, rescue and loose-item recovery, low-eye lighting, enemy pursuit, real-client behavior and Quest performance. Offline geometry and route proof do not close headset/multiplayer acceptance.

- [x] Prepare the [hospital district top-down concept](hospital-district-layout.md), including ground-floor rooms and supporting scenery across all three thirds. Drawing only.
- [x] User approved hospital district V2 site layout and specified two storeys plus accessible roof with ventilation, water tanks and HVAC.
- [x] Record hospital V4 direction: south ER, glowing hospital sign, crashed/overturned ambulances in ER parking and surgery-room replacements; retain two-storey/roof arrangement.
- [x] User approved hospital V4 for 3D; install rough Quackville hospital district, ground/upper/roof routes, surgery rooms, south ER, glowing sign and ambulance wrecks. Bake graph/lighting and register catalogue: 2,479 geometry/navigation/UV checks, 757 catalogue checks and 42 assertions across two offline Play Mode route rounds pass. See [hospital evidence](hospital-district-layout.md#validation-and-limits).
- [ ] Validate hospital natural stairs/hand traversal, roof/courtyard recovery, low-eye lighting, enemy pursuit, real-client behavior and Quest performance. Offline checks do not close these acceptance items.

- [x] Record separate skyscraper and hospital districts, with skyscraper first; deliver the [four-tower top-down layout and height section](skyscraper-layout.md). Drawing delivery only.
- [x] Revise the skyscraper drawing with requested elevator shafts, mostly open concrete floors, pillars and construction supplies; V2 is a drawing proposal, not implementation approval.
- [x] User approved skyscraper V2 for 3D; install four open towers, shafts, skybridges, static crane crossing and construction supplies with rough Quackville materials, offline graph/lighting and catalogue registration. 7,085 physical checks, 711 catalogue checks and 42 offline route Play Mode assertions pass. See [installed skyscraper](skyscraper-layout.md#installed-rough-blockout--november-4-2026).
- [ ] Review skyscraper stairs/shafts/crane in VR, rescue and loose-item recovery, real-client train clearance and enemy behavior, low-eye lighting and Quest performance. Offline evidence does not close these acceptance items.
- [x] Develop the separate hospital site layout; V2 approved, vertical detail study recorded above.

- [x] User approved the [underground station V1](underground-station-layout.md) for 3D with rough Quackville materials: descending tunnel, two-sided public halls, upper concourse, second-track walk-through wreck and connecting staff areas.
- [x] Install approved underground station rough Quackville blockout, graded railway, connected public/staff spaces and static wreck; bake navigation/lighting. 6,141 physical/navigation checks, 665 catalogue checks and two offline route rounds pass. See [evidence](underground-station-layout.md#verification).
- [ ] Review station traversal/boarding and low-eye lighting in VR; verify moving players/cargo on slopes, real-client late joining/authority changes, enemy pursuit and Quest performance.

November 4 compound art: approved the Quackville/depot material concept for implementation on the existing subway geometry. See [material pass](subway-compound.md#material-concept--november-4-2026) for authoring and evidence; this does not complete headset or multiplayer acceptance.

- [x] Apply compound Quackville/depot surface treatment and rebake: 253 surfaces, saved prefab appearance, atlas/UV/lightmap checks, 55 subway checks and session checks passed. Actual forward/low-eye/reverse captures are recorded in the compound guide.
- [ ] Review the compound material pass and retained lighting on the target headset; measure performance and twelve-player visibility/crowding.

November 3 art-test utility: implemented Page Down block skip using the existing player warp. Compilation and seven destination checks passed; live input/headset validation remains pending. See [shared rig](shared-rig.md).

## November 2 — Duckz fix ports

- [x] Correct coal interior selection in the shared grab path and align coal's Photon observation order with standard items; compile and pass focused editor checks. See [coal pickup maintenance](week1-session.md#coal-pickup-maintenance--november-2-2026).
- [x] Two authenticated desktop clients pass non-master interior selection, release and exactly-once furnace consumption stopped, moving and after master handoff. Evidence: `docs/validation/coal-pickup/`.
- [ ] Verify natural coal pickup on target headsets, including two non-masters with an active master and after master standby. Synthetic desktop selection does not close this acceptance.

- [x] Port the sixteen supplied Duckz revisions in focused commits, adapting movement, palm/hand input, train hand support, cargo releases and pickup recovery to Rails. Preserve Rails downing, shared rig and train cargo architecture. See [source mapping and validation](duckz-fix-ports.md).
- [ ] Complete target-headset acceptance for smoothed movement, calibrated controller/bare-hand interaction, hanging from the moving train, natural cargo throws/regrabs and LIV recording. Editor and synthetic transport checks do not complete this acceptance.

## Art direction deliverables

- [x] Install approved 20%-narrower Yard Mule with 4 × 10 m deck, matching collision/passenger bounds, one-quad textured floor and uniform square texels. Validate 6,720 triangle-edge UV metrics and 45 modular-train checks; see [evidence](yard-mule-train.md).

- [x] Build the approved [Yard Mule train shell](yard-mule-train.md), with pixel atlases, emissive markers and one roof spotlight; retain modular gameplay links and pass 45 modular-train editor checks.
- [ ] Review Yard Mule appearance, marker visibility, roof spotlight reach, natural boarding/hand contact, moving cargo and performance on headsets and real clients.

- [x] Revise convenience-store drawing to [V5 arcade and streetlights](convenience-store-layout.md#v5-arcade-revision--current-review-plan) following user selection; include arcade interior and road-lamp positions.
- [x] User approved V5 for 3D. Install rail-facing store/arcade, gas canopy, car wash, restaurant strip, glowing signs, road streetlights and outer scenery with rough Quackville materials. Offline graph/lighting, 1,328 geometry assertions, 757 catalogue checks and 42 assertions across two offline route-loader Play Mode rounds pass. See [implementation evidence](convenience-store-layout.md#v5-implementation--november-5).
- [ ] Review convenience-store V5 natural traversal, low-eye lighting/readability, enemy pursuit, loose-item recovery, real-client behavior and Quest performance. Offline checks do not complete these acceptance items.

- [x] Draw the [V4 convenience-store site and interior plan](convenience-store-layout.md#v4-top-down-revision--november-5) from the endorsed wide concept, including both railway sides, track-facing doors, gas canopy, garage/wreck yard, car wash, rest stop and outer scenery.
- [x] V4 footprint review superseded by approved V5 above; reconcile restaurants with east-side navigation bounds, retain spawn counts, rebake offline navigation/lighting and repeat physical/runtime validation.

- [x] November 5: deliver two [convenience-store revision concepts](convenience-store-layout.md#november-5-revision--rail-facing-storefront-and-glowing-signs), exploring the requested rail-facing frontage and glowing signs, plus proposed gas forecourt and supporting details. Generated studies only.
- [x] Select convenience-store additions and review revised top-down layout; V5 approved and installed above.

- [x] User approved Canyon V3 for 3D, adding multiple descending plateaus for exploration and enemy encounters. See [approved direction](canyon-layout.md#approved-3d-work--november-4-2026).
- [x] Install Canyon rough Quackville block, six connected landings, both bridges and shaft cave; register catalogue and bake offline graph/lighting. 3,699 physical/navigation/UV checks, 665 catalogue checks and 42 offline Play Mode assertions passed. See [evidence](canyon-layout.md#installed-rough-block-and-validation).
- [ ] Review Canyon natural hand locomotion, falls/cave recovery, narrow bridge/catwalk traversal, enemy encounters, real-client behavior and Quest performance. Rope reuse remains separate.

- [x] Approve and install Canyon V3 with the east pedestrian suspension crossing; physical deck/landing checks passed. Natural hand traversal/recovery remains in the headset acceptance item above. See [Canyon block](canyon-layout.md).

- [x] Install approved [mine V5 rough blockout](quarry-layout.md#installed-rough-blockout): staggered twin pits, long east excavation, raised west climb, three underground branches, office, crane and machinery; revise navigation/exclusion by height and rebake offline lighting. 1,798 geometry/navigation and 619 catalogue checks pass.
- [ ] Review mine hand-climbing, slopes, item recovery and low-eye lighting in VR; validate enemy pursuit/knockback, real-client late joining and Quest performance. Offline editor/route evidence does not close these acceptance items.

- [x] Carry Canyon V2 shaft/cave route into the approved V3 blockout and validate physical support, headroom and walk-out connection. Natural fall/rescue/item recovery remains pending; see [shaft proposal](canyon-layout.md#v2-shaft-route-proposal).

- [x] Prepare [mine V5 drawing](quarry-layout.md#v5--much-longer-right-quarry) with a much longer right excavation (proposed 110 m), extended haul ramp and repositioned crane/equipment. Geometry and validation remain pending.

- [x] Revise mine drawing to [V4 staggered pits and crane](quarry-layout.md#v4--staggered-pits-and-crane), retaining three branching underground connections and lit office. User endorsed V3's concept; V4 exact offsets/crane placement remain drawing proposals.

- [x] Review and build the approved [Canyon rough block](canyon-layout.md) with bridges, wall paths, worker routes, caves and creek. Catalogue, navigation and lighting checks passed. Raider Legends rope reuse, real-client/headset traversal and performance remain pending.

- [x] User approved Farm Silos V3 for 3D with apple trees. Install rough Quackville farm, four-level farmhouse and nine apple trees; preserve rail/loot/enemy contracts and bake offline navigation/lighting. See [implementation and validation](farm-silos-layout.md#installed-rough-blockout).
- [ ] Review farm stairs, basement recovery, attic headroom, hay-to-roof climbing/descent, corn visibility and apple-tree scale on headset; complete real-client and Quest performance acceptance.

- [x] Prepare the [expanded quarry site plan and terrain section](quarry-layout.md), with two pit exits and supporting scenery across the block. Drawing deliverable only.
- [x] Revise quarry to the requested [twin-mine V3 drawing](quarry-layout.md#v3--twin-pits-and-underground-branches): raised climb-over rim, three under-track tunnel branches, lit underground office and surface machinery/gravel. Drawing only.
- V3 was superseded by the approved V5 implementation above; headset/multiplayer acceptance remains separate.

- [x] Revise the [farm proposal to V3](farm-silos-layout.md#v3-farmhouse-revision), replacing northeast corn with a two-storey farmhouse plus basement/attic and a schematic section.
- [x] Apply approved farmhouse footprint and author rooms, stairs, basement excavation and attic headroom. Physical editor checks are separate from headset traversal acceptance.

- [x] Prepare the revised [Farm Silos top-down layout and hay-climb study](farm-silos-layout.md) with corn, water tower and full-block supporting scenery. Drawing only.
- [x] Obtain farm layout approval and build rough Quackville geometry with offline navigation/lighting. Headset/multiplayer/performance acceptance remains open above.

- [x] Prepare revised [convenience-store top-down site/interior proposal](convenience-store-layout.md), including outer-third scenery following the user's density direction.
- [x] User confirmed the expanded convenience-store layout; install rough Quackville blockout, store/restaurant interiors, road/crossing and outer-third scenery. Offline navigation/lighting, 1,087 physical/layout/UV checks, 619 catalogue checks and lighting exports pass. See [implementation](convenience-store-layout.md#installed-blockout).
- [ ] Review convenience-store scale, hand locomotion, loose-item recovery, atmosphere and performance on headset; complete multiplayer acceptance.
- All future block upgrades follow top-down drawing, user confirmation, then rough Quackville blockout. Populate supporting areas toward block edges; avoid empty outer thirds while retaining flat seams and clear railway passage.

- [x] November 4: add saved local **Invincible from enemies** Test Center checkbox. Compilation and authenticated solo checks cover enemy contacts, bomber explosion, runtime toggles, private-run gating and preserved Down self. See [evidence and limits](testing-tools.md#enemy-invincibility-in-test-center); headset/multiplayer acceptance remains separate.

- [x] November 4: add saved Test Block Order prefab list to Duck Rails Tests; support variable-length private review routes. Compilation, 59 focused editor checks, 29 diagnostic checks and an authenticated solo three-block run passed. See [workflow and evidence](testing-tools.md#test-block-order--november-4-2026).
- [ ] Verify variable-length test routes with real peers, late joining/host migration and target headsets; single-editor evidence does not close multiplayer acceptance.

- [x] Draw expanded campground V2 with tree clusters and campsite/shore landmarks across the approach and departure areas, following the user's permission to use former empty buffers. See [expanded proposal](lakeside-campground-layout.md#expanded-layout-proposal--november-4). Drawing only; installed greybox remains V1.
- [x] Implement user-approved expanded campground V2 with rough Quackville textures, relocated buildings, 73 trees, three campsite areas and shore/overlook/footbridge landmarks. Navigation/lighting rebuilt; 2,055 physical and 619 catalogue checks pass. See [V2 evidence](lakeside-campground-layout.md#installed-expanded-campground-v2).
- [ ] Review expanded campground paths, lake/gully recovery, prop scale and full-width hand clearance on headset; profile the denser vegetation and complete multiplayer acceptance.

- [x] November 4: apply motel Quackville rough materials and subtle perimeter banks as requested. Offline navigation/lighting rebuilt; surface, bank-support, layout and catalogue checks pass. See [material and elevation evidence](roadside-motel-layout.md#quackville-rough-materials-and-edge-elevation). Headset feel and Quest performance remain pending.

- [x] November 4: prepare [nine remaining scavenging-block top-down studies](remaining-block-layouts.md), including proposed hills, berms and quarry benches following the user's elevation request. Drawing deliverable only.
- [ ] Review these block proposals before greyboxing; measure graded paths, maintain level track/flat seams, rebake navigation and lighting, and verify low-eye visibility, loose-item recovery and headset traversal.

- [x] November 4: prepare the selected campground layout with a lake, low terrain rises and flat perimeter connections. See [campground plan](lakeside-campground-layout.md). Drawing deliverable only.
- [x] Install approved campground greybox and requested rough Quackville materials: irregular lake basin, raised cabin pads, graded paths, flat perimeter, preserved track/spawns, offline navigation and lighting; 1,052 physical and 619 catalogue checks pass. See [evidence](lakeside-campground-layout.md#editor-verification).
- [ ] Review campground shoreline/item recovery, full-width path and low-eye traversal in VR; validate multi-client play and Quest performance. Bright editor captures do not establish final gameplay visibility.

- [x] Revise the motel whole-block drawing with the requested transformer yard, machine rows and broken chain-link perimeter. See [V3 layout](roadside-motel-layout.md#v3-transformer-yard-addition). Drawing deliverable only; greybox and traversal acceptance remain pending.

- [x] November 4: prepare Roadside Motel scaled site, ground/upper floor and service-building plans following the user's selected layout direction. See [motel plans](roadside-motel-layout.md). This completes drawings only.
- [x] Implement the user-approved motel greybox, west office/garage and transformer yard with broken fencing. Offline navigation/lighting exports, 176 physical/layout checks, 619 Week 3 checks and an authenticated solo private-run loading check pass. See [implementation and MRUK startup-error notes](roadside-motel-layout.md#installed-greybox).
- [ ] Review motel stairs, balcony guards, ajar doors, room scale, transformer aisles and boarding in VR; complete multi-client and Quest performance acceptance.

- [x] Apply the user-approved depot rough material direction to existing V3 geometry using the Quackville Town/Industrial atlases. Preserve collision, track and gameplay layout; rebake lighting for the new surfaces. Further model work and final atmosphere tuning remain separate.
- [ ] Review depot material texel scale, repetition, dark-surface readability and performance on the target headset before final art.

- [x] Validate Bright Lighting / Gameplay Lighting test controls: editor preselection, live panel button events, rendered fog/brightness changes, exact fog/ambient restoration and unchanged baked textures passed in an authenticated private depot run. Headset readability/reach remains separate. See [lighting preview](testing-tools.md#lighting-preview).

- [x] November 3: deliver v3 depot site and two-floor blueprint studies following the user's scale feedback, including proposed enlarged footprints, rooms, props and aligned stairs. Drawings only; see [review set](maintenance-depot-prototype.md#november-3-review-v3-drawings-only).
- [x] User approved the V3 blueprints for greyboxing. Implement enlarged footprints/heights, both-floor rooms, six stairs, galleries, guards and placeholder props on the actual depot prefab; rebake navigation and lighting. Physical stair/headroom, ground-only mezzanine connectivity, doorway/yard access, 607 Week 3 checks and lighting export checks pass. See [V3 evidence](maintenance-depot-prototype.md#v3-greybox-implementation).
- [ ] Review V3 stairs, upper-floor loops, room visibility and hand clearance in VR before the first art pass.

- [x] November 2: select Maintenance Depot for the first location prototype and establish the requested sequence: rough renders and labeled overhead layout, layout review, Unity greybox, then first art pass. See [depot prototype](maintenance-depot-prototype.md). Layout details remain proposals; this does not complete greybox or art acceptance.
- [x] Install the authorized scaled depot greybox on its actual block prefab: exact building/area footprints, simple collision shells, flush paths, fencing, preserved track curve, rebaked navigation and lighting. Compilation, 607 Week 3 checks and geometric/export checks pass; see [depot evidence](maintenance-depot-prototype.md).
- [ ] Review depot distances, heights, door clearance and locomotion in VR; revise the greybox before the first art pass. Editor checks do not complete this acceptance.

- [x] November 1: install the first Compound baked-lighting/probe sample with UV mesh copies, scene-owned lighting settings, subtle fog and a baked reflection probe. Existing geometry/binding checks pass; see `docs/subway-compound.md` for authoring limits.
- [ ] Review Compound lighting and probe transitions on standalone Quest with twelve players; establish measured frame-time/memory budgets before expanding lighting density. Small expedition-block samples are already implemented separately.

- [x] November 1: create and install the modular subway compound blockout with rough pixel textures, open store, shallow fountain, sealed stairs and retained session controls. Editor geometry checks and solo DuckPlayer departure passed. See [subway compound](subway-compound.md).
- [ ] Review subway layout/lighting in Quest 3 and with twelve compound players; complete finished art, ambience, commerce presentation and performance profiling. The blockout does not close these acceptance items.
- [x] November 1: implement V2 concept-fidelity geometry: 18 m occupied floor, former side boarding edges with recessed tracks and improvised barriers, narrow viewing gaps, and 7 m original ceiling above low shelters. All 55 geometry checks and session rule checks pass; dimensions and actual Unity views are recorded in `docs/subway-compound.md`. V1 assets are preserved.
- [ ] Review V2 against the concept in headset and approve final dimensions; produce the final annotated plan/section and calibrate concept-matched cameras before finished art. Current coordinate/section notes and approximate cameras are tuning aids, not visual acceptance.

- [x] Build and install the modular Unity train blockout (October 30): main train prefab in Expedition, four nested station prefabs, separate shell pieces, hollow furnace, relocated coal acceptance, existing drive/horn/radio interactions, headlight and bounded fire/smoke. Geometry/binding checks and all eleven quick regression suites passed; two authenticated clients passed stopped/moving/post-handoff coal consumption. The Week 3 builder preserves the edited train prefab. See [implementation and evidence](train-production.md#unity-blockout--october-30-2026).
- [ ] Review the blockout on target headsets for control reach, natural low sightlines, rear/side boarding and eight-player circulation; profile lights and particles. Finish production meshes and atlas mapping after layout review.

- [x] Deliver five-view train turnaround and proposed 16-tile material atlas in the [production study](train-production.md): front, back, right side, top and rear-right three-quarter; two-page supplement visually reviewed. Station sides and camera-facing details refined. Generated concepts only, not exact projections from a shared mesh; UV mapping, padding and production texture settings remain pending.

- [x] Clarify the furnace construction in concept revision 5: actual mouth, hollow cube interior and separate fire particle FX. Retain the approved larger opening and tall stack. Updated [concept package](train-production.md), visually reviewed the revised image/PDF page, and recorded future collision/insertion constraints; no Unity implementation claimed.

- [x] Deliver furnace art revision 4: enlarged single opening and stack three times the prior concept height above the casing, retaining thickness. Saved standalone image and updated five-page [train package](train-production.md); changed PDF pages visually reviewed. Supersedes the old 0.5 x 0.5 m opening target; final dimensions and implementation remain pending.

- [x] Deliver [train study revision 3](train-production.md) (October 30): furnace and radio outside the cockpit, straight furnace stack into open sky, approved simplified radio, centered seat/throttle with fuel left and horn right, no displayed rear bags, and open rear boarding without a transverse rail. Revised top-view and cockpit concepts plus four-page PDF visually reviewed. Earlier storage-hook requirements remain to reconcile; no Unity implementation or ergonomic acceptance claimed.

- [x] Revise the [train production study](train-production.md) to a four-page v2 PDF (October 30): simplified receiver, retained mic-base TALK button, front headlight, center seat with throttle ahead, two side lamps, and one larger furnace with a single approximately 0.5 x 0.5 m mouth and connected thick stack. Decorative coal and hanging lamp removed. Images and PDF visually reviewed; generated orientation/spacing remains illustrative. No models or Unity changes; rig reach, sightlines, seat behavior, particles and lighting acceptance remain pending.

- [x] Create the [train production study](train-production.md): three-page PDF with deck layout, provisional heights, turnaround views, and the required table-mounted radio/mic with TALK button at the mic base (October 30). PDF visually reviewed. Drawing delivery only; model dimensions, interaction reach and headset acceptance remain unverified.

- [x] Record user approval of the low-poly train render and the three latest night location renders as the visual direction (October 30). Production geometry, interaction fit, headset lighting and performance remain unverified.

- [x] Produce darker night location concepts in the endorsed low-poly style: Maintenance Depot, Roadside Motel and Lakeside Campground (October 30). Images and prompts are saved in the [concept index](art-concepts.md); no production scene or lighting acceptance is claimed.

- [x] Revise the train concept toward DuckZ's low-poly, pixel-textured asset style using the supplied references; update Art Guide v0.3 (October 30). Generated image only, with no 3D/Unity asset or performance claim. See [concept index](art-concepts.md).
- [ ] Carry the confirmed DuckZ style through production meshes and texture treatment, then profile the assembled route on Quest 3.

- [x] Deliver initial [rough 2D concepts](art-concepts.md): an open-air train sheet with low-eye-line study, Maintenance Depot and Roadside Motel (October 30). Visual exploration only; no 3D/Unity changes, production design approval or headset acceptance.

- [x] Create the initial [art guide](art-guide.md), with an editable source and six-page PDF reading copy (`output/pdf/duck-rails-art-guide.pdf`); all PDF pages visually reviewed October 30. This completes the guide deliverable, not approval of the proposed look or in-game art acceptance.
- [ ] Develop train concepts and a Maintenance Depot lighting sample using the guide; establish an authoring workflow that protects art from builder regeneration.
- [ ] Apply the October 30 mostly open-air train direction, with optional light enclosure at the front. Review a section drawing and in-rig views at approximately 0.5 metres above the deck; verify low windows, front bodywork, rails and cargo preserve visibility of the track and nearby hazards without standing or climbing. Exact dimensions remain to be validated.
- [ ] Validate the finished train/depot art slice on Quest 3 before expanding the treatment across the route.

## October 31 — gold economy

Confirmed scope and validation are tracked in [gold-economy.md](gold-economy.md). This supersedes the old no-wallet and free train gear requirements.

- [x] Local economy implementation and bounded validation: seven-item shops, gold spawners, train safe, persistent-reward queue, backend welcome grant/admin controls. Twelve editor suites (35 economy assertions), solo intake/pull checks, three-client expedition regression, Go tests and admin build passed. See the validation record for manual test setup and limits; this does not close deployed economy or headset acceptance.
- [ ] Validate first-wallet 100 coins exactly once, persistent balance and admin credits against the deployed Rails backend.
- [ ] Verify both shops with all seven standard gear types, physical pull line, success, insufficient funds and restocking on real clients/headsets.
- [ ] Verify shared shop payouts, safe count/irreversible intake, escape-only safe payout, failure, loose/held exclusions, reconnect and master handoff.
- [ ] Apply additive economy SQL and deploy API/admin images to Duck Rails; record production evidence separately.

## Planning basis

- [x] October 31 fuel-exhaustion coast implemented: deterministic deceleration from exhaustion speed, preserving empty moving checkpoints and refuelling continuity. Runtime/editor compilation, 13 fuel/coast checks and Unity Session Checks passed; Console error query empty. See [evidence](week1-session.md#fuel-exhaustion-coast--october-31).
- [ ] Verify fuel-exhaustion coasting and loose cargo across real clients, late joining/master loss, and target headsets.

- [x] October 31 editor/desktop throttle convenience: hold T for GO, release for STOP in Keyboard or VR mode, retaining shared lever ownership. Unity compilation, 28 shared-rig checks, 17 drive checks and a solo live Photon/lever hold-release check passed. See [evidence and limits](shared-rig.md#hold-t-throttle-shortcut--october-31). Actual headset and multi-client shortcut acceptance remain separate.

October 30 shared-rig follow-up: replace the DesktopPlayer gameplay launch path with simulated poses/buttons on the existing DuckPlayer; preserve Quest Link and allow input switching during Play. The accepted controls and verification workflow are in [shared-rig.md](shared-rig.md). Older DesktopPlayer evidence below remains historical and does not establish acceptance of the shared rig. Headset and multi-client acceptance remain required.

Shared-rig editor evidence: all 11 quick suites and the solo real-frame hand/button/lever check passed. Two authenticated editor clients then passed stopped/moving-train coal and master-handoff checks (21:53:42 UTC). [Recorded evidence](shared-rig.md#recorded-editor-evidence-october-30). Worn Quest Link switching and target-headset acceptance remain open; these editor results do not close the headset milestone boxes.

October 30 Voice Patrol configuration: integration retained, with GameManager's **Enable Voice Patrol** startup switch off by default. Bootstrap and initialization require explicit opt-in. This does not change Photon voice requirements or complete multiplayer/headset voice acceptance.

Validation: source diff/whitespace checks passed. Unity MCP initially verified `X:/Work/duck-rails`, then became unavailable during script reload (no fresh discovery files), so post-change editor/Play Mode verification remains pending. The diagnostic inbox query failed DNS resolution; three local reports remain open and were not resolved by this configuration change.

October 30 testing-tools follow-up: [testing-tools.md](testing-tools.md) records the development panel, report capture/upload pipeline, automatic state checks, presets and unified runner. Two real desktop clients passed the dashboard-launched coal/handoff test. Production backend deployment and Quest panel acceptance remain pending; this tooling does not close the existing multiplayer/headset acceptance boxes.

Build on Duckz's working framework. Keep Photon Realtime. Implement room/session architecture before the full expedition, with a small real moving-platform/shared-item test rather than an empty-room-only prototype.

Scheduling proposal: four-week closed alpha, six-week public V1. This is a milestone sequence, not a guaranteed estimate. Team capacity, source dependencies, and platform submission lead times are unknown. Weeks assume existing systems are portable and some content work can proceed alongside engineering. Re-estimate after source inspection and after the first session milestone. Do not silently fund a networking rewrite by shortening verification.

## Phase 0 — source and project readiness

Deliverable: runnable baseline plus a verified reuse inventory.

October 28 authentication progress: separate Unity and simplified backend copies are confirmed; client/backend sign-in compatibility changes compile and local backend tests pass. See `authentication-setup.md`. Justin subsequently confirmed backend and Photon authentication setup completed. This milestone targets DesktopPlayer first; headset acceptance is deferred. The broader readiness items below are not complete.

Analytics follow-up: separate Duck Rails Android keys are now entered, inherited gold polling is removed, and Rails session events are instrumented. Editor previews are local only. Actual dashboard delivery and Windows platform configuration remain pending; see `analytics-setup.md`.

- [ ] Obtain the intended Duckz source location and confirm whether this repo will be a full project fork, an imported subset, or another supported arrangement.
- [ ] Read source/project guidance before copying or changing anything. Preserve the original Duckz project.
- [ ] Record Unity version, render pipeline, target headsets, SDK/package versions, required native plugins, and build steps.
- [ ] Identify actual Realtime/PUN APIs, voice stack, room manager, authentication, item authority, player identity, save/commerce services, and scene loading.
- [ ] Locate locomotion, helicopter/platform motion, backpacks, grabbing, enemies, crawlgraph tools, flare, and cosmetics.
- [ ] Build and run the unchanged baseline on at least two intended headsets.
- [ ] Record known defects separately from regressions introduced by Duck Rails.
- [ ] Establish ignored local configuration/secrets, asset and large-file handling as appropriate to the actual project.
- [ ] Confirm release platform, account/app identifiers, audience/voice eligibility, and store preparation requirements early.

Acceptance: a clean checkout can be configured and built using documented steps; two clients reproduce the known working interactions. Update the design's reuse inventory with actual component paths and limitations.

Do not copy credentials into tracked documentation. Do not claim systems verified merely because they compile.

## Week 1 — session architecture with real state

Deliverable: friends can form a crew, depart, join halfway through, see the same moving platform and objects, and return together.

### Decisions confirmed October 28
- Standalone Login art and random camera spots; authenticate/connect/join before transitioning to Compound. No Quackville use.
- Twelve-player public compounds, automatic initial compound placement; eight-player expeditions.
- First platform occupant starts twenty seconds. Entry/exit cannot reset a nonempty timer. Empty cancels; re-entry starts twenty seconds again.
- Photon Master Client coordinates changes and passes responsibility on departure; initial private creator launches manually.
- Authenticated identity; 120-second expedition reconnect reservation while the room remains alive; shared items persist without restoring duplicate disk-saved supplies.
- Public return after success or failure: crews of one to four connected players try an existing compatible compound with whole-crew reservations and bounded fallback to a fresh compound; crews of five to eight use a fresh compound. Keep one coordinated destination for the crew. Runtime implementation is present; multi-client acceptance remains pending.
- All gameplay rooms use five-digit numeric codes; the private station joins public or private rooms by code. Automatic creation handles occupied codes without matching an unrelated crew.
- Private staging/run/return retain the same room and code. Last connected player leaving destroys the room immediately.
- DesktopPlayer is the current test target. Backend/Photon setup is reported complete; multiple actual clients are still required for network acceptance.

### Implementation and evidence
Private replay follow-up (October 29): fixed the reproduced four-supply duplication by requiring old networked cargo to be removed before staging readiness and restart. Cleanup runs on each item's controlling client and includes inactive objects. Eight authenticated solo rounds/seven immediate replays passed with success/failure and inactive bag/bat coverage. A subsequent two-client check passed four rounds/three replays with distinct authenticated accounts, both supplies owned by the non-master, and active/inactive cargo: both clients observed zero old supplies before staging readiness and exactly two fresh supplies per run. See `week1-session.md` for repeat instructions and evidence. Actual held/stowed interactions, disconnected owners, master-loss and headset acceptance remain unchecked.

Public return follow-up: `RailCrewReturn` keeps a temporary authenticated coordination connection while one gameplay client searches and reserves an existing compound. Search failure/timeout or coordinator loss selects a shared fresh allocation. Five-to-eight-player crews skip the search. Reservation cleanup uses retained per-user deadlines and a compare-and-swap update to preserve concurrent crews. Schema 4 separates this protocol from older clients. Local policy/lease checks and authenticated single-client return evidence are recorded in `week1-session.md`; the group acceptance boxes remain unchecked.

October 30 stability follow-up: three authenticated desktop processes passed four public cycles with two departing crew and one shopper left behind, adverse-network train controls, master transport timeout/automatic rejoin, unique bound avatars, cargo cleanup and voice-room membership. Scene-transfer avatar duplication, reconnect destination loss and a cached-avatar binding race were fixed. All 547 editor checks passed. See [multiplayer-stability.md](multiplayer-stability.md) for evidence. One/four/five/eight-player capacity cases, return-coordinator loss, VR/audio quality and full public gameplay acceptance remain open; these tests do not close the broad boxes below.

Fuel consumption follow-up: motion and starting fuel are retained atomically in schema 3 room properties, with deterministic distance-based consumption and a zero-fuel stop. Runtime/editor compilation and six new local regression checks passed; two-client fuel agreement, leave/rejoin, and master-departure acceptance remain pending. Coal insertion is not implemented by this change.

See `week1-session.md` for source paths, controls, validation results and pending multi-client evidence. Implemented work is kept separate from milestone acceptance; unchecked scenarios below remain unverified until exercised across real clients. Desktop follow-up adds the tagged Meta-account window from the referenced Classic commits, a Player2-configured Two Ducks scenario, a compact centered status banner and carriage-relative footsteps. Local regression checks passed; two distinct accounts subsequently authenticated together during the October 29 cleanup check.

### Work

- Editor testing convenience implemented: **Two Ducks Quick Start** routes the Two Ducks editors directly into one hidden expedition after normal authentication. Main-editor selection now reads the active scenario configuration rather than its unreliable runtime tags. Six regression checks and the runtime/editor build passed; live main-editor quick-start entry is verified. Justin confirmed the combined two-client launch works. Live disabled-toggle behavior still requires verification. This does not change production boarding requirements or satisfy the acceptance scenarios below.
- [ ] Define session phases: compound/staging, departing, running, ending, returning. Define valid transitions and who can request/commit them.
- [ ] Give each expedition a run identifier so completion and transfers can be deduplicated.
- [ ] Build a gray-box public compound and occupancy/countdown platform.
- [ ] Freeze and validate the departing roster at zero; transfer only that roster and keep the source compound usable.
- [ ] Implement separate private creation with five-digit input and server-confirmed collision handling.
- [ ] Distinguish Create from Join; show occupied, missing, full, incompatible, and connection-failure states.
- [ ] Keep private staging/run/return in the same room where compatible with existing architecture.
- [ ] Support late entry to the run with a loading stage before spawn/interaction.
- [ ] Replicate actual moving-platform pose, sample fuel value, selected test block, one bat, and one loaded backpack.
- [ ] Define current-state reconstruction instead of depending on historical events a newcomer did not receive.
- [ ] Implement temporary success/failure controls and group return.
- [ ] Extend public return for crews of one to four: coordinate an existing-compound search, reserve the whole crew, and fall back to a fresh compound within a bounded time. Keep fresh-compound return for crews of five to eight.
- [ ] Handle departures during transfer, interrupted loading, and loss of coordinating client without an indefinite loading state.
- [ ] Verify voice reconnects/routes correctly through the session changes.
- [ ] Perform an early loose-item acceleration/braking experiment to expose a separate major risk.

### Acceptance scenarios
- [ ] Seven players can leave while an eighth remains shopping in the compound.
- [ ] Repeated platform entry/exit cannot reset a nonempty group's timer indefinitely.
- [ ] Two users attempting the same private code do not silently create/join the wrong expedition.
- [ ] A friend enters a public expedition code at the private station and appears aboard the correct train; exercise automatic code collisions and crew transfers.
- [ ] A friend enters a running private code and appears aboard the correct moving train with current world state.
- [ ] A full run rejects a ninth player cleanly.
- [ ] Reconnection does not duplicate the player's bag or supplies under the selected policy.
- [ ] Coordinating-client departure produces the documented recovery behavior.
- [ ] Crew returns together; private code remains usable while the room remains alive.
- [ ] Public success and failure returns: crews of one and four enter an existing compound with sufficient unreserved capacity; insufficient capacity, simultaneous arrivals and search failure fall back without splitting the crew. Five-player crews use a fresh compound. Verify coordinator loss during search and transfer, and reservation cleanup.

Schedule gate: if transfers or state reconstruction are unreliable, continue this milestone and update the release forecast. Do not build many scenes around a provisional lifecycle.

## Week 2 — one complete scavenging stop

Deliverable: a networked departure, coal search, rescue, and escape using actual Duckz interactions.

### Current commissioned scope — confirmed October 29

Implementation and three-client DesktopPlayer evidence are recorded in [week2-session.md](week2-session.md). Checked implementation items indicate the scoped desktop delivery; eight-player, headset, subjective interaction feel, and human voice-intelligibility acceptance remain separate below.
- Complete Week 2 plus the bridge finale described under Week 3, including its automatic dawn ride and a shared world-space results board for success and failure. Other Week 3 content expansion and commerce are excluded; persistent escape-count implementation remains later work.
- Treat Week 1 as the user-verified working baseline. Preserve its behavior and test regressions introduced by this work without reopening the entire milestone.
- Develop and validate using DesktopPlayer with up to three configured authenticated clients, Unity MCP, and Unity CLI/batch checks. Meta Quest 3 is the launch target; Justin will perform VR testing afterward. Eight-player and headset acceptance are not established by these desktop checks.
- Duck Rails Unity and backend repositories may be changed as needed. Duckz and Classic remain read-only references.

Additional finale/results work in this scope:
- [ ] Review October 31 floating results placement in-game: backing removed from the train prefab, text centered at local Y=4 m / Z=9 m, and legacy authoring updated. File structure/reference checks passed; Unity and headset visual validation remain pending.
- [x] Add advance bridge warning signs and require manual braking. Hitting the raised or incompletely lowered bridge explodes the train and fails the team; no protective automatic stop.
- [x] Complete the Week 3 bridge-button, timed defense, synchronized lowering, finish snapshot, catch-up, and automatic dawn-ride tasks.
- [x] Show the same world-space run-stat canvas after success and failure, with **SUCCESS** or **FAIL** at the top and **Escaped** or **Didn't make it** for every participant.
- [x] Derive overall success from at least one escaped player. Zero escapes, including a pre-finish bridge crash or full-crew-down failure, produces failure.
- [x] Test one-player escape with teammates left behind, downed passengers escaping, zero escapes, bridge crash, full-crew-down failure, and results/group return on both outcomes across the available desktop clients. Three-client private sequences passed; see Week 2 evidence.

- [x] Integrate train platform movement and STOP/GO lever with consistent shared state.
- [ ] Validate the October 29 VR grip-and-hold lever on Quest 3 with two clients: trigger-only handle/base without locomotion pushback, exclusive control, forward travel, pull-back OFF/re-engage while gripping, release braking, disconnect/downing cleanup, and preserved desktop toggles. Implementation does not establish headset acceptance.
- [x] Add physical horn control
- [x] Add coal insertion, consumption, fuel gauge, and fuel exhaustion behavior. coal should act the same as any item. it can be held, dropped, put into bag, if you drop, anyone else can pick it up.
- [x] Make fuel insertion authoritative/idempotent so one coal chunk cannot be counted twice.
- [x] Revalidate non-master furnace insertion on two desktop clients after the October 30 failure: found stale duplicate-avatar lookup, fixed selection of the live pose, and verified normal hand release/physics, stopped/moving insertion, exactly-once +2.5 fuel/removal and explicit master handoff. Fixture player teleports position the hand. Headset and bag-to-hand furnace coverage remain pending. The later stability pass fixed duplicate creation during scene transfer and added PUN-registry uniqueness checks across three clients; see `multiplayer-stability.md`, `week2-session.md` and `validation/coal/`.
- [x] Implement stable dropped cargo, including loaded bags, while moving, braking, and changing item ownership.
- [x] Add simple physical holders and full-size backpack hooks using existing interaction patterns.
- [x] Integrate handheld radio communication with a permanent train station.
- [x] Build one gray-box scavenging block using the existing crawlgraph tooling.
- [x] Use one existing enemy type with temporary retreat on hit and no train access.
- [x] Implement one-hit down, immediate flare launch (no intentional delay), and three-second proximity revive. No dead state or bleed-out: downing persists until teammate revival or quitting. No timed automatic revival.
- [x] Decide crawling, repeated flare behavior, post-revive protection, and a minimum viable full-crew failure rule.
- [ ] Confirm locomotion/flashlight/melee/backpack feel has not regressed.
- [x] Historical Week 2 default supplied 10 fuel and twelve items. October 31 supersedes the free items: retain 10 fuel, buy equipment at departure/midpoint stores.

Acceptance:
- [ ] Eight players can collect coal, revive a teammate, board, and leave without developer intervention.
- [x] Dropped bats/coal/bags stay with the train; intentionally thrown items leave it.
- [ ] Repeated grabs and acceleration do not produce divergent ownership or cargo positions.
- [ ] Enemies never board and cannot make every boarding point unusable.
- [ ] Revive completion/interrupt and flare lifetime agree across clients, including a late joiner.
- [ ] Distant handheld users and players at the train station can communicate intelligibly.

Record device, client count, network conditions, and reproducible failures. Test beyond a single LAN/editor setup using the existing supported simulation tools where available.

## Week 3 — full expedition and repeatable content workflow

Lighting reference: [bake/export workflow, probes, fog, troubleshooting and validation](block-lighting.md). November 1 user review reports that the lighting works; target-headset performance remains a separate acceptance item.

- [x] November 1 StartingStation baked-lighting workflow: dedicated bake scene, prefab renderer bindings, additive probe-only companion, translation and unload/reload proof in Play Mode. Updated Week 3 checks pass (463); see `docs/block-lighting.md`.
- [x] November 1 full catalogue lighting samples: four baked lights on each of twelve scavenging blocks plus finale (52), exported lightmaps/probes, route-wide readiness and finale probe relocation. Three offline routes cover every block with binding/sample/teardown checks; authenticated single-editor Expedition also passes. See `docs/block-lighting.md`.
- [ ] Profile complete-route lighting load/tessellation, texture memory and moving-subject quality on Quest; validate lighting during multi-client late join and repeat expeditions before increasing lighting density.

- [x] October 31 fixed starting station: separate editable prefab, centered train spawn, five randomized stops retained, continuous station/route track and updated bridge distance. Unity compilation and 462 Week 3 checks passed, plus assembled-scene inspection. See `week3-session.md`.
- [ ] Validate station departure, boarding and late joining across real clients and headsets. The solo sandbox launcher failed before entering gameplay; editor evidence does not close this acceptance item.

- [x] October 31 left-side layout follow-up: add two buildings to each of the twelve scavenging blocks and distribute every coal variation three per side, retaining six chunks total. Unity compilation, 456 Week 3 checks and representative depot/campground visual inspection passed. Existing enemy patrols remain on the right; headset layout/feel and crew-size balance remain unverified. See `week3-session.md`.

- October 31 authoring improvement: all twelve scavenging blocks now use visible nested CoalSpawner prefabs and spread-out coal positions (12 m measured minimum within each variation). Unity compilation and 504 Week 3 checks passed; this is editor evidence, not new multiplayer/headset acceptance. See `week3-session.md`.

Deliverable: a complete start-to-finish run assembled from authored blocks.

Confirmed scope: twelve gray-box locations, five distinct randomized stops plus the bridge finale, three authored spawn variations per location, roughly twenty seconds or more between encounter areas, and a ten-minute typical-run target. Separate Expedition scene; keep all selected blocks loaded, including collision/gameplay. Internal track bends return to the common connection. Small baked-lighting samples now cover the catalogue; final lighting remains pending. See [week3-session.md](week3-session.md) for implementation and evidence; desktop checks do not close headset or unfamiliar-player acceptance.

- [x] Define a stable block format: geometry, graph, track anchors, stop marker, spawn candidates, identifiers, and route tags.
- [x] Create a repeatable editor template, invoke existing bake tooling, and validate missing graph/markers and track alignment.
- [x] Verify graph coordinates after placement; choose constrained placement if arbitrary rotations add avoidable complexity. Editor checks verify translated graph indexing and disconnected components; runtime placement is translation-only.
- [x] Select the route once through session authority and share the exact identifiers/placements with late joiners. Three authenticated desktop clients agreed after host handoff and leave/re-entry.
- [x] Choose the initial block lifetime: load all five selected blocks plus finale and retain them until return. Target-device profiling remains pending; runtime baking remains excluded.
- [x] Avoid intermediate block unloading so players and dropped supplies remain available for the whole run.
- [x] November 5: implement local scenery visibility for the player's current block and its immediate neighbors, retaining collision/gameplay and baked lighting. Three offline route builds passed 260 assertions; compilation was confirmed again after the user-reported editor crash. See [evidence and limits](block-visibility.md).
- [ ] Validate block visibility on headsets and real clients: measure frame-time benefit, review skyline/seam pop-in, split-party backtracking, late join and replay. The editor crash cause remains unestablished; offline assertion results do not close stability or device acceptance.
- [x] Author all twelve gray-box scavenging prefabs with three spawn variations each (supersedes the two-block initial scope). Individual gameplay playtests remain pending.
- [x] Build the original raised-bridge finale: external button and increased local enemy spawns. November 1 supersedes its unattended timer with a continuous hold; see [bridge hold](bridge-hold.md).
- [ ] Validate the November 1 hold-to-lower finale across real clients and headsets: release saves/pauses, another operator resumes, overlapping holds retain normal speed, downing/disconnect pauses, late join/master change retain progress, and completion stays down. Three authenticated desktop clients passed physical release/pause, resume, master handoff, late rejoin, completion, success/replay/crash and cleanup; a solo physical fixture also verified downing pauses. Natural solo/unarmed balance, concurrent operators, operator disconnect and headset acceptance remain open. Initial holding duration is 30 seconds; enemies remain off the train. See [evidence and limits](bridge-hold.md).
- [x] Synchronize bridge timing and gate passage until fully lowered; handle repeated presses and Master Client changes without restarting progress.
- [x] At bridge entry, atomically capture per-player finish status before any catch-up teleport, then disable driving controls and commit automatic forward travel.
- [x] Teleport only left-behind participants aboard as non-finishers without escape credit; downed players already aboard finish normally. Catch-up teleport does not revive players.
- [x] Add the far-side tunnel and concealed dawn transition. Resolve automatic-ride fuel behavior so the ending cannot stall.
- [x] Keep walking, voice, and horn available during the victory ride; falling off returns the player aboard after a few seconds without changing recorded finish status or reviving a downed player. Decide whether falling itself downs a standing player; do not introduce a dead state.
- [ ] Complete fuel/resource balancing, downtime, and failure/restart behavior. Current budget is 0.05 fuel/metre and +2.5/coal, with six chunks per stop; three-client zero-fuel recovery, private success/replay/crash cleanup pass. Natural ten-minute pacing and crew-size tuning remain pending.
- [x] Add basic in-world onboarding and departure feedback. Compound, train, stop and bridge labels are present; unfamiliar-player comprehension is still unverified.
- [ ] Ensure private and public sessions can repeat full expeditions. Private scene transitions/replay pass on three authenticated desktop clients; four public session cycles with temporary success/failure controls also pass. A full public gameplay run and headset acceptance remain pending; see `multiplayer-stability.md`.

Acceptance:
- [ ] New players understand boarding, fuel, revival, and escape without developer coaching.
- [ ] Selected blocks connect correctly and contain usable navigation without runtime rebaking.
- [x] A late joiner receives the same route and relevant current objects. Three-client desktop leave/re-entry preserved route, cargo, loaded backpack and bridge clock; adverse-network/headset coverage remains separate.
- [ ] Success, failure, and replay all return to a usable group flow.
- [ ] One-player and eight-player runs are feasible under the intended design; tune toward intermediate group sizes as well.
- [ ] Dawn feels like relief and renders acceptably on the actual headset pipeline.
- [ ] All clients agree on bridge activation/completion and cannot drive through the raised bridge.
- [ ] Standing and downed onboard players both finish; standing and downed off-train players both receive did-not-finish. Verify teleporting aboard never changes these results or revives anyone.
- [ ] Bridge entry commits finish results and automatic driving only once, including during authority changes and reconnection.
- [ ] Catch-up teleports and victory-ride falls/returns never grant, revoke, or duplicate escape credit.
- [ ] Non-finishers share the ride and receive an explicit did-not-finish label. Players can walk, talk, and honk; falling off respawns them aboard without blocking crew return.

## Week 4 — external alpha / conditional release candidate

Deliverable: a coherent small game that outside friend groups can play repeatedly.

- [ ] Improve sound, lighting, attack cues, and safe boarding based on observations.
- [ ] Add results with a small stat set and humorous awards; handle ties and zero-action players.
- [ ] Implement bridge-entry finish snapshots and did-not-finish labels on the world-space board. Resolve remaining late-arrival, disconnected-player, onboard-boundary, and ending-stat rules. Downed onboard players are confirmed finishers.
- [ ] Persist escape counts once per eligible player/run; verify duplicate messages or reconnects do not grant duplicates.
- [ ] Reuse existing cosmetic browse/equip/purchase infrastructure in the compound.
- [ ] Adapt direct platform IAP item purchases, verification, transaction deduplication, and ownership/restoration. Keep cosmetic purchases separate from the October 31 earned-coin gameplay wallet. Define product types before implementing consumable handling.
- [ ] Verify voice during radios, horn use, scene changes, and eight-player conversations.
- [ ] Profile actual target devices; set concrete frame-time, memory, and network budgets from the project baseline and platform target.
- [ ] Check footage readability: players, threats, and physical jokes must remain visible in recordings.
- [ ] Conduct external playtests with unfamiliar players and multiple consecutive runs.
- [ ] Prioritize bugs that strand crews, break ownership, corrupt progression, or block replay.

Acceptance: several consecutive sessions succeed without developer recovery, common failure paths return a usable UI, and no known frequent blocking defects remain. A four-week public release is conditional on this evidence and platform readiness; otherwise ship a closed alpha.

## Week 5 — content variety and player feedback

Deliverable: launch content set and improved replayability.

- [ ] Expand toward four scavenging blocks only after the first two work well.
- [ ] Vary authored supply/enemy placements and select existing enemy combinations that produce distinct situations.
- [ ] Tune fuel and enemy pressure across crew sizes; avoid eight bats eliminating all tension.
- [ ] Refine compound onboarding, private joining, awards, and feedback.
- [ ] Add a small themed cosmetic selection if existing tooling makes it low cost.
- [ ] Consider spotlight only after core reliability is proven.
- [ ] Evaluate whether optional emergency bat fuel adds useful recovery; it is not required for launch.

Acceptance: groups understand how to meet, survive, return, and replay; route variety produces different decisions rather than merely shuffled scenery.

## Week 6 — stabilization and launch

Deliverable: tested public V1 and a practical recovery/hotfix process.

- [ ] Freeze features and address launch blockers.
- [ ] Test full rooms, simultaneous code creation, late arrivals, network loss, authority changes, transfer failure, and repeated rounds.
- [ ] Verify cosmetic purchases/restoration using platform-supported test accounts and tools.
- [ ] Verify escape persistence across sessions and interrupted saves.
- [ ] Finish device performance, comfort, and recorded-footage checks.
- [ ] Complete actual store/account/age/voice requirements and release assets.
- [ ] Confirm Photon CCU/traffic plan, authentication, and graceful capacity errors against current account settings.
- [ ] Document build/release steps, diagnostic collection without sensitive data, known limitations, and rollback/hotfix procedure.

Do not assume external platform review completes within this week; initiate prerequisite work during Phase 0.

## State and integration checklist

This is a behavior contract, not a mandate to replace Duckz's networking architecture.

- Session: room type/privacy, phase, countdown/roster, run identifier, transition destination.
- World: selected blocks, transforms, spawn selections, loaded stage.
- Train: pose/motion, control state, fuel, and necessary persistent attachments.
- Objects: identity, location, holder/owner, held/stowed/loose state, bag contents, consumed state.
- Players: identity, participation/reconnect status, downed/reviving state, spawn eligibility.
- Enemies/flares: current relevant state and lifetimes rather than replaying obsolete triggers.
- Finale: bridge activation/time/progress, passage gate, enemy pressure, and committed automatic train ride.
- Results: immutable per-player finish/non-finish records captured before catch-up teleports, final stats, awards, completion credit, and victory-ride respawn state.

For each category, identify the existing serialization path, who may mutate it, how a late joiner learns it, what happens when its owner leaves, and how repeated requests are handled. Keep transient sounds separate from lasting state while restoring ongoing effects when appropriate.

## Highest risks and required evidence

1. **Unknown reuse boundary:** prove baseline builds and record source dependencies before estimating import work.
2. **Late joining/room transfers:** test with real changing state, interrupted connections, and group departures.
3. **Moving cargo:** repeated acceleration, drop/grab/throw, authority changes, and loaded bags on actual clients.
4. **Crawlgraph placement:** validate transformed graph/geometry alignment and no runtime bake work.
5. **Eight-player VR:** measure frame time, memory, traffic, voice clarity, and physical crowding.
6. **Difficulty:** one-hit down and unkillable enemies must create recoverable rescues rather than immediate chain failures.
7. **Persistence/commerce:** reuse established ownership paths and deduplicate completion rewards.
8. **Scope:** use a simple finale and limited blocks before optional content or new tools.

## Four-week scope reduction

Retain: room architecture, public/private departures, late joining, reliable moving cargo, core scavenging/rescue, dawn ending, and basic repeat-play flow.

Reduce first:
- Two scavenging blocks instead of four.
- One or two existing enemy types.
- Coal only; no emergency alternative fuel.
- No spotlight.
- Three stats and a few awards.
- Existing cosmetics with minimal new art.
- Keep the bridge finale to one external hold-to-lower button with retained progress and local enemy defense; no extra fuse/battery puzzle. Replacing it with a plain escape requires a design discussion rather than an automatic scope cut.
- Keep public return matchmaking simple: one coordinated, bounded search for crews of one to four, then fresh-compound fallback; no elaborate room-ranking system.
- Repeatable block template and validation rather than an elaborate generator/editor suite.

If mandatory systems still do not meet their acceptance tests, extend the schedule. Do not relabel a known unreliable build as release-ready.

## Immediate next development session

1. Locate Duckz source and confirm intended import/fork strategy.
2. Establish baseline and populate the reuse inventory with real paths.
3. Settle the minimum session/reconnect policies listed under Week 1.
4. Implement one public and one private session path with placeholder expedition controls.
5. Prove late joining against the actual moving platform and shared bag before broad content production.

All checklist items are initially unverified. Update them only with concrete implementation and test evidence.

Radio/horn follow-up (October 29): physical hold buttons implemented with release-to-stop station transmission and looped horn audio. Runtime/editor compilation passed; editor checks, Quest 3 feel, and two-client release/disconnect/late-join/audio acceptance remain pending. See week2-session.md.

- [ ] November 1 mixed enemy acceptance: validate 4–6 minion/bomber/smasher/spider encounters across real clients, late joining, host migration, bomber respawn and headsets. See [mixed enemies](mixed-enemies.md).

November 4 atmosphere follow-up: removed hardcoded night ambient/fog resets; saved the user's current Expedition settings. Unity compilation and six direct editor atmosphere comparisons passed. Full-run/headset appearance remains unverified; validation limitations are recorded in docs/block-lighting.md.

Starting-station art planning: split-platform direction selected; detailed platform/bridge plans and tunnel/clearance section prepared from existing block and train measurements. Drawing review complete; greybox implementation and VR traversal/boarding acceptance remain pending. See [station layout](starting-station-layout.md).

- [x] Install the approved underground split-platform station greybox on StartingStation; rebake/export lighting and fit the existing departure shop. 326 physical/track/lighting checks and Week 3 checks pass. See [station evidence](starting-station-layout.md#installed-greybox).
- [ ] Review station stair traversal, bridge scale, both-side boarding and shop use in VR; moving-player clearance and final mountain art remain pending.

- [x] Apply the requested Quackville rough station material pass: 215 surfaces, 57 atlas-UV meshes, rebaked/exported lighting and 326 passing physical/track/lighting checks. See [station materials](starting-station-layout.md#quackville-rough-material-pass).
- [ ] Review station material texel scale, repetition, dark readability and performance in VR.

- [x] November 5: install approved farm V5 west farmhouse/orchard, extensive dense corn, no perimeter lanes and elevation/details; rebake offline graph and lighting, pass geometry/catalogue/export checks.
- [ ] Farm V5: validate dense-corn rendering cost and navigation/readability on target headsets, alongside existing farm traversal and multiplayer acceptance.

- [x] Simplify farm corn geometry by 36.4%, disable corn shadow casting, preserve dense field layout; verify saved prefab and geometry/catalogue/lighting checks.
- [ ] Measure optimized corn GPU time on target headset in gameplay fog; decide whether distance culling or LODs offer worthwhile savings.

- [x] November 5: install approved coal pickup art (44 triangles, 32x32 pixel texture), preserve existing pickup physics/network configuration and rebuild grab surface. Unity compile and 927 existing grab-surface assertions passed. See [coal art evidence](art-concepts.md#coal-pickup-art--november-5-2026).
- [ ] Review new coal silhouette/readability in nighttime gameplay on headset and natural grabbing/ground-pushing with the retained simple collision envelope.

- [x] November 5: implement 2,000-coin wallet cap in client/backend and small grey watch cap line with yellow balance at maximum. Unity prefab checks and backend service/repository/router tests pass; see [wallet cap](gold-economy.md#wallet-cap--november-5-2026).
- [ ] Deploy matching wallet-cap API/client; verify persisted cap, concurrent rewards, retries after spending and headset watch readability against the live service.

- [ ] November 6 both-side encounters: validate navigation/pursuit, mixed enemy counts, late joining/master handoff and headset balance across the expanded catalogue. Unity verification omitted at user request; see [implementation](mixed-enemies.md#enemies-on-both-rail-sides--november-6-2026).

- [x] November 6: install both-side enemy markers and offline crawlgraphs in the 12 one-sided encounters, retain the other five bilateral layouts, update the scene finale and guarantee both-side seeded selection. Authoring completed; gameplay acceptance remains open above.
