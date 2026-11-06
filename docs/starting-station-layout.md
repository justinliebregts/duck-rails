# Underground starting station — split-platform proposal

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
