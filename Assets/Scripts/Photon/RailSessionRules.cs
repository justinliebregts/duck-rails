using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Realtime;

public enum RailPhase { Compound, Staging, Departing, Running, Ending, Returning }

public static class RailSessionRules
{
    public const int Version = 3, CompoundCapacity = 12, CrewCapacity = 8, ReconnectMilliseconds = 120000;
    public const double CountdownSeconds = 20;
    public const string Schema = "rails.schema", Kind = "rails.kind", Phase = "rails.phase", Revision = "rails.revision";
    public const string Run = "rails.run", Deadline = "rails.deadline", Roster = "rails.roster", Destination = "rails.destination";
    public const string TransferUsers = "rails.users", TransferUntil = "rails.transferUntil", Ready = "rails.ready", Boarding = "rails.boarding";
    public const string Block = "rails.block", Fuel = "rails.fuel", Motion = "rails.motion", Cargo = "rails.cargo";
    public const string CompoundKind = "compound", PrivateKind = "private", ExpeditionKind = "expedition";

    // Returning still belongs to the expedition. Present the compound only after
    // joining its room (public) or committing Staging (private).
    public static bool IsOnTrain(RailPhase phase) => phase == RailPhase.Departing ||
        phase == RailPhase.Running || phase == RailPhase.Ending || phase == RailPhase.Returning;

    public static bool IsCode(string value)
    {
        if (value == null || value.Length != 5) return false;
        foreach (char c in value) if (c < '0' || c > '9') return false;
        return true;
    }

    public const string Allocation = "rails.allocation";

    // Stable across processes: transferring crew members probe the same codes.
    public static string RoomCode(string allocation, int attempt = 0)
    {
        uint hash = 2166136261;
        unchecked { foreach (char c in allocation) hash = (hash ^ c) * 16777619; }
        return ((hash % 100000 + (uint)attempt) % 100000).ToString("D5", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static RoomOptions Options(string kind, string run = "") => new RoomOptions
    {
        MaxPlayers = (byte)(kind == CompoundKind ? CompoundCapacity : CrewCapacity),
        IsOpen = true, IsVisible = kind == CompoundKind, PublishUserId = true,
        PlayerTtl = kind == CompoundKind ? 0 : ReconnectMilliseconds, EmptyRoomTtl = 0,
        CleanupCacheOnLeave = true, DeleteNullProperties = true,
        CustomRoomPropertiesForLobby = new[] { Kind, Schema },
        CustomRoomProperties = new Hashtable
        {
            [Schema] = Version, [Kind] = kind, [Revision] = 0,
            [Phase] = (int)(kind == CompoundKind ? RailPhase.Compound : kind == PrivateKind ? RailPhase.Staging : RailPhase.Departing),
            [Run] = run, [Deadline] = 0d, [Roster] = Array.Empty<int>(), [Destination] = "",
            [TransferUsers] = Array.Empty<string>(), [TransferUntil] = 0d,
            [Block] = "rail-yard-test-v1", [Fuel] = 100f,
            [Motion] = new object[] { 0d, 0f, 0f, 0f }, [Cargo] = Array.Empty<int>()
        }
    };

    public static bool CanTransition(RailPhase from, RailPhase to) =>
        (from == RailPhase.Staging && to == RailPhase.Departing) ||
        (from == RailPhase.Departing && (to == RailPhase.Running || to == RailPhase.Returning)) ||
        (from == RailPhase.Running && to == RailPhase.Ending) ||
        (from == RailPhase.Ending && to == RailPhase.Returning) ||
        (from == RailPhase.Returning && to == RailPhase.Staging);

    public static double Countdown(double previous, int occupants, double now) =>
        occupants == 0 ? 0d : previous > 0d ? previous : now + CountdownSeconds;

    public static int[] Admit(int[] previous, IEnumerable<int> occupants)
    {
        var available = new SortedSet<int>(occupants);
        var admitted = new List<int>(CrewCapacity);
        foreach (int actor in previous)
            if (available.Remove(actor) && admitted.Count < CrewCapacity) admitted.Add(actor);
        foreach (int actor in available)
            if (admitted.Count < CrewCapacity) admitted.Add(actor);
        return admitted.ToArray();
    }

    // Fuel is retained at the motion command's starting distance and committed
    // atomically with it. Shared time reconstructs consumption without local timers.
    public const float FuelPerMetre = .025f; // 0.1/second at 4 m/s cruise speed.
    public static void EvaluateTrain(object[] command, float startingFuel, double now,
        out float distance, out float speed, out float fuel)
    {
        EvaluateMotion(command, now, out distance, out speed);
        float travelled = Math.Max(0f, distance - (float)command[1]);
        float available = Math.Max(0f, startingFuel);
        fuel = Math.Max(0f, available - travelled * FuelPerMetre);
        if (fuel <= 0f)
        {
            distance = (float)command[1] + available / FuelPerMetre;
            speed = 0f;
        }
    }

    // Exact integration of a two-second ramp, shared by existing and late-joining clients.
    public static void EvaluateMotion(object[] command, double now, out float distance, out float speed)
    {
        double elapsed = Math.Max(0, now - (double)command[0]);
        float t = (float)Math.Min(elapsed, 2d), start = (float)command[2], target = (float)command[3];
        speed = start + (target - start) * t / 2f;
        distance = (float)command[1] + start * t + (target - start) * t * t / 4f + target * (float)Math.Max(0, elapsed - 2d);
    }
}
