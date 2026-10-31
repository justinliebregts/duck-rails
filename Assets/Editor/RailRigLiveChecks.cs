#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Animation;
using UnityEditor;
using UnityEngine;

/// <summary>Opt-in, real-frame checks on the authenticated shared rig in a solo sandbox.</summary>
[InitializeOnLoad]
public static class RailRigLiveChecks
{
    public static string Status { get; private set; } = "Not run";
    static SimulatedRigInput sim;
    static NewPlayer player;
    static GR.Player owner;
    static int stage, lastFrame;
    static float entered;
    static Vector3 origin, checkpoint;
    static Quaternion rotation;
    static GameObject button, floor;
    static RailControl drive;
    static readonly List<string> evidence = new();
    static bool active;
    static RailCoal[] fixtureCoal;
    static int tracedFrame;
    static RailRigLiveChecks() => EditorApplication.update += Tick;

    [MenuItem("Duck Rails/Run Live Shared Rig Checks")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying || !PhotonSession.IsPrivate || Photon.Pun.PhotonNetwork.CurrentRoom.PlayerCount != 1 ||
            PhotonSession.Phase != RailPhase.Running || !PhotonSession.Instance.WorldReady || SimulatedRigInput.Instance == null || !SimulatedRigInput.Instance.IsSimulating)
            throw new InvalidOperationException("Start the Test Center's solo private sandbox in Keyboard mode first");
        sim = SimulatedRigInput.Instance; player = NewPlayer.instance; owner = GR.Player.instance;
        origin = player.transform.position; rotation = player.transform.rotation;
        sim.UseInjectedInput = true; sim.InjectedInput = default;
        evidence.Clear(); active = true; stage = 0; entered = Time.unscaledTime; lastFrame = -1;
        Status = "Running";
        Vector3 testPosition = new Vector3(15, .35f, RailTrain.Instance.transform.position.z - 8);
        floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Shared rig fixture floor"; floor.layer = LayerMask.NameToLayer("Environment");
        floor.transform.position = new Vector3(testPosition.x, -.25f, testPosition.z);
        floor.transform.localScale = new Vector3(8, .5f, 8);
        Physics.SyncTransforms();
        player.Warp(testPosition, Quaternion.identity);
        Save();
    }

    static void Pass(string message) { evidence.Add("PASS " + message); Debug.Log("SHARED RIG: " + message); }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Next() { stage++; entered = Time.unscaledTime; Save(); }
    static void Save()
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllLines("Temp/rails-shared-rig-live.txt", new[] { Status + " stage=" + stage, DateTime.UtcNow.ToString("O") }.Concat(evidence));
    }
    static void Finish(string status)
    {
        active = false; Status = status;
        if (sim != null) { sim.InjectedInput = default; sim.UseInjectedInput = false; }
        if (owner != null) { PhotonHeldItems.DropLocalHands(); owner.leftPlayerHand?.Release(); owner.rightPlayerHand?.Release(); }
        if (button != null) UnityEngine.Object.DestroyImmediate(button);
        if (floor != null) UnityEngine.Object.DestroyImmediate(floor);
        if (player != null) player.Warp(origin, rotation);
        Save();
    }

    static void Tick()
    {
        if (!active) return;
        if (!EditorApplication.isPlaying || sim == null) { Finish("CANCELLED: Play stopped"); return; }
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        try
        {
            float age = Time.unscaledTime - entered;
            Require(age < 10, "Timeout at stage " + stage + " root=" + player.transform.position + " velocity=" + player.rb.linearVelocity + " supported=" + sim.HasBodySupport);
            switch (stage)
            {
                case 0:
                    // Editor load/frame cadence can delay the first physics steps.
                    // Wait for measured support instead of assuming a fixed startup delay.
                    if (age < .5f || !sim.HasBodySupport || Mathf.Abs(player.rb.linearVelocity.y) > .1f) return;
                    Require(UnityEngine.Object.FindObjectsByType<NewPlayer>(FindObjectsSortMode.None).Length == 1 &&
                        UnityEngine.Object.FindObjectsByType<DesktopPlayer>(FindObjectsSortMode.None).Length == 0, "One shared rig, zero DesktopPlayer instances");
                    Require(!owner.leftPlayerHand.IsDesktopInput && !owner.rightPlayerHand.IsDesktopInput, "VR hand paths");
                    Pass("One DuckPlayer, two authored VR hands, no DesktopPlayer or desktop interaction mode");
                    checkpoint = player.transform.position;
                    sim.InjectedInput = new SimulatedRigInput.Frame { move = Vector2.right };
                    Next(); break;
                case 1:
                    if (age < .25f) return;
                    Require(player.transform.position.x - checkpoint.x > .3f, "WASD moves actual rig");
                    Pass("Simulated room-scale movement passes through NewPlayer");
                    sim.InjectedInput = default; checkpoint = player.transform.position; Next(); break;
                case 2:
                    if (age < .3f) return;
                    Require(Mathf.Abs(player.transform.position.x - checkpoint.x) < .1f, "Released movement must stop without drift");
                    Pass("Releasing movement stops horizontal input");
                    sim.InjectedInput = new SimulatedRigInput.Frame { leftReach = true, rightReach = true };
                    Next(); break;
                case 3:
                    if (age < .5f) return;
                    var coal = UnityEngine.Object.FindObjectsByType<RailCoal>(FindObjectsSortMode.None).Where(c => !c.GetComponent<Grabbable>().IsHeld).Take(2).ToArray();
                    fixtureCoal = coal; tracedFrame = -1;
                    Require(coal.Length == 2, "Two fixture coal chunks available");
                    for (int i = 0; i < 2; i++)
                    {
                        var hand = i == 0 ? owner.leftPlayerHand : owner.rightPlayerHand;
                        coal[i].GetComponent<Rigidbody>().isKinematic = true;
                        // Setup places authored items at each palm's surface. Acquisition
                        // itself must go through candidate selection and the grip edge.
                        coal[i].transform.SetPositionAndRotation(hand.HoldPoint.position + Vector3.up * .15f, Quaternion.identity);
                    }
                    Physics.SyncTransforms();
                    evidence.Add("Setup root=" + player.transform.position + " left=" + owner.leftPlayerHand.HoldPoint.position +
                        " right=" + owner.rightPlayerHand.HoldPoint.position + " candidates=" +
                        owner.leftPlayerHand.FindGrabCandidate()?.GetInstanceID() + "/" + owner.rightPlayerHand.FindGrabCandidate()?.GetInstanceID());
                    sim.InjectedInput = new SimulatedRigInput.Frame { leftReach = true, rightReach = true, grip = true };
                    Next(); break;
                case 4:
                    if (tracedFrame != Time.frameCount && age < .15f)
                    {
                        tracedFrame = Time.frameCount;
                        evidence.Add("Grab frame=" + Time.frameCount + " root=" + player.transform.position + " grip=" + owner.GetComponent<ControllerInput>().rightGripButton +
                            " held=" + owner.leftPlayerHand.Held?.GetInstanceID() + "/" + owner.rightPlayerHand.Held?.GetInstanceID() +
                            " palms=" + owner.leftPlayerHand.HoldPoint.position + "/" + owner.rightPlayerHand.HoldPoint.position +
                            " coal=" + fixtureCoal[0].transform.position + "/" + fixtureCoal[1].transform.position);
                    }
                    if (age < .3f) return;
                    Require(owner.leftPlayerHand.IsHolding && owner.rightPlayerHand.IsHolding && owner.leftPlayerHand.Held != owner.rightPlayerHand.Held, "Both hands acquire distinct items from grip input");
                    Pass("Both hands acquire authored coal through ordinary proximity and shared grip; no ForceGrab");
                    sim.InjectedInput = new SimulatedRigInput.Frame { leftReach = true, rightReach = true, trigger = true };
                    Next(); break;
                case 5:
                    if (age < .3f) return;
                    Require(!owner.leftPlayerHand.IsHolding && !owner.rightPlayerHand.IsHolding, "Both items release on input release");
                    var input = owner.GetComponent<ControllerInput>();
                    Require(input.leftTriggerButton.down && input.rightTriggerButton.down, "Both trigger states");
                    Pass("Grip release drops both items; trigger input reaches both hands");
                    var finger = owner.GetComponent<LocalBodyDriver>().Body.RightHand.GetComponentInChildren<SphereCollider>();
                    button = new GameObject("Shared rig fingertip fixture");
                    button.transform.position = finger.transform.TransformPoint(finger.center);
                    var box = button.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = Vector3.one * .08f;
                    button.AddComponent<PressableButton>(); Next(); break;
                case 6:
                    if (age < .2f) return;
                    Require(button.GetComponent<PressableButton>().IsPressed, "Real fingertip must press the button");
                    Pass("Physical button presses through DuckPlayer fingertip contact");
                    sim.InjectedInput = default; Next(); break;
                case 7:
                    if (age < .5f) return;
                    Require(!button.GetComponent<PressableButton>().IsPressed, "Withdrawing hand releases physical button");
                    Pass("Withdrawing simulated hand releases the physical button");
                    UnityEngine.Object.DestroyImmediate(button); button = null;
                    drive = UnityEngine.Object.FindObjectsByType<RailControl>(FindObjectsSortMode.None).First(c => c.action == RailControl.Action.Drive);
                    player.Warp(origin, Quaternion.identity);
                    sim.InjectedInput = new SimulatedRigInput.Frame { rightReach = true }; Next(); break;
                case 8:
                    if (age < .5f) return;
                    player.Warp(player.transform.position + drive.handle.position - owner.rightPlayerHand.transform.position, Quaternion.identity);
                    sim.InjectedInput = new SimulatedRigInput.Frame { rightReach = true, grip = true }; Next(); break;
                case 9:
                    if (age < .3f) return;
                    Require(drive.HeldBy(owner.rightPlayerHand), "Shared hand grip must acquire the real train lever");
                    Pass("Train lever acquired through PlayerHand grip path");
                    sim.InjectedInput = new SimulatedRigInput.Frame { rightReach = true, grip = true, reachDelta = .3f }; Next(); break;
                case 10:
                    // Scroll is an impulse; clear it after the first simulation frame.
                    sim.InjectedInput = new SimulatedRigInput.Frame { rightReach = true, grip = true };
                    if (age < 1) return;
                    Require(PhotonSession.DriveGripActor != 0 && (float)PhotonSession.DriveGripState[3] >= .8f, "Physical forward travel engages GO");
                    Pass("Simulated forward hand travel engages networked hold-to-run GO");
                    sim.InjectedInput = new SimulatedRigInput.Frame { rightReach = true }; Next(); break;
                case 11:
                    if (age < .6f) return;
                    Require(PhotonSession.DriveGripActor == 0 && !drive.HeldBy(owner.rightPlayerHand), "Grip release releases drive lease");
                    Pass("Grip release clears networked drive lease and brakes");
                    Finish("PASS — real frames on one authenticated shared rig; headset and multi-client acceptance separate"); break;
            }
        }
        catch (Exception e) { evidence.Add("FAIL " + e.Message); Finish("FAIL"); Debug.LogError("Shared rig live checks: " + e.Message); }
    }
}
#endif
