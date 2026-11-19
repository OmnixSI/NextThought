using System.Text.Json.Nodes;
using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>Regression tests for the data-loss issues in the 1.3.7 review.</summary>
public sealed class SessionDataSafetyTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "nt-data-safety-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
    }

    [Fact]
    public async Task ResummarizingKeepsMacOnlySummaryGroups()
    {
        var store = new SessionStore(new AppPaths(root));
        var meta = new SessionMeta { Title = "原会议" };
        await store.CreateAsync(meta);
        var path = Path.Combine(store.Folder(meta.Id), "summary.json");
        await File.WriteAllTextAsync(path, """
            {"title":"原会议","markdown":"旧摘要","questions":[{"text":"旧问题"}],
             "structured":{"format":"fishbone","sections":[]},"experience":["经验"],"questionBank":[{"id":"q1"}]}
            """);
        await store.SaveSummaryAsync(meta, "## 新摘要", "test");
        var summary = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
        Assert.Equal("## 新摘要", summary["markdown"]!.GetValue<string>());
        Assert.Equal("原会议", summary["title"]!.GetValue<string>());
        Assert.NotNull(summary["structured"]);
        Assert.NotNull(summary["experience"]);
        Assert.NotNull(summary["questionBank"]);
        Assert.Single(summary["questions"]!.AsArray());
    }

    [Fact]
    public async Task SummaryQuestionsComeFromAnswerRecords()
    {
        var store = new SessionStore(new AppPaths(root));
        var meta = new SessionMeta { Title = "会议" };
        await store.CreateAsync(meta);
        await store.AppendAnswerAsync(meta, new AnswerRecord { Question = "预算多少？", Answer = "一百万。", Model = "m" });
        await store.SaveSummaryAsync(meta, "# 纪要", "m");
        using var summary = System.Text.Json.JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(store.Folder(meta.Id), "summary.json")));
        var question = Assert.Single(summary.RootElement.GetProperty("questions").EnumerateArray());
        Assert.Equal("预算多少？", question.GetProperty("text").GetString());
        Assert.Equal("一百万。", question.GetProperty("answerGist").GetString());
    }

    [Fact]
    public async Task CreatingAMeetingWritesContextAndQuestions()
    {
        var store = new SessionStore(new AppPaths(root));
        var meta = new SessionMeta { Title = "会议" };
        await store.CreateAsync(meta);
        var folder = store.Folder(meta.Id);
        Assert.True(File.Exists(Path.Combine(folder, "context.json")));
        await store.AppendQuestionAsync(meta.Id, new SessionQuestionEvent { Text = "问题", Date = DateTimeOffset.UtcNow });
        var line = (await File.ReadAllTextAsync(Path.Combine(folder, "questions.jsonl"))).Trim();
        using var question = System.Text.Json.JsonDocument.Parse(line);
        Assert.Equal("问题", question.RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public async Task ProofreadingKeepsTheRawRecognizerTranscript()
    {
        var store = new SessionStore(new AppPaths(root));
        var meta = new SessionMeta { Title = "会议" };
        await store.CreateAsync(meta);
        var folder = store.Folder(meta.Id);
        await File.WriteAllTextAsync(Path.Combine(folder, "transcript.jsonl"),
            "{\"text\":\"原始识别结果\",\"speaker\":\"me\"}\n");
        await store.RewriteTranscriptAsync(meta, [new TranscriptLine { Text = "校对后的结果", Speaker = "me" }]);
        var original = await File.ReadAllTextAsync(Path.Combine(folder, "transcript.original.jsonl"));
        Assert.Contains("原始识别结果", original);
        using var written = System.Text.Json.JsonDocument.Parse(
            (await File.ReadAllTextAsync(Path.Combine(folder, "transcript.jsonl"))).Trim());
        Assert.Equal("校对后的结果", written.RootElement.GetProperty("text").GetString());
    }
}
