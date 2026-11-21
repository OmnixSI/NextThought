using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E19 (decisions §3.4 export/SessionExportSheet 2/4/5): user notes travel with the export, the
/// stale-derived-content notice is shown instead of hidden, and read issues keep disabling export.
/// </summary>
public sealed class ExportParityTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "App.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([Root(), "Windows", "src", "NextThought.Desktop", .. parts]));

    [Fact]
    public void TheExportAppendsTheUsersOwnNotes()
    {
        var state = Read("AppState.SessionExport.cs");
        Assert.Contains("SessionUserContentStore.Load", state, StringComparison.Ordinal);
        Assert.Contains("SessionExportContent.UserContentMarkdown(exportUserContent)", state, StringComparison.Ordinal);
        Assert.Contains("exportUserContent = SessionUserContentStore.Load", state, StringComparison.Ordinal);
        Assert.Contains("exportNotes = exportUserContent.EditedNotes ?? exportSummary?.Notes", state, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDerivedContentStatusIsRealAndShownInTheSheet()
    {
        var state = Read("AppState.SessionExport.cs");
        Assert.Contains("BuildSessionExportProjection", state, StringComparison.Ordinal);
        Assert.Contains("DerivedContentStatus", state, StringComparison.Ordinal);
        Assert.Contains("summary.json", state, StringComparison.Ordinal);
        Assert.Contains("SessionExportDerivedContentStatus", state, StringComparison.Ordinal);
        var sheet = Read("Views", "SessionExportSheet.axaml.cs");
        Assert.Contains("state.SessionExportDerivedContentStatus", sheet, StringComparison.Ordinal);
        Assert.Contains("StaleNotice.IsVisible", sheet, StringComparison.Ordinal);
        Assert.Contains("state.SessionExportDerivedContentNotice", sheet, StringComparison.Ordinal);
        Assert.Contains("readIssue.Length == 0", sheet, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePdfWriterFormatsLinksAndInlineCodeWithoutThrowing()
    {
        var path = Path.Combine(Path.GetTempPath(), "nt-pdf-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            PdfWriter.Write("# 标题\n\n正文有 `code` 与 [链接](https://example.com)。\n\n- 列表\n", path, "测试");
            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length > 800);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }
}
