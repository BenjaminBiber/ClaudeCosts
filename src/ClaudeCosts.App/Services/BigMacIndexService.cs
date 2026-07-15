using System.IO;
using System.Net.Http;
using ClaudeCosts.Core.BigMac;

namespace ClaudeCosts.App.Services;

/// <summary>
/// Supplies the current price of a Big Mac in US dollars (Euro-area reference), used to
/// express dollar costs as a number of Big Macs. Sourced live from The Economist's public
/// dataset (TheEconomist/big-mac-data), cached on disk, with an offline fallback.
/// </summary>
public sealed class BigMacIndexService
{
    private const string Url =
        "https://raw.githubusercontent.com/TheEconomist/big-mac-data/master/output-data/big-mac-full-index.csv";

    /// <summary>Reference country whose <c>dollar_price</c> is used as the divisor.</summary>
    private const string Country = "Euro area";

    /// <summary>Used when neither a cache nor the network is available.
    /// Source: TheEconomist/big-mac-data, "Euro area" dollar_price, 2026-01-01.</summary>
    private const double FallbackUsd = 7.05;

    private static readonly string CachePath = Path.Combine(SettingsService.AppDataDir, "big-mac-index.csv");

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public BigMacIndexService()
    {
        CurrentPriceUsd = FallbackUsd;
        TryLoadFromCache();
    }

    /// <summary>Current USD price of one (Euro-area) Big Mac. Always positive.</summary>
    public double CurrentPriceUsd { get; private set; }

    /// <summary>Raised (on the calling thread) after <see cref="RefreshAsync"/> changes the price.</summary>
    public event EventHandler? Updated;

    private void TryLoadFromCache()
    {
        try
        {
            if (File.Exists(CachePath) &&
                BigMacIndexParser.TryGetLatestPrice(File.ReadAllText(CachePath), Country, out var price, out _))
            {
                CurrentPriceUsd = price;
            }
        }
        catch
        {
            // corrupt/unreadable cache → keep the fallback value
        }
    }

    /// <summary>
    /// Downloads the latest index, updates <see cref="CurrentPriceUsd"/>, refreshes the on-disk
    /// cache and raises <see cref="Updated"/> if the price changed. Never throws — network and
    /// IO failures leave the current (cached or fallback) value in place. Call from the UI thread
    /// so the continuation, and thus <see cref="Updated"/>, is raised there.
    /// </summary>
    public async Task RefreshAsync()
    {
        string csv;
        try
        {
            csv = await Http.GetStringAsync(Url);
        }
        catch
        {
            return; // offline / transient failure — keep cached or fallback value
        }

        if (!BigMacIndexParser.TryGetLatestPrice(csv, Country, out var price, out _))
            return;

        try
        {
            Directory.CreateDirectory(SettingsService.AppDataDir);
            File.WriteAllText(CachePath, csv);
        }
        catch
        {
            // caching is best-effort
        }

        var changed = Math.Abs(price - CurrentPriceUsd) > 1e-9;
        CurrentPriceUsd = price;
        if (changed)
            Updated?.Invoke(this, EventArgs.Empty);
    }
}
