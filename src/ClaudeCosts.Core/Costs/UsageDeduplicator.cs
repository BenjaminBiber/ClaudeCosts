using ClaudeCosts.Core.Models;

namespace ClaudeCosts.Core.Costs;

/// <summary>
/// De-duplicates usage entries. A streamed turn is written to several JSONL
/// lines that share one <c>(message.id, requestId)</c>: input and cache counts
/// stay constant while <c>output_tokens</c> grows toward the final total. We
/// therefore keep, per key, the record with the largest output — the completed
/// turn — matching ccusage's totals. Records missing either id are always kept.
/// </summary>
public static class UsageDeduplicator
{
    public static IReadOnlyList<UsageEntry> Distinct(IEnumerable<UsageEntry> entries)
    {
        var bestByKey = new Dictionary<string, UsageEntry>(StringComparer.Ordinal);
        var unkeyed = new List<UsageEntry>();

        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.MessageId) || string.IsNullOrEmpty(entry.RequestId))
            {
                unkeyed.Add(entry);
                continue;
            }

            var key = entry.MessageId + ":" + entry.RequestId;
            if (!bestByKey.TryGetValue(key, out var current) || entry.OutputTokens > current.OutputTokens)
                bestByKey[key] = entry;
        }

        var result = new List<UsageEntry>(bestByKey.Count + unkeyed.Count);
        result.AddRange(bestByKey.Values);
        result.AddRange(unkeyed);
        return result;
    }
}
