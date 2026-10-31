#if UNITY_EDITOR
using System;
using System.Reflection;
using Game.Animation;
using UnityEditor;
using UnityEngine;

public static class RailRigChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);

    [MenuItem("Duck Rails/Run Shared Rig Checks")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run shared rig checks in Edit Mode");
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
        var previousPlayer = NewPlayer.instance;
        var previousCursor = Cursor.lockState;
        bool previousCursorVisible = Cursor.visible;
        GameObject rig = null;
        try
        {
            DesktopPlay.SetSessionSimulation(true);
            rig = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/DuckPlayer.prefab"));
            rig.hideFlags = HideFlags.HideAndDontSave;
            var player = rig.GetComponent<NewPlayer>();
            var owner = rig.GetComponent<GR.Player>();
            var input = rig.GetComponent<ControllerInput>();
            var body = rig.GetComponent<LocalBodyDriver>();
            var left = owner.leftPlayerHand; var right = owner.rightPlayerHand;
            Set(player, "_bodyDriver", body);
            Set(player, "headBodyOffset", Quaternion.Inverse(player.headCollider.transform.rotation) *
                (player.bodyCollider.transform.position - player.headCollider.transform.position));
            player.InitializeValues();
            var simulator = rig.AddComponent<SimulatedRigInput>();
            Invoke(simulator, "Awake"); Invoke(simulator, "Start");
            Check(rig.GetComponent<DesktopPlayer>() == null && rig.GetComponent<CharacterController>() == null, "Simulation must not add an alternate player or character controller");
            Check(rig.GetComponents<ControllerInput>().Length == 1 && rig.GetComponentsInChildren<PlayerHand>().Length == 2, "Reuse exactly the prefab's input and two hands");
            Check(owner.leftPlayerHand == left && owner.rightPlayerHand == right, "Both authored hands retained");
            Check(left.GrabTolerance == .06f && right.GrabTolerance == .06f, "No desktop grab tolerance override");
            Check(!left.IsDesktopInput && !right.IsDesktopInput && !input.mouseGrip, "Use VR contact and drive paths");
            Check(body.HeadAnchor == player.headCollider.transform && body.LeftHandAnchor == player.leftHandTransform && body.RightHandAnchor == player.rightHandTransform, "Body and locomotion consume the same anchors");
            Check(rig.GetComponentInChildren<OVRCameraRig>().enabled && rig.GetComponentInChildren<OVRManager>().gameObject.activeSelf, "Keep Quest Link rig alive during simulation");

            input.ApplySimulationButtons(new SimulatedRigInput.Frame { grip = true });
            Check(input.leftGripButton.wasPressed && input.rightGripButton.wasPressed, "One grip input presses both hands");
            Check(input.leftGripValue == 1 && input.rightGripValue == 1, "Finger grip values agree");
            input.ApplySimulationButtons(new SimulatedRigInput.Frame { grip = true });
            Check(input.leftGripButton.down && input.rightGripButton.down && !input.leftGripButton.wasPressed && !input.rightGripButton.wasPressed, "Holding does not repeat press edges");
            input.ApplySimulationButtons(default);
            Check(input.leftGripButton.wasReleased && input.rightGripButton.wasReleased, "One release releases both hands");
            input.ApplySimulationButtons(new SimulatedRigInput.Frame { trigger = true });
            Check(input.leftTriggerButton.wasPressed && input.rightTriggerButton.wasPressed && input.leftTriggerValue == 1 && input.rightTriggerValue == 1, "Both trigger edges and finger curls");

            Vector3 leftRest = body.LeftHandAnchor.localPosition, rightRest = body.RightHandAnchor.localPosition;
            for (int i = 0; i < 20; i++) simulator.ApplyFrame(new SimulatedRigInput.Frame { rightReach = true }, .02f);
            Check(body.RightHandAnchor.localPosition.z > rightRest.z + .3f && Vector3.Distance(body.LeftHandAnchor.localPosition, leftRest) < .001f, "E reaches only right hand");
            for (int i = 0; i < 20; i++) simulator.ApplyFrame(new SimulatedRigInput.Frame { leftReach = true }, .02f);
            Check(body.LeftHandAnchor.localPosition.z > leftRest.z + .3f && Vector3.Distance(body.RightHandAnchor.localPosition, rightRest) < .001f, "Q reaches only left hand and right returns");
            var beforeMove = body.HeadAnchor.position;
            simulator.ApplyFrame(new SimulatedRigInput.Frame { move = Vector2.right }, .02f);
            Check(Mathf.Abs(Vector3.Distance(beforeMove, body.HeadAnchor.position) - .09f) < .001f, "Keyboard movement enters tracked space at bounded speed");

            // Synthetic tracked poses exercise the actual transition implementation;
            // they do not claim headset tracking or bypass the public availability check.
            simulator.ApplyFrame(new SimulatedRigInput.Frame { throttle = true }, .02f);
            Check(simulator.ThrottleHeld, "Keyboard throttle follows held T input");
            simulator.ApplyFrame(default, .02f);
            Check(!simulator.ThrottleHeld, "Keyboard throttle stops on release");
            simulator.ApplyFrame(new SimulatedRigInput.Frame { throttle = true }, .02f);
            Set(simulator, "trackedHead", new Vector3(.08f, 1.75f, -.02f));
            Set(simulator, "trackedHeadRotation", Quaternion.Euler(12, 37, 0));
            Set(simulator, "trackedLeft", new Vector3(-.3f, 1.3f, .2f));
            Set(simulator, "trackedRight", new Vector3(.3f, 1.3f, .2f));
            Set(simulator, "trackedLeftRotation", Quaternion.identity); Set(simulator, "trackedRightRotation", Quaternion.identity);
            Vector3 beforeHead = body.HeadAnchor.position;
            float beforeYaw = body.HeadAnchor.eulerAngles.y;
            player.rb.linearVelocity = Vector3.one * 5;
            Invoke(simulator, "SwitchInputSource", false);
            Check(!simulator.ThrottleHeld, "Switching to VR releases the throttle");
            simulator.UseInjectedInput = true;
            simulator.InjectedInput = new SimulatedRigInput.Frame { throttle = true };
            Invoke(simulator, "Update");
            Check(!simulator.ThrottleHeld, "Held T must go neutral after switching modes");
            simulator.InjectedInput = default; Invoke(simulator, "Update");
            simulator.InjectedInput = new SimulatedRigInput.Frame { throttle = true }; Invoke(simulator, "Update");
            Check(simulator.ThrottleHeld, "VR mode accepts the keyboard throttle without cursor capture");
            simulator.InjectedInput = default; Invoke(simulator, "Update");
            Check(!simulator.ThrottleHeld, "VR mode throttle stops on release");
            simulator.InjectedInput = new SimulatedRigInput.Frame { throttle = true }; Invoke(simulator, "Update");
            Invoke(simulator, "OnApplicationFocus", false);
            Check(!simulator.ThrottleHeld, "Focus loss releases throttle immediately");
            Invoke(simulator, "OnApplicationFocus", true); Invoke(simulator, "Update");
            Check(!simulator.ThrottleHeld, "Focus regain cannot restart throttle until T is released");
            simulator.InjectedInput = default; simulator.UseInjectedInput = false;
            Check(!simulator.IsSimulating && !DesktopPlay.UseDesktop, "Switch selects tracked input on same rig");
            Check(Vector3.Distance(beforeHead, body.HeadAnchor.position) < .001f && Mathf.Abs(Mathf.DeltaAngle(beforeYaw, body.HeadAnchor.eulerAngles.y)) < .001f, "Switch preserves head location and heading");
            Check(player.rb.linearVelocity == Vector3.zero && Vector3.Distance(player.lastLeftHandPosition, player.leftHandTransform.position) < .2f, "Switch clears motion and reseeds hand history");
            Check(!input.leftGripButton.down && !input.rightGripButton.down && !input.leftTriggerButton.down && !input.rightTriggerButton.down, "Switch clears held inputs");
            Invoke(simulator, "SwitchInputSource", true);
            Check(simulator.IsSimulating && Vector3.Distance(beforeHead, body.HeadAnchor.position) < .001f, "Switch back retains location and rig");
        }
        finally
        {
            if (rig != null) UnityEngine.Object.DestroyImmediate(rig);
            NewPlayer.instance = previousPlayer;
            DesktopPlay.SetSessionSimulation(null);
            Cursor.lockState = previousCursor; Cursor.visible = previousCursorVisible;
        }
        Debug.Log("Shared rig checks passed (" + checks + " checks; synthetic tracking transition, not headset acceptance).");
    }
}
#endif
