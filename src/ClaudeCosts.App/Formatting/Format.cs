using System.Globalization;
using ClaudeCosts.Core.Aggregation;

namespace ClaudeCosts.App.Formatting;

/// <summary>Display formatting for money, tokens, tray text, and period labels (German).</summary>
public static class Format
{
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Full money string, e.g. <c>$1,234.56</c>.</summary>
    public static string Money(double value) => "$" + value.ToString("N2", Inv);

    /// <summary>Compact token count, e.g. <c>1.2M</c>, <c>34.5k</c>.</summary>
    public static string Tokens(long t)
    {
        if (t >= 1_000_000_000) return (t / 1e9).ToString("0.##", Inv) + "B";
        if (t >= 1_000_000) return (t / 1e6).ToString("0.##", Inv) + "M";
        if (t >= 1_000) return (t / 1e3).ToString("0.#", Inv) + "k";
        return t.ToString(Inv);
    }

    /// <summary>Short string rendered onto the tray icon (kept to ~4 glyphs).</summary>
    public static string TrayText(double cost)
    {
        if (cost >= 100_000) return $"${cost / 1000:0}k";
        if (cost >= 10_000) return $"${cost / 1000:0}k";
        if (cost >= 1_000) return $"${cost / 1000:0.0}k";
        if (cost >= 10) return $"${cost:0}";
        return $"${cost:0.0}";
    }

    /// <summary>German period label for a bucket, e.g. "Juli 2026", "KW 29 · 13.–19.07.2026", "Mo. 14.07.2026", "Gesamt".</summary>
    public static string PeriodLabel(UsageBucket bucket)
    {
        if (bucket.Granularity == Granularity.All || bucket.PeriodStart is not { } start)
            return "Gesamt";

        var startDt = start.ToDateTime(TimeOnly.MinValue);
        return bucket.Granularity switch
        {
            Granularity.Day => startDt.ToString("ddd dd.MM.yyyy", De),
            Granularity.Month => startDt.ToString("MMMM yyyy", De),
            Granularity.Week => $"KW {ISOWeek.GetWeekOfYear(startDt)} · "
                                + $"{startDt:dd.}–{startDt.AddDays(6):dd.MM.yyyy}",
            _ => start.ToString("yyyy-MM-dd", Inv),
        };
    }

    public static string PeriodTypeName(Granularity g) => g switch
    {
        Granularity.Day => "Tag",
        Granularity.Week => "Woche",
        Granularity.Month => "Monat",
        Granularity.All => "Gesamt",
        _ => "",
    };
}
