using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for export format names and extensions.</summary>
public sealed class SessionExportFormatTests
{
    [Fact]
    public void ExtensionsUseMdForMarkdown()
    {
        Assert.Equal("md", SessionExportFormat.Markdown.FileExtension());
        Assert.Equal("docx", SessionExportFormat.Docx.FileExtension());
        Assert.Equal("pdf", SessionExportFormat.Pdf.FileExtension());
        Assert.Equal("srt", SessionExportFormat.Srt.FileExtension());
        Assert.Equal("vtt", SessionExportFormat.Vtt.FileExtension());
    }

    [Fact]
    public void TitlesAreUserFacing()
    {
        Assert.Equal("Markdown", SessionExportFormat.Markdown.Title());
        Assert.Equal("Word 文档", SessionExportFormat.Docx.Title());
        Assert.Equal("PDF", SessionExportFormat.Pdf.Title());
        Assert.Equal("SRT 字幕", SessionExportFormat.Srt.Title());
        Assert.Equal("WebVTT 字幕", SessionExportFormat.Vtt.Title());
    }

    [Fact]
    public void EveryFormatHasAUniqueExtensionAndTitle()
    {
        var formats = Enum.GetValues<SessionExportFormat>();
        Assert.Equal(formats.Length, formats.Select(format => format.FileExtension()).Distinct().Count());
        Assert.Equal(formats.Length, formats.Select(format => format.Title()).Distinct().Count());
    }
}
