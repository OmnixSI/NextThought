namespace NextThought.Desktop;

public partial class AppState
{

    public AppSettings SettingsSnapshot => Settings.Build();

    /// <summary>
    /// G8: applies an interface language at runtime. The markup extensions update by themselves; this
    /// also refreshes the code-built labels, lists and rows (used by the settings picker, the
    /// screenshots batch and the ui-language self-test).
    /// </summary>
    public void ApplyUiLanguage(bool english)
    {
        UiTextSource.Instance.SetEnglish(english);
        LanguageChanged?.Invoke(english);
    }

    public void SetStatus(string text, bool error = false)
    {
        Status = UiText.Translate(text, UiTextSource.Instance.IsEnglish);
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
