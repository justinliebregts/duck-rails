#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using ExitGames.Client.Photon;
using UnityEditor;
using UnityEngine;
using NetPlayer = Photon.Realtime.Player;

/// <summary>EditMode checks for the PC spectator's crew exclusion (docs/duckz-ports.md, spectator entry).</summary>
public static class RailSpectatorChecks
{
    static int checks;
    static void Check(bool pass, string message)
    {
        if (!pass) throw new Exception("Duck Rails spectator check failed: " + message);
        checks++; Debug.Log("RAILS PASS: " + message);
    }

    // Realtime's Player constructor and IsInactive setter are internal to the SDK.
    static NetPlayer Make(int actor, bool spectator, bool inactive = false)
    {
        var properties = new Hashtable();
        if (spectator) properties[RailSpectators.Key] = true;
        var player = (NetPlayer)Activator.CreateInstance(typeof(NetPlayer), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            null, new object[] { "Duck" + actor, actor, false, properties }, null);
        if (inactive)
            typeof(NetPlayer).GetProperty(nameof(NetPlayer.IsInactive)).SetValue(player, true);
        return player;
    }

    [MenuItem("Duck Rails/Legacy/Run Spectator Checks")]
    public static void Run()
    {
        checks = 0;
        NetPlayer duck = Make(1, false), spectator = Make(2, true), away = Make(3, false, inactive: true), awaySpectator = Make(4, true, inactive: true);
        Check(!RailSpectators.Is(duck) && RailSpectators.Is(spectator) && !RailSpectators.Is(null), "Only the spectator property marks a spectator");
        Check(RailSpectators.IsLiveCrew(duck) && !RailSpectators.IsLiveCrew(spectator) && !RailSpectators.IsLiveCrew(away) &&
            !RailSpectators.IsLiveCrew(awaySpectator) && !RailSpectators.IsLiveCrew(null), "Live crew excludes spectators and reconnecting slots");
        var crew = RailSpectators.Crew(new[] { duck, spectator, away, null, awaySpectator });
        Check(crew.Length == 2 && crew.Contains(duck) && crew.Contains(away), "Crew lists keep reconnecting ducks and drop every spectator");
        Check(RailSpectators.Crew(null).Length == 0, "A missing player list is an empty crew");
        var notSpectator = new Hashtable { [RailSpectators.Key] = false };
        var explicitFalse = (NetPlayer)Activator.CreateInstance(typeof(NetPlayer), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            null, new object[] { "Duck5", 5, false, notSpectator }, null);
        Check(!RailSpectators.Is(explicitFalse) && RailSpectators.IsLiveCrew(explicitFalse), "A false spectator flag is an ordinary duck");
        string[] roomKeys = { RailSessionRules.Ready, RailSessionRules.Boarding, RailSessionRules.Roster, RailSessionRules.TransferUsers,
            RailHoldControl.BridgeKey, PhotonSession.DriveGripProperty };
        Check(!roomKeys.Contains(RailSpectators.Key), "The spectator flag does not reuse a session or player key");
        // Eight ducks fill a train; a spectator never takes an admission slot (admission only sees crew actors).
        var admitted = RailSessionRules.Admit(Array.Empty<int>(), Enumerable.Range(1, 8));
        Check(admitted.Length == RailSessionRules.CrewCapacity, "Train capacity stays eight ducks");
        Check(DuckzSpectatorBuild.Define == "DUCKZ_SPECTATOR" && DuckzSpectatorBuild.ExeName.EndsWith(".exe"), "Spectator build define and exe name");
        Debug.Log("RAILS SPECTATOR CHECKS: " + checks + " passed.");
    }

    /// <summary>Batch entry: spectator checks, then the existing session checks; exits non-zero on failure.</summary>
    public static void RunBatch()
    {
        int code = 0;
        try { Run(); RailSessionChecks.Run(); }
        catch (Exception e) { Debug.LogError(e.Message); code = 1; }
        EditorApplication.Exit(code);
    }
}
#endif
