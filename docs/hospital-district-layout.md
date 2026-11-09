# Hospital district — top-down proposal

November 4, 2026. The user approved the hospital drawings including the V4 south ER/surgery changes and requested the 3D build. The rough Quackville district is now installed; see implementation and validation below. This is a separate district, superseding the earlier combined construction/hospital study.

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
