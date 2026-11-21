using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// §3.5 series: WorkspacesView's left list search + sort and the three detail tabs
/// (记录 / 资料 / 经验积累). The list keeps the full <see cref="SeriesRows"/>; this partial owns the
/// filtered view and the selected series' documents for the 资料 tab.
/// </summary>
public partial class AppState
{
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string seriesQuery = "";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string seriesSort = "最近活动";

    public IReadOnlyList<string> SeriesSortOptions => [UiText.T("最近活动"), UiText.T("名称")];

    /// <summary>The left list after the search box and the sort picker (Mac WorkspaceList.shown).</summary>
    public System.Collections.ObjectModel.ObservableCollection<SeriesRow> SeriesShownRows { get; } = [];

    /// <summary>The selected series' documents for the 资料 tab (WorkspaceDocumentsTab read-only side).</summary>
    public System.Collections.ObjectModel.ObservableCollection<SeriesDocumentRow> SelectedSeriesDocuments { get; } = [];

    public bool HasSelectedSeriesDocuments => SelectedSeriesDocuments.Count > 0;

    partial void OnSeriesQueryChanged(string value) => RefreshSeriesShown();

    partial void OnSeriesSortChanged(string value) => RefreshSeriesShown();

    private void RefreshSeriesShown()
    {
        var term = SeriesQuery.Trim();
        IEnumerable<SeriesRow> items = SeriesRows;
        if (term.Length > 0)
        {
            // Mac matches name, type name, notes and document names.
            items = items.Where(row =>
            {
                var series = workspace.Find(row.Id);
                var haystack = new List<string> { row.Name, row.ModeTitle, row.Meta };
                if (series is { } found)
                {
                    haystack.Add(found.Notes ?? "");
                    haystack.AddRange(found.Documents.Select(document => document.Name));
                }
                return haystack.Any(text => text.Contains(term, StringComparison.OrdinalIgnoreCase));
            });
        }
        items = MatchesFilter(SeriesSort, "名称")
            ? items.OrderBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)
            : items;
        SeriesShownRows.Clear();
        foreach (var row in items) SeriesShownRows.Add(row);
        SelectedSeries = SeriesShownRows.FirstOrDefault(row => row.Id == SelectedSeries?.Id) ?? SeriesShownRows.FirstOrDefault();
    }

    /// <summary>Called whenever the selected series changes (or the workspace is reloaded).</summary>
    private void RefreshSeriesPanel()
    {
        SelectedSeriesDocuments.Clear();
        if (SelectedSeries is { } selected && workspace.Find(selected.Id) is { } series)
            foreach (var document in series.Documents)
                SelectedSeriesDocuments.Add(new SeriesDocumentRow(document));
        OnPropertyChanged(nameof(SelectedSeriesDocuments));
        OnPropertyChanged(nameof(HasSelectedSeriesDocuments));
    }
}
