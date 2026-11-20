using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NextThought.Desktop;

public partial class AppState
{
    [ObservableProperty] private string seriesEditorName = "";
    [ObservableProperty] private string seriesEditorMode = "";
    [ObservableProperty] private string seriesEditorStyle = "";
    [ObservableProperty] private string seriesEditorPreferences = "";
    [ObservableProperty] private bool seriesEditorUseKnowledge = true;
    [ObservableProperty] private string seriesEditorFieldsText = "";
    [ObservableProperty] private string seriesEditorStageReview = "";

    public void LoadSeriesEditor()
    {
        if (SelectedSeries is not { } selected || workspace.Find(selected.Id) is not { } series) return;
        SeriesEditorName = series.Name;
        SeriesEditorMode = series.Mode;
        SeriesEditorStyle = series.Style ?? "";
        SeriesEditorPreferences = series.Preferences ?? "";
        SeriesEditorUseKnowledge = series.UseKnowledge;
        SeriesEditorFieldsText = string.Join("\n", (series.Fields ?? new Dictionary<string, string>())
            .OrderBy(pair => pair.Key).Select(pair => pair.Key + "=" + pair.Value));
        SeriesEditorStageReview = series.StageReview ?? "";
        SeriesEditorDocuments.Clear();
        foreach (var document in series.Documents) SeriesEditorDocuments.Add(document);
    }


    public System.Collections.ObjectModel.ObservableCollection<NextThought.Core.ContextDocument> SeriesEditorDocuments { get; } = [];

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void RemoveSeriesDocument(NextThought.Core.ContextDocument? document)
    {
        if (document is null || SelectedSeries is not { } selected || workspace.Find(selected.Id) is not { } series) return;
        workspace = workspace.WithSeries(series with
        {
            Documents = series.Documents.Where(item => item.Id != document.Id).ToList(),
            DocumentIds = series.DocumentIds.Where(id => id != document.Id).ToList()
        });
        SaveWorkspace();
        RefreshSeries();
        SeriesEditorDocuments.Remove(document);
        SetStatus("系列文档已移除。");
    }


    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void ArchiveSeries()
    {
        if (SelectedSeries is not { } selected || workspace.Find(selected.Id) is not { } series) return;
        workspace = workspace.WithSeries(series with { Archived = true });
        SaveWorkspace();
        RefreshSeries();
        SetStatus("系列已归档。");
    }

    [RelayCommand]
    private void SaveSeriesEditor()
    {
        if (SelectedSeries is not { } selected || workspace.Find(selected.Id) is not { } series) return;
        var name = SeriesEditorName.Trim();
        if (name.Length == 0) { SetStatus("系列名不能为空。", true); return; }
        workspace = workspace.WithSeries(series with
        {
            Name = name,
            Mode = SeriesEditorMode.Trim(),
            Style = SeriesEditorStyle.Trim(),
            Preferences = SeriesEditorPreferences.Trim(),
            UseKnowledge = SeriesEditorUseKnowledge,
            Fields = SeriesEditorFieldsText.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Split('=', 2))
                .Where(parts => parts.Length == 2 && parts[0].Trim().Length > 0)
                .GroupBy(parts => parts[0].Trim())
                .ToDictionary(group => group.Key, group => group.Last()[1].Trim()),
            StageReview = SeriesEditorStageReview.Trim().Length == 0 ? null : SeriesEditorStageReview.Trim(),
            StageReviewAt = SeriesEditorStageReview.Trim().Length == 0 ? null : DateTimeOffset.UtcNow
        });
        SaveWorkspace();
        RefreshSeries();
        SetStatus("系列信息已保存。");
    }
}
