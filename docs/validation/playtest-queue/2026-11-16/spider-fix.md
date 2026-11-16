# Spider hit displacement correction — November 16, 2026

Queue entry: Train model-aligned collision. Unity 6000.3.24f1, base 789a5be13f11d220f9b09a520a16fc204d169c36 with pre-existing art edits.

The unchanged rear spider loop failed melee knock-off. Its trace shows crawl adhesion reattaching to the convex deck edge, moving the root backward and cancelling the remaining shove as blocked. Resetting the fixture to a level train did not fix it; that attempted fixture edit was removed.

`ClimbLocomotion.DisplaceHit` retains sphere sweeps and wall sliding on the initial contact plane but omits crawl reattachment. Authoritative and locally predicted hit paths use the same operation. Ordinary crawl/boarding paths remain unchanged.

Validation:
- [Original failure](spider-rear-attempt1.txt), [edge trace](spider-trace.txt), [unsuccessful level-only attempt](spider-rear-level-attempt.txt).
- [Original rear loop rerun](spider-rear-fixed.txt): actual boarding, carry through yaw/slope, pursuit, exits, reboard and melee knock-off PASS.
- [Both sides, blocked exits and stalking](spider-sides-fixed.txt) PASS.
- [Wall/normal/planted-foot carry](spider-wall-carry-fixed.txt) PASS 32 poses. Immediate offline fixture teardown then logged a companion-load error; this is movement evidence, not clean lighting acceptance.
- [Minion rear loop](minion-depot-fixed.txt) PASS after shared hit-callsite change.
- [Focused actual-Physics checks](spider-hit-regression.txt) PASS 61/61 in empty offline Play; final Console errors/exception/asserts empty. Flat, 31/50 degree slopes, wall support, solid wall stopping/sliding, 30/90/144 Hz local-authority displacement/budget parity, both real train gaps/solid rails/rear. [Fixture](spider-hit-regression.cs.txt).
- [First focused attempt](spider-hit-regression-terrain.txt) passed 60/61; its borrowed low tilted train had terrain beneath the rear, invalidating the no-support assumption. The repeat uses a separate train over empty space and passed.

These checks do not establish real-client transport, headset melee feel, or overall train-collision acceptance. Those remain in the ongoing queue run.

Fresh actual two-client validation now passes (spider-network-main.txt and spider-network-Player2.txt): natural RailSpider selection, moving carriage-relative pose, authority migration, non-owner melee RPC observed by authority and actual detachment without teleport fallback, late rejoin aboard moving train, left exit/reboard and right exit. Editor fixture adds RunSpider mode matched to the arm token; normal transport-only mode retains its older fallback. This is real Photon desktop acceptance for the fix, not headset or cross-machine jitter evidence. No compile errors; known LCK/DX12 startup error remains.
