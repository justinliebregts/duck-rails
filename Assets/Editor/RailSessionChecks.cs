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
        var codes = Enumerable.Range(0, 100000).Select(i => RailSessionRules.RoomCode("crew-token", i)).ToArray();
        Check(codes.All(RailSessionRules.IsCode) && codes.Distinct().Count() == 100000,
            "Allocation probes cover every five-digit code without repeats");
        Check(RailSessionRules.RoomCode("crew-token") == RailSessionRules.RoomCode("crew-token", 100000),
            "Crew allocation is deterministic and wraps at the code-space boundary");
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
        Check(Enumerable.Range(1, 4).All(RailSessionRules.SearchExistingCompound), "One through four returning players search existing compounds");
        Check(!RailSessionRules.SearchExistingCompound(0) && Enumerable.Range(5, 4).All(n => !RailSessionRules.SearchExistingCompound(n)),
            "Five through eight returning players skip random matchmaking");
        var leases = RailSessionRules.ReservationLeases(new[] { "crew-a", "crew-b" }, null, 100d);
        Check((double)leases["crew-a"] == 130d && (double)leases["crew-b"] == 130d, "Missing reservation leases receive thirty seconds");
        var resumed = RailSessionRules.ReservationLeases(new[] { "crew-a", "crew-b", "new-crew" }, leases, 120d);
        Check((double)resumed["crew-a"] == 130d && (double)resumed["new-crew"] == 150d,
            "Master succession preserves deadlines while concurrent arrivals receive their own lease");
        var remaining = RailSessionRules.ReservationLeases(new[] { "new-crew" }, resumed, 131d);
        Check(remaining.Count == 1 && (double)remaining["new-crew"] == 150d,
            "Removing an expired crew does not shorten another crew's reservation");
        Check(RailSessionRules.ReservationLeases(Array.Empty<string>(), remaining, 151d).Count == 0,
            "Consumed reservations remove obsolete lease metadata");
        Check(RailSessionRules.CanTransition(RailPhase.Staging, RailPhase.Departing) && !RailSessionRules.CanTransition(RailPhase.Staging, RailPhase.Ending), "Invalid phase shortcuts rejected");
        Check(new[] { RailPhase.Departing, RailPhase.Running, RailPhase.Ending, RailPhase.Returning }.All(RailSessionRules.IsOnTrain),
            "Crew stays aboard through ending and returning instead of spawning in a temporary compound");
        Check(!RailSessionRules.IsOnTrain(RailPhase.Compound) && !RailSessionRules.IsOnTrain(RailPhase.Staging),
            "Public compound arrival and private staging present the compound");
        RailSessionRules.EvaluateMotion(new object[] { 10d, 0f, 0f, 4f }, 11, out float z, out float v);
        Check(Mathf.Abs(z - 1f) < .0001f && v == 2, "Acceleration integrates correct distance and speed");
        RailSessionRules.EvaluateMotion(new object[] { 10d, 0f, 0f, 4f }, 15, out z, out v);
        Check(z == 16 && v == 4, "Late join reconstructs moving train from current command");
        RailSessionRules.EvaluateMotion(new object[] { 15d, 16f, 4f, 0f }, 20, out z, out v);
        Check(z == 20 && v == 0, "Braking stops without changing past position");
        var go = new object[] { 10d, 0f, 0f, 4f };
        RailSessionRules.EvaluateTrain(go, 100f, 21d, out z, out v, out float fuel);
        Check(Mathf.Abs(fuel - 98f) < .0001f && z == 40f, "Fuel burns with travelled distance, including acceleration");
        var stop = new object[] { 21d, z, v, 0f };
        RailSessionRules.EvaluateTrain(stop, fuel, 30d, out z, out v, out float stoppedFuel);
        RailSessionRules.EvaluateTrain(stop, fuel, 300d, out float laterZ, out _, out float laterFuel);
        Check(Mathf.Abs(stoppedFuel - 97.8f) < .0001f && stoppedFuel == laterFuel && z == laterZ,
            "Braking consumes fuel; a stopped train does not");
        RailSessionRules.EvaluateTrain(go, 100f, 21d, out z, out v, out fuel);
        var checkpoint = new object[] { 21d, z, v, 4f };
        RailSessionRules.EvaluateTrain(checkpoint, fuel, 41d, out z, out v, out fuel);
        RailSessionRules.EvaluateTrain(go, 100f, 41d, out laterZ, out float laterV, out laterFuel);
        Check(z == laterZ && v == laterV && Mathf.Abs(fuel - laterFuel) < .0001f,
            "Restored room snapshot preserves fuel and motion across a master checkpoint");
        RailSessionRules.EvaluateTrain(go, .1f, 100d, out z, out v, out fuel);
        Check(z == 2f && v == 0f && fuel == 0f, "Fuel exhaustion caps travel and stops even without authority ticks");
        RailSessionRules.EvaluateTrain(new object[] { 100d, z, v, 4f }, fuel, 200d, out laterZ, out laterV, out laterFuel);
        Check(laterZ == z && laterV == 0f && laterFuel == 0f, "Empty fuel cannot restart or become negative");
        RailSessionRules.EvaluateTrain(go, 100f, 9d, out z, out v, out fuel);
        Check(z == 0f && v == 0f && fuel == 100f, "Future motion commands do not burn fuel early");
        Check(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).SequenceEqual(new[] { "Assets/Scenes/Login.unity", "Assets/Scenes/Compound.unity", "Assets/Scenes/Expedition.unity" }), "Build starts at Login, includes Expedition and excludes Quackville/Main");
        foreach (string name in new[] { "RailBat", "RailBag" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Rails/" + name + ".prefab");
            Check(prefab != null && prefab.GetComponent<PhotonView>().ObservedComponents.Contains(prefab.GetComponent<RailCargo>()), name + " observes cargo state over PUN");
        }
        Debug.Log("RAILS CHECKS PASSED: " + checks + ". These are local checks, not multi-client acceptance.");
    }
}
#endif
