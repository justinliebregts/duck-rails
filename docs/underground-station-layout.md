# Abandoned underground station — V1 proposal

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

Open `Assets/DuckRails/Lighting/Blocks/passenger-station-v1/Bake.unity`. Authoring commands are under **Duck Rails > Authoring > Abandoned Station**. `RailAbandonedStation.Install()` regenerates the owned geometry/materials/markers and navigation; it does not automatically bake lighting. Run **Validate geometry**, then **Duck Rails > Lighting > All Blocks > Bake Open Block**. Wait for the export status to complete before **Capture review**.

Use the existing Test Block Order list to select `passenger-station-v1.prefab` for a private development expedition. The saved offline integration scene is `Assets/DuckRails/AbandonedStation/RuntimeProof.unity` and uses the existing `RailCatalogLightingProof` component with seeds 0 and 1.

Still pending: natural VR stairs/escalators/boarding and low-eye readability; moving players, held objects and loaded/loose cargo through the full pitch transition; real-client late joining/host migration; enemy pursuit/rescue; final wreck/vault/room prop art; and Quest draw-call/memory/frame-time profiling. The 2,034-renderer rough blockout is not a performance acceptance result. These checks remain open in the execution plan.
