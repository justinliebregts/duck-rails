# Playtest queue

Changes that need in-Unity verification (Play Mode, single or multi-client) are **queued here instead of being tested immediately**. The user asks for the queue to be run when they are away ("run the playtest queue"). Results are written back into this file.

## Rules for the agent

### When finishing a change
1. Do the cheap checks now: compile, Console errors, EditMode/static checks that take under ~1 minute. Do not launch Play Mode or multi-client scenarios unless the user asks.
2. If the change needs a live check, add an entry under **Pending** (template below) in the same commit as the change when practical. Reference the destination commit SHA once it exists.
3. In the final message say "queued for playtest" and name the entry. Never write "verified" or tick a milestone/`docs/duckz-ports.md` item as validated for something that is only queued.
4. Omit `Mode` unless the entry should be report-only; fix-on-fail is the default.
5. If a later change invalidates a pending entry, edit or merge it. If an entry is superseded, move it to **Dropped** with a reason.

### When asked to run the queue
1. Verify Unity MCP targets Duck Rails, and that Unity compiles with an empty Console of new errors.
2. Work through **Pending** in order (cheapest/single-client first, multi-client later). Between entries, return Unity to Edit Mode and clear leftover test state.
3. Per entry: run the listed scenario, collect evidence (Test Center evidence under `Logs/Diagnostics/test-*`, `Temp/rails-*.txt`, Console excerpts), and record PASS / FAIL / BLOCKED with date, Unity version, client count, and the commit tested.
4. Do not stop on the first failure: continue with unrelated entries. **Fix on fail is the default** (an entry can opt out with `Mode: report-only`):
   - Diagnose, fix, recompile, and re-run that entry. Try at most 3 fix attempts per entry, then mark it FAIL and record what was tried.
   - Commit each fix separately on `main` (only the files changed, follow AGENTS.md), message naming the queue entry, so the user can review or revert each one. Never push.
   - Fixes stay within the entry's intended behaviour. If a fix would change a confirmed design decision, networking/boarding rules, or need a large new subsystem, don't apply it: record the failure and the proposed fix, and continue.
   - After a fix, re-run any earlier PASSed entries that touch the same code.
   - Record the failure, the root cause, the fix commit SHA, and the passing re-run in Results.
5. Move finished entries to **Results** (newest first) with evidence links, and update the matching docs (`docs/duckz-ports.md`, milestone checklist, feature docs) only for PASS.
6. Do not query diagnostic reports (see AGENTS.md).
7. At the end, give a short summary: counts of PASS / FAIL / BLOCKED and what the user must do (headset checks, anything not automatable).

### Not automatable (always stays manual)
Headset hand tracking and VR feel, real network jitter across machines, audio quality, anything marked `manual-only`. List these as **Needs human** in the entry; the run reports them but never marks them PASS.

## Entry template

```
### <short title> — queued <YYYY-MM-DD>
- Commit(s): <SHA or "uncommitted">
- Changed: <one line behaviour summary>
- Setup: <Test Center button / scenario / preset / block order, client count>
- Check: <what to observe; explicit pass criteria>
- Needs human: <headset/feel items, or "none">
- Priority: <low | normal | blocks other work>
- Mode: <fix-on-fail (default) | report-only>
```

## Pending

### Zombie hit shapes + first-contact throws (dcd2f9e1, 62ea1a73) — queued 2026-11-10
- Commit(s): f073d29a4
- Changed: zombie hit shapes follow animated bones (all five enemies); thrown items hit the earliest-contacted enemy; Spider head hit radius 23 cm.
- Setup: fix the `GrabbablePlayChecks` fixture (it fails at "Actual grab", also before this port), then run it in a disposable batch; then 1 client in Play Mode: throw/swing bat and gold bar at each enemy type, including crowded pairs and a locked (just-hit) zombie.
- Check: `GrabbablePlayChecks` passes incl. frontmost contact in both spawn orders; hits land on visible limbs/head; a throw hits the nearer of two zombies; deflect direction sane; held-swing feel unchanged.
- Needs human: throw and grab feel, hit-shape fit on animated enemies in headset.
- Priority: normal

### Enemy tuning + late-join flares (22262f75, 98ec9f5b, 6f4c3cba) — queued 2026-11-10
- Commit(s): see git log "Port Duckz enemy tuning group"
- Changed: speeds rebalanced, retreat 4–12 s, all zombies fear 40 s flares, flares restored for late joiners, smaller kill triggers, slower spider stalk.
- Setup: 2 clients; run `FlareJoinNetworkChecks`, `DeathLifecycleChecks`, `SmasherRetreatJumpChecks`; then manual: client A dies (flare), client B joins late (also aboard a moving train).
- Check: B sees the active flare with correct remaining life; enemies of every type avoid it; flare expires ~40 s; master handoff keeps it; smasher/hook behave near flares.
- Needs human: speed/kill-trigger feel in headset.
- Priority: normal

### ZombieHook pull: no snapback, hands/gear clear of surfaces (6f27966d, 7b5dad14) — queued 2026-11-10
- Commit(s): see git log "Port Duckz ZombieHook pull"
- Changed: hook pulls no longer snap the VR player back; while pulled, hands, held weapons/items and a hand-held bag stay out of floors and walls (locally and as sent to others), releases mid-pull drop instead of fling, and observers keep dropped items above the floor.
- Setup: 2 clients (A in headset). Force `RailHook` spawns. Pull A (1) on the ground near a wall, (2) standing on the moving train, including near the cabin walls/rear, (3) on a curve. Repeat with bare hands, nail bat, gold bar, and a hand-held bag; release the item mid-pull once inside and once outside the train cargo volume.
- Check: no rewind after the pull; no hand/bat/bag visibly inside geometry for A or B; train travel neither lost nor added; item released on the train becomes cargo and rides along, one released off it falls in place; B never sees a dropped bat tip through the floor.
- Needs human: headset hand/arm feel during and after the pull, comfort aboard the moving train.
- Priority: normal


## Results

_(empty)_

## Dropped

_(empty)_
