using System;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>Room-scoped slots. Only the master allocates; all peers consume the room snapshot.</summary>
public sealed class DuckPlayerColors : MonoBehaviourPunCallbacks
{
    public const string Property = "duckColors";
    public const int Count = 12;
    static readonly System.Random ColorRandom = new System.Random();
    static readonly Color32[] Palette =
    {
        new Color32(255,255,255,255), // Default yellow (tint bypassed)
        new Color32(255,0,0,255),     // Bright scarlet #FF0000
        new Color32(255,121,15,255),  // Orange #FF790F
        new Color32(155,214,32,255),  // Lime #9BD620
        new Color32(20,107,54,255),   // Forest green #146B36
        new Color32(0,158,148,255),   // Teal #009E94
        new Color32(114,223,255,255), // Ice blue #72DFFF
        new Color32(38,61,207,255),   // Royal blue #263DCF
        new Color32(135,54,201,255),  // Violet #8736C9
        new Color32(255,88,180,255),  // Pink #FF58B4
        new Color32(117,64,37,255),   // Chocolate #754025
        new Color32(255,244,220,255)  // Pale ivory #FFF4DC
    };
    // Retain submitted allocations while Photon is still echoing earlier updates.
    int[] allocated;

    void Start() => Assign();

    public static Color Tint(int index) => Palette[Mathf.Clamp(index, 0, Count - 1)];

    public static bool TryGet(int actor, out int index)
    {
        index = 0;
        if (actor <= 0 || !PhotonNetwork.InRoom) return false;
        var slots = PhotonNetwork.CurrentRoom.CustomProperties[Property] as int[];
        if (slots == null || slots.Length != Count) return false;
        int found = Array.IndexOf(slots, actor);
        if (found < 0) return false;
        index = found;
        return true;
    }

    public override void OnJoinedRoom() { allocated = null; Assign(); }
    public override void OnLeftRoom() => allocated = null;
    public override void OnPlayerEnteredRoom(Player player) => Assign();
    public override void OnPlayerLeftRoom(Player player) => Assign();
    public override void OnMasterClientSwitched(Player player) { allocated = null; Assign(); }

    void Assign()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;
        if (allocated == null)
        {
            var saved = PhotonNetwork.CurrentRoom.CustomProperties[Property] as int[];
            allocated = saved != null && saved.Length == Count ? (int[])saved.Clone() : new int[Count];
        }
        var players = PhotonNetwork.PlayerList;
        int[] actors = Array.ConvertAll(players, p => p.ActorNumber);
        int[] next = Allocate(allocated, actors);
        bool changed = !System.Linq.Enumerable.SequenceEqual(allocated, next);
        allocated = next;
        if (changed || !PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(Property))
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { Property, (int[])next.Clone() } });
    }

    /// <summary>Preserves occupied slots and randomly assigns each newcomer an unused color.</summary>
    public static int[] Allocate(int[] previous, int[] actors)
    {
        var result = new int[Count];
        var remaining = new System.Collections.Generic.SortedSet<int>(actors);
        remaining.RemoveWhere(actor => actor <= 0);
        for (int i = 0; i < Count; i++)
            if (previous != null && i < previous.Length && remaining.Remove(previous[i]))
                result[i] = previous[i];
        var unused = new System.Collections.Generic.List<int>(Count);
        for (int i = 0; i < Count; i++)
            if (result[i] == 0) unused.Add(i);
        foreach (int actor in remaining)
        {
            if (unused.Count == 0) break;
            int choice = ColorRandom.Next(unused.Count);
            result[unused[choice]] = actor;
            unused.RemoveAt(choice);
        }
        return result;
    }
}
