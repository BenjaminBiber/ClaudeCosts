using ClaudeCosts.Core;
using ClaudeCosts.Core.Aggregation;
using ClaudeCosts.Core.Costs;
using ClaudeCosts.Core.Models;
using ClaudeCosts.Core.Pricing;

namespace ClaudeCosts.App.Services;

/// <summary>
/// Loads usage in the background and exposes aggregation helpers. <see cref="Updated"/>
/// fires on the thread that awaited <see cref="ReloadAsync"/> (call it from the UI thread).
/// </summary>
public sealed class UsageService
{
    private readonly UsageAggregator _aggregator;

    public UsageService(PricingTable pricing)
    {
        _aggregator = new UsageAggregator(new CostCalculator(pricing));
    }

    public IReadOnlyList<UsageEntry> Entries { get; private set; } = Array.Empty<UsageEntry>();

    public DateTimeOffset? LastLoadedUtc { get; private set; }

    public bool HasData => Entries.Count > 0;

    public event EventHandler? Updated;

    public async Task ReloadAsync()
    {
        var entries = await Task.Run(UsageLoader.LoadAll).ConfigureAwait(true);
        Entries = entries;
        LastLoadedUtc = DateTimeOffset.UtcNow;
        Updated?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<UsageBucket> Aggregate(Granularity granularity, bool weekStartsMonday) =>
        _aggregator.Aggregate(Entries, granularity, weekStartsMonday);

    public UsageBucket CurrentPeriod(Granularity granularity, bool weekStartsMonday) =>
        _aggregator.CurrentPeriod(Entries, granularity, weekStartsMonday);
}
