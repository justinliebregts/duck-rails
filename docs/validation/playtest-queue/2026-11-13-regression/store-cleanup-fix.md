# Shared complete store: Compound purchase cleanup regression

November 13, 2026; Unity 6000.3.24f1; three real authenticated MPPM clients. Starting tree `8522aae9f2065946a9e4e639b7ca0910e5871166` plus retained unrelated local edits.

The corrected wall-pull fixture reproduced a production failure in Compound: a flashlight and bag charged coins and spawned, then `RailWorld.ClearCargo` removed them on its half-second not-aboard cleanup. See `store-Compound-before-fix.txt`. The earlier parent-catalog/bag-grip fixture attempts are not this failure.

Fix attempt 1 tags store deliveries with room, run and buyer in retained instantiation data. Compound/staging readiness ignores current purchases, while cleanup retains them only for that buyer's current room/run (including an inactive reconnect slot). Hard departure removes them; private same-room departure clears them before expedition manifest maintenance. Ordinary expedition cargo keeps its existing behavior. Pickup pose accepts the extra lifetime field; bag request metadata remains backward compatible.

Live rerun:

- `store-fixed-Player2-Compound.txt`: actual flashlight 566 → 541 and bag 541 → 441; exactly one charge each, actual held network objects.
- `store-fixed-main-observer.txt`, `store-lifetime-Player3.txt`: matching IDs/tokens survive repeated cleanup ticks, late join and master handoff; readiness remains true.
- `store-buyer-leave.txt`: buyer hard-leave removes both purchases from the old Compound.
- `store-fixed-Player3-Staging.txt`, `store-private-departure.txt`: purchases in private staging survive, then same-room private Start reaches Running/WorldReady with no tagged purchases remaining.
- `store-fixed-Player2-Running.txt`, `store-fixed-expedition-main.txt`: both expedition stores still deliver actual flashlights and bags, charge exactly once and replicate their IDs/tokens to the peer without Compound lifetime tags.
- `cargo-floor-after-fix.txt`: actual coal/gold below the floor are still destroyed.
- `public-return-main.txt`, `public-return-Player2.txt`, `public-return-Player3.txt`: two actual public departures, failed bridge finish/normal return and control-connection disconnect return; all three gather in the same destination with zero cargo/enemies/revive roots and closed return control connections. Old 08:13 preparation timeouts in peer files predate the actual 08:37 run.

Unity refresh/compilation succeeded. MPPM initially delayed reload; recovered to idle Edit Mode without restarting the other project. Existing DX12 LCK and microphone-output-device errors, plus MRUK shared-context initialization errors on repeated Play entry, are separate environment issues; no new compile error. Headset reach, owned bundle equip/cooldown/checkout and real audio acceptance remain pending. All three accounts lack the new bundle entitlements; ownership was not fabricated.
