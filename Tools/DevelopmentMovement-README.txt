DEVELOPMENT VR JOYSTICK MOVEMENT - 2026-11-02

Scope and controls
- NewPlayer.DevelopmentMovement.cs and its integration hooks compile only for
  UNITY_EDITOR or DEVELOPMENT_BUILD. Normal release movement remains unchanged.
- Right-stick click toggles joystick mode without changing the turning preference.
- Left stick is head-relative, with a radial 0.18 deadzone remapped to proportional
  0-4.5 m/s movement. Acceleration is 30 m/s2 and braking is 60 m/s2 (approximately
  150 ms to full speed and 75 ms to stop). Diagonal input cannot increase top speed.
- Right-stick up uses its own 0.18 vertical deadzone and approaches 0-3 m/s upward
  velocity at 30 m/s2. This is hold-to-rise, not a grounded jump. Releasing leaves
  momentum/gravity intact; down does not add thrust. Sideways turning alone cannot
  activate lift. Lift starts from actual velocity, including falls/launches.
- Editor WASD/Space share the motor; Shift retains the existing 2.5x travel speed.
  Editor keyboard braking cannot keep VR joystick mode enabled after toggling off.

Collision and presentation
- Sweeps the head sphere and full body capsule using locomotionEnabledLayers,
  ignoring triggers and the local rig. Three slide iterations retain wall travel,
  handle corners and discard unresolved remainder. A 3 mm margin avoids contact
  chatter. Historical head/body sweeps protect gravity and room-scale movement
  even when sticks are idle. Two overlap-recovery passes handle initial overlaps
  and moving obstacles; saturated query buffers fail closed.
- Upward clearance caps powered speed before ceilings without clipping horizontal
  travel. Gravity and the native body contact solver handle the remaining gap.
- Calculations start from the authoritative Rigidbody pose. Horizontal script
  displacement uses the existing four-sample presentation history. Lift/gravity
  retain native vertical interpolation, avoiding extra jitter during forward/lift.
  Actual head/body blocking corrections remain immediate.
- No per-moving-frame InitializeValues reset. Mode transitions, warp, disable and
  pause discard powered state. Launch suspension stops input while retaining safe
  anchors; external Rigidbody velocity is preserved except into blocking geometry.
- Powered travel is capped at 50 ms after a hitch; historical collision sweeps
  still inspect the full observed displacement. Looking straight up/down uses
  the existing body heading, including after turns and warps.

Optimization and networking
- One reusable 16-hit array and one reusable 16-collider array, allocated on first
  activation only. Fixed per-player state and bounded loops; no LINQ, coroutines,
  per-frame logging/file I/O, global SyncTransforms calls or runtime diagnostics.
- At most 14 sweep queries and four overlap queries per active rendered frame:
  historical head/body up to three each, requested movement up to six, lift two.
  Each overlap result has at most 16 penetration tests; there is no allocating
  fallback. Zero-displacement sweeps exit without a query. Inactive VR mode does
  not perform development collision queries. This is additional bounded CPU work
  in development joystick mode, not a claim of zero native physics cost.
- Existing Photon head/hand pose capture, compact 39-byte pose payload, send rate
  and remote playback remain unchanged. No joystick RPCs or protocol fields.

Validation entry points (isolated Unity batch project only)
- DevelopmentMovementChecks.Run: analog speed/ramp across 72/90/120/144 Hz,
  deadzone, diagonal speed, hitch cap, turn preference/vertical gaze, thin head/body
  walls, sliding/corners, room-scale/fast motion, triggers, initial/moving overlaps,
  saturated buffers, floors, carry, lift/ceiling/release, launch, warp/toggle/pause,
  and zero managed bytes over 1,000 warmed movement calls.
- DevelopmentMovementPlayChecks.Run: real player-loop/rendered forward-plus-lift
  at 72/90/120/144 Hz, with/without gravity and zero/two physics steps. Includes
  releasing and reapplying lift while continuing forward; compares vertical
  frame travel with an identically commanded native Rigidbody reference.
- DevelopmentMovementNetworkChecks.Run: actual motor, Photon writer/codec and
  remote body playback, normal and 80-250 ms jitter/loss/reordered delivery, late
  join, finite/bounded poses, forward continuity and unchanged wire layout.
  The Collision partial adds wall sliding, inside/outside corners, ceiling lift,
  four actual right-stick toggles near a corner, and combined wall/ceiling travel.
  Each runs with normal and adverse delivery; checks local head/body penetration,
  remote eye and head-sphere clearance, transmitted/playback hands, bounded remote
  steps, exercised contacts/mode transitions and final convergence for both peers.
- MovementProductionPlayChecks.Run: lifecycle/shared-code parity, no development
  motor/state in release, and all existing NewPlayer release methods compared
  against the pre-change release DLL. Requires DevelopmentMovementBaselineRelease.dll
  beside the usual MovementDiagnosticsDevelopment.dll/Release.dll comparison files.
- Existing movement cadence/snapback/contact-gravity, head-pivot, interaction,
  respawn/network, helicopter collision/carry and avatar-recovery checks retained.

Artifacts and limits
Final isolated results: 232 development movement assertions; eight rendered
cadences including 1,440 lift-transition frames; 153,412 development network
assertions; 891 lifecycle/build-parity assertions; 8,481 avatar-recovery assertions.
Existing contact/gravity suite passed 43,200 frames / 101,807 assertions; cadence,
snapback, head-pivot timing/stress, interactions, respawn/network, helicopter
collision/carry and combat-position checks also passed. Working Editor import
finished with no console errors. Warmed movement measured 0 managed bytes over
1,000 calls; rendered lift error matched the native interpolation reference.
Editor and Android development/release C# compilations passed; no APK was packaged.
Expanded network validation (2026-11-02): 12 collision/network combinations plus
the two existing open-travel cases, 10,080 simulated frames total. Every adverse
collision case dropped 26 of 180 scheduled packets and delivered 69 out of order,
with 80-250 ms delay. No local penetration or remote head-edge overlap was measured
in these fixtures; both peers settled within 1 cm of the accepted final pose.
Results: .utmp/development-movement/NetworkCollision.log. This pass changed only
Editor tests/documentation and required no further movement-runtime changes.
Per user preference, this follow-up was automated testing only; no live room,
device installation or headset performance test was attempted.
Compiler outputs, baseline sources/DLL and run logs: .utmp/development-movement.
Tests run against Unity physics/player-loop code in .utmp/EnemySchedulingChecks,
not the user's open scene. Offline peers exercise production serialization and
playback; they are not a live Photon room or a headset performance measurement.
Final headset comfort, Quest CPU timing and live multiplayer require device checks.
No APK installation or publishing is part of this source change.
