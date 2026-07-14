namespace ClaudeCosts.Core.Aggregation;

/// <summary>Per-model usage totals within a bucket (or across all data).</summary>
public sealed record ModelUsage(
    string Model,
    long InputTokens,
    long OutputTokens,
    long CacheCreationTokens,
    long CacheReadTokens,
    double Cost,
    bool KnownPricing)
{
    public long TotalTokens => InputTokens + OutputTokens + CacheCreationTokens + CacheReadTokens;
}

/// <summary>
/// Aggregated usage for one period (a day, week, month, or the whole dataset).
/// </summary>
public sealed record UsageBucket(
    Granularity Granularity,
    DateOnly? PeriodStart,
    string Label,
    long InputTokens,
    long OutputTokens,
    long CacheCreationTokens,
    long CacheReadTokens,
    double Cost,
    IReadOnlyList<ModelUsage> Models)
{
    public long TotalTokens => InputTokens + OutputTokens + CacheCreationTokens + CacheReadTokens;

    /// <summary>True when at least one entry contributed a model without a known price.</summary>
    public bool HasUnknownPricing => Models.Any(m => !m.KnownPricing);
}
