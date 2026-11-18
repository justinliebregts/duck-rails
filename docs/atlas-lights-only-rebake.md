# Lights-only rebake for atlas-integrated blocks

Use this when a block that already ships an approved Blender atlas (Convenience
Store, Skyscraper, Container Yard, Farm Silos, Motel) needs **only its baked lights
changed**, for example a light trapped inside its own lamp mesh. It rebakes the
block without undoing and rebuilding the atlas.

**Not for** mesh, material, lightmap-UV, renderer-setting or probe-position changes.
Those change the lightmap packing or the approval fingerprint, and still need the
full reconcile → rebake → regenerate atlas path in each block's atlas README.
The normal **Bake Open Block** / Prepare / Export commands refuse integrated blocks
on purpose (`Rail*AtlasProduction.RequireOriginalLightingWorkflow`); this procedure
does not use them.

## Editor tool (do it yourself)

**Duck Rails ▸ Lighting ▸ Block Lighting Tool** (`Assets/Editor/RailBlockLightingTool.cs`)
runs this loop on whichever block bake scene is open. It detects the block type:
atlas-integrated blocks get the lights-only procedure below; other blocks get the
standard **Bake Open Block** path.

1. **Preview open map lighting**: atlas blocks bind each chunk to its lightmap
   (unsaved, the same as the Farm review rebind); other blocks turn on Scene view lighting.
2. **Check baked lights for blocking meshes**: the ray check from step 1 below (26
   directions for point lights, 25 samples across a spot cone, front and back faces,
   adjustable probe distance; default 1 m). It lists TRAPPED / check / ok with the
   most-hit mesh and a Select button. Re-run it after moving a light to test the position.
3. **Add baked light**: creates a Baked light under `Added baked lights`, the
   last child of the block, at the Scene view pivot or copied from the selected light.
   Atlas blocks can also list renderers that cast no shadows during this bake only.
4. **Rebake**:
   - Atlas blocks, **Rebake atlas block lights**: applies only light Light/Transform scene
     edits to the prefab, reopens the scene from disk, runs `ValidateSaved`, snapshots
     placements, applies the temporary setup, bakes, reopens, compares placements,
     lightmap count and bake-folder GUIDs, and recaptures `bakedFixtures` if the light
     set changed. It refuses to start if the bake folder has uncommitted changes. When
     the result is good, it shows **Approve: record new fingerprint**, which writes
     `production-state.json` only when you click it. **Discard bake** runs
     `git checkout --` on the bake folder and keeps your light edits.
     Pending state is kept in `Temp/DuckRailsLightsOnlyRebake.json`.
   - Other blocks: **Rebake this block** calls Bake Open Block And Keep Open. That
     applies all the bake scene's prefab overrides.

The window opens with a **How to use** section listing these steps.
Recording the run (step 7) and the playtest-queue entry remain manual.
Scripts can call `RailBlockLightingTool.PreviewOpenScene()` and
`RailBlockLightingTool.CheckOpenScene(probe)` (returns the flagged list as text).

Verified 2026-11-17: compiles with a clean Console. Preview and check ran on the
Motel bake scene without dirtying it: 55 baked lights, 25 TRAPPED (lights inside
their closed, shadow-casting lamp-post, streetlight and pool-flood box meshes), plus
1 "check" at 1 m (the motel sign lamp behind its letters). The user then fixed
those lights with the standard path before the Motel became an atlas block
(November 17); the Motel now uses the lights-only path. The lights-only
rebake/approve/discard buttons have not been exercised yet.

## Why it works

- Each atlas chunk's UV2 was built in lightmap space from the original meshes'
  lightmap placement, and its binding points at the block's live lightmap texture
  with identity scale/offset. The chunks sample the lightmap; they do not hold a copy.
- The original source renderers stay in the prefab, disabled, and the bake scene's
  LightingData still holds their placements.
- If a rebake with the originals enabled packs every original into **exactly** the
  same lightmap index and scale/offset, the existing chunk UVs and bindings stay
  correct and only the lightmap texels change.
- The approval fingerprint (`Rail*AtlasProduction.Fingerprint`) covers renderers,
  meshes, materials and bindings, plus each bound lightmap's asset dependency hash.
  Light transforms are not in it. So moving lights leaves the fingerprint unchanged,
  and the rebake changes it only through the lightmap content. That change needs the
  user's explicit approval before it is recorded.

## Procedure

Use idle Duck Rails Edit Mode, verified through Unity MCP. Start with the block's
bake folder, prefab and `production-state.json` clean in git, and no dirty scenes.

1. **Preview and diagnose.** Open `<block>-bake.unity`. In Edit Mode the visible
   chunks show unlit because they were not part of Unity's own bake. For an unsaved
   preview, assign each enabled binding's renderer its lightmap index and
   scale/offset from `RailBakedBlockLighting.bindings`, like the Farm review
   rebind helper does. Find blocked lights by copying the shadow-casting
   originals into a preview physics scene as MeshColliders and casting rays out
   of each light both ways (front and back faces). Lights blocked in about 25 of
   26 directions, or across their whole spot cone, are trapped. About 9 of 26
   (only upward, under a housing) is normal for a lamp. Check each flagged light's
   **Shadows** setting before calling it trapped: a light with shadows off ignores
   occluders in the bake, so moving it out of its housing changes nothing
   (Skyscraper, 2026-11-17).
2. **Check the starting state.** `Rail*AtlasProduction.ValidateSaved()` passes.
   Every lightmapped original in the bake scene matches its prefab binding
   (texture and scale/offset). Save a snapshot of each original's
   `GlobalObjectId`, lightmap index and scale/offset to the scratchpad.
3. **Edit lights in the prefab.** Move, retune or add Light objects only: no new
   meshes, materials or lamp models. New lights must be **Baked**, and must go
   under an **`Added baked lights`** object that is the **last child of the
   prefab root**. Never insert them beside existing objects. Each guard finds
   its original meshes by child *position* (`sourceKeys` such as `3/0/12/4`,
   resolved with `GetChild`), not by name. A sibling inserted earlier shifts
   those positions. The guard then crashes with a missing MeshRenderer, or, worse,
   silently checks the wrong renderer. Renaming does not help. Test candidate
   positions with the same ray check first. If the user edited the
   prefab by hand, check the git diff: only Light and Transform changes, plus
   URP light data and reordered but byte-identical blocks. Confirm the
   fingerprint is unchanged afterwards. Also check that every entry in
   `ArtSource/<Block>Atlas/unity-meshes.json` `sourceKeys` resolves to exactly
   the set of disabled originals referenced by bindings.
4. **Set up the bake in the scene only, without saving.** Enable the originals
   that have a lightmap index, disable every chunk under the atlas root, and
   disable renderers matching the catalogue exclusions (`Helicopter`, `Bridge hinge`,
   `Spawn variation*`, `Exit rail`, `DAWN`). Only if needed, turn shadows off on a
   specific sealing mesh such as lantern glass, and record it. Confirm
   `Lightmapping.lightingSettings` is the block's `Lighting.lighting`, then
   `Lightmapping.BakeAsync()`.
5. **Verify the layout.** After the bake finishes, reopen the bake scene from
   disk without saving it, which drops the temporary setup. Compare every
   original against the snapshot. Keep the bake only if all of them are
   identical, the lightmap count is unchanged, and the lightmap and LightingData
   `.meta` GUIDs are unchanged (git shows no `.meta` change). Otherwise run
   `git checkout --` on the bake folder and use the full path. The
   `<block>-Lighting.unity` companion references the same LightingData, so its
   probes update with the bake.
   **If lights were added or removed**, call `CaptureBakedFixtures()` on the
   prefab's `RailBakedBlockLighting` (in `LoadPrefabContents`), save it, and check
   that `bakedFixtures` equals the prefab's Baked light count. Export normally
   does this. At runtime the game switches listed fixtures off because the
   lightmap already carries their light. An unlisted baked light would render
   as a second, real-time light, which is costly on Quest. The list is not part
   of the fingerprint.
6. **Preview and approve.** Re-apply the preview and show the user the changed
   areas. `ValidateSaved` now fails ("changed since approved integration") only
   because the lightmap's dependency hash changed. Record the new fingerprint in
   the block's active `production-state.json` (keep the GUID and format) **only on
   the user's approval**, then confirm `ValidateSaved` passes and the Console is
   clear.
7. **Record and queue.** Add a short entry to the block's atlas README or
   validation doc covering what moved, any bake-only settings, the layout
   result, and the old → new fingerprint. Add a playtest-queue entry for runtime
   and headset appearance. Commit only the prefab, the bake folder's
   LightingData and lightmaps, `production-state.json` and the docs.

## Runs

| Date | Block | Lights changed | Layout | Fingerprint | Record |
|---|---|---|---|---|---|
| 2026-11-17 | Farm Silos | 4 yard posts, 1 attic spot, 5 pond lanterns (pond lantern glass shadows off during the bake only) | 907/907 identical, 1 lightmap | 4a583248… → 59a2d0ac… | [Farm polish README](validation/farm-silos-polish-2026-11-15/README.md#lights-only-rebake-2026-11-17-user-approved) |
| 2026-11-17 | Container Yard | 4 yard lamp-post lights moved out of their poles; user retuned intensity/range and added 19 baked lights (now under `Added baked lights`); fixtures 40 → 59 | 1,191/1,191 identical, 2 lightmaps, 982 probes | bf95ae3b… → 4dc2e6d7… | [Container Yard atlas README](../ArtSource/ContainerYardAtlas/README.md#lights-only-rebake-2026-11-17-user-approved) |
| 2026-11-17 | Skyscraper | 18 worklights moved out of housings (shadows off, so no visible change); user raised 2 guide lights 8 → 15, nudged 1 fill and added 9 baked point lights (`Added baked lights`); fixtures 53 → 62; raised sign off during the bake only | 2,788/2,788 identical, 3 lightmaps, 439 probes | f17b5243… → 3cf2f85d… | [Skyscraper atlas README](../ArtSource/SkyscraperAtlasLowPoly/README.md#lights-only-rebake-2026-11-17-user-approved) |
