# Skyscraper Works — V2 layout

## Enemy quadrant coverage — November 8, 2026

User requested enough candidates for the new spawning-only crew tiers. Added eight
enemy prefab markers through the approach/departure yards, two per quadrant,
preserving all 18 existing candidates, including elevated tower positions. Pool is
now 26 enemies; 150 coal and 150 gold are unchanged. Existing geometry, transforms,
graph and lighting remain unchanged; no rebake. All crew sizes across 100 seeds
reach their 4/8/12 targets with 15 m horizontal spacing. The prefab remains open
for inspection; placement taste, natural gameplay and headset performance are pending.
See [placement and saved-selection evidence](enemy-spawn-balance.md#requested-marker-additions).

## Spawn placement first pass — November 8, 2026

User requested about 150 coal, 150 gold and 18 enemies across the floors, weighting gold toward harder-to-reach areas. Replaced 42 old markers with exactly 150 coal, 150 gold and 18 enemy prefab candidates. Existing per-run counts remain 6/2/5 and selection logic is unchanged.

Coal: six on each of twenty tower floors (supply piles and structural corners), 24 on outer-yard supplies and six on elevated crossing decks. Gold: 3/4/5/8/10 candidates per tower at 0/4/8/12/16 m, plus twelve skybridge, seven crane-deck, one crane-machinery, eight shelter-roof and two yard-stash candidates. Of 150 gold candidates, 112 are at least 8 m high and 84 at least 12 m high. This rewards longer vertical/exposed routes; height is not proof of VR difficulty. Enemies have four candidates per tower distributed between ground and upper floors, plus two outer-yard locations; all five floor heights are represented across the pool.

Verified in the Duck Rails editor: support, four-corner loot footprint, minimum spacing and spawn-envelope overlap checks passed during placement. Enemy candidates fit current bounds and lie within 0.8 m of baked ground navigation. Saved-prefab reference checks, 100 seeds per type, bilateral enemy selection and navigation passed (636 checks); overlap positive control detected a concrete pillar. Preserved all 3,459 environment transforms and the graph reference. No geometry, lighting, navigation bake or runtime code changes.

Saved and left open in Prefab Mode for user inspection. Placement taste, natural VR traversal/hand reach, item settling and real-client/headset acceptance remain pending. User subsequently requested committing and pushing this pass. No diagnostic reports were inspected.

November 4, 2026. User approved V2 for 3D. The rough Quackville blockout is now installed; see implementation and validation below. Earlier drawing dimensions remain design history where superseded by the installed geometry.

## Confirmed direction

The user selected separate skyscraper and hospital blocks, with skyscraper first. Multiple construction towers can surround the railway, connected by open-air skybridges including crossings above the train. A fallen crane connecting towers is a requested exploratory idea. This supersedes the shared-block assumption in [the previous study](construction-hospital-layout.md).

## Proposed drawing

[V2 top-down plan and height section](../output/imagegen/skyscraper-layout-v2/skyscraper-plan-v2.png) | [Editable V2 drawing script](../output/imagegen/draw-skyscraper-layout-v2.ps1)

### V2 — shafts and open construction floors

User confirmed elevator shafts, mostly open skyscraper floors, concrete pillars and building supplies strewn around the floors. Represent the building as exposed concrete slabs and a structural column grid, with only localized stair/shaft cores rather than finished offices or continuous facade walls.

The V2 drawing proposes paired shafts per tower, in an 8 x 6 m core toward the north side of each footprint. Count and dimensions remain proposals. Shaft openings, barriers, bottom recovery and any climbable service route need design review; working elevator cars are not established by the shaft request. Keep stairs as independent return routes.

Scatter timber, pallets, pipes, rebar bundles and cement bags in irregular, floor-specific authored clusters. The drawing uses schematic supply markers; exact prop positions and pillar clashes must be resolved in detailed floors. Keep continuous circulation, stair landings, shaft approaches and bridge/crane landing areas clear. Random-looking clutter does not require runtime randomization or changes to networked loose-item behavior. Mostly static construction dressing is the proposed default.

Move the A-side crane landing south of the new shafts, to approximately (-28,68); its D endpoint remains (25,91). These are proposed horizontal coordinates at Y+16 m. The previous V1 sheet is retained in `output/imagegen/skyscraper-layout-v1/`.

96 x 160 m envelope, track X=0/Y=0, +Z departure, proposed flat 6 m perimeter. Four 26 x 24 m towers surround the track: A southwest (X -38..-12, Z54..78), B southeast (X12..38, Z54..78), C northwest (X-38..-12, Z86..110), D northeast (X12..38, Z86..110). Main footprints remain predominantly in the middle third. Site offices, workshop, loading, materials, delivery, formwork, generator and tool shed occupy the outer thirds toward the boundaries.

- A-B: open-air bridge, 4 m wide, Z64..68, deck Y+8 m.
- A-C and B-D: side bridges, 4 m wide across the 8 m gaps, deck Y+8 m.
- C-D: open-air bridge, 4 m wide, Z96..100, deck Y+12 m. Internal stairs in C/D connect the two loop elevations.
- A-D: optional fallen lattice crane crossing diagonally above the railway at Y+16 m, from approximately (-28,68) to (25,91) in V2. Proposed fixed wreck with a broad supported walking strip inside the truss; not a moving crane or mandatory balance beam. Detailed support, landing and truss geometry remain to design.
- Each tower has two independent stair cores. Proposed accessible landings are 0/4/8/12/16 m; upper structural silhouettes could extend to 24–32 m. Final height and playable upper floors remain unapproved. The drawing overlays connections at different heights rather than showing a single floor.

The main route can run A → B → D → C → A using stairs to change bridge levels. The crane adds an optional A-D shortcut. Ground entrances and exterior return lanes remain available independently; no crane crossing or jump is required to return to the train. Open air means no roofs; guards/edge treatment and actual stair layouts need a detailed pass.

## Clearance and future acceptance

Reserve X -7..7 for the railway at ground level; no columns, bridge supports or fallen debris inside that reserve. The lowest bridge proposes a 0.5 m structure with underside Y+7.5 m. The earlier [station measurement](starting-station-layout.md#measured-references) recorded the train mesh reaching +5 m above rail, excluding riders/particles. This reference is not a current collision sweep or proof of clearance. Verify the actual train, stack, passengers, hands and carried objects before implementing the spans.

Bridge navigation must be represented at its real elevation without creating enemy access to the operating train corridor. Authored offline crawlgraphs and lighting, stair headroom/hand clearance, recoverable falls/items, rescue routes, real-client behavior and headset performance remain future validation. No new fall-damage rule, runtime baking or dynamic crane simulation is implied.

## Delivery evidence and next gate

The reproducible System.Drawing script generated the PNG; the exported plan and height section were visually inspected. The user subsequently requested the 3D build. Hospital remains a separate block.

## Installed rough blockout — November 4, 2026

- Prefab: `Assets/DuckRails/Blocks/skyscraper-works-v1.prefab`, displayed as **Skyscraper Works**. Registered as the fourteenth scavenging location in Expedition; the original catalogue builder preserves this additional block. It is available through the existing Test Block Order selector.
- Four 26 x 24 m open concrete towers, each with accessible floors at Y=0/4/8/12/16 m, two switchback stairs and paired hollow elevator shafts. Unfinished structural frames reach 24/28/32/24 m. Those upper frames are skyline geometry, not additional finished floors.
- Shafts remain hollow through the upper floors to Y=0. Their ground openings allow recovery; upper openings have temporary guardrails. No working elevators were added.
- Four 4 m-wide open-air skybridges: A-B and both side links at +8 m, C-D at +12 m. The lowest underside is +7.5 m. Partial construction edge guards preserve bridge portals.
- Static fallen crane with a 3 m-wide walking deck and lattice sides at +16 m. Final endpoints are **(-20,16,62) to (25,16,90)**: moved from the drawing to clear shafts, upper stairs and tower guardrails. A supply pallet was removed from its landing. Pillars near cross-track bridge entrances were offset to preserve circulation.
- Concrete pillars, exposed slabs, timber stacks, cement-bag blockouts, pallets and pipe bundles decorate the floors. Placement is deterministic and varies by floor/tower. These are static construction props; the existing shared-item system is unchanged.
- Approach/departure yards contain shelters, stockpiles and worklights, with flat perimeter seams and a straight level railway. Rough materials reuse Quackville concrete, timber, weathered paint and gravel.
- Three authored variations retain six coal (three per side), six candidate enemy markers and two gold markers, distributed across floors. Existing selection still produces 4–6 mixed enemies. Twenty tower levels are connected by offline navigation; the lower central railway corridor is excluded by height while the skybridges remain navigable.
- 24 baked worklights, 3,394 lightmap receiver bindings and a probe-only lighting companion registered in Build Settings. Runtime Expedition atmosphere settings were preserved.

### Validation

- Unity MCP verified `X:/Work/duck-rails/Assets` before editor operations; scripts compile and final Console error/exception query was empty.
- [7,085 physical/navigation/UV checks](validation/skyscraper/geometry-checks.txt) pass: floor and stair support/headroom, turn/top landings, bridge/crane walking widths and transitions, hollow shafts with ground exits, flat seams, spawn support, navigation and texture/lightmap UVs. Railway checks sweep a 5.8 m-wide, 6.3 m-high clearance box through the block. This is geometric evidence, not a moving-client test.
- [711 catalogue checks](validation/skyscraper/week3-checks.txt) pass. Initial navigation-origin mismatch was corrected to (-40,-2,32), aligned to the shared 16-cell grid. The installed graph contains 31,196 nodes.
- [Ground-only reachability](validation/skyscraper/ground-connectivity.txt) connects all 20 floor samples; climbing-only connectivity is not the basis for this result.
- [Lighting export validation](validation/skyscraper/lighting-export-checks.txt) passes, including receiver bindings and build registration.
- [Two offline assembled-route Play Mode rounds](validation/skyscraper/runtime-proof.txt), seeds 0 and 7, pass 42 assertions. Both include Skyscraper Works plus Container Yard, Lumber Yard, Scrapyard and Tunnel Works, station and finale. Checks cover readiness, bindings, translated probe samples, finale relocation, route replacement and complete teardown. Each round loaded 13 lightmaps and 2,464 probes. These are real route-loader checks, not Photon or headset tests.

### Review and repeat

[Overview](../output/skyscraper/overview.png) | [Railway view](../output/skyscraper/rail-corridor.png) | [Open floor and shafts](../output/skyscraper/open-floor.png) | [Crane](../output/skyscraper/crane.png). These editor captures use temporary review lighting; they do not alter gameplay atmosphere.

Open `Assets/DuckRails/Lighting/Blocks/skyscraper-works-v1/skyscraper-works-v1-bake.unity` to inspect. For gameplay, select **Skyscraper Works** in Duck Rails Tests → Test Block Order and use the existing private test launcher. Bright Lighting can help review structure. Authoring source: `Assets/Editor/RailSkyscraper.cs`; physical checks/captures: `Assets/Editor/RailSkyscraperChecks.cs`. The saved `Assets/DuckRails/SkyscraperWorks/RuntimeProof.unity` repeats the offline two-round loading proof when played.

Pending: natural VR stair/hand movement, shaft falls and item/rescue recovery, crane/edge traversal, enemy pursuit and knockback, moving train/cargo/passenger clearance across real clients, low-eye gameplay visibility and Quest frame-time/memory. The rough block has 3,394 renderers; headset profiling and any needed geometry batching remain art-performance acceptance work. No full feature/headset acceptance is claimed by this installation.

## Approved yard polish — November 11, 2026

User approved [the top-down plan](../output/imagegen/skyscraper-yard-polish/yard-polish-plan.png), then explicitly limited geometry work to the north/south yards. Existing tower geometry is retained: the installer compared all 3,088 tower transforms, plus the railway route, before saving.

- South: two enterable office trailers, warm windows, desks/chairs, covered break table/benches, parked cars, toilets, CutePixel development sign, delivery flatbed, loading shelter and material stacks.
- North: shallow foundation excavation with two wide ramps, footings/rebar, static excavator, mixer truck, maintenance shelter, aggregate and formwork. Short fences, pipe sections, muddy tracks and puddles dress the outer areas. Materials reuse the rough Quackville palette.
- Existing 150 coal and 150 gold candidates retained; append 120 of each type: per tower, 10 on +12 m and 20 on +16 m. Final pools: **270 coal / 270 gold / 26 enemies**. Per-run quantities and selection logic unchanged. Yard markers were adjusted for replaced shelters/supply geometry. More upper-floor candidates increase selection probability; individual floors are not guaranteed loot.
- Scoped authoring: `Assets/Editor/RailSkyscraperYardPolish.cs`; installed additions under `Env/Approved yard polish`. Do not rerun the original whole-block installer. New mesh assets have combined renderers by material with authored UVs; collision remains separate.
- Validation: [install preservation/support checks](validation/skyscraper-polish/install.txt), [1,000 seeded selection cases](validation/skyscraper-polish/selection.txt). Editor compile passed. Navigation rebaked; final dressing lighting export and saved validation recorded below when complete.
- Natural traversal, pit recovery, item settling, nighttime visibility, multiplayer/headset performance: **queued for playtest**, not verified.

Final editor evidence: navigation and lighting rebaked/exported after pipe/puddle dressing; [600 saved checks](validation/skyscraper-polish/saved-checks.txt) pass, including the 240 new loot envelopes/native markers, overlap positive control, railway clearance, seams, enemy navigation and saved lighting bindings. [Ground-only connectivity](validation/skyscraper-polish/yard-routes.txt) reaches both office interiors, the pit bottom and both ramps. Console error/exception query empty. Review captures use temporary bright lighting and are not nighttime/headset evidence. `git diff --check` reports Unity-generated empty `m_Name: ` whitespace only; no script/document whitespace errors. No Play Mode was launched.

## Workbench repair and brighter construction lighting — November 11, 2026

User reported floating workbench tops in the southeast shelter and supplied warm/cool nighttime construction-lighting references. Added 32 grounded legs and 16 cross braces beneath all eight workbenches across the south loading and north maintenance shelters. Preserved current Prefab Mode edits before saving.

Existing tower spotlights increased from 65 to 95 and yard lights from 55 to 90. Added four top-floor accents (130), four warm yard floodlights (210) and two cool fills (150). All lights are baked; global Expedition atmosphere and shared moonlight stay unchanged. Tower structure and loot pools are unchanged. Authoring entry point: `RailSkyscraperYardPolish.FixBenchesAndLight()` (one-time scoped patch).

Navigation rebuilt for bench legs/floodlight bases. Final bake and editor evidence: `docs/validation/skyscraper-polish/bench-lighting-followup.txt`. Nighttime balance, glare, natural movement and headset performance remain queued for playtest.

Follow-up result: 32 grounded leg checks and ten baked-light checks passed, along with the 600 saved geometry/loot/navigation/reference checks. Navigation and lighting exports completed, Console error/exception query empty. Inspected baked [bench close-up](../output/skyscraper-polish/fixed-benches.png) and [lighting overview](../output/skyscraper-polish/brighter-lighting.png); these use the authoring bake with a dark camera background, not a gameplay/headset run. Existing dark gaps remain between local pools of light. Queued review includes final brightness taste/glare.

## Rooftop tripod supports — November 11, 2026

User requested grounded tripod stands beneath the four floating upper-floor lights. Added four telescopic poles with short mounting arms, collars, three splayed legs and rubber feet. Poles sit slightly beside each lamp to avoid blocking its downward beam. All 12 feet are supported by the existing +16 m floors and footprints avoid the existing loot envelopes. Light positions/settings, tower structure and spawn counts retained. Authoring entry: `RailSkyscraperYardPolish.AddRooftopTripods()`. Evidence: `docs/validation/skyscraper-polish/rooftop-tripods.txt`. Natural movement/headset inspection queued with the yard polish.

## Bridge and crane guide lights — November 11, 2026

User requested small baked point lights along the inter-building bridges and fallen crane. Added 23 warm point lights with small rail-clamped housings: five on each cross-track bridge, two on each short side bridge, nine on the crane. Lights alternate sides at roughly 4–6 m intervals, intensity 8, range 6.5 m, baked soft shadows. Placement uses the current deck transforms, including the user's slightly raised crane. Fixtures are noncolliding; existing transforms, spawn pools and offline navigation unchanged. Authoring: `RailSkyscraperYardPolish.AddCrossingPointLights()`. Evidence: `docs/validation/skyscraper-polish/crossing-point-lights.txt`. Lighting rebake/export and visual review recorded there; headset brightness/glare review queued.

## Foundation floodlight repair — November 11, 2026

User requested replacing the unsupported foundation light with a grounded tripod, retaining cool white-blue light and aiming forward over the whole foundation plus some of the excavator. Replaced the old downward fixture/mast with a three-legged stand at the southwest excavation corner (-41,0,119), head height 4.5 m, aimed toward (-26,-0.1,138.5). Retained the existing cool color and added a matching emissive lens. Baked spotlight: intensity 850, range 40 m, 115-degree outer cone, 85-degree inner cone. All three feet have ground support; four pit corners and the excavator body fit the cone/range. Removed only the old mast's 12 triangles from its shared rebar mesh; rebar and pit geometry retained. Initial mid-edge placement failed cone coverage before prefab save; corner placement passes. Authoring: `FixFoundationFloodlight()`. Evidence: `docs/validation/skyscraper-polish/foundation-tripod.txt`. Natural traversal and headset lighting review queued.

## Concrete side boundaries — November 11, 2026

User requested continuous concrete side fencing, approximately 3 m tall, with unstretched pixel textures, and a reusable inset based on Farm. Farm's saved boundary posts are at X +/-47 on ground extending to +/-48: **1 m centerline inset**. See [the reusable side-fence standard](side-fences.md).

Installed two solid walls at X +/-47, Y0..3, thickness 0.5 m, full length Z0..160. Each side is one continuous concrete box; railway entry/exit stays open. Collision uses Environment BoxColliders. Dimensions are baked into the render mesh and planar UV0 repeats every 3 m on both axes, with separate lightmap UVs; 72 UV triangle edges passed the equal-density check. Preserved the user's current prefab edits and all pre-existing transforms. Authoring: `AddConcreteSideWalls()`. Navigation/lighting rebuilt; final saved checks recorded in `docs/validation/skyscraper-polish/side-walls.txt`. Headset and actual wall-contact acceptance queued.
