namespace NextThought.Desktop;

public partial class AppState
{

    [RelayCommand]
    private void SetHistoryFilter(string filter) => HistoryFilter = filter;

    public bool OnHistory => Section == AppSection.History;

    partial void OnSectionChanged(AppSection value)
    {
        OnPropertyChanged(nameof(OnHome));
        OnPropertyChanged(nameof(OnLive));
        OnPropertyChanged(nameof(OnNotes));
        OnPropertyChanged(nameof(OnLibrary));
        OnPropertyChanged(nameof(OnHistory));
        OnPropertyChanged(nameof(OnSeries));
        OnPropertyChanged(nameof(OnSettings));
        OnPropertyChanged(nameof(ContextSidebarVisible));
    }

    partial void OnHistoryQueryChanged(string value) => ApplyHistoryFilter();

    partial void OnCurrentSessionChanged(SessionMeta? value) => OnPropertyChanged(nameof(StatsText));

    public void RefreshHistory(Guid? select = null)
    {
        var list = Sessions.List();
        History.Clear();
        foreach (var item in list) History.Add(new RecentMeetingRow(item));
        if (select is { } id)
        {
            var match = list.FirstOrDefault(x => x.Id == id);
            if (match is not null) CurrentSession ??= match;
        }
        HomeRecent.Clear();
        foreach (var item in list.Take(5)) HomeRecent.Add(new RecentMeetingRow(item));
        OnPropertyChanged(nameof(IsHistoryEmpty));
        ApplyHistoryFilter();
    }

    partial void OnHistoryLengthFilterChanged(string value) => ApplyHistoryFilter();

    partial void OnHistorySortChanged(string value) => ApplyHistoryFilter();

    public bool IsHistoryEmpty => History.Count == 0;

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync()
    {
        if (CurrentSession is null || PickSaveFile is null) return;
        var name = Regex.Replace(CurrentSession.Title, "[<>:\"/\\\\|?*]", "_") + ".md";
        if (await PickSaveFile("Markdown (*.md)|*.md", name) is not { } file) return;
        try
        {
            await Task.WhenAll(writes.ToArray());
            await Sessions.ExportMarkdownAsync(CurrentSession.Id, file);
            SetStatus("会议已导出。");
        }
        catch (Exception ex) { SetStatus(ex.Message, true); }
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportDocxAsync()
    {
        if (CurrentSession is null || PickSaveFile is null) return;
        var name = Regex.Replace(CurrentSession.Title, "[<>:\"/\\|?*]", "_") + ".docx";
        if (await PickSaveFile("Word 文档 (*.docx)|*.docx", name) is not { } file) return;
        try
        {
            await Task.WhenAll(writes.ToArray());
            await Sessions.ExportDocxAsync(CurrentSession.Id, file);
            SetStatus("会议已导出为 Word 文档。");
        }
        catch (Exception ex) { SetStatus(ex.Message, true); }
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportPdfAsync()
    {
        if (CurrentSession is null || PickSaveFile is null) return;
        var name = Regex.Replace(CurrentSession.Title, "[<>:\"/\\|?*]", "_") + ".pdf";
        if (await PickSaveFile("PDF (*.pdf)|*.pdf", name) is not { } file) return;
        try
        {
            await Task.WhenAll(writes.ToArray());
            await Sessions.ExportPdfAsync(CurrentSession.Id, file);
            SetStatus("会议已导出为 PDF。");
        }
        catch (Exception ex) { SetStatus(ex.Message, true); }
    }

    [RelayCommand(CanExecute = nameof(CanExportSubtitles))]
    private Task ExportSrtAsync() => ExportSubtitlesAsync(SessionSubtitleFormat.Srt);

    [RelayCommand(CanExecute = nameof(CanExportSubtitles))]
    private Task ExportVttAsync() => ExportSubtitlesAsync(SessionSubtitleFormat.Vtt);

    private bool CanExportSubtitles() => !Working && CurrentSession != null && currentLines.Count > 0;

    [RelayCommand(CanExecute = nameof(CanBackup))]
    private async Task BackupAsync()
    {
        if (PickSaveFile is null) return;
        if (await PickSaveFile("NextThought 备份|*.ntbackup", "NextThought-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".ntbackup") is not { } file) return;
        await RunWorkAsync(async ct =>
        {
            await Task.WhenAll(writes.ToArray());
            await Task.Run(() => new BackupService(Paths).ExportAsync(file, ct), ct);
            SetStatus("备份已导出：包含会议与资料；不包含 API Key、语音模型和设备设置。");
        });
    }

    [RelayCommand(CanExecute = nameof(CanRestore))]
    private async Task RestoreAsync()
    {
        if (PickOpenFile is null) return;
        if (await PickOpenFile("NextThought 备份|*.ntbackup;*.ihbackup") is not { } file) return;
        await RunWorkAsync(async ct =>
        {
            var result = await Task.Run(() => new BackupService(Paths).ImportAsync(file, ct), ct);
            RefreshHistory();
            Library.Refresh();
            SetStatus($"已导入 {result.SessionsAdded} 场会议、{result.MaterialsAdded} 份资料、{result.WorkspacesAdded} 个系列" + (result.SettingsRestored ? "，并恢复设置" : "") + $"；{result.ConflictsSkipped} 个重复项保留现有版本。导入前备份：{result.SafetyBackup}。");
        });
    }

    private int companionHistoryIndex = -1;
}
