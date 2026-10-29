#if UNITY_EDITOR
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only Game view readout for the Photon connection.
/// </summary>
static class PhotonGameDebug
{
    const string VisiblePrefKey = "Duck.PhotonGameDebug.Visible";
    static readonly Rect OverlayRect = new Rect(8f, 8f, 192f, 268f);
    static readonly Rect ToggleRect = new Rect(8f, 8f, 72f, 24f);
    const float RoomListHeight = 96f;
    static Overlay _overlay;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Start()
    {
        if (_overlay != null)
            return;

        GameObject overlay = new GameObject(nameof(PhotonGameDebug));
        overlay.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(overlay);
        _overlay = overlay.AddComponent<Overlay>();
    }

    sealed class Overlay : MonoBehaviour, ILobbyCallbacks
    {
        readonly Dictionary<string, RoomInfo> _rooms = new Dictionary<string, RoomInfo>();
        readonly List<RoomInfo> _visibleRooms = new List<RoomInfo>();
        Vector2 _scroll;
        bool _roomsDirty;

        void OnEnable()
        {
            PhotonNetwork.AddCallbackTarget(this);
        }

        void OnDisable()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
        }

        void OnDestroy()
        {
            DesktopPlayer.SuppressCursorCapture = false;

            if (_overlay == this)
                _overlay = null;
        }

        public void OnJoinedLobby()
        {
            ClearRooms();
        }

        public void OnLeftLobby()
        {
            ClearRooms();
        }

        public void OnRoomListUpdate(List<RoomInfo> roomList)
        {
            if (roomList == null)
                return;

            for (int i = 0; i < roomList.Count; i++)
            {
                RoomInfo info = roomList[i];
                if (info == null || string.IsNullOrEmpty(info.Name))
                    continue;

                if (info.RemovedFromList || !info.IsVisible)
                    _rooms.Remove(info.Name);
                else
                    _rooms[info.Name] = info;
            }

            _roomsDirty = true;
        }

        public void OnLobbyStatisticsUpdate(List<TypedLobbyInfo> lobbyStatistics)
        {
        }

        GUIStyle _box;
        GUIStyle _label;
        bool _visible;

        void Awake()
        {
            _visible = EditorPrefs.GetBool(VisiblePrefKey, false);
        }

        void OnGUI()
        {
            EnsureStyles();

            if (!_visible)
            {
                DesktopPlayer.SuppressCursorCapture = ToggleRect.Contains(Event.current.mousePosition);
                if (GUI.Button(ToggleRect, "Photon"))
                    SetVisible(true);
                return;
            }

            bool showRooms = GameManager.observePhotonRooms && !PhotonNetwork.InRoom;
            Rect area = showRooms
                ? new Rect(OverlayRect.x, OverlayRect.y, OverlayRect.width, OverlayRect.height + RoomListHeight)
                : OverlayRect;
            DesktopPlayer.SuppressCursorCapture = area.Contains(Event.current.mousePosition);

            GUILayout.BeginArea(area);
            GUILayout.BeginVertical(_box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Photon", _label);
            if (GUILayout.Button("−", GUILayout.Width(24f)))
                SetVisible(false);
            GUILayout.EndHorizontal();
            GUILayout.Label("Mic: " + MicLabel(), _label);
            GUILayout.Label("State   " + StateLabel(), _label);
            GUILayout.Label("Room    " + RoomLabel(), _label);
            GUILayout.Label("You     " + PlayerLabel(), _label);
            GUILayout.Label("Master  " + MasterLabel(), _label);

            if (showRooms)
                DrawRoomList();

            if (PhotonNetwork.InRoom)
            {
                if (GUILayout.Button("Leave room"))
                    PhotonSession.Ensure().LeaveCurrentRoom();
            }
            else if (PhotonNetwork.IsConnectedAndReady)
            {
                if (GUILayout.Button("Join room"))
                    PhotonSession.Ensure().JoinOpenRoom();
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        void SetVisible(bool visible)
        {
            _visible = visible;
            EditorPrefs.SetBool(VisiblePrefKey, visible);
        }

        void DrawRoomList()
        {
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(RoomListHeight));

            if (!PhotonNetwork.IsConnected || PhotonNetwork.Server != ServerConnection.MasterServer)
                GUILayout.Label("Not connected", _label);
            else if (!HasVisibleRooms())
                GUILayout.Label("No rooms", _label);
            else
            {
                for (int i = 0; i < _visibleRooms.Count; i++)
                {
                    RoomInfo info = _visibleRooms[i];
                    GUILayout.Label(info.Name + "    " + info.PlayerCount + "/" + info.MaxPlayers, _label);
                }
            }

            GUILayout.EndScrollView();
        }

        bool HasVisibleRooms()
        {
            if (!_roomsDirty)
                return _visibleRooms.Count > 0;

            _roomsDirty = false;
            _visibleRooms.Clear();
            foreach (KeyValuePair<string, RoomInfo> pair in _rooms)
            {
                RoomInfo info = pair.Value;
                if (info != null && info.IsVisible && !info.RemovedFromList && !string.IsNullOrEmpty(info.Name))
                    _visibleRooms.Add(info);
            }

            _visibleRooms.Sort(CompareRoomName);
            return _visibleRooms.Count > 0;
        }

        static int CompareRoomName(RoomInfo a, RoomInfo b)
        {
            return string.CompareOrdinal(a.Name, b.Name);
        }

        void ClearRooms()
        {
            _rooms.Clear();
            _visibleRooms.Clear();
            _roomsDirty = false;
        }

        void EnsureStyles()
        {
            if (_box != null)
                return;

            Texture2D background = new Texture2D(1, 1);
            background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f));
            background.Apply();

            _box = new GUIStyle(GUI.skin.box);
            _box.normal.background = background;
            _box.padding = new RectOffset(10, 10, 6, 8);

            _label = new GUIStyle(GUI.skin.label);
            _label.fontSize = 14;
            _label.normal.textColor = Color.white;
        }

        static string MicLabel()
        {
            if (PhotonVoiceChat.PunishmentMuted)
                return "Muted (ban)";
            if (PlayerListItem.ReadSelfMuted())
                return "Muted";
            return "On";
        }

        static string StateLabel()
        {
            switch (PhotonNetwork.NetworkClientState)
            {
                case ClientState.PeerCreated:
                case ClientState.Disconnected:
                    return "Disconnected";
                case ClientState.ConnectingToNameServer:
                case ClientState.ConnectedToNameServer:
                case ClientState.DisconnectingFromNameServer:
                case ClientState.Authenticating:
                case ClientState.Authenticated:
                case ClientState.ConnectingToMasterServer:
                case ClientState.ConnectWithFallbackProtocol:
                    return "Connecting to Photon";
                case ClientState.ConnectedToMasterServer:
                case ClientState.JoiningLobby:
                case ClientState.JoinedLobby:
                    return "Connected";
                case ClientState.DisconnectingFromMasterServer:
                case ClientState.ConnectingToGameServer:
                case ClientState.ConnectedToGameServer:
                case ClientState.Joining:
                    return "Connecting to room";
                case ClientState.Joined:
                    return "In room";
                case ClientState.Leaving:
                    return "Leaving room";
                case ClientState.DisconnectingFromGameServer:
                case ClientState.Disconnecting:
                    return "Disconnecting";
                default:
                    return PhotonNetwork.NetworkClientState.ToString();
            }
        }

        static string RoomLabel()
        {
            Room room = PhotonNetwork.CurrentRoom;
            if (room == null || string.IsNullOrEmpty(room.Name))
                return "none";

            return room.Name;
        }

        static string PlayerLabel()
        {
            Player local = PhotonNetwork.LocalPlayer;
            if (local != null && !string.IsNullOrEmpty(local.NickName))
                return local.NickName;

            PlayerSession session = PlayerSession.Instance;
            if (session != null && !string.IsNullOrEmpty(session.DisplayName))
                return session.DisplayName;

            return "none";
        }

        static string MasterLabel()
        {
            Player master = PhotonNetwork.MasterClient;
            if (master == null)
                return "none";

            string name = string.IsNullOrEmpty(master.NickName)
                ? master.ActorNumber.ToString()
                : master.NickName;

            if (master.IsLocal)
                return name + " (you)";

            return name;
        }
    }
}
#endif
