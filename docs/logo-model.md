# Duck Rails logo model — November 6, 2026

User-approved direction: custom chunky ivory DUCK / red RAILS, shallow low-poly extrusion, burgundy sides, sparse pixel cracks and railway underline. Subtitle: ESCAPE FROM QUACKVILLE. This is a modeled interpretation of the approved concept, not a pixel-identical reconstruction.

## Assets and use

- Drag `Assets/DuckRails/Branding/Logo/DuckRailsLogo.prefab` into a scene. It faces local -Z, Y is up, and its origin is near the center of the lettering. Default bounds are about 5.6 m wide, 4.5 m high and 0.26 m deep. Scale uniformly for the desired installation.
- Runtime prefab combines the sign into one mesh / one URP Lit material. No runtime generator, scripts, collider, network identity or embedded lights.
- `DuckRailsLogo.fbx` retains separate editable parts for the nine title letters, two rails, four sleepers and subtitle.
- `LogoPixels.png`: 256×256 atlas, point filtering, clamp wrapping, uncompressed, mipmaps enabled. Matte faces and burgundy sides. Cracks are texture details, not extra geometry.
- The approved title is custom polygon artwork. The extruded subtitle uses the project's `Assets/Fonts/CutePixel.ttf`, the source of CutePixel SDF; no substitute font or TMP default change.
- `ArtSource/Logo/DuckRailsLogo.blend` is the editable source with its texture packed.
- `LogoSpotlightPreview.unity` is a standalone presentation scene, separate from gameplay scenes. Preview-only objects use layer 31 to isolate rendering from other loaded scenes.

## Rebuild

Run Blender 4.4 in background mode with `--python Tools/Art/build_logo.py`, then Unity menu **Duck Rails > Art > Build Logo Prefab**. The builder regenerates the dedicated logo assets; edit the source script for repeatable geometry/texture changes. Manual Blender edits can instead be exported to the same FBX and imported with the Unity builder.

**Duck Rails > Art > Render Logo Spotlight Preview** writes `docs/validation/logo/spotlight.png`, saves the dedicated preview scene and restores the previously active scene. It does not install the sign in Login, Compound or Expedition.

## Validation and limits

Blender source/export: 3,520 triangles including the physical CutePixel subtitle. Unity compilation, FBX import, pixel-atlas UVs, material assignment, letter-hole appearance and isolated camera render were checked. See `validation/logo/editor-checks.txt` and `validation/logo/spotlight.png` for actual Unity evidence.

The current Mobile_RPAsset allows additional pixel lights but disables additional-light shadows. The preview retains that pipeline configuration; the concept image's strong spotlight cast shadows require baked lighting at final placement or an explicitly reviewed rendering change. No project rendering settings were changed.

Final placement, baked lighting, headset readability and device performance are not yet accepted. This deliverable is the reusable asset and preview, not an installation in a gameplay scene.
