# Farm Silos full-block Blender atlas

November 14, 2026. User-approved production integration is complete. See [integration/recovery](../../docs/validation/farm-silos-atlas/production-integration.md).
See [authoring evidence](../../docs/validation/farm-silos-atlas/README.md).

- Editable full-block asset: FarmSilos-FullMap-Atlas.blend, including retained
  corn/water/horsetail context, a hidden original-material reference and atlas chunks.
- Original saved prefab/bake: original.prefab / original-bake.unity, with metadata
  snapshots and dependency-sha256.tsv. Includes existing local edits.
- Blender-before-conversion.blend preserves all previously open Blender work;
  original other-block Blender files were not overwritten.
- inventory.txt, exclusions.txt, source.json, textures/ and exceptions.json record
  the actual source. manifest.json records geometry/atlas/surface-area evidence.
- export-source.cs.txt runs in verified idle Duck Rails and refuses an existing
  source snapshot. build_atlas.py runs in Blender in a new scene and refuses an
  existing final review. No production assets are written.
- export-exceptions.cs.txt / append_exceptions.py add reference-only probe-lit
  geometry to Blender; Unity retains the actual original renderers.
- unity-meshes.json carries exact Unity-coordinate mesh data and stable source keys.
  Assets/Editor/RailFarmSilosAtlasReview.cs checks the source dependency hash,
  refuses an existing destination, preserves source objects and imports one mesh
  asset with subassets plus one shared material.
- Use the Farm Silos Open full-map Blender review menu for the isolated review.
  Its editor-only rebind logic and scene-only baked-fixture preview suppression
  preserve correct lighting through reopening; see evidence for details.

Regenerate into a new versioned destination after source edits. Do not overwrite
these snapshots, reset production to them, run old full-block installers, or
modify other blocks to refresh this review. Review approval and production integration are complete; runtime/headset acceptance remains queued.

Production assets: Assets/DuckRails/FarmSilosAtlas (retain when cleaning review artifacts).
pre-integration.prefab and its metadata snapshot preserve the prior production state.
production-state.json records the approved rendering fingerprint; the production build guard
and catalogue lighting guards reject stale derived visuals or an unsupported bake/export.
