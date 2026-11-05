# Quarry / twin mine layout

## Installed rough blockout

November 4, 2026. User approved V5: "looks great. let's do it." Installed on the existing `Assets/DuckRails/Blocks/quarry-v1.prefab`, retaining its stable ID, straight 160 m track, entry/exit and central stop. Historical drawing sections below describe the design progression; their pending-approval notes are superseded by this approval.

- Long east excavation spans Z=18..128; the shorter west excavation is farther toward departure. Floors are at -6 m. West rim reaches +2 m and has outer hand-climb shelves and inner descending ledges. The east haul route is graded from the surface to the floor, with an 82 m graded segment for its 6 m descent.
- Three branching tunnels share the staggered pit junctions. The floor/ceiling pair under the tracks is -6/-3 m. Ground and tunnel meshes are generated offline as a union, leaving junctions open and preserving the railway surface; the old solid ground slab is removed.
- Underground office has a desk, locker, noticeboard and warm work lights. Timber supports and small cooler tunnel markers provide route landmarks. Ten baked-only fixtures; moving subjects use conventional probes at surface and underground heights.
- Stationary lattice crane with cab, boom, counterweight and suspended hook; excavator, loader, haul truck, spare steel and gravel piles. Rough Quackville rock, grass, gravel, timber and weathered machinery materials. No working-crane mechanic was added.
- Same three spawn variations, six coal (three per side), six enemy candidates and two gold markers per set. Loot markers are grounded in the new excavation/tunnels.
- Enemy bounds now include both pits and underground passages but retain the central Z=45..115 encounter span. The baked graph excludes the surface railway; a per-block runtime exclusion also prevents knockback/drift into that strip while allowing passage below Y=-2. Other blocks default to no additional exclusion. No new enemy counts or inter-block navigation.

Authoring: `RailQuarryMine.Install()` (menu **Duck Rails > Authoring > Quarry Mine > Install approved V5**), then `RailQuarryMineChecks.Run()`, then the existing **Bake Open Block** lighting command. Use `Assets/DuckRails/Lighting/Blocks/quarry-v1/Bake.unity` for editing. The catalogue builder preserves the authored mine. `RefreshTerrainMaterials()` rebuilds the terrain and graph without replacing machinery; mesh updates explicitly refresh GPU buffers as well as saved mesh data.

### Verification and limits

Post-crash restart verification (November 4): reopened the saved mine in the restarted Duck Rails editor with no compilation errors. All 1,798 geometry/navigation and 619 catalogue checks pass again. The saved prefab retains its 890,307-byte navigation graph, 498 renderer bindings, three lightmaps and ten baked lights. Fresh surface/crane/climb/office captures were inspected; no lost assets or recurrence of stale mesh rendering was found. The mine bake scene is left open in Edit Mode. The pre-crash successful offline Play Mode proof is preserved; Play Mode was not re-entered during recovery verification. This verifies persistence, not the cause of the Unity crash. See [restart record](validation/quarry-mine/restart-verification.txt).

- Unity compilation passed. [1,798 geometry/navigation checks](validation/quarry-mine/geometry-checks.txt) cover rail/seam support, tunnel floors and body clearance, all three corridor sweeps, haul grades, climb shelves, loot support, machinery clearance, light/UV presence and translated surface-vs-underground enemy exclusion.
- Ground-only navigation flood fill connects every branch waypoint and both pits. No baked navigation node is inside the surface rail exclusion. Graph origin remains chunk-aligned with the catalogue.
- [619 catalogue checks](validation/quarry-mine/week3-checks.txt) passed, including route graph assembly, marker support, spawn spacing and retained track contracts. [Lighting export checks](validation/quarry-mine/lighting-checks.txt) cover receiver bindings and companion registration.
- One outer climb shelf was raised slightly after the clearance check caught terrain intrusion. A repeat terrain material pass exposed stale GPU mesh buffers; the save path now resets mesh data through Unity's Mesh API before rebaking.
- Review images: [surface](../output/quarry-mine/surface.png), [east pit and crane](../output/quarry-mine/east-pit-crane.png), [west climb](../output/quarry-mine/west-climb.png), [office](../output/quarry-mine/office.png), [under-track view](../output/quarry-mine/under-track.png). Exterior captures use a temporary review light and no fog; the office/tunnel captures remove that fill. These are not target-headset darkness acceptance.
- Offline Play Mode integration evidence is saved in [runtime proof](validation/quarry-mine/runtime-proof.txt). This exercises actual route assembly, baked lighting translation, payload replacement and teardown; it is not authenticated multiplayer or human traversal evidence.
- The final proof uses `output/quarry-mine/runtime-proof/` to avoid overwriting catalogue screenshots held open by another process (Windows IO error 1224). The reusable proof component defaults to its original output directory; the mine proof scene selects the separate directory. Stopping an interrupted run also emitted a Meta/OpenXR `ovrp_UnityOpenXR_OnInstanceDestroy` exception; this is recorded as an editor XR teardown limitation, not a mine geometry failure or a resolved headset issue.
- Still pending: headset hand-climbing/scale, full-width natural locomotion, recovery of dropped items on rock slopes, live enemy pursuit/knockback through the mine, real-client late joining and Quest performance. Do not mark these acceptance items complete from editor checks.

## V5 — much longer right quarry

November 4, 2026. User requested a much longer right quarry. [V5 drawing](../output/imagegen/quarry-v5/twin-mine-plan.png) | [Editable source](../output/imagegen/draw-quarry-v5.ps1).

Proposed east excavation length increases from 59 m to 110 m, spanning Z=18..128, with its existing width and -6 m floor depth retained. The left pit stays in its higher V4 position. This requested extension occupies more of the approach/departure thirds; flat perimeter seams and the level railway remain intact. Extend the east haul ramp and floor longitudinally, retaining the same three tunnel junctions and lit office. Crane pad moves to X=24..36, Z=132..142 with boom over the northern end of the east pit; loader moves to Z=146..152. Rearrange gravel and truck clear of the enlarged excavation; surface crossings are at Z=12 and 145. These are drawing coordinates, not verified geometry. No Unity assets changed; previous implementation/validation requirements still apply.

## V4 — staggered pits and crane

November 4, 2026. User liked V3 and its three branching paths, then requested the left pit farther up the block, right pit slightly lower, and a crane. V4 applies these changes to the drawing; no Unity assets changed.

[V4 surface, underground plan and unfolded section](../output/imagegen/quarry-v4/twin-mine-plan.png) | [Editable V4 source](../output/imagegen/draw-quarry-v4.ps1)

- Proposed offset: west/left pit +10 m along Z toward departure; east/right pit -10 m toward approach. West excavation now spans roughly Z=59..122 and east Z=42..101. Pit depths and west climb-over rim remain as V3.
- Preserve three branching routes and two loops, now running diagonally between west junction (-25,92) and east junction (25,72). West-side branch points move +10 m, east-side points -10 m, while centreline crossings remain at Z=101, 82 and 63. Office moves with the eastern branch to X=7..15, Z=77..84; its doorway still connects to the direct route. The section is unfolded along that diagonal route rather than a straight cross-block slice.
- Add a stationary crane on a level pad at X=24..36, Z=108..118, north of the east pit. Its boom points into the east excavation, away from the railway; hook, clearance and support geometry remain to detail. No operating crane mechanic is requested. Loader moves farther north to avoid the crane pad; gravel and other machinery remain distributed across the outer thirds.
- Surface crossings move to Z=32 and Z=130, outside the staggered excavations. All V3 navigation, tunnel collision, loot-count, lighting and validation requirements remain applicable. Exact offsets and crane placement are drawing proposals for review before blockout.

## V3 — twin pits and underground branches

November 4, 2026. Supersedes the V2 single-pit proposal below. User confirmed the direction: two excavations on opposite sides of the railway, one with raised edges that require climbing up and over before descending; three branching connecting tunnels beneath the tracks; a small underground office providing light; heavy machinery and gravel piles above ground. This is now more mine than quarry. The revised drawing and its dimensions await review before Unity blockout.

[V3 surface, underground plan and section](../output/imagegen/quarry-v3/twin-mine-plan.png) | [Editable V3 source](../output/imagegen/draw-quarry-v3.ps1)

Proposed implementation details:

- Preserve `quarry-v1`, its 96 x 160 m footprint, straight level railway and Z=80 stop. Main excavations occupy roughly Z=49..112; supporting equipment, spoil and gravel extend toward both ends while retaining flat seams.
- West pit has a continuous raised rocky rim, proposed crest Y=+2 m. The highlighted surface approach climbs over the rim, then uses broad inner ledges to descend. It is not a flat entrance cut into the rim. Ledge heights, hand contact and a return climb need detailed geometry and headset validation.
- East pit has a winding haul ramp linking ground level to the pit floor, proposed Y=-6 m. Reserve at least 72 m graded run for a 6 m descent at 1:12, plus level joins; the drawn centreline is approximately 123 m overall and is schematic until graded in 3D. Carrying loose coal can use the east ramp and underground connection rather than requiring the west climb.
- Three 4 m-wide underground branches share west/east junctions at approximately (-25,82) and (25,82). North runs via (-15,98), (0,101), (15,98); direct runs across Z=82; south runs via (-15,66), (0,63), (15,66). All connect both pits, creating two loops rather than three dead ends. Portal/headwall transitions need detail where open excavation becomes roofed tunnel.
- Proposed tunnel floor Y=-6 m and clear ceiling Y=-3 m give 3 m headroom and approximately 3 m nominal ground cover below the track. Build hollow collision shells and continuous ground support above; do not leave the existing solid ground collider filling the tunnel void.
- Small office at X=7..15, Z=87..94, floor Y=-6 m, opens off the direct branch. An 8 x 7 m footprint allows a desk, lockers and a work light. Bake localized warm light spilling through the doorway; the office is a navigation landmark, not a guaranteed enemy-free safe room. No new switch/power puzzle.
- Static excavator, loader, haul truck, spares and gravel piles occupy surface work areas. No moving-machine mechanic is implied. Rough Quackville materials follow site approval.

Retain three spawn variations and the existing coal/gold/enemy counts; keep coal distributed on both sides. Exact underground placements and enemy coverage remain to author. The old east-only bounds (X=9..37, Y=-2..10) cannot cover the new tunnels: navigation volume/bounds must be revised deliberately for underground traversal while excluding train boarding and inter-block travel. Existing counts do not authorize adding enemies. Preserve above-ground railway exclusion without accidentally blocking traversable tunnels below it.

Next: approve the V3 drawing, then build the excavation, climbable rim, ramps, three hollow tunnels and lit office. Rebake navigation and lighting offline. Validate low-eye visibility, ledge recovery, items on slopes, room/portal clearances, rail support and enemy routing at different heights. Headset/multiplayer/performance acceptance remains pending. No Unity assets changed for this revision.

## V2 — historical single-pit study (superseded)

November 4, 2026. V2 drawing for user review; dimensions and terrain are proposals. No Unity assets changed. This expands the earlier catalogue study with supporting scenery through the approach and departure thirds.

[Top-down plan and terrain section](../output/imagegen/quarry-v2/quarry-plan.png) | [Editable drawing source](../output/imagegen/draw-quarry-v2.ps1)

## Existing block verified

Unity MCP targets `X:/Work/duck-rails/Assets`. Read-only prefab inspection confirms `quarry-v1`, length 160 m, straight sampled track, three spawn variations with six coal, six enemy candidates and two gold markers each. Enemy bounds span X=9..37 and Z=45..115. Existing geometry uses raised quarry-rim/terrace primitives, access ramps and a conveyor shed; the proposed negative-height excavation is a replacement, not existing terrain. The catalogue builder sets the stop at Z=80. A later MCP query failed to load its dynamic command assembly; no editor modifications or runtime validation were attempted.

## Proposed arrangement

- East central excavation: dry floor at Y=-2 m, intermediate rock shelf at -1 m and a small eastern crest at +1 m. The main pit occupies approximately X=10..41, Z=52..105. Avoid an enclosing wall that hides both exits from low eye height.
- Exit A: 4 m wide, X=16, Z=54..80, descending from 0 to -2 m. Exit B: X=30, Z=101..75, same width and drop. Each has 26 m of graded run (1:13), plus level end transitions and links into the pit. Actual mesh joins, collision, full-width grades and loose-item recovery require implementation checks.
- West office/lockers: X=-36..-16, Z=54..67, 20 x 13 m. West repair bay: X=-37..-15, Z=94..111, 22 x 17 m. Open circulation connects their rear and railway-facing sides.
- Loading shed: X=14..32, Z=105..114, 18 x 9 m. Static conveyor on the eastern lip, outside exit B. No moving machinery or new damage mechanic. Main buildings have two exits; interior partitions remain to detail after site review.
- Approach: gate hut, static weighbridge, parked loader, gravel/rubble and sparse scrub. Departure: spare-parts racks, aggregate piles, boulders and scrub. Proposed broken fence sections must remain outside routes. These secondary features populate the outer thirds without expanding enemy encounters.
- Keep the railway level, with a proposed clear X=-7..7 corridor, crossings at Z=47 and 116, and a 6 m flat perimeter. Blend terrain height and slope to zero before the perimeter. Drawing contours are schematic.

## Retained gameplay and next step

Keep the stable ID, track, entry/exit and stop, three variations, six coal per selected set split three per side, two gold markers and current enemy counts/bounds. Marker positions will need ground support checks after excavation; no new loot or enemies are approved by this drawing.

Obtain top-down layout confirmation as required by AGENTS.md before rough Quackville blockout. Then construct actual excavated terrain, building shells/interiors and static machinery; rebake crawlgraphs and lighting offline. Verify both pit exits, recovery from every shelf, low-eye navigation, train clearance and all marker surfaces. Headset traversal, multiplayer and performance acceptance remain pending.
