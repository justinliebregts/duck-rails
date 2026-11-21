#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Photon.Pun;
using Unity.Multiplayer.PlayMode;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Opt-in two-editor fault injection; no diagnostic report access.</summary>
[InitializeOnLoad]
public static class RailStartupRecoveryChecks
{
    static string Root
    {
        get { string p = Application.dataPath.Replace('\\', '/'); int i = p.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase); return i < 0 ? Path.GetDirectoryName(p) : p.Substring(0, i); }
    }
    static string Arm => Root + "/Temp/rails-startup-recovery.txt";
    static string Control => Root + "/Temp/rails-startup-recovery-" + mode;
    static string Prefix => Root + "/docs/validation/spawn-startup/recovery" + mode;
    static bool Main => CurrentPlayer.IsMainEditor;
    static string Role => Main ? "main" : "remote";
    static bool active, resumed, checkedResult;
    static int mode, stage, errors, lastSettling;
    static double started, settleAt, nextTick, settleStarted;
    static string lastState;
    static RailWorld heldWorld;

    static RailStartupRecoveryChecks()
    {
        EditorApplication.update += Tick;
        SceneManager.sceneLoaded += Loaded;
        Application.logMessageReceived += Log;
    }
    public static void Start(int fault)
    {
        if (fault < 0 || fault > 2 || EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Use faults 0..2 in Edit Mode");
        mode = fault;
        Directory.CreateDirectory(Path.GetDirectoryName(Prefix));
        foreach (string suffix in new[] { "main.txt", "remote.txt", "before", "main.inventory", "remote.inventory" })
            File.WriteAllText(Prefix + "-" + suffix, "");
        File.WriteAllText(Control + "-room", "");
        File.WriteAllText(Control + "-recovered", "");
        File.WriteAllText(Arm, DateTime.UtcNow.AddMinutes(5).ToString("O") + "|" + mode);
        RailTestRunner.Start("two-clients");
    }
    static void Record(string value) => File.AppendAllText(Prefix + "-" + Role + ".txt", DateTime.UtcNow.ToString("O") + " " + value + "\n");
    static void Log(string message, string stack, LogType type)
    {
        if (!active || !EditorApplication.isPlaying) return;
        if (type == LogType.Exception && stack.Contains("UnityEditor.Connect."))
        { Record("EDITOR SERVICE " + message); return; }
        if (message.Contains("PhotonView ID duplicate") || type == LogType.Exception ||
            type == LogType.Error && !message.Contains("OVR") && !message.Contains("OpenXR") && !message.Contains("FormFactor") &&
            !message.StartsWith("LCK requires", StringComparison.Ordinal)) // LIV capture rejects the editor's DX12 device; unrelated to startup.
        { errors++; Record("ERROR " + message + "\n" + stack); }
    }
    static void Loaded(Scene scene, LoadSceneMode loadMode)
    {
        if (!active || !Main || stage != 2 || !PhotonSession.IsPrivate || !PhotonSession.OnTrain || !PhotonNetwork.IsMasterClient) return;
        heldWorld = UnityEngine.Object.FindFirstObjectByType<RailWorld>();
        if (heldWorld != null) { heldWorld.enabled = false; Record("HOLD world before initial seeding"); }
    }
    static PhotonView[] Views() => UnityEngine.Object.FindObjectsByType<PhotonView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(v => v.ViewID != 0).ToArray();
    static string Inventory() => string.Join("\n", Views().Where(v => v.GetComponent<RailCoal>() != null || v.GetComponent<ItemGoldBar>() != null)
        .OrderBy(v => v.ViewID).Select(v => v.ViewID + ":" + v.SpawnToken));
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextTick) return;
        nextTick = EditorApplication.timeSinceStartup + .1;
        if (!EditorApplication.isPlaying) { active = false; return; }
        if (!active)
        {
            if (!File.Exists(Arm)) return;
            var parts = File.ReadAllText(Arm).Split('|');
            if (parts.Length != 2 || !DateTime.TryParse(parts[0], out var expiry) || DateTime.UtcNow > expiry.ToUniversalTime()) return;
            mode = int.Parse(parts[1]); active = true; resumed = checkedResult = false; stage = errors = lastSettling = 0;
            started = EditorApplication.timeSinceStartup; settleAt = 0; heldWorld = null; lastState = null;
            Record("START fault=" + mode);
        }
        try { Step(); }
        catch (Exception e) { Record("FAIL " + e.Message); if (Main) Stop("Recovery fixture failed; inspect saved evidence"); else checkedResult = true; }
    }
    static void Step()
    {
        if (Main && EditorApplication.timeSinceStartup - started > 150) throw new Exception("bounded recovery test timeout");
        var session = PhotonSession.Instance;
        if (session == null) return;
        string state = "actor=" + PhotonNetwork.LocalPlayer.ActorNumber + " master=" + PhotonNetwork.MasterClient?.ActorNumber +
            " phase=" + PhotonSession.Phase + " ready=" + session.WorldReady + " seeded=" + PhotonSession.Get("rails.seeded", "") +
            " views=" + Views().Length + " paused=" + EditorApplication.isPaused;
        if (state != lastState) { Record(state); lastState = state; }
        var handoff = typeof(PhotonMasterHandoff).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
        int settling = handoff == null ? 0 : (int)typeof(PhotonMasterHandoff).GetField("settlingFrom", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(handoff);
        if (settling != lastSettling)
        {
            if (settling != 0) { settleStarted = EditorApplication.timeSinceStartup; Record("SETTLE waiting for previous master " + settling); }
            else Record("SETTLE ended after " + (EditorApplication.timeSinceStartup - settleStarted).ToString("F2") + " s; previous master " + lastSettling +
                (PhotonNetwork.CurrentRoom?.GetPlayer(lastSettling) is var old && (old == null || old.IsInactive) ? " left" : " still active"));
            lastSettling = settling;
        }
        if (Main && stage == 0 && session.WorldReady && !session.HasRoomRequest)
        { stage = 1; session.RequestCreatePrivate(UnityEngine.Random.Range(20000, 90000).ToString()); return; }
        if (Main && stage == 1 && PhotonSession.IsPrivate && PhotonSession.Phase == RailPhase.Staging && session.WorldReady)
        {
            File.WriteAllText(Control + "-room", PhotonNetwork.CurrentRoom.Name);
            if (PhotonNetwork.PlayerList.Length == 2 && PhotonNetwork.PlayerList.All(p => p.CustomProperties[RailSessionRules.Ready] as string == ":Staging"))
            { stage = 2; session.StartPrivateRun(); }
            return;
        }
        if (!Main && !PhotonSession.IsPrivate && session.WorldReady && !session.HasRoomRequest)
        {
            string code = File.ReadAllText(Control + "-room").Trim();
            if (code.Length == 5) session.RequestJoinCode(code);
            return;
        }
        if (!PhotonSession.IsPrivate || !PhotonSession.OnTrain) return;
        if (Main && stage == 2 && heldWorld != null && !session.HasRoomRequest && PhotonPlayer.Local != null)
        {
            if (mode > 0 && !RailRoute.Instance.EnsureReady()) return;
            Require(!session.WorldReady, "fault must precede world readiness");
            if (mode == 1) RailExpedition.Instance.SeedCargo(); // Spawns issued; manifest not committed.
            if (mode == 2) typeof(RailWorld).GetMethod("MaintainCargo", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(heldWorld, null);
            File.WriteAllText(Prefix + "-before", string.Join("\n", Views().Where(v => v.GetComponent<RailCoal>() != null || v.GetComponent<ItemGoldBar>() != null)
                .OrderBy(v => v.ViewID).Select(v => v.ViewID + ":" + v.SpawnToken + "|" + string.Join("/", v.InstantiationData))));
            PhotonNetwork.SendAllOutgoingCommands();
            stage = 3;
            Record("PAUSE host before " + (mode == 0 ? "route selection and spawning" : mode == 1 ? "manifest commit" : "first cargo serialization"));
            // MPPM mirrors EditorApplication.isPaused to every editor. Stall only
            // this process's main/game loop instead; Photon's background ACK
            // thread remains alive, exactly the failure the election must handle.
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (File.ReadAllText(Control + "-recovered").Length == 0 && watch.Elapsed.TotalSeconds < 25)
                System.Threading.Thread.Sleep(100);
            Require(File.ReadAllText(Control + "-recovered").Length > 0, "peer did not recover within 25 seconds of host stall");
            resumed = true; Record("RESUME after " + watch.Elapsed.TotalSeconds.ToString("F2") + " seconds");
            return;
        }
        if (!Main && PhotonNetwork.IsMasterClient && session.WorldReady && !checkedResult)
        {
            if (File.ReadAllText(Control + "-recovered").Length == 0)
            { Record("RECOVERED while original host paused"); File.WriteAllText(Control + "-recovered", PhotonSession.RunId); }
        }
        if (Main && resumed && heldWorld != null && !PhotonNetwork.IsMasterClient) { heldWorld.enabled = true; heldWorld = null; }
        if (!session.WorldReady || session.HasRoomRequest || PhotonSession.Phase != RailPhase.Running ||
            PhotonNetwork.PlayerList.Length != 2 || !PhotonNetwork.PlayerList.All(p => p.CustomProperties[RailSessionRules.Ready] as string == PhotonSession.RunId + ":Running")) return;
        if (settleAt == 0) settleAt = EditorApplication.timeSinceStartup;
        if (EditorApplication.timeSinceStartup - settleAt < 5) return;
        if (!checkedResult)
        {
            Require(errors == 0, "unexpected errors=" + errors);
            var views = Views();
            Require(views.GroupBy(v => v.ViewID).All(g => g.Count() == 1) && views.All(v => PhotonView.Find(v.ViewID) == v), "unique registered views");
            var loot = views.Where(v => v.GetComponent<RailCoal>() != null || v.GetComponent<ItemGoldBar>() != null).ToArray();
            Require(loot.Count(v => v.GetComponent<RailCoal>() != null) == RailExpedition.Instance.coalPoints.Length, "coal count");
            Require(loot.Count(v => v.GetComponent<ItemGoldBar>() != null) == RailExpedition.Instance.goldPoints.Length, "gold count");
            Require(loot.Select(v => string.Join("/", v.InstantiationData)).Distinct().Count() == loot.Length, "unique loot markers");
            string inventory = Inventory();
            // November 21: spawns the stalled host issued but could not send (Photon's 50-command
            // reliable window) are replaced by the successor's copies, so require each pre-pause
            // marker to survive exactly once rather than its original view ID and spawn token.
            var markers = new System.Collections.Generic.HashSet<string>(loot.Select(v => string.Join("/", v.InstantiationData)));
            var before = File.ReadAllLines(Prefix + "-before").Where(line => line.Length > 0).ToArray();
            int kept = before.Count(line => inventory.Split('\n').Contains(line.Substring(0, line.IndexOf('|'))));
            Require(before.All(line => markers.Contains(line.Substring(line.LastIndexOf('|') + 1))), "pre-pause loot markers retained");
            Record("pre-pause identities kept " + kept + "/" + before.Length + "; markers kept " + before.Length + "/" + before.Length);
            File.WriteAllText(Prefix + "-" + Role + ".inventory", inventory);
            checkedResult = true; Record("PASS ready after recovery; unique IDs/markers; exact loot counts; pre-pause markers retained");
        }
        if (Main && File.ReadAllText(Prefix + "-remote.inventory").Length > 0)
        {
            Require(File.ReadAllText(Prefix + "-main.inventory") == File.ReadAllText(Prefix + "-remote.inventory"), "peer inventories match");
            Record("PASS COMPLETE identical peer inventories; host resumed without duplicates");
            Stop("Startup recovery fault " + mode + " passed");
        }
    }
    public static void Stop(string result)
    {
        if (heldWorld != null) heldWorld.enabled = true;
        EditorApplication.isPaused = false;
        File.WriteAllText(Arm, ""); active = false;
        RailTestRunner.Stop(result);
    }
}
#endif
