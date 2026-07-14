using ClaudeCosts.Core.Costs;
using ClaudeCosts.Core.Discovery;
using ClaudeCosts.Core.Models;
using ClaudeCosts.Core.Parsing;

namespace ClaudeCosts.Core;

/// <summary>
/// End-to-end loading: discover transcripts, parse usage records, and
/// de-duplicate them into a single list ready for aggregation.
/// </summary>
public static class UsageLoader
{
    /// <summary>Loads and de-duplicates usage from every discovered transcript.</summary>
    public static List<UsageEntry> LoadAll() =>
        LoadFrom(ClaudeDataLocator.EnumerateTranscripts());

    /// <summary>Loads and de-duplicates usage from the given transcript files.</summary>
    public static List<UsageEntry> LoadFrom(IEnumerable<string> files)
    {
        var parsed = files.SelectMany(JsonlUsageReader.ReadFile);
        return UsageDeduplicator.Distinct(parsed).ToList();
    }
}
