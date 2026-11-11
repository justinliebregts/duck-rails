# Duckz port history

Shared handoff for Duckz-to-Duck Rails ports. Follow the porting rules in `../AGENTS.md`.
Source: `X:\Work\duckz` (read-only). Destination: `X:\Work\duck-rails` or its active worktree.

For each port, record the full source SHA, behavior, status, destination files/adaptations,
dependencies, validation evidence, remaining work, and destination commit when available.
Use explicit statuses such as pending, in progress, implemented (validation pending),
validated, or already present (verified). A source diff inspection alone is not port validation.
This history starts November 8, 2026; it is not an audit of earlier imports.

## 31aa84116b80c9fb9682493c4c88f51de505b97f — Enemy mono audio

- Status: implemented; Unity import validation passed November 8, 2026.
- Source title: `[update] optimize audio, add ZombieSpider_Scar.ogg`.
- Behavior/destination: set `forceToMono` to 1 on eight existing clips under
  `Assets/Audio/Enemies/ZombieSmasher` and six under `Assets/Audio/Enemies/ZombieSpider`;
  add `ZombieSpider_Scar.ogg` and its source `.meta` in the latter directory.
  The source commit adds no playback binding for Scar; none was introduced here.
- Dependencies/adaptations: both projects use Unity 6000.3.24f1; all existing audio
  paths were present. No code or prefab changes required. Existing GUIDs, Android
  overrides and other importer settings retained. Enemy managers use PUN over the
  retained Realtime stack; networking and Rails boarding behavior are unaffected.
- Validation: exact source/parent diff inspected; all 16 destination Git blobs
  match the requested source commit. New GUID `35120b179e43ef04dabe675d0f93f37f`
  occurs only in its intended meta; source reference scan found no playback binding.
  Verified MCP target `X:/Work/duck-rails/Assets`, synchronously imported each clip,
  and confirmed all 15 AudioImporters force mono, all AudioClips have one channel
  and positive sample counts, and the Scar GUID resolves to the intended asset.
  Initial pre-edit validation failed after the local Python launcher was unavailable;
  PowerShell extraction and the final Unity rerun succeeded. Focused diff check passed.
- Remaining: target-headset listening/spatialization and device performance review;
  real-client gameplay acceptance remains open. No diagnostic reports inspected;
  source Duckz tree and unrelated destination edits untouched.
- Destination commit: the commit introducing this entry (see Git history).

## 43dc293e65b37f9d12498dc209ccd9e9d4588cbe — Retreat timing and stationary exits

- Status: implemented; Unity editor validation passed November 8, 2026.
  Real-client/headset acceptance remains pending.
- Source title: `Set zombie retreats to 3-9 seconds and sync retreat exits`.
- Behavior: supersedes the earlier 1–8 second retreat tuning with 3–9 seconds.
  Preserve PUN's last distinct pose packet while suppressing idle updates so a
  stationary enemy's retreat exit is transmitted.
- Destination: `Assets/Scripts/Enemies/{Enemy,EnemyNetSync}.cs`, four base prefabs
  `Assets/Prefabs/Enemies/Zombie{Bomber,Minion,Smasher,Spider}.prefab`, and
  `Assets/Editor/Checks/Enemies/EnemyRetreatChecks.cs`.
- Dependencies/adaptations: both projects use Unity 6000.3.24f1 and PUN over Realtime.
  Existing retreat logic and packet equality support the change. Applied the exact
  timing/check changes and adapted only the packet-swap hunk, retaining Rails' 22-byte
  packet, separate spider lift byte, train/displaced flags, heartbeat and boarding.
  No new assets, GUID changes or source-tree edits; unrelated local edits preserved.
- Validation: exact source/parent diff reviewed; focused whitespace check passed.
  Verified MCP target `X:/Work/duck-rails/Assets`. After recompilation,
  `EnemyRetreatChecks.Run()` passed prefab settings, 100 duration samples,
  kill/search/reset behavior and the actual PUN stationary-update suppression/exit
  regression. All four Rails resource variants resolve to 3–9 seconds; asset-wide
  prefab/scene scan found no timing overrides. `RailEnemyBoardingChecks.Run()` passed
  graph/packet/authoring regressions. Final Console error/exception/assert query empty.
  An initial call used stale compiled checks and failed the prefab-range assertion;
  the recompiled rerun passed. Existing boarding evidence file preserved.
- Remaining: natural retreat timing and stationary exits across actual peers,
  including moving-train encounters, late join/master handoff and target headsets.
  Editor serialization tests do not establish network/headset acceptance.
  No diagnostic reports inspected.
- Destination commit: the commit introducing this entry (see Git history).

## a2d4b6a64d83f6e77362488b337f400360e9ef0b — Backpack grip rotation

- Status: implemented; focused Unity editor validation passed November 8, 2026.
  Full batch and real-client/headset acceptance remain pending.
- Source title: `Fix backpack grip rotation after input mode changes`.
- Behavior: initialize the skin-attached hold point from the calibrated palm rotation,
  preventing mirrored 90-degree offsets after starting with hand tracking and switching
  to controllers. Position offsets use the same frame; attachment timing is unchanged.
- Destination: `Assets/Scripts/Player/PlayerHand.cs` (exact runtime hunk),
  `Assets/Editor/Checks/Rendering/BagGripOrientationChecks.cs` and its source `.meta`.
  Added `RunAttachmentChecks` for safe preview-scene validation without synthetic PUN
  identity replacement. Original full suite retains its disposable-batch guard.
- Dependencies/adaptations: both projects use Unity 6000.3.24f1; existing calibrated
  palms, real DuckPlayer and bag prefabs, and PUN over Realtime are present. Rails
  cargo recovery, drive controls and network schema remain unchanged. Removed one
  stale `HelicopterCargo` initialization from `TrackedHandInputChecks.Holds.cs`:
  that earlier-port fixture referenced a missing Duckz type and nonexistent Rails
  bag field, preventing editor compilation. No runtime dependency was added.
- Assets: existing GUIDs and serialized settings retained; the imported test GUID
  occurs only in its own meta. Authored DuckPlayer hold offsets were inspected;
  this code-only correction needs no prefab/scene override changes.
- Validation: exact source/parent diff inspected; runtime hunk applied without
  replacing Rails code. Verified MCP target `X:/Work/duck-rails/Assets`, compiled,
  then passed 830 attachment assertions (four startup modes, three rotations,
  both hands, mode changes, loss retention, deferred attachment and zero-allocation
  initialized ticks), `ControllerPalmCalibrationChecks.Run`, `RailRigChecks.Run`
  and `RailDriveChecks.Run`. Preview fixture initially needed prefab unpacking;
  final rerun succeeded without reported errors. Focused diff whitespace check passed.
- Remaining: full `RunBatch`/`RunRegressions` in a disposable editor, all four bags,
  real peers/recovery and worn-headset mode switching, including moving-train use.
  Source commit's assertion/build results are not Rails validation. No diagnostic
  reports inspected; source Duckz tree untouched.
- Destination commit: the commit introducing this entry (see Git history).

## 60ae0da45436b626c0cec19b13da7dfe622c4cb5 — Worn goggles grab point

- Status: implemented (runtime validation pending), November 8, 2026.
- Source title: `Move worn goggles grab point closer to face`.
- Behavior: move GrabPoint local Z from 0.22 m to 0.08 m; preserve X=0,
  Y=-0.03 and the separate EquipPoint position.
- Destination: `Assets/Prefabs/Items/ItemNightVisionGoggles.prefab`, transform
  `9100300300300300322`. Applied the exact source change without replacing the prefab.
- Dependencies/adaptations: existing equipment grab anchor is already wired. Both
  projects use Unity 6000.3.24f1. `Assets/Resources/Rails/RailGoggles.prefab` and
  `Assets/DuckRails/Economy/RailShopItemNightVisionGoggles.prefab` inherit this anchor
  without overrides; their Rails cargo/PUN configuration is preserved. No new assets
  or GUID changes, and no source-project edits.
- Validation: inspected the exact commit/parent diff, serialized anchor references and
  prefab/scene references; destination prefab diff matches the single source change;
  targeted `git diff --check` passed.
- Remaining: Unity testing explicitly skipped at user request; natural worn-goggle
  grabbing on headsets and real-client acceptance remain unverified.
- Destination commit: the commit introducing this entry (see Git history).

## a1be1207774efb66bbe6e7df0c0003c2793a926f — Zombie retreat duration

- Status: implemented; editor validation passed November 8, 2026. Real-client/headset
  gameplay acceptance remains pending.
- Source title: `Set zombie retreat duration to 1-8 seconds`.
- Intended behavior: change the retreat duration range from 4–10 seconds to 1–8 seconds.
- Source changes: defaults in `Assets/Scripts/Enemies/Enemy.cs`, serialized values in
  `Assets/Prefabs/Enemies/ZombieBomber.prefab`, `ZombieMinion.prefab`,
  `ZombieSmasher.prefab`, and `ZombieSpider.prefab`, and expectations in
  `Assets/Editor/Checks/Enemies/EnemyRetreatChecks.cs`.
- Dependencies: Rails already has `BeginRetreat` random duration selection and kill/search
  retreat behavior. Both projects use Unity 6000.3.24f1; both enemy managers use PUN
  over the retained Realtime stack. No new subsystem or assets required.
- Destination files/adaptations: the same six source paths listed above; only the two
  timing values and existing check expectations changed. Rails resources `RailZombie`,
  `RailBomber`, `RailSmasher`, and `RailSpider` inherit the base prefab settings without
  timing overrides. GUIDs, boarding/navigation logic and unrelated local edits retained.
- Evidence: exact source/parent diff inspected with `git show`. Verified MCP target
  `X:/Work/duck-rails/Assets`, refreshed scripts and successfully executed
  `EnemyRetreatChecks.Run()` (four base prefabs, 100 duration samples, kill/search/reset
  behavior). Unity SerializedObject checks confirmed all four Rails resource variants
  resolve to 1–8 seconds. Asset-wide prefab/scene text scan found no timing overrides.
  Focused `git diff --check` passed.
- Remaining work: natural retreat/balance review across real clients and target headsets,
  including train encounters; editor checks do not close those acceptance requirements.
- Destination commit: the commit adding this retreat entry (see Git history).

## 00ee3e71dd2c70d33f69059bc3caee32eef6bbbd — Holds survive hand tracking loss

- Status: implemented; Unity compilation and core editor regressions passed November 8,
  2026. Expanded batch, real-client and headset acceptance remain pending.
- Source title: `Keep held objects attached through hand tracking loss`.
- Behavior: existing item/weapon/bag holds survive missing or low-confidence bare-hand
  samples indefinitely. Closed recovery retains the hold; a valid open grip releases.
  UI/new-grab/trigger edges still time out normally. Focus/availability cleanup and
  controller-up/desktop switches still release, with existing throw suppression.
- Dependencies: existing tracked-hand curl input, PlayerHand, BagInventory and item
  recovery are present. Both projects use Unity 6000.3.24f1 and PUN over Realtime;
  source tests also use direct Realtime clients. No networking format/ownership changes.
- Destination: `Assets/Scripts/Player/{TrackedHandInput,ControllerInput.HandTracking,
  ControllerInput,PlayerHand}.cs`, `Assets/Scripts/Items/Bag/BagInventory.cs`, and
  `Assets/Editor/Checks/Rendering/TrackedHandInputChecks{,.Holds,.Cloud}.cs`.
- Adaptations: retained release handling runs after Rails' drive-control branch, so
  the physical train lever keeps its original grip-down/release behavior. Rails'
  simulated-rig update clears transient controller-switch flags; its existing mode
  switch still drops local hands. World-ready/downed gates, moving cargo and boarding
  remain intact. Source README behavior/evidence is summarized here instead of copying
  Duckz validation claims into Rails. Source project was not modified.
- Assets: no runtime serialized fields, prefab/scene overrides or asset references
  changed. The two new editor partials include source `.meta` GUIDs; asset-wide scans
  found each GUID only on its intended file. Existing GUIDs are retained.
- Validation: exact source commit/parent diff inspected; adapted diff reviewed and
  focused `git diff --check` passed. Verified Unity MCP target `X:/Work/duck-rails/Assets`,
  refreshed and compiled scripts, then ran `TrackedHandInputChecks.Run()` (363 passing
  assertions), `RailDriveChecks.Run()` (17 checks) and `RailRigChecks.Run()` (passed).
  Core checks exercise actual DuckPlayer hands, item retention/open recovery, trigger
  suppression, controller switching, production packet roundtrips and zero-allocation
  gesture sampling. Console error/exception/assert query returned no entries.
- Remaining: `RunHeldObjects` and `RunCloudHolds` compile but were not executed: they
  require disposable batch mode, replace PUN identity and open an empty scene. Do not
  bypass their guard in the working editor. The cloud fixture's synthetic identities
  also need review against Rails authentication before use. Validate loaded bags/all
  weapons, actual peers with impaired transport and owner/master departure, then worn
  headset occlusion, recovery timing and Quest performance. Source test results are
  not Rails acceptance evidence. No diagnostic reports were inspected.
- Destination commit: the commit introducing this entry (see Git history).

## 2fd048b293dd2c3e1d9e2bfc2e9e8d2d6701de06 — Late-join pickup ownership

- Status: implemented; editor and disposable batch validation passed November 8, 2026.
- Source title: `Preserve held pickups across late joins and recover repeated ownership losses`.
- Behavior: spawn-matched, current-master join ownership initializes only untouched
  views; live transfers, confirmed grabs and explicit room returns take precedence.
  Repeated losses on different pickups request controller-owned live checkpoints.
  Observer repair requires matching master confirmation and a newer tagged checkpoint;
  intervening ownership invalidates it. Bags require settled, known contents. Two
  reporters recurring on different objects after repair can trigger master handoff,
  with four reports/second, four-second repair grace and a 60-second handoff cooldown.
- Dependencies: both projects use Unity 6000.3.24f1 and PUN over Realtime. Rails already
  has spawn tokens, room checkpoints, bag claims and availability heartbeats.
- Destination: `Assets/Photon/PhotonUnityNetworking/Code/{PhotonHandler,
  PhotonNetworkPart,PhotonView}.cs`; `Assets/Scripts/Items/{PhotonGrabbable,
  PhotonGrabbable.Recovery}.cs`, `Items/Bag/{BagObject,BagObject.Recovery}.cs`;
  `Assets/Scripts/Photon/{PhotonHeldItems,PhotonMasterHandoff,
  PhotonMasterHandoff.Pickups,RoomObjectRecovery,PhotonSession}.cs`; networking
  `PickupRecoveryChecks{,.Join}.cs` and `PickupJoinNetworkChecks.cs`.
- Adaptations: pickup events use 111–113 (Duckz 107–109 collide with Rails train,
  expedition and reward events). Append `-rails-pickup-v2` to the configured app
  version; all players need matching updated builds. Crew-return and Voice use the
  connected PUN version. Preserve Rails focus/shared-rig handling, RailCargo frames,
  bag reattachment, authentication, session lifecycle and enemy boarding.
  Fixtures use Rails RequiredScene; the live fixture's synthetic identities still
  need authenticated account wiring before use with the configured Photon app.
- Assets: no prefab/scene changes needed; handoff is added at runtime and the new
  option defaults true. Three new script metas retain source GUIDs, each unique
  across destination Assets. Existing GUIDs and unrelated edits preserved.
- Validation: exact source/parent diff inspected; verified MCP target
  `X:/Work/duck-rails/Assets`; Unity compilation and `RailSessionChecks.Run`,
  `RailDriveChecks.Run`, `RailDuckzPortChecks.Cargo` passed. Focused diff whitespace
  check passed. Disposable `PickupRecoveryChecks.Run` passed 298 assertions (including
  150 regrab assertions), exit 0. Fixture owner/controller now identify the local
  actor as Rails requires; an initial fixture-readiness failure was corrected.
  See [validation evidence](validation/pickup-join-port/checks.txt).
- Remaining: authenticated three-client late join, repeated-loss repair/handoff,
  loaded-bag request during master departure/rejoin, moving train cargo and worn
  headset checks. Source test results are not Rails acceptance evidence. No diagnostic
  reports inspected; source Duckz files untouched.
- Destination commit: the commit introducing this entry (see Git history).

## 0f9a8a96d278d4e0a488d8f9306423e87bde94e7 — Holder checks before stale-heartbeat drops

- Status: implemented; editor compile and disposable batch validation passed November 10,
  2026. Real-client/headset acceptance pending.
- Source title: `Preserve active players' pickups when heartbeats go stale`.
- Behavior: heartbeat silence alone no longer makes the master drop a player's held
  items or bags. Heartbeats now carry input availability. Validated avatar poses and
  owner-sent held snapshots count as holder activity. Otherwise the master sends the
  holder a direct reliable query and releases only after two unanswered checks (3 s
  apart), bound to the observed view, spawn token, holder, hold version and ownership
  generation, so a late reply or an expired check cannot revoke a newer grip. Explicit
  standby heartbeats and departures still release immediately. A not-ready but focused
  player (for example, still loading) keeps its holds.
- Destination: new `Assets/Scripts/Photon/PhotonMasterHandoff.Holders.cs`; hooks in
  `Photon/{PhotonMasterHandoff,PhotonHeldItems,PhotonPlayer,PhotonSession}.cs`,
  `Items/PhotonGrabbable.cs`, `Items/Bag/BagObject.cs`; checks
  `Editor/Checks/Items/HeldItemRecoveryChecks.cs`, new
  `Editor/Checks/Networking/PickupJoinNetworkChecks.Holders.cs` (`-holderCheck`).
- Dependencies/adaptations: both projects use Unity 6000.3.24f1 and PUN over Realtime;
  builds on the `2fd048b` pickup port. Holder events use 116/117 (Duckz 110/111 collide
  with Rails drive-grip and pickup-report events). The app-version partition is now
  `-rails-pickup-v3` because old clients reject three-field heartbeats; all players need
  matching builds. Rails clock-skew handling is kept: a peer with live heartbeat
  cadence is never queried (`UnavailableReason`), and the master-recovery diagnostics
  remain. A skewed holder's standby beat is discarded by the timestamp filter, so it
  is released by the unanswered checks rather than at once
  ([pickup-clock-skew.md](pickup-clock-skew.md) updated). The Rails `ClockSkew` check
  now asserts that and sends three-field heartbeats. Source HelicopterCargo and hook
  protections are absent from the Rails hunks. New script metas keep source GUIDs,
  each unique in destination Assets.
- Validation: exact source/parent diff inspected. Verified MCP target
  `X:/Work/duck-rails/Assets`; the editor recompiled with an empty error Console. In
  the synced disposable copy, `PickupRecoveryChecks` (alone), `HeldItemRecoveryChecks`
  (twice), `RoomObjectRecoveryChecks` and `PhotonGrabbableChecks` passed. Repeating
  `PickupRecoveryChecks` later in the same process fails its "round-robin retry budget"
  assertion; the pre-port HEAD code fails identically in the same order, so this is an
  existing fixture order dependency, not a regression. Diff whitespace check passed.
  See [batch evidence](validation/holder-check-port/batch.txt).
- Remaining: three-client `-holderCheck` live trial (its synthetic identities still
  need the authenticated wiring noted for `2fd048b`). Also: moving-train holds while
  heartbeats drop, Quest standby/wake and master handoff on headsets. No diagnostic
  reports inspected; source Duckz untouched.
- Destination commit: the commit introducing this entry (see Git history).

## ZombieHook group — 73b78c46, 7ed6d880, 2719d1a1, b591543e

- Status: implemented; Unity editor checks passed November 10, 2026. Real-client/headset
  acceptance (including the hook-defense handshake across peers and master handoff) is pending.
- Source SHAs: `73b78c46fe9af08bc0e13c2fe87f094356ff60d4` (enemy, synchronized interruptible
  chain attack, audio, prefab, RPCs), `7ed6d880612717454c6672119f2ec4581bd9bb75` (footstep clip),
  `2719d1a1406cbef1577851d1dc47c81af975be10` (windup 0.2→0.25 s, flight 0.25→0.3 s on the prefab),
  `b591543ef3b68f4d0dead775280cb125b3d094e8` (warmup catalog refresh).
- Decision (November 10): the hook joins the Rails mix but does **not** board the train; only minions and
  spiders do. Per-crew enemy counts (4/8/12) are unchanged.
- Destination: `Assets/Scripts/Enemies/ZombieHookAttack{,.Contacts,.Defense}.cs`,
  `Assets/Prefabs/Enemies/ZombieHook.prefab` (+ `Resources/Rails/RailHook.prefab`, a bounds + coal-carrier
  variant created by `RailEnemyAuthoring.InstallVariants`), `Assets/Audio/**/ZombieHook*`, chain
  model/material/shader, `Assets/Editor/{Authoring/ZombieHookModelImporter,Checks/Enemies/ZombieHookChecks}.cs`.
  Hooks into `Enemy`, `EnemyManager`, `GroundLocomotion`, `PhotonGrabbable`, `PhotonMasterHandoff`,
  `PhotonPlayer`, `DesktopPlayer`, `NewPlayer*`, `Player` were applied (four hunks merged by hand around
  Rails' boarding/coal-carrier code). Photon RPC list and `GameAudioSettings` updated.
- Adaptations: `RailEnemySpawns.Names` gains `RailHook` at index 4 (spider stays 3, forced-type override
  unchanged). `RailEnemySpawnBalance` now spreads five types: no repeat within a quadrant, totals differ by at
  most one (4 spawns → 4 of 5 types, 8 → 1–2 each, 12 → 2–3 each; the omitted/extra type is seeded). The finale
  selection (`RailSpawnPool.SelectEnemies`, which already cycles `Names.Length`) now includes the hook, so its 5 fixed enemies cover all five types and bridge defenders can be hooks (same seed gives different finale types than before). Hook prefab retreat set to Rails' 3–9 s.
  Source `Main.unity` spawn-list registration not applicable; Rails registers via `RailEnemySpawns.Register`.
- Networking: hook state uses three PUN RPCs (`ReceiveHookState`, `CheckHookDefense`, `ReportHookDefense`); no raised
  events, so codes 101–117 are unaffected. `EnemyNetSync`'s 22-byte packet, spider lift byte and train/displaced flags
  are not modified. No AppVersion change: the version is bumped every build.
- b591543e: **not ported.** Rails has no gameplay warmup catalog/subsystem (no `GameplayWarmupCatalog`,
  `GameplayWarmupBatch`); importing it would be a separate decision.
- Validation: project compiled; `ZombieHookChecks`, `RailEnemyChecks` (now five prefabs, hook has no boarding component),
  `RailEnemySpawnBalanceChecks` (updated type-total rule) and `EnemyRetreatChecks` (now includes Hook) passed. New GUIDs
  checked against all tracked metas: no collisions. `RailEnemyBoardingChecks` could not write its log (Win32 1224)
  and is not claimed.
- Remaining: two-client/headset hook tests, block-by-block clearance for the hook's firing lane, Quest cost, balance review.
  Out of scope: impact sparks/blood, player pull fixes, later tuning commits.

## Hook impact and blood group — 67116b71, 1a71d7fa, 589c7bf0, 161dfe62, 91134b24, d54ee62b, 0d60a7bb, 91613bff

- Status: implemented; editor compiled with an empty error Console and the asset wiring was read back
  through Unity MCP (target `X:/Work/duck-rails/Assets`) on November 10, 2026. The Play Mode / two-client
  checks below have **not** been run. Real-client/headset acceptance pending.
- Source SHAs (full): `67116b7140a0493024f6be69851457edd5a65c6c` (pooled blood sprays with impact sounds),
  `1a71d7fa3d5848768cffd354c58e07ba47945c0b` (prewarmed hook sparks + pooled blood on confirmed hook impacts),
  `589c7bf0dcda6734a5427c260661028ee5b9d5f7` (remote hook visuals on unfocused desktop clients),
  `161dfe62066145514dc1302df9d25f46895ce4cc`, `91134b24e415fd65d79ab9baa96d6b004cc03c0a`,
  `d54ee62b52e617c2947b0ce7babd6c6bcd3b76db`, `0d60a7bb645a5b0e3e5617a6a24093b7953286f5`,
  `91613bffbeeb0b97517f6adf09d828e8f9b1a402` (spark material/prefab/preview tuning; ported as the final state at 91613bff).
- Behavior: weapon/item flesh-impact `SoundEvent`s now carry optional `impactParticles`. `RoomRpc.PlaySound` sends the
  impact direction (third array element only when particles exist) and every peer plays the pooled
  `BloodEffectPool.SpawnHit` burst with the sound, replacing per-enemy/per-player `hitBloodPrefab` instantiation and the
  separate blood RPC (`ReceiveItemHitBlood` stays as an empty stub for old builds; `ReceiveRemoteHitEffect` removed).
  Hook solid hits emit from a shared prewarmed spark system (cap 32, 5 per hit); flesh hits use the hook flesh sound's
  blood. A chain crossing a body now reports flesh rather than solid. Remote hook presentation continues on an unfocused
  desktop client (`CanPresentRemoteActions`), and a local focus change invalidates local hook shots.
- Destination: `Scripts/Audio/SoundEvent.cs`, `Photon/{RoomRpc,PhotonPlayer,PhotonMasterHandoff}.cs`, `Items/{ItemSounds,PhotonGrabbable}.cs`,
  `Enemies/{Enemy,EnemyNetSync,EnemyManager,ZombieHookAttack,ZombieHookAttack.Contacts}.cs`, `Environment/BloodEffectPool.cs`;
  `impactParticles` set on the six weapon/item flesh events plus `ZombieHook_ChainHookFleshImpact`, all pointing at existing
  `BloodSplatPFX` (GUID `b4154b99…` reused, nothing duplicated); `hitBloodPrefab` removed from the four non-hook enemy
  prefabs and `PhotonPlayer.prefab`; new `Prefabs/Effects/HookImpactSparks.prefab`, `Materials/Effects/HookSpark.{mat,png}`
  (source GUIDs kept, none collide in Rails; shader GUID `f0b4e4b6…` exists here); checks
  `Editor/Checks/{Effects/BloodEffectChecks,Items/MeleeGraceChecks,Items/PlayerKnockbackChecks,Performance/RecurringAllocationChecks}.cs`
  updated and `Enemies/ZombieHookImpactChecks.cs` added.
- Adaptations: `ZombieHook.prefab` already held the `sparkPrefab` reference from the earlier hook port (dangling until now), so it
  needed no edit; `RailHook` inherits it as a variant. PhotonMasterHandoff was merged by hand: Rails' `LocalAvailable` already
  ignores focus in keyboard play, so `CanPresentRemoteActions` is `InputAvailable ||` the Duckz expression, which keeps it a
  superset of `LocalAvailable`; clock-skew, startup-barrier and holder-check partials are untouched. `PhotonPlayer.CanReceiveHookReleasedAt`
  now also requires `LocalAvailable`. The Duckz `GameplayWarmupCatalog` entry (1a71d7fa) is **not ported**: Rails has no such catalog
  (same reason as `b591543e`); the spark system is warmed by an explicit emit-and-clear at enemy prewarm instead, and the
  catalog assertion was dropped from the ported check. No new Photon event codes; the sound event (101) just gains an optional field,
  and the AppVersion is auto-bumped, so mixed builds are not an issue.
- Validation: compile clean; every `impactParticles` and the hook `sparkPrefab` (incl. `RailHook`) resolve; the four enemy prefabs no longer
  expose `hitBloodPrefab`. Edit diffs for the checks match the source stat line for line. Not run: `BloodEffectChecks`, `MeleeGraceChecks`,
  `PlayerKnockbackChecks` (live PUN), `RecurringAllocationChecks`, `ZombieHookImpactChecks` (isolated batch Play Mode only).
- Remaining: run those batch checks; two-client hook/melee blood and spark parity, unfocused desktop observer, Quest cost of 32 pooled hit
  bursts; decide whether to import a warmup catalog. No diagnostic reports inspected; source Duckz untouched.
- Destination commit: `1ea23d07f`.

## Enemy tuning group — 22262f75, 98ec9f5b, 6f4c3cba

- Status: implemented; Unity editor checks passed November 10, 2026. Real-client/headset acceptance pending (queued for playtest).
- Source SHAs (full): `22262f75960d59b8aafd625298a0cd6f4a61be36` (speed rebalance, retreat 4–12 s),
  `98ec9f5ba49b75dcc23e120cf3afd3150ed12226` (all zombies fear 40 s flares; late-join flare restore),
  `6f4c3cba9325db190b08d58d84706266bf722828` (smaller kill triggers, slower spider stalk).
- Intentionally not ported from the same range: `cad672e0` (Main.unity max zombies), `63914895` (starting-bus climb block, Quackville
  crawl graph), `b4e09c75` (room-size population scaling; conflicts with the Rails November 8 spawn balance).
- Decision: retreat 4–12 s deliberately supersedes the 3–9 s of `43dc293e`; `game-design.md` updated.
- Destination: `Enemies/{Enemy,GroundLocomotion,ZombieHookAttack,ZombieSmasherJump}.cs`, `Photon/{PhotonPlayer,PhotonRoomFlares(new)}.cs`,
  `Player/Player.cs`, `Utility/DeathFlare.cs`, the five `Prefabs/Enemies/Zombie*.prefab`, `Prefabs/Effects/DeathFlare.prefab`, checks
  `EnemyRetreatChecks`, `SpiderFlareChecks`, `SmasherRetreatJumpChecks`, `DeathLifecycleChecks`, new `Networking/FlareJoinNetworkChecks`.
- Adaptations: Rails prefabs were on older Duckz values, so speeds/stalk multiplier use the source's final absolute values (Bomber 2.5/3.6,
  Minion 2.6/4.4, Smasher 3.8/5.5, Spider 3.3/4.2, stalk 0.4; Hook already matched). Kill-trigger colliders: Bomber/Minion/Smasher take the source's new
  values; the Rails spider trigger (head-following, from `eb52afd5`) was scaled by the source's shrink ratios instead. Hook collider/flare radius were already
  at the final state. Hook retreat set to 4–12 s. Rails `DeathFlare.DeathLaunchDelay` is 0 (source 2), kept. No serialized overrides of these fields on
  Rails variants (`Resources/Rails/*`) or scenes. `EnemyNetSync` packet, boarding rules, crawl graphs and spawn balance untouched; no new RPCs or event codes.
- Late-join flares: death flares are now recorded in Photon room properties (`deathFlare:*` keys, master publishes, expires at lifetime), so
  players joining later, including aboard the train, restore active flares. Flare positions are still world-space at death time; **not yet reconciled with
  train motion** (a flare dropped on a moving train is not train-relative).
- Validation: compiled, empty Console; `EnemyRetreatChecks`, `RailEnemyBoardingChecks`, `SpiderFlareChecks` passed. Not run: `SmasherRetreatJumpChecks`,
  `DeathLifecycleChecks`, `FlareJoinNetworkChecks` (network, needs Play Mode).
- Remaining: two-client late-join flare test (incl. master handoff and joining aboard a moving train), hook/smasher flare behavior, balance feel of new speeds.
- Destination commit: `1ea23d07f`.
