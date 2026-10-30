#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Photon.Pun;
using Unity.Multiplayer.PlayMode;
using UnityEditor;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>Opt-in, three authenticated clients. Fixtures position the train/players; replication uses real Photon.</summary>
[InitializeOnLoad]
public static class RailWeek3LiveChecks
{
    static string Root { get { string p = Application.dataPath.Replace('\\', '/'); int i = p.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase); return i < 0 ? Path.GetDirectoryName(p) : p.Substring(0, i); } }
    static string ArmPath => Root + "/Temp/rails-week3-arm.txt";
    static string RoomPath => Root + "/Temp/rails-week3-room.txt";
    static string RecoveryPath => Root + "/Temp/rails-week3-recovery.txt";
    static bool Host => CurrentPlayer.IsMainEditor;
    static bool Second => CurrentPlayer.Tags.Contains("Player2");
    const string StageKey = "test.week3.stage", AckKey = "test.week3.ack";
    static string token, room, firstRun, firstRoute;
    static int initial, stage = -1;
    static bool done, action, recovery;
    static double started, entered, tick, bridgeAt;
    static Vector3 bagLocal;
    static readonly List<string> evidence = new List<string>();
    static double worldReadySince;
    static RailWeek3LiveChecks() => EditorApplication.update += Tick;
    [MenuItem("Duck Rails/Arm Three Client Week 3 Test")]
    public static void Arm()
    {
        if (EditorApplication.isPlaying) throw new Exception("Arm before Play.");
        if (!MppmStandaloneCredentials.TryGet("Player2", out _) || !MppmStandaloneCredentials.TryGet("Player3", out _)) throw new Exception("Three configured accounts required.");
        Directory.CreateDirectory(Root + "/Temp");
        File.WriteAllText(ArmPath, DateTime.UtcNow.AddMinutes(25).ToString("O")); File.WriteAllText(RoomPath, "");
        File.WriteAllText(Root + "/Temp/rails-week2-arm.txt", "");
        File.WriteAllText(RecoveryPath, "false");
        EditorPrefs.SetBool("Rails.Week3.PreviousQuickStart", RailEditorQuickStart.Enabled); RailEditorQuickStart.Enabled = false;
    }
    [MenuItem("Duck Rails/Arm Three Client Week 3 Coal Recovery Test")]
    public static void ArmRecovery() { Arm(); File.WriteAllText(RecoveryPath, "true"); }
    [MenuItem("Duck Rails/Disarm Three Client Week 3 Test")]
    public static void Disarm() { File.WriteAllText(ArmPath, ""); RailEditorQuickStart.Enabled = EditorPrefs.GetBool("Rails.Week3.PreviousQuickStart", true); }
    static void Record(string message)
    {
        evidence.Add(DateTime.UtcNow.ToString("O") + " " + message);
        File.WriteAllLines(Root + "/Temp/rails-week3-" + (Host ? "main" : Second ? "player2" : "player3") + ".txt", evidence);
        Debug.Log("WEEK 3 LIVE " + message);
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Ack()
    {
        string avatars = RailAvatarChecks.AssertCurrentRoom();
        if (!(PhotonNetwork.LocalPlayer.CustomProperties[AckKey] is int previous) || previous != stage)
            Record("Unique avatars stage=" + stage + " " + avatars);
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [AckKey] = stage });
    }
    static bool AllAck => PhotonNetwork.PlayerList.Length == 3 && PhotonNetwork.PlayerList.All(p => p.CustomProperties[AckKey] is int n && n == stage);
    static void Stage(int value) => PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [StageKey] = value });
    static void Teleport(Vector3 p) => GR.Player.instance.GetComponent<DesktopPlayer>().Teleport(p, Quaternion.identity, true);
    static void Board() => Teleport(RailTrain.Instance.transform.TransformPoint(new Vector3((PhotonNetwork.LocalPlayer.ActorNumber % 3 - 1) * 1.2f, .35f, -2)));
    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < tick || !File.Exists(ArmPath)) return;
        tick = EditorApplication.timeSinceStartup + .1;
        string arm = File.ReadAllText(ArmPath);
        if (!DateTime.TryParse(arm, out var until) || DateTime.UtcNow > until.ToUniversalTime()) return;
        if (arm != token) { token = arm; initial = 0; stage = -1; done = false; recovery = File.Exists(RecoveryPath) && File.ReadAllText(RecoveryPath) == "true"; evidence.Clear(); started = EditorApplication.timeSinceStartup; Record("Started authenticated desktop " + (recovery ? "coal recovery" : "sequence") + " checks"); }
        if (done) return;
        try
        {
            Require(EditorApplication.timeSinceStartup - started < 1000, "Overall timeout");
            var session = PhotonSession.Instance;
            if (session == null || !PhotonSession.IsRailsRoom || !session.WorldReady || session.HasRoomRequest) { worldReadySince = 0; return; }
            if (worldReadySince == 0) worldReadySince = EditorApplication.timeSinceStartup;
            if (initial == 3 && !Host && !Second && PhotonNetwork.CurrentRoom.Name != room)
            { if (EditorApplication.timeSinceStartup - entered > 5) session.RequestJoinCode(room); return; }
            if (initial == 0)
            {
                if (Host) { room = UnityEngine.Random.Range(10000, 90000).ToString(); session.RequestCreatePrivate(room); initial = 1; }
                else { room = File.ReadAllText(RoomPath).Trim(); if (room.Length == 5) { session.RequestJoinCode(room); initial = 2; } }
                return;
            }
            if (initial == 1) { if (!PhotonSession.IsPrivate || PhotonNetwork.CurrentRoom.Name != room) return; File.WriteAllText(RoomPath, room); initial = 2; }
            if (PhotonNetwork.CurrentRoom.Name != room || !PhotonSession.IsPrivate) return;
            if (initial == 2)
            {
                if (PhotonNetwork.PlayerList.Length != 3) return;
                Require(PhotonNetwork.PlayerList.Select(p => p.UserId).Distinct().Count() == 3, "Three distinct accounts");
                PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { ["test.week3.second"] = Second }); initial = 3;
                if (Host) { Stage(1); session.StartPrivateRun(); }
            }
            int current = PhotonSession.Get(StageKey, 0);
            if (current != stage) { stage = current; entered = EditorApplication.timeSinceStartup; action = false; }
            double age = EditorApplication.timeSinceStartup - entered;
            Require(age < 160, "Stage timeout " + stage + ", phase=" + PhotonSession.Phase);
            var route = RailRoute.Instance; var run = RailExpedition.Instance; var train = RailTrain.Instance;
            var bagView = PhotonView.Find(PhotonSession.Get("test.week3.bag", 0)); var bag = bagView != null ? bagView.GetComponent<BagObject>() : null;
            int second = PhotonNetwork.PlayerList.FirstOrDefault(p => p.CustomProperties["test.week3.second"] is bool yes && yes)?.ActorNumber ?? 0;
            if (stage == 1 && PhotonSession.Phase == RailPhase.Running && age > 3)
            {
                Require(GameScenes.IsLoaded(GameScenes.Expedition) && !GameScenes.IsLoaded(GameScenes.Compound), "Expedition scene replaces Compound");
                Require(route.Ready && route.Blocks.Count == 5 && route.Blocks.Select(b => b.id).SequenceEqual(route.Selection.blocks), "Exact five-block route loaded");
                // WorldReady covers cargo reconstruction, while enemy spawn messages can
                // arrive afterwards. Give each client a bounded replication settle window.
                bool seeded = RailCargo.Active.Count == 42 && Game.AI.Enemy.Active.Count == 10;
                if (!seeded && EditorApplication.timeSinceStartup - worldReadySince < 15) return;
                Require(seeded, "Expected 42 cargo and 10 enemies after settling; observed cargo=" + RailCargo.Active.Count + " enemies=" + Game.AI.Enemy.Active.Count);
                firstRun = PhotonSession.RunId; firstRoute = PhotonSession.Get(RailRoute.StateKey, "");
                if (!action) { Record("Five matching blocks/placements, 42 cargo, 10 enemies; separate Expedition scene"); Ack(); action = true; }
                if (Host && AllAck) Stage(recovery ? 20 : 2);
            }
            else if (stage == 2 && age > 1)
            {
                if (PhotonNetwork.IsMasterClient && !action)
                {
                    float distance = 0; bool curved = false;
                    foreach (var b in route.Blocks) { if (b.track.Any(p => Mathf.Abs(p.x) > 1)) { curved = true; break; } distance += b.TrackLength; }
                    Require(curved, "Selected route includes a curve for cargo test");
                    if (!session.Commit(new Hashtable { [RailSessionRules.Motion] = new object[] { PhotonNetwork.Time, distance + 10, 0f, 0f }, [RailSessionRules.Fuel] = 40f })) return;
                    bag = BagObject.Active.First(); var coal = UnityEngine.Object.FindFirstObjectByType<RailCoal>(); bag.Put(bag.FirstEmptySlot(), "RailCoal", coal.transform.position, .3f); PhotonNetwork.Destroy(coal.gameObject);
                    PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { ["test.week3.bag"] = bag.photonView.ViewID }); action = true;
                }
                if (age > 4 && bag != null && bag.Keys().Contains("RailCoal")) { Board(); bagLocal = train.transform.InverseTransformPoint(bag.transform.position); Ack(); }
                if (Host && AllAck) { session.SetTrainMoving(true); Stage(3); }
            }
            else if (stage == 3 && age > 33)
            {
                Require(bag != null && bag.Keys().Contains("RailCoal") && train.Contains(bag.transform.position), "Loaded bag retained through curve");
                Require(Vector3.Distance(bagLocal, train.transform.InverseTransformPoint(bag.transform.position)) < 1, "Cargo local pose stable through curve");
                Require(train.Contains(GR.Player.instance.transform.position + Vector3.up), "Passenger carried through curve");
                Ack(); if (Host && AllAck) { session.SetTrainMoving(false); Record("Loaded loose bag and all three passengers rode curved track"); Stage(4); }
            }
            else if (stage == 4 && age > 3)
            {
                if (Second && !action && bag.RequestControl()) { bag.HoldLocal(GR.Player.instance.rightPlayerHand); action = true; }
                if (Second && action && age > 5 && bag.State == BagState.Held) bag.DropLocal(Vector3.zero);
                if (age > 7 && bag.photonView.OwnerActorNr == second && bag.State == BagState.Dropped) { Require(bag.Keys().Contains("RailCoal"), "Remote bag takeover contents"); Ack(); }
                if (Host && AllAck) { Record("Remote grab/drop of loaded bag after bend"); Stage(5); }
            }
            else if (stage == 5)
            {
                if (PhotonNetwork.IsMasterClient && !action && age > 1)
                {
                    var enemy = Game.AI.Enemy.Active.First(e => run.State.carriers.Any(c => c.marker == e.GetComponent<RailCoalCarrier>().Marker && c.carrying));
                    enemy.ReceiveTrustedHit(new Vector3(4, 0, 2), enemy.Root.position); action = true;
                }
                if (run.State.carriers.Any(c => c.drops.Count > 0) && RailCargo.Active.Count == 42) Ack();
                if (Host && AllAck) { Record("Accepted enemy hit dropped one replicated coal chunk"); Stage(6); }
            }
            else if (stage == 6)
            {
                if (PhotonNetwork.IsMasterClient && !action) action = session.Commit(new Hashtable { [RailSessionRules.Motion] = new object[] { PhotonNetwork.Time, route.FinaleStart + 55, 0f, 0f }, [RailSessionRules.Fuel] = 10f });
                if (age > 2) { Board(); if (Second && run.State.bridgeAt <= 0) { Teleport(run.bridgeButton.position + Vector3.back); run.Request("bridge"); } }
                if (run.State.bridgeAt > 0) { bridgeAt = run.State.bridgeAt; Board(); Ack(); }
                if (Host && AllAck) Stage(7);
            }
            else if (stage == 7)
            {
                if (Host && age > 2 && !action) { PhotonNetwork.SetMasterClient(PhotonNetwork.CurrentRoom.GetPlayer(second)); action = true; }
                if (!Host && !Second && age > 4 && !action) { action = true; session.LeaveCurrentRoom(); }
                if (Host && age > 6 && PhotonNetwork.PlayerList.Length == 2) { Record("Master handoff and third-client departure during bridge lowering"); Stage(8); }
            }
            else if (stage == 8 && age > 3 && PhotonNetwork.PlayerList.Length == 3)
            {
                // Room membership is published before the returning client has
                // loaded Expedition and spawned its avatar. Wait for its pose;
                // the existing stage timeout still catches a missing spawn.
                if (PhotonNetwork.PlayerList.Any(p => PhotonPlayer.ForActor(p.ActorNumber) == null)) return;
                Require(firstRoute == PhotonSession.Get(RailRoute.StateKey, "") && route.Ready, "Late re-entry preserves exact route");
                Require(run.State.bridgeAt == bridgeAt && run.State.BridgeProgress(PhotonNetwork.Time) > .1f, "Bridge clock survives handoff/re-entry");
                Require(bag != null && bag.Keys().Contains("RailCoal") && RailCargo.Active.Count == 42, "Re-entry retains cargo and bag contents");
                Board(); Ack(); if (Host && AllAck) { Record("Route, bridge clock and loaded cargo reconstructed by late rejoiner"); Stage(9); }
            }
            else if (stage == 9 && run.State.BridgeProgress(PhotonNetwork.Time) >= 1)
            { if (!action) { if (!Host && !Second) Teleport(train.transform.position + Vector3.left * 12); else Board(); Ack(); action = true; } if (Host && AllAck) Stage(10); }
            else if (stage == 10)
            {
                if (Second && age > 1 && !run.State.finished) session.SetTrainMoving(true);
                if (run.State.finished) { Require(run.State.success && run.State.players.Count(p => p.escaped) == 2, "Two finishers, one non-finisher"); Ack(); if (Host && AllAck) { Record("Bridge finish and shared automatic ride: two escaped, one caught up without credit"); Stage(11); } }
            }
            else if (stage == 11 && PhotonSession.Phase == RailPhase.Staging)
            {
                Require(GameScenes.IsLoaded(GameScenes.Compound) && !GameScenes.IsLoaded(GameScenes.Expedition) && !RailWorld.HasLiveCargo && Game.AI.Enemy.Active.Count == 0, "Clean private return to Compound");
                Ack(); if (Host && AllAck) { Record("Private code retained; scene return removed all old cargo/enemies"); Stage(12); }
            }
            else if (stage == 12 && PhotonSession.Phase == RailPhase.Staging) { if (PhotonNetwork.IsMasterClient && age > 1) session.StartPrivateRun(); }
            else if (stage == 12 && PhotonSession.Phase == RailPhase.Running && age > 3)
            {
                Require(PhotonSession.RunId != firstRun && route.Selection.run == PhotonSession.RunId && RailCargo.Active.Count == 42, "Replay has new route snapshot and exactly fresh supplies");
                Board(); Ack(); if (Host && AllAck) { Record("Replay loaded fresh five-block route and supplies"); Stage(13); }
            }
            else if (stage == 13 && PhotonSession.Phase == RailPhase.Running)
            { if (PhotonNetwork.IsMasterClient && age > 1) session.Commit(new Hashtable { [RailSessionRules.Motion] = new object[] { PhotonNetwork.Time, route.FinaleStart + 80, 0f, 0f }, [RailSessionRules.Fuel] = 10f }); }
            else if (stage == 13 && PhotonSession.Phase == RailPhase.Ending)
            { Require(run.State.finished && !run.State.success && run.State.reason == "Bridge collision", "Raised bridge crash fails replay"); Ack(); if (Host && AllAck) Stage(14); }
            else if (stage == 14 && PhotonSession.Phase == RailPhase.Staging)
            { Require(!RailWorld.HasLiveCargo && Game.AI.Enemy.Active.Count == 0, "Failure return cleanup"); Record("Unique avatars final " + RailAvatarChecks.AssertCurrentRoom()); Record("PASS: route, curve, shared bag, coal carrier, authority handoff, rejoin, success, replay, crash and private cleanup"); done = true; }
            else if (stage == 20)
            {
                // All crew look away. Empty the authored supplies and hit every
                // initially equipped carrier so recovery, not untouched loot, is required.
                GR.Player.instance.GetComponent<DesktopPlayer>().Teleport(train.transform.position + Vector3.back * 2 + Vector3.up * .35f, Quaternion.Euler(0, 180, 0), true);
                if (PhotonNetwork.IsMasterClient && !action)
                {
                    if (!session.Commit(new Hashtable { [RailSessionRules.Fuel] = 0f, [RailSessionRules.Motion] = new object[] { PhotonNetwork.Time, 0f, 0f, 0f } })) return;
                    foreach (var enemy in Game.AI.Enemy.Active) enemy.ReceiveTrustedHit(new Vector3(4, 0, 2), enemy.Root.position);
                    action = true;
                }
                if (age > 3 && !run.State.carriers.Any(c => c.carrying)) { Ack(); if (Host && AllAck) Stage(21); }
            }
            else if (stage == 21)
            {
                if (PhotonNetwork.IsMasterClient && !action && age > 1)
                { foreach (var coal in UnityEngine.Object.FindObjectsByType<RailCoal>(FindObjectsSortMode.None)) PhotonNetwork.Destroy(coal.gameObject); action = true; }
                if (age > 3 && !UnityEngine.Object.FindFirstObjectByType<RailCoal>()) { Require(PhotonSession.CurrentFuel == 0, "Fixture has no usable fuel"); Ack(); }
                if (Host && AllAck) { Record("Empty-fuel fixture: all coal removed and all carriers spent"); Stage(22); }
            }
            else if (stage == 22)
            {
                if (age < 10) Require(!run.State.carriers.Any(c => c.generation > 0), "Recovery respects cooldown");
                if (run.State.carriers.Any(c => c.generation > 0 && c.carrying)) { Require(age >= 10, "Recovery not immediate"); Ack(); }
                if (Host && AllAck) { Record("Cooldown re-equipped an existing out-of-view carrier with zero fuel and no coal"); Stage(23); }
            }
            else if (stage == 23)
            {
                if (PhotonNetwork.IsMasterClient && !action)
                {
                    var enemy = Game.AI.Enemy.Active.First(e => run.State.carriers.Any(c => c.marker == e.GetComponent<RailCoalCarrier>().Marker && c.carrying));
                    enemy.ReceiveTrustedHit(new Vector3(4, 0, 2), enemy.Root.position); action = true;
                }
                if (UnityEngine.Object.FindObjectsByType<RailCoal>(FindObjectsSortMode.None).Length == 1) { Ack(); if (Host && AllAck) Stage(24); }
            }
            else if (stage == 24)
            {
                if (PhotonNetwork.IsMasterClient && !action && age > 1)
                {
                    bag = BagObject.Active.First(b => b.photonView.IsMine); var coal = UnityEngine.Object.FindFirstObjectByType<RailCoal>();
                    bag.Put(bag.FirstEmptySlot(), "RailCoal", coal.transform.position, .3f); PhotonNetwork.Destroy(coal.gameObject);
                    PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { ["test.week3.bag"] = bag.photonView.ViewID }); action = true;
                }
                if (age > 5 && bag != null && bag.Keys().Contains("RailCoal")) { Require(!UnityEngine.Object.FindFirstObjectByType<RailCoal>(), "Stored carrier coal must not duplicate"); Ack(); }
                if (Host && AllAck) { Record("Recovered coal stored in bag without respawning a duplicate"); Stage(25); }
            }
            else if (stage == 25)
            {
                if (Host && age > 1 && !action) { var other = PhotonNetwork.PlayerList.First(p => p != PhotonNetwork.MasterClient); PhotonNetwork.SetMasterClient(other); action = true; }
                if (age > 8) { Require(bag != null && bag.Keys().Contains("RailCoal") && !UnityEngine.Object.FindFirstObjectByType<RailCoal>(), "Issuance receipts survive handoff and bag storage"); Ack(); }
                if (Host && AllAck) Stage(26);
            }
            else if (stage == 26)
            {
                if (PhotonNetwork.IsMasterClient && !action && age > 1)
                {
                    if (!bag.photonView.IsMine) { bag.RequestControl(); return; }
                    string key = bag.Take(bag.Slots.First(s => !s.IsEmpty));
                    var coal = PhotonNetwork.InstantiateRoomObject(key, run.furnace.position, Quaternion.identity);
                    PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { ["test.week3.coal"] = coal.GetComponent<PhotonView>().ViewID }); action = true;
                }
                Board(); int id = PhotonSession.Get("test.week3.coal", 0);
                if (PhotonNetwork.IsMasterClient && id != 0 && PhotonView.Find(id) != null) run.Request("coal", id);
                if (PhotonSession.CurrentFuel == 2.5f && !UnityEngine.Object.FindFirstObjectByType<RailCoal>()) { Ack(); }
                if (Host && AllAck) Stage(27);
            }
            else if (stage == 27)
            {
                if (Host && !action) { session.SetTrainMoving(true); action = true; }
                if (age > 4 && train.Distance > 5 && train.Speed > 0) { Record("PASS: empty fuel, delayed carrier replenishment, shared hit drop, bag storage, host handoff, exactly-once refuel and resumed driving"); done = true; }
            }
        }
        catch (Exception error) { Record("FAIL stage " + stage + ": " + error); done = true; }
    }
}
#endif
