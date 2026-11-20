namespace NextThought.Desktop;

public partial class AppState
{

    public string IslandHeadline => IslandLines.Count > 0 ? IslandLines[0].Spoken : "";

    public string IslandDetail => IslandLines.Count > 1
        ? string.Join(UiText.EnglishUi ? " " : "　", IslandLines.Skip(1).Select(line => line.Spoken))
        : "";

    partial void OnWorkingChanged(bool value) => RaiseBusy();

    partial void OnStoppingChanged(bool value) => RaiseBusy();

    partial void OnSummarizingChanged(bool value) => RaiseBusy();

    public string InitTimings { get; private set; } = "";

    public event Action? Initialized;

    private void OnInitialized() => Initialized?.Invoke();

    [ObservableProperty] private string coachHint = "";
    public bool HasCoachHint => CoachHint.Length > 0;

    partial void OnCoachHintChanged(string value) => OnPropertyChanged(nameof(HasCoachHint));

    private IWebSearchProvider? webSearchProvider;

    private bool webSearchResolved;

    /// <summary>E14: verification results for automatic background lookups (stale after 30 minutes).</summary>
    private readonly WebVerificationCache webVerificationCache = new();

    private IWebSearchProvider? WebSearch()
    {
        if (webSearchResolved) return webSearchProvider;
        webSearchResolved = true;
        try { webSearchProvider = WebSearchConfiguration.CreateProvider(keyOverride: SettingsStore.ReadTavilyKey()); }
        catch { webSearchProvider = null; }
        return webSearchProvider;
    }

    /// <summary>E14: 密钥状态回显 — whether a key (settings or TAVILY_API_KEY) can drive web search.</summary>
    public bool IsWebSearchConfigured => WebSearch() is not null;

    public string WebSearchProviderLabel => WebSearch()?.Provider ?? "";

    public bool HasStoredWebSearchKey => SettingsStore.ReadTavilyKey().Trim().Length > 0;

    public int WebVerificationCacheCount => webVerificationCache.Count;

    /// <summary>The cache itself, for the self-check (no network calls).</summary>
    public WebVerificationCache VerificationCacheForCheck => webVerificationCache;

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void ClearWebVerificationCache()
    {
        webVerificationCache.Invalidate();
        OnPropertyChanged(nameof(WebVerificationCacheCount));
        SetStatus(UiText.T("联网查证缓存已清空。"));
    }

    public string WebSearchKeyStatusLine => HasStoredWebSearchKey
        ? UiText.T("Tavily 密钥已保存（当前 Windows 用户加密）。")
        : UiText.T("未填写");

    /// <summary>E14: 密钥状态回显 for the settings page (provider name or the setup hint).</summary>
    public string WebSearchAvailabilityText => IsWebSearchConfigured
        ? WebSearchKeyStatusLine + " · " + WebSearchProviderLabel
        : UiText.T("联网未配置：先在设置里保存 Tavily 密钥。");

    /// <summary>E14: the last automatic answer used cached verification results.</summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private bool lastWebSearchUsedCache;

    private bool CanAsk() => !Working && !Stopping;

    private int slideIndex = -1;

    public string SlideStatus => slides.Count == 0
        ? UiText.T("未导入 PPT")
        : slideIndex < 0
            ? UiText.Format("%lld 页", slides.Count)
            : UiText.Format("第 %lld 页，共 %lld 页", slides[slideIndex].Number, slides.Count);

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string shortcutConflicts = "";
    public bool HasShortcutConflicts => ShortcutConflicts.Length > 0;
    partial void OnShortcutConflictsChanged(string value) => OnPropertyChanged(nameof(HasShortcutConflicts));

    private string BuildShortcutText()
    {
        var lines = new List<string>();
        foreach (var action in ShortcutKeys.Actions)
        {
            var binding = ShortcutKeys.ParseBinding(shortcutOverrides.GetValueOrDefault(action.Id), action.DefaultModifiers, action.DefaultKey);
            lines.Add(action.Id + "=" + ShortcutKeys.Format(binding.Modifiers, binding.Key));
        }
        return string.Join("\n", lines);
    }

    public (uint Modifiers, uint Key) ShortcutBinding(string id, uint fallbackKey) =>
        ShortcutKeys.ParseBinding(shortcutOverrides.GetValueOrDefault(id), fallbackKey);

    public (uint Modifiers, uint Key) ShortcutBinding(string id, uint fallbackModifiers, uint fallbackKey) =>
        ShortcutKeys.ParseBinding(shortcutOverrides.GetValueOrDefault(id), fallbackModifiers, fallbackKey);

    public void OpenExternal(string target)
    {
        try
        {
            var psi = target.StartsWith("http", StringComparison.OrdinalIgnoreCase) || target.StartsWith("mailto", StringComparison.OrdinalIgnoreCase)
                ? new ProcessStartInfo(target) { UseShellExecute = true }
                : Directory.Exists(target)
                    ? new ProcessStartInfo("explorer.exe", target) { UseShellExecute = true }
                    : new ProcessStartInfo(target) { UseShellExecute = true };
            Process.Start(psi);
        }
        catch (Exception ex) { SetStatus(ex.Message, true); }
    }

    public Task RunWork(Func<CancellationToken, Task> operation) => RunWorkAsync(operation);

    public bool ShuttingDown => closing;

    public bool AllowClose => allowClose;
}
