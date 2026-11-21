using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Port of CodexEventParserTests.swift: only complete agent messages and failures surface.</summary>
public sealed class CodexEventParserParityTests
{
    [Fact]
    public void ReadsCompleteAgentMessage()
    {
        const string line = """{"type":"item.completed","item":{"type":"agent_message","text":"Ready"}}""";
        var parsed = CodexOutputParser.Parse(line);
        Assert.NotNull(parsed);
        Assert.True(parsed!.IsMessage);
        Assert.Equal("message", parsed.Kind);
        Assert.Equal("Ready", parsed.Text);
    }

    [Fact]
    public void ReadsCodexFailureDetails()
    {
        const string line = """{"type":"turn.failed","error":{"message":"Account unavailable"}}""";
        var parsed = CodexOutputParser.Parse(line);
        Assert.NotNull(parsed);
        Assert.True(parsed!.IsFailure);
        Assert.Equal("failure", parsed.Kind);
        Assert.Equal("Account unavailable", parsed.Text);
    }

    [Fact]
    public void IgnoresProgressAndUnknownDeltaEvents()
    {
        const string progress = """{"type":"item.started","item":{"type":"agent_message","text":"Draft"}}""";
        const string delta = """{"type":"item.agent_message.delta","delta":"Draft"}""";
        Assert.Null(CodexOutputParser.Parse(progress));
        Assert.Null(CodexOutputParser.Parse(delta));
    }

    [Fact]
    public void IgnoresMalformedJSON()
    {
        Assert.Null(CodexOutputParser.Parse("not json"));
    }
}
