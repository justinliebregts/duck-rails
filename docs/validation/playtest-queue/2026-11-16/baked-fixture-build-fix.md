# Android baked-fixture build correction

First native build failed at RailBakedBlockLighting.cs45: Light.lightmapBakeType is editor-only. Actual prefab inspection also shows bakingOutput.lightmapBakeType=Realtime/isBaked=false on authored Baked fixtures, so replacing the getter alone would silently restore double illumination.

Runtime now suppresses an exported serialized list of authored baked fixtures, preserving realtime/mixed lights and originally disabled state. Both lighting exporters capture it; the legacy finale installer captures after cloning fixtures. A pre-build check rejects stale, missing, duplicate or null membership in all DuckRails lighting prefabs.

Unity serialized28 lighting owners and1009 fixture references. The migration retained only the added field and31 required nested stripped-Light records on StartingStation; reversing those additions reproduces every prior prefab byte (baked-metadata-preservation.txt). Unrelated art changes remain intact.

Fresh offline actual Play lifecycle on ten named production/review prefabs passes all load/translate/disable/reload/unload checks, suppression, three retained helipad realtime lights, and exact restoration including a deliberately initially disabled baked fixture. Console contains only the known LCK unsupported-DX12 startup error; no lighting or compiler errors after the run. See lighting-lifecycle-player-metadata.txt. Android rebuild and native validation remain in progress.


Commit boundary: three prefabs already contained unrelated user art edits. The index patch adds only fixture membership against HEAD: StartingStation14 legacy fixtures, finale24, Convenience64. All other25 prefab fields are clean isolated additions. The current working StartingStation has45 fixtures, including31 nested CityStationV3 lights; those additional membership/stripped-reference changes remain with the existing uncommitted CityV3 art. Thus committed legacy metadata totals978 fixtures while the actual tested working tree totals1009. No existing art is staged or reverted.
Android rebuild PASS: Succeeded errors=0, duration00:07:49.2006392. Native device checks still pending; see quest-build-attempt2.txt and quest-apk-sha256.txt.

Follow-up: InstallFinale now records prefab instance property modifications after capturing baked fixtures and before saving its scene. This retains the captured references as scene overrides. Source review and Unity compilation passed; no scene was rewritten for this follow-up.
