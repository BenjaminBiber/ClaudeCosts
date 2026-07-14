using ClaudeCosts.Core.Aggregation;
using ClaudeCosts.Core.Costs;
using ClaudeCosts.Core.Models;
using ClaudeCosts.Core.Pricing;

namespace ClaudeCosts.Core.Tests;

public class UsageAggregatorTests
{
    private readonly UsageAggregator _agg = new(new CostCalculator(PricingTable.Defaults()));

    private static UsageEntry Entry(string model, DateTimeOffset ts, long input) => new()
    {
        TimestampUtc = ts,
        Model = model,
        InputTokens = input,
    };

    // Two July entries ($5 opus + $1 haiku) and one June entry ($5 opus).
    private static UsageEntry[] Sample() =>
    [
        Entry("claude-opus-4-8",  new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero), 1_000_000),
        Entry("claude-haiku-4-5", new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero), 1_000_000),
        Entry("claude-opus-4-8",  new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero), 1_000_000),
    ];

    [Fact]
    public void Month_buckets_are_sorted_desc_with_correct_totals()
    {
        var buckets = _agg.Aggregate(Sample(), Granularity.Month, timeZone: TimeZoneInfo.Utc);

        Assert.Equal(2, buckets.Count);

        Assert.Equal(new DateOnly(2026, 7, 1), buckets[0].PeriodStart);
        Assert.Equal(6d, buckets[0].Cost, 6);

        Assert.Equal(new DateOnly(2026, 6, 1), buckets[1].PeriodStart);
        Assert.Equal(5d, buckets[1].Cost, 6);
    }

    [Fact]
    public void Model_breakdown_is_ordered_by_cost_desc()
    {
        var july = _agg.Aggregate(Sample(), Granularity.Month, timeZone: TimeZoneInfo.Utc)[0];

        Assert.Equal(2, july.Models.Count);
        Assert.Equal("claude-opus-4-8", july.Models[0].Model);
        Assert.Equal(5d, july.Models[0].Cost, 6);
        Assert.Equal("claude-haiku-4-5", july.Models[1].Model);
        Assert.Equal(1d, july.Models[1].Cost, 6);
        Assert.All(july.Models, m => Assert.True(m.KnownPricing));
    }

    [Fact]
    public void CurrentPeriod_returns_month_containing_now()
    {
        var now = new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero);

        var bucket = _agg.CurrentPeriod(Sample(), Granularity.Month, timeZone: TimeZoneInfo.Utc, now: now);

        Assert.Equal(new DateOnly(2026, 7, 1), bucket.PeriodStart);
        Assert.Equal(6d, bucket.Cost, 6);
    }

    [Fact]
    public void CurrentPeriod_is_zero_when_no_usage_in_current_period()
    {
        var now = new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero);

        var bucket = _agg.CurrentPeriod(Sample(), Granularity.Month, timeZone: TimeZoneInfo.Utc, now: now);

        Assert.Equal(new DateOnly(2026, 8, 1), bucket.PeriodStart);
        Assert.Equal(0d, bucket.Cost);
        Assert.Empty(bucket.Models);
    }

    [Fact]
    public void Day_granularity_isolates_single_day()
    {
        var now = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);

        var bucket = _agg.CurrentPeriod(Sample(), Granularity.Day, timeZone: TimeZoneInfo.Utc, now: now);

        Assert.Equal(new DateOnly(2026, 7, 20), bucket.PeriodStart);
        Assert.Equal(1d, bucket.Cost, 6); // only the haiku entry
    }

    [Fact]
    public void All_granularity_is_single_bucket()
    {
        var buckets = _agg.Aggregate(Sample(), Granularity.All, timeZone: TimeZoneInfo.Utc);

        Assert.Single(buckets);
        Assert.Null(buckets[0].PeriodStart);
        Assert.Equal("Gesamt", buckets[0].Label);
        Assert.Equal(11d, buckets[0].Cost, 6);
    }

    [Theory]
    [InlineData("2026-07-15", true, "2026-07-13")]  // Wed -> Monday
    [InlineData("2026-07-15", false, "2026-07-12")] // Wed -> Sunday
    [InlineData("2026-07-13", true, "2026-07-13")]  // Monday stays
    public void Week_start_respects_setting(string date, bool mondayStart, string expected)
    {
        var d = DateOnly.Parse(date);
        var start = UsageAggregator.PeriodStartOf(d, Granularity.Week, mondayStart);
        Assert.Equal(DateOnly.Parse(expected), start);
    }
}
