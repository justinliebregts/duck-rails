# Older-queue regression — November 13, 2026

Execution complete: **58 PASS, 3 FAIL, 2 BLOCKED and 18 deliberately excluded scenario groups**. See the [full scenario ledger](scenario-ledger.md) for exact counting units, raw evidence, remaining failures and checks not reached. Unity 6000.3.24f1; starting HEAD `3c64b55a44e9c118c176838fa2e19136bd4b3eec`, final gameplay HEAD `8379de0f15405a0541434dc20037fd929d8161f3`, with existing unrelated changes retained. Main editor and two MPPM peers authenticate as three distinct accounts against the real backend and Photon. Disposable batch copies use the current destination tree. No diagnostic reports were inspected and nothing was pushed.

## Deliberate exclusions

These are scenario groups, not whole feature exclusions. The source of scope is the [November 12 evening notes](../2026-11-12-evening/run-notes.txt) and their named raw evidence. Earlier PASS results outside that run are rerun.

1. Flashlight pickup/use/release/regrab, actual owner 60-second timeout and component `Stash` path (`flashlight-main/player2.txt`). Natural backpack insertion is not included in this exclusion.
2. Flashlight off-audio source playback on owner and nearby peer; peer used a near-expiry seeded timer (`flashlight-off-sound-main.txt` and peer sample).
3. Furnace idle/full/empty/refuel particle emission, 12% empty emission, and fuel ten/five/zero segments (`solo-runtime.txt`).
4. Four committed furnace coal deposits: stopped, moving, after master handoff, and empty-fuel recovery; matching fuel credit, popup and burst on both clients (`rails-coal-main/remote.txt`, `observer-coal-*.txt`).
5. No historical furnace popup/burst on a third late join (`observer-late-player3.txt`).
6. Distance display departure zero, seeded bridge-distance reading and short live travel/peer/late-join readings (`solo-runtime.txt`, network evidence). Full-route travel is not excluded.
7. Direct lease progress at 0.2/0.6/1 and corresponding 0/2/4 m/s speeds (`variable-drive-*.txt`). Physical grip and input binding are not excluded.
8. Direct lease release braking and approximately quarter-second visual return (`variable-drive-*.txt`). Early unacknowledged release and regrip during return are not excluded.
9. Missing-heartbeat drive expiry (`variable-drive-*.txt`). Disconnect, focus, menu, down and host-loss paths are not excluded.
10. Direct drive-lease actor/progress/speed agreement on peers and late join (`drive-peer-Player2/Player3.txt`). Attached remote hand poses are not excluded.
11. All 25 local reverb-zone inside/outside listener-plumbing probes (`environment-runtime.txt`). Independent peer listener positions, unload and perceptual checks are not excluded.
12. Two expedition security-camera feeds with independent 256-pixel targets and near/range-off behavior, plus Compound feed activation (`environment-runtime.txt`, `final-network-main.txt`). Purchases, camera framing and headset quality are not excluded.
13. Current five-block route readiness and cargo/enemy/geometry/collider/reverb inventory agreement, including late join (`snapshot-five-*`, `snapshot-late-*`). Traversal, settling/reach in new areas, pursuit and special-block handoff coverage are not excluded.
14. Three-client revive rescuer handover and brief interruption/grace (`revive-main/player2/player3.txt`). Longer dropout is not excluded.
15. One rescuer with overlapping downed players in that same revive suite. Independent simultaneous rescues are not excluded.
16. Revive progress pitch and receipt-triggered completion audio/four-layer particles observed during that suite (`observer-revive-*.txt`). Moving/victory train, late-progress join and cleanup cases are not excluded.
17. Seeded successful results board with three distinct titles agreeing after master handoff (`results-*.txt`); repels/spent were test values. Natural attribution, failed finish and pre-finish reconnect are not excluded.
18. Targeted stranded non-master disconnect during Ending/rejoin into retained Returning token, using a third client to retain the old room (`stranded-*.txt`). Normal public departure/return and control-connection failure are not excluded.

## Evidence interpretation

- Raw fixture attempts are retained. A fixture exception, invalid placement or audio distance cull is not by itself a product regression.
- `traversal.txt` uses actual simulated room-scale input and collision, warping only to route starts. The old utility-aisle coordinates cross the newer Farm holding cage; they are not a current open-aisle assertion. Repair-container and barn floor thresholds stopped walking. Jump retry evidence is separate and does not yet establish those threshold passes.
- `finish-main/Player2/Player3.txt` is invalid for boundary acceptance: it used the old rig-root interpretation of `Warp`, and train placement was not acknowledged. `finish-corrected-*` waits for acknowledged placement and puts the measured gameplay head at the boundary. The actual crossing classifies side/nose as escaped and far ground as not escaped. The nose follow-up is resolved by `finish-nose-trace.txt`: locomotion disabled to isolate the volume, actual/reported head agree, zero fall-timer frames or rescue jumps over six seconds. This does not establish natural standing comfort.
- `contact-probes.txt` initially dispatched distant sounds and correctly hit AudioManager distance culling. It is not missing-contact evidence. Nearby playback rerun is required.
- `bridge-firsthold.txt` preserves a temporary fixture null lookup after the first hold. The first no-defenders, two-defender spawn and pursuit observations are valid. Its continuation's repeated “No defenders before first” label is inaccurate (the pair already existed); use the earlier baseline and explicit retained IDs.
- `store-fixture-parent-catalog.txt` and `store-fixture-bag-and-both-grips.txt` are rejected setup/assertion attempts. The corrected `store-main-Running.txt` checks stores actually bound to the shared walls, separate bag inventory, and one simulated hand. Actual unintended fixture purchases remain recorded in wallet/spent totals; no balances were fabricated.
- `throttle-left-isolated.txt` is a left-only trial despite its inherited terminal “both hands” label. The combined right-to-left sequence did not pass. Separate right and left acquisition/stroke/release/breakaway coverage must be reported separately.
- The first two flare batch processes waited through a cold disposable-copy import and failed coordination. Warmed retry host and guest both exited 0; both attempts are retained.

Final verdicts and remaining human acceptance are in [scenario-ledger.md](scenario-ledger.md). The full-route audit rejects a TMP-tag parser false failure, accepts all 475 retained display samples, and records the supplied test fuel and pauses. The temporary cold-Login helper was removed after use; its source is retained under `fixtures/`. Final preference/scenario restoration is recorded in [cleanup.txt](cleanup.txt). The crash erased the original temporary byte backup; only logical preference restoration can be claimed.
