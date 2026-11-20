using System.Diagnostics;
using System.Text.Json;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;
using NextThought.Platform;

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
    [ObservableProperty] private bool updateInstallerReady;
    private string? prefetchedInstaller;
    private string? prefetchedForUrl;
    private Task? installerPrefetch;
    private string? prefetchUrl;

    /// <summary>Arguments used by the installer smoke tests as well (Inno Setup 7).</summary>
    public const string SilentInstallArguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /LAUNCH=1";

    /// <summary>Raised after a verified installer has been started; the window exits.</summary>
    public event Action? UpdateInstalled;

    /// <summary>
    /// F16: the title-bar "重启以更新" button appears once the background download has finished
    /// and passed SHA-256 (Updater.phase == .ready), not merely when a release exists.
    /// </summary>
    public bool CanInstallUpdateInApp =>
        UpdateAvailable && LastUpdateReport is { CanInstall: true } && UpdateInstallerReady && !InstallingUpdate;

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
    partial void OnUpdateInstallerReadyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanInstallUpdateInApp));
        InstallUpdateCommand.NotifyCanExecuteChanged();
    }
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
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                LastUpdateReport = report;
                UpdateAvailable = report.Available;
                UpdateVersion = report.LatestVersion ?? "";
                UpdateUrl = report.DownloadUrl;
                UpdateStatus = report.Describe();
                UpdateInstallerReady = prefetchedForUrl == report.SetupUrl && prefetchedInstaller is { } cached && File.Exists(cached);
                OnPropertyChanged(nameof(CanInstallUpdateInApp));
                InstallUpdateCommand.NotifyCanExecuteChanged();
            });
            // Updater.check: download in the background so "重启以更新" is instant once it is ready.
            if (report is { Available: true, CanInstall: true }) _ = StartInstallerPrefetch(report);
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

    /// <summary>Silent background check after launch and every six hours (review: auto update).</summary>
    public void BeginBackgroundUpdateCheck()
    {
        if (!Settings.AutoCheckUpdates) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30));
                while (true)
                {
                    try
                    {
                        var report = await CheckForUpdatesAsync();
                        if (report.Available)
                            Dispatcher.UIThread.Post(() => SetStatus($"发现新版本 {report.LatestVersion}，可在设置中更新。"));
                    }
                    catch
                    {
                        // Offline or rate-limited: automatic checks stay silent.
                    }
                    await Task.Delay(TimeSpan.FromHours(6));
                }
            }
            catch
            {
                // The background loop must never take the app down.
            }
        });
    }

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


    /// <summary>F16: download the installer in the background so the title-bar button is ready.</summary>
    private Task StartInstallerPrefetch(UpdateReport report)
    {
        if (report.SetupUrl is not { Length: > 0 } setupUrl || report.SetupName is not { Length: > 0 }
            || report.ChecksumsUrl is not { Length: > 0 })
            return Task.CompletedTask;
        if (installerPrefetch is { IsCompleted: false } && prefetchUrl == setupUrl) return installerPrefetch;
        prefetchUrl = setupUrl;
        installerPrefetch = Task.Run(() => PrefetchInstallerAsync(report));
        return installerPrefetch;
    }

    /// <summary>Self-test entry: waits until the background download and SHA-256 check finished.</summary>
    public Task EnsureInstallerPrefetchedAsync(UpdateReport report) => StartInstallerPrefetch(report);

    private async Task PrefetchInstallerAsync(UpdateReport report)
    {
        try
        {
            var directory = Path.Combine(Path.GetTempPath(), "NextThoughtPrefetch");
            var prepared = await UpdateInstaller.PrepareAsync(
                report.SetupUrl!, report.SetupName!, report.ChecksumsUrl!, directory);
            prefetchedInstaller = prepared.Path;
            prefetchedForUrl = report.SetupUrl;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                UpdateInstallerReady = true;
                UpdateInstallStatus = "安装包已在后台下载并通过 SHA-256 校验；点「重启以更新」安装。";
            });
        }
        catch (Exception ex)
        {
            // The click path can still try again and reports its own failure.
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                UpdateInstallerReady = false;
                UpdateInstallStatus = "后台下载失败：" + ex.Message;
            });
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
        UpdateInstallStatus = "正在准备安装包…";
        string? installer = null;
        string expectedSha;
        try
        {
            var cached = prefetchedForUrl == report.SetupUrl ? prefetchedInstaller : null;
            if (cached is not null && File.Exists(cached))
            {
                UpdateInstallStatus = "正在校验后台下载的安装包…";
                expectedSha = await UpdateInstaller.VerifyAsync(cached, report.ChecksumsUrl!);
                installer = cached;
                UpdateInstallProgress = 1;
            }
            else
            {
                UpdateInstallStatus = "正在下载安装包…";
                var directory = Path.Combine(Path.GetTempPath(), "NextThoughtUpdate-" + Guid.NewGuid().ToString("N")[..8]);
                var progress = new Progress<double>(value => UpdateInstallProgress = value);
                var prepared = await UpdateInstaller.PrepareAsync(
                    report.SetupUrl!, report.SetupName ?? "NextThought-setup.exe", report.ChecksumsUrl!, directory, progress);
                expectedSha = prepared.Sha256;
                installer = prepared.Path;
            }

            UpdateInstallStatus = "正在检查安装包签名…";
            var signature = await NextThought.Platform.Authenticode.StatusAsync(installer);
            var signer = await NextThought.Platform.Authenticode.SignerThumbprintAsync(installer);
            if (NextThought.Platform.Authenticode.IsRequired
                && (!NextThought.Platform.Authenticode.IsValid(signature) || !NextThought.Platform.Authenticode.SignerAllowed(signer)))
            {
                File.Delete(installer);
                throw new InvalidOperationException(signature != "Valid"
                    ? $"安装包签名校验未通过（{signature}）。"
                    : "安装包签名有效，但签名者不在允许列表（NEXTTHOUGHT_SIGNER_THUMBPRINT）。");
            }
            SetStatus("安装包签名：" + signature + (signer.Length > 0 ? "，签名者 " + signer[..Math.Min(12, signer.Length)] : ""));

            // F17: the self-test marker lives under the app data directory, not an environment bypass.
            var launch = StartupOptions.SelfTest is { Length: > 0 } ? Path.Combine(Paths.Root, "update-launch.json") : null;
            if (launch is { Length: > 0 })
            {
                await File.WriteAllTextAsync(launch, JsonSerializer.Serialize(new
                {
                    installer,
                    sha256 = expectedSha,
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
