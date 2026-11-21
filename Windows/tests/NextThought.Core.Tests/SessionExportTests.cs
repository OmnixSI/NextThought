using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for SessionSubtitleExporter (SessionExport.swift).</summary>
public sealed class SessionExportTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 18, 10, 0, 0, TimeSpan.FromHours(8));

    private static SessionMeta Meta() => new() { Id = Guid.NewGuid(), Title = "组会", StartedAt = Start };

    private static TranscriptLine Line(int startSeconds, int? endSeconds, string text, string speaker = "remote", string? translation = null) => new()
    {
        Date = Start.AddSeconds(startSeconds),
        StartedAt = Start.AddSeconds(startSeconds),
        EndedAt = endSeconds is { } end ? Start.AddSeconds(end) : null,
        Text = text,
        Speaker = speaker,
        Translation = translation is null ? null : new SessionTranslation(Guid.NewGuid(), 1, "hash", "zh-en", translation, "test", Start)
    };

    [Fact]
    public void SubtitleContentSelectsOriginalTranslationOrBoth()
    {
        var lines = new[] { Line(0, 2, "original", "me", "translated") };
        var original = SessionSubtitleExporter.Export(Meta(), lines, SessionSubtitleFormat.Srt, SessionSubtitleContent.Original).Text;
        Assert.Contains("original", original);
        Assert.DoesNotContain("translated", original);
        var translated = SessionSubtitleExporter.Export(Meta(), lines, SessionSubtitleFormat.Srt, SessionSubtitleContent.Translation).Text;
        Assert.Contains("translated", translated);
        Assert.DoesNotContain("original", translated);
        var both = SessionSubtitleExporter.Export(Meta(), lines, SessionSubtitleFormat.Srt, SessionSubtitleContent.Bilingual).Text;
        Assert.Contains("original", both);
        Assert.Contains("translated", both);
    }

    [Fact]
    public void SrtHasSequentialCuesAndCommaTimestamps()
    {
        var export = SessionSubtitleExporter.Export(Meta(),
        [
            Line(0, 3, "先说结论", speaker: "me"),
            Line(4, 8, "再说依据")
        ], SessionSubtitleFormat.Srt);
        Assert.Equal("1\n00:00:00,000 --> 00:00:03,000\n我：先说结论\n\n2\n00:00:04,000 --> 00:00:08,000\n对方：再说依据\n",
            export.Text);
        Assert.Equal(0, export.EstimatedTimingCount);
    }

    [Fact]
    public void VttStartsWithTheHeaderAndUsesDots()
    {
        var export = SessionSubtitleExporter.Export(Meta(), [Line(1, 2, "hello")], SessionSubtitleFormat.Vtt);
        Assert.StartsWith("WEBVTT\n\n", export.Text);
        Assert.Contains("00:00:01.000 --> 00:00:02.000", export.Text);
        Assert.DoesNotContain("00:00:01,000", export.Text);
    }

    [Fact]
    public void MissingEndTimeIsEstimatedFromTextLength()
    {
        var export = SessionSubtitleExporter.Export(Meta(), [Line(0, null, "一二三四五六七八九十")], SessionSubtitleFormat.Srt);
        Assert.Equal(1, export.EstimatedTimingCount);
        // Ten characters / 5 = 2 seconds.
        Assert.Contains("00:00:00,000 --> 00:00:02,000", export.Text);
    }

    [Fact]
    public void EstimateIsCappedByTheNextLine()
    {
        var export = SessionSubtitleExporter.Export(Meta(),
        [
            Line(0, null, new string('字', 200)),
            Line(3, 5, "下一句")
        ], SessionSubtitleFormat.Srt);
        Assert.Contains("00:00:00,000 --> 00:00:03,000", export.Text);
    }

    [Fact]
    public void EstimateKeepsAtLeastAQuarterSecond()
    {
        var export = SessionSubtitleExporter.Export(Meta(),
        [
            Line(0, null, "短"),
            new TranscriptLine
            {
                Date = Start.AddMilliseconds(100),
                StartedAt = Start.AddMilliseconds(100),
                EndedAt = Start.AddSeconds(5),
                Text = "紧接"
            }
        ], SessionSubtitleFormat.Srt);
        Assert.Contains("00:00:00,000 --> 00:00:00,250", export.Text);
    }

    [Fact]
    public void TranslationIsAppendedAsASecondLine()
    {
        var export = SessionSubtitleExporter.Export(Meta(), [Line(0, 2, "先说结论", translation: "Start with the conclusion")], SessionSubtitleFormat.Srt);
        Assert.Contains("对方：先说结论\n译文：Start with the conclusion", export.Text);
    }

    [Fact]
    public void EmptyTranscriptProducesAnEmptyFile()
    {
        Assert.Equal("", SessionSubtitleExporter.Export(Meta(), [], SessionSubtitleFormat.Srt).Text);
        Assert.Equal("WEBVTT\n\n", SessionSubtitleExporter.Export(Meta(), [], SessionSubtitleFormat.Vtt).Text);
    }

    [Theory]
    [InlineData(0, ':', "00:00:00")]
    [InlineData(3661.5, '.', "01:01:01")]
    [InlineData(59.999, ',', "00:00:59")]
    public void TimestampsPadHoursMinutesSeconds(double seconds, char separator, string prefix)
    {
        var stamp = SessionSubtitleExporter.Stamp(seconds, separator);
        Assert.StartsWith(prefix + separator, stamp);
        Assert.Equal(3, stamp.Split(separator)[^1].Length);
    }

    [Fact]
    public void SpeakerLabelsMatchTheInterface()
    {
        Assert.Equal("对方", SessionSubtitleExporter.SpeakerLabel("remote"));
        Assert.Equal("现场", SessionSubtitleExporter.SpeakerLabel("room"));
        Assert.Equal("我", SessionSubtitleExporter.SpeakerLabel("me"));
        Assert.Equal("guest", SessionSubtitleExporter.SpeakerLabel("guest"));
    }
}
