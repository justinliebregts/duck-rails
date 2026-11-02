using Game.Animation;
using UnityEditor;
using UnityEngine;

/// <summary>Test-only operator placement. Actual fingertip contact drives the production button and Photon hold.</summary>
[InitializeOnLoad]
public static class RailBridgeHoldFixture
{
    static bool holding;
    static RailBridgeHoldFixture() => EditorApplication.update += Tick;
    public static void SetHeld(bool value)
    {
        if (holding == value) return;
        holding = value;
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
        // These network fixtures test state, not combat difficulty. Recover the
        // placed operator through the existing editor-only self-revive action.
        if (RailExpedition.LocalDown) { run.Request("editor-revive"); return; }
        var button = run.bridgeButton.GetComponent<RailHoldControl>().button;
        var finger = GR.Player.instance.GetComponent<LocalBodyDriver>().Body.RightHand.GetComponentInChildren<SphereCollider>();
        Vector3 delta = button.transform.position - finger.transform.TransformPoint(finger.center);
        NewPlayer.instance.Warp(NewPlayer.instance.transform.position + delta, Quaternion.identity);
    }
}
