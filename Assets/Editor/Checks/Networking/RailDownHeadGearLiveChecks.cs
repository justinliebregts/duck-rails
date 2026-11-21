#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.AI;
using Photon.Pun;
using Unity.Multiplayer.PlayMode;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Opt-in two-client playtest for "Downing drops head equipment" (playtest-queue.md; HeadEquipment.DropAllOnDowned from
/// RailExpedition beside BagInventory.DropAllOnDowned). Each round one role (chosen by master status) wears a test helmet,
/// night vision goggles and a loaded basic bag, then goes down (off the train via RequestDown or an enemy-down request, or
/// aboard via a boarded enemy). Both clients check: exactly one loose copy of each item near the downed duck, nothing worn,
/// held or duplicated, the downed player's head/eyes/bag saves cleared and its NVG effect off. The peer revives; nothing
/// comes back automatically. Optionally the rescuer leaves and late-joins and must see the items on the ground. Then the
/// rescuer picks up and re-wears the helmet, the revived player re-wears the goggles and its bag, and saves follow.
/// Account loadout keys are backed up and restored. Logs: Temp/rails-down-head-gear/{main,player2}.txt.
/// Runtime game code is not instrumented. No diagnostic report access.
/// </summary>
[InitializeOnLoad]
public static class RailDownHeadGearLiveChecks
{
    [Serializable] sealed class Control
    {
        public string token, expires, privateCode, room, downed;
        public int step, revision;
    }
    [Serializable] sealed class Receipt
    {
        public string token, room, note, error, pos;
        public int revision, actor;
        public bool master, aboard;
        public Vector3 world, local;
    }
    enum How { OffSelf, AboardEnemy, OffEnemy }
    sealed class Round { public bool master, lateJoin; public How how; public string name; }
    static readonly Round[] Rounds =
    {
        new Round { master = false, how = How.OffSelf, lateJoin = true, name = "non-master downed off the train (RequestDown); rescuer late-joins after revive" },
        new Round { master = true, how = How.AboardEnemy, name = "master downed aboard by a boarded enemy (enemy-down request)" },
        new Round { master = false, how = How.OffSelf, name = "non-master downed off the train (RequestDown), other role" },
        new Round { master = true, how = How.OffEnemy, name = "master downed off the train by an enemy-down request" },
    };
    enum P { Clean, Wear, Down, Revive, LateJoin, TakeHelmet, WearHelmet, TakeGoggles, WearGoggles, TakeBag, WearBag, Final }
    const int RoundBase = 10, RoundSpan = 20;

    const string Key = "Rails.DownHeadGearLive";
    static readonly string[] Roles = { "main", "player2" };
    static string Root { get { var p = Application.dataPath.Replace('\\', '/'); var i = p.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase); return i < 0 ? Path.GetDirectoryName(p) : p.Substring(0, i); } }
    static string Folder => Root + "/Temp/rails-down-head-gear";
    static string ControlPath => Folder + "/control.json";
    static bool Host => CurrentPlayer.IsMainEditor;
    static string Role => Host ? "main" : "player2";
    static string Other => Host ? "player2" : "main";
    static string ReceiptPath(string role) => Folder + "/" + role + ".json";
    static string BackupPath(string role) => Folder + "/" + role + "-backup.txt";

    const string TestTag = "down-head-gear-test", Helmet = "RailShopItemHelmet", Eyes = "RailGoggles";
    const byte BagId = 2;
    static readonly string[] BagKeys = { "RailShopItemFlashlight", "RailCoal", "", "" };
    static string BagText => BagId + "|" + string.Join("|", BagKeys);
    static readonly string[] SaveKeys = { "bag.back", "bag.back.room", "bag.back.identity",
        "equipment.head", "equipment.head.room", "equipment.head.identity",
        "equipment.eyes", "equipment.eyes.room", "equipment.eyes.identity" };
    const float DropNear = 1.5f, LaterNear = 2.5f;

    static Control control;
    static string token, networkState, last = "";
    static int revision = -1, attempts, baseHelmets = -1, baseGoggles = -1;
    static bool action, ack, done, backedUp, left, rejoined, grabbed, sawPeerLeave;
    static double entered, nextTick, readySince, okSince = -1, acted, restoreAt, subAt, holdOkSince = -1;
    static Enemy attacker;

    static RailDownHeadGearLiveChecks()
    {
        EditorApplication.update += Tick;
        EditorApplication.update += Restore;
        EditorApplication.playModeStateChanged += s =>
        {
            if (s != PlayModeStateChange.ExitingPlayMode) return;
            ClearInput();
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
        var c = new Control { token = Guid.NewGuid().ToString("N"), expires = DateTime.UtcNow.AddMinutes(90).ToString("O"),
            privateCode = UnityEngine.Random.Range(10000, 99999).ToString(), step = 0 };
        File.WriteAllText(ControlPath, JsonUtility.ToJson(c));
        property.SetValue(Manager, AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Settings/PlayMode/4 Diagnostic Two Client.asset"));
        Manager.GetType().GetMethod("Start", Type.EmptyTypes).Invoke(Manager, null);
    }

    public static void Stop()
    {
        if (File.Exists(ControlPath)) File.WriteAllText(ControlPath, "");
        if (Status == "Running") Manager.GetType().GetMethod("Stop", Type.EmptyTypes).Invoke(Manager, null);
    }

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
        + " master=" + PhotonNetwork.IsMasterClient + " actor=" + PhotonNetwork.LocalPlayer?.ActorNumber + " step=" + control?.step;
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Next(int step) { control.step = step; control.revision++; File.WriteAllText(ControlPath, JsonUtility.ToJson(control)); }
    static Receipt pendingReceipt;
    static void Ack(string message)
    {
        if (ack) return;
        var r = pendingReceipt ?? new Receipt();
        r.token = token; r.revision = revision; r.room = PhotonNetwork.CurrentRoom?.Name; r.note = message;
        r.actor = PhotonNetwork.LocalPlayer?.ActorNumber ?? 0; r.master = PhotonNetwork.IsMasterClient;
        File.WriteAllText(ReceiptPath(Role), JsonUtility.ToJson(r));
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

    // ---------- Session helpers ----------

    static bool Ready => PhotonSession.Instance != null && PhotonSession.IsRailsRoom && PhotonSession.Instance.WorldReady && !PhotonSession.Instance.HasRoomRequest
        && GR.Player.instance != null && BagInventory.Local != null && NewPlayer.instance != null;
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
    static bool InTestRoom => PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.Name == control.room;
    static bool Together => InTestRoom && Running && Settled && ActivePlayers == 2 && AllPoses;
    static void Teleport(Vector3 position) => NewPlayer.instance.Warp(position, Quaternion.identity);
    static Transform Train => RailTrain.Instance.transform;
    static void Board() => Teleport(Train.TransformPoint(new Vector3(Host ? -1.2f : 1.2f, .35f, -2)));
    static Photon.Realtime.Player Peer() => PhotonNetwork.InRoom ? PhotonNetwork.PlayerList.Where(p => !p.IsLocal).OrderBy(p => p.IsInactive).FirstOrDefault() : null;
    static bool PeerHere => Peer() is Photon.Realtime.Player p && !p.IsInactive && PhotonPlayer.ForActor(p.ActorNumber) != null;
    static void Nudge(Action request, double every = 4)
    {
        if (EditorApplication.timeSinceStartup - acted < every) return;
        acted = EditorApplication.timeSinceStartup;
        request();
    }
    static bool Stable(Func<string> check, double seconds)
    {
        string failure = check();
        double now = EditorApplication.timeSinceStartup;
        if (failure != null)
        {
            if (okSince >= 0) Log("  (check flipped back to failing: " + failure + ")");
            okSince = -1; last = failure; return false;
        }
        if (okSince < 0) okSince = now;
        return now - okSince >= seconds;
    }
    static void GrantFocus()
    {
        foreach (var handoff in UnityEngine.Object.FindObjectsByType<PhotonMasterHandoff>(FindObjectsSortMode.None))
            typeof(PhotonMasterHandoff).GetField("focused", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(handoff, true);
    }

    static void Grip(bool down)
    {
        var rig = SimulatedRigInput.Instance;
        if (rig == null) return;
        rig.UseInjectedInput = true;
        var frame = rig.InjectedInput; frame.grip = down; rig.InjectedInput = frame;
    }
    static void ClearInput()
    {
        var rig = SimulatedRigInput.Instance;
        if (rig != null) { rig.InjectedInput = default; rig.UseInjectedInput = false; }
    }

    // ---------- Gear ----------

    static readonly FieldInfo EquippedActor = typeof(HeadEquipment).GetField("equippedActorNumber", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo EquipPoint = typeof(HeadEquipment).GetField("equipPoint", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo EffectActive = typeof(NightVisionGoggles).GetField("effectActive", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo SpotLight = typeof(NightVisionGoggles).GetField("spotLight", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo BackZone = typeof(BagInventory).GetField("backZone", BindingFlags.Instance | BindingFlags.NonPublic);
    static string Tag(PhotonView v) => v != null && v.InstantiationData is object[] d && d.Length > 0 ? d[0] as string : null;
    static List<HeadEquipment> AllGear(string key) => UnityEngine.Object.FindObjectsByType<HeadEquipment>(FindObjectsSortMode.None)
        .Where(h => h.GetComponent<PhotonView>() is PhotonView v && v.ViewID != 0 && EquipmentInventory.PrefabKey(h) == key).ToList();
    static List<HeadEquipment> Gear(string key) => AllGear(key).Where(h => Tag(h.GetComponent<PhotonView>()) == TestTag).ToList();
    static HeadEquipment One(string key) { var l = Gear(key); return l.Count == 1 ? l[0] : null; }
    static string Text(BagObject bag) => bag.BagId + "|" + string.Join("|", bag.Keys());
    static List<BagObject> Bags() => BagObject.Active.Where(b => b != null && b.photonView != null && b.photonView.ViewID != 0 && Text(b) == BagText).ToList();
    static int ViewId(Component c) => c.GetComponent<PhotonView>().ViewID;
    static PhotonGrabbable Net(HeadEquipment h) => h.GetComponent<PhotonGrabbable>();
    static Grabbable G(HeadEquipment h) => h.GetComponent<Grabbable>();
    static int Wearer(HeadEquipment h) => (int)EquippedActor.GetValue(h);
    static bool OnSomeone(Component c) => c.GetComponentInParent<PhotonPlayer>() != null || c.GetComponentInParent<GR.Player>() != null;
    static PlayerHand Left => GR.Player.instance.leftPlayerHand;
    static PlayerHand Right => GR.Player.instance.rightPlayerHand;
    static float Flat(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }
    static string V3(Vector3 v) => v.x.ToString("F2") + "," + v.y.ToString("F2") + "," + v.z.ToString("F2");
    static void PlaceAt(Component c, Vector3 position)
    {
        c.transform.position = position;
        if (c.TryGetComponent(out Rigidbody body)) { body.position = position; if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; } }
    }
    static string Saved(string key) => UserPlayerPrefs.GetString(key, "");
    static string Saves() => "saves head='" + Saved("equipment.head") + "' eyes='" + Saved("equipment.eyes") + "' bag.back='" + Saved("bag.back") + "'";
    static bool NvgActive(HeadEquipment goggles) => goggles != null && goggles.GetComponent<NightVisionGoggles>() is NightVisionGoggles n && (bool)EffectActive.GetValue(n);
    static bool NvgLight(HeadEquipment goggles) => goggles != null && goggles.GetComponent<NightVisionGoggles>() is NightVisionGoggles n && SpotLight.GetValue(n) is Light l && l != null && l.enabled;
    static List<HeadEquipment> LocallyWorn() => GR.Player.instance.helmetAnchor.GetComponentsInChildren<HeadEquipment>(true).Where(h => h.IsLocallyEquipped).ToList();

    static bool IAmDowned => control.downed == Role;
    static int DownedActor => IAmDowned ? PhotonNetwork.LocalPlayer.ActorNumber : Peer()?.ActorNumber ?? 0;
    static int RescuerActor => IAmDowned ? Peer()?.ActorNumber ?? 0 : PhotonNetwork.LocalPlayer.ActorNumber;
    static Vector3 ActorPosition(int actor)
    {
        if (actor == PhotonNetwork.LocalPlayer.ActorNumber) return NewPlayer.instance.bodyCollider.bounds.center;
        var avatar = PhotonPlayer.ForActor(actor);
        return avatar != null && avatar.helmetAnchor != null ? avatar.helmetAnchor.transform.position : new Vector3(1e6f, 0, 0);
    }
    static Vector3 HeadOf(int actor)
    {
        if (actor == PhotonNetwork.LocalPlayer.ActorNumber) return GR.Player.instance.helmetAnchor.position;
        var avatar = PhotonPlayer.ForActor(actor);
        return avatar != null && avatar.helmetAnchor != null ? avatar.helmetAnchor.transform.position : new Vector3(1e6f, 0, 0);
    }
    static Receipt DownReceipt;
    /// <summary>A point beside where the downed player was put (world for off-train, train-relative aboard).</summary>
    static Vector3 BesideDowned(Vector3 offset)
    {
        var r = DownReceipt;
        return r.aboard ? Train.TransformPoint(r.local + offset) : r.world + offset;
    }

    // ---------- Checks ----------

    /// <summary>Exactly one loose test helmet, goggles and bag, nothing worn or held; near the downed duck.</summary>
    static string LooseCheck(float near, bool needDown, out string detail)
    {
        detail = "";
        int downed = DownedActor;
        if (downed == 0) return "no downed actor";
        bool down = RailExpedition.ActorDown(downed);
        if (needDown != down) return "downed #" + downed + " down=" + down + " (want " + needDown + ")";
        Vector3 at = ActorPosition(downed), head = HeadOf(downed);
        var parts = new List<string>();
        foreach (string key in new[] { Helmet, Eyes })
        {
            var all = AllGear(key); var list = Gear(key);
            if (list.Count != 1) return "room has " + list.Count + " test " + key + " copies";
            int baseline = key == Helmet ? baseHelmets : baseGoggles;
            if (baseline >= 0 && all.Count - list.Count != baseline) return key + " non-test copies " + (all.Count - list.Count) + ", baseline " + baseline;
            var h = list[0];
            if (h.IsEquipped) return key + "#" + ViewId(h) + " still equipped by #" + Wearer(h);
            if (OnSomeone(h)) return key + "#" + ViewId(h) + " parented under a player (" + h.transform.parent?.name + ")";
            if (Net(h) != null && Net(h).IsHeldByAnyPlayer) return key + "#" + ViewId(h) + " held by #" + Net(h).HeldActor;
            float d = Flat(h.transform.position, at);
            if (d > near) return key + "#" + ViewId(h) + " is " + d.ToString("F2") + " m from the downed duck";
            parts.Add(key + "#" + ViewId(h) + " loose " + d.ToString("F2") + "m dyHead=" + (h.transform.position.y - head.y).ToString("F2") + " owner#" + h.GetComponent<PhotonView>().OwnerActorNr + " @" + V3(h.transform.position));
        }
        var bags = Bags();
        if (bags.Count != 1) return "room has " + bags.Count + " test bags";
        var bag = bags[0];
        if (bag.State != BagState.Dropped) return "test bag state " + bag.State + " holder #" + bag.HolderActor;
        float bd = Flat(bag.transform.position, at);
        if (bd > near) return "test bag is " + bd.ToString("F2") + " m from the downed duck";
        parts.Add("bag#" + bag.photonView.ViewID + " Dropped " + bd.ToString("F2") + "m @" + V3(bag.transform.position));
        if (IAmDowned)
        {
            if (RailExpedition.LocalDown != needDown) return "LocalDown " + RailExpedition.LocalDown;
            var worn = LocallyWorn();
            if (worn.Count != 0) return "downed still wears " + string.Join(",", worn.Select(EquipmentInventory.PrefabKey));
            if (BagInventory.Local.BackBag != null) return "downed still has a back bag";
            if (Saved("equipment.head") != "" || Saved("equipment.eyes") != "" || Saved("bag.back") != "") return "downed " + Saves();
            var g = One(Eyes);
            if (NvgActive(g) || NvgLight(g)) return "NVG effect still active=" + NvgActive(g) + " light=" + NvgLight(g);
            parts.Add("self: nothing worn, no back bag, " + Saves() + ", NVG effect off");
        }
        else
        {
            var onDowned = UnityEngine.Object.FindObjectsByType<HeadEquipment>(FindObjectsSortMode.None).Where(h => h.IsEquipped && Wearer(h) == downed).ToList();
            if (onDowned.Count != 0) return "observer sees " + onDowned.Count + " head item(s) equipped by downed #" + downed;
            parts.Add("observer: no head item equipped by #" + downed);
        }
        detail = "downed #" + downed + " (" + control.downed + ") down=" + down + " totals helmet=" + AllGear(Helmet).Count + " goggles=" + AllGear(Eyes).Count + " | " + string.Join("; ", parts);
        return null;
    }

    static string WornCheck(out string detail)
    {
        detail = "";
        int downed = DownedActor;
        var helmet = One(Helmet); var goggles = One(Eyes); var bags = Bags();
        if (helmet == null || goggles == null) return "test helmet " + Gear(Helmet).Count + " goggles " + Gear(Eyes).Count;
        if (bags.Count != 1) return "test bags " + bags.Count;
        foreach (var h in new[] { helmet, goggles })
        {
            if (!h.IsEquipped || Wearer(h) != downed) return EquipmentInventory.PrefabKey(h) + " equipped " + h.IsEquipped + " by #" + Wearer(h);
            float d = Vector3.Distance(h.transform.position, HeadOf(downed));
            if (d > .05f) return EquipmentInventory.PrefabKey(h) + " " + d.ToString("F3") + " m from head anchor";
        }
        if (bags[0].State != BagState.Stowed || bags[0].HolderActor != downed) return "bag " + bags[0].State + " holder #" + bags[0].HolderActor;
        if (IAmDowned)
        {
            if (!helmet.IsLocallyEquipped || !goggles.IsLocallyEquipped) return "not locally equipped";
            if (BagInventory.Local.BackBag != bags[0]) return "back bag not the test bag";
            if (Saved("equipment.head") != Helmet || Saved("equipment.eyes") != Eyes || Saved("bag.back") != BagText) return Saves();
            if (!NvgActive(goggles)) return "NVG effect not active";
        }
        detail = "#" + downed + " wears helmet#" + ViewId(helmet) + " goggles#" + ViewId(goggles) + " bag#" + bags[0].photonView.ViewID
            + (IAmDowned ? " (self " + Saves() + ", NVG active)" : " (observer)") + " totals helmet=" + AllGear(Helmet).Count + " goggles=" + AllGear(Eyes).Count;
        return null;
    }

    // ---------- Grab and re-wear (PlayerHand / BagInventory paths) ----------

    /// <summary>Own item: moved to the hand then an injected grip press; peer item: grip held and PlayerHand.TryGrab (ownership request).</summary>
    static bool Take(HeadEquipment item, PlayerHand hand, out string detail)
    {
        detail = null;
        double now = EditorApplication.timeSinceStartup;
        var net = Net(item);
        bool mine = item.GetComponent<PhotonView>().IsMine;
        if (!grabbed)
        {
            if (subAt == 0) { Grip(false); subAt = now; return false; }
            if (mine && attempts < 2)
            {
                PlaceAt(item, hand.HoldPoint.position);
                if (now - subAt < .3) return false;
                Grip(true); Log("GRIP press (own item " + EquipmentInventory.PrefabKey(item) + "#" + ViewId(item) + ") attempt " + (attempts + 1));
            }
            else
            {
                Grip(true); bool ok = hand.TryGrab(G(item));
                Log("TryGrab " + EquipmentInventory.PrefabKey(item) + "#" + ViewId(item) + " owner#" + item.GetComponent<PhotonView>().OwnerActorNr + " -> " + ok + " attempt " + (attempts + 1));
            }
            grabbed = true; subAt = now; holdOkSince = -1;
            return false;
        }
        bool held = G(item).Holder == hand && G(item).IsHeld && (net == null || !net.PickupPending) && item.GetComponent<PhotonView>().IsMine;
        if (!held)
        {
            holdOkSince = -1;
            last = "not held: holder=" + G(item).Holder + " pending=" + net?.PickupPending + " mine=" + item.GetComponent<PhotonView>().IsMine;
            if (now - subAt > 4) { attempts++; Require(attempts < 5, "Could not pick up " + EquipmentInventory.PrefabKey(item) + ": " + last); Log("  pickup not confirmed: " + last); grabbed = false; subAt = 0; }
            return false;
        }
        if (holdOkSince < 0) holdOkSince = now;
        if (now - holdOkSince < 1.5) return false;
        detail = "holds " + EquipmentInventory.PrefabKey(item) + "#" + ViewId(item) + " in " + (hand.IsControllerLTouch ? "left" : "right") + " (now owner, pickup confirmed, " + (mine ? "own item" : "was peer-owned") + ")";
        return true;
    }

    /// <summary>The held item is brought to the head anchor and the hand lets go: HeadEquipment.HandleReleased equips it.</summary>
    static void Rewear(HeadEquipment item, PlayerHand hand)
    {
        var anchor = GR.Player.instance.helmetAnchor;
        var point = EquipPoint.GetValue(item) as Transform;
        Vector3 delta = anchor.position - (point != null ? point.position : item.transform.position);
        PlaceAt(item, item.transform.position + delta);
        hand.Release();
        Grip(false);
        Log("RELEASE " + EquipmentInventory.PrefabKey(item) + "#" + ViewId(item) + " at the head anchor; equipped=" + item.IsLocallyEquipped);
    }

    // ---------- Account backup ----------

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
        var back = BagInventory.Local.BackBag;
        if (back != null && back.photonView.IsMine) PhotonNetwork.Destroy(back.gameObject);
        foreach (var item in LocallyWorn())
            if (item.GetComponent<PhotonView>().IsMine) PhotonNetwork.Destroy(item.gameObject);
        foreach (string key in SaveKeys) UserPlayerPrefs.DeleteKey(key);
        UserPlayerPrefs.Save();
    }
    static void DestroyOwnTestGear()
    {
        foreach (var h in Gear(Helmet).Concat(Gear(Eyes))) if (h.GetComponent<PhotonView>().IsMine) { Log("CLEAN destroy " + EquipmentInventory.PrefabKey(h) + "#" + ViewId(h)); PhotonNetwork.Destroy(h.gameObject); }
        foreach (var b in Bags()) if (b.photonView.IsMine) { Log("CLEAN destroy bag#" + b.photonView.ViewID); PhotonNetwork.Destroy(b.gameObject); }
    }

    static void Finish(string message)
    {
        done = true;
        Grip(false);
        try
        {
            if (PhotonNetwork.InRoom)
            {
                foreach (var hand in new[] { Left, Right }) if (hand != null && hand.Held != null) hand.Release();
                DestroyOwnTestGear();
            }
        }
        catch (Exception e) { Log("cleanup error: " + e.Message); }
        restoreAt = EditorApplication.timeSinceStartup + 2;
        Log(message);
    }

    // ---------- Step machine ----------

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
                token = control.token; revision = -1; done = false; backedUp = false; baseHelmets = baseGoggles = -1; attacker = null;
                Log("START down-head-gear two-client playtest; Unity " + Application.unityVersion + " role=" + Role + " privateCode=" + control.privateCode);
            }
            if (done) return;
            string state = PhotonNetwork.NetworkClientState + " " + Where() + " ready=" + PhotonSession.Instance?.WorldReady + " players=" + (PhotonNetwork.InRoom ? ActivePlayers + "/" + PhotonNetwork.PlayerList.Length : "0") + " down=" + RailExpedition.LocalDown;
            if (state != networkState) { networkState = state; Log("STATE " + state); }
            foreach (string role in Roles)
                if (ReadReceipt(role)?.error is string error && error.Length > 0) throw new Exception("Peer " + role + " failure: " + error);
            if (revision != control.revision)
            {
                revision = control.revision; entered = EditorApplication.timeSinceStartup;
                action = ack = left = rejoined = grabbed = sawPeerLeave = false; readySince = 0; okSince = -1; last = ""; acted = 0; subAt = 0; attempts = 0; holdOkSince = -1;
                pendingReceipt = null;
                string label = "";
                if (control.step >= RoundBase) { int r = (control.step - RoundBase) / RoundSpan, p = (control.step - RoundBase) % RoundSpan; if (r < Rounds.Length) label = " round " + (r + 1) + " " + (P)p + " downed=" + control.downed + ": " + Rounds[r].name; }
                Log("step=" + control.step + label);
                if (control.step >= RoundBase && (control.step - RoundBase) % RoundSpan > (int)P.Down)
                    DownReceipt = JsonUtility.FromJson<Receipt>(File.ReadAllText(Folder + "/down-" + ((control.step - RoundBase) / RoundSpan) + ".json"));
            }
            double age = EditorApplication.timeSinceStartup - entered;
            double limit = control.step >= RoundBase && (control.step - RoundBase) % RoundSpan == (int)P.LateJoin ? 300 : 180;
            Require(age < limit, "Timeout step=" + control.step + " last check: " + last + " " + Where() + " status=" + PhotonSession.Instance?.Status);
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
        if (control.step == 0)
        {
            if (!Settled || age < 8) return;
            if (!backedUp) { BackupSaves(); GrantFocus(); ClearLoadout(); backedUp = true; Log("CLEARED own loadout " + Where()); return; }
            if (Host)
            {
                if (!action) { session.RequestCreatePrivate(control.privateCode); action = true; readySince = 0; return; }
                if (!InStaging || !Settled) return;
                control.room = PhotonNetwork.CurrentRoom.Name;
            }
            Ack("account loadout backed up and cleared");
            if (Host && AllAck) Next(1);
            return;
        }
        if (control.step == 1)
        {
            if (!Host && Ready && PhotonNetwork.CurrentRoom.Name != control.room && (!action || age % 15 < .1)) { session.RequestJoinCode(control.privateCode); action = true; readySince = 0; }
            if (!(InStaging && PhotonNetwork.CurrentRoom.Name == control.room && Settled && ActivePlayers == 2 && AllPoses)) return;
            Ack("both in private staging " + control.room);
            if (Host && AllAck) Next(2);
            return;
        }
        if (control.step == 2)
        {
            GrantFocus();
            if (Host && InStaging && Settled) Nudge(session.StartPrivateRun);
            if (!Together) return;
            if (PhotonNetwork.IsMasterClient && !action) { session.SetTrainMoving(false); action = true; Log("STOP train requested (speed " + RailTrain.Instance.Speed.ToString("F2") + ")"); }
            if (age < 6) return;
            Ack("running together; train speed " + RailTrain.Instance.Speed.ToString("F2"));
            if (Host && AllAck) Next(RoundBase);
            return;
        }
        int index = (control.step - RoundBase) / RoundSpan;
        if (index >= Rounds.Length) { Finish("COMPLETE all down-head-gear rounds passed (role " + Role + ")"); return; }
        RoundStep(Rounds[index], index, (P)((control.step - RoundBase) % RoundSpan), age, session);
    }

    static void Advance(int index, P phase, Round r)
    {
        P next = phase + 1;
        if (next == P.LateJoin && !r.lateJoin) next++;
        if (next > P.Final) Next(RoundBase + (index + 1) * RoundSpan);
        else Next(RoundBase + index * RoundSpan + (int)next);
    }

    static void RoundStep(Round r, int index, P phase, double age, PhotonSession session)
    {
        GrantFocus();
        string detail = null;
        switch (phase)
        {
            case P.Clean:
            {
                if (!Together) return;
                Grip(false);
                foreach (var hand in new[] { Left, Right }) if (hand.Held != null) { Log("CLEAN release " + hand.Held.name); hand.Release(); }
                if (!action) { Board(); ClearLoadout(); DestroyOwnTestGear(); action = true; acted = EditorApplication.timeSinceStartup; return; }
                if (Gear(Helmet).Count + Gear(Eyes).Count + Bags().Count > 0) { last = "test gear still in room"; Nudge(DestroyOwnTestGear, 3); okSince = -1; return; }
                if (LocallyWorn().Count != 0 || BagInventory.Local.BackBag != null) { last = "still wearing something"; Nudge(ClearLoadout, 3); okSince = -1; return; }
                if (!Stable(() => null, 2)) return;
                baseHelmets = AllGear(Helmet).Count; baseGoggles = AllGear(Eyes).Count;
                Ack("clean; master=" + PhotonNetwork.IsMasterClient + "; non-test copies in room: helmets=" + baseHelmets + " goggles=" + baseGoggles + "; " + Saves());
                if (Host && AllAck)
                {
                    control.downed = Roles.First(role => ReadReceipt(role).master == r.master);
                    Log("ROUND " + (index + 1) + " downed role " + control.downed + " (master=" + r.master + "): " + r.name);
                    Advance(index, phase, r);
                }
                return;
            }
            case P.Wear:
            {
                if (!Together) return;
                if (IAmDowned && !action)
                {
                    var player = GR.Player.instance; var anchor = player.helmetAnchor;
                    RoomRpc.SpawnBag(BagId, BagKeys, player.transform.position + Vector3.up * .4f, Quaternion.identity, BagObject.Target.Back, storePurchase: true);
                    foreach (string key in new[] { Helmet, Eyes })
                    {
                        var prefab = BagObject.ResolvePrefab(key);
                        Require(prefab != null, "Cannot resolve " + key);
                        PhotonPreparePool.Register(prefab);
                        var go = PhotonNetwork.InstantiateWithRoomLifetime(prefab.name, anchor.position, Quaternion.identity, 0,
                            RailCargo.WithCompoundPurchase(new object[] { TestTag, Role }, PhotonNetwork.LocalPlayer.UserId));
                        Require(go != null, "Could not spawn " + key);
                        RailRemoteBridge.Call(go.GetComponent<HeadEquipment>(), "Equip", anchor);
                        Log("WEAR spawned and equipped " + key + " view " + go.GetComponent<PhotonView>().ViewID);
                    }
                    action = true;
                }
                if (!Stable(() => WornCheck(out detail), 2)) return;
                Ack("worn: " + detail);
                if (Host && AllAck) Advance(index, phase, r);
                return;
            }
            case P.Down:
            {
                if (!InTestRoom || !Running || !Ready) return;
                double now = EditorApplication.timeSinceStartup;
                if (IAmDowned)
                {
                    if (!action)
                    {
                        var rec = new Receipt();
                        if (r.how == How.AboardEnemy) { rec.local = new Vector3(0, .35f, -2); rec.aboard = true; }
                        else rec.local = new Vector3(7, .5f, 0);
                        rec.world = Train.TransformPoint(rec.local);
                        Teleport(rec.world);
                        pendingReceipt = rec; action = true; acted = now;
                        Log("WARP " + (rec.aboard ? "aboard" : "off the train") + " to " + V3(rec.world) + " wearing helmet+NVG+bag; train speed " + RailTrain.Instance.Speed.ToString("F2"));
                        return;
                    }
                    if (!RailExpedition.LocalDown && now - acted > 1.5)
                    {
                        if (r.how == How.OffSelf) Nudge(() => { RailExpedition.RequestDown(); Log("REQUEST down (self) aboard=" + RailTrain.Instance.Contains(ActorPosition(PhotonNetwork.LocalPlayer.ActorNumber))); }, 1);
                        else if (r.how == How.OffEnemy)
                        {
                            var enemy = Enemy.Active.FirstOrDefault(e => e != null && e.GetComponent<PhotonView>() != null);
                            if (enemy == null) { last = "no active enemy"; return; }
                            Nudge(() => { RailExpedition.RequestEnemyDown(enemy); Log("REQUEST enemy-down by " + enemy.name + " view " + enemy.GetComponent<PhotonView>().ViewID + " (off train)"); }, 1);
                        }
                        else
                        {
                            Require(PhotonNetwork.IsMasterClient, "Aboard-enemy round needs the downed role to be master");
                            if (attacker == null)
                            {
                                attacker = Enemy.Active.FirstOrDefault(e => e != null && e.HasAuthority && e.Boarding != null && e.Locomotion is GroundLocomotion && !e.IsKnocked && !e.IsStunned);
                                if (attacker == null) { last = "no authoritative ground enemy"; return; }
                                using (var mover = new SerializedObject((MonoBehaviour)attacker.Locomotion))
                                {
                                    mover.FindProperty("walkSpeed").floatValue = 0;
                                    mover.FindProperty("chaseSpeed").floatValue = 0;
                                    mover.ApplyModifiedPropertiesWithoutUndo();
                                }
                                attacker.Teleport(Train.TransformPoint(new Vector3(.5f, .03f, -2)), Train.rotation);
                                attacker.Boarding.SetRemoteFrame(true, false); attacker.IsDormant = false;
                                Log("ENEMY " + attacker.name + " view " + attacker.GetComponent<PhotonView>().ViewID + " put aboard beside the downed role (speeds frozen); aboard=" + attacker.Boarding.IsAboard);
                                acted = now; return;
                            }
                            Nudge(() =>
                            {
                                attacker.Teleport(Train.TransformPoint(new Vector3(.5f, .03f, -2)), Train.rotation);
                                if (!attacker.Boarding.IsAboard) attacker.Boarding.SetRemoteFrame(true, false);
                                attacker.IsDormant = false;
                                RailExpedition.Position(PhotonNetwork.LocalPlayer.ActorNumber, out var headPos);
                                RailExpedition.RequestEnemyDown(attacker);
                                Log("REQUEST enemy-down by boarded " + attacker.name + " aboard=" + attacker.Boarding.IsAboard + " dormant=" + attacker.IsDormant + " knocked=" + attacker.IsKnocked
                                    + " stunned=" + attacker.IsStunned + " dist=" + Vector3.Distance(attacker.Root.position, headPos).ToString("F2") + " selfAboard=" + RailTrain.Instance.Contains(headPos));
                            }, 1);
                        }
                        return;
                    }
                    if (!RailExpedition.LocalDown) return;
                    if (attacker != null)
                    {
                        Log("DOWN confirmed; removing the boarded enemy " + attacker.name);
                        attacker.Boarding.SetRemoteFrame(false, false);
                        PhotonNetwork.Destroy(attacker.gameObject); attacker = null;
                    }
                }
                else
                {
                    if (!action) { Teleport(Train.TransformPoint(new Vector3(-1.5f, .35f, -6))); action = true; Log("STAND away aboard while the peer goes down"); }
                    if (!RailExpedition.ActorDown(DownedActor)) { last = "peer not down yet"; return; }
                }
                if (!Stable(() => LooseCheck(DropNear, true, out detail), 5)) return;
                if (IAmDowned && !ack) File.WriteAllText(Folder + "/down-" + index + ".json", JsonUtility.ToJson(pendingReceipt));
                Ack("DROP " + (IAmDowned ? "self downed (master=" + PhotonNetwork.IsMasterClient + ", " + (r.how == How.AboardEnemy ? "aboard, boarded-enemy" : r.how == How.OffEnemy ? "off train, enemy-down" : "off train, self down") + ")" : "observer") + ": " + detail);
                if (Host && AllAck) Advance(index, phase, r);
                return;
            }
            case P.Revive:
            {
                if (!InTestRoom || !Running || !Ready) return;
                double now = EditorApplication.timeSinceStartup;
                if (!IAmDowned)
                {
                    bool down = RailExpedition.ActorDown(DownedActor);
                    if (down && (!action || now - acted > 12))
                    {
                        float side = attempts % 2 == 0 ? .5f : .35f;
                        Teleport(BesideDowned(new Vector3(side, 0, DownReceipt.aboard ? .5f : 0)));
                        action = true; acted = now; attempts++;
                        Log("RESCUER teleported beside the downed duck (attempt " + attempts + ")");
                        Require(attempts < 5, "Revive never completed");
                        return;
                    }
                    if (down) { last = "still down"; return; }
                    if (!rejoined) { rejoined = true; Teleport(BesideDowned(new Vector3(DownReceipt.aboard ? -1f : -2f, 0, DownReceipt.aboard ? -3f : 0))); Log("REVIVED; rescuer steps away"); return; }
                }
                else if (RailExpedition.LocalDown) { last = "self still down"; return; }
                if (!Stable(() => LooseCheck(LaterNear, false, out detail), 5)) return;
                Ack("AFTER REVIVE (nothing returned automatically): " + detail);
                if (Host && AllAck) Advance(index, phase, r);
                return;
            }
            case P.LateJoin:
            {
                if (!IAmDowned)
                {
                    if (!left)
                    {
                        if (InTestRoom && Running && Settled) { session.LeaveCurrentRoom(); left = true; Log("LEAVE the running expedition (Back to Compound) as master=" + PhotonNetwork.IsMasterClient); }
                        return;
                    }
                    if (!InTestRoom)
                    {
                        if (Ready && !PhotonSession.Instance.HasRoomRequest && !InTestRoom && Settled && (!rejoined || age % 15 < .1))
                        { session.RequestJoinCode(control.privateCode); rejoined = true; readySince = 0; Log("REJOIN by code from " + PhotonNetwork.CurrentRoom?.Name + " kind=" + PhotonSession.Kind); }
                        return;
                    }
                    if (!Together) return;
                }
                else
                {
                    var peer = Peer();
                    if (peer == null || peer.IsInactive) { if (!sawPeerLeave) { sawPeerLeave = true; Log("PEER left; master now " + PhotonNetwork.IsMasterClient); } return; }
                    if (!sawPeerLeave || !Together) return;
                }
                if (!Stable(() => LooseCheck(LaterNear, false, out detail), 5)) return;
                Ack((IAmDowned ? "STAYER (revived) after peer's late join" : "LATE JOINER sees") + " master=" + PhotonNetwork.IsMasterClient + ": " + detail);
                if (Host && AllAck) Advance(index, phase, r);
                return;
            }
            case P.TakeHelmet: case P.TakeGoggles:
            {
                if (!Together) return;
                bool helmetPhase = phase == P.TakeHelmet;
                bool taker = helmetPhase ? !IAmDowned : IAmDowned;
                var item = One(helmetPhase ? Helmet : Eyes);
                Require(item != null, "test item missing");
                if (taker)
                {
                    if (!action) { Teleport(BesideDowned(new Vector3(.8f, 0, DownReceipt.aboard ? -.8f : .8f))); action = true; acted = EditorApplication.timeSinceStartup; return; }
                    if (EditorApplication.timeSinceStartup - acted < 1.5) return;
                    if (!ack && !Take(item, Right, out detail)) return;
                    if (!ack) Ack("TAKE " + detail);
                }
                else
                {
                    int takerActor = helmetPhase ? RescuerActor : DownedActor;
                    if (!Stable(() => Net(item).HeldActor == takerActor && Net(item).IsHeldByAnyPlayer ? null : "peer pickup not seen (held by #" + Net(item).HeldActor + ")", 1)) return;
                    Ack("SEES " + EquipmentInventory.PrefabKey(item) + "#" + ViewId(item) + " held by #" + takerActor + " owner#" + item.GetComponent<PhotonView>().OwnerActorNr);
                }
                if (Host && AllAck) Advance(index, phase, r);
                return;
            }
            case P.WearHelmet: case P.WearGoggles:
            {
                if (!Together) return;
                bool helmetPhase = phase == P.WearHelmet;
                bool wearer = helmetPhase ? !IAmDowned : IAmDowned;
                var item = One(helmetPhase ? Helmet : Eyes);
                Require(item != null, "test item missing");
                int wearerActor = helmetPhase ? RescuerActor : DownedActor;
                if (wearer)
                {
                    if (!action) { Require(G(item).Holder == Right, "item not in the right hand"); Rewear(item, Right); action = true; return; }
                    if (!Stable(() =>
                    {
                        if (!item.IsLocallyEquipped || item.transform.parent != GR.Player.instance.helmetAnchor) return "not re-equipped (equipped=" + item.IsEquipped + ")";
                        string want = helmetPhase ? Helmet : Eyes;
                        if (Saved(helmetPhase ? "equipment.head" : "equipment.eyes") != want) return Saves();
                        if (!helmetPhase && !NvgActive(item)) return "NVG effect not active after re-wear";
                        return null;
                    }, 2)) return;
                    Ack("REWEAR " + EquipmentInventory.PrefabKey(item) + "#" + ViewId(item) + " worn by self #" + wearerActor + "; " + Saves() + (helmetPhase ? "" : "; NVG effect active=" + NvgActive(item)));
                }
                else
                {
                    if (!Stable(() => item.IsEquipped && Wearer(item) == wearerActor && Vector3.Distance(item.transform.position, HeadOf(wearerActor)) < .05f ? null
                        : "peer re-wear not seen (equipped=" + item.IsEquipped + " by #" + Wearer(item) + ")", 2)) return;
                    Ack("SEES " + EquipmentInventory.PrefabKey(item) + "#" + ViewId(item) + " worn by #" + wearerActor + "; NVG effect active here=" + NvgActive(item) + "; " + Saves());
                }
                if (Host && AllAck) Advance(index, phase, r);
                return;
            }
            case P.TakeBag: case P.WearBag:
            {
                if (!Together) return;
                var bags = Bags(); Require(bags.Count == 1, "test bags " + bags.Count);
                var bag = bags[0];
                int downed = DownedActor;
                if (IAmDowned)
                {
                    double now = EditorApplication.timeSinceStartup;
                    if (phase == P.TakeBag)
                    {
                        if (!ack)
                        {
                            if (!action) { Grip(false); action = true; subAt = now; return; }
                            if (!grabbed)
                            {
                                PlaceAt(bag, Right.transform.position);
                                if (now - subAt < .3) return;
                                if (attempts < 2) Grip(true); else { Grip(true); BagInventory.Local.OnGripPressed(Right); }
                                Log("GRIP press on own dropped bag attempt " + (attempts + 1)); grabbed = true; subAt = now; holdOkSince = -1; return;
                            }
                            if (!(bag.State == BagState.Held && bag.HolderActor == PhotonNetwork.LocalPlayer.ActorNumber && !bag.AwaitingClaim))
                            {
                                holdOkSince = -1; last = "bag " + bag.State + " holder #" + bag.HolderActor;
                                if (now - subAt > 4) { attempts++; Require(attempts < 5, "Could not pick up the bag: " + last); Grip(false); grabbed = false; subAt = now; }
                                return;
                            }
                            if (holdOkSince < 0) holdOkSince = now;
                            if (now - holdOkSince < 1.5) return;
                            Ack("TAKE bag#" + bag.photonView.ViewID + " in right hand (" + (attempts < 2 ? "injected grip press" : "direct OnGripPressed") + ")");
                        }
                    }
                    else
                    {
                        if (!action)
                        {
                            var zone = BackZone.GetValue(BagInventory.Local) as Component;
                            Require(zone != null, "no back zone");
                            PlaceAt(bag, zone.transform.position);
                            BagInventory.Local.OnGripReleased(Right);
                            Grip(false); action = true;
                            Log("RELEASE bag at the back zone; state " + bag.State);
                            return;
                        }
                        if (!Stable(() => BagInventory.Local.BackBag == bag && bag.State == BagState.Stowed && Saved("bag.back") == BagText ? null : "bag not re-worn: " + bag.State + " back=" + (BagInventory.Local.BackBag != null) + " " + Saves(), 2)) return;
                        Ack("REWEAR bag#" + bag.photonView.ViewID + " on own back; " + Saves());
                    }
                }
                else
                {
                    var want = phase == P.TakeBag ? BagState.Held : BagState.Stowed;
                    if (!Stable(() => bag.State == want && bag.HolderActor == downed ? null : "bag " + bag.State + " holder #" + bag.HolderActor, 1)) return;
                    Ack("SEES bag#" + bag.photonView.ViewID + " " + want + " by #" + downed);
                }
                if (Host && AllAck) Advance(index, phase, r);
                return;
            }
            case P.Final:
            {
                if (!Together) return;
                if (!Stable(() =>
                {
                    int downed = DownedActor, rescuer = RescuerActor;
                    var helmet = One(Helmet); var goggles = One(Eyes); var bags = Bags();
                    if (helmet == null || goggles == null || bags.Count != 1) return "copies helmet " + Gear(Helmet).Count + " goggles " + Gear(Eyes).Count + " bags " + bags.Count;
                    if (AllGear(Helmet).Count != baseHelmets + 1 || AllGear(Eyes).Count != baseGoggles + 1) return "totals changed: helmets " + AllGear(Helmet).Count + " (non-test baseline " + baseHelmets + ") goggles " + AllGear(Eyes).Count + " (" + baseGoggles + ")";
                    if (!helmet.IsEquipped || Wearer(helmet) != rescuer) return "helmet by #" + Wearer(helmet);
                    if (!goggles.IsEquipped || Wearer(goggles) != downed) return "goggles by #" + Wearer(goggles);
                    if (bags[0].State != BagState.Stowed || bags[0].HolderActor != downed) return "bag " + bags[0].State;
                    if (IAmDowned)
                    {
                        if (Saved("equipment.head") != "" || Saved("equipment.eyes") != Eyes || Saved("bag.back") != BagText) return Saves();
                        if (!NvgActive(goggles)) return "NVG not active";
                    }
                    else
                    {
                        if (Saved("equipment.head") != Helmet || Saved("equipment.eyes") != "" || Saved("bag.back") != "") return Saves();
                        if (NvgActive(goggles)) return "NVG effect active on the non-wearer";
                    }
                    detail = "helmet#" + ViewId(helmet) + " on rescuer #" + rescuer + ", goggles#" + ViewId(goggles) + " + bag#" + bags[0].photonView.ViewID + " on revived #" + downed
                        + "; 1 copy each; totals helmet=" + AllGear(Helmet).Count + " goggles=" + AllGear(Eyes).Count + "; self " + Saves() + "; NVG here=" + NvgActive(goggles);
                    return null;
                }, 5)) return;
                Ack("FINAL " + detail);
                if (Host && AllAck)
                {
                    Log("CHECK round " + (index + 1) + " PASS (downed " + control.downed + ", master=" + r.master + ") " + r.name);
                    Advance(index, phase, r);
                }
                return;
            }
        }
    }
}
#endif
