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

### Pause-menu rooms, gate launch button, keypad removed — queued 2026-11-11
- Commit(s): uncommitted
- Changed: pause-menu Room page now has two modes. In Compound it keeps the code keypad, Join Room Code, Join Random, Create Public and Create Private. In an expedition (`OnTrain`) it shows only "Room Code: <code>" and **Back to Compound**. That button makes this client alone leave and join a random public compound, or create one if none is open; the crew stays aboard. The physical private keypad and its terminal box are gone from Compound. A wall text says "Want to create a private room? Do so from the pause menu". In private staging, the departure gate shows "<owner>, push to start!" above a **LAUNCH EXPEDITION** button. Only the Master Client can press it, and it calls `StartPrivateRun`. The Tab desktop menu lost its terminal-distance gate and its Start button.
- Setup: 2 clients. (a) Client A, Compound: pause menu > Room > type five digits > Create Private Room. Client B joins that code from its pause menu. (b) Both look at the gate. B presses the button, then A presses it. (c) In the expedition, B opens Room and presses Back to Compound. (d) Repeat (c) from a public expedition with no public compound open.
- Check: (a) both are in the same private staging room. The gate shows A's name and B sees the same prompt. The public countdown text and launch button are hidden in public compounds. (b) B's press does nothing. A's press departs both clients. (c) Room page shows only the code and Back to Compound, with no keypad. B lands alone in a public compound and A stays on the train. (d) A fresh compound is created. The wall hint is readable from the spawn area. After a Master handoff in staging, the prompt names the new owner.
- Cheap checks: compile clean, no Console errors. RailSubwayCompoundChecks passed, including the new launch-button, keypad-removed and hint checks. Wall and Room-page layout were previewed with editor cameras.
- Needs human: launch-button poke feel and hint readability at duck height in a headset.
- Priority: normal

### Pause menu present in Compound and Expedition — queued 2026-11-11
- Commit(s): 5aaa6ffdb
- Changed: PauseMenu.prefab and the CameraTablet it uses are now in Compound.unity and Expedition.unity. Before this they were only in the unused Main.unity, so P/Start opened nothing. `PhotonSession.JoinOpenRoom` now does nothing while in a room (Duckz behaviour). Before this, closing the menu left the current room for a random compound.
- Setup: one client. Compound, then depart into an Expedition (train moving). Desktop keyboard in the editor.
- Check: P opens the menu about 1.2 m in front of the head. It can be grabbed, and its Players/Rooms/Settings/Camera pages work. The Camera page opens the tablet above the menu. Pressing P again closes it, and the client stays in the same room (same room code, no "Leaving room..."). The test panel's GAME MENU button opens it. Dying closes it.
- Needs human: Start button on a Quest controller, and the poke/grab feel in a headset.
- Priority: normal

### Test panel on L3, admin-gated — queued 2026-11-11
- Commit(s): 7ae3a4e70, d46433b7b
- Changed: in VR the test panel toggles on L3 instead of the left menu button. Menu buttons always open the pause menu. The panel needs the Editor or a backend admin in development builds. GoldBarDropper is removed.
- Setup: Editor VR rig, single client; then development build on headset with an admin account and a non-admin account.
- Check: Editor: L3 opens/closes the wrist panel, menu button and P open the pause menu (not the panel), the Game-view overlay button opens the panel in keyboard and VR modes, Esc/Close hides the keyboard panel. Dev build admin: L3 opens panel after sign-in. Dev build non-admin: L3 does nothing, menu button opens pause menu. No gold bar drops on L3.
- Needs human: headset admin/non-admin accounts.
- Priority: normal

### Stranded non-master leaves the ending train — queued 2026-11-11
- Commit(s): d9ab4c4fc
- Changed: a client still in a public expedition during `Returning` that cannot follow the crew leaves on its own. This covers a client missing from `rails.users`, an expired transfer window, or a crew handoff that stopped without arriving. It goes to the crew's known destination, then its previous compound, then any compound. Reported: three-player win, one non-master stayed aboard forever.
- Setup: 3 clients, public departure, simulate success. (a) Normal return: all three reach one compound. (b) Disconnect one non-master during Ending and rejoin after the return token is committed. (c) Block one client's crew-return control connection (for example, invalid auth) so its handoff expires.
- Check: (a) unchanged crew return. (b) and (c): the stranded client leaves the expedition within about 5 s of being stranded and lands in a compound (the crew's compound when known). No client remains on the train beyond about 100 s after the results countdown.
- Needs human: confirming the original headset repro no longer happens.
- Priority: normal

### First-hold finale tower defenders — queued 2026-11-11
- Commit(s): uncommitted
- Changed: finale selects exactly two fixed tower spawns, both regular zombies by default; marker Enemy Type dropdown supports tuning. Existing retained first-hold trigger and room-owned deduplication remain. Clients require the `-tower-finale-v1` matchmaking suffix.
- Setup: two updated clients; use the bridge approach shortcut, then physically hold the button. Repeat with non-master operator, master handoff and late join during a paused hold; start a fresh expedition afterward.
- Check: no finale enemies before first valid hold; exactly one appears on each tower floor afterward and can approach the button. Release/repress and simultaneous holds add no enemies. Both peers and late joiner agree on the same two objects; handoff adds none. Bridge still pauses/resumes/completes normally; fresh run allows a new pair. Existing scavenging enemy counts stay unchanged.
- Cheap checks: compilation; 100 seeds each on saved prefab/live Expedition instance; floor support, clearance, baked ground paths to button; first-hold state serialization/reset; all 17 bridge hold checks passed. Console had an unrelated Unity Connect token-exchange error.
- Needs human: tower spawn presentation and defense difficulty in headset.
- Priority: normal

### Gold crate depositor-only payout, Duckz shower and sound — queued 2026-11-11
- Commit(s): uncommitted
- Changed: shop/crate deposit pays 100 coins to the depositor only; crate plays the 100-coin Duckz shower and the bar's deposit sound. Others are not credited, observers see coins fly into the depositor.
- Setup: two or more clients on a run; deposit one bar at the departure shop and the helipad, from the master and a non-master; also a late joiner deposit.
- Check: depositor's wallet +100 (or up to cap) and watch counts up; other wallets unchanged; depositor sees 100 coins land in the watch, others see them fly into the depositor's body; deposit sound audible to all near the crate; floating "+100" text rises at the crate for everyone (CutePixel font); label reads "+100 coins per bar"; no repeat on reconnect. Compare against Duckz gold crate.
- Needs human: shower/sound feel in headset; economy balance (100 per bar is far above the old budget).
- Priority: normal

### Midpoint and finale loot — queued 2026-11-10
- Commit(s): uncommitted
- Changed: midpoint has 21 coal / 21 gold candidate markers; finale has 28 / 26. Each selects fixed 6 coal / 2 gold. Route now includes midpoint loot; updated clients require the `-special-loot-v1` matchmaking suffix.
- Setup: two updated clients on a normal expedition; inspect midpoint and finale before crossing the finish line. Repeat with late join and master handoff.
- Check: six coal and two gold at each special block, matching objects/poses on both clients; no duplication after handoff/rejoin. Items settle on intended surfaces, remain grabbable, and coal works in the furnace. Midpoint gold pays only through the existing shop intake. Preserve normal scavenging crew scaling and existing finale enemies/bridge behavior.
- Needs human: placement taste, roof/tower reach, tree-root visibility and resource balance in a headset.
- Priority: normal

### Water volumes: wet steps and head underwater — queued 2026-11-10
- Commit(s): uncommitted
- Changed: new WaterVolume (box, top face = surface). Footsteps/hand hits below the surface use the Water surface; head under the surface plays splash in/out, muffles audio (listener low-pass), tints with fog and plays bubbles every ~8 s. Lake in lakeside-campground-v1; shallow wading strip over the finale Ravine floor. See [water](water.md).
- Setup: one desktop client and one headset, campground first. Walk into the lake from the dock/shore, duck head under in the deep middle (bed Y=-2, surface Y=-0.79) and come back up. Repeat while a second client talks nearby. Finale: fall into the river and walk to the recovery stairs.
- Check: wet footsteps and hand slaps in the lake and river; splash in, muffled audio plus teal fog, bubbles after ~8 s, splash out and a clean restore of fog/audio when surfacing (also after teleport/fall recovery while submerged). Pass: no Console errors, fog returns to the block's original look. Finale head stays dry.
- Needs human: how the muffle, fog tint and splash levels feel in a headset; Quest cost of fog.
- Priority: normal

### Player walls and fall recovery — queued 2026-11-10
- Commit(s): uncommitted
- Changed: invisible ±48 m block side walls; local last-ground recovery below the kill floor or past a wall (no down); loose RailCargo below the kill floor is PhotonNetwork.Destroyed. See [player bounds](player-bounds.md).
- Setup: two clients on a normal route. Desktop client walks into a side wall on two different blocks. Then use a test teleport or fixture to drop each client below the floor: off the block ground, from the train while it moves, and twice within 6 s. Throw coal, a gold bar and a dropped bag below the floor (e.g. canyon or past a seam).
- Check: walls block walking and throws at |x| = 48 but not the track or seams. A fall returns the player within about 0.5 s to their last ground spot (train-relative if it was the train), with no down and no revive. A repeat fall steps further back. Items below the floor disappear on both clients with no Console errors or recovery retries. The victory ride still returns players aboard after 2 s. No new recurring allocations.
- Needs human: headset feel of wall contact and the instant warp; hand-climbing the invisible wall.
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
