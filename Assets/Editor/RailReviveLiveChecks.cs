#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Unity.Multiplayer.PlayMode;
using UnityEditor;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// Three-client revive scenario: (1) two rescuers on one downed player, one leaves mid-revive and
/// progress must carry over; (2) a sub-grace hand dropout must not reset progress; (3) one rescuer
/// reviving two overlapping downed players. Each client writes Temp/rails-revive-*.txt.
/// </summary>
[InitializeOnLoad]
public static class RailReviveLiveChecks
{
    const string StageKey = "test.revive.stage", AckKey = "test.revive.ack", RoleKey = "test.revive.role";
    static readonly Vector3 P0 = new Vector3(4, .1f, 120);
    static string Root
    {
        get { string p = Application.dataPath.Replace('\\', '/'); int i = p.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase); return i < 0 ? Path.GetDirectoryName(p) : p.Substring(0, i); }
    }
    static string ArmPath => Path.Combine(Root, "Temp/rails-revive-arm.txt");
    static string RoomPath => Path.Combine(Root, "Temp/rails-revive-room.txt");
    static bool Host => CurrentPlayer.IsMainEditor;
    static bool Second => CurrentPlayer.Tags.Contains("Player2");
    static string RoleName => Host ? "main" : Second ? "player2" : "player3";
    static string token, room;
    static int localStage = -1, initial, baseline;
    static double entered, tick, t0, tRec, awayAt;
    static bool done, action, checkedHandover, started, sawLost, away, back;
    static readonly List<string> evidence = new List<string>();
    static RailReviveLiveChecks() => EditorApplication.update += Tick;

    [MenuItem("Duck Rails/Legacy/Arm Three Client Revive Test")]
    public static void Arm()
    {
        if (EditorApplication.isPlaying) throw new Exception("Arm before Play.");
        Directory.CreateDirectory(Path.Combine(Root, "Temp"));
        File.WriteAllText(ArmPath, DateTime.UtcNow.AddMinutes(15).ToString("O")); File.WriteAllText(RoomPath, "");
        RailEditorQuickStart.Enabled = false;
        Debug.Log("Three-client revive test armed. Launch the three-client scenario.");
    }
    [MenuItem("Duck Rails/Legacy/Disarm Three Client Revive Test")]
    public static void Disarm() { Directory.CreateDirectory(Path.Combine(Root, "Temp")); File.WriteAllText(ArmPath, ""); }

    static void Record(string value)
    {
        evidence.Add(DateTime.UtcNow.ToString("O") + " " + value);
        File.WriteAllLines(Path.Combine(Root, "Temp/rails-revive-" + RoleName + ".txt"), evidence);
        Debug.Log("REVIVE LIVE: " + value);
    }
    static void Require(bool pass, string message) { if (!pass) throw new Exception(message); }
    static void Ack() => PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [AckKey] = localStage });
    static bool AllAck => PhotonNetwork.PlayerList.Length == 3 && PhotonNetwork.PlayerList.All(p => p.CustomProperties[AckKey] is int n && n == localStage);
    static void Stage(int next) { PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [StageKey] = next }); }
    static void Teleport(Vector3 p) => NewPlayer.instance.Warp(p, Quaternion.identity);
    static void Board() => Teleport(RailTrain.Instance.transform.TransformPoint(new Vector3((PhotonNetwork.LocalPlayer.ActorNumber % 3 - 1) * 1.2f, .35f, -2)));
    static RailParticipant Of(RailExpedition run, string role)
    {
        var player = PhotonNetwork.PlayerList.FirstOrDefault(p => p.CustomProperties[RoleKey] is string r && r == role);
        return player == null ? null : run.State.players.Find(x => x.actor == player.ActorNumber);
    }
    static void Reset() { action = false; checkedHandover = false; started = false; sawLost = false; away = false; back = false; t0 = 0; tRec = 0; awayAt = 0; }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < tick || !File.Exists(ArmPath)) return;
        tick = EditorApplication.timeSinceStartup + .1;
        string arm = File.ReadAllText(ArmPath);
        if (!DateTime.TryParse(arm, out var until) || DateTime.UtcNow > until.ToUniversalTime()) return;
        if (token != arm) { token = arm; done = false; initial = 0; localStage = -1; room = null; evidence.Clear(); Reset(); Record("Started " + RoleName); }
        if (done) return;
        try
        {
            var session = PhotonSession.Instance;
            if (session == null || !PhotonSession.IsRailsRoom || !session.WorldReady || session.HasRoomRequest) return;
            if (initial == 0)
            {
                if (Host) { room = UnityEngine.Random.Range(10000, 90000).ToString(); session.RequestCreatePrivate(room); initial = 1; }
                else { room = File.ReadAllText(RoomPath).Trim(); if (room.Length > 0) { session.RequestJoinCode(room); initial = 2; } }
                return;
            }
            if (initial == 1)
            { if (!PhotonSession.IsPrivate || PhotonNetwork.CurrentRoom.Name != room) return; File.WriteAllText(RoomPath, room); initial = 2; }
            if (PhotonNetwork.CurrentRoom.Name != room || !PhotonSession.IsPrivate) return;
            if (initial == 2)
            {
                if (PhotonNetwork.PlayerList.Length != 3) return;
                Require(PhotonNetwork.PlayerList.Select(p => p.UserId).Distinct().Count() == 3, "Three distinct authenticated identities");
                initial = 3; Record("Three authenticated clients joined private room");
                PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [RoleKey] = RoleName });
                if (Host) { Stage(1); session.StartPrivateRun(); }
            }
            int stage = PhotonSession.Get(StageKey, 0);
            if (stage != localStage) { localStage = stage; entered = EditorApplication.timeSinceStartup; Reset(); }
            double age = EditorApplication.timeSinceStartup - entered, now = PhotonNetwork.Time;
            Require(age < 90, "Stage timeout " + stage + " phase=" + PhotonSession.Phase);
            var run = RailExpedition.Instance;
            if (run == null || PhotonSession.Phase != RailPhase.Running) return;
            var h = Of(run, "main"); var p2 = Of(run, "player2"); var p3 = Of(run, "player3");
            if (stage == 0) return;
            if (run.State.players.Count != 3 || h == null || p2 == null || p3 == null) return;
            string myId = PhotonNetwork.LocalPlayer.UserId;
            int revives = run.State.players.Sum(p => p.revives);

            if (stage == 1)
            {
                if (!action) { Board(); Ack(); action = true; }
                if (Host && AllAck && age > 2) Stage(2);
            }
            else if (stage == 2)
            {
                // Two rescuers; whichever is current leaves after ~1s; progress must carry over.
                if (Host && !action) { baseline = revives; Teleport(P0); action = true; }
                if (Host && age > 1 && !back) { RailExpedition.RequestDown(); back = true; }
                if (!Host && !action && h.down) { Teleport(P0 + (Second ? Vector3.right : Vector3.left) * .5f); action = true; }
                if (!Host && action && !away && h.down && PhotonSession.Get("test.revive.leaver", "") == myId) { Board(); away = true; }
                if (Host && t0 > 0 && !away && EditorApplication.timeSinceStartup - tRec > 1.0)
                { PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { ["test.revive.leaver"] = startRescuer }); away = true; }
                if (Host && t0 <= 0 && h.down && h.reviveAt > 0) { t0 = h.reviveAt; tRec = EditorApplication.timeSinceStartup; Record("Revive started, rescuer=" + h.rescuer); startRescuer = h.rescuer; }
                if (Host && t0 > 0 && !checkedHandover && EditorApplication.timeSinceStartup - tRec > 1.9)
                {
                    checkedHandover = true;
                    Require(h.down, "Still down at handover check");
                    Require(Math.Abs(h.reviveAt - t0) < .01, "Handover kept progress (reviveAt " + t0 + " -> " + h.reviveAt + ")");
                    Require(!string.IsNullOrEmpty(h.rescuer) && h.rescuer != startRescuer, "Second helper took over");
                    Record("Handover kept progress; rescuer " + startRescuer + " -> " + h.rescuer);
                }
                if (Host && checkedHandover && !h.down)
                { Require(revives == baseline + 1, "One rescue credited"); Record("Revive completed after handover, credited once"); Stage(3); }
                Require(!(Host && t0 > 0 && !checkedHandover && h.reviveAt <= 0 && h.down), "Revive reset to zero during handover");
            }
            else if (stage == 3)
            {
                // Single rescuer, 0.1s hand dropout (< grace) must not reset progress.
                if (Host && !action) { baseline = revives; action = true; }
                if (!Host && !action) { if (Second) Teleport(P0 + Vector3.right * .5f); else Board(); action = true; }
                if (Host && age > 4 && !away) { RailExpedition.RequestDown(); away = true; }
                if (Second && h.down && h.reviveAt > 0 && !away && now - h.reviveAt > 1.0) { Board(); away = true; awayAt = EditorApplication.timeSinceStartup; }
                if (Second && away && !back && EditorApplication.timeSinceStartup - awayAt > .1) { Teleport(P0 + Vector3.right * .5f); back = true; }
                if (Host && h.down && h.reviveAt > 0) started = true;
                if (Host && h.reviveLostAt > 0) sawLost = true;
                Require(!(Host && started && h.down && h.reviveAt <= 0), "Brief dropout reset revive progress");
                if (Host && started && !h.down)
                { Require(revives == baseline + 1, "One rescue credited"); Record("Brief dropout did not reset revive (grace pause observed: " + sawLost + ")"); Stage(4); }
            }
            else if (stage == 4)
            {
                // One rescuer, two overlapping downed players.
                if (Host && !action) { baseline = revives; action = true; }
                if (!Host && !action) { if (Second) Board(); else Teleport(P0 + Vector3.forward * .3f); action = true; }
                if (Host && age > 1 && !started) { Teleport(P0); started = true; }
                if (age > 5 && !Second && !back) { RailExpedition.RequestDown(); back = true; }
                if (Second && !away && h.down && p3.down) { Teleport(P0 + Vector3.right * .5f); away = true; }
                if (Host && h.down && p3.down) sawLost = true; // reused: both were down at once
                if (Host && sawLost && !h.down && !p3.down)
                { Require(revives == baseline + 2, "Both overlapping players credited to the one rescuer (" + (revives - baseline) + ")"); Record("One rescuer revived two overlapping downed players"); Stage(5); }
            }
            else if (stage == 5)
            { Record("PASS: revive, handover, brief dropout and overlapping players"); done = true; }
        }
        catch (Exception ex) { Record("FAIL: " + ex.Message); done = true; }
    }
    static string startRescuer;
}
#endif
