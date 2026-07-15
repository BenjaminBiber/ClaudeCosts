using System.Globalization;

namespace ClaudeCosts.Core.BigMac;

/// <summary>
/// Pure parser for The Economist's Big Mac index CSV
/// (<c>output-data/big-mac-full-index.csv</c> from the TheEconomist/big-mac-data repo).
/// Column layout: <c>date,iso_a3,currency_code,name,local_price,dollar_ex,dollar_price,…</c>.
/// </summary>
public static class BigMacIndexParser
{
    private const int DateCol = 0;
    private const int NameCol = 3;
    private const int DollarPriceCol = 6;

    /// <summary>
    /// Finds the most recent (latest-dated) row for <paramref name="countryName"/> and returns
    /// its <c>dollar_price</c> — the price of a Big Mac in that country expressed in US dollars.
    /// </summary>
    /// <returns><c>true</c> if a usable row was found; otherwise <c>false</c>.</returns>
    public static bool TryGetLatestPrice(string? csv, string countryName,
                                         out double dollarPrice, out string date)
    {
        dollarPrice = 0;
        date = "";
        if (string.IsNullOrWhiteSpace(csv)) return false;

        var found = false;
        foreach (var raw in csv.Split('\n'))
        {
            var line = raw.Trim('\r', ' ');
            if (line.Length == 0) continue;

            var cols = line.Split(',');
            if (cols.Length <= DollarPriceCol) continue;
            if (!string.Equals(cols[NameCol], countryName, StringComparison.OrdinalIgnoreCase)) continue;

            // ISO yyyy-MM-dd dates sort correctly as text, so an ordinal compare finds the latest.
            var rowDate = cols[DateCol];
            if (found && string.CompareOrdinal(rowDate, date) <= 0) continue;

            if (!double.TryParse(cols[DollarPriceCol], NumberStyles.Float, CultureInfo.InvariantCulture, out var price))
                continue;
            if (price <= 0) continue;

            dollarPrice = price;
            date = rowDate;
            found = true;
        }

        return found;
    }
}
