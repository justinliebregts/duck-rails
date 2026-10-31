#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Photon.Pun;
using Photon.Voice.PUN;
using Unity.Multiplayer.PlayMode;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Opt-in three-process regression. Shared files coordinate test steps only;
/// room transfers, avatars, cargo, motion, recovery and voice use real Photon.
/// Two players depart while the third keeps the source compound alive.
/// </summary>
[InitializeOnLoad]
public static class RailStabilityLiveChecks
{
    [Serializable] sealed class Control
    {
        public string token, expires, compound, expedition, run, route;
        public int step, round, revision, faultActor;
    }
    [Serializable] sealed class Receipt
    {
        public string token, room, avatars, error;
        public int revision;
        public float fuel;
    }
    static string Root { get { var p = Application.dataPath.Replace('\\', '/'); var i = p.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase); return i < 0 ? Path.GetDirectoryName(p) : p.Substring(0, i); } }
    static string Folder => Root + "/Temp/rails-stability";
    static string ControlPath => Folder + "/control.json";
    static bool Host => CurrentPlayer.IsMainEditor;
    static bool Second => CurrentPlayer.Tags.Contains("Player2");
    static bool Shopper => !Host && !Second;
    static string Role => Host ? "main" : Second ? "player2" : "player3";
    static Control control;
    static string token, networkState;
    static int revision = -1, commands;
    static bool action, ack, done, disconnected, simulation;
    static double entered, nextTick, nextCommand, readySince;
    static float distance;
    static int[] savedSimulation;
    static string ReceiptPath(string role) => Folder + "/" + role + ".json";
    static RailStabilityLiveChecks()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.ExitingPlayMode) RestoreSimulation(); };
    }
    [MenuItem("Duck Rails/Arm Three Client Stability Test")]
    public static void Arm()
    {
        if (EditorApplication.isPlaying) throw new Exception("Arm before Play");
        if (!MppmStandaloneCredentials.TryGet("Player2", out _) || !MppmStandaloneCredentials.TryGet("Player3", out _))
            throw new Exception("Three configured accounts required");
        Directory.CreateDirectory(Folder);
        RailCoalLiveChecks.Disarm(); RailWeek3LiveChecks.Disarm();
        File.WriteAllText(Root + "/Temp/rails-week2-arm.txt", "");
        EditorPrefs.SetBool("Rails.Stability.PreviousQuickStart", RailEditorQuickStart.Enabled);
        RailEditorQuickStart.Enabled = false;
        var c = new Control { token = Guid.NewGuid().ToString("N"), expires = DateTime.UtcNow.AddMinutes(40).ToString("O") };
        File.WriteAllText(ControlPath, JsonUtility.ToJson(c));
    }
    [MenuItem("Duck Rails/Disarm Three Client Stability Test")]
    public static void Disarm()
    {
        if (File.Exists(ControlPath)) File.WriteAllText(ControlPath, "");
        RestoreSimulation();
        RailEditorQuickStart.Enabled = EditorPrefs.GetBool("Rails.Stability.PreviousQuickStart", true);
    }
    static void Log(string message)
    {
        File.AppendAllText(Folder + "/" + Role + ".txt", DateTime.UtcNow.ToString("O") + " " + message + "\n");
        Debug.Log("STABILITY " + Role + " " + message);
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Next(int step)
    {
        control.step = step; control.revision++;
        File.WriteAllText(ControlPath, JsonUtility.ToJson(control));
    }
    static void Ack(string message, bool avatarCheck = true)
    {
        if (ack) return;
        string avatars = avatarCheck ? RailAvatarChecks.AssertCurrentRoom() : "n/a";
        File.WriteAllText(ReceiptPath(Role), JsonUtility.ToJson(new Receipt { token = token, revision = revision,
            room = PhotonNetwork.CurrentRoom?.Name, avatars = avatars, fuel = PhotonSession.CurrentFuel }));
        Log("PASS round=" + control.round + " step=" + control.step + " " + message + " avatars=" + avatars);
        ack = true;
    }
    static Receipt ReadReceipt(string role)
    {
        if (!File.Exists(ReceiptPath(role))) return null;
        var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText(ReceiptPath(role)));
        return receipt != null && receipt.token == token ? receipt : null;
    }
    static bool AllAck => new[] { "main", "player2", "player3" }.All(r => ReadReceipt(r)?.revision == revision);
    static bool Ready => PhotonSession.Instance != null && PhotonSession.IsRailsRoom && PhotonSession.Instance.WorldReady && !PhotonSession.Instance.HasRoomRequest;
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
    static void Board() => Teleport(RailTrain.Instance.transform.TransformPoint(new Vector3(Host ? -1 : 1, .35f, -1)));
    static bool VoiceReady()
    {
        var voice = UnityEngine.Object.FindFirstObjectByType<PunVoiceClient>();
        return voice != null && voice.Client.InRoom
            && voice.Client.CurrentRoom.Name == PhotonNetwork.CurrentRoom.Name + PunVoiceClient.VoiceRoomNameSuffix
            && voice.Client.CurrentRoom.PlayerCount == PhotonNetwork.PlayerList.Count(p => !p.IsInactive);
    }
    static void Simulate(bool loss)
    {
        var peer = PhotonNetwork.NetworkingClient.LoadBalancingPeer;
        Require(!peer.IsSimulationEnabled, "Existing network simulation must be disabled before this test");
        var s = peer.NetworkSimulationSettings;
        savedSimulation = new[] { s.IncomingLag, s.OutgoingLag, s.IncomingJitter, s.OutgoingJitter, s.IncomingLossPercentage, s.OutgoingLossPercentage };
        s.IncomingLag = s.OutgoingLag = loss ? 0 : 120;
        s.IncomingJitter = s.OutgoingJitter = loss ? 0 : 25;
        s.IncomingLossPercentage = s.OutgoingLossPercentage = loss ? 100 : 3;
        simulation = true; peer.IsSimulationEnabled = true;
        Log(loss ? "Injected 100% transport loss" : "Injected 120ms lag each direction, 25ms jitter, 3% packet loss each direction");
    }
    static void RestoreSimulation()
    {
        if (!simulation) return;
        var peer = PhotonNetwork.NetworkingClient.LoadBalancingPeer;
        peer.IsSimulationEnabled = false;
        var s = peer.NetworkSimulationSettings;
        s.IncomingLag = savedSimulation[0]; s.OutgoingLag = savedSimulation[1];
        s.IncomingJitter = savedSimulation[2]; s.OutgoingJitter = savedSimulation[3];
        s.IncomingLossPercentage = savedSimulation[4]; s.OutgoingLossPercentage = savedSimulation[5];
        simulation = false;
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextTick) return;
        nextTick = EditorApplication.timeSinceStartup + .1;
        try
        {
            if (!File.Exists(ControlPath)) { RestoreSimulation(); return; }
            string json = File.ReadAllText(ControlPath);
            if (string.IsNullOrEmpty(json)) { RestoreSimulation(); return; }
            try { control = JsonUtility.FromJson<Control>(json); } catch (ArgumentException) { return; }
            if (control == null || !DateTime.TryParse(control.expires, out var expires) || DateTime.UtcNow > expires.ToUniversalTime())
            { RestoreSimulation(); return; }
            if (token != control.token)
            {
                token = control.token; revision = -1; done = false;
                File.WriteAllText(Folder + "/" + Role + ".txt", "");
                Log("Starting three-client public lifecycle and network recovery test; Unity " + Application.unityVersion);
            }
            if (done) return;
            var currentNetworkState = PhotonNetwork.NetworkClientState + " room=" + PhotonNetwork.CurrentRoom?.Name
                + " master=" + PhotonNetwork.IsMasterClient;
            if (currentNetworkState != networkState)
            { networkState = currentNetworkState; Log("Network " + networkState); }
            foreach (string role in new[] { "main", "player2", "player3" })
                if (ReadReceipt(role)?.error is string error && error.Length > 0) throw new Exception("Peer failure: " + error);
            if (revision != control.revision)
            {
                revision = control.revision; entered = EditorApplication.timeSinceStartup;
                action = ack = disconnected = false; commands = 0; nextCommand = 0; readySince = 0;
                Log("step=" + control.step + " round=" + control.round);
            }
            double age = EditorApplication.timeSinceStartup - entered;
            Require(age < 150, "Timeout phase=" + PhotonSession.Phase + " status=" + PhotonSession.Instance?.Status + " " + PhotonSession.Instance?.CrewReturnDiagnostic);
            var session = PhotonSession.Instance;
            switch (control.step)
            {
                case 0:
                    if (!Host || !Settled) break;
                    if (!action) { session.RequestCreatePublic(); action = true; readySince = 0; break; }
                    control.compound = PhotonNetwork.CurrentRoom.Name; Next(1); break;
                case 1:
                    if (!Ready) break;
                    if (PhotonNetwork.CurrentRoom.Name != control.compound) { session.RequestJoinCode(control.compound); readySince = 0; break; }
                    if (!Settled || PhotonNetwork.PlayerList.Length != 3 || !AllPoses) break;
                    Require(PhotonNetwork.PlayerList.Select(p => p.UserId).Distinct().Count() == 3, "Distinct authenticated accounts");
                    Teleport(new Vector3(8 + (Shopper ? 2 : 0), .4f, 4));
                    Ack("Three accounts gathered in fresh public compound");
                    if (Host && AllAck) Next(2); break;
                case 2:
                    if (!Ready) break;
                    if (Shopper)
                    {
                        Require(PhotonSession.Kind == RailSessionRules.CompoundKind && PhotonNetwork.CurrentRoom.Name == control.compound, "Shopper left source compound");
                        if (PhotonNetwork.PlayerList.Length == 1) Ack("Shopper remained in source compound");
                    }
                    else if (PhotonSession.Kind == RailSessionRules.CompoundKind)
                        Teleport(RailWorld.Instance.boardingTrigger.bounds.center - Vector3.up * .8f);
                    else if (PhotonSession.Phase == RailPhase.Running && Settled && AllPoses)
                    {
                        Require(PhotonNetwork.PlayerList.Length == 2 && RailCargo.Active.Count == 42, "Exactly two departing crew and 42 fresh supplies");
                        Board(); Ack("Public departure loaded Expedition with only platform occupants");
                    }
                    if (Host && AllAck)
                    {
                        if (!PhotonNetwork.IsMasterClient) { PhotonNetwork.SetMasterClient(PhotonNetwork.LocalPlayer); break; }
                        control.expedition = PhotonNetwork.CurrentRoom.Name; control.run = PhotonSession.RunId;
                        control.route = PhotonSession.Get(RailRoute.StateKey, ""); Next(3);
                    }
                    break;
                case 3:
                    if (Shopper) { Ack("Shopper stays available"); break; }
                    if (!Settled) break;
                    if (!action)
                    {
                        if (Second && PhotonNetwork.IsMasterClient)
                        { PhotonNetwork.SetMasterClient(PhotonNetwork.PlayerList.First(p => !p.IsLocal)); break; }
                        Board(); distance = RailTrain.Instance.Distance;
                        if (Second) { Simulate(false); session.SetTrainMoving(true); }
                        action = true;
                    }
                    if (age > 16)
                    {
                        Require(RailTrain.Instance.Distance > distance + 20 && RailTrain.Instance.Speed > 3.9f, "Non-master GO under latency");
                        Require(BagObject.Active.All(b => RailTrain.Instance.Contains(b.transform.position)), "Loose bags remain aboard");
                        Require(RailTrain.Instance.Contains(GR.Player.instance.transform.position + Vector3.up), "Local passenger remains aboard");
                        Ack("Moving passengers and loose bags under simulated latency/loss");
                    }
                    if (Host && AllAck) Next(4); break;
                case 4:
                    if (Shopper) { Ack("Shopper stays available"); break; }
                    if (!Ready) break;
                    if (Second && commands < 16 && EditorApplication.timeSinceStartup > nextCommand)
                    { session.SetTrainMoving(commands % 2 == 0); commands++; nextCommand = EditorApplication.timeSinceStartup + .25; }
                    if (age > 12)
                    {
                        Require(RailTrain.Instance.Speed == 0, "Final STOP lost after rapid commands");
                        RestoreSimulation();
                        Ack("16 rapid GO/STOP requests converged to stopped train");
                    }
                    if (Host && AllAck)
                    {
                        Require(Mathf.Abs(ReadReceipt("main").fuel - ReadReceipt("player2").fuel) < .02f, "Stopped fuel differs between clients");
                        control.faultActor = PhotonNetwork.LocalPlayer.ActorNumber;
                        Next(control.round == 0 ? 5 : 7);
                    }
                    break;
                case 5:
                    if (Shopper) { Ack("Shopper unaffected by expedition transport loss"); break; }
                    if (Host)
                    {
                        if (!action)
                        {
                            if (!PhotonNetwork.IsMasterClient) { PhotonNetwork.SetMasterClient(PhotonNetwork.LocalPlayer); break; }
                            Simulate(true); action = true; nextCommand = EditorApplication.timeSinceStartup;
                        }
                        double lossAge = EditorApplication.timeSinceStartup - nextCommand;
                        if (!PhotonNetwork.InRoom) disconnected = true;
                        if (lossAge > 20) RestoreSimulation();
                        if (lossAge > 23 && disconnected && Settled)
                        {
                            Require(PhotonNetwork.CurrentRoom.Name == control.expedition, "Reconnect returned to a different room instead of the expedition");
                            if (AllPoses) Ack("Master experienced transport disconnect and automatically rejoined reserved slot");
                        }
                    }
                    else if (Ready && PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom.GetPlayer(control.faultActor)?.IsInactive == true)
                        Ack("Remaining crew assumed authority while disconnected master was inactive");
                    if (Host && AllAck) Next(6); break;
                case 6:
                    if (Shopper) { Ack("Shopper unaffected"); break; }
                    if (!Settled || PhotonNetwork.PlayerList.Count(p => !p.IsInactive) != 2 || !AllPoses) break;
                    Require(PhotonSession.RunId == control.run && PhotonSession.Get(RailRoute.StateKey, "") == control.route, "Reconnect changed run/route");
                    Require(RailCargo.Active.Count == 42, "Reconnect duplicated or lost cargo");
                    if (!VoiceReady()) break;
                    Ack("Reconnect preserved route, supplies, unique avatars and voice room");
                    if (Host && AllAck) Next(7); break;
                case 7:
                    if (Shopper) { Ack("Shopper waits for returning crew"); break; }
                    if (!Ready) break;
                    if (PhotonNetwork.IsMasterClient && PhotonSession.Phase == RailPhase.Running) session.FinishRun(control.round % 2 == 0);
                    if (PhotonSession.Phase == RailPhase.Ending || PhotonSession.Phase == RailPhase.Returning) Ack("Session finish path requested", false);
                    if (Host && AllAck) Next(8); break;
                case 8:
                    if (!Settled || PhotonSession.Kind != RailSessionRules.CompoundKind || !AllPoses) break;
                    string destination = PhotonNetwork.CurrentRoom.Name;
                    bool sourceCompound = destination == control.compound;
                    if (Shopper && PhotonNetwork.PlayerList.Length == 1)
                    {
                        var returned = ReadReceipt("main");
                        if (returned?.revision != revision || returned.room == control.compound) break;
                        Require(sourceCompound, "Shopper moved during coordinated return");
                        if (!VoiceReady()) break;
                        Ack("Shopper stayed isolated while crew used another shared compound");
                        break;
                    }
                    if (PhotonNetwork.PlayerList.Length != (sourceCompound ? 3 : 2)) break;
                    Require(!RailWorld.HasLiveCargo && Game.AI.Enemy.Active.Count == 0, "Return leaked expedition objects");
                    if (!VoiceReady()) break;
                    Teleport(new Vector3(8 + (Shopper ? 2 : 0), .4f, 4));
                    Ack(sourceCompound ? "Whole crew returned to occupied compound; clean objects and voice routing"
                        : "Whole crew reached shared alternative compound; clean objects and voice routing; fresh="
                            + PhotonSession.Get(RailSessionRules.Allocation, "").StartsWith("return-", StringComparison.Ordinal));
                    if (Host && AllAck)
                    {
                        Require(ReadReceipt("main").room == ReadReceipt("player2").room, "Return split departing crew");
                        if (++control.round < 4)
                        {
                            control.compound = destination;
                            Next(sourceCompound ? 2 : 1); // Regroup shopper explicitly before the next departure.
                        }
                        else Next(9);
                    }
                    break;
                case 9:
                    RestoreSimulation(); Log("PASS: four public departures/returns, shopper isolation, latency/loss, rapid controls, master transport loss/rejoin, unique avatars, cleanup and voice routing"); done = true; break;
            }
        }
        catch (IOException) { /* A different test process may be writing its small coordination file. Retry next tick. */ }
        catch (Exception e)
        {
            RestoreSimulation(); done = true;
            Log("FAIL " + e.Message);
            File.WriteAllText(ReceiptPath(Role), JsonUtility.ToJson(new Receipt { token = token, revision = revision, error = e.Message }));
        }
    }
}
#endif
