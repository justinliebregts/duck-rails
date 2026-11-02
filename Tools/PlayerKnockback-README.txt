Player knockback validation (2026-10-31)

Behavior
- Nail bat and Gorilla weapon use playerLaunchSpeed = 12 m/s.
- Player launch strength scales from zero at a 2 m/s swing to full at 9 m/s.
  The shared receiver adds upward lift and caps the launch at 12 m/s.
- The frying pan remains at playerLaunchSpeed = 0.
- Player hits do not directly damage or kill the victim.

Automated coverage
PlayerKnockbackChecks.Run passed 300 assertions and 32 live PUN hit RPCs:
- Both authored weapon prefabs, master-to-client and client-to-master.
- VR Rigidbody launch and desktop push velocity; held and thrown sweeps.
- 9 m/s swings produced 12 m/s launches; 5.5 m/s swings produced 6 m/s.
- Normal networking and simulated 150 ms incoming/outgoing lag, 40 ms
  jitter, and 5% configured packet loss on both UDP clients.
- Zero, below-minimum, exact-minimum, just-above-minimum, half, full and
  over-full swing speeds in five directions; capped launch speed.
- Unknown item, wrong sender, slow impact, non-finite velocity/position,
  distance limit, tracked-head/root offset, cooldown, dead attacker/victim,
  self-hit and non-owner replica guards.
- Frying pan configuration remains unchanged.

Related regressions passed: GorillaShapeChecks, PhotonGrabbableChecks
(including EquipmentPersistenceChecks), and GrabbablePlayChecks.
Fresh runtime/editor compilation and git diff --check passed.

Running the dedicated check
Use Unity 6000.3.24f1 in a disposable project/check-out with the current
scripts, prefab dependencies and Photon settings. The entry point is:
  -batchmode -nographics -executeMethod PlayerKnockbackChecks.Run
Do not add -quit: the check enters Play Mode and exits with status 0/1.
It creates a hidden two-player Photon room under a unique application
version and requires licensing/network access. Do not run in a working
editor session: the check replaces the scene and network singleton.

Scope and limitations
Two real Photon UDP clients share one Unity process and deterministic
scene fixtures. The harness selects each client context and dispatches
received payloads through PUN ExecuteRpc. Authored weapon samples feed
the production player sweep, RPC sender, receiver and launch methods.
Ownership assignments are controlled by the fixture; this is not a test
of live ownership negotiation or separate game processes.

The harness does not verify physical headset tracking, rendering,
perceived latency, audio or haptics. Blood visuals/audio are suppressed
in the dedicated fixture. A two-headset playtest remains unverified.

The disposable batch environment logged unrelated sign-in/Multiplayer
Play Mode setup exceptions, and the general play test logged a missing
LCK native library. The listed checks completed and all final Unity
runs exited 0. An initial exact-6m test failure was fixture roundoff
(scaled skeletal head yielded squared distance 36.00002); using an
unparented tracked-head source made the boundary deterministic.
No gameplay code correction was required.

Local run artifacts (not committed): .utmp/PlayerKnockback/
  network-validation.log, regression.log, play-regression.log,
  compile.log, editor-compile.log, source-hashes.json
