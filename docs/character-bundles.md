# Character bundles — November 12, 2026

Quackbear, Huggy and Scrap Duck are installed in the compound's existing supplies-store module. Each has an avatar, a separate four-slot basic-bag placeholder and a separate frying-pan weapon placeholder. Default Duck is also available. The user's request authorizes these repeated bundle pulls in the compound; Departure Shop also has these three bundles beside its seven standard supplies; the midpoint retains seven standard supplies.

Both projects use Unity 6000.3.24f1. Duckz's StoreUI, StoreWallItem, StoreCatalog, wallet/IAP and loadout code are already inherited. They use PUN over Photon Realtime, including PhotonNetwork, room objects and player properties; no Fusion migration. Duckz was inspected read-only; no source commit was requested or imported.

Owned avatars equip immediately. Each weapon and bag has its own 120-second per-player timer, shared across store instances and reset on Photon room change, matching Duckz. An unowned pull opens the existing Meta checkout; successful checkout unlocks the bundle and the next pull takes/equips it. Gameplay coins are not charged. A private scene transition within the same room does not reset the timer.

## Assets and replacement art

New assets live in `Assets/DuckRails/Economy/Bundles/`:

- `RailBundleCatalog.asset`: global GameConfig catalog; seven standard supplies, default duck and nine bundle items. The expedition RailStoreCatalog is unchanged.
- `Skin_<character>.prefab`: rigged bodies with separately authored eyes/lips attached to the head. Scrap Duck lips use its body material because no separate lips material is supplied. Skin IDs: Quackbear 4, Huggy 5, ScrapDuck 6.
- `BagModel_<character>.prefab`: independent basic-bag copies, bag IDs 5, 6, 7. Replace the visual Mesh, retaining Slots/BagSlot and GrabLeft/GrabRight. Four slots are initial placeholder capacity, not an approved larger-bag upgrade.
- `Weapon_<character>.prefab`: independent Rails frying-pan cargo copies. Preserve GUIDs, root Photon names (`RailBundleWeaponQuackbear`, `RailBundleWeaponHuggy`, `RailBundleWeaponScrapDuck`), PhotonView/observed RailCargo and interaction/weapon components. Refit collision and regenerate grab surfaces for final models.
- `RailBundleWall.prefab`: nine character displays plus Default Duck, CutePixel headings and countdown labels. **Replace the corresponding bag/weapon display visual too** when replacing the physical model. Miniature displays are separate from network/physics components. Match mesh/axes to the held prefab for ItemPickupPose and refit the pull collider. Avatars directly reference assembled skin prefabs.

The wall is nested in `Assets/DuckRails/Compound/SubwayV2/Modules/05 Walk-through supplies store.prefab`. Only rear filler stock is hidden; geometry and side shelves remain. GameManager's shared Photon pool registers all three weapons for peers and late joiners.

`Duck Rails > Bundles > Install character bundles` creates missing assets/bindings without overwriting later art or placement edits. `Check character bundles` runs focused EditMode checks.

## Commerce setup still required

Matching entries are prepared in `X:/Work/duck-rails-web/api/gofiber/config/json/duckrails_economy.json`:

- `duckrails_quackbear_bundle`: skin_duck_quackbear, weapon_quackbear, bag_quackbear.
- `duckrails_huggy_bundle`: skin_duck_huggy, weapon_huggy, bag_huggy.
- `duckrails_scrap_duck_bundle`: skin_duck_scrap_duck, weapon_scrap_duck, bag_scrap_duck.

Create/configure these durable products and prices in the Duck Rails Meta app and deploy the prepared backend configuration. No price was specified, so fallback prices remain blank until Meta supplies localized prices. Neither deployment nor Meta product creation was performed. Existing ownership verification/restoration and backend bundle-exclusion logic are reused.

## Evidence and remaining acceptance

Verified Unity MCP target: `X:/Work/duck-rails/Assets`, Edit Mode. Compilation and [103 focused checks](validation/bundles/editmode.txt) pass: catalog/display bindings, distinct IDs/names, remote prefab registration, RailCargo observation, face materials/head parenting, four-slot bags, CutePixel labels, timer start/expiry, independent bag/weapon timers and unrestricted avatars. Console error/exception query was empty.

Reviewed [EditMode preview](../output/bundles/preview.png); corrected backward avatars, overlapping inherited display offsets and shelf clearance. Existing scene labels emitted unsupported CutePixel bullet/em-dash warnings during rendering; bundle labels do not use them. Backend `go test -p 2 ./config/... ./service/duckz/...` passes. No Play Mode run. Live purchase/cancel/restore, natural pulls, peers/late join, room reset, moving cargo and headset acceptance are **queued for playtest**.

## Departure Shop addition — November 12

At user request, the same bundle-wall prefab is now nested under `StartingStation/Departure Shop/Departure Bundles` in `Assets/Scenes/Expedition.unity`, along the south wall beside the gold exchange. Nine active bundle pulls retain the existing catalog, ownership and 0/120-second cooldowns. Default Duck is hidden on this instance. The seven original coin displays and midpoint shop are unchanged. No backend changes are needed.

Compilation/existing 103 bundle checks and focused departure checks pass: nine bundle displays plus seven supplies, valid catalog/SKU links and cooldowns, no solid Environment overlap, seven midpoint displays. See [evidence](validation/bundles/departure.txt). No Play Mode run; departure reach, purchase/spawn, cooldown sharing and multiplayer acceptance remain queued for playtest.

## Horizontal Duckz layout — November 12

User rejected the initial vertical stacks and requested Duckz layout, allowing overlap for manual adjustment. The shared wall now uses weapon–bag–avatar rows in both shops. Positions follow the visual centers of the inherited GorillaBundle template (byte-identical Store.prefab SHA256 in Duckz/Rails); bundle centers are 1.6 m apart. Character and placeholder visual sizes remain unchanged. Headings and the nine explicit per-item countdown labels copy Duckz TMP settings: CutePixel, white, centered, size 1.5; heading rect 0.8 × 0.35 m, countdown rect 0.4 × 0.2 m, baseline approximately 1.08 m (exact source per-item offsets retained). Cooldown labels remain bound to StoreWallItem and show the existing remaining time; no static timer text is saved. Names, localized prices/Owned and Equipped still use existing store behavior.

Use `Duck Rails > Bundles > Match Duckz horizontal layout` to reapply the authored arrangement; normal installation preserves later manual moves. Default Duck remains separate. Source-styling/order checks and existing 103 checks pass; [preview](../output/bundles/horizontal-preview.png), [evidence](validation/bundles/horizontal.txt). Earlier no-overlap placement evidence predates this authorized wider layout. Live readability/reach/countdown updates remain queued for playtest.

## Shared store in all three locations — November 12

User requested the same store in Compound, Departure Shop and midpoint helicopter, including seven standard supplies, all three bundles, Default Duck, SecurityCam and SecurityTV with HelicopterMirror. `Assets/DuckRails/Economy/RailSharedStore.prefab` is now the common content prefab. All standard prices use the original Store.prefab TMP labels and animated StorePriceCoin children underneath each item. The same bundle ownership and independent 120-second bag/weapon timers apply everywhere.

The shared prefab retains the helicopter store layout. It mounts at identity in RailParkedHelicopter, turned 90 degrees in RailShop at the starting station, and at 100% scale in the Compound shop module. The user explicitly permits overlap and will adjust the compound layout to fit. Earlier store/display/camera objects remain inactive, including the scene-added Departure Bundles. Gold-deposit receivers remain separate and unchanged. Shared GameManager registration now includes all standard Rails cargo for compound peer purchases. Each TV cameraMount points within its own shared prefab; existing 256-resolution/range-gated rendering behavior is retained. No backend change.

Edit `RailSharedStore.prefab` for future store layout/model changes; the earlier RailBundleWall remains a legacy authoring source. `Duck Rails > Store > Install shared store in all three locations` installs missing instances and preserves authored shared content; `Validate shared store` checks inventory, price coins, cooldowns, instance identity and camera links. Existing helicopter authoring checks now accept the full catalog. Earlier seven-only midpoint and isolated-wall layout evidence is superseded. Gameplay, coin purchase/delivery, checkout/restoration, countdowns and camera feeds across actual clients/headsets are queued for playtest.
`docs/validation/bundles/shared-store.txt` records passing final checks after reloading Expedition; Console error/exception query empty. The shared-store EditMode preview was inspected at `output/bundles/shared-store.png`. A Unity external-change reload dialog temporarily blocked follow-up MCP calls; reloading the saved scene restored checks without entering Play Mode.

Explicit acceptance requested: equip all three new avatars and verify correct local appearance and visibility to connected remote players and late joiners, including switching back to Default Duck and scene/rejoin persistence. Tracked in the shared-store playtest entry; not yet verified.
