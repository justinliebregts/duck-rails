# LIV recording audio

## Local capture lips — November 15, 2026

Reported: talking does not move the local lips in LIV. Found a concrete skin
binding defect: LocalBodyDriver searched the body for Lip_T/Lip_B once, including
inactive default attachments, and retained those transforms after skin changes.
DuckSkinVisuals binds replacement faces only to the network face components;
the separate local capture rig could therefore animate hidden default lips.

DuckSkinVisuals now exposes its selected face and LocalBodyDriver reads that
binding before each external-camera pose. This also updates local capture eyes
and handles returning to the default skin or missing face parts. The existing
Photon microphone sampling, remote speech and camera visibility rules are unchanged.

Validation: verified Duck Rails MCP target in Edit Mode, refreshed/compiled,
and checked active selected lip references on isolated actual DuckPlayer prefab
instances with Quackbear, Huggy and Scrap Duck. All three passed; Console
error/exception query was empty and scoped whitespace checks passed. No saved
prefab changes or Play Mode runs. Actual microphone/LIV recording, skin switching,
default duck and mirror acceptance remain queued; the user's exact runtime cause
is not confirmed by these asset checks.

## Missing game mix — November 15, 2026

Reported symptom on Quest: recordings contain the local microphone but neither other
players nor game sounds. Normal headset playback while recording is not yet confirmed.

Found a concrete capture-lifetime defect in the embedded LCK 1.4.5 package:
`LckAudioMixer` is a persistent singleton, while `PhotonSession.LoadWorldScene`
replaces the scene and its DuckPlayer listener. The mixer stores its capture as an
`ILckAudioSource`; a destroyed Unity component held through that interface does
not compare equal to null. It can therefore find the new listener but continue
reading the old, silent capture. Its native microphone capture is independent.
This explains a possible path to the reported symptom; an actual recording repro
has not yet confirmed that this was the user's cause.

The package now detects destroyed captures and disabled/inactive listeners,
rebinds to the available listener, and restores capture when recording is active.
Temporary listener gaps clear stale samples/levels and no longer prevent reading
the independent microphone. Game mute/gain controls remain unchanged. Remote
Photon voice already uses Unity AudioSources routed through GameAudio's Voice
group, so it belongs to the same listener mix as game sounds.

Validation: Unity MCP confirmed `X:/Work/duck-rails/Assets`, successful compilation,
and all nine `RailLckAudioChecks.Run` assertions passed in Edit Mode. The checks
exercise the real binding method without constructing native recording services;
the Console error/exception query was empty and scoped diff whitespace checks passed.
Live recording, listening and
scene-transition acceptance remain queued in `playtest-queue.md`. No diagnostic
reports were inspected. Preserve this local package fix when upgrading LIV.

LIV's [audio integration documentation](https://lck-docs.liv.tv/integration/audio)
describes listener-based game capture and the separate microphone source.
