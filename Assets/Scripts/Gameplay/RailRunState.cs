using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// One retained, versioned snapshot. The master commits it with fuel/motion/phase
// using PhotonSession's revision CAS; never mutate a cached snapshot in place.
[Serializable]
public sealed class RailRunState
{
    public string run;
    public double bridgeAt, allDownAt, finishedAt;
    public double bridgeElapsed, bridgeHeldAt, bridgeHoldUntil;
    public bool finished, success;
    public string reason;
    public List<RailGoldDeposit> gold = new List<RailGoldDeposit>();
    public List<RailParticipant> players = new List<RailParticipant>();
    public List<int> consumed = new List<int>();
    public List<string> consumedSpawns = new List<string>();
    public List<string> consumedMarkers = new List<string>();
    public List<RailCarrierState> carriers = new List<RailCarrierState>();
    public double recoveryAt;
    public bool WasConsumed(int viewId, string spawnToken)
    {
        for (int i = 0; i < consumed.Count && i < consumedSpawns.Count; i++)
            if (consumed[i] == viewId && consumedSpawns[i] == spawnToken) return true;
        return false;
    }
    public RailParticipant Player(string id) => players.Find(p => p.id == id);
    public double BridgeElapsed(double now) => Math.Min(BridgeSeconds, bridgeElapsed +
        (bridgeHeldAt > 0 ? Math.Max(0, Math.Min(now, bridgeHoldUntil) - bridgeHeldAt) : 0));
    public float BridgeProgress(double now) => Mathf.Clamp01((float)(BridgeElapsed(now) / BridgeSeconds));
    public bool BridgeMoving(double now) => !finished && bridgeHeldAt > 0 && bridgeHoldUntil > now && BridgeProgress(now) < 1;
    // Retain both completed work and a bounded live segment. A missing operator or
    // master cannot turn a stale hold into an unattended lowering timer.
    public void UpdateBridgeHold(double now, double until)
    {
        if (finished) return;
        if (double.IsNaN(until) || double.IsInfinity(until) || until <= now || until > now + 2) until = 0;
        if (until == bridgeHoldUntil && until > now && BridgeProgress(now) < 1) return;
        bridgeElapsed = BridgeElapsed(now);
        bridgeHeldAt = bridgeElapsed < BridgeSeconds && until > now ? now : 0;
        bridgeHoldUntil = bridgeHeldAt > 0 ? until : 0;
        if (bridgeHeldAt > 0 && bridgeAt <= 0) bridgeAt = now;
    }
    // Initial hold-duration tuning; solo/enemy balance still needs playtesting.
    public const double BridgeSeconds = 30, ReviveSeconds = 3, ProtectionSeconds = 3, FlareCooldown = 45, AllDownGrace = 5;
    public void Finish(double now, string cause, IEnumerable<string> aboard)
    {
        if (finished) return;
        UpdateBridgeHold(now, 0);
        var winners = new HashSet<string>(aboard);
        foreach (var p in players) { p.escaped = winners.Contains(p.id); p.reviveAt = 0; p.rescuer = ""; }
        finished = true; finishedAt = now; reason = cause; success = players.Any(p => p.escaped);
    }
}

[Serializable]
public sealed class RailGoldDeposit
{
    public string token;
    public int receiver;
    public string[] recipients;
}

[Serializable]
public sealed class RailCarrierState
{
    public string marker;
    public int generation;
    public bool carrying = true;
    public List<Vector3> drops = new List<Vector3>();
}

[Serializable]
public sealed class RailParticipant
{
    public string id, name, rescuer;
    public int actor, coal, revives, downs, horns;
    public bool down, escaped;
    public double protectedUntil, flareAt, reviveAt, hornAt;
    public Vector3 flarePosition;
}
