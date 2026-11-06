# Gold, shops and the train safe — October 31, 2026

## Confirmed rules

- November 5 wallet limit: user confirmed a maximum of 2,000 coins. Rewards credit only available space; overflow is discarded. The watch shows a half-size grey `/2000` below the balance, with the balance yellow at maximum and its original color after spending.

- Each account receives 100 coins once when its backend wallet is initialized. This is not a per-run allowance or a minimum balance.
- Coins persist in the backend and buy only in-run equipment. Purchases remain spent after failure; shop earnings are credited immediately. There is no additional penalty to the existing wallet on failure.
- Departure and midpoint shops each show one of every standard gear type: nail bat, frying pan, basic bag, helmet, flashlight, walkie-talkie and night vision. Bundle weapons, larger bags and cosmetics are excluded.
- Reuse Duckz's exact `StoreWallItem` interaction: grab/pull, visible tether, server-authorized deduction and item handoff, snap-back on insufficient funds, then restocking. A display is not a one-purchase stock limit.
- No gameplay equipment spawns on the train by default. Initial furnace fuel remains 10.
- Depositing a bar at either shop grants the bar value up to each connected crew member's wallet limit, with Duckz coin particles flying to each recipient's own watch. New arrivals do not receive earlier shop sales.
- The train safe accepts bars irreversibly and shows its bar count. It gives no immediate money. Only its recorded deposits pay on successful extraction, and only to participants recorded as escaped. Downed passengers aboard qualify; people left behind do not. Catch-up teleports do not alter eligibility.
- Bars held in a hand, in a backpack or loose on the train never cash out automatically. A failed run pays nothing from the safe.
- Admin can inspect a wallet, grant/remove coins and inspect transactions, using the existing admin authorization.

## Initial tuning, not balance acceptance

Reuse Duckz's 100 coins per bar and standard prices: flashlight 25, radio 50, helmet 75, basic bag 100, frying pan 175, nail bat 275, night vision 300. The starting 100 coins are not enough for every item. Each of the five chosen scavenging blocks has two gold spawns, selected with the existing authored variation (ten bars per expedition). These values require natural-run playtesting.

The midpoint shop sits in the quiet stretch after the third scavenging encounter, before the fourth. It does not replace one of the five blocks or change the track distances. The starting shop occupies the station's reserved store area. Both shops and the safe are basic editable blockouts.

## Implementation and authoring

- `Assets/DuckRails/Economy/RailShop.prefab`: shared seven-item store, using cloned Duckz wall displays and the existing purchase code. `RailStoreCatalog.asset` references train-compatible cargo variants.
- `TrainSafe.prefab`: hollow safe with a front insertion slot, intake trigger, count label and the original Duckz `CoinShower`. Its nested instance is in `DuckRailsTrain.prefab`.
- `GoldExchange.prefab`: Duckz gold crate visuals/effects with Rails shared-payout logic.
- Every scavenging block variation has two `RailGoldSpawner` markers. Move those transforms in Prefab Mode; `RailSpawnSet.gold` is the authoritative spawn list. They do not replenish during a run.
- `RailEconomyBuilder.Install` installs without rebuilding existing block layouts or train modules. Repeated installation preserves existing economy assets and authored marker positions. The Week 3 builder also installs economy content when rebuilding its generated scenes.
- The explicit **Rebuild Prototype Safe** command overwrites the generated safe geometry and resets its train placement; do not use it to preserve later hand-edited safe art.
- `RailExpedition` consumes bars and records receipts in the same room-state revision as the safe count/recipient list. Owner/spawn token/hold generation, intake bounds, player proximity and sample age are validated. Physical bars are destroyed only after committed consumption.
- `DuckzWallet` persists pending rewards per account; deterministic per-run/bar/recipient IDs deduplicate retries. Balance mutations are serialized locally. The backend retains Duckz's player-row transaction locks, server prices, signed authenticated requests, caps and transaction ledger. It trusts authenticated game clients for gold awards as Duckz does; it is not a server simulation of Photon outcomes.
- `BagInventory` chooses the existing Rails bag prefab in Rails rooms, so purchased bags retain moving-cargo synchronization and cleanup.
- Networking partition is `session-v7`; restart all clients on the same build.

## Backend and rollout

November 1 publication update: game/API/admin commits are pushed and both Docker images are published to TCR. SQL execution remains with Justin; TKE rollout remains pending until the migration. See [image tags and digests](validation/gold-economy/tcr-2026-11-01.txt). The October 31 validation record below describes the earlier local-only state.

Changes are in `X:\Work\duck-rails-web`; source Duckz repositories remain untouched. The Rails API base is `https://duckrails.gunraidersapi.com/api/v1`. Economy paths are `/wallet`, `/store`, `/store/buy`, `/coins/deposit` and `/admin/...`, without a game-name prefix. Duckz request/response contracts and implementation are reused; internal package/table names do not determine the public URL. Update the API, Unity client and admin together; the unshipped prefixed routes are not retained as aliases.

Apply `api/sql/create_duckz_economy.sql` only to **duckrails_tencent_global**, then publish/deploy the Duck Rails API and admin using that repository's `AGENTS.md`. The migration creates the Duckz economy receipt tables plus `rails_wallet_grants`; balances remain in the existing `transactions` ledger. The welcome receipt is independent of balance and ledger soft-deletion, preventing another starter grant when a wallet reaches zero. No production migration or deployment is implied by local implementation.

## Acceptance still required

- Actual deployed first-account grant, repeat login, purchase, balance persistence and admin grant/removal.
- Two or more authenticated clients: simultaneous deposits, non-master purchases/intake, visible per-recipient particles, master handoff, reconnect and repeated runs without duplicate credit or lost equipment.
- Both shops: real hand pull, insufficient funds, restock, all seven items and basic bag contents/ownership.
- Successful escape with a downed passenger and a player left behind; failure; held/loose/bagged bars excluded; safe count synchronized and no retrieval.
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

## Wallet cap — November 5, 2026

Client: `DuckzWallet.MaxCoins = 2000` bounds all balance responses. `Watch` retains coin-arrival animation and mute/status support; the cap line remains grey even when the balance is yellow. `RailWalletCapChecks.Run()` passed eight prefab balance/status cases (0, 1999, 2000 and 1950, with and without mute text), checking two lines, no overflow, smaller grey cap text and color restoration after spending. Unity compiled successfully. Headset readability remains pending.

Backend: `X:\Work\duck-rails-web\api\gofiber\service\duckz\economy.go` enforces the cap while holding the existing player-row transaction lock. Partial/full deposits store actual credited amounts, including zero-credit receipts so retries after spending cannot reclaim overflow. Admin grants obey the same cap. Existing balances over 2000 receive an auditable `wallet_cap` debit on wallet access or a successful balance operation; this removes excess rather than hiding it behind a display clamp. No database migration is required.

`go test -p 2 ./service/duckz ./repository/duckz ./router/duckz` passed, including new partial/full/retry/spend/refill and old-wallet/admin tests. At implementation validation, no image publication, production balance change or TKE rollout was performed. Deploy the matching API and client before live acceptance; test concurrent real-client rewards and watch readability on headset. Until backend rollout, the client display clamp alone cannot enforce the persisted wallet cap.

## Shop price and reach correction — November 6, 2026

The seven RailShop displays referenced sibling price objects in the original Store prefab asset, so no local labels appeared and the fallback was suppressed. Cleared those foreign references in the shared shop and its builder: each display now creates a local shelf label beneath its collider, at its actual placement. Fixed generated-label initialization so OnEnable subscribes after the store is assigned, and corrected TMP camera-facing direction. Prices continue to use StoreUI and the backend catalogue updates.

Lowered the shared display origins from 1.10 m to 0.75 m (35 cm); the midpoint inherits this. The departure station retains its authored position overrides, while its labels use those actual positions. Unity compilation and the economy suite passed, including checks for foreign label references and lower shelf height. A Unity preview fixture generated all seven labels below their colliders (0.47–0.642 m above the shop origin). Headset reach/readability, live price refresh and real-client purchases remain pending; this is not multiplayer or headset acceptance.
