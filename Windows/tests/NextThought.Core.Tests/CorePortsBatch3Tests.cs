using System.Text.Json.Nodes;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the third self-contained Core batch.</summary>
public sealed class CorePortsBatch3Tests
{
    [Fact]
    public void QuestionDetectorAcceptsDirectQuestions()
    {
        var detector = new QuestionDetector();
        var now = DateTimeOffset.UtcNow;
        Assert.Equal("为什么用 Redis？", detector.Detect("为什么用 Redis？", now));
        Assert.Equal("What is the cache consistency model?", detector.Detect("What is the cache consistency model?", now.AddSeconds(1)));
    }

    [Fact]
    public void QuestionDetectorIgnoresStatements()
    {
        var detector = new QuestionDetector();
        Assert.Null(detector.Detect("我们今天先过一遍监测方案。"));
        Assert.Null(detector.Detect("嗯。"));
    }

    [Fact]
    public void QuestionDetectorMergesShortFragments()
    {
        var detector = new QuestionDetector();
        var now = DateTimeOffset.UtcNow;
        Assert.Null(detector.Detect("can you", now));
        Assert.Equal("can you explain the cache?", detector.Detect("explain the cache?", now.AddSeconds(2)));
    }

    [Fact]
    public void QuestionDetectorExpiresOldFragments()
    {
        var detector = new QuestionDetector();
        var now = DateTimeOffset.UtcNow;
        Assert.Null(detector.Detect("can you", now));
        Assert.Equal("explain the cache", detector.Detect("explain the cache", now.AddSeconds(9)));
    }

    [Fact]
    public void QuestionDetectorSuppressesRepeatsForFifteenSeconds()
    {
        var detector = new QuestionDetector();
        var now = DateTimeOffset.UtcNow;
        Assert.NotNull(detector.Detect("为什么用 Redis？", now));
        Assert.Null(detector.Detect("为什么用 Redis？", now.AddSeconds(10)));
        Assert.NotNull(detector.Detect("为什么用 Redis？", now.AddSeconds(16)));
    }

    [Fact]
    public void QuestionDetectorAcceptsShortFollowUps()
    {
        var detector = new QuestionDetector();
        Assert.Equal("为什么", detector.Detect("为什么", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void QuestionDetectorBoundsLongText()
    {
        var detector = new QuestionDetector();
        var question = new string('字', 3000) + "？";
        var result = detector.Detect(question);
        Assert.NotNull(result);
        Assert.True(result!.Length <= 2000);
    }

    [Fact]
    public void QuestionNormalisationUsesChineseBigrams()
    {
        var normalised = QuestionDetector.Normalize("为什么用 Redis");
        Assert.Contains("为什", normalised);
        Assert.Contains("redis", normalised);
    }

    [Fact]
    public void DocumentNoticesSeparateNotesFromWarnings()
    {
        var lines = new[] { "第 7 页的图片没有读取", "表格识别失败", "第 7 页的图片没有读取" };
        Assert.Equal(["第 7 页的图片没有读取", "表格识别失败"], DocumentNotice.Unique(lines));
        Assert.Equal(["表格识别失败"], DocumentNotice.Warnings(lines));
        Assert.True(DocumentNotice.IsNote("第 7 页的图片没有读取"));
        Assert.False(DocumentNotice.IsNote("表格识别失败"));
    }

    [Fact]
    public void DocumentNoticeSummarisesPagesAndSlides()
    {
        Assert.Equal("p.7、15 的图片没有读取，正文已完整提取。",
            DocumentNotice.Note(["p.7 的图片没有读取", "p.15 的图片没有读取", "p.7 的图片没有读取"]));
        Assert.Equal("幻灯片 3 的图片没有读取，正文已完整提取。",
            DocumentNotice.Note(["幻灯片 3 的图片没有读取"]));
        Assert.Equal("部分图片里的文字没有读取，正文已完整提取。",
            DocumentNotice.Note(["图片里的文字没有读取，请查看"]));
        Assert.Null(DocumentNotice.Note(["表格识别失败"]));
    }

    [Fact]
    public void BackupFingerprintIsContentBased()
    {
        var first = Path.Combine(Path.GetTempPath(), "nt-backup-a-" + Guid.NewGuid().ToString("N"));
        var second = Path.Combine(Path.GetTempPath(), "nt-backup-b-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(first, "Sessions"));
            Directory.CreateDirectory(Path.Combine(second, "Sessions"));
            File.WriteAllText(Path.Combine(first, "Sessions", "meta.json"), "{\"id\":1}");
            File.WriteAllText(Path.Combine(second, "Sessions", "meta.json"), "{\"id\":1}");
            File.WriteAllText(Path.Combine(second, "._meta.json"), "junk");
            Assert.Equal(BackupContent.Fingerprint(first), BackupContent.Fingerprint(second));

            File.WriteAllText(Path.Combine(second, "Sessions", "meta.json"), "{\"id\":2}");
            Assert.NotEqual(BackupContent.Fingerprint(first), BackupContent.Fingerprint(second));
        }
        finally
        {
            try { Directory.Delete(first, true); } catch { }
            try { Directory.Delete(second, true); } catch { }
        }
        Assert.Throws<FileNotFoundException>(() => BackupContent.Fingerprint(Path.Combine(Path.GetTempPath(), "nt-missing-" + Guid.NewGuid().ToString("N"))));
    }

    [Fact]
    public void BackupStatusClassifiesImports()
    {
        Assert.Equal(BackupImportStatus.New, BackupContent.Status(null, "abc"));
        Assert.Equal(BackupImportStatus.Identical, BackupContent.Status("abc", "abc"));
        Assert.Equal(BackupImportStatus.Conflict, BackupContent.Status("abc", "def"));
    }

    [Fact]
    public void BackupArchiveAcceptsAWellFormedListing()
    {
        BackupArchivePolicy.Validate("a/b.txt\nc.txt", "Archive: x\n2 files, 1000 bytes uncompressed", 2L * 1024 * 1024 * 1024);
    }

    [Fact]
    public void BackupArchiveRejectsUnsafePaths()
    {
        Assert.Throws<BackupArchiveException>(() => BackupArchivePolicy.Validate("../escape.txt", "1 file, 10 bytes uncompressed", 1L << 31));
        Assert.Throws<BackupArchiveException>(() => BackupArchivePolicy.Validate("/absolute.txt", "1 file, 10 bytes uncompressed", 1L << 31));
        Assert.Throws<BackupArchiveException>(() => BackupArchivePolicy.Validate("C:/windows.txt", "1 file, 10 bytes uncompressed", 1L << 31));
    }

    [Fact]
    public void BackupArchiveRejectsSymlinksDuplicatesAndBadCounts()
    {
        Assert.Throws<BackupArchiveException>(() => BackupArchivePolicy.Validate("a.txt", "lrwxr-xr-x a.txt\n1 file, 10 bytes uncompressed", 1L << 31));
        Assert.Throws<BackupArchiveException>(() => BackupArchivePolicy.Validate("a.txt\na.txt", "1 file, 10 bytes uncompressed", 1L << 31));
        Assert.Throws<BackupArchiveException>(() => BackupArchivePolicy.Validate("a.txt\nb.txt", "3 files, 10 bytes uncompressed", 1L << 31));
        Assert.Throws<BackupArchiveException>(() => BackupArchivePolicy.Validate("a.txt", "nonsense", 1L << 31));
    }

    [Fact]
    public void BackupArchiveRejectsResourceLimits()
    {
        Assert.Throws<BackupArchiveException>(() => BackupArchivePolicy.Validate("a.txt", "1 file, 70000000000 bytes uncompressed", 1L << 40));
        Assert.Throws<BackupArchiveException>(() => BackupArchivePolicy.Validate("a.txt", "1 file, 1000 bytes uncompressed", 100L * 1024 * 1024));
        Assert.Throws<BackupArchiveException>(() => BackupArchivePolicy.Validate("a.txt", "1 file, 600000000 bytes uncompressed", 700L * 1024 * 1024));
    }

    [Fact]
    public void OnboardingStepsClampAndNavigate()
    {
        Assert.Equal(OnboardingStep.Scene, OnboardingSteps.Clamped(-5));
        Assert.Equal(OnboardingStep.Start, OnboardingSteps.Clamped(99));
        Assert.Equal(OnboardingStep.Answers, OnboardingSteps.Clamped(2));
        Assert.Equal(OnboardingStep.Sound, OnboardingStep.Scene.Next());
        Assert.Null(OnboardingStep.Start.Next());
        Assert.Null(OnboardingStep.Scene.Previous());
        Assert.True(OnboardingStep.Start.IsLast());
        Assert.Equal(3, OnboardingStep.Answers.Position());
        Assert.True(OnboardingStep.Answers.IsSkippable());
        Assert.False(OnboardingStep.Scene.IsSkippable());
        Assert.Equal(90, OnboardingStep.Answers.EstimatedSeconds());
    }

    [Fact]
    public void OnboardingPendingListsWhatIsOpen()
    {
        Assert.Equal([OnboardingPending.Sound, OnboardingPending.Answers], OnboardingPendings.Open(false, false));
        Assert.Equal([OnboardingPending.Answers], OnboardingPendings.Open(true, false));
        Assert.Empty(OnboardingPendings.Open(true, true));
    }

    [Fact]
    public void WebLookupPromptBoundsEveryField()
    {
        var (system, user) = WebLookupPromptBuilder.MakeParts(
            $$"""{"question":"{{new string('q', 3000)}}","user_material":"{{new string('m', 1500)}}","recent_transcript":"{{new string('t', 2000)}}","today":"2026-11-18"}""");
        Assert.Equal(WebLookupPromptBuilder.Instructions, system);
        var parsed = JsonNode.Parse(user)!.AsObject();
        Assert.Equal(2000, parsed["question"]!.GetValue<string>().Length);
        Assert.Equal(1000, parsed["user_material"]!.GetValue<string>().Length);
        Assert.Equal(1200, parsed["recent_transcript"]!.GetValue<string>().Length);
        Assert.Equal("2026-11-18", parsed["today"]!.GetValue<string>());
    }

    [Fact]
    public void WebLookupPromptKeepsTheLastTwoTurnsAndTheMaterialBudget()
    {
        var user = """
            {
              "question": "q",
              "recent_answers": [
                {"question": "q1", "answer": "a1"},
                {"question": "q2", "answer": "a2"},
                {"question": "q3", "answer": "a3"}
              ],
              "retrieved_material": [
                {"text": "aaaaaaaaaa"},
                {"text": "bbbbbbbbbb"},
                {"text": "cccccccccc"}
              ]
            }
            """;
        var parsed = JsonNode.Parse(WebLookupPromptBuilder.MakeParts(user).User)!.AsObject();
        var turns = parsed["recent_answers"]!.AsArray();
        Assert.Equal(2, turns.Count);
        Assert.Equal("q2", turns[0]!["question"]!.GetValue<string>());
        Assert.Equal("q3", turns[1]!["question"]!.GetValue<string>());
        Assert.Equal(2, parsed["retrieved_material"]!.AsArray().Count);
    }

    [Fact]
    public void WebLookupPromptSurvivesMalformedInput()
    {
        var (_, user) = WebLookupPromptBuilder.MakeParts("not json at all");
        var parsed = JsonNode.Parse(user)!.AsObject();
        Assert.Equal("not json at all", parsed["question"]!.GetValue<string>());
    }

    [Fact]
    public void TranscriptLocatorPrefersTheExactLineId()
    {
        var start = new DateTimeOffset(2026, 11, 18, 10, 0, 0, TimeSpan.FromHours(8));
        var lines = new[]
        {
            new TranscriptLine { Date = start, StartedAt = start, EndedAt = start.AddSeconds(4), Text = "a" },
            new TranscriptLine { Date = start.AddSeconds(30), StartedAt = start.AddSeconds(30), EndedAt = start.AddSeconds(34), Text = "b" }
        };
        Assert.Equal(1, SessionTranscriptLocator.Index(lines[1].Id, 10, start, lines));
    }

    [Fact]
    public void TranscriptLocatorFindsTheLineContainingTheOffset()
    {
        var start = new DateTimeOffset(2026, 11, 18, 10, 0, 0, TimeSpan.FromHours(8));
        var lines = new[]
        {
            new TranscriptLine { Date = start, StartedAt = start, EndedAt = start.AddSeconds(4), Text = "a" },
            new TranscriptLine { Date = start.AddSeconds(10), StartedAt = start.AddSeconds(10), EndedAt = start.AddSeconds(14), Text = "b" }
        };
        Assert.Equal(1, SessionTranscriptLocator.Index(null, 12, start, lines));
        Assert.Equal(0, SessionTranscriptLocator.Index(null, 2, start, lines));
    }

    [Fact]
    public void TranscriptLocatorRefusesDistantOffsets()
    {
        var start = new DateTimeOffset(2026, 11, 18, 10, 0, 0, TimeSpan.FromHours(8));
        var lines = new[]
        {
            new TranscriptLine { Date = start, StartedAt = start, EndedAt = start.AddSeconds(4), Text = "a" }
        };
        Assert.Null(SessionTranscriptLocator.Index(null, 20, start, lines));
        Assert.Null(SessionTranscriptLocator.Index(null, -1, start, lines));
    }

    [Fact]
    public void TranscriptLocatorUsesThePrintedTimestampForLegacyLines()
    {
        var start = new DateTimeOffset(2026, 11, 18, 10, 0, 0, TimeSpan.FromHours(8));
        var lines = new[] { new TranscriptLine { Date = start.AddSeconds(8), Text = "legacy" } };
        Assert.Equal(0, SessionTranscriptLocator.Index(null, 8, start, lines));
        Assert.Null(SessionTranscriptLocator.Index(null, 15, start, lines));
    }
}
