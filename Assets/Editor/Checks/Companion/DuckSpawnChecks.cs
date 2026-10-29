using System;
using System.Collections.Generic;
using System.Reflection;
using Photon.Pun;
using UnityEngine;
using UnityEditor.SceneManagement;

// Real offline Photon instantiation/destruction; peer roles and arrival order
// are simulated. No server, game scene, microphone or speech synthesis needed.
public static class DuckSpawnChecks
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object o, string name, object value) => o.GetType().GetField(name, Flags).SetValue(o, value);
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Debug.Log("SPAWN PASS: " + label); }
    sealed class Pool : IPunPrefabPool
    {
        public int ducks;
        public readonly List<GameObject> objects = new List<GameObject>();
        public GameObject Instantiate(string name, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject(name); go.SetActive(false); objects.Add(go);
            go.transform.SetPositionAndRotation(position, rotation);
            go.AddComponent<PhotonView>();
            if (name == "DuckCompanion") { go.AddComponent<DuckCompanion>(); ducks++; }
            var player = go.AddComponent<PhotonPlayer>();
            PhotonPlayer.Active.Add(player);
            return go;
        }
        public void Destroy(GameObject go)
        {
            PhotonPlayer.Active.Remove(go.GetComponent<PhotonPlayer>());
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new Exception("Isolated batch fixture only");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        PhotonPlayer.Active.Clear();
        PhotonNetwork.OfflineMode = true;
        PhotonNetwork.CreateRoom("Duck spawn regression");
        Check(PhotonNetwork.InRoom, "offline room ready");
        var pool = new Pool(); PhotonNetwork.PrefabPool = pool;
        var manager = new GameObject("Manager").AddComponent<GameManager>(); GameManager.instance = manager;
        var bootstrap = new GameObject("Bootstrap").AddComponent<DuckCompanionBootstrap>();
        var method = typeof(DuckCompanionBootstrap).GetMethod("CheckCompanion", Flags);
        var tick = (Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>), bootstrap, method);
        var find = (Func<DuckCompanion>)Delegate.CreateDelegate(typeof(Func<DuckCompanion>), bootstrap,
            typeof(DuckCompanionBootstrap).GetMethod("FindCompanion", Flags));
        var prefab = new GameObject("DuckCompanion"); Set(bootstrap, "prefab", prefab);
        tick(true); Check(pool.ducks == 0, "missing local player waits without spawning");
        var human = PhotonNetwork.Instantiate("Human", Vector3.zero, Quaternion.identity);
        Check(PhotonPlayer.Local == human.GetComponent<PhotonPlayer>(), "local human registered");
        tick(true); Check(pool.ducks == 0, "missing spawn pose waits for next retry");
        manager.companionSpawnPoint = new GameObject("Spawn").transform;
        manager.companionSpawnPoint.position = new Vector3(4, 2, 7);
        Set(bootstrap, "prefab", null); tick(true); Check(pool.ducks == 0, "missing prefab cannot create duplicates");
        Set(bootstrap, "prefab", prefab);
        tick(false); Check(pool.ducks == 0, "non-master never spawns a missing Duck");
        manager.disableDuckCompanion = true; tick(true); Check(pool.ducks == 0, "GameManager switch disables spawn");
        manager.disableDuckCompanion = false;
        // A previously non-master bootstrap takes responsibility with no opt-in.
        tick(true); Check(pool.ducks == 1, "new master spawns missing Duck once prerequisites are ready");
        var duck = find(); Check(duck != null, "spawn result cached");
        Check(duck.GetComponent<PhotonView>().OwnerActorNr == 0, "Duck is room-owned, not owned by departing master");
        Check(duck.transform.position == manager.companionSpawnPoint.position, "companion spawn point used");
        for (int i = 0; i < 10; i++) tick(true);
        Check(pool.ducks == 1, "repeated checks never duplicate existing Duck");
        PhotonPlayer.Active.Remove(duck.GetComponent<PhotonPlayer>());
        duck.gameObject.SetActive(false); tick(true);
        Check(pool.ducks == 1 && find() == duck, "temporary disable/registry gap retains known instance");
        duck.gameObject.SetActive(true); PhotonPlayer.Active.Add(duck.GetComponent<PhotonPlayer>());
        // Fresh peer with no cached reference discovers the replayed room object.
        Set(bootstrap, "knownCompanion", null); tick(false);
        Check(find() == duck && pool.ducks == 1, "late joiner discovers existing Duck without spawning");
        tick(true); Check(pool.ducks == 1, "master takeover with existing Duck creates no duplicate");
        find(); long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) find();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated == 0, "10000 steady-state companion lookups allocate zero bytes");
        Set(bootstrap, "nextCheck", Time.time + 100f);
        typeof(DuckCompanionBootstrap).GetMethod("Update", Flags).Invoke(bootstrap, null);
        Check(pool.ducks == 1, "per-frame update respects periodic check gate");
        manager.disableDuckCompanion = true; tick(false);
        Check(find() == duck, "non-master cannot remove room Duck");
        tick(true); Check(find() == null, "master disable removes room Duck");
        tick(true); Check(pool.ducks == 1, "disabled state does not respawn Duck");
        manager.disableDuckCompanion = false; tick(true);
        Check(pool.ducks == 2 && find() != null, "re-enable spawns exactly one replacement");
        PhotonNetwork.Destroy(find().gameObject);
        Set(bootstrap, "knownCompanion", null); tick(false); Check(pool.ducks == 2, "peer waits after object disappears");
        tick(true); Check(pool.ducks == 3, "next master recreates missing room object");
        Debug.Log("ALL DUCK SPAWN CHECKS PASSED (offline room, simulated peer roles)");
    }
}
