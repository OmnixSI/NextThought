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
        OnPropertyChanged(nameof(OnPrepare));
        OnPropertyChanged(nameof(OnNotes));
        OnPropertyChanged(nameof(OnLibrary));
        OnPropertyChanged(nameof(OnHistory));
        OnPropertyChanged(nameof(OnSeries));
        OnPropertyChanged(nameof(OnSettings));
        OnPropertyChanged(nameof(ContextSidebarVisible));
        if (value == AppSection.Prepare) SetPageSubtitle("准备这场会 · 类型、资料与检查");
    }

    partial void OnHistoryQueryChanged(string value) => ApplyHistoryFilter();

    partial void OnCurrentSessionChanged(SessionMeta? value) { OnPropertyChanged(nameof(StatsText)); OnPropertyChanged(nameof(HasSelectedHistory)); }

    /// <summary>F20: refreshing the list never waits for the session lock on the UI thread.</summary>
    public void RefreshHistory(Guid? select = null) => _ = RefreshHistoryAsync(select);

    /// <summary>F20: read the meeting list off the UI thread, then update the history on it.</summary>
    public async Task RefreshHistoryAsync(Guid? select = null)
    {
        try
        {
            var list = await Task.Run(() => Sessions.List());
            History.Clear();
            foreach (var item in list) History.Add(new RecentMeetingRow(item));
            if (select is { } id && list.FirstOrDefault(x => x.Id == id) is { } match) CurrentSession ??= match;
            HomeRecent.Clear();
            foreach (var item in list.Take(5)) HomeRecent.Add(new RecentMeetingRow(item));
            OnPropertyChanged(nameof(IsHistoryEmpty));
            ApplyHistoryFilter();
        }
        catch (Exception ex)
        {
            SetStatus("读取会议列表失败：" + ex.Message, true);
        }
    }

    partial void OnHistoryLengthFilterChanged(string value) => ApplyHistoryFilter();

    partial void OnHistorySortChanged(string value) => ApplyHistoryFilter();

    public System.Collections.ObjectModel.ObservableCollection<HistoryGroupRow> HistoryGroups { get; } = [];


    public System.Collections.ObjectModel.ObservableCollection<NextThought.Platform.SessionContentHit> HistoryContentHits { get; } = [];

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private async Task SearchHistoryContentAsync()
    {
        if (!initialized) return;
        var query = HistoryQuery.Trim();
        if (query.Length < 2) { SetStatus("全文搜索至少输入两个字。"); return; }
        try
        {
            var hits = await Task.Run(() => Sessions.SearchContent(query));
            HistoryContentHits.Clear();
            foreach (var hit in hits) HistoryContentHits.Add(hit);
            SetStatus(UiText.Format("全文搜索：%lld 条命中。", hits.Count));
        }
        catch (Exception ex) { SetStatus("全文搜索失败：" + ex.Message, true); }
    }


    public System.Collections.ObjectModel.ObservableCollection<AnswerRecord> SelectedSessionAnswers { get; } = [];
    public bool HasSelectedHistory => CurrentSession is not null;


    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private async Task OpenSession(RecentMeetingRow row)
    {
        await SelectHistoryAsync(row.Meta);
        Section = AppSection.Notes;
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private async Task ExportSession(RecentMeetingRow row)
    {
        await SelectHistoryAsync(row.Meta);
        if (CurrentSession is not null) ExportCommand.Execute(null);
    }



    public System.Collections.ObjectModel.ObservableCollection<Avalonia.Media.Imaging.Bitmap> SelectedSessionScreenshots { get; } = [];

    /// <summary>F20: listing a saved meeting's screenshots waits for the session lock on a worker.</summary>
    private async Task LoadSessionScreenshotsAsync(Guid id)
    {
        foreach (var bitmap in SelectedSessionScreenshots) bitmap.Dispose();
        SelectedSessionScreenshots.Clear();
        try
        {
            var paths = await Task.Run(() => Sessions.Screenshots(id));
            foreach (var path in paths)
                SelectedSessionScreenshots.Add(new Avalonia.Media.Imaging.Bitmap(path));
        }
        catch { }
        SelectedSessionScreenshotCount = SelectedSessionScreenshots.Count;
    }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private int selectedSessionScreenshotCount;

    public System.Collections.ObjectModel.ObservableCollection<Avalonia.Media.Imaging.Bitmap> CurrentScreenshots { get; } = [];

    public async Task ArchiveScreenshotAsync(string path)
    {
        if (CurrentSession is not { } meta) return;
        try
        {
            await Sessions.SaveScreenshotAsync(meta.Id, path);
            try { CurrentScreenshots.Add(new Avalonia.Media.Imaging.Bitmap(path)); } catch { }
            await LoadSessionScreenshotsAsync(meta.Id);
        }
        catch { }
    }

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
            // A13: restored settings/glossary/skills/templates must show without a restart.
            Settings.LoadFromStore();
            LoadSkills();
            LoadGlossary();
            LoadCustomTemplates();
            RefreshHistory();
            Library.Refresh();
            SetStatus(UiText.Format("已导入 %lld 场会议、%lld 份资料、%lld 个系列", result.SessionsAdded, result.MaterialsAdded, result.WorkspacesAdded)
    + (result.SettingsRestored ? UiText.T("，并恢复设置") : "")
    + UiText.Format("；%lld 个重复项保留现有版本。导入前备份：%@。", result.ConflictsSkipped, result.SafetyBackup));
        });
    }

    // MARK: E30 备份与恢复 (DataBackup + BackupSettingsSection / BackupRestoreReviewView)

    [ObservableProperty] private bool autoBackup = true;
    [ObservableProperty] private bool backupBusy;
    [ObservableProperty] private string backupStatusText = "";
    [ObservableProperty] private bool backupFailed;
    [ObservableProperty] private bool backupCanRetry;
    [ObservableProperty] private string? backupIssue;
    [ObservableProperty] private string? recoveryPointWarning;
    [ObservableProperty] private bool backupIncludeSettings = true;
    private bool autoBackupLoaded;

    public ObservableCollection<BackupFileRow> Backups { get; } = [];
    public bool HasBackups => Backups.Count > 0;
    public string BackupLatestText => Backups.Count == 0 ? "" : "最近备份：" + Backups[0].DateText;
    public string BackupsRoot => new BackupService(Paths).BackupsRoot;

    public string BackupNote => "导入会合并，不删除现有数据；先预览，再决定恢复什么。"
        + (Backups.Count == 0 ? "" : " " + BackupLatestText + "。");
    public bool HasBackupStatus => BackupStatusText.Length > 0;
    public bool ShowBackupRetry => BackupFailed && BackupCanRetry;
    public bool CanIncludeSettingsOnImport => true;

    partial void OnBackupStatusTextChanged(string value)
    {
        OnPropertyChanged(nameof(BackupStatusLine));
        OnPropertyChanged(nameof(HasBackupStatus));
    }
    public string BackupStatusLine => BackupStatusText;

    partial void OnBackupFailedChanged(bool value) => OnPropertyChanged(nameof(ShowBackupRetry));
    partial void OnBackupCanRetryChanged(bool value) => OnPropertyChanged(nameof(ShowBackupRetry));

    partial void OnBackupBusyChanged(bool value)
    {
        BackupNowCommand.NotifyCanExecuteChanged();
        ExportAllBackupCommand.NotifyCanExecuteChanged();
    }

    partial void OnBackupIssueChanged(string? value) => OnPropertyChanged(nameof(HasBackupIssue));
    public bool HasBackupIssue => BackupIssue is { Length: > 0 };
    partial void OnRecoveryPointWarningChanged(string? value) => OnPropertyChanged(nameof(HasRecoveryPointWarning));
    public bool HasRecoveryPointWarning => RecoveryPointWarning is { Length: > 0 };

    partial void OnAutoBackupChanged(bool value)
    {
        if (!autoBackupLoaded) return;
        _ = SaveAutoBackupPreferenceAsync(value);
    }

    private async Task SaveAutoBackupPreferenceAsync(bool value)
    {
        try { await SettingsStore.SaveMacPreferencesAsync(new Dictionary<string, object?> { ["autoBackup"] = value }); }
        catch (Exception ex) { SetStatus("保存自动备份设置失败：" + ex.Message, true); }
    }

    /// <summary>Mac <c>autoBackup</c> (AppState.swift: default true); read fresh for the after-session hook.</summary>
    public bool AutoBackupEnabled => SettingsStore.LoadMacValue("autoBackup") is not false;

    /// <summary>Reads the Mac preference once so the settings toggle shows the stored value.</summary>
    public void LoadBackupPreferences()
    {
        if (autoBackupLoaded) return;
        var stored = SettingsStore.LoadMacValue("autoBackup");
        AutoBackup = stored is not false;
        autoBackupLoaded = true;
        BackupIncludeSettings = true;
    }

    private bool CanRunBackupTask() => IsIdle && !BackupBusy;

    [RelayCommand(CanExecute = nameof(CanRunBackupTask))]
    private async Task BackupNowAsync() => await RunBackupTaskAsync("正在备份…", canRetryBackup: true, async () =>
    {
        var point = await Task.Run(() => new BackupService(Paths).CreateRecoveryPointAsync("manual"));
        await RefreshBackupsAsync();
        RecoveryPointWarning = point.Size > 2L * 1024 * 1024 * 1024
            ? "最新恢复点超出 2 GiB 软预算，已保留完整恢复依据；可手动导出后管理空间。"
            : null;
        return "已备份到 " + point.Name + "。";
    });

    [RelayCommand(CanExecute = nameof(CanRunBackupTask))]
    private async Task ExportAllBackupAsync()
    {
        if (PickSaveFile is null) return;
        if (await PickSaveFile("NextThought 备份|*.ntbackup", "NextThought-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".ntbackup") is not { } file) return;
        await RunBackupTaskAsync("正在导出…", canRetryBackup: false, async () =>
        {
            await Task.WhenAll(writes.ToArray());
            await Task.Run(() => new BackupService(Paths).ExportAsync(file));
            return "已导出到 " + Path.GetFileName(file) + "。";
        });
    }

    [RelayCommand]
    private async Task RefreshBackupsAsync()
    {
        try
        {
            var listed = await Task.Run(() => new BackupService(Paths).ListBackups());
            Backups.Clear();
            foreach (var file in listed) Backups.Add(new BackupFileRow(file));
            OnPropertyChanged(nameof(HasBackups));
            OnPropertyChanged(nameof(BackupLatestText));
            OnPropertyChanged(nameof(BackupNote));
        }
        catch (Exception ex)
        {
            BackupIssue = "读取备份列表失败：" + ex.Message;
        }
    }

    [RelayCommand]
    private async Task DeleteBackupAsync(BackupFileRow? row)
    {
        if (row is null) return;
        try
        {
            await Task.Run(() => new BackupService(Paths).DeleteRecoveryPoint(row.File));
            await RefreshBackupsAsync();
            BackupStatusText = "已删除恢复点：" + row.DateText + "。";
            BackupFailed = false;
        }
        catch (Exception ex)
        {
            BackupStatusText = "删除失败：" + ex.Message;
            BackupFailed = true;
            SetStatus(BackupStatusText, true);
        }
    }

    [RelayCommand]
    private void OpenBackupsFolder()
    {
        try
        {
            Directory.CreateDirectory(BackupsRoot);
            OpenExternal(BackupsRoot);
        }
        catch (Exception ex) { SetStatus("打开备份文件夹失败：" + ex.Message, true); }
    }

    private async Task RunBackupTaskAsync(string progress, bool canRetryBackup, Func<Task<string>> work)
    {
        if (BackupBusy) return;
        BackupBusy = true;
        BackupStatusText = progress;
        BackupFailed = false;
        BackupCanRetry = canRetryBackup;
        try
        {
            BackupStatusText = await work();
            BackupFailed = false;
            BackupCanRetry = false;
        }
        catch (Exception ex)
        {
            BackupStatusText = "操作失败：" + ex.Message;
            BackupFailed = true;
            SetStatus(BackupStatusText, true);
        }
        finally
        {
            BackupBusy = false;
        }
    }

    /// <summary>DataBackup.prepareImport: validation and the review plan happen before anything changes.</summary>
    public async Task<PreparedBackupImport?> PrepareBackupImportAsync(string path)
    {
        if (Recording || !IsIdle)
        {
            SetStatus("请先结束记录和正在进行的操作，再导入备份。", true);
            return null;
        }
        if (BackupBusy) return null;
        BackupBusy = true;
        BackupStatusText = "正在校验备份并准备预览…";
        BackupFailed = false;
        try
        {
            var prepared = await Task.Run(() => new BackupService(Paths).PrepareImportAsync(path));
            BackupStatusText = "预览已就绪；确认前不会修改本机数据。";
            return prepared;
        }
        catch (Exception ex)
        {
            BackupStatusText = "操作失败：" + ex.Message;
            BackupFailed = true;
            SetStatus(BackupStatusText, true);
            return null;
        }
        finally { BackupBusy = false; }
    }

    /// <summary>DataBackup.applyImport: chosen conflicts, template merge and settings are applied together.</summary>
    public async Task<BackupImportResult?> ApplyBackupImportAsync(PreparedBackupImport prepared,
        IReadOnlySet<Guid> useBackupSessions, IReadOnlySet<string> useBackupTemplates, bool applySettings)
    {
        BackupBusy = true;
        BackupStatusText = "正在恢复…";
        BackupFailed = false;
        try
        {
            var result = await Task.Run(() => new BackupService(Paths).ApplyPreparedImportAsync(
                prepared, useBackupSessions, useBackupTemplates, applySettings));
            // A13: restored settings/glossary/skills/templates must show without a restart.
            Settings.LoadFromStore();
            LoadSkills();
            LoadGlossary();
            LoadCustomTemplates();
            await RefreshHistoryAsync();
            Library.Refresh();
            await RefreshBackupsAsync();
            BackupStatusText = DescribeImport(result);
            BackupIssue = null;
            return result;
        }
        catch (Exception ex)
        {
            BackupStatusText = "恢复未全部完成：" + ex.Message + "已有内容请以实际结果为准；如已开始应用，可从恢复前的恢复点找回。";
            BackupFailed = true;
            SetStatus(BackupStatusText, true);
            return null;
        }
        finally
        {
            BackupBusy = false;
            prepared.Discard();
        }
    }

    /// <summary>DataBackup.ImportSummary.description, in the wording the Windows restore already used.</summary>
    private static string DescribeImport(BackupImportResult result)
    {
        var text = "已导入 " + result.SessionsAdded + " 场会议、" + result.MaterialsAdded + " 份资料、" + result.WorkspacesAdded + " 个系列"
                   + (result.SessionsReplaced > 0 ? "，替换 " + result.SessionsReplaced + " 场会议" : "")
                   + (result.SessionsKept > 0 ? "，保留本机 " + result.SessionsKept + " 场会议" : "")
                   + (result.SessionsIdentical > 0 ? "，相同跳过 " + result.SessionsIdentical + " 场会议" : "")
                   + (result.SettingsRestored ? "，并恢复设置" : "")
                   + (result.TemplatesAdded + result.TemplatesReplaced + result.TemplatesKept > 0
                       ? "，模板 新增 " + result.TemplatesAdded + "、替换 " + result.TemplatesReplaced + "、保留 " + result.TemplatesKept
                       : "")
                   + "；" + result.ConflictsSkipped + " 个重复项保留现有版本。";
        return text;
    }

    /// <summary>DataBackup.afterSession: the post-meeting recovery point, started when the meeting stops.</summary>
    public async Task RunAfterSessionBackupAsync(Guid sessionId)
    {
        if (!AutoBackupEnabled) return;
        try
        {
            var point = await Task.Run(() => new BackupService(Paths).CreateRecoveryPointAsync("session"));
            BackupIssue = null;
            RecoveryPointWarning = point.Size > 2L * 1024 * 1024 * 1024
                ? "最新恢复点超出 2 GiB 软预算，已保留完整恢复依据；可手动导出后管理空间。"
                : null;
            await RefreshBackupsAsync();
        }
        catch (Exception ex)
        {
            BackupIssue = "会议归档或恢复点创建失败：" + ex.Message;
            SetStatus(BackupIssue, true);
        }
    }

    private int companionHistoryIndex = -1;
}
