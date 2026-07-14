using System.Globalization;
using System.Text.Json;
using ClaudeCosts.Core.Models;

namespace ClaudeCosts.Core.Parsing;

/// <summary>
/// Streams a Claude Code <c>*.jsonl</c> transcript and yields one
/// <see cref="UsageEntry"/> per assistant line that carries token usage.
/// Non-assistant lines, lines without <c>message.usage</c>, <c>&lt;synthetic&gt;</c>
/// models, and malformed lines are skipped.
/// </summary>
public static class JsonlUsageReader
{
    private const string SyntheticModel = "<synthetic>";

    /// <summary>Reads a transcript file. Opens shared so a file being appended to can still be read.</summary>
    public static IEnumerable<UsageEntry> ReadFile(string path)
    {
        FileStream stream;
        try
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }
        catch
        {
            yield break; // file vanished / locked exclusively — skip it
        }

        using (stream)
        using (var reader = new StreamReader(stream))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0) continue;

                UsageEntry? entry;
                try
                {
                    entry = ParseLine(line);
                }
                catch
                {
                    entry = null; // malformed / partially-written line — skip
                }

                if (entry is not null)
                    yield return entry;
            }
        }
    }

    public static IEnumerable<UsageEntry> ParseLines(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line)) continue;

            UsageEntry? entry;
            try { entry = ParseLine(line); }
            catch { entry = null; }

            if (entry is not null)
                yield return entry;
        }
    }

    /// <summary>Parses a single JSONL line, or returns <c>null</c> if it is not a usable usage record.</summary>
    public static UsageEntry? ParseLine(string line)
    {
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return null;

        if (GetString(root, "type") != "assistant") return null;
        if (!root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object) return null;
        if (!message.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object) return null;

        var model = GetString(message, "model");
        if (string.IsNullOrWhiteSpace(model) || model == SyntheticModel) return null;

        if (!TryGetTimestampUtc(root, out var timestamp)) return null;

        long cache5m, cache1h;
        if (usage.TryGetProperty("cache_creation", out var cacheCreation) && cacheCreation.ValueKind == JsonValueKind.Object)
        {
            cache5m = GetLong(cacheCreation, "ephemeral_5m_input_tokens");
            cache1h = GetLong(cacheCreation, "ephemeral_1h_input_tokens");
        }
        else
        {
            // No 5m/1h split available → attribute all cache-creation to the 5m rate.
            cache5m = GetLong(usage, "cache_creation_input_tokens");
            cache1h = 0;
        }

        return new UsageEntry
        {
            TimestampUtc = timestamp,
            Model = model!,
            MessageId = GetString(message, "id"),
            RequestId = GetString(root, "requestId"),
            InputTokens = GetLong(usage, "input_tokens"),
            OutputTokens = GetLong(usage, "output_tokens"),
            CacheCreation5m = cache5m,
            CacheCreation1h = cache1h,
            CacheReadTokens = GetLong(usage, "cache_read_input_tokens"),
            SessionId = GetString(root, "sessionId"),
            ProjectPath = GetString(root, "cwd"),
        };
    }

    private static string? GetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static long GetLong(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Number) return 0;
        if (v.TryGetInt64(out var l)) return l;
        return (long)v.GetDouble();
    }

    private static bool TryGetTimestampUtc(JsonElement root, out DateTimeOffset timestamp)
    {
        timestamp = default;
        var raw = GetString(root, "timestamp");
        if (string.IsNullOrEmpty(raw)) return false;

        return DateTimeOffset.TryParse(
            raw,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out timestamp);
    }
}
