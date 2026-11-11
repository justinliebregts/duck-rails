using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game.AI;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Two disposable batch editors, same -flareRun GUID, with -flareRole host or guest; omit -quit.
[InitializeOnLoad]
public static class FlareJoinNetworkChecks
{
    const string Running = "Duckz.FlareJoinCheck";
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    static IEnumerator steps;
    static Func<bool> condition;
    static double deadline;
    static string stage, folder, run;
    static DeathFlare prefab;
    static Enemy smasher;
    static EnemyManager manager;
    static bool host;

    static FlareJoinNetworkChecks()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            steps = Scenario();
            EditorApplication.update += Step;
        };
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Disposable batch editors only");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Fixture sign-in guard").AddComponent<LoginFlow>().enabled = false;
        SessionState.SetBool(Running, true);
        EditorApplication.EnterPlaymode();
    }

    static Func<bool> Until(string description, Func<bool> ready)
    {
        stage = description;
        Debug.Log("FLARE CHECK " + (host ? "host " : "guest ") + stage);
        deadline = EditorApplication.timeSinceStartup + 120;
        return ready;
    }

    static void Step()
    {
        int result = 0;
        try
        {
            if (condition != null && !condition())
            {
                if (EditorApplication.timeSinceStartup >= deadline)
                    throw new TimeoutException(stage + " / " + PhotonNetwork.NetworkClientState);
                return;
            }
            if (steps.MoveNext()) { condition = (Func<bool>)steps.Current; return; }
            Debug.Log("FLARE LIVE PASS " + (host ? "host" : "guest"));
        }
        catch (Exception e) { Debug.LogException(e); result = 1; }
        EditorApplication.update -= Step;
        SessionState.SetBool(Running, false);
        PhotonNetwork.Disconnect();
        EditorApplication.Exit(result);
    }

    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Mark(string name) => File.WriteAllText(Path.Combine(folder, name), "");
    static bool Marked(string name) => File.Exists(Path.Combine(folder, name));
    static int Active => DeathEffectPool.Get().ActiveCount;
    static int Saved()
    {
        int count = 0;
        foreach (var entry in PhotonNetwork.CurrentRoom.CustomProperties)
            if (entry.Key is string key && key.StartsWith("deathFlare:", StringComparison.Ordinal) && entry.Value != null) count++;
        return count;
    }

    static PhotonPlayer Avatar(int viewId, bool companion = false)
    {
        // Inert avatars exercise the production RPCs without starting XR, voice, or login.
        var go = new GameObject("Network death fixture");
        go.SetActive(false);
        var view = go.AddComponent<PhotonView>();
        var avatar = go.AddComponent<PhotonPlayer>();
        if (companion) go.AddComponent<DuckCompanion>();
        typeof(PhotonPlayer).GetField("deathFlarePrefab", Fields).SetValue(avatar, prefab);
        view.ViewID = viewId;
        if (avatar.IsLocalHuman) PhotonPlayer.Active.Add(avatar);
        return avatar;
    }

    static void SetupSmasher()
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.layer = 6;
        floor.transform.position = Vector3.down * .5f;
        floor.transform.localScale = new Vector3(100, 1, 100);
        smasher = SmasherTallBuildingChecks.MakeActor(out _);
        var net = smasher.gameObject.AddComponent<EnemyNetSync>();
        net.photonView.ViewID = 951;
        net.photonView.Synchronization = ViewSynchronization.UnreliableOnChange;
        net.photonView.ObservedComponents = new List<Component> { net };
        typeof(Enemy).GetMethod("Awake", Fields).Invoke(smasher, null);
        manager = SmasherTallBuildingChecks.Manager();
        Physics.SyncTransforms();
    }

    static IEnumerator Scenario()
    {
        var args = Environment.GetCommandLineArgs();
        run = Guid.Parse(args[Array.IndexOf(args, "-flareRun") + 1]).ToString("N");
        host = args[Array.IndexOf(args, "-flareRole") + 1] == "host";
        folder = Path.Combine(Path.GetTempPath(), "DuckzFlare-" + run);
        Directory.CreateDirectory(folder);
        prefab = AssetDatabase.LoadAssetAtPath<DeathFlare>("Assets/Prefabs/Effects/DeathFlare.prefab");
        var settings = PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());
        settings.AppVersion = "flare-check-" + run;
        settings.FixedRegion = PhotonSession.Region;
        Check(PhotonNetwork.ConnectUsingSettings(settings), "Connect");
        yield return Until("connect", () => PhotonNetwork.NetworkClientState == ClientState.ConnectedToMasterServer);
        if (host)
            Check(PhotonNetwork.CreateRoom(run, new RoomOptions { MaxPlayers = 2, IsVisible = false, DeleteNullProperties = true }), "Create");
        else
        {
            Mark("guest-ready");
            yield return Until("two overlapping deaths", () => Marked("join"));
            Check(PhotonNetwork.JoinRoom(run), "Join");
        }
        yield return Until("joined", () => PhotonNetwork.NetworkClientState == ClientState.Joined);
        Avatar(PhotonNetwork.MAX_VIEW_IDS + 1);
        var duck = Avatar(950, true);
        duck.transform.position = Vector3.right * 20;
        PhotonRoomFlares.Ready(prefab);
        SetupSmasher();
        if (host)
        {
            yield return Until("guest connected", () => Marked("guest-ready"));
            PhotonPlayer.SpawnDeathFlare(prefab, Vector3.up);
            yield return Until("human flare burning", () => DeathFlare.FindNear(Vector3.up, 100) != null && Saved() == 1);
            duck.EmitCompanionDeathBlood();
            yield return Until("companion checkpoint", () => Saved() == 2);
            Mark("join");
            yield return Until("guest joined", () => PhotonNetwork.CurrentRoom.PlayerCount == 2);
        }
        else
        {
            Check(Active == 2, "Late join reconstructs both flares exactly once");
            foreach (var flare in UnityEngine.Object.FindObjectsByType<DeathFlare>(FindObjectsSortMode.None))
            {
                var key = (string)typeof(DeathFlare).GetProperty("RoomEventKey", Fields).GetValue(flare);
                var data = (object[])PhotonNetwork.CurrentRoom.CustomProperties[key];
                double originalAge = unchecked(PhotonNetwork.ServerTimestamp - (int)data[0]) / 1000d;
                double launchedAt = (double)typeof(DeathFlare).GetField("started", Fields).GetValue(flare);
                Check(Math.Abs(Time.unscaledTimeAsDouble - launchedAt - originalAge + DeathFlare.DeathLaunchDelay) < .2,
                    "Recovered flare uses its original launch time");
            }
        }
        Avatar(2 * PhotonNetwork.MAX_VIEW_IDS + 1);
        if (host) Mark("remote-ready");
        else
        {
            yield return Until("remote avatar", () => Marked("remote-ready"));
            PhotonPlayer.SpawnDeathFlare(prefab, Vector3.left * 20);
        }
        yield return Until("RPC and property deduplication", () => Active == 3 && Saved() == 3);
        if (host)
        {
            yield return Until("flare above takeoff", () => DeathFlare.FindNear(Vector3.up * 24, 5) != null);
            var flare = DeathFlare.FindNear(Vector3.up * 24, 5);
            smasher.Teleport(flare.transform.position + Vector3.right, Quaternion.identity);
            smasher.TickAuthority(manager, .02f, false);
            Check(smasher.IsJumping && smasher.CurrentState == Enemy.State.Retreat, "Master starts flare retreat in midair");
            yield return Until("observer received airborne retreat", () => Marked("airborne"));
            PhotonPlayer.Active.Remove(PhotonPlayer.Local);
            Check(PhotonNetwork.LeaveRoom(), "Master leaves during fall");
            yield return Until("left", () => PhotonNetwork.NetworkClientState == ClientState.ConnectedToMasterServer);
            Check(Active == 0, "Leaving clears local room flares");
            yield return Until("successor landed", () => Marked("landed"));
            Check(PhotonNetwork.JoinRoom(run), "Join as a fresh actor while the original flares burn");
            yield return Until("joined during burn", () => PhotonNetwork.NetworkClientState == ClientState.Joined);
            PhotonRoomFlares.Ready(prefab);
            Check(PhotonNetwork.LocalPlayer.ActorNumber != 1 && Saved() == 3 && Active == 3,
                "Original human and companion flares survive their publisher leaving");
            Check(PhotonNetwork.LeaveRoom(), "Leave before expiry");
            yield return Until("left again", () => PhotonNetwork.NetworkClientState == ClientState.ConnectedToMasterServer);
            yield return Until("original deadlines", () => Marked("expired"));
            Check(PhotonNetwork.JoinRoom(run), "Rejoin after expiry");
            yield return Until("rejoined", () => PhotonNetwork.NetworkClientState == ClientState.Joined);
            PhotonRoomFlares.Ready(prefab);
            Check(Active == 0 && Saved() == 0, "Expired deaths never replay");
            Mark("rejoined");
        }
        else
        {
            yield return Until("airborne observer", () => {
                smasher.ApplyRemote(.02f);
                return smasher.CurrentState == Enemy.State.Retreat && smasher.Root.position.y > 2
                    && (bool)typeof(Enemy).GetField("_airborne", Fields).GetValue(smasher);
            });
            Mark("airborne");
            yield return Until("replacement master", () => PhotonNetwork.IsMasterClient && smasher.HasAuthority);
            int landings = 0;
            smasher.GetComponent<ZombieSmasherJump>().Landed += () => landings++;
            smasher.TickAuthority(manager, .02f, false);
            Check(smasher.IsJumping && smasher.CurrentState == Enemy.State.Retreat, "New master respects recovered flare");
            for (int i = 0; i < 250 && smasher.IsJumping; i++) smasher.TickAuthority(manager, .02f, false);
            Check(landings == 1 && smasher.Root.position.y < .1f, "New master lands once");
            Mark("landed");
            yield return Until("independent first expiry", () => Active == 2);
            yield return Until("all effects and room records expired", () => Active == 0 && Saved() == 0);
            Mark("expired");
            yield return Until("subsequent join", () => Marked("rejoined"));
        }
    }
}
