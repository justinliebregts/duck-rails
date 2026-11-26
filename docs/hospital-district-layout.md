# Hospital district — top-down proposal

November 4, 2026. The user approved the hospital drawings including the V4 south ER/surgery changes and requested the 3D build. The rough Quackville district is now installed; see implementation and validation below. This is a separate district, superseding the earlier combined construction/hospital study.

## Neon crosses and track-facing signs — November 26

The six ambulance crosses are painted red. The two hospital facade crosses, the entrance pylon and both pharmacy crosses use red channels with a bright glass outline. Each fixed cross has its own baked red point light, 1.25 m in front of the face, with a 4.8 m range. [Hospital entrance](../output/hospital-night-signs/neon-entrance.png) · [Ambulance apron](../output/hospital-night-signs/ambulance-and-crosses.png)

The three west-side building names sit on the east-facing boards over their track-side doors. The WAITING board hangs 0.33 m lower, with 2.17 m underneath and raised lettering on both sides. [Clinic](../output/hospital-night-signs/outpatient-clinic-from-track.png) · [Pharmacy](../output/hospital-night-signs/pharmacy-from-track.png) · [Laundry](../output/hospital-night-signs/central-laundry-from-track.png) · [WAITING from reception](../output/hospital-night-signs/waiting-from-reception.png) · [WAITING from the corridor](../output/hospital-night-signs/waiting-from-corridor.png)

## Windows, raised signs and laundry — November 26

All four glazing and reflection swatches have zero emission. Amber and cyan panes retain their tint in reflected light. All 35 visible signs use solid CutePixel letter meshes with baked emission. The facade and ambulance crosses use red emissive atlas faces. The entrance monument and pharmacy crosses retain their ivory glow. The hospital bake has 30 active fixtures, 1,387 bound renderers on one lightmap, and 398 probes in the build companion. [Baked entrance](../output/hospital-night-signs/hospital-front.png)

Central Laundry's four boiler-side washers moved 4.35 m north along the west wall, toward WASH. Their collision and the coal pickup moved with them. The DANGER HOT lettering and backing plate are gone. [Baked laundry](../output/hospital-night-signs/laundry.png)

All 176 low bush placements use the corn rustle event while the local listener moves through their footprint. Edit Mode checks covered every bush at head height, silence while stationary, and clear samples on the garden path and railway. Nighttime readability and listening are queued for playtest.

The hospital geometry suite passed 3,187 checks, including support and clearance for the washer pickups. Its one failure is the existing eight-player coal capacity. The block has 64 coal candidates against the current ceiling of 93. [Geometry checks](validation/hospital/finished-campus/geometry-checks.txt)

## Darker hospital lighting — November 26

In the B project, 31 of the 61 active baked lights are off. The other 30 use half their previous intensity, with pools of light at the ER approach, on both hospital floors, on the roof and in the outbuildings. The 512 × 512 lightmap's mean linear luminance fell from 0.252 to 0.088, about 65%. Unity reloaded all 1,352 receiver bindings against the new atlas, and the build companion loaded all 398 probes. Nighttime visibility at duck eye height is queued for playtest.

## Hospital sound — November 24

Four localized loops mark the reception and upper north strip lights, middle rooftop HVAC unit and laundry washer bank. Water drips at the upper surgery scrub sink and the clinic exam sink. The roof duct seam and laundry pipes occasionally creak. The drips and creaks are positional and intermittent. The scrub sink has the shortest interval (14–32 seconds) and the rooftop duct has the longest (55–105 seconds), leaving quiet stretches as players explore the abandoned campus.

Four reverb boxes follow the two-storey hospital around the open courtyard. Clinic, pharmacy and laundry each have their own response. Pharmacy has the shortest decay (0.55 seconds) and laundry the longest (1.35 seconds). Saved-prefab checks resolved all eight emitter references through to their audio clips. Reverb sampling found 11 indoor positions inside the fades and 580 courtyard, railway and roof positions outside them. Listening is queued for playtest.

## Spawn distribution and round trees — November 23

The hospital prefab has 64 coal candidates, 80 gold candidates and 32 enemy starts. Loot covers the staff garden, ambulance approaches, pharmacy, clinic, laundry yard, visitor transit, ground-floor departments, upper wards and roof equipment. Each quadrant has eight enemy candidates, including three existing railway starts across the block. Review the placements using Test Block Order. The hospital remains outside normal random rotation.

The existing balance selects 6 coal, 10 gold and 4 enemies for a solo crew, rising to 31 coal, 52 gold and 12 enemies for eight players. In Edit Mode, all 176 saved candidates passed support and clearance checks against collision and visible geometry. Enemy starts also passed bounds and existing crawlgraph checks. Across 100 seeds at each crew size, the selectors met their quotas with distinct markers and at least 15 m between selected enemy starts.

All four foliage sections on each of the 21 round trees now have a downward-facing base in their original Quackville leaf material. Upward rays confirmed all 84 caps on the imported meshes. The prefab totals 157,507 triangles across 1,388 active mesh renderers, including world text. All 3,180 hospital checks passed. The lighting export has 1,352 valid receiver bindings on one atlas. [Unity view from below](../output/hospital-district-repairs/tree-underside.png).

## Hospital District repairs — November 23

The campus now uses worn Quackville Town and Industrial tiles on the ground, envelope, glazing, equipment and signs. The campus color and emission atlases import at 1024 pixels with point filtering and mipmaps. Shell materials use repeating crops, and interior lenses and screens have hospital-local textured materials. The exterior builder trimmed overlapping faces on 469 rectangles and the interior builder trimmed 74. Turf and paving end at the building footprints, and the ballast has its own strip through the grass.

Both hospital stair cores have steel guards around the openings, flight handrails, landing posts and stringers. Stair lights mount to the slab soffits, rooftop lights have posts, and the interiors have working vertical supports for fixtures, hanging signs and furniture. Pharmacy cartons have all six faces. The redundant facade panel and its MEDICAL CENTER lettering were removed to expose the crosses. The separate garden sign remains. At Central Laundry, the pitched roof seats on enclosed gables. Shutters and a solid south wall enclose the linen racks. The raised south dock was removed and the east entry grades into the yard.

Ground collision is independently taggable by finish. Turf uses Grass, ballast and planting soil use Dirt, and asphalt, concrete, garden paths, stair treads and landings use Stone. The steel guards and laundry sheet roof use Metal. The saved prefab has 322 explicit contact tags. The Edit Mode checks cast onto the actual surfaces, including the narrow curb beside a planter and the east laundry ramp.

All 2,901 Edit Mode checks passed. The lighting export contains 1,352 valid receiver bindings on one lightmap. All 507 campus meshes and 13 furnished room meshes are bound, with 61 baked lights and 398 probes. The final material inspection found a texture on every active environment renderer slot.

The published crawlgraph is the one from the furnished-interiors pass. **Wait for Justin to request the navigation bake.** Current geometry differs from its input. Quest traversal, flicker in motion, contact audio and frame time are queued for playtest. [Geometry and contact checks](validation/hospital/finished-campus/geometry-checks.txt) · [Entrance](../output/hospital-district-repairs/entrance.png) · [Stairs](../output/hospital-district-repairs/stairs.png) · [Pharmacy](../output/hospital-district-repairs/pharmacy.png) · [Laundry](../output/hospital-district-repairs/laundry.png) · [Paving](../output/hospital-district-repairs/paving.png).

## Finished campus in Unity

The existing 96 × 160 m Hospital District now uses the approved [full-block art target](../output/hospital-finished-review/hospital-district-full-block.png). [Whole block in Unity](../output/hospital-campus-unity/whole-block.png) · [south garden and ER](../output/hospital-campus-unity/garden-and-er.png) · [north laundry and transit](../output/hospital-campus-unity/laundry-and-transit.png) · [baked ER entrance](../output/hospital-campus-unity/er-baked.png). The first three views use a temporary inspection light.

The south approach has a planted loop, pergola, slatted benches, medical-center sign, striped ambulance apron, three wrecks, stretchers and an illuminated ER canopy. The center uses facade ribs, alternating open and glazed ward windows, roof tanks and ductwork, clinic clerestories and pharmacy awnings. The north has a stepped laundry loading dock, crates, drying lines, visitor shuttle, covered shelter, timetable and marked parking.

The campus uses the review model adapted around the original playable rooms, two stair cores and open courtyard. The importer reuses production vehicle/pine materials and a shared color/emission atlas for the new geometry. Signs use CutePixel SDF. The bake scene and expedition catalogue resolve the same prefab. The migrated candidate pools retain 18 coal, six gold and 18 enemies.

**Edit Mode evidence.** 2,671 checks passed, including rail clearance, pedestrian routes, stair access, all candidate positions and ground-to-roof graph connectivity. The rebuilt graph has 34,313 nodes and one retained component. All 416 campus mesh renderers have exported bindings. The block has 49 active baked fixtures, 1,439 bound receivers, three lightmaps and 398 probes. The main editor reloaded the published prefab and found zero missing campus maps or broken receiver bindings. [Geometry checks](validation/hospital/finished-campus/geometry-checks.txt) · [bake evidence](validation/hospital/finished-campus/bake.txt)

**Authoring.** Edit the playable shell on the existing prefab. The Blender source for the exterior is [build_campus.py](../ArtSource/HospitalCampus/build_campus.py). Run it in background Blender to regenerate the FBX, atlas textures and label/fixture source. Mesh names identify campus area, spatial cell and collision role. Keep mesh identifiers stable for existing references. The install command creates the finished hierarchy on an unconverted shell. When changing exterior topology, check mesh and collider references before rebaking the graph and lighting. Headset traversal, train-side visibility, late join and Quest performance are queued for playtest.

## Furnished interiors — November 23

User feedback on the finished campus: several building interiors were still empty. Every enterable room is now furnished following the approved V4 floor plans, in the Compound / Starting Station style: Quackville Town and Industrial atlas tiles at 16 px/m, the Compound's stocked shelving, and the Compound atlas, lens and glow materials.

- **Hospital ground floor.** South ER: four curtained bays along the south wall, two more against the corridor wall, an L-shaped triage desk, a crash cart and gurneys, with the arrival lane from the south doors kept clear. West reception: an information desk north of the entrance lane, three waiting rows, vending machines, records cabinets and wheelchairs. East wing: a CT suite with a gantry, a control desk and an x-ray light box, separated by a new partition at Z 77 from a four-bed general ward. North supplies: stocked racking on both sides of the north exit lane, an oxygen cylinder cage and boxes.
- **Hospital upper floor.** Surgery 1 to 3, each with an operating table, a ceiling surgical lamp, an anaesthesia machine, trolleys and monitors, plus scrub sinks and cabinets. Also a U-shaped nurse station with records and a crash cart, patient rooms 201 to 203 (five beds with cabinets, tables and wall TVs), and a seven-bay recovery ward.
- **Corridors.** Vinyl floors, a teal dado with a bumper rail, ceiling troffers (some dark), wayfinding plates, gurneys, wheelchairs, seating and scattered paperwork.
- **Clinic.** A waiting room with seating rows and a CHECK IN counter, plus three exam rooms behind a new partition with 1.6 m openings. Each exam room has a padded table, sink, cabinet and computer.
- **Pharmacy.** Four double-sided aisles, wall shelving, a checkout, and a pharmacist counter with a hanging PRESCRIPTIONS sign. Behind it is a dispensary with island and wall shelving, reached through a pass-through by the north wall.
- **Laundry.** Eight washers, twelve stacked dryers, a boiler and water heater, folding tables, carts (one tipped over), linen racks, an ironing press, hanging sheets and pipework.
- **Finishes.** Floors, thresholds and dados are skins over the shell slabs and walls. The shell wall material keeps its asset but changes from rusty green paint to pale plaster (`Textures/Hospital plaster.png`, the plaster cell of the Quackville Town atlas). The rough blockout beds, desks, scanner, shelving and street-building counters are deactivated, not deleted.

Abandoned dressing stays sparse and off the routes: tipped chairs and wheelchairs, fallen IV stands, dark screens and loose papers. All coal, gold and enemy candidates keep their positions. The Blender build refuses any solid prop that intrudes on a corridor loop line, a stair approach, a doorway, the ER arrival lane, the reception return, the north exit lane, a street-building entrance or a 1 m radius around a spawn candidate.

**Budget.** There are 13 room meshes (about 39.5k triangles, 3 to 5 material slots each, all on shared Compound materials). Physics uses 214 box colliders rather than mesh colliders. There are 20 CutePixel SDF room signs and 18 new baked room lights; the 6 old street-building lamps are retired, so the block has 61 baked fixtures. The whole prefab now has about 123k triangles on active renderers. That is below Farm Silos (198k) but well above the earlier hospital; Quest frame time is queued for playtest.

**Authoring.** Run `blender -b --factory-startup --python ArtSource/HospitalInteriors/build_interiors.py -- --export` (Blender 5.3). It writes `Assets/DuckRails/HospitalDistrict/Interiors/Hospital Interiors.fbx` and `ArtSource/HospitalInteriors/interiors.json` (lights, sign labels, collider boxes and troffer positions). **Duck Rails > Authoring > Hospital > Install finished interiors** replaces the generated interiors group. For mesh-only edits on the existing prefab, retain the hierarchy and check the unpacked renderers' material order against the imported FBX. Run `RailHospitalChecks` in the bake scene, then bake and export lighting with **Bake Open Block And Keep Open**. Navigation has the separate approval gate above.

**Edit Mode evidence.** The Blender keep-out assertions passed. The navigation rebake has 34,301 nodes and kept all 42 markers. 2,852 hospital checks passed, including new walks through the clinic, pharmacy and laundry entrances, the exam doorways, the dispensary pass-through and the north supplies exit. The lighting export has 1,323 valid receiver bindings (all 13 room meshes and 416 campus meshes), 2 lightmaps, 61 baked fixtures and 398 probes. The Console was clean. [Evidence](validation/hospital/interiors/evidence.txt) · [geometry checks](validation/hospital/finished-campus/geometry-checks.txt). Baked views with no fill light: [ER](../output/hospital-interiors-review/baked-er.png) · [reception](../output/hospital-interiors-review/baked-reception.png) · [ward](../output/hospital-interiors-review/baked-ward.png) · [imaging](../output/hospital-interiors-review/baked-imaging.png) · [supplies](../output/hospital-interiors-review/baked-supplies.png) · [surgery 1](../output/hospital-interiors-review/baked-surgery1.png) · [surgery 2](../output/hospital-interiors-review/baked-surgery2.png) · [nurse station](../output/hospital-interiors-review/baked-nurses.png) · [patient room](../output/hospital-interiors-review/baked-patient.png) · [recovery](../output/hospital-interiors-review/baked-recovery.png) · [clinic](../output/hospital-interiors-review/baked-clinic.png) · [exam room](../output/hospital-interiors-review/baked-exam.png) · [pharmacy](../output/hospital-interiors-review/baked-pharmacy.png) · [dispensary](../output/hospital-interiors-review/baked-dispensary.png) · [laundry](../output/hospital-interiors-review/baked-laundry.png). Before: [hospital ground floor](../output/hospital-interiors-review/before-cut-hospital-ground.png) · [laundry](../output/hospital-interiors-review/before-eye-laundry.png). No Play Mode, multiplayer or headset run; queued for playtest.


## Installed rough blockout

- Prefab: `Assets/DuckRails/Blocks/hospital-district-v1.prefab`; catalogue name **Hospital District**, registered in Expedition as the fifteenth scavenging location. Normal runs still select five stops.
- Site: 96 × 160 m, straight central rail and flat seams. East hospital 32 × 50 m, west clinic/pharmacy, northwest laundry/utilities, northeast visitor parking, southeast ER parking, southwest staff parking/park scenery and perimeter trees.
- Hospital ground at 0 m, upper floor +4 m, accessible roof +8 m. Both switchback stair cores connect all levels with real slab openings. Courtyard stays open to the sky; roof has outer/courtyard guards, two water tanks, three HVAC units, ventilation ducts and stacks.
- South ER has treatment beds, canopy and direct corridor access. Upper floor has three surgery spaces with tables, lamps and carts, plus nurses station, patient rooms and recovery ward. Ground north supplies, reception and imaging remain.
- Large HDR HOSPITAL lettering and baked warm sign spill face south; three static rough ambulance wrecks include side-overturned, upside-down and barrier-crashed poses. Vehicles and furniture are blockout geometry, not finished models or new interactions.
- Reused Quackville materials with mesh UVs, 40 baked lights, conventional probes and 1,246 exported lightmap receiver bindings. No runtime baking or gameplay atmosphere change. Three spawn variations retain six coal, six enemy candidates and two gold markers each; enemy navigation excludes the railway.

Authoring: `Assets/Editor/RailHospital.cs` (Duck Rails > Authoring > Hospital > Install approved district). Bake/review scene: `Assets/DuckRails/Lighting/Blocks/hospital-district-v1/hospital-district-v1-bake.unity`. The install command rebuilds the authored block; use ordinary prefab edits for hand refinement, then rebake graph/lighting as needed. Review via Test Block Order by selecting the Hospital District prefab. Do not use the offline RouteProof scene as a gameplay entry point.

### Validation and limits

- Unity compilation passed; MCP verified `X:/Work/duck-rails/Assets` before authoring.
- 2,479 geometry/navigation/UV assertions passed: train clearance, flat seams, both stair flights and landings, corridor/roof loops, ER/reception/department doors, open courtyard, spawn clearance and graph connectivity across ground/upper/roof. Evidence: `docs/validation/hospital/geometry-checks.txt`.
- 757 catalogue checks passed, including graph origin/chunk alignment and route contracts. Evidence: `docs/validation/hospital/catalogue-checks.txt`.
- Lighting export validated, including build-enabled companion; evidence: `docs/validation/hospital/lighting-exports.txt`.
- Two offline Play Mode assembled-route rounds passed 42 assertions for loading, translated atlas/probe binding, replacement and teardown. Evidence: `docs/validation/hospital/runtime/runtime-proof.txt`; reproducible scene `Assets/DuckRails/HospitalDistrict/RouteProof.unity` uses the existing route proof harness with hospital plus four existing blocks. This is not multiplayer or player-locomotion acceptance.
- Initial checks caught obstructed ER/reception circulation and a one-metre graph-origin mismatch; corrected and revalidated. Visual review caught recessed sign lettering; moved lettering forward and regenerated its mesh.
- Captures: [overview](../output/hospital/overview.png), [south ER](../output/hospital/south-er.png), [roof](../output/hospital/roof.png), [surgery](../output/hospital/surgery.png). These are actual Unity rough-block captures with temporary review lighting; the bake scene and runtime proof retain their own lighting.

Remaining acceptance: natural hand/stair traversal, low-eye readability and sign brightness, rescue/item recovery, enemy pursuit, real-client late join/authority changes and Quest performance. No headset or multiplayer completion is claimed. Wreck silhouettes, landscaping, window treatment and medical furniture remain rough art.

[Top-down drawing](../output/imagegen/hospital-district-v1/layout.png)

## Proposed layout

### V4 — south ER, illuminated sign and surgery rooms

[V4 revised floor plans and south exterior](../output/imagegen/hospital-district-v1/south-er-surgery-v4.png). Built-in image generation: initial two-reference edit failed with a network transport error; a simpler new concept sheet succeeded. Brief: ground-floor south ER, north supplies, upper surgery replacements, courtyard/two stairs, south nighttime glowing sign and ambulance wrecks. Visually checked requested changes. **Drawing correction:** the unsolicited small roof inset incorrectly fills the courtyard and rearranges equipment; disregard it and retain the V3 roof drawing with open courtyard. The floor plans and south facade are the V4 review content; image dimensions, room openings and vehicle clearances remain schematic.

November 4: user endorsed V3 and requested the emergency room on the south side, a large glowing hospital sign, crashed or overturned ambulances in ER parking, and surgery rooms replacing the staff lounge, store and waiting area. These changes supersede earlier north-ER and upper-floor room assignments. Retain two storeys, accessible equipment roof, courtyard and both stairs.

Interpret the named lounge/store/waiting spaces as the V3 upper-floor staff lounge, linen/store and observation/waiting area; convert them to three proposed surgery rooms with operating tables, lamps and carts. Retain the upper nurses station, recovery ward and east patient rooms. Surgery-room count and props are proposed drawing details.

Move ground-floor ER to the south wing with direct exterior doors and canopy facing the southeast parking area. Proposed swap: former south supplies move to the north wing; southeast visitor parking becomes ER parking and former northeast ambulance yard becomes visitor parking. Keep a clear walking route from the ER doors to the railway and rear path. Propose three static ambulance wrecks (side-overturned, upside-down and nose-crashed); exact count and poses remain flexible. Vehicles must not block the ER doorway, rail reservation or both return paths.

Place a large glowing HOSPITAL sign on the south-facing facade above the ER approach, with a smaller EMERGENCY entrance sign. Exact color, brightness and mounting are proposed; test bloom/readability from low player eye height during later art work. This requests scenery, not functional ambulance driving or surgery mechanics. The existing roof equipment arrangement remains unchanged. No Unity edits or validation are claimed.

### V2 proportion correction

[V2 portrait layout](../output/imagegen/hospital-district-v1/layout-v2.png) supersedes V1 for review. Generated with built-in image_gen, using V1 as reference and the coordinate brief below. Visually reviewed: the long portrait footprint and smaller train correct the main presentation problem. The generated grid/scale remains approximate (including uneven Z ticks); its printed 'exact' ratio is not a measurement guarantee.

The user requested closer correspondence to the actual block dimensions. Verified `Assets/Editor/RailWeek3Builder.cs` constructs ground at (0, -0.5, 80) with size (96, 1, 160); `Assets/DuckRails/Blocks/convenience-store-v1.prefab` retains that ground scale and length 160. The site therefore spans X=-48..48 and Z=0..160, with width:length 0.6. V1 incorrectly illustrated a landscape-shaped site despite its labels.

V2 uses a portrait site, central proposed 12 m railway reservation, hospital footprint X=10..42 / Z=55..105 (32 × 50 m), clinic X=-40..-14 / Z=82..103, and pharmacy X=-40..-14 / Z=57..76. Train reference is approximately 5 × 10 m, consistent with the modular train builder's deck/chassis, rather than V1's oversized silhouette. Building coordinates and rail reservation remain proposals. Preserve supporting scenery through both outer thirds. Generated artwork remains illustrative; use these coordinates for subsequent measured geometry.

- Use the earlier 96 × 160 m study envelope, straight central railway and +Z departure direction. Dimensions are illustrative, not a measured plan.
- Main hospital east of the train, mainly in the middle third: reception/waiting facing the railway, courtyard circulation loop, emergency ward, wards, imaging, supplies and two stair cores.
- Clinic and pharmacy opposite, with a plaza facing the train and rear service alley.
- Departure third: ambulance yard/canopy east, laundry/utilities/loading yard west.
- Approach third: visitor parking east, staff parking and pocket park west. Supporting trees, paths and service scenery extend toward the edges; maintain flat seams and railway clearance.
- Proposed pedestrian crossings beyond each end of the stopped train. Actual train clearance and crossing safety require later measurement.

V2 shows the ground floor only; the V3 vertical direction below supersedes its undecided upper-floor/roof scope. Basement is not requested. Loot/enemy positions and exact building heights remain undecided. Pharmacy is a location concept, not authorization for another gameplay shop. Exterior rear paths and service exits should provide alternate returns; drawing door gaps, corridor widths and arrow directions are schematic and require a measured floor-plan pass.

### V3 — two storeys and accessible roof

[Upper-floor and roof drawing](../output/imagegen/hospital-district-v1/upper-floor-roof-v3.png). Built-in image_gen used V2 as reference with the brief: matching 32 × 50 m overhead upper-floor/roof plans, courtyard void, aligned two stair cores, patient/staff rooms, clear roof loop, tanks/HVAC/vents and a two-storey height section. Visually reviewed for two floors, both roof exits and equipment outside the circulation loop. This remains an illustrative plan; the elevation's courtyard and stair detail need reconciliation in measured geometry. Roof mechanical pads are proposed scenery, not gameplay exclusion zones despite the legend wording.

Confirmed: retain the approved V2 site arrangement; hospital has two storeys total (ground and one upper floor), plus an accessible roof with ventilation, water tanks and HVAC.

Proposed drawing details: retain the 32 × 50 m hospital envelope and open courtyard. Extend both northeast/southeast stair cores from ground through upper floor to roof headhouses. Upper floor has a connected courtyard corridor loop, recovery/patient rooms, nurses station, staff lounge and linen/store rooms. Roof has two northwest water tanks, east HVAC units and south/west ventilation, with an unobstructed walking loop and both stair exits. Outer parapets and courtyard-edge guards protect the roof paths; equipment does not require jumping to pass. These room assignments, equipment counts, guard geometry and positions are proposals, not additional user requirements.

Suggested levels are ground 0 m, upper floor +4 m and roof +8 m, with plant above; exact stair dimensions, heights and hand/head clearance need measured authoring and headset review. Keep courtyard open to sky. No working HVAC/water simulation, lift, roof puzzle or basement is implied. Later implementation must bake navigation for the approved accessible levels and verify connected stair routes, rescue paths, low-eye visibility, loose-item recovery and target-headset performance.

## Review gate and evidence

Built-in image generation produced the drawing, visually inspected for top-down composition, central railway, hospital/clinic arrangement and populated outer thirds. The image is an illustrative concept and is not reliably to scale despite its dimension labels and scale bar. No geometry, navigation, gameplay or headset validation is claimed. Obtain drawing approval before rough Quackville blockout.

Generation brief: true overhead abandoned modern hospital campus; large east courtyard hospital with cutaway ground-floor departments, west clinic/pharmacy, ambulance and utilities yards north, parking and park south, clear central railway, amber return loops, muted sage/cream/asphalt materials. Built-in image_gen mode used.
