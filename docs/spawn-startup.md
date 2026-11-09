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
