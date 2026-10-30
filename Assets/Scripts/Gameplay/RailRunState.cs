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
    public bool finished, success;
    public string reason;
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
    public float BridgeProgress(double now) => bridgeAt <= 0 ? 0 : Mathf.Clamp01((float)((now - bridgeAt) / BridgeSeconds));
    public const double BridgeSeconds = 60, ReviveSeconds = 3, ProtectionSeconds = 3, FlareCooldown = 45, AllDownGrace = 5;
    public void Finish(double now, string cause, IEnumerable<string> aboard)
    {
        if (finished) return;
        var winners = new HashSet<string>(aboard);
        foreach (var p in players) { p.escaped = winners.Contains(p.id); p.reviveAt = 0; p.rescuer = ""; }
        finished = true; finishedAt = now; reason = cause; success = players.Any(p => p.escaped);
    }
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
