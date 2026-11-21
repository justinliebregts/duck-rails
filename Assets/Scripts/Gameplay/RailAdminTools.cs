#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;
#endif

// Like the existing admin UI, this is a client convenience gate, not server authorization.
public static class RailAdminTools
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public const string AdminProperty = "rails.adminTools";
    public static bool Available => PlayerSession.Instance != null && PlayerSession.Instance.IsAdmin;
    static bool noClip, invincible, invincibilityOverridden;
    static PlayerSession contextSession;
    static Photon.Realtime.Room contextRoom;
    static string contextRun;
    public static bool NoClip => Available && noClip && !RailExpedition.LocalDown;
    public static bool InvincibilityOverridden => Available && invincibilityOverridden;
    public static bool Invincible => Available && invincible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { noClip = invincible = invincibilityOverridden = false; contextSession = null; contextRoom = null; contextRun = null; }

    public static void Refresh()
    {
        var session = PlayerSession.Instance;
        var room = PhotonNetwork.CurrentRoom;
        string run = PhotonSession.RunId;
        if (!Available || contextSession != session || contextRoom != room || contextRun != run)
        {
            noClip = invincible = invincibilityOverridden = false;
            contextSession = session; contextRoom = room; contextRun = run;
        }
        var player = PhotonNetwork.LocalPlayer;
        if (PhotonNetwork.InRoom && player != null &&
            ((player.CustomProperties[AdminProperty] is bool value) ? value != Available : Available))
            player.SetCustomProperties(new Hashtable { [AdminProperty] = Available });
    }

    public static void ToggleNoClip() { Refresh(); if (Available) noClip = !noClip; }
    public static void ToggleInvincibility()
    {
        Refresh();
        if (!Available) return;
        bool current = Invincible || RailTestTools.EnemyInvincible;
        invincibilityOverridden = true;
        invincible = !current;
    }
    public static void DepositCoal(int count)
    {
        Refresh();
        if (!Available || (count != 1 && count != 50) || PhotonSession.Phase != RailPhase.Running ||
            PhotonSession.Instance == null || !PhotonSession.Instance.WorldReady) return;
        RailExpedition.Instance?.Request("admin-coal", count);
    }
    // Shared run state: the master applies it for everyone and it resets with the next run.
    public static void ToggleTrainSpeed()
    {
        Refresh();
        if (!Available || PhotonSession.Phase != RailPhase.Running ||
            PhotonSession.Instance == null || !PhotonSession.Instance.WorldReady) return;
        RailExpedition.Instance?.Request("admin-train-speed", RailTestTools.FastTrainEnabled ? 1 : 5);
    }
#else
    public static bool Available => false;
    public static bool NoClip => false;
    public static bool Invincible => false;
#endif
}
