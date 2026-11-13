# Gold, shops and the train safe — October 31, 2026

November 13 regression: actual master, non-master and late-player deposits freshly pass depositor-only +100 credit, one recipient, consumed bar and no replay; peers observe the CutePixel reward text. Actual shared-store flashlight/bag purchases exposed Compound cleanup deleting the delivery after charging. Fix `8379de0f15405a0541434dc20037fd929d8161f3` retains current room/run/buyer purchases, removes them on buyer departure and clears them before same-room expedition manifests. Three-client purchases, late join, handoff, departure, both expedition stores and unrelated floor/public-return cleanup rerun PASS. [Fix and validation](validation/playtest-queue/2026-11-13-regression/store-cleanup-fix.md). Owned bundles, checkout and headset acceptance remain pending; no wallet balances or entitlements were fabricated.

November 11 finale crate lighting: the stationary `CrateGoldBarMesh` now uses a dedicated lightmap-UV mesh copy, Contribute GI and baked lightmap receiving. Finale lighting was baked/exported and installed into Expedition; 17 editor checks pass. Source FBX, collider, receiver, label and coin effects are unchanged. The finale reward-text spawn/font/material/effect fields were null in both the pre-polish baseline and current prefab; this lighting correction does not validate the reward-text wiring described below. Physical deposit/readability checks remain queued. See item 24 in `docs/level-polish-batch.md`.

November 10, 2026: the train safe is removed. Gold pays only when a bar is deposited at the departure or midpoint shop, immediately, to each connected crew member, up to the wallet cap. Held, bagged and loose bars do not cash out. Escape, failure and who remains aboard do not create another payout. `TrainSafe.prefab`, the train placement and **Rebuild Prototype Safe** are gone. Matchmaking appends `-no-train-safe-v1`, so older clients cannot join these rooms. The backend deposit routes stay: shop sales already submit those signed receipts, and there was no safe-specific API.

November 11, 2026 — depositor-only payout (matches Duckz): a deposited bar now pays 100 coins to the depositor alone, via the plain `coins/deposit` API (one bar, wallet cap still applies). `RailGoldReceiver.ApplyDeposit` records only the depositor as recipient, so other players are never credited and observers see the coin shower fly into the depositor's body (event 109, unchanged). The crate plays Duckz's 100-coin shower (via `CoinShower`) and the bar's `DepositSound` through `RoomRpc.PlaySound` when the credit lands, plus Duckz's floating "+100" reward text (`GoldRewardText`, shown to depositor and observers; font/material/effect/spawn wired on `GoldExchange`, the helicopter and the finale receivers). This supersedes the shared/scaled payout described in "Confirmed rules" (the "Everyone gets coins" bullet, shared-presentation notes) and the November 8 per-recipient scaling: `RailResourceBalance.GoldCredit`/`/coins/deposit/scaled` are no longer used for shop sales. **Balance flag:** with G = floor(10E) bars per block this now pays 100 per bar (about 1000+ coins per block), far above the old 100/block budget; resource-balance tuning is unresolved. Compiled in Unity; real-client verification queued in `playtest-queue.md`.

## Confirmed rules

- November 5 wallet limit: user confirmed a maximum of 2,000 coins. Rewards credit only available space; overflow is discarded. The watch shows a half-size grey `/2000` below the balance, with the balance yellow at maximum and its original color after spending.
- Each account receives 100 coins once when its backend wallet is initialized. This is not a per-run allowance or a minimum balance.
- Coins persist in the backend and buy only in-run equipment. Purchases remain spent after failure; shop earnings are credited immediately. There is no additional penalty to the existing wallet on failure.
- November 10: the bridge finale has a third deposit crate (receiver ID 3, `Finale gold deposit` in `bridge-finale-v1.prefab`) beside the lower-bridge hold button, cloned from the helipad crate. It pays like the shops: immediately, to everyone connected at the sale. It has only an editor-time check so far (receiver wired to its own intake, label and coin shower); no run has reached it, and it has not been lit, baked or looked at in the scene.
- Departure and midpoint shops each show one of every standard gear type: nail bat, frying pan, basic bag, helmet, flashlight, walkie-talkie and night vision. Bundle weapons, larger bags and cosmetics are excluded.
- Reuse Duckz's exact `StoreWallItem` interaction: grab/pull, visible tether, server-authorized deduction and item handoff, snap-back on insufficient funds, then restocking. A display is not a one-purchase stock limit.
- November 9 exception: one free flashlight per departing player spawns on the train; late joiners get no extra flashlight. Other equipment remains shop-bought. Current starting fuel is 50 (see resource-balance.md).
- Depositing a bar at either shop grants the bar value up to each connected crew member's wallet limit. Every player sees coin streams to all credited recipients: their own stream lands in their watch, and other streams land at the recipients' bodies. New arrivals do not receive earlier shop sales.
- November 10: there is no train safe. Bars held in a hand, in a backpack or loose on the train never cash out. Escape and failure leave those bars unpaid. Shop credits already issued stay in the wallet.
- Admin can inspect a wallet, grant/remove coins and inspect transactions, using the existing admin authorization.

## November 8 balance revision — implemented, rollout and acceptance pending

Approved effective workers E = 1 + (crew - 1) * 0.6. Each scavenging block spawns
up to G = floor(10E) gold bars, each averaging 100/G coins per eligible recipient.
Full collection pays 100 coins per recipient per block, independent of crew size.
Departure roster is retained; integer payouts carry fractional value across eligible
shop sales. November 10 removes safe deposits, so the carry index counts shop sales only. The new `/coins/deposit/scaled` API calculates credits
and retains receipt deduplication/caps; deployment is pending. Legacy queued rewards
retain their old contract. See [resource balance](resource-balance.md) for tuning,
validation and rollout details. This supersedes the earlier two-bar/100-coin tuning;
existing payout eligibility remains unchanged.

## Initial tuning, not balance acceptance

November 6 shared coin presentation: previously `RailGoldReceiver` played only the local wallet's coin shower. Each confirmed positive credit now also sends reliable, uncached Photon event 109 to the other clients, identifying the run, receiver and receipt. Observers render that stream toward the sender's avatar with `CoinShower.PlayTo`; receipt deduplication prevents repeated effects. The event never changes wallet balances or another player's watch. Zero-credit capped rewards produce no stream. This shared path covers both shops and safe payout. Unity compiled and the existing `RailEconomyChecks.Run()` passed. Real-client all-recipient visibility, simultaneous deposits and headset performance remain unverified.

Reuse Duckz's 100 coins per bar and standard prices: flashlight 25, radio 50, helmet 75, basic bag 100, frying pan 175, nail bat 275, night vision 300. The starting 100 coins are not enough for every item. Each of the five chosen scavenging blocks has two gold spawns, selected with the existing authored variation (ten bars per expedition). These values require natural-run playtesting.

November 8: normal runs now contain four scavenging blocks (eight authored gold bars). The midpoint shop sits in the quiet stretch after the second encounter, before the third. It does not replace a scavenging block or add track distance. This supersedes the five-block/ten-bar budget above. The starting shop occupies the station's reserved store area. Both shops and the safe are basic editable blockouts.

## Implementation and authoring

- `Assets/DuckRails/Economy/RailShop.prefab`: shared seven-item store, using cloned Duckz wall displays and the existing purchase code. `RailStoreCatalog.asset` references train-compatible cargo variants.
- `GoldExchange.prefab`: Duckz gold crate visuals/effects with Rails shared-payout logic. November 10 removes `TrainSafe.prefab` and its train instance.
- Every scavenging block variation has two `RailGoldSpawner` markers. Move those transforms in Prefab Mode; `RailSpawnSet.gold` is the authoritative spawn list. They do not replenish during a run.
- `RailEconomyBuilder.Install` installs shops and gold markers without rebuilding existing block layouts or placing anything on the train. Repeated installation preserves existing economy assets and authored marker positions. The Week 3 builder also installs economy content when rebuilding its generated scenes.
- `RailExpedition` consumes bars and records shop receipts in the same room-state revision as the recipient list. Owner/spawn token/hold generation, intake bounds, player proximity and sample age are validated. Physical bars are destroyed only after committed consumption.
- `DuckzWallet` persists pending rewards per account; deterministic per-run/bar/recipient IDs deduplicate retries. Balance mutations are serialized locally. The backend retains Duckz's player-row transaction locks, server prices, signed authenticated requests, caps and transaction ledger. It trusts authenticated game clients for gold awards as Duckz does; it is not a server simulation of Photon outcomes.
- `BagInventory` chooses the existing Rails bag prefab in Rails rooms, so purchased bags retain moving-cargo synchronization and cleanup.
- Networking partition appends `-no-train-safe-v1`; restart all clients on the same build.

## Backend and rollout

November 1 publication update: game/API/admin commits are pushed and both Docker images are published to TCR. SQL execution remains with Justin; TKE rollout remains pending until the migration. See [image tags and digests](validation/gold-economy/tcr-2026-11-01.txt). The October 31 validation record below describes the earlier local-only state.

Changes are in `X:\Work\duck-rails-web`; source Duckz repositories remain untouched. The Rails API base is `https://duckrails.gunraidersapi.com/api/v1`. Economy paths are `/wallet`, `/store`, `/store/buy`, `/coins/deposit` and `/admin/...`, without a game-name prefix. Duckz request/response contracts and implementation are reused; internal package/table names do not determine the public URL. Update the API, Unity client and admin together; the unshipped prefixed routes are not retained as aliases.

Apply `api/sql/create_duckrails_economy.sql` only to **duckrails_tencent_global**, then publish/deploy the Duck Rails API and admin using that repository's `AGENTS.md`. The migration creates the Duckz economy receipt tables plus `duckrails_wallet_grants`; balances remain in the existing `transactions` ledger. The welcome receipt is independent of balance and ledger soft-deletion, preventing another starter grant when a wallet reaches zero. No production migration or deployment is implied by local implementation.

## Acceptance still required

- Actual deployed first-account grant, repeat login, purchase, balance persistence and admin grant/removal.
- Two or more authenticated clients: simultaneous deposits, non-master purchases/intake, visible per-recipient particles, master handoff, reconnect and repeated runs without duplicate credit or lost equipment.
- Both shops: real hand pull, insufficient funds, restock, all seven items and basic bag contents/ownership.
- Successful escape with a downed passenger and a player left behind; failure; held, loose and bagged bars stay unpaid. November 10 removes the safe count and escape-only payout.
- Quest 3 slot reach, shop item reach, labels/watch readability and particle performance; eight-player crowding and natural-run economic balance.

Editor/unit evidence does not close these acceptance items. See the validation record below for checks actually run.

## Validation record — October 31, 2026 (Vancouver)

Evidence: [validation/gold-economy](validation/gold-economy/).

- Unity compiled; all twelve quick-check suites passed, including 35 economy assertions covering payout eligibility, permanent shop earnings after failure, serialization/receipt stability, catalogue, cargo variants, gold markers and safe bounds.
- Authenticated solo Play Mode: ten gold and thirty coal spawned with no free equipment. Placing a real networked bar into the safe's physics trigger consumed it exactly once, updated the count and created no pre-escape reward. Loose deck gold did not count. The shop trigger consumed another bar and queued one recipient reward. Fixture placement used Rigidbody positioning, not a headset hand insertion.
- The original display could be grabbed with PlayerHand, showed its tether and returned without spawning gear when pulled with an unavailable wallet. This is not evidence of an actual server insufficient-funds response or a successful purchase. The synthetic unpaid shop claim was removed by exact receipt ID after backup; no account was credited or charged.
- Three distinct authenticated editor clients passed the expedition regression: curved moving platform, loaded bag transfer, carrier drop, master handoff, late rejoin, two escapees/one left behind, station return, fresh replay, bridge collision failure and cleanup. The initial host was in VR mode and ceded master authority; keyboard simulation, host reassignment and the initial StartPrivateRun were performed manually before the fixture proceeded. All three completed the remaining checks and the runner stopped/restored the session. This regression did not exercise networked gold deposits or deployed wallet credits.
- Go repository/service/config/router checks passed; welcome-grant tests cover first grant, repeat access, zero balance and transaction rollback. The admin production build passed with Sass deprecation and bundle-size warnings.
- The editor Console recorded Meta MRUK shared/global-context initialization errors at multiplayer startup. No claim of a clean XR runtime or headset acceptance is made. Existing diagnostic reports were inspected and none were marked resolved by this feature work.
- No production migration, Git/image push or API/admin rollout was performed. Wallet loading remained unavailable against the currently deployed API; backend rollout and the acceptance list above remain required before this is playable end to end.
- URL follow-up: removed the inherited game-name prefix from the Rails client, backend, admin and route documentation. `go test -p 2 ./router ./router/duckz` passed, including all ten economy endpoints rejecting anonymous requests, the old wallet path returning 404 and a later public route remaining accessible. Unity refreshed with no script compilation errors; the admin production build passed with its existing warnings. This route change has not been deployed.

## Economy table naming — November 2, 2026

Justin reports applying the original economy SQL. The backend now targets duckrails_coin_deposits, duckrails_store_purchases, duckrails_entitlements, duckrails_loadouts and duckrails_wallet_grants. Existing installations must use api/sql/rename_duckz_economy.sql in the web repository, preserving receipts and rows; do not run the fresh-install creation script first. Stop API traffic for the rename and matching API rollout. SQL rename and new API deployment remain pending; local changes do not establish live wallet or multiplayer coin-effect acceptance.
Publication — November 2, 2026: backend commit 5f444c8650566b242b0b11696a7260bebe438fe3 pushed to origin/main. API image uswccr.ccs.tencentyun.com/gr-tcr/duckrails-api-golang:api-5f444c8 built and pushed successfully; TCR push digest sha256:b0e2260eac6bd44745a5d8ce17855d99e4d6302fdd3e3fa0a00d461af79452e3. Admin image unchanged. Table-rename migration and TKE rollout are not verified or performed by this publication; apply the rename with the matching API rollout before live economy verification.

## Midpoint deposit investigation — November 4, 2026

Report `32e4ad0109d34f75b1fe42b598a58b3a` remains open. Authorized backend inbox read confirmed upload; the initial sandbox-restricted query failed and was retried successfully. The report contains 60 samples of the Motel / Depot / Campground test route. Carried view 2 reaches (-14.64, 0.42, 463.52), then disappears as revision changes from 41 to 42, about seven seconds before reporting. This is consistent with midpoint intake consumption, but the report omits object types, gold receipts and wallet responses, so it cannot prove payout outcome alone.

The local Unity Editor.log contains repeated `Deposit of 1 bars failed (401): invalid signature` messages from DuckzWallet.SendDeposit. This establishes a signed-deposit rejection in the editor log; the log messages themselves do not carry the report ID. Wallet code retains 401 deposits for retry. Client and checked-in backend both sign nonce plus raw body with a session-derived HMAC key; the actual cause of the deployed signature mismatch is not yet established. Do not resolve the report or claim coins credited without a successful authenticated retry and receipt evidence. Current Unity Console was already reset to compilation warnings, so it does not retain the incident's runtime messages. No gameplay or backend changes were made during this investigation.

Signing configuration follow-up: Unity MCP verified that the active Assets/Resources/GameConfig.asset uses the C# requestSigningSecret default. That client default differs from the backend config/nonce.go fallback. Backend NONCE_SECRET overrides its fallback when nonempty. No NONCE_SECRET entry was found in the local runtime.env, config/build.env, .env.development or .env files. Production may supply it through TKE; kubectl has no current context configured, so the deployed value remains unverified. No secrets or configuration values were changed. Check the existing Duck Rails API workload NONCE_SECRET / referenced Secret before choosing which configuration to correct.

After the user added the TKE nonce environment setting, an authenticated Unity client test still returned HTTP 401 / invalid signature from POST coins/deposit. The probe used a fresh request ID and zero bars (validation-only; cannot grant coins). Normal wallet loading succeeded with 100 coins; one pending deposit remained and its automatic retry also logged invalid signature. The verification Play session was stopped. Confirm exact uppercase NONCE_SECRET, matching value, and restarted/replaced API pods; deployed environment access is still unavailable locally. Report remains open.
Post-redeploy retest at 2026-11-04 20:44:34 UTC: a fresh authenticated Unity session and signed zero-bar probe still returned HTTP 401 invalid signature. Automatic pending deposit retry also failed; wallet loaded with 100 coins and one pending deposit. Session stopped; no fix or payout claimed.
Successful retest at 2026-11-04 20:48:08 UTC after user TKE configuration/redeployment: authenticated signed zero-bar probe returned HTTP 400 duckz invalid bar count, proving the request passed HMAC middleware into deposit validation. The existing pending one-bar reward automatically cleared: wallet changed from 100 to 200 coins and pendingDepositCount from 1 to 0. No synthetic payable bar was submitted. Verification session stopped. This verifies deployed signing and pending payout recovery; a fresh physical shop deposit and coin animation remain untested.

## Wallet cap — November 5, 2026

Client: `DuckzWallet.MaxCoins = 2000` bounds all balance responses. `Watch` retains coin-arrival animation and mute/status support; the cap line remains grey even when the balance is yellow. `RailWalletCapChecks.Run()` passed eight prefab balance/status cases (0, 1999, 2000 and 1950, with and without mute text), checking two lines, no overflow, smaller grey cap text and color restoration after spending. Unity compiled successfully. Headset readability remains pending.

Backend: `X:\Work\duck-rails-web\api\gofiber\service\duckz\economy.go` enforces the cap while holding the existing player-row transaction lock. Partial/full deposits store actual credited amounts, including zero-credit receipts so retries after spending cannot reclaim overflow. Admin grants obey the same cap. Existing balances over 2000 receive an auditable `wallet_cap` debit on wallet access or a successful balance operation; this removes excess rather than hiding it behind a display clamp. No database migration is required.

`go test -p 2 ./service/duckz ./repository/duckz ./router/duckz` passed, including new partial/full/retry/spend/refill and old-wallet/admin tests. At implementation validation, no image publication, production balance change or TKE rollout was performed. Deploy the matching API and client before live acceptance; test concurrent real-client rewards and watch readability on headset. Until backend rollout, the client display clamp alone cannot enforce the persisted wallet cap.

## Shop price and reach correction — November 6, 2026

The seven RailShop displays referenced sibling price objects in the original Store prefab asset, so no local labels appeared and the fallback was suppressed. Cleared those foreign references in the shared shop and its builder: each display now creates a local shelf label beneath its collider, at its actual placement. Fixed generated-label initialization so OnEnable subscribes after the store is assigned, and corrected TMP camera-facing direction. Prices continue to use StoreUI and the backend catalogue updates.

Lowered the shared display origins from 1.10 m to 0.75 m (35 cm); the midpoint inherits this. The departure station retains its authored position overrides, while its labels use those actual positions. Unity compilation and the economy suite passed, including checks for foreign label references and lower shelf height. A Unity preview fixture generated all seven labels below their colliders (0.47–0.642 m above the shop origin). Headset reach/readability, live price refresh and real-client purchases remain pending; this is not multiplayer or headset acceptance.

## Departure flashlights — November 9, 2026

RailWorld uses rails.resourceCrew (captured at public/private departure) to spawn
one existing Rails/RailFlashlight room object per departure slot. Eight separated
positions span the rear deck. Flashlights remain shared, grabbable cargo. The
retained per-run flashlightSlots ledger prevents replacement of lost items;
instantiation markers recover grants if the master changes before ledger commit.
New runs use a new run ID and existing staging cargo cleanup. Late joins and
reconnects never increase the departure count. Purchased flashlights remain available.

Verified in the Duck Rails Unity editor: resource prefab includes PhotonView,
RailCargo and HandFlashlight; all eight positions clear train colliders and have
floor support; session and drive checks pass. Real-client spawning, late joins,
reconnects, host migration, repeated runs and headset interaction remain untested.

## Standalone midpoint size preview — November 9, 2026

User requested a minimal 30 m block with the existing shop for size review. Created `Assets/DuckRails/Blocks/midpoint-shop-preview.prefab`: standard 96 m width, 30 m flat ground/straight track, entry/exit/stop anchors, and a nested RailShop at (-16, 0, 15), facing the track. Geometry is under Env; no spawn candidates. Unity saved/reloaded the prefab and verified 30 m block/track length and seven displays, then opened Prefab Mode for review. This is a size preview only: no crawlgraph or lighting bake, route registration, multiplayer integration or gameplay acceptance. Live midpoint placement remains unchanged. Layout review precedes further art and integration.

November 9 size revision: user requested 40 m. Extended preview ground, rails, sleepers, exit anchor and block bounds to 40 m; recentered RailShop and suggested stop at Z20. Unity saved and verified 40 m track length. Width remains 96 m. Art concepts are proposals for review; route integration and bakes remain pending.

## Approved helipad blockout — November 9, 2026

User approved the 60 m top-down helipad plan and excluded the gold deposit. Built separate `Assets/DuckRails/Blocks/midpoint-helipad-preview.prefab`, preserving the earlier shop preview and its user-added helicopter edits. Standard 96 x 60 m ground, straight 60 m track, 30 x 30 m pad at (-23,0,30), 15 cm pad height, eight amber edge fixtures, apron, open-door 6 x 5 m hut, rear tank/bund, windsock, fencing, 26 pines and rocks. Uses existing Quackville tile materials and pine meshes; generated box meshes retain tiled UVs. Authoring source: `Assets/Editor/RailHelipadPreview.cs`; Build refuses to overwrite an existing prefab. No helicopter, shop displays, gold receiver or resource/enemy spawns in this new asset.

Unity compilation and saved asset checks passed: 60 m track, all mesh/material references present, no gold receiver, no non-ground colliders inside the 8 m railway corridor. Editor top-down visual review completed; MCP multi-angle capture returned black in Prefab Mode, so native editor view was used. Left open in Prefab Mode. Lighting fixtures are configured for baking but lighting/crawlgraph bakes, actual helicopter clearance, route integration and headset/multiplayer acceptance remain pending. This is an editable rough blockout, not a playable integrated shop.

## Parked helicopter shop — November 9, 2026

User authorized the existing helicopter as a stationary shop on the helipad, with spinning rotors, looping sound, rotor wash, retained security camera and an interior Rails gold receiver. Created `Assets/DuckRails/Economy/RailParkedHelicopter.prefab` as a variant of the already-imported `Assets/Prefabs/Photon/PhotonHelicopter.prefab`; source Duckz files and original helicopter prefab are unchanged. Both projects use Unity 6000.3.24f1. Added the variant under `Env/Parked helicopter` in the 60 m preview, preserving model scale and fitting landing gear to pad top. Visible bounds are about 15.9 x 6.3 x 22.1 m including rotors, entirely inside the 30 m pad and clear of the 8 m railway corridor.

Seven original cabin displays retain their positions and local price labels, now using RailStoreCatalog; disabled ten extra Duckz displays, their labels and bundle headings. Purchases use existing Rails cargo prefabs; basic bag uses BagInventory/RailWorld bag spawning. Cleared purchase UnityEvents, removed manager-dependent HelicopterStoreTrigger, zeroed sway, and retained Helicopter rotor animation without HelicopterManager. SecurityCam/TV and HelicopterMirror remain. Replaced CrateGoldBar logic with RailGoldReceiver (receiverId 2, not safe), retaining the crate mesh, intake and CoinShower; this supersedes the earlier omission of the gold receiver for the environment-only blockout. All TMP labels use CutePixel SDF.

RailParkedHelicopter handles local sound startup/retry and conservative concrete rotor wash (16 particles/s, max 32), stopping both when disabled. Dust is nested in the reusable helicopter variant. No flight schedule or networked transform is needed for the authored stationary cabin. Live route is still unchanged: do not instantiate this receiver alongside the old midpoint receiver in a playable route. Dedicated block route integration and offline navigation/lighting bakes remain pending.

Validation: RailParkedHelicopterSetup.Validate checks catalogue/display bindings, local labels, cargo/bag wiring, camera/audio/dust references, stationary configuration and receiver uniqueness. Isolated Play Mode fixture RailParkedHelicopterPlayCheck verifies fixed cabin pose, both rotating rotors, audio loop, dust emission, stop/restart lifecycle and security camera drawing to its render texture. Commerce is disabled in that fixture; authenticated purchases/deposits, hand reach, headset audio/camera performance and multiplayer acceptance remain untested. Evidence: output/helipad/parked-helicopter-check.txt. Fixture scene is Assets/DuckRails/Authoring/ParkedHelicopterCheck.unity; not added to builds. Test temporarily selects its play-mode start scene and restores the previous setting on exit.

November 9 integration supersedes preview-only status above: the live route now uses midpoint-helipad-v1.prefab as a fixed 60 m segment, with one interior receiver ID 2. Old midpoint scene shop and size preview are removed; departure RailShop is retained. Navigation and lighting are baked. See [helipad authoring/rotation and acceptance evidence](helipad-shop.md).
