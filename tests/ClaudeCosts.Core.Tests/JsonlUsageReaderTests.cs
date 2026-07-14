using ClaudeCosts.Core.Parsing;

namespace ClaudeCosts.Core.Tests;

public class JsonlUsageReaderTests
{
    [Fact]
    public void Parses_assistant_line_with_cache_split()
    {
        const string line = """
        {"type":"assistant","requestId":"req_1","timestamp":"2026-07-14T06:54:29.845Z","sessionId":"s1","cwd":"C:\\proj","message":{"model":"claude-opus-4-8","id":"msg_1","usage":{"input_tokens":10,"output_tokens":20,"cache_read_input_tokens":5,"cache_creation_input_tokens":10,"cache_creation":{"ephemeral_5m_input_tokens":3,"ephemeral_1h_input_tokens":7}}}}
        """;

        var e = JsonlUsageReader.ParseLine(line);

        Assert.NotNull(e);
        Assert.Equal("claude-opus-4-8", e!.Model);
        Assert.Equal("msg_1", e.MessageId);
        Assert.Equal("req_1", e.RequestId);
        Assert.Equal(10, e.InputTokens);
        Assert.Equal(20, e.OutputTokens);
        Assert.Equal(5, e.CacheReadTokens);
        Assert.Equal(3, e.CacheCreation5m);
        Assert.Equal(7, e.CacheCreation1h);
        Assert.Equal(10, e.CacheCreationTokens);
        Assert.Equal("s1", e.SessionId);
        Assert.Equal("C:\\proj", e.ProjectPath);
        Assert.Equal(TimeSpan.Zero, e.TimestampUtc.Offset); // normalized to UTC
    }

    [Fact]
    public void Falls_back_to_flat_cache_creation_as_5m()
    {
        const string line = """
        {"type":"assistant","timestamp":"2026-07-14T06:54:29.845Z","message":{"model":"claude-opus-4-8","id":"m","usage":{"input_tokens":1,"output_tokens":1,"cache_creation_input_tokens":8}}}
        """;

        var e = JsonlUsageReader.ParseLine(line);

        Assert.NotNull(e);
        Assert.Equal(8, e!.CacheCreation5m);
        Assert.Equal(0, e.CacheCreation1h);
    }

    [Theory]
    [InlineData("""{"type":"user","message":{"role":"user","content":"hi"}}""")]
    [InlineData("""{"type":"assistant","timestamp":"2026-07-14T06:54:29Z","message":{"model":"claude-opus-4-8","id":"m"}}""")]
    [InlineData("""{"type":"assistant","timestamp":"2026-07-14T06:54:29Z","message":{"model":"<synthetic>","id":"m","usage":{"input_tokens":1}}}""")]
    [InlineData("""{"type":"assistant","message":{"model":"claude-opus-4-8","id":"m","usage":{"input_tokens":1}}}""")]
    public void Skips_non_usage_records(string line)
    {
        // user message / missing usage / synthetic model / missing timestamp
        Assert.Null(JsonlUsageReader.ParseLine(line));
    }

    [Fact]
    public void ParseLines_skips_blank_and_malformed_lines()
    {
        var lines = new[]
        {
            "",
            "{ not valid json",
            """{"type":"assistant","timestamp":"2026-07-14T06:54:29Z","message":{"model":"claude-opus-4-8","id":"m","usage":{"input_tokens":42}}}""",
        };

        var entries = JsonlUsageReader.ParseLines(lines).ToList();

        Assert.Single(entries);
        Assert.Equal(42, entries[0].InputTokens);
    }
}
