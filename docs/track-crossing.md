# Enemy track crossings — November 6, 2026

The user confirmed that enemies should pursue players across the railway, including the motel, while trying to avoid the train itself. This replaces the earlier permanent railway exclusion.

## Cause and change

The motel prefab had a ten-metre-wide railway bake blocker. Its graph had no ground nodes near the track at local Z 50, 70, 90 or 110. Several other blocks used equivalent exclusions, and RailEnemyBounds also pushed enemies out of these fixed corridors.

Removed those railway blockers, cleared their legacy bounds, and rebaked all 17 block graphs offline (16 locations plus the finale). Retained the outer encounter bounds and actual scene geometry. Updated the relevant authoring builders and old exclusion assertions so future authoring preserves the new policy. Other graph blockers remain intact.

RailTrainObstacle supplies a bounded set of occupied world-graph nodes at the start of each path search. The volume follows train translation/yaw, adds body clearance and up to one second of forward travel, and includes one cell of graph clearance to prevent diagonal corner cutting. Searches snapshot that set; the graph asset is never rebuilt at runtime. Normal ground movement and path shortcuts check current occupancy, so an old path cannot deliberately walk into a newly occupied crossing. A train overtaking an enemy does not trap it inside the avoidance volume; physical collision handling still applies.

Train-local navigation and the explicit minion boarding transfer are exempt. Boarding chooses its ground anchor outside the pathfinding clearance. Underpasses below the train volume remain available.

## Repeatable checks

- **Duck Rails > Authoring > Track Crossings > Bake Motel / Bake All Blocks**: offline navigation-only rebuild. Save open scene edits first.
- **Duck Rails > Tests > Track Crossings > Graph And Train Avoidance**: all block ground-side connectivity, four motel crossings, parked-train detours at three yaw angles, shortcut rejection, underpass clearance and release of the old crossing.
- **Motel Play Loop**: actual motel prefab, minion brain and collision geometry in an isolated Play Mode scene. First tests natural perception/pursuit across empty track. The parked-train and departure stages keep the target visible to AI by disabling the fixture's vision occlusion mask, isolating navigation from the normal short sight-memory window.
- Existing **Enemy Boarding** checks cover the separate rear boarding transfer and train-local motion.

Evidence is in [validation/track-crossing](validation/track-crossing/). The graph/avoidance suite passed 125 checks. The final Play loop passed empty-track pursuit, parked-train detour, reverse pursuit after departure, and replanning an already-computed route when the train newly occupied that crossing. The existing boarding Play loop (including interrupted boarding, riding, deck pursuit and knock-off), its 390 graph/packet checks, and resumable navigation scheduler regressions also passed after the change. These are desktop/editor checks; incoming-train timing, crowded crossings, real-client pursuit and headset performance still need gameplay acceptance. Avoidance is not a guarantee that a moving train cannot overtake an enemy already crossing.

Start a new expedition to load the rebaked graphs. The temporary minion-only spawn switch remains enabled.
