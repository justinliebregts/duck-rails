#if UNITY_EDITOR
using System;
using System.Linq;
using Photon.Pun;
using UnityEditor;
using UnityEngine;

public static class RailSessionChecks
{
    static int checks;
    static void Check(bool pass, string message)
    {
        if (!pass) throw new Exception("Duck Rails check failed: " + message);
        checks++; Debug.Log("RAILS PASS: " + message);
    }
    [MenuItem("Duck Rails/Run Session Checks")]
    public static void Run()
    {
        checks = 0;
        Check(RailSessionRules.IsCode("00001") && RailSessionRules.IsCode("99999"), "Five-digit codes preserve leading zeroes");
        Check(!RailSessionRules.IsCode("1234") && !RailSessionRules.IsCode("123456") && !RailSessionRules.IsCode("12a45") && !RailSessionRules.IsCode("１２３４５"), "Invalid and non-ASCII codes rejected");
        double start = RailSessionRules.Countdown(0, 1, 10);
        Check(start == 30, "First occupant starts twenty seconds");
        Check(RailSessionRules.Countdown(start, 8, 14) == 30 && RailSessionRules.Countdown(start, 1, 29) == 30, "Entry/exit cannot extend a nonempty countdown");
        double empty = RailSessionRules.Countdown(start, 0, 29);
        Check(empty == 0 && RailSessionRules.Countdown(empty, 1, 40) == 60, "Empty cancels and re-entry starts a fresh countdown");
        int[] admitted = RailSessionRules.Admit(Array.Empty<int>(), Enumerable.Range(1, 9));
        Check(admitted.SequenceEqual(Enumerable.Range(1, 8)), "Ninth occupant waits outside the eight-player roster");
        Check(RailSessionRules.Admit(admitted, new[] { 2, 3, 4, 5, 6, 7, 8, 9 }).SequenceEqual(new[] { 2, 3, 4, 5, 6, 7, 8, 9 }), "Vacated slots refill without evicting admitted players");
        var publicRoom = RailSessionRules.Options(RailSessionRules.CompoundKind);
        var privateRoom = RailSessionRules.Options(RailSessionRules.PrivateKind);
        Check(publicRoom.MaxPlayers == 12 && publicRoom.IsVisible && publicRoom.PlayerTtl == 0, "Public compounds: twelve players, discoverable, no inactive shoppers");
        Check(privateRoom.MaxPlayers == 8 && !privateRoom.IsVisible && privateRoom.IsOpen, "Private rooms: eight players, hidden, code joinable");
        Check(privateRoom.PlayerTtl == 120000 && privateRoom.EmptyRoomTtl == 0, "120-second reconnect slots; empty rooms close immediately");
        Check(RailSessionRules.CanTransition(RailPhase.Staging, RailPhase.Departing) && !RailSessionRules.CanTransition(RailPhase.Staging, RailPhase.Ending), "Invalid phase shortcuts rejected");
        RailSessionRules.EvaluateMotion(new object[] { 10d, 0f, 0f, 4f }, 11, out float z, out float v);
        Check(Mathf.Abs(z - 1f) < .0001f && v == 2, "Acceleration integrates correct distance and speed");
        RailSessionRules.EvaluateMotion(new object[] { 10d, 0f, 0f, 4f }, 15, out z, out v);
        Check(z == 16 && v == 4, "Late join reconstructs moving train from current command");
        RailSessionRules.EvaluateMotion(new object[] { 15d, 16f, 4f, 0f }, 20, out z, out v);
        Check(z == 20 && v == 0, "Braking stops without changing past position");
        Check(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).SequenceEqual(new[] { "Assets/Scenes/Login.unity", "Assets/Scenes/Compound.unity" }), "Build starts at Login and excludes Quackville/Main");
        foreach (string name in new[] { "RailBat", "RailBag" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Rails/" + name + ".prefab");
            Check(prefab != null && prefab.GetComponent<PhotonView>().ObservedComponents.Contains(prefab.GetComponent<RailCargo>()), name + " observes cargo state over PUN");
        }
        Debug.Log("RAILS CHECKS PASSED: " + checks + ". These are local checks, not multi-client acceptance.");
    }
}
#endif
