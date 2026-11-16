# Raider Legends revive feedback — November 12, 2026

## Current particle replacement — November 15, 2026

User selected `Assets/Plugins/PolygonParticleFX/Prefabs/FX_Heal_02.prefab` for completion feedback. Its three layers now replace the contents of `Assets/Resources/RailRevive/Revived.prefab`, preserving the resource path and GUID. The vendor source stays unchanged. The revive copy disables looping and play-on-awake, uses local simulation, and retains the authored scale, timing and materials. Existing completion trigger, sounds and head-minus-0.5 m placement remain unchanged.

Verified Duck Rails editor: save/reload checks pass for three particle systems, root playback component, non-looping/manual playback, local simulation, supported primary materials and unchanged asset GUID. Empty trail-material slots are unused (trails disabled). Live appearance is **queued for playtest** as “FX_Heal_02 revive completion”; historical four-layer evidence below applies to the previous effect.

Implemented; live multiplayer/headset acceptance is queued as **Raider Legends revive feedback** in `playtest-queue.md`.

## Source and imported assets

Read-only source: `X:\Work\arpg`, HEAD `465b0ac322454aebdcc5a3bb22485c7595756c25`, Unity 2022.3.62f2. Destination: Duck Rails, Unity 6000.3.24f1. Source `Assets/Scripts/Photon/PhotonPlayer.cs` uses PUN RPCs and Photon Realtime player records. Rails retains its existing PUN/Realtime room-property snapshot/CAS authority; no source gameplay or networking systems were imported.

- `Assets/Prefabs/Managers/MasterAudio.prefab`: `RevivingSFX/RevivingSFX1` references `Assets/Audio/Announcer/RevivingPlayerSFX.ogg`; `RevivedSFX/RevivedSFX1` references `Assets/Audio/Player/ReviveSFX.ogg`. Both original clips are copied unchanged.
- `Assets/Prefabs/Photon/PhotonPlayer.prefab`: extracted `RevivedPFX` (particle fileID `5239132974448311950`) and its swirl, sparkle and beam children. Copied three PolygonParticleFX materials/textures, `FX_Cylinder_01.fbx`, and `CustomRP/DitherParticle.shadergraph`.
- Destination assets are self-contained under `Assets/Resources/RailRevive/`, with fresh GUIDs and remapped references. Existing Rails asset GUIDs were not replaced. The source skeleton's combined rotation is identity and its combined scale is one; these are flattened into the standalone prefab root.

## Rails adaptation

`RailReviveEffects` replaces the generated local-only tone. Every client presents every participant's committed progress as positional audio, using Raider Legends' 0.25–1.25 pitch ramp. Progress and the label freeze during the existing interruption grace; audio stops while contact is lost, and restarts at the retained progress if contact resumes. Reset after grace and the three-second revive requirement remain unchanged. Multiple simultaneous rescues have independent sources.

Successful authority commits increment `RailParticipant.reviveSequence` and record `revivedAt`, together with clearing downed state. This receipt is independent of gameplay statistics, so victory-ride revivals also produce feedback without modifying frozen results. Each observing client plays the completion sound and all four particle layers once. New clients baseline existing receipts, avoiding historical bursts; a missing avatar can catch up within three seconds. Compound return, disconnect and disabling clear presentation; a new run resets receipt tracking. The editor revive action also creates the receipt.

Effects follow the revived duck at head height minus 0.5 m. Particle simulation is local so the swirl follows aboard the moving train. Audio uses the existing Effects mixer/game-volume control, 3D positioning, no Doppler, 10 m near distance, and linear falloff to 40 m for progress / 30 m for completion. These falloff curves replace MasterAudio's custom curves; loudness and placement still need headset review. No MasterAudio dependency was imported.

## Validation

November 13 older-queue regression: three real authenticated MPPM clients pass moving-train feedback, longer interruption reset, two helpers without faster revival, master handoff without duplicate completion, victory-ride revival without locked-stat changes, joining during progress at the current pitch, Compound cleanup and joining after completion without replay. Game-effects mixer changes at 100/50/0 percent also pass and were restored. Down/ending setup was controlled; actual replicated hands, authority receipts and real joins drove the checks. See the [fresh scenario ledger and raw evidence](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md). Four independent clients remain blocked by the missing Player4 account; headset audio/particles/performance remain pending. This supplements the historical evidence below without marking the feature fully accepted.

Verified Duck Rails MCP target in Edit Mode. Unity compiled the runtime changes; Console error/exception query was empty. Thirty-one EditMode checks passed: progress start/midpoint/clamping, grace freeze/reset, completion receipt deduplication/stale/future/down-again rejection, ending-state JSON roundtrip without stat changes, both audio clips, four emitting particle layers, imported mesh/material/shader references, flattened transform, and Effects mixer. The shader is supported and reports no errors. Evidence summary: `Temp/rails-revive-effects-checks.txt`.

The checks above are EditMode evidence. November 12 evening: three authenticated MPPM clients passed the existing live revive suite (handover, brief dropout/grace, one rescuer with two overlapping downed players). Each client emitted rising progress audio and committed completion audio/four particle layers; see [live evidence](validation/playtest-queue/2026-11-12-evening/run-notes.txt). This is partial acceptance: independent simultaneous rescues need a fourth account, and moving train, ending ride, late-progress join, master handoff, room return/replay cleanup and perceptual volume/scale/performance remain queued for playtest.
