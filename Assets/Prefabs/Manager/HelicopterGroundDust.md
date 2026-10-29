# Helicopter ground dust

The HelicopterManager prefab references HelicopterGroundDust.prefab. At startup it
creates one local effect under each landing point unless that point already has a
HelicopterGroundDust child. Adding more children to LandingParent before entering
Play mode automatically adds more supported landing spots.

To customize a surface, drag HelicopterGroundDust.prefab under its Landing1,
Landing2, etc. transform and override the component settings (or use a prefab
variant). Keep it under the landing point, not directly under LandingParent,
where it would be mistaken for another landing spot.

- Dirt defaults are brown cubes. For concrete, try gray colors, sizes 0.025–0.08,
  full emission rate 25, and a cap of 40 live particles.
  Any small low-poly mesh can replace Particle Mesh.
- Adjust Ground Offset or the child transform to put the emitter on the ground.
  Height response is measured above the helicopter's landing position, so this
  artistic ground offset does not change the activation threshold.
- Activation Height defaults to 10 m. Ramp Exponent controls the strength curve.
  Ground level stays at full emission, including stayForever mode.
- Appearance settings configure the particle system on entering Play mode.
  The custom Inspector also refreshes the particle system when settings change
  during Play mode, so you can tune mesh, material, colors, and budget live.

## Preview without waiting for the helicopter

1. Enter Play mode. Select the HelicopterGroundDust scene instance under a
   landing point, or drag the base prefab into an empty test scene before playing.
   Select the scene instance, not the prefab asset in the Project window.
2. In its Inspector, under Dust Preview (Play Mode), click Play at Ground.
3. Look at the effect in the Scene view (select it and press F to frame it).
   Keep Play mode running and unpaused.
4. Drag Preview Height from 0 to 10 m to test the landing/takeoff ramp.
   Stop / Let Settle holds emission off while existing particles die.
5. Return to Helicopter Control ends the override and clears preview particles.
   Disabling the component also ends the override.

Preview controls are editor-only and do not affect headset builds. They work
even while the manager is waiting or visiting another pad. Inspector edits in
Play mode are temporary; copy component values before stopping and paste them
onto the intended prefab/instance afterward if you want to keep the tuning.

Budget: 80 live particles maximum per pad (hard limit 128), 55 emitted per second
at full strength, lifetimes 0.55–1.2 seconds. Only the selected pad emits; the old
pad can briefly retain its dying particles. Cubes use a single opaque, unlit URP
material with vertex colors, fog, and stereo support. There are no textures,
transparent dust layers, collisions, shadows, lights, ground raycasts, particle
GameObjects, or network messages. The ParticleSystem is built once at runtime;
edit the component's settings on the prefab rather than looking for a serialized
ParticleSystem component.

Check in Play mode:
1. During approach, no new dust at or above 10 m; progressively stronger below.
2. At touchdown and while parked, continuous full-strength dust.
3. During takeoff, decreasing emission; above 10 m, no new particles and remaining
   particles disappear within 1.2 seconds.
4. Repeat at each pad and with a concrete override; confirm ground placement.
5. Check both eyes and frame time on Quest 2. The conservative budget is not a
   substitute for profiling in the actual scene.
