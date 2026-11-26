# Abandoned underground station — V1 proposal

## V3 installed — Tokyo metro, up-and-over to a buried wreck

November 26, 2026. Justin found the installed V2 too basic and asked for a Tokyo-style subway with a low-ceiling main platform, many hallways, escalators going up and over to a second abandoned platform with a crashed train, and haunted lighting. Starting reference: [roof-cut concept](../output/full-block-concepts/passenger-station.png).

Confirmed answers: the abandoned platform is on the **same level** as the main platform (up, over the live track, back down). V3 **replaces** the V2 lower floor. Style is **Tokyo**. The wreck is **buried under a cave-in**. Flicker is **shader/emissive only**; baked light stays steady. Justin approved the [scaled layout sheet](../output/passenger-station-v3/layout-v1.png) with "build in 3D". The built concourse floor is +3 m, not the sheet's +2.5 m (see below).

### What is built (block-local metres)

- Retained: block id, 96 x 160 envelope, curved descending track and its atlas rails/sleepers/rail bed (moved, not rebuilt), stop marker (-12,-4,80), midpoint shop pad, flat seams.
- **Platform 1** (live, west): floor -3, 2.4 m ceiling, screen-door units with wide boarding gaps, tactile strip, columns with line-colour bands, `KAMOBASHI` name signs. Near both ends the channel wall and ceiling edge step back to X -16.2 because a 14 m carriage swings out of the curve there. An east **service ledge** beside the train has a door to Platform 3, so neither side of the train is a dead end.
- Behind Platform 1: toilets, waiting room with vending machines, coin lockers, konbini (gondolas, fridges, counter) and staff room. A 128 m **long transfer hall** (stopped moving walkway) links the tiled **service portal** with the duck mural and closed exit A2, lost and found, konbini stockroom, staff room and the NW **cable vault / cable gallery** (lit windows; matching lit windows are on the departure tunnel wall).
- **Escalators**: four shafts (south and north, both sides), each a stopped escalator plus stairs, 6 m rise over 18 m (34 treads). Shafts are fully enclosed with sloped ceilings.
- **Concourses** at +3 over the live track: south ticket concourse (gate line, fare machines, departure board, station office) and north shuttered concourse (shutters, kiosk, toppled gates, red emergency lamp). Deck underside +2.7 clears the sweep box (top +1.3 to +1.5).
- **Platform 3** (abandoned, east): dead tubes, red emergency lamps only, toppled screen doors, benches. Dead track with a three-car commuter wreck (longitudinal seats, side doors both sides, gangways, amber-lit windows in cars 2–3). Car 1's nose and roof are buried by a **cave-in** whose chimney opens through the overburden to the sky, with a baked moonlight spot. A 2 m strip beside the north escalator shaft stays open past the rubble. Rails here are named `Dead track rail/tie` so the catalogue track-surface validator does not treat them as running rails.
- East side: maintenance hall (Z 40–128) with south, middle (through wreck car 2) and north passages to Platform 3; crew lockers, signal room, canteen, electrical room, parts store; SE **fan gallery** and fan control room behind the three recessed stopped fans in the approach tunnel; NE **pump room** with sump grilles.
- Lighting: 69 baked lights (cool tubes, red emergency, amber wreck, warm windows, moon spot). Glow uses the new `DuckRails/StationGlow` shader (`Assets/Shaders/StationGlow.shader`): unlit, per-object random flicker bursts when `_Flicker > 0`. Flicker materials sit on dim or dying tubes, departure board, screens and wreck windows; their baked light does not flicker.

### Spawns

Replaced the V2 markers (they sat on removed geometry). 120 coal and 64 gold candidates (crew-8 ceilings 93/52), chosen round-robin across 30 areas from 607 supported, clear surface spots, 1.8 m same-type and 0.8 m cross-type spacing. Gold is weighted to Platform 3, the wreck, cable rooms, pump, fan control, parts and electrical rooms, the north concourse and the office. 51 enemy candidates (quadrants roughly 12–14 each) after removing those without ground navigation within 0.8 m.

### Validation (Edit Mode, November 26)

- [2,857 checks](validation/passenger-station-v3/checks.txt) pass: train sweep every 0.5 m, walk lines on every platform, hall, passage, concourse, wreck aisle and door, every escalator/stair tread, coal/gold support heights, entrance-exit sightline blocked, `RailRoute.SelectResources` crews 1–8 × 100 seeds exact and unique, ground graph connects Platform 1 to Platform 3, both concourses, the wreck, transfer hall, maintenance hall, pump room and fan gallery, and `RailEnemySpawnBalance` 4/8/12 for crews 1–8 × 100 seeds. [Install report](validation/passenger-station-v3/install.txt), [prune and probes](validation/passenger-station-v3/prune-and-probes.txt).
- Navigation: offline full-area bake, 77,690 nodes. The concourse floor was raised from +2.5 to +3 because crawl-graph cells sit at n+0.5 and a floor exactly on a cell centre got no ground nodes.
- Not yet verified: headset readability and darkness, hand locomotion on the 6 m escalators, moving players and cargo through the sloped track on real clients, late joining, enemy pursuit up escalators, flicker look on Quest, and Quest frame time with the larger graph and 69 lights. Station is not in the normal-run pool; use Test Block Order.

### Rebuild steps

With the bake scene saved and no dirty scenes: **Duck Rails > Authoring > Abandoned Station > 1 Install V3 Tokyo station**, **2 Bake V3 navigation**, **3 Prune enemies and set probes**, **Validate geometry**, then **Duck Rails > Lighting > All Blocks > Bake Open Block And Keep Open**. Builder: `Assets/Editor/RailPassengerStationV3.cs`; install/markers/probes: `RailPassengerStationV3Install.cs`; checks and captures: `RailMetroChecks.cs`. Reinstalling regenerates all markers.

## V2 installed rough metro

November 6: approved lower-floor revision installed in the existing `passenger-station-v1` catalogue prefab. Rough Quackville concrete/paint/metal materials and CutePixel signs are retained. Main pedestrian floor is -3 m; lower floor is -8 m. Clear pedestrian ceilings are 2.13 m, with taller train/wreck channels. Two stairs and one stopped escalator connect the main platforms through the lower cross-passage, wreck platform and maintenance passages. The three-car wreck has side entrances, open gangways, seats, a buckled nose and rubble; pump/electrical rooms extend into the approach/departure areas.

The station is offset 12 m west, retaining the 96 x 160 m block envelope and aligned entry/exit seams. Gentle descending/ascending curves produce a 164.0438 m sampled rail route. Solid tunnel geometry blocks the entrance-to-exit sightline. Main platform length is 54 m. The active rail channel retains the existing train envelope; seven feet does not describe train-channel height.

Validation: 1,313 physical/navigation assertions pass, including sampled train envelope, main/lower floors, carriage aisle and side doors, all stair treads, loot support, cross-passage/service access, sightline obstruction and ground graph connectivity across both levels. Offline graph rebaked; lighting rebaked with 52 fixtures and exported receiver bindings. Player-height views saved in `output/underground-station-v2/`. See [geometry evidence](validation/underground-station/metro-v2-checks.txt). These checks do not establish headset comfort, real-client sloped moving-platform/cargo behavior or Quest performance.

Authoring: `RailAbandonedStation.Install()` now calls the V2 builder in `RailAbandonedMetro.cs`. The station validation/capture menu items now use `RailMetroChecks`; the old V1 checker remains historical and must not be used as V2 acceptance.

Offline Play Mode: two assembled-route rounds (seeds 11/43) passed with the station plus four existing blocks, validating exported atlas bindings, translated probes, seven lighting owners, replacement and final teardown. [Runtime evidence](../output/underground-station-v2/runtime/runtime-proof.txt). The initial attempt used an underground probe position for unrelated surface blocks and failed on scrapyard; rerunning with the established catalogue sample (-26,2,61) passed. Final station export has 1,817 receiver bindings (verify serialized count if subsequently rebuilt). The final offline graph has 48,123 nodes; Quest performance is not established by editor checks.

## V2 horror-metro concept revision — November 6, 2026

### Lower-floor revision

User approved the revised sheet with “Fantastic. Build it.” Implementation is underway: five-metre floor separation, 2.13 m clear pedestrian ceilings, separate taller train channels, enclosed stair access and an enterable crashed wreck. Current iteration uses a main station offset west by 12 m and gentle approach/departure curves to preserve train sweep clearance while blocking the through-view. Exact generated drawing proportions are not construction dimensions. V2 validation is recorded separately in `validation/underground-station/metro-v2-checks.txt`; do not treat the V1 evidence below as V2 acceptance.

User likes the proposed layout and requests stairs down to a lower-floor platform with a crashed train inside. This supersedes the same-level hidden-platform proposal. [Revised concept sheet](../output/imagegen/underground-station-v2/lower-platform-layout.png) shows separate main/lower plans and a sectional view. Two enclosed stair connections (A/B), offset low corridors and a maintenance bypass are proposed to preserve a loop. The crashed three-car wreck has open side doors and explorable interiors; rubble at its nose must not seal the circulation route. The lower chamber is separated by a solid slab, not an open atrium. Exact floor separation must accommodate the wreck, slab and clear stair headroom; seven feet remains the pedestrian-clearance target, not the train-channel height. Layout remains schematic with the middle-third constraint and verification caveats below. Revised layout review is pending; no Unity changes made.

Generated using built-in imagegen; [prompt](../output/imagegen/underground-station-v2/lower-platform-prompt.txt) retained with the image.

User approved the visual direction with “Yes much better.” A [proposed top-down layout](../output/imagegen/underground-station-v2/proposed-layout.png) now translates that direction into enclosed main platforms, a separate wreck chamber, two offset transfer passages, shops/stopped escalators, and branching maintenance areas. Layout approval is still pending; no Unity changes made.

The generated sheet is schematic: the central station is drawn longer than its intended middle-third footprint, and overhead beams are visible across the active railway in the roof-removed view. These must not become rail obstructions. Public cross-track connections require verified grade separation/clearance, not walls or paths through the train envelope. Target station extent remains approximately Z=53–107 within the 96 x 160 m block; supporting rooms occupy the outer thirds. Proposed curved approaches must be checked against the actual train sweep and retain aligned flat seams. The sheet's seven-foot dimension means clear pedestrian headroom beneath beams. Generated labels are concept annotations, not game font assets.

User feedback: the installed block is too open. Requested a horror-movie underground metro, approximately seven-foot ceilings, narrower entry tunnel, no entrance-to-exit sightline, and an explorable second platform that is partly closed off, potentially containing a broken train. User requested low-poly concept renders before implementation.

[Main platform concept](../output/imagegen/underground-station-v2/main-platform.png) | [Hidden platform and wreck interior](../output/imagegen/underground-station-v2/hidden-platform-and-wreck.png) | [Generation prompts](../output/imagegen/underground-station-v2/prompts.txt)

The renders propose roughly 2.13 m ceilings over walkable platforms/corridors, heavy beams, narrow bays, projecting kiosk/service enclosures, thick separating walls and dogleg connections. A curved rail approach/departure blocks the through-view using solid geometry. The second platform becomes a separate chamber, discovered through a low transfer passage; its static wreck has open side doors and connected carriage interiors. Dirty cream tile, faded olive paint, oxidized metal, cold fluorescent pools and warm utility-room light retain the rough pixel/low-poly direction.

Train clearance remains a separate constraint: confine any additional height to the narrow active rail channel and verify the current train envelope before setting its dimensions. Do not assume a seven-foot rail tunnel can contain the current train. These images are illustrative, not measured geometry; no exact curve radius, platform width, ceiling/beam clearance or route footprint is approved by their generation. A coordinated top-down revision and user confirmation precede replacement blockout. Prior V1 geometry/evidence below remains historical, not validation of V2.

Generated with the built-in image-generation tool and visually inspected. No Unity assets, enemy behavior, navigation or lighting changed for this concept revision. Follow current enemy boarding rules in AGENTS.md and docs/enemy-boarding.md; older V1 no-boarding notes below describe the earlier implementation context.

November 4, 2026. The user approved V1 for 3D with rough Quackville materials. The implementation record below supersedes historical drawing-review status.

[Concept sheet](../output/imagegen/underground-station-v1/layout.png) | [Generation prompt](../output/imagegen/underground-station-v1/prompt.txt)

Generated with the built-in imagegen tool and visually reviewed. The sheet communicates levels and circulation; illustrated scale, individual door gaps and stair/rail clearances are not measured construction evidence. The lower inset ghosts tracks above the underpass. The side section is schematic, with vertical relief exaggerated.

## Direction and scope

The user requests a train descent through a tunnel into a large abandoned underground railway station, substantial explorable space on both sides, possible escalators and convenience stores, another track with a walk-through broken train, and branching maintenance rooms/corridors connecting public and private areas.

Treat this as a redesign of the existing Passenger Station scavenging block (`passenger-station-v1`), separate from StartingStation and Compound. The approved build uses that catalogue mapping and supersedes the earlier outdoor Passenger Station drawing.

## Proposed arrangement

- Retain the 96 x 160 m envelope. Place the main station hall around Z=53–107; tunnel approaches, pump alcoves, ventilation spaces, cable runs and service rooms occupy the outer thirds. Keep flat entry/exit seams and outer boundary connections.
- Main railway runs through the center. West side has a broad platform, waiting/ticket hall, convenience store with back stockroom, and rear staff corridor.
- East side has an island platform, a second disused track with a static three-car broken train, and an outer hall with kiosk and toilets. Open train doors on both sides, an end-to-end internal aisle and an external bypass make the wreck a route rather than a dead end. No dynamic derailment or new train vehicle behavior is implied.
- Stopped escalators and adjacent stairs rise from both sides to an upper public concourse crossing above both tracks. Shops line its edges; voids look down into the railway hall. Exact height must clear the actual train smokestack, passengers and held objects.
- Rear maintenance corridors connect stockrooms, workshop, lockers, electrical and pump rooms to both ends of the public areas. A lower staff underpass connects west and east without crossing the operating rails at grade. Stairs connect its endpoints to the rear corridors. Public bridge and staff underpass create different return loops.
- Public/staff distinctions are architectural, not keys or access permissions. Abandoned shops are scavenging spaces, not additional functioning economy shops. No escalator motion, electrical puzzle or new hazard is proposed.

## Descent proposal and engineering boundary

Show a real internal descent rather than disguising a level track. Initial study: entry and exit at Y=0, station rail bed near Y=-4 m, flat first/last 6 m, smooth approach/departure transitions and a level central stop. Raised enclosing terrain provides cover over the tall station chamber and upper concourse. Depth, grade, tunnel length and roof profile remain proposals; do not treat the illustration as dimensioned construction geometry.

This intentionally revises the earlier level-track drawing assumption for this block, following the user's descent request. `RailBlock.Sample` uses 3D track points, distances and tangent rotation, but this source inspection alone does not verify sloped moving-platform/cargo behavior. Before implementation acceptance, check route distance versus block-placement length, stop offsets, train pitch transitions, tunnel clearances, loading and late joining on slopes, cargo/locomotion feel and authority handoff. Preserve the same height and +Z tangent at both seams.

## Implementation after drawing approval

Use rough Quackville Town/Industrial materials: dirty tile, faded station paint, concrete, oxidized metal and dark service spaces. Reserve final darkness for headset review; maintain low-eye readability and clear platform edges. Every major exploration branch should have a return loop, recoverable dropped items and room for rescue.

Retain existing loot counts and variation identity. Coal placement, enemy coverage and exact room footprints are deferred to approved geometry. Existing east-side enemy bounds do not automatically cover the enlarged station; explicitly author and bake appropriate coverage while excluding the operating train corridor by height. No runtime navigation bake or enemy boarding. Rebuild offline lighting and crawlgraphs after geometry changes.

Pending acceptance: physical dimensions and clearance, all room/bridge/underpass connections, hand locomotion on stairs and stationary escalators, wreck traversal, item recovery, sloped train movement across real clients, late joining/authority changes, low-eye lighting and target-headset performance.


## Installed rough blockout — November 4, 2026

The user approved the concept with: "build in 3D with the rough quackville materials". Installed on `Assets/DuckRails/Blocks/passenger-station-v1.prefab`; its stable ID remains `passenger-station-v1`, with display name **Abandoned Underground Station**. StartingStation and Compound are separate and unchanged by this build.

- 96 x 160 m footprint, flat first/last 6 m and side boundary strips. Railway descends from Y=0 to -4 m using smooth approach tangents, remains level through Z=45..115, and ascends to the original exit. Actual sampled rail distance is 160.4899 m; maximum pitch is 8.733 degrees. The stopping marker is (0,-4,80).
- Main station floor/platforms at -3 m, spanning most of X=-44..44. Eight lower rooms: tickets, mini mart, stockroom, staff lockers, toilets, kiosk, electrical room and workshop. Both rear staff corridors connect through real side door openings. Props remain rough shelf/counter/seat placeholders rather than finished equipment models.
- Upper concourse at +3.2 m crosses both railways, with a newsagent and cafe, two public stairways and two stationary walkable escalators. The high chamber roof is simple blockout massing at +9 m, not finished vault architecture.
- Lower staff passage at -7 m crosses beneath the tracks, with separate west/east stairwells and actual floor openings. The operating-track navigation exclusion permits the upper/lower crossings while blocking enemies from the active train corridor.
- Static three-car walk-through wreck on the disused east track: connected gangways, side doors, seats, windows, roof and end access ramps. The second track bed is recessed 0.4 m below the platforms; its visual-only rails lie beneath the carriage floor. Detailed damage and vehicle modeling remain art refinement.
- Four pump/ventilation alcoves extend into the approach/departure thirds. Terrain flanks, tunnel walls/ribs and fixtures enclose the approach. A flat western departure pad preserves the existing `RailRoute` midpoint-shop placement at (-16,0,148).
- Reused rough Quackville concrete, weathered paint, metal, roofing, timber, gravel and grass textures through station-owned material/UV assets. Forty-two baked fixtures, conventional light probes and 2,034 static lightmap receiver bindings exported to the existing companion scene. Expedition atmosphere settings were not changed.
- Three spawn variations retain six coal (three per side), six enemy candidates and two gold markers each. Navigation covers the station's connected public/staff spaces and is baked offline. The catalogue builder preserves this authored station on rebuild.

### Verification

- Unity compilation succeeded. [6,141 physical/navigation/material checks](validation/underground-station/geometry-checks.txt) pass: train envelope sweep, exact track seam tangents, floor/body clearance, room doors, stairs/escalators, upper and lower crossings, wreck aisle/side doors, alcoves, shop reserve, loot support, ground-only navigation connectivity and texture/lightmap UVs.
- [665 catalogue checks](validation/underground-station/week3-checks.txt) pass. The station graph origin was aligned to the catalogue's 16-cell grid before final baking.
- [Lighting export checks](validation/underground-station/lighting-checks.txt) pass with the station companion included in the build.
- [Two offline Play Mode route rounds](validation/underground-station/runtime-proof.txt) pass using the actual route builder with Passenger Station, Depot, Convenience Store, Farm and Canyon. This proves translated baked lighting, route replacement/readiness, probe movement and clean teardown. It does not exercise authenticated peers, natural player locomotion or loose-cargo physics on slopes.
- Visual review corrected the initial bench obstruction, through-wall/mirrored signs and rail rendering through the wreck floor. The first incomplete installer cleanup error was fixed and the saved station reloaded before installation was rerun. A capture helper initially targeted the exported lighting-only scene; it now opens the geometry bake scene explicitly. These issues are resolved in the saved outputs.
- No current Console errors were returned during the offline proof query. No diagnostic reports were queried or resolved.

Review images: [bright arrival](../output/underground-station/bright-arrival.png), [bright cutaway](../output/underground-station/bright-cutaway.png), [baked arrival](../output/underground-station/arrival.png), [platform](../output/underground-station/west-platform.png), [concourse](../output/underground-station/concourse.png), [wreck interior](../output/underground-station/wreck-interior.png), [underpass](../output/underground-station/underpass.png), [mini mart](../output/underground-station/mini-mart.png), [descent](../output/underground-station/descent.png). Bright views use temporary fill and disabled lightmaps for geometry review; the cutaway hides roofs. All review overrides were discarded by reloading the saved scene. Baked captures disable fog only for visibility; final gameplay darkness remains a headset review.

### Continue editing/testing

Open `Assets/DuckRails/Lighting/Blocks/passenger-station-v1/passenger-station-v1-bake.unity`. Authoring commands are under **Duck Rails > Authoring > Abandoned Station**. `RailAbandonedStation.Install()` regenerates the owned geometry/materials/markers and navigation; it does not automatically bake lighting. Run **Validate geometry**, then **Duck Rails > Lighting > All Blocks > Bake Open Block**. Wait for the export status to complete before **Capture review**.

Use the existing Test Block Order list to select `passenger-station-v1.prefab` for a private development expedition. The saved offline integration scene is `Assets/DuckRails/AbandonedStation/RuntimeProof.unity` and uses the existing `RailCatalogLightingProof` component with seeds 0 and 1.

Still pending: natural VR stairs/escalators/boarding and low-eye readability; moving players, held objects and loaded/loose cargo through the full pitch transition; real-client late joining/host migration; enemy pursuit/rescue; final wreck/vault/room prop art; and Quest draw-call/memory/frame-time profiling. The 2,034-renderer rough blockout is not a performance acceptance result. These checks remain open in the execution plan.
