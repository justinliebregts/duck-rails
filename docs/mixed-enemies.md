# Mixed enemies — November 1, 2026

## Track-level spawn coverage — November 6, 2026

All 17 encounter blocks (16 scavenging locations plus the finale) now have usable candidates on both sides and on the railway itself. Bilateral coverage was already installed, but most blocks lacked railway candidates; centerline markers in Quarry/Passenger Station were below the railway and did not count as track-level spawns.

`RailTrackSpawnAuthoring` moves one existing candidate per variation onto supported railway geometry near the middle of the encounter. It distinguishes track height from underpasses, checks physical support/capsule clearance and existing baked ground navigation, and requires ground-edge connectivity to both sides. Six candidates and 4–6 selected enemies remain; both sides are guaranteed, while the railway candidate participates in the seeded shuffle and need not appear in every encounter. Elevated candidates are preferentially retained. Unsafe markers are repaired on their original side, including four finale markers outside bounds/off navigation, motel markers and two convenience-store markers. No geometry or graph rebake was needed. The spider-only override is unchanged.

After a location rebuild, run **Duck Rails > Authoring > Enable Track Enemy Spawns**, then **Duck Rails > Tests > Enemy Spawns > All Blocks And Scene Finale**. Save scenes first. This operates on catalogue prefabs with stable prefab references and checks the actual Expedition finale too. StartingStation remains enemy-free.

Verified through the Duck Rails Unity editor: saved/reopened catalogue plus Expedition finale passed 50 variation checks, each checking all six candidates and 100 deterministic selections (5,000 selections plus matching replays); existing mixed-enemy checks passed 154 assertions and track-crossing/train-avoidance checks passed 125. Evidence: [spawn coverage](validation/enemy-spawns/track-spawns.txt), `output/mixed-enemies/editor-checks.txt`, and [crossing regression](validation/track-crossing/graph-checks.txt). This pass used Edit Mode checks; natural pursuit, incoming-train interactions, multiplayer agreement and headset balance remain gameplay acceptance work. Start a fresh expedition with matching updated client assets to use the new marker positions.

The November 6 spiders-only boarding playtest override was turned off on November 10 (RailEnemySpawns.ForcedEnemyType = -1), so the normal mix described below spawns. Set it to 3 to force spiders again. Minions and spiders can use the train rear and both side gaps; see [enemy boarding](enemy-boarding.md). Start a fresh expedition for the override to replace the population.

Each scavenging block now starts with 4–6 enemies. Every encounter includes the existing minion, bomber, smasher and spider; any fifth/sixth enemy is randomly selected from those four. Six authored candidates per variation are shuffled without replacement. Bridge activation adds another 4–6 defenders within the finale approach. StartingStation has no enemies.

The existing right-side encounter bounds and editor-baked graphs are retained. This change does not add left-side navigation. Counts/types/positions derive from an integer seed retained in the route snapshot, independently of route selection. Late joining, replacement spawning, authority changes and exact-route presets reconstruct the same choices. Photon room objects remain master-spawned and retain their existing identity markers. Protocol is session-v8: restart/update all clients together.

Resources/Rails now contains RailBomber, RailSmasher and RailSpider variants of the existing project prefabs, preserving explosion, jump, stalking, animation, sound and network synchronization. They add the same block boundary and coal-carrier components as RailZombie. Approximately half of the population can carry coal, with every bridge defender eligible; the increased renewable coal supply needs balance testing. Bomber/recovery respawns select a point in the enemy's original block, identified by its retained spawn marker. Bomber blast downing also excludes passengers aboard the train.

## Authoring

Use **Duck Rails > Authoring > Install Mixed Enemies** to install missing variants and regenerate the six enemy candidates in all catalogue prefabs and Expedition's finale. This overwrites enemy marker positions/references; it preserves geometry, coal, graphs and lighting. Candidate positions come from the existing baked ground graph and are checked against solid geometry, encounter bounds and four-metre spacing. The full Week 3 builder calls this step too. Edit the selected variation's enemy markers to customize placement, retaining at least six valid candidates.

## Validation

- Unity compiled the runtime/editor changes. **Run Mixed Enemy Checks** passed 81 checks, including 100 seeds for every variation across all 13 encounter blocks: bounded candidate navigation, counts 4/5/6, unique positions, all four types and exact reconstruction after seed serialization.
- The updated **Duck Rails > Legacy > Run Week 3 Checks** passed 607 checks, including route/graph/marker and starting-station regressions.
- One authenticated editor expedition reached Running/world-ready with 26 network room enemies: five stops had 5, 6, 5, 6 and 4, all with all four types. A test-triggered bomber explosion entered dormancy and subsequently respawned inside its original block. A bridge spawn-path fixture added four defenders. This fixture directly exercised the spawn path; it did not exercise the physical bridge button. Sandbox was stopped afterward.
- Console error/exception query was empty during the live run. Evidence is in output/mixed-enemies/editor-checks.txt, week3-checks.txt and live-checks.txt.
- The backend diagnostic inbox was unavailable due to DNS failure. Existing local reports were inspected and left open; this work does not resolve them.

Still required: real-client type/count agreement through late joining and master loss, networked bomber lifecycle, natural melee/smasher/spider encounters, crew-size balance, and Quest CPU/rendering/network performance at the higher population. Single-editor smoke tests do not close those acceptance gates.

## Enemies on both rail sides — November 6, 2026

User requested enemy spawning on both sides in every block. The audit found 12 of 17 encounter prefabs had only right-side candidates (including the bridge finale). Eleven also restricted encounter bounds to the right; Hospital already had bilateral bounds/navigation but only eastern markers. Canyon, Quarry, Passenger Station, Skyscraper Works and Nuclear Power Plant already had both-side candidates.

`RailBothSidesAuthoring` expands the one-sided bounds horizontally, retains encounter Z spacing, adds railway navigation exclusions, bakes both sides offline and moves three of six candidates west in each affected variation. Existing two-sided authored layouts are retained. The scene-owned Expedition finale receives the matching bounds, graph and marker positions. StartingStation remains enemy-free. No lighting or scenery rebuild is required.

Selection still produces 4–6 enemies and all four enemy types, but guarantees at least one spawn per rail side, including the asymmetric Canyon and underground candidates. It preserves the seeded RNG stream and exact replay. The session partition is now `session-v11`; rebuild/restart all clients together because old assets/selection produce different enemy positions. The mixed-enemy installer preserves already bilateral candidate layouts; the full Week 3 builder runs the bilateral migration after installation.

Use **Duck Rails > Authoring > Enable Enemies On Both Sides** after rebuilding a location with a legacy one-sided builder. Save/close Prefab Mode before running this scene-changing authoring command. Existing location art notes about east-only navigation are historical and superseded by this request.

Unity playtesting/verification was omitted at the user's request. Updated regression assertions are provided for subsequent runs; no multiplayer or headset acceptance is claimed.

Installation result: the authoring command completed successfully and saved all 12 affected prefabs and crawlgraphs plus the scene-owned finale. The five already bilateral encounter prefabs were retained. Source whitespace checks passed. Regression suites and gameplay validation were not run.

## Candidate-pool counts — November 8, 2026

The user replaced variation-based spawn authoring with per-block pools and fixed configurable counts. Default enemy count is now 5 (editable, including zero), selected randomly from the combined candidates; the previous random 4–6 count is superseded for RailRoute. Bilateral selection remains when at least two enemies and candidates on both sides are available. Existing forced-spider playtest type override is retained. See [pool authoring, migration and validation](testing-tools.md#random-spawn-pools--november-8-2026).

## ZombieHook — November 10, 2026

`RailHook` (Duckz `ZombieHook`, see [port](duckz-ports.md#zombiehook-group--73b78c46-7ed6d880-2719d1a1-b591543e)) is the fifth type in scavenging encounters. It keeps block boundaries and **does not board the train**. Scavenging selection is in `RailEnemySpawnBalance`; the finale and bridge defenders (`RailSpawnPool.SelectEnemies`) also include it, since they cycle all `Names`. Counts per crew size are unchanged.
