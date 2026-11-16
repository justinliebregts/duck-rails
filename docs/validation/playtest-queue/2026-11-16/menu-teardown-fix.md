# Pause menu teardown audio — November 16

The open-menu room return logged `Some objects were not cleaned up when closing
the scene` and named `Audio Manager (fallback)` at 11:57:39 UTC. OnDisable played
the menu-close sound through AudioManager.instance after the scene's manager
could already have been destroyed; the singleton getter created a replacement
during teardown. The fix keeps OnDisable cleanup silent. Explicit TogglePause
and CloseForDeath still play their normal close cue.

Unity 6000.3.24f1 compilation and PauseMenuAudioChecks pass. The fresh two-client
room-return rerun passed all lifecycle assertions, restored the original private
room and rig state, and produced an empty Error/Exception/Assert Console after
the 12:21 UTC transition. See `menu-lifecycle-fixed-actor1.txt` and
`menu-teardown-console.txt`. The earlier raw menu identity assertion is separately a
fixture error: Compound and Expedition each own their menu and rig, and
PhotonSession loads the new scene with Single mode. A new menu is expected;
the required fresh-head resummon already passed. No persistent-menu behavior is
added. See `menu-lifecycle-actor1.txt` and the corrected fixture source.
