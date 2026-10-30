# Multiplayer stability regression — October 30, 2026

This pass uses the existing three authenticated desktop accounts in Unity 6000.3.24f1, PUN/Realtime and Photon Voice. Unity MCP confirmed `X:/Work/duck-rails/Assets` before operating the editor. The source Duckz project was not modified. Existing uncommitted development work was retained.

## Duplicate-avatar fix

The two-client baseline reproduced a real duplicate: one actor had both views 2001 and 2002 in the peer's PUN registry. Instrumentation traced the extra spawn to the outgoing Compound scene, after joining the expedition room but while Photon dispatch was paused and the new scene was loading. The scene-preservation sweep had already run. Unloading Compound destroyed that new avatar locally, but the room's cached instantiate event remained, so the subsequent Expedition spawn created a second copy on peers.

`PhotonAvatarSpawner.TrySpawn` now waits for Photon dispatch, completion of the session request, and the scene required by the room phase. Existing avatar rebinding remains intact. The temporary trace was removed after diagnosis. `RailAvatarChecks.AssertCurrentRoom` inspects the actual PUN registry, including inactive objects: exactly one human avatar per room actor, no orphan actors, active registrations for connected actors, and the current local hand anchors.

Abrupt-loss testing also reproduced a second avatar race. On a reserved-slot rejoin, the spawner could instantiate before PUN delivered the retained avatar. PUN reported duplicate ViewID 2001, destroyed the newly bound instance and installed the cached, unbound copy. The client was in the correct room but had no local pose/hand sources. The spawner now waits for the cached avatar when `LocalPlayer.HasRejoined`, and tracks the actual bound instance instead of a one-time boolean so replacement instances are rebound.

## Transport-loss recovery fix

The first public stress run confirmed a second defect. After 100% packet loss caused a real Photon timeout, the remaining expedition player became master, but the disconnected client reconnected to the compound instead of its reserved expedition slot. The Realtime SDK calls `OnLeftRoom` before `OnDisconnected` on a transport disconnect (`LoadBalancingClient.OnStatusChanged`). `PhotonSession.OnLeftRoom` unconditionally cleared `joinedRoom`, preventing the later callback from selecting `Request.Rejoin`.

`OnLeftRoom` now clears that destination only for an intentional leave/transfer. Unexpected disconnects retain it until `OnDisconnected` captures the recovery request. The fault-injection harness records connection-state transitions and rejects recovery into any other room.

Two follow-up assertions were corrected in the new harness: Photon Voice intentionally uses the gameplay code plus `PunVoiceClient.VoiceRoomNameSuffix` (`_voice_`), and an automatic master handoff can occur between test stages. The fixture now waits for authority immediately before fault injection and waits for the other client to observe the disconnected actor as inactive. Neither assertion failure required a voice or authority implementation change.

## Private expedition regression

All three processes passed the full sequence at 09:30 UTC: five shared blocks; 42 cargo and ten enemies; 33 seconds of curved travel with passengers and a loaded loose bag; remote bag takeover/drop; one shared carrier coal drop; master handoff during bridge lowering; third-client departure/re-entry with the same route, bag and clock; two finishers and one non-finisher; successful return; fresh replay; raised-bridge failure; and cleanup after both outcomes. Avatar uniqueness was checked throughout, including re-entry and both scene returns.

The first run exposed a test synchronization error: room membership arrived before the rejoining avatar. The harness now waits for all player poses before the uniqueness assertion; its existing timeout still rejects missing avatars. The clean rerun above passed without intervention.

Evidence: [main](validation/stability/private-main.txt), [Player2](validation/stability/private-player2.txt), [Player3](validation/stability/private-player3.txt), [duplicate baseline](validation/stability/duplicate-baseline.txt), [spawn trace](validation/stability/spawn-trace.txt). The original reconnect fault is captured in [main](validation/stability/reconnect-before-main.txt) and [remaining crew](validation/stability/reconnect-before-player2.txt); that diagnostic run was stopped after confirming the wrong destination, before its stage timeout.

## Public lifecycle and network faults

All three processes completed the final four-cycle suite at 10:13 UTC with the final runtime fixes. Two crew repeatedly departed while the third account remained in the compound. Each new expedition had 42 cargo. The train carried passengers and loose bags with 120 ms lag in each direction, 25 ms jitter and 3% packet loss on Player2. Non-master GO worked; sixteen rapid GO/STOP commands per cycle converged to STOP with matching fuel.

The first cycle cut all gameplay transport on the current master. The other crew member observed the actor become inactive and assumed authority. The disconnected client automatically rejoined its reserved slot; both clients retained the same route/run, 42 cargo, one correctly bound avatar per player and the expected voice-room membership. All four success/failure session finishes returned the crew together, with clean cargo/enemy state and matching voice membership. These public finishes use the temporary session finish control; the separate private sequence exercises the actual bridge ending.

Evidence: [main](validation/stability/public-main.txt), [Player2](validation/stability/public-player2.txt), [shopper](validation/stability/public-player3.txt). Main Console errors were the two deliberately induced Photon timeout messages and one Unity AI/MCP relay subprocess exit. No duplicate PhotonView error or gameplay exception occurred in the final run. The other editors' recent logs were also checked for duplicate views, missing references and test failures.

An earlier four-cycle run reused the occupied compound three times, then selected a fresh shared fallback on the fourth. Both departing accounts reached the same new room; MCP verified its return-allocation token, two unique/bound avatars, zero cargo/enemies and two voice members. The original fixture incorrectly waited for all three accounts in the original compound and timed out. The updated fixture accepts a shared alternative destination, checks that the shopper stayed behind, and explicitly regroups before another departure. It does not claim the fallback's initiating condition was diagnosed or that every capacity/coordinator failure path is covered.

That diagnostic run is retained separately: [main](validation/stability/fallback-observed-main.txt), [Player2](validation/stability/fallback-observed-player2.txt), [shopper](validation/stability/fallback-observed-shopper.txt). Its timeout is the obsolete test expectation, not a clean automated pass. MCP found both crew in room 77103 with a `return-` allocation, avatar views 1001/2001, zero cargo/enemies and two members in `77103_voice_`.

## Editor regression checks

All 547 checks passed: session rules 33, desktop 10, quick-start 8, Week 2 state/furnace rules 24, Week 3 blocks/navigation/routes 432, drive controls 17, button contact 10, hold controls 9 and avatar lookup 4. [Full editor evidence](validation/stability/editor-checks.txt). The session suite initially had obsolete fuel and two-scene build expectations; those assertions now match the already-approved 0.05 fuel/metre and Login/Compound/Expedition build configuration. No tuning was changed in this pass.

## Coal recovery

All three processes passed the current-tuning recovery sequence at 10:16 UTC: no usable coal/zero fuel/all carriers spent; the recovery cooldown; out-of-view carrier replenishment; one shared hit drop; storage without duplicate respawning; master handoff preserving bag contents/issuance; exactly-once +2.5 fuel; and resumed driving. Bound-avatar uniqueness passed throughout. Main Console had no errors. Evidence: [main](validation/stability/recovery-main.txt), [Player2](validation/stability/recovery-player2.txt), [Player3](validation/stability/recovery-player3.txt).

The final two-client furnace sequence passed at 10:19 UTC. Non-masters used actual hand grab/release and physics while stopped, while moving and after explicit master handoff. Both peers observed coal removal/credit and exactly +2.5 fuel, adjusted for travel consumption. Deliberate duplicate requests did not consume twice. Both had one avatar per actor after handoff, and the main Console error query was empty. Evidence: [main](validation/stability/furnace-main.txt), [remote](validation/stability/furnace-remote.txt). Player teleports position the hand; this is not a headset/manual-input test.

## Reproduction and limitations

- Private sequence: select `3 Week 2 Test`, choose `Duck Rails > Arm Three Client Week 3 Test`, then press the toolbar Play button. Disarm after testing.
- Coal exhaustion/recovery: use `Duck Rails > Arm Three Client Week 3 Coal Recovery Test` with the same scenario.
- Public lifecycle/network faults: use `Duck Rails > Arm Three Client Stability Test`. This is an opt-in fixture with a 40-minute arm expiry and 150-second per-stage timeout. It writes coordination and per-process logs under `Temp/rails-stability`. It restores Photon simulation settings on completion, failure, disarm, and Play Mode exit.
- These fixtures teleport players/train and inject accepted gameplay actions to exercise session state. They do not establish human melee/scavenging feel or unfamiliar-player completion time.
- Three desktop processes do not establish eight-player capacity, Quest performance, VR interaction feel, or intelligible human voice. Voice-room checks establish routing only. Public return capacity contention and coordinator loss during return require separate scenarios.

At completion, Play Mode was stopped, all four test arm/control files were cleared, and the original `2 Multi Duck` scenario and enabled quick-start preference were restored. The active Login scene was clean. No build was deployed or changes committed.
