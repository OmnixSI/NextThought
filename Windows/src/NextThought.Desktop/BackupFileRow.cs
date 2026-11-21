using CommunityToolkit.Mvvm.ComponentModel;

namespace NextThought.Desktop;

/// <summary>E30: one row of the backup list (DataBackup.BackupFile) with display text.</summary>
public sealed class BackupFileRow : ObservableObject
{
    public BackupFileRow(NextThought.Platform.BackupFile file) => File = file;

    public NextThought.Platform.BackupFile File { get; }

    public string Name => File.Name;
    public string DateText => File.Date.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public string SizeText => FormatSize(File.Size);
    public bool IsRecoveryPoint => File.IsRecoveryPoint;
    public string Detail => ReasonTitle + " · " + SizeText;

    /// <summary>DataMaintenanceAdvanced.reasonTitle: the Mac original sentences through UiText.</summary>
    public string ReasonTitle => File.Reason switch
    {
        "daily" => NextThought.Core.UiText.T("旧每日备份"),
        "session" => NextThought.Core.UiText.T("会议结束"),
        "update" => NextThought.Core.UiText.T("更新前"),
        "import" => NextThought.Core.UiText.T("导入前"),
        "manual" => NextThought.Core.UiText.T("手动"),
        "" => NextThought.Core.UiText.T("导出备份"),
        _ => File.Reason
    };

    /// <summary>Called after a live language switch so the bound text re-reads the table.</summary>
    public void NotifyLanguageChanged()
    {
        OnPropertyChanged(nameof(ReasonTitle));
        OnPropertyChanged(nameof(Detail));
    }

    /// <summary>ByteCountFormatter-style text for the list.</summary>
    public static string FormatSize(long bytes)
    {
        if (bytes < 1024) return bytes + " B";
        if (bytes < 1024 * 1024) return Math.Round(bytes / 1024.0, 1).ToString("0.#") + " KB";
        if (bytes < 1024L * 1024 * 1024) return Math.Round(bytes / (1024.0 * 1024), 1).ToString("0.#") + " MB";
        return Math.Round(bytes / (1024.0 * 1024 * 1024), 2).ToString("0.##") + " GB";
    }
}
