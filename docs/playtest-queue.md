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

### Results board superlatives: stats, counters and award assignment — queued 2026-11-12
- Commit(s): uncommitted (B clone)
- Changed: honks removed; board shows Coal, Gold (deposited), Revives, Downs; 20 superlatives + Precious Cargo assigned once at the finish (`RailAwards`), counters from the master's 10 Hz sample and a batched client `stats` request. Spec: `docs/game-design.md` "Superlatives and board stats".
- Cheap checks: `Duck Rails/Legacy/Run Superlative Checks` (`RailAwardsChecks`) PASSED 2026-11-12 in B's editor (MCP verified `Application.dataPath` = `X:/Work/duck-rails-b/Assets`): project compiled with 0 errors (only pre-existing warnings) and the menu item logged "25 superlative checks passed". Edit-mode only; no Play Mode, network or headset run. Later the same day the new `Boundaries` method (20 awards x 2 assertions) first failed at Pacifist (float `RunSeconds * .8f` rounding); after `Present()` and Window Seat were changed to exact integer multiples, it recompiled with 0 errors and the menu item logged "65 superlative checks passed" (all awards' minimums are inclusive, just-below gets no title).
- Setup: private run to a bridge finish with 2+ clients (3+ ideal), a shop purchase, a gold sale, a revive, a hit on an enemy, a flashlight on for a minute.
- Check: board rows show Coal/Gold/Revives/Downs and exactly one title each; no title repeats; the top stat holder gets the title; players under every minimum read Precious Cargo; a non-escaper reads Missed the Train unless a higher title applies; titles are identical on every client and after a master handoff or reconnect before the finish; failed runs show titles too.
- Needs human: whether thresholds, the 15 m / 2 m proximity radii and the stillness limit feel right with real VR hand jitter.
- Priority: normal
- Mode: report-only

### Train aboard margin: escaped and end-of-run teleport — queued 2026-11-11
- Commit(s): 84985d551
- Changed: `RailTrain.ContainsPadded()` (Inspector-tunable `paddedMin`/`paddedMax`, yellow gizmo when selected) now decides who "Escaped" at the bridge finish and whether the end-of-run rescue teleport fires. Horns, boarding and cargo keep the tight `Contains()`.
- Cheap checks: compiles; fields read back on the train prefab instance. Not run in Play Mode.
- Setup: private run to a successful bridge crossing, one desktop client (plus a second client if available).
- Check: a player hanging off the side by hand, or standing on the front of the train, at the finish is listed "Escaped" and is not teleported. A player on the ground well away from the train still reads "Didn't make it" and is teleported to the deck.
- Needs human: hanging off the side by hand in VR.
- Priority: normal

### Expedition render warmup: flashlight and helicopter TV first use — queued 2026-11-11
- Commit(s): 1adf32ebe
- Changed: on every Expedition load, the transition veil stays up while `ExpeditionRenderWarmup` draws each scene and catalog render state with lights off and with a warm spot light, through the viewer camera and a copy of the helicopter feed camera. See docs/transition-veil.md.
- Cheap checks: compiles, Console empty. Not run in Play Mode.
- Setup: Compound → private run departure, one desktop client in DUCKZ_DIAGNOSTICS, then a second departure in the same session; one late join aboard a running train from Login.
- Check: log `[ExpeditionWarmup] Prepared N render states...` with N > 0 on the first departure, no "time limit", and far fewer states on the second departure. Veil reads ALL ABOARD / "Getting the train ready...", then releases. A late joiner sees no uncovered Expedition frame before the veil. No leftover "Warm proxies" object or extra camera after release. Scene lights such as the helicopter light come back on afterwards. Editor-only: proxies visible in the Scene view during the warmup is fine.
- Needs human: Quest development build profiled on a cold shader cache. Confirm the first flashlight or helmet light-on and the first helicopter TV view produce no shader or pipeline-creation spikes, and record the added veil time.
- Priority: normal

### Compound Discord button — queued 2026-11-11
- Commit(s): uncommitted
- Changed: local JOIN DISCORD physical button with a "Join the Discord!" heading on the inner right partition of the Station Info nook; opens https://discord.gg/sfyvhahnDW.
- Cheap checks: installed via Unity MCP, installer validation (callback, URL, CutePixel labels) passed, no Console errors; camera capture shows readable placement.
- Setup: Compound, one desktop client and a Quest client.
- Check: desktop click opens the invite in the default browser; headset touch opens it in the Quest browser. Repeated contact within 3 s opens it once. Returning to the game leaves the session usable; no room transition.
- Needs human: Quest browser launch/return, headset reach and readability at duck height.
- Priority: normal

### Compound rating button and instruction PNG — queued 2026-11-11
- Commit(s): uncommitted
- Changed: local RATE GAME physical button and replaceable 1024 × 768 PNG canvas in the Station Info nook; Quest requests native Meta rating/review UI, desktop opens the configured app's store listing.
- Cheap checks: Unity 6000.3.24f1 compiled and installed in batch Edit Mode, exit 0; callback, texture, canvas and font references passed; no C# errors or exceptions. Scene diff limited to the new instance.
- Setup: Compound, one desktop client and a Quest client authenticated through the normal login flow.
- Check: desktop click opens Duck Rails' store listing; headset touch requests the native rating UI without an eligibility precheck. Cancel/close returns to usable gameplay. Failures display a retry message, repeated contact does not repeatedly launch requests, and no room transition or reward occurs. Replace PNG while preserving its .meta and confirm the saved canvas picks up the new art.
- Needs human: actual Meta review-page launch/cancel, headset reach/readability and normal session recovery after system UI. Do not submit a rating as part of testing.
- Priority: normal

### Pause menu and rooms: headset buttons and feel (5aaa6ffdb, d480df22a) — queued 2026-11-11, manual-only
- Commit(s): 5aaa6ffdb, d480df22a; fix 1f1203b1c (automated part PASSED 2026-11-11, see Results)
- Check: Start button on a Quest controller and the P key open/close the menu (key input was not simulated). Poke/grab feel of the menu, keypad and gate LAUNCH EXPEDITION button. Hint readability at duck height.
- Needs human: all of the above.
- Priority: normal

### Test panel on L3, admin-gated: controllers and dev builds (7ae3a4e70, d46433b7b) — queued 2026-11-11, manual-only
- Commit(s): 7ae3a4e70, d46433b7b; help-text fix aeb8c3051 (editor toggle PASSED 2026-11-11, see Results)
- Check: Editor VR rig: L3 opens/closes the wrist panel and the menu button opens the pause menu. Esc closes the keyboard panel. Dev build: admin L3 opens the panel after sign-in; non-admin L3 does nothing and the menu button opens the pause menu; no gold bar drops on L3.
- Needs human: controllers, admin and non-admin accounts on a headset.
- Priority: normal

### Stranded return: three clients and the original repro (d9ab4c4fc) — queued 2026-11-11, manual-only
- Commit(s): d9ab4c4fc (2-client cases PASSED 2026-11-11, see Results)
- Check: with 3 clients (so the expedition room stays open), disconnect a non-master during Ending and rejoin after the return token; it must leave within about 5 s. Confirm the original three-player headset win no longer leaves anyone aboard.
- Needs human: headset repro. The 3-client rejoin case can be run with the bridge (see testing-tools) when a third account is configured.
- Priority: normal

### Finale defenders, loot, gold crate, water, walls: headset feel (0a062e440, 2b8c0d3e8, a38008a7b, 3a3056c87, 1f35eb82b) — queued 2026-11-11, manual-only
- Commit(s): as listed (automated parts PASSED 2026-11-11, see Results)
- Check: tower spawn presentation and defense difficulty; loot placement taste, roof/tower reach, resource balance; coin shower into the depositor, deposit sound and +100 text as seen by others; water splash/bubble/muffle levels and teal fog in a headset, Quest fog cost; invisible-wall contact, hand-climbing the wall and the instant fall-recovery warp. Economy balance at 100 coins per bar.
- Needs human: all of the above.
- Priority: normal

### Zombie hit shapes: in-game throws and swings (dcd2f9e1, 62ea1a73) — queued 2026-11-10, manual-only
- Commit(s): f073d29a4; fixture fixed in b76f4618f (automated part PASSED, see Results)
- Changed: zombie hit shapes follow animated bones (all five enemies); thrown items hit the earliest-contacted enemy; Spider head hit radius 23 cm.
- Setup: 1 client in Play Mode (headset preferred): throw/swing bat and gold bar at each enemy type, including crowded pairs and a locked (just-hit) zombie.
- Check: hits land on visible limbs/head; a throw hits the nearer of two zombies; deflect direction sane; held-swing feel unchanged.
- Needs human: all of the above (visual fit on animated enemies), throw and grab feel in headset.
- Priority: normal

### Enemy tuning + late-join flares: in-game (22262f75, 98ec9f5b, 6f4c3cba) — queued 2026-11-10, manual-only
- Commit(s): 1ea23d07f; fixture fixes 136fe60ea, 8005743a5 (automated part PASSED, see Results)
- Changed: speeds rebalanced, retreat 4–12 s, all zombies fear 40 s flares, flares restored for late joiners, smaller kill triggers, slower spider stalk.
- Setup: 2 real game clients: client A dies (flare), client B joins late, once on the ground and once aboard a moving train.
- Check: B sees the active flare with correct remaining life; enemies of every type avoid it; flare expires ~40 s. Known gap: flares are world-space, so one dropped on a moving train is left behind (not train-relative); record what B sees, do not treat as a fix target here.
- Needs human: speed/kill-trigger feel in headset; real-game late join (not covered by the batch fixture, which uses inert avatars).
- Priority: normal

### ZombieHook pull: no snapback, hands/gear clear of surfaces (6f27966d, 7b5dad14) — queued 2026-11-10, manual-only
- Commit(s): see git log "Port Duckz ZombieHook pull"; batch fixture re-run on HEAD 2026-11-10 PASS (see Results)
- Changed: hook pulls no longer snap the VR player back; while pulled, hands, held weapons/items and a hand-held bag stay out of floors and walls (locally and as sent to others), releases mid-pull drop instead of fling, and observers keep dropped items above the floor.
- Setup: 2 clients (A in headset). Force `RailHook` spawns. Pull A (1) on the ground near a wall, (2) standing on the moving train, including near the cabin walls/rear, (3) on a curve. Repeat with bare hands, nail bat, gold bar, and a hand-held bag; release the item mid-pull once inside and once outside the train cargo volume.
- Check: no rewind after the pull; no hand/bat/bag visibly inside geometry for A or B; train travel neither lost nor added; item released on the train becomes cargo and rides along, one released off it falls in place; B never sees a dropped bat tip through the floor.
- Needs human: headset hand/arm feel during and after the pull, comfort aboard the moving train.
- Priority: normal


## Results

### Queue run 2026-11-11 — live multi-client portions (Unity 6000.3.24f1, MPPM, tested at f7aeaf8de + fixes)
Two real clients: the main editor and the MPPM Player2, on the Photon cloud and backend, with the desktop simulated rig. Unity MCP drove the main editor. Player2 was driven through the new test-only `RailRemoteBridge`; see [testing tools](testing-tools.md). Buttons used their own `Press()` paths. P, L3 and Start were not simulated: an unfocused editor drops injected key input. Full evidence: [2026-11-11 live run](validation/playtest-queue/2026-11-11-live-queue-run.txt). The working tree also had unrelated uncommitted train/bridge-finale prefab edits.

- **Pause menu in Compound and Expedition — PASS** (automated part), after one fix. The menu opens 0.5 m in front of the head (the prefab value, same as Duckz; the entry said 1.2 m). It is grabbable and all four pages switch. The camera tablet opens above the menu and closes. Closing keeps the same room in Compound and Expedition. The GAME MENU path opens it and death closes it. FAIL first: opening the tablet logged two kinematic-velocity warnings. Fixed in `1f1203b1c`; the re-run was clean.
- **Pause-menu rooms, gate launch button, keypad removed — PASS** (automated part). (a) The keypad create/join put both clients in private staging 48213, and both gates show "lebo, push to start!". The gate is hidden in public compounds. The terminal is gone and the CutePixel hint is present. (b) The non-master's press was refused, even with a forced event. The owner's press departed both clients. (c) In an expedition the Room page shows only the code and Back to Compound. Back moved Player2 alone; main stayed aboard. (d) From a public expedition with no compound open, a fresh compound was created. After a staging owner handoff, the prompt names the new owner.
- **Test panel on L3 — PASS** (editor toggle only). The overlay-button path `Toggle()` opens and closes the panel without opening the pause menu, and GoldBarDropper is gone. Stale "P opens the test panel" help text was fixed in `aeb8c3051`. L3, the menu button, Esc and dev builds remain manual.
- **Midpoint and finale loot — PASS** (automated part). Each special block has 6 coal and 2 gold on both clients, with identical ViewIDs, tokens and poses, resting on real surfaces. After a Player2 rejoin, a master handoff and a late rejoin, the loot is the same with no duplicates.
- **Gold crate depositor-only payout — PASS** (automated part). Master, non-master and late-joiner deposits each paid +100 to the depositor only, with one recipient each. The observer saw "+100" in CutePixel at the crate. No replay after a rejoin or late join. The shower and sound were not audible-checked.
- **Player walls and fall recovery — PASS** (automated part). Walls sit at ±48 m on three blocks, with none on the track line. Below-floor falls recover in about 0.27 s of game time, with no down. A repeat fall within 6 s steps back one entry. Lateral recovery works past the wall. A fall from the moving train returns the player to the stored train-local spot. Coal and gold below the floor are destroyed on both clients.
- **Water volumes — PASS** (automated part). With the head under the lake: Water surface, a 900 Hz low-pass and the teal exponential fog. Teleporting out while submerged restores the exact original fog and removes the filter. In the finale river the floor is Water and the head stays dry.
- **First-hold finale tower defenders — PASS** (automated part). There are no defenders before the first hold. A non-master hold spawned exactly one RailZombie per tower, with the same views on both clients, and they walk to the button. The pair is unchanged after release, repress, a master handoff, a late join during the paused hold, and simultaneous holds. The bridge paused, resumed and completed, and a fresh run spawned a new pair. One earlier run ended "Bridge collision": a test artifact from an injected simulated throttle press.
- **Stranded non-master leaves the ending train — PASS** (2 clients, not 3). (a) A normal public return puts both clients in one fresh compound in about 6 s. (c) Cutting the client's crew-return control connection at the token made it leave and join the crew's compound. (b) The disconnect/rejoin case could not keep the room open with 2 clients: Player2's rejoin hit "Room not found" and it fell back into the crew's compound. Separately, an MPPM-only exception killed one handoff before it started. That client rode the train for about 90 s, then the new safety net moved it into the crew's compound, within the ~100 s limit. The clone was missing `Library/ScriptAssemblies`; that environment issue is fixed (see testing tools).

### Queue run 2026-11-10 — automated portions (Unity 6000.3.24f1, batch, tested at HEAD 5b21d0009 + fixture fixes)
Run in the ignored disposable copies `tmp/duckz-port-validation` and `-2` (synced to `main`), separate batch processes. Manual/headset parts were not run and remain in Pending.

- **Zombie hit shapes — `GrabbablePlayChecks`: PASS** (1 process). Initially FAIL at "Actual grab": `PhotonMasterHandoff` exists in Play Mode and the unfocused batch process has no input focus (`handCanGrab=False avail=False`). Fixture-only fix `b76f4618f` grants focus as `ZombieHookPullChecks` does. Re-run passed: idle/moving held and thrown sweeps, frontmost forearm contact in both spawn orders, cooldown front lets rear take the hit, dormant/respawned Bomber. [Evidence](validation/playtest-queue/2026-11-10-grabbable-play-checks.txt)
- **Enemy tuning + late-join flares: PASS** (fixtures).
  - `DeathLifecycleChecks` (1 process): FAIL ×3 then PASS. (1) "Expired network event ignored": the port missed 98ec9f5b's second check hunk (30 s is no longer expired with a 40 s lifetime); fixed `136fe60ea` with the source expression. (2,3) `DuckHomeChecks` follow/conversation assertions: Rails `PhotonPlayer.ForActor` only resolves remote avatars with a received pose (d7ddf37db), the imported fixture never gave its players one. Fails identically when run alone, so it predates the port. Fixture-only fix `8005743a5`. No production code changed.
  - `SmasherRetreatJumpChecks` (1 process): PASS ("ALL PASSED"; `Run` does not exit by itself, process stopped after the pass).
  - `FlareJoinNetworkChecks` (2 batch editors, real Photon cloud): host and guest PASS: late join reconstructs both flares once with original launch time, RPC/property dedup, master leaves mid-smasher-fall and the successor lands it once while respecting the flare, flares survive the publisher leaving, independent expiry, expired deaths never replay. First attempt BLOCKED by the second copy failing its first-open package resolution; passed after warming it.
  - [Evidence](validation/playtest-queue/2026-11-10-enemy-tuning-flares.txt)
- **ZombieHook pull — `ZombieHookPullChecks` re-run on HEAD: PASS** (24 pull + 6 surface cases, same values as the port run). Everything else in that entry is headset/two-client. [Evidence](validation/playtest-queue/2026-11-10-hook-pull.txt)

## Dropped

_(empty)_
