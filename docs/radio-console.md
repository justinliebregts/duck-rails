# Integrated radio console — November 15, 2026

## Talk sounds — November 16

Train radio activation/release now reuse the handheld's existing WalkieTalkie_TalkOn
and WalkieTalkie_TalkOff SoundEvents, assigned on RadioTable's RailHoldControl.
Local hold edges broadcast through the same RoomRpc audio path as ItemSounds;
heartbeats and remote hold updates do not replay them. Forced release uses the off
cue too. No new audio assets. Unity compilation and saved radio/train binding
checks passed in Edit Mode; Console error query empty. Listening and two-client
acceptance are [queued for playtest](playtest-queue.md#train-radio-pressrelease-sounds--queued-2026-11-16).

User approved the integrated yellow-edged charcoal shelf, broader teal/cream
speaker, upright microphone, octagonal orange talk button, six sparse fasteners,
and short cable connecting the **backs** of the two housings. This replaces the
earlier freestanding-table art direction. User requested the full wall length.

## Deliverables

`ArtSource/RadioConsole/` contains editable `RadioConsole.blend`,
`RadioConsole.fbx` with embedded palette, `Palette.png`, approved concept,
`preview.png`, `rear-cable.png`, deterministic `build.py`, `verify.py`, and
`model-checks.json`.

Nine meshes: Shelf, Supports, Speaker, MicBase, ButtonMount, TalkButton,
Microphone, Label, Cable. Total 2,842 triangles, one shared palette material.
Grilles have actual openings. The planar label uses actual CutePixel.ttf.
TalkButton has its own press-anchor origin and metadata for its press direction
and proposed 12 mm travel. Preview wall/posts/floor/lights are excluded from FBX.

## Measured fit

Verified Unity MCP target: `X:/Work/duck-rails/Assets`. Read-only inspection of
the current train prefab's rendered Boarding rails mesh found post inner faces
at train Z=1.13 and 2.87: **1.74 m clear span**. Shelf width is 1.72 m with 10 mm
end clearances, depth 0.48 m, top at train Y=0.42. Speaker is 0.70 m wide and
0.444 m tall. Headset reach is unverified.

Blender X runs across shelf, +Y toward wall, Z up. For exact source-mesh
integration, map vertices to train coordinates `(1.64 + y, z, 2 - x)`.
Account for FBX axis conversion; do not apply it twice. Existing RadioTable root
is `(1.55,0,2.1)`; old interaction anchors do not automatically match the new
geometry. Preserve station/audio references, hold/release behavior and aligned
collision when installing. Existing train wall geometry is only review context.

## Validation and remaining work

Built via live Blender MCP, then rebuilt in Blender 5.3 CLI after the editor
closed during reset. Original SubwayTerminal file was clean before work and
was not overwritten. Corrected material/UV mismatches found during rendering.
Final front and rear renders reviewed. Static checks pass for nine meshes,
finite vertices, one shared material/UV layer, shelf dimensions, cream label
UVs/width, button metadata and cable clearance above shelf/in front of rear
strip. FBX reimport preserves all nine meshes and 2,842 triangles.

The Blender delivery was subsequently approved for Unity installation; see below.

From repository root, rebuild in a fresh Blender process with
`--background --factory-startup --python ArtSource/RadioConsole/build.py`.
Then validate/render with `--background ArtSource/RadioConsole/RadioConsole.blend
--python ArtSource/RadioConsole/verify.py`. Build opens its isolated output file
at the end, so do not run it over unsaved work in another Blender scene.

## Unity installation — November 15

Installed in the original `RadioTable.prefab`, inherited by `DuckRailsTrain.prefab`.
Existing RadioTable, RailControl, RailHoldControl, PressableButton, cap and TMP
identities are retained. Old table/legs/speaker/mic artwork is inactive and its
solid colliders disabled. Eight Blender render meshes plus the retained CutePixel
SDF label replace it. The label is stationary on the base; cap and icon move together.

Exact metre-scale geometry exported by `export_unity.py` is in
`Assets/DuckRails/Train/RadioConsole/`; this avoids FBX axis/scale ambiguity.
`Assets/Editor/RailRadioConsoleArt.cs` provides the scoped installer and checks.
The talk control is aligned with the sloped base; its fixed trigger encloses the
raised cap. Original 18 mm travel, speed, interactability, press/release events,
radio routing and runtime voice creation remain unchanged. Six simple/exact
stationary collision shapes replace old table solids. New renderers share the
train light-probe anchor. No gameplay or network script changed.

Validation: compilation and 24 saved-radio/train checks pass, including original
serialized gameplay configuration, single button/hold controller, CutePixel SDF,
cap/trigger alignment, 10 mm wall clearances and no missing scripts. Installed
button press/release/disable checks and nine existing hold lease/button checks
pass in Edit Mode. Offline train navigation rebuilt to 2,600 nodes / 31,906 edges;
rear and both side landings connect and existing boarding checks pass. The bake
used a disposable additive scene with external collider/blocker states restored;
the user's unsaved Compound scene stayed open and was not saved by this work.

Unity render reviewed; final Console error query empty. Temporary preview target
cleanup and destroyed-reference errors during initial verification were corrected
before successful reruns. Evidence: `docs/validation/radio-console/` plus existing
`docs/validation/enemy-boarding/editor-checks.txt`.

Live physical hand contact, microphone audio, moving-train/multiplayer behavior
and headset appearance remain **queued for playtest**. No Play Mode was started.

## Button alignment correction — November 15

Corrected Blender rotation composition: local octagon spin now precedes the base slope (Rx * Rz), instead of rotating the slope normal around world Z (Rz * Rx). The incorrect order tilted both octagonal solids sideways relative to the plate and icon, causing the left icon pixels to intersect the cap. Rebuilt Blender/FBX and updated only ButtonMount and TalkButton mesh assets in Unity, retaining asset GUIDs, prefab transforms, trigger and hold settings. Static Blender checks now assert matching face normals and at least 0.8 mm clearance for every icon vertex above the cap; these and Unity saved-prefab checks pass. Blender MCP was unavailable, so source rebuild used installed Blender 5.3 CLI. Live appearance remains queued for playtest.

