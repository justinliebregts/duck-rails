# Starting Station raised lettering — November 16, 2026

Converted all four current station labels (CONCOURSE, SUPPLIES, WASHROOM, OFFICE)
to actual solid 3D lettering as requested. Retained wording, placement, rotation
and measured label bounds. Each has 2 cm depth, a 4 mm outward offset, ivory
URP/Lit material, lightmap UVs and baked shadows. No colliders added. Signboards
and station geometry remain unchanged, including the corrected ceiling/deck gap.

Outlines use Assets/Fonts/CutePixel.ttf, the source identified by CutePixel SDF's
m_SourceFontFileGUID (64a757b9334401f4ebe6358e10e06aa6). No alternate font or TMP
default change. Other game/UI text is unaffected.

- [Concourse close-up](raised-sign-close.png): native Unity render with solid edges.
- [Station view](raised-signs-east.png): Office/Washroom and smooth station lighting.
- Four meshes, 4,772 triangles total, in Assets/DuckRails/StartingStationV3/Signs.
- Both V3 source and runtime StartingStation prefabs pass saved/reloaded checks:
  four valid MeshFilters with HideFlags.None, 0.02 m depth, no TMP label components.
- Lighting rebaked/exported: 95 bindings, zero texture/index/UV mismatches.
  Compilation passed. Final export/captures borrowed an idle/saved editor and
  restored the other task's skyscraper bake scene afterward.
- Persistence testing caught TMP's inherited DontSave MeshFilter flags; the
  converter now clears them and repairs already-converted labels. Final images
  and checks are after this correction.

Source: ArtSource/StartingStation/Signs/build_signs.py, layout.tsv and per-label
JSON. Run the script in headless Blender. RailStationMeshSigns.BuildAssets creates
the mesh assets; RailCityStation.BuildPrefab calls Convert automatically. Existing
generated assets retain their GUIDs on reruns. Deliberate outline changes require
regenerating the owned meshes followed by another bake/export.

No Play Mode/headset run. Companion loading and Quest readability/performance
remain queued for playtest; no device acceptance claimed.
