using System.Text.Json;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from RealtimeRegressionTests.swift: duplicate expiry and punctuation-free
/// follow-ups, stale fragment handling, manual questions, overlap trimming, prompt JSON,
/// shortcut validation and the segmenter's overlap-only flush.
/// </summary>
public sealed class RealtimeRegressionParityTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(10_000);

    [Fact]
    public void DuplicateExpiresAndFollowUpsWorkWithoutPunctuation()
    {
        var detector = new QuestionDetector();
        Assert.Equal("然后呢", detector.Detect("然后呢", Now));
        Assert.Null(detector.Detect("然后呢", Now.AddSeconds(1)));
        Assert.Equal("然后呢", detector.Detect("然后呢", Now.AddSeconds(16)));
        Assert.Equal("why", detector.Detect("why", Now));
    }

    [Fact]
    public void StaleFragmentsDoNotAttachToLaterStatements()
    {
        var detector = new QuestionDetector();
        Assert.Null(detector.Detect("Can you", Now));
        Assert.Null(detector.Detect("The meeting has ended", Now.AddSeconds(9)));
    }

    [Fact]
    public void DifferentQuestionsWithSimilarWordsAreNotDeleted()
    {
        var detector = new QuestionDetector();
        Assert.NotNull(detector.Detect("Why does cache A outperform cache B?", Now));
        Assert.NotNull(detector.Detect("Why does cache B outperform cache A?", Now));
    }

    [Fact]
    public void ManualTriggerUsesLiveTextAndBypassesQuestionHeuristics()
    {
        Assert.Equal("Custom", ManualQuestionBuilder.Make("  Custom  ", "live", ["old"]));
        Assert.True(ManualQuestionBuilder.Make("", "ordinary statement", [])?.Contains("ordinary statement") == true);
        Assert.Null(ManualQuestionBuilder.Make("", "", []));
    }

    [Fact]
    public void RepeatedSpeechIsOnlyTrimmedForOverlappingAudio()
    {
        Assert.Equal("Why?",
            TranscriptDeduplicator.RemoveOverlappingPrefix("Why?", "Why?", Now, Now.AddSeconds(1)));
        Assert.Equal("",
            TranscriptDeduplicator.RemoveOverlappingPrefix("Why?", "Why?", Now, Now.AddSeconds(-1)));
    }

    [Fact(
        Skip = "Blocked: the C# port has no SessionContextIndex/ContextChunker overload over ContextDocument "
             + "(Pages/Enabled), so disabled-file filtering cannot be exercised; SessionContextIndex only "
             + "accepts MaterialDocument, which has no Enabled flag.")]
    public void RetrievalSupportsChineseEnglishPageReferencesAndDisabledFiles()
    {
        // Intentionally not translated: the required C# API does not exist yet.
    }

    [Fact]
    public void CustomPreferencesAreSeparateAndFreshContextIsKept()
    {
        var context = string.Concat(Enumerable.Repeat("old ", 4_000)) + "LATEST";
        var prompt = AnswerPromptBuilder.Make(
            question: "why", context: context, profile: "data", turns: [], hasScreenshot: false,
            preferences: "Use English", mode: MeetingMode.Lesson);
        var json = prompt[(AnswerPromptBuilder.Instructions().Length + 2)..];
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("Use English", root.GetProperty("user_preferences").GetString());
        Assert.Equal("lesson", root.GetProperty("session_mode").GetString());
        Assert.EndsWith("LATEST", root.GetProperty("recent_transcript").GetString());
    }

    [Fact]
    public void RejectsShortcutCollisionsAndUnmodifiedKeys()
    {
        Assert.NotNull(ShortcutBinding.Validate(new Dictionary<string, ShortcutBinding>
        {
            ["a"] = new ShortcutBinding(36),
            ["b"] = new ShortcutBinding(36)
        }));
        Assert.False(new ShortcutBinding(0, Command: false).IsSafe);
        Assert.Null(ShortcutBinding.Validate(new Dictionary<string, ShortcutBinding>
        {
            ["a"] = new ShortcutBinding(36),
            ["b"] = new ShortcutBinding(35)
        }));
    }

    [Fact]
    public void OverlapAloneIsNotFlushedAsANewUtterance()
    {
        var segmenter = new VoiceActivitySegmenter();
        var chunk = Enumerable.Repeat((short)5_000, 1_600).ToArray();
        for (var index = 0; index < 120; index++) segmenter.Append(chunk);
        Assert.Null(segmenter.Flush());
    }
}
