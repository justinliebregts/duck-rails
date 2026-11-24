using System;
using System.Collections.Generic;
using System.Reflection;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Object = UnityEngine.Object;

// Play Mode with a synthetic room; no Photon connection is opened. Run from Duck Rails > Tests > Networking.
// Feeds PUN's real instantiate-event path two room spawns that share one ViewID (two clients
// allocating during a master handoff) and checks every arrival order keeps the same spawn.
// handoff timing still need a two-headset play test.
public static class DuplicateViewIdChecks
{
    const string Key = nameof(DuplicateViewIdChecks);
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    static int checks;
    static Room room;
    static Player local, remote;
    static Pool pool;

    sealed class Actor : Player { public Actor(int id, bool isLocal = false) : base("duplicate check", id, isLocal) { } }

    sealed class Pool : IPunPrefabPool
    {
        public readonly List<GameObject> Returned = new List<GameObject>();
        public GameObject Instantiate(string prefabId, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject(prefabId);
            go.SetActive(false);
            go.AddComponent<PhotonView>();
            return go;
        }
        // Mirrors EnemyPrefabPool.Destroy: deactivate, clear the ID, keep the instance.
        public void Destroy(GameObject go)
        {
            go.SetActive(false);
            var view = go.GetComponent<PhotonView>();
            if (view.ViewID != 0) view.ViewID = 0;
            Returned.Add(go);
        }
    }

    static FieldInfo Field(Type type, string name, BindingFlags flags)
    {
        for (; type != null; type = type.BaseType)
        {
            var field = type.GetField(name, flags | BindingFlags.Public);
            if (field != null) return field;
        }
        throw new Exception("Missing field " + name);
    }
    static void Set(object o, string name, object value) => Field(o.GetType(), name, Private).SetValue(o, value);
    static T Get<T>(object o, string name) => (T)Field(o.GetType(), name, Private).GetValue(o);
    static T Static<T>(Type type, string name) => (T)Field(type, name, PrivateStatic).GetValue(null);
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception("FAIL: " + message); Debug.Log("PASS: " + message); }

    [MenuItem("Duck Rails/Tests/Networking/Run Duplicate ViewID Checks")]
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // EditorPlaySignIn normally boots matchmaking from an empty play scene.
        var login = new GameObject("Suppress automatic editor sign-in");
        login.SetActive(false);
        login.AddComponent<LoginFlow>();
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    static void Register() => EditorApplication.update += Poll;

    static void Poll()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.EraseBool(Key);
        int code = 0;
        try
        {
            Execute();
            Debug.Log($"Duplicate ViewID checks passed: {checks} assertions");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            code = 1;
        }
        if (Application.isBatchMode) EditorApplication.Exit(code);
        else EditorApplication.ExitPlaymode();
    }

    static void Execute()
    {
        JoinSyntheticRoom();
        pool = new Pool();
        PhotonNetwork.PrefabPool = pool;

        // The reported case: Bomber cached first, Hook second, both as room view 17.
        foreach (bool master in new[] { true, false })
            foreach (bool newerFirst in new[] { false, true })
                Collision($"{(master ? "master" : "client")}, {(newerFirst ? "newer" : "older")} spawn first",
                    master, newerFirst, 1000, 2000, "bomberToken", "hookToken");
        // Equal timestamps fall back to the token; ordinal order picks the same spawn everywhere.
        Collision("tie, older first", true, false, 5000, 5000, "aaaa", "bbbb");
        Collision("tie, newer first", true, true, 5000, 5000, "aaaa", "bbbb");
        // Server timestamps wrap around int.MaxValue.
        Collision("wrapped timestamp, older first", true, false, int.MaxValue - 5, int.MinValue + 5, "beforeWrap", "afterWrap");
        Collision("wrapped timestamp, newer first", true, true, int.MaxValue - 5, int.MinValue + 5, "beforeWrap", "afterWrap");

        NormalSpawnUnchanged();
        ReuseAfterRemovalIsNotADuplicate();
        PlayerViewDuplicateKeepsOldBehaviour();
        StaleViewCannotEvictLiveView();
        LocalSpawnSendsItsOwnTimestamp();
    }

    static void JoinSyntheticRoom()
    {
        var client = PhotonNetwork.NetworkingClient;
        // Requests deliberately cannot reach a server.
        client.LoadBalancingPeer.DebugOut = DebugLevel.OFF;
        local = new Actor(1, true);
        remote = new Actor(2);
        room = new Room("duplicate viewid checks", new RoomOptions());
        room.LoadBalancingClient = client;
        room.AddPlayer(local);
        room.AddPlayer(remote);
        typeof(LoadBalancingClient).GetProperty("LocalPlayer").SetValue(client, local);
        client.CurrentRoom = room;
        client.State = ClientState.Joined;
        SetMaster(local);
        Check(PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient, "synthetic room joined as master");
    }

    static void SetMaster(Player player) => room.masterClientId = player.ActorNumber;

    static Hashtable RemoveFilter => Static<Hashtable>(typeof(PhotonNetwork), "removeFilter");

    static readonly MethodInfo ReceiveInstantiate = typeof(PhotonNetwork).GetMethod("NetworkInstantiate", PrivateStatic,
        null, new[] { typeof(Hashtable), typeof(Player) }, null);

    // The cached/live instantiate event exactly as SendInstantiate builds it.
    static GameObject Receive(int viewId, int timestamp, string token)
    {
        var ev = new Hashtable { [(byte)0] = "Spawn", [(byte)6] = timestamp, [(byte)7] = viewId, [(byte)10] = token };
        try { return (GameObject)ReceiveInstantiate.Invoke(null, new object[] { ev, remote }); }
        catch (TargetInvocationException e) { throw e.InnerException ?? e; }
    }

    static void Reset()
    {
        foreach (var view in Object.FindObjectsByType<PhotonView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (view.ViewID != 0) view.ViewID = 0;
            Object.DestroyImmediate(view.gameObject);
        }
        pool.Returned.Clear();
        RemoveFilter.Clear();
        SetMaster(local);
    }

    static void Collision(string label, bool master, bool newerFirst, int olderTime, int newerTime, string olderToken, string newerToken)
    {
        Reset();
        SetMaster(master ? local : remote);
        const int id = 17;
        GameObject first = newerFirst ? Receive(id, newerTime, newerToken) : Receive(id, olderTime, olderToken);
        GameObject second = newerFirst ? Receive(id, olderTime, olderToken) : Receive(id, newerTime, newerToken);

        var winner = PhotonView.Find(id);
        Check(winner != null && winner.SpawnToken == newerToken && winner.gameObject.activeSelf, label + ": newer spawn holds the ID and is live");
        Check(newerFirst ? second == null && first == winner.gameObject : first != winner.gameObject && second == winner.gameObject,
            label + ": NetworkInstantiate returns only the kept spawn");
        Check(pool.Returned.Count == 1 && pool.Returned[0] != winner.gameObject, label + ": exactly one spawn returned to the pool");
        var loser = pool.Returned[0].GetComponent<PhotonView>();
        Check(loser.SpawnToken == olderToken && loser.ViewID == 0 && !loser.gameObject.activeSelf && PhotonView.Find(id) == winner,
            label + ": older spawn cleared and inactive; winner still listed");
        if (master)
            Check(RemoveFilter[(byte)7] is int filterId && filterId == id && (string)RemoveFilter[(byte)10] == olderToken,
                label + ": master removes the older spawn from the room cache");
        else
            Check(RemoveFilter.Count == 0, label + ": non-master leaves the room cache alone");
    }

    static void NormalSpawnUnchanged()
    {
        Reset();
        var go = Receive(18, 4242, "solo");
        var view = PhotonView.Find(18);
        Check(go != null && view != null && view.gameObject == go && go.activeSelf && view.SpawnTimestamp == 4242 && view.IsRoomView,
            "single room spawn registers normally with its timestamp");
        Check(pool.Returned.Count == 0 && RemoveFilter.Count == 0, "single room spawn touches neither pool nor cache");
    }

    static void ReuseAfterRemovalIsNotADuplicate()
    {
        Reset();
        var old = Receive(19, 3000, "first");
        typeof(PhotonNetwork).GetMethod("RemoveInstantiatedGO", PrivateStatic).Invoke(null, new object[] { old, true });
        // A later spawn may carry an earlier timestamp (clock skew between senders); with no listed view it is not a duplicate.
        var reused = Receive(19, 2000, "second");
        Check(reused != null && PhotonView.Find(19)?.SpawnToken == "second" && pool.Returned.Count == 1 && pool.Returned[0] == old,
            "ID reused after a proper removal registers even with an older timestamp");
    }

    static void PlayerViewDuplicateKeepsOldBehaviour()
    {
        Reset();
        const int id = 2017; // actor 2's view, not a room view
        Receive(id, 9000, "playerNewer");
        Receive(id, 1000, "playerOlder");
        Check(PhotonView.Find(id)?.SpawnToken == "playerOlder" && RemoveFilter.Count == 0,
            "player-owned duplicate keeps PUN's last-registered-wins behaviour");
    }

    static void StaleViewCannotEvictLiveView()
    {
        Reset();
        Receive(20, 100, "live");
        var live = PhotonView.Find(20);
        var stale = new GameObject("Stale view");
        stale.SetActive(false);
        var staleView = stale.AddComponent<PhotonView>();
        Set(staleView, "viewIdField", 20); // holds the ID without being the listed view
        Check(!PhotonNetwork.LocalCleanPhotonView(staleView) && PhotonView.Find(20) == live, "cleaning a stale view leaves the live view listed");
        Set(staleView, "removedFromLocalViewList", false);
        staleView.ViewID = 0;
        Check(PhotonView.Find(20) == live, "zeroing a stale view's ID leaves the live view listed");
        Set(staleView, "viewIdField", 20);
        Set(staleView, "removedFromLocalViewList", false); // OnDestroy now runs LocalCleanPhotonView
        Object.DestroyImmediate(stale);
        Check(PhotonView.Find(20) == live, "destroying a stale view leaves the live view listed");
    }

    static void LocalSpawnSendsItsOwnTimestamp()
    {
        Reset();
        var parameters = new Photon.Pun.InstantiateParameters("Spawn", Vector3.zero, Quaternion.identity, 0, null, 0, null, local, 777);
        var method = typeof(PhotonNetwork).GetMethod("NetworkInstantiate", PrivateStatic, null,
            new[] { typeof(Photon.Pun.InstantiateParameters), typeof(bool), typeof(bool), typeof(bool) }, null);
        var go = (GameObject)method.Invoke(null, new object[] { parameters, true, false, false });
        var view = go.GetComponent<PhotonView>();
        var sent = Static<Hashtable>(typeof(PhotonNetwork), "SendInstantiateEvHashtable");
        Check(view.IsRoomView && view.SpawnTimestamp == 777 && sent[(byte)6] is int time && time == 777 &&
            (string)sent[(byte)10] == view.SpawnToken, "local room spawn sends the same timestamp and token it keeps");
    }
}
