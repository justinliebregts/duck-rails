using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Photon fetches the server-time offset once per connection, then advances it with a local
// monotonic timer. On Quest/Android that timer stops while the device sleeps, so a short
// standby that keeps the connection leaves PhotonNetwork.ServerTimestamp behind by the
// sleep duration. Heartbeats compare sender and receiver timestamps; a skewed peer's
// heartbeats were rejected indefinitely, and the master then dropped every item that
// healthy-but-"unavailable" player picked up. Re-sync the clock after sleeps and when a
// live peer disagrees, and judge item-recovery liveness from real-time arrival cadence.
public sealed partial class PhotonMasterHandoff
{
    const byte ClockResyncCode = 115;
    const double ClockFetchCooldown = 2, ClockRequestCooldown = 10, ClockCheckInterval = 1;
    const int ClockJumpToleranceMs = 1000;
    // Two arrivals at least this far apart, whose sender-clock spacing matches local
    // receipt spacing, prove the sender's game loop is running now (not a queued burst).
    const double LivenessMinSpacing = 0.5, LivenessTolerance = 0.75;

    struct Arrival
    {
        public int AnchorSent, LastSent;
        public double AnchorLocal, LastLocal;
        public bool Ready;
    }

    readonly Dictionary<int, Arrival> arrivals = new Dictionary<int, Arrival>(PhotonSession.MaxPlayers);
    readonly Dictionary<int, double> clockRequests = new Dictionary<int, double>(PhotonSession.MaxPlayers);
    readonly RaiseEventOptions clockTarget = new RaiseEventOptions { TargetActors = new int[1] };
    double arrivalsSince, nextClockFetch, nextClockCheck, clockBaselineAfter;
    int lastClockDrift;
    bool hasClockDrift;
    Room clockRoom;
    /// <summary>Count of local server-clock re-syncs, for checks and diagnostics.</summary>
    public static int ClockResyncs { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetClockStatistics() => ClockResyncs = 0;

    static int WallMilliseconds => unchecked((int)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond));

    void ResetArrivals()
    {
        arrivals.Clear();
        clockRequests.Clear();
        arrivalsSince = Time.realtimeSinceStartupAsDouble;
    }

    // Every structurally valid heartbeat from an active peer, accepted or not.
    void RecordArrival(int sender, int sentAt, bool ready)
    {
        double now = Time.realtimeSinceStartupAsDouble;
        bool known = arrivals.TryGetValue(sender, out Arrival a);
        int step = known ? unchecked(sentAt - a.LastSent) : 1;
        // Reordered by the sender's own clock. Equal stamps are accepted, as heartbeats
        // are: a state change can be published in the same millisecond. A large backwards
        // step is a re-synchronised sender clock: restart its series instead of rejecting it.
        if (step < 0 && -step < TimeoutMilliseconds) return;
        bool consistent = known && step >= 0 && a.AnchorLocal >= arrivalsSince &&
            Math.Abs(unchecked(sentAt - a.AnchorSent) / 1000d - (now - a.AnchorLocal)) <= LivenessTolerance;
        if (!consistent) { a.AnchorSent = sentAt; a.AnchorLocal = now; }
        a.LastSent = sentAt; a.LastLocal = now; a.Ready = ready;
        arrivals[sender] = a;
    }

    /// <summary>The peer is sending ready heartbeats in real time, even if its clock disagrees with ours.</summary>
    bool LiveDespiteClock(int actor)
    {
        if (!arrivals.TryGetValue(actor, out Arrival a) || !a.Ready) return false;
        double now = Time.realtimeSinceStartupAsDouble;
        return now - a.LastLocal < TimeoutMilliseconds / 1000d && a.AnchorLocal >= arrivalsSince &&
            a.LastLocal - a.AnchorLocal >= LivenessMinSpacing;
    }

    void ObserveClockDisagreement(Player sender, int sentAt)
    {
        if (Fresh(sentAt, PhotonNetwork.ServerTimestamp) || !LiveDespiteClock(sender.ActorNumber)) return;
        double now = Time.realtimeSinceStartupAsDouble;
        if (clockRequests.TryGetValue(sender.ActorNumber, out double next) && now < next) return;
        clockRequests[sender.ActorNumber] = now + ClockRequestCooldown;
        int skew = unchecked(PhotonNetwork.ServerTimestamp - sentAt);
        if (DuckzLog.AllowWarning(out var notice, "HeartbeatClockSkew"))
            notice.Write($"Heartbeat clock disagreement: actor={sender.ActorNumber} is live but its timestamps differ by {skew} ms (local actor={PhotonNetwork.LocalPlayer.ActorNumber}, master={PhotonNetwork.CurrentRoom?.MasterClientId}). Requesting server-time re-sync.");
        if (RailDiagnostics.Instance != null) RailDiagnostics.Instance.Event($"heartbeat clock skew actor={sender.ActorNumber} skew={skew}ms");
        // Either side may be the skewed one; both re-fetch from the server.
        clockTarget.TargetActors[0] = sender.ActorNumber;
        PhotonNetwork.RaiseEvent(ClockResyncCode, null, clockTarget, SendOptions.SendReliable);
        FetchServerClock("peer clock disagreement");
    }

    void ReceiveClockResync(EventData data)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.OfflineMode) return;
        Player sender = PhotonNetwork.CurrentRoom.GetPlayer(data.Sender);
        if (sender == null || sender.IsInactive || sender.IsLocal) return;
        FetchServerClock("requested by actor " + data.Sender);
    }

    void FetchServerClock(string reason)
    {
        double now = Time.realtimeSinceStartupAsDouble;
        if (now < nextClockFetch || !PhotonNetwork.IsConnected || PhotonNetwork.OfflineMode) return;
        nextClockFetch = now + ClockFetchCooldown;
        hasClockDrift = false;
        clockBaselineAfter = now + 1; // The fetch completes after a roundtrip.
        ClockResyncs++;
        PhotonNetwork.FetchServerTimestamp();
        PhotonNetwork.SendAllOutgoingCommands();
        if (DuckzLog.AllowWarning(out var notice, "ServerClockResync"))
            notice.Write($"Re-syncing Photon server time ({reason}).");
        if (RailDiagnostics.Instance != null) RailDiagnostics.Instance.Event("server clock resync: " + reason);
    }

    // A monotonic timer that stopped during sleep shows up as a sudden jump between
    // the wall clock and ServerTimestamp. Gradual drift and slewing are tracked, not flagged.
    void CheckLocalClock()
    {
        double now = Time.realtimeSinceStartupAsDouble;
        if (now < nextClockCheck) return;
        nextClockCheck = now + ClockCheckInterval;
        // ServerTimestamp follows the connected server's own tick count; a master/game
        // server switch or room transfer is a new baseline, not a stopped local timer.
        if (!PhotonNetwork.IsConnectedAndReady || PhotonNetwork.Server != ServerConnection.GameServer ||
            !ReferenceEquals(clockRoom, PhotonNetwork.CurrentRoom) || now < clockBaselineAfter)
        { hasClockDrift = false; clockRoom = PhotonNetwork.CurrentRoom; return; }
        int drift = unchecked(WallMilliseconds - PhotonNetwork.ServerTimestamp);
        int change = unchecked(drift - lastClockDrift);
        bool jumped = hasClockDrift && (change > ClockJumpToleranceMs || change < -ClockJumpToleranceMs);
        lastClockDrift = drift;
        hasClockDrift = true;
        if (jumped) FetchServerClock($"local timer jumped {change} ms against the wall clock");
    }

    /// <summary>Why item recovery treats the actor as unavailable, or null when available.</summary>
    public string UnavailableReason(int actor)
    {
        if (actor <= 0 || !PhotonNetwork.InRoom) return null;
        if (actor == PhotonNetwork.LocalPlayer.ActorNumber) return LocalAvailable ? null : "local input unavailable";
        Player player = PhotonNetwork.CurrentRoom.GetPlayer(actor);
        if (player != null && player.IsInactive) return "inactive";
        // Real-time ready arrivals prove a running, available client regardless of clock offset.
        if (LiveDespiteClock(actor)) return null;
        int now = PhotonNetwork.ServerTimestamp;
        if (heartbeats.TryGetValue(actor, out Heartbeat heartbeat))
        {
            if (heartbeat.Received && !heartbeat.Ready) return "reported not ready";
            return Fresh(heartbeat.SentAt, now) ? null : $"heartbeat stale ({unchecked(now - heartbeat.SentAt)} ms)";
        }
        // A confirmed master change must not restart the observation already done
        // since waking. observeUntil separately protects master-election stability.
        return Fresh(observationStartedAt, now) ? null : "no heartbeat since observation start";
    }

    internal void NoteHeldRecovery(PhotonView view, int holder, int departedActor)
    {
        string reason = holder == departedActor ? "departed" : UnavailableReason(holder);
        if (DuckzLog.AllowWarning(out var notice, "MasterHeldRecovery"))
            notice.Write($"Master recovering held object: view={view.ViewID}, holder={holder}, owner={view.OwnerActorNr}, reason={reason}.", view);
        if (RailDiagnostics.Instance != null) RailDiagnostics.Instance.Event($"master recovered view={view.ViewID} holder={holder} reason={reason}");
    }
}
