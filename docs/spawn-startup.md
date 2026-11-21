# Photon spawn startup regression — November 9, 2026

## Fault and correction

The reported Console contained duplicate room-view IDs 51–65 on RailCoal while
processing incoming PUN instantiation events. A fresh baseline did not reproduce
those exact collisions, but the two-editor observer caught actor 2 becoming master
while both clients still had `WorldReady=false`. Previously, the heartbeat/election
gate considered an avatar in the destination scene sufficient readiness. The
world's cached cargo/lighting snapshot can still be incomplete at that point.
Seeding on an incompletely loaded successor can allocate IDs already assigned by
the previous master whose spawn messages have not arrived yet.

Normal `PhotonMasterHandoff.Ready` handoffs require the Rails world snapshot to
be ready and no room/scene request in progress. This uses RailWorld's retained
cargo manifest and per-item state readiness. The separate interrupted-startup
recovery path below avoids making master-produced state a prerequisite for
replacing a stalled master. Non-Rails behavior is unchanged.

The first catalogue sweep then exposed a second edge: the master changed during
private departure while the new world was unready, because a recent **staging**
heartbeat was still eligible for five seconds. `SelectCandidate` now also checks
the candidate's existing retained readiness token against the current run and
phase. No heartbeat wire-format change is needed. The regression fixture injects
a fresh ready heartbeat and checks that loading, staging and previous-run tokens
are rejected, while the current ready token remains eligible. Temporary test
changes to local cached properties/heartbeat data are restored synchronously.
See [staging trace](validation/spawn-startup/stale-staging-main.txt).

### Interrupted-startup recovery

The additional review identified a dependency cycle in the initial readiness
fix: a peer waiting for route selection, `rails.seeded`, or the first cargo pose
could not replace the silent host that was supposed to produce that state.

The normal election gate remains strict. A separate Rails-only fallback allows
a peer with the destination scene and avatar loaded to recover Departing/Running
startup after five seconds without host heartbeats. A fresh heartbeat counts as
host activity even when its readiness bit is false, so a live loading host does
not trigger this fallback. Route readiness is deliberately not required because
route selection is also master-produced state.

Before requesting authority, the peer sends event 114 reliably to itself through
the server, on the same channel as PUN instantiation. Its echo follows cached/live
spawns already queued by the server. The probe is bound to room, run, phase,
master and a local sequence; scene/room transitions, renewed host activity,
resuming input and master changes invalidate stale replies. Only after that echo
does the peer request the existing server-compared master change. Normal world
readiness still gates player input; recovery does not mark an incomplete world
ready.

`RailStartupRecoveryChecks.Start(0..2)` is an opt-in two-editor regression fixture
for interruption before route selection, after loot spawning but before manifest
commit, and after manifest commit but before initial cargo serialization. It
temporarily holds the host's `RailWorld`, stalls only that editor's main thread
(bounded to 25 seconds, with Photon's background ACK thread still alive), waits
for the peer to recover, then resumes the original host. It verifies exact loot
counts, unique registered view IDs/markers, preservation of pre-stall loot
identities, and matching inventories on both peers. It stops the scenario and
restores the held component on completion/failure.

The first fixture attempt used Unity's pause flag, which MPPM mirrored to both
editors; it was stopped and is not acceptance evidence. The separate
`recovery-coupled-pause-*.txt` logs retain that attempt. One unrelated Unity Editor
Connect token-exchange exception occurred there. The fixture records that editor
service exception separately from gameplay errors, along with the existing
OVR/OpenXR exclusions.

All three isolated-host faults passed on November 9 with two authenticated Unity
6000.3.24f1 editor processes. The host resumed after 11.16 seconds (before route),
5.23 seconds (before manifest), and 4.92 seconds (before first pose), respectively;
the five-second silence interval is measured from the last heartbeat, not the
fixture's stall timestamp. In each run both peers reached Running, agreed on all
45 loot ID/spawn-token pairs, preserved every pre-stall loot identity and reported
no duplicate-ID or unexpected gameplay errors. The before-pose trace confirms the
peer had the committed manifest while still unready before takeover. See
[reconciled recovery summary](validation/spawn-startup/recovery-summary.txt) and
`recovery0-main.txt` / `recovery0-remote.txt` through fault 2 in that folder.
Unity compilation, session rules, drive checks and `git diff --check` also passed.
After the live runs, fixture-only cleanup moved coordination sentinels into Temp
and removed the superseded coupled-pause branch; that cleanup compiled. Final
state: `Playing=False`, `Runner=False`, `Scenario=NotRunning`. The earlier catalogue
sweep remains separate evidence; these follow-up tests exercise startup recovery,
not another all-block traversal or headset acceptance.

Two related transition hazards are corrected:

- `PhotonSession.LoadWorldScene` preserves `isRuntimeInstantiated` objects.
  PUN's obsolete `IsSceneView` is an alias for `IsRoomView`, and is also true for
  dynamically spawned room-owned coal/gold; it cannot distinguish authored scene
  objects from runtime loot.
- `RailExpedition.Update` waits while the Photon message queue is paused or a
  room/scene transition is in progress, preventing authority updates in a partial
  world.

No prefab GUIDs, scene assets, authored placements or networking stack changed.
Diagnostic report inboxes and report JSON were not inspected.

## Validation

Verified Unity MCP target: `X:/Work/duck-rails/Assets`, Unity 6000.3.24f1.

- Unity compilation, session rules, assembled route-order checks, spawn-pool/
  placement checks and exported catalogue lighting validation passed. Existing
  shortfall and CutePixel missing-glyph warnings were reported separately.
- Drive (17), Week 2 rules (24), and current resource-balance checks (2,740) pass.
  The legacy Week 2 test still expected ten starting fuel; corrected its assertion
  to the already-approved fifty fuel in `resource-balance.md`. Gameplay tuning
  was not changed.
- Two authenticated editor processes used the actual Test Center
  `RailTestRunner.Start("two-clients")` scenario and Photon room 11111.
- Fixed normal run `acbfdca6ad7a43cabeaf9e35f82d0724`: both clients observed exactly
  24 coal, 40 gold and 16 enemies, unique live view IDs, valid registry entries,
  unique loot markers and identical ID/spawn-token inventories. No duplicate-ID
  errors or unexpected captured errors occurred. The real election gate rejected
  a synchronous `MarkWorldLoading` probe on each client.
- Explicit master transfer to the main client, main departure, automatic host
  succession, and main late join as actor 3 retained the same run and all 64 loot
  identities. Both clients repeated the route smoke checks after rejoining.
- Evidence: [normal main](validation/spawn-startup/normal-main.txt),
  [normal remote](validation/spawn-startup/normal-remote.txt),
  [baseline main](validation/spawn-startup/baseline-main.txt) and
  [baseline remote](validation/spawn-startup/baseline-remote.txt). The baseline's
  station graph assertion was a fixture error: the safe station intentionally has
  no enemy graph. The fixture was corrected before the passing checks.

Full-catalogue two-editor sweep passed on the final runtime changes: all **16
scavenging blocks**, the starting station and bridge finale, in four batches of
four encounters. Both peers passed six visits per batch (**48 peer/block
checks**), with identical loot ID/spawn-token inventories and no captured
duplicate-ID or unexpected gameplay errors. The master was retained during
unready private departures; a subsequent handoff in batch 2 occurred after world
readiness. [Reconciled summary](validation/spawn-startup/summary.txt); per-client
logs are `batch0-main.txt` / `batch0-remote.txt` through batch 3 in that folder.
Final editor state: `Playing=False`, `Runner=False`, `Scenario=NotRunning`.

One targeted heartbeat positive-control attempt failed because existing real
heartbeat ordering can reject an injected timestamp. The fixture now removes the
candidate's existing heartbeat before injection and restores it afterward. The
affected catalogue batch was rerun; the failed attempt is preserved in
`validation/spawn-startup/fixture-attempt-remote.txt`, not counted as acceptance.

### Separate findings and limits

The catalogue is not uniformly provisioned for the newer crew-scaled resource
targets. In two-player private routes (target 9 coal / 16 gold per encounter),
the four batches actually selected coal/gold/enemy totals of **35/41/16**,
**35/27/16**, **36/20/13**, and **34/28/16**, respectively. These match both clients'
selected marker lists; they are authored candidate shortfalls, not lost network
spawns. Gold is particularly undersupplied in older disabled-normal-run blocks.
The third batch also selects fewer enemies under the current spacing/quadrant
rules. Existing editor checks report these shortfalls. No candidate positions or
balance settings were silently changed during this networking fix.

Existing CutePixel missing-glyph warnings and Meta/OpenXR desktop startup errors
remain separate. The observer excludes known OpenXR/OVR initialization errors
from its gameplay-error counter. One account-registration retry recovered with
HTTP 200; authenticated tests subsequently completed. No sandbox-blocked network
query or diagnostic-inbox query was involved.

## Repeat the focused checks

`RailSpawnNetworkChecks.Start(-1)` launches and observes the normal two-client
quick start. `RunCatalogue()` runs four batches of four scavenging blocks. Each
batch first launches the real two-client scenario, then transfers both accounts
into a private room so the existing ordered-route override can select disabled
catalogue blocks. Each peer visits the station, four encounters and finale,
checking lighting readiness, visible renderers, active colliders, encounter graph
presence, exact selected loot/enemy counts and network identity uniqueness.
Both acknowledgements are required before advancing. Evidence is written under
`docs/validation/spawn-startup/`, not the diagnostic inbox.

These are opt-in editor fixtures. They temporarily override the route callback
and enemy-invincibility callback, restore them on exiting Play, and use the Test
Center's scenario/quick-start restoration. `RailSpawnNetworkChecks.Stop()` cancels
the sweep and stops both clients. No saved authoring/player preferences are edited.

This is desktop transport and block-loading evidence, not natural full-run
gameplay, full navigation traversal, headset interaction, voice or Quest
performance acceptance. The original screenshot's exact packet interleaving was
not captured; the premature election was observed and its readiness gate is now
explicitly regression-tested.

## November 20 room 26164 — coal views 151–159 (fix implemented, queued)

Incident: three clients departed public Compound 75292 together into expedition
26164 ([report](validation/playtest-queue/2026-11-20-rc/bridge-return.md)). Actor 2
(main editor) logged duplicate `RailCoal(Clone)` views 151–159 at 19:49:53 UTC and
kept them unready, also after its transport rejoin; actors 1 and 3 were ready.

Trace from the client samples (`bridge-return-samples.tsv.gz`):

- All three joined 26164 between 19:49:46.1 and 46.9 with actor 1 as master; none
  was `WorldReady`. Actor 1 never reported ready while master.
- **Actor 3's Photon server clock was unsynchronised**: it sampled
  `PhotonNetwork.Time = 0` from 19:49:46.9 to at least 51.6. This happened in no
  other room of the run. Photon returns `ServerTimestamp = 0` until the
  first time fetch is acknowledged.
- With a zero clock every heartbeat from the live, loading host failed `Fresh`.
  Its observation start was also stamped 0. After the sync, the wrapped (negative)
  host stamps still failed the `sentAt - observationStartedAt` filter. The host
  therefore looked silent.
- The master switched 1 → 3 at 19:49:52.85 on all three clients. That is about
  5.9 s after actor 3 joined, just after its five-second observation window. No
  test handoff was requested. The only path that elects a non-ready peer is the
  interrupted-startup recovery (event 114 echo, then `SetMasterClient`), so the
  takeover came from there.
- Actor 1 was seeding cargo at that time: its samples have a 0.6 s frame gap from
  51.98 to 52.57. The views 1–159 are ordered flashlights, gold, then coal. The
  duplicates are exactly the last nine coal (`coal-55`…`coal-63` by index).
  Actor 3's `SeedCargo` filled the markers it had not yet received, reusing room
  IDs 151–159 that it did not know about. Actor 1's in-flight tail then arrived
  with the same IDs.
- **Two spawns, not one registration replayed:** after actor 2's rejoin, the room
  cache replayed both instantiations for each of 151–159 and none for 1–150. That
  requires two cached events per ID. A preserved local object plus a single cached
  event would have duplicated every view. The incident left no per-view markers or
  senders. Senders actor 1 and actor 3 for marker-identical spawns are inferred from
  the timeline and the seeding code; that interleaving was not captured.

Why event 114 did not help: its echo covers spawns queued at the server before the
probe. Actor 1 was alive, did not yet know it had lost authority, and was still
sending. The barrier cannot cover spawns a live old master sends after the switch.

Correction (`PhotonMasterHandoff*.cs`, `RailWorld.cs`, `RailExpedition.cs`):

1. **No election without a synchronised clock.** While a connected peer's
   `ServerTimestamp` is 0 it keeps publishing heartbeats but neither elects nor
   recovers, and it re-requests server time (2 s cooldown). An observation begun
   on that clock is restamped, with a fresh five-second window, on the first
   synchronised frame.
2. **Silence means no arrivals.** Startup recovery, and the normal path for an
   unknown master, also require no structurally valid heartbeat *received* from
   the master in the last five seconds by local time. Disagreeing stamps alone no
   longer make a live host silent.
3. **Post-switch settle handshake (events 118/119).** A newly confirmed local master
   sends the previous master a reliable query. The server orders the switch before
   that query. The previous master's reliable reply on the instantiation channel
   therefore follows every spawn it sent while it still believed itself master.
   Until the reply, `PhotonMasterHandoff.CanSpawnRoomObjects` is false. The wait
   also ends if that peer leaves or goes inactive, or after five seconds. A host
   stalled that long has already received the switch and dispatches it in
   FixedUpdate before its next Update can spawn. The gate covers
   `RailWorld.MaintainCargo` (cargo seed, flashlights, manifest),
   `RailExpedition.SpawnEnemies` and `SpawnCarrierDrops`. It also requires a running
   message queue. All are stateless retries. With one spawn per marker, the room cache
   is clean, so a rejoining peer replays one instantiation per view.

Not changed: event 114 recovery itself, heartbeat wire format, enemy/item spawn
markers, room-RPC bag/equipment spawns and the Duckz `EnemyManager` spawner.

Validation: Unity MCP target `X:/Work/duck-rails/Assets` (6000.3.24f1) compiled
with no errors. In the disposable batch copy, the new
`MasterHandoffChecks.RunStartupSettle` passed 13/13 assertions. They cover the
unsynchronised clock, the post-sync restamp, the positive silence control,
arrival-based liveness, the settle query/reply/nonce, a remote successor and the
timeout. `HeldItemRecoveryChecks` passed 166. The full `MasterHandoffChecks.Run`
stops at the pre-existing "lowest ready actor" assertion on HEAD code too, a fixture
break predating this change and still open.
[Batch evidence](validation/spawn-startup/coal-collision-26164-settle.txt).

**Not yet verified:** the three-client acceptance described in the playtest queue.

### November 21 — spawns stuck behind a stalled host's send window (fix implemented, uncommitted)

The two-client stalled-host faults 1 and 2 failed with 56–57 duplicate loot views. Photon 4.1.8.17
keeps at most `SequenceDeltaLimitSends` = 50 unacknowledged reliable commands in flight. The
stalled host's seed (enemies, then 56+ coal/gold) was issued but partly unsent when it froze. The
successor's settle timed out after 5 s and it seeded the same markers with the same view IDs.
The host then resumed and sent its queued spawns.

Correction (`PhotonMasterHandoff.Settle.cs`, `PhotonNetworkPart.cs`): when a settle times out while
the previous master is still connected, the new master moves the room view ID cursor 300 past
every registered view (`PhotonNetwork.SkipRoomViewIds`) before it spawns. For the rest of that
room it then destroys, every 0.25 s, any room view below that floor whose `rails/run/marker`
duplicates one of its own. `PhotonNetwork.Destroy` also clears the cached instantiation. The
acknowledged path is unchanged. The stalled-host fixture now requires each pre-pause marker
exactly once, not the original view ID and token, because unsent spawns are replaced.

Two-client validation, Unity 6000.3.24f1, real Photon US: faults 0, 1 and 2 PASS with zero
`PhotonView ID duplicate` errors, identical peer inventories and every marker once. Faults 1 and 2
kept 50 and 48 of 106 pre-pause identities; the rest were the successor's copies. Two-client
departures, three acknowledged handoffs and a non-master reconnect still pass.
[Evidence](validation/playtest-queue/2026-11-21-queue/departure-settle.md)
