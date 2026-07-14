namespace ClaudeCosts.Core.Pricing;

/// <summary>
/// Per-model prices in USD per 1,000,000 tokens, plus the cache multipliers
/// applied relative to the input rate. The cache multipliers are the standard
/// Anthropic values (5-minute write 1.25×, 1-hour write 2×, read 0.1×) and are
/// rarely overridden per model.
/// </summary>
public sealed record ModelPricing
{
    public required double InputPerMTok { get; init; }
    public required double OutputPerMTok { get; init; }

    public double CacheWrite5mMultiplier { get; init; } = 1.25;
    public double CacheWrite1hMultiplier { get; init; } = 2.0;
    public double CacheReadMultiplier { get; init; } = 0.10;
}

/// <summary>
/// Maps a model-id prefix to a <see cref="ModelPricing"/>. Longer prefixes are
/// matched first, so a specific id (<c>claude-haiku-4-5</c>) beats a broader
/// family prefix (<c>claude-haiku-4</c>).
/// </summary>
public sealed record PricingRule(string Prefix, ModelPricing Pricing);
