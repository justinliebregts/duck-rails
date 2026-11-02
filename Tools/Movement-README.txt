DEFAULT VR MOVEMENT - 2026-10-30

The headset-validated head-pivot-v4 movement (formerly B) is now normal VR movement
in Editor, development and release. No button, settings file or diagnostic session
is needed. Old movement-interpolation.txt files are ignored. Desktop is unchanged.

Runtime
- NewPlayer.Awake enables interpolation and installs the pose synchronization
  stages once, independently of logging.
- NewPlayer.MovementPose.cs separates physics from presentation. Collision anchors
  use the Rigidbody pose; free travel uses native interpolation. Hand contact and
  release use four fixed value samples at the same one-physics-step display delay,
  without prediction. Head/body blocking corrections remain immediate.
- Hand collision rules, launch thresholds and jump tuning are unchanged from the
  validated build. Warp clears momentum/history and places the destination beneath
  the tracked head, including after room-scale walking.
- Turning pivots around the headset; continuous root recentering is removed.
  Gameplay needing horizontal player location uses the guarded head helper.
- Helicopter and ragdoll retain their existing global sync calls through
  SyncTransformsPreservingMovementPose; no additional sync call is introduced.
- Disable clears display history and unlinks pending pose work. Re-enable seeds
  fresh physical anchors. Pause clears old contact display history.

Optimization
Four value samples, bounded per-player state, no recorder buffers/worker thread,
no per-frame file I/O/logging, no new collision queries or global sync calls, no
recurring managed allocations in the tested warmed movement/sync/feedback paths.
Player-loop insertion allocates at initial setup only. Initial velocity-history
allocation is retained. No new network messages or protocol fields.
Physics stays tied to the selected headset refresh rate (1/72 at 72 Hz). A render
frame can still contain zero or two physics steps; smoothing handles that cadence.

Cleanup
Removed the recorder partial, recorder implementation, Trace hooks, A/B state and
file reader, report tools and recorder-only tests. MovementTestRig is now a small
Editor-only fixture. Useful movement/gameplay regression tests remain. The existing
development joystick control is separate from the experiment and remains unchanged.

Validation
MovementProductionPlayChecks verifies automatic startup, disable/re-enable/pause,
absence of recording APIs and normalized movement IL parity across build variants.
It uses the isolated harness's compiled comparison DLLs in its parent directory.
HeadPivotContactGravityPlayChecks runs 90 combinations / 43,200 frames including
regular/irregular cadence, gravity, lowering, hopping, two-hand contact, body/launch
controls, blocking walls, teleport and the 3 mm contact regression. Measurements
are Editor-only and do not require runtime instrumentation.
Cadence, snapback and interaction checks cover 72/144 Hz display, turn/carry/contact
combinations and zero/two physics steps. Integration suites cover death/respawn,
gameplay positions, item sounds, analytics, helicopter and offline networking.

Artifacts
Existing headset reports in .utmp/movement-B-* and earlier A folders are preserved.
.utmp/movement-production/before has pre-promotion sources; archived has the retired
investigation code/tools. validated-v4-contact-gravity.csv and contact-parity.json
preserve the baseline comparison. pass1 and final hold Unity logs and suite results.
These local .utmp archives are not shipped or included in a commit.

Limits
Checks run real Unity physics/player-loop code in an isolated project. They do not
replace a final headset check with diagnostics removed. Network checks use offline
peers and production codecs, not a live multiplayer room. Unrelated loading/render
stalls and boundary interruptions are outside this movement cleanup.
