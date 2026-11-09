# Duckz port history

Shared handoff for Duckz-to-Duck Rails ports. Follow the porting rules in `../AGENTS.md`.
Source: `X:\Work\duckz` (read-only). Destination: `X:\Work\duck-rails` or its active worktree.

For each port, record the full source SHA, behavior, status, destination files/adaptations,
dependencies, validation evidence, remaining work, and destination commit when available.
Use explicit statuses such as pending, in progress, implemented (validation pending),
validated, or already present (verified). A source diff inspection alone is not port validation.
This history starts November 8, 2026; it is not an audit of earlier imports.

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
