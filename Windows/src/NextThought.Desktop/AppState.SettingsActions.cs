using CommunityToolkit.Mvvm.Input;

namespace NextThought.Desktop;

public partial class AppState
{
    /// <summary>
    /// E14: saving a Tavily key (settings UI) invalidates the verification cache: earlier results
    /// may belong to another account, and the resolved provider must be rebuilt.
    /// </summary>
    [RelayCommand]
    private void SaveTavilyKey(string? key)
    {
        try
        {
            var trimmed = (key ?? "").Trim();
            SettingsStore.SaveTavilyKey(trimmed);
            webSearchResolved = false;
            webSearchProvider = null;
            webVerificationCache.Invalidate();
            LastWebSearchUsedCache = false;
            OnPropertyChanged(nameof(IsWebSearchConfigured));
            OnPropertyChanged(nameof(WebSearchProviderLabel));
            OnPropertyChanged(nameof(HasStoredWebSearchKey));
            OnPropertyChanged(nameof(WebSearchKeyStatusLine));
            OnPropertyChanged(nameof(WebVerificationCacheCount));
            Settings.TavilyKeyStatus = WebSearchKeyStatusLine;
            SetStatus(trimmed.Length == 0
                ? UiText.T("未填写") + " · " + UiText.T("密钥只保存在当前 Windows 用户；也可以继续用 TAVILY_API_KEY 环境变量。")
                : UiText.T("Tavily 密钥已保存（当前 Windows 用户加密）。"));
            OnPropertyChanged(nameof(WebSearchAvailabilityText));
        }
        catch (Exception ex) { SetStatus(UiText.T("保存 Tavily 密钥失败：") + ex.Message, true); }
    }
}
