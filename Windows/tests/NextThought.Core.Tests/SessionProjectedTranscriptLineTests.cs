using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for projected transcript lines and their export integration.</summary>
public sealed class SessionProjectedTranscriptLineTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 18, 9, 0, 0, TimeSpan.FromHours(8));
    private static readonly TranscriptLine Line = new()
    {
        Text = "我们用库伯奈提斯部署",
        Speaker = "remote",
        Date = Start.AddSeconds(65),
        StartedAt = Start.AddSeconds(65)
    };

    private static readonly TranscriptCorrection Correction = new()
    {
        Line = 0,
        LineId = Line.Id,
        Original = "库伯奈提斯",
        Corrected = "Kubernetes",
        Source = TranscriptCorrectionSource.Model
    };

    [Fact]
    public void CorrectedVariantUsesTheCorrectedTextAndShowsAppliedCorrections()
    {
        var projected = SessionProjectedTranscriptLine.From(Line, "我们用Kubernetes部署", Start,
            new SessionProjectionOptions(), appliedCorrections: [Correction]);
        Assert.Equal("我们用Kubernetes部署", projected.DisplayText);
        Assert.Equal(SessionProjectedTranscriptLine.From(Line, Line.Text, Start, new SessionProjectionOptions()).OriginalRevision,
            projected.OriginalRevision);
        Assert.NotEqual(projected.OriginalRevision, projected.SourceRevision);
        Assert.Single(projected.AppliedCorrections);
        Assert.Equal("对方", projected.SpeakerLabel);
    }

    [Fact]
    public void OriginalVariantKeepsTheStoredTextAndHidesCorrections()
    {
        var projected = SessionProjectedTranscriptLine.From(Line, "我们用Kubernetes部署", Start,
            new SessionProjectionOptions { TranscriptVariant = SessionTranscriptVariant.Original },
            appliedCorrections: [Correction]);
        Assert.Equal(Line.Text, projected.DisplayText);
        Assert.Empty(projected.AppliedCorrections);
    }

    [Fact]
    public void TranslatedViewPrefersANonEmptyTranslation()
    {
        var withTranslation = Line with
        {
            Translation = new SessionTranslation(Guid.NewGuid(), 2, "hash", "zh-en", "We deploy with Kubernetes", "model", Start)
        };
        var projected = SessionProjectedTranscriptLine.From(withTranslation, withTranslation.Text, Start,
            new SessionProjectionOptions { LanguageView = SessionLanguageView.Translated });
        Assert.Equal("We deploy with Kubernetes", projected.DisplayText);
        Assert.Equal("translation-2", projected.TranslationInputRevision);
    }

    [Fact]
    public void OffsetIsRelativeToTheMeetingStart()
    {
        var projected = SessionProjectedTranscriptLine.From(Line, Line.Text, Start, new SessionProjectionOptions());
        Assert.Equal(65, projected.Offset);
    }

    [Fact]
    public void BuilderAppliesCorrectionsAndPopulatesTheTranscript()
    {
        var projection = SessionProjectionBuilder.Build(Guid.NewGuid(), Start, [Line], [], corrections: [Correction]);
        var projected = Assert.Single(projection.Transcript);
        Assert.Equal("我们用Kubernetes部署", projected.DisplayText);
        var text = Assert.Single(projection.SearchableText);
        Assert.Equal("我们用Kubernetes部署", text.Text);
        Assert.Equal(Line.Text, text.AlternateText);
    }

    [Fact]
    public void ExporterUsesTheProjectedTranscriptWhenPresent()
    {
        var projection = SessionProjectionBuilder.Build(Guid.NewGuid(), Start, [Line], [], corrections: [Correction]);
        var markdown = SessionMarkdownExporter.Export("标题", "会议", Start, 60, "", projection, []);
        Assert.Contains("- `01:05` **对方**：我们用Kubernetes部署", markdown);
        Assert.DoesNotContain("库伯奈提斯", markdown);
    }
}
