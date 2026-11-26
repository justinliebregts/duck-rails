using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Builds the end-of-run stats table shown on the RESULTS label: one row per duck, stat columns
/// aligned with TMP <c>&lt;pos&gt;</c> tags, and the top value in each stat column highlighted.
/// Pure string logic so it can be checked in EditMode.
/// </summary>
public static class RailResultsTable
{
    public const string Highlight = "#FFD84A";
    const string Escaped = "#8AFFBA", Lost = "#FF8866", Header = "#9A9A9A";

    // Column x positions as a percentage of the label width (left aligned within the label).
    const int NameAt = 20, TitleAt = 66, MaxName = 20;
    static readonly (string label, int at, Func<RailParticipant, int> value)[] Stats =
    {
        ("Coal", 40, p => p.coal),
        ("Gold", 46, p => p.gold),
        ("Revives", 52, p => p.revives),
        ("Downs", 59, p => p.downs),
    };

    public static string Build(IList<RailParticipant> players)
    {
        var best = Stats.Select(s => players.Count == 0 ? 0 : players.Max(s.value)).ToArray();
        var sb = new StringBuilder();
        sb.Append("<align=left><size=55%><color=").Append(Header).Append('>');
        sb.Append("<pos=").Append(NameAt).Append("%>Duck");
        foreach (var s in Stats) sb.Append("<pos=").Append(s.at).Append("%>").Append(s.label);
        sb.Append("<pos=").Append(TitleAt).Append("%>Title</color></size>");
        foreach (var p in players)
        {
            sb.Append("\n<size=75%><pos=").Append(NameAt).Append("%><color=").Append(p.escaped ? Escaped : Lost).Append('>')
                .Append(Name(p.name)).Append("</color>");
            for (int i = 0; i < Stats.Length; i++)
            {
                int v = Stats[i].value(p);
                sb.Append("<pos=").Append(Stats[i].at).Append("%>");
                if (v > 0 && v == best[i]) sb.Append("<color=").Append(Highlight).Append('>').Append(v).Append("</color>");
                else sb.Append(v);
            }
            sb.Append("</size><size=55%><pos=").Append(TitleAt).Append("%>")
                .Append(string.IsNullOrEmpty(p.award) ? RailAwards.Fallback : Safe(p.award)).Append("</size>");
        }
        return sb.Append("</align>").ToString();
    }

    /// <summary>Names longer than <see cref="MaxName"/> are cut to fit the name column ("..." included).</summary>
    public static string Name(string name)
    {
        string safe = Safe(name).Trim();
        return safe.Length <= MaxName ? safe : safe.Substring(0, MaxName - 3).TrimEnd() + "...";
    }

    static string Safe(string text) => (text ?? "Duck").Replace("<", "").Replace(">", "");
}
