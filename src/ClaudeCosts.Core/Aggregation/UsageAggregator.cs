using System.Globalization;
using ClaudeCosts.Core.Costs;
using ClaudeCosts.Core.Models;

namespace ClaudeCosts.Core.Aggregation;

/// <summary>
/// Buckets usage entries by <see cref="Granularity"/> in a given time zone and
/// computes cost per bucket and per model. Entries are expected to be already
/// de-duplicated (see <see cref="UsageDeduplicator"/>).
/// </summary>
public sealed class UsageAggregator
{
    private readonly CostCalculator _calc;

    public UsageAggregator(CostCalculator calc) => _calc = calc;

    /// <summary>Aggregates <paramref name="entries"/> into buckets, most-recent first.</summary>
    public IReadOnlyList<UsageBucket> Aggregate(
        IEnumerable<UsageEntry> entries,
        Granularity granularity,
        bool weekStartsMonday = true,
        TimeZoneInfo? timeZone = null)
    {
        timeZone ??= TimeZoneInfo.Local;
        var buckets = new Dictionary<DateOnly, Accumulator>();

        foreach (var e in entries)
        {
            var localDate = LocalDateOf(e.TimestampUtc, timeZone);
            var key = PeriodStartOf(localDate, granularity, weekStartsMonday);

            if (!buckets.TryGetValue(key, out var acc))
                buckets[key] = acc = new Accumulator();

            acc.Add(e, _calc);
        }

        return buckets
            .OrderByDescending(kvp => kvp.Key)
            .Select(kvp => Build(granularity, kvp.Key, kvp.Value))
            .ToList();
    }

    /// <summary>
    /// Returns the bucket for the period that contains <paramref name="now"/>
    /// (default: current time). If there is no usage in that period, an empty
    /// zero-cost bucket for the period is returned — so the tray shows the true
    /// current-period value, not the last active one.
    /// </summary>
    public UsageBucket CurrentPeriod(
        IEnumerable<UsageEntry> entries,
        Granularity granularity,
        bool weekStartsMonday = true,
        TimeZoneInfo? timeZone = null,
        DateTimeOffset? now = null)
    {
        timeZone ??= TimeZoneInfo.Local;
        var nowLocalDate = LocalDateOf(now ?? DateTimeOffset.UtcNow, timeZone);
        var key = PeriodStartOf(nowLocalDate, granularity, weekStartsMonday);

        var all = Aggregate(entries, granularity, weekStartsMonday, timeZone);
        var match = all.FirstOrDefault(b => (b.PeriodStart ?? DateOnly.MinValue) == key);

        return match ?? Build(granularity, key, new Accumulator());
    }

    private static DateOnly LocalDateOf(DateTimeOffset utc, TimeZoneInfo tz)
    {
        var local = TimeZoneInfo.ConvertTime(utc, tz);
        return DateOnly.FromDateTime(local.DateTime);
    }

    /// <summary>Start-of-period date for a given local date and granularity.</summary>
    public static DateOnly PeriodStartOf(DateOnly date, Granularity g, bool weekStartsMonday) => g switch
    {
        Granularity.Day => date,
        Granularity.Week => StartOfWeek(date, weekStartsMonday),
        Granularity.Month => new DateOnly(date.Year, date.Month, 1),
        Granularity.All => DateOnly.MinValue,
        _ => date,
    };

    private static DateOnly StartOfWeek(DateOnly date, bool weekStartsMonday)
    {
        int start = weekStartsMonday ? (int)DayOfWeek.Monday : (int)DayOfWeek.Sunday;
        int diff = ((int)date.DayOfWeek - start + 7) % 7;
        return date.AddDays(-diff);
    }

    private static UsageBucket Build(Granularity g, DateOnly key, Accumulator acc)
    {
        var models = acc.Models.Values
            .Select(m => new ModelUsage(m.Model, m.In, m.Out, m.Cache, m.Read, m.Cost, m.Known))
            .OrderByDescending(m => m.Cost)
            .ThenByDescending(m => m.TotalTokens)
            .ToList();

        DateOnly? periodStart = g == Granularity.All ? null : key;

        return new UsageBucket(
            g,
            periodStart,
            LabelFor(g, key),
            acc.In,
            acc.Out,
            acc.Cache,
            acc.Read,
            acc.Cost,
            models);
    }

    private static string LabelFor(Granularity g, DateOnly key) => g switch
    {
        Granularity.Day => key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Granularity.Week => key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Granularity.Month => key.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        Granularity.All => "Gesamt",
        _ => key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    };

    private sealed class Accumulator
    {
        public long In, Out, Cache, Read;
        public double Cost;
        public readonly Dictionary<string, ModelAccumulator> Models = new(StringComparer.Ordinal);

        public void Add(UsageEntry e, CostCalculator calc)
        {
            double cost = calc.CostOf(e);
            long cache = e.CacheCreationTokens;

            In += e.InputTokens;
            Out += e.OutputTokens;
            Cache += cache;
            Read += e.CacheReadTokens;
            Cost += cost;

            if (!Models.TryGetValue(e.Model, out var m))
                Models[e.Model] = m = new ModelAccumulator { Model = e.Model, Known = calc.IsKnownModel(e.Model) };

            m.In += e.InputTokens;
            m.Out += e.OutputTokens;
            m.Cache += cache;
            m.Read += e.CacheReadTokens;
            m.Cost += cost;
        }
    }

    private sealed class ModelAccumulator
    {
        public string Model = "";
        public long In, Out, Cache, Read;
        public double Cost;
        public bool Known;
    }
}
