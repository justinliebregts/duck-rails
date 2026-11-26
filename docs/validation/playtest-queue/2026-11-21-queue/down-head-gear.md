# Downing drops head equipment — November 21 two-client run

**Result: PASS in all 4 two-client rounds. Headset feel still needs a human.** No game code was changed.

## Setup

- Unity 6000.3.24f1, main `3a3392865` with unrelated local edits. Main editor plus MPPM Player2 (`4 Diagnostic Two Client`), two authenticated accounts, real Photon, quick start off, one private room (code join), editor "Invincible from enemies" on (it does not block the explicit down requests below). No headset.
- Harness: `Assets/Editor/Checks/Networking/RailDownHeadGearLiveChecks.cs` (editor-only, opt-in; `RailDownHeadGearLiveChecks.Start()`, coordinated through `Temp/rails-down-head-gear/control.json`). Runtime code was not instrumented.
- Each client backed up its account's 9 saved loadout keys (main had 6 set, Player2 had 0), cleared them, and restored them at the end. Both logs show `RESTORED`; no backup was left unrestored.
- The master stopped the train at the start (speed 0.00 throughout), so the aboard round dropped gear on a stationary train.
- **Gear per round:** the downed role spawned a test-tagged helmet and night vision goggles (`InstantiateWithRoomLifetime`, then `HeadEquipment.Equip` on its head anchor) and a loaded basic bag delivered to its back (`RoomRpc.SpawnBag(..., Back)`, contents `RailShopItemFlashlight|RailCoal`). Nothing was bought. Before each down, both clients confirmed: both items worn by the downed actor (≤0.05 m from its head anchor), bag `Stowed` on it, and the downed account's `equipment.head`, `equipment.eyes` and `bag.back` saves set, NVG effect active.
- **How downs were made:** off the train, a warp to train-local (7, 0.5, 0) then `RailExpedition.RequestDown()` or `RequestEnemyDown(<active enemy>)`. Aboard, the master put a natural `RailZombie` aboard 0.65 m from itself (speeds frozen, `Boarding.SetRemoteFrame(true)`, not dormant) and sent `RequestEnemyDown` with it. The master accepted it (aboard, boarded attacker), then the harness destroyed that enemy.
- **Revive:** the rescuer warped 0.5 m beside the downed duck and the master's normal hand-near revive ran (3 s). It completed on the first placement every round.
- **Pick-up and re-wear (after revive):** the rescuer took the helmet and the revived player took its goggles and bag. For an item it owned, the harness moved the item to the hand and injected a grip press. For a peer-owned item, it held the grip and called `PlayerHand.TryGrab`, which runs the ownership request. To re-wear, the item was moved so its equip point sat on the head anchor and the hand released it (`HeadEquipment.HandleReleased` → `Equip`). For the bag, it was moved to the back zone and `BagInventory.OnGripReleased` stowed it. Faked: item positions at grab/release and the grip input.

## Checks (`down-head-gear-main.txt`, `down-head-gear-player2.txt`)

What was measured after each down, held for 5 s on **both** clients:
- Exactly 1 test helmet and 1 test goggles copy, with no other helmet or goggles copies (0 before, 0 after).
- Neither item is equipped or held, and neither is parented under any player.
- Each item is within 1.5 m (horizontal) of the downed duck. Exactly 1 test bag is `Dropped` within 1.5 m.
- On the downed client: nothing worn, no back bag, saves `head=''`, `eyes=''` and `bag.back=''`, and the NVG effect and spotlight off.
- On the observer: no head item equipped by the downed actor.

Both clients reported the same item positions. Drop distances were 0.18–0.76 m.

| # | Downed (master?) | Way down | Drop on both clients | After revive (5 s) | Late joiner | Pick-up / re-wear | Result |
|---|---|---|---|---|---|---|---|
| 1 | Player2 (non-master) | off train, `RequestDown` | **PASS** helmet 0.49–0.55 m, goggles 0.71–0.76 m, bag 0.53–0.59 m | **PASS**: nothing came back to the head; saves still empty; NVG off | **PASS**: main (master) left the running expedition for a public Compound, then rejoined by code as a new non-master actor. It saw 1 loose helmet, 1 loose goggles and 1 dropped bag beside the revived duck, none on a head. The stayer saw no change. | **PASS**: main picked up P2's helmet (ownership moved to main; P2 saw it held by main) and re-wore it (`head` save = helmet). P2 re-wore its goggles (`eyes` save set, NVG on again) and its bag (`bag.back` saved). | **PASS** |
| 2 | Player2 (master after round 1 handoff) | aboard, boarded `RailZombie` enemy-down | **PASS** helmet 0.62–0.66 m, goggles 0.18–0.24 m, bag 0.55–0.61 m on the deck | **PASS** | – | **PASS** as round 1 | **PASS** |
| 3 | main (non-master) | off train, `RequestDown` | **PASS** | **PASS** | – | **PASS**: P2 took and re-wore main's helmet; main re-wore its goggles and bag | **PASS** |
| 4 | Player2 (master) | off train, enemy-down request (`RailSpider`) | **PASS** | **PASS** | – | **PASS** | **PASS** |

At the end of every round, each client saw for 5 s: one copy of each item (helmet on the rescuer, goggles and bag on the revived player) and no extra copies. The revived player's saves were `head=''`, `eyes=RailGoggles` and `bag.back` set. The rescuer's saves were `head=RailShopItemHelmet`, with the other two empty. The NVG effect was active only on its wearer.

Harness notes (not game failures):
- In every round, the rescuer's first `TryGrab` of the peer's helmet succeeded and the owner saw it held. The injected grip, though, applies to both hands, so the empty left hand then took the item from the right hand. The harness required the right hand and re-took the item, which was now its own, on the next attempt.
- For the goggles, the move-to-hand plus grip press never caught them, in every round. The grip press worked for the helmet and bag. The direct `TryGrab` fallback then worked on the first try. This is likely the goggles' grab surface sitting away from the item's root. Real-hand grabbing of dropped goggles still needs a check on a headset.
- Console: only the known LCK graphics-API error. No exceptions.

## Not covered / Needs human

- A down by a real enemy contact (here, the editor sent the same `enemy-down`/`down` requests the contact path sends; invincibility was on). A down aboard a **moving** train (the train was stopped).
- Three or more clients, or a third player joining while the downed player is still down (with two clients, the only rescuer cannot leave while someone is down without ending the run).
- On Quest: how the drop looks and feels, the NVG view turning off when downed, and grabbing and re-wearing with real hands.
