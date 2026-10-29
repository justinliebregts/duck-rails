using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class PhotonManager : MonoBehaviourPunCallbacks
{
    public override void OnPlayerPropertiesUpdate(Photon.Realtime.Player target, Hashtable changed)
    {
    }
}

public static class PlayerProps
{
    public const string Skin = "skin";
    public const string Badge = "badge";
    public const string Nametag = "nametag";

    public static byte GetSkin(this Photon.Realtime.Player p) =>
        p.CustomProperties.TryGetValue(Skin, out var v) && v is byte b ? b : (byte)0;

    public static void SetSkin(byte id) =>
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { Skin, id } });

    public static string GetBadge(this Photon.Realtime.Player p) => CustomString(p, Badge);

    public static string GetNametag(this Photon.Realtime.Player p) => CustomString(p, Nametag);

    public static void PushCosmetics()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
            return;
        PlayerSession session = PlayerSession.Instance;
        string badge = session != null ? session.BadgeId : "";
        string nametag = session != null ? session.NametagId : "";
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
        {
            { Badge, badge ?? "" },
            { Nametag, nametag ?? "" },
        });
    }

    static string CustomString(Photon.Realtime.Player p, string key)
    {
        if (p != null && p.CustomProperties.TryGetValue(key, out var value) && value is string text)
            return text;
        return "";
    }
}
