# Shared DuckPlayer rig

October 30, 2026. Justin requested one gameplay rig for Quest Link/headset use and keyboard/mouse testing, with input switching during the same Editor Play session. Q reaches the left hand, E reaches the right, and right mouse supplies grip to both hands. The coal interior-selection bug is separate work; this change deliberately retains the existing item selection and 6 cm grab tolerance.

## Controls

- Choose **Keyboard** or **VR** in the Scene view before Play. Both use the authored DuckPlayer prefab.
- Connect Quest Link before pressing Play if headset use is needed. XR initialization stays enabled even when starting in Keyboard mode.
- **F2** switches between simulated and tracked inputs during Play. The Scene view Keyboard/VR buttons also switch the running rig. A switch to VR requires an available tracked headset pose; otherwise Keyboard mode remains active.
- Hold **T** for throttle **GO**, release for **STOP**, in either Keyboard or VR mode on the editor/desktop client. No hand reach or grip is needed. The shortcut uses the lever's shared ownership lease and cannot take it from another holder. Keyboard mode requires the captured game cursor; VR mode does not. Focus loss, input blocking, downing, or switching modes releases it; release T before pressing again after a switch or focus loss.
- **WASD** moves; mouse looks; **Space** jumps when supported; **C** lowers the simulated head.
- Hold **Q** to reach left, **E** to reach right, or both together. Release to bring the corresponding hand back. Aim with the mouse; the hands reach through space, without raycast acquisition.
- Hold **right mouse** for both grips. Hold **left mouse** for both triggers. Releasing releases the corresponding button on both hands.
- Mouse wheel adjusts forward reach (0.25–1.1 metres). **Z/X** rolls both wrists. **1/2** supply left controller X/Y; **3/4** supply right A/B.
- **P** opens the forearm diagnostic panel. Touch its buttons with the other hand. **Tab** retains the session/testing tools. **Esc** frees the cursor; click to recapture without pressing a trigger. **F1** displays controls.

For the train lever, hold T as the keyboard shortcut in either input mode. Physical control also remains available: reach the handle, grip with right mouse, then increase forward reach with the wheel; release right mouse to brake. Horn, radio and keypad buttons use the actual fingertip colliders, just as in VR. Keyboard mode does not use the old E-to-toggle or mouse-to-click world control paths.

## Implementation boundaries

`DesktopPlay` now selects inputs and attaches `SimulatedRigInput` to the existing DuckPlayer. It no longer disables DuckPlayer, disables XR, or spawns the DesktopPlayer prefab. `SimulatedRigInput` provides poses to the same head/hand anchors consumed by `NewPlayer` and `LocalBodyDriver`, and button states through the existing `ControllerInput`. Both authored PlayerHands, skin/fingers, bag inventory, HUD, rigidbody, moving-platform support and Photon pose output remain in use.

Keyboard movement supplies simulated room-scale displacement before the existing locomotion solver. It does not add a CharacterController or use the old editor WASD/jetpack path. Real controller locomotion and turn settings remain intact in tracked mode. The old Q/E snap-turn keyboard shortcuts are disabled on the shared editor rig to avoid conflicting with reaching.

The OVR camera rig and manager stay alive. Simulated poses are reapplied through OVR's UpdatedAnchors event, including its before-render update, so Quest Link can remain initialized without overwriting keyboard-controlled poses. Switching releases hand-held items/bags and the drive control, clears button state, requires neutral inputs, aligns the incoming tracked head with the outgoing location/heading, and reseeds movement history. This prevents the source change from being treated as a throw or hand launch. The selected input source persists across gameplay scene changes, but the next Play session starts from the selected editor preference. Android device builds keep native headset input and do not install the simulator.

The retired DesktopPlayer asset/class remain for historical non-Rails fixtures; they are not a supported gameplay launch path. New features and Rails tests must use DuckPlayer. Rails live fixtures now warp `NewPlayer`, and the shared-rig interaction check uses grip edges and actual candidate selection. Existing furnace/network fixtures that use ForceGrab remain network tests, not evidence of natural reach/grab acceptance.

## Validation

### Hold-T throttle shortcut — October 31

- Unity compiled the change. All 28 shared-rig checks and 17 drive checks passed, including Keyboard and synthetic VR-mode hold/release, source-switch release/neutral gating, and focus-loss/reacquisition behavior.
- One authenticated private sandbox passed injected keyboard hold/release against actual Photon room state: held input acquired the shared lease, commanded GO and posed the handle at +35 degrees; release cleared the lease, commanded braking and returned the handle to -35 degrees. Evidence: [throttle check](validation/shared-rig/throttle-20261031.txt). Worn-headset and multi-client shortcut acceptance remain unverified.
- The first sandbox launch failed through the scenario reflection adapter; retrying after the editor settled launched successfully. The completed sandbox was asked to stop through the runner, but the Unity process exited before final setup restoration could be confirmed. The runner's saved cleanup state remains in ignored `Temp/rails-test-center.json` for its next editor initialization. Four existing open diagnostic reports were checked in the backend/local inbox and left open; this shortcut does not resolve them.

- **Duck Rails > Run Shared Rig Checks** checks prefab/input identity, native tolerance, simultaneous grip/trigger edges, Q/E reach independence and source-switch continuity using synthetic tracked poses. Included in Test Center quick regressions. Synthetic tracking is not headset acceptance.
- Launch **Test Center > Manual sandbox**, then **Duck Rails > Run Live Shared Rig Checks**. This requires one authenticated player in a private running expedition. It exercises real frames for movement, natural candidate selection/grip/release on authored coal (fixture setup positions coal at the palms' surfaces), fingertip buttons and the physical train lever. Evidence is written to `Temp/rails-shared-rig-live.txt`. It moves the rig/items for test setup and is intended only for the disposable sandbox.
- Actual worn Quest Link switching, headset feel and two-headset acceptance must be recorded separately. Sharing a rig improves test coverage; simulated poses do not establish tracking performance or comfort.

### Recorded editor evidence (October 30)

- Unity compiled the changed runtime and editor scripts. All 11 quick regression suites passed, including 20 shared-rig checks: [quick results](validation/shared-rig/quick-20261030.txt).
- The authenticated solo Play Mode check passed at 21:51:31 UTC: one DuckPlayer, no DesktopPlayer, movement and stopping, distinct coal acquisition by both hands on the first grip frame, release/trigger state, physical fingertip press/withdrawal, and the actual networked drive lever's acquire/GO/release sequence. [Live results](validation/shared-rig/live-20261030.txt). This is surface-positioned fixture evidence, not a fix or acceptance of the reported coal interior-selection defect.
- Two authenticated editor clients passed the existing coal regression at 21:53:42 UTC with the shared rig: stopped train, moving train, master handoff, unique avatars, exactly-once fuel credit and agreed removal. [Main evidence](validation/shared-rig/coal-main-20261030.txt), [second client evidence](validation/shared-rig/coal-remote-20261030.txt). This fixture uses ForceGrab for setup; natural acquisition is covered by the separate solo check. Unity Console contained zero errors/exceptions during this run. Test runner restored the prior scenario and stopped Play; the prior input preference was restored.
- The public switch rejected an unavailable tracked headset safely and retained keyboard control. Synthetic transitions preserved head location/heading and cleared velocity and held inputs. A worn Quest Link headset was unavailable for actual transition acceptance; that remains required.
- The first live fixture attempts warped outside the authored floor, then relied on a transient render-frame ground flag/fixed settling delay. The fixture now creates its own floor and waits for measured support and settled velocity. Keyboard jumping also checks actual support because the native grounded counter can expire between physics steps. The four resulting fixture reports were resolved only after the successful rerun above; the three pre-existing reports remain open.

The diagnostic inbox query initially failed DNS resolution inside the restricted shell sandbox; an authorized query outside that sandbox succeeded.

### Session Controls cursor fix (October 30)

The Photon editor overlay overwrites the legacy cursor-suppression flag during OnGUI. With Session Controls open, a real frame showed `RailWorld.InputBlocked=true` but `DesktopPlayer.SuppressCursorCapture=false`, allowing the simulator to steal a Create/Join click. `ReadKeyboard` now also checks `RailWorld.InputBlocked` before capturing the cursor. No keypad, reach or grip behavior changed.

Unity compiled the fix. Actual Game view clicks on Create and Join with a blank code each reached the button action and displayed the expected five-digit validation message (23:18:39 and 23:18:46 UTC). After both clicks, the cursor remained visible/unlocked and the trigger remained released even with the legacy suppression flag false. Close dismissed the panel normally. These checks deliberately did not create/join another room. The cleanup command timed out and the Duck Rails editor subsequently disappeared from the window inventory, so restoration of the temporary startup settings could not be confirmed (test used Login as playModeStartScene and disabled Two Ducks Quick Start). Prior settings were captured in SessionState under SessionCursor.PreviousStart/PreviousQuick. Two MRUK global-context initialization errors occurred at startup, before these button clicks; they are separate from this cursor fix. The three existing diagnostic reports were left open.
