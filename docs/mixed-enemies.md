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
