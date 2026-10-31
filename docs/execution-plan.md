# Duck Rails — execution plan

Handoff date: October 28, 2026. Read `game-design.md` for decision status and scope.

## Planning basis

- [x] October 31 editor/desktop throttle convenience: hold T for GO, release for STOP in Keyboard or VR mode, retaining shared lever ownership. Unity compilation, 28 shared-rig checks, 17 drive checks and a solo live Photon/lever hold-release check passed. See [evidence and limits](shared-rig.md#hold-t-throttle-shortcut--october-31). Actual headset and multi-client shortcut acceptance remain separate.

October 30 shared-rig follow-up: replace the DesktopPlayer gameplay launch path with simulated poses/buttons on the existing DuckPlayer; preserve Quest Link and allow input switching during Play. The accepted controls and verification workflow are in [shared-rig.md](shared-rig.md). Older DesktopPlayer evidence below remains historical and does not establish acceptance of the shared rig. Headset and multi-client acceptance remain required.

Shared-rig editor evidence: all 11 quick suites and the solo real-frame hand/button/lever check passed. Two authenticated editor clients then passed stopped/moving-train coal and master-handoff checks (21:53:42 UTC). [Recorded evidence](shared-rig.md#recorded-editor-evidence-october-30). Worn Quest Link switching and target-headset acceptance remain open; these editor results do not close the headset milestone boxes.

October 30 Voice Patrol configuration: integration retained, with GameManager's **Enable Voice Patrol** startup switch off by default. Bootstrap and initialization require explicit opt-in. This does not change Photon voice requirements or complete multiplayer/headset voice acceptance.

Validation: source diff/whitespace checks passed. Unity MCP initially verified `X:/Work/duck-rails`, then became unavailable during script reload (no fresh discovery files), so post-change editor/Play Mode verification remains pending. The diagnostic inbox query failed DNS resolution; three local reports remain open and were not resolved by this configuration change.

October 30 testing-tools follow-up: [testing-tools.md](testing-tools.md) records the development panel, report capture/upload pipeline, automatic state checks, presets and unified runner. Two real desktop clients passed the dashboard-launched coal/handoff test. Production backend deployment and Quest panel acceptance remain pending; this tooling does not close the existing multiplayer/headset acceptance boxes.

Build on Duckz's working framework. Keep Photon Realtime. Implement room/session architecture before the full expedition, with a small real moving-platform/shared-item test rather than an empty-room-only prototype.

Scheduling proposal: four-week closed alpha, six-week public V1. This is a milestone sequence, not a guaranteed estimate. Team capacity, source dependencies, and platform submission lead times are unknown. Weeks assume existing systems are portable and some content work can proceed alongside engineering. Re-estimate after source inspection and after the first session milestone. Do not silently fund a networking rewrite by shortening verification.

## Phase 0 — source and project readiness

Deliverable: runnable baseline plus a verified reuse inventory.

October 28 authentication progress: separate Unity and simplified backend copies are confirmed; client/backend sign-in compatibility changes compile and local backend tests pass. See `authentication-setup.md`. Justin subsequently confirmed backend and Photon authentication setup completed. This milestone targets DesktopPlayer first; headset acceptance is deferred. The broader readiness items below are not complete.

Analytics follow-up: separate Duck Rails Android keys are now entered, inherited gold polling is removed, and Rails session events are instrumented. Editor previews are local only. Actual dashboard delivery and Windows platform configuration remain pending; see `analytics-setup.md`.

- [ ] Obtain the intended Duckz source location and confirm whether this repo will be a full project fork, an imported subset, or another supported arrangement.
- [ ] Read source/project guidance before copying or changing anything. Preserve the original Duckz project.
- [ ] Record Unity version, render pipeline, target headsets, SDK/package versions, required native plugins, and build steps.
- [ ] Identify actual Realtime/PUN APIs, voice stack, room manager, authentication, item authority, player identity, save/commerce services, and scene loading.
- [ ] Locate locomotion, helicopter/platform motion, backpacks, grabbing, enemies, crawlgraph tools, flare, and cosmetics.
- [ ] Build and run the unchanged baseline on at least two intended headsets.
- [ ] Record known defects separately from regressions introduced by Duck Rails.
- [ ] Establish ignored local configuration/secrets, asset and large-file handling as appropriate to the actual project.
- [ ] Confirm release platform, account/app identifiers, audience/voice eligibility, and store preparation requirements early.

Acceptance: a clean checkout can be configured and built using documented steps; two clients reproduce the known working interactions. Update the design's reuse inventory with actual component paths and limitations.

Do not copy credentials into tracked documentation. Do not claim systems verified merely because they compile.

## Week 1 — session architecture with real state

Deliverable: friends can form a crew, depart, join halfway through, see the same moving platform and objects, and return together.

### Decisions confirmed October 28
- Standalone Login art and random camera spots; authenticate/connect/join before transitioning to Compound. No Quackville use.
- Twelve-player public compounds, automatic initial compound placement; eight-player expeditions.
- First platform occupant starts twenty seconds. Entry/exit cannot reset a nonempty timer. Empty cancels; re-entry starts twenty seconds again.
- Photon Master Client coordinates changes and passes responsibility on departure; initial private creator launches manually.
- Authenticated identity; 120-second expedition reconnect reservation while the room remains alive; shared items persist without restoring duplicate disk-saved supplies.
- Public return after success or failure: crews of one to four connected players try an existing compatible compound with whole-crew reservations and bounded fallback to a fresh compound; crews of five to eight use a fresh compound. Keep one coordinated destination for the crew. Runtime implementation is present; multi-client acceptance remains pending.
- All gameplay rooms use five-digit numeric codes; the private station joins public or private rooms by code. Automatic creation handles occupied codes without matching an unrelated crew.
- Private staging/run/return retain the same room and code. Last connected player leaving destroys the room immediately.
- DesktopPlayer is the current test target. Backend/Photon setup is reported complete; multiple actual clients are still required for network acceptance.

### Implementation and evidence
Private replay follow-up (October 29): fixed the reproduced four-supply duplication by requiring old networked cargo to be removed before staging readiness and restart. Cleanup runs on each item's controlling client and includes inactive objects. Eight authenticated solo rounds/seven immediate replays passed with success/failure and inactive bag/bat coverage. A subsequent two-client check passed four rounds/three replays with distinct authenticated accounts, both supplies owned by the non-master, and active/inactive cargo: both clients observed zero old supplies before staging readiness and exactly two fresh supplies per run. See `week1-session.md` for repeat instructions and evidence. Actual held/stowed interactions, disconnected owners, master-loss and headset acceptance remain unchecked.

Public return follow-up: `RailCrewReturn` keeps a temporary authenticated coordination connection while one gameplay client searches and reserves an existing compound. Search failure/timeout or coordinator loss selects a shared fresh allocation. Five-to-eight-player crews skip the search. Reservation cleanup uses retained per-user deadlines and a compare-and-swap update to preserve concurrent crews. Schema 4 separates this protocol from older clients. Local policy/lease checks and authenticated single-client return evidence are recorded in `week1-session.md`; the group acceptance boxes remain unchecked.

October 30 stability follow-up: three authenticated desktop processes passed four public cycles with two departing crew and one shopper left behind, adverse-network train controls, master transport timeout/automatic rejoin, unique bound avatars, cargo cleanup and voice-room membership. Scene-transfer avatar duplication, reconnect destination loss and a cached-avatar binding race were fixed. All 547 editor checks passed. See [multiplayer-stability.md](multiplayer-stability.md) for evidence. One/four/five/eight-player capacity cases, return-coordinator loss, VR/audio quality and full public gameplay acceptance remain open; these tests do not close the broad boxes below.

Fuel consumption follow-up: motion and starting fuel are retained atomically in schema 3 room properties, with deterministic distance-based consumption and a zero-fuel stop. Runtime/editor compilation and six new local regression checks passed; two-client fuel agreement, leave/rejoin, and master-departure acceptance remain pending. Coal insertion is not implemented by this change.

See `week1-session.md` for source paths, controls, validation results and pending multi-client evidence. Implemented work is kept separate from milestone acceptance; unchecked scenarios below remain unverified until exercised across real clients. Desktop follow-up adds the tagged Meta-account window from the referenced Classic commits, a Player2-configured Two Ducks scenario, a compact centered status banner and carriage-relative footsteps. Local regression checks passed; two distinct accounts subsequently authenticated together during the October 29 cleanup check.

### Work

- Editor testing convenience implemented: **Two Ducks Quick Start** routes the Two Ducks editors directly into one hidden expedition after normal authentication. Main-editor selection now reads the active scenario configuration rather than its unreliable runtime tags. Six regression checks and the runtime/editor build passed; live main-editor quick-start entry is verified. Justin confirmed the combined two-client launch works. Live disabled-toggle behavior still requires verification. This does not change production boarding requirements or satisfy the acceptance scenarios below.
- [ ] Define session phases: compound/staging, departing, running, ending, returning. Define valid transitions and who can request/commit them.
- [ ] Give each expedition a run identifier so completion and transfers can be deduplicated.
- [ ] Build a gray-box public compound and occupancy/countdown platform.
- [ ] Freeze and validate the departing roster at zero; transfer only that roster and keep the source compound usable.
- [ ] Implement separate private creation with five-digit input and server-confirmed collision handling.
- [ ] Distinguish Create from Join; show occupied, missing, full, incompatible, and connection-failure states.
- [ ] Keep private staging/run/return in the same room where compatible with existing architecture.
- [ ] Support late entry to the run with a loading stage before spawn/interaction.
- [ ] Replicate actual moving-platform pose, sample fuel value, selected test block, one bat, and one loaded backpack.
- [ ] Define current-state reconstruction instead of depending on historical events a newcomer did not receive.
- [ ] Implement temporary success/failure controls and group return.
- [ ] Extend public return for crews of one to four: coordinate an existing-compound search, reserve the whole crew, and fall back to a fresh compound within a bounded time. Keep fresh-compound return for crews of five to eight.
- [ ] Handle departures during transfer, interrupted loading, and loss of coordinating client without an indefinite loading state.
- [ ] Verify voice reconnects/routes correctly through the session changes.
- [ ] Perform an early loose-item acceleration/braking experiment to expose a separate major risk.

### Acceptance scenarios
- [ ] Seven players can leave while an eighth remains shopping in the compound.
- [ ] Repeated platform entry/exit cannot reset a nonempty group's timer indefinitely.
- [ ] Two users attempting the same private code do not silently create/join the wrong expedition.
- [ ] A friend enters a public expedition code at the private station and appears aboard the correct train; exercise automatic code collisions and crew transfers.
- [ ] A friend enters a running private code and appears aboard the correct moving train with current world state.
- [ ] A full run rejects a ninth player cleanly.
- [ ] Reconnection does not duplicate the player's bag or supplies under the selected policy.
- [ ] Coordinating-client departure produces the documented recovery behavior.
- [ ] Crew returns together; private code remains usable while the room remains alive.
- [ ] Public success and failure returns: crews of one and four enter an existing compound with sufficient unreserved capacity; insufficient capacity, simultaneous arrivals and search failure fall back without splitting the crew. Five-player crews use a fresh compound. Verify coordinator loss during search and transfer, and reservation cleanup.

Schedule gate: if transfers or state reconstruction are unreliable, continue this milestone and update the release forecast. Do not build many scenes around a provisional lifecycle.

## Week 2 — one complete scavenging stop

Deliverable: a networked departure, coal search, rescue, and escape using actual Duckz interactions.

### Current commissioned scope — confirmed October 29

Implementation and three-client DesktopPlayer evidence are recorded in [week2-session.md](week2-session.md). Checked implementation items indicate the scoped desktop delivery; eight-player, headset, subjective interaction feel, and human voice-intelligibility acceptance remain separate below.
- Complete Week 2 plus the bridge finale described under Week 3, including its automatic dawn ride and a shared world-space results board for success and failure. Other Week 3 content expansion and commerce are excluded; persistent escape-count implementation remains later work.
- Treat Week 1 as the user-verified working baseline. Preserve its behavior and test regressions introduced by this work without reopening the entire milestone.
- Develop and validate using DesktopPlayer with up to three configured authenticated clients, Unity MCP, and Unity CLI/batch checks. Meta Quest 3 is the launch target; Justin will perform VR testing afterward. Eight-player and headset acceptance are not established by these desktop checks.
- Duck Rails Unity and backend repositories may be changed as needed. Duckz and Classic remain read-only references.

Additional finale/results work in this scope:
- [x] Add advance bridge warning signs and require manual braking. Hitting the raised or incompletely lowered bridge explodes the train and fails the team; no protective automatic stop.
- [x] Complete the Week 3 bridge-button, timed defense, synchronized lowering, finish snapshot, catch-up, and automatic dawn-ride tasks.
- [x] Show the same world-space run-stat canvas after success and failure, with **SUCCESS** or **FAIL** at the top and **Escaped** or **Didn't make it** for every participant.
- [x] Derive overall success from at least one escaped player. Zero escapes, including a pre-finish bridge crash or full-crew-down failure, produces failure.
- [x] Test one-player escape with teammates left behind, downed passengers escaping, zero escapes, bridge crash, full-crew-down failure, and results/group return on both outcomes across the available desktop clients. Three-client private sequences passed; see Week 2 evidence.

- [x] Integrate train platform movement and STOP/GO lever with consistent shared state.
- [ ] Validate the October 29 VR grip-and-hold lever on Quest 3 with two clients: trigger-only handle/base without locomotion pushback, exclusive control, forward travel, pull-back OFF/re-engage while gripping, release braking, disconnect/downing cleanup, and preserved desktop toggles. Implementation does not establish headset acceptance.
- [x] Add physical horn control
- [x] Add coal insertion, consumption, fuel gauge, and fuel exhaustion behavior. coal should act the same as any item. it can be held, dropped, put into bag, if you drop, anyone else can pick it up.
- [x] Make fuel insertion authoritative/idempotent so one coal chunk cannot be counted twice.
- [x] Revalidate non-master furnace insertion on two desktop clients after the October 30 failure: found stale duplicate-avatar lookup, fixed selection of the live pose, and verified normal hand release/physics, stopped/moving insertion, exactly-once +2.5 fuel/removal and explicit master handoff. Fixture player teleports position the hand. Headset and bag-to-hand furnace coverage remain pending. The later stability pass fixed duplicate creation during scene transfer and added PUN-registry uniqueness checks across three clients; see `multiplayer-stability.md`, `week2-session.md` and `validation/coal/`.
- [x] Implement stable dropped cargo, including loaded bags, while moving, braking, and changing item ownership.
- [x] Add simple physical holders and full-size backpack hooks using existing interaction patterns.
- [x] Integrate handheld radio communication with a permanent train station.
- [x] Build one gray-box scavenging block using the existing crawlgraph tooling.
- [x] Use one existing enemy type with temporary retreat on hit and no train access.
- [x] Implement one-hit down, immediate flare launch (no intentional delay), and three-second proximity revive. No dead state or bleed-out: downing persists until teammate revival or quitting. No timed automatic revival.
- [x] Decide crawling, repeated flare behavior, post-revive protection, and a minimum viable full-crew failure rule.
- [ ] Confirm locomotion/flashlight/melee/backpack feel has not regressed.
- [x] Default run starts with 10 fuel already in the train, 2 bats, 2 bags, 2 walkie talkies, 2 helmets, 2 night vision googles, 2 flashlights

Acceptance:
- [ ] Eight players can collect coal, revive a teammate, board, and leave without developer intervention.
- [x] Dropped bats/coal/bags stay with the train; intentionally thrown items leave it.
- [ ] Repeated grabs and acceleration do not produce divergent ownership or cargo positions.
- [ ] Enemies never board and cannot make every boarding point unusable.
- [ ] Revive completion/interrupt and flare lifetime agree across clients, including a late joiner.
- [ ] Distant handheld users and players at the train station can communicate intelligibly.

Record device, client count, network conditions, and reproducible failures. Test beyond a single LAN/editor setup using the existing supported simulation tools where available.

## Week 3 — full expedition and repeatable content workflow

Deliverable: a complete start-to-finish run assembled from authored blocks.

Confirmed scope: twelve gray-box locations, five distinct randomized stops plus the bridge finale, three authored spawn variations per location, roughly twenty seconds or more between encounter areas, and a ten-minute typical-run target. Separate Expedition scene; keep all selected blocks loaded, including collision/gameplay. Internal track bends return to the common connection. Lighting is deferred. See [week3-session.md](week3-session.md) for implementation and evidence; desktop checks do not close headset or unfamiliar-player acceptance.

- [x] Define a stable block format: geometry, graph, track anchors, stop marker, spawn candidates, identifiers, and route tags.
- [x] Create a repeatable editor template, invoke existing bake tooling, and validate missing graph/markers and track alignment.
- [x] Verify graph coordinates after placement; choose constrained placement if arbitrary rotations add avoidable complexity. Editor checks verify translated graph indexing and disconnected components; runtime placement is translation-only.
- [x] Select the route once through session authority and share the exact identifiers/placements with late joiners. Three authenticated desktop clients agreed after host handoff and leave/re-entry.
- [x] Choose the initial block lifetime: load all five selected blocks plus finale and retain them until return. Target-device profiling remains pending; runtime baking remains excluded.
- [x] Avoid intermediate block unloading so players and dropped supplies remain available for the whole run.
- [x] Author all twelve gray-box scavenging prefabs with three spawn variations each (supersedes the two-block initial scope). Individual gameplay playtests remain pending.
- [x] Build the confirmed raised-bridge finale (included in the current Week 2 scope): external button, approximately 60-second lowering interval, and increased local enemy spawns. Enemies remain off the train.
- [x] Synchronize bridge timing and gate passage until fully lowered; handle repeated presses and Master Client changes without restarting progress.
- [x] At bridge entry, atomically capture per-player finish status before any catch-up teleport, then disable driving controls and commit automatic forward travel.
- [x] Teleport only left-behind participants aboard as non-finishers without escape credit; downed players already aboard finish normally. Catch-up teleport does not revive players.
- [x] Add the far-side tunnel and concealed dawn transition. Resolve automatic-ride fuel behavior so the ending cannot stall.
- [x] Keep walking, voice, and horn available during the victory ride; falling off returns the player aboard after a few seconds without changing recorded finish status or reviving a downed player. Decide whether falling itself downs a standing player; do not introduce a dead state.
- [ ] Complete fuel/resource balancing, downtime, and failure/restart behavior. Current budget is 0.05 fuel/metre and +2.5/coal, with six chunks per stop; three-client zero-fuel recovery, private success/replay/crash cleanup pass. Natural ten-minute pacing and crew-size tuning remain pending.
- [x] Add basic in-world onboarding and departure feedback. Compound, train, stop and bridge labels are present; unfamiliar-player comprehension is still unverified.
- [ ] Ensure private and public sessions can repeat full expeditions. Private scene transitions/replay pass on three authenticated desktop clients; four public session cycles with temporary success/failure controls also pass. A full public gameplay run and headset acceptance remain pending; see `multiplayer-stability.md`.

Acceptance:
- [ ] New players understand boarding, fuel, revival, and escape without developer coaching.
- [ ] Selected blocks connect correctly and contain usable navigation without runtime rebaking.
- [x] A late joiner receives the same route and relevant current objects. Three-client desktop leave/re-entry preserved route, cargo, loaded backpack and bridge clock; adverse-network/headset coverage remains separate.
- [ ] Success, failure, and replay all return to a usable group flow.
- [ ] One-player and eight-player runs are feasible under the intended design; tune toward intermediate group sizes as well.
- [ ] Dawn feels like relief and renders acceptably on the actual headset pipeline.
- [ ] All clients agree on bridge activation/completion and cannot drive through the raised bridge.
- [ ] Standing and downed onboard players both finish; standing and downed off-train players both receive did-not-finish. Verify teleporting aboard never changes these results or revives anyone.
- [ ] Bridge entry commits finish results and automatic driving only once, including during authority changes and reconnection.
- [ ] Catch-up teleports and victory-ride falls/returns never grant, revoke, or duplicate escape credit.
- [ ] Non-finishers share the ride and receive an explicit did-not-finish label. Players can walk, talk, and honk; falling off respawns them aboard without blocking crew return.

## Week 4 — external alpha / conditional release candidate

Deliverable: a coherent small game that outside friend groups can play repeatedly.

- [ ] Improve sound, lighting, attack cues, and safe boarding based on observations.
- [ ] Add results with a small stat set and humorous awards; handle ties and zero-action players.
- [ ] Implement bridge-entry finish snapshots and did-not-finish labels on the world-space board. Resolve remaining late-arrival, disconnected-player, onboard-boundary, and ending-stat rules. Downed onboard players are confirmed finishers.
- [ ] Persist escape counts once per eligible player/run; verify duplicate messages or reconnects do not grant duplicates.
- [ ] Reuse existing cosmetic browse/equip/purchase infrastructure in the compound.
- [ ] Adapt direct platform IAP item purchases, verification, transaction deduplication, and ownership/restoration. Remove inherited client wallet/coin requests; no wallet or coins in V1. Define product types before implementing consumable handling.
- [ ] Verify voice during radios, horn use, scene changes, and eight-player conversations.
- [ ] Profile actual target devices; set concrete frame-time, memory, and network budgets from the project baseline and platform target.
- [ ] Check footage readability: players, threats, and physical jokes must remain visible in recordings.
- [ ] Conduct external playtests with unfamiliar players and multiple consecutive runs.
- [ ] Prioritize bugs that strand crews, break ownership, corrupt progression, or block replay.

Acceptance: several consecutive sessions succeed without developer recovery, common failure paths return a usable UI, and no known frequent blocking defects remain. A four-week public release is conditional on this evidence and platform readiness; otherwise ship a closed alpha.

## Week 5 — content variety and player feedback

Deliverable: launch content set and improved replayability.

- [ ] Expand toward four scavenging blocks only after the first two work well.
- [ ] Vary authored supply/enemy placements and select existing enemy combinations that produce distinct situations.
- [ ] Tune fuel and enemy pressure across crew sizes; avoid eight bats eliminating all tension.
- [ ] Refine compound onboarding, private joining, awards, and feedback.
- [ ] Add a small themed cosmetic selection if existing tooling makes it low cost.
- [ ] Consider spotlight only after core reliability is proven.
- [ ] Evaluate whether optional emergency bat fuel adds useful recovery; it is not required for launch.

Acceptance: groups understand how to meet, survive, return, and replay; route variety produces different decisions rather than merely shuffled scenery.

## Week 6 — stabilization and launch

Deliverable: tested public V1 and a practical recovery/hotfix process.

- [ ] Freeze features and address launch blockers.
- [ ] Test full rooms, simultaneous code creation, late arrivals, network loss, authority changes, transfer failure, and repeated rounds.
- [ ] Verify cosmetic purchases/restoration using platform-supported test accounts and tools.
- [ ] Verify escape persistence across sessions and interrupted saves.
- [ ] Finish device performance, comfort, and recorded-footage checks.
- [ ] Complete actual store/account/age/voice requirements and release assets.
- [ ] Confirm Photon CCU/traffic plan, authentication, and graceful capacity errors against current account settings.
- [ ] Document build/release steps, diagnostic collection without sensitive data, known limitations, and rollback/hotfix procedure.

Do not assume external platform review completes within this week; initiate prerequisite work during Phase 0.

## State and integration checklist

This is a behavior contract, not a mandate to replace Duckz's networking architecture.

- Session: room type/privacy, phase, countdown/roster, run identifier, transition destination.
- World: selected blocks, transforms, spawn selections, loaded stage.
- Train: pose/motion, control state, fuel, and necessary persistent attachments.
- Objects: identity, location, holder/owner, held/stowed/loose state, bag contents, consumed state.
- Players: identity, participation/reconnect status, downed/reviving state, spawn eligibility.
- Enemies/flares: current relevant state and lifetimes rather than replaying obsolete triggers.
- Finale: bridge activation/time/progress, passage gate, enemy pressure, and committed automatic train ride.
- Results: immutable per-player finish/non-finish records captured before catch-up teleports, final stats, awards, completion credit, and victory-ride respawn state.

For each category, identify the existing serialization path, who may mutate it, how a late joiner learns it, what happens when its owner leaves, and how repeated requests are handled. Keep transient sounds separate from lasting state while restoring ongoing effects when appropriate.

## Highest risks and required evidence

1. **Unknown reuse boundary:** prove baseline builds and record source dependencies before estimating import work.
2. **Late joining/room transfers:** test with real changing state, interrupted connections, and group departures.
3. **Moving cargo:** repeated acceleration, drop/grab/throw, authority changes, and loaded bags on actual clients.
4. **Crawlgraph placement:** validate transformed graph/geometry alignment and no runtime bake work.
5. **Eight-player VR:** measure frame time, memory, traffic, voice clarity, and physical crowding.
6. **Difficulty:** one-hit down and unkillable enemies must create recoverable rescues rather than immediate chain failures.
7. **Persistence/commerce:** reuse established ownership paths and deduplicate completion rewards.
8. **Scope:** use a simple finale and limited blocks before optional content or new tools.

## Four-week scope reduction

Retain: room architecture, public/private departures, late joining, reliable moving cargo, core scavenging/rescue, dawn ending, and basic repeat-play flow.

Reduce first:
- Two scavenging blocks instead of four.
- One or two existing enemy types.
- Coal only; no emergency alternative fuel.
- No spotlight.
- Three stats and a few awards.
- Existing cosmetics with minimal new art.
- Keep the proposed bridge finale to one external button and a timed lowering defense; no extra fuse/battery puzzle. Replacing it with a plain escape now requires a design discussion rather than an automatic scope cut.
- Keep public return matchmaking simple: one coordinated, bounded search for crews of one to four, then fresh-compound fallback; no elaborate room-ranking system.
- Repeatable block template and validation rather than an elaborate generator/editor suite.

If mandatory systems still do not meet their acceptance tests, extend the schedule. Do not relabel a known unreliable build as release-ready.

## Immediate next development session

1. Locate Duckz source and confirm intended import/fork strategy.
2. Establish baseline and populate the reuse inventory with real paths.
3. Settle the minimum session/reconnect policies listed under Week 1.
4. Implement one public and one private session path with placeholder expedition controls.
5. Prove late joining against the actual moving platform and shared bag before broad content production.

All checklist items are initially unverified. Update them only with concrete implementation and test evidence.

Radio/horn follow-up (October 29): physical hold buttons implemented with release-to-stop station transmission and looped horn audio. Runtime/editor compilation passed; editor checks, Quest 3 feel, and two-client release/disconnect/late-join/audio acceptance remain pending. See week2-session.md.

