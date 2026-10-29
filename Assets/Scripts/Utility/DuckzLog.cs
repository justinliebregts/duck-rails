using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary>Routine diagnostics require DUCKZ_DIAGNOSTICS, including in development builds.</summary>
public static class DuckzLog
{
    [Conditional("DUCKZ_DIAGNOSTICS")]
    public static void Log(object message, Object context = null,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        if (Allow(0, null, file, line, out Notice notice))
            notice.Write(message, context);
    }

    const double WarningIntervalSeconds = 10;
    const int MaxEntries = 512;
    sealed class Entry
    {
        public long Next;
        public long Suppressed;
    }
    static readonly Dictionary<(byte severity, string file, int line, string detail), Entry> Entries = new();
    // Never retain overflow keys, evict active entries, or scan the table under overload.
    // Separate buckets ensure warning/diagnostic traffic cannot suppress the error bucket.
    static readonly Entry[] Overflow = { new Entry(), new Entry(), new Entry() };

    /// <summary>
    /// Guard Notice.Write with this so rejected messages are never constructed.
    /// Use existing stable details (category, SKU, asset key), not formatted messages.
    /// At capacity, untracked events share one bucket per severity for this play session.
    /// The next allowed message includes its repeat count in the same console write.
    /// </summary>
    public static bool AllowWarning(out Notice notice, string detail = null,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        => Allow(1, detail, file, line, out notice);

    public static bool AllowError(out Notice notice, string detail = null,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        => Allow(2, detail, file, line, out notice);

    // A value carried directly to the write: no ambient/thread-static pending summary,
    // no closures, and no second lookup. Nested or concurrent logging cannot steal it.
    public readonly struct Notice
    {
        readonly bool allowed;
        readonly byte severity;
        readonly long suppressed;
        readonly bool overflow;

        internal Notice(byte severity, long suppressed, bool overflow)
        {
            allowed = true;
            this.severity = severity;
            this.suppressed = suppressed;
            this.overflow = overflow;
        }

        public void Write(object message, Object context = null)
        {
            if (!allowed) return;
            object output = message;
            if (overflow)
                output = $"{message} [Logging capacity reached; {suppressed} additional events suppressed; showing one untracked example.]";
            else if (suppressed > 0)
                output = $"{message} [Suppressed {suppressed} repeats.]";

            if (severity == 2) UnityEngine.Debug.LogError(output, context);
            else if (severity == 1) UnityEngine.Debug.LogWarning(output, context);
            else UnityEngine.Debug.Log(output, context);
        }
    }

    static bool Allow(byte severity, string detail, string file, int line, out Notice notice)
    {
        long now = Stopwatch.GetTimestamp();
        var key = (severity, file, line, detail);
        lock (Entries)
        {
            bool overflow = false;
            if (!Entries.TryGetValue(key, out Entry entry))
            {
                if (Entries.Count >= MaxEntries)
                {
                    entry = Overflow[severity];
                    overflow = true;
                }
                else
                {
                    entry = new Entry();
                    Entries.Add(key, entry);
                }
            }
            if (now < entry.Next)
            {
                if (entry.Suppressed < long.MaxValue) entry.Suppressed++;
                notice = default;
                return false;
            }
            notice = new Notice(severity, entry.Suppressed, overflow);
            entry.Suppressed = 0;
            entry.Next = now + (long)(WarningIntervalSeconds * Stopwatch.Frequency);
        }
        // Unity output is performed later by Notice.Write, outside this lock.
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetWarnings()
    {
        lock (Entries)
        {
            Entries.Clear();
            foreach (Entry entry in Overflow)
                entry.Next = entry.Suppressed = 0;
        }
    }
}