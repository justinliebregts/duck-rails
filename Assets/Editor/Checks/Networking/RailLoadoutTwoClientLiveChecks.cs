#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Photon.Pun;
using Unity.Multiplayer.PlayMode;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Opt-in two-client playtest for "Loadout survives scene changes, quits, crashes and disconnects" (playtest-queue.md).
/// Player2 is the mover: it wears a basic bag with contents, a helmet and goggles (delivered through RoomRpc.SpawnBag and
/// HeadEquipment.Equip, as RailLoadoutLiveChecks does). The main editor is the observer and, in the private room, the master.
/// After each transition the observer must see exactly one copy of each item, worn by the mover's avatar, and nothing left in a
/// room the mover left. Drop-outs: one simulated transport loss, a clean Back to Compound, and a client disconnect while the
/// bag is held in a hand (the held bag must stay in the room; worn head gear must not). Each check holds for 5 s.
/// Both accounts' saved loadout keys are backed up first and restored at the end, pass or fail.
/// Logs: Temp/rails-loadout-2client/{main,player2}.txt. Runtime game code is not instrumented. No diagnostic report access.
/// </summary>
[InitializeOnLoad]
public static class RailLoadoutTwoClientLiveChecks
{
    [Serializable] sealed class Control
    {
        public string token, expires, compound, privateCode;
        public int step, revision;
    }
    [Serializable] sealed class Receipt
    {
        public string token, room, note, error, bag;
        public int revision, actor;
    }
    const string Key = "Rails.LoadoutTwoClient";
    static readonly string[] Roles = { "main", "player2" };
    static string Root { get { var p = Application.dataPath.Replace('\\', '/'); var i = p.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase); return i < 0 ? Path.GetDirectoryName(p) : p.Substring(0, i); } }
    static string Folder => Root + "/Temp/rails-loadout-2client";
    static string ControlPath => Folder + "/control.json";
    static bool Host => CurrentPlayer.IsMainEditor;
    static string Role => Host ? "main" : "player2";
    static string ReceiptPath(string role) => Folder + "/" + role + ".json";
    static string BackupPath(string role) => Folder + "/" + role + "-backup.txt";

    const byte BagId = 2; // Basic bag (store item bag_basic).
    static readonly string[] BagKeys = { "RailCoal", "RailGoldBar", "RailShopItemFlashlight", "" };
    const string Helmet = "RailShopItemHelmet";
    const string Eyes = "RailGoggles";
    static readonly string[] SaveKeys = { "bag.back", "bag.back.room", "bag.back.identity",
        "equipment.head", "equipment.head.room", "equipment.head.identity",
        "equipment.eyes", "equipment.eyes.room", "equipment.eyes.identity" };
    const double StableSeconds = 5, OfflineSeconds = 15;
    static readonly FieldInfo SessionRetryAt = typeof(PhotonSession).GetField("retryAt", BindingFlags.Instance | BindingFlags.NonPublic);

    static Control control;
    static string token, networkState, expectedBag = "", last = "";
    static int revision = -1, sub;
    static bool action, ack, done, backedUp, simulation, disconnected, verifiedAway;
    static double entered, nextTick, readySince, okSince = -1, faultStarted, awaySince, acted, restoreAt;
    static int[] savedSimulation;

    static RailLoadoutTwoClientLiveChecks()
    {
        EditorApplication.update += Tick;
        EditorApplication.update += Restore;
        EditorApplication.playModeStateChanged += s =>
        {
            if (s != PlayModeStateChange.ExitingPlayMode) return;
            RestoreSimulation();
            RestoreBackup("exiting Play Mode");
        };
    }

    static ScriptableObject Manager => Resources.FindObjectsOfTypeAll<ScriptableObject>().FirstOrDefault(o => o.GetType().FullName == "Unity.PlayMode.Editor.PlayModeManager");
    public static string Status => Manager?.GetType().GetProperty("CurrentState")?.GetValue(Manager)?.ToString() ?? "Unavailable";

    /// <summary>Main editor, Edit Mode: arm and launch the two-client scenario.</summary>
    public static void Start()
    {
        if (!CurrentPlayer.IsMainEditor || EditorApplication.isPlayingOrWillChangePlaymode || Status != "NotRunning") throw new Exception("Main editor in Edit Mode with no scenario running");
        if (!MppmStandaloneCredentials.TryGet("Player2", out _)) throw new Exception("Configure Player2 account");
        Directory.CreateDirectory(Folder);
        foreach (var role in Roles)
            if (File.Exists(BackupPath(role)) && File.ReadAllText(BackupPath(role)).Length > 0)
                throw new Exception("An unrestored loadout backup exists for " + role + "; run RestoreBackupsInEditMode first");
        foreach (var role in Roles) { File.WriteAllText(ReceiptPath(role), ""); File.WriteAllText(Folder + "/" + role + ".txt", ""); }
        var property = Manager.GetType().GetProperty("ActivePlayModeConfig");
        SessionState.SetString(Key + ".scenario", AssetDatabase.GetAssetPath((UnityEngine.Object)property.GetValue(Manager)));
        SessionState.SetBool(Key + ".quick", RailEditorQuickStart.Enabled);
        SessionState.SetBool(Key, true);
        RailEditorQuickStart.Enabled = false;
        var c = new Control { token = Guid.NewGuid().ToString("N"), expires = DateTime.UtcNow.AddMinutes(60).ToString("O"),
            privateCode = UnityEngine.Random.Range(10000, 99999).ToString() };
        File.WriteAllText(ControlPath, JsonUtility.ToJson(c));
        property.SetValue(Manager, AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Settings/PlayMode/4 Diagnostic Two Client.asset"));
        Manager.GetType().GetMethod("Start", Type.EmptyTypes).Invoke(Manager, null);
    }

    public static void Stop()
    {
        if (File.Exists(ControlPath)) File.WriteAllText(ControlPath, "");
        RestoreSimulation();
        if (Status == "Running") Manager.GetType().GetMethod("Stop", Type.EmptyTypes).Invoke(Manager, null);
    }

    /// <summary>Edit Mode fallback: write back any backup a stopped run left unrestored (raw scoped PlayerPrefs keys).</summary>
    public static string RestoreBackupsInEditMode()
    {
        string result = "";
        foreach (var role in Roles)
        {
            string path = BackupPath(role);
            if (!File.Exists(path) || File.ReadAllText(path).Length == 0) { result += role + ": none; "; continue; }
            Apply(File.ReadAllText(path));
            File.WriteAllText(path, "");
            result += role + ": restored; ";
        }
        return result;
    }

    static void Restore()
    {
        if (!CurrentPlayer.IsMainEditor || !SessionState.GetBool(Key, false)) return;
        if (Status != "NotRunning" || EditorApplication.isPlayingOrWillChangePlaymode) return;
        RailEditorQuickStart.Enabled = SessionState.GetBool(Key + ".quick", true);
        string scenario = SessionState.GetString(Key + ".scenario", "");
        if (scenario.Length > 0) Manager.GetType().GetProperty("ActivePlayModeConfig").SetValue(Manager, AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(scenario));
        SessionState.SetBool(Key, false);
    }

    // ---------- Logging and coordination ----------

    static void Log(string message)
    {
        try { File.AppendAllText(Folder + "/" + Role + ".txt", DateTime.UtcNow.ToString("O") + " " + message + "\n"); } catch (IOException) { }
    }
    static string Where() => "room=" + PhotonNetwork.CurrentRoom?.Name + " kind=" + PhotonSession.Kind + " phase=" + PhotonSession.Phase
        + " request=" + PhotonSession.Instance?.HasRoomRequest + " step=" + control?.step;
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Next(int step) { control.step = step; control.revision++; File.WriteAllText(ControlPath, JsonUtility.ToJson(control)); }
    static void Ack(string message)
    {
        if (ack) return;
        File.WriteAllText(ReceiptPath(Role), JsonUtility.ToJson(new Receipt { token = token, revision = revision, room = PhotonNetwork.CurrentRoom?.Name, note = message,
            bag = expectedBag, actor = PhotonNetwork.LocalPlayer?.ActorNumber ?? 0 }));
        Log("ACK step=" + control.step + " " + message);
        ack = true;
    }
    static Receipt ReadReceipt(string role)
    {
        if (!File.Exists(ReceiptPath(role))) return null;
        string json = File.ReadAllText(ReceiptPath(role));
        if (string.IsNullOrEmpty(json)) return null;
        var receipt = JsonUtility.FromJson<Receipt>(json);
        return receipt != null && receipt.token == token ? receipt : null;
    }
    static bool Acked(string role) => ReadReceipt(role)?.revision == revision;
    static bool AllAck => Roles.All(Acked);
    static void Check(string name, string detail)
    {
        var m = ReadReceipt("main"); var p = ReadReceipt("player2");
        Log("CHECK " + name + " PASS | observer(main): " + m?.note + " | mover(player2): " + p?.note);
    }

    // ---------- Session helpers (as RailDepartureHornLiveChecks) ----------

    static bool Ready => PhotonSession.Instance != null && PhotonSession.IsRailsRoom && PhotonSession.Instance.WorldReady && !PhotonSession.Instance.HasRoomRequest
        && GR.Player.instance != null && BagInventory.Local != null;
    static int ActivePlayers => PhotonNetwork.PlayerList.Count(p => !p.IsInactive);
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
    static bool InCompound => PhotonSession.Kind == RailSessionRules.CompoundKind;
    static void Teleport(Vector3 position) => NewPlayer.instance.Warp(position, Quaternion.identity);
    static void StandAway() => Teleport(new Vector3(8 + Array.IndexOf(Roles, Role), .4f, 4));
    static void StandInBoarding()
    {
        if (Ready && RailWorld.Instance != null && RailWorld.Instance.boardingTrigger != null)
            Teleport(RailWorld.Instance.boardingTrigger.bounds.center - Vector3.up * .8f);
    }
    static bool Gather()
    {
        if (!Ready) return false;
        if (PhotonNetwork.CurrentRoom.Name != control.compound)
        {
            double age = EditorApplication.timeSinceStartup - entered;
            if (!Host && InCompound && age > 2 && (!action || age % 15 < .1)) { PhotonSession.Instance.RequestJoinCode(control.compound); action = true; readySince = 0; }
            return false;
        }
        if (!Settled || ActivePlayers != 2 || !AllPoses) return false;
        StandAway();
        return true;
    }
    // Phase commits can lose a CAS in a busy room; re-issue every four seconds.
    static void Nudge(Action request)
    {
        if (EditorApplication.timeSinceStartup - acted < 4) return;
        acted = EditorApplication.timeSinceStartup;
        request();
    }
    static bool Stable(Func<string> check)
    {
        string failure = check();
        double now = EditorApplication.timeSinceStartup;
        if (failure != null)
        {
            if (okSince >= 0) Log("  (check flipped back to failing: " + failure + ")");
            okSince = -1; last = failure; return false;
        }
        if (okSince < 0) okSince = now;
        return now - okSince >= StableSeconds;
    }
    static void GrantFocus()
    {
        foreach (var handoff in UnityEngine.Object.FindObjectsByType<PhotonMasterHandoff>(FindObjectsSortMode.None))
            typeof(PhotonMasterHandoff).GetField("focused", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(handoff, true);
    }

    // ---------- Transport fault (as RailDepartureSettleLiveChecks) ----------

    static void Simulate()
    {
        var peer = PhotonNetwork.NetworkingClient.LoadBalancingPeer;
        var s = peer.NetworkSimulationSettings;
        Require(!peer.IsSimulationEnabled, "Network simulation already enabled");
        savedSimulation = new[] { s.IncomingLossPercentage, s.OutgoingLossPercentage };
        s.IncomingLossPercentage = s.OutgoingLossPercentage = 100;
        peer.IsSimulationEnabled = true;
        simulation = true;
        Log("FAULT 100% transport loss " + Where());
    }
    static void RestoreSimulation()
    {
        if (!simulation) return;
        var peer = PhotonNetwork.NetworkingClient.LoadBalancingPeer;
        peer.IsSimulationEnabled = false;
        peer.NetworkSimulationSettings.IncomingLossPercentage = savedSimulation[0];
        peer.NetworkSimulationSettings.OutgoingLossPercentage = savedSimulation[1];
        simulation = false;
        Log("FAULT restored transport");
    }

    // ---------- Gear: inspection ----------

    static string Text(BagObject bag) => bag.BagId + "|" + string.Join("|", bag.Keys());
    static string Text(string[] keys) => BagId + "|" + string.Join("|", keys);
    static readonly FieldInfo EquippedActor = typeof(HeadEquipment).GetField("equippedActorNumber", BindingFlags.Instance | BindingFlags.NonPublic);
    static List<BagObject> TestBags() => BagObject.Active.Where(b => b != null && b.photonView != null && b.photonView.ViewID != 0 && Text(b) == expectedBag).ToList();
    static List<HeadEquipment> Gear(string key) => UnityEngine.Object.FindObjectsByType<HeadEquipment>(FindObjectsSortMode.None)
        .Where(h => h.GetComponent<PhotonView>() is PhotonView v && v.ViewID != 0 && EquipmentInventory.PrefabKey(h) == key).ToList();
    static int ViewId(Component c) => c.GetComponent<PhotonView>().ViewID;

    // The other client: active if present, otherwise inactive.
    static Photon.Realtime.Player Mover() => PhotonNetwork.PlayerList.Where(p => !p.IsLocal).OrderBy(p => p.IsInactive).FirstOrDefault();
    static bool MoverAway() { var m = Mover(); return m == null || m.IsInactive; }

    enum Seen { Worn, Gone, BagDroppedHeadGone, BagDroppedHeadWorn }

    /// <summary>Observer: what this client sees of the mover's gear. Null when as expected; detail describes what was measured.</summary>
    static string Observe(Seen expect, out string detail)
    {
        detail = "";
        var bags = TestBags();
        var helmets = Gear(Helmet);
        var goggles = Gear(Eyes);
        var mover = Mover();
        int actor = mover?.ActorNumber ?? 0;
        if (expect == Seen.Gone || expect == Seen.BagDroppedHeadGone)
        {
            if (helmets.Count != 0 || goggles.Count != 0) return "head gear left in room: helmets " + helmets.Count + " goggles " + goggles.Count;
            if (expect == Seen.Gone)
            {
                if (bags.Count != 0) return "test bag copies left in room: " + bags.Count;
                detail = "no test bag, helmet or goggles copy in room " + PhotonNetwork.CurrentRoom?.Name + "; mover " + (mover == null ? "absent" : "#" + actor + (mover.IsInactive ? " inactive" : " active"));
                return null;
            }
        }
        if (expect == Seen.BagDroppedHeadGone || expect == Seen.BagDroppedHeadWorn)
        {
            if (bags.Count != 1) return "room has " + bags.Count + " copies of the test bag (want 1 dropped)";
            var bag = bags[0];
            if (bag.State != BagState.Dropped) return "test bag state " + bag.State + " holder #" + bag.HolderActor + " (want Dropped)";
            detail = "1 bag dropped in room (view " + ViewId(bag) + ", " + Text(bag) + ")";
            if (expect == Seen.BagDroppedHeadGone) { detail += "; no helmet or goggles copy; mover " + (mover == null ? "absent" : "#" + actor + (mover.IsInactive ? " inactive" : " active")); return null; }
        }
        if (mover == null || mover.IsInactive) return "mover not active";
        var avatar = PhotonPlayer.ForActor(actor);
        if (avatar == null || avatar.helmetAnchor == null) return "no avatar for mover #" + actor;
        Vector3 head = avatar.helmetAnchor.transform.position;
        if (expect == Seen.Worn)
        {
            if (bags.Count != 1) return "room has " + bags.Count + " copies of the test bag";
            var bag = bags[0];
            if (bag.State != BagState.Stowed || bag.HolderActor != actor) return "test bag state " + bag.State + " holder #" + bag.HolderActor + ", mover #" + actor;
            float d = Vector3.Distance(bag.transform.position, head);
            if (d > 1.2f) return "worn bag is " + d.ToString("F2") + " m from the mover's head";
            detail = "bag view " + ViewId(bag) + " " + Text(bag) + " stowed on #" + actor + " (" + d.ToString("F2") + " m from head)";
        }
        foreach (var (name, list) in new[] { ("helmet", helmets), ("goggles", goggles) })
        {
            if (list.Count != 1) return "room has " + list.Count + " " + name;
            var item = list[0];
            int wearer = (int)EquippedActor.GetValue(item);
            if (!item.IsEquipped || wearer != actor) return name + " equipped " + item.IsEquipped + " by #" + wearer + ", mover #" + actor;
            float d = Vector3.Distance(item.transform.position, head);
            if (d > .05f) return name + " is " + d.ToString("F3") + " m from the mover's head anchor";
            detail += (detail.Length > 0 ? "; " : "") + name + " view " + ViewId(item) + " on #" + actor + " (" + d.ToString("F3") + " m)";
        }
        return null;
    }

    /// <summary>Mover: own view of its worn gear and disk saves. wornBag false: the bag is not worn (and not saved).</summary>
    static string SelfCheck(out string detail, bool wornBag = true)
    {
        detail = "";
        int me = PhotonNetwork.LocalPlayer.ActorNumber;
        var worn = BagObject.Active.Where(b => b != null && b.State == BagState.Stowed && b.HolderActor == me).ToList();
        var back = BagInventory.Local.BackBag;
        if (wornBag)
        {
            if (worn.Count != 1) return "worn bags: " + worn.Count;
            if (back != worn[0]) return "BagInventory.BackBag is not the worn bag";
            if (Text(back) != expectedBag) return "worn bag " + Text(back) + ", expected " + expectedBag;
            int copies = TestBags().Count;
            if (copies != 1) return "room has " + copies + " copies of the test bag";
            if (UserPlayerPrefs.GetString("bag.back", "") != expectedBag) return "bag.back on disk is '" + UserPlayerPrefs.GetString("bag.back", "") + "'";
            detail = "wearing bag view " + ViewId(back) + " " + expectedBag;
        }
        else
        {
            if (worn.Count != 0 || back != null) return "unexpected worn bag";
            if (UserPlayerPrefs.GetString("bag.back", "").Length != 0) return "bag.back on disk is '" + UserPlayerPrefs.GetString("bag.back", "") + "'";
            detail = "no worn bag, bag.back save empty";
        }
        var anchor = GR.Player.instance.helmetAnchor;
        var head = anchor.GetComponentsInChildren<HeadEquipment>(true).Where(h => h.IsLocallyEquipped).ToList();
        foreach (var (slot, key, saveKey) in new[] { (HeadEquipment.EquipmentSlot.Head, Helmet, "equipment.head"), (HeadEquipment.EquipmentSlot.Eyes, Eyes, "equipment.eyes") })
        {
            var inSlot = head.Where(h => h.Slot == slot).ToList();
            if (inSlot.Count != 1 || EquipmentInventory.PrefabKey(inSlot[0]) != key)
                return slot + " worn: [" + string.Join(",", inSlot.Select(EquipmentInventory.PrefabKey)) + "], expected " + key;
            int copies = Gear(key).Count;
            if (copies != 1) return "room has " + copies + " " + key;
            if (UserPlayerPrefs.GetString(saveKey, "") != key) return saveKey + " on disk is '" + UserPlayerPrefs.GetString(saveKey, "") + "'";
            detail += "; " + key + " view " + ViewId(inSlot[0]);
        }
        detail += " (actor #" + me + ", room " + PhotonNetwork.CurrentRoom.Name + ")";
        return null;
    }

    static bool WornAnything() =>
        BagInventory.Local.BackBag != null ||
        GR.Player.instance.helmetAnchor.GetComponentsInChildren<HeadEquipment>(true).Any(h => h.IsLocallyEquipped);

    // ---------- Gear: actions (delivery and equip paths, no purchases) ----------

    static void WearTestLoadout()
    {
        var player = GR.Player.instance;
        var anchor = player.helmetAnchor;
        RoomRpc.SpawnBag(BagId, BagKeys, player.transform.position + Vector3.up * .4f, Quaternion.identity, BagObject.Target.Back, storePurchase: true);
        foreach (string key in new[] { Helmet, Eyes })
        {
            var prefab = BagObject.ResolvePrefab(key);
            Require(prefab != null, "Cannot resolve " + key);
            PhotonPreparePool.Register(prefab);
            var go = PhotonNetwork.InstantiateWithRoomLifetime(prefab.name, anchor.position, Quaternion.identity, 0,
                RailCargo.WithCompoundPurchase(new object[] { "loadout-2client-test" }, PhotonNetwork.LocalPlayer.UserId));
            Require(go != null, "Could not spawn " + key);
            RailRemoteBridge.Call(go.GetComponent<HeadEquipment>(), "Equip", anchor);
        }
    }

    // The grip path's BagInventory.Hold: takes the worn bag off the back into the right hand (clears the bag.back save).
    static void HoldBackBagInHand()
    {
        var inventory = BagInventory.Local;
        var back = inventory.BackBag;
        Require(back != null, "No worn bag to take off");
        var hold = typeof(BagInventory).GetMethod("Hold", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(BagObject), typeof(PlayerHand) }, null);
        hold.Invoke(inventory, new object[] { back, GR.Player.instance.rightPlayerHand });
    }

    static void ClearLoadout()
    {
        var player = GR.Player.instance;
        if (player == null || BagInventory.Local == null) return;
        var back = BagInventory.Local.BackBag;
        if (back != null && back.photonView.IsMine) PhotonNetwork.Destroy(back.gameObject);
        foreach (var item in player.helmetAnchor.GetComponentsInChildren<HeadEquipment>(true))
            if (item.IsLocallyEquipped && item.GetComponent<PhotonView>().IsMine) PhotonNetwork.Destroy(item.gameObject);
        foreach (string key in SaveKeys) UserPlayerPrefs.DeleteKey(key);
        UserPlayerPrefs.Save();
    }

    static void DestroyTestGear()
    {
        if (!PhotonNetwork.InRoom) return;
        if (expectedBag.Length > 0)
            foreach (var bag in TestBags()) if (bag.photonView.IsMine) PhotonNetwork.Destroy(bag.gameObject);
        foreach (var item in Gear(Helmet).Concat(Gear(Eyes)))
            if (item.GetComponent<PhotonView>().IsMine) PhotonNetwork.Destroy(item.gameObject);
    }

    // ---------- Account backup (raw scoped PlayerPrefs, as RailLoadoutLiveChecks) ----------

    static void BackupSaves()
    {
        var scoped = SaveKeys.Select(UserPlayerPrefs.ScopedKey).ToArray();
        File.WriteAllText(BackupPath(Role), Pack(scoped));
        Log("BACKUP " + scoped.Count(PlayerPrefs.HasKey) + " of " + scoped.Length + " saved loadout keys present for this account");
    }
    static void RestoreBackup(string why)
    {
        try
        {
            string path = BackupPath(Role);
            if (!File.Exists(path)) return;
            string packed = File.ReadAllText(path);
            if (packed.Length == 0) return;
            Apply(packed);
            File.WriteAllText(Folder + "/" + Role + "-backup-applied.txt", packed);
            File.WriteAllText(path, "");
            Log("RESTORED account loadout saves (" + why + ")");
        }
        catch (IOException) { }
    }
    static void Apply(string packed)
    {
        foreach (string line in packed.Split('\n'))
        {
            var parts = line.Split('\t');
            if (parts.Length != 3 || parts[0].Length == 0) continue;
            if (parts[1] == "1") PlayerPrefs.SetString(parts[0], parts[2]);
            else PlayerPrefs.DeleteKey(parts[0]);
        }
        PlayerPrefs.Save();
    }
    static string Pack(IEnumerable<string> keys) => string.Join("\n", keys.Select(k =>
        k + "\t" + (PlayerPrefs.HasKey(k) ? "1\t" + PlayerPrefs.GetString(k) : "0\t")));

    // Destroying worn gear clears its save when it is disabled (end of frame), so the backup goes back a little later.
    static void Finish(string message)
    {
        done = true;
        RestoreSimulation();
        try { DestroyTestGear(); } catch (Exception e) { Log("cleanup error: " + e.Message); }
        restoreAt = EditorApplication.timeSinceStartup + 2;
        Log(message);
    }

    // ---------- Step machine ----------

    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextTick) return;
        nextTick = EditorApplication.timeSinceStartup + .1;
        if (restoreAt > 0 && EditorApplication.timeSinceStartup > restoreAt) { restoreAt = 0; RestoreBackup("run finished"); }
        try
        {
            if (!File.Exists(ControlPath)) { RestoreSimulation(); return; }
            string json = File.ReadAllText(ControlPath);
            if (string.IsNullOrEmpty(json)) { RestoreSimulation(); control = null; return; }
            try { control = JsonUtility.FromJson<Control>(json); } catch (ArgumentException) { return; }
            if (control == null || !DateTime.TryParse(control.expires, out var expires) || DateTime.UtcNow > expires.ToUniversalTime()) { control = null; return; }
            if (token != control.token)
            {
                token = control.token; revision = -1; done = false; backedUp = false; expectedBag = "";
                Log("START loadout two-client playtest; Unity " + Application.unityVersion + " role=" + Role + " privateCode=" + control.privateCode);
            }
            if (done) return;
            string state = PhotonNetwork.NetworkClientState + " " + Where() + " ready=" + PhotonSession.Instance?.WorldReady + " players=" + (PhotonNetwork.InRoom ? ActivePlayers + "/" + PhotonNetwork.PlayerList.Length : "0") + " actor=" + PhotonNetwork.LocalPlayer?.ActorNumber;
            if (state != networkState) { networkState = state; Log("STATE " + state); }
            foreach (string role in Roles)
                if (ReadReceipt(role)?.error is string error && error.Length > 0) throw new Exception("Peer " + role + " failure: " + error);
            if (revision != control.revision)
            {
                revision = control.revision; entered = EditorApplication.timeSinceStartup;
                action = ack = disconnected = verifiedAway = false; readySince = 0; okSince = -1; last = ""; sub = 0; faultStarted = 0; awaySince = 0; acted = 0;
                Log("step=" + control.step);
            }
            double age = EditorApplication.timeSinceStartup - entered;
            Require(age < 300, "Timeout step=" + control.step + " last check: " + last + " " + Where() + " status=" + PhotonSession.Instance?.Status);
            Step(age);
        }
        catch (IOException) { }
        catch (Exception e)
        {
            Finish("FAIL " + e.Message);
            File.WriteAllText(ReceiptPath(Role), JsonUtility.ToJson(new Receipt { token = token, revision = revision, error = e.Message }));
        }
    }

    static void Step(double age)
    {
        var session = PhotonSession.Instance;
        string detail;
        switch (control.step)
        {
            case 0: // Each client: back up and clear its account's loadout; host opens a fresh public Compound.
                if (!Settled || age < 8) break;
                if (!backedUp) { BackupSaves(); GrantFocus(); ClearLoadout(); backedUp = true; Log("CLEARED own loadout " + Where()); break; }
                if (Host)
                {
                    if (!action) { session.RequestCreatePublic(); action = true; readySince = 0; break; }
                    if (!InCompound || !Settled) break;
                    control.compound = PhotonNetwork.CurrentRoom.Name;
                }
                if (!Stable(() => WornAnything() ? "loadout still worn after clearing" : null)) break;
                Ack("account loadout backed up and cleared");
                if (Host && AllAck) Next(1);
                break;
            case 1: // Gather in the host's public Compound.
                if (!Gather()) break;
                Ack("gathered in " + control.compound);
                if (Host && AllAck) Next(2);
                break;
            case 2: // Mover puts on the test loadout; observer sees one copy of each on the mover.
                if (!Host)
                {
                    if (!action) { GrantFocus(); WearTestLoadout(); action = true; Log("WEAR test loadout requested"); break; }
                    if (expectedBag.Length == 0)
                    {
                        var back = BagInventory.Local.BackBag;
                        if (back == null) { last = "test bag not on the back yet"; break; }
                        var keys = back.Keys();
                        Require(back.BagId == BagId && keys.Length >= 3 && keys[0] == BagKeys[0] && keys[1] == BagKeys[1] && keys[2] == BagKeys[2] && keys.Skip(3).All(k => k == ""),
                            "Delivered bag " + Text(back) + " does not hold the test contents");
                        expectedBag = Text(back);
                        Log("delivered bag " + expectedBag);
                    }
                    string d2 = "";
                    if (!Stable(() => SelfCheck(out d2))) break;
                    Ack(d2);
                    break;
                }
                if (Ready) StandAway();
                if (!Acked("player2")) break;
                expectedBag = ReadReceipt("player2").bag;
                if (ObserveStable(Seen.Worn, out detail)) { Ack(detail); Check("baseline: public Compound, mover wears test loadout", detail); Next(3); }
                break;
            case 3: // Compound -> public Expedition (mover alone). Observer stays and sees nothing left behind.
                if (!Host)
                {
                    if (InCompound) { StandInBoarding(); break; }
                    if (PhotonSession.Phase != RailPhase.Running || !Settled) break;
                    string d3 = "";
                    if (!Stable(() => SelfCheck(out d3))) break;
                    Ack("aboard public expedition " + PhotonNetwork.CurrentRoom.Name + ": " + d3);
                    break;
                }
                if (InCompound && Ready) StandAway();
                if (!InCompound || ActivePlayers != 1) { okSince = -1; last = "mover still in the Compound"; break; }
                if (!ObserveStable(Seen.Gone, out detail) || !Acked("player2")) break;
                Ack("stayed in " + PhotonNetwork.CurrentRoom.Name + ": " + detail);
                Check("Compound -> public Expedition (mover departs alone; observer checks the old Compound)", detail);
                Next(4);
                break;
            case 4: // Expedition -> Compound via crew return (mover alone), then into the observer's Compound.
                if (!Host)
                {
                    if (!InCompound) { if (Ready && PhotonSession.Phase == RailPhase.Running && PhotonNetwork.IsMasterClient) Nudge(() => session.FinishRun(false)); break; }
                    if (sub == 0 && Settled) { sub = 1; Log("RETURNED by crew return to " + PhotonNetwork.CurrentRoom.Name + (PhotonNetwork.CurrentRoom.Name == control.compound ? " (the observer's Compound)" : " (a different Compound)")); }
                    if (sub == 1 && PhotonNetwork.CurrentRoom.Name != control.compound && Settled)
                    {
                        string d4a = "";
                        if (!Stable(() => SelfCheck(out d4a))) break;
                        Log("SELF after crew return in " + PhotonNetwork.CurrentRoom.Name + ": " + d4a);
                        sub = 2; okSince = -1; action = false;
                    }
                    if (sub == 0) break;
                    if (!Gather()) break;
                    string d4 = "";
                    if (!Stable(() => SelfCheck(out d4))) break;
                    Ack("in observer's Compound " + PhotonNetwork.CurrentRoom.Name + " (crew return " + (sub == 2 ? "landed elsewhere, then joined by code" : "landed here") + "): " + d4);
                    break;
                }
                if (Ready) StandAway();
                if (ActivePlayers != 2 || !AllPoses) { okSince = -1; last = "waiting for the mover"; break; }
                if (!ObserveStable(Seen.Worn, out detail) || !Acked("player2")) break;
                Ack(detail);
                Check("Expedition -> Compound via crew return (mover), observed in the Compound", detail);
                Next(5);
                break;
            case 5: // Both board a public departure together.
                if (InCompound) { StandInBoarding(); break; }
                if (PhotonSession.Phase != RailPhase.Running || !Settled || ActivePlayers != 2 || !AllPoses) { okSince = -1; break; }
                if (!Host)
                {
                    string d5 = "";
                    if (Stable(() => SelfCheck(out d5))) Ack(d5);
                    break;
                }
                if (!ObserveStable(Seen.Worn, out detail) || !Acked("player2")) break;
                Require(ReadReceipt("player2").room == PhotonNetwork.CurrentRoom.Name, "Crew split across expeditions");
                Ack(detail);
                Check("Compound -> public Expedition (crew boards together)", detail);
                Next(6);
                break;
            case 6: // Crew return together (master ends the run; nobody is downed).
                if (!InCompound) { if (Ready && PhotonSession.Phase == RailPhase.Running && PhotonNetwork.IsMasterClient) Nudge(() => session.FinishRun(false)); break; }
                if (!Settled || ActivePlayers != 2 || !AllPoses) { okSince = -1; break; }
                StandAway();
                if (!Host)
                {
                    string d6 = "";
                    if (Stable(() => SelfCheck(out d6))) Ack(d6);
                    break;
                }
                if (!ObserveStable(Seen.Worn, out detail) || !Acked("player2")) break;
                Require(ReadReceipt("player2").room == PhotonNetwork.CurrentRoom.Name, "Crew split on return");
                Ack(detail);
                Check("Expedition -> Compound via crew return (together)", detail);
                control.compound = PhotonNetwork.CurrentRoom.Name;
                Next(7);
                break;
            case 7: // Host creates the private room; mover waits in the public Compound.
                if (!Host)
                {
                    if (!Ready) break;
                    string d7 = "";
                    if (Stable(() => SelfCheck(out d7))) Ack("waiting in " + PhotonNetwork.CurrentRoom.Name + ": " + d7);
                    break;
                }
                if (!Ready) break;
                if (!action) { session.RequestCreatePrivate(control.privateCode); action = true; readySince = 0; break; }
                if (!PhotonSession.IsPrivate || !Settled || PhotonSession.Phase != RailPhase.Staging) break;
                Ack("created private " + PhotonNetwork.CurrentRoom.Name);
                if (AllAck) Next(8);
                break;
            case 8: // Mover joins by code (room change Compound -> private staging).
                if (!Host)
                {
                    if (!PhotonSession.IsPrivate && Ready && (!action || age % 15 < .1)) { session.RequestJoinCode(control.privateCode); action = true; readySince = 0; }
                    if (!PhotonSession.IsPrivate || PhotonSession.Phase != RailPhase.Staging || !Settled || ActivePlayers != 2) break;
                    string d8 = "";
                    if (Stable(() => SelfCheck(out d8))) Ack(d8);
                    break;
                }
                if (ActivePlayers != 2 || !AllPoses || !Settled) { okSince = -1; break; }
                if (!ObserveStable(Seen.Worn, out detail) || !Acked("player2")) break;
                Ack(detail);
                Check("public Compound -> private staging (mover joins by code)", detail);
                Next(9);
                break;
            case 9: case 11: // Private departure (same-room swap).
                if (Host && PhotonSession.Phase == RailPhase.Staging && Settled) Nudge(session.StartPrivateRun);
                if (PhotonSession.Phase != RailPhase.Running || !Settled || ActivePlayers != 2 || !AllPoses) { okSince = -1; break; }
                if (!Host) { string d9 = ""; if (Stable(() => SelfCheck(out d9))) Ack(d9); break; }
                if (!ObserveStable(Seen.Worn, out detail) || !Acked("player2")) break;
                Ack(detail);
                Check(control.step == 9 ? "private departure (staging -> Expedition, same room)" : "second private departure (before drop-outs)", detail);
                Next(control.step + 1);
                break;
            case 10: // Private return (same-room swap).
                if (Host && PhotonSession.Phase == RailPhase.Running && Settled) Nudge(() => session.FinishRun(false));
                if (PhotonSession.Phase != RailPhase.Staging || !Settled || ActivePlayers != 2 || !AllPoses) { okSince = -1; break; }
                if (!Host) { string d10 = ""; if (Stable(() => SelfCheck(out d10))) Ack(d10); break; }
                if (!ObserveStable(Seen.Worn, out detail) || !Acked("player2")) break;
                Ack(detail);
                Check("private return (Expedition -> staging, same room)", detail);
                Next(11);
                break;
            case 12: // Transport loss: the mover goes inactive, then rejoins the same run.
                if (!Host)
                {
                    if (!action) { Simulate(); faultStarted = EditorApplication.timeSinceStartup; action = true; break; }
                    double lossAge = EditorApplication.timeSinceStartup - faultStarted;
                    if (!PhotonNetwork.InRoom && !disconnected) { disconnected = true; Log("DISCONNECTED after " + lossAge.ToString("F1") + "s"); }
                    if (lossAge > 25) RestoreSimulation();
                    if (lossAge < 28 || !disconnected || !PhotonNetwork.InRoom || PhotonSession.Phase != RailPhase.Running || !Settled || ActivePlayers != 2 || !AllPoses) { okSince = -1; break; }
                    Require(PhotonNetwork.CurrentRoom.Name == control.privateCode, "Rejoined a different room " + PhotonNetwork.CurrentRoom.Name);
                    string d12 = "";
                    if (Stable(() => SelfCheck(out d12))) Ack("rejoined same run after " + lossAge.ToString("F1") + "s: " + d12);
                    break;
                }
                if (DropOutObserved(Seen.Gone, Seen.Worn, "transport loss"))
                {
                    Check("transport loss (inactive) -> rejoin, worn bag and head gear go with the mover", "");
                    Next(13);
                }
                break;
            case 13: // Clean leave mid-run (Back to Compound): nothing stays with the crew.
                if (!Host)
                {
                    if (!action) { if (!Settled) break; session.LeaveCurrentRoom(); action = true; Log("LEAVE Back to Compound"); break; }
                    if (!InCompound || !Settled) break;
                    StandAway();
                    string d13 = "";
                    if (Stable(() => SelfCheck(out d13))) Ack("left to public " + PhotonNetwork.CurrentRoom.Name + ": " + d13);
                    break;
                }
                if (!MoverAway()) { okSince = -1; last = "mover still present"; break; }
                if (!ObserveStable(Seen.Gone, out detail) || !Acked("player2")) break;
                Ack("crew room " + PhotonNetwork.CurrentRoom.Name + " phase " + PhotonSession.Phase + ": " + detail);
                Check("clean leave mid-run (Back to Compound): no room copy for the crew", detail);
                Next(14);
                break;
            case 14: // Mover rejoins the running private expedition by code.
                if (!Host)
                {
                    if (!PhotonSession.IsPrivate && Ready && (!action || age % 15 < .1)) { session.RequestJoinCode(control.privateCode); action = true; readySince = 0; }
                    if (!PhotonSession.IsPrivate || PhotonSession.Phase != RailPhase.Running || !Settled || ActivePlayers != 2 || !AllPoses) { okSince = -1; break; }
                    string d14 = "";
                    if (Stable(() => SelfCheck(out d14))) Ack(d14);
                    break;
                }
                if (ActivePlayers != 2 || !AllPoses) { okSince = -1; break; }
                if (!ObserveStable(Seen.Worn, out detail) || !Acked("player2")) break;
                Ack(detail);
                Check("rejoin the running expedition by code after leaving: one copy, worn by the mover", detail);
                Next(15);
                break;
            case 15: // Bag held in a hand, then the mover drops out (client disconnect): the bag stays, the head gear goes.
                if (!Host)
                {
                    if (sub == 0)
                    {
                        if (!Settled) break;
                        GrantFocus(); HoldBackBagInHand(); sub = 1; Log("HOLD worn bag taken into the right hand"); break;
                    }
                    if (sub == 1)
                    {
                        string held = null;
                        if (!Stable(() =>
                        {
                            var bags = TestBags();
                            if (bags.Count != 1) return "test bags " + bags.Count;
                            if (bags[0].State != BagState.Held || bags[0].HolderActor != PhotonNetwork.LocalPlayer.ActorNumber) return "bag state " + bags[0].State + " holder #" + bags[0].HolderActor;
                            if (BagInventory.Local.BackBag != null) return "bag still on back";
                            if (UserPlayerPrefs.GetString("bag.back", "").Length != 0) return "bag.back save not cleared";
                            held = "bag view " + ViewId(bags[0]) + " held in hand, bag.back save cleared";
                            return null;
                        })) break;
                        Log("HELD " + held + "; disconnecting (client drop-out, no transition capture)");
                        sub = 2; faultStarted = EditorApplication.timeSinceStartup; okSince = -1;
                        PhotonNetwork.Disconnect();
                        break;
                    }
                    if (!PhotonNetwork.InRoom && !disconnected) { disconnected = true; Log("DISCONNECTED " + PhotonNetwork.NetworkClientState + "; holding the automatic rejoin for " + OfflineSeconds + " s (stands in for a relaunch)"); }
                    // PhotonSession rejoins 2 s after a disconnect; keep the mover away long enough for the observer's 5 s check.
                    if (disconnected && EditorApplication.timeSinceStartup - faultStarted < OfflineSeconds && !PhotonNetwork.InRoom)
                        SessionRetryAt.SetValue(session, Time.realtimeSinceStartupAsDouble + .5);
                    if (!disconnected || !PhotonNetwork.InRoom || PhotonSession.Phase != RailPhase.Running || !Settled || ActivePlayers != 2 || !AllPoses) { okSince = -1; break; }
                    Require(PhotonNetwork.CurrentRoom.Name == control.privateCode, "Rejoined a different room " + PhotonNetwork.CurrentRoom.Name);
                    string d15 = "";
                    if (!Stable(() =>
                    {
                        string self = SelfCheck(out d15, wornBag: false);
                        if (self != null) return self;
                        var bags = TestBags();
                        if (bags.Count != 1 || bags[0].State != BagState.Dropped) return "room bags " + bags.Count + (bags.Count > 0 ? " state " + bags[0].State : "");
                        d15 += "; 1 test bag dropped in the room (view " + ViewId(bags[0]) + ")";
                        return null;
                    })) break;
                    Ack("rejoined after " + (EditorApplication.timeSinceStartup - faultStarted).ToString("F1") + "s: " + d15);
                    break;
                }
                if (sub == 0)
                {
                    var bags = TestBags();
                    if (bags.Count == 1 && bags[0].State == BagState.Held && bags[0].HolderActor == Mover()?.ActorNumber)
                    { sub = 1; Log("OBSERVED test bag held in the mover's hand (view " + ViewId(bags[0]) + ")"); }
                    break;
                }
                if (DropOutObserved(Seen.BagDroppedHeadGone, Seen.BagDroppedHeadWorn, "client disconnect with the bag in hand"))
                {
                    Check("hand-held bag drops for the room; worn head gear goes with the mover (client disconnect -> rejoin)", "");
                    Next(16);
                }
                break;
            case 16:
                Finish("COMPLETE all loadout two-client checks passed (role " + Role + ")");
                break;
        }
    }

    static bool ObserveStable(Seen expect, out string detail)
    {
        string d = "";
        bool ok = Stable(() => Observe(expect, out d));
        detail = d;
        return ok;
    }

    /// <summary>
    /// Observer side of a drop-out: while the mover is away (inactive or gone) the room must settle to `away` for 5 s;
    /// after the mover is back, `back` must hold for 5 s and the mover must have acknowledged.
    /// </summary>
    static bool DropOutObserved(Seen away, Seen back, string what)
    {
        double now = EditorApplication.timeSinceStartup;
        var mover = Mover();
        if (!verifiedAway)
        {
            if (!MoverAway()) { if (awaySince != 0) throw new Exception("Mover returned before the room was observed as expected for 5 s (" + what + "); last: " + last); okSince = -1; return false; }
            if (awaySince == 0) { awaySince = now; Log("AWAY mover " + (mover == null ? "left the room" : "#" + mover.ActorNumber + " inactive") + " (" + what + ")"); }
            string d = "";
            if (!Stable(() => Observe(away, out d))) return false;
            verifiedAway = true; okSince = -1;
            Log("AWAY CHECK PASS after " + (now - awaySince).ToString("F1") + "s away: " + d);
            return false;
        }
        if (MoverAway() || !AllPoses) { okSince = -1; last = "waiting for the mover to return"; return false; }
        if (!ObserveStable(back, out string detail) || !Acked("player2")) return false;
        Ack("mover back as #" + Mover().ActorNumber + " after " + (now - awaySince).ToString("F1") + "s: " + detail);
        return true;
    }
}
#endif
