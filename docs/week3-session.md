# Week 3 — authored expedition blocks

## Scope and setup

This phase implements the approved twelve-location gray-box catalogue, five distinct randomized stops plus the existing bridge finale, authored spawn variations, consistent prefab connections, internal bends, a separate Expedition scene, and renewable coal carriers. Lighting remains deferred. The original train controls, item interactions, revive flow and bridge ending are retained.

Open `Assets/Scenes/Login.unity` and enter Play normally. Private staging and public boarding use Compound; departures load Expedition. Private runs keep the same Photon room/code across both scenes. Build settings contain Login, Compound and Expedition. Network protocol is `session-v5`; all clients must use this version.

`Assets/DuckRails/Blocks` contains Maintenance Depot, Convenience Store, Roadside Motel, Lumber Yard, Container Yard, Passenger Station, Farm Silos, Tunnel Works, Scrapyard, Lakeside Campground, Electrical Substation and Quarry. Each is an editable prefab, with a versioned stable ID, tags, graph asset, entry/exit/stop markers, three authored sets of six coal/two enemy markers, and sampled track. Four locations bend gently left/right before returning to the centerline.

## Authoring contract

- Root/entry `(0,0,0)`, forward +Z, unit scale; exit `(0,0,160)`. Ground surface Y=0; train reference Y=1. Runtime placements are translations only, starting Z=70 in 160-metre increments.
- Encounter bounds are X=9–37, Z=45–115 locally. The remaining lead-in/out provides 90 metres between adjacent encounters, about 22.5 seconds at 4 m/s. Preserve that spacing when changing layouts.
- Every selected block remains loaded with rendering, collision, supplies and AI. No block streaming or renderer culling is enabled yet.
- Bake navigation in the editor with the existing crawlgraph baker. The runtime assembler translates/reindexes baked nodes and preserves disconnected components; it never probes geometry, rebakes or creates inter-block edges. Enemies are additionally clamped to their own authored bounds and cannot target crew aboard.
- `Duck Rails > Run Week 3 Checks` validates assets, markers, translated graphs and route serialization. `Duck Rails > Build Week 3 Expedition` regenerates all twelve prefabs, graphs and both gameplay scenes from the preserved `Assets/DuckRails/Authoring/Week2Baseline.unity`. **Regeneration overwrites generated block/scene edits.** Save authored changes separately or extend the builder before using that command again. It refuses unsaved open scenes.
- Lighting currently uses temporary ambient light, existing moonlight and fog. No claim is made about baked lightmaps or Quest rendering performance.

## Retained state and scene lifetime

The master publishes one `rails.route` snapshot containing run ID, five distinct block IDs, their variation indices and exact positions. Late joiners use that snapshot rather than rerolling. The shared train command measures distance along the sampled track. Cargo and passenger carry use the train's rotation as well as translation.

Scene changes pause Photon dispatch and preserve dynamically networked roots (including avatars and room objects) before replacing the scene. Existing avatars rebind to the new rig. The new world waits for its route, cargo manifest and item state before enabling play. Staging removes old cargo and enemies before readiness; replay publishes a new run and route.

Coal carrier equipment/drop positions are retained in the expedition snapshot. An accepted authoritative hit releases one chunk. Separate retained issuance receipts prevent a released chunk from respawning after consumption or bag storage; receipts are trimmed to the current run. At fuel <=2 with no nearby usable loose/bagged coal, a retained 20-second timer starts. An existing nearby carrier is the first recovery opportunity; otherwise an enemy is re-equipped outside every connected player's view and at least 15 metres away. Subsequent opportunities use a 30-second cooldown. Missing enemies are restored at their authored spawn markers by the normal spawn maintenance. Players still have to hit the carrier, retrieve the chunk and feed the furnace; no fuel is granted automatically.

## Pacing and acceptance limits

Five blocks total roughly 800 metres. Driving to the bridge, the 60-second bridge defense and the automatic 32-second ending occupy approximately five minutes; about one minute of scavenging at each stop yields the ten-minute target. This is a tuning budget, **not a measured unfamiliar-player completion time**. Stops are opportunities, not invisible mandatory gates.

Initial balancing defaults: ten starting fuel, 0.05 fuel per metre (eight per straight block), +2.5 per coal chunk, cap 40. A chunk buys 50 metres. Roughly fourteen collected chunks supplement the starting fuel to reach the bridge, with thirty authored chunks available plus carriers. This leaves room for missed/wasted supplies without making stops irrelevant.

Target-headset rendering/memory, eight-player balance, one-player difficulty, voice through the new scene boundary, natural ten-minute pacing, and uncoached onboarding need hands-on validation. Editor and authenticated desktop tests do not close those milestone gates.

## Verification

Unity MCP verified the target project path, regenerated all assets and editor-baked graphs, and ran 324 Week 3 structural/navigation/serialization/fuel checks. The existing Week 2 state checks passed 16 checks. [Editor evidence](validation/week3/editor-checks.txt). MCP camera captures also inspected the assembled track and representative campground/store layouts; these are gray-box assets, not a visual-polish acceptance pass.

Run that scenario using `Duck Rails > Arm Three Client Week 3 Test`, then select the existing `3 Week 2 Test` Play Mode scenario and press its toolbar Play button. It uses the existing three configured accounts; it does not create credentials. Logs are written to `Temp/rails-week3-main.txt`, `Temp/rails-week3-player2.txt` and `Temp/rails-week3-player3.txt`. Disarm after testing. Fixtures accelerate setup by placing the train/crew and inject an accepted enemy hit; the actual route, items, room lifecycle and authority traffic use Photon.

The first sequence passed on all three desktop processes at 06:34 UTC October 30: five matching blocks, separate scene, 27 cargo/10 enemies; 33 seconds of curved travel with three passengers and a loaded loose bag; remote bag takeover/drop; one shared carrier drop; host handoff during bridge lowering; third-client leave/re-entry with identical route, bag and clock; two finishers/one non-finisher; private return; fresh replay; raised-bridge failure and cleanup. Evidence: [main](validation/week3/sequence-main.txt), [Player2](validation/week3/sequence-player2.txt), [Player3](validation/week3/sequence-player3.txt). This run needed fixture authority reassignment after automatic scene-load handoffs; the harness was then changed to let the current master perform setup. The Unity Console reported one AI/MCP relay subprocess exit and no gameplay exceptions. It was a local desktop network test, without packet-loss simulation or headsets.

`Duck Rails > Arm Three Client Week 3 Coal Recovery Test` runs the separate exhaustion/storage/handoff/refueling scenario. All three processes passed at 06:43 UTC October 30 with the final fuel budget and exact connection tangents: no coal/zero fuel/all carriers spent; cooldown honored; an out-of-view carrier replenished; its one shared drop stored without duplication; another master handoff preserved the issuance receipt and bag contents; retrieving/refueling added exactly five fuel once; driving resumed. No gameplay errors appeared in the main Console. Evidence: [main](validation/week3/recovery-main.txt), [Player2](validation/week3/recovery-player2.txt), [Player3](validation/week3/recovery-player3.txt).

Later desktop evidence for public departure, two-person whole-crew matchmaking return and abrupt master transport loss is recorded in [multiplayer-stability.md](multiplayer-stability.md). Still unverified: a full public gameplay run, every individual location with human melee/scavenging, public capacity/contention and coordinator-loss cases, eight-client/headset behavior, and natural completion-time balance. Private success/failure/replay and the smaller public session fixtures do not substitute for those remaining gates.

October 30 coal tuning: doubled authored coal to six per stop (30 across five stops), with paired markers 0.6 metres apart, and halved all furnace coal contributions to 2.5. Carrier drop count remains one per accepted hit. Updated builder, gauge and existing check expectations. Historical multiplayer evidence above predates this tuning. The subsequent [multiplayer stability pass](multiplayer-stability.md) compiled the current build, passed 432 Week 3 checks, and repeated the full private sequence on three authenticated desktop clients with 42 cargo. That pass also fixes duplicate-avatar creation at the Compound/Expedition boundary and checks avatar uniqueness through handoff, rejoin, return and replay. See the linked report for additional fault-injection results and remaining acceptance limits.
