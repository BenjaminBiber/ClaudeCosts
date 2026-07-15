using ClaudeCosts.Core.BigMac;

namespace ClaudeCosts.Core.Tests;

public class BigMacIndexParserTests
{
    // Header + two dates, unordered, so the "latest date wins" logic is actually exercised.
    private const string Csv =
        "date,iso_a3,currency_code,name,local_price,dollar_ex,dollar_price,USD_raw\n" +
        "2025-01-01,EUZ,EUR,Euro area,5.95,0.95,6.26,0.1\n" +
        "2026-01-01,EUZ,EUR,Euro area,6.08,0.86192,7.05401893447188,0.1\n" +
        "2026-01-01,USA,USD,United States,6.12,1,6.12,0\n" +
        "2025-01-01,USA,USD,United States,5.79,1,5.79,0\n";

    [Fact]
    public void Returns_latest_dated_price_for_country()
    {
        var ok = BigMacIndexParser.TryGetLatestPrice(Csv, "Euro area", out var price, out var date);

        Assert.True(ok);
        Assert.Equal("2026-01-01", date);
        Assert.Equal(7.054, price, 3);
    }

    [Fact]
    public void Picks_the_requested_country_independently()
    {
        var ok = BigMacIndexParser.TryGetLatestPrice(Csv, "United States", out var price, out var date);

        Assert.True(ok);
        Assert.Equal("2026-01-01", date);
        Assert.Equal(6.12, price, 2);
    }

    [Fact]
    public void Country_match_is_case_insensitive()
    {
        Assert.True(BigMacIndexParser.TryGetLatestPrice(Csv, "euro area", out var price, out _));
        Assert.Equal(7.054, price, 3);
    }

    [Fact]
    public void Unknown_country_returns_false()
    {
        var ok = BigMacIndexParser.TryGetLatestPrice(Csv, "Atlantis", out var price, out _);

        Assert.False(ok);
        Assert.Equal(0, price);
    }

    [Fact]
    public void Null_or_empty_input_returns_false()
    {
        Assert.False(BigMacIndexParser.TryGetLatestPrice(null, "Euro area", out _, out _));
        Assert.False(BigMacIndexParser.TryGetLatestPrice("", "Euro area", out _, out _));
        Assert.False(BigMacIndexParser.TryGetLatestPrice("   ", "Euro area", out _, out _));
    }
}
