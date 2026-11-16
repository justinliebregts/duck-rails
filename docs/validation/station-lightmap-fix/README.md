# Starting Station square lightmap patches — fixed November 16, 2026

Cause: `City_Deck` has an upward-facing exterior roof at Y=9, exactly coplanar
with the inward/downward-facing station ceiling. Bake rays from the interior
can hit the deck's backface rather than the ceiling front face. The station's
Default-Medium lightmap parameters require 90% front-facing hits. Invalid texels
are filled from neighbours, producing large rectangular patches. This was not
a floor UV unwrap or lightmap-resolution defect.

## Evidence

- Original matched camera: [before](before.png).
- Disabling filtering alone retained the patches: [unfiltered](unfiltered.png).
- At six platform positions, 128 hemisphere rays each hit City_Deck backfaces
  25, 34, 29, 20, 38 and 33 times (15.6–29.7%); all other hits were front faces.
- Moving only City_Deck up 0.25 m reduced backface hits to zero at all six
  positions. Ceiling UVs, floor geometry, lights and validity threshold stayed
  unchanged. The independent test bake restored original Auto filtering:
  [deck separation](deck-separated.png). Large patches disappeared.
- Production was separately rebaked/exported: [east platform](production-fixed.png),
  [west platform](production-west.png). Native URP camera renders, same east
  camera before/after. These replace the earlier audit's encoded-RGB previews
  as visual acceptance evidence for the fix.
- [Reproducible ray probe](backface-probe.cs.txt) and [production checks](checks.txt):
  768/768 front-facing hits; 91 bindings, zero texture/index/UV-transform
  mismatches; prefab 91 bindings and zero missing textures; two lightmaps and
  596 probes; runtime deck has no collider; no script compilation failure;
  station scene saved and reopened.

Unity documents [invalid texels and neighbour replacement](https://docs.unity3d.com/es/2020.2/Manual/GIVis.html).
The ray test is a geometric diagnostic, not an extraction of Unity's internal
validity buffer; the controlled geometry-only bake establishes the causal fix.

## Changes

- Production station bake scene and the reusable StartingStationV3 prefab:
  City_Deck local Y offset 0 -> 0.25. This is hidden, collider-free scenery.
- `Assets/Editor/RailCityStation.cs` applies the same offset when building from
  the FBX so regeneration cannot restore the coplanar surfaces. FBX, mesh UVs,
  authored interior geometry, collision, fixtures and shared materials unchanged.
- Rebaked StartingStation and exported its normal runtime prefab bindings and
  probe-only lighting companion. Retained resolution 16, max atlas 2048,
  interior scale 2, ground scale 0.05, all user light edits and Auto filtering.

No Play Mode or device test. Runtime companion appearance, translated station,
Quest readability and performance remain queued for playtest. Separate existing
UV-overlap warnings on props/exterior/track are not claimed fixed. Console check
also contained an unrelated Curl error 55; it was not interpreted as a bake failure.
No diagnostic reports inspected. Temporary comparison scenes/assets were archived
under `tmp/station-lighting-fix/DiagnosticAssets` and removed from Assets; the
production station bake scene was left open.
