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
            if (SelfTestOptions.Presenter) state.Settings.PresenterMode = true;
            SetupTray();
            if (SelfTestOptions.Scene is { } scene) state.SetSceneCommand.Execute(scene);
            if (SelfTestOptions.HistoryFilter is { } filter) state.HistoryFilter = filter;
            if (SelfTestOptions.HistoryQuery is { } query) state.HistoryQuery = query;
            if (SelfTestOptions.ImportWave is { } wave) await RunImportWaveTestAsync(wave);
            if (SelfTestOptions.Island) ToggleCueIsland();
            if (SelfTestOptions.Teleprompter) { ToggleTeleprompter(); if (SelfTestOptions.TeleprompterThrough) teleprompter?.ApplyClickThrough(true); }
            if (SelfTestOptions.CaptureTest is { } captureTest) await RunCaptureTestAsync(captureTest);
            if (SelfTestOptions.UpdateCheck is { } feed)
            {
                var update = await state.CheckForUpdatesAsync(feed);
                var target = SelfTestOptions.UpdateReport ?? feed + ".report.json";
                await File.WriteAllTextAsync(target, System.Text.Json.JsonSerializer.Serialize(update, UpdateReport.Options));
            }
            if (SelfTestOptions.CheckReport is { } report) await WriteStartupReportAsync(report);
            if (SelfTestOptions.KnowledgeCheck is { } knowledge) await RunKnowledgeCheckAsync(knowledge);
            if (SelfTestOptions.PresenterCheck is { } presenterCheck) await RunPresenterCheckAsync(presenterCheck);
            if (SelfTestOptions.RelayCheck is { } relayCheck) await RunRelayCheckAsync(relayCheck);
            if (SelfTestOptions.UpdateInstallCheck is { } updateInstall) await RunUpdateInstallCheckAsync(updateInstall);
            if (SelfTestOptions.SubtitleExportCheck is { } subtitleExport) await RunSubtitleExportCheckAsync(subtitleExport);
            if (SelfTestOptions.HotwordsCheck is { } hotwordsCheck) await RunHotwordsCheckAsync(hotwordsCheck);
            if (SelfTestOptions.FormulaCheck is { } formulaCheck) await RunFormulaCheckAsync(formulaCheck);
            if (SelfTestOptions.BackupCheck is { } backupCheck) await RunBackupCheckAsync(backupCheck);
            if (SelfTestOptions.PromptCheck is { } promptCheck) await RunPromptCheckAsync(promptCheck);
            if (SelfTestOptions.ApiCheck is { } apiCheck) await RunApiCheckAsync(apiCheck);
            if (SelfTestOptions.RetrievalCheck is { } retrievalCheck) await RunRetrievalCheckAsync(retrievalCheck);
            if (SelfTestOptions.CloudCheck is { } cloudCheck) await RunCloudCheckAsync(cloudCheck);
            if (SelfTestOptions.StreamCheck is { } streamCheck) await RunStreamCheckAsync(streamCheck);
            if (SelfTestOptions.QuestionCheck is { } questionCheck) await RunQuestionCheckAsync(questionCheck);
            if (SelfTestOptions.PdfCheck is { } pdfCheck) await RunPdfCheckAsync(pdfCheck);
            if (SelfTestOptions.GazeCheck is { } gazeCheck) await RunGazeCheckAsync(gazeCheck);
            if (SelfTestOptions.BpeCheck is { } bpeCheck) await RunBpeCheckAsync(bpeCheck);
            if (SelfTestOptions.GlossarySearchCheck is { } glossarySearch) await RunGlossarySearchCheckAsync(glossarySearch);
            if (SelfTestOptions.ProcessCheck is { } processCheck) await RunProcessCheckAsync(processCheck);
            if (SelfTestOptions.SessionSearchCheck is { } sessionSearch) await RunSessionSearchCheckAsync(sessionSearch);
            if (SelfTestOptions.SeriesBriefingCheck is { } seriesBriefing) await RunSeriesBriefingCheckAsync(seriesBriefing);
            if (SelfTestOptions.HistorySearchCheck is { } historySearch) await RunHistorySearchCheckAsync(historySearch);
            if (SelfTestOptions.BackupManifestCheck is { } backupManifest) await RunBackupManifestCheckAsync(backupManifest);
            if (SelfTestOptions.MeetingPlanCheck is { } meetingPlan) await RunMeetingPlanCheckAsync(meetingPlan);
            if (SelfTestOptions.EvidenceCheck is { } evidence) await RunEvidenceCheckAsync(evidence);
            if (SelfTestOptions.UserContentCheck is { } userContent) await RunUserContentCheckAsync(userContent);
            if (SelfTestOptions.WorkspaceDeletionCheck is { } deletion) await RunWorkspaceDeletionCheckAsync(deletion);
            if (SelfTestOptions.DocumentJobCheck is { } documentJob) await RunDocumentJobCheckAsync(documentJob);
            if (SelfTestOptions.PageCheck is { } pageCheck) await RunPageCheckAsync(pageCheck);
            if (SelfTestOptions.ArchivePersistenceCheck is { } archiveCheck) await RunArchivePersistenceCheckAsync(archiveCheck);
            if (SelfTestOptions.AcceptanceCheck is { } acceptance) await RunAcceptanceCheckAsync(acceptance);
            if (SelfTestOptions.UpdateBackupCheck is { } updateBackup) await RunUpdateBackupCheckAsync(updateBackup);
            if (SelfTestOptions.UpdateRestoreCheck is { } updateRestore) await RunUpdateRestoreCheckAsync(updateRestore);
            if (SelfTestOptions.SignatureCheck is { } signatureCheck) await RunSignatureCheckAsync(signatureCheck);
            if (SelfTestOptions.SlideFollowCheck is { } slideFollow) await RunSlideFollowCheckAsync(slideFollow);
            if (SelfTestOptions.PrepareModelsCheck is { } prepareModels) await RunPrepareModelsCheckAsync(prepareModels);
            if (SelfTestOptions.CompanionCheck is { } companionCheck) await RunCompanionCheckAsync(companionCheck);
            if (SelfTestOptions.ShortcutCheck is { } shortcutCheck) await RunShortcutCheckAsync(shortcutCheck);
            if (SelfTestOptions.DemoCheck is { } demoCheck) await RunDemoCheckAsync(demoCheck);
            if (SelfTestOptions.WindowShotCheck is { } windowShot) await RunWindowShotCheckAsync(windowShot);
            if (SelfTestOptions.UiLanguageCheck is { } uiLanguage) await RunUiLanguageCheckAsync(uiLanguage);
            if (SelfTestOptions.SettingsSearchCheck is { } settingsSearch) await RunSettingsSearchCheckAsync(settingsSearch);
            if (SelfTestOptions.ProviderPresetCheck is { } presetCheck) await RunProviderPresetCheckAsync(presetCheck);
            if (SelfTestOptions.AppearanceCheck is { } appearanceCheck) await RunAppearanceCheckAsync(appearanceCheck);
            if (SelfTestOptions.CloudSettingsCheck is { } cloudSettings) await RunCloudSettingsCheckAsync(cloudSettings);
            if (SelfTestOptions.CloudImportCheck is { } cloudImport) await RunCloudImportCheckAsync(cloudImport);
            if (SelfTestOptions.NotesJumpCheck is { } notesJump) await RunNotesJumpCheckAsync(notesJump);
            if (SelfTestOptions.LiveModeCheck is { } liveMode) await RunLiveModeCheckAsync(liveMode);
            if (SelfTestOptions.ShareHideCheck is { } shareHide) await RunShareHideCheckAsync(shareHide);
            if (SelfTestOptions.WindowCloseCheck is { } closeCheck) await RunWindowCloseCheckAsync(closeCheck);
            if (SelfTestOptions.SubtitleSettingsCheck is { } subtitleCheck) await RunSubtitleSettingsCheckAsync(subtitleCheck);
            if (SelfTestOptions.CaptureHideCheck is { } captureHide) await RunCaptureHideCheckAsync(captureHide);
            if (SelfTestOptions.AudioSwitchCheck is { } audioSwitch) await RunAudioSwitchCheckAsync(audioSwitch);
            if (SelfTestOptions.TeleprompterHandleCheck is { } handleCheck) await RunTeleprompterHandleCheckAsync(handleCheck);
            if (StartupOptions.SelfTest is { Length: > 0 } selfTest) await RunNamedSelfTestAsync(selfTest);
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
