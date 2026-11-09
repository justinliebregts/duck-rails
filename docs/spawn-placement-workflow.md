# Assisted block spawn placement

## Scope and reference

Established November 8, 2026. The user approved the assisted placement approach demonstrated on Container Yard, Farm and Skyscraper Works. Stop after requested blocks; this is not authorization to populate the remaining catalogue. Resume when the user names the next block and desired counts.

The manually authored Convenience Store is the style reference. Inspect its current prefab if needed rather than relying on old documented counts. Loot should belong to a place: tables, counters, shelves, pallets, machinery, inside containers/rooms, wall corners, tree roots, hay stacks and roofs. Spread it through meaningful destinations and approach/departure scenery, not arbitrary empty ground. Include multiple floors and both railway sides.

- Coal generally favors accessible exploration routes and low props, with some climb rewards. Follow block-specific direction: the Container Yard expansion put every new coal candidate on or inside containers.
- Gold mixes accessible discoveries with harder exploration/climbing. Roughly half accessible was the original general preference; Skyscraper explicitly favors upper floors and exposed routes. Difficulty includes route length, concealment, access and return path, not just height.
- Enemies use spaced candidates at major destinations, with ground and elevated coverage where navigation supports it. Preserve bilateral coverage and existing track-level rules. Follow the quadrant requirements below for every future scavenging block.
- Requested placement counts are **candidate pool sizes**, not simultaneously spawned items. The old 6 coal / 2 gold / 5 enemy counts are historical. Current scavenging routes use [resource scaling](resource-balance.md) and [enemy quadrant scaling](enemy-spawn-balance.md). Positive inspector counts are fixed authoring/finale counts; zero disables a type.
- Runtime enemy selection now enforces quadrant caps and horizontal spacing. Loot selection still does not guarantee floor coverage or an easy/hard gold split. Do not change selection/network rules during a placement pass.

## Enemy quadrant requirements — November 8, 2026

- Divide block-local X/Z into four quadrants: X < 0 versus X >= 0, and Z below versus at/above half the block length. These are initial-placement regions, not AI boundaries.
- The departure crew selects one enemy per quadrant for 1–2 players, two for 3–5, and three for 6–8: targets 4/8/12 per scavenging block. The finale retains its separate fixed encounter.
- Each quadrant needs **at least three simultaneously usable candidates**, with **15 m minimum horizontal separation between every selected pair across the entire block**, including across quadrant boundaries. Merely having three markers per quadrant is not enough.
- Aim for at least **six candidate locations per quadrant** to allow random variation and alternatives. This is an authoring starting point, not proof of usable capacity. Spread them through approach/departure destinations as well as the centre. Preserve existing elevated/track-level candidates.
- Height does not count toward the 15 m separation: markers stacked on different floors are alternatives, not independent capacity. Add horizontally distinct destinations/outer-yard coverage when towers are too concentrated. Check support, clearance, bounds and baked ground navigation as below.
- Preserve existing candidates and append additions unless replacement is explicitly requested. Never automatically scatter markers onto arbitrary floors or rebake navigation to satisfy a quota.
- **Acceptance before declaring a pool ready:** run `RailEnemySpawnBalance.Select(block, seed, crew)` for crew sizes 1–8 and at least 100 seeds. Require the full 4/8/12 target, the correct per-quadrant quota, unique selected markers and at least 15 m horizontal separation for every selected pair. Verify saved asset reload and nested EnemySpawnMarker prefab references. `RailEnemySpawnBalanceChecks.Run()` covers currently enabled normal blocks.
- If capacity fails, add validated candidates in the affected quadrants and rerun; do not weaken spacing, overfill another quadrant, or report a raw candidate count as acceptance. Runtime intentionally spawns fewer rather than force a cluster and logs the shortfall.
- For loot, also validate capacity against the eight-player maxima of 31 coal/52 gold (current tuning). Keep candidate counts separate from per-run counts in handoffs.

## How the passes were made

These were tailored C# commands through the existing Unity MCP, not a new installed generator. The saved marker prefabs and layout documentation are the durable result; chat-local helper code is not available reliably to future chats. Reconstruct/adapt a small placement command from the target geometry and existing editor helpers.

1. Read AGENTS, design/execution plan, target layout notes and relevant authoring source. Verify `Application.dataPath` is Duck Rails before editor operations. Inspect Git status and active Prefab Stage, including unsaved state; preserve unrelated user work.
2. Inspect actual prefab hierarchy, colliders, transforms and current pools. Builders explain names and intended routes, but current prefab geometry is authoritative. Do not run a whole-block rebuild to place markers.
3. Allocate candidates by area/floor before choosing exact points. Use named props and local coordinates transformed to world space. Corn placement used actual planted rectangles and the builder's path/prop-clearing predicate to hide loot inside vegetation. Tower placement used per-floor budgets so early floors could not consume the entire count.
4. Generate and validate positions without mutation first. Use deterministic choices for reproducibility; reject invalid candidates and select alternatives. Spread choices across landmarks rather than filling the quota from the first surface found.
5. Replace existing points only within the authorized replacement scope. For additions/doubling, retain existing objects, transforms and list order, then append. Keep all three marker groups and native marker prefab connections.
6. Save the target prefab, verify the saved asset, update its layout evidence and milestone notes, and leave it open in Prefab Mode for inspection. Commit/push only when requested; stage the task's files explicitly.

## Implementation references

- `Assets/Editor/RailLootPlacementWindow.cs`: runtime item bounds, floor raycasts, slope clearance, registration and enemy-location checks. Internal helpers may not be callable from dynamic MCP code; reproduce the relevant small calculation if needed, without changing visibility or using reflection.
- `Assets/Editor/RailSpawnerPrefabs.cs`: instantiate the existing Coal/Gold/Enemy marker prefab in the target stage scene. Parent beneath `Coal Spawns`, `Gold Spawns` or `Enemy Spawns`.
- `Assets/Scripts/Gameplay/RailSpawnPool.cs`: register transforms in `block.spawnCandidates` with `SetPoints`; an unregistered marker does not participate. Preserve migration state and do not revive obsolete variation lists.
- Register creations/modifications/deletions with the MCP command's result/Unity undo mechanisms. Record prefab-instance property overrides for names and transforms. Mark the block/stage dirty and save only the intended prefab.
- Descriptive marker names help review: type, sequential number, area/floor/prop, and optional accessible/climb description. These names do not implement difficulty metadata.

## Placement checks and their limits

- Use enabled, non-trigger Environment colliders from the target block. Cast downward from the intended surface/room height, below the next ceiling for interiors; ensure the hit is the intended floor rather than a lower floor through a hole.
- Compute the upright runtime coal/gold envelope from renderers and box colliders, including prefab scale. Place its lowest point about 0.05 m above support; use the existing slope-lift calculation on sloped surfaces.
- Check the footprint corners have nearby support. Reject overhangs and intersections using the Prefab Stage's physics scene `OverlapBox`. Verify that the query detects a known solid collider as a positive control. A single ray hit is insufficient.
- Previous passes used approximately 1.8 m minimum between same-type candidates and 0.8 m between different types as authoring defaults, not permanent design requirements. Consider larger spacing between enemies and keep stair/bridge access clear.
- Enemy origins must be inside local `enemyBounds` and within 0.8 m of `CrawlGraph.FindNearestGroundNode`. Use block-local coordinates for navigation/bounds, especially for transformed instances. A generic 0.8 m preview envelope does not prove clearance for every final enemy type.
- Verify saved candidate counts, valid nested marker-prefab references, unique active positions, and preserved authored settings. Sample 100 seeds per type; use the crew-scaled runtime selectors for per-run acceptance, including the enemy quadrant requirements above. Preserve environment transforms, graph references, lighting and unrelated edits; run `git diff --check`.
- Support/clearance is not proof of natural hand reach, climb difficulty, item settling on pitched roofs, visibility in corn, or multiplayer/headset acceptance. Record these as pending. Earlier automated Scene-view captures were black; do not call those visual validation. User inspection remains the review step.
- No diagnostic reports are needed or authorized by this workflow.

## Completed examples and handoff

- [Container Yard](container-yard-layout.md#spawn-placement-review--november-8-2026): 168 coal / 142 gold / 18 enemies; additions emphasized container interiors and roofs. Committed/pushed as `df8cc7b2a`.
- [Farm](farm-silos-layout.md#spawn-placement-first-pass--november-8-2026): 100 / 100 / 18; 42 of each loot type hidden across three cornfields, remaining coverage across house levels, props, roofs and trees. User approved; committed/pushed as `1b6fabbed`.
- [Skyscraper Works](skyscraper-layout.md#spawn-placement-first-pass--november-8-2026): 150 / 150 / 18; six coal per tower floor, gold weighted upward plus bridges/crane. User says the completed blocks are good for now. User subsequently requested committing and pushing this pass together with the workflow documentation.

As of this handoff, unrelated local changes in `Assets/Scenes/Expedition.unity` and `Assets/Resources/DevAgentSettings.asset` predate these placement tasks. The user subsequently explicitly requested committing and pushing all current changes, including these two files. Always recheck Git status rather than assuming this snapshot remains current.
