# Duck Rails train production study

## Unity blockout — October 30, 2026

The user authorized a basic 3D implementation after the concept handoff below. The active `Assets/Scenes/Expedition.unity` now uses `Assets/DuckRails/Train/Prefabs/DuckRailsTrain.prefab`. This is an editable Unity primitive-mesh blockout with shared flat materials and a point-filtered radio grille, not the finished atlas-mapped art asset.

Nested station assets in the same folder:

- `CockpitChair.prefab`: centered low seat; separate pedestal, cushion and back. Visual seat only, with no camera snapping or seated-mode changes.
- `CockpitControls.prefab`: low dash, center grip throttle, fuel readout left and momentary horn right. Uses the existing `RailControl`, `PressableButton` and `RailHoldControl` interactions.
- `RadioTable.prefab`: table, plain receiver, textured grille, mic and physical TALK button. The existing radio hold/voice behavior moves with the table.
- `Furnace.prefab`: separate floor, ceiling and walls, a real inward-facing opening, hollow stack, internal insertion anchor, fire particles and motion-driven smoke.

Open the main train prefab in Prefab Mode to move the nested stations. Move a **module root** to carry its collision, controls, labels and anchors together; keep root scale at `(1,1,1)` because network coal validation retains its existing world-metre radius. Individual shell pieces remain separate MeshFilter/MeshRenderer objects under Hull and deck, Cockpit shell, Side rails, Wheels and Lighting. Applying nested-prefab changes edits that station asset. These are editor authoring modules, not networked pickup objects.

Blockout dimensions are implementation starting values for playtesting: original 5 x 10 m floor and passenger volume retained; roof at 1.65 m; sill at 0.20 m; seat top 0.18 m; dash top 0.27 m; table top 0.35 m. Furnace body is 1.6 x 1.4 x 1.6 m, mouth is 1.28 m wide by 1.08 m high, and stack walls extend 2.1 m above the collar. Furnace stays left and radio right, both outside the roof. The rear is open, with two 2 m side boarding gaps. No decorative coal or loaded backpack rack was added; the twelve functional supply spawn references and results UI were retained.

`RailTrain` and the original `Helicopter` passenger/cargo mechanism remain on the assembly root. `RailTrainAssembly` holds local module links and binds the expedition's furnace, gauge, results and supply references. Relocating a module preserves those references. Furnace requests keep the existing authority/ownership/deduplication protocol and additionally require the coal center inside the hollow chamber's acceptance box, on both the sending and authoritative sides. Fire and smoke are separate bounded particle systems (32 and 24 particles maximum); smoke emission is enabled only while aboard and moving. Headlight and the two side lamps use shadowless real-time lights. Actual headset cost is unmeasured.

The Week 3 scene builder now instantiates the existing train prefab when generating Expedition from the legacy Week2Baseline. It does not regenerate station geometry or overwrite prefab edits. The original baseline is retained for historical authoring; the live scene replacement is in Expedition. `Duck Rails > Train > Create Modular Blockout` is intentionally one-time and refuses to overwrite an existing train asset. Edit the prefabs thereafter.

Validation and previews:

- `Duck Rails > Train > Validate Modular Train` checks nested assets, scene bindings, hollow-mouth collision, low sightline, boarding clearance, relocated furnace acceptance, controls and effects. Evidence: `docs/validation/modular-train/editmode-checks.txt`.
- All eleven existing quick regression suites passed after installation.
- A live solo check confirmed the fuel readout and fire, smoke emission at 4 m/s, and no smoke emission once stopped (`live-effects.txt`). The baseline-to-prefab replacement was also exercised without saving the historical baseline (`builder-preservation.txt`).
- Two authenticated clients passed the existing coal integration suite while stopped, moving and after Master Client handoff, with exactly-once fuel/removal agreement. Evidence: `docs/validation/modular-train/coal-main.txt` and `coal-remote.txt`.
- The existing shared-rig live suite passed on one authenticated client, including actual PlayerHand grip acquisition of the new throttle, forward hand travel engaging the networked GO lease and release braking. Evidence: `docs/validation/modular-train/shared-rig-live.txt`. This uses the keyboard-driven shared VR rig, not a headset ergonomic test.
- `Duck Rails > Train > Render Blockout Views` regenerates six actual views of this same prefab in `output/train-blockout/`: front, back, side, top, three-quarter and a 0.5 m cockpit camera. These are shared-geometry renders, unlike the earlier generated concepts.
- Target-headset control reach, eight-player circulation, final lighting/particle performance and finished mesh/atlas work remain unverified. The geometric sightline test is not a substitute for headset review.
- Editor limitation: repeated Play sessions reported Meta MRUK global-context initialization errors and an `OVRPlugin.UnityOpenXR.OnInstanceDestroy` exception during shutdown. The recorded stack traces are in `docs/validation/modular-train/editor-xr-errors.txt`; no train-code frame appears in them. The coal and shared-rig checks completed successfully, but a clean repeated XR lifecycle is not claimed or fixed by this blockout.

October 31 results presentation: removed the opaque Results backing mesh from `DuckRailsTrain.prefab` and moved the existing text center from local Y=2.3 / Z=4.3 to Y=4 / Z=9 metres (about four metres beyond the deck front). Text remains attached to Run support and uses the same finish-triggered content/visibility. The Week 2 authoring code now creates this text-only layout; the historical Week2Baseline is unchanged, and Week 3 scene rebuilds install the edited train prefab. File structure/reference checks passed. Unity was not used at the user's request; in-game readability and headset placement remain unverified.
