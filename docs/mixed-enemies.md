# Mixed enemies — November 1, 2026

Each scavenging block now starts with 4–6 enemies. Every encounter includes the existing minion, bomber, smasher and spider; any fifth/sixth enemy is randomly selected from those four. Six authored candidates per variation are shuffled without replacement. Bridge activation adds another 4–6 defenders within the finale approach. StartingStation has no enemies.

The existing right-side encounter bounds and editor-baked graphs are retained. This change does not add left-side navigation. Counts/types/positions derive from an integer seed retained in the route snapshot, independently of route selection. Late joining, replacement spawning, authority changes and exact-route presets reconstruct the same choices. Photon room objects remain master-spawned and retain their existing identity markers. Protocol is session-v8: restart/update all clients together.

Resources/Rails now contains RailBomber, RailSmasher and RailSpider variants of the existing project prefabs, preserving explosion, jump, stalking, animation, sound and network synchronization. They add the same block boundary and coal-carrier components as RailZombie. Approximately half of the population can carry coal, with every bridge defender eligible; the increased renewable coal supply needs balance testing. Bomber/recovery respawns select a point in the enemy's original block, identified by its retained spawn marker. Bomber blast downing also excludes passengers aboard the train.

## Authoring

Use **Duck Rails > Authoring > Install Mixed Enemies** to install missing variants and regenerate the six enemy candidates in all catalogue prefabs and Expedition's finale. This overwrites enemy marker positions/references; it preserves geometry, coal, graphs and lighting. Candidate positions come from the existing baked ground graph and are checked against solid geometry, encounter bounds and four-metre spacing. The full Week 3 builder calls this step too. Edit the selected variation's enemy markers to customize placement, retaining at least six valid candidates.

## Validation

- Unity compiled the runtime/editor changes. **Run Mixed Enemy Checks** passed 81 checks, including 100 seeds for every variation across all 13 encounter blocks: bounded candidate navigation, counts 4/5/6, unique positions, all four types and exact reconstruction after seed serialization.
- The updated **Run Week 3 Checks** passed 607 checks, including route/graph/marker and starting-station regressions.
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
