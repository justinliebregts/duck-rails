# Motel east-wing atlas pilot

## Quest comparison follow-up

User approved Unity appearance and authorized the comparison. Native results and
remaining limits are in `docs/validation/motel-wing-comparison/results.md`.
The separate `Assets/DuckRails/MotelWingBenchmark` fixture shares 590 outside
chunks between a 130-chunk original-material wing and the 29-chunk atlas wing.
Use **Duck Rails > Tests > Quest Performance > Build Motel Wing Comparison**,
then the PC helper with `-MotelWingVariant control` or `-MotelWingVariant atlas`.
Only that explicit development build substitutes the catalogue reference. The
ordinary build command, production prefab and review prefab remain unchanged.
Both alternatives resident: memory/load results cannot predict a one-variant release.

Four valid views per variant at 120 m: promising wing-facing GPU/draw reduction,
mixed CPU results, both within sampled 72 Hz budget. The 56 cm train-stop difference
is retained as a limitation; the user accepted it and cancelled the repeat. No
more captures requested. Full runtime/headset/multiplayer acceptance remains open.

## Unity review import — November 13

User approved the Blender appearance and authorized a separate Unity review import.
Open **Duck Rails > Authoring > Motel > Open Blender wing review**. The scene is
`Assets/DuckRails/MotelWingReview/Motel-Atlas-Review.unity`; the full-block review
prefab is beside it. Scene View is framed on the east wing with Scene Lighting on.
Inspect exterior, room interiors, emissive fixtures and cut-boundary seams; the
Scene Lighting toggle allows neutral inspection. No Play Mode is needed for review.

The 29 Blender meshes are imported as native Unity mesh assets through an explicit
coordinate/normal/UV export (`unity-meshes.json`), avoiding FBX axis or UV regeneration.
One URP Lit material uses the base/emission atlases plus a Unity metallic-red,
smoothness-alpha map derived from the Blender surface map. Textures use Point,
mipmaps, clamp and uncompressed review imports. Compression is not yet tuned.

The clone retains the source objects, all collision and gameplay; only wing source
renderers are disabled. 32 generated render remnants retain outside-wing geometry
from shared/crossing surfaces. Cut-boundary duplicate faces were removed from those
remnants. Lightmap UV2 and original two atlas identities are retained; new bindings
use identity scale/offset. The review scene copies the original bake scene's lighting
setup and explicitly binds the textures. Production prefab, bake scene, catalogue,
navigation and build settings are untouched.

The old Motel runtime consolidation component/generated root are removed from the
review clone only, so it cannot double-render or overwrite the wing. The rest of the
review block uses original rendering: this is an appearance review, NOT a ready
performance A/B against the production consolidated block. Match that outside-wing
workload before any future performance comparison.

Import completed; Unity scene capture shows baked illumination on the wing. Authoring
helper compiled and the Console error query was empty before the final seam cleanup.
No test suites, Play Mode, build, device run or lighting bake performed. User Unity
visual review pending; runtime loading/rebinding and performance remain unverified.

## Original Blender pilot

November 13, 2026: user authorized one Blender wing for visual review before any tests.

Open `Motel-EastWing-Atlas.blend`. The active scene is `Motel east wing - atlas review`.
`PILOT - one shared material` is visible; `REFERENCE - original materials (hidden)`
contains the same extracted geometry with the original material assignments.
Toggle the collections mutually exclusively for appearance comparison. Review lighting
is neutral Blender lighting, not the game's baked nighttime lighting.

Scope: east guest-room wing, Unity X >= 22 and Z 54 through 94, both floors,
balcony, Stair A and rooftop equipment. The wing is extracted from saved geometry,
including clipping joined building shells at the wing boundary. No layout redesign.
Blender coordinates are (Unity X - 31, Unity Z - 74, Unity Y).

415 source renderers intersect the wing; the pilot has 29 chunks split by longitudinal
room bay, floor/roof and original lightmap identity. All use one material with three
1024x1024 atlas maps: base color, emission (8x multiplier), metallic/roughness.
Source textures retain their native resolution, with four-pixel edge padding and
nearest sampling. Source tint is incorporated into the base atlas. Repeating UVs
are clipped at texture-tile boundaries to avoid sampling neighbouring atlas tiles.
This increases geometry: 18,741 original triangles intersect the wing extraction;
the resulting clipped/chunked model contains 30,448 triangles. These are authoring
counts, not performance measurements or exact full-source before/after counts.
Texture seam/mipmap quality, color equivalence and the extra vertex cost remain
unvalidated. This pilot could lose performance despite fewer renderer objects.

Original atlas UV2 is retained in the second UV layer and lightmap asset identity
is recorded on each chunk. Unity lighting integration/rebinding is not installed.
TMP labels are excluded from mesh conversion; the existing CutePixel text remains
unchanged in the production level. No collision, navigation, gameplay, lighting
assets, prefab or scene was modified. No Unity asset import or runtime switch added.

`source.json` and `textures/` are a snapshot exported read-only from Unity using
`export-source.cs.txt` via the verified Duck Rails editor command tool. `build_pilot.py`
creates a separate Blender scene from that snapshot. Run once in a fresh review file;
re-running creates another scene rather than deleting existing user work.

Visual review only. No EditMode suite, Play Mode, build, Quest or performance tests
were run. User review is required before integration or tests. Queued for playtest.
