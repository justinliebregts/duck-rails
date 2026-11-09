using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using TMPro;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>Gray-box compound and expedition presentation. The room, not this scene, owns the run.</summary>
public sealed class RailWorld : MonoBehaviour
{
    public static RailWorld Instance { get; private set; }
    public GameObject compound, expedition;
    public BoxCollider boardingTrigger;
    public Transform compoundSpawn, privateTerminal;
    public RailTrain train;
    public TMP_Text departureBoard;
    public GameObject batPrefab, bagPrefab;
    string presentedRoom, presentedRun;
    bool presentedOnTrain, positioned, menu;
    float nextManifest;
    string code = "";
    GUIStyle statusStyle;
    public static bool InputBlocked => Instance != null && (Instance.menu || PhotonSession.Instance == null || !PhotonSession.Instance.WorldReady);

    void Awake()
    {
        Instance = this;
        PhotonPreparePool.Register(batPrefab); PhotonPreparePool.Register(bagPrefab);
    }
    void OnDestroy() { if (Instance == this) Instance = null; DesktopPlayer.SuppressCursorCapture = false; }

    public static bool IsActorOnPlatform(int actor)
    {
        if (Instance == null || Instance.boardingTrigger == null) return false;
        if (PhotonNetwork.LocalPlayer != null && actor == PhotonNetwork.LocalPlayer.ActorNumber)
            return IsLocalOnPlatform();
        var player = PhotonPlayer.ForActor(actor);
        if (player == null || player.Chest == null) return false;
        return Instance.boardingTrigger.bounds.Contains(player.Chest.position);
    }
    public static bool IsLocalOnPlatform()
    {
        var player = GR.Player.instance;
        if (Instance == null || Instance.boardingTrigger == null || player == null) return false;
        var rig = player.GetComponent<NewPlayer>();
        // The tracking origin can settle below the floor with simulated poses.
        // Test the physical duck, not an assumed metre above that origin.
        Vector3 position = rig != null && rig.bodyCollider != null
            ? rig.bodyCollider.bounds.center : player.transform.position + Vector3.up;
        return Instance.boardingTrigger.bounds.Contains(position);
    }
    void Update()
    {
        var session = PhotonSession.Instance;
        if (session == null) return;
        if (PhotonSession.IsRailsRoom && gameObject.scene.name != PhotonSession.RequiredScene) { session.MarkWorldLoading(); return; }
        if (RailDesktopInput.GetKeyDown(KeyCode.Tab))
        {
            menu = !menu;
            Cursor.lockState = menu ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = menu;
        }
        DesktopPlayer.SuppressCursorCapture = menu || !session.WorldReady;
        if (!PhotonSession.IsRailsRoom || session.HasRoomRequest || session.AwaitingCrewDestination) { positioned = false; return; }
        string room = PhotonNetwork.CurrentRoom.Name, run = PhotonSession.RunId;
        bool aboard = PhotonSession.OnTrain;
        if (room != presentedRoom || run != presentedRun || aboard != presentedOnTrain)
        {
            presentedRoom = room; presentedRun = run; presentedOnTrain = aboard; positioned = false;
            if (compound != null) compound.SetActive(!aboard); if (expedition != null) expedition.SetActive(aboard);
            menu = false;
        }
        if (aboard && RailRoute.Instance != null && !RailRoute.Instance.EnsureReady()) { session.MarkWorldLoading(); return; }
        if (Time.unscaledTime >= nextManifest)
        {
            nextManifest = Time.unscaledTime + .5f;
            // Held cargo may belong to another connected player. Every client
            // removes its own cargo; room-owned cargo is removed by the master.
            if (!aboard) ClearCargo();
            else if (PhotonNetwork.IsMasterClient) MaintainCargo();
        }
        var local = GR.Player.instance;
        if (local == null) return;
        // Staging is not ready until the old objects have actually been removed.
        // Otherwise an immediate Start can skip the half-second cleanup tick.
        bool snapshotReady = !aboard ? !HasLiveCargo : (PhotonSession.Get("rails.seeded", "") == run &&
            PhotonSession.Get(RailSessionRules.Cargo, Array.Empty<int>()).All(id =>
            {
                var view = PhotonView.Find(id);
                var cargo = view != null ? view.GetComponent<RailCargo>() : null;
                return cargo != null && cargo.StateReady;
            }));
        if (!snapshotReady)
        {
            if (!aboard) session.MarkWorldLoading();
            return;
        }
        if (!positioned)
        {
            int spawnSlot = (PhotonNetwork.LocalPlayer.ActorNumber - 1) % 12;
            Vector3 position = aboard ? train.transform.TransformPoint(new Vector3((PhotonNetwork.LocalPlayer.ActorNumber % 4 - 1.5f) * .7f, .35f, -2f))
                : compoundSpawn.position + new Vector3((spawnSlot % 4 - 1.5f) * 1.5f, 0, spawnSlot / 4 * 1.5f);
            var desktop = local.GetComponent<DesktopPlayer>();
            if (desktop != null) desktop.Teleport(position, Quaternion.identity, true);
            else if (NewPlayer.instance != null) NewPlayer.instance.Warp(position, Quaternion.identity);
            positioned = true;
        }
        session.MarkWorldReady();
        if (!aboard)
        {
            bool onPlatform = IsLocalOnPlatform();
            session.SetBoarding(onPlatform);
            int[] roster = PhotonSession.Get(RailSessionRules.Roster, Array.Empty<int>());
            double deadline = PhotonSession.Get(RailSessionRules.Deadline, 0d);
            departureBoard.text = PhotonSession.IsPrivate ? "PRIVATE CREW\nCode " + room + "\nMaster starts at the terminal"
                : "PUBLIC DEPARTURE\n" + roster.Length + " / 8 aboard\n" +
                    (deadline > 0 ? Math.Max(0, Math.Ceiling(deadline - PhotonNetwork.Time)) + " seconds" : "Step onto the platform") +
                    (onPlatform && roster.Length == 8 && !roster.Contains(PhotonNetwork.LocalPlayer.ActorNumber) ? "\nFULL — wait for the next departure" : "");
        }
    }

    // The Photon registry includes inactive/stowed objects and excludes local
    // bag miniatures, prefab assets and objects already removed from the network.
    static IEnumerable<PhotonView> CargoViews()
    {
        foreach (var view in PhotonNetwork.PhotonViewCollection)
            if (view != null && view.ViewID != 0 && view.TryGetComponent<RailCargo>(out _)) yield return view;
    }
    public static bool HasLiveCargo => CargoViews().Any();
    static void ClearCargo()
    {
        if (PhotonNetwork.IsMasterClient)
            foreach (var enemy in Game.AI.Enemy.Active.ToArray())
                if (enemy != null && enemy.TryGetComponent<PhotonView>(out var enemyView) && enemyView.IsMine) PhotonNetwork.Destroy(enemy.gameObject);
        // Destroy unregisters views synchronously; do not mutate the live iterator.
        foreach (var view in CargoViews().Where(v => v.IsMine).ToArray())
            if (view != null && PhotonView.Find(view.ViewID) == view) PhotonNetwork.Destroy(view.gameObject);
    }
    void MaintainCargo()
    {
        string run = PhotonSession.RunId;
        if (string.IsNullOrEmpty(run)) return;
        var changes = MaintainFlashlights(run);
        if (PhotonSession.Get("rails.seeded", "") != run)
        {
            if (RailExpedition.Instance != null)
            {
                RailExpedition.Instance.SeedCargo();
                changes["rails.seeded"] = run;
                changes[RailSessionRules.Cargo] = CurrentCargo();
                PhotonSession.Instance.Commit(changes);
                return;
            }
            // Instantiation markers survive a master change between spawn and manifest commit.
            bool HasSeed(string name) => CargoViews().Any(view =>
                view.InstantiationData is object[] data && data.Length >= 3 &&
                data[data.Length - 2] as string == run && data[data.Length - 1] as string == name);
            if (!HasSeed("bat")) PhotonNetwork.InstantiateRoomObject(batPrefab.name,
                train.transform.TransformPoint(new Vector3(-1, .6f, 0)), Quaternion.Euler(0, 0, 90), 0, new object[] { "rails", run, "bat" });
            if (!HasSeed("bag"))
            {
                object[] initial = BagObject.SpawnData(1, new[] { batPrefab.name, "", "", "" }, 0, BagObject.Target.Dropped, 0);
                var data = new object[initial.Length + 3]; initial.CopyTo(data, 0);
                data[data.Length - 3] = "rails"; data[data.Length - 2] = run; data[data.Length - 1] = "bag";
                PhotonNetwork.InstantiateRoomObject(bagPrefab.name, train.transform.TransformPoint(new Vector3(1, .6f, 0)), Quaternion.identity, 0, data);
            }
            changes["rails.seeded"] = run;
            changes[RailSessionRules.Cargo] = CurrentCargo();
            PhotonSession.Instance.Commit(changes);
        }
        else
        {
            int[] current = CurrentCargo();
            if (!current.SequenceEqual(PhotonSession.Get(RailSessionRules.Cargo, Array.Empty<int>())))
                changes[RailSessionRules.Cargo] = current;
            if (changes.Count > 0) PhotonSession.Instance.Commit(changes);
        }
    }
    Hashtable MaintainFlashlights(string run)
    {
        var changes = new Hashtable();
        if (train == null || (PhotonSession.Phase != RailPhase.Departing && PhotonSession.Phase != RailPhase.Running)) return changes;
        // Use retained departure crew size, never live player count. Retain grants
        // for lost items; instantiation markers cover a host change before commit.
        string[] previous = PhotonSession.Get("rails.flashlightRun", "") == run
            ? PhotonSession.Get("rails.flashlightSlots", Array.Empty<string>()) : Array.Empty<string>();
        var granted = new HashSet<string>(previous, StringComparer.Ordinal);
        foreach (var view in CargoViews())
            if (view.InstantiationData is object[] data && data.Length == 3 &&
                data[0] as string == "rails" && data[1] as string == run &&
                data[2] is string marker && marker.StartsWith("flashlight:", StringComparison.Ordinal))
                granted.Add(marker.Substring("flashlight:".Length));
        int count = Mathf.Clamp(PhotonSession.Get(RailResourceBalance.CrewKey, 1), 1, 8);
        for (int slot = 0; slot < count; slot++)
        {
            string id = slot.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (granted.Contains(id)) continue;
            var position = new Vector3((slot % 4 - 1.5f) * .55f, .25f, -3.5f + slot / 4 * .65f);
            var item = PhotonNetwork.InstantiateRoomObject("Rails/RailFlashlight",
                train.transform.TransformPoint(position), train.transform.rotation,
                0, new object[] { "rails", run, "flashlight:" + id });
            if (item != null) granted.Add(id);
        }
        if (PhotonSession.Get("rails.flashlightRun", "") != run || !granted.SetEquals(previous))
        {
            changes["rails.flashlightRun"] = run;
            changes["rails.flashlightSlots"] = granted.OrderBy(id => id, StringComparer.Ordinal).ToArray();
        }
        return changes;
    }
    static int[] CurrentCargo() => CargoViews().Select(view => view.ViewID).OrderBy(id => id).ToArray();

    void OnGUI()
    {
        var session = PhotonSession.Instance;
        if (session == null) return;
        statusStyle ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter };
        float width = Mathf.Min(420, Screen.width - 24);
        string status = "DUCK RAILS  •  " + PhotonSession.Phase +
            (PhotonNetwork.InRoom ? "  •  Code: " + PhotonNetwork.CurrentRoom.Name : "") +
            (session.WorldReady ? "" : "\n" + session.Status);
        GUI.Box(new Rect((Screen.width - width) * .5f, 12, width, 36), status, statusStyle);
        if (!string.IsNullOrEmpty(session.LastError))
        {
            float errorWidth = Mathf.Min(620, Screen.width - 24);
            GUI.Box(new Rect((Screen.width - errorWidth) * .5f, 52, errorWidth, 48), session.LastError);
        }
        if (!menu) return;
        GUILayout.BeginArea(new Rect(20, 145, 370, 420), GUI.skin.box);
        GUILayout.Label("SESSION CONTROLS");
        GUI.enabled = session.WorldReady;
        bool nearTerminal = privateTerminal != null && GR.Player.instance != null && Vector3.Distance(GR.Player.instance.HeadCenteredGameplayPosition(GR.Player.instance.transform.position), privateTerminal.position) < 4;
        if (!PhotonSession.OnTrain && nearTerminal)
        {
            GUILayout.Label("Five-digit code: create private or join any room");
            code = GUILayout.TextField(code, 5);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Create")) session.RequestCreatePrivate(code);
            if (GUILayout.Button("Join")) session.RequestJoinCode(code);
            GUILayout.EndHorizontal();
        }
        else if (!PhotonSession.OnTrain) GUILayout.Label("Walk to the private terminal to Create or Join.");
        if (PhotonSession.IsPrivate && PhotonSession.Phase == RailPhase.Staging)
        {
            GUI.enabled = session.WorldReady && PhotonNetwork.IsMasterClient;
            if (GUILayout.Button("Start expedition")) session.StartPrivateRun();
            GUILayout.Label("Master: " + PhotonNetwork.MasterClient?.NickName);
        }
        if (PhotonSession.Phase == RailPhase.Running)
        {
            GUILayout.Label("Fuel: " + PhotonSession.CurrentFuel.ToString("0.0") + "   Stops: 5 + bridge");
            GUI.enabled = session.WorldReady && !session.HasRoomRequest;
            if (GUILayout.Button("GO — accelerate")) session.SetTrainMoving(true);
            if (GUILayout.Button("STOP — brake")) session.SetTrainMoving(false);
#if UNITY_EDITOR
            if (GUILayout.Button("+1 Fuel")) RailExpedition.Instance?.Request("editor-fuel");
            if (RailExpedition.LocalDown && GUILayout.Button("Revive"))
                RailExpedition.Instance.Request("editor-revive");
#endif
            GUI.enabled = session.WorldReady && PhotonNetwork.IsMasterClient;
            GUILayout.Label("Keyboard or VR mode: press T for throttle GO; press again to STOP.\nQ/E reach hands; right mouse grips in Keyboard mode.\nTouch horn / TALK buttons to hold; withdraw to release.\nRevive: hold a hand near a downed teammate.");
        }
        GUI.enabled = !session.HasRoomRequest;
        if (GUILayout.Button("Leave for public compound")) session.RequestJoinRandom();
        GUI.enabled = true;
        if (GUILayout.Button("Close")) { menu = false; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        GUILayout.EndArea();
    }
}
