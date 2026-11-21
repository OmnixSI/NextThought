using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// §6.7 E25/E30: header fields for the series editor (WorkspaceEditor header: SceneGlyph + the
/// series name + SceneTag + SeriesStatsLine) and the skills-section empty state.
/// </summary>
public partial class AppState
{
    private WorkspaceSeries? EditorWorkspace => SelectedSeries is { } row ? workspace.Find(row.Id) : null;

    public MeetingMode SelectedSeriesMode => EditorWorkspace?.EffectiveMode ?? MeetingMode.General;

    public string SelectedSeriesName => SelectedSeries?.Name ?? "";

    public string SelectedSeriesTypeName => EditorWorkspace is { } w
        ? (w.Mode == "custom" && w.CustomTypeName is { Length: > 0 } custom
            ? custom
            : UiText.T(SessionModes.Parse(w.Mode).Title()))
        : "";

    /// <summary>WorkspaceEditor header: SeriesStatsLine plus the editor fallback for empty stats.</summary>
    public string SeriesEditorStatsLine => SelectedSeriesStatsLine.Length > 0
        ? SelectedSeriesStatsLine
        : UiText.Meetings(SelectedSeries?.Count ?? 0) + " · " + UiText.Materials(SelectedSeries?.DocumentCount ?? 0);

    public bool HasSeriesSkills => SeriesSkills.Count > 0;

    /// <summary>Mac state.openSettings(.skills) — the settings overlay consumes this request.</summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string settingsRequestedPage = "";
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string settingsRequestedAnchor = "";

    public void OpenSettingsPage(string page, string anchor = "")
    {
        SettingsRequestedAnchor = anchor;
        SettingsRequestedPage = page;
        Navigate("Settings");
    }

    /// <summary>Stage review in the knowledge section is always editable (Mac ReviewTab).</summary>
    public bool IsSeriesEditorReadOnly => false;
}
