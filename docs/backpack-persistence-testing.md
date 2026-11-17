# Backpack downing and persistence acceptance

Status: **queued for playtest**, November 16, 2026. Unity compilation, Week 2
rules checks, and an empty Console error/exception query passed during
implementation. The inherited save and death-cleanup methods match the inspected
Duckz source. These checks do not establish runtime persistence or multiplayer
acceptance. Run only when the user authorizes the playtest queue.

## Contract

Downing drops worn and handheld bags intact, cancels pending bag claims, and
clears the saved worn bag. Contents remain inside; no separate spill is added.
Only a worn bag is saved in account-scoped UserPlayerPrefs. Removing it from the
back clears that save immediately. Rewearing saves it again, including changes
to its contents. Revival alone never restores the dropped bag. Room recovery
takes precedence over a stale disk copy, so reconnecting cannot duplicate loot.

## Setup and evidence

- Use two authenticated real clients with distinct accounts and the same build;
  add a third for late-join/observer cases. Disable DebugBagEquip and enemy
  invincibility for the downing cases. Record any test fixtures used.
- Record commit/build, platform, account aliases, room kind, master actor,
  network conditions and initial bag type/slot keys. Do not record credentials.
- Use a loaded basic bag with distinguishable slots (coal, gold, equipment and
  an empty slot), then repeat persistence with an empty bag and a bundle bag.
- Before and after each step, record each client's bag count, network view and
  recovery identity, owner/holder/state, exact slot keys, and the local account's
  `bag.back` presence/value. Include pending claims when testing races.
- Record Console exceptions, completion times and observed discrepancies.
  Store evidence under `docs/validation/backpack-persistence/` and link results
  from the queue. Do not read diagnostic report inboxes or local reports.

## Required scenarios

1. **Downing and revival:** Wear a loaded bag and hold another bag; down the
   wearer on the ground, then aboard a moving train. Both clients must see each
   bag drop exactly once with unchanged slots. The worn save must be absent;
   revival must leave bags on the ground. A teammate must be able to pick up,
   empty, refill and wear either bag. Repeated down/revive cycles must neither
   duplicate contents nor leave an invisible worn or handheld bag.
2. **Cold restart:** Wear a loaded bag, quit the application normally and
   relaunch. Restore exactly one bag of the same type with the same occupied
   and empty slots. Change a slot while worn, restart again, and verify the
   latest contents. Repeat with an empty bag and a bundle bag. Test application
   termination separately from a graceful quit and report its actual result.
3. **Save removal:** In separate runs, quit while the bag is held, lying on the
   ground, handed to a teammate, or dropped by downing. The original wearer
   must restore no bag. Rewear before quitting in a control run: restore that
   bag once. Removing one bag and wearing another must restore only the second.
4. **Account isolation:** Save different bag types/contents under two accounts
   on the same installation. Switching accounts and restarting must restore
   only each account's own saved bag. An account with no save gets no bag.
5. **Room transitions and cleanup:** Run public and private Compound ->
   Expedition -> Compound -> replay, both as master and non-master. Include
   success, failure and rapid relaunch. A worn bag must survive via restoration
   exactly once, after world readiness, with exact contents. Wait at least five
   seconds in each destination to catch repeated cleanup ticks. Take off a
   restored Compound bag and verify it remains a usable world object. No stuck
   loading, phantom bag or reappearance from an older save is acceptable.
6. **Reconnect and late join:** Disconnect/rejoin the same room while wearing
   a loaded bag. Recover the original when it survives; do not create a second
   copy. Repeat with another player holding the original room copy before the
   owner returns. A late joiner must see exactly the same bags, slots and poses.
   Also re-enter a different room and test recovery after the old room closes.
7. **Ownership races:** Down during a bag pull, pending ownership grant,
   stow/un-stow and slot retrieval. Repeat with latency/packet loss and with
   master handoff. Delayed grants must not re-equip a downed player or restore
   an invalidated save. Inventory totals across slots and loose items must be
   conserved; log any lost or duplicated item with its event sequence.
8. **Headset and moving train:** On target headsets, retrieve and rewear a
   restored bag, then down/revive while accelerating, braking and turning.
   Check grip release, locomotion shapes, visibility and passenger/cargo motion.
   Bag drops must not trap hands, move the player unexpectedly or desynchronize
   observers. Confirm normal crawling, flare and revival remain functional.

## Completion requirements

Record pass/fail per scenario with evidence; do not substitute editor fixtures
for real-client or headset results. Repeat transition and down/revive cycles at
least three times per room kind. Any duplicate/lost contents, stale saved bag,
unexpected world cleanup, failed restore or blocked room readiness is a failure
to fix and rerun. Leave unavailable headset/adverse-network coverage explicitly
pending. Mark the queue and milestone accepted only for the coverage actually run.
