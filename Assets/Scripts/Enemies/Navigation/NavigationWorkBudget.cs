using System.Diagnostics;
using UnityEngine;

namespace Game.AI
{
    // Both lanes charge actual elapsed work against one frame allowance. Individual
    // native queries cannot be interrupted; an overrun consumes the remaining allowance.
    public static class NavigationWorkBudget
    {
        static int frame = -1;
        static long spent;
        static long duckSpent, enemySpent;
        public static int PendingDuckPlans { get; private set; }
        public static double SpentMilliseconds => spent * 1000d / Stopwatch.Frequency;
        public static void RegisterDuck() => PendingDuckPlans++;
        public static void UnregisterDuck() => PendingDuckPlans = Mathf.Max(0, PendingDuckPlans - 1);
        public static bool TryBegin(float milliseconds, bool share, out long started, out long deadline, bool duck = false)
        {
            if (frame != Time.frameCount) { frame = Time.frameCount; spent = duckSpent = enemySpent = 0; }
            started = Stopwatch.GetTimestamp();
            long total = (long)(Mathf.Max(0.1f, milliseconds) * Stopwatch.Frequency / 1000d);
            long available = total - spent;
            if (share) available = System.Math.Min(available, total / 2 - (duck ? duckSpent : enemySpent));
            deadline = started + System.Math.Max(0, available);
            return available > 0;
        }
        public static void End(long started, bool duck = false)
        {
            long elapsed = Stopwatch.GetTimestamp() - started;
            spent += elapsed;
            if (duck) duckSpent += elapsed; else enemySpent += elapsed;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { frame = -1; spent = duckSpent = enemySpent = 0; PendingDuckPlans = 0; }
    }
}
