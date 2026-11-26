#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Photon.Pun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Live, single authenticated client: proves the player's loadout survives every way of changing worlds and every way
/// of dropping out. The loadout is a worn bag with contents, a worn helmet, worn night-vision goggles and a held item.
///
///  1. Private departure (same room, Compound to Expedition)        worn kept, held item dropped at feet
///  2. Bag contents changed aboard                                   disk save updated immediately
///  3. Private failure return (same room, Expedition to Compound)    worn kept with new contents, held dropped
///  4. Back to Compound mid-run (room change to a public Compound)   worn kept, held dropped
///  5. Create a private room (room change Compound to Compound)      worn kept, held dropped
///  6. Connection loss aboard (inactive, rejoin)                     worn back exactly once
///  7. Connection loss in a public Compound (no rejoin slot)         worn back exactly once
///  8. Quit (stop Play Mode) and crash (saves restored to their pre-quit state, as no teardown ran), relaunch
///                                                                   worn back exactly once
/// Every check must hold continuously for five seconds, to catch the half-second cleanup ticks.
/// The account's own saved loadout is backed up first and put back at the end, pass or fail.
/// Run from Edit Mode with Compound.unity open: Duck Rails > Tests > Loadout Persistence (Live).
/// Results: Temp/rails-loadout-live.txt. Never reads diagnostic reports.
/// </summary>
[InitializeOnLoad]
public static class RailLoadoutLiveChecks
{
    const string P = "RailLoadoutLive.";
    const string Evidence = "Temp/rails-loadout-live.txt";
    const byte BagId = 2; // Basic bag (store item bag_basic).
    static readonly string[] BagKeys = { "RailCoal", "RailGoldBar", "RailShopItemFlashlight", "" };
    const string AddedKey = "RailCoal";
    const string Helmet = "RailShopItemHelmet";
    const string Eyes = "RailGoggles";
    const string Held = "RailShopItemWalkieTalkie";
    const string CarryTag = "rails-carry-v1";
    static readonly string[] SaveKeys = { "bag.back", "bag.back.room", "bag.back.identity",
        "equipment.head", "equipment.head.room", "equipment.head.identity",
        "equipment.eyes", "equipment.eyes.room", "equipment.eyes.identity" };
    const double StableSeconds = 5, StageTimeout = 180;

    static RailLoadoutLiveChecks()
    {
        // Back in Edit Mode after a finished run: reopen the scene that was open before it started.
        string reopen = SessionState.GetString(P + "reopen", "");
        if (reopen.Length > 0 && !Running) EditorApplication.update += ReopenWhenEditing;
        if (!Running) return;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void ReopenWhenEditing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        EditorApplication.update -= ReopenWhenEditing;
        string reopen = SessionState.GetString(P + "reopen", "");
        SessionState.EraseString(P + "reopen");
        if (Running || reopen.Length == 0) return;
        if (EditorSceneManager.GetActiveScene().path != reopen && File.Exists(reopen)) EditorSceneManager.OpenScene(reopen);
    }

    // ---------- Persistent state (survives the domain reloads of entering and leaving Play Mode) ----------

    static bool Running { get => SessionState.GetBool(P + "running", false); set => SessionState.SetBool(P + "running", value); }
    static int Stage { get => SessionState.GetInt(P + "stage", 0); set => SessionState.SetInt(P + "stage", value); }
    static double Entered { get => double.Parse(SessionState.GetString(P + "entered", "0")); set => SessionState.SetString(P + "entered", value.ToString("R")); }
    static double OkSince { get => double.Parse(SessionState.GetString(P + "ok", "-1")); set => SessionState.SetString(P + "ok", value.ToString("R")); }
    static string Code { get => SessionState.GetString(P + "code", ""); set => SessionState.SetString(P + "code", value); }
    static string ExpectedBag { get => SessionState.GetString(P + "bag", ""); set => SessionState.SetString(P + "bag", value); }
    static string Log { get => SessionState.GetString(P + "log", ""); set => SessionState.SetString(P + "log", value); }
    static string Last { get => SessionState.GetString(P + "last", ""); set => SessionState.SetString(P + "last", value); }
    static double Now => EditorApplication.timeSinceStartup;

    [MenuItem("Duck Rails/Tests/Loadout Persistence (Live)")]
    public static void Start()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Start from Edit Mode.");
        if (Running) throw new InvalidOperationException("Already running; use Stop first.");
        if (RailRegressionLauncher.Status != "NotRunning") throw new InvalidOperationException("Stop the Multiplayer Play Mode scenario first.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.isDirty) throw new InvalidOperationException("Save or discard changes to " + scene.path + " first.");
        foreach (string key in new[] { "log", "last", "backup", "scoped", "snapshot", "code", "reopen" }) SessionState.EraseString(P + key);
        SessionState.SetString(P + "reopen", scene.path);
        if (scene.path != "Assets/Scenes/Compound.unity") EditorSceneManager.OpenScene("Assets/Scenes/Compound.unity");
        // Record the quick-start preference first: some fixtures' Disarm changes it.
        SessionState.SetBool(P + "quickStart", RailEditorQuickStart.Enabled);
        // Entering Play Mode directly launches only this editor; other armed live fixtures must not interfere.
        RailCoalLiveChecks.Disarm(); RailWeek2LiveChecks.Disarm(); RailWeek3LiveChecks.Disarm(); RailStabilityLiveChecks.Disarm();
        RailDeepRegressionChecks.Disarm(); RailMultiClientCleanupChecks.Disarm(); RailReviveLiveChecks.Disarm();
        RailEditorQuickStart.Enabled = false; // Land in a public Compound.
        Running = true; Stage = 0; Entered = Now; OkSince = -1;
        Note("started " + DateTime.UtcNow.ToString("u"));
        Save("running"); // Replace any previous run's result straight away.
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Duck Rails/Tests/Loadout Persistence (Live) - Stop")]
    public static void Stop() => Fail("stopped by user");

    // ---------- Stage machine ----------

    static void Next(string passed)
    {
        Note("PASS " + passed);
        Debug.Log("LOADOUT LIVE PASS: " + passed);
        Stage = Stage + 1; Entered = Now; OkSince = -1; Last = "";
        SessionState.SetString(P + "acted", Now.ToString("R"));
        Save("running");
    }

    // PhotonSession drops a room commit that lands within a second of another (for example a cargo manifest
    // update), so a phase request the stage is still waiting on is re-issued every four seconds.
    static void Nudge(Action request)
    {
        if (Now - double.Parse(SessionState.GetString(P + "acted", "0")) < 4) return;
        SessionState.SetString(P + "acted", Now.ToString("R"));
        request();
    }

    static void Tick()
    {
        if (!Running) { EditorApplication.update -= Tick; return; }
        try
        {
            if (Now - Entered > StageTimeout)
                throw new Exception("Timed out in stage " + Stage + ". Last check: " + Last + ". Session: " + SessionSummary());
            Step();
        }
        catch (Exception ex)
        {
            Debug.LogError("LOADOUT LIVE FAIL: " + ex);
            Fail(ex.Message);
        }
    }

    static void Step()
    {
        var session = PhotonSession.Instance;
        switch (Stage)
        {
            // Public Compound: back up and clear the account's real loadout so the run starts empty.
            case 0:
                // Ready for a while, so any restore of the account's own loadout has landed before it is cleared.
                if (!Stable(() => InWorld(false) && !PhotonSession.IsPrivate ? null : "waiting for a public Compound: " + SessionSummary())) return;
                BackupSaves();
                GrantFocus();
                ClearLoadout();
                Next("Signed in to a public Compound; account loadout backed up and cleared");
                return;
            case 1:
                if (Now - Entered < 3) return;
                Require(!WornAnything(), "Loadout still present after clearing: " + Describe());
                Code = UnityEngine.Random.Range(70000, 79999).ToString();
                session.RequestCreatePrivate(Code);
                Next("Cleared loadout stayed cleared; creating private room " + Code);
                return;
            // Private staging: put the test loadout on.
            case 2:
                if (!Stable(() => InWorld(false) && PhotonSession.IsPrivate ? null : "waiting for private staging")) return;
                Require(PhotonNetwork.IsMasterClient && PhotonSession.Phase == RailPhase.Staging, "Not staging master of the private room");
                GrantFocus();
                WearTestLoadout();
                ExpectedBag = "";
                Next("Private staging: test loadout spawned (basic bag " + string.Join(",", BagKeys) + ", " + Helmet + ", " + Eyes + ", holding " + Held + ")");
                return;
            case 3:
            {
                // The delivered bag fixes the expected save, including however many empty slots the bag has.
                var delivered = BagInventory.Local.BackBag;
                if (ExpectedBag.Length == 0)
                {
                    if (delivered == null) { Last = "test bag not on the back yet"; return; }
                    var keys = delivered.Keys();
                    Require(delivered.BagId == BagId && keys.Length >= 3 && keys[0] == BagKeys[0] && keys[1] == BagKeys[1] && keys[2] == BagKeys[2] &&
                        keys.Skip(3).All(k => k == ""), "Delivered bag " + SaveText(keys) + " does not hold the test contents");
                    ExpectedBag = SaveText(keys);
                    Note("delivered bag " + ExpectedBag);
                }
            }
                if (!Stable(() => Check(heldInHand: true))) return;
                // Crash safety: everything is already on disk before any quit or transition.
                Next("Loadout worn and held; disk saves already current (bag.back, equipment.head, equipment.eyes)");
                session.StartPrivateRun();
                return;
            // 1. Same-room swap Compound -> Expedition.
            case 4:
                if (InWorld(false) && PhotonSession.Phase == RailPhase.Staging) Nudge(session.StartPrivateRun);
                if (!InWorld(true) || PhotonSession.Phase != RailPhase.Running) return;
                if (!Stable(() => Check(heldDropped: true, privateRoom: true))) return;
                Next("Private departure: worn bag, helmet and goggles kept once; held item dropped once at feet; nothing left behind");
                return;
            // 2. Contents change aboard is saved immediately.
            case 5:
            {
                var back = BagInventory.Local.BackBag;
                var slot = back.FirstEmptySlot();
                Require(slot != null, "Test bag has no empty slot");
                back.Put(slot, AddedKey, back.transform.position, .15f);
                ExpectedBag = SaveText(back.Keys());
                Next("Put " + AddedKey + " into the worn bag aboard");
                return;
            }
            case 6:
                if (!Stable(() => DiskCheck() ?? (SaveText(BagInventory.Local.BackBag.Keys()) == ExpectedBag ? null : "bag contents not applied"))) return;
                Require(ExpectedBag.EndsWith("|" + AddedKey), "Unexpected contents " + ExpectedBag);
                Require(Grab(), "Could not grab the carried item again");
                Next("Bag contents change saved to disk immediately: " + ExpectedBag);
                session.FinishRun(false);
                return;
            // 3. Same-room swap Expedition -> Compound (after the failure results).
            case 7:
                if (InWorld(true) && PhotonSession.Phase == RailPhase.Running) Nudge(() => session.FinishRun(false));
                if (!InWorld(false) || PhotonSession.Phase != RailPhase.Staging) return;
                if (!Stable(() => Check(heldDropped: true, privateRoom: true))) return;
                Require(Grab(), "Could not grab the carried item again");
                Next("Private failure return: loadout kept with the new contents; held item dropped once; nothing left behind");
                session.StartPrivateRun();
                return;
            case 8:
                if (InWorld(false) && PhotonSession.Phase == RailPhase.Staging) Nudge(session.StartPrivateRun);
                if (!InWorld(true) || PhotonSession.Phase != RailPhase.Running) return;
                if (!Stable(() => Check(heldDropped: true, privateRoom: true))) return;
                Require(Grab(), "Could not grab the carried item again");
                Next("Second private departure: loadout kept; held item dropped once");
                session.LeaveCurrentRoom(); // The Room page's Back to Compound.
                return;
            // 4. Room change Expedition -> public Compound.
            case 9:
                if (!InWorld(false) || PhotonSession.IsPrivate) return;
                if (!Stable(() => Check(heldDropped: true))) return;
                Require(Grab(), "Could not grab the carried item again");
                Code = UnityEngine.Random.Range(70000, 79999).ToString();
                Next("Back to Compound mid-run: loadout kept in a public Compound; held item dropped once");
                session.RequestCreatePrivate(Code);
                return;
            // 5. Room change Compound -> Compound.
            case 10:
                if (!InWorld(false) || !PhotonSession.IsPrivate || PhotonSession.Phase != RailPhase.Staging) return;
                if (!Stable(() => Check(heldDropped: true, privateRoom: true))) return;
                Require(Grab(), "Could not grab the carried item again");
                Next("Create private room from a public Compound: loadout kept; held item dropped once; nothing left behind");
                session.StartPrivateRun();
                return;
            // 6. Connection loss aboard: the player goes inactive and rejoins the same expedition.
            case 11:
                if (InWorld(false) && PhotonSession.Phase == RailPhase.Staging) Nudge(session.StartPrivateRun);
                if (!InWorld(true) || PhotonSession.Phase != RailPhase.Running || Now - Entered < 5) return;
                if (!Stable(() => Check())) return;
                Next("Aboard before connection loss (holding the carried item)");
                PhotonNetwork.NetworkingClient.SimulateConnectionLoss(true);
                return;
            case 12:
                if (PhotonNetwork.IsConnected) return;
                PhotonNetwork.NetworkingClient.SimulateConnectionLoss(false);
                Next("Connection timed out aboard");
                return;
            case 13:
                if (!PhotonNetwork.InRoom || !PhotonSession.IsRailsRoom || !session.WorldReady || session.HasRoomRequest) return;
                if (!Stable(() => Check(privateRoom: PhotonNetwork.CurrentRoom.Name == Code))) return;
                Next("Reconnected to " + (PhotonNetwork.CurrentRoom.Name == Code ? "the same expedition" : "room " + PhotonNetwork.CurrentRoom.Name) +
                    ": worn bag, helmet and goggles back exactly once" + HeldReport());
                session.LeaveCurrentRoom();
                return;
            // 7. Connection loss in a public Compound (no reconnect slot: comes back from disk).
            case 14:
                if (!InWorld(false) || PhotonSession.IsPrivate) return;
                if (!Stable(() => Check())) return;
                Next("Public Compound before connection loss");
                PhotonNetwork.NetworkingClient.SimulateConnectionLoss(true);
                return;
            case 15:
                if (PhotonNetwork.IsConnected) return;
                PhotonNetwork.NetworkingClient.SimulateConnectionLoss(false);
                Next("Connection timed out in Compound");
                return;
            case 16:
                if (!InWorld(false)) return;
                if (!Stable(() => Check())) return;
                Next("Reconnected to Compound " + PhotonNetwork.CurrentRoom.Name + ": loadout back exactly once from disk");
                return;
            // 8. Quit and crash: snapshot the saves, stop Play Mode, check teardown changed nothing, relaunch.
            case 17:
                Require(DiskCheck() == null, DiskCheck());
                SnapshotSaves();
                Next("Saves current before quitting; stopping Play Mode");
                EditorApplication.isPlaying = false;
                return;
            case 18:
                if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
                string changed = CompareSnapshot();
                Require(changed == null, "Quitting changed the saves: " + changed);
                RestoreSnapshot(); // A crash runs no teardown: the disk holds exactly the pre-quit state.
                Next("Quit left the saves untouched (crash state identical); relaunching");
                EditorApplication.isPlaying = true;
                return;
            case 19:
                if (!InWorld(false)) return;
                GrantFocus();
                if (!Stable(() => Check())) return;
                Next("Relaunch: worn bag with contents, helmet and goggles restored exactly once");
                Finish();
                return;
        }
    }

    // ---------- Checks ----------

    // Aboard means the Expedition scene; otherwise the Compound scene (a public compound or a private room's staging).
    static bool InWorld(bool aboard)
    {
        var session = PhotonSession.Instance;
        return EditorApplication.isPlaying && session != null && PhotonNetwork.InRoom && PhotonSession.IsRailsRoom &&
            session.WorldReady && !session.HasRoomRequest && PhotonSession.OnTrain == aboard &&
            UserPlayerPrefs.IsReady && GR.Player.instance != null && BagInventory.Local != null;
    }

    // Passes once the check has held continuously for StableSeconds.
    static bool Stable(Func<string> check)
    {
        string failure = check();
        if (failure != null)
        {
            if (OkSince >= 0) Note("  (check flipped back to failing: " + failure + ")");
            OkSince = -1; Last = failure; return false;
        }
        if (OkSince < 0) OkSince = Now;
        return Now - OkSince >= StableSeconds;
    }

    static string Check(bool heldInHand = false, bool heldDropped = false, bool privateRoom = false)
    {
        int me = PhotonNetwork.LocalPlayer.ActorNumber;
        var worn = BagObject.Active.Where(b => b != null && b.State == BagState.Stowed && b.HolderActor == me).ToList();
        if (worn.Count != 1) return "worn bags: " + worn.Count;
        var back = BagInventory.Local.BackBag;
        if (back != worn[0]) return "BagInventory.BackBag is not the worn bag";
        string contents = SaveText(back.Keys());
        if (back.BagId != BagId || contents != ExpectedBag) return "worn bag " + back.BagId + " " + contents + ", expected " + ExpectedBag;
        if (privateRoom)
        {
            int copies = BagObject.Active.Count(b => b != null && SaveText(b.Keys()) == ExpectedBag);
            if (copies != 1) return "room has " + copies + " copies of the test bag";
        }

        var anchor = GR.Player.instance.helmetAnchor;
        var head = anchor.GetComponentsInChildren<HeadEquipment>(true).Where(h => h.IsLocallyEquipped).ToList();
        foreach (var (slot, key) in new[] { (HeadEquipment.EquipmentSlot.Head, Helmet), (HeadEquipment.EquipmentSlot.Eyes, Eyes) })
        {
            var inSlot = head.Where(h => h.Slot == slot).ToList();
            if (inSlot.Count != 1 || EquipmentInventory.PrefabKey(inSlot[0]) != key)
                return slot + " worn: [" + string.Join(",", inSlot.Select(EquipmentInventory.PrefabKey)) + "], expected " + key;
            if (privateRoom)
            {
                int copies = UnityEngine.Object.FindObjectsByType<HeadEquipment>(FindObjectsSortMode.None)
                    .Count(h => h.GetComponent<PhotonView>() is PhotonView v && v.ViewID != 0 && EquipmentInventory.PrefabKey(h) == key);
                if (copies != 1) return "room has " + copies + " " + key;
            }
        }

        string disk = DiskCheck();
        if (disk != null) return disk;

        var hand = GR.Player.instance.rightPlayerHand;
        if (heldInHand && (hand.Held == null || Key(hand.Held.name) != Held)) return "not holding " + Held;
        if (heldDropped)
        {
            var drops = Items(Held).Where(i => IsCarryDrop(i, me)).ToList();
            if (drops.Count != 1) return "carried " + Held + " drops: " + drops.Count;
            var drop = drops[0];
            if (drop.IsHeldByAnyPlayer) return "carried item arrived in a hand";
            float distance = Vector3.Distance(drop.transform.position, GR.Player.instance.transform.position);
            if (distance > 3f) return "carried item is " + distance.ToString("0.0") + " m away";
            if (privateRoom && Items(Held).Count() != 1) return "room has " + Items(Held).Count() + " " + Held + " (original left behind?)";
        }
        return null;
    }

    static string DiskCheck()
    {
        if (UserPlayerPrefs.GetString("bag.back", "") != ExpectedBag) return "bag.back on disk is '" + UserPlayerPrefs.GetString("bag.back", "") + "'";
        if (UserPlayerPrefs.GetString("equipment.head", "") != Helmet) return "equipment.head on disk is '" + UserPlayerPrefs.GetString("equipment.head", "") + "'";
        if (UserPlayerPrefs.GetString("equipment.eyes", "") != Eyes) return "equipment.eyes on disk is '" + UserPlayerPrefs.GetString("equipment.eyes", "") + "'";
        return null;
    }

    static IEnumerable<PhotonGrabbable> Items(string key) =>
        UnityEngine.Object.FindObjectsByType<PhotonGrabbable>(FindObjectsSortMode.None)
            .Where(i => i != null && Key(i.name) == key && i.GetComponent<PhotonView>().ViewID != 0);

    static bool IsCarryDrop(PhotonGrabbable item, int me)
    {
        var view = item.GetComponent<PhotonView>();
        return view.CreatorActorNr == me && view.InstantiationData is object[] data && data.Length > 0 && data[0] as string == CarryTag;
    }

    static string HeldReport()
    {
        var hand = GR.Player.instance.rightPlayerHand;
        int drops = Items(Held).Count();
        return " (held item after the drop-out: " + (hand.Held != null ? "still in hand" : drops + " in the room") + "; losing it is allowed)";
    }

    static bool WornAnything() =>
        BagInventory.Local.BackBag != null ||
        GR.Player.instance.helmetAnchor.GetComponentsInChildren<HeadEquipment>(true).Any(h => h.IsLocallyEquipped);

    // ---------- Actions ----------

    static void WearTestLoadout()
    {
        var player = GR.Player.instance;
        var anchor = player.helmetAnchor;
        RoomRpc.SpawnBag(BagId, BagKeys, player.transform.position + Vector3.up * .4f, Quaternion.identity, BagObject.Target.Back, storePurchase: true);
        foreach (string key in new[] { Helmet, Eyes })
        {
            var go = Spawn(key, anchor.position);
            RailRemoteBridge.Call(go.GetComponent<HeadEquipment>(), "Equip", anchor);
        }
        var held = Spawn(Held, player.rightPlayerHand.transform.position);
        Require(player.rightPlayerHand.ForceGrab(held.GetComponent<Grabbable>()), "Could not put " + Held + " in the right hand");
    }

    // Like a shop delivery: room lifetime, tagged so the Compound's leftover cleanup keeps it.
    static GameObject Spawn(string key, Vector3 position)
    {
        var prefab = BagObject.ResolvePrefab(key);
        Require(prefab != null, "Cannot resolve " + key);
        PhotonPreparePool.Register(prefab);
        var go = PhotonNetwork.InstantiateWithRoomLifetime(prefab.name, position, Quaternion.identity, 0,
            RailCargo.WithCompoundPurchase(new object[] { "loadout-live-test" }, PhotonNetwork.LocalPlayer.UserId));
        Require(go != null, "Could not spawn " + key);
        return go;
    }

    static bool Grab()
    {
        GrantFocus();
        var hand = GR.Player.instance.rightPlayerHand;
        if (hand.Held != null && Key(hand.Held.name) == Held) return true;
        int me = PhotonNetwork.LocalPlayer.ActorNumber;
        var item = Items(Held).FirstOrDefault(i => IsCarryDrop(i, me) && !i.IsHeldByAnyPlayer);
        return item != null && hand.ForceGrab(item);
    }

    static void ClearLoadout()
    {
        var player = GR.Player.instance;
        foreach (var hand in new[] { player.leftPlayerHand, player.rightPlayerHand })
            if (hand.Held != null && hand.Held.GetComponent<PhotonView>() is PhotonView v && v.IsMine) PhotonNetwork.Destroy(hand.Held.gameObject);
        var back = BagInventory.Local.BackBag;
        if (back != null && back.photonView.IsMine) PhotonNetwork.Destroy(back.gameObject);
        foreach (var item in player.helmetAnchor.GetComponentsInChildren<HeadEquipment>(true))
            if (item.IsLocallyEquipped && item.GetComponent<PhotonView>().IsMine) PhotonNetwork.Destroy(item.gameObject);
        foreach (string key in SaveKeys) UserPlayerPrefs.DeleteKey(key);
        UserPlayerPrefs.Save();
    }

    static void GrantFocus()
    {
        foreach (var handoff in UnityEngine.Object.FindObjectsByType<PhotonMasterHandoff>(FindObjectsSortMode.None))
            typeof(PhotonMasterHandoff).GetField("focused", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(handoff, true);
    }

    // ---------- Saves: account backup, and the quit/crash snapshot (raw scoped PlayerPrefs, usable outside Play Mode) ----------

    static void BackupSaves()
    {
        var scoped = SaveKeys.Select(UserPlayerPrefs.ScopedKey).ToArray();
        SessionState.SetString(P + "scoped", string.Join("\n", scoped));
        SessionState.SetString(P + "backup", Pack(scoped));
        Note("backed up " + scoped.Count(PlayerPrefs.HasKey) + " saved loadout keys for this account");
    }

    static void SnapshotSaves() => SessionState.SetString(P + "snapshot", Pack(Scoped()));
    static string[] Scoped() => SessionState.GetString(P + "scoped", "").Split('\n').Where(s => s.Length > 0).ToArray();

    static string CompareSnapshot()
    {
        var expected = Unpack(SessionState.GetString(P + "snapshot", ""));
        foreach (string key in Scoped())
        {
            expected.TryGetValue(key, out string want);
            string have = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
            if (want != have) return key + ": '" + want + "' became '" + have + "'";
        }
        return null;
    }

    static void RestoreSnapshot() => Apply(SessionState.GetString(P + "snapshot", ""));

    static void Apply(string packed)
    {
        var values = Unpack(packed);
        foreach (string key in Scoped())
            if (values.TryGetValue(key, out string value)) PlayerPrefs.SetString(key, value);
            else PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }

    // key \t present \t value, one per line; values never contain tabs or newlines.
    static string Pack(IEnumerable<string> keys) => string.Join("\n", keys.Select(k =>
        k + "\t" + (PlayerPrefs.HasKey(k) ? "1\t" + PlayerPrefs.GetString(k) : "0\t")));

    static Dictionary<string, string> Unpack(string packed)
    {
        var values = new Dictionary<string, string>();
        foreach (string line in packed.Split('\n'))
        {
            var parts = line.Split('\t');
            if (parts.Length == 3 && parts[1] == "1") values[parts[0]] = parts[2];
        }
        return values;
    }

    // ---------- Finish ----------

    static void Finish()
    {
        Cleanup();
        Save("PASS — single authenticated client; see the stage list above");
        Debug.Log("LOADOUT LIVE: PASS\n" + Log);
        End();
    }

    static void Fail(string message)
    {
        Note("FAIL " + message);
        try { Cleanup(); } catch (Exception ex) { Note("cleanup error: " + ex.Message); }
        Save("FAIL");
        End();
    }

    // Removes the test loadout and puts the account's own saved loadout back, which comes back on the next world.
    static void Cleanup()
    {
        if (EditorApplication.isPlaying && PhotonNetwork.InRoom && GR.Player.instance != null && BagInventory.Local != null)
        {
            ClearLoadout();
            foreach (var item in Items(Held).Where(i => i.GetComponent<PhotonView>().IsMine).ToList())
                PhotonNetwork.Destroy(item.gameObject);
        }
        if (PhotonNetwork.IsConnected) PhotonNetwork.NetworkingClient.SimulateConnectionLoss(false);
        string backup = SessionState.GetString(P + "backup", "");
        if (backup.Length > 0) { Apply(backup); Note("account loadout saves restored"); }
    }

    static void End()
    {
        Running = false;
        EditorApplication.update -= Tick;
        RailEditorQuickStart.Enabled = SessionState.GetBool(P + "quickStart", true);
        if (EditorApplication.isPlaying)
        {
            // Reopen once back in Edit Mode, with or without a domain reload on exiting Play Mode.
            EditorApplication.update -= ReopenWhenEditing;
            EditorApplication.update += ReopenWhenEditing;
            EditorApplication.isPlaying = false;
        }
        else
        {
            string reopen = SessionState.GetString(P + "reopen", "");
            SessionState.EraseString(P + "reopen");
            if (reopen.Length > 0 && EditorSceneManager.GetActiveScene().path != reopen && File.Exists(reopen)) EditorSceneManager.OpenScene(reopen);
        }
    }

    // ---------- Helpers ----------

    static string SaveText(string[] keys) => BagId + "|" + string.Join("|", keys);

    static string Key(string name)
    {
        int cut = name.IndexOf('(');
        return (cut > 0 ? name.Substring(0, cut) : name).Trim();
    }

    static string Describe()
    {
        var back = BagInventory.Local != null ? BagInventory.Local.BackBag : null;
        var head = GR.Player.instance != null ? GR.Player.instance.helmetAnchor.GetComponentsInChildren<HeadEquipment>(true)
            .Where(h => h.IsLocallyEquipped).Select(EquipmentInventory.PrefabKey) : Enumerable.Empty<string>();
        return "back " + (back != null ? SaveText(back.Keys()) : "none") + ", head [" + string.Join(",", head) + "]";
    }

    static string SessionSummary()
    {
        var s = PhotonSession.Instance;
        return s == null ? "no session" : "room " + PhotonNetwork.CurrentRoom?.Name + " kind " + PhotonSession.Kind + " phase " + PhotonSession.Phase +
            " ready " + s.WorldReady + " request " + s.HasRoomRequest + " status '" + s.Status + "' connected " + PhotonNetwork.IsConnected +
            (GR.Player.instance != null && BagInventory.Local != null ? "; " + Describe() : "");
    }

    static void Note(string line) { Log = Log + line + "\n"; }
    static void Save(string status)
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(Evidence, status + "\nstage=" + Stage + "\n" + Log);
    }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
#endif
