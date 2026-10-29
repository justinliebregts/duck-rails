using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PlayersMenu : MonoBehaviour, IInRoomCallbacks
{
    public PlayerListItem playerListItem;

    bool callbacksRegistered;
    readonly Dictionary<int, PlayerListItem> playerRows = new Dictionary<int, PlayerListItem>();
    readonly List<PlayerListItem> orderedRows = new List<PlayerListItem>();
    readonly List<int> missingActors = new List<int>();
    PlayerListItem companionRow;
    PhotonPlayer companionPlayer;

    int numberOfPlayersPerPage = 5;
    int currentPage = 1;
    public GameObject nextPageButton;
    public GameObject previousPageButton;

    // PlayerList is toggled with the panel, so PauseMenu keeps these registered while it is alive.
    public void RegisterCallbacks() {
        if (playerListItem != null) {
            playerListItem.gameObject.SetActive(false);
        }
        if (!callbacksRegistered) {
            PhotonNetwork.AddCallbackTarget(this);
            callbacksRegistered = true;
        }
    }

    public void UnregisterCallbacks() {
        if (callbacksRegistered) {
            PhotonNetwork.RemoveCallbackTarget(this);
            callbacksRegistered = false;
        }
    }

    public void SyncPlayerRows() {
        if (playerListItem == null) {
            return;
        }

        if (!PhotonNetwork.InRoom) {
            ClearPlayerRows();
            return;
        }

        Player[] players = PhotonNetwork.PlayerList;
        bool removed = RemoveMissingRows(players);
        bool added = false;
        for (int i = 0; i < players.Length; i++) {
            Player player = players[i];
            if (player == null || playerRows.ContainsKey(player.ActorNumber)) {
                continue;
            }

            PlayerListItem row = Instantiate(playerListItem, transform);
            row.gameObject.SetActive(true);
            row.Init(player);
            playerRows.Add(player.ActorNumber, row);
            added = true;
        }

        bool companionChanged = SyncCompanionRow();
        if (added || removed || companionChanged) {
            OrderRows(players);
        }
    }

    // PlayerList lives under the pause menu mesh, so this only runs while the menu is open.
    void Update() {
        if (playerListItem == null || !PhotonNetwork.InRoom)
            return;

        PhotonPlayer duck = FindCompanion();
        bool rowStale = companionRow != null && (duck == null || !ReferenceEquals(duck, companionPlayer));
        bool rowMissing = companionRow == null && duck != null;
        if (rowStale || rowMissing)
            SyncPlayerRows();
    }

    static PhotonPlayer FindCompanion() {
        for (int i = 0; i < PhotonPlayer.Active.Count; i++) {
            PhotonPlayer player = PhotonPlayer.Active[i];
            if (player != null && player.IsCompanion)
                return player;
        }
        return null;
    }

    bool SyncCompanionRow() {
        PhotonPlayer duck = FindCompanion();
        if (duck == null) {
            if (companionRow == null)
                return false;
            Destroy(companionRow.gameObject);
            companionRow = null;
            companionPlayer = null;
            return true;
        }

        if (companionRow == null) {
            companionRow = Instantiate(playerListItem, transform);
            companionRow.gameObject.SetActive(true);
            companionRow.InitCompanion(duck);
            companionPlayer = duck;
            return true;
        }

        if (ReferenceEquals(companionPlayer, duck))
            return false;

        companionRow.InitCompanion(duck);
        companionPlayer = duck;
        return true;
    }

    bool RemoveMissingRows(Player[] players) {
        missingActors.Clear();
        foreach (int actor in playerRows.Keys) {
            if (!ContainsActor(players, actor)) {
                missingActors.Add(actor);
            }
        }

        for (int i = 0; i < missingActors.Count; i++) {
            int actor = missingActors[i];
            if (playerRows.TryGetValue(actor, out PlayerListItem row) && row != null) {
                Destroy(row.gameObject);
            }
            playerRows.Remove(actor);
        }
        return missingActors.Count > 0;
    }

    static bool ContainsActor(Player[] players, int actor) {
        for (int i = 0; i < players.Length; i++) {
            if (players[i] != null && players[i].ActorNumber == actor) {
                return true;
            }
        }
        return false;
    }

    void ClearPlayerRows() {
        foreach (KeyValuePair<int, PlayerListItem> pair in playerRows) {
            if (pair.Value != null) {
                Destroy(pair.Value.gameObject);
            }
        }
        playerRows.Clear();
        if (companionRow != null)
            Destroy(companionRow.gameObject);
        companionRow = null;
        companionPlayer = null;
        orderedRows.Clear();
        ApplyPage();
    }

    public void PreviousPage() {
        currentPage--;
        ApplyPage();
    }

    public void NextPage() {
        currentPage++;
        ApplyPage();
    }

    void ApplyPage() {
        int pageCount = orderedRows.Count == 0
            ? 1
            : (orderedRows.Count + numberOfPlayersPerPage - 1) / numberOfPlayersPerPage;
        currentPage = Mathf.Clamp(currentPage, 1, pageCount);
        int first = (currentPage - 1) * numberOfPlayersPerPage;
        for (int index = 0; index < orderedRows.Count; index++) {
            PlayerListItem row = orderedRows[index];
            if (row == null) {
                continue;
            }
            row.gameObject.SetActive(index >= first && index < first + numberOfPlayersPerPage);
        }
        if (previousPageButton != null) {
            previousPageButton.SetActive(currentPage > 1);
        }
        if (nextPageButton != null) {
            nextPageButton.SetActive(currentPage < pageCount);
        }
    }

    // PlayerList is already in actor-number order. VerticalLayoutGroup draws the first sibling at the top.
    void OrderRows(Player[] players) {
        int sibling = 0;
        orderedRows.Clear();
        for (int i = 0; i < players.Length; i++) {
            Player player = players[i];
            if (player == null || !player.IsLocal) {
                continue;
            }
            if (playerRows.TryGetValue(player.ActorNumber, out PlayerListItem row) && row != null) {
                row.transform.SetSiblingIndex(sibling++);
                orderedRows.Add(row);
            }
            break;
        }
        for (int i = 0; i < players.Length; i++) {
            Player player = players[i];
            if (player == null || player.IsLocal) {
                continue;
            }
            if (playerRows.TryGetValue(player.ActorNumber, out PlayerListItem row) && row != null) {
                row.transform.SetSiblingIndex(sibling++);
                orderedRows.Add(row);
            }
        }
        if (companionRow != null) {
            companionRow.transform.SetSiblingIndex(sibling++);
            orderedRows.Add(companionRow);
        }
        ApplyPage();
    }

    void RefreshPlayerName(Player player) {
        if (player == null) {
            return;
        }
        if (playerRows.TryGetValue(player.ActorNumber, out PlayerListItem row) && row != null) {
            row.Init(player);
        }
    }

    public void OnPlayerEnteredRoom(Player newPlayer) {
        SyncPlayerRows();
    }

    public void OnPlayerLeftRoom(Player otherPlayer) {
        SyncPlayerRows();
    }

    public void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged) { }

    public void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps) {
        RefreshPlayerName(targetPlayer);
    }

    public void OnMasterClientSwitched(Player newMasterClient) { }
}
