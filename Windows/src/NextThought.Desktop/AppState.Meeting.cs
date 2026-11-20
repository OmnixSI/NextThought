namespace NextThought.Desktop;

public partial class AppState
{

    public string SceneTitle => SelectedScene?.Title ?? UiText.T("通用");

    partial void OnLiveModeChanged(LiveWindowMode value) => OnPropertyChanged(nameof(LiveModeLabel));

    public ObservableCollection<RecentMeetingRow> HomeRecent { get; } = [];

    private static bool IsNewMeetingTitle(string title) => title is "新会议" or "New meeting";

    public string LiveModeLabel => LiveMode.Title();
}
