# November 16 queue run closure

Unity 6000.3.24f1; Duck Rails only. Started from
`789a5be13f11d220f9b09a520a16fc204d169c36` on main with the pre-existing changes in
[initial status](initial-status.txt). Final gameplay source is `b9bbf51d4`.
Nothing was pushed. No diagnostic report inbox, script or local report JSON was read.

The [65-entry ledger](coverage-ledger.md) and [scenario rollup](scenario-rollup.md)
are the acceptance record: 32 scoped passing groups, one inconclusive stair group
and four blocked prerequisite families. These counts do not close 65 whole entries.
Unexecuted objective cases remain identified alongside, and distinct from, human
headset/listening/jitter requirements. Prepared scripts and failed fixture setups
are preserved but are not counted as passes.

## Focused commits

- `217f5a65d`: spider knock-off collision displacement; rear/side/obstruction and
  carry regressions pass in the documented scopes.
- `730f48edc`: adapt the spider network fixture to the actual melee path.
- `33a080740`: serialize baked-fixture metadata for Android compilation/runtime
  suppression; preserve existing user art and prefab references.
- `cd39a40e0`: follow revive feedback after network avatar interpolation;
  continuous three-client observers pass.
- `889806182`: persist captured finale prefab overrides in the authoring tool.
- `6fbeb4eda`: remove furnace prefab's forced debug fuel level; fresh-load displays
  follow actual shared fuel on both clients.
- `f7c6e0099`: prevent menu disable/teardown from creating fallback audio playback;
  leave and rejoin remain clean.
- `00945ca91`: install the shared store in the active Compound terminal and persist
  the inspected security-camera position; actual purchases and cleanup pass.
- `ac918293b`: correct the bridge-hold fixture's standing-point placement and pose
  update ordering; physical hold passes on two clients.
- `b9bbf51d4`: permit same-run regrip of the local actor's exact released throttle
  token before its echo; default 0.25-second return and contention regressions pass.

Seven are production defects; the spider/bridge fixtures and finale authoring
correction are recorded separately. Report-only results-board behavior was not
changed. The [repel attribution interpretation](repel-attribution-interpretation.md)
explains why a confounded exclusivity assertion is not a demonstrated stats defect.

## Quest artifact and device limitation

The earlier current-source build/install and tracked survey attempt are recorded in
[the native attempt](quest-final-attempt.md). Run
`95ab6f5b377f4e759420b79e12649a6c` had zero valid frames out of 7,024 because positional
and head tracking were false despite app focus and orientation tracking. Its entire
performance capture is excluded. Cancellation, request removal, app stop and device
property/recorder cleanup were verified. A user readiness question remains unanswered;
no subsequent tracking survey is claimed.

The final APK rebuild includes the throttle correction and preserved working art.
See [source HEAD](quest-throttle-build-head.txt),
[working-tree provenance](quest-throttle-build-working-tree.txt),
[build result](quest-throttle-build.txt), [SHA-256](quest-throttle-apk-sha256.txt),
and [installation/idle-device readback](quest-throttle-install.txt).
Build and installation are separate from native performance acceptance.

## Restoration and preservation

See [final editor readback](final-editor-cleanup.txt). The original clean open scene
was `Assets/DuckRails/Lighting/StartingStation/StartingStation-bake.unity`.
Original route order was recovered exactly from the chat: Farm, Canyon, Container,
Skyscraper. The November 16 boolean backups in Temp were lost on editor restart;
logical flags use the last recorded values (desktop/quick/bright off, ordered route
and enemy invincibility on). This is not byte-identical preference restoration.

Only this run's result section is staged from the already-dirty queue file. Existing
art, scenes, atmosphere changes and other documentation remain outside the run's
commits. Five shop prefabs gained only Unity trailing whitespace during installation;
those exact incidental changes are removed after the build. No unrelated file is
stashed, reverted, staged or committed.
