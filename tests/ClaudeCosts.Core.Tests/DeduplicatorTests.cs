using ClaudeCosts.Core.Costs;
using ClaudeCosts.Core.Models;

namespace ClaudeCosts.Core.Tests;

public class DeduplicatorTests
{
    private static UsageEntry Entry(string? messageId, string? requestId, long output = 0, long input = 1) => new()
    {
        TimestampUtc = DateTimeOffset.UnixEpoch,
        Model = "claude-opus-4-8",
        MessageId = messageId,
        RequestId = requestId,
        InputTokens = input,
        OutputTokens = output,
    };

    [Fact]
    public void Collapses_duplicate_message_request_pairs()
    {
        var entries = new[]
        {
            Entry("msg_1", "req_1"),
            Entry("msg_1", "req_1"), // duplicate key
            Entry("msg_2", "req_2"),
        };

        var result = UsageDeduplicator.Distinct(entries);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Keeps_the_record_with_the_largest_output_per_key()
    {
        // Streamed snapshots of one turn: constant input, growing output.
        var entries = new[]
        {
            Entry("msg_1", "req_1", output: 10),
            Entry("msg_1", "req_1", output: 50), // final/complete
            Entry("msg_1", "req_1", output: 30),
        };

        var result = UsageDeduplicator.Distinct(entries);

        Assert.Single(result);
        Assert.Equal(50, result[0].OutputTokens);
    }

    [Fact]
    public void Keeps_entries_missing_either_id()
    {
        var entries = new[]
        {
            Entry(null, "req_1"),
            Entry("msg_1", null),
            Entry(null, null),
            Entry(null, null),
        };

        var result = UsageDeduplicator.Distinct(entries);

        Assert.Equal(4, result.Count); // none are de-duplicated
    }
}
