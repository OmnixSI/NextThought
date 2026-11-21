using System.Text.Json;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Translated from Tests/NextThoughtCoreTests/SessionRecordTests.swift.</summary>
public sealed class SessionRecordTestsParityTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    [Fact]
    public void JsonLinesSurviveATornLastLine()
    {
        var first = new TranscriptLine { Date = Start, StartedAt = Start, Speaker = "remote", Text = "你好" };
        var second = new TranscriptLine { Date = Start.AddSeconds(2), StartedAt = Start.AddSeconds(2), Speaker = "me", Text = "我是候选人" };
        var data = JSONLines.Line(first).Concat(JSONLines.Line(second))
            .Concat(System.Text.Encoding.UTF8.GetBytes("{\"id\":\"broken")).ToArray();
        var lines = JSONLines.Decode<TranscriptLine>(data);
        Assert.Equal(2, lines.Count);
        Assert.Equal("me", lines[1].Speaker);
        Assert.Equal(first.Date.AddSeconds(2), lines[1].Date);
    }

    [Fact]
    public void InterruptedSessionReconcilesTranscriptCountBeforeSummaryRecovery()
    {
        var transcript = JSONLines.Line(new TranscriptLine { Date = Start, StartedAt = Start, Speaker = "remote", Text = "先确认范围" })
            .Concat(JSONLines.Line(new TranscriptLine { Date = Start.AddSeconds(2), StartedAt = Start.AddSeconds(2), Speaker = "me", Text = "我来跟进" })).ToArray();
        var lastWrite = Start.AddSeconds(3);
        var meta = new SessionMeta
        {
            StartedAt = Start, Mode = "interview", TranscriptCount = 0,
            SummaryState = "pending", SummaryError = "旧错误"
        };
        Assert.True(meta.RecoverInterruptedRecording(transcript, lastWrite));
        Assert.Equal(2, meta.TranscriptCount);
        Assert.Equal(lastWrite, meta.EndedAt);
        Assert.Equal("none", meta.SummaryState);
        Assert.Null(meta.SummaryError);

        var noTranscript = new SessionMeta { StartedAt = Start, Mode = "interview", SummaryState = "pending" };
        Assert.False(noTranscript.RecoverInterruptedRecording(null, null));
        Assert.Equal(0, noTranscript.TranscriptCount);
        Assert.Equal("none", noTranscript.SummaryState);
    }

    [Fact]
    public void DeferredNotesRuleAndBackwardCompatibleDecoding()
    {
        var meta = new SessionMeta { StartedAt = Start, EndedAt = Start.AddSeconds(60), Mode = "interview", TranscriptCount = 4 };
        Assert.False(meta.NeedsDeferredNotes);
        meta.NotesDeferred = true;
        Assert.True(meta.NeedsDeferredNotes);
        var empty = meta with { TranscriptCount = 0 };
        Assert.False(empty.NeedsDeferredNotes);
        var running = meta with { EndedAt = null };
        Assert.False(running.NeedsDeferredNotes);
        foreach (var state in new[] { "pending", "ready", "failed" })
            Assert.False((meta with { SummaryState = state }).NeedsDeferredNotes);

        var data = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(meta, JsonDefaults.Options);
        var decoded = System.Text.Json.JsonSerializer.Deserialize<SessionMeta>(data, JsonDefaults.Options)!;
        Assert.True(decoded.NeedsDeferredNotes);

        const string oldJson = """{"id":"11111111-1111-1111-1111-111111111111","startedAt":"2026-01-01T00:00:00Z","endedAt":"2026-01-01T00:01:00Z","mode":"general","transcriptCount":2}""";
        var old = System.Text.Json.JsonSerializer.Deserialize<SessionMeta>(oldJson, JsonDefaults.Options)!;
        Assert.Null(old.NotesDeferred);
    }

    [Fact]
    public void StandingSeriesFlagDecodesFromOlderWorkspaces()
    {
        var workspace = new WorkspaceSeries(Guid.NewGuid().ToString("D"), "面试 · 未命名", DateTimeOffset.UnixEpoch, [], [])
        {
            Mode = "interview"
        };
        Assert.False(workspace.Standing == true);
        workspace = workspace with { Standing = true };
        var data = JsonSerializer.SerializeToUtf8Bytes(workspace, WorkspaceStore.Options);
        Assert.True(JsonSerializer.Deserialize<WorkspaceSeries>(data, WorkspaceStore.Options)!.Standing);
        var legacy = JsonSerializer.Deserialize<WorkspaceSeries>(
            $$"""{"id":"{{Guid.NewGuid():D}}","name":"旧系列","mode":"general"}""", WorkspaceStore.Options)!;
        Assert.Null(legacy.Standing);
    }

    [Fact]
    public void SceneAndCoachPreferencesTravelInSettingsBackup()
    {
        var keys = new HashSet<string>(SettingsSnapshot.ExportedKeys);
        Assert.True(keys.IsSupersetOf(new[]
        {
            "cueSurface", "cueWidth", "cueStyle", "cueScreen", "cueLauncher", "cueAvoidsSharing",
            "coachEnabled", "coachWatchesGaze", "coachGazeSensitivity", "lastScene", "sceneOverrides"
        }));
        Assert.DoesNotContain("coachGazeBaseline", keys);
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

    [Fact]
    public void LongTranscriptsAreChunkedOnLineBoundaries()
    {
        var text = string.Join("\n", Enumerable.Range(0, 100)
            .Select(index => $"[00:{index}] 对方：" + new string('字', 50)));
        var chunks = SessionSummaryPrompt.Chunks(text, 1_000);
        Assert.True(chunks.Count > 1);
        Assert.All(chunks, chunk => Assert.True(chunk.Length <= 1_000));
        Assert.Equal(text, string.Join("\n", chunks));
    }

    [Fact]
    public void PromptLabelsSpeakersAndOffsets()
    {
        var meta = new SessionMeta
        {
            StartedAt = Start,
            EndedAt = Start.AddSeconds(600),
            Mode = "interview"
        };
        var lines = new[]
        {
            new TranscriptLine
            {
                Date = Start.AddSeconds(65), StartedAt = Start.AddSeconds(65), Speaker = "remote",
                Text = "介绍一下你自己"
            },
            new TranscriptLine
            {
                Date = Start.AddSeconds(70), StartedAt = Start.AddSeconds(70), Speaker = "me",
                Text = "我叫小王"
            }
        };
        var header = SessionSummaryPrompt.Header("面试", meta.StartedAt, 10, null, "", []);
        var prompt = SessionSummaryPrompt.Full(header, lines, meta.StartedAt, [], []);
        Assert.Contains("[01:05] 对方：介绍一下你自己", prompt);
        Assert.Contains("[01:10] 我：我叫小王", prompt);
        Assert.Contains("时长：10 分钟", prompt);
    }

    [Fact]
    public void MarkdownExportContainsSummaryAnswersAndTranscript()
    {
        var meta = new SessionMeta
        {
            StartedAt = Start,
            EndedAt = Start.AddSeconds(120),
            Mode = "meeting",
            AudioSource = "Zoom"
        };
        var summary = new SessionProjectedSummary("周会", "# 标题\n周会\n## 摘要\n讨论上线。");
        var lines = new[]
        {
            new TranscriptLine
            {
                Date = Start.AddSeconds(5), StartedAt = Start.AddSeconds(5), Speaker = "remote",
                Text = "什么时候上线"
            }
        };
        var answers = new[]
        {
            new SessionProjectedAnswer(Guid.NewGuid(), Start, "什么时候上线", "下周三", "rev")
        };
        var projection = SessionProjectionBuilder.Build(Guid.NewGuid(), meta.StartedAt, lines, answers, summary);
        var text = SessionMarkdownExporter.Export("周会", "工作会议", meta.StartedAt, 120, meta.AudioSource,
            projection, lines);

        Assert.StartsWith("# 周会\n", text);
        Assert.Contains("## 摘要\n讨论上线。", text);
        Assert.DoesNotContain("# 标题", text);
        Assert.Contains("### 什么时候上线", text);
        Assert.Contains("- `00:05` **对方**：什么时候上线", text);
    }
}
