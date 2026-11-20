namespace NextThought.Desktop;

public partial class AppState
{

    public bool OnHome => Section == AppSection.Home;

    public bool OnLive => Section == AppSection.Live;

    public bool OnNotes => Section == AppSection.Notes;

    public bool OnSettings => Section == AppSection.Settings;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private bool contextSidebarHidden;

    public bool ContextSidebarVisible => !ContextSidebarHidden && !Recording
        && Section is AppSection.Live or AppSection.Library or AppSection.Series;

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
