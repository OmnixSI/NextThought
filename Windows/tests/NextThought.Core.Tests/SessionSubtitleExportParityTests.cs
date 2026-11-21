using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>B16: translated from SessionExportTests.swift subtitle rules.</summary>
public sealed class SessionSubtitleExportParityTests
{
    private static readonly SessionMeta Meta = new()
    {
        StartedAt = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000),
        EndedAt = DateTimeOffset.FromUnixTimeSeconds(1_800_000_100)
    };

    private static SessionProjectedTranscriptLine Line(string corrected, string? translation = null,
        bool stale = false, string label = "对方") => new()
    {
        Id = Guid.NewGuid(), Offset = 65, EndedAt = Meta.StartedAt.AddSeconds(70),
        SpeakerLabel = label, Speaker = "remote", OriginalText = corrected, CorrectedText = corrected,
        TranslationText = translation, TranslationIsStale = stale, SourceRevision = "r"
    };

    [Fact]
    public void TranslationModeFallsBackToSourceAndCountsStaleCues()
    {
        var lines = new[]
        {
            Line("周三上线", "Go live Wednesday", stale: true),
            Line("先确认范围", null)
        };
        var exported = SessionSubtitleExporter.ExportProjected(Meta, lines, SessionSubtitleFormat.Srt,
            SessionSubtitleContent.Translation);
        Assert.Contains("对方 · 译文（可能已过期）：Go live Wednesday", exported.Text);
        Assert.Contains("对方 · 无译文，显示原文：先确认范围", exported.Text);
        Assert.Equal(1, exported.StaleTranslationCount);
        Assert.Contains("00:01:05,000 --> 00:01:10,000", exported.Text);
    }

    [Fact]
    public void BilingualAndOriginalModesUseTheProjectedSpeakerAndText()
    {
        var line = Line("我们用 Kubernetes 部署", "We deploy with Kubernetes", label: "小王");
        var bilingual = SessionSubtitleExporter.ExportProjected(Meta, [line], SessionSubtitleFormat.Vtt);
        Assert.StartsWith("WEBVTT\n\n00:01:05.000 --> 00:01:10.000\n小王：我们用 Kubernetes 部署\n译文：We deploy with Kubernetes", bilingual.Text);
        var original = SessionSubtitleExporter.ExportProjected(Meta, [line], SessionSubtitleFormat.Srt, SessionSubtitleContent.Original);
        Assert.Contains("小王：我们用 Kubernetes 部署", original.Text);
        Assert.DoesNotContain("We deploy with Kubernetes", original.Text);
    }
}
