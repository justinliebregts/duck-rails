using System;
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
        if (Instance == null) return false;
        if (PhotonNetwork.LocalPlayer != null && actor == PhotonNetwork.LocalPlayer.ActorNumber)
            return GR.Player.instance != null && Instance.boardingTrigger.bounds.Contains(GR.Player.instance.transform.position + Vector3.up);
        var player = PhotonPlayer.ForActor(actor);
        if (player == null || player.Chest == null) return false;
        return Instance.boardingTrigger.bounds.Contains(player.Chest.position);
    }
    void Update()
    {
        var session = PhotonSession.Instance;
        if (session == null) return;
        if (Input.GetKeyDown(KeyCode.Tab))
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
            compound.SetActive(!aboard); expedition.SetActive(aboard);
            menu = false;
        }
        if (PhotonNetwork.IsMasterClient && Time.unscaledTime >= nextManifest)
        {
            nextManifest = Time.unscaledTime + .5f;
            MaintainCargo();
        }
        var local = GR.Player.instance;
        if (local == null) return;
        bool snapshotReady = !aboard || (PhotonSession.Get("rails.seeded", "") == run &&
            PhotonSession.Get(RailSessionRules.Cargo, Array.Empty<int>()).All(id =>
            {
                var view = PhotonView.Find(id);
                var cargo = view != null ? view.GetComponent<RailCargo>() : null;
                return cargo != null && cargo.StateReady;
            }));
        if (!snapshotReady) return;
        if (!positioned)
        {
            Vector3 position = aboard ? train.transform.TransformPoint(new Vector3((PhotonNetwork.LocalPlayer.ActorNumber % 4 - 1.5f) * .7f, .35f, -2f)) : compoundSpawn.position;
            var desktop = local.GetComponent<DesktopPlayer>();
            if (desktop != null) desktop.Teleport(position, Quaternion.identity, true);
            else if (NewPlayer.instance != null) NewPlayer.instance.Warp(position, Quaternion.identity);
            positioned = true;
        }
        session.MarkWorldReady();
        if (!aboard)
        {
            bool onPlatform = boardingTrigger.bounds.Contains(local.transform.position + Vector3.up);
            session.SetBoarding(onPlatform);
            int[] roster = PhotonSession.Get(RailSessionRules.Roster, Array.Empty<int>());
            double deadline = PhotonSession.Get(RailSessionRules.Deadline, 0d);
            departureBoard.text = PhotonSession.IsPrivate ? "PRIVATE CREW\nCode " + room + "\nMaster starts at the terminal"
                : "PUBLIC DEPARTURE\n" + roster.Length + " / 8 aboard\n" +
                    (deadline > 0 ? Math.Max(0, Math.Ceiling(deadline - PhotonNetwork.Time)) + " seconds" : "Step onto the platform") +
                    (onPlatform && roster.Length == 8 && !roster.Contains(PhotonNetwork.LocalPlayer.ActorNumber) ? "\nFULL — wait for the next departure" : "");
        }
    }

    void MaintainCargo()
    {
        if (!PhotonSession.OnTrain)
        {
            foreach (var cargo in FindObjectsByType<RailCargo>(FindObjectsSortMode.None))
                if (cargo.photonView.ViewID != 0) PhotonNetwork.Destroy(cargo.gameObject);
            return;
        }
        string run = PhotonSession.RunId;
        if (string.IsNullOrEmpty(run)) return;
        if (PhotonSession.Get("rails.seeded", "") != run)
        {
            // Instantiation markers survive a master change between spawn and manifest commit.
            bool HasSeed(string name) => FindObjectsByType<RailCargo>(FindObjectsSortMode.None).Any(c =>
                c.photonView.InstantiationData is object[] data && data.Length >= 3 &&
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
            PhotonSession.Instance.Commit(new Hashtable { ["rails.seeded"] = run,
                [RailSessionRules.Cargo] = CurrentCargo() });
        }
        else
        {
            int[] current = CurrentCargo();
            if (!current.SequenceEqual(PhotonSession.Get(RailSessionRules.Cargo, Array.Empty<int>())))
                PhotonSession.Instance.Commit(new Hashtable { [RailSessionRules.Cargo] = current });
        }
    }
    static int[] CurrentCargo() => FindObjectsByType<RailCargo>(FindObjectsSortMode.None).Select(c => c.photonView.ViewID).Where(id => id != 0).OrderBy(id => id).ToArray();

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
        bool nearTerminal = GR.Player.instance != null && Vector3.Distance(GR.Player.instance.transform.position, privateTerminal.position) < 4;
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
            GUILayout.Label("Fuel: " + PhotonSession.CurrentFuel.ToString("0.0") + "   Block: rail-yard-test-v1");
            GUI.enabled = session.WorldReady && !session.HasRoomRequest;
            if (GUILayout.Button("GO — accelerate")) session.SetTrainMoving(true);
            if (GUILayout.Button("STOP — brake")) session.SetTrainMoving(false);
            GUI.enabled = session.WorldReady && PhotonNetwork.IsMasterClient;
            if (GUILayout.Button("Test success / return crew")) session.FinishRun(true);
            if (GUILayout.Button("Test failure / return crew")) session.FinishRun(false);
        }
        GUI.enabled = !session.HasRoomRequest;
        if (GUILayout.Button("Leave for public compound")) session.RequestJoinRandom();
        GUI.enabled = true;
        if (GUILayout.Button("Close")) { menu = false; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        GUILayout.EndArea();
    }
}
