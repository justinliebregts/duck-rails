# Duck Rails — execution plan

Handoff date: October 28, 2026. Read `game-design.md` for decision status and scope.

## Planning basis

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
Public return follow-up: `RailCrewReturn` keeps a temporary authenticated coordination connection while one gameplay client searches and reserves an existing compound. Search failure/timeout or coordinator loss selects a shared fresh allocation. Five-to-eight-player crews skip the search. Reservation cleanup uses retained per-user deadlines and a compare-and-swap update to preserve concurrent crews. Schema 4 separates this protocol from older clients. Local policy/lease checks and authenticated single-client return evidence are recorded in `week1-session.md`; the group acceptance boxes remain unchecked.

Fuel consumption follow-up: motion and starting fuel are retained atomically in schema 3 room properties, with deterministic distance-based consumption and a zero-fuel stop. Runtime/editor compilation and six new local regression checks passed; two-client fuel agreement, leave/rejoin, and master-departure acceptance remain pending. Coal insertion is not implemented by this change.

See `week1-session.md` for source paths, controls, validation results and pending multi-client evidence. Implemented work is kept separate from milestone acceptance; unchecked scenarios below remain unverified until exercised across real clients. Desktop follow-up adds the tagged Meta-account window from the referenced Classic commits, a Player2-configured Two Ducks scenario, a compact centered status banner and carriage-relative footsteps. Local regression checks passed; two-client sign-in remains pending the second saved account.

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

- [ ] Integrate train platform movement and STOP/GO lever with consistent shared state.
- [ ] Add physical horn control and limit audio masking of voice/radio.
- [ ] Add coal insertion, consumption, fuel gauge, and fuel exhaustion behavior.
- [ ] Make fuel insertion authoritative/idempotent so one coal chunk cannot be counted twice.
- [ ] Implement stable dropped cargo, including loaded bags, while moving, braking, and changing item ownership.
- [ ] Add simple physical holders and full-size backpack hooks using existing interaction patterns.
- [ ] Integrate handheld radio communication with a permanent train station.
- [ ] Build one gray-box scavenging block using the existing crawlgraph tooling.
- [ ] Use one existing enemy type with temporary retreat on hit and no train access.
- [ ] Implement one-hit down, existing flare behavior, and three-second proximity revive.
- [ ] Decide crawling, repeated flare behavior, post-revive protection, and a minimum viable full-crew failure rule.
- [ ] Confirm locomotion/flashlight/melee/backpack feel has not regressed.

Acceptance:
- [ ] Eight players can collect coal, revive a teammate, board, and leave without developer intervention.
- [ ] Dropped bats/coal/bags stay with the train; intentionally thrown items leave it.
- [ ] Repeated grabs and acceleration do not produce divergent ownership or cargo positions.
- [ ] Enemies never board and cannot make every boarding point unusable.
- [ ] Revive completion/interrupt and flare lifetime agree across clients, including a late joiner.
- [ ] Distant handheld users and players at the train station can communicate intelligibly.

Record device, client count, network conditions, and reproducible failures. Test beyond a single LAN/editor setup using the existing supported simulation tools where available.

## Week 3 — full expedition and repeatable content workflow

Deliverable: a complete start-to-finish run assembled from authored blocks.

- [ ] Define a stable block format: geometry, graph, track anchors, stop marker, spawn candidates, identifiers, and route tags.
- [ ] Create a repeatable editor template, invoke existing bake tooling, and validate missing graph/markers and track alignment.
- [ ] Verify graph coordinates after placement; choose constrained placement if arbitrary rotations add avoidable complexity.
- [ ] Select the route once through session authority and share the exact identifiers/placements with late joiners.
- [ ] Choose a block load/unload strategy based on target-device profiling; runtime baking remains excluded.
- [ ] Handle players and dropped items before unloading any block; avoid deleting active participants or retained supplies.
- [ ] Complete two initial scavenging blocks with authored variation.
- [ ] Add a simple final escape stretch and dawn lighting transition.
- [ ] Complete fuel/resource balancing, downtime, and failure/restart behavior.
- [ ] Add basic in-world onboarding and departure feedback.
- [ ] Ensure private and public sessions can repeat full expeditions.

Acceptance:
- [ ] New players understand boarding, fuel, revival, and escape without developer coaching.
- [ ] Selected blocks connect correctly and contain usable navigation without runtime rebaking.
- [ ] A late joiner receives the same route and relevant current objects.
- [ ] Success, failure, and replay all return to a usable group flow.
- [ ] One-player and eight-player runs are feasible under the intended design; tune toward intermediate group sizes as well.
- [ ] Dawn feels like relief and renders acceptably on the actual headset pipeline.

## Week 4 — external alpha / conditional release candidate

Deliverable: a coherent small game that outside friend groups can play repeatedly.

- [ ] Improve sound, lighting, attack cues, and safe boarding based on observations.
- [ ] Add results with a small stat set and humorous awards; handle ties and zero-action players.
- [ ] Finalize escape-credit eligibility, including late joins and downed/disconnected participants.
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
- Results: eligible participants, final stats, awards, completion credit.

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
- Simple final escape, no bespoke gate puzzle.
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
