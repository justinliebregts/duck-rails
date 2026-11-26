# Expedition departure horn — November 21 editor run

**Result: PASS for every horn-trigger case, adapted to two clients. Level and feel still need a human.**

## Setup

- Unity 6000.3.24f1. Main editor plus MPPM Player2 (`4 Diagnostic Two Client`), using two authenticated accounts on real Photon. Quick start was off, so both clients landed in the Compound through Login. No headset was used.
- Harness: `Assets/Editor/Checks/Networking/RailDepartureHornLiveChecks.cs` (editor-only, opt-in). It coordinates both clients through `Temp/rails-departure-horn/control.json`. Runtime game code was not changed.
- What was measured: each client checked the `RailAudioDirector.departureHorn` AudioSource ("Expedition departure horn"), found by reflection, on every editor update. A start counted when `isPlaying` went from false to true, or when `time` jumped back during playback. Each start was logged with the room kind, phase and loaded scenes.
  - Audio is live in the MPPM clone. Every start played for 2.00–2.30 s, matching the 2 s clip, through the Effects mixer, with `audioMasterMute=False`.
  - The longest gap between checks while the horn played was 1.57 s, during the private scene swap. A horn that started and finished inside one gap would have been missed. No gap that long happened at arrival.
- Stayer countdown evidence: whenever the Compound `Deadline` was set, the harness logged the countdown as `RailAudioDirector.UpdateCountdown` derives it.

## Cases (final run, logs `departure-horn-main.txt` / `departure-horn-player2.txt`)

| Case | Expected | Result |
|---|---|---|
| A1 public: Player2 boards, main stays | Player2 hears 1 horn as its transfer starts, none on arrival. Main hears none and sees the countdown. | **PASS.** Player2 heard 1 horn, starting in `compound/Compound` with a room request in flight, and none in the 6 s after arrival. Main heard 0 horns and logged the countdown from 20 s to 0. |
| A1 crew return | No horn | **PASS** (0 / 0) |
| A2 public: both board together | 1 horn each, none on arrival | **PASS** (1 / 1, both starting in `compound/Compound`; same expedition room) |
| A2 crew return | No horn | **PASS** (0 / 0) |
| B private create + join by code in staging | No horn | **PASS** (0 / 0) |
| B private launch | 1 horn each when leaving staging, none on arrival | **PASS** (1 / 1, `private/Departing` while the Compound scene was loaded) |
| B return to staging | No horn | **PASS** (0 / 0) |
| B private replay | 1 horn each, none on arrival | **PASS** (1 / 1) |
| B replay return | No horn | **PASS** (0 / 0) |
| C host launches alone | Host hears 1 horn; Player2 is in a public Compound and hears none | **PASS** (1 / 0) |
| C Player2 joins by code mid-run (phase Running) | No horn for Player2 or the host | **PASS** (0 / 0, checked 6 s or more after Player2 arrived) |

Totals: 4 horn starts per client, each one at a departure.

An earlier run (`departure-horn-run1-*.txt`) also passed A1, A2 and the first private launch. It then stalled at the private return, because the harness called `FinishRun` only once. The running room commits several revisions per second, so that one Commit lost its CAS. The harness now retries, which was a test-harness issue, not a game issue. In that run's A1, the stayer countdown was not logged because of a harness ordering bug. Both bugs were fixed before the final run.

## Not covered / needs human

- **Needs human:** listen to the horn's level and feel against voice chat and the existing cockpit horn, on Quest.
- Three-player variant (two board and one stays): not run, because three-client runs crash Unity on this machine. A1 and A2 cover the same code paths.
- No headset checks.
