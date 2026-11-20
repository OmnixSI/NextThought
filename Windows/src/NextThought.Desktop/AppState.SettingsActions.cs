using CommunityToolkit.Mvvm.Input;

namespace NextThought.Desktop;

public partial class AppState
{
    [RelayCommand]
    private void SaveTavilyKey(string? key)
    {
        try
        {
            SettingsStore.SaveTavilyKey((key ?? "").Trim());
            webSearchResolved = false;
            webSearchProvider = null;
            SetStatus("Tavily 密钥已保存（当前 Windows 用户加密）。");
        }
        catch (Exception ex) { SetStatus("保存 Tavily 密钥失败：" + ex.Message, true); }
    }
}
