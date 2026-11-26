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
/// Opt-in two-client playtest for "Held items carry between Compound and Expedition" (playtest-queue.md, RailHeldCarry).
/// Each round gives each role a load (one item, two items one per hand, or its own loaded basic bag), takes it through the
/// game's grip path (the item or bag is moved to the hand, then an injected simulated-rig grip press runs PlayerHand's
/// grab / BagInventory.OnGripPressed), waits for a confirmed local hold (IsHeld, no pending pickup) that the peer also
/// sees, then runs a transition. On arrival each client checks every role in its room: exactly the carried copies, loose
/// (not in a hand) within 1.5 m of the carrier, bag contents intact, originals gone, held for 5 s. A client left behind
/// checks the old world for the departed carrier's originals. A downed carrier must carry nothing.
/// Account loadout saves are backed up and restored. Logs: Temp/rails-held-carry/{main,player2}.txt.
/// Runtime game code is not instrumented. No diagnostic report access.
/// </summary>
[InitializeOnLoad]
public static class RailHeldCarryLiveChecks
{
    [Serializable] sealed class Control
    {
        public string token, expires, compound, privateCode;
        public int step, revision;
    }
    [Serializable] sealed class Receipt
    {
        public string token, room, note, error, views;
        public int revision, actor;
        public bool master;
    }
    enum T { PubAlone, LeaveToCompound, JoinCompound, PubTogether, CrewFail, CrewSuccess, CreatePrivate, JoinPrivate, PrivDepart, PrivFail, PrivSuccess, DownReturn, LeaveMidRun }
    sealed class Round { public string name, main, p2; public T t; }
    static Round R(T t, string main, string p2, string name) => new Round { t = t, main = main, p2 = p2, name = name };

    // Loads: "" nothing, "one" flashlight in the right hand, "two" coal left + gold bar right, "bag" the role's basic bag in the right hand.
    static readonly Round[] Rounds =
    {
        R(T.PubAlone, "", "one", "public Compound -> public Expedition, carrier departs alone (non-master in Compound); stayer checks the old Compound"),
        R(T.LeaveToCompound, "", "two", "public Expedition -> Compound via Back to Compound (carrier alone, master)"),
        R(T.JoinCompound, "", "bag", "Compound -> other public Compound by code (carrier joins the peer)"),
        R(T.PubTogether, "bag", "two", "public Compound -> public Expedition, both board carrying"),
        R(T.CrewFail, "one", "bag", "public Expedition -> Compound, failure crew return, both carrying"),
        R(T.PubTogether, "two", "one", "public Compound -> public Expedition, both board carrying (2nd)"),
        R(T.CrewSuccess, "bag", "two", "public Expedition -> Compound, success crew return, both carrying"),
        R(T.CreatePrivate, "bag", "", "public Compound -> private staging (create), carrier leaves; stayer checks the old Compound"),
        R(T.JoinPrivate, "", "two", "public Compound -> private staging (join by code), non-master carrier"),
        R(T.PrivDepart, "one", "bag", "private departure (staging -> Expedition, same room)"),
        R(T.PrivFail, "two", "one", "private failure return (Expedition -> staging, same room)"),
        R(T.PrivDepart, "bag", "two", "private departure (2nd)"),
        R(T.PrivSuccess, "one", "bag", "private success return (Expedition -> staging, same room)"),
        R(T.PrivDepart, "two", "one", "private departure (3rd)"),
        R(T.DownReturn, "one", "two", "private return with the non-master carrier downed (downed carries nothing)"),
        R(T.PrivDepart, "bag", "one", "private departure (4th)"),
        R(T.LeaveMidRun, "", "bag", "private Expedition -> public Compound via Back to Compound mid-run; stayer (master) checks the old Expedition"),
    };

    const string Key = "Rails.HeldCarryLive";
    static readonly string[] Roles = { "main", "player2" };
    static string Root { get { var p = Application.dataPath.Replace('\\', '/'); var i = p.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase); return i < 0 ? Path.GetDirectoryName(p) : p.Substring(0, i); } }
    static string Folder => Root + "/Temp/rails-held-carry";
    static string ControlPath => Folder + "/control.json";
    static string DownPath => Folder + "/player2-down.txt";
    static bool Host => CurrentPlayer.IsMainEditor;
    static string Role => Host ? "main" : "player2";
    static string Other => Host ? "player2" : "main";
    static string ReceiptPath(string role) => Folder + "/" + role + ".json";
    static string BackupPath(string role) => Folder + "/" + role + "-backup.txt";

    const string TestTag = "held-carry-test", CarryTag = "rails-carry-v1";
    const byte BagId = 2;
    static readonly string[] MainBag = { "RailCoal", "RailGoldBar", "RailShopItemFlashlight", "" };
    static readonly string[] P2Bag = { "RailGoldBar", "", "RailCoal", "RailShopItemFlashlight" };
    static readonly string[] ItemKeys = { "RailCoal", "RailGoldBar", "RailShopItemFlashlight" };
    static readonly string[] SaveKeys = { "bag.back", "bag.back.room", "bag.back.identity",
        "equipment.head", "equipment.head.room", "equipment.head.identity",
        "equipment.eyes", "equipment.eyes.room", "equipment.eyes.identity" };
    const double StableSeconds = 5;
    const float NearFeet = 1.5f;

    static Control control;
    static string token, networkState, last = "", heldState = "";
    static int revision = -1, sub, attempts;
    static bool action, ack, done, backedUp, loaded, released, downFlagged;
    static double entered, nextTick, readySince, okSince = -1, acted, restoreAt, phaseAt, holdOkSince = -1;
    // Cached at the start of a go step: own and peer load (actor, room, original view IDs).
    static int myLoadActor, peerLoadActor;
    static string myLoadRoom = "", peerLoadRoom = "";
    static HashSet<int> myLoadViews = new HashSet<int>(), peerLoadViews = new HashSet<int>();
    static bool viaFallback;

    static RailHeldCarryLiveChecks()
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

    /// <summary>Main editor, Edit Mode: arm and launch the two-client scenario. firstRound skips earlier rounds (0 = all).</summary>
    public static void Start(int firstRound = 0)
    {
        if (!CurrentPlayer.IsMainEditor || EditorApplication.isPlayingOrWillChangePlaymode || Status != "NotRunning") throw new Exception("Main editor in Edit Mode with no scenario running");
        if (!MppmStandaloneCredentials.TryGet("Player2", out _)) throw new Exception("Configure Player2 account");
        Directory.CreateDirectory(Folder);
        foreach (var role in Roles)
            if (File.Exists(BackupPath(role)) && File.ReadAllText(BackupPath(role)).Length > 0)
                throw new Exception("An unrestored loadout backup exists for " + role + "; run RestoreBackupsInEditMode first");
        foreach (var role in Roles) { File.WriteAllText(ReceiptPath(role), ""); File.WriteAllText(Folder + "/" + role + ".txt", ""); }
        File.WriteAllText(DownPath, "");
        var property = Manager.GetType().GetProperty("ActivePlayModeConfig");
        SessionState.SetString(Key + ".scenario", AssetDatabase.GetAssetPath((UnityEngine.Object)property.GetValue(Manager)));
        SessionState.SetBool(Key + ".quick", RailEditorQuickStart.Enabled);
        SessionState.SetBool(Key, true);
        SessionState.SetInt(Key + ".first", firstRound);
        RailEditorQuickStart.Enabled = false;
        var c = new Control { token = Guid.NewGuid().ToString("N"), expires = DateTime.UtcNow.AddMinutes(90).ToString("O"),
            privateCode = UnityEngine.Random.Range(10000, 99999).ToString(), step = 0 };
        File.WriteAllText(ControlPath, JsonUtility.ToJson(c));
        File.WriteAllText(Folder + "/first-round.txt", firstRound.ToString());
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
    static void Ack(string message, string views = "")
    {
        if (ack) return;
        File.WriteAllText(ReceiptPath(Role), JsonUtility.ToJson(new Receipt { token = token, revision = revision, room = PhotonNetwork.CurrentRoom?.Name, note = message,
            views = views, actor = PhotonNetwork.LocalPlayer?.ActorNumber ?? 0, master = PhotonNetwork.IsMasterClient }));
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
    static void Teleport(Vector3 position) => NewPlayer.instance.Warp(position, Quaternion.identity);
    static void StandAway() => Teleport(new Vector3(8 + Array.IndexOf(Roles, Role), .4f, 4));
    static void StandInBoarding()
    {
        if (Ready && RailWorld.Instance != null && RailWorld.Instance.boardingTrigger != null)
            Teleport(RailWorld.Instance.boardingTrigger.bounds.center - Vector3.up * .8f);
    }
    static Photon.Realtime.Player Peer() => PhotonNetwork.InRoom ? PhotonNetwork.PlayerList.Where(p => !p.IsLocal).OrderBy(p => p.IsInactive).FirstOrDefault() : null;
    static bool PeerHere => Peer() is Photon.Realtime.Player p && !p.IsInactive && PhotonPlayer.ForActor(p.ActorNumber) != null;
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

    // ---------- Simulated rig grip (the real PlayerHand / BagInventory grip path) ----------

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

    // ---------- Objects ----------

    static string Load(Round r, string role) => role == "main" ? r.main : r.p2;
    static string[] RoleBag(string role) => role == "main" ? MainBag : P2Bag;
    static string BagText(string[] keys) => BagId + "|" + string.Join("|", keys);
    static string Text(BagObject bag) => bag.BagId + "|" + string.Join("|", bag.Keys());
    static string[] LoadItems(string load) => load == "one" ? new[] { "RailShopItemFlashlight" } : load == "two" ? new[] { "RailCoal", "RailGoldBar" } : new string[0];
    static string PrefabKey(string name) { int cut = name.IndexOf('('); return (cut > 0 ? name.Substring(0, cut) : name).Trim(); }
    static string Tag(PhotonView v) => v != null && v.InstantiationData is object[] d && d.Length > 0 ? d[0] as string : null;
    static bool Tagged(PhotonView v) { var t = Tag(v); return t == TestTag || t == CarryTag; }

    /// <summary>Test or carried items (coal, gold bar, flashlight) created by this actor in the current room.</summary>
    static List<PhotonGrabbable> ItemsOf(int actor) => UnityEngine.Object.FindObjectsByType<PhotonGrabbable>(FindObjectsSortMode.None)
        .Where(g => g != null && V(g) != null && V(g).ViewID != 0 && Tagged(V(g)) && V(g).CreatorActorNr == actor
            && ItemKeys.Contains(PrefabKey(g.name)) && PhotonView.Find(V(g).ViewID) == V(g)).ToList();
    static List<BagObject> BagsOf(string role) => BagObject.Active.Where(b => b != null && b.photonView != null && b.photonView.ViewID != 0 && Text(b) == BagText(RoleBag(role))).ToList();
    static PhotonView V(Component c) => c.GetComponent<PhotonView>();
    static string Describe(PhotonGrabbable g) => PrefabKey(g.name) + "#" + V(g).ViewID + "(" + Tag(V(g)) + ")";

    static PlayerHand Left => GR.Player.instance.leftPlayerHand;
    static PlayerHand Right => GR.Player.instance.rightPlayerHand;
    static void PlaceAt(Component c, Vector3 position)
    {
        c.transform.position = position;
        if (c.TryGetComponent(out Rigidbody body)) { body.position = position; if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; } }
    }

    // ---------- Load: acquire and grab through the grip path ----------

    static List<(PlayerHand hand, PhotonGrabbable item)> assigned = new List<(PlayerHand, PhotonGrabbable)>();
    static BagObject assignedBag;
    static double subAt;

    /// <summary>Returns true while this role's load is confirmed held (stable 1.5 s); null detail while working.</summary>
    static bool DriveLoad(string load, out string detail)
    {
        detail = null;
        int me = PhotonNetwork.LocalPlayer.ActorNumber;
        double now = EditorApplication.timeSinceStartup;
        string[] keys = LoadItems(load);
        bool wantBag = load == "bag";
        switch (sub)
        {
            case 0: // Clean slate: keep one of each needed key and this role's bag; destroy the rest; spawn what is missing.
            {
                Grip(false);
                var mine = ItemsOf(me).Where(g => V(g).IsMine && !g.IsHeldByAnyPlayer).ToList();
                var keep = new List<PhotonGrabbable>();
                foreach (string k in keys)
                {
                    var found = mine.FirstOrDefault(g => PrefabKey(g.name) == k && !keep.Contains(g));
                    if (found != null) keep.Add(found);
                    else
                    {
                        var prefab = BagObject.ResolvePrefab(k);
                        Require(prefab != null, "Cannot resolve " + k);
                        PhotonPreparePool.Register(prefab);
                        var go = PhotonNetwork.InstantiateWithRoomLifetime(prefab.name, Right.HoldPoint.position + Vector3.up * .3f, Quaternion.identity, 0,
                            RailCargo.WithCompoundPurchase(new object[] { TestTag, Role }, PhotonNetwork.LocalPlayer.UserId));
                        Require(go != null, "Could not spawn " + k);
                        Log("SPAWN test " + k + " view " + go.GetComponent<PhotonView>().ViewID);
                    }
                }
                foreach (var g in mine.Where(g => !keep.Contains(g))) { Log("CLEAN destroy own " + Describe(g)); PhotonNetwork.Destroy(g.gameObject); }
                var bags = BagsOf(Role);
                var keepBag = wantBag ? bags.FirstOrDefault(b => b.State == BagState.Dropped) : null;
                foreach (var b in bags.Where(b => b != keepBag))
                {
                    if (b.photonView.IsMine) { Log("CLEAN destroy own bag view " + b.photonView.ViewID + " state " + b.State); PhotonNetwork.Destroy(b.gameObject); }
                    else Log("CLEAN cannot destroy bag view " + b.photonView.ViewID + " (owner #" + b.photonView.OwnerActorNr + ")");
                }
                if (wantBag && keepBag == null)
                {
                    RoomRpc.SpawnBag(BagId, RoleBag(Role), Right.transform.position + Vector3.up * .3f, Quaternion.identity, BagObject.Target.Dropped, storePurchase: true);
                    Log("SPAWN test bag " + BagText(RoleBag(Role)));
                }
                sub = 1; subAt = now; attempts = 0; viaFallback = false;
                return false;
            }
            case 1: // Resolve the objects and move them to the hands.
            {
                Grip(false);
                assigned.Clear(); assignedBag = null;
                var mine = ItemsOf(me).Where(g => V(g).IsMine && !g.IsHeldByAnyPlayer).ToList();
                var hands = keys.Length == 2 ? new[] { Left, Right } : new[] { Right };
                for (int i = 0; i < keys.Length; i++)
                {
                    var g = mine.FirstOrDefault(x => PrefabKey(x.name) == keys[i] && !assigned.Any(a => a.item == x));
                    if (g == null) { last = "waiting for " + keys[i]; Require(now - subAt < 20, "Load item " + keys[i] + " never appeared"); return false; }
                    assigned.Add((hands[i], g));
                }
                if (wantBag)
                {
                    assignedBag = BagsOf(Role).FirstOrDefault(b => b.State == BagState.Dropped && b.photonView.IsMine && !b.AwaitingClaim);
                    if (assignedBag == null) { last = "waiting for own bag"; Require(now - subAt < 20, "Load bag never appeared/owned"); return false; }
                }
                if (Left.Held != null || Right.Held != null) { last = "hands not empty"; Require(now - subAt < 10, "Hands still hold " + Left.Held + " / " + Right.Held); return false; }
                foreach (var (hand, item) in assigned) PlaceAt(item, hand.HoldPoint.position);
                if (assignedBag != null) PlaceAt(assignedBag, Right.transform.position);
                sub = 2; subAt = now;
                return false;
            }
            case 2: // Grip press (rising edge after at least two released ticks).
                if (now - subAt < .25) { foreach (var (hand, item) in assigned) PlaceAt(item, hand.HoldPoint.position); if (assignedBag != null) PlaceAt(assignedBag, Right.transform.position); return false; }
                foreach (var (hand, item) in assigned) PlaceAt(item, hand.HoldPoint.position);
                if (assignedBag != null) PlaceAt(assignedBag, Right.transform.position);
                Grip(true);
                sub = 3; subAt = now; holdOkSince = -1;
                Log("GRIP press attempt " + (attempts + 1) + " for " + load);
                return false;
            case 3: // Confirmed local hold.
            {
                string fail = OwnHoldFailure(load, me, out string d);
                if (fail != null)
                {
                    holdOkSince = -1; last = fail;
                    if (now - subAt > 3)
                    {
                        attempts++;
                        Log("GRIP attempt " + attempts + " not confirmed: " + fail + " (left=" + Left.Held + " right=" + Right.Held + ")");
                        if (attempts >= 3 && !viaFallback)
                        {
                            // Fallback: the hand's own TryGrab / BagInventory grip handler, called directly with grip held.
                            viaFallback = true;
                            foreach (var (hand, item) in assigned) if (hand.Held == null) { PlaceAt(item, hand.HoldPoint.position); hand.TryGrab(item); }
                            if (assignedBag != null) { PlaceAt(assignedBag, Right.transform.position); BagInventory.Local.OnGripPressed(Right); }
                            subAt = now; Log("FALLBACK direct PlayerHand.TryGrab / BagInventory.OnGripPressed");
                            return false;
                        }
                        Require(attempts < 5, "Could not confirm the hold: " + fail);
                        Grip(false); sub = 1; subAt = now;
                    }
                    return false;
                }
                if (holdOkSince < 0) holdOkSince = now;
                if (now - holdOkSince < 1.5) return false;
                detail = d + (viaFallback ? " [via direct TryGrab fallback]" : " [via injected grip press]");
                return true;
            }
        }
        return false;
    }

    static string OwnHoldFailure(string load, int me, out string detail)
    {
        detail = "";
        foreach (var (hand, item) in assigned)
        {
            if (item == null) return "assigned item destroyed";
            if (item.Holder != hand || !item.IsHeld) return Describe(item) + " not held by " + (hand.IsControllerLTouch ? "left" : "right") + " (holder " + item.Holder + ")";
            if (item.PickupPending) return Describe(item) + " pickup pending";
            if (item.IsStashed) return Describe(item) + " stashed";
            detail += (hand.IsControllerLTouch ? "left" : "right") + "=" + Describe(item) + " ";
        }
        if (assignedBag != null)
        {
            var b = assignedBag;
            if (b.State != BagState.Held || b.HolderActor != me || b.AwaitingClaim) return "bag view " + b.photonView.ViewID + " state " + b.State + " holder #" + b.HolderActor;
            float d = Vector3.Distance(b.transform.position, Right.HoldPoint.position);
            if (d > .7f) return "bag " + d.ToString("F2") + " m from right hand";
            if (Left.Held != null) return "left hand unexpectedly holds " + Left.Held.name;
            detail += "right=bag#" + b.photonView.ViewID + "[" + Text(b) + "] ";
        }
        if (assigned.Count == 1 && Left.Held != null) return "left hand unexpectedly holds " + Left.Held.name;
        if (BagObject.Active.Any(x => x != null && x != assignedBag && x.State == BagState.Held && x.HolderActor == me)) return "an unexpected bag is held";
        return null;
    }
    static string LoadViews()
    {
        var ids = assigned.Select(a => V(a.item).ViewID).ToList();
        if (assignedBag != null) ids.Add(assignedBag.photonView.ViewID);
        return string.Join(",", ids);
    }

    /// <summary>What this client sees of the peer holding its load.</summary>
    static string PeerHoldFailure(string role, string load, int actor, out string detail)
    {
        detail = "";
        var items = ItemsOf(actor);
        var keys = LoadItems(load);
        var held = items.Where(g => g.HeldActor == actor).ToList();
        var want = keys.OrderBy(k => k).ToArray();
        var got = held.Select(g => PrefabKey(g.name)).OrderBy(k => k).ToArray();
        if (!want.SequenceEqual(got)) return "peer #" + actor + " seen holding [" + string.Join(",", got) + "], want [" + string.Join(",", want) + "]";
        var bags = BagsOf(role).Where(b => b.State == BagState.Held && b.HolderActor == actor).ToList();
        if (bags.Count != (load == "bag" ? 1 : 0)) return "peer #" + actor + " seen holding " + bags.Count + " role bags";
        detail = "peer #" + actor + " seen holding [" + string.Join(",", held.Select(Describe)) + (bags.Count > 0 ? " bag#" + bags[0].photonView.ViewID : "") + "]";
        return null;
    }

    // ---------- Arrival checks ----------

    static bool SameRoomSwap(T t) => t == T.PrivDepart || t == T.PrivFail || t == T.PrivSuccess || t == T.DownReturn;

    static Vector3 RolePosition(int actor, bool self)
    {
        if (self) return NewPlayer.instance.bodyCollider.bounds.center;
        var avatar = PhotonPlayer.ForActor(actor);
        return avatar != null && avatar.helmetAnchor != null ? avatar.helmetAnchor.transform.position : new Vector3(1e6f, 0, 0);
    }
    static float Flat(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }

    /// <summary>One role's carried copies in the current room. Null when as expected.</summary>
    static string RoleArrival(Round r, string role, int actor, bool self, HashSet<int> loadViews, bool downed, out string detail)
    {
        detail = "";
        string load = Load(r, role);
        bool same = SameRoomSwap(r.t);
        Vector3 at = RolePosition(actor, self);
        var items = ItemsOf(actor);
        var copies = same ? items.Where(g => !loadViews.Contains(V(g).ViewID)).ToList() : items;
        var originals = same ? items.Where(g => loadViews.Contains(V(g).ViewID)).ToList() : new List<PhotonGrabbable>();
        var bags = BagsOf(role);
        var bagCopies = same ? bags.Where(b => !loadViews.Contains(b.photonView.ViewID)).ToList() : bags;
        var bagOriginals = same ? bags.Where(b => loadViews.Contains(b.photonView.ViewID)).ToList() : new List<BagObject>();
        if (self && (Left.Held != null || Right.Held != null)) return role + " hands not empty: " + Left.Held + " / " + Right.Held;
        var heldAny = items.Where(g => g.IsHeldByAnyPlayer).ToList();
        if (heldAny.Count > 0) return role + " items in a hand: " + string.Join(",", heldAny.Select(Describe));
        if (bags.Any(b => b.State != BagState.Dropped)) return role + " bag not dropped: " + string.Join(",", bags.Select(b => b.State + "#" + b.photonView.ViewID));
        if (downed)
        {
            var carried = copies.Where(g => Tag(V(g)) == CarryTag).ToList();
            if (carried.Count != 0 || bagCopies.Count != 0) return role + " (downed) has carried copies: " + string.Join(",", carried.Select(Describe)) + " bags " + bagCopies.Count;
            detail = role + " #" + actor + " downed: 0 carried copies, 0 bags; originals still in room: [" + string.Join(",", originals.Select(Describe)) + "] other own items: ["
                + string.Join(",", copies.Select(Describe)) + "]";
            return null;
        }
        var want = LoadItems(load).OrderBy(k => k).ToArray();
        var got = copies.Select(g => PrefabKey(g.name)).OrderBy(k => k).ToArray();
        if (!want.SequenceEqual(got)) return role + " #" + actor + " copies [" + string.Join(",", copies.Select(Describe)) + "], want [" + string.Join(",", want) + "]";
        if (load != "" && copies.Any(g => Tag(V(g)) != CarryTag)) return role + " copies not from RailHeldCarry: " + string.Join(",", copies.Select(Describe));
        if (originals.Count != 0 || bagOriginals.Count != 0) return role + " originals still present: " + string.Join(",", originals.Select(Describe)) + " bags " + bagOriginals.Count;
        if (bagCopies.Count != (load == "bag" ? 1 : 0)) return role + " bag copies " + bagCopies.Count + " (want " + (load == "bag" ? 1 : 0) + ")";
        var parts = new List<string>();
        foreach (var g in copies)
        {
            float d = Flat(g.transform.position, at);
            if (d > NearFeet) return role + " " + Describe(g) + " is " + d.ToString("F2") + " m from the carrier";
            parts.Add(Describe(g) + " " + d.ToString("F2") + "m dy=" + (g.transform.position.y - at.y).ToString("F2"));
        }
        foreach (var b in bagCopies)
        {
            float d = Flat(b.transform.position, at);
            if (d > NearFeet) return role + " bag#" + b.photonView.ViewID + " is " + d.ToString("F2") + " m from the carrier";
            parts.Add("bag#" + b.photonView.ViewID + "[" + Text(b) + "] Dropped " + d.ToString("F2") + "m");
        }
        detail = role + " #" + actor + (load == "" ? " (no load) nothing" : "") + " [" + string.Join("; ", parts) + "]";
        return null;
    }

    /// <summary>Stayer: the departed carrier left nothing in this (old) world.</summary>
    static string OldWorld(string role, int actor, HashSet<int> loadViews, out string detail)
    {
        detail = "";
        var items = ItemsOf(actor);
        var bags = BagsOf(role);
        var originals = loadViews.Where(id => PhotonView.Find(id) != null).ToList();
        if (items.Count != 0) return "old world still has " + role + " items: " + string.Join(",", items.Select(Describe));
        if (bags.Count != 0) return "old world still has " + role + " bag(s): " + string.Join(",", bags.Select(b => "#" + b.photonView.ViewID + " " + b.State));
        if (originals.Count != 0) return "old world still has original views " + string.Join(",", originals);
        detail = "old world " + PhotonNetwork.CurrentRoom?.Name + ": 0 of " + role + "'s items/bag (departed actor #" + actor + ", originals [" + string.Join(",", loadViews) + "] gone)";
        return null;
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

    static void Finish(string message)
    {
        done = true;
        Grip(false);
        try
        {
            if (PhotonNetwork.InRoom)
            {
                foreach (var g in ItemsOf(PhotonNetwork.LocalPlayer.ActorNumber)) if (V(g).IsMine) PhotonNetwork.Destroy(g.gameObject);
                foreach (var b in BagsOf(Role)) if (b.photonView.IsMine) PhotonNetwork.Destroy(b.gameObject);
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
                token = control.token; revision = -1; done = false; backedUp = false; downFlagged = false;
                Log("START held-carry two-client playtest; Unity " + Application.unityVersion + " role=" + Role + " privateCode=" + control.privateCode);
            }
            if (done) return;
            string state = PhotonNetwork.NetworkClientState + " " + Where() + " ready=" + PhotonSession.Instance?.WorldReady + " players=" + (PhotonNetwork.InRoom ? ActivePlayers + "/" + PhotonNetwork.PlayerList.Length : "0") + " down=" + RailExpedition.LocalDown;
            if (state != networkState) { networkState = state; Log("STATE " + state); }
            foreach (string role in Roles)
                if (ReadReceipt(role)?.error is string error && error.Length > 0) throw new Exception("Peer " + role + " failure: " + error);
            if (revision != control.revision)
            {
                revision = control.revision; entered = EditorApplication.timeSinceStartup;
                action = ack = loaded = released = false; readySince = 0; okSince = -1; last = ""; sub = 0; acted = 0; phaseAt = 0;
                Log("step=" + control.step + (control.step >= 2 && (control.step - 2) / 2 < Rounds.Length ? " round " + ((control.step - 2) / 2 + 1) + " " + ((control.step % 2 == 0) ? "LOAD" : "GO") + ": " + Rounds[(control.step - 2) / 2].name : ""));
                if (control.step >= 3 && control.step % 2 == 1) CacheLoads();
            }
            double age = EditorApplication.timeSinceStartup - entered;
            Require(age < 300, "Timeout step=" + control.step + " last check: " + last + " " + Where() + " status=" + PhotonSession.Instance?.Status);
            TraceHolds();
            Step(age);
        }
        catch (IOException) { }
        catch (Exception e)
        {
            Finish("FAIL " + e.Message);
            File.WriteAllText(ReceiptPath(Role), JsonUtility.ToJson(new Receipt { token = token, revision = revision, error = e.Message }));
        }
    }

    static void CacheLoads()
    {
        var mine = ReadReceipt(Role); var peer = ReadReceipt(Other);
        myLoadActor = mine?.actor ?? 0; myLoadRoom = mine?.room ?? ""; myLoadViews = Views(mine?.views);
        peerLoadActor = peer?.actor ?? 0; peerLoadRoom = peer?.room ?? ""; peerLoadViews = Views(peer?.views);
        Log("LOADS own #" + myLoadActor + " in " + myLoadRoom + " views [" + mine?.views + "]; peer #" + peerLoadActor + " in " + peerLoadRoom + " views [" + peer?.views + "]");
    }
    static HashSet<int> Views(string s) => new HashSet<int>((s ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse));

    // Logs whenever what this client's hands hold changes (shows releases before capture, during the veil, on down).
    static void TraceHolds()
    {
        var player = GR.Player.instance;
        string now = player == null ? "no player" : "L=" + (player.leftPlayerHand?.Held != null ? PrefabKey(player.leftPlayerHand.Held.name) : "-")
            + " R=" + (player.rightPlayerHand?.Held != null ? PrefabKey(player.rightPlayerHand.Held.name) : "-")
            + " bags=" + BagObject.Active.Count(b => b != null && b.State == BagState.Held && b.HolderActor == PhotonNetwork.LocalPlayer?.ActorNumber);
        if (now == heldState) return;
        heldState = now;
        Log("HANDS " + now + " | " + Where() + " ready=" + PhotonSession.Instance?.WorldReady + " transition=" + PhotonSession.Instance?.InWorldTransition + " down=" + RailExpedition.LocalDown);
    }

    static void Step(double age)
    {
        var session = PhotonSession.Instance;
        int first = SessionState.GetInt(Key + ".first", 0);
        if (control.step == 0)
        {
            if (!Settled || age < 8) return;
            if (!backedUp) { BackupSaves(); GrantFocus(); ClearLoadout(); backedUp = true; Log("CLEARED own loadout " + Where()); return; }
            if (Host)
            {
                if (!action) { session.RequestCreatePublic(); action = true; readySince = 0; return; }
                if (!InCompound || !Settled) return;
                control.compound = PhotonNetwork.CurrentRoom.Name;
            }
            Ack("account loadout backed up and cleared");
            if (Host && AllAck) Next(1);
            return;
        }
        if (control.step == 1)
        {
            if (!Gather()) return;
            Ack("gathered in " + control.compound);
            if (Host && AllAck)
            {
                if (File.Exists(Folder + "/first-round.txt") && int.TryParse(File.ReadAllText(Folder + "/first-round.txt"), out int f) && f > 0) Next(2 + 2 * f);
                else Next(2);
            }
            return;
        }
        int index = (control.step - 2) / 2;
        if (index >= Rounds.Length) { Finish("COMPLETE all held-carry rounds passed (role " + Role + ")"); return; }
        var r = Rounds[index];
        if (control.step % 2 == 0) LoadStep(r, age); else GoStep(r, age, session);
    }

    static void LoadStep(Round r, double age)
    {
        string mine = Load(r, Role), peers = Load(r, Other);
        if (!Ready || !Settled) { if (!loaded) Grip(false); return; }
        GrantFocus();
        if (r.t == T.JoinCompound)
        {
            if (!Host && PhotonNetwork.CurrentRoom.Name == control.compound) { Grip(false); Ack("SKIP already in the peer's Compound " + control.compound); }
            if (Host && Acked("player2") && ReadReceipt("player2").note.StartsWith("SKIP")) { Ack("SKIP"); Log("CHECK " + r.name + " SKIPPED (Back to Compound landed in the peer's Compound)"); Next(control.step + 2); return; }
            if (ack) return;
        }
        if (!loaded)
        {
            if (mine == "")
            {
                // Clean slate only: destroy this role's leftovers so the next arrival can be counted exactly.
                if (sub == 0) { DriveLoad("", out _); }
                Grip(false);
                assigned.Clear(); assignedBag = null;
                if (EditorApplication.timeSinceStartup - entered < 2) return;
                loaded = true; Log("LOAD none (clean slate) " + Where());
            }
            else
            {
                if (!DriveLoad(mine, out string d)) return;
                loaded = true; Log("HOLD confirmed " + mine + ": " + d + " " + Where());
                phaseAt = EditorApplication.timeSinceStartup;
            }
        }
        string detail = "own " + (mine == "" ? "none" : mine);
        if (mine != "")
        {
            string fail = OwnHoldFailure(mine, PhotonNetwork.LocalPlayer.ActorNumber, out string d2);
            Require(fail == null || EditorApplication.timeSinceStartup - phaseAt < 1, "Hold lost while waiting: " + fail);
            detail += " held " + d2;
        }
        if (peers != "" && PeerHere)
        {
            var peer = Peer();
            string pd = "";
            string pf = PeerHoldFailure(Other, peers, peer.ActorNumber, out pd);
            if (pf != null) { last = pf; return; }
            detail += "; " + pd;
        }
        Ack(detail, LoadViews());
        if (Host && AllAck) Next(control.step + (r.t == T.JoinCompound && ReadReceipt("player2").note.StartsWith("SKIP") ? 2 : 1));
    }

    static void GoStep(Round r, double age, PhotonSession session)
    {
        string mine = Load(r, Role);
        // Release the grip once the transition has visibly started (the player may let go during the veil).
        if (mine != "" && !released && (session.HasRoomRequest || session.InWorldTransition || RailExpedition.LocalDown || !PhotonNetwork.InRoom))
        { released = true; Grip(false); Log("RELEASE grip: transition under way " + Where() + " ready=" + session.WorldReady + " down=" + RailExpedition.LocalDown); }
        bool arrived = false;
        bool together = false, selfOnly = false, oldWorld = false;
        switch (r.t)
        {
            case T.PubAlone:
                if (!Host) { if (InCompound) { if (Ready) StandInBoarding(); break; } arrived = Running && Settled; selfOnly = true; }
                else { if (InCompound && Ready && age < 3) StandAway(); arrived = InCompound && Settled && ActivePlayers == 1; oldWorld = true; }
                break;
            case T.LeaveToCompound:
                if (!Host)
                {
                    if (!action && Settled && !InCompound) { session.LeaveCurrentRoom(); action = true; Log("LEAVE Back to Compound"); break; }
                    arrived = action && InCompound && Settled; selfOnly = true;
                }
                else
                {
                    if (!Acked("player2") || !Settled) break;
                    arrived = true; selfOnly = !(ReadReceipt("player2").room == PhotonNetwork.CurrentRoom.Name && PeerHere); together = !selfOnly;
                }
                break;
            case T.JoinCompound:
                if (!Host && PhotonNetwork.CurrentRoom?.Name != control.compound && Ready && (!action || age % 15 < .1)) { session.RequestJoinCode(control.compound); action = true; readySince = 0; }
                arrived = PhotonNetwork.CurrentRoom?.Name == control.compound && Settled && ActivePlayers == 2 && AllPoses && (Host || action); together = true;
                break;
            case T.PubTogether:
                if (InCompound) { if (Ready) StandInBoarding(); break; }
                arrived = Running && Settled && ActivePlayers == 2 && AllPoses; together = true;
                break;
            case T.CrewFail: case T.CrewSuccess:
                if (!InCompound) { if (Ready && Running && PhotonNetwork.IsMasterClient) Nudge(() => session.FinishRun(r.t == T.CrewSuccess)); break; }
                arrived = Settled && ActivePlayers == 2 && AllPoses; together = true;
                break;
            case T.CreatePrivate:
                if (Host)
                {
                    if (!action && Ready) { session.RequestCreatePrivate(control.privateCode); action = true; readySince = 0; break; }
                    arrived = action && InStaging && Settled; selfOnly = true;
                }
                else { arrived = InCompound && Settled && ActivePlayers == 1; oldWorld = true; }
                break;
            case T.JoinPrivate:
                if (!Host && !PhotonSession.IsPrivate && Ready && (!action || age % 15 < .1)) { session.RequestJoinCode(control.privateCode); action = true; readySince = 0; }
                arrived = InStaging && Settled && ActivePlayers == 2 && AllPoses && (Host || action); together = true;
                break;
            case T.PrivDepart:
                if (Host && InStaging && Settled) Nudge(session.StartPrivateRun);
                arrived = Running && Settled && ActivePlayers == 2 && AllPoses; together = true;
                break;
            case T.PrivFail: case T.PrivSuccess:
                if (Host && Running && Settled) Nudge(() => session.FinishRun(r.t == T.PrivSuccess));
                arrived = InStaging && Settled && ActivePlayers == 2 && AllPoses; together = true;
                break;
            case T.DownReturn:
                if (!Host)
                {
                    if (Running && !RailExpedition.LocalDown && Ready && RailTrain.Instance != null)
                    {
                        if (!action) { Teleport(RailTrain.Instance.transform.TransformPoint(new Vector3(7, .5f, 0))); action = true; acted = EditorApplication.timeSinceStartup; Log("WARP off train holding " + mine); }
                        else if (EditorApplication.timeSinceStartup - acted > 1) RailExpedition.RequestDown();
                    }
                    if (RailExpedition.LocalDown && !downFlagged) { downFlagged = true; File.WriteAllText(DownPath, token + ":" + control.revision); Log("DOWN confirmed " + Where()); }
                }
                else if (Running && Settled && File.Exists(DownPath) && File.ReadAllText(DownPath) == token + ":" + control.revision) Nudge(() => session.FinishRun(false));
                arrived = InStaging && Settled && ActivePlayers == 2 && AllPoses && (Host || downFlagged); together = true;
                break;
            case T.LeaveMidRun:
                if (!Host)
                {
                    if (!action && Settled && Running) { session.LeaveCurrentRoom(); action = true; Log("LEAVE Back to Compound mid-run"); break; }
                    arrived = action && InCompound && Settled; selfOnly = true;
                }
                else
                {
                    var peer = Peer();
                    arrived = Settled && Running && (peer == null || peer.IsInactive); oldWorld = true;
                }
                break;
        }
        if (!arrived) { okSince = -1; return; }
        string detail = "";
        bool ok = Stable(() =>
        {
            var parts = new List<string>();
            int me = PhotonNetwork.LocalPlayer.ActorNumber;
            bool myDowned = r.t == T.DownReturn && Role == "player2";
            string f = RoleArrival(r, Role, me, true, myLoadViews, myDowned, out string d);
            if (f != null) return "self: " + f;
            parts.Add("self " + d);
            if (together)
            {
                if (!PeerHere) return "peer not in room";
                int pa = Peer().ActorNumber;
                bool peerDowned = r.t == T.DownReturn && Other == "player2";
                f = RoleArrival(r, Other, pa, false, peerLoadViews, peerDowned, out d);
                if (f != null) return "peer: " + f;
                parts.Add("peer " + d);
            }
            if (oldWorld)
            {
                Require(peerLoadRoom == PhotonNetwork.CurrentRoom.Name, "Stayer is not in the room the peer loaded in (" + peerLoadRoom + ")");
                f = OldWorld(Other, peerLoadActor, peerLoadViews, out d);
                if (f != null) return f;
                parts.Add(d);
            }
            detail = string.Join(" | ", parts);
            return null;
        });
        if (!ok) return;
        Ack((together ? "together" : oldWorld ? "stayer" : "alone") + " in " + PhotonNetwork.CurrentRoom.Name + " master=" + PhotonNetwork.IsMasterClient + ": " + detail);
        if (Host && AllAck)
        {
            var m = ReadReceipt("main"); var p = ReadReceipt("player2");
            if (together) Require(m.room == p.room, "Crew split: " + m.room + " / " + p.room);
            Log("CHECK round " + ((control.step - 2) / 2 + 1) + " PASS " + r.name + " | main(load " + (r.main == "" ? "none" : r.main) + ", master=" + m.master + "): " + m.note
                + " || player2(load " + (r.p2 == "" ? "none" : r.p2) + ", master=" + p.master + "): " + p.note);
            if (r.t == T.CrewFail || r.t == T.CrewSuccess || r.t == T.LeaveToCompound && together) control.compound = PhotonNetwork.CurrentRoom.Name;
            Next(control.step + 1);
        }
    }
}
#endif
