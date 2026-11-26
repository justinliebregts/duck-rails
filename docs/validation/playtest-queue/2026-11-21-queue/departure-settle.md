# Duplicate coal views after a startup master change (room 26164 fix) — November 21, 2026

Unity 6000.3.24f1, one workstation, real Photon US, authenticated MPPM accounts, desktop rigs. Tested main `be54013bd` (faults, three-client round) and `26c614c8e` (two-client run), each with the existing uncommitted Huggy/Quackbear work in the tree. Editor-only test harness changes, uncommitted:

- `Assets/Editor/Checks/Networking/RailDepartureSettleLiveChecks.cs` (new): repeated simultaneous public departures. Each round compares every coal/gold view ID, spawn token and marker across clients, hands authority to a Ready successor and records how its settle ended, transport-faults the previous master (now a non-master) for 20 s, and returns the crew by an off-train all-down. Run with `RailDepartureSettleLiveChecks.Start(rounds, clients)`.
- `Assets/Editor/RailStartupRecoveryChecks.cs`: ignores the LIV LCK "requires Vulkan, OpenGLCore or DirectX 11" editor error and records settle start/end.

## Results

- **PASS — fault 0 (host stalled before route selection).** The successor's settle ended after 4.98 s with the stalled host still connected. That is the timeout path. Identical inventories on both clients, no duplicates. [Main](../../spawn-startup/recovery0-main.txt) · [Remote](../../spawn-startup/recovery0-remote.txt)
- **FAIL — fault 1 (host stalled after `SeedCargo`, before the manifest).** 56 duplicates per client, views 52–107: every gold and coal plus the flashlights. [Main](../../spawn-startup/recovery1-main.txt) · [Remote](../../spawn-startup/recovery1-remote.txt)
- **FAIL — fault 2 (host stalled after `MaintainCargo`, before the first serialization).** 57 duplicates per client, same pattern. [Main](../../spawn-startup/recovery2-main.txt) · [Remote](../../spawn-startup/recovery2-remote.txt)
- **PASS — three-client round 0 (before Unity crashed).** All three departed public Compound together into expedition 68640. They agreed on 156 loot views with the same IDs and tokens. Handoff 1→2 settled by acknowledgement in 0.22 s. Actor 1 (the old master) lost transport and reclaimed actor 1, and all three again had identical inventories. Unity crashed later in that run, and the log in `Temp/` was lost. The lines above were read before the crash. Three-client runs are paused: they often crash Unity on this machine.
- **PASS — two clients, rounds 0–1, plus the round 2 departure and handoff.** There were three simultaneous public departures (expeditions 98501, 71873, 88569). Each time both clients had the same 116 loot views with the same IDs and spawn tokens, zero pending cargo, and no `PhotonView ID duplicate` errors. Five master changes, including ones during Departing and Ending, all settled by acknowledgement within 0.22–0.33 s. Both non-master reconnects reclaimed their actor in about 30 s and showed identical inventories afterwards. Clients saw a brief unsynchronised Photon clock in six rooms, the 26164 trigger, and no takeover or duplicate followed. [Main log](departure-settle-2client-main.txt) · [Player2 log](departure-settle-2client-player2.txt)
- **BLOCKED — round 2 reconnect.** Player2 ran 20 s of simulated 100% loss, but neither client ever saw a disconnect. The fault injection did not take effect, so this isn't a game failure. The check timed out after 180 s.

## Fault 1/2 root cause

Photon 4.1.8.17 sends no more than `SequenceDeltaLimitSends` = 50 unacknowledged reliable commands. The fault-1 host sent about 50 enemy spawns first. The remote received those (its view count rose from 2 to 52 before the stall ended). The host's 56 loot `InstantiateRoomObject` calls then stayed queued behind that window while its main thread stalled. The successor's settle query timed out after about 5 s, so it seeded every missing marker with IDs 52–107. When the host resumed, its queued spawns went out with the same IDs. On November 9 the whole seed was 49 views, inside the window, so these faults passed then. Block content has grown since.

The settle design assumes that a host stalled for 5 s dispatches the switch before it can spawn again. That holds for new spawns. It does not hold for spawns already issued but still unsent. A live host can hit this if it issues more than 50 reliable spawns and then hitches for over 5 s.

## Proposed fix (not applied: it changes networking behaviour and needs a decision)

1. After a settle that ended by **timeout** while the previous master was still connected, the successor moves the room view ID allocator ahead (e.g. +256) before spawning, so it cannot reuse IDs the stalled host already allocated.
2. The master removes any later room loot or enemy view whose `rails/run/marker` duplicates an existing one, with `PhotonNetwork.Destroy`, which also clears the cached instantiation.

Alternatives: keep the settle waiting while the previous master is connected and active. This could stall spawning for up to the background keep-alive (60 s) and breaks the fixtures' "recover while the host is stalled" premise. Raising `SequenceDeltaLimitSends` or spreading the seed across frames only makes the race less likely.

Re-run faults 1 and 2 after the fix. All three faults must pass.

## Fix and re-run — November 21, evening (Justin approved the proposal)

Implemented the proposed fix in commit `ae7fdf3f9`: `Assets/Scripts/Photon/PhotonMasterHandoff.Settle.cs` and `Assets/Photon/PhotonUnityNetworking/Code/PhotonNetworkPart.cs` (`SkipRoomViewIds`). The new master skips the room view ID cursor 300 past every registered view after a timed-out settle. It then destroys any later view below that floor that duplicates one of its own run markers. The stalled-host fixture (`RailStartupRecoveryChecks`) now requires each pre-pause marker exactly once, instead of the original view ID and token.

- **PASS — fault 1.** Zero duplicate errors, 106/106 markers once, identical peer inventories. 50 of 106 pre-pause identities were kept; the successor replaced the others.
- **PASS — fault 2.** Zero duplicates, 106/106 markers once, identical inventories. 48 of 106 identities kept.
- **PASS — fault 0.** Settle timed out after 5.02 s on the stalled host. Zero duplicates, identical inventories.
- **PASS — two-client regression, round 0 and round 1 departure and handoff.** 116 loot views were identical on both clients after each step. Three handoffs settled by acknowledgement in 0.22–0.33 s. The non-master reconnect reclaimed actor 2. [Main](departure-settle-postfix-main.txt) · [Player2](departure-settle-postfix-player2.txt)
- **BLOCKED (fixture) — round 1 reconnect.** Main's second simulated transport loss in the same process never caused a disconnect. Both runs showed the same pattern: each process's first fault works and any later fault has no effect. This is a limitation of the test fixture's network simulation, not a game failure.

Still open: a three-client rerun (paused because it crashes Unity here) and the headset run.
