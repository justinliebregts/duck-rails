using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// PC spectators (see SpectatorMode) are Photon actors with no duck. Every build, including Quest,
/// leaves them out of the crew: run participants, crew size, readiness, boarding, colours and menus.
/// The flag is a player property the spectator sets before joining, so it arrives with the join.
/// </summary>
public static class RailSpectators
{
    public const string Key = "rails.spectator";

    public static bool Is(Player player) => player != null && player.CustomProperties[Key] is bool spectator && spectator;

    /// <summary>A connected (not inactive) crew member.</summary>
    public static bool IsLiveCrew(Player player) => player != null && !player.IsInactive && !Is(player);

    /// <summary>Connected crew members in the current room.</summary>
    public static int LiveCrewCount()
    {
        int count = 0;
        foreach (Player player in PhotonNetwork.PlayerList)
            if (IsLiveCrew(player)) count++;
        return count;
    }

    /// <summary>The players, without spectators (inactive crew members are kept).</summary>
    public static Player[] Crew(Player[] players)
    {
        if (players == null) return new Player[0];
        var crew = new List<Player>(players.Length);
        foreach (Player player in players)
            if (player != null && !Is(player)) crew.Add(player);
        return crew.ToArray();
    }
}
