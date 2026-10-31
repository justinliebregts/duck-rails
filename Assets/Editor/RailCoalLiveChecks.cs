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
                beforeCount = run.State.consumed.Count;
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
                Log("Unique avatars after handoff " + RailAvatarChecks.AssertCurrentRoom());
                if (PhotonNetwork.IsMasterClient) PhotonSession.Instance.SetTrainMoving(false);
                Log("PASS stopped, moving, and after master handoff; exactly-once fuel/removal agreed on two clients"); done = true; return;
            }
            if (age < 3) return;
            if (!PhotonNetwork.IsMasterClient)
            {
                if (!grabbed)
                {
                    var coal = UnityEngine.Object.FindObjectsByType<RailCoal>(FindObjectsSortMode.None).First(c => !c.GetComponent<PhotonGrabbable>().IsHeldByAnyPlayer);
                    coalId = coal.GetComponent<PhotonView>().ViewID;
                    Teleport(coal.transform.position + Vector3.left);
                    GR.Player.instance.rightPlayerHand.ForceGrab(coal.GetComponent<PhotonGrabbable>());
                    grabbed = true;
                    PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { ["test.coal.id"] = coalId });
                }
                var view = PhotonView.Find(coalId);
                if (!released && view != null && view.IsMine && view.GetComponent<PhotonGrabbable>().IsHeld)
                {
                    var target = run.furnace.position + Vector3.up * .35f;
                    var delta = target - view.transform.position;
                    if (delta.magnitude > .06f) Teleport(GR.Player.instance.transform.position + delta);
                    else
                    {
                        beforeFuel = PhotonSession.CurrentFuel; beforeDistance = RailTrain.Instance.Distance;
                        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { ["test.coal.fuel"] = beforeFuel, ["test.coal.distance"] = beforeDistance, ["test.coal.actor"] = PhotonNetwork.LocalPlayer.ActorNumber });
                        GR.Player.instance.rightPlayerHand.Release(); released = true;
                        Log("Released actual held coal at offset=" + (view.transform.position - run.furnace.position) + " owner=" + view.OwnerActorNr);
                    }
                }
            }
            if (run.State.consumed.Count == beforeCount + 1 && !ack && age > 5)
            {
                var props = PhotonNetwork.CurrentRoom.CustomProperties;
                int id = (int)props["test.coal.id"];
                if (PhotonView.Find(id) != null) return;
                float expected = (float)props["test.coal.fuel"] + 2.5f - (RailTrain.Instance.Distance - (float)props["test.coal.distance"]) * .05f;
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
