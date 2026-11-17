# Playtest queue

## End-of-run results table — queued 2026-11-17

- Changed: RESULTS label stats are now a table (`RailResultsTable`): duck names
  on the left (green = escaped, red = didn't make it; no legend line; names over
  20 characters end in "..."), tight Coal/Gold/Revives/Downs columns, then
  Title. The top value in each stat column is yellow (ties all highlighted;
  zeros never).
- Editor evidence: compiles, no Console errors; an offscreen render of an
  8-duck mock at the label's 5×4.5 size and font fits with no wrapping.
- Check: finish a run with 2+ clients; the table is readable in headset at the
  ending position, columns line up, highlights match the highest values, and
  late joiners see the same table. Status: queued for playtest; no Play Mode run.

## Farm northeast pond water footsteps — queued 2026-11-17

- Changed: pond underside MeshCollider now has Water AudioSurface (prefab-only).
- Check: walking through the northeast pond plays water footsteps/hand hits; the
  shore, path and ditch bridge keep their normal sounds. Status: queued for
  playtest; no Play Mode run.

## Ambient director — queued 2026-11-16

- November 17 refinement: long details use random 8–14 second excerpts and
  1.25-second fades; all 13 beds have baked three-second circular overlaps.
  Compilation and 1,236 excerpt/import checks pass, plus PCM boundary checks.
  Listen through complete compressed loops for dips/seams; verify excerpt starts
  vary, ends fade smoothly, short accents remain whole, and pause/resume, area
  changes and gameplay voice reclamation leave no lingering detail sounds.

- Scope: 21 approved Artlist clips, five scavenging blocks, Starting Station and
  Bridge Finale. See [configuration and source selections](ambient-director.md).
- Editor evidence: compilation and 352 focused checks pass; no Console errors.
- Run: visit outdoor/interior regions in all five blocks, station hall/rooms/tunnel,
  and both gorge elevations. Check three-second bed transitions, quiet crickets,
  fluorescent/hum balance, localized wood/metal/water/foliage details and voice
  clarity. Listen through complete bed loops for seams. Ghost accents should
  remain roughly 150–300 seconds apart plus deferred quiet gaps, one at a time,
  with no immediate repeat and none at Starting Station.
- Check split-party clients choose their own locations, shuffled/transformed routes,
  late join, repeat runs, disconnect/listener replacement and successful/failed
  endings. No stale audio should continue into Compound or ending music.
- Measure Quest streaming/CPU/memory and ambience pool use during transitions and
  loud gameplay. Midpoint helipad has no profile in this pass.
- Status: queued for playtest. No Play Mode or headset listening performed.

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

### Finale catwalk metal footsteps — queued 2026-11-17
- Commit(s): uncommitted
- Changed: `bridge-finale-v1.prefab` `Cross-track catwalk +8` and `Off-path button balcony` AudioSurface changed Stone to Metal.
- Setup: Expedition finale block, one client; walk the +8 catwalk and button balcony.
- Check: footsteps and hand hits on both decks play the Metal set; the fort stairs and keep decks still play Stone.
- Needs human: listening check
- Priority: low
- Mode: fix-on-fail

### Admin pause page — queued 2026-11-17
- Layout correction: original four tab positions/scales restored exactly; Test
  sits separately to the right. Check headset reach and unchanged original row
  for both admin and non-admin accounts. Serialized transform comparison passed;
  Unity refresh succeeded, but offscreen preview did not return.
- Commit(s): uncommitted.
- Changed: admin-only Test tab inside pause menu, exactly Deposit 1 coal,
  Deposit 50 coal, No Clip and Invincibility. Removed L3 and separate test UI.
- Evidence: Unity Editor and Android release-reference C# compilation,
  28 focused EditMode assertions, prefab render
  inspection. No Play Mode run. See [current behavior](testing-tools.md#admin-pause-page--november-17-2026).
- Setup: two authenticated clients, one admin and one non-admin; repeat with
  admin as host and peer, in public and private expeditions. Include release
  headset builds and an Editor account without admin privileges.
- Check: non-admin has no Test tab; admin gets only four controls. L3 does
  nothing. Start/P, ordinary pages, grab/follow/dismiss and re-open still work.
  Pokes fire once until withdrawal. Labels immediately reflect each toggle.
- Check: focused Editor Game view F adds 100 fuel once per key press, capped
  at 100; both clients agree with Editor host/peer. Ignore F while typing or
  loading; player builds have no F shortcut. Preserve train motion.
- Check: at partially depleted fuel, 1 coal adds one crew-scaled value and
  50 adds fifty values capped at 100, once per press; both clients agree.
  Train distance/speed/target remain continuous; coal stats/items unchanged.
  Repeat rapid presses, full fuel, stationary players, late join and host handoff.
- Check: No Clip passes through walls, train and ground, moves horizontally
  with left stick regardless of head pitch, rises/falls with right-stick Y,
  and hovers on release. No fall recovery or train/helicopter carry while on.
  Menu remains pressable while flying. Off restores gravity, collision and
  ordinary locomotion without a stale hand-launch impulse; also check warp,
  rig disable, F2 input changes, account loss, room transfer and next run.
- Check: Invincibility rejects enemy, bomber and hazard downs only locally;
  Off restores them even when the old editor enemy-protection preference is on.
  No automatic revive or prevention of shared bridge failure. Toggles reset
  on admin revocation and room/run/account change.
- Needs human: headset reach/readability, actual joystick mapping and flight feel.
- Priority: normal



### Bridge fortress refresh and midpoint defender — queued 2026-11-17
- Scoped live result November 17: **PASS** in one authenticated editor client for
  both initial defenders naturally reaching controls, one reinforcement near
  the 30-second threshold and bridge completion at exactly 60 seconds accumulated
  holding. [Trace](../output/bridge-finale-live/trace.txt); details under Results.
  Remaining criteria below stay queued; this is not multiplayer/headset acceptance.
- Commit(s): uncommitted.
- Changed: new-environment crawlgraph and loot, exported latest build lighting,
  60-second accumulated bridge hold, two defenders approaching controls and one
  extra zombie at 30 seconds. See [implementation/evidence](final-escape-layout.md#fortress-encounter-refresh--november-17-2026).
- Editor evidence: compilation, 1,652 scoped checks and bridge-hold/Week 2/terminal
  regressions pass. Both tower paths and reinforcement path reach control ground;
  54 loot envelopes and 100 seeds/type pass. Three lightmaps/1,311 bindings/498
  probes/69 baked fixtures exported; saved Expedition inherits them.
- Remaining setup: rebuilt matching `tower-finale-v2` clients, bridge finale,
  two real peers and a target-headset build. Solo Play Mode completed; no player build run.
- Check: first physical hold spawns exactly two defenders, both naturally reach
  the control approach without needing initial sight. Pause at 29 seconds and
  wait: no wave. Resume past 30: exactly one additional zombie, approaching the
  controls. Repeated release/resume, master handoff before/after 30 and late join
  preserve count/progress. Complete at 60 seconds total, not 30. Normal pursuit,
  repel/flare/retreat and existing boarding remain functional. Replay resets.
- Check: third defender recovery uses its authored marker; no repeated spawn
  loop or stacking. Loot settles on the new floors/props. In player build,
  fortress fixtures illuminate static surfaces and moving players/items via
  maps/probes without duplicate realtime lights; late join/replay lighting loads.
- Needs human: natural difficulty across crew sizes, loot reach/climb readability,
  target-headset darkness and lighting appearance/performance.
- Priority: normal. Status: queued for playtest.

### Finale fort architecture blockout — queued 2026-11-16

Revision 3 (same day): Starting Station materials, north-east deck on pillars with a yard stair and bridge, west loggia and arcade, climb blocks removed. Also check frame rate on headset: the fort rose from about 6,700 to 18,566 triangles. See `docs/final-escape-layout.md`, revision 3.

Far side pass (same day): low-poly hills, new trees, tunnel arch portals and ribs. Ride the full route through the tunnel into Happy Land and check that nothing clips the train, the valley edges stay hidden, and the new trees and hills read well at dawn lighting. See the far side section of the same document.

Block lights (same day): 38 baked wall and floor block lights around the fort. After the lighting rebake, check the pools read well, nothing is blown out, and the floor lanterns do not block routes.

Revision 4 (2026-11-17): z-fighting fixes, see-through steel walkway replacing the concrete catwalk and balcony (colliders unchanged), brighter floor concrete. Check for any remaining shimmer, and that the lever terminal and gold deposit still work on the balcony.
- Commit(s): uncommitted
- Changed: fort rebuilt to layout v2 (asymmetric keep/depot, stairs, new towers, moved floodlights and east tower defender, re-seated fort loot markers). Blockout only.
- Setup: navigation rebuilt and current lighting exported November 17. Solo finale loading and defender approaches passed; remaining architecture checks use finale via Test Center / route to the bridge, then multi-client.
- Check: no lighting-loader failure for the finale; all seven stairs walkable both ways without snagging; every terrace, roof, wall-walk, both towers, the gangway and the catwalk reachable; breach, collapsed-wall slope and gate usable by enemies; first hold spawns exactly two tower defenders (west keep, east watchtower) that can path to the terminal; 6 coal / 2 gold spawn on valid surfaces over several seeds; train passes the gate gantry and catwalk; no player can fall into a sealed pocket.
- Needs human: headset stair feel at duck height, sightlines through rails, night readability with the moved floodlights, defense balance, Quest frame time.
- Priority: normal
- Mode: report-only

### Bridge alarm reverb audition — queued 2026-11-16
- Integration checks: Unity compilation and 7 standalone alarm EditMode checks passed (cooldown/restart/boundaries and saved prefab clip binding). Earlier terminal/hold/drive checks passed before isolation. No Play Mode run.
- Asset: `Assets/Audio/Bridge/BridgeAlarmBeep_Reverb.wav`, original retained; processed copy now bound to a separate spatial one-shot at the bridge control.
- Processing: 35 ms end taper, 26 ms reverb predelay, damped diffuse reflections with 2.1 s nominal decay, three seconds of appended tail, final 250 ms fade; stereo 44.1 kHz/16-bit PCM. Source is 0.983 s, stereo 8-bit; conversion does not restore source detail.
- Checks passed: output 3.983 s, peak -1.34 dBFS (no sample clipping), final samples zero; synchronous import in verified Duck Rails editor with matching duration/rate/channels.
- Pending: hold continuously (beep starts at least four seconds apart); release/re-grab before four seconds (wait for remaining cooldown), resume after expiry (immediate beep), remain idle through expiry (silent), complete/finish (no new beeps). Check retained natural tails on release, effects volume and 5–80 m spatial falloff on real clients/headsets. Audio quality/listening acceptance is unverified.


### Bridge lever terminal — queued 2026-11-16
- Loop replacement: use supplied `Assets/Audio/Bridge/BridgeLower_Loop.wav`; 0.18 s pitch/volume spin-up (0.55→1 pitch), 0.22 s spin-down/fade to silence. Check smooth quick stop/resume without a playhead restart during the fade, completion/expiry tails and immediate silence on unload. Brief stop tail supersedes the earlier “no audio while paused” wording below. EditMode checks cover assigned clip and ramp boundaries/reversal; in-game listening remains queued.
- Audio follow-up: compare directly with the train throttle—same grab sound, rising-pitch travel clicks, full-forward latch and release/return clicks. The latch follows lever engagement, not bridge completion. Shared code and all four sound-event references confirmed in Edit Mode; listening remains pending. Keep the separate mechanical loop while the bridge lowers.
- Commit(s): uncommitted.
- Changed: Approved Blender wedge terminal replaces the floor button; train palm snapping/arc/detent, retained bridge hold, CutePixel percentage/bar, and spatial mechanical loop; nearby gold crate unchanged. See docs/final-escape-layout.md.
- Passed now: Unity compilation, 27 terminal + 17 bridge-hold + 27 train-drive EditMode checks; saved Expedition reference inheritance and visual preview. Evidence: output/bridge-terminal/checks.txt. No Play Mode run.
- Setup: Two real clients at finale, then third-client late join; use updated RailBridgeHoldFixture for automated holds and manual headset grip for feel. Recheck normal train throttle afterward.
- Check: 0% at fresh start; grip alone does not lower; push forward lowers, release/pullback/breakaway/downing/focus loss stops, progress remains and resumes without a jump. 100% only at full lowering. Both clients agree on display, bridge and mechanical loop; no audio while paused/completed/finished. Disable/unload stops audio.
- Check: Master handoff, disconnect/lease expiry, late join during motion/pause, two simultaneous operators, fresh-run reset, bridge crash and success all retain existing authoritative behavior; bridge interaction never drives/stops/acquires the train throttle. Only first valid hold creates the two tower defenders. Gold deposit still works alongside it.
- Needs human: Headset grip height (~45 cm), palm orientation/snapping, full throw and release comfort, small-duck reach/display readability, mechanical loop character/seam/volume/reverb, nearby gold access and night appearance.
- Priority: normal.

### Compound and starting station reverb — queued 2026-11-16
- Commit(s): uncommitted
- Changed: Six authored box zones cover Compound concourse/stairs and Starting Station hall/side rooms/tunnel; tunnel has stronger reverb and 2.8 s decay versus the hall's 1.4 s. See docs/block-reverb.md.
- Setup: Two real clients; Compound then private expedition Starting Station; drive through the departure tunnel to outdoors.
- Check: Coverage on concourse/stairs, both station platforms, footbridge and side rooms; stronger tunnel, smooth hall boundary and dry exterior beyond Z 142; separate listeners hear their own region; departure/return and late join do not retain old zones.
- Needs human: Listen on headset for decay, transitions/overlap, voice clarity, footsteps/item sounds and audio performance.
- Priority: normal


### Duckz backpack downing and persistence — queued 2026-11-16
- Commit(s): backpack downing/persistence commit containing this entry.
- Required procedure and evidence: [backpack persistence acceptance](backpack-persistence-testing.md). Includes exact slot/identity accounting, cold restart, no-save exits, account isolation, both room kinds, cleanup timing, reconnect/late join, ownership races and headset checks. All runtime scenarios remain pending.
- Changed: downing invokes inherited Duckz bag death cleanup (bags drop with contents intact, saved bag cleared, pending life claims invalidated). Restoring a worn bag from UserPlayerPrefs is enabled in Rails after world readiness; restored Compound bags use the existing staging lifetime tag. No separate item spill or player death state.
- Passed now: verified Duck Rails Edit Mode target, matching Unity 6000.3.24f1 in source/destination, inherited PUN over Realtime paths inspected; Unity compilation and existing Week 2 rules checks pass. No Play Mode launched.
- Setup: two real clients, normal loadout (DebugBagEquip None), public and private rooms; load a worn bag with mixed items and use a second held bag.
- Check: downing releases both bags once with exact contents on both peers, cancels pending pulls, clears the worn save and leaves revival working. Ground and moving train drops stay recoverable; revival never duplicates/auto-equips the old bag. Rewearing a bag saves it again.
- Check: quit/relaunch with a worn loaded bag restores its type and exact slots. Taking it off, holding it, dropping it, or downing before quitting restores nothing. Test changed contents, empty bags, multiple accounts, same-room reconnect, another player recovering the original and late join; never duplicate a surviving room copy.
- Check: public/private Compound -> Expedition -> Compound and replay preserve the worn save through cleanup and restore exactly once after readiness, including rapid transitions, non-master restores and pending ownership at downing. Restored Compound bags survive periodic cleanup and remain usable after being taken off.
- Needs human: headset backpack retrieval/drop feel.
- Priority: normal.

### Train radio press/release sounds — queued 2026-11-16
- Commit(s): uncommitted.
- Changed: train TALK activation/release reuse the handheld WalkieTalkie_TalkOn/TalkOff SoundEvents through RoomRpc; only local hold transitions broadcast, not heartbeats or remote lease updates. Forced release also plays the off cue.
- Passed now: verified Duck Rails MCP in Edit Mode; compilation passed, Console error query empty, saved RadioTable and nested DuckRailsTrain bindings match handheld on/off assets and retain the button reference.
- Setup: two clients aboard stopped and moving train, plus handheld walkie for comparison.
- Check: activation and release each sound once locally and to nearby peers; holding does not repeat; rapid taps, mute, downing and focus loss stop transmission with the release cue; horn/bridge remain unchanged.
- Needs human: headset listening and volume/position comparison with handheld.
- Priority: normal.

### Coal pickup and drop sounds — queued 2026-11-16
- Commit(s): uncommitted.
- Changed: RailCoal uses existing networked ItemSounds: Furnace_CoalGrab_01 on pickup and Furnace_CoalThud_01 on loose surface impacts (minimum normal speed 0.5 m/s, 0.1-second cooldown). Furnace deposit audio remains active.
- Passed now: verified Duck Rails editor in Edit Mode; sound imports, saved prefab bindings, single ItemSounds component and both network sound registry entries pass; scripts compile. Console contains MetaXR form-factor and LCK graphics-API errors, unrelated to these asset bindings.
- Setup: two clients; pick up coal from ground and bag, release onto ground and moving train, then deposit in furnace.
- Check: both clients hear positional pickup and impact cues once per qualifying event; held/stashed coal and gentle sliding remain silent; listen for excessive overlapping thuds during furnace insertion and bouncing.
- Needs human: headset listening and volume balance.
- Priority: normal.

### Replacement countdown tick — queued 2026-11-16
- Commit(s): uncommitted.
- Changed: overwrote `Assets/Audio/Menu/ClockTick.wav` with supplied InspectorJ Plastic Toy Clock Ticking Single; original filename, GUID, importer settings and SoundEvent retained.
- Passed now: source/destination SHA256 match; Unity import and SoundEvent clip reference check (0.973 seconds, stereo, 48 kHz).
- Setup: public compound countdown and expedition return countdown.
- Check: new tick is heard once per countdown second, without an audible overlap or clipped tail; existing cadence and Effects volume control remain intact.
- Needs human: listening and volume balance on headset.
- Priority: normal.


### Compound looping music — queued 2026-11-16
- Commit(s): uncommitted.
- Changed: supplied Dark Horizons track loops in Compound at volume 0.0175 through Effects; stereo Vorbis quality 70, Streaming on desktop and Android. See [settings](subway-compound.md#compound-music--november-16-2026).
- Passed now: Unity import and command compilation; saved clip and importer settings, scene loop/autoplay/mixer references checked.
- Setup: enter Compound, stay for at least two 206.76-second cycles, depart and return; desktop and Quest.
- Check: one continuous music source throughout the compound, clean repeat boundary, acceptable balance with voice/countdown, respects Effects volume, fades in over 1.5 seconds on entry/return and out over 0.75 seconds before departure unload; no duplicate source or Quest streaming hitches. Try departing during entry fade and with time scale zero. Direct object disable/application quit are immediate stops.
- Needs human: listening quality, repeat boundary and headset mix/performance.
- Priority: normal.


### Station and compound material contact sounds — queued 2026-11-16
- Commit(s): uncommitted.
- Changed: active starting-station and compound environment surfaces use Stone for concrete/masonry, Metal for metal and Wood for timber, with Dirt for compound ballast and existing Water retained. Mixed Blender collision meshes are partitioned by atlas tile without changing their triangles or visible models.
- Passed now: Unity compilation; 61 active environment colliders have non-default types; exact original triangle/vertex coverage and physics settings preserved; loaded Compound inheritance; final Console error/exception query empty. See [evidence](block-audio-surfaces.md#starting-station-and-compound--november-16-2026).
- Setup: visit Compound then Starting Station; one client for traversal/listening, two real clients for remote hand-contact audio.
- Check: walk and tap concrete floors/walls/stairs, timber benches/crates/office floor, metal fences/rails/fixtures and fountain water. Sounds match each material, including mixed barricades. Traverse material boundaries and stairs without new catches or gaps; confirm the pressure plate and fountain keep their existing sounds. A second client hears matching hand contacts.
- Needs human: headset listening and Gorilla Tag locomotion feel; real-client remote audio.
- Priority: normal.

### Throttle grip breakaway distance — queued 2026-11-16
- Commit(s): uncommitted.
- Changed: doubled the tracked palm's breakaway distance from the gripped contact from 18 cm to 36 cm.
- Setup: one client in an expedition with the train throttle available.
- Check: with either hand, hold grip and move the tracked palm sideways or upward past 18 cm. The palm should stay attached through 36 cm and detach beyond it. Regrip and confirm letting go still detaches immediately.
- Needs human: headset grip feel through the lever's full travel.
- Priority: normal.

### Low wall displays in both supplies rooms — queued 2026-11-16
- Commit(s): uncommitted.
- Changed: all 17 Compound and Starting Station displays arranged low against room walls without stands (mesh bottoms at 0.25 m); item tops are 0.37–0.675 m above the floor. Prices/cooldowns lowered with items; monitor lowered; all item/monitor stands removed per user correction. Helicopter layout retained.
- Passed now: Unity compilation, saved prefab/Expedition inheritance, actual mesh height and pairwise display bounds; removal of 18 stands per room with all display positions preserved, existing shared-store bindings and active Compound checks; Console error/exception query empty. See [evidence](validation/supply-room-layout/editmode.txt).
- Spacing follow-up: expedition bundles extend onto the south wall; bat/pan moved west; five north-wall supplies spaced 1.05–1.15 m apart. Saved height/overlap and shared-store checks pass. Compound unchanged by this follow-up.
- Setup: Compound, then Starting Station, with a roughly 0.5 m duck; two authenticated clients for delivery/equip visibility.
- Check: all seven coin supplies, nine bundle pulls and Default Duck remain reachable; successful pulls/restocks return to their new positions; price/countdown text is readable; entrances and hand locomotion stay clear; camera feeds work.
- Needs human: Gorilla Tag movement, natural hand reach, text readability and appearance on headset. Not verified by editor checks.
- Priority: normal.

### Starting Station raised sign lettering — queued 2026-11-16

- **Change:** all four station TMP labels replaced with 2 cm solid CutePixel meshes;
  placement/wording/signboards preserved; lighting rebaked/exported.
- **Passed now:** four saved/reloaded meshes in source/runtime prefabs, persistent
  MeshFilters, no duplicate TMP, compilation, 95 matched lighting bindings and
  native close-up/station inspection.
- **Run:** enter expedition; check all four labels remain visible and lit after
  companion load/reload and station translation.
- **Needs human:** Quest readability near signs/from platforms, appearance and performance.
- **Evidence:** [checks and images](validation/station-raised-signs/README.md).

### Skyscraper raised development sign — queued 2026-11-16
- Commit(s): uncommitted.
- Changed: solid CutePixel lettering and a bottom-mounted baked spotlight on the front development sign; isolated fascia lightmap preserves existing tower atlas lighting.
- Setup: Skyscraper Works in an expedition; inspect the approach sign at duck height.
- Check: wording and raised edges readable, uplight visible without realtime double-lighting, sign lightmap remains bound after route translation and reload.
- Public departure regression: the sign's missing additive lighting scene blocked world readiness on a route with Skyscraper. After adding the companion, one authenticated editor countdown reached Running with all three sign bindings, a cleared loading veil and input enabled. See [the fix and evidence](skyscraper-layout.md#public-departure-regression--november-16-2026).
- Needs human: headset readability and final art approval.
- Priority: normal.

### Starting Station ceiling/deck lightmap fix — queued 2026-11-16

- **Change:** separate the hidden City_Deck from the coplanar station ceiling by
  0.25 m; preserve in source prefab/builder; production rebaked and companion exported.
- **Passed now:** matched native east-platform before/after and west-platform
  inspection; 768/768 sampled rays front-facing; 91 exported renderer bindings
  match bake; no missing prefab textures or script compilation failure.
- **Run:** enter expedition, inspect both platforms, stairs and room entrances for
  returning rectangular patches; verify companion loading and translated binding
  consistency. Preserve the exterior roof separation.
- **Needs human:** Quest lighting readability and memory/performance, final visual approval.
- **Evidence:** [cause, fix and editor checks](validation/station-lightmap-fix/README.md).

### Starting Station washroom mirror and Supplies tripods — queued 2026-11-16

- Added a matching hanging bulb mesh inside Supplies on the far side of the doorway. Unity Edit Mode confirmed the ceiling mount and shared warm bulb material. The fixture uses a separate FBX and has no Light component. Check placement in Expedition after user-authored lighting is ready.
- Flipped the washroom mirror above the sinks to face into the room. Added two warm baked tripod work lights on the platform, one on either side of the Supplies entrance.
- Edit Mode checks passed for the mirror's face direction, two warm fixtures in the saved station prefab, nine clear rays through the doorway, and lighting export with two atlases and 596 probes.
- In Expedition, check the mirror from the washroom entrance and at the sinks. Approach Supplies from both sides, verify the warm pools of light and doorway clearance, and check for lightmap seams or overly bright patches.

### City starting station v3 — queued 2026-11-16
- Commit(s): uncommitted.
- Changed: StartingStation uses the new Blender-built city subway station (greybox disabled): open platforms, recessed supplies room with the shared store against its west and north walls (no partition), washroom, office, sealed rear, tunnel, city portal and fenced yard; rebaked with neutral Compound-style floodlights and a few warm accents after the user's review.
- Cheap checks: compiles, Console clean, 30 Edit Mode ray/overlap checks on the first build and 15 on the reviewed build pass. See docs/starting-station-layout.md.
- Setup: start an expedition from the Compound, single client then two clients; default block order.
- Check: train spawns at the stop and both platforms board across the 0.5 m gap; stairs and bridge walkable with no snagging on treads; rails and bridge guard stop falls; supplies doorway and shop aisle reachable, every shared-store item pulls, prices and bundle titles readable, security TV works; washroom and office enterable; rear doors, gate, retaining wall and both yard fences block; train leaves through the tunnel with no clipping (players, smoke, lanterns); lightmaps and probes load with the station (no black or unlit surfaces, ducks lit in rooms, tunnel and yard); no seam or gap at Z 160 into the first block; late joiner aboard sees the same.
- Needs human: VR scale and mood of the station, stair comfort, shop reach at duck height, yard darkness, Quest frame cost.
- Priority: normal.


### Bridge crash collapse — queued 2026-11-16
- Commit(s): uncommitted.
- Changed: five bridge sections collapse from the impact angle, settle on the river bed and burn; front-of-train large dark explosion, retained crash audio, 3.2-second results delay.
- Cheap checks: compilation and 81 EditMode assertions pass; neutral geometry still inspected. See docs/final-escape-layout.md.
- Setup: two real clients, crash with bridge fully raised, halfway down and almost down; repeat with reconnect during/after collapse and master departure. Replay a fresh run and complete a successful crossing.
- Check: front impact explosion/sound once; intact bridge disappears exactly as matching sections start falling; no initial jump or floor penetration; three persistent fire pockets; all peers agree on collapse stage, late arrival does not replay impact; results at 3.2 seconds, unchanged return countdown; passenger recovery still works; fresh bridge/collision restored and success has no destruction. Check explosion scale, smoke occlusion, fire orientation and actual frame cost on Quest.
- Needs human: VR impact/scale/pacing, fire/smoke appearance and comfort.
- Priority: normal.


### Test panel fuel 100 — queued 2026-11-16
- Commit(s): uncommitted.
- Changed: desktop and VR test panel fuel buttons now set shared fuel to 100 instead of 20, with matching labels.
- Setup: ready private Expedition as master; open test tools on desktop and in VR.
- Check: Fuel 100 fills fuel to 100; existing authority restrictions remain enforced.
- Needs human: VR label readability.
- Priority: low.

### Baked fixture double lighting — queued 2026-11-16
- Station follow-up: refreshed StartingStation prefab membership from 62 to 63 to include already-baked `Supplies corner bulb (3)`. Exact valid-reference coverage, inherited Expedition/bake-scene membership and 95 renderer UV bindings pass Edit Mode inspection. Check Supplies room has no extra direct pool while retaining the baked bulb illumination; overall station softness still needs visual acceptance.
- Commit(s): uncommitted.
- Changed: companion-lit blocks suppress baked-only fixture Lights during gameplay, retaining native lightmaps/probes and realtime lights; restore original enabled states on disable. User wants the softer finale bake-scene lighting.
- Cheap checks: Unity compilation and actual-finale Edit Mode lifecycle checks pass; 1,746 texture/UV bindings match the bake and Expedition export. See docs/block-lighting.md.
- Setup: one-client Expedition with finale and loaded companions; compare finale approach/platforms with bridge-finale-v1-bake.unity. Then unload/reload route and replay; sample station, helipad and a scavenging block.
- Check: owners ready with no failures; all 24 finale baked fixtures disabled, expected maps bound; no hard direct fixture cones/double lighting; soft baked pools/shadows retained. Flashlights and helipad realtime lights still work. Replay preserves lighting and teardown leaves no companion leaks. Account for gameplay fog/ambient differences instead of requiring identical overall exposure.
- Needs human: headset appearance and confirmation of preferred softness.
- Priority: normal.

### Compound pressure plate and countdown display — queued 2026-11-16
- Commit(s): uncommitted.
- Changed: plate moves from local Y 0.05 to 0 when the shared countdown starts. The countdown sign is enabled again.
- Setup: public Compound, two real clients; enter, add second player, remove one, remove last, restart; also complete countdown and inspect private room.
- Check: public sign shows the idle prompt, roster, and decreasing seconds, and returns to the prompt on cancellation. Plate reaches Y 0 on start and Y 0.05 on stop. One On per countdown start, no extra cue when someone joins/leaves while another remains, and one Off on stop. Collision stays fixed and metal contact audio works. Private room shows its code and the plate stays at rest.
- Needs human: countdown readability, audible balance and headset contact feel.
- Priority: normal.


### Compound fountain water sounds — queued 2026-11-16
- Commit(s): uncommitted.
- Changed: dedicated Water AudioSurface collider 1 cm below the fountain water; removed the visual water plane from solid mesh collision only.
- Cheap checks: 11 Edit Mode basin/rim/pedestal/outside rays passed; footsteps and hand hits resolve HandHit_Water; Console error query empty. See docs/subway-compound.md.
- Setup: Compound; move through basin and touch water, rim and pedestal.
- Check: basin contacts play water SFX; rim, pedestal and surrounding floor keep their usual sounds; no snagging at basin edges.
- Needs human: audible SFX and headset contact feel; review the November 16 blue fountain apron border under gameplay lighting.
- Priority: normal.


### Compound pressure plate dark-metal finish — queued 2026-11-15
- Commit(s): uncommitted.
- Changed: moving slab top now uses dark grey Industrial metal; yellow/black hazard border retained. Source Blender model and generator updated.
- Cheap checks: Unity import/command compilation, zero yellow slab UVs, retained collider, empty Console errors, actual Edit Mode appearance capture; see docs/subway-compound.md.
- Setup: Compound, stand on/off the departure pressure plate.
- Check: dark interior and yellow striped border remain readable while the slab lowers/returns.
- Needs human: headset appearance and preference.
- Priority: low.


### Saved fog settings and atmosphere tuner — queued 2026-11-15
- Commit(s): uncommitted.
- Changed: shared saved night/pre-dawn/dawn fog endpoints and an Edit Mode Scene View preview. Existing defaults and transition timing retained; see docs/block-lighting.md.
- Cheap checks: Unity compilation; 14 Edit Mode checks for saved resource, endpoints, restoration, unchanged scene dirty state, custom interpolation and timing.
- Setup: private development expedition; Gameplay Lighting; Test success - bridge and dawn ride. Use saved tuner values, then replay; two real clients for matching visuals.
- Check: game uses saved fog colours/densities, transitions through the tunnel at 12–20 seconds, and returns to night on replay. Closing the tuner/entering Play leaves no preview override.
- Needs human: headset horizon continuity, visibility and visual preference.
- Priority: normal.

### Quackbear lip pivot normalization — queued 2026-11-15
- Commit(s): uncommitted.
- Changed: `Lips_Quackbear.fbx` Lip_B and Lip_T now exactly match Default local position/rotation. Removed compensating root/lip rotations and baked pivot offsets into mesh vertices, preserving resting geometry and existing asset IDs.
- Cheap checks: verified Duck Rails editor; FBX reimport passed; exact imported position/quaternion equality with Default for both lips; all 247 imported vertices retained with maximum world-position difference 0.000000067 m.
- Setup: equip Quackbear; speak and stop speaking while viewing mirror/LIV, then switch to Default and back.
- Check: both lips animate around the intended pivots without rotated motion, separation or a resting-position jump.
- Needs human: microphone and visual appearance.
- Priority: normal.

### LIV local lips follow equipped skin — queued 2026-11-15
- Commit(s): uncommitted.
- Changed: local LIV/mirror face posing uses DuckSkinVisuals' selected face instead of cached name lookups that can target hidden default lips. See [evidence](liv-recording-audio.md#local-capture-lips--november-15-2026).
- Cheap checks: Unity compile; actual DuckPlayer with Quackbear, Huggy and Scrap Duck selected lip references active; Console error/exception query empty.
- Setup: headset, live microphone, LIV selfie preview and recording; default duck and each bundle skin. Switch skins with the preview open, then transfer compound/expedition and repeat. Check shared-store mirror too.
- Check: visible lips open while speaking and settle during silence; switching skins and switching back to default targets the current visible face; first-person head remains hidden. Confirm remote speech animation on a second client.
- Needs human: actual microphone, preview/recording and headset visual acceptance.
- Priority: normal.

### FX_Heal_02 revive completion — queued 2026-11-15
- Commit(s): uncommitted.
- Changed: Revived resource prefab now uses FX_Heal_02's three layers, with looping/play-on-awake disabled and local simulation. Existing trigger, audio and position retained.
- Cheap checks: verified Duck Rails editor; save/reload confirms three layers, root ParticleSystem, manual one-shot playback, local simulation, supported primary materials and unchanged GUID. No Play Mode run.
- Setup: two clients; revive on ground and aboard the moving train, then repeat a revive.
- Check: healing effect plays once only on completion, follows the revived duck, finishes cleanly, and restarts on the next rescue; no particles during revive progress or historical replay on late join.
- Needs human: headset appearance, scale/placement and nighttime material rendering.
- Priority: normal.

### Farm Silos screenshot repairs and pond collider — queued 2026-11-15

- Scope: slide side/deck collider alignment, five ramp/landing joins, north path
  overlap removal, mounted/lowered lanterns, two removed lights with fresh bake,
  outdoor tractor removal, and pond-shaped solid collider below water. Rebuilt
  offline navigation and Blender atlas without triangle growth. Narrow ditch also
  has a solid box with Water AudioSurface; verify its water footsteps.
- Checks now: 69 collision/reference checks, all 244 spawn poses/order preserved,
  per-triangle Blender comparison, eight Unity visual pairs, production guard and
  compile/Console checks. [Evidence](validation/farm-silos-polish-2026-11-15/README.md).
- Run: traverse/climb both slide edges and every ramp landing; inspect path/ramp
  surfaces while moving, fixture darkness/mounts, and pond walking/item support.
  Check pond water footsteps after the user assigns Water AudioSurface. Exercise
  enemy pursuit and real-client/late-join agreement on the revised block.
- Manual: headset hand contact, darkness and moving-view texture readability.

### Arcade open-window collision — queued 2026-11-15
- Commit(s): uncommitted.
- Changed: removed only the two invisible BoxColliders spanning the arcade front windows in `convenience-store-v1.prefab`; visuals and all frame/sill/mullion colliders retained.
- Cheap checks: Unity reimport and EditMode inspection pass; both window objects have no collider, all 11 arcade window-frame colliders and the two other shop-window colliders remain. Prefab diff contains only the two component removals; Console error query empty.
- Setup: one client at the Convenience Store arcade.
- Check: hands/items pass through the pictured window openings; solid sills and frames still block contact. Existing prebaked navigation is unchanged by this collider-only request.
- Needs human: headset contact/traversal feel.
- Priority: normal.

### Speed-matched train wheels — queued 2026-11-15
- Commit(s): uncommitted.
- Changed: four visible wheels rotate about local X from shared train distance / 0.43 m radius, following actual acceleration, braking and fuel-exhaustion coasting. Suspension mounts and existing collision stay fixed relative to the train.
- Cheap checks: Unity compilation and EditMode prefab checks pass: four bindings, all 704 source triangles and positions retained, four stationary mounts, unchanged collision mesh, quarter/half/full-turn, reverse and long-distance angles, stable stopped phase. Console error query empty. No Play Mode run.
- Setup: one client driving at normal and 5x test speed, then two clients including a moving late joiner.
- Check: wheels roll forward without orbiting their axles, speed follows acceleration/braking/coasting, freeze at rest, agree across clients and reconstruct on late join/replay; mounts remain fixed. Review faceted wheels at speed.
- Needs human: headset appearance.
- Priority: normal.

### Quackville blocked-exit sign — queued 2026-11-15
- Installed `↑ QUACKVILLE` above the blocked Compound stairway with one left-side up arrow, raised CutePixel lettering and matching platform sign materials.
- Editor compilation, assets/instance checks and visual capture pass; sign lowered on short supports to clear ceiling-pipe sightlines. See `docs/subway-compound.md` and `output/subway-terminal-model/quackville-exit-sign.png`.
- Pending: headset readability and sightlines from normal approach and stairs. No Play Mode run; not verified live.

### Integrated radio console — queued 2026-11-15
- Commit(s): uncommitted.
- Changed: approved 1.72 m wall-mounted console installed in shared RadioTable prefab; original voice/hold/button identities retained, aligned trigger and six new solids replace old table collision. See [integration and checks](radio-console.md#unity-installation--november-15).
- Cheap checks: compilation, 24 saved-prefab checks, installed press/release/disable and nine hold checks pass; offline train graph rebaked, boarding regressions pass, Unity render reviewed, final Console error query empty.
- Setup: one client for physical press/release; two clients with station and handheld walkie for voice, late join and moving train.
- Button correction: collar/cap now share the plate slope; verify full microphone icon and even seating at rest and while held. Blender normal/clearance checks and Unity saved-prefab checks pass; no live test run.
- Check: fits between wall posts without overlaps, cable exits rear faces, hold/release stops/starts transmission, cap moves with contact, no extra table legs or duplicate old geometry, late joining retains station operation.
- Needs human: headset reach, CutePixel label/icon readability, nighttime appearance and voice quality.
- Priority: normal.

### Subway terminal raised sign lettering — queued 2026-11-15
- Commit(s): uncommitted.
- Changed: Platforms, Station Info and Supplies now use solid CutePixel mesh letters, 22 mm deep; Platforms includes two up arrows. Only the new SubwayTerminal prefab's Sign labels group changed; original TMP labels are inactive for authoring reference.
- Cheap checks: verified Duck Rails editor, compiled installation/check commands, saved three mesh references and depths, preserved all non-label transforms, visually inspected forward/oblique editor captures; Console error query empty.
- Setup: one client in Compound.
- Check: all three signs readable, arrows face up, letter faces/edges render without clipping or duplicate text at approach and side angles.
- Needs human: headset readability and cream material appearance under station lighting.
- Priority: low.

### Farm stalk-only foliage sounds — queued 2026-11-15
- Commit(s): uncommitted.
- Changed November 16: supersedes contact override. Dirt ground contacts remain separate from local movement rustling inside 52 corn patches / 3,669 stalks. Repeats every 0.32 s, random pitch 0.95–1.05 and volume 0.4–0.5; no new collision.
- Setup: Farm expedition, keyboard and headset movement; then two clients to check each local listener independently. Remote-player rustle replication is not implemented.
- Check: move through stalks without hitting the ground/hands to hear repeats; dirt contacts still play. Stop, leave corn, enter paths/clearings, teleport, pause or unload the block: rustle stops. Tiny tracking jitter and vertical bobbing alone stay silent. Resume and cross patch boundaries without stacked voices. Check transformed blocks, route reload, pond/water priority and above-canopy silence.
- Evidence: compilation, 3,809 EditMode assertions (cadence at 72/90 Hz, movement gates, saved coverage/references, dirt contacts, randomized selection) and empty Console error query pass; see block-audio-surfaces.md. Runtime/listening remains unrun.
- Needs human: headset listening, cadence/boundary feel and volume balance with dirt contacts.
- Priority: normal.


### Platform and return clock ticks — queued 2026-11-15
- Commit(s): uncommitted.
- Changed: copied the supplied ClockTick.wav from Duckz (source unchanged); RailAudioBank SoundEvent plays through AudioManager once per positive public platform countdown second, and only at 5–1 on expedition returns. Shared room time drives each client's local sound; no catch-up bursts, zero tick or private staging ticks. Event uses fixed pitch, 2D playback and the Effects volume controls.
- Cheap checks: verified Duck Rails MCP target; Unity compilation completed, imported clip/event/bank binding checks passed in Edit Mode (clip duration 0.625 s), Console error query empty. No Play Mode run.
- Setup: public Compound with two clients; cancel/restart boarding, complete departure, then success/failure returns; repeat with private expedition.
- Check: one tick for each displayed positive platform second; cancel stops sound and restart resumes. Return stays silent above 5 then ticks at 5, 4, 3, 2, 1 across Ending/Returning without duplication. No tick at zero or after transfer; subsequent runs work; late join hears only current/future ticks. Game volume controls ticks.
- Needs human: headset loudness and timing against the displayed clocks.
- Priority: normal.

### Pause menu follows the train — queued 2026-11-15
- Commit(s): uncommitted.
- Cheap checks: Duck Rails Unity target confirmed in Edit Mode; compilation and `PauseMenuAudioChecks.Run()` passed (audio bindings, physical drop, close freeze and reopen pose). Scoped diff whitespace check passed. Console had one unrelated Package Manager cancellation error. Train-follow runtime/headset behavior remains untested; the attempted ad-hoc reflection probe was rejected by the MCP namespace restriction and is not validation evidence.
- Changed: summoning aboard captures the passenger train frame; the floating menu follows its position/rotation before distance dismissal. Every summon resets the pose near the current head. Grabbing clears the frame and retains physical grab/drop behavior; closing clears it too. No train parenting, so unloading cannot destroy the menu.
- Setup: one shared DuckPlayer in Keyboard mode, then target headset, aboard a moving expedition train.
- Check: open while stopped then accelerate, and open while already moving; menu stays in its train-relative position through slopes and braking without auto-dismissing. Look/walk around without dragging it. Close/reopen from another position and confirm it appears in front of you. Walk off and away to dismiss, then reopen ashore and confirm it stays world-fixed. Grab/drop/reopen; return to Compound with menu open and confirm it can still be summoned.
- Needs human: headset stability, reach and grab/drop feel.
- Priority: normal.

### Subway terminal model swapped into Compound — queued 2026-11-15
- Commit(s): uncommitted.
- Changed: Compound now shows the Blender-modelled `SubwayTerminal` prefab (mesh colliders plus four invisible guards) instead of the SubwayV2 blockout, which is disabled, not deleted. Later the same day: gate lower sheets given concourse-side faces, and a two-sided alpha-clipped wire mesh (`Mesh_Fence`, no collider) added above the barricades on both track sides and the Platforms end; rebaked. Then the Supplies south wall and all shelving were removed (stall open and empty); rebaked again. Then the fountain was rebuilt as a rotated centre piece on a dark apron, and a pressure plate (`RailPressurePlate`, visual only, sinks 3 cm while the boarding trigger is occupied) was added at the gate; the boarding trigger was shrunk from 6 x 3.6 m to the 3.6 x 2.6 m plate. Boarding trigger, countdown, launch button, private-room hint, rating and Discord stations were re-placed; new baked lights; rebaked. See [subway compound](subway-compound.md).
- Cheap checks: Edit Mode only. Floor raycasts at spawn, player start, boarding area, both stalls and stair landing hit; track, gate and stair guards block; spawn-to-gate line clear; 8/8 meshes lightmapped, 347 probes; after the mesh addition, rays at 4-5 m height toward both tracks and past the gate still hit the guards; all labels CutePixel SDF; Console error query empty. `Validate Subway Blockout` was not run and is expected to fail, since it checks SubwayV2.
- Setup: one desktop client in Keyboard mode from Login into the public Compound; then two real clients.
- Check: spawn on the floor clear of pillars and the stairs for all spawn slots; walk around the fountain, into Station Info and through both Supplies openings and its now-open stair side; cannot pass or climb the side barricades, the Platforms gate line or the stair barricade; boarding trigger starts the countdown only when standing on the pressure plate (not beside it), the plate sinks for local and remote ducks and rises when empty, the 5 cm plate lip and 1 cm fountain apron do not snag walking, up to eight ducks fit on the plate, the countdown cancels on leaving, and the full countdown transfers to a ready expedition; private owner can press Launch Expedition on the gate; RATE GAME and JOIN DISCORD buttons are reachable and respond. `RailBoardingLiveChecks` should still derive its positions from the moved trigger.
- Needs human: headset climbing and hand contact on the one-sided fence panels and thin props, button reach at duck height, text readability on the gate, lighting mood, whether the wire mesh shimmers or reads too dense in the headset, the gate sheets visible from the concourse, and Quest frame time with twelve players.
- Priority: blocks other Compound work.

### Train model-aligned collision — queued 2026-11-15
- Commit(s): see git log "Align train collision with reviewed model".
- Changed: disable obsolete/overlapping shell boxes and hulls; use three non-convex model-derived shell meshes (1,947 triangles) plus matching dash housing collision. Preserve furnace wall meshes, radio table, triggers and passenger/network code. Rebuilt train navigation.
- Cheap checks: compilation, 50 focused collision/clearance checks, prefab/scene preservation and 489 boarding graph checks pass. [Evidence and historical-validator limits](validation/train-mesh-collision/README.md).
- Setup: normal Expedition train; one client for contact/deposit checks, then two real clients including late join and master handoff. Run existing isolated minion/spider rear/side/surface-carry loops only when the queue is requested.
- Check: hands/body meet deck, rails, canopy supports, nose, roof and dash at visible surfaces with no invisible blocks or pass-through. Rear and both side gaps stay open; coal enters furnace and remains exactly-once. Dropped/thrown coal, gold and tools collide and ride through GO/STOP/turns/slopes; natural passenger movement/support and deliberate exits remain correct. Minions/spiders board, pursue and dismount through all authorized gaps and retain wall support. Peers agree after late join/master loss. Compare Quest physics/frame time with previous collision; do not infer a speedup from fewer colliders.
- Needs human: natural VR hand/body feel, edge contact, thin-surface snagging, moving-train feel and Quest performance.
- Priority: normal.

### LIV recording game audio and remote voice — queued 2026-11-15
- Commit: see git log "Fix LIV game audio capture after listener replacement".
- Changed: embedded LCK mixer discards destroyed listener capture references, rebinds after scene/listener replacement, and resumes active capture; microphone reading survives temporary listener gaps. See [cause and scope](liv-recording-audio.md).
- Cheap checks: verified Duck Rails Unity target, compilation successful, all nine listener-lifecycle EditMode assertions passed, Console error/exception query empty, scoped diff whitespace checks passed. No Play Mode or native recording started.
- Setup: Quest build (user's reported device); two real clients. Start from Login, record in Compound, depart into Expedition, return, and repeat a recording. Also cross a transition while recording if supported by the camera lifecycle.
- Check: clips contain local microphone, audible nearby remote speech, and game effects (horn/footsteps); no silent game track after transitions or restarting capture. Game-audio/microphone toggles remain independent, and headset playback remains normal.
- Needs human: listen to saved clips on the user's recording device, confirm remote voice/game levels and audio/video sync.
- Priority: normal.

### Midpoint helipad side fences — queued 2026-11-14
- November 15 size follow-up (see git log "Vary helipad rocks at two to four times their original size"): all 14 rocks use independent authored random 2–4x scale factors (actual 2.19–3.65x), with grounded bases. Three rocks shifted 0.5–0.75 m to preserve loot clearance; all markers and counts retained. Navigation/lighting rebuilt (8,233 nodes / 216 bindings); saved scale/grounding/clearance and mesh checks pass. Include enlarged-rock traversal, loot reach and coal-versus-scenery readability. See [evidence](validation/helipad-rocks/README.md#random-enlargement--november-15-2026).
- Rock follow-up: user approved fence appearance; replaced all 14 oval rocks with three jagged, flat-shaded meshes and matching convex collision. 10,752 -> 328 triangles; all transforms, shop/loot and fence geometry retained. Navigation/lighting rebuilt (7,976 nodes / 216 bindings), mesh/collision/reference and preservation checks pass. [Rock evidence](validation/helipad-rocks/README.md). Include rock hand/body/loose-item contact, ground seating and nighttime/headset silhouette in this entry's live checks.
- Commit(s): see git log "Add helipad side fences and jagged low-poly rocks".
- Changed: two 2 m timber side fences at X +/-47 (1 m centerline inset), full Z0..60; replace the old partial west fence. Open railway ends and existing rear decorative fence retained. Collision/audio, offline navigation and baked lighting updated.
- Cheap checks: saved-prefab checks pass for edge/seam bounds, UVs, 432 triangles/two renderers, continuous ground-level collision, timber audio, 42 retained loot candidates clear of fences, helicopter clearance and 216 valid lightmap bindings. Navigation rebuilt to 8,023 nodes; error Console empty. [Evidence](validation/helipad-fences/README.md).
- Setup: requested queue run, normal expedition midpoint or Test Block Order; one client for traversal/lighting, two clients including late join for agreement.
- Check: both fences render and block passage without gaps below ground; railway entry/exit and helicopter/shop access remain clear; no missing lighting at translated route placement or block transitions; loot stays reachable.
- Needs human: natural hand/body contact, night readability and edge/seam appearance in headset.
- Priority: normal.

### Farm Silos production atlas integration — queued 2026-11-14
- Commit(s): see git log "Integrate approved Farm Silos atlas"; user-approved appearance integrated into original-GUID production prefab.
- Changed: 899 compatible baked renderers / 27 materials -> 117 chunks / one shared atlas material. Retain 52 corn patches, pond water and horsetails: total 953 -> 171 renderers. Converted triangles 59,234 -> 104,040; whole block 149,978 -> 194,784. Constant HDR emission retained; no textured-emission atlas or animated brushes in Farm.
- Cheap checks: all 2,361 transforms, 4,503 serialized components, 1,434 colliders, gameplay/navigation/spawn settings, 734 dependency hashes and 899 original baked bindings preserved. 117 exact imported meshes and all 1,016 scene lightmap bindings pass, including scene reopen. 554 probes, 41 original prefab lights, six reverb zones and all 54 exceptions retained. Twelve matched visual pairs inspected; compilation and final error Console checks pass. [Evidence and scene-only lighting adaptation](validation/farm-silos-atlas/README.md).
- Integration checks: all 8,738 original object IDs and production metadata preserved; production matches approved review with 171 renderers, 1,434 colliders and 1,016 bindings; 733 existing dependency files unchanged. Build fingerprint/lighting workflow guards pass. [Integration/recovery](validation/farm-silos-atlas/production-integration.md).
- Setup: use normal production Farm Silos via Test Block Order on a requested queue run. One client for lighting/readiness/visibility; two real clients plus late join for agreement. Include nonzero route placement, companion unload/rebind and return/replay. Production assets are in Assets/DuckRails/FarmSilosAtlas; the approved review remains available.
- Check: no missing/duplicate scenery or stale source re-enable; correct lighting, normals, shadows, emission and minification. Preserve silo grain/ceiling/ramp, farmhouse floors/stairs/cages, hay/roof routes, corn, pond/playground, collision, water audio, 105 coal / 117 gold / 22 enemy candidates and per-run rules, navigation and reverb. The review's editor-only lightmap restoration is not evidence for runtime loading.
- Needs human: Unity visuals approved. Moving-view/headset appearance, natural stairs/silo/roof/cage/playground traversal and loot reach/recovery. Existing seven roof/hay loot failures and Farm gameplay acceptance remain separately recorded in farm-silos-layout.md.
- Priority: normal. No repeated pilot, routine Quest comparison or comparison APK unless explicitly requested; renderer reduction does not establish Farm device performance.


### Container Yard production atlas integration — queued 2026-11-14
- Commit(s): see git log "Integrate approved Container Yard atlas"; user-approved visuals integrated into the original-GUID production prefab.
- Changed: 1,191 compatible scenery renderers / 17 materials -> 246 chunks / one atlas material, plus three retained CutePixel labels. Total 1,194 -> 249 renderers; triangles 108,336 -> 202,488. Cells 12 x 6 x 12 m; 11 oversized sources separate. No textured emission or animated brushes exist in this block.
- Cheap checks: all 2,360 original transforms, 4,110 serialized components, 663 colliders, navigation/spawn settings, 354 original dependency hashes and 1,191 original baked bindings preserved. Exact mesh data and 1,437 review-scene lightmap bindings pass; ten paired visual samples inspected; compilation succeeds and final error Console empty. [Evidence](validation/container-yard-atlas/README.md).
- Integration checks: all 8,147 original local object IDs retained; production metadata byte-identical; all 663 colliders and 1,437 lighting bindings match the approved review. 353 other original dependency files unchanged. Build/source fingerprint and lighting-workflow guards pass. [Evidence and recovery](validation/container-yard-atlas/production-integration.md).
- Setup: use normal production Container Yard via Test Block Order on a requested queue run. One client for loading/readiness/visibility, two real clients plus late join for agreement; include nonzero route placement and return/replay. Production atlas assets are in `Assets/DuckRails/ContainerYardAtlas`; the approved review remains available.
- Check: original lighting/readiness at origin and translated placement, companion unload/rebind, hide/show/re-enable and no missing/duplicate scenery. Preserve container interiors/roof routes/gantry, north shed/trailer/dock/tanks, ground repeats, collision, 168 coal / 142 gold / 32 enemy candidates and per-run selection, navigation, reverb and CutePixel text. Inspect normals/shadows, seams and distant minification.
- Needs human: full Unity appearance approved. Moving-view/headset darkness/minification, natural climbing/container/trailer/dock traversal and item reach/recovery remain queued. Existing block gameplay acceptance remains open.
- Priority: normal. No repeated wing pilot, routine Quest comparison or comparison APK unless explicitly requested. Renderer reduction does not establish Container Yard device performance.

### Skyscraper reduced geometry and low-poly production atlas — queued 2026-11-14
- Commit(s): see git log "Optimize Skyscraper geometry and preserve atlas triangle counts".
- Atlas follow-up: user approved this source; new isolated low-poly Blender review preserves 37,184 triangles and reduces enabled renderers 2,789 -> 649. 619 atlas chunks plus 29 repeat exceptions; simple cubes stay at 12 triangles. 3,011 colliders, all original transforms/components, spawns/navigation, lightmaps/probes and 304 dependency hashes preserved. Mesh/corner checks, eight matched visual pairs, compile/Console pass. [Evidence](validation/skyscraper-low-poly-atlas/README.md). User approved the conversion; it is integrated into the original-GUID production prefab. All 19,064 original IDs retained; approved-review comparison and 303 other dependency hashes pass. [Integration](validation/skyscraper-low-poly-atlas/production-integration.md); runtime acceptance remains queued.
- Material follow-up: all 50 pallets now have grounded one-to-four-layer stacks with varied rotations and distinct floor arrangements; 50 loose pipe pieces grounded. Southeast truck/formwork and eight shelter stocks corrected/varied; northeast concrete pipe pair removed. 52 markers follow support; counts unchanged. Current totals supersede the first revision below: **37,184 triangles / 2,789 renderers / 2,788 lighting bindings**. Navigation/lighting rebuilt; 711 dressing checks pass, 8,702 broader assertions pass with only the same 18 baseline loot-overlap failures. [Evidence](validation/skyscraper-materials/README.md).
- Additional check: inspect low side views for pallet/stack/floor contact, varied floor layouts, cleared northeast pipe location, loot on relocated stacks, stair/bridge access and natural item/hand contact. Source appearance approved; inspect the new conversion for matching texture repeats, close seams, moving-view minification and culling.
- Changed: retired production atlas; restored source geometry, removed NW +16 m and SE +12/+16 m playable floors, retained tall framing and five crossings. Simplified upper detail and buried slab sides; 37,784 triangles / 2,813 unchunked mesh renderers. Rooftop stands/reverb follow new top floors; navigation/lighting rebuilt. Pools now 202 coal / 192 gold / 23 enemies; per-run rules unchanged.
- Cheap checks: 8,726/8,744 assertions pass, including connected ground routes across all 17 remaining floors, absent navigation at removed-floor samples, staircase/crossing/train clearance, footprint support, all 800 crew/seed selection cases, UVs and 2,812 baked bindings. The only 18 failures reproduce the documented, unchanged B/C/D ground-floor loot overlaps. All 12 roof-tripod feet have independently checked support. [Evidence and visual review](validation/skyscraper-reduction/README.md).
- Setup: source and conversion appearance approved; accepted chunks are integrated into production. Review remains at `Assets/DuckRails/SkyscraperLowPolyAtlasReview/Skyscraper-LowPoly-Atlas-Review.unity`. On a requested runtime queue run use normal production Skyscraper via Test Block Order; one client for routes/visibility/lighting, two clients plus late join for agreement. Include translated placement and return/replay. The scene-only baked-light preview correction is not runtime-loading evidence.
- Check: no floating loot/enemies or stairs to removed floors; 17 remaining floors and all bridges/crane traversable, skyline preserved, supported floodlights, no missing slab undersides, valid lighting/probes after load/hide/show. Inspect the 18 baseline ground-loot overlap cases for pop/settling. Confirm current candidate pools/per-run counts and peer agreement.
- Needs human: appearance approved; natural VR stair/shaft/crane movement, railing/contact feel, loose-item reach/recovery, darkness and headset performance. Reduced triangles/renderers are not GPU acceptance.
- Priority: normal. Queued for playtest; no Play Mode or device run in this revision.

### Convenience Store production atlas integration — queued 2026-11-14
- Commit(s): see git log "Integrate approved Convenience Store atlas"; user-approved visuals integrated into the original-GUID production prefab.
- Changed: 1,139 compatible static renderers -> 285 chunks / one shared atlas material. Retain 231 exceptions including textured HDR neon, seven animated brush hierarchies, transparent cooler panes, CutePixel labels and incomplete-UV rooftop models. Total enabled MeshRenderers 1,370 -> 516; whole-block MeshFilter triangles 55,702 -> 127,134.
- Review-stage cheap checks: saved preservation of 3,208 transforms / 5,852 components / 1,420 colliders, exact imported mesh arrays and all 1,557 scene lightmap bindings, 1,378 original dependency hashes, compilation/empty final error Console and seven source/atlas visual pairs pass. [Evidence](validation/convenience-store-atlas/README.md). No Play Mode/APK/device tests.
- Integration checks: all 11,745 original local object IDs preserved; production metadata byte-identical; 1,420 colliders and 1,557 lighting bindings match the approved review; 1,377 pre-existing dependency files unchanged. Build fingerprint/bake-workflow guards pass. [Evidence and recovery](validation/convenience-store-atlas/production-integration.md).
- Setup: use the normal production Convenience Store via Test Block Order on a requested queue run. One client for loading/readiness/visibility, two real clients plus late join for agreement; include a nonzero route placement and return/replay. Production atlas assets are in `Assets/DuckRails/ConvenienceStoreAtlas`; the approved review remains available.
- Check: correct lighting/readiness at origin and translated placement, companion unload/rebind, block hide/show/re-enable, no duplicate/missing scenery; retained brush animation with stationary full-size hulls; original collider contact, loot/spawns/navigation, sparse shelves/empty coolers/stockroom/trailer and CutePixel text remain intact. Inspect close/interior/distant seams, atlas minification, normal/shadow/reflection behavior and neon HDR colors.
- Needs human: full-block Unity appearance approved. Moving-view/headset minification/interior darkness and natural traversal/brush collision remain queued; existing block gameplay acceptance is not closed by authoring checks.
- Priority: normal. No repeated wing pilot or routine per-block Quest comparison; performance captures/builds only if requested.


Atlas rollout policy — November 13: user accepts Motel's approach and authorizes
the remaining blocks, Convenience Store next. Follow
[the workflow](block-atlas-optimization.md). Do not repeat full Quest A/Bs for each
block by default; keep authoring preservation checks/visual review and queue live
acceptance. Historical benchmark proposals below do not override this direction.
No new block conversion or device run occurred during this documentation task.

### Full Motel Blender atlas review — queued 2026-11-13
- Scoped Quest evidence 2026-11-13: accepted same-build 120 m pair, all four views 100% tracked, applied full-map variants confirmed. Draw calls fall 24–70%; CPU medians improve 0.1–0.6 ms and maximum view CPU p95 improves 12.5 -> 11.8 ms. GPU medians improve in three views; forward GPU p95 rises 10.0 -> 10.4 ms. All view p95 CPU/GPU remain under 13.89 ms; sampled native FPS remains capped around 72. Pose difference 0.176 m / 0.23 degrees; both variants resident, no release-memory claim. [Results/limits](validation/motel-full-atlas/results.md). Initial cached wing-fixture build was rejected before measurement; clean-build fix and substitution assertion passed. User's visual fixes intentionally deferred; broader runtime/headset/multiplayer acceptance remains queued.
- November 13 follow-up: user requested Quest performance testing before addressing remaining visual issues. Dedicated same-build fixture now selects exact production consolidation (767 enabled mesh renderers) or full-map atlas (315), with 10 common renderers and unchanged collision/gameplay. Both alternatives remain resident. Capture results pending below; this does not approve visual issues or production rollout.
- Commit(s): uncommitted. User authorized extending the approved wing approach across the rest of Motel; no repeat of the wing Quest capture requested.
- Changed: isolated full-map prefab/scene under `Assets/DuckRails/MotelAtlasReview`; 1,964 source renderers become 305 spatial chunks sharing one atlas material, with 10 originals retained (315 enabled mesh renderers total). Production Motel/catalogue unchanged. Open **Duck Rails > Authoring > Motel > Open full-map Blender review**.
- Cheap checks: importer compiles; saved assets retain 2,688 original transforms, 8,003 serialized components except renderer-enabled changes, all 1,900 colliders and 1,964 original lightmap bindings. 2,269 total bindings are valid. Surface-area preservation passed during conversion. See [import evidence](validation/motel-full-atlas/import-checks.txt) and [source/workflow](../ArtSource/MotelAtlas/README.md).
- Tradeoff: converted geometry rises from 83,344 to 168,243 triangles to preserve repeating UVs. Atlas emission/surface mipmaps disabled after correcting visible cross-tile glow; base-color mipmaps retained. No full-map performance benefit claimed.
- Setup when authorized: review appearance first, then isolated full-map route fixture against current production consolidation at 120 m with matched settings/poses. The existing wing comparison builder does not select this prefab. Exercise translated placement, companion unload/rebinding, hide/show/re-enable, interiors and natural traversal.
- Check: no missing/duplicate faces, color/emission/lightmap drift, atlas seams/bleeding or culling problems; unchanged collision/spawns/interactions; compare CPU/GPU median/p95, draws, triangles, memory and load cost. Keep production rollout pending until evidence supports it.
- Needs human: full-map Unity review, headset minification/interior appearance and traversal. Scoped device performance completed above; remaining acceptance queued for playtest.
- Priority: normal.

### Motel east-wing Blender atlas pilot — queued 2026-11-13, scoped Quest comparison recorded
- User approved Blender and Unity appearance, then authorized the Quest comparison. Same-build 120 m pair has all four Motel views at 100% tracking. Wing-facing yaw-90: draws 411 -> 309; median CPU/GPU 10.15/8.0 -> 9.9/7.2 ms. All per-view CPU/GPU p95 under 13.89 ms; forward CPU p95 increases 11.9 -> 12.7 ms. This is directional evidence, not a uniform speedup: stopped train/head positions differ 0.556 m, angles within 0.03 degrees. User accepted this positional limitation and requested no repeat. Third run cancelled at login with zero valid frames, excluded. See `docs/validation/motel-wing-comparison/results.md`.
- User approved Blender appearance and authorized separate Unity review import. Open **Duck Rails > Authoring > Motel > Open Blender wing review**, or `Assets/DuckRails/MotelWingReview/Motel-Atlas-Review.unity`.
- Changed: separate full-block review prefab/scene, 29 single-material wing chunks, retained collision/gameplay and outside-wing render portions, original two baked atlas bindings. Production Motel unchanged. See `ArtSource/MotelWingPilot/README.md` for workflow and limits.
- Comparison fixture: separate `MotelWingBenchmark` prefab has 590 shared outside chunks and selectable 130 original-material/29 atlas wing chunks; only the explicit development comparison build injects it. Production assets unchanged. Both variants resident, so shipping memory/load savings remain unmeasured.
- Proposed later checks: source/atlas color and emission equivalence, seams/mipmaps, normals, lightmap UV2/rebinding, interior and exterior culling, followed by matched 120 m Quest CPU/GPU/memory/load comparison if integration is authorized. Preserve all collision, gameplay and CutePixel text.
- Needs human: headset interior/seam/culling appearance and natural traversal. Blender and Unity appearance approved.
- Status: scoped native rail-view performance evidence only; production integration, runtime visibility/rebinding regressions, interior/combat, thermal and multiplayer acceptance remain queued for playtest.

### Player viewing distance 120 m to 175 m at dawn — queued 2026-11-13
- Commit(s): uncommitted.
- Changed: shared DuckPlayer cameras default to 120 m; runtime controller follows the existing successful dawn blend to 175 m. Failure/pre-dawn stay at 120 m; restore/replay returns to 120 m. Temporary editor-only 50 m override removed. Explicit development benchmark override still takes precedence.
- Checks passed: compilation and 17 EditMode checks for saved camera/controller references, transition endpoints/midpoint, failure, late-arrival dawn value, reset and unaffected unrelated cameras. See [evidence](validation/motel-render-chunks/view-distance-checks.txt).
- Setup: editor desktop and Quest; review nighttime blocks and successful finale through dawn, then return/replay. Test late join at dawn and optional benchmark override separately.
- Check: local XR eyes/simulated view use 120 m, smoothly reach 175 m with dawn and reset; no visible cutoffs on important signs/lights/buildings, no dawn valley disappearance, no mirror/veil camera changes; target-device CPU/GPU budget holds. These distances have not yet been device-profiled.
- Needs human: head-turning, distant silhouettes/emissive lights, flashlight reach and dawn appearance.
- Priority: normal.

### Motel spatial render consolidation — queued 2026-11-13
- 100 m comparison 2026-11-13: same-build native 100 m capture and fresh 50 m control pass four Motel viewpoints with 100% tracking; pitch within 1 degree. Forward median CPU/GPU 11.4/8.5 ms at 100 m versus 10.0/7.9 at 50 m, both around 72 FPS. All sampled Motel/Convenience 100 m views under budget at p95. Visual/dawn/multiplayer acceptance remains pending; no production clip change. See [comparison](quest-performance.md#100-m-versus-fresh-50-m--november-13-2026).
- Editor review setup 2026-11-13: temporary **Duck Rails > Tests > Preview 50m player far clip** toggle enabled for desktop/XR Play Mode. User to review popping, signs/headlamps, interiors and dawn. Editor-only; production camera assets/builds unchanged. Queued for playtest.
- Far-clip experiment 2026-11-13: explicit 50 m benchmark and same-build authored-distance control completed all Motel views. Closely matched yaw-180 CPU/GPU improved 12.5/9.9 -> 9.6/7.1 ms, draws 2,470 -> 317; yaw-0 comparison is confounded by headset movement. All 50 m views meet the sampled frame budget, but visual/dawn/multiplayer acceptance remains open. Production far clip remains 1,400 m. See [experiment](quest-performance.md#50-m-far-clip-experiment--november-13-2026).
- Quest partial PASS 2026-11-13: fresh optimized build, explicit Motel-first route, all four Motel views at 100% tracking. Worst view 14.7/14.7 ms median CPU/GPU and 61.5 FPS versus prior 19.2/17.5 ms and 51 FPS; still misses 72 Hz budget. Head pitch differs ~8 degrees, so controlled component on/off A/B, runtime visual/culling and manual checks remain pending. See [device evidence](quest-performance.md#quest-device-check--november-13-2026).
- Commit(s): uncommitted.
- Changed: offline-generated 698 render chunks replace 1,905 static source renderers at runtime; source geometry/collision/spawns and original baked lighting retained. Active Motel mesh renderers: 1,974 to 767. See [evidence and tradeoffs](quest-performance.md#motel-consolidation-pilot--november-13-2026).
- Setup: first check Motel lighting readiness in a translated route and block hide/show/re-enable; then matched short Quest runs with `RailSceneryChunks` enabled versus disabled on Motel, identical route/settings/poses to baseline `e1dc96566b4e4ab0ab45c83e870e9221`. Restore component enabled after the comparison. Do not change render scale/MSAA/foveation during this A/B.
- Check: no missing/duplicate surfaces or lightmap drift at origin/translated placements and after another lighting companion unloads; source/optimized views match, including interiors/reflections; collision, markers and interactions unchanged; disabling component restores originals; leaving/re-entering visibility range restores chunks. Compare CPU/GPU median/p95, draw calls, triangles, memory, loading and missed frames; report regressions rather than assuming fewer renderers guarantees a win. Aim for both CPU/GPU below the 13.89 ms budget at 72 Hz; further optimization may be needed.
- Needs human: headset close-up/interior appearance, culling transitions and natural traversal.
- Priority: normal.

### Quest performance runner robustness and expanded coverage — queued 2026-11-13
- Follow-up 2026-11-13: default 180-second expedition stop/save, collection and cleanup passed on device in run `fa93ca6c863246c68abcc707546cac32`; explicit Motel-first route confirmed in metadata. This closes the timed-stop uncertainty below, not broader coverage/robustness acceptance.
- Commit(s): uncommitted.
- Changed: opt-in native Quest 3 solo rail survey, buffered frame counters and PC run/collect controls. See [setup and device evidence](quest-performance.md).
- Device evidence: 483-second native capture, four scavenging blocks plus helipad, automatic refuelling/travel, CPU/GPU metrics, cancellation/timeouts, CSV retrieval and cleanup passed. User deliberately stopped after sufficient representative data; full route is not required for initial tuning. New default PC 180-second timer has syntax checks only and needs validation on the next requested run. See linked evidence for the one tracking-rejected view.
- Setup: Development APK on authorized Quest 3; explicit `Tools/quest-performance.ps1 -Action Run`. Run additional four-block sets separately.
- Check: cancellation, login/room/readiness timeouts, focus/tracking-loss rejection, request consumed after crash/restart, cleanup restores callbacks and PC proximity state; compare repeated captures with matched settings and release-equivalent overhead. Interior/combat and actual multiplayer load need separate scenarios.
- Needs human: stationary upright headset framing and headset comfort checks are separate from unattended frame-cost measurements.
- Priority: normal.

### Train, furnace, throttle, horn and ending-music audio — queued 2026-11-13
- Commit(s): uncommitted
- Changed: `RailAudioBank` (Resources) plus `RailAudioDirector` drive rumble (volume and pitch follow speed), rail-joint clack every 3.5 m, stop sound at rest, low-fuel beep (clip not supplied yet) and out-of-fuel cue, coal deposit (thud, flare, chime via `ShowFuelGain`), looping furnace fire, bridge crash, and success-ending music with fade-out. Throttle grab, release, pitched click every 25% and latch on top at 100% (`RailControl`). Horn is now a one-shot that fades out on release (`RailHoldControl`). `AudioManager.Play` gained an optional `pitchScale`.
- Setup: Expedition from Test Center, 1 client for most checks; 2 clients to confirm remote players hear deposits, throttle clicks and the horn.
- Check: deposit coal (thud, flare, chime together; no replay on late join); fire loop fades in with fuel and out at 0; rumble fades in and pitches up with speed, down as it slows; clack cadence follows speed and stops at rest; stop sound only at full stop; out-of-fuel cue fires once when fuel hits 0; throttle clicks at 25/50/75/100% up and down with latch at 100%; grab and release sounds; horn plays while held, never loops, fades quickly on release; bridge collision crash; success ending music plays through the ending and fades before return.
- Needs human: how it sounds (levels, pitch range of clicks, rumble pitch range, music loop seam; the ending ride is longer than the track so it loops, `loopMusic` on the bank), plus `Train_LowFuelWarning.wav` not yet supplied (slot is empty and silent).
- Priority: normal

### Shared complete store and security camera — queued 2026-11-12
- Regression 2026-11-13: Desktop purchases/lifetime/peer/late-join/handoff/departure PASS after fix `8379de0f1`; owned bundle and headset acceptance remain pending. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED for full acceptance (3 clients)**: Compound has 17 active display items and one camera; both expedition feeds create independent textures nearby and shut off at distance. Owned-product equip/cooldown/checkout and natural purchases remain untested: configured test accounts have no new bundle entitlements. Headset framing/reach/performance still required. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Avatar acceptance: equip Quackbear, Huggy and Scrap Duck individually from each store; verify the local avatar/mirror and hands update correctly, every connected remote client sees the correct equipped avatar, a late joiner sees the current avatar, switching between all three and Default Duck replicates without stale meshes/faces, and the selection survives the intended scene transitions/rejoin. Use real authenticated clients; a single editor or visual prefab check is insufficient. Still queued, not verified.
- Commit(s): uncommitted.
- Changed: one RailSharedStore prefab in Compound, Departure Shop and midpoint helicopter: seven coin goods with price/coin labels underneath, nine bundle items, Default Duck and local SecurityCam/SecurityTV/HelicopterMirror. Supersedes previous midpoint-exclusion expectations.
- Cheap checks: shared-store and helicopter authoring checks pass; no Play Mode.
- Setup: two authenticated clients and late joiner; Meta bundle products and backend rollout for checkout checks.
- Check: each location has one active store and TV; coin purchases charge once and spawn actual Rails cargo/bags on peers, all bundle ownership/equip/120-second timers, same-room cooldown sharing, default avatar, camera sees local face and remote players/held objects, screens have independent feeds and shut down out of range. Gold exchange still works only at the existing expedition intakes.
- Needs human: spacing/reach at all shops (compound is 100% scale; overlap accepted pending user layout adjustment), prices/icons and countdown readability, camera framing and headset rendering cost.
- Priority: normal.


### Furnace Polygon flames — queued 2026-11-12
- Regression 2026-11-13: The evening run's exercised desktop scenarios were deliberately excluded; headset scale/placement remains pending. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED only on headset scale/placement (solo, 2 clients, late third). Desktop portion PASS after fix 79b69296b**: idle/12% empty/refuel emission, four actual coal deposits including moving train/master handoff/empty recovery, one burst per receipt on peers, no historical burst on late join. See this run's Results. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Commit(s): uncommitted.
- Changed: `FX_Fire_Big_02` (already in `Assets/Plugins/PolygonParticleFX`, identical to the arpg copy) is a permanent child of `Furnace.prefab` ("Idle fire", tune its transform there; `RailFurnaceModule.idleFire`); emission fades to 12% (`outOfFuelEmission`) when `PhotonSession.CurrentFuel` is 0 and back up on refuel. Each committed coal deposit plays `FX_Fire_Explosion_01` once on every client, from the same path as the "+N" fuel popup. Prefab refs live in `Furnace.prefab`; `burstScale` 0.25 is a first guess.
- Cheap checks: compile clean; prefab references resolve; burst bounds ~1.7 m at scale 0.25. No Play Mode run.
- Setup: two clients on the train, a late joiner.
- Check: flames burn idly; at 0 fuel they dwindle; dropping coal in gives a burst visible to the other client; size/position suit the firebox opening.
- Needs human: scale/position feel (headset).
- Priority: normal
- Mode: fix-on-fail

### Quackbear, Huggy and Scrap Duck bundles — queued 2026-11-12
- Regression 2026-11-13: BLOCKED: all three real accounts still lack the bundle entitlements; owned equip/cooldown/persistence was not substituted with fabricated ownership. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED (3 clients)**: active store inventory checked; no owned test entitlements for these three bundles, so avatar equip/peer/late-join persistence and cooldown/checkout acceptance remain unverified. Meta rollout and human checkout/reach are prerequisites; no ownership was fabricated. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Commit(s): uncommitted.
- Changed: compound and Departure Shop bundle walls now use Duckz horizontal weapon–bag–avatar rows and source heading/countdown styling (overlap allowed for manual adjustment), three rigged avatars, separate four-slot bag and frying-pan placeholders, existing independent 120-second bag/weapon cooldowns and no avatar cooldown. See `docs/character-bundles.md`.
- Cheap checks: Unity compilation, 103 EditMode checks, visual preview and backend config/economy tests passed; no Play Mode run.
- Setup: two authenticated clients, then a late joiner and Quest; Meta durable products and matching backend catalog must be configured/deployed for purchase tests.
- Check: unowned checkout/cancel/restoration; owned avatar/default equip; bag and weapon spawn naturally into either hand; same item blocked until 120s, other item and avatar unaffected; shared wall cooldown, actual Photon room reset versus private same-room scene transition; peer/late-join appearances and unique weapon spawn IDs; held/bagged/loose moving cargo and departure cleanup. No gameplay coin charge; Departure Shop retains seven standard supplies alongside nine bundle pulls; the midpoint now uses the same shared full store. Check reach and cooldown sharing across all three stores.
- Needs human: headset reach (especially lower weapon row), natural grip, avatar eye/lip alignment and labels, Meta checkout interaction.
- Priority: normal.

### Matching fuel/distance backing — queued 2026-11-12
- Regression 2026-11-13: Evening fuel/backing scenarios excluded; full-route distance now passes. Headset backing appearance remains pending. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED only on visual/headset acceptance (solo)**: live ten/five/zero fuel segments and distance values PASS; shared backing readability and appearance still require review. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Shared backing mesh/material; original fuel text, ten yellow segments and dark sockets retained. Compile and serialized UI/gameplay preservation checks pass. Check headset appearance and fuel readability/full-to-empty state. No Play Mode run.

### Throttle hand offset, variable speed and return — queued 2026-11-12
- Regression 2026-11-13: Separate right/left physical paths and shared-input safety PASS; combined hand-switch, remote regrip/palm and right wide-bar end acceptance remain FAIL/incomplete. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED for full acceptance (solo and 3 clients)**: lease-level dead zone, intermediate/full speed, immediate release target, smooth visual return and heartbeat expiry PASS; peer and late-join values agree. Physical hand/input fixture cannot reach its motion checks (see Results). Palm tuning, detent, early-release/regrip, physical binding and headset feel remain unverified. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Commit(s): uncommitted.
- Changed: live palm offset, speed dead zone to 20% then proportional speed, retained forward snap, immediate STOP with 0.25s visual return; regrip preserves current angle. Prior cockpit works well in headset per user report.
- Cheap checks: Unity compile, 25 drive and session EditMode checks pass; Console error/exception query empty; saved prefab/scene tuning defaults checked.
- Setup: headset then two real clients with late join, disconnect and host handoff; do not treat user report of prior art as acceptance of these new behaviors.
- Check: tune Y/-Z offset while holding without altering tracked input or breakaway; speed zero through 20%, intermediate speed thereafter, forward detent at 85%; return finishes in ~0.25s without instant snap, including release before network acknowledgement and regrip mid-return. Remote visual and train speeds agree; fuel is distance-based; timeout/disconnect/handoff brake; desktop T remains full-speed/STOP.
- Needs human: palm clearance on both hands, variable-speed feel and detent/return feel.
- Priority: normal.

### Approved cockpit and lowered throttle axle — queued 2026-11-12
- Regression 2026-11-13: Focused physical grips attempted both hands and wide-bar ends; right-end acceptance remains incomplete. Headset cockpit reach/feel remains pending. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED (manual-only)**: live distance/fuel and lease replication were sampled with 3 clients, but full physical grip arc, horn, hand travel, clipping and headset reach/readability were not verified. Existing geometry checks do not establish these. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Commit(s): uncommitted.
- Changed: solid grip joining three arms/slots, axle 18 cm lower, faceted horn, distance bezel and coal/fire/fuel picture. See `docs/cockpit-v2.md`.
- Cheap checks: compilation, saved bindings/fonts, lowered neutral geometry, 71-angle slot envelope; EditMode render reviewed.
- Setup: solo then two real clients, late join and host handoff.
- Check: full STOP/GO arc, grip at either end of the wide bar, detent/release/breakaway, replicated grip, horn press/release, live distance and fuel. No arm/housing clipping at either end; no stale ownership on disconnect.
- Needs human: headset reach and increased hand travel, label/card readability and moving lighting; manual-only.
- Priority: normal.

### Furnace fuel popup — queued 2026-11-12
- Regression 2026-11-13: Evening exercised receipt/popups were deliberately excluded; remaining headset appearance stays pending. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED only on human readability (solo, 2 clients and late third)**: PASS four committed physical deposits with matching crew-scaled fuel credit/popups on both peers; no historical popup on late join. See furnace fix and receipt evidence in Results. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Dropping coal into the furnace floats a "+N" (N = per-run `RailResourceBalance.CoalValue`, e.g. +12.5 solo) up from the firebox opening, using the gold crate's `GoldRewardText` look. Shown on every client once per committed deposit; late joiners do not replay old ones. Check solo and two-client; verify the number matches the gauge gain and reads well. Compile/Console checked only; queued for playtest.

### Seven-block material contact sounds — queued 2026-11-12
- Regression 2026-11-13: 29 nearby material-specific hand/foot playback probes PASS. Natural contact cadence, duplicates, listening and headset quality remain pending. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED (manual listening/contacts)**: no new representative foot/hand-contact playback or peer-contact coverage in this run. Existing saved AudioSurface checks remain evidence only for assignments, not audible quality or duplicate/missing live contacts. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Commit(s): uncommitted.
- Changed: AudioSurface assignments across Final Bridge, Farm, Skyscraper, Helicopter Shop, Convenience Store, Motel and Container Yard. See `docs/block-audio-surfaces.md` for material choices and approximation limits.
- Cheap checks: installer compiled in Unity 6000.3.24f1; all seven saved prefabs reloaded; 10,344 environment colliders match planned surface types; all seven used types resolve to valid player footstep sounds. Serialized comparison preserves existing geometry, collision, gameplay and baked data. Console contains an LCK graphics-API error. No Play Mode run.
- Setup: visit the seven blocks using Test Block Order; walk and make hand contact on representative surfaces, then compare remote contacts with two real clients.
- Check: distinct metal/concrete/wood/grass/gravel sounds on bridge, silo, tower slabs, helicopter cabin, store floors, motel and containers; wooden fences/playground/pallets/trailer decking sound wooden. Check water-volume transitions and mixed ground seams. Confirm no missing or duplicate sounds and no traversal changes.
- Needs human: headset listening and suitability of existing Wood/Stone/Metal approximations for rubber, fabric, glass and plastic; manual-only sound-quality acceptance.
- Priority: normal.

### Broad block reverb — queued 2026-11-12
- Regression 2026-11-13: Independent peer listener regions and unload/recreation PASS. Perceived audio/voice and headset acceptance remain pending. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED for listening/performance acceptance (solo listener, 3-client route)**: all 25 live zones pass inside/outside listener-position plumbing; peers agree on zone counts. Independent moving peer listeners, unload tails, voice clarity, smooth audible transitions and Quest cost remain unverified. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Commit(s): uncommitted.
- Changed: 25 box zones across Final Bridge (3), Farm (6), Skyscraper (4), Convenience Store (4), Motel (4), Container Yard (4). Helicopter Shop excluded. See `docs/block-reverb.md` for placements/tuning.
- Cheap checks: installer compiled; saved 25-zone count, enabled components, child references, positive sizes and matching fade radii pass; Console error/exception query empty. Existing serialized prefab objects preserved. No Play Mode run.
- Setup: visit all six blocks in Test Block Order, then two real clients with late join; headset listening separately.
- Check: smooth entry/exit; dry open railway except finale tunnel; restrained tower/container reflections; stronger tunnel/cellar/silo tails. Check cellar versus farmhouse, motel wings/change-room partition, container roof and skybridge transitions. Hear item impacts/footsteps and peer voice clearly; each client follows its own listener location. Tunnel fades on moving-train entry/exit; no lingering reverb after leaving/unloading a block.
- Needs human: audio quality, voice intelligibility, headset transition feel and Quest performance. Manual-only listening acceptance.
- Priority: normal.

### Container Yard north yard, fences and open containers — queued 2026-11-12
- Regression 2026-11-13: Scoped desktop collision routes, selected loot settling/carry/drop and pursuit were attempted; completed routes PASS, repair threshold/full traversal acceptance remains incomplete. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED for traversal/headset acceptance (3 clients)**: latest five-block route loads and peers/late joiner match cargo, enemy and geometry inventories. Natural shed/trailer/fence/climb traversal, loot reach/settling in all new areas and enemy pursuit remain unverified. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Latest ground/spawn revision supersedes prior pool/marker evidence: main ground repeats at 12 m instead of 3 m; 168 coal / 142 gold / 32 enemy candidates redistributed across the updated yard, with unchanged runtime scaling. 3,092 editor checks pass, including all 342 supported/clear candidates and all 800 crew/seed cases per selector; 1,191 lighting bindings re-exported. See `validation/container-yard-redistribution/`.
- Redistribution checks: inspect loot in the shed workbenches/lower shelves, repair-container and trailer interiors, dock, tank-service corners, machinery, main container interiors and intended roof/climb rewards. Confirm natural reach, item settling, bilateral/quadrant encounters, low-eye ground texture scale and peer agreement. The old unsupported counterweight coal marker has been moved and its support issue is resolved. No Play Mode run for this revision.
- North extension: approved repair shed with workbenches/dismantled container, enterable truck trailer and loading dock, static forklift/pallet climb, two horizontal propane tanks and two baked floodlights. Original markers/poses/tuning retained. Additional 1,776 editor checks and six ground-route connectivity checks pass; navigation/lighting rebuilt, 1,191 total valid bindings including all 33 new combined renderers. See `validation/container-north-yard/`.
- North checks: walk around forklift into shed, inspect workbench/door/chain collision, traverse dock steps and threshold into trailer, climb pallets onto the fallen container, walk between/around propane tanks, confirm tank/support/valve contact and natural ground-enemy pursuit into the new interiors. Carry/drop items on the dock/trailer and compare both peers/late join. Headset night readability, dark rear-shed pocket and Quest frame time remain manual. No Play Mode run for this extension.
- Commit(s): uncommitted.
- Changed: east/west fences at X +/-47 m with continuous 9 cm wires, terminal posts and gap-free collision; four ground interiors per side, new Row 1 and existing Row 4 search openings face the tracks.
- Historical fence/interior checks: Unity 6000.3.24f1 compiled; 2,052 checks passed with 1,158 lighting bindings. The then-unsupported counterweight coal is resolved by the latest redistribution above. No Play Mode run.
- Setup: Container Yard first in Test Block Order; desktop then two real clients with late join; worn Quest separately.
- Check: walk into all eight interiors, carry/drop items through new openings, retain roof/climb routes; continuous fence contact along both sides and no seam protrusions or railway end caps; enemies navigate the opened interiors; both clients agree on geometry/loot and lighting loads correctly.
- Needs human: headset door/fence contact, nighttime wire/interior readability and Quest performance.
- Priority: normal.

### Raider Legends revive feedback — queued 2026-11-12
- Regression 2026-11-13: Moving/victory/two-helper handoff/late-progress/post-completion and volume-control desktop portions PASS. Four-client independent rescues remain BLOCKED; headset acceptance stays pending. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED for full acceptance (3 clients)**: rescue handover, brief interruption/grace and overlapping downed players PASS; all clients emit rising progress audio and receipt-triggered completion audio/four-layer particles. Moving train, victory ride, late-progress join, handoff/replay cleanup and four-client independent rescues still need coverage; Player4 is unconfigured. Headset audio/particles remain manual. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Commit(s): uncommitted.
- Changed: original ARPG progress/completion clips and four-layer revive burst; positional feedback on every client, pitch follows shared progress; committed completion receipts avoid duplicate/historical playback. See `revive-effects.md`.
- Cheap checks: Unity 6000.3.24f1 compiles; 31 EditMode checks pass, shader/material/mesh/audio resolve, Console error query empty. No Play Mode run.
- Setup: three real clients (rescuer, downed duck, uninvolved observer); repeat with moving train and during victory ride. Four clients for simultaneous independent rescues.
- Check: everyone nearby hears the rising pitch and one completion sound and sees one burst at the revived duck; no burst before authoritative revival. Hand withdrawal stops sound; brief grace freezes progress, longer interruption resets it. Two helpers never accelerate it. Simultaneous rescues remain distinct. Effect follows duck/train; completion remains visible to observer. Late join during progress starts at current pitch; joining after completion does not replay it. Master handoff does not duplicate effects. Disconnect/compound return/replay leave no sounds or particles. Victory-ride revival plays feedback without changing locked results/stats. Game-volume control affects both sounds.
- Needs human: headset spatial audio, volume, particle placement/scale/readability, Quest shader/performance and natural hand rescue.
- Priority: normal

### Train distance display — queued 2026-11-12
- Regression 2026-11-13: 475 full-route samples and actual bridge crossing PASS, without seeded distance. Supplied fuel and traversal pauses are disclosed; headset legibility stays pending. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED only on headset legibility (solo and 3 clients)**: acknowledged departure state reads 0 m, seeded bridge approach reads 1000 m; moving-run peer and late-join labels agree at 18 m. No claim of a natural full-route drive. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Commit(s): uncommitted
- Changed: new `RailDistanceDisplay` + "Distance display" TMP label above the fuel gauge on `DuckRailsTrain.prefab`; shows `DISTANCE n m`, `RailTrain.Distance` rescaled so the bridge area (`RailRoute.FinaleStart`, ~780 m of track) reads ~1000 m (run ends ~1096 m). Display only; label hides with the other running labels at run end. Compiles clean, Console clear; not run in Play Mode.
- Setup: any expedition, one client, then a late joiner.
- Check: counter starts at 0 at departure, rises smoothly with speed, reads ~1000 at the bridge approach, is legible from the driver position, and matches on a late joiner.
- Needs human: legibility/placement in headset.
- Priority: low
- Mode: fix-on-fail

### Motel community leisure layout — queued 2026-11-12
- Regression 2026-11-13: Balcony/door routes, selected loot carry/drop and pursuit PASS; both stair probes still fail to complete. Natural full circulation and headset acceptance remain pending. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED for traversal/headset acceptance (3 clients)**: initial route startup FAIL from six null baked-renderer bindings; normal lighting re-export in fix 3c64b55a4 leaves 1,964 valid bindings. Rerun reaches Running/WorldReady, including late join, and peer inventories agree. Natural leisure-area/bus-roof traversal and enemy pursuit remain unverified. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Latest correction supersedes prior pool counts: 48 coal / 64 gold / 32 enemy candidates, unchanged runtime spawn rules. All markers replaced for the current layout; all 100 seeds × crew sizes 1–8 meet full resource/enemy targets, quadrant quotas and spacing (2,691 checks). Inspect reach/return routes, bus-roof gold access, pool basin/upper-room enemy pursuit, ceiling-mounted lights, downward streetlight readability, low-poly wheels, six benches and outward golf/rink sides. No Play Mode run for this correction.
- Detail feedback adds nine unique greens/duck/shoot-through house, shack counter/clubs, golf/rental mesh signs, boarded windows/green handled doors, guest furniture/bathrooms, roof equipment, grey/blue cars, continuous stair/wall/rink surfaces and Quackville pixel materials. Inspect all board/stair/rail joints, furnished-room and bathroom circulation, house passage, wall seams at moving viewpoints, material scale and white-only facade lighting. Remains queued; no Play Mode authorization for this batch.
- Detail checks: 4,186 editor assertions pass, all 24 loot candidates supported/clear and all 18 enemy markers grounded; navigation rebuilt. Current saved layout contains no waterslide. Follow the current prefab and detail notes rather than the older slide-specific checks below.
- Screenshot revision supersedes original layout: seven closed rooms, half-width doors, room 101/102 connection, unglazed windows, stair rails; mesh neon/car improvements; nine-hole golf and relocated shack; north rink/rental entrances, white/blue rink walls; simple side fences, varied convex-collider trees; food vendor/exterior left cubbies/loungers; deeper pool, two diving boards and straight slide; brighter lights.
- Revision checks: 2,186 assertions pass; all loot support/clearance and named doorway checks pass. Check natural room/window passage, closed-room exclusion, internal door, all pool/board/slide recovery, vendor freezer/fryer/shelf/cubby reach, north rental/rink access, fences/trees, lights and peer agreement. Lighting brightness and Quest performance remain manual acceptance.
- Commit(s): uncommitted.
- Changed: separate public pool with change rooms/cubbies/static old waterslide; mini-golf, roller rink, five buses versus two retained cars; motel retained.
- Cheap checks: 2,114 editor assertions pass; all loot support/clearance checked; offline navigation rebuilt. See docs/roadside-motel-layout.md and docs/validation/motel-leisure/.
- Setup: motel-first private expedition, desktop then two real clients/late join; worn Quest separately.
- Check: pool entry/recovery and slide stairs/flume; open cubbies and shower/bench circulation; rink/rental/golf access; bus clearances; carried-item settling; enemy routes and boarding; identical environment/loot across peers.
- Candidate-capacity limitation resolved by the latest correction: 48/64/32 pools pass eight-player maxima and 800 enemy crew/seed selections. Natural gameplay balance and route accessibility still need playtest acceptance.
- Needs human: natural VR reach/locomotion, slide fall recovery, darkness, scenery review and Quest performance.
- Priority: normal.
- Status: queued for playtest; no Play Mode run for this change.

### Convenience Store screenshot polish and arcade mascot — queued 2026-11-12
- Regression 2026-11-13: Court/arcade/rear-road samples, fixed-collision cosmetic rollers and loot carry/drop PASS; straight wash/pursuit acceptance remains incomplete; full natural routes/headset remain pending. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED for traversal/headset acceptance (3 clients)**: current route, renderer/collider inventory, selected cargo and enemy IDs agree on peers and late join. Natural stair/shop/car-wash interaction and visual/performance acceptance remain pending; earlier scoped checks are retained below. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Queue run 2026-11-12: two-client loot/enemy consistency, late rejoin, loot support and cosmetic-only brushes PASS; all 235 candidates pass footprint/clearance (see Results). Still queued: avatar traversal (court gap, wash/rear road, semi, coolers, restaurants, arcade), enemy pursuit, rear doors, art/lighting and Quest performance.
- Basketball court: rim segments, net strands and playing surface now have fitted boxes; all 121 court colliders pass editor ray checks, hoop centres and south entrance remain clear, and navigation is rebaked. Check hand/item/player contact in the existing playtest.
- Crossing arms: one continuous fitted BoxCollider per lowered arm now replaces segmented collision. Editor rays hit along both full lengths; check avatar/hand/carried-item contact at the tips and stripe seams. Queued for playtest.
- Collision/sparse-stock follow-up: trees now collide across trunks and canopy tiers, all six rooftop models have shape-matching collision, and seven brush roots have stationary full-size hulls with rotating visual children. Check head/hand/carried-item contact and walking routes, particularly beneath trees and around wash rollers; ensure the Q opening remains open. Solid cooler glass/displays/fan/ceiling fixtures also collide. Shop shelves now have only nine scattered brown cubes; cooler shelves have no drinks, retaining coal/gold candidates. Verify loot reach and settling after local collision repairs; queued for playtest.
- Latest collision editor evidence: 1,848 saved-prefab checks pass, 184 new collider shape-ray checks, empty Console error query, offline navigation and lighting rebuilt (1,272 valid bindings). See `docs/validation/convenience-round2/collision-and-stock.txt`; earlier round-two totals below are historical.
- Q-logo follow-up: corrected inverted ring faces/normals; inspect the exterior and inner opening from both sides under the rooftop spots. Mesh winding and Unity preview checked; headset appearance queued for playtest.
- Round two follow-up: preserve the user's moved semi/deletions; inspect the court's open south-side gap (no swinging gate), off-court light and denser trees; lowered red/white railway road barriers; simple blue van; closed ornamental restaurant rear doors and empty framed windows; staggered wash pillars/horizontal rear brush; party carpet/yellow-pink arcade and eight back-room machines; all tree canopy undersides. Five new vendor roof models plus arcade duck have paired spotlights. Store has four upright drinks coolers (two open), varied shelf stock, six inventory racks and long fluorescent fixtures with one burnt out. Gas neon is cyan/teal again, superseding red below.
- Round two editor evidence: `docs/validation/convenience-round2/`; loot redistribution follows model changes, same candidate counts/identities and per-run settings. Check all restaurant loot, open drink-cooler reach, stockroom aisle and moved-trailer cargo. Rear restaurant doors must block exit. Validate cosmetic brush motion, both crossing road arms, rooftop spotlight readability and burnt-out fixture appearance. Compare shared-neon-atlas/combined-detail GPU and draw cost on Quest; performance acceptance is pending.
- Commit(s): Convenience Store polish commit; see Git history.
- Changed: bilateral streetlights, picnic grove/tables, rail-facing car wash/access road and rotating bristle rollers, northwest basketball court/open gate, wooden side fences, twelve scattered cars, enterable articulated semi, open freezers with loot, distinct restaurant interiors, shell/paint/sign repairs, rooftop equipment and low-poly arcade duck mascot. Wall triangle count reduced 86.4%; static details consolidated.
- Cheap checks: round-two compilation and 1,852 saved-prefab checks pass, including 800 enemy selection cases; navigation and lighting rebuilt/exported (1,450 bindings, two lightmaps). Console has an unrelated Unity Connect token-exchange error, no compile/block-authoring error. Round-one evidence (4,010 assertions/1,109 bindings) is historical. See `docs/convenience-store-layout.md` and `docs/validation/convenience-round2/`.
- Setup: Convenience Store first in a private expedition; one desktop client, then two authenticated clients including late join; worn Quest separately.
- Check: enter/exit court through the side gap; traverse car wash and rear road past rotating bristles; reach semi via ramp/open doors and recover trailer loot; reach open cooler loot; traverse each restaurant from its front and both arcade exits around the doubled middle cabinet row. Check enemy pursuit, roof spawns, vehicle clearance, side fence/seam behavior and loose-item recovery. Confirm paint does not flicker/collide and floating sign fixtures are gone. Review teal gas, red Noodle Box, green wash, added lamps and rooftop models at actual expedition darkness. Brush motion must remain cosmetic and must not move or trap players/items. Check both clients agree on loot and environment.
- Candidate pools: 126 coal / 109 gold / 27 enemies; per-run counts unchanged. Trailer/freezer bars are candidates, not guaranteed simultaneous contents.
- Needs human: mascot/art review, natural hand reach, full avatar/headset traversal, lighting/glare, draw cost and Quest frame time.
- Status: queued for playtest; no Play Mode run for this change.
- Priority: normal.

### Skyscraper north/south yards and upper-floor loot — November 11
- Regression 2026-11-13: Both foundation escape probes, selected cargo settling/carry/drop and pursuit PASS; 18 known candidate overlap flags retained. Full upper-floor/natural/headset acceptance stays pending. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED for traversal/headset acceptance (3 clients)**: current block and selected cargo/enemy inventories agree on peers and late join. Upper-floor reach, foundation recovery, wall contact, pursuit and night/Quest performance were not newly verified. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Queue run 2026-11-12: two-client loot/enemy consistency and late rejoin PASS. The editor check flags 18 floor-0 candidates in Towers B–D overlapping the ground slab; the live spawns settled asleep with no gap, so they were left unchanged. Review if items visibly pop on spawn. Traversal, lighting and performance remain queued.
- Side walls: check 3 m concrete walls at X +/-47, ground contact/collision along both full lengths, entry/exit seam transitions, no stranded loot/enemies, square pixel density and headset appearance.
- Foundation light: check grounded southwest tripod, broad cool forward illumination across the pit and onto the excavator, and glare/foot clearance on headset.
- Crossing lights: review the 23 baked warm point lights on all four skybridges and the fallen crane for nighttime visibility and glare, including carried-item traversal.
- Rooftop follow-up (revised November 14): check grounded stands on A/D +16 m, B +8 m and C +12 m top floors for attachment and unobstructed walking/grabbing.
- Follow-up: eight workbenches now have legs/braces; brighter baked spots plus warm/cool floods. Check bench collision, nighttime visibility and glare on all floors and in both yards.
- Status: pending, queued for playtest.
- Scope: skyscraper-works-v1 only; use the November 14 reduced 17-floor geometry and preserved skyline, superseding the earlier geometry-preservation instruction.
- Checks: enter/exit both office trailers; walk the loading/maintenance shelters; enter the foundation and recover via each ramp; carry coal/gold out without catching on edges. Inspect worklights, windows, pipes and clutter at night on headset. Review upper-floor loot across repeated seeded runs (November 14: 202 coal / 192 gold candidates; per-run totals unchanged). Test enemy pursuit around new yards, late-join consistency, moving-train clearance and target headset performance.
- Editor evidence: docs/validation/skyscraper-polish; tower/rail preservation, spawn support and 1,000 seeded selection cases passed. See docs/skyscraper-layout.md for bake and final validation evidence. No Play Mode run performed for this change.

### Attached hold-forward throttle — queued 2026-11-11
- Regression 2026-11-13: Fuelled driver/master disconnects, shared-input safety and ordinary pickups afterward PASS. Physical regrip/remote attached-hand acceptance remains incomplete. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED for physical-input/headset acceptance (3 clients)**: current variable-speed lease, remote lever, release and heartbeat expiry PASS; earlier disconnect/handoff evidence remains below. Broad rig fixture fails before physical throttle coverage; attached hands, wide-bar grabs, ordinary pickups and actual keyboard binding remain unverified. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Queue run 2026-11-12: multiplayer lease cases PASS through the simulated-rig hold path: exclusivity/denied lease, peer lever, focus/blocked/down/disconnect cleanup, host loss and late join (see Results). Still queued: remote attached-hand visual with a real hand grip, keyboard T, ordinary pickups afterwards, and all headset items.
- Solo live follow-up: PASS on both hands for injected physical grip, smooth forward arc GO, forward jitter tolerance, pull-back OFF, re-push GO, release and sideways breakaway. Attached-palm query/closed finger curl pass; sampled palm gap 6.3 mm. Shared target speed/lease zero afterward; normal movement/input restored and existing Play session left running. See `docs/validation/shared-rig/throttle-hands-20261111.txt`. Remaining cases below, actual skin-contact visual review, headset feel and multiplayer remain queued; this is not full acceptance.
- Attempt 2026-11-11: user authorized solo desktop simulated-hand Play Mode. Verified Duck Rails in Edit Mode, then `RailTestRunner.Start("sandbox")` and the follow-up state query each timed out after 120 seconds. Editor process remained present; computer-use window activation failed twice (`failed to activate captured window`). Runner state remained `launching`. No interaction checks ran; no PASS claimed. On recovery inspect editor/runner state and stop through `RailTestRunner.Stop` to restore the prior scenario before retrying. Do not discard the dirty Farm bake scene.
- Commit(s): uncommitted.
- Changed: attached local/replicated palm and closed fingers; arc-driven lever; GO at 85%, OFF at 72%; 36 cm breakaway (increased November 16); detent haptic/audio; immediate state-edge requests. Keyboard T toggle retained.
- Cheap checks: Unity compilation, 20 Edit Mode drive checks and empty Console error query. See `docs/shared-rig.md#attached-throttle-trial--november-11`.
- Setup: private running expedition, shared DuckPlayer simulated physical grip first; two authenticated clients with late join/master handoff; worn Quest separately.
- Check: either hand acquires exclusively, palm stays on handle throughout slow/fast strokes and train motion; raw controller remains independent. Hold forward sustains GO with small tremor tolerance; pulling back while gripping cuts GO, pushing again resumes. Release, sideways/upward separation beyond 36 cm, downing, blocked input, focus loss and disconnect stop driving and restore free hand pose. Denied/lost lease does not attach or steal control. Peers see attached hand and matching lever; late join/host departure preserve safe ownership. Test keyboard T and ordinary pickups afterward.
- Needs human: hand/finger alignment, comfortable travel/tolerance, fast-stroke breakaway, mechanical sound and haptics on both controllers.
- Priority: normal.

### Farm full screenshot polish — queued 2026-11-11
- Regression 2026-11-13: Ten aligned cell entry/exit cases and accepted silo/playground samples PASS; barn/ditch/pond/full traversal acceptance remains incomplete. Human reach/lighting remains pending. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED for traversal/headset acceptance (3 clients)**: current block/cargo/enemy inventories agree on peers and late join. Natural routes, surface contact, loot reach, enemy pursuit and device performance remain pending. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Queue run 2026-11-12: the flagged roof/hay loot plus Gold 087 were repaired in `bfcd86651`; 222/222 candidates now pass. Two-client loot consistency and late rejoin PASS. Traversal (stairs, cellar, attic, silo ramp, basement cells), ditch footsteps, lights and headset items remain queued.
- Basement holding cages: central cage/four watching chairs, four side cells, three open lockers, four green-white baked fixtures. Added two coal/four gold candidates (pools now 105/117/22); per-run settings preserved. New loot checks and eight doorway clearance samples pass; navigation and lighting rebuilt. Queued for playtest: enter/exit every cell, reach locker loot, return through both basement exits, enemy pursuit, bar shadows and headset darkness/performance. See `validation/farm-polish/basement-checks.txt`.
- Silo ceiling follow-up: enterable silo underside now uses planar XZ texture coordinates (1.8m repeats). Geometry and baked UV2 retained; all 16 underside UV triangles pass. Inspect ceiling texel scale from doorway/in motion; queued for playtest.
- Corn follow-up: existing 3,669 stalks/52 patches retained; five leaves become four (20% fewer leaves, 102,732 -> 88,056 triangles). Geometry-scaled UVs replace stretched stem/leaf mapping. Saved mesh checks and Console pass; close-up editor capture inspected. Check in-headset texel scale, field concealment/readability and performance in motion. Queued for playtest.
- Silo surface follow-up: doorway overlap removed; grain uses planar UVs and golden dirt. Retained user's grain/shell offsets and adjusted nine silo loot heights. Check threshold flicker and grain texel scale in motion. Current broad checks flag seven roof/hay loot footprint/clearance issues outside the silo; resolve against the user's latest geometry when running this queue.
- Commit(s): uncommitted.
- Changed: furnished farm buildings/house; pond/entrance baked lights; railway-facing shed doors; ditch water audio; low-poly side fencing; elevated silo interior with wraparound ramp/loot; house floor/attic roof/stair/cellar repairs. Local user edits and per-run settings preserved. Pools 103 coal / 113 gold / 22 enemies.
- Cheap checks: Unity compilation, Console error query, 791 geometry/loot/reference/light checks, 35 entrance/closure/export checks and 1,000 seeded selections pass. Navigation and lighting rebaked/exported. See `docs/validation/farm-polish/` and `docs/farm-silos-layout.md`.
- Setup: private Farm route, one desktop client then two authenticated clients with late join; headset review separately.
- Check: all four nine-step house flights traverse without falling through side/bottom gaps; compact cellar is enterable/exitable; attic has no floor flicker or roof obstruction. Reach barn loft and silo grain, traverse ramp turns both ways, collect/return loot and recover drops. Furnished paths and doorways stay passable. Ditch produces wet footsteps; bridge stays dry. Review entrance/pond lights. Enemies pursue around structures; peers agree on scenery and seeded loot.
- Needs human: larger stair-rise and ramp comfort/hand locomotion, loft reach, water sound quality, nighttime readability and Quest performance.
- Priority: normal.

### Bridge finale batch polish — queued 2026-11-11
- Regression 2026-11-13: Middle/river ramp samples and fresh defender pair PASS; both upper ramps and west inward ramp remain incomplete. Headset/full route acceptance stays pending. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED for traversal/headset acceptance (3 clients)**: loaded finale inventory and three reverb-zone plumbing checks pass; this run uses a controlled success trigger for results, not the actual bridge sweep. Ten-ramp traversal, defenders, natural crossing and headset/performance checks remain pending. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Queue run 2026-11-12: all 54 finale loot candidates pass footprint/clearance; finale loot (6 coal / 2 gold) matched on both clients and after a late rejoin. Traversal, tunnel floor, lighting and Quest checks below remain queued.
- Stationary gold-crate lighting: finale-only UV2 mesh copy, Contribute GI, Receive GI Lightmaps and block bake/export installed; 17 editor checks pass. **Queued for playtest:** baked crate readability beside the button and unchanged intake/coin behaviour. Reward-text fields were already null in the saved baseline; floating reward text is not validated by this change. See `output/level-polish-batch/followup/gold-crate-checks.txt` and before/after views.
- Bank lighting correction: static far-bank/tunnel floor now uses its own exported lightmap instead of one probe sample across the whole renderer. One finale bake/export and 23 editor checks pass; 1,741 bindings installed, all 22 user-edited lights preserved. Compare `output/level-polish-batch/followup/bank-light-BEFORE.png` / `bank-light-AFTER.png`. **Queued for playtest:** entrance light stays local on grass during approach, tunnel passage and dawn transition; floor/bridge remain visible on clients/headset.
- Tunnel-floor correction: restored the 149 disabled bake-excluded finale renderers and corrected their visibility lifecycle. Twenty-four EditMode checks pass, including export/reload persistence, Expedition visibility and tunnel floor collision. **Queued for playtest:** confirm visible floor throughout the tunnel and intact moving drawbridge on actual clients/headset; no Play Mode run for this fix.
- Follow-up November 11: items 17–21 add subdivided/higher-detail ground lighting, two thin middle ramp slabs, four movable fixture/light groups, dark non-overlapping bank lip, and perimeter walls extended to Z80. One lighting export installed (1,740 bindings), navigation rebuilt (72,503 nodes), and 1,862 scoped editor checks pass. Review `output/level-polish-batch/followup/`. Still **queued for playtest**: traverse both thin slabs and inspect underneath, test outer-wall/river junction access, view the dark bank lip in motion, inspect red light pools from headset height, and measure Quest performance/memory. Total loaded mesh cost is 44,230 triangles and 1,900 mesh renderers; editor evidence does not establish device performance.
- Commit(s): November 11 commit titled `Polish bridge finale geometry and baked lighting`.
- Changed: all 16 approved finale polish requests: ramps/inward access, climb blocks, asymmetric crates, low walls, concrete seam fixes, grounded cliff/tunnel, recessed stream, gravel/gables, floodlights and red warning caps. Two on-stair coal candidates move to adjacent flat roofs; counts and tower defenders retained.
- Cheap checks: compilation and Console error query pass; 1,375 geometry/spawn checks and 398 final bridge-sweep/train-clearance/reference checks pass. Both tower ground paths to the button pass; navigation and lighting baked/exported, 1,740 bindings installed in Expedition. See `docs/level-polish-batch.md` and `output/level-polish-batch/`.
- Setup: private test route to bridge-finale-v1, one desktop client followed by two authenticated clients with a late join; Quest review separately.
- Check: all ten ramps traverse without snagging, both river switchbacks return to the fortress, both four-block climbs reach north platforms, and crate groups preserve patrol circulation. Two tower defenders can reach the button without route traps. Hold/pause/resume and the full bridge sweep remain functional; train passes the tunnel and reaches dawn. Confirm no visible flickering on north concrete/river landings, tunnel gaps, open gables or exposed side-hill holes. Inspect recessed stream/collision and ballast from low viewpoints; ensure gravel does not obscure rails. Coal on X±23/Y4.2/Z62 settles and remains reachable. Peers/late joiners agree on geometry and loot counts.
- Needs human: natural VR ramp and block-climbing feel, hand clearance on recovery rails, night/dawn lighting and emissive readability, valley/cliff sightlines from elevated train positions, and Quest CPU/GPU/memory comparison. Geometry has 109 fewer renderer objects and 5,718 more serialized triangles; no frame-time claim.
- Priority: normal

### Farm northeast pond and playground — queued 2026-11-11
- Regression 2026-11-13: Playground platforms/bridges and static swing PASS; ditch/pond route probes remain incomplete. Human natural reach/locomotion remains pending. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED for traversal/headset acceptance (3 clients)**: current Farm selections agree on peers/late join. This does not verify natural ladder/bridge/slide/pond traversal, loose-item recovery, enemy approach or Quest performance. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Queue run 2026-11-12: peers and the late joiner reconstruct identical Farm selections, and the swing has no physics components (see Results). Platform/ladder/slide/pond traversal, enemy approach and Quest performance remain queued.
- Commit(s): uncommitted
- Changed: replace northeast corn with shallow pond, 108 horsetail stems, three playground platforms/two bridges, static slide/ladders, empty swing frame and hay/cart/fence dressing. Net whole-Farm reduction 43,104 triangles (21.5%). Reposition 14 coal/14 gold and two enemies; append four enemy candidates. Per-run counts unchanged.
- Cheap checks: compile, navigation bake, lighting bake/export and scoped saved references; loot footprint/clearance, enemy navigation/bounds, sampled deck/pond collision, 800 enemy crew/seed selections and 200 maximum-crew loot selections pass. See `docs/validation/farm-paddock/`. Broader catalogue lighting check has unrelated helipad light-count failure.
- Setup: private test order containing Farm, one desktop client then two authenticated clients with late join; Quest review separately.
- Check: reach all three platforms via ladders/bridges/slide without snags; traverse pond banks and recover loose items from its 0.38 m depression; inspect hay/cart loot support and natural reach. Enemies can approach/pursue through this area without trapping on new geometry. Peers and late joiners reconstruct matching candidate selections. Swing frame has no hanging parts or simulated physics. Review night/flashlight readability and measure Quest CPU/GPU time against the prior cornfield.
- Needs human: hand climbing, slide and shallow-water feel, loot reach/readability, headset performance.
- Priority: normal
- Mode: fix-on-fail

### Results board superlatives: stats, counters and award assignment — queued 2026-11-12
- Regression 2026-11-13: Current award checks and actual-action result agreement after rejoin/handoff PASS; seeded evening title scenarios excluded. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED for full acceptance; report-only (3 clients)**: after a real master handoff and controlled success, board counters and three distinct titles agree on every client. Repels/spent stats were submitted as test values; this is not a natural shop/gold/hit/minute-light run. Failed-run, pre-finish reconnect, all attribution cases and VR threshold feel remain unverified. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Commit(s): uncommitted (B clone)
- Changed: honks removed; board shows Coal, Gold (deposited), Revives, Downs; 20 superlatives + Precious Cargo assigned once at the finish (`RailAwards`), counters from the master's 10 Hz sample and a batched client `stats` request. Spec: `docs/game-design.md` "Superlatives and board stats".
- Cheap checks: `Duck Rails/Legacy/Run Superlative Checks` (`RailAwardsChecks`) PASSED 2026-11-12 in B's editor (MCP verified `Application.dataPath` = `X:/Work/duck-rails-b/Assets`): project compiled with 0 errors (only pre-existing warnings) and the menu item logged "25 superlative checks passed". Edit-mode only; no Play Mode, network or headset run. Later the same day the new `Boundaries` method (20 awards x 2 assertions) first failed at Pacifist (float `RunSeconds * .8f` rounding); after `Present()` and Window Seat were changed to exact integer multiples, it recompiled with 0 errors and the menu item logged "65 superlative checks passed" (all awards' minimums are inclusive, just-below gets no title).
- Setup: private run to a bridge finish with 2+ clients (3+ ideal), a shop purchase, a gold sale, a revive, a hit on an enemy, a flashlight on for a minute.
- Check: board rows show Coal/Gold/Revives/Downs and exactly one title each; no title repeats; the top stat holder gets the title; players under every minimum read Precious Cargo; a non-escaper reads Missed the Train unless a higher title applies; titles are identical on every client and after a master handoff or reconnect before the finish; failed runs show titles too.
- Needs human: whether thresholds, the 15 m / 2 m proximity radii and the stillness limit feel right with real VR hand jitter.
- Priority: normal
- Mode: report-only

### Train aboard margin: escaped and end-of-run teleport — queued 2026-11-11, manual-only
- Regression 2026-11-13: Side/nose/far-ground desktop classification rerun; traced nose stays padded for six seconds without rescue. Natural headset standing/hanging remains pending. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED (manual-only)**: prior solo spatial-boundary PASS remains valid evidence; this run's seeded success with 3 clients does not test actual padded-boundary finishes. Hand-hanging and a real second-client finish still pending. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Queue run 2026-11-12: automated part PASSED (side hang, nose, far on the ground; see Results). Remaining: hanging off by hand in VR; a second client at the finish.
- Commit(s): 84985d551
- Changed: `RailTrain.ContainsPadded()` (Inspector-tunable `paddedMin`/`paddedMax`, yellow gizmo when selected) now decides who "Escaped" at the bridge finish and whether the end-of-run rescue teleport fires. Horns, boarding and cargo keep the tight `Contains()`.
- Cheap checks: compiles; fields read back on the train prefab instance. Not run in Play Mode.
- Setup: private run to a successful bridge crossing, one desktop client (plus a second client if available).
- Check: a player hanging off the side by hand, or standing on the front of the train, at the finish is listed "Escaped" and is not teleported. A player on the ground well away from the train still reads "Didn't make it" and is teleported to the deck.
- Needs human: hanging off the side by hand in VR.
- Priority: normal

### Expedition render warmup: flashlight and helicopter TV first use — queued 2026-11-11, manual-only
- Regression 2026-11-13: First/repeat, warm Compound late join and cold initial Login late join PASS under the veil; headset first-use hitch/appearance remains pending. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED (cold-cache/headset)**: third client joined the running route successfully, but no new veil/cold-cache timing instrumentation was run. Prior desktop warmup PASS retained; cold Login join and Quest profile remain pending. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Queue run 2026-11-12: desktop part PASSED (see Results). Remaining: a cold late join from Login, and the Quest cold-cache profile below.
- Commit(s): 1adf32ebe
- Changed: on every Expedition load, the transition veil stays up while `ExpeditionRenderWarmup` draws each scene and catalog render state with lights off and with a warm spot light, through the viewer camera and a copy of the helicopter feed camera. See docs/transition-veil.md.
- Cheap checks: compiles, Console empty. Not run in Play Mode.
- Setup: Compound → private run departure, one desktop client in DUCKZ_DIAGNOSTICS, then a second departure in the same session; one late join aboard a running train from Login.
- Check: log `[ExpeditionWarmup] Prepared N render states...` with N > 0 on the first departure, no "time limit", and far fewer states on the second departure. Veil reads ALL ABOARD / "Getting the train ready...", then releases. A late joiner sees no uncovered Expedition frame before the veil. No leftover "Warm proxies" object or extra camera after release. Scene lights such as the helicopter light come back on afterwards. Editor-only: proxies visible in the Scene view during the warmup is fine.
- Needs human: Quest development build profiled on a cold shader cache. Confirm the first flashlight or helmet light-on and the first helicopter TV view produce no shader or pipeline-creation spikes, and record the added veil time.
- Priority: normal

### Compound Discord button — queued 2026-11-11, manual-only
- Regression 2026-11-13: Actual button dispatch/debounce/room retention PASS again; external browser appearance was not visually accepted. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED (manual-only)**: previous desktop PASS retained; Quest browser launch/return and reach/readability not available in this desktop run. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Queue run 2026-11-12: desktop press, 3 s guard and no room transition PASSED (see Results). Remaining: Quest browser launch/return, headset reach/readability.
- Commit(s): uncommitted
- Changed: local JOIN DISCORD physical button with a "Join the Discord!" heading on the inner right partition of the Station Info nook; opens https://discord.gg/sfyvhahnDW.
- Cheap checks: installed via Unity MCP, installer validation (callback, URL, CutePixel labels) passed, no Console errors; camera capture shows readable placement.
- Setup: Compound, one desktop client and a Quest client.
- Check: desktop click opens the invite in the default browser; headset touch opens it in the Quest browser. Repeated contact within 3 s opens it once. Returning to the game leaves the session usable; no room transition.
- Needs human: Quest browser launch/return, headset reach and readability at duck height.
- Priority: normal

### Compound rating button and instruction PNG — queued 2026-11-11, manual-only
- Regression 2026-11-13: Actual button dispatch/debounce/status/room retention PASS again; external store and headset PNG/readability remain human checks. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED (manual-only)**: previous desktop PASS retained; native Meta UI/cancel, replacement PNG review and headset reach remain pending. No rating submitted. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Queue run 2026-11-12: desktop press, status text, 3 s guard and no room transition PASSED (see Results). Remaining: native Meta rating UI, cancel/return, PNG replacement, and headset reach.
- Commit(s): uncommitted
- Changed: local RATE GAME physical button and replaceable 1024 × 768 PNG canvas in the Station Info nook; Quest requests native Meta rating/review UI, desktop opens the configured app's store listing.
- Cheap checks: Unity 6000.3.24f1 compiled and installed in batch Edit Mode, exit 0; callback, texture, canvas and font references passed; no C# errors or exceptions. Scene diff limited to the new instance.
- Setup: Compound, one desktop client and a Quest client authenticated through the normal login flow.
- Check: desktop click opens Duck Rails' store listing; headset touch requests the native rating UI without an eligibility precheck. Cancel/close returns to usable gameplay. Failures display a retry message, repeated contact does not repeatedly launch requests, and no room transition or reward occurs. Replace PNG while preserving its .meta and confirm the saved canvas picks up the new art.
- Needs human: actual Meta review-page launch/cancel, headset reach/readability and normal session recovery after system UI. Do not submit a rating as part of testing.
- Priority: normal

### Pause menu and rooms: headset buttons and feel (5aaa6ffdb, d480df22a) — queued 2026-11-11, manual-only
- Regression 2026-11-13: Desktop menu/pages/tablet/rooms/owner gate paths rerun PASS; physical controller/key delivery and headset feel remain pending. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED (manual-only)**: no Quest controllers; Start/P and natural menu/keypad/gate interaction were not newly verified. Prior automated results retained. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Commit(s): 5aaa6ffdb, d480df22a; fix 1f1203b1c (automated part PASSED 2026-11-11, see Results)
- Check: Start button on a Quest controller and the P key open/close the menu (key input was not simulated). Poke/grab feel of the menu, keypad and gate LAUNCH EXPEDITION button. Hint readability at duck height.
- Needs human: all of the above.
- Priority: normal

### Stranded return: three clients and the original repro (d9ab4c4fc) — queued 2026-11-11, manual-only
- Regression 2026-11-13: Evening stranded-token case excluded; fresh normal public return and cut return-control connection PASS with three clients. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED only on original headset repro (3 clients)**: targeted desktop return/rejoin portion PASS. A disconnected non-master rejoined the retained Returning expedition after its return token, immediately entered Leaving, and recovered to Compound in 2.70 s from that sample. Public-return state was seeded in an isolated private room and third client's session updates held to retain it; not a natural public win. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Commit(s): d9ab4c4fc (2-client cases PASSED 2026-11-11, see Results)
- Check: with 3 clients (so the expedition room stays open), disconnect a non-master during Ending and rejoin after the return token; it must leave within about 5 s. Confirm the original three-player headset win no longer leaves anyone aboard.
- Needs human: headset repro. The 3-client rejoin case can be run with the bridge (see testing-tools) when a third account is configured.
- Priority: normal

### Finale defenders, loot, gold crate, water, walls: headset feel (0a062e440, 2b8c0d3e8, a38008a7b, 3a3056c87, 1f35eb82b) — queued 2026-11-11, manual-only
- Regression 2026-11-13: Applicable automated/live loot, payout, defenders, water and fall-recovery portions freshly rerun PASS; human acceptance stays pending. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED (manual-only)**: no new headset encounter, economy-balance, splash/fog/audio or climbing/warp-feel evidence. Previous automated results retained. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Commit(s): as listed (automated parts PASSED 2026-11-11, see Results)
- Check: tower spawn presentation and defense difficulty; loot placement taste, roof/tower reach, resource balance; coin shower into the depositor, deposit sound and +100 text as seen by others; water splash/bubble/muffle levels and teal fog in a headset, Quest fog cost; invisible-wall contact, hand-climbing the wall and the instant fall-recovery warp. Economy balance at 100 coins per bar.
- Needs human: all of the above.
- Priority: normal

### Zombie hit shapes: in-game throws and swings (dcd2f9e1, 62ea1a73) — queued 2026-11-10, manual-only
- Regression 2026-11-13: Current-tree GrabbablePlayChecks rerun PASS; natural visual fit and headset throws/swings remain pending. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED (manual-only)**: animated-enemy visual fit and natural throws/swings were not tested with a headset; prior fixture evidence retained. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Commit(s): f073d29a4; fixture fixed in b76f4618f (automated part PASSED, see Results)
- Changed: zombie hit shapes follow animated bones (all five enemies); thrown items hit the earliest-contacted enemy; Spider head hit radius 23 cm.
- Setup: 1 client in Play Mode (headset preferred): throw/swing bat and gold bar at each enemy type, including crowded pairs and a locked (just-hit) zombie.
- Check: hits land on visible limbs/head; a throw hits the nearer of two zombies; deflect direction sane; held-swing feel unchanged.
- Needs human: all of the above (visual fit on animated enemies), throw and grab feel in headset.
- Priority: normal

### Enemy tuning + late-join flares: in-game (22262f75, 98ec9f5b, 6f4c3cba) — queued 2026-11-10, manual-only
- Regression 2026-11-13: Current batch suites plus actual ground/moving-train enemy deaths and real late-join original-deadline expiry PASS. Known world-space flare behavior retained; headset feel remains pending. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED for full acceptance**: current natural death/40-second flare/ground-and-moving-train late-join scenario was not run; prior inert-avatar fixture does not cover it. Speed/kill-trigger feel also needs a headset. Known world-space flare behavior remains out of scope. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Commit(s): 1ea23d07f; fixture fixes 136fe60ea, 8005743a5 (automated part PASSED, see Results)
- Changed: speeds rebalanced, retreat 4–12 s, all zombies fear 40 s flares, flares restored for late joiners, smaller kill triggers, slower spider stalk.
- Setup: 2 real game clients: client A dies (flare), client B joins late, once on the ground and once aboard a moving train.
- Check: B sees the active flare with correct remaining life; enemies of every type avoid it; flare expires ~40 s. Known gap: flares are world-space, so one dropped on a moving train is left behind (not train-relative); record what B sees, do not treat as a fix target here.
- Needs human: speed/kill-trigger feel in headset; real-game late join (not covered by the batch fixture, which uses inert avatars).
- Priority: normal

### ZombieHook pull: no snapback, hands/gear clear of surfaces (6f27966d, 7b5dad14) — queued 2026-11-10, manual-only
- Regression 2026-11-13: 24 pull + six surface batch cases and an actual real-network bare-hand ground pull PASS; full live held-gear/train/curve/headset acceptance remains pending. See the [fresh scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md).
- Queue run 2026-11-12 evening: **BLOCKED (manual-only)**: no worn-headset ground/train/curve pulls or held-item release coverage; prior batch PASS does not establish natural hands/gear clearance or comfort. Shared Unity/commit metadata and evidence are in the newest Results entry.
- Commit(s): see git log "Port Duckz ZombieHook pull"; batch fixture re-run on HEAD 2026-11-10 PASS (see Results)
- Changed: hook pulls no longer snap the VR player back; while pulled, hands, held weapons/items and a hand-held bag stay out of floors and walls (locally and as sent to others), releases mid-pull drop instead of fling, and observers keep dropped items above the floor.
- Setup: 2 clients (A in headset). Force `RailHook` spawns. Pull A (1) on the ground near a wall, (2) standing on the moving train, including near the cabin walls/rear, (3) on a curve. Repeat with bare hands, nail bat, gold bar, and a hand-held bag; release the item mid-pull once inside and once outside the train cargo volume.
- Check: no rewind after the pull; no hand/bat/bag visibly inside geometry for A or B; train travel neither lost nor added; item released on the train becomes cargo and rides along, one released off it falls in place; B never sees a dropped bat tip through the floor.
- Needs human: headset hand/arm feel during and after the pull, comfort aboard the moving train.
- Priority: normal


## Results

### Bridge defender approaches and 60-second hold — PASS (scoped), 2026-11-17

- Unity 6000.3.24f1, one authenticated private-room editor client, keyboard rig.
  Tested working tree based on `9952d5c8d3df992cebfc988ab8db75f3dceed8df`, including
  uncommitted finale refresh. No gameplay fix was required.
- Production lever grab/hold via `RailBridgeHoldFixture`; actual Photon retained
  state, room spawning, enemy brain, pathfinding and collision locomotion.
  Train/operator initially placed through test tools; no enemy teleport or bridge
  progress injection. Existing enemy-invincibility test option kept the operator
  alive, so this does not establish combat difficulty.
- Both tower defenders reached within 2 m of the control approach at **11.415 s**
  accumulated holding; minimum distances **0.598 / 0.660 m**. They initially walked
  toward the objective without seeing the operator, then switched to Chase.
  No sampled movement discontinuity above 3 m was detected.
- Exactly one `bridge-wave-0` appeared, first observed at **30.209 s** accumulated
  work (10 Hz observation plus authority/Photon update latency). Two enemies before,
  three afterward; no duplicate through completion. The third naturally reached
  controls at **36.958 s**, minimum **0.597 m**, no detected teleport discontinuity.
- Bridge reached **60.000 s / 100%**, deck local rotation **(0,0,0)**; held at 60
  and stopped moving during the subsequent five seconds. Small physical-fixture
  hold gaps did not count toward progress.
- Console contained Meta XR no-headset `ErrorFormFactorUnavailable` and LCK's
  unsupported-editor-graphics-API error. Neither prevented these gameplay checks.
- Stopped Play and restored saved ordered-route/invincibility, scenario and
  quick-start preferences. [Full trace](../output/bridge-finale-live/trace.txt).
  Reproduce with `RailFinaleLiveChecks.Start()` and clean up with `.Stop()`.
- Still queued: deliberate pause/resume around 30, real-peer agreement, late join,
  master handoff, replay, headset/build lighting and natural difficulty.

### Queue run 2026-11-16 — scoped desktop results and remaining acceptance

**32 PASS scenario groups / 1 INCONCLUSIVE group / 4 BLOCKED prerequisite families.** These are scoped groups, not 65 completed queue entries. All 65 Pending entries retain their unaccepted criteria. The [coverage ledger](validation/playtest-queue/2026-11-16/coverage-ledger.md) maps every entry to fresh evidence and remaining work; the [scenario rollup](validation/playtest-queue/2026-11-16/scenario-rollup.md) defines the counts. Some objective subcases remain unrun and are explicitly distinguished from Needs human.

Unity 6000.3.24f1; one workstation with up to three distinct authenticated MPPM clients on the real Photon/backend, plus Quest 3 build/install and an unsuccessful tracking-gated device survey. Starting HEAD `789a5be13f11d220f9b09a520a16fc204d169c36`; final gameplay HEAD `b9bbf51d4`, with pre-existing working art preserved. Work stayed on main; no push or diagnostic report inspection. Related subscenarios shared live worlds; this is not a claim of an Edit Mode reset between every subcase.

- **Seven production defects fixed and rerun:** spider knock-off reattached around convex edges (`217f5a65d`); baked-fixture suppression used editor-only light metadata and failed Android compilation (`33a080740`); remote revive effects followed before avatar interpolation (`cd39a40e0`); furnace prefab forced its debug level (`6fbeb4eda`); menu teardown played a close cue through a newly created audio manager (`f7c6e0099`); the active Compound terminal lacked the shared store (`00945ca91`); default-duration throttle regrip rejected the local actor's exact released token before its echo (`b9bbf51d4`). Each has focused before/after evidence linked from the ledger. No whole-feature/headset acceptance follows from these fixes.
- **Three separate fixture/authoring corrections:** real-client spider fixture (`730f48edc`), persisted finale prefab overrides (`889806182`), and bridge-hold standing-point placement (`ac918293b`). These are not three additional gameplay defects.
- **Fresh scoped passes include** current lighting lifecycle and Android build, real standard-goods purchases/charges and cleanup, coal/fuel/late-receipt behavior, physical simulated throttle/default return/contention, moving radio contact/disable, wheel coasting/moving rejoin, controlled audio curves/mixer/ticks, current block geometry/visibility/loot samples, bridge crash/success/reconnect, three-client revive feedback, natural public journey/finish and original-token stranded return. Report-only results evidence includes actual actions and pure award checks; repel exclusivity remains confounded, without a production change.
- **INCONCLUSIVE:** station upper-stair traversal fixture. Hand/posture contact interferes with the timed jump; the evidence does not establish a collision defect. No speculative station geometry change was made.
- **BLOCKED:** owned bundles lack entitlements, independent simultaneous rescues lack a fourth account, native LIV is unsupported by the current DX12 path, and Quest positional/head tracking is unavailable. The current device attempt produced zero valid frames; all of its performance samples are excluded. Build/install success is not device-performance acceptance.
- **Needs human / still open:** natural headset movement and reach, stair comfort, visual/readability and acoustic quality, microphone/voice, real cross-machine jitter, manual-only entries, plus the explicitly unrun objective subcases in the ledger. No fabricated entitlement, balance, score or device result was used to close a criterion.

Final restoration, device build provenance and cleanup are recorded in [run closure](validation/playtest-queue/2026-11-16/run-closure.md). Original November 16 boolean preference backups were lost when Unity cleared Temp during restart; restoration uses the exact recovered four-block route and last recorded logical flags, not a claimed byte-identical preference restore.

### Synthetic four/eight duck-and-light Quest comparison — 2026-11-14
- **PASS for requested capture/fixture validation**, not full multiplayer or all-view performance acceptance. Explicit user request; one Quest 3, Unity 6000.3.24f1, uncommitted on main over `61ea0c9452024cf78a538391f796b1a7b6055caf`.
- Built and installed the same Development APK for four full surveys in order 8/4/4/8. Production default rigs use arm IK/fingers and synthetic lips; totals include one local rig. Four/eight authored lights sweep the scene. All 96 measured views pass coverage/focus/tracking checks; 82,942 measured frame samples.
- Generally 72 FPS in both workloads. Forward Skyscraper reached a 70 FPS view median in one four-duck repeat; worst GPU p95 14.0/13.9 ms is near the 13.89 ms budget. Mean of matched-view median times increased +0.16 ms CPU / +0.17 ms GPU, with material repeat/position variation. [Full evidence](validation/quest-crew-load-2026-11-14/README.md).
- Initial obstructed-camera fixture attempt cancelled/excluded; stable local visual hand poses/head hiding added, rebuilt and both workloads repeated. Final app stopped, request absent, recorder disabled and original device properties retained. Opt-in fixture is inactive on ordinary launch.
- No other queue items were run or closed. Real eight clients/voice, interior traversal, release performance, atlas before/after gains and natural headset appearance remain unverified.

### Older-queue regression 2026-11-13 — execution complete

**58 PASS / 3 FAIL / 2 BLOCKED / 18 deliberately excluded scenario groups.** Human-only acceptance stays in Pending. The counting units, every fresh verdict, raw evidence, rejected fixture attempts, genuine blockers and checks not reached are in the [scenario ledger](validation/playtest-queue/2026-11-13-regression/scenario-ledger.md). [Exact scenario-level exclusions](validation/playtest-queue/2026-11-13-regression/run-notes.md#deliberate-exclusions) come only from the November 12 evening run and its linked evidence. Older Results, including previous Claude passes, were rerun rather than grandfathered.

Unity 6000.3.24f1; starting HEAD `3c64b55a44e9c118c176838fa2e19136bd4b3eec`, final gameplay HEAD `8379de0f15405a0541434dc20037fd929d8161f3`, with unrelated working-tree edits preserved. Three distinct authenticated desktop MPPM clients on the real Photon/backend, plus current-tree disposable batch processes. No push, branches, stashes, unrelated reverts or diagnostic report inspection.

- **Fixed and rerun:** Compound cleanup deleted purchased flashlights/bags after charging. Production fix `8379de0f1` retains current purchases with room/run/buyer metadata and clears them at the appropriate departure. Three-client purchase/charge, peer/late-join/handoff, buyer leave, private departure, both expedition stores, floor cleanup and public-return reruns pass. [Fix evidence](validation/playtest-queue/2026-11-13-regression/store-cleanup-fix.md).
- **Remaining FAIL groups:** the broad rig fixture still fails its legacy lever stage after three corrections (`8522aae9f` fixes its earlier movement/grab assertions); physical-throttle hand-switch/regrip/remote-palm/right-end acceptance remains incomplete; several desktop traversal/pursuit routes do not finish acceptably. These are failed acceptance/harness groups, not three diagnosed production defects. Scope, attempts and unexecuted downstream steps are explicit in the ledger; no fourth correction or speculative geometry/VR redesign was applied.
- **Remaining genuine blockers:** the three accounts lack the new bundle entitlements, and no Player4 account is configured for independent simultaneous rescues. Owned equip/cooldown/persistence and four-client acceptance remain pending; no ownership or balances were fabricated.
- **New remaining-scope passes:** real Compound/cold-Login late warmup; natural flashlight bag insertion; fuelled driver/master disconnect braking; ordinary pickups after throttle; independent peer reverb/unload; six-block carried-item agreement; live loot settling and scoped collision traversal; full-route odometer; traced nose finish margin; moving/victory/handoff/late-progress/post-completion revive feedback; ground and moving-train natural enemy death/late flare; real ground Hook pull.
- **Older passes freshly rerun:** all five batch suites, menus/rooms/button actions, private launch ownership, special-block loot/handoff, depositor-only gold/replay, actual results attribution/awards, player recovery/water, first-hold/fresh-run defenders, normal public return and cut return-control connection. All details are in the ledger; no whole-feature acceptance is implied.
- **Environment/hygiene:** an OpenXR teardown crash required the user's restart; no crash report was opened. Existing microphone/output-device, LCK DX12 and MRUK errors limit listening/device acceptance. Related network/traversal subscenarios shared worlds; this run does not claim an Edit Mode reset before every numbered group. Final cleanup is now verified after the user's Ready response: Edit Mode, scenario NotRunning/Default, original logical test preferences restored, fixture inputs disarmed. [Cleanup readback](validation/playtest-queue/2026-11-13-regression/cleanup.txt).

### Queue run 2026-11-12 evening — current queue, up to three authenticated clients

Unity **6000.3.24f1**, verified main project `X:/Work/duck-rails/Assets`; MPPM desktop clients on real Photon/backend accounts, one workstation. Tested starting at `5f4a6d72e199aa7304018a9d56b412529e1dc868` with the existing uncommitted working tree, then fixes `79b69296b` and `3c64b55a4`. Timestamps in raw evidence are UTC November 13; the run began on November 12 local time. [Evidence and limits](validation/playtest-queue/2026-11-12-evening/run-notes.txt).

**Full-entry outcomes: 1 PASS / 0 outstanding product FAIL / 31 BLOCKED for full acceptance.** BLOCKED includes manual/headset prerequisites, missing owned bundle products/fourth account, and incomplete scenario coverage explicitly identified per entry above; it does not mean every remaining desktop case was attempted. Partial PASS results below do not close those entries. Two observed product failures were fixed and passed reruns. The broad physical rig fixture still fails before useful throttle coverage, with no retained fixture edit. No diagnostic reports were read and nothing was pushed.

- **Hand flashlight stays on after release — PASS, complete (queued November 12; 2 clients).** Actual hand pickup and use input turn it on; release leaves it lit, regrab preserves light, a full 60.0-second abandonment turns it off, and the component's stash path turns it off. Peer state agrees throughout. Off AudioSources were observed playing on both clients; the remote audio sample used a supplemental timeout seeded near expiry with the observer nearby. Stash coverage invokes the real component path, not natural backpack insertion. No human acceptance was required by this entry. [Owner](validation/playtest-queue/2026-11-12-evening/flashlight-main.txt), [peer](validation/playtest-queue/2026-11-12-evening/flashlight-player2.txt), [owner sound](validation/playtest-queue/2026-11-12-evening/flashlight-off-sound-main.txt), [peer sound](validation/playtest-queue/2026-11-12-evening/flashlight-off-sound-player2.txt).
- **Furnace coal intake — FAIL, fixed in `79b69296b`, rerun PASS (2 clients).** Actual thrown coal settled at insertion-relative Y -0.27 m, below the old 0.22 m acceptance half-height despite being inside the firebox. Increased only the half-height to 0.32 m; below-floor coal remains excluded. Four actual deposits now pass while stopped, moving, after master handoff, and at empty fuel, with exactly-once credit/removal and resumed driving. Both clients emitted one burst and popup per receipt; a late third client replayed neither. Solo full/empty/refuel emission and fuel segments pass. Headset scale/readability remain queued. [Before](validation/playtest-queue/2026-11-12-evening/coal-before-main.txt), [rerun](validation/playtest-queue/2026-11-12-evening/rails-coal-main.txt), [peer rerun](validation/playtest-queue/2026-11-12-evening/rails-coal-remote.txt), [late join](validation/playtest-queue/2026-11-12-evening/observer-late-player3.txt).
- **Motel route readiness — FAIL, fixed in `3c64b55a4`, rerun PASS (up to 3 clients).** Six stale null baked-renderer references prevented WorldReady and timed out departure. Re-exported the existing Motel lighting scene through its normal exporter: 1,970 bindings become 1,964 valid bindings, with bake scene and lighting companion unchanged. The same five-block route now starts and the late joiner becomes ready. All three clients reconstruct the same 142 remaining cargo identities/tokens after one deposit, matching geometry/reverb inventories and enemy IDs. This covers Container Yard, Motel, Convenience Store, Farm, Skyscraper and route special blocks; not natural traversal/pursuit acceptance. [Before](validation/playtest-queue/2026-11-12-evening/motel-load-before.txt), [after](validation/playtest-queue/2026-11-12-evening/motel-load-after.txt), snapshots `snapshot-late-{main,player2,player3}.txt` in the evidence folder.
- **Throttle, displays and environment — PASS scoped desktop portions.** Solo and a non-master driver with 3 clients: requested lever progress 0.2/0.6/1 gives 0/2/4 m/s, release clears target/lease, rendered return has intermediate frames (~0.20–0.22 s sampled), heartbeat expiry brakes. Remote values and late-join distance match; 0 m departure and seeded 1000 m bridge-approach display pass. Physical grip/keyboard input remain unverified: broad rig fixture attempts failed at support/movement setup, and its experimental adjustment was reverted. All 25 live reverb zones follow the actual local listener inside/outside; two expedition mirror cameras create independent 256×256 targets nearby and stop at distance. These are runtime plumbing checks, not audible/visual/headset acceptance. [Drive](validation/playtest-queue/2026-11-12-evening/variable-drive-network-main.txt), [solo](validation/playtest-queue/2026-11-12-evening/solo-runtime.txt), [environment](validation/playtest-queue/2026-11-12-evening/environment-runtime.txt).
- **Revive feedback — PASS scoped 3-client portions.** Existing real-client revive suite passes handover, brief dropout/grace and one rescuer with two overlapping downed players. All three clients show rising progress-audio pitch and receipt-triggered completion audio/four emitting particle layers. This does not close moving-train/victory-ride, independent simultaneous rescues, late-progress join, handoff/replay cleanup or headset acceptance. [Suite](validation/playtest-queue/2026-11-12-evening/revive-main.txt), observer logs in the evidence folder.
- **Results board — PASS controlled transport/assignment portion, report-only (3 clients).** After real host handoff and controlled success, all peers show matching Coal/Gold/Revives/Downs and distinct Personal Space Enforcer / Big Spender / Emotional Support Duck titles. Repels/spent inputs were seeded through the normal stats request; coal came from an actual deposit. Natural action attribution, failed runs, reconnect-before-finish and threshold feel remain pending. Evidence: `results-{main,player2,player3}.txt`.
- **Stranded return — PASS targeted 3-client desktop reproduction.** Non-master disconnects during Ending; third client retains the old room; after the main client returns and the room contains a return token, reconnect enters Returning/Leaving immediately and reaches ready Compound 2.70 s later. Seeded public-return state in an isolated private room and disabled third-client session updates deliberately retain that room; this is not the original natural headset win. Third client was re-enabled before shutdown. [Reconnect trace](validation/playtest-queue/2026-11-12-evening/stranded-player2.txt), [retained room/token](validation/playtest-queue/2026-11-12-evening/stranded-player3.txt).

Cleanup: scenario stopped, editor in clean Compound Edit Mode, saved four-block order restored byte-identical, original desktop preference restored, bridge commands cleared. Script compilation has no failures. Final error query contains the pre-existing LCK unsupported graphics-API startup error, not an empty Console. Headset/Quest, original natural repros and missing ownership/account prerequisites remain **queued for playtest**.

### Queue run 2026-11-12 — solo and two-client live portions (Unity 6000.3.24f1, MPPM, tested at 81b4538ab + fix bfcd86651)
Main editor via Unity MCP, Player2 via `RailRemoteBridge`, real Photon cloud and backend, desktop simulated rig. Keys, L3 and Start were not injected. Test Block Order was set temporarily to Convenience Store, Farm and Skyscraper, then restored byte-identical. Full evidence: [2026-11-12 run](validation/playtest-queue/2026-11-12-queue-run.txt).

- **Train aboard margin — PASS** (solo, automated part). Three runs, each with the bridge seeded fully lowered and the train driven across it. Head hanging off the side at train-local (2.6, 1.0, 0): tight box false, padded true, "Escaped", no rescue teleport over 6 s. Head on the nose at (0, 1.5, 6.0): "Escaped", no teleport. On the ground 25 m away: "Didn't make it", teleported to the deck on the finish frame. An earlier attempt was invalid (wrong rig lookup) and is not counted.
- **Expedition render warmup — PASS** (desktop part). First departure: 37 states in 1.93 s, no time limit, no leftover "Warm proxies" or extra camera, helicopter lights on. Second departure in the same process: 0 new states, warmup entirely under a covered ALL ABOARD veil. Late join aboard the running train (Player2 from a compound): veil covered from join to release, "Getting the train ready..." shown, 2 new states, no uncovered Expedition frame. Not covered: a cold join from Login (the process was already warm). On the second departure "Getting the train ready..." did not appear, because world readiness arrived on the same frame the hold ended. This is cosmetic.
- **Compound Discord and Rate buttons — PASS** (desktop part). `PressableButton` presses fired each action once. Repeat presses and direct calls within 3 s hit the guard. RATE GAME status read "Store opened. Find Ratings and Reviews.". Same room before and after, no room request. The agent did not see the browser window itself.
- **Block loot consistency: Convenience Store, Farm (incl. pond/playground), Skyscraper — PASS** (2 clients). The 91 coal/gold items match on both clients: view IDs, spawn tokens and positions agree within 1 cm, all asleep on real colliders. The 12 enemies have matching IDs. After a late rejoin there are the same 103 items with no duplicates. Car-wash spin is cosmetic: 7 spinners with no colliders, and no rigidbodies or joints in the block. The Farm swing has no physics.
- **Farm loot candidates — FAIL, then fixed in `bfcd86651`.** An isolated editor check of all candidates found 8 failures: the 7 flagged roof/hay items, plus Gold 087, which now clips the orchard retaining stones. `RailFarmPolishChecks.RepairLoot` moved only those markers (mostly 9 cm up out of the walkable roof). Re-check: 222/222 pass, pools unchanged. Convenience Store (235) and bridge finale (54) pass. Skyscraper reports 18 floor-0 items overlapping the ground slab. Live, those items spawned asleep with a 0.00 m gap, so they were left unchanged (noted, not a fix target).
- **Attached throttle multiplayer — PASS** (lease level, 2 clients, simulated-rig hold path on the same shared lease). Results:
  - Peers see the driver's actor and a matching lever (35°/325°).
  - A second client's press is denied, and a key still held when the driver releases does not steal control.
  - A non-master can drive.
  - Lease release and train stop on each of: driver focus loss, blocked input (menu), driver down (seeded state, because normal downs are rejected aboard), driver disconnect (about 0.3 s), and master disconnect. On master disconnect Player2 became master and cleared the orphaned lease in about 0.1 s.
  - Rejoiners see OFF without re-acquiring, and a late joiner sees the other player driving.
  - Not covered: the remote attached-hand visual, real hand grip, keyboard T, and ordinary pickups afterwards.

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

### L3 test panel — superseded 2026-11-17
Old L3/wrist/desktop panel acceptance is dropped because the user replaced it
with the admin pause page. Historical November 11–13 evidence remains in Results.


### Skyscraper Works production atlas integration — superseded 2026-11-14
The user requested restoration and revision of the original geometry before another Blender pass. The previous 394-chunk / 228,866-triangle version is retired; use the reduced-source entry above. Historical [integration evidence](validation/skyscraper-atlas/production-integration.md) remains available.

_(empty)_
