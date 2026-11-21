using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E20/§3.5 series-editor 3 and 6: the 导入资料 menu is the Mac one (自动归类 or 导入到 a material
/// slot, classified by the Core document classifier), and leaving a dirty basic/requirements draft
/// asks save / discard / keep editing before switching sections or closing the window.
/// </summary>
public sealed class SeriesEditorClosureTests
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
    public void TheImportButtonOffersAutoCategorizeAndOneSlotPerCategory()
    {
        var state = Read("AppState.SeriesEditor.cs");
        Assert.Contains("SeriesEditorImportCategories", state, StringComparison.Ordinal);
        Assert.Contains("ImportSeriesDocumentsAsync(IReadOnlyList<string> paths, string? category = null)", state, StringComparison.Ordinal);
        Assert.Contains("DocumentClassifier.Classify", state, StringComparison.Ordinal);
        Assert.Contains("SeriesMaterials.Attach(series, material,", state, StringComparison.Ordinal);
        var window = Read("Views", "SeriesEditorWindow.axaml.cs");
        Assert.Contains("MenuFlyout", window, StringComparison.Ordinal);
        Assert.Contains("UiText.T(\"自动归类\")", window, StringComparison.Ordinal);
        Assert.Contains("UiText.Format(\"导入到 %@\", category)", window, StringComparison.Ordinal);
        var xaml = Read("Views", "SeriesEditorWindow.axaml");
        Assert.Contains("按内容自动归类", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SwitchingSectionsOrClosingADirtyDraftAsksFirst()
    {
        var state = Read("AppState.SeriesEditor.cs");
        Assert.Contains("SeriesEditorHasChanges", state, StringComparison.Ordinal);
        Assert.Contains("MarkSeriesEditorClean()", state, StringComparison.Ordinal);
        Assert.Contains("ShowSeriesEditorUnsaved()", state, StringComparison.Ordinal);
        // The basic tab's custom type name and background notes are persisted, not only bound.
        Assert.Contains("Notes = SeriesEditorNotes.Trim()", state, StringComparison.Ordinal);
        Assert.Contains("CustomTypeName = SeriesEditorCustomTypeName.Trim()", state, StringComparison.Ordinal);
        Assert.Contains("SeriesEditorNotes = series.Notes ?? \"\"", state, StringComparison.Ordinal);

        var window = Read("Views", "SeriesEditorWindow.axaml.cs");
        Assert.Contains("OnClosing", window, StringComparison.Ordinal);
        Assert.Contains("e.Cancel = true", window, StringComparison.Ordinal);
        Assert.Contains("RequestSection(", window, StringComparison.Ordinal);
        Assert.Contains("SeriesEditorHasChanges", window, StringComparison.Ordinal);
        Assert.Contains("LoadSeriesEditor()", window, StringComparison.Ordinal);

        var xaml = Read("Views", "SeriesEditorWindow.axaml");
        Assert.Contains("{Binding ShowSeriesEditorUnsavedPrompt}", xaml, StringComparison.Ordinal);
        Assert.Contains("{loc:Tr 有未保存的修改}", xaml, StringComparison.Ordinal);
        Assert.Contains("{loc:Tr 保存后继续刚才的操作；放弃更改会丢弃本次草稿。}", xaml, StringComparison.Ordinal);
        Assert.Contains("{loc:Tr 保存更改}", xaml, StringComparison.Ordinal);
        Assert.Contains("{loc:Tr 放弃更改}", xaml, StringComparison.Ordinal);
        Assert.Contains("{loc:Tr 继续编辑}", xaml, StringComparison.Ordinal);
        Assert.Contains("SaveUnsavedSeries_Click", xaml, StringComparison.Ordinal);
        Assert.Contains("DiscardUnsavedSeries_Click", xaml, StringComparison.Ordinal);
        Assert.Contains("KeepEditingSeries_Click", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSeriesMaterialsAttachKeepsTheChosenSlotWhereMacDoes()
    {
        var materials = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Core", "SeriesMaterials.cs"));
        Assert.Contains("Attach(WorkspaceSeries series, MaterialDocument material, string? category = null)", materials, StringComparison.Ordinal);
        Assert.Contains("document with { Category = category }", materials, StringComparison.Ordinal);
    }
}
