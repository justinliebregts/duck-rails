# Pickups dropping right after grab — November 10, 2026

## Report

Seen twice in headset play (three players last week, two players on November 9): a
player grips an item, and a split second later it falls as if released, while the grip
button stays down. It persists until someone leaves the room. When a player went into
standby long enough to be removed, or left (often the master) and came back after
about 15 seconds, pickups worked again. This is probably the same fault as the earlier
"two non-masters blocked until master standby" report in
[week1-session.md](week1-session.md#coal-pickup-maintenance--november-2-2026).

## Cause (code analysis plus synthetic reproduction; not yet seen on a headset with logs)

- `PhotonNetwork.ServerTimestamp` is the server's tick count, estimated once per
  connection and then advanced by a local monotonic timer (Photon SDK docs:
  "automatically fetched after connecting (once)"). On Android/Quest that timer stops
  while the device sleeps. A short standby that does not time the connection out
  leaves that headset's clock behind real server time by the sleep duration, for the
  rest of the connection. A longer standby drops the connection; the rejoin fetches a
  new offset, which explains why going into standby or leaving fixes it.
- `PhotonMasterHandoff` heartbeats carry the sender's `ServerTimestamp`. Receivers drop
  heartbeats more than 1 s in the future or 5 s in the past. With a lagging peer, one
  side drops the other's heartbeats on every send. A lag of 1–5 s hits one direction,
  more than 5 s hits both.
- The master's `PhotonHeldItems.RecoverUnavailable` runs every second. It treats a
  holder without a fresh heartbeat as unavailable and sends an authoritative drop. So
  each pickup by that holder is accepted, then dropped within about a second. A
  non-master that sees the master's heartbeats as stale also takes master. The
  skewed headset can therefore end up as master and drop everyone else's pickups.
- The existing repeated-loss repair could not step in: pickup reports go through the
  same `Fresh` timestamp filter, so a skewed reporter's reports are discarded.

## Fix

`Assets/Scripts/Photon/PhotonMasterHandoff.Clock.cs` (new partial) plus small hooks in
`PhotonMasterHandoff.cs` and `PhotonHeldItems.cs`:

1. **Re-sync the clock.** `PhotonNetwork.FetchServerTimestamp()` runs:
   - on wake (pause or focus return);
   - when the gap between the wall clock and `ServerTimestamp` jumps by more than 1 s
     between one-second checks. This re-baselines on any room or server change,
     because each Photon server has its own tick count;
   - when a peer is shown to be live but its timestamps disagree with ours. Both
     sides fetch: the receiver sends reliable event 115 to the sender and also
     fetches itself.

   Re-syncs are limited to one every 2 s locally, and requests to one every 10 s per
   peer.
2. **Item recovery judges liveness by arrival cadence.** Every heartbeat that passes
   the structural checks is recorded, including ones rejected for their timestamp.
   A peer counts as live when ready heartbeats keep arriving locally at least 0.5 s
   apart, their sender-clock spacing matches local receipt spacing to within 0.75 s,
   and the newest arrived within 5 s. Item and bag recovery then treat that peer as
   available whatever its clock offset. These still release: a not-ready heartbeat,
   five seconds of silence, an inactive player, or a burst of queued old heartbeats
   (which cannot prove liveness). Master election is unchanged and still uses only
   accepted heartbeats.
3. **Diagnostics for the next occurrence**, as rate-limited warnings (Console/logcat)
   and diagnostic-report events:
   - `Master recovering held object: view, holder, owner, reason=` is logged on the
     master for every availability drop. The reason is inactive, reported not ready,
     heartbeat stale (age), no heartbeat since observation start, or departed.
   - `Heartbeat clock disagreement: actor=… differ by N ms`.
   - `Re-syncing Photon server time (reason)`.

   The existing `Pickup revoked (master released active hand)` warning still fires
   on the player who lost the item.

No serialized data, prefab, scene or Photon partition changed. Event code 115 was
unused. A mixed group with older builds still connects; only clients with this fix
send or answer re-sync requests.

## Validation

- Unity MCP confirmed `X:/Work/duck-rails/Assets`. The editor compiled with no
  errors, and the new script meta GUID is unique.
- **Reproduction before the fix:** the new `HeldItemRecoveryChecks.ClockSkew` cases
  ran against `main` HEAD's runtime code in the disposable batch copy. All existing
  assertions passed, then the master released a live holder whose clock lagged 8 s:
  `Exception: live holder whose clock lags 8 s keeps its pickup`. See
  [before](validation/clock-skew/batch-before.txt).
- **After the fix**, all of these passed in Unity 6000.3.24f1 batch mode:
  - `PickupRecoveryChecks` (298, including 150 regrab assertions);
  - `RoomObjectRecoveryChecks` (289);
  - `PhotonGrabbableChecks`;
  - `HeldItemRecoveryChecks` (143), twice in one process.

  The new cases cover the skewed live holder kept, the re-sync request, a skewed
  not-ready holder released (including an equal-millisecond state change), a queued
  burst not proving liveness, and a skewed peer released after going silent. See
  [after](validation/clock-skew/batch-after.txt). The second same-process run exposed
  an equal-timestamp bug in the first version of the arrival tracker; it was fixed
  before these results.
- `MasterHandoffChecks` already fails on unmodified `main` (stale fixture: it names
  the scene `PlayScene` instead of `RequiredScene` and gives the avatar no owner; even
  with those patched in the disposable copy it next fails at "unfocused local actor
  cannot take authority"). It was not used as evidence and was not changed here.
- **Live single-client check** (authenticated manual sandbox, Running): sign-in
  through the master server, game server and expedition caused no re-syncs. An
  earlier attempt, before re-baselining on server or room change, showed one
  unnecessary fetch at the server switch; that is fixed. A temporary editor probe
  then moved Photon's internal server-time offset back by 8 s to simulate a sleep.
  Within about a second the client logged `Re-syncing Photon server time (local
  timer jumped 8000 ms against the wall clock)`, and `ServerTimestamp` came back
  within 11 ms of the expected value. The world stayed ready and in the room. See
  [live evidence](validation/clock-skew/live-sandbox.txt). The probe was not committed.
- **Two-client regression** with the fix (`RailRegressionLauncher.Start("coal", 2)`,
  two authenticated editors, 09:18:55 UTC): both clients reported `PASS COMPLETE`.
  The run covered non-master palm grabs and releases of real coal while stopped,
  while moving and after a master handoff, plus empty-fuel recovery, with
  exactly-once deposits and unique avatars. During the run the main Console had no
  re-sync, clock-disagreement, master-recovery or pickup-revoked warnings. Its only
  error was the known Meta XR `ErrorFormFactorUnavailable`. See
  [main](validation/clock-skew/coal-two-client-main.txt) and
  [remote](validation/clock-skew/coal-two-client-remote.txt). These clocks were
  healthy, so this shows no regression rather than the skew path.

## Remaining

- Real two- or three-headset acceptance: put one Quest into a short standby (under
  about 10 s, so it stays connected), wake it, and confirm that every player's
  pickups stay in hand. Each client should log `Re-syncing Photon server time`. If a
  drop still happens, the master's `Master recovering held object … reason=` line
  identifies the path.
- Whether Photon's timer actually pauses during Quest sleep is inferred from the SDK
  docs and IL2CPP/Android monotonic clocks, not measured on a device. The cadence
  liveness rule protects pickups either way, but a measurement would confirm it.
- No diagnostic reports matched this bug. The backend inbox held 43 open, all
  editor-originated errors, tests or manual reports, and none were resolved.
