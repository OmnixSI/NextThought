using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the complete meeting record aggregate.</summary>
public sealed class SessionRecordTests
{
    private static SessionData Data()
    {
        var meta = new SessionMeta
        {
            Id = Guid.NewGuid(), Title = "组会", StartedAt = DateTimeOffset.UtcNow.AddHours(-1),
            EndedAt = DateTimeOffset.UtcNow, SummaryError = null
        };
        var line = new TranscriptLine { Id = Guid.NewGuid(), Text = "先演练", Date = meta.StartedAt };
        var answer = new AnswerRecord
        {
            Id = Guid.NewGuid(), Date = meta.StartedAt.AddMinutes(5), Question = "怎么上线？",
            Answer = "先灰度", Model = "gpt", Automatic = false
        };
        return new SessionData(meta, [line], [answer], "## 摘要\n先演练再上线");
    }

    [Fact]
    public void FromSessionDataCarriesMetaTranscriptAnswersAndSummary()
    {
        var data = Data();
        var record = SessionRecord.FromSessionData(data);
        Assert.Equal(data.Meta.Id, record.Id);
        Assert.Equal("组会", record.Title);
        Assert.Single(record.Transcript);
        Assert.Single(record.Answers);
        Assert.Equal("先灰度", record.Answers[0].Answer);
        Assert.Equal(data.Summary, record.Summary);
        Assert.Empty(record.Questions);
        Assert.Empty(record.Translations);
    }

    [Fact]
    public void ToSessionDataRoundTripsTheSameContent()
    {
        var data = Data();
        var back = SessionRecord.FromSessionData(data).ToSessionData();
        Assert.Equal(data.Meta.Id, back.Meta.Id);
        Assert.Equal(data.Meta.Title, back.Meta.Title);
        Assert.Equal(data.Meta.StartedAt, back.Meta.StartedAt);
        Assert.Equal(data.Lines.Select(line => line.Text), back.Lines.Select(line => line.Text));
        Assert.Equal(data.Answers.Select(answer => answer.Answer), back.Answers.Select(answer => answer.Answer));
        Assert.Equal(data.Answers[0].Id, back.Answers[0].Id);
        Assert.Equal(data.Summary, back.Summary);
    }

    [Fact]
    public void QuestionsTranslationsAndSpeakerOrderAreOptional()
    {
        var record = new SessionRecord
        {
            Questions = [new SessionQuestionEvent { Text = "为什么？", Intent = "clarify", Score = 0.8 }],
            Translations = [new SessionTranscriptTranslation { Text = "先演练", InputRevision = "r1" }],
            SpeakerOrder = ["speaker:0", "speaker:1"]
        };
        Assert.Single(record.Questions);
        Assert.Equal("clarify", record.Questions[0].Intent);
        Assert.Equal("r1", record.Translations[0].InputRevision);
        Assert.Equal(2, record.SpeakerOrder.Count);
        Assert.Empty(record.Transcript);
    }

    [Fact]
    public void EmptyRecordsHaveSafeDefaults()
    {
        var record = new SessionRecord();
        Assert.NotEqual(Guid.Empty, record.Id);
        Assert.True(record.StartedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Empty(record.Transcript);
        Assert.Empty(record.Answers);
        Assert.Equal("", record.Summary);
        Assert.Null(record.EndedAt);
    }

    [Fact]
    public void UnknownMacFieldsRoundTrip()
    {
        const string json = """
            {"id":"11111111-1111-1111-1111-111111111111","title":"客户会","startedAt":"2026-01-01T00:00:00Z",
             "workspaceID":"22222222-2222-2222-2222-222222222222","workspaceName":"客户项目","mode":"client",
             "documents":[{"id":"33333333-3333-3333-3333-333333333333","name":"方案","pages":["正文"]}]}
            """;
        var record = System.Text.Json.JsonSerializer.Deserialize<SessionRecord>(json, NextThought.Core.JsonDefaults.Options)!;
        Assert.NotNull(record.AdditionalProperties);
        var saved = System.Text.Json.JsonSerializer.Serialize(record, NextThought.Core.JsonDefaults.Options);
        Assert.Contains("\"workspaceID\"", saved);
        Assert.Contains("\"workspaceName\"", saved);
        Assert.Contains("\"documents\"", saved);
        Assert.Contains("\"title\":\"客户会\"", saved);
    }

    [Fact]
    public void AnswerRecordKeepsMacExtras()
    {
        const string json = """
            {"id":"11111111-1111-1111-1111-111111111111","date":"2026-01-01T00:00:00Z","question":"问","answer":"答",
             "model":"m","seconds":2.5,"firstTokenSeconds":0.4,"automatic":true,"screenshotPath":"screenshots/a.png",
             "webAnswer":"联网答","webSources":[{"title":"标题","url":"https://example.com","snippet":"摘要"}],
             "futureAnswer":"x"}
            """;
        var answer = System.Text.Json.JsonSerializer.Deserialize<AnswerRecord>(json, NextThought.Core.JsonDefaults.Options)!;
        Assert.Equal(0.4, answer.FirstTokenSeconds);
        Assert.Equal("screenshots/a.png", answer.ScreenshotPath);
        Assert.Single(answer.WebSources!);
        var saved = System.Text.Json.JsonSerializer.Serialize(answer, NextThought.Core.JsonDefaults.Options);
        Assert.Contains("\"firstTokenSeconds\"", saved);
        Assert.Contains("\"webSources\"", saved);
        Assert.Contains("\"futureAnswer\"", saved);
    }

    [Fact]
    public void TranscriptLineKeepsMacTranslationAndUnknownFields()
    {
        const string json = """
            {"id":"11111111-1111-1111-1111-111111111111","date":"2026-01-01T00:00:00Z","speaker":"room",
             "text":"原文","translatedText":"translation","translationInputRevision":"rev","futureLine":"x"}
            """;
        var line = System.Text.Json.JsonSerializer.Deserialize<TranscriptLine>(json, NextThought.Core.JsonDefaults.Options)!;
        Assert.Equal("translation", line.TranslatedText);
        Assert.Equal("rev", line.TranslationInputRevision);
        var saved = System.Text.Json.JsonSerializer.Serialize(line, NextThought.Core.JsonDefaults.Options);
        Assert.Contains("\"translatedText\"", saved);
        Assert.Contains("\"translationInputRevision\"", saved);
        Assert.Contains("\"futureLine\"", saved);
    }
}
