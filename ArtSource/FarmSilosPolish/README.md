# Farm Silos revised Blender source

See [fixes, validation and recovery](../../docs/validation/farm-silos-polish-2026-11-15/README.md).
Built from current saved original geometry after scoped screenshot repairs and a
fresh Farm-only lighting bake. `before.prefab` preserves the pre-task source;
`original.prefab` is the corrected unchunked source after rebaking.

Background Blender: build_atlas.py, then verify_export.py. Repeats fit a bounded
1024-square atlas without subdividing geometry; oversized UV spans keep their
original materials. `unity-meshes.json` is imported by RailFarmPolishAtlas.
Never rerun the one-time migration or overwrite later authoring edits.
