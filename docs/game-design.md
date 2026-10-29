# Duck Rails — V1 game design

Handoff date: October 28, 2026.

## Status and intent

This document transfers the design conversation into the repository. **Confirmed** means explicitly selected or clearly established by the user. **Proposed** means a suggested implementation or tuning default that has not received a final decision. Existing Duckz capabilities are user-reported and require source inspection.

The original ambition was a one-month release. A four-week external alpha and six-week public-release target were proposed, not guaranteed or committed. Session architecture comes first to avoid building gameplay around simultaneous entry and later retrofitting late joining.

## Experience and tone — confirmed

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

## Train

### Confirmed
- Reuse moving-platform technology for a real moving train.
- Large physical STOP/GO lever: accelerate or stop. Exact acceleration curve is tuning.
- Train horn operated by a physical button or cord. Pull cord is the favored proposal; exact implementation is open.
- Coal fuels the train; furnace and readable fuel feedback are needed.
- Fuel decreases slowly as the train moves and remains shared room state across late joining, rejoining, and master changes. Prototype tuning: 0.025 fuel per metre (1 fuel per 10 seconds at cruise); no idle consumption. This rate is provisional.
- Permanent radio communications station aboard the train, communicating both ways with handheld walkie-talkies.
- Physical equipment storage, not backpack-style slots: an item stays where placed until picked up.
- Approximately four full-size backpack hooks. Bags remain full size and usable; no inventory nesting.
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

Confirmed: coal scavenging, existing backpack/item interactions, and freely shared equipment.

Proposed: limited fuel creates reasons to stop and decisions about how deeply to explore. Keep initial personal equipment consistent until balancing demonstrates another need. Night vision availability and starting loadouts remain open.

Optional emergency fuel was discussed: wooden bats could burn for much less fuel than coal, sacrificing defense for movement. This is NOT confirmed. The proposed first version would exclude bags and electronic equipment and consume fuel only on deliberate release into the furnace. Do not implement a universal burn-anything system by default.

## Enemies, downing, and rescue

### Confirmed direction
- Reuse enemies that can be repelled/stunned rather than killed.
- No health pool: one hit downs the player.
- Enemies stay within their authored scavenging block in V1.
- Enemies cannot board or navigate the train in V1.
- A down triggers Duckz's flare: temporary illumination and deterrence for susceptible enemies.
- Revive by holding a hand near a downed player for approximately three seconds. A rising-pitch sound marks progress; completion revives the player. No item or extra button.

### Proposed details requiring tuning
- Downed players crawl slowly and cannot climb onto the train unaided.
- Moving the hand away resets revive progress; multiple hands do not accelerate it.
- Both rescuer and downed player hear progress.
- Brief post-revive protection prevents immediate repeated downing.
- Readable attack windups and retreat feedback; avoid unavoidable hit chains.
- Keep boundaries out of obvious player routes and give real breathing room after departure.

### Open decisions
- Bleed-out, permanent elimination, full-crew failure, and spectator behavior.
- Repeated down/flare behavior and whether it permits permanent enemy deterrence.
- Initial enemy types and tuning across one to eight players.
- Exact safe-boarding rules so enemies cannot seal every entrance.

Deferred: enemy boarding, moving-train pathfinding, pounce-and-retreat attacks, and acid spit or other ranged threats aboard.

## Authored level blocks and crawlgraphs

### Confirmed
- Pre-generate/author reusable level pieces and bake their custom crawlgraphs ahead of time.
- Runs select from prebuilt blocks at runtime. No runtime geometry generation or crawlgraph rebaking.
- Keep enemies local to their blocks; no inter-block navigation is needed for V1.
- Build a practical workflow to make many blocks without repeating error-prone setup.

### Proposed block contract
- Geometry, baked crawlgraph, track entry/exit anchors, and stopping position.
- Candidate coal/equipment/enemy spawn markers.
- Basic route/difficulty tags and a stable identifier.
- Editor validation for bake presence, marker placement, and track alignment.
- Runtime placement must correctly transform graph data if block transforms vary.
- Decide block loading lifetime and clean up departed blocks without losing player or shared-object state.

Initial content proposal: four scavenging blocks, choose three per run, one fixed escape stretch. Four-week scope can start with two. Candidate settings: convenience store, maintenance depot, railway service yard. A power-restoration gate finale is only an idea, not a requirement.

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
- Returning public crews enter a fresh compound together, with destination capacity reserved for that crew.

## Late joining and reconnection

Required: a code can join an active run; player spawns aboard after loading current world state.

Synchronize route/block selection, run phase, train pose/motion, fuel, shared items and bag contents, relevant enemy state, and player states before enabling interactions.

Confirmed: authenticated account identity is used across reconnects. Reserve disconnected expedition slots for 120 seconds while another connected player keeps the room alive. Retain gear as shared room objects and do not grant duplicate supplies on reconnect. The Photon Master Client coordinates session transitions; a successor reconstructs current state. If recovery cannot continue, provide a usable return path.

Prototype defaults: late arrivals use the same shared test supplies, without a personal loadout; new joins close during ending/return and reopen in private staging. Final gameplay loadouts and escape-credit eligibility remain open.

Current validation target: DesktopPlayer, including multiple authenticated desktop clients. Justin reports backend and Photon authentication setup completed. Headset validation is deferred, not satisfied by desktop checks.

## Dawn ending, results, and progression

### Confirmed
- Ride into dawn on success.
- Show individual stats and humorous superlatives, then return the group to a compound.
- Persistent visible escape count beneath the nametag.
- Reuse cosmetic ownership/purchasing.

### Proposed presentation
- Tunnel/covered stretch hides a switch from night to dawn lighting; no continuous day/night simulation needed.
- Let players keep moving, talking, and honking during the victory ride.
- One readable world-space results board with all players, roughly 15–20 seconds, then countdown/fade.
- Candidate stats: fuel supplied, revives, enemies repelled, downs, horn use.
- Candidate titles: Hero of the Day, Certified Chicken, Frequent Flyer, Personal Space Enforcer, Boiler Room Employee, Public Nuisance, Precious Cargo, Somehow Survived.
- One title per player with tie handling and sensible zero-activity fallback.

### Persistence direction and open rules
- Current recommended scope: fresh gameplay supplies each run; persist cosmetics, preferences, and escape count.
- No permanent power progression, classes, or economy planned.
- Define escape credit for late arrivals, downed passengers, disconnects, and duplicate completion events. Earlier suggestion: downed players aboard count; late-arrival eligibility is unresolved.
- Escape credit must be applied once per eligible player per completed run.

## Monetization and scope

Confirmed: reuse existing Duckz cosmetic purchasing infrastructure and provide compound access.

Confirmed October 28: Duck Rails V1 has no coins or wallet. Items are purchased directly through platform IAP transactions. Reuse applicable purchase verification, transaction deduplication, ownership, and restoration code; do not port coin balances, deposits, or coin-priced purchases. Exact purchasable items and whether any are consumable remain to be defined.

Proposed: free-to-play, small themed cosmetic selection, no paid gameplay supplies or revives. Upfront pricing is not finalized. No new premium currency, battle pass, shop backend, or cosmetic economy is authorized by this plan.

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
