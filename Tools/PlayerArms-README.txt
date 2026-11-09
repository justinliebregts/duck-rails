PLAYER VISUAL ARM STRETCH - 2026-11-04

DuckPlayer, DesktopPlayer and PhotonPlayer use the original duck arm offsets:
approximately 0.287854 m upper arm + 0.263743 m forearm = 0.551598 m at rest.
RemotePlayerBody.PlayerArmMaxStretch is 1.4: maximum shoulder-to-wrist reach
is approximately 0.772237 m. The existing palm calibration is unchanged.

Only the human-player visual arms opt in, through the existing TracksPalms flag.
TwoBoneLimb uses the original elbow/twist solve within reach. Beyond rest reach
the arm straightens and its upper bone scales along the length axis, capped at
1.4. The forearm inherits the length scale. Scale resets when the hand returns.
Each visual hand is reparented once to the upper arm's parent, retaining its
world size; its fingers and existing held-item anchors follow it without shear.
Local and Photon bodies use the same solve. Photon still sends the existing
calibrated hand poses; no stretch value or additional network field is sent.

Player physics, locomotion, controller calibration, frame timing, PlayerHand,
Grabbable and Photon item code are unchanged. Held items still follow the same
hand/hold-point references and existing networking reconstructs their grip.
The changed hand pose is visual-rig output; it does not write tracking anchors,
player rigidbodies, locomotion state, or item collider dimensions.

All player prefab +10% arm offsets and the player ragdoll's length/collider
overrides have been removed. DuckSkin_Ragdoll remains the original base;
DuckPlayer_Ragdoll inherits its original dimensions. Its existing joint
projection tuning is retained. On death, only rotations and finger pose transfer;
corpse scales and lengths stay at their authored defaults, including deaths at
maximum stretch. A stretched living hand can therefore move inward at death.
The companion's compensating arm-length overrides were removed as redundant.

PlayerArmGeometry, its cloned fitted meshes and automatic prefab/scene mesh
preview are removed. Skins use their original shared meshes and bind poses.
The separate elbow-tuning Inspector preview remains at original arm length;
it neither stretches nor reparents linked prefab bones.

Performance: no extra update loop, physics query/sync, renderer, material,
draw pass, mesh copy, mesh topology, bone count, or network message. Rest data
is cached; bone scale is written only when stretch changes. Reach classification
uses a squared-distance check; stretched poses skip the bent-elbow trigonometric solve.
The 1.4x regression measured the whole solve at 4.50-5.27 microseconds per arm,
with 0 recurring managed bytes across 20,000 alternating near/far solves.
These are desktop Editor measurements, not Quest CPU/GPU profiling.

Validation entry point: PlayerArmRegressionChecks.Run (Unity batch mode).
The focused arm-only entry point remains PlayerArmLengthChecks.RunAll.
Covers original prefab lengths, 1.4 cap, six reach directions, wrist rotations,
stretch/return, no hand shear, local/Photon codec parity, four skins, actual held
item grab/transfer/throw/wall interactions, remote held-item reconstruction,
controller and bare-hand input, normal corpses, pooled deaths and finger pose.
PlayerArmGeometryChecks also renders 48 skin/pose/route combinations.
Additional regressions cover near-zero and shoulder targets, minimum fold reach,
collinear vertical poles, stretch/cap boundaries, and extreme overreach. The
shoulder-target regression exposed a detached wrist gap; the player-only solve
now clamps to the minimum fold radius and supplies a valid degenerate bend plane.
NPC/leg solvers and all locomotion and physics code remain unchanged.

PhotonAvatarRecoveryChecks now also validates actual receiver/playback arm
lengths against the displayed interpolated pose at 80/220 ms delay, with a
500 ms packet gap, rotating wrists, overreach and recovery. Existing checks
cover bad/stale/duplicate packets, reconnect clock recovery, timestamp wrap,
authority changes, death/respawn and zero recurring receiver allocations.
The aggregate also runs pickup, respawn, owner-collision and moving-platform
network regressions (80/250/450 ms, jitter, loss, late join and host switch).
The 1.4x aggregate passed in an isolated Unity project with freshly compiled
runtime code and current checks: 76 cm targets, 1.35x/1.4x poses, the new cap,
held items, default-length corpses and delayed/lossy Photon playback. The
production change from 1.3x to 1.4x is only the shared visual stretch constant.
Log: .utmp/stretch-arms-1_4/regression.log.
Earlier 1.3x log: .utmp/stretch-arms-1_3/isolated-regression-2.log.
Earlier 1.2x log: .utmp/stretch-arms-network/regression.log.
Earlier URP renders: .utmp/stretch-arms/renders/.

Live Photon verification at the previous 1.2x cap (2026-11-04): two separate Unity Editor clients joined
a private, invisible Photon Cloud room in the US region. One drove the current
VR prefab, the other the current desktop prefab; both instantiated the current
PhotonPlayer prefab and used production capture, serialization, transport,
receive and interpolation. The isolated projects used a freshly compiled runtime
assembly and current project prefab/model/skin assets. Each client passed 910
checks with zero failures over 25 synchronized phases: all four skins, normal
reach, 1.1x stretch, capped overreach, return to normal, 1.2x stretch, death and
respawn. Both clients measured and rendered the OTHER client's stretched avatar.
Corpses retained default 55.160 cm arm lengths and normal collider dimensions.
Results and screenshots: .utmp/stretch-arms-network/A/Results/ and B/Results/.
Logs: .utmp/stretch-arms-network/live-A-2.log and live-B-2.log.
This minimal network harness uses scripted tracking inputs, kinematic local
bodies and diagnostic materials; it does not exercise headset tracking or URP
shading. Held-item network reconstruction is covered by the separate production
callback regression suite, not these live room phases. Missing XR/LIV native
plugins and an Editor search-index startup exception are harness environment
limitations; the live network/arm checks completed successfully on both clients.
No headset APK or Quest hardware profiling has been performed.
