# Revive effect following — November 16

Unity 6000.3.24f1, three authenticated desktop clients. The original moving and
victory-ride run exposed five remote follow failures on each peer: the effect
was positioned in Update before PhotonPlayer interpolated its head in LateUpdate.
The failure was independently observed after interpolation, not inferred from
editor-tick timing. Original evidence is retained in `revive-fx-before-follow-*`.

`RailReviveFollow` now follows the current actor head in LateUpdate order 10000,
after PhotonPlayer. Present still sets the initial position and updates the actor
on reconnect. Receipt, audio and particle logic is unchanged.

The corrected run passed ground helper handover and overlapping rescues, moving
4 m/s rescue, withdrawal/reset, master handoff, the normal 32-second moving
victory ride, one completion sound per receipt, frozen victory scores, late
progress join and post-completion leave/rejoin. Continuous post-LateUpdate checks
at order 30000 sampled every playing particle frame with a 5 cm tolerance:

- Main: 10 completions, 522 playing-frame observations, zero failures.
- Player2: 10 completions, 533 observations, zero failures.
- Player3: 8 completions, 422 observations, zero failures; absent during two
  intentionally seeded late-join retries.

Evidence: `revive-follow-fixed-*`, `revive-moving-follow-fixed-*`,
`revive-late-follow-fixed-*`, and `revive-fx-follow-fixed-*`. The observer source
is `revive-observer.cs.txt`. Late join covered 36 warmup frames with zero uncovered
frames and no historical sound replay during four seconds after rejoin.

The ground rerun did not sample the 0.1-second grace pause itself; the earlier
uninterrupted run remains that specific evidence. Moving tests seed down state,
then exercise actual shared proximity/progress/completion. The runner was held
open for the related fixtures and restored before Stop. No editor commands were
compiled during narrow timed assertions. Temporary trace logging was removed.

Headset appearance and sound quality remain Needs human. Local bounds recovery
executes later than this follower; a recovery teleport frame was not tested.
