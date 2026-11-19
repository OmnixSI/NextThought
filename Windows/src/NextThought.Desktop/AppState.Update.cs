using System.Diagnostics;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>检查更新（正式版本 / main 最新构建）：可下载安装包、校验 SHA-256 后静默安装并重启。</summary>
public partial class AppState
{
    [ObservableProperty] private UpdateChannel updateChannel = UpdateChannel.Stable;
    [ObservableProperty] private string updateStatus = "还没有检查更新。";
    [ObservableProperty] private bool updateAvailable;
    [ObservableProperty] private string updateVersion = "";
    [ObservableProperty] private string? updateUrl;
    [ObservableProperty] private bool checkingUpdate;
    [ObservableProperty] private bool installingUpdate;
    [ObservableProperty] private double updateInstallProgress;
    [ObservableProperty] private string updateInstallStatus = "";
    [ObservableProperty] private bool updateInstallSucceeded;

    /// <summary>Arguments used by the installer smoke tests as well (Inno Setup 7).</summary>
    public const string SilentInstallArguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS";

    /// <summary>Raised after a verified installer has been started; the window exits.</summary>
    public event Action? UpdateInstalled;

    public bool CanInstallUpdateInApp =>
        UpdateAvailable && LastUpdateReport is { CanInstall: true } && !InstallingUpdate;

    public IReadOnlyList<UpdateChannelChoice> UpdateChannels { get; } =
    [
        new(UpdateChannel.Stable, "正式版本"),
        new(UpdateChannel.Main, "main 最新构建")
    ];

    public UpdateChannelChoice? SelectedUpdateChannel
    {
        get => UpdateChannels.FirstOrDefault(choice => choice.Channel == UpdateChannel);
        set { if (value is not null) UpdateChannel = value.Channel; }
    }

    partial void OnUpdateChannelChanged(UpdateChannel value) => OnPropertyChanged(nameof(SelectedUpdateChannel));
    partial void OnUpdateAvailableChanged(bool value)
    {
        OnPropertyChanged(nameof(CanOpenUpdatePage));
        OnPropertyChanged(nameof(CanInstallUpdateInApp));
        InstallUpdateCommand.NotifyCanExecuteChanged();
    }
    partial void OnUpdateUrlChanged(string? value) => OnPropertyChanged(nameof(CanOpenUpdatePage));
    partial void OnInstallingUpdateChanged(bool value)
    {
        OnPropertyChanged(nameof(CanInstallUpdateInApp));
        InstallUpdateCommand.NotifyCanExecuteChanged();
    }

    public bool CanOpenUpdatePage => UpdateAvailable && UpdateUrl is { Length: > 0 };

    /// <summary>Checks the feed; <paramref name="source"/> is only set by the QA switch.</summary>
    public async Task<UpdateReport> CheckForUpdatesAsync(string? source = null)
    {
        if (CheckingUpdate) return LastUpdateReport ?? new UpdateReport(DateTimeOffset.UtcNow, source ?? UpdateFeed.Source,
            UpdateChannel.Title(), UpdateCheck.CurrentVersion, UpdateCheck.CurrentBuild, null, null, false, null, "正在检查");
        CheckingUpdate = true;
        UpdateStatus = "正在检查更新…";
        try
        {
            var report = await UpdateCheck.RunAsync(source ?? UpdateFeed.Source, UpdateChannel);
            LastUpdateReport = report;
            UpdateAvailable = report.Available;
            UpdateVersion = report.LatestVersion ?? "";
            UpdateUrl = report.DownloadUrl;
            UpdateStatus = report.Describe();
            OnPropertyChanged(nameof(CanInstallUpdateInApp));
            InstallUpdateCommand.NotifyCanExecuteChanged();
            return report;
        }
        finally
        {
            CheckingUpdate = false;
        }
    }

    public UpdateReport? LastUpdateReport { get; private set; }

    [RelayCommand]
    private async Task CheckUpdateAsync() => await CheckForUpdatesAsync();

    public static string UpdateBackupRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NextThought", "UpdateBackups");

    public bool HasUpdateBackup => UpdateBackup.List(UpdateBackupRoot).Count > 0;

    /// <summary>Restores the newest pre-update backup after this process exits.</summary>
    [RelayCommand]
    private void RestoreUpdateBackup()
    {
        var backup = UpdateBackup.List(UpdateBackupRoot).FirstOrDefault();
        if (backup is null)
        {
            SetStatus("没有可用的更新备份。", true);
            return;
        }
        try
        {
            var executable = Path.Combine(AppContext.BaseDirectory, "NextThought.exe");
            UpdateRestore.Launch(backup.Path, AppContext.BaseDirectory, executable, Environment.ProcessId);
            UpdateInstallStatus = $"将在退出后恢复 {backup.Version}，并重新打开 NextThought。";
            SetStatus(UpdateInstallStatus);
            UpdateInstalled?.Invoke();
        }
        catch (Exception exception)
        {
            SetStatus("回滚失败：" + exception.Message, true);
        }
    }

    [RelayCommand(CanExecute = nameof(CanInstallUpdateInApp))]
    private async Task InstallUpdateAsync()
    {
        if (LastUpdateReport is not { Available: true, CanInstall: true } report) return;
        if (InstallingUpdate) return;
        InstallingUpdate = true;
        UpdateInstallSucceeded = false;
        UpdateInstallProgress = 0;
        UpdateInstallStatus = "正在下载安装包…";
        string? installer = null;
        try
        {
            var directory = Path.Combine(Path.GetTempPath(), "NextThoughtUpdate-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(directory);
            installer = Path.Combine(directory, report.SetupName ?? "NextThought-setup.exe");
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            using (var response = await http.GetAsync(report.SetupUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength ?? 0;
                await using var source = await response.Content.ReadAsStreamAsync();
                await using var target = File.Create(installer);
                var buffer = new byte[81920];
                long done = 0;
                int read;
                while ((read = await source.ReadAsync(buffer)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read));
                    done += read;
                    if (total > 0) UpdateInstallProgress = Math.Round((double)done / total, 3);
                }
            }

            UpdateInstallStatus = "正在校验 SHA-256…";
            var checksums = await http.GetStringAsync(report.ChecksumsUrl);
            var expected = UpdateChecksums.Find(checksums, Path.GetFileName(installer));
            if (expected is null) throw new InvalidOperationException("发布里没有这个安装包的校验值。");
            if (!UpdateChecksums.Verify(installer, expected))
            {
                File.Delete(installer);
                throw new InvalidOperationException("安装包校验失败，已删除下载文件。");
            }

            UpdateInstallStatus = "正在检查安装包签名…";
            var signature = await NextThought.Platform.Authenticode.StatusAsync(installer);
            if (NextThought.Platform.Authenticode.IsRequired && !NextThought.Platform.Authenticode.IsValid(signature))
            {
                File.Delete(installer);
                throw new InvalidOperationException($"安装包签名校验未通过（{signature}）。");
            }
            SetStatus("安装包签名：" + signature);

            // The QA switch lets the installer step be observed without replacing this build.
            var launch = Environment.GetEnvironmentVariable("NEXTTHOUGHT_UPDATE_LAUNCH");
            if (launch is { Length: > 0 })
            {
                await File.WriteAllTextAsync(launch, JsonSerializer.Serialize(new
                {
                    installer,
                    sha256 = expected,
                    args = SilentInstallArguments
                }, new JsonSerializerOptions { WriteIndented = true }));
                UpdateInstallStatus = "自检：安装包已下载并通过 SHA-256，启动命令已记录。";
                UpdateInstallSucceeded = true;
                SetStatus(UpdateInstallStatus);
                return;
            }

            UpdateInstallStatus = "正在备份当前版本…";
            try
            {
                var backupRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NextThought", "UpdateBackups");
                var backup = UpdateBackup.Create(AppContext.BaseDirectory, backupRoot, UpdateCheck.CurrentVersion);
                UpdateBackup.Prune(backupRoot, 2);
                SetStatus("已备份当前版本：" + Path.GetFileName(backup.Path));
            }
            catch (Exception backupError)
            {
                // A failed backup must not block a checksum-verified update; it is reported instead.
                SetStatus("备份失败，仍继续更新：" + backupError.Message, true);
            }

            Process.Start(new ProcessStartInfo(installer, SilentInstallArguments) { UseShellExecute = true });
            UpdateInstallStatus = "校验通过，正在安装…";
            UpdateInstallSucceeded = true;
            SetStatus("正在安装更新，NextThought 会退出并在安装完成后重新打开。");
            UpdateInstalled?.Invoke();
        }
        catch (Exception ex)
        {
            UpdateInstallStatus = "安装失败：" + ex.Message;
            SetStatus(UpdateInstallStatus, true);
            if (installer is not null && File.Exists(installer)) { try { File.Delete(installer); } catch { } }
        }
        finally
        {
            InstallingUpdate = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenUpdatePage))]
    private void OpenUpdatePage()
    {
        if (UpdateUrl is not { Length: > 0 } url) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            SetStatus("已在浏览器中打开下载页。");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
        }
    }
}

public sealed record UpdateChannelChoice(UpdateChannel Channel, string Title)
{
    public override string ToString() => Title;
}
