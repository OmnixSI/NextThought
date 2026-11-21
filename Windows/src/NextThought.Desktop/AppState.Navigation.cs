namespace NextThought.Desktop;

public partial class AppState
{

    public bool OnHome => Section == AppSection.Home;

    public bool OnLive => Section == AppSection.Live;

    public bool OnPrepare => Section == AppSection.Prepare;

    public bool OnNotes => Section == AppSection.Notes;

    public bool OnSettings => Section == AppSection.Settings;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private bool contextSidebarHidden;

    /// <summary>§5.2: the title row shows the context-sidebar toggle only on 所有记录 and 系列
    /// (Mac AppNavigationSidebar: meetings and library sections).</summary>
    public bool ShowsContextSidebarToggle => Section is AppSection.History or AppSection.Series;

    public bool ContextSidebarVisible => !ContextSidebarHidden && !Recording
        && Section is AppSection.History or AppSection.Series;

    partial void OnContextSidebarHiddenChanged(bool value) => OnPropertyChanged(nameof(ContextSidebarVisible));

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private async Task ToggleContextSidebarAsync()
    {
        ContextSidebarHidden = !ContextSidebarHidden;
        try
        {
            var settings = SettingsStore.Load();
            settings.ContextSidebarHidden = ContextSidebarHidden;
            await SettingsStore.SaveAsync(settings);
        }
        catch { }
    }
}
