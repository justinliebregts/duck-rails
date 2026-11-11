# Midpoint helipad shop — November 9, 2026

## Loot placement — November 10

Added 21 coal and 21 gold candidates around the hut workbench/interior/roof, fuel bund, pad corner and tree roots on both sides. Initial tuning is **6 coal / 2 gold per run**, independent of crew size, matching the finale's fixed-count treatment. These are supplementary service-stop supplies; the four scavenging blocks retain crew scaling. The historical no-resource description below is superseded.

`RailRoute` now includes midpoint loot in the expedition's seeded resource lists. Updated clients use the `-special-loot-v1` matchmaking suffix to avoid mixing incompatible loot manifests. No enemies were added. See [placement and route evidence](validation/special-block-loot.txt). Physical settling, reach, balance and multiplayer acceptance are queued for playtest.

The live Expedition scene now references `Assets/DuckRails/Blocks/midpoint-helipad-v1.prefab` through `RailRoute.midpointBlock`. The old scene-owned Midpoint Shop and the size-only midpoint-shop-preview prefab are removed. The helipad preview was promoted with its GUID preserved. `RailShop.prefab` is retained because the departure station still uses it.

## Route

Normal runs contain four scavenging blocks plus a fixed 60 m helipad after encounter two. Test routes insert it after index `(count - 1) / 2`. Scavenging counts, resources and encounter selection are unchanged. Later block placements, finale world position, train path distance, visibility and Page Down skip account for the service segment. The extra distance uses normal distance-based fuel consumption.

`RailRoute.Blocks` remains the scavenging list; `TravelBlocks` inserts the scene-owned helipad for traversal, visibility and navigation bounds. Retained route layout version 2 rejects old placements. Photon AppVersion adds `-helipad-v1`, so all clients must use the updated build. The midpoint contains exactly one receiver with ID 2.

## Rotating the helicopter

Open the helipad prefab and rotate **Env > Parked helicopter** around local Y. This parent includes the entire cabin, displays, gold crate, camera, sound and wash. It is currently Y=180 degrees, with the nose facing the incoming train (toward block entry / negative Z). Preserve its position and scale; do not rotate the block root or its track anchors. Avoid rotating only HelicopterMesh, which would leave the cabin interactions behind.

The flight manager never controls this parent, so runtime will retain the authored rotation. Recheck rotor/rail/prop clearance and rebake the offline crawlgraph after changing the orientation because the fixed cabin colliders participate in navigation. After saving the prefab, use **Duck Rails > Helipad > Rebake Navigation**. The helicopter uses probes rather than static lightmaps; rebake lighting after changes affecting the surrounding fixtures or static geometry.

## Bakes and authoring

The helipad graph includes the fixed helicopter geometry: 7,656 nodes across the full 60 m footprint, with no enemy/resource spawn candidates. Navigation is baked offline. Graph assembly now permits integer-cell translations that are not multiples of 16 cells. It reindexes existing nodes/edges and stores cluster member indices so random cluster sampling can reach both sides of a translated chunk boundary; it does not bake or create navigation connections at runtime. Cross-block enemy edges are not introduced.

Lighting bake scene: `Assets/DuckRails/Lighting/Blocks/midpoint-helipad-v1/midpoint-helipad-v1-bake.unity`. Its exported companion is registered in Build Settings. Use the existing **Duck Rails > Lighting > All Blocks > Bake Open Block** command from this bake scene. The short block uses its authored pad/hut fixtures and length-aware probes; helicopter geometry is excluded from static lightmap binding.

## Validation

- Unity compilation, dedicated scene/route/graph checks: 175,035 assertions. Checks preserve graph topology, exact-cell lookup and split-cluster sampling; verify retained layout, receiver uniqueness, travel order, seam continuity, train positions/headings, visibility and 60 m skip. Evidence: `validation/helipad/route-checks.txt`.
- Block-order regression: 430 assertions across route lengths 1/2/3/4/5/12, all 24 normal orders and 100 deterministic seeds. Older optional blocks still report existing resource/enemy candidate shortfalls. Evidence: `validation/test-block-order/editor-checks.txt`.
- Navigation bake and lighting companion export completed. Parked-helicopter prefab binding checks passed.
- Real-client late joining/host migration, headset traversal/shop interactions and performance remain acceptance work; offline reconstruction is not a substitute.

- Offline Play Mode visibility proof passed across three route builds (forward/reversed five-block routes and one-block route), both travel directions, seams, lighting readiness and teardown. Evidence: `validation/helipad/play-mode.txt` and `validation/block-visibility/play-mode.txt`.
