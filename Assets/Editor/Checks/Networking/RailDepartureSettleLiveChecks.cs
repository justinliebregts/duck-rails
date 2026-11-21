#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Photon.Pun;
using Unity.Multiplayer.PlayMode;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Opt-in three-process acceptance for the November 20 room 26164 fix (spawn-startup.md).
/// Each round: the crew departs one public Compound together, every client compares coal/gold
/// view IDs and spawn tokens, the master hands authority to a Ready successor (settle must end
/// by acknowledgement), the previous master transport-reconnects as a non-master and must
/// replay one instantiation per view, then the crew downs and returns. Shared files coordinate
/// steps only; rooms, spawns, handoffs and recovery use real Photon. No diagnostic report access.
/// </summary>
[InitializeOnLoad]
public static class RailDepartureSettleLiveChecks
{
    [Serializable] sealed class Control
    {
        public string token, expires, compound, expedition;
        public int step, round, revision, rounds = 4, oldMaster, faultActor, clients = 3;
    }
    [Serializable] sealed class Receipt
    {
        public string token, room, inventory, note, error;
        public int revision, actor, master, duplicates;
    }
    const string Key = "Rails.DepartureSettle";
    static string Root { get { var p = Application.dataPath.Replace('\\', '/'); var i = p.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase); return i < 0 ? Path.GetDirectoryName(p) : p.Substring(0, i); } }
    static string Folder => Root + "/Temp/rails-departure-settle";
    static string ControlPath => Folder + "/control.json";
    static bool Host => CurrentPlayer.IsMainEditor;
    static string Role => Host ? "main" : CurrentPlayer.Tags.Contains("Player2") ? "player2" : "player3";
    static string[] Roles => control != null && control.clients == 2 ? new[] { "main", "player2" } : new[] { "main", "player2", "player3" };
    static string ReceiptPath(string role) => Folder + "/" + role + ".json";

    static Control control;
    static string token, networkState;
    static int revision = -1, lastMaster, lastSettling, duplicates;
    static bool action, ack, done, disconnected, simulation, clockNoted;
    static double entered, nextTick, readySince, settleStarted, faultStarted;
    static int[] savedSimulation;

    static RailDepartureSettleLiveChecks()
    {
        EditorApplication.update += Tick;
        EditorApplication.update += Restore;
        Application.logMessageReceived += OnLog;
        EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.ExitingPlayMode) RestoreSimulation(); };
    }

    static ScriptableObject Manager => Resources.FindObjectsOfTypeAll<ScriptableObject>().FirstOrDefault(o => o.GetType().FullName == "Unity.PlayMode.Editor.PlayModeManager");
    public static string Status => Manager?.GetType().GetProperty("CurrentState")?.GetValue(Manager)?.ToString() ?? "Unavailable";

    /// <summary>Main editor, Edit Mode: arm and launch the three-client scenario.</summary>
    public static void Start(int rounds = 4, int clients = 3)
    {
        if (!CurrentPlayer.IsMainEditor || EditorApplication.isPlayingOrWillChangePlaymode || Status != "NotRunning") throw new Exception("Main editor in Edit Mode with no scenario running");
        if (!MppmStandaloneCredentials.TryGet("Player2", out _) || !MppmStandaloneCredentials.TryGet("Player3", out _)) throw new Exception("Configure Player2/Player3 accounts");
        Directory.CreateDirectory(Folder);
        foreach (var role in Roles) { File.WriteAllText(ReceiptPath(role), ""); File.WriteAllText(Folder + "/" + role + ".txt", ""); }
        var property = Manager.GetType().GetProperty("ActivePlayModeConfig");
        SessionState.SetString(Key + ".scenario", AssetDatabase.GetAssetPath((UnityEngine.Object)property.GetValue(Manager)));
        SessionState.SetBool(Key + ".quick", RailEditorQuickStart.Enabled);
        SessionState.SetBool(Key, true);
        RailEditorQuickStart.Enabled = false;
        var c = new Control { clients = clients, rounds = rounds, token = Guid.NewGuid().ToString("N"), expires = DateTime.UtcNow.AddMinutes(60).ToString("O") };
        File.WriteAllText(ControlPath, JsonUtility.ToJson(c));
        property.SetValue(Manager, AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(clients == 2 ? "Assets/Settings/PlayMode/4 Diagnostic Two Client.asset" : "Assets/Settings/PlayMode/3 Week 2 Test.asset"));
        Manager.GetType().GetMethod("Start", Type.EmptyTypes).Invoke(Manager, null);
    }

    public static void Stop()
    {
        if (File.Exists(ControlPath)) File.WriteAllText(ControlPath, "");
        RestoreSimulation();
        if (Status == "Running") Manager.GetType().GetMethod("Stop", Type.EmptyTypes).Invoke(Manager, null);
    }

    static void Restore()
    {
        if (!CurrentPlayer.IsMainEditor || !SessionState.GetBool(Key, false)) return;
        if (Status != "NotRunning" || EditorApplication.isPlayingOrWillChangePlaymode) return;
        RailEditorQuickStart.Enabled = SessionState.GetBool(Key + ".quick", true);
        string scenario = SessionState.GetString(Key + ".scenario", "");
        if (scenario.Length > 0) Manager.GetType().GetProperty("ActivePlayModeConfig").SetValue(Manager, AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(scenario));
        SessionState.SetBool(Key, false);
    }

    static void Log(string message)
    {
        try { File.AppendAllText(Folder + "/" + Role + ".txt", DateTime.UtcNow.ToString("O") + " " + message + "\n"); } catch (IOException) { }
    }
    static void OnLog(string message, string stack, LogType type)
    {
        if (control == null || done || !EditorApplication.isPlaying) return;
        if (message.Contains("PhotonView ID duplicate")) { duplicates++; Log("DUPLICATE " + message); }
        else if (message.Contains("Master handoff settle")) Log("LOG " + message);
        else if (type == LogType.Exception || type == LogType.Error && !message.Contains("OVR") && !message.Contains("OpenXR") && !message.Contains("FormFactor") && !message.StartsWith("LCK requires", StringComparison.Ordinal))
            Log("ERROR " + message.Split('\n')[0]);
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Next(int step)
    {
        control.step = step; control.revision++;
        File.WriteAllText(ControlPath, JsonUtility.ToJson(control));
    }
    static void Ack(string message, string inventory = "")
    {
        if (ack) return;
        File.WriteAllText(ReceiptPath(Role), JsonUtility.ToJson(new Receipt { token = token, revision = revision, room = PhotonNetwork.CurrentRoom?.Name,
            inventory = inventory, note = message, actor = PhotonNetwork.LocalPlayer.ActorNumber, master = PhotonNetwork.MasterClient?.ActorNumber ?? 0, duplicates = duplicates }));
        Log("PASS round=" + control.round + " step=" + control.step + " " + message);
        ack = true;
    }
    static Receipt ReadReceipt(string role)
    {
        if (!File.Exists(ReceiptPath(role))) return null;
        string json = File.ReadAllText(ReceiptPath(role));
        if (string.IsNullOrEmpty(json)) return null;
        var receipt = JsonUtility.FromJson<Receipt>(json);
        return receipt != null && receipt.token == token ? receipt : null;
    }
    static bool AllAck => Roles.All(r => ReadReceipt(r)?.revision == revision);
    static bool Ready => PhotonSession.Instance != null && PhotonSession.IsRailsRoom && PhotonSession.Instance.WorldReady && !PhotonSession.Instance.HasRoomRequest;
    static int ActivePlayers => PhotonNetwork.PlayerList.Count(p => !p.IsInactive);
    static bool AllPoses => PhotonNetwork.PlayerList.All(p => !p.IsInactive && PhotonPlayer.ForActor(p.ActorNumber) != null);
    static bool Settled
    {
        get
        {
            if (!Ready) { readySince = 0; return false; }
            if (readySince == 0) readySince = EditorApplication.timeSinceStartup;
            return EditorApplication.timeSinceStartup - readySince > 3;
        }
    }
    static void Teleport(Vector3 position) => NewPlayer.instance.Warp(position, Quaternion.identity);
    static void Board() => Teleport(RailTrain.Instance.transform.TransformPoint(new Vector3(PhotonNetwork.LocalPlayer.ActorNumber % 2 == 0 ? -.8f : .8f, .35f, -1 - (PhotonNetwork.LocalPlayer.ActorNumber % 4) / 2 * 1.5f)));

    static int SettlingFrom()
    {
        const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var instance = typeof(PhotonMasterHandoff).GetField("instance", F)?.GetValue(null);
        return instance == null ? 0 : (int)typeof(PhotonMasterHandoff).GetField("settlingFrom", F).GetValue(instance);
    }
    static PhotonView[] Views() => UnityEngine.Object.FindObjectsByType<PhotonView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(v => v.ViewID != 0).ToArray();
    static string Marker(PhotonView v) => v.InstantiationData == null ? "" : string.Join("/", v.InstantiationData.Select(o => o is object[] a ? "[" + string.Join(",", a) + "]" : o?.ToString()));
    /// <summary>Checks unique registration and authored counts, then returns the sorted loot inventory.</summary>
    static string CheckedInventory()
    {
        var views = Views();
        var shared = views.GroupBy(v => v.ViewID).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        Require(shared.Length == 0, "Duplicate view objects " + string.Join(",", shared));
        Require(views.All(v => PhotonView.Find(v.ViewID) == v), "Unregistered view");
        var loot = views.Where(v => v.GetComponent<RailCoal>() != null || v.GetComponent<ItemGoldBar>() != null).ToArray();
        Require(loot.Count(v => v.GetComponent<RailCoal>() != null) == RailExpedition.Instance.coalPoints.Length, "Coal count " + loot.Count(v => v.GetComponent<RailCoal>() != null) + " != " + RailExpedition.Instance.coalPoints.Length);
        Require(loot.Count(v => v.GetComponent<ItemGoldBar>() != null) == RailExpedition.Instance.goldPoints.Length, "Gold count " + loot.Count(v => v.GetComponent<ItemGoldBar>() != null) + " != " + RailExpedition.Instance.goldPoints.Length);
        Require(loot.Select(Marker).Distinct().Count() == loot.Length, "Two loot views share a marker");
        int pending = RailCargo.Active.Count(c => !c.StateReady);
        Require(pending == 0, "Pending cargo " + pending);
        Require(duplicates == 0, "PhotonView ID duplicate errors " + duplicates);
        return string.Join(";", loot.OrderBy(v => v.ViewID).Select(v => v.ViewID + ":" + v.SpawnToken + ":" + Marker(v)));
    }
    static void CompareInventories(string what)
    {
        string first = ReadReceipt("main").inventory;
        foreach (var role in Roles) Require(ReadReceipt(role).inventory == first, what + ": inventory differs on " + role);
        Log("ROUND " + control.round + " " + what + ": identical inventories (" + first.Split(';').Length + " loot views) on all clients");
    }

    static void Simulate(bool on)
    {
        var peer = PhotonNetwork.NetworkingClient.LoadBalancingPeer;
        var s = peer.NetworkSimulationSettings;
        if (on)
        {
            Require(!peer.IsSimulationEnabled, "Network simulation already enabled");
            savedSimulation = new[] { s.IncomingLossPercentage, s.OutgoingLossPercentage };
            s.IncomingLossPercentage = s.OutgoingLossPercentage = 100;
            simulation = peer.IsSimulationEnabled = true;
            Log("FAULT 100% transport loss");
        }
        else RestoreSimulation();
    }
    static void RestoreSimulation()
    {
        if (!simulation) return;
        var peer = PhotonNetwork.NetworkingClient.LoadBalancingPeer;
        peer.IsSimulationEnabled = false;
        peer.NetworkSimulationSettings.IncomingLossPercentage = savedSimulation[0];
        peer.NetworkSimulationSettings.OutgoingLossPercentage = savedSimulation[1];
        simulation = false;
        Log("FAULT restored transport");
    }

    static void Monitor()
    {
        string state = PhotonNetwork.NetworkClientState + " room=" + PhotonNetwork.CurrentRoom?.Name + " actor=" + PhotonNetwork.LocalPlayer?.ActorNumber
            + " phase=" + PhotonSession.Phase + " ready=" + PhotonSession.Instance?.WorldReady;
        if (state != networkState) { networkState = state; Log("STATE " + state); }
        if (!PhotonNetwork.InRoom) { lastMaster = 0; lastSettling = 0; clockNoted = false; return; }
        if (PhotonNetwork.ServerTimestamp == 0 && !clockNoted) { clockNoted = true; Log("CLOCK unsynchronised in " + PhotonNetwork.CurrentRoom.Name); }
        if (PhotonNetwork.ServerTimestamp != 0 && clockNoted) { clockNoted = false; Log("CLOCK synchronised"); }
        int master = PhotonNetwork.MasterClient?.ActorNumber ?? 0;
        if (master != lastMaster)
        {
            if (lastMaster != 0) Log("MASTER " + lastMaster + " -> " + master + " room=" + PhotonNetwork.CurrentRoom.Name + " phase=" + PhotonSession.Phase + " ready=" + PhotonSession.Instance?.WorldReady + " step=" + control.step);
            lastMaster = master;
        }
        int settling = SettlingFrom();
        if (settling != lastSettling)
        {
            double now = EditorApplication.timeSinceStartup;
            if (settling != 0) { settleStarted = now; Log("SETTLE waiting for previous master " + settling); }
            else
            {
                var old = PhotonNetwork.CurrentRoom.GetPlayer(lastSettling);
                double took = now - settleStarted;
                string reason = old == null || old.IsInactive ? "left" : took >= 4.8 ? "timeout" : "ack";
                Log("SETTLE ended reason=" + reason + " after=" + took.ToString("F2") + "s previous=" + lastSettling);
                if (control.step == 3) handoffSettle = reason;
            }
            lastSettling = settling;
        }
    }
    static string handoffSettle;

    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextTick) return;
        nextTick = EditorApplication.timeSinceStartup + .1;
        try
        {
            if (!File.Exists(ControlPath)) { RestoreSimulation(); return; }
            string json = File.ReadAllText(ControlPath);
            if (string.IsNullOrEmpty(json)) { RestoreSimulation(); control = null; return; }
            try { control = JsonUtility.FromJson<Control>(json); } catch (ArgumentException) { return; }
            if (control == null || !DateTime.TryParse(control.expires, out var expires) || DateTime.UtcNow > expires.ToUniversalTime()) { RestoreSimulation(); return; }
            if (token != control.token)
            {
                token = control.token; revision = -1; done = false; duplicates = 0; lastMaster = lastSettling = 0;
                Log("START departure settle acceptance; Unity " + Application.unityVersion + " rounds=" + control.rounds);
            }
            if (done) return;
            Monitor();
            foreach (string role in Roles)
                if (ReadReceipt(role)?.error is string error && error.Length > 0) throw new Exception("Peer " + role + " failure: " + error);
            if (revision != control.revision)
            {
                revision = control.revision; entered = EditorApplication.timeSinceStartup;
                action = ack = disconnected = false; readySince = 0; handoffSettle = null;
                Log("step=" + control.step + " round=" + control.round);
            }
            double age = EditorApplication.timeSinceStartup - entered;
            Require(age < 180, "Timeout step=" + control.step + " phase=" + PhotonSession.Phase + " status=" + PhotonSession.Instance?.Status);
            var session = PhotonSession.Instance;
            switch (control.step)
            {
                case 0: // Host opens a fresh public Compound.
                    if (!Host || !Settled) break;
                    if (!action) { session.RequestCreatePublic(); action = true; readySince = 0; break; }
                    if (PhotonSession.Kind != RailSessionRules.CompoundKind) break;
                    control.compound = PhotonNetwork.CurrentRoom.Name; Next(1); break;
                case 1: // Gather all three in the Compound.
                    if (!Ready) break;
                    if (PhotonNetwork.CurrentRoom.Name != control.compound)
                    {
                        if (age > 2 && (!action || age % 15 < .1)) { session.RequestJoinCode(control.compound); action = true; readySince = 0; }
                        break;
                    }
                    if (!Settled || ActivePlayers != control.clients || !AllPoses) break;
                    Require(PhotonNetwork.PlayerList.Select(p => p.UserId).Distinct().Count() == control.clients, "Distinct authenticated accounts");
                    Teleport(new Vector3(8 + Array.IndexOf(Roles, Role), .4f, 4));
                    Ack("gathered in " + control.compound);
                    if (Host && AllAck) Next(2);
                    break;
                case 2: // Depart together; compare markers, view IDs and spawn tokens once all are ready.
                    if (PhotonSession.Kind == RailSessionRules.CompoundKind)
                    {
                        if (Ready && RailWorld.Instance != null && RailWorld.Instance.boardingTrigger != null) Teleport(RailWorld.Instance.boardingTrigger.bounds.center - Vector3.up * .8f);
                        break;
                    }
                    Require(PhotonSession.Phase != RailPhase.Ending && PhotonSession.Phase != RailPhase.Returning, "Departure aborted before ready");
                    if (PhotonSession.Phase != RailPhase.Running || !Settled || ActivePlayers != control.clients || !AllPoses) break;
                    if (age < 8) break; // Let a possible startup takeover finish before sampling.
                    if (!action) { Board(); action = true; }
                    Ack("departed together into " + PhotonNetwork.CurrentRoom.Name + " master=" + PhotonNetwork.MasterClient.ActorNumber, CheckedInventory());
                    if (Host && AllAck)
                    {
                        Require(Roles.All(r => ReadReceipt(r).room == ReadReceipt("main").room), "Crew split across expeditions");
                        CompareInventories("departure");
                        control.expedition = PhotonNetwork.CurrentRoom.Name;
                        control.oldMaster = PhotonNetwork.MasterClient.ActorNumber;
                        Next(3);
                    }
                    break;
                case 3: // Current master hands authority to a Ready successor; settle must end by acknowledgement.
                    if (!action && PhotonNetwork.IsMasterClient && PhotonNetwork.MasterClient.ActorNumber == control.oldMaster)
                    {
                        var successor = PhotonNetwork.PlayerList.Where(p => !p.IsLocal && !p.IsInactive).OrderBy(p => p.ActorNumber).First();
                        Log("HANDOFF request " + control.oldMaster + " -> " + successor.ActorNumber);
                        PhotonNetwork.SetMasterClient(successor); action = true; break;
                    }
                    if (PhotonNetwork.MasterClient.ActorNumber == control.oldMaster || SettlingFrom() != 0 || !Settled) break;
                    if (PhotonNetwork.IsMasterClient && handoffSettle == null) break;
                    if (age < 6) break;
                    string settleNote = PhotonNetwork.IsMasterClient ? " settle=" + handoffSettle : "";
                    if (PhotonNetwork.IsMasterClient) Require(handoffSettle == "ack", "Live previous master settle ended by " + handoffSettle);
                    Ack("handoff " + control.oldMaster + " -> " + PhotonNetwork.MasterClient.ActorNumber + settleNote, CheckedInventory());
                    if (Host && AllAck)
                    {
                        CompareInventories("after handoff");
                        control.faultActor = control.oldMaster; Next(4);
                    }
                    break;
                case 4: // The previous master, now a non-master, loses transport and rejoins its reserved actor.
                    if (action || PhotonNetwork.LocalPlayer.ActorNumber == control.faultActor)
                    {
                        if (!action) { Require(!PhotonNetwork.IsMasterClient, "Fault peer is still master"); Simulate(true); faultStarted = EditorApplication.timeSinceStartup; action = true; }
                        double lossAge = EditorApplication.timeSinceStartup - faultStarted;
                        if (!PhotonNetwork.InRoom) disconnected = true;
                        if (lossAge > 20) RestoreSimulation();
                        if (lossAge > 23 && disconnected && Settled && ActivePlayers == control.clients && AllPoses && PhotonSession.Phase == RailPhase.Running && age > 30)
                        {
                            Require(PhotonNetwork.CurrentRoom.Name == control.expedition, "Rejoined a different room");
                            Require(PhotonNetwork.LocalPlayer.ActorNumber == control.faultActor, "Actor not reclaimed: " + PhotonNetwork.LocalPlayer.ActorNumber);
                            Ack("non-master transport reconnect reclaimed actor " + control.faultActor + " after " + lossAge.ToString("F1") + "s", CheckedInventory());
                        }
                    }
                    else
                    {
                        if (PhotonNetwork.CurrentRoom?.GetPlayer(control.faultActor)?.IsInactive == true) disconnected = true;
                        if (disconnected && Settled && ActivePlayers == control.clients && AllPoses && PhotonSession.Phase == RailPhase.Running && age > 30)
                            Ack("observed peer " + control.faultActor + " leave and return", CheckedInventory());
                    }
                    if (Host && AllAck) { CompareInventories("after non-master reconnect"); Next(5); }
                    break;
                case 5: // Everyone downs; the crew returns to a Compound.
                    if (!Ready) break;
                    // Downs aboard are ignored without a boarded attacker, so step off the stopped train first.
                    if (PhotonSession.Phase == RailPhase.Running && RailExpedition.Instance != null)
                    {
                        if (!action) { Teleport(RailTrain.Instance.transform.TransformPoint(new Vector3(6 + Array.IndexOf(Roles, Role), .5f, 0))); action = true; break; }
                        RailExpedition.RequestDown();
                    }
                    if (PhotonSession.Kind != RailSessionRules.CompoundKind || !Settled || !AllPoses) break;
                    Ack("returned to " + PhotonNetwork.CurrentRoom.Name);
                    if (Host && AllAck)
                    {
                        Log("ROUND " + control.round + " PASS");
                        control.compound = PhotonNetwork.CurrentRoom.Name;
                        if (++control.round < control.rounds) Next(1); else Next(6);
                    }
                    break;
                case 6:
                    RestoreSimulation(); done = true;
                    Log("PASS COMPLETE " + control.rounds + " simultaneous public departures: identical loot view IDs/spawn tokens, no duplicate views, ready with zero pending cargo, acknowledged handoffs and non-master reconnects");
                    break;
            }
        }
        catch (IOException) { }
        catch (Exception e)
        {
            RestoreSimulation(); done = true;
            Log("FAIL " + e.Message);
            File.WriteAllText(ReceiptPath(Role), JsonUtility.ToJson(new Receipt { token = token, revision = revision, error = e.Message }));
        }
    }
}
#endif
