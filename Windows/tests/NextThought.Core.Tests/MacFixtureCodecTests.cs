using System.Text;
using System.Text.Json;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// A15: decode the pure-data Mac fixtures with the Windows codecs, checking the six differences the
/// fixture README calls out against the old hand-written emit. A write/read round-trip over the same
/// values proves the Windows side keeps what it read.
/// </summary>
public sealed class MacFixtureCodecTests
{
    private static readonly JsonSerializerOptions Read = JsonDefaults.ArchiveOptions;

    private static readonly string[] JsonlFiles =
        ["transcript.jsonl", "translations.jsonl", "voices.jsonl", "questions.jsonl", "answers.jsonl"];

    [Fact]
    public void FixturesArePresentAndDocumentTheDifferences()
    {
        if (!MacFixtures.Available) return; // SKIP: the fixture directory only exists in the repository checkout.
        var readme = MacFixtures.Text(MacFixtures.FixturePath("README.md"));
        foreach (var sentence in new[]
                 {
                     "Workspace` 没有 `sessionIds", "BackupManifest` 记的是 `kind`", "SessionQuestionEvent` 必须有 `score`",
                     "SessionMeta` 必须有 `audioSource`", "Glossary` 编码为 `{\"entries\": [...]}`", "紧凑 JSON 对象"
                 })
            Assert.Contains(sentence, readme);
        foreach (var name in new[]
                 {
                     "session-meta.json", "transcript.jsonl", "summary.json", "structured-notes.json", "workspace.json",
                     "skills.json", "templates.json", "glossary.json", "backup-manifest.json", "revisions.json"
                 })
            Assert.True(File.Exists(MacFixtures.FixturePath(name)), name);
    }

    [Fact]
    public void EveryJsonlFileHoldsOneCompactObjectPerLine()
    {
        if (!MacFixtures.Available) return; // SKIP: fixtures live in the repository checkout.
        foreach (var name in JsonlFiles)
        {
            var lines = File.ReadAllLines(MacFixtures.FixturePath(name), Encoding.UTF8)
                .Where(line => line.Trim().Length > 0).ToList();
            Assert.NotEmpty(lines);
            var compact = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            foreach (var line in lines)
            {
                Assert.StartsWith("{", line);
                Assert.EndsWith("}", line);
                using var document = JsonDocument.Parse(line);
                Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
                // Mac's encoder writes one compact object per line: no indentation or spaces between tokens.
                Assert.Equal(line, JsonSerializer.Serialize(document.RootElement, compact));
                // JSONLines writes and reads one object per line without adding blank lines.
                var bytes = JSONLines.Line(document.RootElement.Clone());
                Assert.Single(Encoding.UTF8.GetString(bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries));
            }
        }
    }

    [Fact]
    public void SessionMetaHasTheRequiredMacFieldsAndSecondDates()
    {
        if (!MacFixtures.Available) return; // SKIP: fixtures live in the repository checkout.
        using var raw = MacFixtures.Document(MacFixtures.FixturePath("session-meta.json"));
        foreach (var property in new[]
                 {
                     "audioSource", "recognizer", "answerModel", "transcriptCount", "questionCount", "answerCount", "summaryState"
                 })
            Assert.True(raw.RootElement.TryGetProperty(property, out _), property);

        var meta = MacFixtures.Json<SessionMeta>(MacFixtures.FixturePath("session-meta.json"));
        Assert.Equal("system+mic", meta.AudioSource);
        Assert.Equal("zipformer-xasr-480", meta.Recognizer);
        Assert.Equal("fixture-model", meta.AnswerModel);
        Assert.Equal(4, meta.TranscriptCount);
        Assert.Equal(1, meta.QuestionCount);
        Assert.Equal(1, meta.AnswerCount);
        Assert.Equal("ready", meta.SummaryState);
        Assert.Equal("33333333-3333-4333-8333-333333333333", meta.WorkspaceId);
        Assert.Equal("王老师", meta.VoiceNames?["1"]);
        Assert.Empty(meta.MeVoices ?? []);

        // Mac dates are whole seconds, ISO-8601 with Z; the archive writer keeps that shape.
        var written = JsonSerializer.Serialize(meta, Read);
        using var document = JsonDocument.Parse(written);
        Assert.Equal("2025-11-24T16:00:00Z", document.RootElement.GetProperty("startedAt").GetString());
        Assert.Equal("2025-11-24T16:10:00Z", document.RootElement.GetProperty("endedAt").GetString());
    }

    [Fact]
    public void TranscriptAndEventFilesDecodeWithTheirMacFields()
    {
        if (!MacFixtures.Available) return; // SKIP: fixtures live in the repository checkout.
        var lines = MacFixtures.Lines<TranscriptLine>(MacFixtures.FixturePath("transcript.jsonl"));
        Assert.Equal(4, lines.Count);
        Assert.Equal("44444444-4444-4444-8444-000000000001", lines[0].Id.ToString("D"));
        Assert.Equal("remote", lines[0].Speaker);
        Assert.Equal(1, lines[0].Voice);
        Assert.Contains("Zipformer", lines[0].Text);
        Assert.Equal(new DateTimeOffset(2025, 11, 24, 16, 0, 10, TimeSpan.Zero), lines[0].Date.ToUniversalTime());
        Assert.NotNull(lines[0].StartedAt);

        var translations = MacFixtures.Lines<SessionTranscriptTranslation>(MacFixtures.FixturePath("translations.jsonl"));
        Assert.Equal(3, translations.Count);
        Assert.Equal("518b5518dc01a6e5f88d10a4767d3da76ed5805ed4c46df299cf82271d928f4e", translations[0].InputRevision);

        var voices = MacFixtures.Lines<SessionVoiceTag>(MacFixtures.FixturePath("voices.jsonl"));
        Assert.Equal(3, voices.Count);
        Assert.Equal(2, voices.Single(tag => tag.Id.ToString("D") == "44444444-4444-4444-8444-000000000003").Voice);

        var questions = MacFixtures.Lines<SessionQuestionEvent>(MacFixtures.FixturePath("questions.jsonl"));
        var question = Assert.Single(questions);
        Assert.Equal(0.82, question.Score, 3);
        Assert.Equal("auto", question.Disposition);
        Assert.Equal("confirm", question.Intent);
        Assert.Equal("提交时间定了吗？", question.Text);
        Assert.Equal("那我问一下", question.LeadIn);

        var answers = MacFixtures.Lines<SessionAnswerEvent>(MacFixtures.FixturePath("answers.jsonl"));
        var answer = Assert.Single(answers);
        Assert.Equal("先按周五提交。", answer.Answer);
        Assert.Equal(2.5, answer.Seconds);
        Assert.Equal(0.75, answer.FirstTokenSeconds);
        Assert.True(answer.Automatic);
        Assert.True(answer.IsComplete);
    }

    [Fact]
    public void SummaryDecodesIncludingEvidenceReviews()
    {
        if (!MacFixtures.Available) return; // SKIP: fixtures live in the repository checkout.
        var summary = MacFixtures.Json<SessionSummary>(MacFixtures.SessionPath(
            "sessions/11111111-1111-4111-8111-111111111111", "summary.json"));
        Assert.Equal("A15 第一次交付会", summary.Title);
        Assert.Contains("先按周五提交", summary.Markdown);
        Assert.Equal("fixture-model", summary.Model);
        Assert.Equal("edeed56b0a7b62f0670f5591b59ace2791968183e3a5c490f2a63e02c589f412", summary.TranscriptSourceRevision);
        Assert.Equal("b7889559a26808053e849498189fce3108a0d04ad842921235901571f47df389",
            summary.StructuredNotesSourceRevision);
        var review = Assert.Single(summary.EvidenceReviews!);
        Assert.Equal("notes:decisions:EEEEEEEE-EEEE-4EEE-8EEE-000000000002", review.Key);
        Assert.Equal("0e311dffffaee9db77e5cee16717b266f7bfe3b36190d8f3b5efe71c9b0fdf9a", review.Value.ClaimRevision);
        Assert.Equal("660b02b4e232fe347b67bb4118d65d1c6ebe7bb655caa0aeb5998afff8b28c22", review.Value.LineSourceRevision);
        Assert.NotNull(summary.GeneratedAt);
    }

    [Fact]
    public void StructuredNotesAndUserContentDecode()
    {
        if (!MacFixtures.Available) return; // SKIP: fixtures live in the repository checkout.
        var notes = MacFixtures.Json<StructuredNotesDoc>(MacFixtures.SessionPath(
            "sessions/11111111-1111-4111-8111-111111111111", "structured-notes.json"));
        Assert.Equal("work", notes.TemplateId);
        Assert.Equal("zh", notes.Language);
        Assert.Equal(2, notes.Sections.Count);
        Assert.Equal(NotesSectionKind.OneLine, notes.Sections[0].Kind);
        var decision = Assert.Single(notes.Sections[1].Items);
        Assert.Equal("先按周五提交", decision.Text);
        Assert.Equal("周五之前提交", decision.Quote);
        var correction = Assert.Single(notes.Corrections!);
        Assert.Equal("Sherpa", correction.Original);
        Assert.Equal("Sherpa-ONNX", correction.Corrected);
        Assert.Equal(TranscriptCorrectionSource.Model, correction.Source);

        var user = MacFixtures.Json<SessionUserContent>(MacFixtures.FixturePath("user-content.json"));
        Assert.Equal(1, user.SchemaVersion);
        Assert.Equal(3, user.Revision);
        Assert.Contains("评测表", user.PersonalNotes);
        var highlight = Assert.Single(user.Highlights);
        Assert.Equal(20, highlight.OffsetSeconds);
        Assert.Equal("周五之前提交没问题", highlight.Quote);
        var decisionCorrection = Assert.Single(user.CorrectionDecisions);
        Assert.Equal("CCCCCCCC-CCCC-4CCC-8CCC-000000000002", decisionCorrection.Id);
        Assert.Equal("Sherpa-ONNX", decisionCorrection.Corrected);
    }

    [Fact]
    public void WorkspaceHasNoSessionIdsAndDecodesItsSeries()
    {
        if (!MacFixtures.Available) return; // SKIP: fixtures live in the repository checkout.
        using var raw = MacFixtures.Document(MacFixtures.FixturePath("workspace.json"));
        Assert.False(raw.RootElement.TryGetProperty("sessionIds", out _));
        Assert.Equal("33333333-3333-4333-8333-333333333333", raw.RootElement.GetProperty("id").GetString());

        var series = MacFixtures.Json<WorkspaceSeries>(MacFixtures.FixturePath("workspace.json"));
        Assert.Equal("A15 交付系列", series.Name);
        Assert.Equal("meeting", series.Mode);
        Assert.NotNull(series.Preferences);
        Assert.Equal("每周一次的交付例会。", series.Notes);
        Assert.Empty(series.Documents);
        Assert.Single(series.Glossary?.Entries!); // the series glossary adds one term to the global table.
        Assert.Empty(series.SessionIds); // Mac derives membership from meta.workspaceID.

        var item = Assert.Single(series.SeriesItems);
        Assert.Equal(2, item.Revision);
        Assert.Equal(WorkspaceSeriesItem.KindValue.Decision, item.Kind);
        Assert.Equal("正式改为周一提交", item.Text);
        var history = Assert.Single(item.History);
        Assert.Equal(1, history.Revision);
        Assert.Equal("A1C46CA5-2166-55DA-AD0B-5FABCE0473FB", history.Id);
    }

    [Fact]
    public void BackupManifestKeepsCountsNotLists()
    {
        if (!MacFixtures.Available) return; // SKIP: fixtures live in the repository checkout.
        using var raw = MacFixtures.Document(MacFixtures.FixturePath("backup-manifest.json"));
        Assert.False(raw.RootElement.TryGetProperty("sessions", out _));
        Assert.False(raw.RootElement.TryGetProperty("workspaces", out _));
        foreach (var property in new[] { "kind", "appVersion", "appBuild", "workspaceCount", "sessionCount" })
            Assert.True(raw.RootElement.TryGetProperty(property, out _), property);

        var manifest = MacFixtures.Json<BackupManifest>(MacFixtures.FixturePath("backup-manifest.json"));
        Assert.Equal(BackupManifest.BackupKind.Full, manifest.Kind);
        Assert.Equal("fixture", manifest.AppVersion);
        Assert.Equal("0", manifest.AppBuild);
        Assert.Equal(1, manifest.WorkspaceCount);
        Assert.Equal(2, manifest.SessionCount);
        Assert.False(manifest.IncludesSecrets);
        manifest.Validate();

        var written = JsonSerializer.Serialize(manifest, Read);
        using var document = JsonDocument.Parse(written);
        Assert.False(document.RootElement.TryGetProperty("sessions", out _));
        Assert.Equal(2, document.RootElement.GetProperty("sessionCount").GetInt32());
    }

    [Fact]
    public void SkillsTemplatesAndGlossaryDecodeAndRoundTrip()
    {
        if (!MacFixtures.Available) return; // SKIP: fixtures live in the repository checkout.
        var skills = MacFixtures.Json<List<Skill>>(MacFixtures.FixturePath("skills.json"));
        var skill = Assert.Single(skills);
        Assert.Equal("A15 交付口径", skill.Name);
        Assert.Equal("回答交付时间时先给日期", skill.Summary);
        Assert.False(skill.Global);
        Assert.True(skill.Enabled);
        var skillsBack = JsonSerializer.Deserialize<List<Skill>>(JsonSerializer.Serialize(skills, Read), Read)!;
        Assert.Equal(skills, skillsBack);

        var templates = MacFixtures.Json<List<NotesTemplate>>(MacFixtures.FixturePath("templates.json"));
        var template = Assert.Single(templates);
        Assert.Equal("A15 交付纪要", template.Name);
        Assert.Equal(NotesTemplateKind.Work, template.Base);
        Assert.Equal(5, template.Sections.Count);
        Assert.Equal(NotesSectionKind.Actions, template.Sections[2].Kind);
        Assert.Equal(NotesCarry.Lesson, template.Sections[2].Carry);
        Assert.True(template.Sections[3].IsGap);
        var templatesJson = JsonSerializer.Serialize(templates, Read);
        var templatesBack = JsonSerializer.Deserialize<List<NotesTemplate>>(templatesJson, Read)!;
        Assert.Equal(templatesJson, JsonSerializer.Serialize(templatesBack, Read));

        // The fixture file is the Mac wrapper shape; a bare array is the legacy shape and still reads.
        var glossary = MacFixtures.Json<Glossary>(MacFixtures.FixturePath("glossary.json"));
        Assert.Equal(2, glossary.Entries.Count);
        Assert.Equal("Zipformer", glossary.Entries[0].Canonical);
        Assert.Equal(GlossaryKind.Term, glossary.Entries[0].Kind);
        Assert.Equal(GlossaryKind.Person, glossary.Entries[1].Kind);
        var written = JsonSerializer.Serialize(glossary, GlossaryStore.Options);
        Assert.StartsWith("{\n  \"entries\":", written.Replace("\r\n", "\n"));
        Assert.Equal(glossary.Entries.Count,
            JsonSerializer.Deserialize<Glossary>(written, JsonDefaults.Options)!.Entries.Count);
        using var raw = MacFixtures.Document(MacFixtures.FixturePath("glossary.json"));
        var bare = raw.RootElement.GetProperty("entries").GetRawText();
        var fromBareArray = JsonSerializer.Deserialize<Glossary>(bare, JsonDefaults.Options)!;
        Assert.Equal(glossary.Entries.Count, fromBareArray.Entries.Count);
        Assert.Equal(glossary.Entries[0].Canonical, fromBareArray.Entries[0].Canonical);
    }

    [Fact]
    public void WritingTheReadValuesKeepsTheMacFilesReadable()
    {
        if (!MacFixtures.Available) return; // SKIP: fixtures live in the repository checkout.
        // Write into a temporary directory shaped like a Mac backup, then read every file back.
        var directory = Path.Combine(Path.GetTempPath(), "nt-a15-roundtrip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var glossary = MacFixtures.Json<Glossary>(MacFixtures.FixturePath("glossary.json"));
            File.WriteAllText(Path.Combine(directory, "glossary.json"),
                JsonSerializer.Serialize(glossary, GlossaryStore.Options), Encoding.UTF8);

            var skills = MacFixtures.Json<List<Skill>>(MacFixtures.FixturePath("skills.json"));
            File.WriteAllText(Path.Combine(directory, "skills.json"), JsonSerializer.Serialize(skills, Read), Encoding.UTF8);

            var templates = MacFixtures.Json<List<NotesTemplate>>(MacFixtures.FixturePath("templates.json"));
            File.WriteAllText(Path.Combine(directory, "note-templates.json"),
                JsonSerializer.Serialize(templates, Read), Encoding.UTF8);

            var manifest = MacFixtures.Json<BackupManifest>(MacFixtures.FixturePath("backup-manifest.json"));
            File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonSerializer.Serialize(manifest, Read), Encoding.UTF8);

            Assert.Equal(glossary.Entries.Count,
                JsonSerializer.Deserialize<Glossary>(File.ReadAllText(Path.Combine(directory, "glossary.json")),
                    JsonDefaults.Options)!.Entries.Count);
            Assert.Equal(skills, JsonSerializer.Deserialize<List<Skill>>(
                File.ReadAllText(Path.Combine(directory, "skills.json")), Read));
            var templatesRoundTrip = JsonSerializer.Deserialize<List<NotesTemplate>>(
                File.ReadAllText(Path.Combine(directory, "note-templates.json")), Read)!;
            Assert.Equal(JsonSerializer.Serialize(templates, Read), JsonSerializer.Serialize(templatesRoundTrip, Read));
            var manifestBack = JsonSerializer.Deserialize<BackupManifest>(
                File.ReadAllText(Path.Combine(directory, "manifest.json")), Read)!;
            Assert.Equal(manifest.SessionCount, manifestBack.SessionCount);
            Assert.Equal(manifest.WorkspaceCount, manifestBack.WorkspaceCount);
            manifestBack.Validate();
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch { }
        }
    }

    [Fact]
    public void FixtureSessionsCarryTheirMacDirectoryLayout()
    {
        if (!MacFixtures.Available) return; // SKIP: fixtures live in the repository checkout.
        foreach (var id in new[]
                 {
                     "11111111-1111-4111-8111-111111111111", "22222222-2222-4222-8222-222222222222"
                 })
        {
            var folder = Path.Combine(MacFixtures.Directory, "sessions", id);
            Assert.True(Directory.Exists(folder), folder);
            foreach (var name in new[] { "meta.json", "transcript.jsonl", "summary.json", "structured-notes.json" })
                Assert.True(File.Exists(Path.Combine(folder, name)), name);
            var meta = MacFixtures.Json<SessionMeta>(Path.Combine(folder, "meta.json"));
            Assert.Equal(Guid.Parse(id), meta.Id);
            var lines = MacFixtures.Lines<TranscriptLine>(Path.Combine(folder, "transcript.jsonl"));
            Assert.Equal(meta.TranscriptCount, lines.Count);
        }
    }
}
