# Character bundles — November 12, 2026

## Huggy final art and flare gun — November 20

The artist's `Assets/Models/WeaponHuggy` (finger gun) and `Assets/Skins/Bag_Huggy` replace the Huggy placeholders. User decisions: melee like the pan, plus a flare shot. The cooldown belongs to the gun. The flare glow lasts 15 s. A direct enemy hit does small damage.

- **Bag** (`BagModel_Huggy.prefab`): 8 `BagSlot` instances, 2 × 4 as in Rambro, placed inside the recessed front frame (x ±0.05, y 0.076 to −0.128). Plates use the FBX `Slot` mesh at 0.8 scale. `Bag_Huggy.fbx` import scale is changed from 1.0 to 0.7, the same as the default and Scrap Duck bags; at 1.0 it was 40% larger. GrabLeft/Right move to x ±0.175. Bag ID 6 is unchanged.
- **Weapon** (`Weapon_Huggy.prefab`): still a `WeaponFryPan` variant, so pan melee, sounds and the Photon/RailCargo setup are unchanged. The pan mesh is replaced by the Huggy mesh at scale 1: pistol grip at the origin, finger pointing +Z. The convex MeshCollider uses the gun mesh, the BoxCollider covers the grip, and the grab surface is rebaked. New children: `Muzzle` at the fingertip and `ChargeBar/Fill`, a green unlit bar in the forearm slot (`HuggyFlareCharge.mat`). `WeaponHuggy.mat` now uses the Huggy diffuse and emissive textures. The committed version pointed at the Scrap Duck diffuse; this fix was made in the editor during this work.
- **`HuggyFlareGun`** (`Assets/Scripts/Items/HuggyFlareGun.cs`): any use button on the holding hand fires when charged, the same input as the flashlight. The charge is `lastShotAt` in server milliseconds, kept on the gun. It is set by the shot RPC and observed by the PhotonView, so new holders and late joiners see the right bar. The bar scales from the cuff end toward the hand and stays full when charged. A shot is `RpcFlareShot(key, origin, direction, serverTime)` to the other clients. Each peer simulates the same path from the timestamp, catching up any delay. Only the shooter tests enemies. On a hit, it calls `Enemy.SubmitThrownHit` at the enemy's minimum hit speed plus 2.5 m/s (the light push) and sends `RpcFlareDeflect` so peers follow the deflection.
- **Shot flare** (`DeathFlare.SpawnShot`, prefab `Assets/Prefabs/Effects/HuggyFlare.prefab`): a copy of DeathFlare recoloured yellow/orange, with its own glow material, a 15 s `lifetime` and an `Impact Sparks` burst. It moves in fixed 1/120 s steps: 32 m/s straight; after 25 m it floats down at the death-flare descent speed. On a Default/Environment surface, it sparks and reflects at 30% speed, then falls with gravity and drag. It rests on a floor-like surface once slow, or after 5 bounces. Death flares keep their 40 s burn and vertical launch. Because it is a `DeathFlare`, enemies fear it through the existing `FindNear`/`AllowsMovement` path, and the nearest-light selection applies.
- **Store displays** (`RailSharedStore.prefab`, legacy `RailBundleWall.prefab`): use the same meshes, so ItemPickupPose matches them. The gun is in side profile at 0.45 scale; the bag is at 0.6 scale with 8 plates. Pull colliders are refit.

Known limits: shots are not restored for players who join after the shot, since they last only 15 s. Peers check environment bounces against their own colliders, so a shot that grazes the moving train could land slightly differently on each client.

Checks: `Duck Rails > Bundles > Check Huggy flare gun` (`HuggyFlareChecks`) simulates open-air flight (about 16 m at 0.5 s, stop near 25 m, slow descent), a wall deflection to rest on a floor, the 15 s burn and the 10 s charge. `RailBundleChecks` now expects 8 slots for Huggy and Scrap Duck and checks the gun wiring. These pass in Edit Mode, together with PickupPose, shared-store and SpiderFlare checks. The batch-only `DeathFlareLightChecks`, `DeathLifecycleChecks` and `SmasherRetreatJumpChecks` were not run because the editor was open. Previews: `output/bundles/huggy-final-art.png`, `output/bundles/huggy-charge-bar.png`. Firing feel, enemy fear and push, peer and late-join sync and headset readability are **queued for playtest**.

## Scrap Duck final art — November 20

The artist's `Assets/Models/WeaponScrapDuck` (shovel) and `Assets/Skins/Bag_ScrapDuck` replace the Scrap Duck frying-pan and four-slot placeholders. Quackbear and Huggy still use placeholders.

- **Shovel** (`Weapon_ScrapDuck.prefab`). User decision: it works exactly like the nail bat. The prefab is now a variant of `Assets/Prefabs/Weapons/WeaponNailBat.prefab`, not the pan, so it inherits the bat's grab, rigidbody, ItemSounds (nail-bat audio) and Photon setup. The bat mesh is removed and the shovel FBX added. The shovel is scaled to the bat's 1.0 m length (0.6175×), with the D-grip at the bat's handle end. The BoxCollider is refit and the grab surface rebaked. The prefab GUID, root fileID, RailCargo observation and GameManager pool entry are unchanged.
- **Bag** (`BagModel_ScrapDuck.prefab`). User decision: 8 slots instead of 4, following the Duckz Rambro layout (2 columns × 4 rows). The rows are packed at 0.08 m spacing (y 0.105 to −0.135, x ±0.05) to fit the scrap bag's recessed front frame. Slot plates use the FBX's own `Slot` mesh at 0.9 scale. All eight slots are `BagSlot.prefab` instances. Bag ID 7 is unchanged. No code change was needed: slot count comes from the children of `Slots`.
- **Store displays** (`RailSharedStore.prefab`, legacy `RailBundleWall.prefab`). The pan and basic-bag miniatures are replaced with the same meshes as the held items, so ItemPickupPose still matches them. The shovel stands upright at 0.5 m, blade facing out. The bag is at 0.6 scale with eight plates. Pull colliders are refit. No location prefab or scene overrides targeted the replaced children.

`RailBundleChecks` now expects 8 Scrap Duck slots (4 for the others). It also requires the shovel to be a nail-bat variant using the new meshes. Bundle, PickupPose and shared-store checks pass in Edit Mode. Preview: `output/bundles/scrap-duck-final-art.png`. Combat parity, reach and readability of the slots on a headset, and multiplayer behavior are **queued for playtest**.

## Low supplies-room layouts — November 16

User requested wall-aligned, low displays reachable by roughly 0.5 m Gorilla Tag-style ducks in Compound and Starting Station. These two locations now override the shared store's transforms: all 17 displays sit low on the walls, with mesh bottoms at 0.25 m and tops at 0.37–0.675 m, lowered price/countdown labels and grouped bundles. The standard bat/pan lie sideways. Camera/TV prefab defaults were lowered; Justin subsequently tuned their final positions, rotations and scales through Compound/Expedition scene overrides, including disabling the expedition camera bracket. Compound uses its east, north and west wall sections, retaining both entrances; Starting Station now wraps Quackbear onto the south wall, shifts Huggy/Scrap south along the west wall, and places Default Duck, bat and pan farther along that west wall. The five remaining north-wall supplies have 1.05–1.15 m center spacing; the monitor stays on the west to avoid the northeast barrel. This spacing revision follows the user screenshots; heights and stand-free presentation remain unchanged. Use the Starting Station-only fit command to preserve Compound placement.

Location layout edits now belong in `SubwayTerminal.prefab` / `RailShop.prefab`, or the explicit `Duck Rails > Store > Fit low supplies in Compound and Starting Station` authoring command. The shared catalog/models still come from `RailSharedStore.prefab`; its helicopter layout is unchanged. Reapplying the fit command replaces these authored location placements. User follow-up removed all item and monitor stands; the authoring command no longer creates them. Camera wall brackets remain. Existing room lightmaps are unchanged. Expedition inherits the station overrides without resaving its unrelated scene edits.

Compilation, saved inheritance, all 34 actual mesh height checks, pairwise display bounds and existing shared-store/active Compound checks pass; Console errors/exceptions empty. [Evidence](validation/supply-room-layout/editmode.txt). Earlier native previews show the superseded stand layout; stand removal preserved every display position. Runtime purchases/restock, multiplayer and headset reach/readability are **queued for playtest**, not verified.

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
