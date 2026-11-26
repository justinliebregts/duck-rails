# Loadout survives scene changes, quits, crashes and disconnects — November 21 two-client run

**Result: PASS on every two-client check. Headset feel still needs a human.** No game code was changed.

## Setup

- Unity 6000.3.24f1, main `2380603d0` with unrelated local edits. Main editor plus MPPM Player2 (`4 Diagnostic Two Client`), two authenticated accounts, real Photon, quick start off. No headset.
- Harness: `Assets/Editor/Checks/Networking/RailLoadoutTwoClientLiveChecks.cs` (editor-only, opt-in, coordinated through `Temp/rails-loadout-2client/control.json`).
- **Player2 is the mover.** It wore basic bag 2 holding `RailCoal|RailGoldBar|RailShopItemFlashlight|` (delivered by `RoomRpc.SpawnBag(..., Back, storePurchase)`), plus `RailShopItemHelmet` and `RailGoggles` (room-lifetime spawns equipped through `HeadEquipment.Equip`). This is the same setup `RailLoadoutLiveChecks` uses; nothing was bought.
- **Main is the observer** and the private room's master.
- Before the run, each client backed up its account's 9 saved loadout keys (main had 6 set, Player2 had 0) and cleared them. Both restored their keys at the end. In Edit Mode afterwards, main's 9 keys matched the backup. Player2's restore was logged in its own process.
- **What was measured:** every check had to hold for 5 s in a row.
  - Observer: copies in its current room of the test bag (matched by contents) and of each head item (matched by networked view). The bag also had to be `Stowed` with `HolderActor` = mover and at most 1.2 m from the mover avatar's `helmetAnchor`. Each head item also had to be `IsEquipped` by the mover's actor (`equippedActorNumber`) and at most 0.05 m from that anchor.
  - Mover: it wears exactly one bag and one item per head slot, with the right contents. The room holds exactly one copy of each. The `bag.back`, `equipment.head` and `equipment.eyes` saves are current.

## Checks (final run: `loadout-main.txt`, `loadout-player2.txt`)

| # | Transition / drop-out | Result |
|---|---|---|
| 1 | Baseline in a public Compound | **PASS.** One bag, helmet and goggles, all worn by #2. Measured 0.41 m bag-to-head and 0.000 m head gear. |
| 2 | Compound → public Expedition, mover departs alone | **PASS.** The observer stayed in the old Compound and saw no copy of any item. The mover wore one copy of each aboard. |
| 3 | Expedition → Compound via crew return (mover only, `FinishRun`; nobody downed) | **PASS.** The return landed in the observer's Compound. The observer saw one copy of each, worn by #3. |
| 4 | Compound → public Expedition, crew boards together | **PASS.** Both clients were in the same expedition. The observer saw one copy of each, worn by the mover. |
| 5 | Expedition → Compound via crew return, together | **PASS.** One copy of each, worn by the mover. |
| 6 | Public Compound → private staging (mover joins by code) | **PASS** |
| 7 | Private departure (same-room swap) | **PASS.** One copy of each in the room. New view IDs show the gear was removed and restored. |
| 8 | Private return to staging (same-room swap) | **PASS** |
| 9 | Second private departure | **PASS** |
| 10 | Transport loss (100% simulated loss): mover inactive, then rejoins the same run | **PASS.** The observer saw no bag, helmet or goggles copy for the 5 s after the mover went inactive. The mover then reclaimed actor #2 (29.9 s away) and both clients saw exactly one restored copy of each, worn by #2. |
| 11 | Clean leave mid-run (Back to Compound) | **PASS.** No copy stayed in the running expedition. The mover wore one copy of each in a public Compound. |
| 12 | Rejoin the running expedition by code | **PASS.** One copy of each, worn by the new actor #3. |
| 13 | Bag held in a hand (taken off the back through `BagInventory.Hold`, which cleared `bag.back`), then a client disconnect | **PASS.** While the mover was inactive, the room held one test bag `Dropped`, with its contents and not held, and no helmet or goggles. After the rejoin, the mover wore restored head gear (one copy each) and no bag. The single bag stayed dropped in the room. |

Drop-out notes:
- The disconnect in check 13 is `PhotonNetwork.Disconnect()` with no transition capture, so it behaves like a quit or crash. `PhotonSession` normally rejoins about 2 s later. The harness held that rejoin for 15 s, standing in for a relaunch, so the observer could hold its 5 s check.
- Only one simulated transport fault was used per client process.

**Run 1** (`loadout-run1-*.txt`): checks 1–12 passed. Check 13 was inconclusive. The automatic rejoin came 3.6 s after the disconnect, before the 5 s away check could finish. The room looked clean during that time. This was a harness timing issue, fixed by holding the rejoin before the final run.

## Not covered / Needs human

- **Needs human:** headset feel. On Quest, check that the restored bag and head gear look and feel right after each transition and rejoin.
- Three clients were not run, because three-client runs crash Unity here. A real app quit or crash with relaunch was simulated by the disconnect in check 13. The single-client quit/crash checks from November 18 still apply.
- Expedition → Compound by crew return empties the old room, so no client could check what it left behind. The Back to Compound check (11) covers what a leaver leaves in an expedition.
