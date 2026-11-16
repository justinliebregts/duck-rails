# Current-source Quest build and blocked survey

Build at ac918293b plus the preserved user working tree succeeded with0errors in
6m54.1798979s; APK2,552,541,888bytes. Hash and exact dirty-path provenance are in
quest-final-apk-sha256.txt and quest-final-build-working-tree.txt. Installation
succeeded on the connected Quest3. Desktop was Edit/NotRunning throughout.

Full survey95ab6f5b377f4e759420b79e12649a6c reached authenticated Compound Ready,
with app/VR/input focus, XR running and userPresent true. Positional tracking and
headTracked remained false while orientationTracked was true. It never passed
the tracked-pose startup gate:0valid of7,024frames over101s. Main explicitly
cancelled, collected and summarized the attempt. No performance-budget or
viewpoint acceptance follows; even its native GPU samples are not route evidence.

The game is stopped, request absent, proximity false, Meta CSV recording false,
and guardian/dialog/autosleep settings unchanged. Cleanup is independently read
back in quest-final-attempt-cleanup.txt. Raw dedicated capture remains under
output/quest-performance-queue-20261116-final/95ab6f5b377f4e759420b79e12649a6c.
A retry needs normal headset positional tracking in a lit, unobstructed location;
the user has been asked to signal readiness. This is a device tracking blocker,
not a sandbox-network or backend failure.
