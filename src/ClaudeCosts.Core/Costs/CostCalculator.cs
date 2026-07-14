using ClaudeCosts.Core.Models;
using ClaudeCosts.Core.Pricing;

namespace ClaudeCosts.Core.Costs;

/// <summary>
/// Computes the USD API-list-price equivalent of a usage record from token
/// counts and the model's <see cref="ModelPricing"/>.
/// </summary>
public sealed class CostCalculator
{
    private readonly PricingTable _pricing;

    public CostCalculator(PricingTable pricing) => _pricing = pricing;

    /// <summary>Cost of a single entry. Unknown models contribute 0.</summary>
    public double CostOf(UsageEntry e)
    {
        var p = _pricing.Resolve(e.Model);
        if (p is null) return 0d;

        double inRate = p.InputPerMTok / 1_000_000d;
        double outRate = p.OutputPerMTok / 1_000_000d;

        return e.InputTokens * inRate
             + e.OutputTokens * outRate
             + e.CacheCreation5m * inRate * p.CacheWrite5mMultiplier
             + e.CacheCreation1h * inRate * p.CacheWrite1hMultiplier
             + e.CacheReadTokens * inRate * p.CacheReadMultiplier;
    }

    /// <summary>True when the model has a pricing rule (so its cost is meaningful).</summary>
    public bool IsKnownModel(string? model) => _pricing.Resolve(model) is not null;
}
