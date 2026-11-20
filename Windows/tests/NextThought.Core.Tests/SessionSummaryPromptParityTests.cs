using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>B13: translated from SessionRecordTests.swift (speaker labels, chunks, parser).</summary>
public sealed class SessionSummaryPromptParityTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    [Fact]
    public void PromptLabelsSpeakersAndOffsets()
    {
        var lines = new[]
        {
            new TranscriptLine { Date = Start.AddSeconds(65), StartedAt = Start.AddSeconds(65), Speaker = "remote", Text = "介绍一下你自己" },
            new TranscriptLine { Date = Start.AddSeconds(70), StartedAt = Start.AddSeconds(70), Speaker = "me", Text = "我叫小王" },
            new TranscriptLine { Date = Start.AddSeconds(75), StartedAt = Start.AddSeconds(75), Speaker = "room", Text = "现场发言" }
        };
        var text = SessionSummaryPrompt.TranscriptText(lines, Start);
        Assert.Contains("[01:05] 对方：介绍一下你自己", text);
        Assert.Contains("[01:10] 我：我叫小王", text);
        Assert.Contains("[01:15] 现场：现场发言", text);
    }

    [Fact]
    public void LongTranscriptsAreChunkedOnLineBoundaries()
    {
        var text = string.Join("\n", Enumerable.Range(0, 100).Select(index => $"[00:{index}] 对方：" + new string('字', 50)));
        var chunks = SessionSummaryPrompt.Chunks(text, 1_000);
        Assert.True(chunks.Count > 1);
        Assert.All(chunks, chunk => Assert.True(chunk.Length <= 1_000));
        Assert.Equal(text, string.Join("\n", chunks));
    }

    [Fact]
    public void ParsesTitleAndQuestionList()
    {
        const string markdown = """
            # 标题
            **字节后端二面：缓存一致性**
            ## 摘要
            面试官主要考察缓存。
            ## 问题清单
            1. 你们怎么保证数据库和缓存一致？ —— 我的回答要点：先更新数据库再删缓存
            2. 如果删除缓存失败怎么办 —— 我的回答要点：未回答
            3) 介绍一下你自己
            ## 关键信息与结论
            - Redis
            """;
        var (title, questions) = SessionSummaryParser.Parse(markdown);
        Assert.Equal("字节后端二面：缓存一致性", title);
        Assert.Equal(3, questions.Count);
        Assert.Equal("你们怎么保证数据库和缓存一致？", questions[0].Text);
        Assert.Equal("先更新数据库再删缓存", questions[0].AnswerGist);
        Assert.Equal("介绍一下你自己", questions[2].Text);
        Assert.Null(questions[2].AnswerGist);
    }
}
