using ClaudeCosts.Core.Costs;
using ClaudeCosts.Core.Models;
using ClaudeCosts.Core.Pricing;

namespace ClaudeCosts.Core.Tests;

public class CostCalculatorTests
{
    private readonly CostCalculator _calc = new(PricingTable.Defaults());

    [Fact]
    public void Computes_all_token_categories_including_cache()
    {
        // Opus 4.8: input $5 / output $25 per 1M tokens.
        // 1M of each category:
        //   input  1M * 5e-6           = 5.00
        //   output 1M * 25e-6          = 25.00
        //   cache5m 1M * 5e-6 * 1.25   = 6.25
        //   cache1h 1M * 5e-6 * 2.00   = 10.00
        //   read    1M * 5e-6 * 0.10   = 0.50
        //                        total = 46.75
        var entry = new UsageEntry
        {
            TimestampUtc = DateTimeOffset.UnixEpoch,
            Model = "claude-opus-4-8",
            InputTokens = 1_000_000,
            OutputTokens = 1_000_000,
            CacheCreation5m = 1_000_000,
            CacheCreation1h = 1_000_000,
            CacheReadTokens = 1_000_000,
        };

        Assert.Equal(46.75, _calc.CostOf(entry), 6);
    }

    [Fact]
    public void Unknown_model_costs_zero_and_is_not_known()
    {
        var entry = new UsageEntry
        {
            TimestampUtc = DateTimeOffset.UnixEpoch,
            Model = "some-unknown-model",
            InputTokens = 1_000_000,
            OutputTokens = 1_000_000,
        };

        Assert.Equal(0d, _calc.CostOf(entry));
        Assert.False(_calc.IsKnownModel("some-unknown-model"));
        Assert.True(_calc.IsKnownModel("claude-opus-4-8"));
    }

    [Fact]
    public void Haiku_cheaper_than_opus_for_same_tokens()
    {
        UsageEntry Make(string model) => new()
        {
            TimestampUtc = DateTimeOffset.UnixEpoch,
            Model = model,
            InputTokens = 500_000,
            OutputTokens = 500_000,
        };

        Assert.True(_calc.CostOf(Make("claude-haiku-4-5")) < _calc.CostOf(Make("claude-opus-4-8")));
    }
}
