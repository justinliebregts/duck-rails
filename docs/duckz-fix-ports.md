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
