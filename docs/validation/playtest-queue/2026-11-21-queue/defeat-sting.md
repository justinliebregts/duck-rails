# Defeat sting on a lost run — November 21 editor run

**Result: PASS for every trigger case, adapted to two clients. The late-joiner case does not apply, because fresh joins during Ending are refused. Mix and feel still need a human.**

## Setup

- Unity 6000.3.24f1. Main editor plus MPPM Player2 (`4 Diagnostic Two Client`), using two authenticated accounts on real Photon. Quick start was off, so both clients entered through Login. No headset was used. Admin invincibility was off on both clients (`invincible=False` is logged at each down).
- Harness: `Assets/Editor/Checks/Networking/RailDefeatStingLiveChecks.cs` (editor-only, opt-in). It coordinates both clients through `Temp/rails-defeat-sting/control.json`. Runtime game code was not changed.
- Measurement: on every editor update, each client read `RailAudioDirector.defeat` ("Defeat sting") and `RailAudioDirector.music` ("Bridge music") through reflection.
  - A start counted when `isPlaying` went from false to true, or when `time` jumped back during playback.
  - Sting volume was sampled every 2 s, and every 0.25 s after Returning began.
  - Each client also logged the run's finish reason and whether its duck was aboard and down.
- Source wiring checked live: clip `Train_Defeat` is 14.10 s long, on the Effects mixer, with `spatialBlend=0` (2D) and `loop=False`.
- How each loss was reached:
  - Everyone down: each duck warped to `RailTrain.TransformPoint(6,.5,0)`, off the train, then called `RailExpedition.RequestDown()`.
  - Crash: the master called `RailTestTools.Bridge()`, which moves the train to finale +45 m, with the bridge raised and the train stopped. The master then called `SetTrainMoving(true)`. The train reached `bridgeZ` about 9 s later and the run ended with "Bridge collision".
  - Success: the master called `RailTestTools.SimulateSuccess()`.

## Cases (final run: `defeat-sting-main.txt` / `defeat-sting-player2.txt`)

| # | Case | Expected | Result |
|---|---|---|---|
| 1a | Public expedition, both ducks down off the train ("Crew down") | 1 sting per client | **PASS** (1 / 1). Both ducks were off the train (`aboard=False`) when they went down. By the first Ending frame, when the sting started, both were back aboard (`aboard=True down=True`). Each sting played its full 14.1 s at volume 1.0. The crew then returned to a new Compound with no restart. |
| 1b | Private run, both ducks down off the train | 1 per client | **PASS** (1 / 1), full 14.1 s at volume 1.0 |
| 2 | Private run, raised-bridge crash ("Bridge collision") | 1 per client | **PASS** (1 / 1), both aboard and not down |
| 3 | Successful escape (`SimulateSuccess`, "Test success preview") | No sting, bridge music only | **PASS**: 0 stings and 1 bridge-music start per client. The music stopped on the return to staging. |
| 4a | Fade when the crew is sent back | Sting fades over about 1.5 s | **PASS, forced.** At sting t = 4.5 s, the master committed Ending → Returning early. Volume went 0.98 → 0.46 → 0.30 → 0.13 → 0, and the sting stopped 1.49 s (Player2) and 1.50 s (main) after Returning began. |
| 4b | Late joiner during Ending hears it from the start | — | **N/A.** The host lost a solo private run. Player2 then tried to join by code during Ending and was refused: "This expedition is ending. Join after the crew returns." The host heard 1 sting and Player2 heard none. After the refusal, Player2 landed in a different public Compound (46716) from the one it left (23024). |

Every launch, departure and return in between played no sting (0 / 0). Totals: 5 sting starts on main and 4 on Player2, each one at a loss. No sting restarted during playback.

## Findings

- **In normal play the fade never runs.** Ending lasts 32 s, and the 14.1 s sting ends about 18 s before Returning. In every natural loss (1a, 1b, 2, 4b), the sting ended on its own at full volume while still in Ending. The fade code works when Returning comes early (4a), but in practice that would only happen if the Ending length or the clip length changed.
- **Off-train listeners.** Downed ducks were off the train when they went down. By the time the sting started, the ending presentation had already moved them aboard. Because the sting is 2D, this makes no difference to what they hear.
- An earlier attempt (`defeat-sting-run1-*.txt`) passed 1a and 1b, then stalled on the crash. The main editor was no longer master after a private launch, and the harness issued the master-only `Bridge()` from the main editor. The harness now acts from whichever client is master. This was a harness issue, not a game issue.

## Not covered / needs human

- **Needs human:** listen to the sting's mix against the bridge-crash sound (`bridgeCrash` plus collapse) and against the rest of the ending on Quest. Also judge the volume of `defeatVolume = 1`.
- No headset checks. Three-client runs were not attempted, because they crash Unity on this machine.
- No late joiner could be tested, because the room is closed during Ending/Returning. A transport reconnect of an existing crew member during Ending was not tested.
