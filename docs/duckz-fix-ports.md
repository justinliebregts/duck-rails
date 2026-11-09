# Duckz fixes ported to Duck Rails — November 2, 2026

Justin supplied sixteen Duckz revisions for selective porting. Source checkout: `X:/Work/duckz`, read only. Each behavior change has a focused Rails commit with source provenance. Existing local asset/settings/build-linker changes were excluded from these commits.

## Scope and adaptations

- `d795d86` → `bd709b8`: correct head collision displacement and expand inherited cabin/exterior hop allowance. Helicopter interior bounds remain helicopter-specific; the train retains its authored passenger volume.
- `5967004` → `7f698f4`: -2.8 cm controller palm calibration; bind network avatar sources to the calibrated palms while retaining Rails avatar lifecycle safeguards.
- `bd22691` → `cd83a1a`: separate authoritative physics from rendered movement, turn about the head, preserve carry/warp poses, and use head-centered gameplay locations. Rails downing is retained. Rails terminal/control proximity, local expedition position, bomber safety and ending-ride containment were adapted for room-scale offsets.
- `54645cc` → `7b6c8d7`: resting single-hand support on walls/undersides, with bounded surface revalidation and release cleanup. Locomotion now routes contact reports to the active train cabin; the train has no HelicopterManager, so a literal source patch would not fix hanging from it.
- `ea0c960` → `e71afaf`: adapt release sampling and pending-ownership carry to `RailCargo`. Hand/bag release samples subtract actual passenger displacement before throw scaling. Predicted loose drops move locally while ownership is pending, without granting publication authority. Cancellation clears provisional carry and stale checkpoints cannot override the prediction. The train retains its existing bounded cargo volume and run identity; helicopter roof-support grace and visit scheduling are not imported.
- `7c19ad6` → `4316246`: enable player knockback on the inherited Gorilla weapon. This does not add that weapon to Rails shops.
- `dadd6d9` → `7398437`: clear stale hand/head contacts on warp. Duckz Main's bus spawn-height adjustment and bus-specific fixtures are omitted because Rails does not use that scene.
- `0bb981c` → `413d817`: inherited Ninja/Rambro knife throw multipliers become 2.5. No shop inventory expansion.
- `56b353c` → `b6d4ef6`: worn equipment can produce flesh-impact audio.
- `627be74` → `b0f4a55`: LIV Creator Kit 1.4.7 → 1.4.5 with the source customizations and native library replacements. Duck Rails was closed to release loaded DLLs and reopened afterward. Recording/streaming and Android build acceptance remain separate.
- `d46b8df` → `7068516`: confirmed versus issued recovery revisions, preservation of live interactions, bounded retries, ownership reconciliation and pickup timeout recovery. Helicopter visit-departure expiry is omitted: Rails uses run-scoped train cargo, not helicopter visits.
- `4e528bf` → `045277e`: zombie sound pitch variation from 0.95 to 1.05.
- `0e8c048` → `3d49d51`: inherited zombie kill-trigger dimensions. Rails enemy variants inherit these colliders without size/center overrides; their pre-existing local edits remain separate.
- `2dfce74` → `234fb63`: smooth collision-aware development joystick movement. Shared keyboard simulation continues through the normal locomotion solver; duplicate legacy keyboard movement is suppressed.
- `8718c8d` → `a2e3084`: bare-hand palm orientation while retaining controller calibration.
- `d785f6b` → `0bd952c`: tracked finger curls, fist grip and index trigger. Preserve simulated input, source-switch neutral gating and Rails headset-focus policy.

## Validation

Unity MCP verified `X:/Work/duck-rails/Assets` before operating the editor and after restart. The shell sandbox restricts network access; the diagnostic inbox query succeeded with authorized access and returned six open reports. Existing local reports were inspected. None of those reports was resolved by this port without matching acceptance evidence.

- Editor compilation passed after the package replacement.
- All thirteen existing Rails quick-regression suites passed. Evidence: [quick.txt](validation/duckz-ports/quick.txt). Fixture creation emitted duplicate-OVRCameraRig warnings.
- Palm calibration passed 1,956 assertions; tracked-hand input passed 363 assertions. The adapted cargo fixture passed sampled release, pending carry, publication-authority preservation, stale-checkpoint rejection, cancellation and off-train release. Evidence: [editor-ports.txt](validation/duckz-ports/editor-ports.txt).
- Imported fixtures run in a disposable project copy under ignored `tmp/duckz-port-validation`; their batch guards are retained. Tests that enter Play Mode or exit the editor require their own process, rather than being counted as synchronous checks.
- All fifteen isolated batch suite entries passed with process exit code 0. Evidence: [batch-final.txt](validation/duckz-ports/batch-final.txt) and [assertion counts](validation/duckz-ports/batch-counts.txt). Fixtures were adapted to Rails scene readiness, explicit local Photon ownership, calibrated palm sources, and nested execution without terminating the batch early.
- The three authenticated live clients all passed the expedition suite on November 2 at 17:54 UTC: route/curve travel, shared bag, coal carrier, authority handoff, rejoin, success, replay, crash and private cleanup. Evidence: [main](validation/duckz-ports/live-main.txt), [player 2](validation/duckz-ports/live-player2.txt), [player 3](validation/duckz-ports/live-player3.txt). The fixture now retries departure on the current master during staging and checks the head-centered passenger location. Earlier runs exposed stale fixture assumptions, recorded separately.
- The separate headless `DevelopmentMovementPlayChecks` process crashed in native OpenXR with `XR_ERROR_GRAPHICS_DEVICE_INVALID`, including a retry with automatic XR initialization disabled only in the disposable copy. That Play Mode check remains unverified; [limitation](validation/duckz-ports/headless-xr-limitation.txt). The synchronous development movement and simulated-network suites passed.
- The train prefab and baseline scene explicitly serialize the updated 1.2 m / 1.2 s support-hop allowances; changing only the cabin code defaults would leave the previous 0.9 values active.
- Headset motion/comfort, natural hanging/release, two-device pickup/throw behavior, LIV capture and Android/device profiling remain required; editor or synthetic transport assertions do not close those acceptance items.

## Repeating checks

Run `RailTestRunner.Quick()` in Edit Mode for the Rails suites. `RailDuckzPortChecks.Cargo()` runs the focused cargo fixture. Run the imported isolated fixtures in a disposable copy with the same Assets, Packages and ProjectSettings, using Unity 6000.3.24f1 and `-batchmode -executeMethod RailDuckzPortChecks.RunBatch`. Real multiplayer uses Test Center's three-client expedition suite with three configured authenticated accounts; final PASS from every client is required.

## Individual follow-up ports — November 8, 2026

- Source `5ad1aadf762b686dcc649ec32f90838db0649d53`: applied the exact three-line change moving DeadEffect and NightVisionEffect from 0.22 m to 0.05 m and changing the legacy DesktopPlayer near clip from 0.08 m to 0.04 m. The active shared DuckPlayer rig already has all three cameras at 0.04 m; no adaptation was needed. Unity MCP verified the Duck Rails project in Edit Mode, refreshed assets, confirmed successful script compilation and both imported overlay depths, and checked all three shared-rig camera near planes. Git diff whitespace checks passed. In-game/headset visual review remains unverified. The corresponding Rails commit records the full source SHA in its message.
- Source `06fd834c00db734444bd0a94e8f11d9d6bc83c2d`: applied the exact two-line PlayerHUD change restoring night-vision grain/scanline UV tiling from 960 x 640 to 4224 x 2816 following the closer overlay placement. No adaptation needed. Unity MCP verified Duck Rails in Edit Mode and synchronously imported the prefab; checked the exact UV rectangle, retained 0.05 m overlay depth and NightVisionGrainScanlines128 texture reference. Git diff whitespace checks passed. Headset visual review remains unverified.

## Visual arm stretch port — November 8, 2026

User authorized combined sources `81b6997f52ad671ae494f504ff0a6134f0ed3d81`, `6406c039cb8ad0025549a9db3fb8d4039e774c7c`, and `5aaca8c14cdf9af0ed66da9f7ce0ab3db309e087`, ending at 1.4x visual stretch. Runtime/prefab changes applied directly. Omitted the edit to absent Duckz-only RespawnDeathSequencePlayChecks. Adapted ControllerPalmInteractionChecks to Rails' existing 11-field held-item packet (cargo serializes separately), preserving the complete receive/pose assertions.

Unity compilation and arm-length checks passed in Duck Rails. Disposable copied-project PlayerArmRegressionChecks passed: 2912 held-item interaction checks, 12809 avatar recovery/playback assertions, 127990 simulated moving-platform network assertions, calibration/tracked input, skin geometry and ragdoll checks. See [batch evidence](validation/arm-stretch/batch.txt), [extended arms](validation/arm-stretch/extended.png), [bent arms](validation/arm-stretch/bent.png). These are synthetic transport/editor checks, not real-peer or headset acceptance. Source suite produced 48 renders; two default-skin renders were visually inspected.

Rails quick suites passed except the previously documented canyon-v1 reusable coal marker assertion in RailWeek3Checks. Live solo shared-rig fixture failed floor support at its original station position. Relocating the temporary floor away from scenery passed support/rig setup but failed the next WASD movement assertion; cause is unresolved. Do not count this as a live pass or attribute it to the port without further evidence. Sandbox was stopped and fixture cleaned up. Justin completed the requested manual headset review and reported "tested. looks good. no issues." on November 8. This records user-confirmed visual/interaction acceptance; the automated keyboard-fixture failure remains unresolved and real-peer testing at the final 1.4x cap is still unverified. The combined port is accepted for commit with those limits recorded.

## Smasher rooftop exploration — November 8, 2026

Ported `0e3f02f79eb167920f82e099a9e7d72de085de3d`. User explicitly approved the increased reach in Skyscraper Works: 12 m maximum jump height, 16 m horizontal distance, 12 m planned drop and 1.3 s duration. Imports intermediate arc tiers, elevation-balanced wandering, reversible exploration routes and navigation-budget continuation. No runtime graph baking or new boarding opt-in.

Preserved Rails' ground-mover train avoidance, boarding graph selection, passenger targeting restrictions and boundary enforcement. Added early rejection of out-of-home-block arc positions and supported landings, train occupied-volume crossings and train-collider landing surfaces. The broad occupied volume remains conservative near the train. GroundLocomotion's source BOM-only hunk was omitted; the other hunks apply over the Rails adaptations. New source wander fixtures use Rails' worldGraph field instead of Duckz's removed auto-property backing field.

Validation: Unity compilation passed. The complete disposable-batch SmasherWanderExplorationChecks suite passed, including tier continuation/revalidation, reversible routes, 30-actor scheduling, allocation tests, synthetic Photon landing/handoff, actual Quackville police roofs, stairs and recovery. [Source suite evidence](validation/smasher-port/source-suite.txt).

RailSmasherPortChecks passed explicit block/arc rejection, stationary and translated/rotated train exclusion, inherited RailSmasher tuning and no boarding component. Real Skyscraper Works colliders supported 24 successful synchronous physics traversal cases: ground approaches onto 4 m/8 m floors across all four towers and two open crossings between 16 m top floors. The selected upward jumps from covered lower storeys were rejected; the test does not establish every floor-to-floor route. Existing boarding and track-crossing regressions also passed. [Rails evidence](validation/smasher-port/rails-suite.txt). Repeat RailSmasherPortChecks.Run only in a disposable batch copy; it opens scenes and exits Unity.

These are editor/synchronous physics and synthetic network checks, not real-client or headset acceptance. Natural rooftop exploration, late joining/authority handoff with actual peers, moving-train encounters and Quest frame-time/balance remain playtest work. Current spider-only spawn override is unchanged. No diagnostic report inbox was queried.

## Enemy slope and penetration recovery — November 8, 2026

Ported `9a0bab3ecc9461f4015171ebf32fe5b6958b6f20`. Runtime hunks apply unchanged over Rails: shallow support repair now requires a real capsule overlap, preventing false lift at convex slope seams. Cached ground/spider query colliders are triggers, enabled only inside synchronous penetration recovery and disabled in finally blocks. Existing train avoidance, boarding and smasher rooftop behavior are retained. The GroundRecoveryChecks fixture uses Rails' worldGraph field rather than Duckz's auto-property backing field.

Unity compilation passed. A disposable batch run passed EnemyPenetrationChecks (216 slope/scale/depth/seam cases, 1112 assertions; zero warmed managed allocation for all four movers), GroundRecoveryChecks (including 32 convex-slope traversals), boarding/track-crossing checks, and the complete smasher wandering/tall-building suite with synthetic Photon landing/handoff. [Batch evidence](validation/penetration-port/batch.txt).

Imported recovery Play Mode checks passed 48 embedded production-prefab cases and four fast-fall cases, checking frame-boundary helper retirement and destruction. [Recovery Play evidence](validation/penetration-port/recovery-play.txt). Actual motel/train spider side exit/reboarding and blocked-opening checks passed; wall contacts and planted feet remained stable through 32 transformed train poses. [Spider side evidence](validation/penetration-port/spider-side-play.txt), [surface carry evidence](validation/penetration-port/spider-surface-play.txt).

The imported exhaustive Quackville riverbank Play Mode matrix and direct-cloud recovery harness were not run for this port. The network assertions run here use synthetic transport; actual-peer recovery, natural authored-slope encounters and headset performance remain unverified. No diagnostic report inbox was queried. Repeat EnemyPenetrationChecks.Run and GroundRecoveryChecks.Run in a disposable batch project, and EnemyPenetrationChecks.RunPlay in a separate disposable process without -quit.

Actual minion side exits/reboarding, blocked-nearest fallback, all-blocked hold and retry after clearance also passed. [Minion Play evidence](validation/penetration-port/minion-side-play.txt). The isolated tests restored the editor to Edit Mode.
