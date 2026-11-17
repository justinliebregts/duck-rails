using Game.Animation;
using UnityEditor;
using UnityEngine;

/// <summary>Test-only operator placement through production grip/contact and Photon holds.</summary>
[InitializeOnLoad]
public static class RailBridgeHoldFixture
{
    static bool holding;
    static int lastPoseFrame = -1;
    static int leverFrames;
    static RailBridgeHoldFixture() => EditorApplication.update += Tick;
    public static void SetHeld(bool value)
    {
        if (holding == value) return;
        holding = value;
        lastPoseFrame = -1;
        leverFrames = 0;
        if (SimulatedRigInput.Instance != null)
        {
            SimulatedRigInput.Instance.UseInjectedInput = value;
            SimulatedRigInput.Instance.InjectedInput = value ? new SimulatedRigInput.Frame { rightReach = true } : default;
        }
    }
    static void Tick()
    {
        if (!holding) return;
        var run = RailExpedition.Instance;
        if (!EditorApplication.isPlaying || run == null || PhotonSession.Phase != RailPhase.Running ||
            run.State.BridgeProgress(Photon.Pun.PhotonNetwork.Time) >= 1)
        { SetHeld(false); return; }
        if (!PhotonSession.Instance.WorldReady || GR.Player.instance == null || SimulatedRigInput.Instance == null) return;
        // Editor ticks can run more often than simulation/skin updates.
        if (lastPoseFrame == Time.frameCount) return;
        lastPoseFrame = Time.frameCount;
        // These network fixtures test state, not combat difficulty. Recover the
        // placed operator through the existing editor-only self-revive action.
        if (RailExpedition.LocalDown) { run.Request("editor-revive"); return; }
        var hold = run.bridgeButton.GetComponent<RailHoldControl>();
        var button = hold.button;
        var driver = GR.Player.instance.GetComponent<LocalBodyDriver>();
        var rig = NewPlayer.instance;
        driver.PoseNow();
        if (hold.lever != null)
        {
            var lever = hold.lever;
            var hand = GR.Player.instance.rightPlayerHand;
            bool acquired = lever.HeldBy(hand);
            // One neutral frame before grip. Once acquired, move the tracked palm
            // through the real arc; never inject the bridge hold property itself.
            bool nearGrip = Vector3.Distance(hand.HoldPoint.position,
                lever.handle.TransformPoint(lever.handle.GetComponent<BoxCollider>().center)) < .06f;
            SimulatedRigInput.Instance.InjectedInput = new SimulatedRigInput.Frame {
                rightReach = true, grip = acquired || (nearGrip && (++leverFrames % 4) >= 2) };
            var target = acquired ? lever.transform.TransformPoint(Quaternion.Euler(35, 0, 0) * Vector3.up * .34f) :
                lever.handle.TransformPoint(lever.handle.GetComponent<BoxCollider>().center);
            var current = acquired ? driver.RightPalmSource.position : hand.HoldPoint.position;
            Vector3 standing = new Vector3(rig.headCollider.transform.position.x, rig.transform.position.y, rig.headCollider.transform.position.z);
            rig.Warp(standing + Vector3.ClampMagnitude(target - current, .04f));
            driver.PoseNow();
            return;
        }
        var finger = driver.Body.RightHand.GetComponentInChildren<SphereCollider>();
        var volume = button.GetComponent<BoxCollider>();
        Vector3 delta = volume.transform.TransformPoint(volume.center) - finger.transform.TransformPoint(finger.center);
        // Warp takes a standing point under the head, not the tracking-rig origin.
        Vector3 stand = new Vector3(rig.headCollider.transform.position.x, rig.transform.position.y, rig.headCollider.transform.position.z);
        rig.Warp(stand + delta);
        driver.PoseNow();
    }
}
