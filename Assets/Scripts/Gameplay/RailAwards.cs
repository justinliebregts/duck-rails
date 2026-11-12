using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// End-of-run superlatives. Pure logic over <see cref="RailParticipant"/> counters so it can be
/// checked in EditMode. Awards are processed in priority order; each goes to the eligible player
/// with the highest value who has no title yet. Ties: escaped first, then lower actor number.
/// Anyone left over receives <see cref="Fallback"/>. See docs/game-design.md (Superlatives).
/// </summary>
public static class RailAwards
{
    public const string Fallback = "Precious Cargo";

    public sealed class Context
    {
        public float RunSeconds;
    }

    sealed class Award
    {
        public string Title;
        public Func<RailParticipant, float> Value;
        public float Min;
        public Func<RailParticipant, Context, bool> Eligible; // optional extra requirement
    }

    static Award A(string title, Func<RailParticipant, float> value, float min, Func<RailParticipant, Context, bool> eligible = null) =>
        new Award { Title = title, Value = value, Min = min, Eligible = eligible };

    // A player counts as "present for the whole run" when sampled for at least 80% of it.
    // Written as tracked/run >= 4/5 with integer multiples so the boundary is exact (0.8f is not representable).
    static bool Present(RailParticipant p, Context c) => c.RunSeconds > 0 && p.trackedSeconds * 5f >= c.RunSeconds * 4f;

    // Priority order: dramatic and rare titles first so common-stat titles do not absorb them.
    static readonly Award[] Awards =
    {
        A("Hero of the Day", p => p.revives, 2),
        A("Certified Chicken", p => p.downs, 2),
        A("Somehow Survived", p => p.downSeconds, 1, (p, c) => p.escaped && p.downs >= 1),
        A("Damsel in Distress", p => p.revived, 2),
        A("Personal Space Enforcer", p => p.repels, 3),
        A("Gold Digger", p => p.gold, 3),
        A("Boiler Room Employee", p => p.coal, 5),
        A("Big Spender", p => p.spent, 1),
        A("Designated Driver", p => p.throttleSeconds, 30),
        A("Bridge Troll", p => p.bridgeSeconds, 10),
        A("Butterfingers", p => p.drops, 5),
        A("Loot Goblin", p => p.pickups, 10),
        A("Missed the Train", p => p.offTrainSeconds, 0, (p, c) => !p.escaped),
        A("Night Light", p => p.flashlightSeconds, 60),
        A("Tourist", p => p.maxFromTrain, 100),
        A("Lone Wolf", p => p.loneSeconds, 60),
        A("Emotional Support Duck", p => p.nearSeconds, 120),
        A("Napping on the Job", p => p.stillSeconds, 60),
        A("Window Seat", p => p.aboardSeconds, 0, (p, c) => Present(p, c) && p.aboardSeconds * 5f >= p.trackedSeconds * 4f),
        A("Pacifist", p => p.trackedSeconds, 0, (p, c) => p.escaped && p.repels == 0 && Present(p, c)),
    };

    public static IEnumerable<string> Titles => Awards.Select(a => a.Title).Append(Fallback);

    /// <summary>Assigns one title to every participant. Safe to call once at the finish; result is stored on each player.</summary>
    public static void Assign(IList<RailParticipant> players, Context context)
    {
        foreach (var p in players) p.award = "";
        foreach (var award in Awards)
        {
            var winner = players
                .Where(p => string.IsNullOrEmpty(p.award) && award.Value(p) >= award.Min && award.Value(p) > 0 &&
                    (award.Eligible == null || award.Eligible(p, context)))
                .OrderByDescending(award.Value).ThenByDescending(p => p.escaped).ThenBy(p => p.actor)
                .FirstOrDefault();
            if (winner != null) winner.award = award.Title;
        }
        foreach (var p in players) if (string.IsNullOrEmpty(p.award)) p.award = Fallback;
    }
}
