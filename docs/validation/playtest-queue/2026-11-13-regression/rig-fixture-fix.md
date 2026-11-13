# Shared-rig fixture correction

Three correction attempts on Unity 6000.3.24f1, one authenticated desktop client, current working tree based on `3c64b55a4`.

The older fixture measured the rig root, but simulated room-scale input moves tracking space and the gameplay head. Its support floor also assumed root Y was foot height and intersected the departure-shop geometry. A frame trace showed the collision solver cancelling head movement against that scenery.

`RailRigLiveChecks` now measures the head, places its floor under the actual body bounds, and runs the isolated movement fixture above authored scenery. The third run passes support, actual movement, released-input stopping, two ordinary coal grabs, both releases/triggers, and fingertip button press/withdrawal. See `rig-fixture-fixed-attempt3.txt` and the preceding attempts/trace.

The whole broad fixture still FAILS at stage 9 (legacy lever placement/acquisition). Stop after the third correction; do not claim this suite passed. Separate focused physical throttle fixtures pass right and left hands independently. No production behavior was changed. The retained correction is validated for the support/movement/input stages it fixes; the remaining lever-fixture repair is deferred.
