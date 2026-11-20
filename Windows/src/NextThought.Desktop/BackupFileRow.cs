namespace NextThought.Desktop;

/// <summary>E30: one row of the backup list (DataBackup.BackupFile) with display text.</summary>
public sealed class BackupFileRow(NextThought.Platform.BackupFile file)
{
    public NextThought.Platform.BackupFile File { get; } = file;

    public string Name => File.Name;
    public string DateText => File.Date.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public string ReasonTitle => File.ReasonTitle;
    public string SizeText => FormatSize(File.Size);
    public bool IsRecoveryPoint => File.IsRecoveryPoint;
    public string Detail => ReasonTitle + " · " + SizeText;

    /// <summary>ByteCountFormatter-style text for the list.</summary>
    public static string FormatSize(long bytes)
    {
        if (bytes < 1024) return bytes + " B";
        if (bytes < 1024 * 1024) return Math.Round(bytes / 1024.0, 1).ToString("0.#") + " KB";
        if (bytes < 1024L * 1024 * 1024) return Math.Round(bytes / (1024.0 * 1024), 1).ToString("0.#") + " MB";
        return Math.Round(bytes / (1024.0 * 1024 * 1024), 2).ToString("0.##") + " GB";
    }
}
