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

The following sections preserve the earlier 2D concept handoff and its decision history.

October 30, 2026. Package revision 5; deck layout revision 3 retained. A 2D handoff for the next train modeling and interaction review. The low-poly, pixel-textured look and mostly open-air layout follow the approved train concept. No model, prefab, scene, collider or gameplay code was changed. Previous revisions are retained as history. Both stations remain outside the cockpit, with the rear clear for boarding and the approved simplified radio and centered cockpit controls. The furnace now explicitly shows a real opening and hollow firebox.

## Deliverables

- [Five-view turnaround and atlas supplement](../output/pdf/duck-rails-train-turnaround-and-atlas-v6.pdf): front, back, right side, top and rear-right three-quarter concepts plus a 16-tile material atlas and key. [Turnaround PNG](../output/imagegen/duck-rails-train-turnaround-v6.png), [atlas PNG](../output/imagegen/duck-rails-train-texture-atlas-v1.png), and [built-in generation prompts](../output/imagegen/duck-rails-train-turnaround-v6-prompts.json). Furnace remains train-left, radio train-right, and chair/control station centered; front/rear projections reverse apparent screen sides. The microphone is explored at the table's inboard edge, aligned longitudinally with the receiver. These are concept views, not exact projections from a shared mesh; use the written layout for model placement. The atlas is a color/material draft without UV binding, padding or finalized game-resolution import settings. Gauge/icon decals and masks require separate authoring. The detailed v5 hollow furnace remains the construction reference.
- [Five-page production PDF](../output/pdf/duck-rails-train-production-v5.pdf): deck layout, top view, cockpit, approved radio, and hollow furnace. The latest furnace detail supersedes earlier filled-mouth illustrations.
- [Furnace revision 5](../output/imagegen/duck-rails-furnace-v5.png): actual open mouth, hollow cube interior, and separate fire particle FX. Retains the approved enlarged opening and three-times-taller stack from revision 4. Exact opening dimensions remain to be set. [Built-in image edit prompt](../output/imagegen/duck-rails-furnace-v5-prompt.json).
- [Top plan](../output/imagegen/duck-rails-train-top-plan-v3-centered.png): corrected empty center seat and aligned controls; stations outside the cockpit, no displayed bags, and an open rear boarding edge. This replaces the v3 image with an offset duck placeholder.
- [Cockpit](../output/imagegen/duck-rails-train-cockpit-v3.png): center seat; fuel gauge left, throttle center, horn right; two side lamps and headlight illuminating the track.
- [Radio and furnace detail](../output/imagegen/duck-rails-train-radio-furnace-v2.png): plain receiver, angular microphone with TALK button, and one furnace opening with connected thick stack.
- [Generation prompts](../output/imagegen/duck-rails-train-production-v3-prompts.json): built-in image generation, references and refinement prompts. The unchanged prop detail retains its [v2 prompts](../output/imagegen/duck-rails-train-production-v2-prompts.json).
- [Seat correction prompt](../output/imagegen/duck-rails-train-top-plan-v3-centered-prompt.json): remove the duck and center the empty seat/control station without changing the remaining layout.

Use PDF page 1 and this written brief for dimensions and equipment placement. Generated views communicate appearance; perspective, proportions and minor placement differences are illustrative, not dimensionally reliable engineering drawings. Heights in the vector diagrams share the same deck-zero datum.

## Confirmed requirements

- Mostly open-air crew deck; only the small front area may be lightly enclosed.
- Typical eye line approximately 0.5 metres above the local deck, supplied by the user. Windows must extend low enough for that posture; no standing or climbing required to see the track.
- A dedicated table carries a radio and separate upright microphone. Following the user reference, put the physical TALK button at the bottom of the microphone assembly, on its base, facing the player. Hold to transmit; release to stop. The button is on top of the table, not underneath it or on the radio's rear.
- Simplify the receiver: no modeled buttons or knobs. Optional control marks and the grille are flat pixel textures. Only the microphone's TALK button presents a physical radio interaction.
- A front headlight illuminates the track. The cockpit has left/right side lamps and no hanging lamp.
- A front-center seat has the throttle on the low dashboard directly ahead, fuel gauge to the driver's left and horn to the right. The user approved this arrangement. Seated-mode mechanics and camera changes are not authorized by the art drawing.
- Exactly one larger furnace sits outside the cockpit, at the front-left of the open deck as in the user's supplied top view. A thick smokestack rises straight from its top into open sky, without passing through the cockpit roof. Latest user revision: enlarge its single inward-facing opening beyond the former 0.5 x 0.5 metre target and make the stack three times its previous height above the casing. Exact new opening dimensions remain unspecified. No coal bin, pile or loose coal as decoration; actual scavenged gameplay coal remains functional cargo. Smoke particles emit above the stack while the train moves.
- The radio table is also outside the cockpit, opposite the furnace at the front-right of the open deck. Use the approved simplified teal/ivory receiver and mic design.
- Remove the displayed rear bags. The back has no transverse railing and provides boarding access. Preserve functional loose cargo and player backpacks; this concept change does not delete their gameplay systems. Earlier four-hook storage requirements need reconciliation before implementation; this drawing omits the loaded rack rather than inventing a new storage installation.
- Match DuckZ's simple low-poly geometry, visibly pixelated textures, legless avatars and long arms. Production performance still requires profiling.

## Coordinate convention and source footprint

Front is forward along local +Z; right is local +X when facing forward. Deck top is Y=0. In the PDF top plan, front is at the left of the page, so train-right is at the top.

`Assets/Editor/RailSceneBuilder.cs` authors a floor 5 metres across X by 10 metres along Z, centered at local `(0, -0.15, 0)` with 0.3-metre thickness. This source inspection supplies the **10 x 5 metre starting footprint**, not a fresh measurement of the active scene or proof of eight-player fit. Decorative buffers, wheels and other external shapes are outside this floor reference. Preserve the existing passenger/cargo behavior when eventually fitting new art.

## Proposed layout for review

All new placements and sizes below are proposals, not gameplay changes or accepted ergonomic dimensions.

- Front shelter roof zone: forward 2 metres (Z=3 to 5), leaving the remaining 8 metres open above. Center the low seat at X=0, Z=3.25 to 4.15; place the throttle ahead at Z=4.45 to 4.85, gauge at negative X and horn at positive X. Keep controls reachable and the dashboard below the low sightline. Exact dimensions remain proposals.
- Radio table: outside the roof on train-right, facing inward. Start with a 1.2-metre length along Z (1.5 to 2.7), 0.6-metre depth (X=1.6 to 2.2) and 0.35-metre tabletop height. Keep the mic-base button close to the inner edge, with a clear hand approach. Short cable runs behind the station.
- Furnace: outside the roof on train-left, opposite the radio. Proposed footprint: 1.6 x 1.6 metres, X=-2.3 to -0.7 and Z=1.1 to 2.7. Its single enlarged mouth faces inward (+X), toward the deck. Stack rises directly into open sky and is three times the prior concept height above the casing, with unchanged thickness; smoke starts above its mouth. The former 0.5 x 0.5 metre opening target is superseded. Final dimensions require review. Keep remaining faces solid and reconcile insertion anchors before implementation.
- Rear: clear deck to the open trailing edge, no rear transverse rail, bags, loaded rack or decorative cargo. Side rails end at the rear corner posts. The drawing omits the previous rear gear shelf to preserve this clear boarding approach; exact functional storage is unresolved.
- Boarding: open rear edge plus the retained opposed mid-deck side openings, initially 2 metres clear each. Amber edge marks on the vector plan denote rail openings, not raised bars. Preserve hand locomotion access and evaluate whether the rear chassis, coupler, rails or floor edges obstruct boarding.
- Circulation: a 2-metre central aisle is an initial target; confirm eight-player use with full-size bags and supplies. The drawing does not prove capacity.

## Low sightlines and radio reach

The user's eye-height reference is **0.50 m above the deck**. A **0.20 m window sill** and **0.35 m tabletop** are proposed starting heights for review. Both are measured from the deck, not from one another. Do not alter the player rig to force it to match these drawings.

The window section checks relative heights only. A clear horizontal line does not prove visibility of nearby track or hazards: evaluate downward view, distance from eyes to window, the nose, trim, cargo and the driving hand. Also check natural eye-height variation during locomotion.

The mic base should offer a large obvious contact surface with an angled cap. Place it where the low player can hold the button without reaching over the radio, colliding with table supports or being pushed away by locomotion collision. Preserve the existing fingertip hold/release contract. The receiver has no modeled controls. Seat posture and arm approach also need review without snapping or raising the camera to make the concept fit. The cockpit illustration is an overview from behind the seat, not proof of a 0.5-metre camera view.

## Model handoff and review gate

Confirmed modularity requirement: the cockpit chair, controls, radio table and furnace must be separate meshes that can be repositioned in Unity. Proposed delivery is one prefab per module, nested under the train assembly, with useful floor-level pivots and local transforms. Keep throttle, fuel gauge and horn as separate children of a movable control station; keep the radio receiver and mic/button separate children of the table station. Put the furnace shell, stack, shell colliders, insertion anchor and fire/smoke emitters under its module root so relocation moves the full working assembly. Editing station placement does not imply runtime grabbability or independent networked rigidbodies. Use shared materials where practical and preserve modularity when optimizing. These requirements are recorded; meshes/prefabs have not been created.

Confirmed furnace construction: the enlarged mouth is an actual hole into a hollow cube interior, with visible inner floor, walls and ceiling. Coal must pass through the mouth into that volume. Fire is a separate particle effect inside the firebox, never a solid glowing panel filling the opening. Keep the approved exterior shape and tall stack. In a future implementation, use collision around the shell rather than a solid collider sealing the mouth, and place the insertion acceptance volume inside; particle visuals do not determine fuel acceptance. These are handoff constraints, not completed Unity work. The current concept illustrates restrained particles so the cavity can be reviewed; final fire density remains a visual tuning decision.

Keep the hull/deck, front shelter, side rails, seat, low dash, headlight, side lamps, radio table, radio, mic/base/button, furnace/stack, controls and simple wheels separable for iteration. Use flat pixel textures for grilles, seams and wear instead of modeling tiny detail. The approved teal/ivory receiver uses a broad speaker face and an angular microphone. Amber is reserved for light and meaningful touch points. Smoke is a proposed motion-driven particle effect, not stacked mesh decoration or an implemented system.

Generated views can reverse equipment sides or vary spacing; use page 1 and the coordinates above as the proposed layout authority. The isolated furnace detail deliberately avoids implying a second furnace or an alternate train-side placement. Visual smoke does not establish a particle budget. Check headlight range and contrast in the target headset, then profile lighting and transparent effects.

Before final mesh detail, review the proposed layout in the existing rig, confirm reach and sightlines, then check eight-player boarding and circulation with actual equipment. Test button hold/release, control access, cargo motion and bag interactions when eventually implemented. Establish an art authoring workflow that survives scene-builder regeneration first. None of these runtime acceptance checks is claimed by this drawing delivery.

Design context: [art guide](art-guide.md), [game design](game-design.md), [execution plan](execution-plan.md) and [concept index](art-concepts.md). Rebuild the PDF with `Tools/build-train-production-sheet.py` and ReportLab.
