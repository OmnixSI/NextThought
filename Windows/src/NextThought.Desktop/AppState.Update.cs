using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>检查更新（正式版本 / main 最新构建）：只提示并打开下载页，不在应用内替换文件。</summary>
public partial class AppState
{
    [ObservableProperty] private UpdateChannel updateChannel = UpdateChannel.Stable;
    [ObservableProperty] private string updateStatus = "还没有检查更新。";
    [ObservableProperty] private bool updateAvailable;
    [ObservableProperty] private string updateVersion = "";
    [ObservableProperty] private string? updateUrl;
    [ObservableProperty] private bool checkingUpdate;

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
    partial void OnUpdateAvailableChanged(bool value) => OnPropertyChanged(nameof(CanOpenUpdatePage));
    partial void OnUpdateUrlChanged(string? value) => OnPropertyChanged(nameof(CanOpenUpdatePage));

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
