# Held items carry between Compound and Expedition — November 21 two-client run

**Result: PASS on all 16 two-client rounds that ran (1 round skipped, see below). Headset feel still needs a human.** No game code was changed.

## Setup

- Unity 6000.3.24f1, main `24af84d48` with unrelated local edits. Main editor plus MPPM Player2 (`4 Diagnostic Two Client`), two authenticated accounts, real Photon, quick start off. No headset.
- Harness: `Assets/Editor/Checks/Networking/RailHeldCarryLiveChecks.cs` (editor-only, opt-in; `RailHeldCarryLiveChecks.Start()`, coordinated through `Temp/rails-held-carry/control.json`). Runtime code was not instrumented.
- Each client backed up its account's 9 saved loadout keys (main had 6 set, Player2 had 0), cleared them for the run, and restored them at the end. Both logs show `RESTORED`, and no backup was left unrestored.
- **Loads:**
  - *one*: a flashlight in the right hand.
  - *two*: coal in the left hand and a gold bar in the right.
  - *bag*: the role's own loaded basic bag in the right hand. Main's bag held `RailCoal|RailGoldBar|RailShopItemFlashlight|`. Player2's held `RailGoldBar||RailCoal|RailShopItemFlashlight`. The contents identify each bag.
  - Test items were room-lifetime spawns. Bags came from `RoomRpc.SpawnBag(..., Dropped, storePurchase)`. Nothing was bought.
  - Before each round, each role reused its own copies from the previous arrival and destroyed any leftovers it no longer needed. This let every room be counted exactly.
- **How holds were made (real grip path).** The harness moved the item or bag to the hand's `HoldPoint`, or to the hand position for a bag. It then sent a grip press through `SimulatedRigInput` injected input. `PlayerHand.LateUpdate` ran `FindGrabCandidate`/`TryGrab`, or `BagInventory.OnGripPressed` for a bag, as it does for a desktop grip.
  - A hold counted as confirmed when `RailHeldCarry` would capture it: item `Holder` is that hand, `IsHeld`, not `PickupPending`, not stashed; bag `Held` by the local actor, not `AwaitingClaim`, within 0.7 m of the hand. It had to hold for 1.5 s, with nothing unexpected in either hand. The peer also had to see it held (`HeldActor` or the bag's `HolderActor` = the carrier).
  - All 27 grabs succeeded on the first injected press. The direct-`TryGrab` fallback was never used.
  - **Faked:** only the item's position at the moment of the grab (teleported into the hand) and the grip button itself (injected). The grip was released once `HasRoomRequest`/`InWorldTransition` showed the transition had started, which is "letting go during the veil".
- **What was measured on arrival** (held for 5 s in a row on each client, which also covers "nothing is cleared within 5 s"). For every role in the client's room:
  - Its items: test or carried views it created (`CreatorActorNr`). The keys must equal its load exactly, and each copy must carry the `rails-carry-v1` spawn tag.
  - None of its items in any hand, and the carrier's own hands empty.
  - Its bag: exactly 1 copy if the load was a bag, `Dropped`, with contents intact.
  - Every copy within 1.5 m horizontally of the carrier: own body collider for the carrier's own view, the avatar `helmetAnchor` for the peer's view.
  - For same-room private swaps, the original view IDs had to be gone.
  - A client left behind checked the old room: 0 items created by the departed actor, 0 of its bags, and the original view IDs not found.

## Checks (`held-carry-main.txt`, `held-carry-player2.txt`)

Carrier master status is as reported in each receipt. Drop distances were 0.29–0.38 m from the carrier in every round.

| # | Transition | Main load | P2 load | Result |
|---|---|---|---|---|
| 1 | Public Compound → public Expedition, P2 boards alone (non-master in the Compound) | – | one | **PASS.** P2 arrived as master and saw 1 carried flashlight 0.35 m away. Main stayed and saw nothing left in the old Compound; original 2002 was gone. |
| 2 | Public Expedition → Compound via Back to Compound (P2 alone, master) | – | two | **PASS.** P2 landed in main's Compound. Both clients saw 1 coal and 1 gold bar dropped at P2's feet. |
| 3 | Compound → other Compound by code | – | bag | **SKIPPED.** Round 2 already landed in main's Compound, so there was no second room to join from. |
| 4 | Public departure, both board | bag (master) | two (non-master) | **PASS.** Both clients saw main's bag (`Dropped`, contents intact) and P2's two items, each once. |
| 5 | Public failure crew return (`FinishRun(false)`) | one (non-master) | bag (master) | **PASS** |
| 6 | Public departure, both board | two (master) | one | **PASS** |
| 7 | Public success crew return (`FinishRun(true)`) | bag (master) | two | **PASS** |
| 8 | Private create (main leaves the public Compound) | bag (master) | – | **PASS.** Main's bag was in staging. P2 stayed and saw no main item or bag in the old Compound. |
| 9 | Private join by code | – | two (non-master) | **PASS** |
| 10 | Private departure (same-room swap) | one | bag | **PASS.** New view IDs; originals gone. |
| 11 | Private failure return | two | one | **PASS** |
| 12 | Private departure | bag | two | **PASS** |
| 13 | Private success return | one | bag | **PASS** (see observation) |
| 14 | Private departure | two | one | **PASS** |
| 15 | Private return with P2 downed (warped off the train, then `RequestDown`) | one | two | **PASS.** P2's hands dropped both items when it went down. After the return, both clients saw 0 carried copies and 0 bags for P2, and no originals left in staging. Main's flashlight carried normally. |
| 16 | Private departure | bag | one | **PASS** |
| 17 | Private Expedition → public Compound via Back to Compound mid-run | – (master, stays) | bag (non-master) | **PASS.** Main stayed in the running expedition and saw no P2 item or bag; original 506 was gone. P2's bag was dropped at its feet in a public Compound. |

Coverage notes:
- A gold bar was carried into the Compound in rounds 2 and 7. A flashlight was carried into the Compound in round 5, and the peer saw it, so the prefab resolved on both clients.
- **Observation (not a failure):** in round 13, the Player2 hand-held bag showed as not held for a single 0.1 s sample when the phase changed to Ending, then showed held again. It was still carried normally. No item hold flickered in any round.
- Console: only the known LCK graphics-API error. No exceptions.

## Not covered / Needs human

- Compound → another Compound by code (round 3): skipped, as above.
- Grabbing with real hands, letting go mid-veil, and how a drop at the feet looks and feels on Quest.
- Three-client runs (paused, because they crash Unity here).
