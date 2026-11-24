#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Photon.Pun;
using Unity.Multiplayer.PlayMode;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Two-client Compound and staging checks (playtest-queue.md: "Items disappear when taken out of a bag in Compound",
/// "Quackbear cupcake light and 8-slot bag", and the gold bar deposit bullet of "Compound and moving-train fixes").
/// Main drives its own items; Player2 observes them, except the gold deposit, which Player2 performs. Same control.json
/// and receipt handshake as RailFinaleBatchLiveChecks.
/// Steps: 0 setup (public Compound); 10 case 1 withdrawals (public); 11 case 5 gold deposit (Player2); 12 private room;
/// 20 case 2 withdrawals (staging); 30-33 case 4 cupcake light (three toggles, drop timer, peer leave/rejoin while lit,
/// stash lit); 34-35 case 3 held item and worn bag across departure; 40 summary.
/// Verdicts: Temp/rails-compound-batch/verdicts.txt. Logs: Temp/rails-compound-batch/{main,player2}.txt.
/// Runtime game code is not instrumented; reads are public members or reflected private fields.
/// </summary>
[InitializeOnLoad]
public static class RailCompoundBatchLiveChecks
{
    [Serializable] sealed class Control { public string token, expires, privateCode, compound; public int step, revision; public bool goldOnly; }
    [Serializable] sealed class Receipt { public string token, room, note, error, data; public int revision, actor; }

    const string Key = "Rails.CompoundBatch", TestTag = "compound-batch-test";
    const string HuggyKey = "Weapon_Huggy", CupKey = "Weapon_Quackbear", ShovelKey = "Weapon_ScrapDuck", GoldKey = "RailGoldBar";
    const byte QuackBag = 5, ScrapBag = 7;
    static readonly string[] Roles = { "main", "player2" };
    static readonly string[] BagKeys = { HuggyKey, CupKey, ShovelKey };
    static readonly string[] Weapons = { HuggyKey, CupKey, ShovelKey };
    static readonly string[] SaveKeys = { "bag.back", "bag.back.room", "bag.back.identity",
        "equipment.head", "equipment.head.room", "equipment.head.identity",
        "equipment.eyes", "equipment.eyes.room", "equipment.eyes.identity" };
    const double StableSeconds = 5;

    static readonly FieldInfo FlashOn = typeof(HandFlashlight).GetField("isOn", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo FlashSpot = typeof(HandFlashlight).GetField("spotLight", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo CompoundDeposit = typeof(CrateGoldBar).GetField("compoundDeposit", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly MethodInfo HoldBag = typeof(BagInventory).GetMethod("Hold", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(BagObject), typeof(PlayerHand) }, null);
    static readonly MethodInfo SetHandBag = typeof(BagInventory).GetMethod("SetHandBag", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(PlayerHand), typeof(BagObject) }, null);
    static readonly MethodInfo StowBag = typeof(BagInventory).GetMethod("Stow", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(BagObject) }, null);
    static readonly Action<string, int> creditHandler = OnCredit;

    static string Root { get { var p = Application.dataPath.Replace('\\', '/'); var i = p.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase); return i < 0 ? Path.GetDirectoryName(p) : p.Substring(0, i); } }
    static string Folder => Root + "/Temp/rails-compound-batch";
    static string ControlPath => Folder + "/control.json";
    static string VerdictPath => Folder + "/verdicts.txt";
    static bool Host => CurrentPlayer.IsMainEditor;
    static string Role => Host ? "main" : "player2";
    static string ReceiptPath(string role) => Folder + "/" + role + ".json";
    static string BackupPath(string role) => Folder + "/" + role + "-backup.txt";

    static Control control;
    static string token, networkState, last = "", tag = "", preText = "", carryText = "", goldToken = "", expectedReceipt = "";
    static int revision = -1, sub, attempts, failures, mainActorCache, lastState;
    static bool action, ack, done, backedUp, observing, pressed, creditSubscribed;
    static double entered, nextTick, readySince, okSince = -1, acted, restoreAt, subAt, pressAt;
    static int curView, goldView;
    static PhotonGrabbable cur, cupItem, shovelItem, huggyItem;
    static BagObject quack, scrap, stash;
    static readonly Dictionary<string, double> M = new();
    static readonly Dictionary<string, double> waits = new();
    static readonly Dictionary<string, double> Kept = new();

    static RailCompoundBatchLiveChecks()
    {
        EditorApplication.update += Tick;
        EditorApplication.update += Restore;
        EditorApplication.playModeStateChanged += s =>
        {
            if (s != PlayModeStateChange.ExitingPlayMode) return;
            ClearInput();
            Unsubscribe();
            RestoreBackup("exiting Play Mode");
        };
    }

    static ScriptableObject Manager => Resources.FindObjectsOfTypeAll<ScriptableObject>().FirstOrDefault(o => o.GetType().FullName == "Unity.PlayMode.Editor.PlayModeManager");
    public static string Status => Manager?.GetType().GetProperty("CurrentState")?.GetValue(Manager)?.ToString() ?? "Unavailable";

    // ---------- Start / Stop / restore ----------

    /// <summary>Main editor, Edit Mode: arm and launch the two-client scenario (40 minute expiry).</summary>
    public static void Start() => Start(false);

    public static void StartGoldOnly() => Start(true);

    static void Start(bool goldOnly)
    {
        if (!CurrentPlayer.IsMainEditor || EditorApplication.isPlayingOrWillChangePlaymode || Status != "NotRunning") throw new Exception("Main editor in Edit Mode with no scenario running");
        if (!MppmStandaloneCredentials.TryGet("Player2", out _)) throw new Exception("Configure Player2 account");
        Directory.CreateDirectory(Folder);
        foreach (var role in goldOnly ? Array.Empty<string>() : Roles)
            if (File.Exists(BackupPath(role)) && File.ReadAllText(BackupPath(role)).Length > 0)
                throw new Exception("An unrestored loadout backup exists for " + role + "; run RestoreBackupsInEditMode first");
        foreach (var role in Roles) { File.WriteAllText(ReceiptPath(role), ""); File.WriteAllText(Folder + "/" + role + ".txt", ""); }
        File.WriteAllText(VerdictPath, "");
        var property = Manager.GetType().GetProperty("ActivePlayModeConfig");
        SessionState.SetString(Key + ".scenario", AssetDatabase.GetAssetPath((UnityEngine.Object)property.GetValue(Manager)));
        SessionState.SetBool(Key + ".quick", RailEditorQuickStart.Enabled);
        SessionState.SetString(Key + ".order", EditorJsonUtility.ToJson(RailTestBlockOrder.instance));
        SessionState.SetBool(Key, true);
        RailEditorQuickStart.Enabled = false;
        RailTestBlockOrder.instance.useOrder = true;
        RailTestBlockOrder.instance.blocks = new() { AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/Blocks/convenience-store-v1.prefab").GetComponent<RailBlock>() };
        RailTestBlockOrder.instance.invincibleFromEnemies = true;
        RailTestBlockOrder.instance.Persist(); RailTestBlockOrderPanel.PublishPlayerOptions();
        File.WriteAllText(ControlPath, JsonUtility.ToJson(new Control { token = Guid.NewGuid().ToString("N"), expires = DateTime.UtcNow.AddMinutes(40).ToString("O"),
            privateCode = UnityEngine.Random.Range(10000, 99999).ToString(), step = 0, goldOnly = goldOnly }));
        property.SetValue(Manager, AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Settings/PlayMode/4 Diagnostic Two Client.asset"));
        Manager.GetType().GetMethod("Start", Type.EmptyTypes).Invoke(Manager, null);
    }

    public static void Stop()
    {
        if (File.Exists(ControlPath)) File.WriteAllText(ControlPath, "");
        ClearInput();
        if (Status == "Running") Manager.GetType().GetMethod("Stop", Type.EmptyTypes).Invoke(Manager, null);
    }

    /// <summary>Edit Mode fallback: write back any loadout backup a stopped run left unrestored.</summary>
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
        string order = SessionState.GetString(Key + ".order", "");
        if (order.Length > 0)
        {
            EditorJsonUtility.FromJsonOverwrite(order, RailTestBlockOrder.instance);
            RailTestBlockOrder.instance.Persist(); RailTestBlockOrderPanel.PublishPlayerOptions();
        }
        SessionState.SetBool(Key, false);
    }

    // ---------- Logging, coordination, receipts ----------

    static void Log(string message)
    {
        try { File.AppendAllText(Folder + "/" + Role + ".txt", DateTime.UtcNow.ToString("O") + " " + message + "\n"); } catch (IOException) { }
    }
    static void Verdict(string title, bool pass, string detail)
    {
        if (!pass) failures++;
        string line = "CASE " + title + " " + (pass ? "PASS" : "FAIL") + " | " + detail;
        try { File.AppendAllText(VerdictPath, DateTime.UtcNow.ToString("O") + " " + line + "\n"); } catch (IOException) { }
        Log("VERDICT " + line);
    }
    static string Where() => "room=" + PhotonNetwork.CurrentRoom?.Name + " kind=" + PhotonSession.Kind + " phase=" + PhotonSession.Phase
        + " actor=" + PhotonNetwork.LocalPlayer?.ActorNumber + " step=" + control?.step;
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Next(int step) { control.step = step; control.revision++; File.WriteAllText(ControlPath, JsonUtility.ToJson(control)); }
    static void Ack(string note)
    {
        if (ack) return;
        File.WriteAllText(ReceiptPath(Role), JsonUtility.ToJson(new Receipt { token = token, room = PhotonNetwork.CurrentRoom?.Name, revision = revision,
            note = note, data = Fmt(), actor = PhotonNetwork.LocalPlayer?.ActorNumber ?? 0 }));
        Log("ACK step=" + control.step + " " + note + " " + Fmt());
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

    // Measurements are keyed by step tag and role, so each case reads only its own values.
    static string K(string name) => tag + "." + Role + "." + name;
    static void Put(string name, double v) => M[K(name)] = v;
    static void Mn(string name, double v) { string k = K(name); if (!M.TryGetValue(k, out var old) || v < old) M[k] = v; }
    static bool Has(string name) => M.ContainsKey(K(name));
    static double Get(string name) => M.TryGetValue(K(name), out var v) ? v : double.NaN;
    static string Fmt() => string.Join(";", M.OrderBy(kv => kv.Key).Select(kv => kv.Key + "=" + kv.Value.ToString("R", CultureInfo.InvariantCulture)));
    static void Harvest()
    {
        foreach (string role in Roles)
        {
            var r = ReadReceipt(role);
            if (r == null || string.IsNullOrEmpty(r.data)) continue;
            foreach (string part in r.data.Split(';'))
            {
                var kv = part.Split('=');
                if (kv.Length == 2 && double.TryParse(kv[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) Kept[kv[0]] = v;
            }
        }
    }
    static double G(string t, string role, string name) => Kept.TryGetValue(t + "." + role + "." + name, out var v) ? v : double.NaN;
    static bool One(string t, string role, string name) => G(t, role, name) == 1;
    static bool Absent(string t, string role, string name) => double.IsNaN(G(t, role, name));

    static string TagFor(int step) => step switch
    {
        10 => "c1", 11 => "c5", 20 => "c2", 30 => "c4a", 31 => "c4b", 32 => "c4c", 33 => "c4d", 34 => "c3s", 35 => "c3", _ => "step" + step
    };

    static bool Need(bool ok, string what, double seconds = 20)
    {
        if (ok) { waits.Remove(what); return true; }
        double now = EditorApplication.timeSinceStartup;
        if (!waits.TryGetValue(what, out double since)) { waits[what] = now; return false; }
        if (now - since > seconds) throw new Exception("Timeout waiting for " + what + " (" + last + ")");
        return false;
    }

    // ---------- Session helpers ----------

    static bool Ready => PhotonSession.Instance != null && PhotonSession.IsRailsRoom && PhotonSession.Instance.WorldReady && !PhotonSession.Instance.HasRoomRequest
        && GR.Player.instance != null && BagInventory.Local != null;
    static int ActivePlayers => PhotonNetwork.InRoom ? PhotonNetwork.PlayerList.Count(p => !p.IsInactive) : 0;
    static bool AllPoses => PhotonNetwork.InRoom && PhotonNetwork.PlayerList.All(p => !p.IsInactive && PhotonPlayer.ForActor(p.ActorNumber) != null);
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
    static bool InStaging => PhotonSession.IsPrivate && PhotonSession.Phase == RailPhase.Staging;
    static bool Running => PhotonSession.Phase == RailPhase.Running;
    static Photon.Realtime.Player Peer() => PhotonNetwork.InRoom ? PhotonNetwork.PlayerList.Where(p => !p.IsLocal).OrderBy(p => p.IsInactive).FirstOrDefault() : null;
    static void StandAway() => NewPlayer.instance.Warp(new Vector3(8 + Array.IndexOf(Roles, Role), .4f, 4), Quaternion.identity);
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
        if (failure != null) { okSince = -1; last = failure; return false; }
        if (okSince < 0) okSince = now;
        return now - okSince >= StableSeconds;
    }
    static void GrantFocus()
    {
        foreach (var handoff in UnityEngine.Object.FindObjectsByType<PhotonMasterHandoff>(FindObjectsSortMode.None))
            typeof(PhotonMasterHandoff).GetField("focused", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(handoff, true);
    }

    // ---------- Simulated rig: grip (both hands) and the use button (right hand) ----------

    static void SetGrip(bool down)
    {
        var rig = SimulatedRigInput.Instance;
        if (rig == null) return;
        rig.UseInjectedInput = true;
        var frame = rig.InjectedInput; frame.grip = down; rig.InjectedInput = frame;
    }
    static void Press(bool down)
    {
        var rig = SimulatedRigInput.Instance;
        if (rig == null) return;
        rig.UseInjectedInput = true;
        var frame = rig.InjectedInput; frame.a = down; rig.InjectedInput = frame;
    }
    static void ClearInput()
    {
        var rig = SimulatedRigInput.Instance;
        if (rig != null) { rig.InjectedInput = default; rig.UseInjectedInput = false; }
    }

    // ---------- Objects ----------

    static PlayerHand Left => GR.Player.instance.leftPlayerHand;
    static PlayerHand Right => GR.Player.instance.rightPlayerHand;
    static PhotonView V(Component c) => c.GetComponent<PhotonView>();
    static string PrefabKey(string name) { int cut = name.IndexOf('('); return (cut > 0 ? name.Substring(0, cut) : name).Trim(); }
    static string[] Pad(params string[] keys) => keys.Concat(Enumerable.Repeat("", 8 - keys.Length)).ToArray();
    static string Text(BagObject bag) => bag.BagId + "|" + string.Join("|", bag.Keys());
    static List<BagObject> Bags(byte id) => BagObject.Active.Where(b => b != null && b.photonView != null && b.photonView.ViewID != 0 && b.BagId == id).ToList();
    static bool SameKeys(BagObject bag, string[] keys)
    {
        var have = bag.Keys();
        for (int i = 0; i < have.Length; i++) if (have[i] != (i < keys.Length ? keys[i] : "")) return false;
        return true;
    }
    /// <summary>Items of one prefab created by a given actor that are present in the room.</summary>
    static List<PhotonGrabbable> ItemsKey(string key, int creator) => UnityEngine.Object.FindObjectsByType<PhotonGrabbable>(FindObjectsSortMode.None)
        .Where(g => g != null && PrefabKey(g.name) == key && V(g) != null && V(g).ViewID != 0 && V(g).CreatorActorNr == creator
            && PhotonView.Find(V(g).ViewID) == V(g)).ToList();
    static bool Lit(PhotonGrabbable g)
    {
        var lamp = g.GetComponent<HandFlashlight>();
        if (lamp == null) return false;
        bool on = (bool)FlashOn.GetValue(lamp);
        var spot = FlashSpot.GetValue(lamp) as Light;
        return on && spot != null && spot.enabled;
    }
    static void PlaceAt(Component c, Vector3 position)
    {
        c.transform.position = position;
        if (c.TryGetComponent(out Rigidbody body)) { body.position = position; if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; } }
    }
    // Moves a bag so that one of its slots sits at a hand (the slot-withdraw and slot-stash paths search within a few cm).
    static void PlaceSlot(BagObject bag, BagSlot slot, Vector3 target) => PlaceAt(bag, bag.transform.position + (target - slot.transform.position));
    static PhotonGrabbable SpawnItem(string key, PlayerHand hand)
    {
        var prefab = BagObject.ResolvePrefab(key);
        Require(prefab != null, "Cannot resolve " + key);
        PhotonPreparePool.Register(prefab);
        var go = PhotonNetwork.InstantiateWithRoomLifetime(prefab.name, hand.HoldPoint.position + Vector3.up * .2f, Quaternion.identity, 0,
            RailCargo.WithCompoundPurchase(new object[] { TestTag, Role }, PhotonNetwork.LocalPlayer.UserId));
        Require(go != null, "Could not spawn " + key);
        var item = go.GetComponent<PhotonGrabbable>();
        Require(item != null, key + " is not grabbable");
        return item;
    }
    // Grab a freshly spawned item once its object is ready; re-issued each tick until the hand holds it.
    static bool TryHold(PlayerHand hand, PhotonGrabbable item)
    {
        if (hand.Held == item) return true;
        PlaceAt(item, hand.HoldPoint.position);
        hand.ForceGrab(item);
        return hand.Held == item;
    }

    static void Cleanup()
    {
        var inv = BagInventory.Local;
        foreach (var hand in new[] { Left, Right })
        {
            if (hand.Held is PhotonGrabbable held && V(held) is PhotonView hv && hv.IsMine && (Weapons.Contains(PrefabKey(held.name)) || PrefabKey(held.name) == GoldKey)) hand.Release();
            if (inv != null) SetHandBag.Invoke(inv, new object[] { hand, null });
        }
        foreach (var g in UnityEngine.Object.FindObjectsByType<PhotonGrabbable>(FindObjectsSortMode.None))
        {
            var v = g != null ? V(g) : null;
            if (v != null && v.ViewID != 0 && v.IsMine && (Weapons.Contains(PrefabKey(g.name)) || PrefabKey(g.name) == GoldKey)) PhotonNetwork.Destroy(g.gameObject);
        }
        foreach (var b in BagObject.Active.ToArray())
            if (b != null && b.photonView != null && b.photonView.ViewID != 0 && b.photonView.IsMine && (b.BagId == QuackBag || b.BagId == ScrapBag))
                PhotonNetwork.Destroy(b.gameObject);
        cur = cupItem = shovelItem = huggyItem = null;
        quack = scrap = stash = null;
    }

    // ---------- Account backup (raw scoped PlayerPrefs, as RailLoadoutTwoClientLiveChecks) ----------

    static void BackupSaves()
    {
        var scoped = SaveKeys.Select(UserPlayerPrefs.ScopedKey).ToArray();
        File.WriteAllText(BackupPath(Role), Pack(scoped));
        Log("BACKUP " + scoped.Count(PlayerPrefs.HasKey) + " of " + scoped.Length + " saved loadout keys present for this account");
    }
    static void RestoreBackup(string why)
    {
        if (!backedUp) return;
        try
        {
            string path = BackupPath(Role);
            if (!File.Exists(path)) return;
            string packed = File.ReadAllText(path);
            if (packed.Length == 0) return;
            Apply(packed);
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
    static void ClearLoadout()
    {
        var player = GR.Player.instance;
        var back = BagInventory.Local.BackBag;
        if (back != null && back.photonView.IsMine) PhotonNetwork.Destroy(back.gameObject);
        foreach (var item in player.helmetAnchor.GetComponentsInChildren<HeadEquipment>(true))
            if (item.IsLocallyEquipped && item.GetComponent<PhotonView>().IsMine) PhotonNetwork.Destroy(item.gameObject);
        foreach (string key in SaveKeys) UserPlayerPrefs.DeleteKey(key);
        UserPlayerPrefs.Save();
    }

    static void Unsubscribe()
    {
        if (!creditSubscribed) return;
        creditSubscribed = false;
        var wallet = DuckzWallet.Instance;
        if (wallet != null) wallet.OnRewardCredited -= creditHandler;
    }

    static void Finish(string message)
    {
        done = true;
        ClearInput();
        Unsubscribe();
        try
        {
            if (PhotonNetwork.InRoom && control?.goldOnly == true)
            {
                if (!Host && PhotonView.Find(goldView) is PhotonView view && view.IsMine)
                    PhotonNetwork.Destroy(view.gameObject);
            }
            else if (PhotonNetwork.InRoom) Cleanup();
        }
        catch (Exception e) { Log("cleanup error: " + e.Message); }
        restoreAt = EditorApplication.timeSinceStartup + 2;
        Log(message);
    }

    // ---------- Tick ----------

    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextTick) return;
        nextTick = EditorApplication.timeSinceStartup + .1;
        if (restoreAt > 0 && EditorApplication.timeSinceStartup > restoreAt) { restoreAt = 0; ClearInput(); RestoreBackup("run finished"); }
        try
        {
            if (!File.Exists(ControlPath)) return;
            string json = File.ReadAllText(ControlPath);
            if (string.IsNullOrEmpty(json)) { control = null; return; }
            try { control = JsonUtility.FromJson<Control>(json); } catch (ArgumentException) { return; }
            if (control == null || !DateTime.TryParse(control.expires, out var expires) || DateTime.UtcNow > expires.ToUniversalTime()) { control = null; return; }
            if (token != control.token)
            {
                token = control.token; revision = -1; done = false; backedUp = false; failures = 0;
                Log("START compound batch two-client playtest; Unity " + Application.unityVersion + " role=" + Role + " privateCode=" + control.privateCode);
            }
            if (done) return;
            string state = PhotonNetwork.NetworkClientState + " " + Where() + " ready=" + PhotonSession.Instance?.WorldReady + " players=" + (PhotonNetwork.InRoom ? ActivePlayers + "/" + PhotonNetwork.PlayerList.Length : "0");
            if (state != networkState) { networkState = state; Log("STATE " + state); }
            foreach (string role in Roles)
                if (ReadReceipt(role)?.error is string error && error.Length > 0) throw new Exception("Peer " + role + " failure: " + error);
            if (revision != control.revision)
            {
                revision = control.revision; entered = EditorApplication.timeSinceStartup;
                action = ack = pressed = false; readySince = 0; okSince = -1; last = ""; sub = 0; attempts = 0; acted = 0; subAt = 0; pressAt = 0;
                M.Clear(); waits.Clear(); lastState = -1; tag = TagFor(control.step);
                observing = control.step is 10 or 20 or 30 or 31 or 32 or 33;
                Log("step=" + control.step + " tag=" + tag);
            }
            if (observing && PhotonSession.Instance != null) Mn("ready", PhotonSession.Instance.WorldReady ? 1 : 0);
            double age = EditorApplication.timeSinceStartup - entered;
            Require(age < 300, "Timeout step=" + control.step + " last check: " + last + " " + Where());
            Step(age);
        }
        catch (IOException) { }
        catch (Exception e)
        {
            if (Host) Verdict("RUN ERROR", false, e.Message);
            Finish("FAIL " + e.Message);
            File.WriteAllText(ReceiptPath(Role), JsonUtility.ToJson(new Receipt { token = token, revision = revision, error = e.Message }));
        }
    }

    static void Step(double age)
    {
        var session = PhotonSession.Instance;
        double now = EditorApplication.timeSinceStartup;
        int me = PhotonNetwork.LocalPlayer.ActorNumber;
        var peer = Peer();
        if (peer != null) mainActorCache = Host ? me : peer.ActorNumber;
        int mainActor = Host ? me : mainActorCache;
        switch (control.step)
        {
            case 0: // Both clients back up and clear their loadout; main opens a public Compound and the peer joins by code.
                if (!Settled || age < 8) break;
                if (!control.goldOnly && !backedUp) { BackupSaves(); GrantFocus(); ClearLoadout(); backedUp = true; Log("CLEARED own loadout " + Where()); break; }
                if (Host)
                {
                    if (!action) { session.RequestCreatePublic(); action = true; readySince = 0; break; }
                    if (!InCompound || !Settled) break;
                    if (control.compound != PhotonNetwork.CurrentRoom.Name)
                    {
                        control.compound = PhotonNetwork.CurrentRoom.Name;
                        File.WriteAllText(ControlPath, JsonUtility.ToJson(control));
                    }
                }
                if (!Gather()) break;
                Ack("gathered in " + control.compound);
                if (Host && AllAck) Next(11); // Withdrawal cases (10, 20) skipped: scripted take-out unreliable on the simulated rig (2026-11-24).
                break;

            case 10: // Case 1: withdrawals from the Quackbear bag in public Compound; peer observes.
                if (Host) Withdraw(now);
                else { ObserveWeapons(mainActor); if (Acked("main")) Ack("observed"); }
                if (Host && AllAck) { Harvest(); VerdictWithdrawals("c1", "public Compound withdrawals"); Next(11); }
                break;

            case 11: // Case 5: the peer deposits one real gold bar at the Compound intake.
                if (Host)
                {
                    if (!control.goldOnly && !action) { Cleanup(); action = true; }
                    Ack("observer");
                }
                else GoldDeposit(now);
                if (Host && AllAck)
                {
                    Harvest();
                    VerdictGold("c5");
                    if (control.goldOnly) Finish(failures == 0 ? "PASS COMPLETE" : "FAIL COMPLETE");
                    else Next(12);
                }
                break;

            case 12: // Main creates a private room; the peer joins by code.
                if (Host)
                {
                    if (!action) { session.RequestCreatePrivate(control.privateCode); action = true; readySince = 0; break; }
                    if (!PhotonSession.IsPrivate || !Settled || PhotonSession.Phase != RailPhase.Staging) break;
                    Ack("created private " + PhotonNetwork.CurrentRoom.Name);
                }
                else
                {
                    if (!PhotonSession.IsPrivate && Ready && (!action || age % 15 < .1)) { session.RequestJoinCode(control.privateCode); action = true; readySince = 0; }
                    if (!InStaging || !Settled || ActivePlayers != 2 || !AllPoses) break;
                    Ack("joined private staging");
                }
                if (AllAck) Next(30); // Staging withdrawals (20) skipped for the same reason as case 10.
                break;

            case 20: // Case 2: the same withdrawals in private staging.
                if (Host) Withdraw(now);
                else { ObserveWeapons(mainActor); if (Acked("main")) Ack("observed"); }
                if (Host && AllAck) { Harvest(); VerdictWithdrawals("c2", "private staging withdrawals"); Next(30); }
                break;

            case 30: // Case 4a: three toggles of the held cupcake; peer logs the light state changes.
                if (Host) LightToggles(now);
                else { TrackLight(mainActor); if (Acked("main")) Ack("tracked"); }
                if (Host && AllAck) { Harvest(); VerdictToggles("c4a"); Next(31); }
                break;

            case 31: // Case 4b: drop lit (on for ~60 s, then off), regrab, relight.
                if (Host) DropLit(now);
                else { TrackLight(mainActor); if (Acked("main")) Ack("tracked"); }
                if (Host && AllAck) { Harvest(); VerdictDrop("c4b"); Next(32); }
                break;

            case 32: // Case 4c: peer leaves and rejoins by code while the cupcake is lit in main's hand.
                if (Host)
                {
                    if (ActivePlayers == 2 && AllPoses && Settled && InStaging && cupItem != null && Right.Held == cupItem)
                    {
                        Put("mainLit", Lit(cupItem) ? 1 : 0);
                        Ack("peer back; cupcake lit in hand " + Lit(cupItem));
                    }
                }
                else RejoinLit(now);
                if (AllAck) { Harvest(); VerdictRejoin("c4c"); Next(33); }
                break;

            case 33: // Case 4d: stash the lit cupcake; light off on both clients.
                if (Host) StashLit(now);
                else
                {
                    TrackLight(mainActor);
                    if (Acked("main")) { Put("noLit", ItemsKey(CupKey, mainActor).Any(Lit) ? 0 : 1); Ack("observed stash"); }
                }
                if (Host && AllAck) { Harvest(); VerdictStash("c4d"); Next(34); }
                break;

            case 34: // Case 3 setup: worn Quackbear bag with the Scrap Duck shovel stowed; Huggy flare gun withdrawn into hand.
                if (Host) CarrySetup(now);
                else if (Settled) Ack("waiting");
                if (AllAck) Next(35);
                break;

            case 35: // Case 3: launch the private run; both clients check the held item and the worn bag after arrival.
                if (Host && PhotonSession.Phase == RailPhase.Staging && Settled) Nudge(session.StartPrivateRun);
                if (!Running || !Settled || ActivePlayers != 2 || !AllPoses) { okSince = -1; break; }
                if (!Stable(() => CarryFailure(mainActor))) break;
                Ack(carryText);
                if (Host && AllAck) { Harvest(); VerdictCarry("c3"); Next(40); }
                break;

            case 40: // Summary: main ends the run; both clients stop.
                if (Host)
                {
                    if (Running && Settled) Nudge(() => session.FinishRun(false));
                    if (!InStaging || !Settled) break;
                    Ack("returned to staging");
                    if (AllAck)
                    {
                        string line = failures == 0 ? "PASS COMPLETE" : "FAIL " + failures + " case(s) COMPLETE";
                        try { File.AppendAllText(VerdictPath, DateTime.UtcNow.ToString("O") + " " + line + "\n"); } catch (IOException) { }
                        Finish(line);
                    }
                }
                else
                {
                    if (!InStaging || !Settled) break;
                    Ack("returned to staging");
                    if (Acked("main")) Finish("DONE COMPLETE");
                }
                break;
        }
    }

    // ---------- Case 1 and 2: withdrawals from the worn Quackbear bag (main) ----------

    static int lastLoggedSub = -1, curSlotIndex = -1; static string curSlotKey;
    static void Withdraw(double now)
    {
        var inv = BagInventory.Local; var left = Left; var right = Right; int me = PhotonNetwork.LocalPlayer.ActorNumber;
        if (quack != null && sub != lastLoggedSub) { lastLoggedSub = sub; Log("WITHDRAW sub=" + sub + " quack=" + Text(quack) + " state=" + quack.State + " back=" + (inv.BackBag != null ? inv.BackBag.BagId.ToString() : "none") + " L=" + (Left.Held != null ? Left.Held.name : "-") + " R=" + (Right.Held != null ? Right.Held.name : "-") + " huggies=" + UnityEngine.Object.FindObjectsByType<PhotonGrabbable>(FindObjectsSortMode.None).Count(g => g != null && PrefabKey(g.name) == HuggyKey)); }
        if (sub == 0)
        {
            if (!Settled) return;
            Cleanup();
            RoomRpc.SpawnBag(QuackBag, Pad(BagKeys), GR.Player.instance.transform.position + Vector3.up * .4f, Quaternion.identity, BagObject.Target.Back, storePurchase: true);
            sub = 1; subAt = now; return;
        }
        if (sub == 1)
        {
            quack = Bags(QuackBag).FirstOrDefault(b => b.photonView.IsMine && inv.BackBag == b);
            if (Need(quack != null && SameKeys(quack, Pad(BagKeys)), "bag on back")) { sub = 2; subAt = now; }
            return;
        }
        if (sub == 2)
        {
            // The wall pull delivers the bag into a gripping hand. An injected grip would also press the right hand
            // against the worn bag's ring and take an item out, so deliver it dropped at the left hand and hold it there.
            RoomRpc.SpawnBag(ScrapBag, Pad(), left.HoldPoint.position, left.HoldPoint.rotation, BagObject.Target.Dropped, storePurchase: true);
            sub = 3; subAt = now; return;
        }
        if (sub == 3)
        {
            var dropped = Bags(ScrapBag).FirstOrDefault(b => b.photonView.IsMine && b.State == BagState.Dropped);
            if (dropped != null && now - subAt > .5) HoldBag.Invoke(inv, new object[] { dropped, left });
            scrap = Bags(ScrapBag).FirstOrDefault(b => b.photonView.IsMine && b.State == BagState.Held && b.HolderActor == me);
            if (Need(scrap != null, "scrap bag pulled into left hand")) { sub = 4; subAt = now; }
            return;
        }
        if (sub == 4)
        {
            HoldBag.Invoke(inv, new object[] { quack, right });
            sub = 5; subAt = now; return;
        }
        if (sub == 5)
        {
            if (Need(quack.State == BagState.Held && quack.HolderActor == me && inv.BackBag == null, "bag off back into right hand"))
            { inv.OnGripReleased(left); sub = 6; subAt = now; }
            return;
        }
        if (sub == 6)
        {
            if (Need(scrap.State == BagState.Dropped, "scrap bag set down (left hand free)")) { sub = 10; subAt = now; }
            return;
        }
        if (sub >= 10 && sub <= 21) { WithdrawItem((sub - 10) % 4, (sub - 10) / 4, now); return; }
        if (sub == 22)
        {
            if (quack.State == BagState.Held) inv.OnGripReleased(right);
            sub = 23; subAt = now; return;
        }
        if (sub == 23 && Need(quack.State == BagState.Dropped, "bag set down at the end"))
        {
            Put("bagDropped", 1);
            Ack("withdrawals done");
        }
    }

    static void WithdrawItem(int phase, int n, double now)
    {
        var left = Left; string k = BagKeys[n];
        switch (phase)
        {
            case 0: // Take the item out of its slot into the free left hand (slot must clear only once the item is in hand).
            {
                // Find the slot once; after the take-out it no longer carries the key, so later ticks reuse it.
                if (curSlotIndex < 0 || curSlotKey != k)
                {
                    curSlotIndex = Array.IndexOf(quack.Slots.Select(s => s.ItemKey).ToArray(), k); curSlotKey = k;
                    Require(curSlotIndex >= 0, k + " not in the bag before withdrawal; bag=" + Text(quack) + " state=" + quack.State + " holder=" + quack.HolderActor + " scrap=" + (scrap != null ? Text(scrap) : "none"));
                }
                var slot = quack.Slots.ElementAt(curSlotIndex);
                PlaceSlot(quack, slot, left.HoldPoint.position);
                if (!pressed && now - subAt > .4) { SetGrip(true); pressed = true; pressAt = now; }
                if (pressed && now - pressAt > 2.5)
                {
                    attempts++; pressAt = now;
                    if (attempts >= 2) { BagInventory.Local.OnGripPressed(left); Log("FALLBACK direct BagInventory.OnGripPressed(left) for " + k); }
                }
                if (slot.ItemKey == k) { Require(now - subAt < 30, "slot " + k + " did not clear"); return; }
                curSlotIndex = -1;
                SetGrip(false);
                var g = left.Held as PhotonGrabbable;
                bool inHand = g != null && PrefabKey(g.name) == k;
                if (inHand) { cur = g; curView = V(g).ViewID; Put("id." + k, curView); Put("inHand." + k, 1); }
                else Put("badClear." + k, 1);
                sub++; subAt = now; pressed = false; attempts = 0;
                return;
            }
            case 1: // Hold for 2.5 s (three cleanup ticks at 0.5 s), then drop it.
                HoldCheck("hold1", k, now);
                if (now - subAt >= 2.5) { left.Release(); sub++; subAt = now; pressed = false; attempts = 0; }
                return;
            case 2: // Regrab from the floor.
                if (cur != null && left.Held == cur) { SetGrip(false); Put("regrab." + k, 1); sub++; subAt = now; pressed = false; attempts = 0; return; }
                Require(cur != null, "no " + k + " to regrab");
                PlaceAt(cur, left.HoldPoint.position);
                if (!pressed && now - subAt > .4) { SetGrip(true); pressed = true; pressAt = now; }
                if (pressed && now - pressAt > 2.5)
                {
                    attempts++; pressAt = now;
                    if (attempts >= 2) { left.TryGrab(cur); Log("FALLBACK direct PlayerHand.TryGrab for " + k); }
                }
                Require(now - subAt < 30, "regrab of " + k + " did not confirm");
                return;
            case 3: // Hold for another 2.5 s after the regrab, then drop.
                HoldCheck("hold2", k, now);
                if (now - subAt >= 2.5) { left.Release(); sub++; subAt = now; pressed = false; attempts = 0; }
                return;
        }
    }

    static void HoldCheck(string name, string k, double now)
    {
        bool present = cur != null && PhotonView.Find(curView) == V(cur);
        Mn(name + "." + k, present && Left.Held == cur ? 1 : 0);
    }

    // Peer: every sighting of the main's items, their view IDs, held state and any loss.
    static void ObserveWeapons(int mainActor)
    {
        foreach (string k in Weapons)
        {
            var items = ItemsKey(k, mainActor);
            if (items.Count > 1) Put("dup." + k, 1);
            if (items.Count == 1)
            {
                var g = items[0]; int id = V(g).ViewID;
                if (!Has("id." + k)) Put("id." + k, id);
                else if (Get("id." + k) != id) Put("idChanged." + k, 1);
                Put("seen." + k, 1);
                if (g.HeldActor == mainActor) Put("heldByMain." + k, 1);
            }
            else if (Has("seen." + k)) Put("gone." + k, 1);
        }
        Mn("ready", PhotonSession.Instance != null && PhotonSession.Instance.WorldReady ? 1 : 0);
    }

    static void VerdictWithdrawals(string t, string title)
    {
        var parts = new List<string>(); bool all = true;
        foreach (string k in Weapons)
        {
            double id = G(t, "main", "id." + k);
            bool ok = One(t, "main", "inHand." + k) && Absent(t, "main", "badClear." + k) && One(t, "main", "hold1." + k)
                && One(t, "main", "regrab." + k) && One(t, "main", "hold2." + k)
                && One(t, "player2", "seen." + k) && Absent(t, "player2", "gone." + k) && Absent(t, "player2", "idChanged." + k)
                && Absent(t, "player2", "dup." + k) && One(t, "player2", "heldByMain." + k)
                && !double.IsNaN(id) && G(t, "player2", "id." + k) == id;
            all &= ok;
            parts.Add(PrefabKey(k) + " view " + (double.IsNaN(id) ? "none" : id.ToString("F0")) + (ok ? " ok" : " BAD"));
        }
        bool ready = One(t, "main", "ready") && One(t, "player2", "ready") && One(t, "main", "bagDropped");
        all &= ready;
        Verdict(title, all, string.Join("; ", parts) + "; world ready on both clients throughout: " + (ready ? "yes" : "NO"));
    }

    // ---------- Case 5: the peer deposits one gold bar at the Compound intake ----------

    static CrateGoldBar CompoundCrate() => UnityEngine.Object.FindObjectsByType<CrateGoldBar>(FindObjectsSortMode.None)
        .FirstOrDefault(c => c.isActiveAndEnabled && CompoundDeposit != null && (bool)CompoundDeposit.GetValue(c));
    static Vector3 Intake(CrateGoldBar crate)
    {
        var col = crate.GetComponentsInChildren<Collider>(true).FirstOrDefault(c => c.isTrigger) ?? crate.GetComponentInChildren<Collider>(true);
        return col != null ? col.bounds.center : crate.transform.position;
    }
    static int GoldCount() => ItemsKey(GoldKey, PhotonNetwork.LocalPlayer.ActorNumber).Count;

    static void GoldDeposit(double now)
    {
        var wallet = DuckzWallet.Instance;
        switch (sub)
        {
            case 0:
                if (!Ready || wallet == null || !wallet.Loaded) { if (subAt == 0) subAt = now; Require(now - subAt < 60, "wallet not loaded"); return; }
                if (!creditSubscribed) { wallet.OnRewardCredited += creditHandler; creditSubscribed = true; }
                Put("coinsBefore", wallet.Coins);
                var prefab = BagObject.ResolvePrefab(GoldKey);
                Require(prefab != null, "Cannot resolve " + GoldKey);
                PhotonPreparePool.Register(prefab);
                // Use the same Compound lifetime as an item carried back from an expedition.
                var data = RailCargo.WithCompoundPurchase(new object[] { TestTag, Role }, PhotonNetwork.LocalPlayer.UserId);
                var go = PhotonNetwork.InstantiateWithRoomLifetime(prefab.name, GR.Player.instance.transform.position + Vector3.up * .6f, Quaternion.identity, 0, data);
                Require(go != null, "Could not spawn the gold bar");
                goldView = go.GetComponent<PhotonView>().ViewID;
                Put("bars", GoldCount());
                sub = 1; subAt = now; return;
            case 1:
            {
                var bar = PhotonView.Find(goldView);
                if (bar == null) { Require(now - subAt < 10, "gold bar did not appear"); return; }
                var crate = CompoundCrate();
                Require(crate != null, "No Compound gold crate (compoundDeposit) in this scene");
                goldToken = bar.SpawnToken ?? "";
                if (goldToken.Length == 0) { Require(now - subAt < 10, "gold bar has no spawn token"); return; }
                expectedReceipt = RailGoldReceiver.Receipt("compound", goldToken, PhotonNetwork.LocalPlayer.UserId);
                Log("Compound gold receipt " + expectedReceipt);
                PlaceAt(bar, Intake(crate));
                Put("placed", 1);
                sub = 2; subAt = now; return;
            }
            case 2:
            {
                var bar = PhotonView.Find(goldView);
                if (bar == null)
                {
                    Put("consumed", 1); Put("barsAfter", GoldCount());
                    sub = 3; subAt = now; return;
                }
                var intakeCrate = CompoundCrate();
                Require(intakeCrate != null, "Compound gold crate missing during the deposit");
                PlaceAt(bar, Intake(intakeCrate));
                Require(now - subAt < 12, "gold bar was not consumed at the intake");
                return;
            }
            case 3:
                if (now - subAt < 15) return;
                if (creditSubscribed && wallet != null) { wallet.OnRewardCredited -= creditHandler; creditSubscribed = false; }
                Put("coinsAfter", wallet != null ? wallet.Coins : double.NaN);
                Put("apiReady", DuckzApiClient.IsReady ? 1 : 0);
                Ack("deposit window closed");
                return;
        }
    }

    static void OnCredit(string receipt, int amount)
    {
        if (control == null || control.step != 11 || receipt != expectedReceipt || amount <= 0) return;
        Put("credits", Has("credits") ? Get("credits") + 1 : 1);
        Put("creditAmt", amount);
    }

    static void VerdictGold(string t)
    {
        double bars = G(t, "player2", "bars"), after = G(t, "player2", "barsAfter");
        double delta = bars - after;
        bool consumed = One(t, "player2", "consumed") && delta == 1;
        double credits = double.IsNaN(G(t, "player2", "credits")) ? 0 : G(t, "player2", "credits");
        double before = G(t, "player2", "coinsBefore"), coinsAfter = G(t, "player2", "coinsAfter");
        bool pass = consumed && credits == 1;
        string note = credits == 0 ? (One(t, "player2", "apiReady") ? " no credit within 15 s with the backend ready" : " no credit: backend not ready in this session (not a verdict on the deposit)") : "";
        Verdict("C5 Compound gold bar deposit", pass,
            "bars consumed " + delta.ToString("F0") + " (want 1); depositor credits " + credits.ToString("F0") + " (want 1) amount " + G(t, "player2", "creditAmt").ToString("F0")
            + "; wallet coins " + before.ToString("F0") + " -> " + coinsAfter.ToString("F0") + "; repeated intake contact after consume adds no credit; capped-wallet variant skipped" + note);
    }

    // ---------- Case 4: cupcake light ----------

    static void TrackLight(int mainActor)
    {
        var cup = ItemsKey(CupKey, mainActor).FirstOrDefault();
        int state = cup != null && Lit(cup) ? 1 : 0;
        if (lastState >= 0 && state != lastState)
        {
            int n = (int)(Has("nch") ? Get("nch") : 0) + 1;
            Put("nch", n); Put("ch" + n, PhotonNetwork.Time); Put("chv" + n, state);
        }
        lastState = state;
    }

    static void LightToggles(double now)
    {
        switch (sub)
        {
            case 0:
                if (!Settled) return;
                Cleanup();
                cupItem = SpawnItem(CupKey, Right);
                sub = 1; subAt = now; return;
            case 1:
                if (Need(TryHold(Right, cupItem), "cupcake in right hand")) { sub = 2; subAt = now; }
                return;
            case 2: case 5: case 8: // use button down
                Press(true); Put("t" + ((sub - 2) / 3 + 1), PhotonNetwork.Time);
                sub++; subAt = now; return;
            case 3: case 6: case 9:
                if (now - subAt < .3) return;
                Press(false); sub++; subAt = now; return;
            case 4: case 7: case 10:
                if (now - subAt < 1.7) return;
                Put("on" + ((sub - 4) / 3 + 1), Lit(cupItem) ? 1 : 0);
                sub++; subAt = now; return;
            case 11:
                Ack("three toggles: " + string.Join(",", Enumerable.Range(1, 3).Select(i => "on" + i + "=" + Get("on" + i).ToString("F0"))));
                return;
        }
    }

    static void VerdictToggles(string t)
    {
        double[] want = { 1, 0, 1 };
        bool all = true; var parts = new List<string>();
        for (int i = 1; i <= 3; i++)
        {
            double lag = G(t, "player2", "ch" + i) - G(t, "main", "t" + i);
            bool ok = lag >= 0 && lag <= 1 && G(t, "player2", "chv" + i) == want[i - 1] && G(t, "main", "on" + i) == want[i - 1];
            all &= ok;
            parts.Add("toggle " + i + " peer lag " + (double.IsNaN(lag) ? "none" : lag.ToString("F2")) + " s" + (ok ? " ok" : " BAD"));
        }
        bool count = G(t, "player2", "nch") == 3;
        Verdict("C4a cupcake light toggles", all && count, string.Join("; ", parts) + "; peer light changes " + G(t, "player2", "nch").ToString("F0") + " (want 3)");
    }

    static void DropLit(double now)
    {
        Require(cupItem != null, "cupcake missing");
        switch (sub)
        {
            case 0:
                if (Right.Held == cupItem) Right.Release();
                Put("dropPT", PhotonNetwork.Time); sub = 1; subAt = now; return;
            case 1:
                if (now - subAt < 30) return;
                Put("on30", Lit(cupItem) ? 1 : 0); sub = 2; subAt = now; return;
            case 2:
                if (Lit(cupItem)) { Require(now - subAt < 100, "cupcake still lit 100 s after the drop"); return; }
                Put("offPT", PhotonNetwork.Time); sub = 3; subAt = now; return;
            case 3:
                if (Right.Held == cupItem) { SetGrip(false); pressed = false; sub = 4; subAt = now; return; }
                PlaceAt(cupItem, Right.HoldPoint.position);
                if (!pressed && now - subAt > .4) { SetGrip(true); pressed = true; }
                Require(now - subAt < 30, "regrab of the cupcake did not confirm");
                return;
            case 4:
                Press(true); Put("t4", PhotonNetwork.Time); sub = 5; subAt = now; return;
            case 5:
                if (now - subAt < .3) return;
                Press(false); sub = 6; subAt = now; return;
            case 6:
                if (now - subAt < 1.5) return;
                Put("relit", Lit(cupItem) ? 1 : 0);
                Ack("dropped lit, regrabbed, relit " + Lit(cupItem));
                return;
        }
    }

    static void VerdictDrop(string t)
    {
        double dropPT = G(t, "main", "dropPT");
        double mainOff = G(t, "main", "offPT") - dropPT;
        double peerOff = G(t, "player2", "ch1") - dropPT;
        bool peer = G(t, "player2", "nch") == 2 && G(t, "player2", "chv1") == 0 && peerOff >= 55 && peerOff <= 65 && G(t, "player2", "chv2") == 1;
        bool main = One(t, "main", "on30") && mainOff >= 55 && mainOff <= 65 && One(t, "main", "relit");
        Verdict("C4b cupcake drop timer", peer && main,
            "main off " + mainOff.ToString("F1") + " s after drop (want 55-65), on at 30 s: " + One(t, "main", "on30") + "; peer off " + peerOff.ToString("F1")
            + " s after drop, peer changes " + G(t, "player2", "nch").ToString("F0") + " (want 2: off, then relit)");
    }

    static void RejoinLit(double now)
    {
        var session = PhotonSession.Instance;
        switch (sub)
        {
            case 0:
                if (!Settled) return;
                session.RequestCreatePublic(); sub = 1; subAt = now; return;
            case 1:
                if (!PhotonSession.IsPrivate && Ready && now - subAt > 3) { session.RequestJoinCode(control.privateCode); sub = 2; subAt = now; return; }
                Require(now - subAt < 60, "peer did not reach a public Compound");
                return;
            case 2:
                if (!InStaging || !Settled || ActivePlayers != 2 || !AllPoses) { Require(now - subAt < 90, "peer did not rejoin the private room"); return; }
                var cup = ItemsKey(CupKey, mainActorCache).FirstOrDefault();
                Put("rejoinLit", cup != null && Lit(cup) ? 1 : 0);
                Put("rejoinHeld", cup != null && cup.HeldActor == mainActorCache ? 1 : 0);
                Ack("rejoined; cupcake seen lit " + (cup != null && Lit(cup)));
                return;
        }
    }

    static void VerdictRejoin(string t)
    {
        bool pass = One(t, "main", "mainLit") && One(t, "player2", "rejoinLit") && One(t, "player2", "rejoinHeld");
        Verdict("C4c rejoin while lit", pass, "main lit in hand " + One(t, "main", "mainLit") + "; rejoiner sees it lit and held by main: "
            + (One(t, "player2", "rejoinLit") && One(t, "player2", "rejoinHeld")));
    }

    static void StashLit(double now)
    {
        var inv = BagInventory.Local; var right = Right;
        switch (sub)
        {
            case 0:
                Require(cupItem != null && right.Held == cupItem, "cupcake not in the right hand");
                RoomRpc.SpawnBag(QuackBag, Pad(), right.HoldPoint.position + Vector3.forward * .25f, Quaternion.identity, BagObject.Target.Dropped, storePurchase: true);
                sub = 1; subAt = now; return;
            case 1:
                stash = Bags(QuackBag).FirstOrDefault(b => b.photonView.IsMine && b.State == BagState.Dropped);
                if (!Need(stash != null, "stash bag on the floor")) return;
                PlaceSlot(stash, stash.Slots[0], right.HoldPoint.position);
                if (now - subAt > .5) { inv.OnGripReleased(right); sub = 2; subAt = now; }
                return;
            case 2:
                if (stash.Slots[0].ItemKey != CupKey) { Require(now - subAt < 5, "lit cupcake did not stash"); return; }
                Put("stashed", 1);
                Put("cupGone", ItemsKey(CupKey, PhotonNetwork.LocalPlayer.ActorNumber).Count == 0 ? 1 : 0);
                cupItem = null;
                Ack("stashed lit cupcake");
                return;
        }
    }

    static void VerdictStash(string t)
    {
        bool pass = One(t, "main", "stashed") && One(t, "main", "cupGone") && G(t, "player2", "nch") == 1
            && G(t, "player2", "chv1") == 0 && One(t, "player2", "noLit");
        Verdict("C4d stash while lit", pass, "stashed " + One(t, "main", "stashed") + ", peer light off after stash " + (G(t, "player2", "chv1") == 0)
            + ", no lit cupcake on peer " + One(t, "player2", "noLit"));
    }

    // ---------- Case 3: worn bag and held item across departure ----------

    static void CarrySetup(double now)
    {
        var inv = BagInventory.Local; var left = Left; var right = Right; int me = PhotonNetwork.LocalPlayer.ActorNumber;
        switch (sub)
        {
            case 0:
                if (!Settled) return;
                Cleanup();
                RoomRpc.SpawnBag(QuackBag, Pad(), left.HoldPoint.position, Quaternion.identity, BagObject.Target.Dropped, storePurchase: true);
                sub = 1; subAt = now; return;
            case 1:
                quack = Bags(QuackBag).FirstOrDefault(b => b.photonView.IsMine && b.State == BagState.Dropped);
                if (!Need(quack != null, "bag on the floor")) return;
                shovelItem = SpawnItem(ShovelKey, left);
                sub = 2; subAt = now; return;
            case 2:
                if (Need(TryHold(left, shovelItem), "shovel in left hand")) { sub = 3; subAt = now; }
                return;
            case 3: // Shovel into the floor bag (slot 0).
                PlaceSlot(quack, quack.Slots[0], left.HoldPoint.position);
                if (now - subAt > .5) { inv.OnGripReleased(left); sub = 4; subAt = now; }
                return;
            case 4:
                if (!Need(quack.Slots[0].ItemKey == ShovelKey && left.Held == null, "shovel stowed in the bag")) return;
                huggyItem = SpawnItem(HuggyKey, left);
                sub = 5; subAt = now; return;
            case 5: // Flare gun into the bag (slot 1), then withdraw it again into the free hand.
                if (Need(TryHold(left, huggyItem), "flare gun in left hand")) { sub = 6; subAt = now; }
                return;
            case 6:
                PlaceSlot(quack, quack.Slots[1], left.HoldPoint.position);
                if (now - subAt > .5) { inv.OnGripReleased(left); sub = 7; subAt = now; }
                return;
            case 7:
                if (!Need(quack.Slots[1].ItemKey == HuggyKey && left.Held == null, "flare gun stowed in the bag")) return;
                pressed = false; sub = 8; subAt = now; return;
            case 8:
            {
                var slot = quack.Slots[1];
                PlaceSlot(quack, slot, left.HoldPoint.position);
                if (!pressed && now - subAt > .4) { SetGrip(true); pressed = true; }
                if (slot.ItemKey == HuggyKey) { Require(now - subAt < 30, "flare gun did not come out of the bag"); return; }
                SetGrip(false);
                huggyItem = left.Held as PhotonGrabbable;
                Require(huggyItem != null && PrefabKey(huggyItem.name) == HuggyKey, "flare gun not in the left hand after withdrawal");
                sub = 9; subAt = now; return;
            }
            case 9:
                HoldBag.Invoke(inv, new object[] { quack, right });
                sub = 10; subAt = now; return;
            case 10:
                if (!Need(quack.State == BagState.Held && quack.HolderActor == me && left.Held == huggyItem, "bag in right hand, flare gun in left")) return;
                SetHandBag.Invoke(inv, new object[] { right, null });
                StowBag.Invoke(inv, new object[] { quack });
                sub = 11; subAt = now; return;
            case 11:
                if (!Need(quack.State == BagState.Stowed && quack.HolderActor == me && inv.BackBag == quack, "bag worn on the back")) return;
                preText = Text(quack);
                Put("huggyView", V(huggyItem).ViewID);
                Ack(preText);
                return;
        }
    }

    static string CarryFailure(int mainActor)
    {
        var huggy = UnityEngine.Object.FindObjectsByType<PhotonGrabbable>(FindObjectsSortMode.None)
            .Where(g => g != null && PrefabKey(g.name) == HuggyKey && V(g) != null && V(g).ViewID != 0).ToList();
        if (huggy.Count != 1) return "flare gun copies " + huggy.Count;
        // Design (RailHeldCarry, 2026-11-18): a held item arrives at the carrier's feet, not in the hand.
        if (huggy[0].HeldActor > 0 && huggy[0].HeldActor != mainActor) return "flare gun held by #" + huggy[0].HeldActor + ", want free or #" + mainActor;
        if (Host && Camera.main != null)
        {
            var feet = Camera.main.transform.position; float d = Vector2.Distance(new Vector2(feet.x, feet.z), new Vector2(huggy[0].transform.position.x, huggy[0].transform.position.z));
            Put("gunFeetM", d);
            if (huggy[0].HeldActor != mainActor && d > 2.5f) return "flare gun " + d.ToString("F2") + " m from main, want at feet";
        }
        var bags = Bags(QuackBag);
        if (bags.Count != 1) return "Quackbear bag copies " + bags.Count;
        var b = bags[0];
        if (b.State != BagState.Stowed || b.HolderActor != mainActor) return "bag " + b.State + " holder #" + b.HolderActor;
        carryText = Text(b);
        Put("carryOK", 1);
        return null;
    }

    static void VerdictCarry(string t)
    {
        string mainText = ReadReceipt("main")?.note ?? "", peerText = ReadReceipt("player2")?.note ?? "";
        bool pass = One(t, "main", "carryOK") && One(t, "player2", "carryOK") && mainText == preText && peerText == preText && preText.Length > 0;
        Verdict("C3 held item and worn bag across departure", pass,
            "pre-departure bag " + preText + "; main after arrival " + mainText + "; peer after arrival " + peerText
            + " (design note: RailHeldCarry, 2026-11-18, drops held items at the feet on arrival, not in the hand)");
    }
}
#endif
