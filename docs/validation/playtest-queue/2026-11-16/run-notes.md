# November 16 queue run

Run in progress. Starting HEAD `789a5be13f11d220f9b09a520a16fc204d169c36`, main,
Unity 6000.3.24f1, verified MCP project `X:/Work/duck-rails/Assets`.
The [initial working tree](initial-status.txt) is part of the tested state.
Unrelated station/atmosphere/art edits are preserved. No branches, pushes,
diagnostic report inbox queries or diagnostic-report JSON reads.

Original scene: clean `Assets/DuckRails/Lighting/StartingStation/StartingStation-bake.unity`.
Final status supersedes the chronological provisional findings below: see
[run closure](run-closure.md), [65-entry ledger](coverage-ledger.md), and
[scenario counts](scenario-rollup.md). The store, revive-follow, furnace, menu,
spider and throttle defects described during investigation have focused fixes and
passing scoped reruns; unaccepted criteria remain explicit.

Original route preferences were saved to `Temp/rails-queue-20261116/settings.json`
and desktop/quick-start values to `prefs.txt` there. Three real accounts configured;
Player4 is absent. Original enemy-boarding evidence was backed up to
`Temp/rails-queue-20261116-boarding-backup` before fixtures overwrote their outputs.

Evidence interpretation:

- Lighting first attempt passed binding assertions but instantiated offline shop
  scripts without their required wallet services, producing setup exceptions.
  Rejected for clean-run acceptance. The corrected isolated fixture disables
  unrelated gameplay behaviours before activation. Its repeat passes with an
  empty error Console. `lighting-lifecycle.txt` covers actual Play Mode loads,
  translations, disable/reload and teardown for ten explicitly named prefabs.
  No appearance, acoustic quality, GPU benefit or multiplayer claim follows.
- Spider rear fixture fails its real melee knock-off. The trace shows support
  retained at the deck edge. Resetting the fixture train to level did not fix it;
  this is an unsuccessful fixture correction, not a passing rerun.
- Compound keyboard boarding is actual injected simulated movement through
  collision and real public countdown/departure. The observer counts audio voice
  leases, not perceived sound. Two start/stop cycles, 3 cm motion, stationary
  collider and 21 matching positive-second ticks pass.
- Fountain evidence is runtime rays and hand sound selection only. The probe did
  not locate a FootstepEmitter under NewPlayer; blank foot values are not a pass.
- Farm foliage checks resolve all 3,669 stalk samples in 52 translated live
  volumes, exclude above-canopy points and exercise unregister/register. They do
  not measure natural contact cadence or audible quality.
- Station traversal first attempt uses no jump and a fixed forward hand pose.
  Its west platform line intersects authored salvage, and side-facing narrow-door
  probes need alignment. Keep these attempts separate from aligned retries;
  no geometry is changed to satisfy an unsuitable straight-line probe.

Environment: authenticated editor startup reports the known LCK unsupported DX12
graphics API error and a Package Manager cancellation. Native LIV recording is
not validated by editor gameplay. ADB could not start its localhost daemon under
the restricted sandbox; authorized outside-sandbox retry succeeded and found one
Quest 3. Dedicated profiler preflight passed; this alone is not a device playtest.

All manual-only queue entries, natural headset motion/contact/readability,
microphone/voice/audio quality and real cross-machine jitter remain Needs human.

Spider correction: the original rear loop now PASSes with a dedicated collision-swept hit displacement that does not reattach around convex edges. Both authoritative and local-prediction paths use it. Unsuccessful level-only fixture change removed. Side/obstruction/stalking regression PASS. Wall carry PASS across 32 poses, but immediate fixture shutdown logged an asynchronous lighting-companion load failure one second AFTER assertions/exit; that run is movement evidence, not a clean lighting-load proof. Separate lifecycle fixture remains the lighting evidence.

Solo follow-up: actual physical throttle fixture now passes both hands including pull-back, forward re-push, release, regrip and sideways breakaway (throttle-physical-solo.txt). Raised-hand station retry passes both lower flights but fails both upper flights; upper traversal remains unresolved, not accepted or evidence for a geometry fix. Actual live source brush transforms rotate with fixed collider matrices; all loaded neighboring render visibility transitions preserve collision and companion readiness.

The first solo-controls fixture used one-shot private test commands; rejected `Room update pending` writes were not retried and did not exercise fuel/fast/dawn cases. Its normal-speed4, wheel/menu observations are valid independently. A receipt-aware retry exercises Fuel100, Fuel0, refuel, 20m/s speed, frozen wheel phase/axles, moving menu/resummon, rumble dispatch and full saved fog/view transition through normal private return. The retry inherited enabled fast mode from a late accepted toggle, so its first drive max20 is NOT additional normal4 evidence despite its broad `>3.8` assertion label. First attempt has max4; both files retained. No gameplay mutation was made for pending CAS rejection. Later fixture runs must explicitly establish initial 1x mode.

Two-client continuation: four actual coal deposits pass stopped, moving, after master handoff and at empty fuel; both clients agree exactly-once removal/fuel and resumed driving (coal-main.txt, coal-Player2.txt). Furnace observers independently record four new receipts, four bursts and four popups each. Ground rescue uses actual RequestDown and helper proximity, with one authoritative credit; main observer sees three one-shot local FX_Heal_02 layers. Peer FX observer was attached after that completion, so it is not peer playback evidence.

Store latejoin: two initial low-balance flashlights expired before the moving rejoin. Their purchases were more than nine minutes old; PhotonGrabbable's ordinary droppedLifetime is360seconds. All14 subsequent matrix purchases remained present with matching identity after rejoin. The raw two missing-item assertions are an unfiltered lifetime confound, not an established persistence defect. Matrix purchases, peer agreement, exact wallet charges and actual20gold funding are in store-goods-Running/store-peer/gold evidence. One current-run rescue and actual coalition stats feed the report-only results observer; finish itself is controlled SimulateSuccess.
Original enemy-boarding evidence restored after fresh run was archived in2026-11-16.

Three-client revive: two initial handover attempts were interrupted by main-editor RunCommand compilation during the1.9s assertion window. Traced second attempt resumes after4.34s, with departed rescuer hands51.8m away and replacement hands0.48m away, but stale committed rescuer. Re-armed in a fresh private room, held automatic suite shutdown, and used only external file monitoring: handover,0.1s dropout/grace and overlapping rescues PASS on all3. Four committed completions produce the3-layer local particle effect on every peer, no particle assertion failures. Source tracing is temporary and will be removed; no gameplay revive change.

First moving/victory feedback script reports false duplicate-burst failures because its single wasBurst flag is shared while iterating every player's feedback root; a playing target and quiet unrelated roots toggle it repeatedly. The independently keyed particle observer shows one playback per new target receipt. Corrected fixture selects the target participant's own presentation for audio/follow measurements; fresh repeat pending. Initial moving4m/s, handoff, withdrawal,3s duration and locked victory stats assertions pass independently. Seeded down states and extended90s victory deadline are controlled fixtures, not natural deaths or normal ending timing.

Corrected remaining feedback repeat PASS on all3: actual4m/s rescue, long hand withdrawal reset, moving master handoff with two helpers/three-second rescue, normal32s SimulateSuccess victory ride at4m/s, one target completion sound per sequence, shared effects mixer, frozen result counters and no later replay. Initial all-root burst observations are invalid; target-keyed repeat is accepted. Main particle observer has zero failures; audit found five peer follow-position failures each across moving completions. Ground completions have no follow failures. Product follow ordering requires correction; do not accept moving peer-follow from these initial runs. Editor-update follow-gap maxima are timing samples, not a continuous visual-follow tolerance proof. Late-progress join also PASS: pitch matches committed progress, unload clears roots,32 warmup frames allcovered and4s post-completion rejoin has no old completion.


Revive follow corrected in cd39a40e0: ground/moving/victory/masterhandoff/latejoin reruns pass across3 clients; continuous post-interpolation observer main522/Player2 533/Player3 422 playing frames,0fails. See revive-follow-fix.md. Public two-client occupancy passes: retaineddeadline withoneaboard, lastcancel/full20restart, bothreadyexpedition,2On2Off,3cmvisual0collidermotion. Fountain actualbodycollision plus explicitinjectedhandcallbacks9passes0fail; listeningmanual. Compound sharedstore currentlyinactiveunderdisabledSubwayCompound: no purchase/feed proof from locations0 run.
