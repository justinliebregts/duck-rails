# Duckz port history

Shared handoff for Duckz-to-Duck Rails ports. Follow the porting rules in `../AGENTS.md`.
Source: `X:\Work\duckz` (read-only). Destination: `X:\Work\duck-rails` or its active worktree.

For each port, record the full source SHA, behavior, status, destination files/adaptations,
dependencies, validation evidence, remaining work, and destination commit when available.
Use explicit statuses such as pending, in progress, implemented (validation pending),
validated, or already present (verified). A source diff inspection alone is not port validation.
This history starts November 8, 2026; it is not an audit of earlier imports.

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
