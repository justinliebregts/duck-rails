# Player bounds and fall recovery

November 10 decision (user): blocks get invisible side walls plus last-ground fall recovery. Falling never downs a player. Loose items that fall below ground are removed from the network and deleted. Visible edge geometry (fences, cliffs, berms) is still welcome during each block's art pass, with the invisible walls kept as a backstop.

## Implemented behaviour

- **Walls:** `RailBlock.Awake` (Play Mode only) builds a `Player boundary` child with two 1 m `BoxCollider`s on the Environment layer, one on each side at local X = ±`playerHalfWidth` (default 48, which matches the 96 m ground on every block). They run the full block length, from 60 m below to 120 m above the block base. There are no end caps, so seams, the track and boarding are untouched. Static colliders only: no renderers, no networking, no navigation rebake. Enemy movement already clamps to the same ±48 m bounds.
- **Kill floor:** `min(enemyBounds.min.y, 0) - 10 m` below the block base. Examples: about -10 to -12 m on flat blocks, -29 m canyon, -22 m passenger station, -23.5 m finale. The block is chosen by Z footprint: station, travel blocks, midpoint, finale. Outside every footprint the floor is -10 m.
- **Fall recovery (local player, `RailPlayerBounds` on the route):** every 0.2 s while grounded, the client raycasts down from the body (Default/Environment layers). It stores the hit if it is a floor-like surface (normal.y ≥ 0.7), not a loose rigidbody, item, enemy or helicopter, at least 0.5 m inside the walls, and above the kill floor. Points on the train are stored train-relative. A 6-entry history keeps one entry per second. If the body is below the kill floor, or more than 1.5 m past a wall, for 0.25 s, the player warps to the newest entry using the normal `NewPlayer.Warp` / `DesktopPlayer.Teleport` path. A repeat fall within 6 s steps one entry further back. If there is no history, the player goes aboard the train. Downed state is unchanged.
- Disabled once the run is finished: the victory ride's existing two-second return aboard still applies.
- **Items:** `RailCargo` covers coal, gold, bags and shop gear. If the item is loose (not held, stowed, worn or mid-pickup), its Photon owner calls `PhotonNetwork.Destroy` once it crosses the kill floor. Bag contents go with the bag.

## Validation

- November 10: compiles with an empty Console. EditMode prefab check across all 19 RailBlock prefabs passes: 2 walls each, track and all coal/gold/enemy spawn candidates inside the walls, all spawns above the kill floor.
- November 11 live MPPM run (2 desktop clients): walls, below-floor and lateral recovery, repeat-fall step-back, moving-train recovery and below-floor item removal behaved as designed; see the [playtest queue](playtest-queue.md) results. Headset feel and wall climbing are unverified.

## Known limits

- VR hands can climb the invisible walls, the same as any Environment collider. Climbing past the wall is caught by lateral recovery.
- Points on helicopters and other non-train moving platforms are never stored. A fall from one returns you to the last ground point.
- The warp is instant (no fade).
