# Default-return throttle regrip — November 16

Queue entry: **Throttle hand offset, variable speed and return**. Unity6000.3.24f1,
two authenticated desktop clients. Headset feel and physical input delivery stay open.

The original control returned locally in0.25seconds while its released lease was
still advertised. `TryGripDrive` rejected every nonzero shared actor, including
the local actor's exact already-released token. The narrow trace records that
token throughout the nonzero return; shared ownership clears only after the
artwork reaches zero. This was a genuine mid-return regrip gap, not permission to
extend the authored duration. See [original trace](throttle-narrow-actor3.txt).
The trace's separate second-grip setup failed before the panel interruption, so
it supplies no panel-cleanup acceptance.

`RailControl.CanRequestPhysicalGrip` now permits either an unowned control or the
local actor's exact released token in the same current run. Other actors, other
tokens, empty tokens and stale runs remain ineligible. The master still grants a
new GUID through unchanged `RailDriveLease` arbitration. Reliable release precedes
the new request; old-token messages cannot drive or cancel the new lease. Desktop
T, return duration, tracked hand data and prefab tuning are unchanged.

Validation:

- [Edit Mode](throttle-regrip-editmode.txt):27 drive checks and42 session checks,
  including same-actor release/new grant and delayed old heartbeat/release rejection.
- [Nonmaster actor1](throttle-default-postfix-actor1.txt):32checks/0failures;
  [master actor2](throttle-default-postfix-actor2.txt):24checks/0failures. Actual
  physical-palm simulated grip,20% deadzone/proportional speed/85% detent, STOP,
  default return and real new-token regrip pass. Return observation0.24537seconds
  is within one measured0.02989second frame of the0.25second duration.
- At13:59:19 the regrip input was requested while actor1's own released token was
  still shared. Acquisition captured/rendered0.9016726, then received and retained
  the new authority lease. No return extension or direct grip callback was used.
- Disposable inactive controls tested the eligibility predicate against the real
  occupied shared lease: empty/wrong token and stale run reject; exact token/run
  admits only its local actor. These predicate checks are distinct from the real
  acquisition above and never request authority or change the live control.
- Existing contention/pre-ack release regression also passes after the fix:
  [actor1](throttle-edges-postfix-actor1.txt)19checks/0failures and
  [actor2](throttle-edges-postfix-actor2.txt)15checks/0failures. Concurrent authority
  requests, rejected competitor heartbeat/release, release before grant, physical
  peer palm, extended-window regrip and injected focus-loss cleanup pass. Its
  deliberately extended2.5-second return is separate from the default-duration
  positive case above; it is not another default-window claim.

Original scripts and raw role logs are preserved in
[the pre-fix archive](throttle-before-own-release-regrip-fix/).
Independent source review found no actionable issue; live acceptance is limited
to the measured desktop scopes, not natural headset reach or WAN jitter.
