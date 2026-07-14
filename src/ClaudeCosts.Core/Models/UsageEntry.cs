namespace ClaudeCosts.Core.Models;

/// <summary>
/// A single normalized usage record extracted from one assistant line of a
/// Claude Code <c>*.jsonl</c> transcript. Token counts default to 0 when the
/// underlying field is absent.
/// </summary>
public sealed record UsageEntry
{
    /// <summary>Timestamp of the record, in UTC.</summary>
    public required DateTimeOffset TimestampUtc { get; init; }

    /// <summary>The model id, e.g. <c>claude-opus-4-8</c>. Never <c>&lt;synthetic&gt;</c> (those are skipped).</summary>
    public required string Model { get; init; }

    /// <summary><c>message.id</c> — used together with <see cref="RequestId"/> for de-duplication.</summary>
    public string? MessageId { get; init; }

    /// <summary><c>requestId</c> — used together with <see cref="MessageId"/> for de-duplication.</summary>
    public string? RequestId { get; init; }

    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }

    /// <summary>5-minute ephemeral cache-write tokens (billed at 1.25× input).</summary>
    public long CacheCreation5m { get; init; }

    /// <summary>1-hour ephemeral cache-write tokens (billed at 2× input).</summary>
    public long CacheCreation1h { get; init; }

    /// <summary>Cache-read tokens (billed at 0.1× input).</summary>
    public long CacheReadTokens { get; init; }

    /// <summary>Total cache-creation tokens (5m + 1h).</summary>
    public long CacheCreationTokens => CacheCreation5m + CacheCreation1h;

    /// <summary>Sum of every token category on this record.</summary>
    public long TotalTokens =>
        InputTokens + OutputTokens + CacheCreation5m + CacheCreation1h + CacheReadTokens;

    public string? SessionId { get; init; }

    /// <summary>Working directory the session ran in (<c>cwd</c>).</summary>
    public string? ProjectPath { get; init; }
}
