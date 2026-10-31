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

/// <summary>Opt-in integration test across three real authenticated editor processes.</summary>
[InitializeOnLoad]
public static class RailWeek2LiveChecks
{
    const string StageKey = "test.week2.stage", AckKey = "test.week2.ack";
    static string Root
    {
        get { string p = Application.dataPath.Replace('\\', '/'); int i = p.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase); return i < 0 ? Path.GetDirectoryName(p) : p.Substring(0, i); }
    }
    static string ArmPath => Path.Combine(Root, "Temp/rails-week2-arm.txt");
    static string RoomPath => Path.Combine(Root, "Temp/rails-week2-room.txt");
    static string RecoveryPath => Path.Combine(Root, "Temp/rails-week2-recovery.txt");
    static bool Host => CurrentPlayer.IsMainEditor;
    static bool Second => CurrentPlayer.Tags.Contains("Player2");
    static string token, room;
    static int localStage = -1, initial, coalId;
    static double entered, started, tick;
    static bool done, action;
    static float fuelBefore;
    static bool recovery;
    static Vector3 bagLocal;
    static double bridgeStart;
    static readonly List<string> evidence = new List<string>();
    static RailWeek2LiveChecks() => EditorApplication.update += Tick;
    [MenuItem("Duck Rails/Arm Three Client Week 2 Test")]
    public static void Arm()
    {
        if (EditorApplication.isPlaying) throw new Exception("Arm before Play.");
        if (!MppmStandaloneCredentials.TryGet("Player2", out _) || !MppmStandaloneCredentials.TryGet("Player3", out _)) throw new Exception("Three accounts required.");
        Directory.CreateDirectory(Path.Combine(Root, "Temp"));
        File.WriteAllText(ArmPath, DateTime.UtcNow.AddMinutes(25).ToString("O")); File.WriteAllText(RoomPath, "");
        RailEditorQuickStart.Enabled = false;
        File.WriteAllText(RecoveryPath, "false");
        Debug.Log("Three-client Week 2 test armed. Launch the three-client scenario.");
    }
    [MenuItem("Duck Rails/Arm Three Client Week 2 Recovery Test")]
    public static void ArmRecovery() { Arm(); File.WriteAllText(RecoveryPath, "true"); }
    [MenuItem("Duck Rails/Disarm Three Client Week 2 Test")]
    public static void Disarm() { File.WriteAllText(ArmPath, ""); RailEditorQuickStart.Enabled = true; }
    static void Record(string value)
    {
        evidence.Add(DateTime.UtcNow.ToString("O") + " " + value);
        File.WriteAllLines(Path.Combine(Root, "Temp/rails-week2-" + (Host ? "main" : Second ? "player2" : "player3") + ".txt"), evidence);
        Debug.Log("WEEK 2 LIVE: " + value);
    }
    static void Require(bool pass, string message) { if (!pass) throw new Exception(message); }
    static void Ack() => PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [AckKey] = localStage });
    static bool AllAck => PhotonNetwork.PlayerList.Length == 3 && PhotonNetwork.PlayerList.All(p => p.CustomProperties[AckKey] is int n && n == localStage);
    static void Stage(int next) { PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [StageKey] = next }); }
    static void Teleport(Vector3 p) => NewPlayer.instance.Warp(p, Quaternion.identity);
    static void Board() => Teleport(RailTrain.Instance.transform.TransformPoint(new Vector3((PhotonNetwork.LocalPlayer.ActorNumber % 3 - 1) * 1.2f, .35f, -2)));
    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < tick || !File.Exists(ArmPath)) return;
        tick = EditorApplication.timeSinceStartup + .1;
        string arm = File.ReadAllText(ArmPath);
        if (!DateTime.TryParse(arm, out var until) || DateTime.UtcNow > until.ToUniversalTime()) return;
        if (token != arm) { token = arm; done = false; initial = 0; localStage = -1; room = null; evidence.Clear(); recovery = File.Exists(RecoveryPath) && File.ReadAllText(RecoveryPath) == "true"; started = EditorApplication.timeSinceStartup; Record("Started " + (Host ? "main" : Second ? "Player2" : "Player3") + (recovery ? " recovery checks" : "")); }
        if (done) return;
        try
        {
            Require(EditorApplication.timeSinceStartup - started < 1000, "Overall test timeout");
            var session = PhotonSession.Instance;
            if (session == null || !PhotonSession.IsRailsRoom || !session.WorldReady || session.HasRoomRequest) return;
            if (recovery && initial == 3 && !Host && !Second && PhotonNetwork.CurrentRoom.Name != room)
            { if (EditorApplication.timeSinceStartup - entered > 9) session.RequestJoinCode(room); return; }
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
                initial = 3; Record("Three authenticated clients joined private test room");
                PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { ["test.week2.second"] = Second, ["test.week2.host"] = Host });
                if (Host) { Stage(recovery ? 101 : 1); session.StartPrivateRun(); }
            }
            int stage = PhotonSession.Get(StageKey, 0);
            if (stage != localStage) { localStage = stage; entered = EditorApplication.timeSinceStartup; action = false; }
            double age = EditorApplication.timeSinceStartup - entered;
            Require(age < 150, "Stage timeout " + stage + " phase=" + PhotonSession.Phase);
            var run = RailExpedition.Instance;
            if (run == null) return;
            if (stage >= 101) { RecoveryStage(stage, age, run, session); return; }
            if (stage == 1 && PhotonSession.Phase == RailPhase.Running)
            {
                if (run.State.players.Count != 3 || RailCargo.Active.Count != 20) return;
                Require(Mathf.Abs(PhotonSession.CurrentFuel - 10) < .01f, "Ten starting fuel");
                Require(BagObject.Active.Count == 2 && Game.AI.Enemy.Active.Count == 2, "Two bags and two depot enemies");
                if (!action) { Board(); Record("20 cargo, 12 supplies/8 coal, two enemies, 10 fuel"); Ack(); action = true; }
                if (Host && AllAck) { session.SetTrainMoving(true); Stage(2); }
            }
            else if (stage == 2 && age > 7)
            {
                Require(RailTrain.Instance.transform.position.z > 90, "Actual train movement");
                Require(RailTrain.Instance.Contains(GR.Player.instance.transform.position + Vector3.up), "Passenger remains aboard");
                if (!action) { Ack(); action = true; Record("Passenger and supplies rode accelerating train"); }
                if (Host && AllAck) { session.SetTrainMoving(false); Stage(3); }
            }
            else if (stage == 3 && age > 3)
            {
                if (Second)
                {
                    if (!action)
                    {
                        fuelBefore = PhotonSession.CurrentFuel;
                        var coal = UnityEngine.Object.FindFirstObjectByType<RailCoal>(); coalId = coal.GetComponent<PhotonView>().ViewID;
                        Teleport(coal.transform.position + Vector3.left); GR.Player.instance.rightPlayerHand.ForceGrab(coal.GetComponent<PhotonGrabbable>()); action = true;
                    }
                    var view = PhotonView.Find(coalId);
                    if (view != null && view.IsMine && view.GetComponent<PhotonGrabbable>().IsHeld && age > 5)
                    {
                        GR.Player.instance.rightPlayerHand.Release();
                        Board(); var body = view.GetComponent<Rigidbody>(); body.position = run.furnace.position; view.transform.position = body.position; body.linearVelocity = Vector3.zero;
                    }
                    if (run.State.consumed.Contains(coalId))
                    { Require(Mathf.Abs(PhotonSession.CurrentFuel - fuelBefore - 2.5f) < .05f, "Coal adds exactly 2.5 fuel"); run.Request("coal", coalId); Ack(); }
                }
                else if (run.State.consumed.Count == 1) Ack();
                if (Host && AllAck) { Record("Remote-owner coal grab/release/insertion replicated"); Stage(4); }
            }
            else if (stage == 4 && age > 2)
            {
                Require(run.State.consumed.Count == 1 && RailCargo.Active.Count == 19, "Duplicate coal request did not add/retain a chunk");
                if (Host && !action) { Teleport(new Vector3(4, .1f, 120)); action = true; }
                if (Host && age > 3 && !RailExpedition.LocalDown) RailExpedition.RequestDown();
                if (run.State.players.Any(p => p.down)) { Ack(); if (!Host) Board(); }
                if (Host && AllAck) { Record("Down state replicated without death/respawn"); Stage(5); }
            }
            else if (stage == 5)
            {
                if (Second && !action) { Teleport(new Vector3(4.5f, .1f, 120)); action = true; }
                if (run.State.players.Any(p => p.reviveAt > 0) && Host) Stage(6);
            }
            else if (stage == 6)
            {
                if (Second && age > .6 && !action) { Board(); action = true; }
                if (Host && age > 2)
                { Require(run.State.players.Any(p => p.down) && run.State.players.All(p => p.reviveAt == 0), "Interrupted revival resets"); Record("Revive interruption reset across clients"); Stage(7); }
            }
            else if (stage == 7)
            {
                if (Second && !action) { Teleport(new Vector3(4.5f, .1f, 120)); action = true; }
                if (Host && age > 4 && run.State.players.All(p => !p.down))
                { Require(run.State.players.Sum(p => p.revives) == 1, "One rescue credited"); Record("Three-second rescue completed once"); Stage(8); }
            }
            else if (stage == 8)
            {
                if (!action) { Board(); Ack(); action = true; }
                if (Host && AllAck && age > 1)
                {
                    session.Commit(new Hashtable { [RailSessionRules.Motion] = new object[] { PhotonNetwork.Time, 455f, 0f, 0f }, [RailSessionRules.Fuel] = 10f }); Stage(9);
                }
            }
            else if (stage == 9 && age > 2)
            {
                if (Second && !action) { Teleport(run.bridgeButton.position + Vector3.back); action = true; }
                if (Second && age > 3) run.Request("bridge");
                if (run.State.bridgeAt > 0) { Ack(); if (!action) action = true; }
                if (Host && AllAck) { Record("Remote bridge button started shared timer"); Stage(10); }
            }
            else if (stage == 10)
            {
                if (Second && !action) { Board(); action = true; }
                if (age > 3) { Require(Game.AI.Enemy.Active.Count == 4, "Defense adds two enemies"); }
                if (run.State.BridgeProgress(PhotonNetwork.Time) >= 1) { Ack(); if (Host && AllAck) { Record("Full 60-second bridge lowering completed"); Stage(11); } }
            }
            else if (stage == 11)
            {
                if (!action) { if (Host) Board(); else Teleport(new Vector3(-8, .1f, 520)); Ack(); action = true; }
                if (Host && AllAck && age > 2) session.SetTrainMoving(true);
                if (run.State.finished)
                {
                    Require(run.State.success && run.State.players.Count(p => p.escaped) == 1, "One finisher makes success; two do not finish");
                    Require(run.results.gameObject.activeSelf && run.results.text.Contains("SUCCESS") && run.results.text.Contains("Didn't make it"), "Shared mixed results board");
                    Record("One-player success and two non-finishers locked before catch-up"); Ack();
                    if (Host && AllAck) Stage(12);
                }
            }
            else if (stage == 12)
            {
                if (PhotonSession.Phase == RailPhase.Ending)
                { Require(run.State.players.Count(p => p.escaped) == 1, "Catch-up cannot grant escape credit"); }
                if (PhotonSession.Phase == RailPhase.Staging)
                {
                    Require(!RailWorld.HasLiveCargo, "Old cargo cleaned before replay");
                    if (!action) { Record("Success returned crew together, private code retained, cargo cleaned"); Ack(); action = true; }
                    if (Host && AllAck) { session.StartPrivateRun(); Stage(13); }
                }
            }
            else if (stage == 13)
            {
                if (PhotonSession.Phase == RailPhase.Running && !action) { Teleport(new Vector3(-10 - PhotonNetwork.LocalPlayer.ActorNumber * 3, .1f, 100)); action = true; }
                if (PhotonSession.Phase == RailPhase.Running && age > 3 && !RailExpedition.LocalDown) RailExpedition.RequestDown();
                if (run.State.finished)
                { Require(!run.State.success && run.State.reason == "Crew down" && run.results.text.Contains("FAIL"), "Full-crew down failure/results"); Ack(); if (Host && AllAck) { Record("All-down failure and full results verified"); Stage(14); } }
            }
            else if (stage == 14 && PhotonSession.Phase == RailPhase.Staging)
            { if (!action) { Ack(); action = true; } if (Host && AllAck) { session.StartPrivateRun(); Stage(15); } }
            else if (stage == 15 && PhotonSession.Phase == RailPhase.Running)
            {
                if (!action) { Board(); Ack(); action = true; }
                if (Host && AllAck && age > 2)
                { session.Commit(new Hashtable { [RailSessionRules.Motion] = new object[] { PhotonNetwork.Time, 468f, 4f, 4f }, [RailSessionRules.Fuel] = 10f }); Stage(16); }
            }
            else if (stage == 16 && run.State.finished)
            { Require(!run.State.success && run.State.reason == "Bridge collision" && run.State.players.All(p => !p.escaped), "Raised-bridge crash fails everyone"); if (!action) { Record("Bridge crash failed team; shared FAIL results displayed"); Ack(); action = true; } if (Host && AllAck) Stage(17); }
            else if (stage == 17 && PhotonSession.Phase == RailPhase.Staging)
            { Require(!RailWorld.HasLiveCargo, "Failure cargo cleanup"); Record("PASS: three clients, success + crew-down + crash, two replays"); done = true; }
        }
        catch (Exception ex) { Record("FAIL: " + ex.Message); done = true; }
    }

    static void RecoveryStage(int stage, double age, RailExpedition run, PhotonSession session)
    {
        var train = RailTrain.Instance;
        var bag = BagObject.Active.FirstOrDefault(b => b.photonView.ViewID == PhotonSession.Get("test.week2.bag", 0));
        int second = PhotonNetwork.PlayerList.FirstOrDefault(p => p.CustomProperties["test.week2.second"] is bool yes && yes)?.ActorNumber ?? 0;
        if (stage == 101 && PhotonSession.Phase == RailPhase.Running && age > 2)
        {
            if (Host && !action)
            {
                bag = BagObject.Active.First(); var coal = UnityEngine.Object.FindFirstObjectByType<RailCoal>();
                Require(BagObject.ResolvePrefab("RailCoal") != null, "Coal can be restored from bag");
                bag.Put(bag.FirstEmptySlot(), "RailCoal", coal.transform.position, .3f); PhotonNetwork.Destroy(coal.gameObject);
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { ["test.week2.bag"] = bag.photonView.ViewID }); action = true;
            }
            if (bag != null && bag.Keys().Contains("RailCoal") && RailCargo.Active.Count == 19 && age > 4)
            { Board(); bagLocal = train.transform.InverseTransformPoint(bag.transform.position); Ack(); }
            if (Host && AllAck) { Record("Coal stored in shared bag and replicated on all clients"); session.SetTrainMoving(true); Stage(102); }
        }
        else if (stage == 102 && age > 7)
        {
            Require(bag != null && bag.Keys().Contains("RailCoal"), "Loaded bag contents survived movement");
            Require(Vector3.Distance(bagLocal, train.transform.InverseTransformPoint(bag.transform.position)) < 1, "Loaded loose bag stayed in carriage frame");
            if (!action) { Record("Loaded loose bag rode accelerating train"); Ack(); action = true; }
            if (Host && AllAck) { session.SetTrainMoving(false); Stage(103); }
        }
        else if (stage == 103 && age > 3)
        {
            if (Second && !action && bag.RequestControl()) { bag.HoldLocal(GR.Player.instance.rightPlayerHand); action = true; }
            if (Second && action && age > 5 && bag.State == BagState.Held) bag.DropLocal(Vector3.zero);
            if (age > 7 && bag.photonView.OwnerActorNr == second && bag.State == BagState.Dropped)
            { Require(bag.Keys().Contains("RailCoal") && train.Contains(bag.transform.position), "Ownership handoff retains loaded bag aboard"); Ack(); }
            if (Host && AllAck) { Record("Remote player took, held and dropped loaded bag without losing contents"); session.SetTrainMoving(true); Stage(104); }
        }
        else if (stage == 104)
        {
            if (Host && age > 5) session.SetTrainMoving(false);
            if (age > 8)
            {
                Require(bag.Keys().Contains("RailCoal") && train.Contains(bag.transform.position), "Remote-owned loaded bag survived acceleration/braking"); Ack();
                if (Host && AllAck) Stage(105);
            }
        }
        else if (stage == 105)
        {
            if (Host && !action)
            {
                var bat = RailCargo.Active.First(c => c.name.StartsWith("RailBat")); var body = bat.GetComponent<Rigidbody>();
                body.position = train.transform.TransformPoint(new Vector3(-1, .8f, 0)); body.linearVelocity = new Vector3(-10, 4, 0);
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { ["test.week2.throw"] = bat.GetComponent<PhotonView>().ViewID }); action = true;
            }
            if (age > 3)
            {
                var thrown = PhotonView.Find(PhotonSession.Get("test.week2.throw", 0)); Require(thrown != null && !train.Contains(thrown.transform.position), "Deliberate thrown bat leaves carriage on every client"); Ack();
                if (Host && AllAck) { Record("Repeated braking, remote bag ownership and deliberate overboard throw agreed"); Stage(106); }
            }
        }
        else if (stage == 106)
        {
            if (Host && !action) { if (session.Commit(new Hashtable { [RailSessionRules.Motion] = new object[] { PhotonNetwork.Time, 455f, 0f, 0f }, [RailSessionRules.Fuel] = 10f })) action = true; }
            if (Second && age > 2 && run.State.bridgeAt <= 0) { Teleport(run.bridgeButton.position + Vector3.back); run.Request("bridge"); }
            if (run.State.bridgeAt > 0) { bridgeStart = run.State.bridgeAt; Board(); Ack(); }
            if (Host && AllAck) Stage(107);
        }
        else if (stage == 107)
        {
            if (Host && age > 2 && !action) { PhotonNetwork.SetMasterClient(PhotonNetwork.CurrentRoom.GetPlayer(second)); action = true; }
            if (!Host && !Second && age > 4 && !action) { action = true; session.LeaveCurrentRoom(); }
            if (Host && age > 6 && PhotonNetwork.PlayerList.Length == 2) { Record("Authority handed to second client; third client left during bridge lowering"); Stage(108); }
        }
        else if (stage == 108 && age > 3 && PhotonNetwork.PlayerList.Length == 3)
        {
            Require(PhotonNetwork.MasterClient.ActorNumber == second, "Successor remains authority");
            Require(run.State.bridgeAt == bridgeStart && run.State.BridgeProgress(PhotonNetwork.Time) > .15f, "Bridge timer survived master change and re-entry");
            Require(RailCargo.Active.Count == 19 && bag != null && bag.Keys().Contains("RailCoal"), "Re-entry restores shared supplies and bag contents without duplication");
            if (!action) { Board(); Record("Late re-entry restored bridge clock, loaded bag and 19 cargo under successor master"); Ack(); action = true; }
            if (Host && AllAck) Stage(109);
        }
        else if (stage == 109 && run.State.BridgeProgress(PhotonNetwork.Time) >= 1)
        { Ack(); if (Host && AllAck) Stage(110); }
        else if (stage == 110)
        {
            if (!action) { if (Second) Board(); else Teleport(new Vector3(Host ? -8 : -12, .1f, 520)); action = true; }
            if (!Second && age > 3 && !RailExpedition.LocalDown) RailExpedition.RequestDown();
            if (!Second && RailExpedition.LocalDown)
            { if (Host) Teleport(train.transform.TransformPoint(new Vector3(-1.7f, .35f, -3))); Ack(); }
            if (Second) Ack();
            if (Host && AllAck) Stage(111);
        }
        else if (stage == 111)
        {
            if (Second && age > 1) session.SetTrainMoving(true);
            if (run.State.finished)
            {
                Require(run.State.success && run.State.players.Count(p => p.escaped) == 2, "Standing and downed passengers both escape");
                Require(run.State.players.Count(p => p.down && p.escaped) == 1 && run.State.players.Count(p => p.down && !p.escaped) == 1, "Downed left-behind player receives no credit and catch-up does not revive");
                if (!action) { Record("Standing/downed passengers escaped; downed non-finisher caught up without revival or credit"); Ack(); action = true; }
                if (Host && AllAck) Stage(112);
            }
        }
        else if (stage == 112)
        {
            if (Second && !action) { Teleport(train.transform.position + Vector3.right * 10); action = true; }
            if (age > 4)
            {
                Require(train.Contains(GR.Player.instance.transform.position + Vector3.up), "Victory fall/catch-up recovery returns aboard");
                Require(run.State.players.Count(p => p.escaped) == 2, "Victory recovery cannot change credit"); Ack();
                if (Host && AllAck) { Record("Victory fall returned player aboard and preserved results"); Stage(113); }
            }
        }
        else if (stage == 113 && PhotonSession.Phase == RailPhase.Staging)
        { Require(!RailWorld.HasLiveCargo, "Loaded bag cleanup on return"); Record("PASS: loaded bags, ownership, braking/throw, master change, re-entry, downed finish, victory recovery"); done = true; }
    }
}
#endif
