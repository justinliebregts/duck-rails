#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Photon.Pun;
using Unity.Multiplayer.PlayMode;
using UnityEditor;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// Opt-in two-client test: actual grab/release, physics and Photon requests.
[InitializeOnLoad]
public static class RailCoalLiveChecks
{
    const string Key = "test.coal.stage";
    static string Root { get { var p = Application.dataPath.Replace('\\', '/'); var i = p.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase); return i < 0 ? Path.GetDirectoryName(p) : p.Substring(0, i); } }
    static string ArmPath => Root + "/Temp/rails-coal-arm.txt";
    static string LogPath => Root + "/Temp/rails-coal-" + (CurrentPlayer.IsMainEditor ? "main" : "remote") + ".txt";
    static int stage = -1, coalId;
    static double entered, nextTick;
    static bool grabbed, released, ack, done;
    static float beforeFuel, beforeDistance;
    static int beforeCount;
    static RailCoalLiveChecks() => EditorApplication.update += Tick;
    public static void Arm()
    {
        File.WriteAllText(ArmPath, DateTime.UtcNow.AddMinutes(10).ToString("O"));
        File.WriteAllText(Root + "/Temp/rails-coal-main.txt", "");
        File.WriteAllText(Root + "/Temp/rails-coal-remote.txt", "");
    }
    public static void Disarm() => File.WriteAllText(ArmPath, "");
    public static void CheckAvatarLookup()
    {
        if (EditorApplication.isPlaying) throw new Exception("Run lookup checks in Edit Mode");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Photon/PhotonPlayer.prefab");
        var old = UnityEngine.Object.Instantiate(prefab).GetComponent<PhotonPlayer>();
        var current = UnityEngine.Object.Instantiate(prefab).GetComponent<PhotonPlayer>();
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var pose = typeof(PhotonPlayer).GetField("_hasPose", flags);
        var time = typeof(PhotonPlayer).GetField("_lastPoseRealtime", flags);
        try
        {
            old.photonView.OwnerActorNr = current.photonView.OwnerActorNr = 999;
            if (!PhotonPlayer.Active.Contains(old)) PhotonPlayer.Active.Add(old);
            if (!PhotonPlayer.Active.Contains(current)) PhotonPlayer.Active.Add(current);
            Require(PhotonPlayer.ForActor(999) == null, "Uninitialized prefab pose must not identify a player");
            pose.SetValue(current, true); time.SetValue(current, 20d);
            Require(PhotonPlayer.ForActor(999) == current, "Live avatar wins over earlier stale duplicate");
            pose.SetValue(old, true); time.SetValue(old, 10d);
            Require(PhotonPlayer.ForActor(999) == current, "Newest received pose wins over an older duplicate");
            time.SetValue(old, 30d);
            Require(PhotonPlayer.ForActor(999) == old, "Lookup follows the currently updating avatar");
            Debug.Log("COAL AVATAR LOOKUP: 4 checks passed");
        }
        finally
        {
            PhotonPlayer.Active.Remove(old); PhotonPlayer.Active.Remove(current);
            UnityEngine.Object.DestroyImmediate(old.gameObject); UnityEngine.Object.DestroyImmediate(current.gameObject);
        }
    }
    static void Log(string text) { File.AppendAllText(LogPath, DateTime.UtcNow.ToString("O") + " " + text + "\n"); Debug.Log("COAL LIVE: " + text); }
    static void Require(bool ok, string text) { if (!ok) throw new Exception(text); }
    static void Teleport(Vector3 point) => NewPlayer.instance.Warp(point, Quaternion.identity);
    static void Tick()
    {
        if (!EditorApplication.isPlaying || done || EditorApplication.timeSinceStartup < nextTick || !File.Exists(ArmPath)) return;
        nextTick = EditorApplication.timeSinceStartup + .1;
        if (!DateTime.TryParse(File.ReadAllText(ArmPath), out var until) || DateTime.UtcNow > until.ToUniversalTime()) return;
        if (PhotonSession.Phase == RailPhase.Compound && PhotonSession.Instance != null && PhotonSession.Instance.WorldReady && PhotonNetwork.PlayerList.Length == 2)
        {
            Teleport(RailWorld.Instance.boardingTrigger.bounds.center - Vector3.up * .8f);
            return;
        }
        var run = RailExpedition.Instance;
        if (run == null || PhotonSession.Phase != RailPhase.Running || !PhotonSession.Instance.WorldReady || PhotonNetwork.PlayerList.Length != 2) return;
        try
        {
            int current = PhotonNetwork.CurrentRoom.CustomProperties[Key] is int value ? value : 0;
            if (stage != current)
            {
                stage = current; entered = EditorApplication.timeSinceStartup; grabbed = released = ack = false; coalId = 0;
                beforeCount = run.State.consumed.Count; beforeDistance = RailTrain.Instance.Distance;
                Teleport(RailTrain.Instance.transform.TransformPoint(new Vector3(-1, .35f, 1)));
                Log("stage=" + stage + " actor=" + PhotonNetwork.LocalPlayer.ActorNumber + " master=" + PhotonNetwork.IsMasterClient);
            }
            var age = EditorApplication.timeSinceStartup - entered;
            Require(age < 45, "Timeout stage=" + stage + " coal=" + coalId);
            if (stage == 0)
            {
                if (age > 3 && !ack) { Log("Unique avatars " + RailAvatarChecks.AssertCurrentRoom()); ack = true; }
                if (PhotonNetwork.IsMasterClient && age > 4)
                {
                    PhotonSession.Instance.SetTrainMoving(false);
                    PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [Key] = 1 });
                }
                return;
            }
            if (stage == 2 || stage == 4)
            {
                if (PhotonNetwork.IsMasterClient && age > 4)
                {
                    if (stage == 2) PhotonSession.Instance.SetTrainMoving(true);
                    else { PhotonSession.Instance.SetTrainMoving(false); PhotonNetwork.SetMasterClient(PhotonNetwork.PlayerList.First(p => !p.IsMasterClient)); }
                    PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [Key] = stage + 1 });
                }
                return;
            }
            if (stage == 6)
            {
                if(!ack){Log("PASS stopped, moving, and after master handoff; exactly-once fuel/removal agreed on two clients; " + RailAvatarChecks.AssertCurrentRoom());ack=true;}
                if(PhotonNetwork.IsMasterClient && age>2) PhotonSession.Instance.Commit(new Hashtable{[RailSessionRules.Motion]=new object[]{PhotonNetwork.Time,RailTrain.Instance.Distance,0f,0f},[RailSessionRules.Fuel]=0f,[Key]=7});
                return;
            }
            if(stage==7)
            {
                if(PhotonNetwork.IsMasterClient)PhotonSession.Instance.SetTrainMoving(true);
                if(age>5){Require(PhotonSession.CurrentFuel==0 && RailTrain.Instance.Speed==0,"Empty train moved without coal");if(!ack){Log("PASS empty train rejects GO until refueled");ack=true;}if(PhotonNetwork.IsMasterClient){PhotonSession.Instance.SetTrainMoving(false);PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable{[Key]=8});}}
                return;
            }
            if(stage==9)
            {
                if(PhotonNetwork.IsMasterClient)PhotonSession.Instance.SetTrainMoving(true);
                if(age>6){Require(RailTrain.Instance.Distance>beforeDistance+5 && RailTrain.Instance.Speed>3.9f && PhotonSession.CurrentFuel>0,"Refueled train did not resume travel");Log("PASS COMPLETE physical coal stopped, moving, after handoff, empty-fuel recovery and resumed driving; four exactly-once deposits");done=true;}
                return;
            }
            if (age < 3) return;
            // A master change deliberately locks grabs until the recovery barrier
            // completes. Test the first eligible grip, not the fixed three-second
            // timer racing that barrier on a slower editor.
            if (!PhotonMasterHandoff.LocalAvailable) return;
            if (!PhotonNetwork.IsMasterClient)
            {
                if (!grabbed)
                {
                    if (coalId == 0)
                    {
                        var coal = UnityEngine.Object.FindObjectsByType<RailCoal>(FindObjectsSortMode.None).First(c => !c.GetComponent<PhotonGrabbable>().IsHeldByAnyPlayer);
                        coalId = coal.GetComponent<PhotonView>().ViewID;
                    }
                    var selected = PhotonView.Find(coalId).GetComponent<PhotonGrabbable>();
                    var hand = GR.Player.instance.rightPlayerHand;
                    Teleport(selected.transform.position + Vector3.left);
                    // Fixture placement only: put the palm inside the real network
                    // item, then exercise the same candidate selector as a grip edge.
                    // Restore its authored local pose before the next animated frame.
                    var savedPalm = hand.HoldPoint.localPosition;
                    try
                    {
                        hand.HoldPoint.position = selected.transform.position;
                        Physics.SyncTransforms();
                        Require(hand.FindGrabCandidate() == selected, "Non-master selects coal with palm inside on first grip");
                        Require(hand.TryGrab(hand.FindGrabCandidate()), "Non-master coal pickup attaches on first attempt; canGrab=" + hand.CanGrab + ", holding=" + hand.IsHolding + ", available=" + PhotonMasterHandoff.LocalAvailable + ", itemAvailable=" + selected.CanBeGrabbed);
                    }
                    finally { hand.HoldPoint.localPosition = savedPalm; }
                    Log("Interior selection pickup accepted actor=" + PhotonNetwork.LocalPlayer.ActorNumber);
                    grabbed = true;
                    PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { ["test.coal.id"] = coalId });
                }
                var view = PhotonView.Find(coalId);
                if (!released && view != null && view.IsMine && view.GetComponent<PhotonGrabbable>().IsHeld)
                {
                    // The current firebox has a ceiling and an open front.
                    // Place the held chunk outside that mouth, then throw it
                    // through the real collision geometry; no intake teleport.
                    // Aim through the middle of the revised aperture, which was
                    // enlarged downwards. The former high aim could clip the
                    // sloping lintel with a full-size, rotating chunk.
                    var releasePoint = run.furnace.position + run.furnace.forward * .72f - Vector3.up * .08f;
                    // Editor polling is 10 Hz; lead the moving mouth when
                    // placing the rig, but release against its current pose.
                    var delta = releasePoint + RailTrain.Instance.Velocity * .12f - view.transform.position;
                    if (Vector3.Distance(releasePoint,view.transform.position) > .14f)
                    {
                        var player = NewPlayer.instance;
                        var origin = player.headCollider.transform.position;
                        origin.y = player.transform.position.y;
                        player.Warp(origin + delta, Quaternion.LookRotation(-RailTrain.Instance.transform.forward));
                    }
                    else
                    {
                        beforeFuel = PhotonSession.CurrentFuel; beforeDistance = RailTrain.Instance.Distance;
                        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { ["test.coal.fuel"] = beforeFuel, ["test.coal.distance"] = beforeDistance, ["test.coal.actor"] = PhotonNetwork.LocalPlayer.ActorNumber });
                        GR.Player.instance.rightPlayerHand.Release();
                        view.GetComponent<Rigidbody>().linearVelocity = RailTrain.Instance.Velocity - run.furnace.forward * 4f + Vector3.up;
                        released = true;
                        Log("Released actual held coal at offset=" + (view.transform.position - run.furnace.position) + " owner=" + view.OwnerActorNr);
                    }
                }
            }
            if (run.State.consumed.Count == beforeCount + 1 && !ack && age > 5)
            {
                var props = PhotonNetwork.CurrentRoom.CustomProperties;
                int id = (int)props["test.coal.id"];
                if (PhotonView.Find(id) != null) return;
                float expected = (float)props["test.coal.fuel"] + RailResourceBalance.CoalValue(RailRoute.Instance.Selection.resourceCrew) - (RailTrain.Instance.Distance - (float)props["test.coal.distance"]) * RailSessionRules.FuelPerMetre;
                Require(Mathf.Abs(PhotonSession.CurrentFuel - expected) < .08f, "Fuel mismatch actual=" + PhotonSession.CurrentFuel + " expected=" + expected);
                Require(run.State.players.First(p => p.actor == (int)props["test.coal.actor"]).coal > 0, "Missing non-master credit");
                run.Request("coal", id); // Deliberate duplicate after destruction.
                PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { ["test.coal.ack"] = stage });
                Log("PASS stage=" + stage + " consumed=" + id + " fuel=" + PhotonSession.CurrentFuel + " expected=" + expected + " removed=true"); ack = true;
            }
            if (ack) Require(run.State.consumed.Count == beforeCount + 1, "Duplicate consumption");
            if (PhotonNetwork.IsMasterClient && age > 9 && PhotonNetwork.PlayerList.All(p => p.CustomProperties["test.coal.ack"] is int n && n == stage))
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [Key] = stage + 1 });
        }
        catch (Exception e) { Log("FAIL " + e.Message); done = true; }
    }
}
#endif
