# Train, furnace and throttle SFX checklist

Audio lives in `Assets/Audio/Train`. Implemented 2026-11-13 through `Assets/Resources/RailAudioBank.asset` (all slots), `RailAudioDirector` (`Assets/Scripts/Gameplay/RailAudio.cs`), and hooks in `RailControl`, `RailHoldControl`, `RailFurnaceModule`, `RailExpedition`. SoundEvent assets are under `Assets/Audio/Events/{Furnace,Train,Throttle}`.
Status: compiles with no errors; **queued for playtest** (see `docs/playtest-queue.md`). Nothing is verified in game.

## Supplied and wired
- [x] `Furnace_CoalThud_01.wav` (only 1 of the 3 variations supplied)
- [x] `Furnace_FireFlare_01.wav`, `_02.wav`, `_03.wav`
- [x] `Furnace_FuelChime.wav`
- [x] `Furnace_Fire_loop.wav` (plays while fuel is above 0)
- [x] `Train_Rumble_loop.wav` (volume fades in with speed, pitch follows speed)
- [x] `Train_Stop.wav` (only when the train comes to rest)
- [x] `Train_RailJoint_01.wav` (every 3.5 m; only 1 of the 3 variations supplied)
- [x] `Train_Horn_01.wav` (one-shot, fades out on release; only 1 variation supplied)
- [x] `Train_OutOfFuel.wav`
- [x] `Train_BridgeCrash.wav`
- [x] `Train_BridgeCrossed.wav` (ending music, loops and fades before the return)
- [x] `Throttle_Grab.wav`, `Throttle_Release.wav`
- [x] `Throttle_Click.wav` (25/50/75/100%, pitch rises with the step)
- [x] `Throttle_Latch.wav` (at 100%, on top of the highest click)

## Still missing
- [ ] `Train_LowFuelWarning.wav` (single beep, repeated below 20 fuel; slot exists and stays silent until the file is added)
- [ ] More variations (optional): `Furnace_CoalThud_02/03`, `Train_RailJoint_02/03`, `Train_Horn_02/03`

## Supplied but unused
- `Train_Start.wav` (acceleration is the rumble loop pitching up instead). Steam chuff was dropped.

## Adding the missing files
Drop them in `Assets/Audio/Train`, then add the clip to the matching SoundEvent (or `hornClips` on the bank for horns).
