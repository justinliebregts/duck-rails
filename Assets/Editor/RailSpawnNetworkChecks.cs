#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Photon.Pun;
using Unity.Multiplayer.PlayMode;
using UnityEditor;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>Opt-in two-editor spawn/route smoke test. Never reads diagnostic reports.</summary>
[InitializeOnLoad]
public static class RailSpawnNetworkChecks
{
    static string Root
    {
        get { string p = Application.dataPath.Replace('\\', '/'); int i = p.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase); return i < 0 ? Path.GetDirectoryName(p) : p.Substring(0, i); }
    }
    static string Arm => Root + "/Temp/rails-spawn-check.txt";
    static string RoomPath => Root + "/Temp/rails-spawn-room.txt";
    static string Folder => Root + "/docs/validation/spawn-startup";
    static string Role => CurrentPlayer.IsMainEditor ? "main" : "remote";
    static string lastState;
    static int batch, step = -1, errors;
    static double nextTick;
    static double stepAt, readyAt;
    static Func<string, RailRouteState> previousRoute;
    static Func<bool> previousInvincibility;
    static bool gateChecked, completed;
    static int joinStage;
    static bool installed;
    static RailSpawnNetworkChecks()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
    }
    public static void Start(int catalogueBatch = -1)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play first");
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Arm, DateTime.UtcNow.AddMinutes(15).ToString("O") + "|" + catalogueBatch);
        File.WriteAllText(RoomPath, "");
        foreach (string role in new[] { "main", "remote" }) File.WriteAllText(Folder + "/batch" + catalogueBatch + "-" + role + ".txt", "");
        RailTestRunner.Start("two-clients");
    }
    public static void RunCatalogue(int firstBatch = 0)
    {
        if (firstBatch < 0 || firstBatch > 3) throw new ArgumentOutOfRangeException(nameof(firstBatch));
        SessionState.SetBool("Rails.SpawnSweep.All", true);
        SessionState.SetInt("Rails.SpawnSweep.Next", firstBatch);
    }
    static void Record(string text) => File.AppendAllText(Folder + "/batch" + batch + "-" + Role + ".txt", DateTime.UtcNow.ToString("O") + " " + text + "\n");
    static void OnLog(string message, string stack, LogType type)
    {
        if (!installed || !EditorApplication.isPlaying) return;
        if (message.Contains("PhotonView ID duplicate") || type == LogType.Exception ||
            type == LogType.Error && !message.Contains("FormFactor") && !message.Contains("OVR") && !message.Contains("OpenXR"))
        { errors++; Record("ERROR " + message + "\n" + stack); }
    }
    static RailRouteState Route(string run)
    {
        if (batch < 0) return previousRoute?.Invoke(run);
        var blocks = RailRoute.Instance.catalog.OrderBy(b => b.id, StringComparer.Ordinal).Skip(batch * 4).Take(4).ToArray();
        return RailRoute.ChooseOrdered(run, blocks, 12345);
    }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextTick) return;
        nextTick = EditorApplication.timeSinceStartup + .25;
        if (!EditorApplication.isPlaying)
        {
            if (installed) { RailTestTools.OrderedRoute = previousRoute; RailTestTools.EnemyInvincibilityRequested = previousInvincibility; installed = false; }
            int next = SessionState.GetInt("Rails.SpawnSweep.Next", -1);
            if (CurrentPlayer.IsMainEditor && !EditorApplication.isPlayingOrWillChangePlaymode && !RailTestRunner.Running && next >= 0)
            { SessionState.SetInt("Rails.SpawnSweep.Next", -1); Start(next); }
            return;
        }
        if (!File.Exists(Arm)) return;
        string arm = File.ReadAllText(Arm); var parts = arm.Split('|');
        if (parts.Length != 2 || !DateTime.TryParse(parts[0], out var until) || DateTime.UtcNow > until.ToUniversalTime()) return;
        if (!installed)
        {
            batch = int.Parse(parts[1]); errors = 0; step = -1; lastState = null;
            gateChecked = completed = false; readyAt = 0;
            joinStage = 0;
            previousRoute = RailTestTools.OrderedRoute; RailTestTools.OrderedRoute = Route; installed = true;
            previousInvincibility = RailTestTools.EnemyInvincibilityRequested; RailTestTools.EnemyInvincibilityRequested = () => true;
            Record("START " + Application.unityVersion);
        }
        var session = PhotonSession.Instance;
        string state = "actor=" + PhotonNetwork.LocalPlayer.ActorNumber + " master=" + PhotonNetwork.MasterClient?.ActorNumber +
            " room=" + PhotonNetwork.CurrentRoom?.Name + " phase=" + PhotonSession.Phase + " ready=" + session?.WorldReady +
            " queue=" + PhotonNetwork.IsMessageQueueRunning + " coal=" + UnityEngine.Object.FindObjectsByType<RailCoal>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        if (state != lastState) { Record(state); lastState = state; }
        // Ordered route overrides intentionally apply only in private rooms.
        // First exercise the real two-client quick start, then transfer both authenticated peers.
        if (batch >= 0 && session != null && session.WorldReady && !session.HasRoomRequest)
        {
            if (CurrentPlayer.IsMainEditor && joinStage == 0)
            {
                joinStage = 1; session.RequestCreatePrivate(UnityEngine.Random.Range(20000, 90000).ToString()); return;
            }
            if (CurrentPlayer.IsMainEditor && PhotonSession.IsPrivate && PhotonSession.Phase == RailPhase.Staging)
            {
                File.WriteAllText(RoomPath, PhotonNetwork.CurrentRoom.Name);
                if (PhotonNetwork.PlayerList.Length == 2 && PhotonNetwork.PlayerList.All(p => p.CustomProperties[RailSessionRules.Ready] as string == ":Staging"))
                    session.StartPrivateRun();
                return;
            }
            if (!CurrentPlayer.IsMainEditor && !PhotonSession.IsPrivate)
            {
                string code = File.ReadAllText(RoomPath).Trim();
                if (code.Length == 5) session.RequestJoinCode(code);
                return;
            }
            if (!PhotonSession.IsPrivate) return;
        }
        if (session == null || !session.WorldReady || session.HasRoomRequest || PhotonSession.Phase != RailPhase.Running || RailRoute.Instance == null) return;
        if (readyAt == 0) readyAt = EditorApplication.timeSinceStartup;
        if (EditorApplication.timeSinceStartup - readyAt < 5) return; // Allow enemy spawn replication to settle.
        if (!gateChecked)
        {
            gateChecked = true;
            var handoff = UnityEngine.Object.FindFirstObjectByType<PhotonMasterHandoff>();
            var ready = typeof(PhotonMasterHandoff).GetProperty("Ready", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            // Exercise the actual election gate synchronously, restoring readiness before another frame.
            session.MarkWorldLoading();
            try { Require(!(bool)ready.GetValue(handoff), "loading client excluded from host election"); Record("PASS loading client excluded from host election"); }
            finally { session.MarkWorldReady(); }
            if (PhotonNetwork.IsMasterClient && PhotonNetwork.PlayerList.Length == 2) CheckStaleCandidate(handoff);
        }
        int requested = PhotonSession.Get("test.spawn.step", -1);
        if (CurrentPlayer.IsMainEditor && SessionState.GetBool("Rails.SpawnSweep.All", false) && requested == RailRoute.Instance.Blocks.Count + 1 &&
            step == requested && EditorApplication.timeSinceStartup - stepAt > 3 && PhotonNetwork.PlayerList.Length == 2 &&
            PhotonNetwork.PlayerList.All(p => p.CustomProperties["test.spawn.ack"] is int a && a == requested))
        {
            Record("PASS COMPLETE both clients acknowledged every block");
            int next = batch < 3 ? batch + 1 : -1;
            Stop();
            SessionState.SetBool("Rails.SpawnSweep.All", next >= 0);
            SessionState.SetInt("Rails.SpawnSweep.Next", next);
            return;
        }
        if (PhotonNetwork.IsMasterClient && PhotonNetwork.PlayerList.Length == 2 &&
            PhotonNetwork.PlayerList.All(p => p.CustomProperties[RailSessionRules.Ready] as string == PhotonSession.RunId + ":Running"))
        {
            if (requested < 0) { Visit(0); return; }
            if (EditorApplication.timeSinceStartup - stepAt > 2 && PhotonNetwork.PlayerList.All(p => p.CustomProperties["test.spawn.ack"] is int ack && ack == requested))
            {
                if (requested < RailRoute.Instance.Blocks.Count + 1) { Visit(requested + 1); return; }
                if (!completed) { completed = true; Record("PASS COMPLETE two-client route sweep"); }
            }
        }
        if (requested < 0 || requested == step) return;
        try
        {
            var route = RailRoute.Instance;
            var blocks = new[] { route.startingStation }.Concat(route.Blocks).Concat(new[] { route.finale }).ToArray();
            if (requested >= blocks.Length) return;
            var block = blocks[requested];
            NewPlayer.instance.Warp(block.transform.position + new Vector3(0, 2, block.length * .5f), Quaternion.identity);
            route.GetComponent<RailBlockVisibility>().ApplyViewerPosition(block.transform.position + Vector3.forward * (block.length * .5f));
            Require(route.Ready && blocks.All(b => b.GetComponent<RailBakedBlockLighting>().Ready), "all route lighting ready");
            Require(block == route.startingStation || block.graph != null, "encounter baked graph present (station is safe)");
            Require(block.GetComponentsInChildren<Collider>().Any(c => c.enabled), "block colliders active");
            Require(block.GetComponentsInChildren<Renderer>().Any(r => r.enabled && !r.forceRenderingOff), "current block visible");
            var views = UnityEngine.Object.FindObjectsByType<PhotonView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(v => v.ViewID != 0).ToArray();
            Require(views.GroupBy(v => v.ViewID).All(g => g.Count() == 1), "unique live ViewIDs");
            Require(views.All(v => PhotonView.Find(v.ViewID) == v), "every live view registered");
            var loot = views.Where(v => v.GetComponent<RailCoal>() != null || v.GetComponent<ItemGoldBar>() != null).ToArray();
            Require(loot.Count(v => v.GetComponent<RailCoal>() != null) == route.expedition.coalPoints.Length, "exact coal count");
            Require(loot.Count(v => v.GetComponent<ItemGoldBar>() != null) == route.expedition.goldPoints.Length, "exact gold count");
            Require(Game.AI.Enemy.Active.Count == route.expedition.baseEnemyCount, "exact base enemy count");
            var markers = loot.Select(v => string.Join("/", v.InstantiationData.Select(x => x.ToString()))).ToArray();
            Require(markers.Distinct().Count() == markers.Length, "unique loot markers");
            Require(errors == 0, "no unexpected errors since startup (" + errors + ")");
            Record("PASS block=" + block.id + " step=" + requested + " run=" + PhotonSession.RunId + " route=" + string.Join(",", route.Selection.blocks) + " coal=" + route.expedition.coalPoints.Length + " gold=" + route.expedition.goldPoints.Length + " enemies=" + Game.AI.Enemy.Active.Count);
            Record("LOOT " + string.Join(",", loot.OrderBy(v => v.ViewID).Select(v => v.ViewID + ":" + v.SpawnToken)));
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { ["test.spawn.ack"] = requested });
            step = requested;
            stepAt = EditorApplication.timeSinceStartup;
        }
        catch (Exception e) { Record("FAIL " + e.Message); step = requested; }
    }
    static void Require(bool value, string label) { if (!value) throw new Exception(label); }
    static void CheckStaleCandidate(PhotonMasterHandoff handoff)
    {
        var other = PhotonNetwork.PlayerList.Single(p => !p.IsLocal);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var beats = (System.Collections.IDictionary)typeof(PhotonMasterHandoff).GetField("heartbeats", flags).GetValue(handoff);
        bool hadBeat = beats.Contains(other.ActorNumber), hadReady = other.CustomProperties.ContainsKey(RailSessionRules.Ready);
        object oldBeat = beats[other.ActorNumber], oldReady = other.CustomProperties[RailSessionRules.Ready];
        var select = typeof(PhotonMasterHandoff).GetMethod("SelectCandidate", flags);
        try
        {
            // A real peer's clock may be slightly ahead. Its accepted heartbeat
            // would correctly reject our synthetic older timestamp. Isolate the
            // injected event and restore the actual heartbeat in finally.
            beats.Remove(other.ActorNumber);
            var heartbeat = new ExitGames.Client.Photon.EventData { Code = 105 };
            heartbeat.Parameters[Photon.Realtime.ParameterCode.ActorNr] = other.ActorNumber;
            heartbeat.Parameters[Photon.Realtime.ParameterCode.Data] = new object[] { PhotonNetwork.ServerTimestamp, true };
            handoff.OnEvent(heartbeat);
            Require(beats.Contains(other.ActorNumber), "synthetic heartbeat accepted");
            other.CustomProperties[RailSessionRules.Ready] = PhotonSession.RunId + ":" + PhotonSession.Phase;
            Require(ReferenceEquals(select.Invoke(handoff, null), other), "ready remote remains eligible");
            foreach (string stale in new[] { "", ":Staging", "previous-run:Running" })
            {
                other.CustomProperties[RailSessionRules.Ready] = stale;
                Require(select.Invoke(handoff, null) == null, "fresh heartbeat cannot elect stale world " + stale);
            }
            Record("PASS current-world remote eligible; fresh heartbeat rejected with loading/staging/previous-run tokens");
        }
        finally
        {
            if (hadReady) other.CustomProperties[RailSessionRules.Ready] = oldReady; else other.CustomProperties.Remove(RailSessionRules.Ready);
            if (hadBeat) beats[other.ActorNumber] = oldBeat; else beats.Remove(other.ActorNumber);
        }
    }
    public static void Visit(int index)
    {
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { ["test.spawn.step"] = index });
    }
    public static void Stop()
    {
        SessionState.SetBool("Rails.SpawnSweep.All", false);
        SessionState.SetInt("Rails.SpawnSweep.Next", -1);
        File.WriteAllText(Arm, "");
        RailTestRunner.Stop("Spawn/route check stopped; see docs/validation/spawn-startup");
    }
}
#endif
