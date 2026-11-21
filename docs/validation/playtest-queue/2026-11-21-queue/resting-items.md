# Resting items no longer "hit" riders — November 21 editor run

**Result: checks 1 and 3 PASS. In check 2, the held and thrown controls PASS. Lobs that fly longer than 1.5 s do not register a hit (finding; window unchanged). Feel still needs a human on Quest.**

## Setup

- Unity 6000.3.24f1. Main editor (master, actor 1) plus MPPM Player2 (actor 2), using `4 Diagnostic Two Client` on real Photon. The run used a private code room and `StartPrivateRun`. No headset was used, and no runtime code was changed.
- Harness: `Assets/Editor/Checks/Networking/RailRestingItemHitLiveChecks.cs` (editor-only, opt-in, coordinated through `Temp/rails-resting-items/`). Logs: [resting-items-main.txt](resting-items-main.txt) and [resting-items-player2.txt](resting-items-player2.txt).
- Item: the block's `RailGoldBar(Clone)` (view 18). Its flesh sound is `FleshImpact_Items`, with radius 0.044 and `playerLaunchSpeed` 0. Main took it with `PlayerHand.ForceGrab`, released it, and set it on the train floor. That made main the owner. Player2 was warped to stand on it.
- Hit signals, counted without instrumenting game code:
  - On the owner (main):
    - `LOCK`: `PhotonPlayer._swingLockUntil` changed, meaning `TickPlayerSwings` registered a hit and sent `ReceiveItemHit`.
    - `FLESH`: `ItemSounds.nextFleshImpactTime` changed, meaning `PlayFleshImpact` ran from a sweep or from `OnCollisionEnter`.
  - On the victim (Player2):
    - `EVT`: a received `RoomRpc` PlaySound event (code 101) with impact particles. This is the remote flesh SFX plus blood burst.
    - `ACCEPT`: the local `PhotonPlayer._nextItemHitTime` changed (`ReceiveItemHit` was accepted).
  - On both clients, `BLOOD`: the number of active pooled blood hit effects went up.
- Counterfactual metric on main, `overlapOpportunities`: frames in which the bar's motion overlapped Player2's avatar through `PhotonPlayer.OverlapsSwing` at or above `MinHitSpeed` (2 m/s). Before the change, each such frame would have been a hit.

## Results

| Check | Measured | Verdict |
|---|---|---|
| 1. Resting bar under Player2, train at 4.0 m/s cruise, 20 s standing | 0 locks, 0 flesh, 0 events, 0 accepts, 0 blood on either client. Overlap opportunities were present in 961 of 962 frames. The bar stayed 0.34 m from Player2's head, with no enemies within 6 m. | **PASS** |
| 1b. Same, crouched, 12 s (plus acceleration from 0 to 3.94 m/s) | 0 on every signal. Opportunities: 569/580 crouched and 48/98 during acceleration. | **PASS** |
| Gate control: `NotifyPlayerThrown` stamped on the same resting bar while cruising | First hit after 0.02 s. In the window: 6 locks and accepts, 14 flesh plays and 12–14 events with blood. Then 0 owner-side signals from 1.6 s to 5.6 s, with opportunities still at 185/186. Player2's last flesh event arrived about 1.7 s after the stamp; the 2 counted "after the window" were in-flight events received late. | Shows that the 1.5 s gate alone stops the old blood/SFX spam |
| 2a. Held: main holds the bar with an extended reach and walks through Player2 (train stopped) | 2 locks and accepts, 3 flesh and blood on both clients. All happened while `held=True`; closest approach 0.17 m. | **PASS** (simulated rig walk, not a real swing) |
| 2b. Throw: release, then 7 m/s at the head from 1.4 m | 1 hit 0.16 s after release, accepted by Player2 with blood | **PASS** |
| 2c. Lob, 1.2 s flight (apex 1.7 m) | 1 hit at 1.17 s, accepted, with blood | **PASS** |
| 2d. Lob, 1.7 s flight (apex 3.5 m) | 0 hits, although the bar passed 0.05 m from the head at 1.67 s | **Finding: does not count** |
| 2e. Lob, 2.2 s flight (apex 5.9 m) | 0 hits; closest approach 0.06 m at 2.18 s | **Finding: does not count** |
| 3. `ItemSounds.OnCollisionEnter` flesh for a remote rider on a resting item | The remote avatar has **0 colliders** on the owner (`AVATAR colliders on main for actor 2: 0`). There were 0 contact frames and 0 `FLESH` during 32 s at cruise. `OnCollisionEnter` cannot resolve a remote `PhotonPlayer`, and the victim's own client does not own the bar, so this path cannot fire. | **PASS** |

## Notes

- The flight window works as coded: any flight longer than 1.5 s, including a slow lob, cannot hit a player. To fly longer than 1.5 s, a lob must rise about 2.8 m or more above its landing height, so this mainly affects tall arcs or throws dropped from height. The window was left unchanged. Raising it trades directly against how long a newly set-down item on a moving train keeps "hitting" a rider (as the gate control shows). A human should decide.
- The held control moved the bar with the simulated rig's walk (about 7.8 m/s). Simulated hands are capped at 2 m/s, so a true hand swing could not be driven. A human should confirm a real swing.
- Enemy hits were not tested (unchanged by this fix).

## Needs human (Quest, 2 or more clients)

- Set a gold bar down, have a duck stand on it, then ride at cruise: no blood or hit SFX.
- A real held swing and a normal throw still hit.
- Decide whether high or slow lobs that fly longer than 1.5 s should still hit.
