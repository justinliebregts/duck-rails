#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Photon.Pun;
using UnityEditor;
using UnityEngine;

/// <summary>Solo public-compound regression using the real keyboard rig and room timer.</summary>
public static class RailBoardingLiveChecks
{
    static readonly List<string> lines = new List<string>();
    static SimulatedRigInput rig;
    static int stage;
    static double started, stageAt, nextSample, deadline, dueAt;
    static float platformEntry, platformCenter, platformX;
    public static string Status { get; private set; } = "Not run";

    [MenuItem("Duck Rails/Legacy/Run Live Keyboard Boarding Check")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying || PhotonSession.Phase != RailPhase.Compound ||
            !PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom.PlayerCount != 1 ||
            PhotonSession.Instance == null || !PhotonSession.Instance.WorldReady || SimulatedRigInput.Instance == null)
            throw new InvalidOperationException("Requires a ready public compound with only this player");
        if (rig != null) throw new InvalidOperationException("Boarding check already running");
        rig = SimulatedRigInput.Instance;
        if (!rig.IsSimulating || rig.UseInjectedInput) { rig = null; throw new InvalidOperationException("Requires keyboard mode with no injected input"); }
        RailDiagnosticPanel.Instance?.Close();
        lines.Clear(); stage = 0; deadline = dueAt = 0; started = stageAt = EditorApplication.timeSinceStartup; nextSample = 0;
        rig.UseInjectedInput = true;
        var bounds = RailWorld.Instance.boardingTrigger.bounds;
        platformEntry = bounds.min.z; platformCenter = bounds.center.z; platformX = bounds.center.x;
        NewPlayer.instance.Warp(new Vector3(platformX, NewPlayer.instance.transform.position.y, platformEntry - 2), Quaternion.identity);
        Status = "Walking onto platform";
        EditorApplication.update += Tick;
    }
    static void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (!EditorApplication.isPlaying || now - started > 65) { Finish("FAIL interrupted or timed out at " + stage); return; }
        if (stage == 5 && PhotonSession.Phase == RailPhase.Running && PhotonSession.Instance.WorldReady)
        { Finish("PASS keyboard walk-on, empty-platform cancellation, re-entry and full 20-second public departure"); return; }
        if (PhotonSession.Phase != RailPhase.Compound)
        {
            if (stage != 5 || dueAt == 0 || now < dueAt - .5) Finish("FAIL premature departure");
            return;
        }
        // Between rooms the default phase is Compound and the Photon clock can change servers.
        if (stage == 5 && dueAt > 0 && now >= dueAt - .5 &&
            (!PhotonNetwork.InRoom || PhotonSession.Instance.HasRoomRequest || PhotonSession.Instance.AwaitingCrewDestination)) return;
        if (rig == null || NewPlayer.instance == null) { Finish("FAIL lost keyboard rig"); return; }
        var player = NewPlayer.instance;
        double roomDeadline = PhotonSession.Get(RailSessionRules.Deadline, 0d);
        if (now >= nextSample)
        {
            nextSample = now + .25;
            lines.Add($"{now-started:F2} stage={stage} root={player.transform.position} body={player.bodyCollider.bounds.center} aboard={RailWorld.IsLocalOnPlatform()} deadline={roomDeadline:F3}");
        }
        switch (stage)
        {
            case 0:
            case 4:
                // The platform has a 20 cm curb; use the normal keyboard jump to get aboard.
                rig.InjectedInput = new SimulatedRigInput.Frame { move = Vector2.up,
                    jump = player.bodyCollider.bounds.center.z < platformEntry + .3f && rig.HasBodySupport };
                if (player.bodyCollider.bounds.center.z >= platformCenter)
                { rig.InjectedInput = default; stage = stage == 0 ? 1 : 5; stageAt = now; }
                break;
            case 1:
                if (roomDeadline > 0 && now - stageAt > 2) { stage = 2; stageAt = now; }
                break;
            case 2:
                rig.InjectedInput = new SimulatedRigInput.Frame { move = Vector2.down };
                if (player.bodyCollider.bounds.center.z < platformEntry - 1) { rig.InjectedInput = default; stage = 3; stageAt = now; }
                break;
            case 3:
                if (roomDeadline == 0 && !RailWorld.IsLocalOnPlatform()) { stage = 4; stageAt = now; }
                break;
            case 5:
                if (roomDeadline > 0 && deadline == 0) { deadline = roomDeadline; dueAt = now + roomDeadline - PhotonNetwork.Time; }
                if (deadline > 0 && (roomDeadline == 0 || Math.Abs(deadline - roomDeadline) > .01))
                {
                    // At expiry the master clears the timer and sets the transfer destination.
                    if (now >= dueAt - .5 && PhotonSession.Get(RailSessionRules.Destination, "").Length > 0) break;
                    Finish("FAIL countdown cancelled or restarted while body remained on platform");
                }
                break;
        }
    }
    static void Finish(string message)
    {
        EditorApplication.update -= Tick;
        if (rig != null) { rig.InjectedInput = default; rig.UseInjectedInput = false; }
        rig = null; Status = message; lines.Add(message);
        Directory.CreateDirectory("Temp"); File.WriteAllLines("Temp/rails-keyboard-boarding.txt", lines);
        Debug.Log(message);
    }
}
#endif
