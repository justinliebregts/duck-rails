using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ExitGames.Client.Photon;
using Game.AI;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Disposable batch project only. Synthetic room tests; no Photon connection is opened.</summary>
public static class MasterHandoffChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;
    sealed class Actor : Player { public Actor(int id, bool local = false) : base("handoff check", id, local) { } }
    static FieldInfo Field(object o, string name)
    {
        for (Type type = o.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, Private | BindingFlags.Public);
            if (field != null) return field;
        }
        throw new Exception("Missing field " + name);
    }
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
    static T Get<T>(object o, string name) => (T)Field(o, name).GetValue(o);
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Private).Invoke(o, args);
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); Debug.Log("PASS: " + message); }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Disposable batch project only");
        var client = PhotonNetwork.NetworkingClient;
        var previousRoom = client.CurrentRoom;
        var previousLocal = client.LocalPlayer;
        var previousState = client.State;
        var previousLevel = client.LoadBalancingPeer.DebugOut;
        try
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = PhotonSession.PlayScene;
            var local = new Actor(2, true);
            var master = new Actor(1);
            var backup = new Actor(3);
            var room = new Room("synthetic handoff", new RoomOptions());
            room.LoadBalancingClient = client;
            room.AddPlayer(master); room.AddPlayer(local); room.AddPlayer(backup);
            Set(room, "masterClientId", master.ActorNumber);
            client.CurrentRoom = room;
            typeof(LoadBalancingClient).GetProperty("LocalPlayer").SetValue(client, local);
            client.State = ClientState.Joined;
            // Requests deliberately cannot reach a server. Assert attempts using the retry
            // deadline, and verify local authority never changes on a failed/queued request.
            client.LoadBalancingPeer.DebugOut = DebugLevel.OFF;
            var human = new GameObject("Ready local avatar").AddComponent<PhotonPlayer>();
            if (human.photonView == null) human.gameObject.AddComponent<PhotonView>();
            typeof(PhotonView).GetProperty("IsMine").SetValue(human.photonView, true);
            PhotonPlayer.Active.Add(human);
            var handoff = new GameObject("Handoff coordinator").AddComponent<PhotonMasterHandoff>();
            Set(handoff, "focused", true); Set(handoff, "room", room);
            Call(handoff, "ResetObservation");
            Set(handoff, "lastReady", true); Set(handoff, "nextHeartbeat", double.PositiveInfinity);
            Set(handoff, "observeUntil", Time.realtimeSinceStartupAsDouble + 5);
            Heartbeats(handoff, master, local, backup, room);
            AllHeadsetsStandby(handoff, master, local, backup, room);
            GoldSchedules(room, local, backup);
            AuthorityRecovery(local, master);
            HelicopterRecovery(local, master);
            PhotonPlayer.Active.Remove(human);
            Debug.Log("MasterHandoffChecks: ALL PASSED (" + checks + " assertions)");
        }
        finally
        {
            client.CurrentRoom = previousRoom;
            typeof(LoadBalancingClient).GetProperty("LocalPlayer").SetValue(client, previousLocal);
            client.State = previousState;
            client.LoadBalancingPeer.DebugOut = previousLevel;
        }
    }

    static void Beat(PhotonMasterHandoff handoff, Player actor, bool ready, int age = 0)
    {
        var data = new EventData { Code = 105 };
        data.Parameters[ParameterCode.ActorNr] = actor.ActorNumber;
        data.Parameters[ParameterCode.Data] = new object[] { unchecked(PhotonNetwork.ServerTimestamp - age), ready };
        handoff.OnEvent(data);
    }

    static void Heartbeats(PhotonMasterHandoff h, Player master, Player local, Player backup, Room room)
    {
        var fresh = typeof(PhotonMasterHandoff).GetMethod("Fresh", BindingFlags.Static | BindingFlags.NonPublic);
        bool Fresh(int sent, int now) => (bool)fresh.Invoke(null, new object[] { sent, now });
        Check(Fresh(1000, 5999) && !Fresh(1000, 6000), "fallback boundary is exactly five seconds");
        Check(Fresh(int.MaxValue - 2000, unchecked(int.MaxValue + 2999))
            && !Fresh(int.MaxValue - 2000, unchecked(int.MaxValue + 3000)), "heartbeat timeout survives timestamp wrap");
        Check(!Fresh(5000, 1000), "future heartbeat cannot keep a stalled master alive");
        Beat(h, master, true); Beat(h, backup, true);
        Call(h, "Update");
        Check(Get<double>(h, "retryAt") == 0, "responsive master is never replaced");
        Check(ReferenceEquals(Call(h, "SelectCandidate"), local), "lowest ready actor is the sole fallback candidate");
        Set(h, "focused", false);
        Check(ReferenceEquals(Call(h, "SelectCandidate"), backup), "unfocused local actor cannot take authority");
        Beat(h, backup, false);
        Check(Call(h, "SelectCandidate") == null, "no handoff when all possible successors are unavailable");
        Set(h, "focused", true);
        // Expire the master's accepted heartbeat without depending on wall-clock sleeps.
        var beats = (IDictionary)Get<object>(h, "heartbeats");
        object stale = beats[master.ActorNumber];
        stale.GetType().GetField("SentAt").SetValue(stale, unchecked(PhotonNetwork.ServerTimestamp - 5100));
        beats[master.ActorNumber] = stale;
        Call(h, "Update");
        Check(Get<double>(h, "retryAt") > Time.realtimeSinceStartupAsDouble, "silent master's next awake actor requests takeover");
        Check(room.MasterClientId == master.ActorNumber && !PhotonNetwork.IsMasterClient, "request never grants speculative local authority");
        double retry = Get<double>(h, "retryAt");
        Call(h, "Update");
        Check(Get<double>(h, "retryAt") == retry, "failed or pending request retries at most once per second");
        Call(h, "ResetObservation");
        Set(h, "nextHeartbeat", double.PositiveInfinity);
        Call(h, "Update");
        Check(beats.Count == 0 && Get<double>(h, "retryAt") == 0, "resume clears stale candidates and grants unknown master observation time");
        Beat(h, master, true, 6000);
        Check(beats.Count == 0, "queued pre-suspend packets cannot restore a stale candidate");
        Beat(h, master, false);
        Call(h, "Update");
        Check(Get<double>(h, "retryAt") > Time.realtimeSinceStartupAsDouble, "explicit unavailable master bypasses heartbeat timeout");
        Set(h, "retryAt", 0d); Set(room, "masterClientId", local.ActorNumber);
        Set(h, "focused", true); Set(h, "paused", false);
        Beat(h, backup, true);
        Call(h, "OnApplicationFocus", false);
        Check(Get<double>(h, "retryAt") > Time.realtimeSinceStartupAsDouble, "focus loss attempts immediate master handoff");
        Set(h, "retryAt", 0d); Set(h, "focused", true);
        Call(h, "OnApplicationPause", true);
        Check(Get<double>(h, "retryAt") > Time.realtimeSinceStartupAsDouble, "standby attempts immediate master handoff");
        Call(h, "OnApplicationFocus", true);
        Check(!(bool)typeof(PhotonMasterHandoff).GetProperty("Ready", Private).GetValue(h), "focus alone cannot mark a paused headset ready");
        Set(room, "masterClientId", backup.ActorNumber);
        Call(h, "OnApplicationPause", false);
        Set(h, "nextHeartbeat", double.PositiveInfinity);
        Call(h, "Update");
        Check(Get<double>(h, "retryAt") == 0 && room.MasterClientId == backup.ActorNumber, "returning old master does not reclaim authority");
        Set(room, "masterClientId", local.ActorNumber);
        Beat(h, backup, true);
        Call(h, "OnDisplayFocus", false);
        Check(Get<double>(h, "retryAt") > Time.realtimeSinceStartupAsDouble, "XR display focus loss also hands off immediately");
        Call(h, "OnApplicationFocus", true);
        Check(!(bool)typeof(PhotonMasterHandoff).GetProperty("Ready", Private).GetValue(h), "app focus cannot override an unfocused XR display");
        Call(h, "OnDisplayFocus", true);
        Set(room, "masterClientId", backup.ActorNumber);
        Beat(h, backup, true);
        h.OnMasterClientSwitched(backup);
        Set(h, "nextHeartbeat", double.PositiveInfinity);
        Call(h, "Update");
        Check(Get<double>(h, "retryAt") == 0, "newly confirmed master receives a fresh observation interval");
        Beat(h, backup, true);
        var tick = (Action)Delegate.CreateDelegate(typeof(Action), h, typeof(PhotonMasterHandoff).GetMethod("Update", Private));
        for (int i = 0; i < 100; i++) tick();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) tick();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated == 0, "steady heartbeat monitoring allocates zero bytes over 10,000 frames (got " + allocated + ")");
        h.OnLeftRoom();
        Check(beats.Count == 0 && Get<Room>(h, "room") == null, "room departure clears all election state");
    }

    static void AllHeadsetsStandby(PhotonMasterHandoff h, Player previous, Player local, Player backup, Room room)
    {
        Set(h, "room", room);
        Set(room, "masterClientId", local.ActorNumber);
        Set(h, "paused", true); Set(h, "focused", false); Set(h, "xrFocused", false);
        Set(h, "retryAt", 0d);
        // The backup's last ready packet predates its sleep; its unavailable packet was lost.
        Beat(h, backup, true);
        var beats = (IDictionary)Get<object>(h, "heartbeats");
        Call(h, "OnApplicationPause", false);
        Check(beats.Count == 0 && Get<double>(h, "retryAt") == 0,
            "first wake callback clears pre-sleep candidates even before app/XR focus returns");
        Beat(h, backup, true, 100);
        Beat(h, previous, false, 100);
        Check(beats.Count == 0, "recent but pre-wake queued packets cannot repopulate cleared observations");
        Call(h, "OnApplicationFocus", true);
        Call(h, "OnDisplayFocus", true);
        Call(h, "Update");
        Check(PhotonNetwork.IsMasterClient && Get<double>(h, "retryAt") == 0,
            "original master waking first retains authority when no transfer was confirmed");

        // A transfer completed before everyone slept, so the first waking headset is now a client.
        Call(h, "OnApplicationPause", true);
        Set(room, "masterClientId", backup.ActorNumber);
        h.OnMasterClientSwitched(backup);
        Call(h, "OnApplicationPause", false);
        Call(h, "Update");
        Check(Get<double>(h, "retryAt") == 0 && !PhotonNetwork.IsMasterClient,
            "former master waking alone observes the current master before takeover");
        Set(h, "observeUntil", Time.realtimeSinceStartupAsDouble - 0.001);
        Call(h, "Update");
        Check(Get<double>(h, "retryAt") > Time.realtimeSinceStartupAsDouble && !PhotonNetwork.IsMasterClient,
            "first awake former master requests recovery after five seconds without speculative authority");
        Set(room, "masterClientId", local.ActorNumber);
        h.OnMasterClientSwitched(local);
        Call(h, "Update");
        Check(PhotonNetwork.IsMasterClient && Get<double>(h, "retryAt") == 0,
            "confirmed recovery settles with the only awake headset as master");
        Beat(h, backup, true);
        Call(h, "Update");
        Check(PhotonNetwork.IsMasterClient && Get<double>(h, "retryAt") == 0,
            "another headset waking later does not displace the recovered healthy master");

        Call(h, "OnApplicationPause", true);
        Set(room, "masterClientId", backup.ActorNumber);
        Call(h, "OnApplicationPause", false);
        Beat(h, backup, true);
        Set(h, "observeUntil", Time.realtimeSinceStartupAsDouble - 0.001);
        Call(h, "Update");
        Check(Get<double>(h, "retryAt") == 0 && !PhotonNetwork.IsMasterClient,
            "current master's fresh post-wake heartbeat cancels former master's fallback");

        string[][] wakeOrders = {
            new[] { "OnApplicationPause", "OnApplicationFocus", "OnDisplayFocus" },
            new[] { "OnApplicationPause", "OnDisplayFocus", "OnApplicationFocus" },
            new[] { "OnApplicationFocus", "OnApplicationPause", "OnDisplayFocus" },
            new[] { "OnApplicationFocus", "OnDisplayFocus", "OnApplicationPause" },
            new[] { "OnDisplayFocus", "OnApplicationPause", "OnApplicationFocus" },
            new[] { "OnDisplayFocus", "OnApplicationFocus", "OnApplicationPause" }
        };
        foreach (string[] order in wakeOrders)
        {
            Set(room, "masterClientId", local.ActorNumber);
            Set(h, "paused", true); Set(h, "focused", false); Set(h, "xrFocused", false);
            Set(h, "retryAt", 0d);
            Beat(h, backup, true);
            bool retained = true;
            foreach (string callback in order)
            {
                Call(h, callback, callback != "OnApplicationPause");
                Beat(h, backup, true, 100);
                Call(h, "Update");
                retained &= beats.Count == 0 && Get<double>(h, "retryAt") == 0;
            }
            Check(retained && (bool)typeof(PhotonMasterHandoff).GetProperty("Ready", Private).GetValue(h),
                "all-asleep master resumes safely in callback order " + string.Join(", ", order));
        }
    }

    static void GoldSchedules(Room room, Player local, Player other)
    {
        room.CustomProperties["goldInitialized"] = true;
        room.CustomProperties["goldRespawns"] = new int[] { 30000, 31000 };
        var manager = new GameObject("Gold migration").AddComponent<PhotonItemManager>();
        Call(manager, "RestoreSchedule");
        var timers = Get<List<int>>(manager, "respawnAt");
        Check(Get<bool>(manager, "initialSpawn") && timers.Count == 2 && timers[0] == 30000, "replacement restores pending gold deadlines without restarting initial spawn");
        Check(!(bool)Call(manager, "ReconcileRespawns", 2, 15000) && timers[0] == 30000, "handoff does not restart existing respawn delays");
        Check((bool)Call(manager, "ReconcileRespawns", 3, 15000) && timers[2] == 45000, "missing unpublished destruction gets one replacement deadline");
        Check(!(bool)Call(manager, "ReconcileRespawns", 3, 20000), "repeated ticks do not duplicate replacements");
        Call(manager, "ReconcileRespawns", 1, 20000);
        Check(timers.Count == 1 && timers[0] == 45000, "extra live gold cancels oldest unnecessary replacements");
        int[] shared = (int[])room.CustomProperties["goldRespawns"];
        Check(shared.Length == 2 && shared[0] == 30000, "restored schedule does not mutate cached room property arrays");
        manager.OnMasterClientSwitched(local);
        Check(!Get<bool>(manager, "wasMaster") && Get<double>(manager, "nextCheck") > Time.realtimeSinceStartupAsDouble,
            "new master waits for existing item registrations before replenishing");
        room.CustomProperties.Remove("goldInitialized"); room.CustomProperties.Remove("goldRespawns");
        Call(manager, "RestoreSchedule");
        Check(!Get<bool>(manager, "initialSpawn") && timers.Count == 0, "interrupted initial population can resume on replacement");
        Set(room, "masterClientId", other.ActorNumber);
        Check(!(bool)Call(manager, "SpawnGoldBar"), "former master cannot spawn gold");
    }

    static void AuthorityRecovery(Player local, Player previous)
    {
        var go = new GameObject("Enemy repeated transfer"); go.SetActive(false);
        var enemy = go.AddComponent<Enemy>();
        var sync = go.AddComponent<EnemyNetSync>();
        Call(sync, "Awake");
        Set(enemy, "_hadAuthority", true); Set(sync, "_hasSample", true);
        sync.OnControllerChange(previous, local);
        sync.OnControllerChange(local, previous);
        Check(!Get<bool>(enemy, "_hadAuthority") && !Get<bool>(sync, "_hasSample"), "enemy clears brain ownership and stale pose across transfers without Update");
        var duck = new GameObject("Companion repeated transfer").AddComponent<DuckCompanion>();
        Set(duck, "wasAuthority", true);
        duck.OnMasterClientSwitched(previous); duck.OnMasterClientSwitched(local);
        Check(!Get<bool>(duck, "wasAuthority"), "returning companion controller must run navigation/motor recovery");
        Object.DestroyImmediate(duck.gameObject);
        Object.DestroyImmediate(go);
    }

    static void HelicopterRecovery(Player local, Player previous)
    {
        var go = new GameObject("Helicopter migration"); go.SetActive(false);
        var helicopter = go.AddComponent<HelicopterManager>();
        helicopter.spawnTimer = 45; helicopter.stayTimer = 17;
        Set(helicopter, "landTimer", 4f);
        helicopter.OnMasterClientSwitched(previous); helicopter.OnMasterClientSwitched(local);
        Check(Get<Coroutine>(helicopter, "schedule") == null && helicopter.spawnTimer == 45
            && helicopter.stayTimer == 17 && Get<float>(helicopter, "landTimer") == 4,
            "helicopter discards old coroutine without losing replicated timers");
        var routine = (IEnumerator)Call(helicopter, "AdvanceSchedule");
        Check(!routine.MoveNext(), "non-master cannot advance helicopter schedule");
        var room = PhotonNetwork.CurrentRoom;
        var phaseField = Field(helicopter, "phase");
        for (int phase = 0; phase < 4; phase++)
        {
            phaseField.SetValue(helicopter, Enum.ToObject(phaseField.FieldType, phase));
            helicopter.spawnTimer = 45; helicopter.stayTimer = 17;
            Set(helicopter, "landTimer", 4f);
            Set(room, "masterClientId", local.ActorNumber);
            routine = (IEnumerator)Call(helicopter, "AdvanceSchedule");
            Check(routine.MoveNext(), "master resumes helicopter phase " + phase);
            Set(room, "masterClientId", previous.ActorNumber);
            Check(!routine.MoveNext() && helicopter.spawnTimer == 45 && helicopter.stayTimer == 17
                && Get<float>(helicopter, "landTimer") == 4,
                "suspended former-master coroutine cannot change phase " + phase + " timers");
        }
        Object.DestroyImmediate(go);
    }
}
