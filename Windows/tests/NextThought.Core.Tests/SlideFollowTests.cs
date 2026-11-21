using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// E9: the Mac slide-following rules — the manual page keeps until the screen shows another
/// slide, a lost match keeps the page for a grace period, the speaker's own script moves the
/// notes forward, and generated notes are cached by content fingerprint.
/// </summary>
public sealed class SlideFollowTests
{
    private static SlidePage Page(int number, string text, string notes = "") => new()
    {
        DocumentId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Page = number,
        PageCount = 3,
        DocumentName = "talk.pptx",
        Text = text,
        Notes = notes
    };

    private static SlidePage[] Deck() =>
    [
        Page(1, "Cache consistency read write ordering"),
        Page(2, "Write through and write back strategies"),
        Page(3, "Retry and fallback choices")
    ];

    [Fact]
    public void ScreenObservationShowsTheMatchingSlide()
    {
        var controller = new SlideFollowController(Deck());
        var page = controller.ObserveScreen("Cache consistency read write ordering", DateTimeOffset.UnixEpoch);
        Assert.Equal(1, page?.Page);
        Assert.Equal(SlideSource.Screen, controller.Source);
        Assert.False(controller.SlideIsManual);
    }

    [Fact]
    public void AManualStepPausesFollowingUntilAnotherSlideAppears()
    {
        var controller = new SlideFollowController(Deck());
        var now = DateTimeOffset.UnixEpoch;
        controller.ObserveScreen("Cache consistency read write ordering", now);
        var manual = controller.Step(1, now.AddSeconds(1));
        Assert.Equal(2, manual?.Page);
        Assert.True(controller.SlideIsManual);
        Assert.Equal(SlideSource.Manual, controller.Source);
        // The old slide is still on screen: the manual page wins.
        var held = controller.ObserveScreen("Cache consistency read write ordering", now.AddSeconds(2));
        Assert.Equal(2, held?.Page);
        Assert.True(controller.SlideIsManual);
        // A different slide shows up: automatic following resumes.
        var resumed = controller.ObserveScreen("Retry and fallback choices", now.AddSeconds(3));
        Assert.Equal(3, resumed?.Page);
        Assert.False(controller.SlideIsManual);
        Assert.Equal(SlideSource.Screen, controller.Source);
    }

    [Fact]
    public void ALostMatchKeepsThePageForTheGracePeriodOnly()
    {
        var controller = new SlideFollowController(Deck());
        var now = DateTimeOffset.UnixEpoch;
        controller.ObserveScreen("Write through and write back strategies", now);
        // A video / blank between slides: keep the page for 20 seconds.
        Assert.Equal(2, controller.ObserveScreen("unrelated demo text", now.AddSeconds(10))?.Page);
        Assert.Null(controller.ObserveScreen("unrelated demo text", now.AddSeconds(25)));
        Assert.Null(controller.Current);
    }

    [Fact]
    public void PresentingKeepsThePageLongerWhileTheScreenIsUnreadable()
    {
        var controller = new SlideFollowController(Deck());
        var now = DateTimeOffset.UnixEpoch;
        controller.ObserveScreen("Write through and write back strategies", now);
        // Mac: 120 s grace in presenter mode instead of 20 s.
        Assert.Equal(2, controller.ObserveScreen("unrelated demo text", now.AddSeconds(60), presenting: true)?.Page);
    }

    [Fact]
    public void SpeechFollowsTheNextScriptedPageForSubstantialSpeechOnly()
    {
        var controller = new SlideFollowController(Deck());
        var now = DateTimeOffset.UnixEpoch;
        controller.ObserveScreen("Cache consistency read write ordering", now);
        // Too little speech: nothing moves.
        Assert.Null(controller.ObserveSpeech("cache consistency", now));
        var page = controller.ObserveSpeech(
            "the first slide explains cache consistency and read write ordering in detail", now.AddSeconds(5));
        // No notes in the deck: Mac followSpeech needs a presenter script to compare.
        Assert.Null(page);
        var scripted = new[]
        {
            Page(1, "Cache consistency read write ordering",
                "First slide explains cache consistency and read write ordering in detail."),
            Page(2, "Write through and write back strategies",
                "Write through keeps the cache and the store in step on every write.\nWrite back updates the cache first and flushes later."),
        };
        var speechController = new SlideFollowController(scripted);
        speechController.ObserveScreen("Cache consistency read write ordering", now);
        var spoken = speechController.ObserveSpeech(
            "Write through keeps the cache and the store in step on every write. Write back updates the cache first and flushes later.",
            now.AddSeconds(5));
        Assert.Equal(2, spoken?.Page);
        Assert.Equal(SlideSource.Speech, speechController.Source);
    }

    [Fact]
    public void ReplacingTheDeckResetsAPageThatNoLongerExists()
    {
        var controller = new SlideFollowController(Deck());
        controller.ObserveScreen("Retry and fallback choices", DateTimeOffset.UnixEpoch);
        Assert.Equal(3, controller.Current?.Page);
        controller.ReplacePages([Page(1, "Cache consistency read write ordering")]);
        Assert.Null(controller.Current);
    }

    [Fact]
    public void ClassificationSeparatesCodeSlidesQuestionsAndDocuments()
    {
        Assert.Equal(ScreenKind.Code, ScreenClassification.Classify(
            "{\n  var value = 1;\n  return value;\n}\n{\n  x = y;\n}"));
        Assert.Equal(ScreenKind.Slides, ScreenClassification.Classify(
            "Title\nShort line\nAnother short line\nLast short"));
        Assert.Equal(ScreenKind.Question, ScreenClassification.Classify(
            "Which cache is safe?\nWhat happens on a write miss?"));
        Assert.Equal(ScreenKind.Document, ScreenClassification.Classify(
            string.Join("\n", Enumerable.Repeat(
                "This paragraph is long enough to look like a document page rather than a slide, and it keeps going for a while.",
                4))));
        Assert.Equal("video_call", ScreenKind.VideoCall.Raw());
        Assert.Equal("other", ScreenKind.Other.Raw());
    }

    [Fact]
    public void NotesCacheRoundTripsByFingerprintAndRejectsAChangedSlide()
    {
        var page = Page(2, "Write through and write back strategies");
        var directory = Path.Combine(Path.GetTempPath(), "nt-slide-follow-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var notes = new SlideNotes { Points = ["write through", "write back"], Transition = "Retry" };
            var fingerprint = SlideNotesCacheStore.Fingerprint(page, "meeting");
            SlideNotesCacheStore.Save(directory, page, notes, fingerprint);
            var loaded = SlideNotesCacheStore.Notes(directory, page, fingerprint);
            Assert.NotNull(loaded);
            Assert.Equal(["write through", "write back"], loaded!.Points);
            Assert.Equal("Retry", loaded.Transition);
            // The slide text changed: the old notes are stale.
            Assert.Null(SlideNotesCacheStore.Notes(directory, page, "different-fingerprint"));
            // Another mode has its own cache.
            Assert.NotEqual(fingerprint, SlideNotesCacheStore.Fingerprint(page, "interview"));
        }
        finally
        {
            try { Directory.Delete(directory, true); } catch (IOException) { }
        }
    }

    [Fact]
    public void CorruptNotesCacheIsIgnored()
    {
        var page = Page(1, "Cache consistency read write ordering");
        var directory = Path.Combine(Path.GetTempPath(), "nt-slide-follow-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, page.DocumentId.ToString("D").ToUpperInvariant() + ".json"), "{not json");
            Assert.Null(SlideNotesCacheStore.Notes(directory, page, "fingerprint"));
        }
        finally
        {
            try { Directory.Delete(directory, true); } catch (IOException) { }
        }
    }
}
