using ClaudeCosts.Core.Pricing;

namespace ClaudeCosts.Core.Tests;

public class PricingTests
{
    private readonly PricingTable _table = PricingTable.Defaults();

    [Theory]
    [InlineData("claude-opus-4-8", 5, 25)]
    [InlineData("claude-opus-4-7", 5, 25)]
    [InlineData("claude-sonnet-5", 3, 15)]
    [InlineData("claude-sonnet-4-6", 3, 15)]
    [InlineData("claude-haiku-4-5-20251001", 1, 5)]
    [InlineData("claude-fable-5", 10, 50)]
    public void Resolves_current_models_by_prefix(string model, double input, double output)
    {
        var pricing = _table.Resolve(model);

        Assert.NotNull(pricing);
        Assert.Equal(input, pricing!.InputPerMTok);
        Assert.Equal(output, pricing.OutputPerMTok);
    }

    [Fact]
    public void Unknown_model_resolves_to_null()
    {
        Assert.Null(_table.Resolve("gpt-4o"));
        Assert.Null(_table.Resolve(""));
        Assert.Null(_table.Resolve(null));
    }

    [Fact]
    public void Default_cache_multipliers_are_anthropic_standard()
    {
        var pricing = _table.Resolve("claude-opus-4-8")!;

        Assert.Equal(1.25, pricing.CacheWrite5mMultiplier);
        Assert.Equal(2.0, pricing.CacheWrite1hMultiplier);
        Assert.Equal(0.10, pricing.CacheReadMultiplier);
    }

    [Fact]
    public void Roundtrips_through_json_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pricing-{Guid.NewGuid():N}.json");
        try
        {
            _table.Save(path);
            var reloaded = PricingTable.LoadOrCreate(path);

            var pricing = reloaded.Resolve("claude-opus-4-8");
            Assert.NotNull(pricing);
            Assert.Equal(5, pricing!.InputPerMTok);
            Assert.Equal(25, pricing.OutputPerMTok);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadOrCreate_writes_defaults_when_file_missing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pricing-{Guid.NewGuid():N}.json");
        try
        {
            Assert.False(File.Exists(path));
            var table = PricingTable.LoadOrCreate(path);

            Assert.True(File.Exists(path));
            Assert.NotNull(table.Resolve("claude-opus-4-8"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
