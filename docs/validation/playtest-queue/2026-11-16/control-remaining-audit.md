# Narrow control/results audit — November 16

Prepared fixtures are not passing evidence. This note audits the queue wording,
existing run logs and current source; no Unity operation or product edit was made.

## Throttle

- Literal checks include the 20% deadzone/proportional speed, 85% forward detent,
  approximately 0.25-second return, release before acknowledgment, regrip during
  return, and safety cleanup on blocked input/down/disconnect/host departure.
- A27 RUN2 proves current actual simulated physical grip/GO, remote attachment,
  competing/denied lease, pre-ack request/release and injected focus-loss cleanup.
  Its regrip deliberately extends return to2.5s; it does not prove the normal window.
- `throttle-default-network.cs.txt` adds actual physical travel samples, normal
  return without tuning changes, and one ordinary grip opportunity during that
  return. An unsampled/expired short window is BLOCKED, not a gameplay defect.
- Actual attached-hand menu/down/disconnect cleanup is absent from the fresh
  November16 physical-grip evidence. Earlier November12 lease-level fixtures and
  November13 fueled disconnect/handoff evidence exist; these are not wholly
  untested production branches. Do not imply that all older evidence vanished.
- Queue wording does not require the full Cartesian product of default-return
  timing with every cleanup cause. No extra timing matrix is added here. Natural
  downing aboard is rejected by gameplay; a seeded down flag would only prove the
  interruption branch, not natural downing while gripping the train control.

## Radio

- Literal setup includes two clients, station/handheld walkie, late join and moving
  train. The check says late joining retains station operation; it does not demand
  a fresh join halfway through an already-held transmission.
- Existing A20 radio logs prove direct Press/Release lease/transmitter plumbing,
  cap response and remote snapshots. They do not prove physical finger contact.
- `radio-contact-moving-network.cs.txt` uses the actual posed finger collider and
  production button, checks moving lease renewal and actual control disable while
  still touching, then withdrawal/re-enable. It makes no voice-quality claim.
- If `coal-late-fuel-authority.cs.txt` genuinely leaves to Compound and recreates
  the peer expedition, then the radio fixture passes afterward in the same run,
  that ordered chain establishes station operation after late join. Record the
  common run and new actor; no additional held-during-join scenario is mandatory.
- Natural station-to-handheld intelligibility, microphone/audio quality, icon
  visibility, reach and headset appearance remain human acceptance.

## Wheels

- A13 proves observed ordinary acceleration/braking, phase from shared distance,
  fixed axles/mounts and frozen stopped phase. The retry label “normal4” with max20
  remains invalid as normal-speed evidence; the earlier measured4 observation is
  separate. Setting Fuel0 through SetFuel immediately stops/rebases motion, so it
  is not natural exhaustion/coasting evidence.
- Literal unmeasured cases: wheel phase during real fuel-exhaustion coasting,
  fresh-client moving late-join reconstruction, and explicit fresh-run replay.
  Current prepared coal/throttle/radio/repel fixtures do not measure those wheels.
- Prepared `wheels-coast-moving-join.cs.txt` is bounded to60s including rendezvous.
  It requires normal4m/s mode,150m before finale and an idle stopped train; sets
  controlled starting fuel30, drives through ordinary requests, observes genuine
  distance-based depletion/coasting on both clients and a real Compound-to-moving
  fresh-scene join, then restores original fuel at the final traveled distance.
  It is not run evidence and does not establish fresh-run replay/headset appearance.

## Revive follower

- Literal check is completion-only playback, following the revived duck, clean
  finish/repeat and no historical replay. A22/A23/A26 already cover ground,
  moving, repeated, handoff, victory and late-join behavior after the ordering fix.
- `RailReviveFollow` runs LateUpdate10000; local bounds recovery runs12500. A local
  recovery teleport after the follower could create one frame of positional lag.
  This is an unmeasured source-order edge, not an observed failure, and the queue
  does not explicitly add teleport-during-revive acceptance. Do not expand the
  required matrix or reopen accepted moving-follow evidence on this hypothesis.

## Results attribution

- `coal-late-fuel-authority.cs.txt` measures actual committed coal receipt replay
  suppression plus nonmaster Fuel100 rejection, not natural throw/VR acceptance.
- `repel-stat-network.cs.txt` measures exactly+1 shared sender credit after one
  locally accepted callback hit, with independent authority reaction/confirmation.
  Source credits local dispatch before authority confirmation; it is not a count
  guaranteed to represent confirmed knockbacks in every possible network race.
- `flashlight-sixty-stat.cs.txt` measures actual held/on equipment and normal stats
  batching for at least60 authoritative seconds. It missed the public timing
  cutoff and is prepared for the private batch, not already passed.

No new exhaustive award matrix, combined interruption matrix, microphone-quality
claim, or speculative product fix is required by this audit.
