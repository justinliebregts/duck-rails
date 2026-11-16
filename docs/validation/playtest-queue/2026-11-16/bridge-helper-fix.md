# Bridge contact fixture correction — November16

The full-route test initially made no bridge progress at zero train distance.
The operator helper supplied a rig origin to NewPlayer.Warp, whose current API
expects the standing point under the tracked head. Editor ticks could also apply
multiple corrections against an old skinned-hand pose. This was a test-placement
failure, not a production bridge-state failure.

RailBridgeHoldFixture now refreshes the local body pose, targets the actual
button collider center, passes the current head-aligned standing point, and
limits correction to once per Unity frame. It retains actual fingertip contact,
PressableButton processing and Photon hold publication; it never writes bridge
progress or train distance.

The permanent implementation compiled in Unity6000.3.24f1 and passed a fresh
private two-client run: main1010 samples,990 contact samples,983 accepted live
holds; peer1018 progress samples. Both report0failures, normal30-second lowering,
zero train distance/speed, and released helper/button/hold with input/focus/pose
restored. See bridge-helper-verify-main.txt and bridge-helper-verify-Player2.txt.
The preceding temporary recovery is separately recorded in bridge-hold-recovery
and full-route-main; it is not substituted for this permanent-helper rerun.
No headset reach or controller-feel acceptance is inferred.
