# Side fence placement standard

Established November 11, 2026 at the user's request, measured from the saved Farm Silos prefab. When the user asks for fencing along a level's sides, use this placement unless they specify another value.

- **Inset the fence/wall centerline 1.0 m from the ground's lateral edge.** Standard 96 m-wide blocks extend to X = +/-48 m, so place side fences at local X = +/-47 m. Measure the actual ground edge for nonstandard widths; do not infer it from decorative scenery.
- This is a **centerline** measurement, not a face clearance. Farm's 0.16 m posts leave 0.92 m beyond their outer face. A 0.5 m concrete wall leaves 0.75 m.
- Keep the existing invisible player boundary/fall-recovery system as the backstop. Visible walls do not replace it.
- Follow ground elevation and leave no gaps beneath the collision. Keep railway entry/exit open. A side-fence request means the two long lateral edges, not end caps across the railway.
- Default continuous containment walls span the full block length. Farm's existing decorative fence runs Z2..158 on a 160 m block; that 2 m end setback is a measured farm detail, not a mandatory gap in future containment walls. Preserve flat ground at seams and avoid projecting geometry beyond the block.
- Height/material are task-specific. For Skyscraper Works the user requested concrete at approximately 3 m high; installed as two 3 m-high, 0.5 m-thick solid boxes, Z0..160.
- **No stretched pixel patterns:** bake dimensions into the mesh and project UV0 in world-sized units on each face (current Quackville concrete: one repeat per 3 m on both axes, Repeat wrap). Keep object scale at one; use separate nonoverlapping UV1 for lightmaps. Check horizontal and vertical texel density and end/top faces. Do not stretch a unit-cube texture over the entire wall.
- Check collision, spawn overlap, navigation, railway clearance, baked lighting and transitions. Gameplay/headset acceptance is queued under the project playtest workflow.

Measurement sources: `Assets/DuckRails/Blocks/farm-silos-v1.prefab`, `Env/.../Low poly boundary fence` (Boundary post positions X +/-47), and `Assets/Editor/RailFarmPolish.cs`, `Exterior`. Farm was inspected read-only.
