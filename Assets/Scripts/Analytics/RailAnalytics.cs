using System.Collections.Generic;
using System.Linq;
using GameAnalyticsSDK;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// Expedition analytics derived from committed <see cref="RailRunState"/> snapshots.
/// run:start, run:launch and the run:end / run:result family are sent by the actor that claimed them
/// in the committed state (RailRunState.TrackCrew), so each run counts once across handoffs and
/// restarts. Other run-level events (block:*, train:*, bridge:*) are sent by the current master.
/// Personal events (run:join, progression, down:*, revive:*, player:*, award:*) are sent by each
/// client about its own participant. A master handoff can
/// shorten the current block's time; it never re-sends bridge or fuel events.
/// </summary>
public static class RailAnalytics
{
    const float CauseWindowSeconds = 5f;
    static readonly string[] EnemyKinds = { "bomber", "smasher", "spider", "hook", "zombie" };

    static string run;
    static RailRunState last;
    static bool sawRunning, finishSent, primed, wasMaster, localDown;
    static bool bridgeStartSent, bridgeLoweredSent, fuelOut;
    static double downSince, blockSince;
    static string trainBlock;
    static string pendingCause;
    static float pendingCauseAt = -100f, lastFuel = -1f;
    static int goldPicked;

    /// <summary>Called just before a local down request so the committed down can be attributed.</summary>
    public static void NoteDownCause(string cause)
    {
        pendingCause = cause;
        pendingCauseAt = Time.unscaledTime;
    }

    /// <summary>A distinct gold bar picked up by the local player during this run.</summary>
    public static void GoldPickedUp() => goldPicked++;

    /// <summary>Called every frame by RailExpedition while aboard with a ready route.</summary>
    public static void Observe(RailRunState s)
    {
        if (s == null || string.IsNullOrEmpty(s.run)) return;
        if (s.run != run) Reset(s.run);
        double now = PhotonNetwork.Time;
        bool running = PhotonSession.Phase == RailPhase.Running && !s.finished;
        if (running && !sawRunning)
        {
            sawRunning = true;
            DuckzAnalytics.Progression(GAProgressionStatus.Start, "expedition", RunKind(), 0);
        }
        // A client that only sees the ending (or a returning room) reports nothing for this run.
        if (!sawRunning) { last = s; return; }

        bool master = PhotonNetwork.IsMasterClient;
        if (master && !wasMaster) AdoptAuthority(s, now);
        wasMaster = master;
        if (master && !s.finished) TrackAuthority(s, now);
        if (!ReferenceEquals(s, last)) { ObserveCrew(s); ObserveLocal(s, now); }
        if (s.finished && !finishSent)
        {
            finishSent = true;
            FinishPersonal(s);
            if (Mine(s.endReporter) && Once("end")) FinishRun(s);
        }
        last = s;
    }

    static void Reset(string next)
    {
        run = next; last = null;
        sawRunning = finishSent = primed = wasMaster = localDown = false;
        bridgeStartSent = bridgeLoweredSent = fuelOut = false;
        trainBlock = null; blockSince = 0; downSince = 0;
        lastFuel = -1f; goldPicked = 0;
        BlockNames.Clear(); sent.Clear();
    }

    // Committed claims and join times, so a handoff, rejoin or app restart never repeats an event.
    static void ObserveCrew(RailRunState s)
    {
        if (Mine(s.startReporter) && Once("start"))
            DuckzAnalytics.Event(s.peakCrew, null, "run", "start", RunKind());
        if (Mine(s.launchReporter) && Once("launch"))
            DuckzAnalytics.Event(s.launchCrew, null, "run", "launch", CrewKind(), Size(s.launchCrew));
        var p = s.Player(PhotonNetwork.LocalPlayer?.UserId);
        if (p != null && p.joinedAt > 0 && Once("join"))
            DuckzAnalytics.Event(RunSeconds(s, p.joinedAt), null, "run", "join", CrewKind(), p.launched ? "launch" : "late");
    }

    static bool Mine(int reporter) => reporter != 0 && reporter == PhotonNetwork.LocalPlayer?.ActorNumber;

    // Survives an app restart into the same run; only the newest run is remembered.
    const string SentKey = "rails.analytics.sent";
    static readonly HashSet<string> sent = new HashSet<string>();
    static bool Once(string tag)
    {
        if (!sent.Add(tag)) return false;
        string saved = UserPlayerPrefs.GetString(SentKey, "");
        if (!saved.StartsWith(run, System.StringComparison.Ordinal)) saved = run;
        if (saved.Split('|').Contains(tag)) return false;
        UserPlayerPrefs.SetString(SentKey, saved + "|" + tag);
        return true;
    }

    /// <summary>Crew events keep editor and development-build runs out of the public and private win rates.</summary>
    static string CrewKind() => Application.isEditor || Debug.isDebugBuild ? "test" : RunKind();

    static string Size(int crew) => "p" + Mathf.Clamp(crew, 1, RailSessionRules.CrewCapacity);

    static string RunKind() => PhotonSession.IsPrivate ? "private" : "public";

    static float RunSeconds(RailRunState s, double now) =>
        s.startAt > 0 ? Mathf.Max(0f, (float)(now - s.startAt)) : 0f;

    // Becoming master mid-run: adopt what already happened instead of reporting it again.
    static void AdoptAuthority(RailRunState s, double now)
    {
        bridgeStartSent = s.bridgeAt > 0;
        bridgeLoweredSent = s.BridgeProgress(now) >= 1;
        fuelOut = PhotonSession.CurrentFuel <= 0f;
        trainBlock = TrainBlock();
        blockSince = now;
    }

    static void TrackAuthority(RailRunState s, double now)
    {
        string block = TrainBlock();
        if (block != null && block != trainBlock)
        {
            if (trainBlock != null) DuckzAnalytics.Event((float)(now - blockSince), null, "block", "time", trainBlock);
            trainBlock = block; blockSince = now;
        }
        if (PhotonSession.Phase != RailPhase.Running) return;
        float fuel = PhotonSession.CurrentFuel;
        lastFuel = fuel;
        if (fuel <= 0f && !fuelOut)
        {
            fuelOut = true;
            DuckzAnalytics.Event(RunSeconds(s, now), null, "train", "fuel_out", trainBlock ?? "unknown");
        }
        else if (fuel > .5f) fuelOut = false;
        if (!bridgeStartSent && s.bridgeAt > 0)
        {
            bridgeStartSent = true;
            DuckzAnalytics.Event(RunSeconds(s, now), null, "bridge", "start");
        }
        if (!bridgeLoweredSent && s.bridgeAt > 0 && s.BridgeProgress(now) >= 1)
        {
            bridgeLoweredSent = true;
            // Real time from first hold to fully lowered, including pauses.
            DuckzAnalytics.Event((float)(now - s.bridgeAt), null, "bridge", "lowered");
        }
    }

    static void ObserveLocal(RailRunState s, double now)
    {
        var p = s.Player(PhotonNetwork.LocalPlayer?.UserId);
        if (p == null) return;
        if (!primed)
        {
            // A rejoin while already down is not a new down.
            primed = true; localDown = p.down; downSince = now;
            return;
        }
        if (p.down && !localDown)
        {
            localDown = true; downSince = now;
            if (s.finished) return;
            string cause = Time.unscaledTime - pendingCauseAt <= CauseWindowSeconds ? DownCause(pendingCause) : "unknown";
            pendingCauseAt = -100f;
            RailBoardReports.Down(cause);
            DuckzAnalytics.Event(RunSeconds(s, now), null, "down", cause, LocalBlock());
        }
        else if (!p.down && localDown)
        {
            localDown = false;
            DuckzAnalytics.Event((float)(now - downSince), null, "revive", "done");
        }
    }

    static void FinishPersonal(RailRunState s)
    {
        var p = s.Player(PhotonNetwork.LocalPlayer?.UserId);
        if (p == null) return;
        var fields = new Dictionary<string, object>
        {
            ["coal"] = p.coal, ["gold"] = p.gold, ["gold_picked"] = goldPicked, ["downs"] = p.downs,
            ["revives"] = p.revives, ["revived"] = p.revived, ["repels"] = p.repels, ["spent"] = p.spent,
        };
        DuckzAnalytics.Progression(s.success ? GAProgressionStatus.Complete : GAProgressionStatus.Fail,
            "expedition", RunKind(), p.gold, fields);
        DuckzAnalytics.Event(RunSeconds(s, s.finishedAt), fields, "player", p.escaped ? "escaped" : "dnf");
        if (goldPicked > 0) DuckzAnalytics.Event(goldPicked, null, "gold", "picked");
        if (!string.IsNullOrEmpty(p.award)) DuckzAnalytics.Event("award", p.award);
        if (p.down) DuckzAnalytics.Event("down", "unrevived");
    }

    static void FinishRun(RailRunState s)
    {
        if (trainBlock != null) DuckzAnalytics.Event((float)(s.finishedAt - blockSince), null, "block", "time", trainBlock);
        int unbanked = UnbankedGold(s);
        var fields = new Dictionary<string, object>
        {
            ["kind"] = RunKind(),
            ["crew"] = s.players.Count,
            ["launch_crew"] = s.launchCrew, ["end_crew"] = s.endCrew, ["peak_crew"] = s.peakCrew,
            ["late_joins"] = s.players.Count(p => !p.launched),
            ["escaped"] = s.players.Count(p => p.escaped),
            ["coal"] = s.players.Sum(p => p.coal),
            ["gold"] = s.players.Sum(p => p.gold),
            ["unbanked"] = unbanked,
            ["downs"] = s.players.Sum(p => p.downs),
            ["revives"] = s.players.Sum(p => p.revives),
            ["fuel"] = Mathf.Round(Mathf.Max(0f, lastFuel) * 10f) / 10f,
            ["block"] = trainBlock ?? "unknown",
        };
        DuckzAnalytics.Event(RunSeconds(s, s.finishedAt), fields, "run", "end", s.success ? "success" : "fail", s.reason);
        // Win rate by party size: outcome is the last part, so each size reads as success against fail.
        string outcome = s.success ? "success" : "fail";
        float seconds = RunSeconds(s, s.finishedAt);
        DuckzAnalytics.Event(seconds, null, "run", "result", CrewKind(), Size(s.launchCrew), outcome);
        DuckzAnalytics.Event(seconds, null, "run", "result_end", CrewKind(), Size(s.endCrew), outcome);
        DuckzAnalytics.Event("run", "last_block", trainBlock ?? "unknown");
        DuckzAnalytics.Event(unbanked, null, "gold", "unbanked");
    }

    // Loose bars still in the world plus bars carried in bags: gold the crew never cashed out.
    static int UnbankedGold(RailRunState s)
    {
        int count = 0;
        foreach (var view in PhotonNetwork.PhotonViewCollection)
            if (view != null && view.GetComponent<ItemGoldBar>() != null && view.transform.position.y > -.5f &&
                !s.WasConsumed(view.ViewID, view.SpawnToken)) count++;
        var prefab = RailExpedition.Instance != null ? RailExpedition.Instance.goldPrefab : null;
        if (prefab != null)
            foreach (var bag in BagObject.Active)
                if (bag != null) count += bag.Keys().Count(k => k == prefab.name);
        return count;
    }

    static string DownCause(string raw)
    {
        string cause = DuckzAnalytics.Segment(raw);
        foreach (var kind in EnemyKinds) if (cause.Contains(kind)) return kind;
        return cause == "hazard" ? "hazard" : "other";
    }

    static string TrainBlock() =>
        RailTrain.Instance != null ? BlockAt(RailTrain.Instance.transform.position) : null;

    static string LocalBlock() => DuckzAnalytics.LocalHead(out var head) ? BlockAt(head) ?? "unknown" : "unknown";

    /// <summary>Stable block name for a world position, or null outside the route.</summary>
    public static string BlockAt(Vector3 position)
    {
        var route = RailRoute.Instance;
        if (route == null || !route.TryPlayerArea(position, out var block)) return null;
        if (block == route.startingStation) return "station";
        if (block == route.midpointBlock) return "midpoint";
        if (block == route.finale) return "bridge";
        // Sampled every master frame: name each authored block once.
        if (BlockNames.TryGetValue(block, out var name)) return name;
        string id = string.IsNullOrEmpty(block.id) ? block.name : block.id;
        int version = id.LastIndexOf("-v", System.StringComparison.Ordinal);
        if (version > 0 && int.TryParse(id.Substring(version + 2), out _)) id = id.Substring(0, version);
        return BlockNames[block] = DuckzAnalytics.Segment(id);
    }
    static readonly Dictionary<RailBlock, string> BlockNames = new Dictionary<RailBlock, string>();

    /// <summary>"departure" for the starting-station shop, "midpoint" for the helipad shop.</summary>
    public static string ShopName(Vector3 position)
    {
        string block = BlockAt(position);
        return block == "station" ? "departure" : block ?? "unknown";
    }
}
