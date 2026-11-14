# Skyscraper Works full-block atlas review — November 14, 2026

**Production integration completed after user approval:** the original-GUID
production prefab now uses the approved chunks, with the triangle count unchanged.
Atlas material/textures/mesh container moved to `Assets/DuckRails/SkyscraperAtlas`;
review references remain valid. `pre-integration.prefab` and metadata preserve the
prior production state. See [integration and future authoring](../../docs/validation/skyscraper-atlas/production-integration.md).
The conversion-only notes below describe the earlier review stage.

Source: `Assets/DuckRails/Blocks/skyscraper-works-v1.prefab`, including the user's
saved uncommitted edits. Production prefab and dependencies remain unchanged.

Open `Skyscraper-FullMap-Atlas.blend` in Blender. The active Skyscraper scene has
394 optimized meshes and a hidden original-material reference collection. Unity's
CutePixel sign remains a Unity-only exception. Existing Blender scenes were not
deleted; the previously dirty session is recoverable from
`Blender-before-conversion.blend` (local safety snapshot).

Unity review: `Assets/DuckRails/SkyscraperAtlasReview/Skyscraper-FullAtlas-Review.prefab`
and the adjacent `.unity` scene. Menu: **Duck Rails > Authoring > Skyscraper Works >
Open full-map Blender review**. Do not run an old block installer to open it.

## Conversion and recovery

- Adapted Motel's workflow through the Convenience Store exporter/importer.
  `export-source.cs.txt` runs through Unity MCP against the saved source;
  `build_atlas.py` runs in Blender. Both guard existing review output.
- `original.prefab`, `original-bake.unity`, `source.json`, exported source textures
  and `dependency-sha256.tsv` preserve the exact starting geometry and dependencies.
  Existing source GUID metadata is recorded by the dependency manifest. The review
  retains all original objects, meshes, colliders and gameplay; only the 3,403
  selected source renderer enabled flags change.
- Thirteen native-resolution material tiles, four-pixel padding, 1024-square base,
  emission and metallic/smoothness maps. Base is sRGB, property maps linear;
  uncompressed Point sampling. Base mipmaps enabled; constant property mipmaps
  disabled to prevent cross-tile emission. Maximum source emission component is 2,
  retained with an HDR material multiplier of 2. Surface PNG also records Blender
  metallic/roughness; Unity uses metallic red/smoothness alpha.
- No source material has a textured emission/property map. No car-wash spin,
  Animator, Animation or Rigidbody hierarchy exists in this block. The Convenience
  Store textured HDR atlas and brushes were not accessed for conversion or changed.
- Spatial cells are 12 x 4 x 12 m, adapting Motel's six-metre vertical cells to
  four-metre tower floors. Split by lightmap/settings, retain oversized sources
  separately. Blender coordinates are `(Unity X, Unity Z - 80, Unity Y)`; explicit
  Unity-coordinate payload reverses this transform and winding on import.
- Clip repeating UV triangles, interpolate normals/lightmap UVs, and fold original
  lightmap scale/offset into UV2. No decimation or collision mesh replacement.
  `manifest.json` and `unity-meshes.json` contain counts and imported mesh data.
- 3,403 scenery renderers become 394 chunks with one material; one CutePixel
  MeshRenderer remains. Total 3,404 -> 395 (88.4% fewer enabled renderers).
  Scenery triangles 47,996 -> 228,866 (4.77x); output vertices 436,706.
  TMP-generated glyph geometry is outside those MeshFilter triangle counts.
  Maximum relative per-part surface-area error is 6.60e-13.

The source JSON and disabled original renderers are authoritative recovery data;
the hidden Blender reference uses the clipped topology with original materials.
No runtime switch or earlier generated scenery root exists on this source.
No production catalogue replacement, lighting/navigation rebake, Play Mode,
comparison APK or Quest test was performed.

See [authoring evidence and remaining issues](../../docs/validation/skyscraper-atlas/README.md).
