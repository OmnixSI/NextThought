using System.IO;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NextThought.Core;
using NextThought.Platform;
using Avalonia;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NextThought.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly AppState state;
    private readonly Dictionary<AppSection, Control> pages = [];
    private bool closing;
    private bool openedOnce;
    private readonly DispatcherTimer screenShareTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool sharingActive;
    private bool sharingRetractedIsland;

    public MainWindow(AppState state)
    {
        this.state = state;
        InitializeComponent();
        DataContext = state;
        ShowSection();
        state.PickSaveFile = PickSaveFileAsync;
        state.PickOpenFile = PickOpenFileAsync;
        screenShareTimer.Tick += (_, _) => EvaluateScreenSharing();
        screenShareTimer.Start();
        state.CopyText = async text =>
        {
            if (Clipboard is null) return;
            var transfer = new DataTransfer();
            transfer.Add(DataTransferItem.CreateText(text));
            await Clipboard.SetDataAsync(transfer);
        };
        state.Notify = ShowMessageAsync;
        state.Initialized += HandleInitialized;
        state.UpdateInstalled += RequestExit;
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AppState.OnHome) or nameof(AppState.OnLive) or nameof(AppState.OnNotes)
                or nameof(AppState.OnHistory) or nameof(AppState.OnSeries) or nameof(AppState.OnLibrary)
                or nameof(AppState.OnSettings)) ShowSection();
            else if (e.PropertyName == nameof(AppState.LiveMode)) ApplyLiveMode(state.LiveMode);
            else if (e.PropertyName == nameof(AppState.Recording))
            {
                if (state.Recording) ApplyPresenterModeForRecording();
                else ExitPresenterMode();
            }
        };
        Opened += async (_, _) =>
        {
            // Hide()/Show() (hide-all, presenter mode) raises Opened again; the startup work runs once.
            if (openedOnce) return;
            openedOnce = true;
            await state.InitializeAsync();
            state.BeginBackgroundUpdateCheck();
            UiTranslation.Apply(this, state.Settings.UiLanguage == "English");
            state.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AppState.Section))
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => UiTranslation.Apply(this, state.Settings.UiLanguage == "English"));
            };
            state.LanguageChanged += english =>
            {
                UiTranslation.Apply(this, english);
                if (island is not null) UiTranslation.Apply(island, english);
                if (teleprompter is not null) UiTranslation.Apply(teleprompter, english);
                tray?.SetLanguage(english);
            };
            RefreshDisplays();
            if (StartupOptions.Presenter) state.Settings.PresenterMode = true;
            SetupTray();
            if (StartupOptions.Scene is { } scene) state.SetSceneCommand.Execute(scene);
            if (StartupOptions.HistoryFilter is { } filter) state.HistoryFilter = filter;
            if (StartupOptions.HistoryQuery is { } query) state.HistoryQuery = query;
            if (StartupOptions.ImportWave is { } wave) await RunImportWaveTestAsync(wave);
            if (StartupOptions.Island) ToggleCueIsland();
            if (StartupOptions.Teleprompter) { ToggleTeleprompter(); if (StartupOptions.TeleprompterThrough) teleprompter?.ApplyClickThrough(true); }
            if (StartupOptions.CaptureTest is { } captureTest) await RunCaptureTestAsync(captureTest);
            if (StartupOptions.UpdateCheck is { } feed)
            {
                var update = await state.CheckForUpdatesAsync(feed);
                var target = StartupOptions.UpdateReport ?? feed + ".report.json";
                await File.WriteAllTextAsync(target, System.Text.Json.JsonSerializer.Serialize(update, UpdateReport.Options));
            }
            if (StartupOptions.CheckReport is { } report) await WriteStartupReportAsync(report);
            if (StartupOptions.KnowledgeCheck is { } knowledge) await RunKnowledgeCheckAsync(knowledge);
            if (StartupOptions.PresenterCheck is { } presenterCheck) await RunPresenterCheckAsync(presenterCheck);
            if (StartupOptions.RelayCheck is { } relayCheck) await RunRelayCheckAsync(relayCheck);
            if (StartupOptions.UpdateInstallCheck is { } updateInstall) await RunUpdateInstallCheckAsync(updateInstall);
            if (StartupOptions.SubtitleExportCheck is { } subtitleExport) await RunSubtitleExportCheckAsync(subtitleExport);
            if (StartupOptions.HotwordsCheck is { } hotwordsCheck) await RunHotwordsCheckAsync(hotwordsCheck);
            if (StartupOptions.FormulaCheck is { } formulaCheck) await RunFormulaCheckAsync(formulaCheck);
            if (StartupOptions.BackupCheck is { } backupCheck) await RunBackupCheckAsync(backupCheck);
            if (StartupOptions.PromptCheck is { } promptCheck) await RunPromptCheckAsync(promptCheck);
            if (StartupOptions.ApiCheck is { } apiCheck) await RunApiCheckAsync(apiCheck);
            if (StartupOptions.RetrievalCheck is { } retrievalCheck) await RunRetrievalCheckAsync(retrievalCheck);
            if (StartupOptions.CloudCheck is { } cloudCheck) await RunCloudCheckAsync(cloudCheck);
            if (StartupOptions.StreamCheck is { } streamCheck) await RunStreamCheckAsync(streamCheck);
            if (StartupOptions.QuestionCheck is { } questionCheck) await RunQuestionCheckAsync(questionCheck);
            if (StartupOptions.PdfCheck is { } pdfCheck) await RunPdfCheckAsync(pdfCheck);
            if (StartupOptions.GazeCheck is { } gazeCheck) await RunGazeCheckAsync(gazeCheck);
            if (StartupOptions.BpeCheck is { } bpeCheck) await RunBpeCheckAsync(bpeCheck);
            if (StartupOptions.GlossarySearchCheck is { } glossarySearch) await RunGlossarySearchCheckAsync(glossarySearch);
            if (StartupOptions.ProcessCheck is { } processCheck) await RunProcessCheckAsync(processCheck);
            if (StartupOptions.SessionSearchCheck is { } sessionSearch) await RunSessionSearchCheckAsync(sessionSearch);
            if (StartupOptions.SeriesBriefingCheck is { } seriesBriefing) await RunSeriesBriefingCheckAsync(seriesBriefing);
            if (StartupOptions.HistorySearchCheck is { } historySearch) await RunHistorySearchCheckAsync(historySearch);
            if (StartupOptions.BackupManifestCheck is { } backupManifest) await RunBackupManifestCheckAsync(backupManifest);
            if (StartupOptions.MeetingPlanCheck is { } meetingPlan) await RunMeetingPlanCheckAsync(meetingPlan);
            if (StartupOptions.EvidenceCheck is { } evidence) await RunEvidenceCheckAsync(evidence);
            if (StartupOptions.UserContentCheck is { } userContent) await RunUserContentCheckAsync(userContent);
            if (StartupOptions.WorkspaceDeletionCheck is { } deletion) await RunWorkspaceDeletionCheckAsync(deletion);
            if (StartupOptions.DocumentJobCheck is { } documentJob) await RunDocumentJobCheckAsync(documentJob);
            if (StartupOptions.PageCheck is { } pageCheck) await RunPageCheckAsync(pageCheck);
            if (StartupOptions.ArchivePersistenceCheck is { } archiveCheck) await RunArchivePersistenceCheckAsync(archiveCheck);
            if (StartupOptions.AcceptanceCheck is { } acceptance) await RunAcceptanceCheckAsync(acceptance);
            if (StartupOptions.UpdateBackupCheck is { } updateBackup) await RunUpdateBackupCheckAsync(updateBackup);
            if (StartupOptions.UpdateRestoreCheck is { } updateRestore) await RunUpdateRestoreCheckAsync(updateRestore);
            if (StartupOptions.SignatureCheck is { } signatureCheck) await RunSignatureCheckAsync(signatureCheck);
            if (StartupOptions.SlideFollowCheck is { } slideFollow) await RunSlideFollowCheckAsync(slideFollow);
            if (StartupOptions.PrepareModelsCheck is { } prepareModels) await RunPrepareModelsCheckAsync(prepareModels);
            if (StartupOptions.CompanionCheck is { } companionCheck) await RunCompanionCheckAsync(companionCheck);
            if (StartupOptions.ShortcutCheck is { } shortcutCheck) await RunShortcutCheckAsync(shortcutCheck);
            if (StartupOptions.DemoCheck is { } demoCheck) await RunDemoCheckAsync(demoCheck);
            if (StartupOptions.WindowShotCheck is { } windowShot) await RunWindowShotCheckAsync(windowShot);
            if (StartupOptions.UiLanguageCheck is { } uiLanguage) await RunUiLanguageCheckAsync(uiLanguage);
            if (StartupOptions.SettingsSearchCheck is { } settingsSearch) await RunSettingsSearchCheckAsync(settingsSearch);
            if (StartupOptions.ProviderPresetCheck is { } presetCheck) await RunProviderPresetCheckAsync(presetCheck);
            if (StartupOptions.AppearanceCheck is { } appearanceCheck) await RunAppearanceCheckAsync(appearanceCheck);
            if (StartupOptions.CloudSettingsCheck is { } cloudSettings) await RunCloudSettingsCheckAsync(cloudSettings);
            if (StartupOptions.CloudImportCheck is { } cloudImport) await RunCloudImportCheckAsync(cloudImport);
            if (StartupOptions.NotesJumpCheck is { } notesJump) await RunNotesJumpCheckAsync(notesJump);
            if (StartupOptions.LiveModeCheck is { } liveMode) await RunLiveModeCheckAsync(liveMode);
            if (StartupOptions.ShareHideCheck is { } shareHide) await RunShareHideCheckAsync(shareHide);
            if (StartupOptions.WindowCloseCheck is { } closeCheck) await RunWindowCloseCheckAsync(closeCheck);
            if (StartupOptions.SubtitleSettingsCheck is { } subtitleCheck) await RunSubtitleSettingsCheckAsync(subtitleCheck);
            if (StartupOptions.CaptureHideCheck is { } captureHide) await RunCaptureHideCheckAsync(captureHide);
            if (StartupOptions.AudioSwitchCheck is { } audioSwitch) await RunAudioSwitchCheckAsync(audioSwitch);
            if (StartupOptions.TeleprompterHandleCheck is { } handleCheck) await RunTeleprompterHandleCheckAsync(handleCheck);
        };
        Closing += OnClosing;
        Avalonia.Controls.Chrome.WindowDecorationProperties.SetElementRole(TitleBar, WindowDecorationsElementRole.TitleBar);
        Avalonia.Controls.Chrome.WindowDecorationProperties.SetElementRole(MinimizeButton, WindowDecorationsElementRole.MinimizeButton);
        Avalonia.Controls.Chrome.WindowDecorationProperties.SetElementRole(MaximizeButton, WindowDecorationsElementRole.MaximizeButton);
        Avalonia.Controls.Chrome.WindowDecorationProperties.SetElementRole(CloseButton, WindowDecorationsElementRole.CloseButton);
        RegisterHotkeys();
        state.ShortcutsChanged += ReloadHotkeys;
        RefreshDisplays();
        // Dual-screen topologies change while the app runs (docking, projector plugged in).
        try { Screens.Changed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(RefreshDisplays); }
        catch { /* headless test hosts have no screen manager */ }
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = (Avalonia.Media.IBrush?)this.FindResource("Surface") ?? Avalonia.Media.Brushes.White,
            Content = new TextBlock
            {
                Text = message,
                Margin = new Avalonia.Thickness(24),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            }
        };
        await dialog.ShowDialog(this);
    }

    private async Task<string?> PickSaveFileAsync(string filter, string suggestedName)
    {
        var storage = StorageProvider;
        if (storage is null) return null;
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "保存",
            SuggestedFileName = suggestedName,
            DefaultExtension = Path.GetExtension(suggestedName).TrimStart('.'),
            FileTypeChoices = [BuildType(filter)]
        });
        return file?.TryGetLocalPath();
    }

    private async Task<string?> PickOpenFileAsync(string filter)
    {
        var storage = StorageProvider;
        if (storage is null) return null;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "打开",
            AllowMultiple = false,
            FileTypeFilter = [BuildType(filter), new FilePickerFileType("全部文件") { Patterns = ["*"] }]
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private static FilePickerFileType BuildType(string filter)
    {
        var parts = filter.Split('|');
        var name = parts.Length > 1 ? parts[0] : "文件";
        var patterns = (parts.Length > 1 ? parts[1] : "*").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new FilePickerFileType(name) { Patterns = patterns };
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }

    private async void History_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (HistoryList.SelectedItem is RecentMeetingRow row) await state.SelectHistoryAsync(row.Meta);
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (closing) return;
        // AppState+Lifecycle.swift: a meeting in progress never exits on the window's close button;
        // the window goes to the tray and the recording continues.
        if (state.Recording && !ExitRequested)
        {
            e.Cancel = true;
            HideAllWindows();
            return;
        }
        e.Cancel = true;
        closing = true;
        tray?.Dispose();
        tray = null;
        try
        {
            await state.ShutdownAsync();
        }
        catch
        {
            // Never block exit on shutdown errors; everything already written stays on disk.
        }
        // Floating windows keep the process alive under OnLastWindowClose: close them first,
        // then shut the lifetime down instead of leaving an invisible process behind.
        if (island is { } cue) { cue.AllowClose = true; cue.Close(); }
        if (subtitleBar is { } bar) { bar.AllowClose = true; bar.Close(); }
        teleprompter?.Close();
        (Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown();
        Close();
    }
}
