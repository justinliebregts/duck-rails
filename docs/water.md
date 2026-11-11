# Water

November 10, 2026. Local audio/visual feedback for water; no swimming, drowning or damage mechanic is authorized or added.

## What exists
- `Game.Audio.WaterVolume` (Scripts/Audio): an oriented box whose top face is the surface. No colliders or trigger callbacks. Fields: size, water SurfaceType, enter/exit/bubble SoundEvents, bubble interval, underwater low-pass cutoff, fog colour/density.
- Wet contacts: `AudioSurface.TypeAt(collider, point)` returns the volume's Water surface when the point is under the surface (5 cm margin), else the collider's tag. Used by `FootstepEmitter` (local and enemy feet), `RemoteHandHitAudio` and local hand hits in `NewPlayer`. Existing `Water` entries in the surface sound sets are reused.
- Head under water: `WaterListenerEffects` (created by the first enabled WaterVolume) follows the AudioListener transform (the head camera). On going under: `Water_Enter` splash, low-pass on the listener (also muffles Photon voice), exponential fog tint and a bubble sound every `bubbleInterval` s. Surfacing plays `Water_Exit`, fades back over 0.15 s and restores the original fog. 3 cm hysteresis on the surface.
- Local only. Other clients do not hear a splash for your head and remote players' own heads are not tracked.
- SFX copied from `X:\Work\arpg` (Player water splash entry/exit and underwater movement) into `Assets/Audio/Player/Water`; events under `Assets/Audio/Events/Water`. The arpg 25 s UnderwaterGlide clip was not imported.

## Placements
- `lakeside-campground-v1`: `Lake water volume` under Campground greybox v1, matching the lake mesh envelope (X -38..-15, Z 51..109), surface Y -0.79, bottom -3. Max bed depth is about 1.2 m, so the head only goes under in the deeper part.
- `bridge-finale-v1`: `River water volume` over the Ravine collider, top 5 cm above the solid river floor (Y -11.45). The river surface is the floor, so this gives wet footsteps only; the head stays dry. Making the river deep needs a decision on the recovery design (see [final escape layout](final-escape-layout.md)).

## Status
Compiles with no new Console errors; the box maths was checked in Edit Mode. November 11 live MPPM run: submerging, teleporting out while submerged (exact fog restore) and the finale river (wet floor, dry head) behaved as designed, see the [playtest queue](playtest-queue.md) results. Sound levels, headset feel and Quest fog cost are still unverified.
