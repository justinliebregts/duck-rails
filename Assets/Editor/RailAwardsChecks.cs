using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RailAwardsChecks
{
    static int passed;

    [MenuItem("Duck Rails/Legacy/Run Superlative Checks")]
    public static void Run()
    {
        passed = 0;
        var ctx = new RailAwards.Context { RunSeconds = 600 };

        // Highest value wins; minimum threshold applies.
        var a = P(1, revives: 3); var b = P(2, revives: 5); var c = P(3, revives: 1);
        Assign(ctx, a, b, c);
        Check(b.award == "Hero of the Day", "Highest revives is Hero of the Day");
        Check(a.award != "Hero of the Day" && c.award != "Hero of the Day", "Only one Hero");
        Check(c.award == RailAwards.Fallback, "Below threshold falls back");

        // One title per player: the top coal player already holding Hero takes the next-best for coal.
        var hero = P(1, revives: 4, coal: 20); var miner = P(2, coal: 9); var idle = P(3);
        Assign(ctx, hero, miner, idle);
        Check(hero.award == "Hero of the Day", "Hero keeps higher-priority title");
        Check(miner.award == "Boiler Room Employee", "Next-highest coal gets Boiler Room Employee");
        Check(idle.award == RailAwards.Fallback, "Zero-activity player is Precious Cargo");

        // Ties: escaped first, then lower actor.
        var t1 = P(4, downs: 3, escaped: false); var t2 = P(2, downs: 3, escaped: true); var t3 = P(1, downs: 3, escaped: true);
        Assign(ctx, t1, t2, t3);
        Check(t3.award == "Certified Chicken", "Tie goes to escaped, then lower actor");

        // Thresholds.
        var low = P(1, downs: 1, gold: 2, coal: 4, repels: 2);
        Assign(ctx, low);
        Check(low.award == RailAwards.Fallback, "Every stat just below its minimum yields no title");
        var edge = P(1, gold: 3);
        Assign(ctx, edge);
        Check(edge.award == "Gold Digger", "Minimum is inclusive");

        // Conditional titles.
        var survivor = P(1, downs: 1, downSeconds: 8, escaped: true);
        var casualty = P(2, downs: 1, downSeconds: 30, escaped: false, offTrain: 40);
        Assign(ctx, survivor, casualty);
        Check(survivor.award == "Somehow Survived", "Somehow Survived needs an escape and a down");
        Check(casualty.award == "Missed the Train", "Non-escaper with off-train time is Missed the Train");

        var pacifist = P(1, escaped: true, tracked: 590, repels: 0);
        var fighter = P(2, escaped: true, tracked: 590, repels: 1);
        Assign(ctx, pacifist, fighter);
        Check(pacifist.award == "Pacifist", "Pacifist: escaped, whole run, no repels");
        Check(fighter.award == RailAwards.Fallback, "Any repel rules out Pacifist (below Personal Space Enforcer minimum)");
        var late = P(1, escaped: true, tracked: 100, repels: 0);
        Assign(ctx, late);
        Check(late.award == RailAwards.Fallback, "Pacifist requires being present for most of the run");

        var seat = P(1, escaped: true, tracked: 590, aboard: 560, repels: 5);
        Assign(ctx, seat);
        Check(seat.award == "Personal Space Enforcer", "Priority order: repels outrank Window Seat");
        var seat2 = P(1, escaped: true, tracked: 590, aboard: 400, repels: 1);
        Assign(ctx, seat2);
        Check(seat2.award != "Window Seat", "Window Seat needs 80% aboard");

        // Every title is unique and the honking titles are gone.
        var titles = RailAwards.Titles.ToList();
        Check(titles.Distinct().Count() == titles.Count, "Titles unique");
        Check(titles.Count == 21, "21 titles (20 awards + Precious Cargo)");
        Check(!titles.Contains("Public Nuisance") && !titles.Contains("Rescue Crew"), "Honk and old titles removed");

        // Large crew: no player ever has two titles or none.
        var crew = Enumerable.Range(1, 8).Select(i => P(i, revives: i, downs: 8 - i, coal: i * 2, gold: i, drops: i * 2, pickups: i * 3)).ToArray();
        Assign(ctx, crew);
        Check(crew.All(p => !string.IsNullOrEmpty(p.award)), "Everyone receives a title");

        // JSON round trip keeps awards (state is serialized through the room snapshot).
        var state = new RailRunState { run = "r", startAt = 1 };
        state.players.AddRange(crew);
        state.Finish(601, "Bridge crossed", crew.Select(p => p.id));
        var copy = JsonUtility.FromJson<RailRunState>(JsonUtility.ToJson(state));
        Check(copy.players.Select(p => p.award).SequenceEqual(state.players.Select(p => p.award)), "Awards survive snapshot serialization");
        Check(copy.players.All(p => !string.IsNullOrEmpty(p.award)), "Finish locks awards");

        // Stat report validation.
        var q = P(1);
        Check(RailStats.Apply(q, new object[] { 2, 3, 1, 150, 2.5f }) && q.drops == 2 && q.pickups == 3 && q.repels == 1 && q.spent == 150, "Stats report applies");
        Check(!RailStats.Apply(q, new object[] { 1, 2, 3 }), "Malformed stats report rejected");
        RailStats.Apply(q, new object[] { 9999, 0, 0, 0, 999f });
        Check(q.drops == 2 + RailStats.MaxCount && q.flashlightSeconds <= 2.5f + RailStats.MaxSeconds, "Stats report is clamped");

        Boundaries(ctx);

        Debug.Log(passed + " superlative checks passed. Network, headset and natural-run behaviour remain pending.");
    }

    // Every minimum: just below earns nothing, exactly at the minimum earns the title (inclusive).
    static void Boundaries(RailAwards.Context ctx)
    {
        void Edge(string title, Action<RailParticipant, bool> set)
        {
            var below = P(1, escaped: true); set(below, false);
            var at = P(1, escaped: true); set(at, true);
            Assign(ctx, below); Assign(ctx, at);
            Check(below.award == RailAwards.Fallback, title + ": just below the minimum gets no title (got " + below.award + ")");
            Check(at.award == title, title + ": exactly the minimum earns it (got " + at.award + ")");
        }
        Edge("Hero of the Day", (p, ok) => p.revives = ok ? 2 : 1);
        Edge("Certified Chicken", (p, ok) => p.downs = ok ? 2 : 1);
        Edge("Somehow Survived", (p, ok) => { p.downs = 1; p.downSeconds = ok ? 1f : .99f; });
        Edge("Damsel in Distress", (p, ok) => p.revived = ok ? 2 : 1);
        Edge("Personal Space Enforcer", (p, ok) => p.repels = ok ? 3 : 2);
        Edge("Gold Digger", (p, ok) => p.gold = ok ? 3 : 2);
        Edge("Boiler Room Employee", (p, ok) => p.coal = ok ? 5 : 4);
        Edge("Big Spender", (p, ok) => p.spent = ok ? 1 : 0);
        Edge("Designated Driver", (p, ok) => p.throttleSeconds = ok ? 30f : 29.99f);
        Edge("Bridge Troll", (p, ok) => p.bridgeSeconds = ok ? 10f : 9.99f);
        Edge("Butterfingers", (p, ok) => p.drops = ok ? 5 : 4);
        Edge("Loot Goblin", (p, ok) => p.pickups = ok ? 10 : 9);
        Edge("Night Light", (p, ok) => p.flashlightSeconds = ok ? 60f : 59.99f);
        Edge("Tourist", (p, ok) => p.maxFromTrain = ok ? 100f : 99.99f);
        Edge("Lone Wolf", (p, ok) => p.loneSeconds = ok ? 60f : 59.99f);
        Edge("Emotional Support Duck", (p, ok) => p.nearSeconds = ok ? 120f : 119.99f);
        Edge("Napping on the Job", (p, ok) => p.stillSeconds = ok ? 60f : 59.99f);
        // Presence is 80% of a 600 s run = 480 s; Window Seat also needs 80% of tracked time aboard.
        Edge("Pacifist", (p, ok) => p.trackedSeconds = ok ? 480f : 479.99f);
        Edge("Window Seat", (p, ok) => { p.trackedSeconds = 600f; p.aboardSeconds = ok ? 480f : 479.99f; p.repels = 1; });
        Edge("Missed the Train", (p, ok) => { p.escaped = false; p.offTrainSeconds = ok ? .01f : 0f; });
    }

    static void Assign(RailAwards.Context ctx, params RailParticipant[] players) => RailAwards.Assign(players, ctx);

    static RailParticipant P(int actor, int revives = 0, int downs = 0, int coal = 0, int gold = 0, int repels = 0, int drops = 0,
        int pickups = 0, bool escaped = false, float downSeconds = 0, float offTrain = 0, float tracked = 0, float aboard = 0) =>
        new RailParticipant { id = "p" + actor, actor = actor, revives = revives, downs = downs, coal = coal, gold = gold, repels = repels,
            drops = drops, pickups = pickups, escaped = escaped, downSeconds = downSeconds, offTrainSeconds = offTrain,
            trackedSeconds = tracked, aboardSeconds = aboard };

    static void Check(bool value, string message)
    {
        if (!value) throw new Exception("Superlative check failed: " + message);
        passed++;
    }
}
