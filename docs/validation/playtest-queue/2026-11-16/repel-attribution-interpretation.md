# Repel attribution — report-only interpretation

The fresh two-client run in private room68433 submitted one controlled non-owner
`Enemy.SubmitSwing` callback to existing spider109. This was a callback fixture,
not a natural weapon strike. The peer recorded local sequence1 and actual
authority confirmation; the master observed the target's hit reaction.

Both recorded participant baselines were0. At the final assertion the peer's
shared counter was1, but the other participant's counter had also become1. The
fixture therefore failed its “other participants receive no repel credit” check.
Do not relabel the complete fixture PASS. Its `beforeOthers` dictionary was not
serialized, although the logged owner baseline0 and later retained owner1 resolve
the relevant two-player counter comparison.

Subsequent read-only snapshots on both clients agreed: cached and retained state
contained actor1=1 and actor2=1; both local pending counters were0. Crucially, the
master's spider109 `EnemyNetSync._hitSequence` was2, while the peer's was1 with
confirmation true. The master's local `lastRepel` was70.42538, versus the peer's
69.05373 (each process's unscaled clock, not directly interchangeable timestamps).

Current source increments `_hitSequence` only in local `SubmitHit`, which also
calls `RailStats.Repel`. Receiving `RpcHit` instead calls `ReceiveTrustedHit` and
does not increment that local sequence or local repel counter. The master thus
also made local submissions during this target's lifetime; its extra recorded
credit cannot be interpreted as proof that the peer's RPC was credited twice.
The concurrent moving-throttle scenario did not establish a no-other-hit control.
The exact object/contact that caused the master's submissions was not logged.
Locally owned/room-owned grabbable swing or thrown-contact paths are possible
callers, not an observed identification of the item.

Accepted narrow evidence: the controlled sender's actual local dispatch,
authority confirmation/reaction, and shared counter change0→1. Exclusivity is
**inconclusive because independent local submissions confound the assertion**.
No stats misattribution product bug is established, and no product fix or extra
award-matrix retry follows from this result.

Source semantics remain important: `Enemy.SubmitSwing` applies local dormant,
speed and cooldown gates before dispatch. `RailStats.Repel` then counts locally
accepted dispatches, rate-limited to one per second, before authority confirmation.
It is not universally an authority-confirmed knockback counter.

Evidence: [master run](repel-stat-master.txt), [peer run](repel-stat-peer.txt),
[master readback](repel-stat-readback-master.txt),
[peer readback](repel-stat-readback-peer.txt). No Unity or production changes were
made during this read-only analysis.
