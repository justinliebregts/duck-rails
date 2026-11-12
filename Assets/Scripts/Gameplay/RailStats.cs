using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Client-side event counters for end-of-run superlatives (see <see cref="RailAwards"/>).
/// Events are batched and sent to the master every few seconds as a single "stats" request so they
/// never crowd out gameplay requests (coal, down, gold). Only the local player's own actions are counted.
/// </summary>
public static class RailStats
{
    public const float FlushSeconds = 3f;
    // Per-flush clamps keep a malformed request from inflating a stat.
    public const int MaxCount = 100;
    public const float MaxSeconds = 10f;

    static int drops, pickups, repels, spent;
    static float flashlight;
    static float nextFlush, lastRepel = -10;
    static string run;
    static readonly HashSet<int> pickedUp = new HashSet<int>();

    static bool Active => PhotonSession.IsRailsRoom && PhotonSession.OnTrain && PhotonSession.Phase == RailPhase.Running &&
        RailExpedition.Instance != null && !RailExpedition.Instance.State.finished;

    /// <summary>A local melee/thrown hit sent to an enemy. Rate-limited so one swing counts once.</summary>
    public static void Repel()
    {
        if (!Active || Time.unscaledTime - lastRepel < 1f) return;
        lastRepel = Time.unscaledTime; repels++;
    }

    /// <summary>Counts distinct items picked up in the run (re-grabbing the same item does not count again).</summary>
    public static void Pickup(Component item)
    {
        if (!Active || Ignored(item)) return;
        if (pickedUp.Add(Key(item))) pickups++;
    }

    public static void Drop(Component item)
    {
        if (!Active || Ignored(item)) return;
        drops++;
    }

    public static void Spent(int coins) { if (Active && coins > 0) spent += coins; }

    public static void Flashlight(float seconds) { if (Active && seconds > 0) flashlight += seconds; }

    // Furnace coal and gold bars are released on purpose all the time; they are not "fumbles".
    static bool Ignored(Component item) =>
        item == null || item.GetComponent<RailCoal>() != null || item.GetComponent<ItemGoldBar>() != null;

    static int Key(Component item)
    {
        var view = item.GetComponent<Photon.Pun.PhotonView>();
        return view != null && view.ViewID != 0 ? view.ViewID : item.GetInstanceID();
    }

    /// <summary>Called every frame by <see cref="RailExpedition"/> while aboard.</summary>
    public static void Tick()
    {
        if (run != PhotonSession.RunId)
        { run = PhotonSession.RunId; drops = pickups = repels = spent = 0; flashlight = 0; pickedUp.Clear(); nextFlush = 0; }
        if (Time.unscaledTime < nextFlush) return;
        nextFlush = Time.unscaledTime + FlushSeconds;
        if (drops + pickups + repels + spent == 0 && flashlight < .01f) return;
        if (RailExpedition.Instance == null || !Active) return;
        RailExpedition.Instance.RequestStats(new object[] { drops, pickups, repels, spent, flashlight });
        drops = pickups = repels = spent = 0; flashlight = 0;
    }

    /// <summary>Master-side: validates a "stats" payload and applies it to the sender's participant.</summary>
    public static bool Apply(RailParticipant p, object[] v)
    {
        if (p == null || v == null || v.Length != 5 || !(v[0] is int d) || !(v[1] is int pk) || !(v[2] is int r) ||
            !(v[3] is int s) || !(v[4] is float f)) return false;
        p.drops += Mathf.Clamp(d, 0, MaxCount);
        p.pickups += Mathf.Clamp(pk, 0, MaxCount);
        p.repels += Mathf.Clamp(r, 0, MaxCount);
        p.spent += Mathf.Clamp(s, 0, 100000);
        p.flashlightSeconds += Mathf.Clamp(float.IsNaN(f) ? 0 : f, 0, MaxSeconds);
        return true;
    }
}
