# Duck Rails — V1 game design

Handoff date: October 28, 2026.

November 5 confirmed interaction: held coal should support ground-pushing/vaulting through the inherited item locomotion system, like the bat and gold bars. Prefab and builder configuration are updated; headset feel remains pending. See `week1-session.md`.

November 2 maintenance: port the sixteen user-selected Duckz fixes while preserving Rails session/downing rules and shared keyboard/VR rig. Train hand contact now uses the inherited cabin support system directly; cargo release and ownership fixes are adapted to RailCargo. See [fix provenance and validation limits](duckz-fix-ports.md). Inherited weapon tuning does not add new shop inventory.

Art direction draft (October 30): [Art guide](art-guide.md) defines the proposed visual identity, palette, materials, lighting, train and environment treatment, and gameplay readability. It preserves the confirmed design below; its new art choices remain proposals pending review of the first finished train/depot slice.

## Status and intent

November 4 campground expansion: the user approved the expanded drawing for 3D with rough Quackville textures. The [installed V2](lakeside-campground-layout.md#installed-expanded-campground-v2) spreads 73 trees, three campsite areas, canoe beach, overlook, gully footbridge and fallen-tree scene into the approach/departure sections. Flat seams, level track and existing enemy/loot counts remain. Offline navigation/lighting, 2,055 physical and 619 catalogue checks pass; headset/performance acceptance is pending.

November 4 remaining block drawings: [nine top-down layout studies](remaining-block-layouts.md) cover the remaining scavenging catalogue after Depot, Motel and Campground. The user requested hills and ground elevation changes on some blocks. Proposed terrain includes rolling farm pasture, a Tunnel Works hillside, quarry excavation/benches and smaller yard berms; exact dimensions remain drawing proposals. Preserve level track and flat block seams. No Unity implementation or acceptance is claimed by these drawings.

November 4 campground: the user approved the irregular-lake layout for greyboxing and clarified rough Quackville materials. The installed prefab has a real lake basin, +1/+2 m cabin terraces, graded routes and flat perimeter connections; offline navigation/lighting, 1,052 physical and 619 catalogue checks pass. See [campground implementation](lakeside-campground-layout.md#greybox-and-rough-quackville-implementation). Headset traversal and final atmosphere/performance remain unverified.

October 30 shared-rig decision: use one DuckPlayer for headset/Quest Link and keyboard/mouse simulation, including switching input sources during Editor Play. Q/E reach left/right hands and right mouse grips both; simulated input must exercise the same grabbing, fingertip buttons, bag and physical train controls. This supersedes DesktopPlayer as the gameplay test target and its separate toggle/click interaction defaults. See [shared-rig.md](shared-rig.md) for controls and validation limits. Coal's interior pickup defect remains separate.

October 30 Voice Patrol decision: retain the integration but disable moderation by default. The GameManager Inspector's **Enable Voice Patrol** is a per-client startup setting; enable it before launch/Play Mode and restart to apply changes. Photon voice chat remains independent. The shared GameManager prefab defaults to off; scenes without a GameManager do not start moderation. Existing stored bans are retained but are not loaded/applied while the integration is off.

October 30 testing-tools decision: add development-only scenario shortcuts, a unified Unity Test Center, persistent diagnostic reports with backend upload, automatic invariant detection, saved reproduction presets and a left-forearm panel toggled by the existing left menu input. DuckPlayer fingertip buttons include Report Bug. Detected reports produce a headset notification and remain open for investigation/resolution. See [testing-tools.md](testing-tools.md) for implementation, deployment and validation limits.

This document transfers the design conversation into the repository. **Confirmed** means explicitly selected or clearly established by the user. **Proposed** means a suggested implementation or tuning default that has not received a final decision. Existing Duckz capabilities are user-reported and require source inspection.

The original ambition was a one-month release. A four-week external alpha and six-week public-release target were proposed, not guaranteed or committed. Session architecture comes first to avoid building gameplay around simultaneous entry and later retrofitting late joining.

October 30 validation update: the [multiplayer stability pass](multiplayer-stability.md) adds three-client desktop evidence for repeated public/private session transitions, shared cargo, master disconnect/rejoin and voice-room routing, plus 547 editor checks. It fixes avatar lifecycle and reconnect defects without changing the design or fuel tuning. Eight-player, headset, human voice quality and natural-run acceptance remain separate.

## Experience and tone — confirmed

- November 1 track collision decision: track rails and sleepers are visual-only, with no colliders. Ground, platforms, bridge decks and safety barriers retain their physical collision.

- November 1 lighting workflow: trial baked fixture illumination, light probes for moving subjects, subtle fog and a baked reflection probe in Compound before establishing expedition darkness/flashlight tuning. The first scene sample is installed; its intensities, probe layout and headset performance are provisional. See [subway compound](subway-compound.md).

- November 1 compound direction: bank the outdoor fortified station alternative and develop the approved abandoned-subway-terminal refuge as a walkable, roughly textured blockout. Retain station information, platforms access and open supplies space around a broken shallow fountain. Side tracks are inaccessible but intended to be audible; stairs behind the arrival view lead toward the surface and are completely blocked with makeshift crates/panels. Use salvaged partitions, pixel textures and retrofitted cold work lights. See [subway compound](subway-compound.md) for implementation and validation limits.

- October 30 art follow-up: the user endorsed the low-poly train concept's look and requested darker, spooky nighttime locations in the same style. Use deeper ambient shadows and localized lights while retaining nearby action/path readability; the generated night studies are visual explorations, not measured headset lighting targets. See [concept index](art-concepts.md).
- October 30 visual direction: match existing DuckZ assets with low-poly geometry and visibly pixelated textures. Supplied avatar and Quackville screenshots establish the style reference. Preserve DuckZ character anatomy and locomotion silhouette; prioritize target-headset performance. The train render concept is an image exploration, not an implemented or profiled asset.
- Cooperative VR horror-comedy for up to eight players.
- Modern-day setting; no Old West requirement.
- Cartoon duck avatars with expressive eyes; zombie-duck enemies.
- Gorilla Tag–style hand locomotion using Duckz's established implementation.
- Shared train expedition: stop, scavenge coal and equipment, rescue friends, regroup, and escape.
- Horror comes from atmosphere, uncertainty, separation, and pursuit. Comedy comes from ducks and player behavior.
- Favor readable, shareable situations: panicked radio calls, failed rescues, late boarding, and arguments about leaving.
- Success ends with a train ride into dawn, individual statistics and humorous superlatives, then a group return to a compound.

Proposed pacing: a 12–18 minute expedition, approximately three scavenging stops, with quiet travel intervals. Exact length, difficulty, and location count require testing. No repeating day/night cycle is currently planned; nighttime plus a dawn finale is the proposed approach.

## What we are leveraging from Duckz

### User-reported existing capabilities
- Working multiplayer and room codes entered through a pause menu.
- Photon Realtime networking; retain this stack for V1. PUN and Realtime are not synonyms: inspect actual SDK usage.
- Networked moving platforms: players can ride a helicopter, walk around inside, and jump off with good reported feel.
- Hand locomotion and held-item movement already feel good.
- Backpacks with four to eight slots. Bags cannot be nested in other bags.
- Shared physical items: dropped weapons and bags can be picked up by anyone; others can access a bag's contents.
- Five existing enemies and custom crawlgraph baking, including climbing/jumping navigation capabilities.
- Melee hits make enemies retreat temporarily before returning.
- Flashlights, melee weapons, handheld walkie-talkies, and night-vision goggles.
- A flare launched when a player goes down; it lights the area, frightens some enemies, and expires.
- Cosmetic purchasing and supporting infrastructure.

### Verify before estimating integration
- Source repository/path, Unity and package versions, target devices, rendering pipeline, and build process.
- Direct Realtime versus PUN usage, object ownership, spawning, late-join mechanisms, voice integration, authentication, and persistence.
- Which interactions are portable unchanged and which assume a specific scene or room lifetime.
- Whether existing platform support includes dropped rigidbodies, loaded bags, crawling players, and authority changes.
- How crawlgraphs are serialized, transformed, loaded, unloaded, and associated with enemies.

Authentication source inspection (October 28, 2026): the Unity copy is in `X:\Work\duck-rails`; the separately deployed backend copy is in `X:\Work\duck-rails-web`, based on Gun Raiders Classic's simplified backend. The API base is `https://duckrails.gunraidersapi.com/api/v1/`. `Assets/Scripts/Login/GameSessionClient.cs` uses Meta registration and `Assets/Scripts/Photon/PhotonSession.cs` uses PUN over Realtime; `PhotonVoiceChat.cs` uses Photon Voice's PUN integration. See `docs/authentication-setup.md` for the verified contracts and remaining deployment checks. Other reuse capabilities above remain unverified.

November 1 enemy expansion: each scavenging block has 4–6 enemies including minion, bomber, smasher and spider. A retained route seed randomizes counts, types and authored positions. Bridge activation adds 4–6 defenders; the starting station stays safe. See [mixed enemies](mixed-enemies.md).

## Train

November 1 lighting implementation: StartingStation and all twelve scavenging blocks plus the bridge finale now have dedicated bake scenes and additive lighting companions loaded alongside their geometry. Each catalogue block has four sample baked lights. Translation-only probe placement, atlas rebinding, complete-route readiness and unload/reload have offline and authenticated single-editor Play Mode evidence; this does not establish headset performance or final art. See [block lighting](block-lighting.md).

October 31 starting-station decision: every expedition begins at the midpoint of a fixed, editable train-station block before the five randomized scavenging blocks. The station is additional to the random block count and reserves space for a future store; store functionality is not part of this change.

### Confirmed
- Reuse moving-platform technology for a real moving train.
- October 30 art direction: keep the train mostly open air; a small, lightly enclosed front is optional. Windows must extend low enough for the user-reported typical eye line of approximately 0.5 metres above the local ground/deck during gorilla movement. Design around that view, keeping sills and front bodywork from blocking the track and nearby hazards. Exact enclosure and window dimensions require in-rig validation; 0.5 metres is a design reference, not a measured fixed rig height.
- Large physical STOP/GO lever: accelerate or stop. Exact acceleration curve is tuning.
- Driving is hold-to-run: grip the OFF lever, push the hand forward until it snaps to GO, and keep gripping. Pulling back while gripping snaps to OFF; pushing forward again resumes GO. Releasing also springs it back to OFF and brakes the train. Only one player/hand controls it at a time. October 31 shortcut: holding T engages GO and releasing T returns to STOP in either Keyboard or VR mode on the editor/desktop client, using the same shared ownership. Physical hand control remains available.
- Train horn uses a physical hold button: loop the horn while a hand presses it; stop on release. Simulated hands use the same fingertip contact.
- Coal fuels the train; furnace and readable fuel feedback are needed.
- October 31: when fuel runs out, coast smoothly to a stop instead of stopping instantly. Current implementation tuning is 2 m/s² deceleration (two seconds from cruise); headset feel remains to be reviewed.
- Train art modularity: cockpit chair, controls, radio table and furnace must be separate meshes that can be repositioned in the Unity editor. Keep associated interaction anchors and collision aligned when moving a module. This does not request runtime pickup/rearrangement of train equipment.
- October 30 implementation authorization: build the basic 3D blockout from the production PDF, give each station its own prefab and existing functionality, and replace the live scene train with a main assembly prefab. Implemented in Expedition; see [train blockout](train-production.md#unity-blockout--october-30-2026). Shell meshes remain independently editable. Seat mechanics remain unchanged. The open rear takes precedence over the former displayed rack/hook layout; functional supply items and backpacks remain available. Dimensions and light/effect settings are provisional until target-headset review.
- October 30 furnace construction clarification: its mouth must be a real opening into a hollow cube interior so objects can be inserted. Fire is separate particle FX inside the cavity, not a solid textured/emissive front panel. Preserve the approved enlarged mouth and tall-stack exterior. This art handoff does not claim implemented geometry or collision.
- Latest October 30 furnace art correction supersedes the earlier 0.5 x 0.5 metre mouth target: make the single opening larger and the smokestack three times the previous concept's height above its casing, retaining its thickness. Exact enlarged opening dimensions remain unspecified. Furnace stays outside the cockpit. See [furnace revision 4](train-production.md).
- Fuel decreases slowly as the train moves and remains shared room state across late joining, rejoining, and master changes. Prototype tuning: 0.025 fuel per metre (1 fuel per 10 seconds at cruise); no idle consumption. This rate is provisional.
- Permanent radio communications station aboard the train, communicating both ways with handheld walkie-talkies. Transmit through the station only while a hand holds its physical TALK button; release stops transmission. Simulated hands use the same fingertip contact.
- October 30 radio art requirement: a dedicated table carries the radio and a separate upright microphone, following the user's reference. Place the TALK button at the bottom of the microphone assembly/on its base, facing the player. Table size, height and exact placement remain proposals in the [train production study](train-production.md); no scene changes are implied by the drawing.
- October 30 train art revision: simplify the radio receiver to avoid modeled buttons or knobs suggesting interaction; optional control markings are texture only. Keep the physical mic-base TALK button. Add a forward headlight, a front-center seat with the throttle on the dashboard ahead, and left/right cockpit lamps without a hanging lamp. Use one larger furnace connected to a thick smokestack, with one opening on one face approximately 0.5 x 0.5 metres. Remove the extra edge-mounted furnace and all decorative coal bins/piles/loose coal. Smoke particles emit above the stack while moving. Seat mechanics, exact equipment placement and dimensions beyond that opening target remain proposals, with low-eye-line validation required; this is an art decision, not implemented behavior.
- Physical equipment storage, not backpack-style slots: an item stays where placed until picked up.
- Approximately four full-size backpack hooks. Bags remain full size and usable; no inventory nesting.
- October 30 art layout follow-up supersedes the v2 in-cockpit station placement: put the single furnace and radio table outside the cockpit on opposite sides of the open deck, matching the user's top-view reference. Stack rises directly from the furnace into open sky. The simplified radio is approved. Driver seat/throttle are centered, fuel gauge left and horn right. Remove displayed bags at the rear and remove the rear transverse rail to allow boarding from behind. Reconcile the earlier four-hook storage requirement before implementation; player backpacks and loose-cargo behavior remain in scope. See [revision 3](train-production.md).
- Loose cargo must work independently of holders.

### Loose-item contract — launch requirement
- Dropped coal, bats, and loaded backpacks travel with the carriage.
- Repeated acceleration/braking does not cause unintended drifting, launching, or clipping.
- Grabbing, dropping, handing off, and throwing work while moving and across clients.
- Deliberately thrown-overboard items leave naturally.
- Predictability matters more than realistic acceleration-induced sliding.

### Proposed details
- Accessible boarding rails and multiple entrances sized for eight players.
- A large push-to-talk control and speaker on the permanent radio; one crew channel and no radio batteries.
- Train communications remain available when out of coal.
- Physical furnace insertion with clear acceptance and gauge feedback.

### Optional idea to preserve
- A train-mounted spotlight can be grabbed and swiveled, then released to stay on and hold aim relative to the train. Desired idea, but not a launch commitment.

## Resources and equipment

October 31 confirmed economy decision: implement the [gold economy](gold-economy.md). One-time 100-coin starting wallet; departure and midpoint stores with seven standard gear types and Duckz pull-to-buy interaction; no free gear on the train. Shop deposits pay every connected crew member immediately. Only bars irreversibly deposited in the train safe pay at successful extraction, only to escaped participants. Safe count is visible; held, bagged and loose bars never cash out. Persistent coins and admin credits reuse the Duckz backend. This supersedes the earlier no-wallet and fresh-free-loadout directions. Prices and spawn counts remain initial tuning.

Confirmed: coal scavenging, existing backpack/item interactions, and freely shared equipment.

Proposed: limited fuel creates reasons to stop and decisions about how deeply to explore. Keep initial personal equipment consistent until balancing demonstrates another need. Night vision availability and starting loadouts remain open.

Optional emergency fuel was discussed: wooden bats could burn for much less fuel than coal, sacrificing defense for movement. This is NOT confirmed. The proposed first version would exclude bags and electronic equipment and consume fuel only on deliberate release into the furnace. Do not implement a universal burn-anything system by default.

## Enemies, downing, and rescue

### Confirmed direction
- Reuse enemies that can be repelled/stunned rather than killed.
- No health pool: one hit downs the player. There is no dead state or bleed-out. Players remain downed indefinitely until another player revives them or they quit; elapsed time does not recover or eliminate them.
- Enemies stay within their authored scavenging block in V1.
- Enemies cannot board or navigate the train in V1.
- A down immediately launches Duckz's flare, with no intentional delay: temporary illumination and deterrence for susceptible enemies.
- While downed, the local player sees Duckz's existing red vision overlay. It clears on revival or return to the compound.
- Revive by holding a hand near a downed player for approximately three seconds. A rising-pitch sound marks progress; completion revives the player. No item or extra button.

### Proposed details requiring tuning
- Downed players crawl slowly and cannot climb onto the train unaided.
- Moving the hand away resets revive progress; multiple hands do not accelerate it.
- Both rescuer and downed player hear progress.
- Brief post-revive protection prevents immediate repeated downing.
- Readable attack windups and retreat feedback; avoid unavoidable hit chains.
- Keep boundaries out of obvious player routes and give real breathing room after departure.

### Week 2 implementation defaults — October 29
The user authorized sensible defaults: 0.65 m/s crawling, three-second continuous proximity rescue with interruption reset, three-second post-revive protection, a 45-second repeat-flare cooldown, and team failure after all connected crew remain down for five seconds. No player dragging. Two existing zombie minions inhabit the depot and two more join the bridge defense. Enemies cannot enter the carriage corridor. These values need gameplay tuning across crew sizes; they do not establish headset acceptance. See [week2-session.md](week2-session.md).

Deferred: enemy boarding, moving-train pathfinding, pounce-and-retreat attacks, and acid spit or other ranged threats aboard.

## Authored level blocks and crawlgraphs

October 31 layout follow-up: add buildings and coal on the left side of the track as requested. All twelve scavenging blocks now have two additional left-side buildings; each authored variation distributes its existing six coal chunks three per side. Entrances face the track. Existing right-side enemy patrols and baked graphs remain unchanged; the left additions are player scavenging areas. See `week3-session.md` for validation and limits.

October 31 authoring update: coal placements use reusable, editor-visible CoalSpawner markers in every scavenging block. Six pieces per selected variation remain unchanged; closely paired placements were spread across the encounter (12 m minimum in the current authored catalogue). See `week3-session.md` for editing and validation.

### Confirmed
- Pre-generate/author reusable level pieces and bake their custom crawlgraphs ahead of time.
- Runs select from prebuilt blocks at runtime. No runtime geometry generation or crawlgraph rebaking.
- Keep enemies local to their blocks; no inter-block navigation is needed for V1.
- Build a practical workflow to make many blocks without repeating error-prone setup.

### Confirmed Week 3 scope — October 29
- Twelve gray-box locations: Maintenance Depot, Convenience Store, Roadside Motel, Lumber Yard, Container Yard, Passenger Station, Farm Silos, Tunnel Works, Scrapyard, Lakeside Campground, Electrical Substation, and Quarry.
- Each expedition selects five distinct stops with authored spawn variations, then the fixed bridge finale. Target approximately ten minutes for a typical scavenging run, with at least roughly twenty seconds of driving between encounter areas.
- Use a dedicated Expedition scene; Compound remains social/staging. Private rooms retain their Photon room and code through scene changes.
- Prefab root is the track entry at ground height, unit scale, forward +Z. Exit returns to the centerline with forward +Z. Internal curves may go left/right and return to that connection.
- Load the five selected blocks and finale together for the whole run. Keep collision and gameplay active. November 5: user approved local rendering-only visibility of the player's current block and its immediate predecessor/successor, including the station and finale. Keep all other block state loaded; no unloading. See [block visibility](block-visibility.md) for implementation and validation limits.
- Coal-carrying enemies release coal on accepted hits. A cooldown-based recovery opportunity prevents permanent fuel exhaustion; exact thresholds are implementation defaults to tune.
- Lighting was initially deferred until the gray-box phase was playable. November 1: baked-lightmap and conventional-probe samples now cover Compound, StartingStation and the full catalogue; the user reports that lighting works. Follow the [lighting workflow](block-lighting.md). Final art and Quest performance acceptance remain pending.

### Block contract
- Geometry, baked crawlgraph, track entry/exit anchors, and stopping position.
- Candidate coal/equipment/enemy spawn markers.
- Basic route/difficulty tags and a stable identifier.
- Editor validation for bake presence, marker placement, and track alignment.
- Runtime placement must correctly transform graph data if block transforms vary.
- Retain every selected block until the expedition ends; clear network objects at staging before another departure.

Implementation defaults: 160-metre blocks, three authored coal/enemy spawn sets per location, six coal chunks and 4–6 randomized enemies per selected stop. Encounter bounds occupy local Z 45–115, leaving 90 metres between adjacent encounters (22.5 seconds at 4 m/s). Four templates have gentle internal curves. Stable versioned IDs, placements and variation indices are retained in room properties. Navigation is baked in the editor and translated/reindexed at load without generating geometry, rebaking or connecting blocks. See [week3-session.md](week3-session.md) for setup and actual validation limits.

## Compound, public departure, and private rooms

### Confirmed
- Compound is a social room for mingling, cosmetics, shopping, and meeting friends.
- The social room is NOT automatically the expedition group.
- Public departure platform counts down and takes only its occupants, up to eight.
- People outside the platform cannot hold up departure.
- Private creation uses a separate area/terminal, never a toggle that hijacks public boarding.
- Every gameplay room has an exactly five-digit numeric code, including public compounds, public expeditions, private rooms, and editor quick-start expeditions. Leading zeroes are preserved.
- The private station can join any live room by code, including public expeditions for late entry or rejoining. The current code is always shown in the session banner.
- Private creator chooses a five-digit code; Create and Join are separate actions.
- Creation must reject an occupied code. Use server-side room creation as the collision arbiter rather than a check-then-create race.
- Photon uses one region. Compatible app/version settings are still required.
- Private rooms are hidden from public discovery but joinable by code. A code is not authenticated friends-only access.
- Friends can join active runs and spawn on the train, subject to eight-player capacity.

### Confirmed public platform behavior
- First occupant starts a 20-second timer; joining/leaving does not reset it.
- Empty platform cancels; fewer than eight can depart.
- Show time and occupancy; clearly indicate who is aboard and when full.
- At zero, lock the roster, transfer it, and reopen the source platform afterward.
- Validate membership/capacity through the room's coordinating authority.

### Confirmed private lifecycle
- Successful creation enters private staging; friends can arrive before or after departure.
- Private session remains the same Photon room through staging, run, results, and return so its code persists while alive.
- The creator is initially Master Client and starts the expedition manually. Responsibilities pass to another connected player when the Master Client leaves.
- When the last connected player leaves, the room closes immediately and releases its code. No empty-room recovery window.

### Confirmed compound entry and public return
- Start in the standalone Login scene, with its own art and random camera spots. Authenticate, connect to Photon, join a compatible public compound, then transition into Compound.
- Quackville is not used by Duck Rails. Do not preload its environment beneath Login or gameplay.
- Public compounds hold twelve players; expeditions hold eight. Automatic placement selects the initial compound only.
- Public runs take only the trigger occupants at countdown expiry. Emptying the trigger cancels the countdown; the next entrant starts a fresh twenty seconds.
- After either success or failure, returning public crews of one to four connected players first try to join an existing compatible public compound with space reserved for the whole crew. Use a bounded search and fall back to a fresh compound if no suitable destination can be secured. Crews of five to eight connected players return to a fresh compound together, with destination capacity reserved for that crew.
- Returning crew members follow one coordinated destination; they do not independently matchmake into different compounds. The prototype now implements this policy with a temporary coordination room, server-side whole-crew reservations, and a shared fresh-compound fallback. Multi-client acceptance remains pending; see `week1-session.md`.

## Late joining and reconnection

Required: a code can join an active run; player spawns aboard after loading current world state.

Synchronize route/block selection, run phase, train pose/motion, fuel, shared items and bag contents, relevant enemy state, and player states before enabling interactions.

Confirmed: authenticated account identity is used across reconnects. Reserve disconnected expedition slots for 120 seconds while another connected player keeps the room alive. Retain gear as shared room objects and do not grant duplicate supplies on reconnect. The Photon Master Client coordinates session transitions; a successor reconstructs current state. If recovery cannot continue, provide a usable return path.

Prototype defaults: late arrivals use the same shared test supplies, without a personal loadout; new joins close during ending/return and reopen in private staging. Final gameplay loadouts and escape-credit eligibility remain open.

Current validation target: the shared DuckPlayer with simulated input, including multiple authenticated clients, followed by Quest Link/headset validation. Justin reports backend and Photon authentication setup completed. Simulated-input checks do not satisfy headset acceptance.

## Bridge finale, dawn ending, results, and progression

Confirmed October 29, 2026: the current implementation scope is Week 2 plus the bridge finale and a world-space run-results board for both success and failure. This supersedes the earlier gate/power-restoration concept. These are confirmed design requirements, not verified runtime behavior. Commerce and persistent escape-count implementation remain outside this scope.

### Sequence
November 1 confirmed change: the external bridge button requires a continuous physical hold. Releasing pauses lowering and retains all progress; anyone can resume it. This supersedes the one-touch, unattended minute-long timer. The train remains safe from enemies. See [bridge hold](bridge-hold.md) for implementation and validation.

1. Warning signs before the raised bridge give the crew time to brake. Players must stop the train before it hits the raised or incompletely lowered bridge; a collision causes the train to explode and the run to fail for the whole team. Do not automatically stop the train to prevent this failure. A volunteer leaves the stopped train and holds down the external button to lower the bridge.
2. Lowering advances only while at least one eligible player holds the button. Release, downing, leaving the control or disconnecting pauses progress without resetting it. Multiple operators do not increase speed. Additional enemies spawn after the first activation; players defend themselves with the existing repel/retreat and rescue mechanics. Once fully lowered, the bridge stays down. Initial tuning is 30 seconds of accumulated holding, not a confirmed balance target.
3. Once the bridge is fully lowered, players board and manually drive the train toward/across it.
4. The train entering the bridge crosses the finish line. At this instant, record each participant's finish status and switch the train to automatic forward travel. Players can no longer control its motion.
5. Anyone not aboard is teleported onto the train for the shared ending, but receives no completion credit and is marked did not finish. Downed players already aboard finish normally. Teleporting does not itself revive anyone.
6. The train continues across the bridge into a tunnel on the far side. Change lighting to dawn while the sky is hidden, then emerge into the victory ride.
7. Present individual accomplishments, finish status, and humorous superlatives on a world-space board. A failed run also shows this board before group return. Return everyone together using the established public/private compound return policy.

### Run outcome and results — confirmed
- October 31 presentation update: show floating results text above and farther in front of the train, with no opaque backing panel. Retain the shared success/failure content and finish timing.
- At least one escaped player makes the overall run a success; the entire crew does not need to escape. Zero escaped players means failure.
- Lock individual escape status at the bridge-entry finish trigger, before catch-up teleports. Downed players already aboard escape normally; players left behind do not.
- A bridge crash or full-crew-down failure before the finish records no escaped players. Failure ends the run; it does not introduce a dead state or revive players automatically during gameplay.
- Success and failure use the same world-space stat canvas, layout, run information, individual statistics, and awards. The overall outcome appears at the top as **SUCCESS** or **FAIL**; do not replace failure results with a separate reduced screen.
- Every participant's row explicitly shows **Escaped** or **Didn't make it**, independently of the overall outcome and any award. A successful run can therefore contain both statuses.
- Show the results before returning the crew through the established public/private return flow on either outcome.

### Finish credit
- Use bridge entry as the finish trigger, following the user's explicit statement that driving onto the bridge crosses the finish line. Do not wait until the far end or tunnel.
- Every player aboard at that instant finishes, including downed players. Anyone off the train does not finish, whether standing or downed.
- Capture results before catch-up teleports. Teleporting a non-finisher aboard never grants completion credit.
- Persist escape credit once per eligible player/run; preserve the locked result through reconnects, authority changes, and victory-ride respawns.
- Confirmed October 29: downed passengers earn the same escape credit as other onboard players. There is no dead state.
- Week 2 default: connected late arrivals can escape under the same onboard rule; disconnected participants remain on the board but do not escape. New joins remain closed during ending/return.

### Victory ride and results
- Players can walk around, talk, and honk while the train drives automatically.
- Jumping/falling off during this ride returns the player aboard after two seconds without downing a standing player or reviving a downed player. This never revokes an earned finish or grants a non-finisher credit. Teammates can still revive downed players during the ride; the recorded results remain unchanged.
- Use the shared success/failure world-space results board described above. The earlier 15–20-second display plus return countdown/fade remains suggested timing.
- Candidate stats: fuel supplied, revives, enemies repelled, downs, horn use.
- Candidate titles: Hero of the Day, Certified Chicken, Frequent Flyer, Personal Space Enforcer, Boiler Room Employee, Public Nuisance, Precious Cargo, Somehow Survived.
- Suggested award policy: one per player, with tie handling and a zero-activity fallback. Show did-not-finish status independently of any award.

### Encounter constraints and implementation decisions
- Finale enemies remain local to the authored approach block and cannot board the train. No moving-train navigation or runtime crawlgraph baking is introduced.
- Solo players must be able to release the button, repel or evade enemies, retreat to the train, and return to resume work. Readable approaches, usable retreat routes, unarmed feasibility and recovery windows remain playtest requirements; enemy boarding is excluded.
- Define authoritative bridge timing, duplicate-button handling, recovery after Master Client departure, and passage gating until the bridge is fully lowered.
- Define the train reference point/onboard volume used at the finish line, including players hanging from boarding rails.
- Week 2 default: the fixed ending ride receives sufficient fuel in the same atomic finish commit so it cannot stall after credit is committed.
- Tune total holding time (initially 30 seconds), enemy pressure, off-train recovery boundary, return delay, and presentation for one to eight players. Current mixed-enemy counts are not evidence of solo balance.
- Week 2 default: stats freeze at the finish line. Ending horn use still produces sound without changing results.

### Persistence direction
- Persistent escape count beneath the nametag; reuse cosmetic ownership/purchasing.
- Gameplay items reset each run; coins persist in the backend alongside cosmetics, preferences and escape count. The wallet receives 100 coins once, not on every run.
- No permanent item upgrades or classes. Earned gameplay coins fund repeatable shop purchases.

## Monetization and scope

Confirmed: reuse existing Duckz cosmetic purchasing infrastructure and provide compound access.

October 31 supersedes the October 28 no-wallet decision: gameplay coins use the Duckz transaction ledger and admin-credit system. Coins buy only in-run equipment. Existing cosmetic IAP remains separate; this change does not authorize selling coins or gameplay advantages for real money.

Proposed: free-to-play, small themed cosmetic selection, no paid gameplay supplies or revives. Upfront pricing is not finalized. No premium currency, battle pass or new cosmetic economy is authorized; the earned gameplay wallet reuses Duckz infrastructure.

Target headset/store, age-rating suitability, voice/account restrictions, and submission requirements must be verified for the actual release. Earlier discussion assumed Quest and a young audience; treat this as a release requirement to validate, not a completed platform review.

## V1 exclusions
- Photon Fusion migration.
- Enemy navigation on the moving train or between blocks.
- Runtime crawlgraph baking/procedural geometry generation.
- New gun/combat systems, crafting, train construction, bosses, or class progression.
- A repeating day/night simulation.
- Ranked/skill matchmaking or a complex formal party system.
- New commerce infrastructure.

Scope exclusions do not prevent reusing an already-working Duckz feature when it is low-cost and supports the approved loop; avoid expanding the launch checklist simply because a feature exists.


Confirmed October 30 coal tuning: twice the authored coal (six chunks per stop), half the furnace value (2.5 fuel per chunk), preserving total authored fuel per stop.

November 4 testing convenience: user authorized an **Invincible from enemies** checkbox in Duck Rails Tests. It is a saved, local private-development override for enemy contacts and bomber blasts; normal enemy/downing rules remain the gameplay default. See [testing tools](testing-tools.md#enemy-invincibility-in-test-center).

November 4 test workflow: the user authorized a saved, reorderable prefab list in Duck Rails Tests. Private editor-hosted development runs can use one or more distinct scavenging blocks in exact list order, with the station and finale added automatically. Normal gameplay retains five randomized distinct stops. See [Test Block Order](testing-tools.md#test-block-order--november-4-2026) for usage and validation limits.

November 4 motel follow-up: user requested Quackville rough materials and clarified terrain as a little elevation around the edges. Installed six low perimeter banks, preserving level track, courtyard and transformer circulation; rebuilt offline navigation and lighting. See [motel surface/elevation pass](roadside-motel-layout.md#quackville-rough-materials-and-edge-elevation). Headset/performance acceptance remains pending.

November 4 motel greybox: the user approved implementation of the motel/transformer-yard plan. The actual block now contains the two-storey motel, two exterior stairs, balcony, static ajar doors, west office/garage and six-transformer yard with broken chain-link openings. Navigation and lighting are rebaked; 176 physical/layout checks and 619 catalogue checks pass. See [installed motel and validation limits](roadside-motel-layout.md#installed-greybox). VR and multiplayer acceptance remain pending.

November 4 motel drawing history: the user selected a two-storey L-shaped motel, courtyard toward the train, two exterior stairs, connecting balcony and smaller west service building. The [motel drawing set](roadside-motel-layout.md) led to the approved greybox recorded above.

November 5 coal art: user approved the fractured, near-black low-poly lump with coarse blue-grey pixel patches. Installed 44-triangle mesh and 32x32 texture on the shared pickup, retaining physics/network settings and rebaking hand-contact geometry. See [coal art](art-concepts.md#coal-pickup-art--november-5-2026). Headset/night readability acceptance remains pending.

November 5 coin limit: user confirmed the implemented 2,000-coin maximum and that `/2000` always remains grey; only the balance turns yellow at the cap. The watch shows the smaller grey cap below the balance and a yellow balance at maximum. Backend rewards/admin grants cap at remaining capacity; overflow is discarded. See [wallet cap evidence and rollout limits](gold-economy.md#wallet-cap--november-5-2026).
