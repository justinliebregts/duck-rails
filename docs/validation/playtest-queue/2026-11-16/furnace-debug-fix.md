# Furnace fuel response — November 16

The committed `Furnace.prefab` had `debugOverride: 1` and `debugLevel: 0.261`.
This forced idle emission to 26.1%, bypassing fuel and boarding. The first live
control fixture correctly refused that configuration; see
`horn-fuel-ui-before-debug-fix-main.txt`.

The fix changes only `debugOverride` to zero. Debug level, artwork, particle
rates, fade speed and all fuel/network logic remain unchanged. For the first
two-client rerun, the existing instances were assigned the same corrected flag
because they predated the prefab edit. Fresh-load verification then passed on
both authenticated clients: both logged debugOverride=False without runtime
assignment, and repeated all five settled fuel samples with zero failures.
The final main run completed in 46.6 seconds with the fade observed; the peer
completed its 65-second window. See `furnace-fresh-main.txt`,
`furnace-fresh-Player2.txt` and the final `horn-fuel-ui-*` logs. The earlier
runtime-assignment results are archived as `horn-fuel-ui-live-flag-*`.

Unity 6000.3.24f1, two authenticated clients: five settled fuel samples
(50, 100, 50, 0, 100) passed on both peers. The actual native FUEL 100 button
action succeeded with receipt-aware retries. Rendered CutePixel labels, segment
counts and flame emissions agreed. Each peer observed one out-of-fuel cue and
one fire-relight cue. Horn long hold played once, did not loop/retrigger after
its clip ended, and the short release fade was sampled before stopping.
Main reported zero failures in 58.2 seconds; peer reported zero failures across
five settled samples in its 65-second observation window.

Evidence: `horn-fuel-ui-main.txt`, `horn-fuel-ui-Player2.txt` and
`horn-fuel-ui.cs.txt`. Button callbacks and operator positioning are injected;
actual mouse/VR touches, perceived audio and appearance remain Needs human.
