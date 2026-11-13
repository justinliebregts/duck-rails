# Character bundles — November 12, 2026

Quackbear, Huggy and Scrap Duck are installed in the compound's existing supplies-store module. Each has an avatar, a separate four-slot basic-bag placeholder and a separate frying-pan weapon placeholder. Default Duck is also available. The user's request authorizes these repeated bundle pulls in the compound; departure/midpoint coin shops retain seven standard supplies.

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
