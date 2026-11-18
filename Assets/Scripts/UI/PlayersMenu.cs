using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;

public class PlayersMenu : MonoBehaviour, IInRoomCallbacks
{
    public PlayerListItem playerListItem;

    bool callbacksRegistered;
    readonly Dictionary<int, PlayerListItem> playerRows = new Dictionary<int, PlayerListItem>();
    readonly List<PlayerListItem> orderedRows = new List<PlayerListItem>();
    readonly List<int> missingActors = new List<int>();

    // Up to 8 players per page; a full 12-player Compound uses a second page.
    // Rows keep their authored height for up to 5 players. Larger lobbies shrink every row
    // equally so 8 fit in the authored 5-row area, leaving the bottom strip for the arrows.
    const int PlayersPerPage = 8, AuthoredRows = 5;
    float baseRowHeight = -1f;
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

        if (added || removed) {
            OrderRows(players);
        }
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

    void FitRows() {
        if (orderedRows.Count > 0) {
            if (baseRowHeight <= 0f) {
                LayoutElement template = playerListItem.GetComponent<LayoutElement>();
                baseRowHeight = template != null && template.preferredHeight > 0f ? template.preferredHeight : 0.05f;
            }
            // Same size on both pages, sized for the fuller first page.
            int rowsOnFirstPage = Mathf.Min(orderedRows.Count, PlayersPerPage);
            float rowHeight = Mathf.Min(baseRowHeight, baseRowHeight * AuthoredRows / rowsOnFirstPage);
            float scale = rowHeight / baseRowHeight;
            for (int index = 0; index < orderedRows.Count; index++) {
                PlayerListItem row = orderedRows[index];
                if (row != null) {
                    row.SetRowScale(scale, rowHeight);
                }
            }
        }
        ApplyPage();
    }

    void ApplyPage() {
        int pageCount = orderedRows.Count == 0
            ? 1
            : (orderedRows.Count + PlayersPerPage - 1) / PlayersPerPage;
        currentPage = Mathf.Clamp(currentPage, 1, pageCount);
        int first = (currentPage - 1) * PlayersPerPage;
        for (int index = 0; index < orderedRows.Count; index++) {
            PlayerListItem row = orderedRows[index];
            if (row != null) {
                row.gameObject.SetActive(index >= first && index < first + PlayersPerPage);
            }
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
        FitRows();
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
