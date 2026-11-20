namespace NextThought.Desktop;

public partial class AppState
{

    public AppSettings SettingsSnapshot => Settings.Build();

    public void SetStatus(string text, bool error = false)
    {
        Status = UiText.Translate(text, Settings.UiLanguage == "English");
        StatusIsError = error;
    }

    public void RefreshProcesses() => Settings.RefreshProcesses();

    private void LoadShortcutOverrides()
    {
        try { shortcutOverrides = new Dictionary<string, string>(SettingsStore.Load().Shortcuts ?? []); }
        catch { shortcutOverrides = []; }
        ShortcutText = BuildShortcutText();
    }

    private bool CanSaveSettings() => IsIdle;

    private void ApplyAppearance()
    {
        if (Avalonia.Application.Current is not { } app) return;
        app.RequestedThemeVariant = Settings.Build().Appearance switch
        {
            "dark" => Avalonia.Styling.ThemeVariant.Dark,
            "light" => Avalonia.Styling.ThemeVariant.Light,
            _ => Avalonia.Styling.ThemeVariant.Default
        };
    }
}
