# Underground starting station — split-platform proposal

## Z-fighting fixes — November 18, 2026

The user reported flicker at every rib joint (hall and tunnel) and under the Supplies door header.
Cause: the pier, chamfer beam and ceiling rib of each rib shared the same side planes where the
slanted beam ends overlap the other two pieces, and the header's underside was coplanar with the
doorway head. In `build_station.py` the piers and ceiling ribs are now 4 cm narrower than their
beams and the header is 2 cm lower. FBX re-exported, station rebaked and the companion exported.
Queued for playtest (see `playtest-queue.md`).

## Raised sign lettering — November 16, 2026

User requested solid, raised letters. All four current city-station labels now
use 2 cm extruded CutePixel outlines in place of flat TMP quads, with wording,
placement and dimensions preserved. Source/runtime prefabs and builder updated;
lighting rebaked/exported. Save/reload checks and editor renders pass; runtime/
headset readability remains queued. [Evidence](validation/station-raised-signs/README.md).

## City station v3 — layout proposal, November 16, 2026

Status: the user approved the layout and the model is built, installed on `StartingStation.prefab`, baked and exported (see [Built model](#built-model--november-16-2026)). It is queued for playtest; nothing is verified in Play Mode, multiplayer or headset. The greybox described further down is disabled inside the prefab, not deleted.

User direction (confirmed): the starting station is the same station as the Compound and should share its look; reuse the greybox's rough size; the supply room is about double; the two rooms across the tracks stay small; build in Blender with the Compound process ([subway-compound.md](subway-compound.md)); the exit comes out of a city, with a derelict city visible behind that players cannot reach, and chain-link fences on the left and right of the block. This replaces the mountain-side exit. User concept renders (platform toward the rear, platform toward the tunnel, exterior portal) were supplied in chat and are not saved in the repository.

Proposed defaults the user accepted for the layout drawing (still proposals until the drawing is confirmed):

- Supplies moves behind the west wall: X -20..-13, Z 80..92 (7 x 12 m, 84 m2), 3 m open doorway at Z 86.5..89.5. Shared store along the west wall, gold exchange on the north wall. The shared store's real footprint has not been measured against this room.
- East side, behind the wall: washroom X 13..18, Z 80..87 and station office X 13..18, Z 87..92 with a lit window. Set dressing only.
- Both 10 x 24 m platforms become fully open. Footbridge, stairs, platform height, track, train stop Z 80, 160 m block length and the no-enemy contract are unchanged.
- Rear wall at Z 68: sealed rolling doors across the track and a sealed "To Concourse" gate on the west platform, implying the Compound beyond. Z 0..68 is closed off and not modelled.
- Compound structure and survivor dressing (work lamps, generator, crates, salvage) with an olive band on plaster walls, as in the concepts.
- 48 m tunnel kept (8 x 7 m clear). Portal at Z 140 in a city retaining wall about 9 m high across the block, railing and road on top, low-poly derelict buildings above as an unreachable backdrop.
- Outdoor approach Z 140..160: gravel, supporting scenery toward the edges, chain-link fences along both block sides at about X ±47, flat seam at Z 160.

Drawing: [city-station-layout-v3.png](../output/station-concepts/city-station-layout-v3.png), source `output/station-concepts/draw-city-station-v3.ps1`. The drawing was rendered and inspected; it is not tested geometry.

### Built model — November 16, 2026

- Washroom and Supplies follow-up. The mirror quad in `Station_Rooms` faces into the washroom. Two warm tripod spots in `Station_Dressing` light the platform outside Supplies. The station lighting was rebaked and exported after saving the existing scene lighting edits. Unity Edit Mode verified the mirror direction, both prefab lights, and clear doorway rays. Expedition appearance is queued for playtest.

- Source: `ArtSource/StartingStation/build_station.py` (re-runnable, headless: `blender -b --factory-startup --python ArtSource/StartingStation/build_station.py -- --export --render`) and `StartingStation.blend`. It reuses the Compound's `Builder`, atlas tile table and prop helpers by loading `ArtSource/SubwayTerminal/build_terminal.py`, so that file must stay in place. Coordinates are the block's own: Blender X = Unity X, Blender Y = Unity Z, Blender Z = Unity Y. The script also writes `station_data.json` (lights, sign labels, guard lines).
- Model: 12,304 triangles in ten meshes (shell, rooms, stairs and bridge, dressing, tunnel, portal wall, city backdrop, street deck, yard, wire mesh). Rails, sleepers and the ground slab are the existing prefab objects and are not modelled. Both Quackville atlases at 16 px/m; wire mesh reuses the Compound's `SubwayMeshFence` texture.
- Unity: `Assets/DuckRails/StartingStationV3/StartingStationV3.fbx` and `StartingStationV3.prefab`. Materials are the Compound's own (`Assets/DuckRails/Compound/SubwayTerminal/Materials`), so a tint change there changes both. `Assets/Editor/RailCityStation.cs` adds **Duck Rails > Authoring > Station > City station v3 > 1 Build prefab from FBX** (MeshColliders on the Environment layer, 9 invisible guards for the stair rails, bridge rail and yard fences, 27 baked lights, four CutePixel SDF labels: SUPPLIES, WASHROOM, OFFICE, CONCOURSE) and **2 Install in bake scene and bake** (nests the prefab under `Env` as `City station v3`, disables `Underground station greybox`, regenerates 596 probes, starts the bake). Finish with **Duck Rails > Lighting > Starting Station > 3 Export Lighting Companion**.
- Differences from the drawing: ceiling is 9.0 m with a chamfered vault and five concrete ribs; the washroom door moved to Z 84.1..85.3 and the office door and window to Z 90.0..91.2 and 87.6..89.4 to clear the ribs; fences are at X ±47; the city is eight blocks on a street deck that also stops baked moonlight reaching the buried rooms.
- Supplies and the shop: the shared store keeps its own U-shaped layout (2.8 x 6.8 m). The room's west and north walls carry two sides; the third stands free in the room (the user had the first pass's salvaged partition removed in review). In `Assets/Scenes/Expedition.unity` the scene-owned `Departure Shop` moved to local (-19.02, 1, 88.63), yaw 270, and its old `SUPPLIES` label is disabled (the model has its own sign). `GoldExchange` under the shop was already inactive and was left that way, so the gold exchange shown on the drawing is not present. Scene backup: `tmp/station-v3/Expedition-before.unity`. Spreading the items along the larger room's walls instead is a layout decision for the user.
- Lighting: baked and exported through the existing station workflow. After the user's review the hanging warm lanterns were replaced by the Compound's bracketed floodlight fixture with neutral light, plus a few warm accents. A first bake with two floods per side wall was too dark against the user's target image, so a third was added and intensities raised. 27 lights: hall floods (three per side wall, two on the tunnel-end wall, two over and two under the bridge), two supplies floods, three alternating tunnel wall floods, four outside (portal, street, east yard mast, signal hut; the west mast is dead), and warm accents (camp work lamp, fire barrel, concourse gate glow, one bulb each in supplies, washroom and office). Kinds and values are in `RailCityStation.BuildPrefab` (`flood`, `small`, `yard`, `warm`, `bulb`). The authored Expedition atmosphere was not touched.
- Review fixes, November 16: concourse gate panels and the void behind them now face the platform; camp windbreak sheets, shop partition and the QUACKVILLE sign removed; benches moved beside the stairs (X ±9.78, Z 78.8); brick patches no longer overlap themselves and stand 3 cm off the wall; the rolling-door recess no longer has its own floor (it was coplanar with the ground slab).
- Edit Mode checks (bake scene, [checks.txt](validation/station-v3/checks.txt)): 30 pass: floor heights for platforms, rooms, landings and bridge; all 56 treads; clear doorways, shop aisle, bridge and tunnel; rear doors, gate, rails, fences, shop partition and retaining wall block; train envelope X ±2.75, Y 0.3..5.2 clear from Z 75 to 160. Console had no errors. These are ray and overlap tests, not traversal. That list is from the first build (its shop-aisle and partition checks no longer apply). After the review fixes a shorter set was run on the rebuilt, rebaked station ([checks-review-2.txt](validation/station-v3/checks-review-2.txt)): 15 pass, covering the train envelope with the new tunnel fixtures, the open supplies room, sealed gate and doors, platform aisles, bench positions, stair feet, light and label counts. The full 30 were not rerun. Review captures: [hall](../output/starting-station-v3/unity-r2-target.png), [tunnel end](../output/starting-station-v3/unity-r2-tunnel.png), [tunnel](../output/starting-station-v3/unity-r2-intunnel.png), [portal](../output/starting-station-v3/unity-r2-portal.png), [supplies](../output/starting-station-v3/unity-r2-supplies.png).
- Captures: Blender EEVEE [rear](../output/starting-station-v3/blender-rear.png), [tunnel end](../output/starting-station-v3/blender-tunnel.png), [portal](../output/starting-station-v3/blender-portal.png), [bridge](../output/starting-station-v3/blender-bridge.png); Unity bake scene, Edit Mode [rear](../output/starting-station-v3/unity-rear.png), [tunnel end](../output/starting-station-v3/unity-tunnel.png), [portal](../output/starting-station-v3/unity-portal.png), [east rooms](../output/starting-station-v3/unity-east.png), [supplies](../output/starting-station-v3/unity-supplies.png), [yard](../output/starting-station-v3/unity-yard.png); Expedition with a temporary light [shop aisle](../output/starting-station-v3/unity-shop-aisle-items.png).
- Known consequences: `Validate underground greybox`, `Capture bake scene views`, `Fit departure shop in Expedition` and `Apply Quackville rough materials` target the disabled greybox and were not updated; do not run `Install underground greybox` (it deletes the prefab's `Env` child) or `Fit departure shop`. A full scene rebuild still needs the shop position reapplied. Week 3 checks were not rerun.
- Not done: Play Mode, multi-client and headset checks; Quest performance; stair traversal with real locomotion; shop purchases in a networked session; weeds and finer yard dressing; raised sign meshes as in the Compound.

## Concept finish renders — November 16, 2026

City-exit exploration: user requested imagining a visually simple exit from a city in place of the mountainside. [City-edge portal v1](../output/station-concepts/render-study-2026-11-16/city-exit-v1.png) proposes a retaining wall around the existing tunnel, short wing walls, a railing/streetlight and a few box-shaped building silhouettes above/behind it. Open gravel verge in front communicates the city falling behind without a detailed streetscape. [Built-in imagegen prompt](../output/station-concepts/render-study-2026-11-16/city-exit-v1-prompt.json). Proposal awaiting review; mountain geometry and approved station layout are not changed.

Latest review: the expanded supplies render was too large; user requested about half that size and a camera farther back showing station context. [Smaller supplies in context v2](../output/station-concepts/render-study-2026-11-16/supplies-context-v2.png), [prompt](../output/station-concepts/render-study-2026-11-16/supplies-context-v2-prompt.json). Bare interior retained. Generation used a proposed 6 m frontage / 5 m depth; these are not approved measurements. The image shifts adjacent doors onto the supplies side, so it is a room-scale/appearance study, not authority to relocate the established east staff/store rooms. No Unity edits.

User follow-up: make the supplies area much bigger and leave walls bare for user-placed items. [Expanded supplies-room concept](../output/station-concepts/render-study-2026-11-16/supplies-expanded.png) shows a broad open frontage and empty interior. [Prompt](../output/station-concepts/render-study-2026-11-16/supplies-expanded-prompt.json). The roughly 12 x 8 m room and 8 m entrance in the generation brief are proposed proportions, not approved dimensions; the earlier small-room footprint below needs revision before implementation. This request is for a render; Unity geometry remains unchanged.

Three built-in imagegen studies based on the selected split-platform layout and rough Quackville material pass: [platform toward departure tunnel](../output/station-concepts/render-study-2026-11-16/platform.png), [reverse toward rear footbridge](../output/station-concepts/render-study-2026-11-16/bridge.png), and [mountainside portal at night](../output/station-concepts/render-study-2026-11-16/portal.png). [Exact prompts](../output/station-concepts/render-study-2026-11-16/prompts.json).

Visual proposals pending user review, not measured views of a shared mesh or Unity captures. Pixel-textured plaster/olive walls, brick patches, concrete, steel guards and warm practical lamps explore the finish. Exposed rock, ceiling framing, fixtures and dressing are proposed details. Generated train geometry is illustrative, not a revision to the approved train. Platform widths, stair dimensions and tunnel proportions must follow the authored plan rather than these images. No scene, gameplay or lighting assets changed; no new validation claimed.

The user selected concept C (split platforms), then authorized detailed drawings before greyboxing. The underground station, departure tunnel and mountain-side exit are the chosen direction. Dimensions, rooms and lighting below are proposals, not approval to build or tested geometry.

- [Platform and bridge plans](../output/station-concepts/split-platform-detailed-v2.png)
- [Site strip and clearance section](../output/station-concepts/split-platform-site-section-v2.png)
- Reproducible drawing source: `output/station-concepts/draw-detailed-v2.ps1`.

## Measured references

Unity MCP verified this Duck Rails project and read the existing assets without modifying scenes. StartingStation is 96 x 160 m, straight entry (0,0,0), stop (0,0,80), exit (0,0,160). Compound occupied floor is nominally 18 x 24 m per subway-compound.md.

DuckRailsTrain prefab deck is 5 x 10 m. Combined MeshRenderer bounds, including attached details, are X -2.58..2.58, Y -1.11..4.00, Z -5..9 relative to its root. RailRoute.Sample places the train root at Y +1. Thus the conservative measured upper mesh envelope is +5 above the track datum. This is not a collision sweep and excludes particles and players. The drawn train footprint represents the deck only.

## Proposed layout

Coordinates are starting-block local metres. Track remains at X=0; no route length or track elevation change.

- West platform X -13..-3; east X 3..13; both Z 68..92, surface Y +1. Two 10 x 24 m rectangles give 480 m2 gross ground floor, about 11% above Compound. Room partitions reduce usable floor; stairs occupy part of this area.
- Maintain inner 3 m circulation aisles, X -6..-3 and 3..6. Deck edge X +/-2.5 leaves a nominal 0.5 m boarding gap. Current train side rails and low-eye-line reach require greybox review; drawings do not establish both-side boarding works yet.
- West supplies X -13..-6, Z 86..92 with wall shelves and counter. East staff X 6..13, Z 89..92 and store X 6..13, Z 86..89. Doors open toward platforms, 2.4 m clear nominal. Waiting bench X 6.5..9.5, Z 83..83.7 avoids the stair landing.
- Rear west arrival passage is scenic access, not a change to multiplayer spawning or the existing Compound-to-expedition transfer. Regroup pocket X -9.5..-6.5, Z 72..77.
- Bridge X -12.5..12.5, Z 68..71, top Y +6.1, underside +5.8, 0.3 m slab. 75 m2 elevated deck includes upper landings. Proposed 1.2 m guards, ceiling +8.6 gives 2.5 m above bridge floor. No upper rooms.
- Stairs X -12.5..-10.1 and 10.1..12.5. Each ascends southward from Z 82.32 toward Z 71: 30 risers at 0.17 m (5.1 m total rise), two flights of 15 risers/14 treads at 0.34 m, with 1.8 m intermediate landing. Run 11.32 m; lower clear landing extends Z 82.32..84.72. Intermediate landing Z 75.76..77.56. Upper landing is part of bridge. Reserve headroom and guard thickness in implementation.
- L1/L2 room lights, L3/L4 platform pools, L5/L6 stair-area lights, L7/L8 bridge lights. Diagram positions are illumination zones/fixture proposals; fixture mounting heights and supporting room/stair lights to be refined in greybox. Use baked pools and conventional probes; these drawings do not change environment fog/lighting.
- Enclosed departure tunnel Z 92..140: 48 m long, proposed 8 m clear width and 7 m clear height. Mountain portal at Z 140, outdoor approach Z 140..160. Mountain falls away around level track. Rear passage is sealed/scenic; no additional walkable area assumed.

## Review and next step

Drawings were rendered and visually inspected. Bench was placed clear of stair landing. No Unity geometry, gameplay, lightmaps or scene settings changed. These dimensions need greybox traversal/headroom/boarding checks, followed by VR review. The 0.8 m nominal bridge margin is based on current mesh bounds, not validated runtime clearance. Total floor including the bridge is 555 m2; ground footprint remains only slightly larger than Compound.

## Installed greybox

The user authorized greyboxing after the detailed plan. The actual `Assets/DuckRails/StartingStation.prefab` now contains `Underground station greybox`: two 10 x 24 m platforms, the 3 m footbridge, two stairs with intermediate landings, supplies/staff/store rooms, basic props, station ceiling, 48 m tunnel and coarse mountain masses. Entry, exit, start marker, 160 m block length, track and safe/no-enemy contract are retained. The rear arrival passage is represented by a sealed alcove; normal train spawning is unchanged.

Fourteen baked fixtures and refreshed probes were baked and exported through the existing station companion workflow. The final prefab has 271 lightmap renderer bindings. The authored Expedition skybox/ambient/fog settings were preserved. The departure shop remains a scene-owned instance: seven full-size item displays are arranged along two supplies-room walls, its old shop wall/roof are disabled, and the gold exchange is inside the room. Saved transforms and non-trigger collider bounds were verified after scene reload and Physics.SyncTransforms. Purchase interaction was not exercised in a new network session.

Validation: 326 physical/track/lighting checks pass, including every tread, landings, bridge headroom, platform aisles, doorways and a conservative forward train envelope sweep. Week 3 checks also pass. Evidence: [station checks](validation/station-greybox/checks.txt), [Week 3 checks](validation/station-greybox/week3-checks.txt). Native Unity bake-scene captures: [platform](../output/station-greybox/platform.png), [bridge](../output/station-greybox/bridge.png), [portal](../output/station-greybox/portal.png). The first capture helper emitted targetTexture cleanup errors; the helper was corrected and the captures rerun successfully. A stale pre-SyncTransforms shop-bounds query was superseded by the successful scene-reload check.

Authoring code: `Assets/Editor/RailUndergroundStation.cs` and `RailUndergroundStationChecks.cs`. Install regenerates the owned layout and starts a bake; finish with the existing station Export Lighting Companion command. Validate and Capture commands require the station bake scene. Fit departure shop requires Expedition. Do not use a full scene rebuild to retain the scene-owned shop arrangement; if rebuilding, rerun Fit departure shop afterward.

The temporarily unresponsive Unity connection eventually executed the queued install. A fresh bake was explicitly completed after reconnecting, then exported and validated. Before scene work, the user's Expedition edits were saved and copied to `tmp/station-greybox/Expedition-before.unity`. Diagnostics retry returned ten existing open reports; none was resolved by this art change.

Headset traversal, natural both-side boarding across the 0.5 m gap, moving players/smoke clearance and runtime shop usability remain VR/playtest review. The mountain is simple massing, not finished rock art. No new multiplayer/headset acceptance is claimed.

## Quackville rough material pass

The user authorized a rough material pass on the installed station. Applied the existing depot Quackville Town/Industrial materials to 215 station surfaces using 57 station-specific atlas-UV meshes in Assets/DuckRails/StationMaterials/Meshes. Walls use stained plaster, faded olive paint and exposed brick patches; floors/stairs use worn concrete; guards/shelving use oxidized steel; ceilings use dark roofing; bench uses old timber; safety edges use worn yellow. Ground and coarse mountain massing use dark atlas grit. Original atlas files, import settings and shared material values were not changed. Track geometry, colliders, shop layout and Expedition atmosphere are preserved.

Lighting was rebaked and the station companion exported. All 326 physical/track/lighting checks pass afterward. Native bake-scene views were inspected: [platform](../output/station-materials/platform.png), [bridge](../output/station-materials/bridge.png), [portal](../output/station-materials/portal.png). Evidence: docs/validation/station-materials/. This is an editor material/layout pass; target-headset texture repetition, readability and performance remain unverified.

Reapply through Duck Rails > Authoring > Station > Apply Quackville rough materials. It preserves geometry, starts a station bake, and requires Export Lighting Companion after the bake completes. Reinstalling the greybox resets its surfaces; reapply this pass afterward. The implementation reuses RailDepotMaterials atlas mapping with a separate station mesh folder, avoiding edits to depot mesh assets.
