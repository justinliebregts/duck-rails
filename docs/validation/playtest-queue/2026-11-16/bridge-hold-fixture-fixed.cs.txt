using Game.Animation;
using UnityEditor;
using UnityEngine;

/// <summary>Test-only operator placement. Actual fingertip contact drives the production button and Photon hold.</summary>
[InitializeOnLoad]
public static class RailBridgeHoldFixture
{
    static bool holding;
    static int lastPoseFrame = -1;
    static RailBridgeHoldFixture() => EditorApplication.update += Tick;
    public static void SetHeld(bool value)
    {
        if (holding == value) return;
        holding = value;
        lastPoseFrame = -1;
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
        var button = run.bridgeButton.GetComponent<RailHoldControl>().button;
        var driver = GR.Player.instance.GetComponent<LocalBodyDriver>();
        var rig = NewPlayer.instance;
        driver.PoseNow();
        var finger = driver.Body.RightHand.GetComponentInChildren<SphereCollider>();
        var volume = button.GetComponent<BoxCollider>();
        Vector3 delta = volume.transform.TransformPoint(volume.center) - finger.transform.TransformPoint(finger.center);
        // Warp takes a standing point under the head, not the tracking-rig origin.
        Vector3 stand = new Vector3(rig.headCollider.transform.position.x, rig.transform.position.y, rig.headCollider.transform.position.z);
        rig.Warp(stand + delta);
        driver.PoseNow();
    }
}
