using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NextThought.Core;
using NextThought.Platform;
using Avalonia;
using Avalonia.VisualTree;

namespace NextThought.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly AppState state;
    private readonly Dictionary<AppSection, Control> pages = [];
    private bool closing;
    private bool openedOnce;

    public MainWindow(AppState state)
    {
        this.state = state;
        InitializeComponent();
        DataContext = state;
        ShowSection();
        state.PickSaveFile = PickSaveFileAsync;
        state.PickOpenFile = PickOpenFileAsync;
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
            if (StartupOptions.CaptureHideCheck is { } captureHide) await RunCaptureHideCheckAsync(captureHide);
        };
        Closing += OnClosing;
        Avalonia.Controls.Chrome.WindowDecorationProperties.SetElementRole(TitleBar, WindowDecorationsElementRole.TitleBar);
        Avalonia.Controls.Chrome.WindowDecorationProperties.SetElementRole(MinimizeButton, WindowDecorationsElementRole.MinimizeButton);
        Avalonia.Controls.Chrome.WindowDecorationProperties.SetElementRole(MaximizeButton, WindowDecorationsElementRole.MaximizeButton);
        Avalonia.Controls.Chrome.WindowDecorationProperties.SetElementRole(CloseButton, WindowDecorationsElementRole.CloseButton);
        RegisterHotkeys();
        RefreshDisplays();
        // Dual-screen topologies change while the app runs (docking, projector plugged in).
        try { Screens.Changed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(RefreshDisplays); }
        catch { /* headless test hosts have no screen manager */ }
    }

    private void Minimize_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    /// <summary>Off-screen startup self-check: writes a JSON report and exits, like the WPF baseline.</summary>
    private async Task WriteStartupReportAsync(string path)
    {
        await Task.Delay(400);
        var passed = state.Ready && state.StartupErrorText is null;
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var presenterPlan = CurrentPresenterPlan();
            var report = new StartupReport(
                DateTimeOffset.Now,
                passed,
                "Avalonia 12 startup self-check; layout and interactive acceptance are measured separately",
                Math.Round(Program.Clock.Elapsed.TotalSeconds, 3),
                state.Section.ToString(),
                state.Ready,
                state.StartupErrorText,
                state.History.Count,
                state.Transcript.Count,
                state.InitTimings,
                [
                    "capture-dual", "asr-local", "sessions", "summary-markdown", "scenes-9",
                    "cue-island", "subtitles", "teleprompter", "region-capture", "hotkeys-16",
                    "tray", "hide-all", "auto-end", "notes-templates-9", "notes-fishbone",
                    "notes-outline", "notes-viewpoints", "proofread", "due-dates", "series",
                    "glossary", "update-check", "companion-lan",
                    "workspace-knowledge", "knowledge-merge", "question-bank", "live-translation", "translation-gate",
                    "presenter-mode", "companion-relay", "update-install", "material-hotwords"
                ],
                BuildDisplays().Select(display => new StartupScreen(display.Id, display.Name, display.Bounds.ToString(), display.IsPrimary)).ToList(),
                new StartupPresenter(
                    state.SettingsSnapshot.PresenterMode,
                    state.SettingsSnapshot.PresenterSlidesScreen,
                    presenterPlan.CanActivate,
                    presenterPlan.Camera?.Name,
                    presenterPlan.Slides?.Name,
                    presenterPlan.Summary,
                    presenterActive),
                typeof(AppState).Assembly.GetName().Version?.ToString(3),
                Environment.Version.ToString(),
                Math.Round(Environment.WorkingSet / 1048576.0, 1),
                Math.Round(System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 / 1048576.0, 1),
                Math.Round(GC.GetTotalMemory(false) / 1048576.0, 1),
                Math.Round(GC.GetGCMemoryInfo().TotalCommittedBytes / 1048576.0, 1),
                System.Diagnostics.Process.GetCurrentProcess().Threads.Count,
                Environment.ProcessPath,
                Width,
                Height,
                ClientSize.Width,
                ClientSize.Height,
                RenderScaling,
                Screens.Primary?.Scaling,
                Screens.Primary?.Bounds.ToString());
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, StartupJsonContext.Default.StartupReport));
        }
        catch (Exception exception)
        {
            passed = false;
            try
            {
                File.WriteAllText(path + ".error.txt",
                    "ready=" + state.Ready + "\nstartupError=" + (state.StartupErrorText ?? "") + "\n" + exception);
            }
            catch { }
        }
        Console.WriteLine(passed ? "startup-check: passed" : "startup-check: failed");
        Close();
    }

    /// <summary>
    /// Offline proof of the knowledge loop, with no answer service involved:
    /// template notes → candidates → review → workspace.json → prompt text.
    /// </summary>
    private async Task RunKnowledgeCheckAsync(string path)
    {
        await Task.Delay(300);
        var report = KnowledgeCheck.Run(state);
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            }));
        }
        catch
        {
            report = report with { Passed = false };
        }
        Console.WriteLine(report.Passed ? "knowledge-check: passed" : "knowledge-check: failed");
        Close();
    }

    /// <summary>
    /// Dual-screen self-check on a machine with one display: a synthetic second display goes
    /// through the real layout code, then the window side effects are exercised and the real
    /// single-display topology must refuse to activate.
    /// </summary>
    private async Task RunPresenterCheckAsync(string path)
    {
        await Task.Delay(300);
        var steps = new List<PresenterCheckStep>();
        var real = BuildDisplays().ToList();
        var summary = "";
        try
        {
            var primary = real.FirstOrDefault(display => display.IsPrimary) ?? real.FirstOrDefault()
                          ?? new DisplayInfo("display:0", "主屏", 0, 0, 1920, 1080, true, true);
            var fake = new List<DisplayInfo>(real.Count > 0 ? real : [primary])
            {
                new("fake:external", "外接显示器（自检）", primary.X + primary.Width, primary.Y, 1920, 1080, false, false)
            };
            DisplayCatalog.Refresh(fake);
            state.Settings.PresenterMode = true;
            var plan = CurrentPresenterPlan();
            summary = plan.Summary;
            steps.Add(new PresenterCheckStep("plan-with-second-display", plan.CanActivate && plan.SlidesIsSeparate, plan.Summary));

            ApplyPresenterModeForRecording();
            await Task.Delay(200);
            steps.Add(new PresenterCheckStep("page-steps-aside",
                presenterActive && !IsVisible && island is { IsVisible: true },
                $"active={presenterActive} mainVisible={IsVisible} island={island?.IsVisible}"));

            ExitPresenterMode();
            await Task.Delay(200);
            steps.Add(new PresenterCheckStep("restored", !presenterActive && IsVisible, $"mainVisible={IsVisible}"));

            // Always feed one display: with a real second screen attached, `real` would
            // legitimately activate and the refusal branch would never be exercised.
            var single = PresenterLayout.Resolve([primary], null);
            steps.Add(new PresenterCheckStep("single-display-refuses", !single.CanActivate, single.Summary));
        }
        catch (Exception ex)
        {
            steps.Add(new PresenterCheckStep("exception", false, ex.Message));
        }
        finally
        {
            ExitPresenterMode();
            DisplayCatalog.Refresh(real);
            state.Settings.PresenterMode = false;
        }
        var report = new PresenterCheckReport
        {
            Passed = steps.Count > 0 && steps.All(step => step.Passed),
            Scope = real.Count > 1
                ? "real multi-display topology; window visibility and island, not projector pixels"
                : "synthetic second display; window visibility and island, not real projector output",
            Steps = steps,
            RealScreens = string.Join(" | ", real.Select(display => $"{display.Name} {display.Bounds}")),
            PlanSummary = summary
        };
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            }));
        }
        catch
        {
            // The console line still reports the result if the report file cannot be written.
        }
        Console.WriteLine(report.Passed ? "presenter-check: passed" : "presenter-check: failed");
        Close();
    }

    /// <summary>
    /// End-to-end relay check against a local relay: create a room, publish encrypted state,
    /// wait for the simulated phone's encrypted command, acknowledge it. The phone script
    /// reads the pairing file this writes.
    /// </summary>
    private async Task RunRelayCheckAsync(string path)
    {
        await Task.Delay(300);
        var relayUrl = Environment.GetEnvironmentVariable("NEXTTHOUGHT_RELAY_URL") ?? "http://127.0.0.1:8787";
        var pairingFile = path + ".pairing";
        var handled = new System.Collections.Concurrent.ConcurrentBag<string>();
        CompanionRelayClient? client = null;
        var passed = false;
        var error = "";
        var room = "";
        var status = "";
        try
        {
            client = new CompanionRelayClient(
                () => new CompanionSnapshot("中转自检", "手机上看到的问题", "手机上看到的回答", false, "", "1/3"),
                action => { handled.Add(action); return Task.FromResult(action == "next"); },
                message => Console.WriteLine("relay: " + message));
            var pairing = await client.StartAsync(new Uri(relayUrl), new CompanionPairing(), allowLoopback: true);
            room = pairing.Room;
            var directory = Path.GetDirectoryName(Path.GetFullPath(pairingFile));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(pairingFile, System.Text.Json.JsonSerializer.Serialize(pairing, CompanionCrypto.Json));

            var deadline = DateTime.UtcNow.AddSeconds(20);
            var tick = 0;
            while (DateTime.UtcNow < deadline && !handled.Contains("next"))
            {
                client.Publish(new CompanionSnapshot("中转自检", "第 " + (++tick) + " 次状态", "回答内容", false, "", "1/3"));
                await Task.Delay(400);
            }
            status = client.StatusText;
            passed = handled.Contains("next");
            if (!passed) error = "20 秒内没有收到模拟手机的命令（中转或加密层可能不匹配）。";
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
        finally
        {
            if (client is not null)
            {
                try { await client.StopAsync(); } catch { }
            }
        }
        var report = new
        {
            passed,
            relay = relayUrl,
            room,
            commandHandled = handled.Contains("next"),
            status,
            error,
            pairingFile
        };
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            }));
        }
        catch { }
        Console.WriteLine(passed ? "relay-check: passed" : "relay-check: failed");
        Close();
    }

    /// <summary>
    /// In-app update check: read the release feed, download the installer, verify its SHA-256,
    /// and record the silent-install command instead of running it (QA switch).
    /// </summary>
    private async Task RunUpdateInstallCheckAsync(string path)
    {
        await Task.Delay(200);
        var update = await state.CheckForUpdatesAsync();
        var markerPath = Environment.GetEnvironmentVariable("NEXTTHOUGHT_UPDATE_LAUNCH");
        var marker = "";
        var installerExists = false;
        string? sha = null;
        var error = "";
        try
        {
            if (!state.CanInstallUpdateInApp)
            {
                error = update.Error ?? "这个发布没有安装包或校验文件，不能应用内安装。";
            }
            else
            {
                if (markerPath is { Length: > 0 } && File.Exists(markerPath)) File.Delete(markerPath);
                await state.InstallUpdateCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(90));
                if (markerPath is { Length: > 0 } && File.Exists(markerPath))
                {
                    marker = await File.ReadAllTextAsync(markerPath);
                    using var document = System.Text.Json.JsonDocument.Parse(marker);
                    var root = document.RootElement;
                    installerExists = root.TryGetProperty("installer", out var installer) && File.Exists(installer.GetString());
                    sha = root.TryGetProperty("sha256", out var hash) ? hash.GetString() : null;
                }
                else
                {
                    error = "安装步骤没有写出启动标记。";
                }
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
        var passed = state.UpdateInstallSucceeded && installerExists && sha is { Length: 64 } && error.Length == 0;
        var report = new
        {
            passed,
            update = state.LastUpdateReport,
            installSucceeded = state.UpdateInstallSucceeded,
            installStatus = state.UpdateInstallStatus,
            installerExists,
            sha256 = sha,
            marker,
            error
        };
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            }));
        }
        catch { }
        Console.WriteLine(passed ? "update-install-check: passed" : "update-install-check: failed");
        Close();
    }

    /// <summary>
    /// Exports the loaded session as SRT and VTT and checks the files are real subtitles
    /// (sequential cues, WEBVTT header, UTF-8) without opening a save dialog.
    /// </summary>
    private async Task RunSubtitleExportCheckAsync(string directory)
    {
        await Task.Delay(200);
        var error = "";
        var srtText = "";
        var vttText = "";
        var lines = 0;
        var estimated = 0;
        try
        {
            if (state.CurrentSession is null) error = "没有可导出的会议（用 --demo 或打开已有会议）。";
            else
            {
                var srt = state.ExportCurrentSubtitles(SessionSubtitleFormat.Srt);
                var vtt = state.ExportCurrentSubtitles(SessionSubtitleFormat.Vtt);
                srtText = srt.Text;
                vttText = vtt.Text;
                estimated = srt.EstimatedTimingCount;
                lines = state.CurrentLines.Count;
                Directory.CreateDirectory(directory);
                await File.WriteAllTextAsync(Path.Combine(directory, "demo.srt"), srt.Text, new System.Text.UTF8Encoding(false));
                await File.WriteAllTextAsync(Path.Combine(directory, "demo.vtt"), vtt.Text, new System.Text.UTF8Encoding(false));
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
        var passed = error.Length == 0 && lines > 0
                     && srtText.Contains(" --> ", StringComparison.Ordinal)
                     && srtText.StartsWith("1\n", StringComparison.Ordinal)
                     && vttText.StartsWith("WEBVTT\n\n", StringComparison.Ordinal);
        var report = new
        {
            passed,
            lines,
            estimated,
            srtFirstCue = string.Join("\n", srtText.Split('\n').Take(3)),
            vttHeader = vttText.Length >= 6 ? vttText[..6] : vttText,
            hasTranslation = srtText.Contains("译文：", StringComparison.Ordinal),
            files = new[] { Path.Combine(directory, "demo.srt"), Path.Combine(directory, "demo.vtt") },
            error
        };
        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "export-check.json"),
                System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                }));
        }
        catch { }
        Console.WriteLine(passed ? "subtitle-export-check: passed" : "subtitle-export-check: failed");
        Close();
    }

    /// <summary>
    /// Imports a small text document, then checks that its technical terms reach the recognizer
    /// hotword list together with the glossary (MaterialTerms integration).
    /// </summary>
    private async Task RunHotwordsCheckAsync(string directory)
    {
        await Task.Delay(200);
        var error = "";
        IReadOnlyList<string> material = [];
        IReadOnlyList<string> hotwords = [];
        try
        {
            Directory.CreateDirectory(directory);
            var fixture = Path.Combine(directory, "materials.txt");
            await File.WriteAllTextAsync(fixture,
                "The XL-MIMO testbed uses TSIR and DeepMIMO data; RQ2 is open.\nAgain XL-MIMO and TSIR; RQ2 is answered by the DeepMIMO run.\n",
                new System.Text.UTF8Encoding(false));
            await state.Library.ImportAsync([fixture]);
            state.GlossaryText = "Kubernetes | K8s";
            state.SaveGlossaryCommand.Execute(null);
            material = state.MaterialHotwords();
            hotwords = state.RecognitionHotwords();
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
        var passed = error.Length == 0
                     && new[] { "XL-MIMO", "TSIR", "DeepMIMO", "RQ2" }.All(term => material.Contains(term))
                     && hotwords.Contains("Kubernetes")
                     && hotwords.Contains("XL-MIMO");
        var report = new
        {
            passed,
            materialTerms = material,
            hotwords,
            error
        };
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "hotwords-check.json"),
                System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                }));
        }
        catch { }
        Console.WriteLine(passed ? "hotwords-check: passed" : "hotwords-check: failed");
        Close();
    }

    /// <summary>
    /// Renders a Markdown answer containing inline and display formulas through the real
    /// MarkdownText control, and reports the text the user would see.
    /// </summary>
    private async Task RunFormulaCheckAsync(string path)
    {
        await Task.Delay(150);
        const string input = """
            答案：面积为 $\frac{1}{2}$，函数为 $x^2$。
            $$\sum_{i=1}^{n} i$$
            代码 `$a$` 保持原样。
            """;
        var converted = NextThought.Core.LaTeXMath.Markdown(input);
        var view = new MarkdownText { Text = input };
        var rendered = ReadText(view.Content);

        static string ReadText(object? node) => node switch
        {
            SelectableTextBlock block => block.Inlines is { Count: > 0 }
                ? string.Concat(block.Inlines.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text))
                : block.Text ?? "",
            TextBlock block => block.Inlines is { Count: > 0 }
                ? string.Concat(block.Inlines.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text))
                : block.Text ?? "",
            Border border => ReadText(border.Child),
            Panel panel => string.Join(" ", panel.Children.Select(ReadText)),
            _ => ""
        };
        var passed = converted.Contains("½", StringComparison.Ordinal)
                     && converted.Contains("x²", StringComparison.Ordinal)
                     && converted.Contains("∑ᵢ₌₁ⁿ i", StringComparison.Ordinal)
                     && converted.Contains("`$a$`", StringComparison.Ordinal)
                     && rendered.Contains("x²", StringComparison.Ordinal)
                     && rendered.Contains("½", StringComparison.Ordinal);
        var report = new { passed, converted, rendered };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            }));
        }
        catch { }
        Console.WriteLine(passed ? "formula-check: passed" : "formula-check: failed");
        Close();
    }

    /// <summary>
    /// Creates a restore point with the real store, restores it into a temp folder and checks
    /// dedupe/prune on a second identical snapshot.
    /// </summary>
    private async Task RunBackupCheckAsync(string directory)
    {
        await Task.Delay(120);
        var error = "";
        var passed = false;
        long storage = 0;
        string restored = "";
        try
        {
            Directory.CreateDirectory(directory);
            var store = new NextThought.Core.IncrementalBackupStore(Path.Combine(directory, "store"));
            var files = new Dictionary<string, byte[]>
            {
                ["settings.json"] = System.Text.Encoding.UTF8.GetBytes("{\"theme\":\"light\"}"),
                ["Sessions/a/transcript.jsonl"] = System.Text.Encoding.UTF8.GetBytes("{\"text\":\"你好\"}\n")
            };
            var point = store.Create(files, reason: "check", keep: 2);
            var again = store.Create(files, reason: "check", keep: 2);
            var destination = Path.Combine(directory, "restored");
            store.Restore(point, destination);
            restored = File.ReadAllText(Path.Combine(destination, "settings.json"));
            storage = store.StorageBytes();
            var manifests = Directory.GetFiles(Path.Combine(store.Root, "RestorePoints"), "*.ntrestore").Length;
            passed = point == again && manifests == 1 && restored.Contains("light") && storage > 0
                     && File.Exists(Path.Combine(destination, "Sessions", "a", "transcript.jsonl"));
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
        var report = new { passed, storageBytes = storage, restored, error };
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "backup-check.json"),
                System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                }));
        }
        catch { }
        Console.WriteLine(passed ? "backup-check: passed" : "backup-check: failed");
        Close();
    }

    /// <summary>
    /// Builds a real answer prompt through the ported builder and checks the system section,
    /// the user JSON and the budgets.
    /// </summary>
    private async Task RunPromptCheckAsync(string path)
    {
        await Task.Delay(120);
        var (system, user) = AnswerPromptBuilder.MakeParts(
            "为什么温度补偿后残差仍集中在支座附近？",
            "对方：温度补偿后残差还是集中在支座附近。",
            "我做过连续刚构桥监测。",
            [new AnswerTurn("上一问", "上一答")],
            hasScreenshot: false,
            mode: NextThought.Core.MeetingMode.LabMeeting,
            style: "concise",
            excerpts: [new PromptExcerpt(new string('资', 900), "监测报告", new DocumentLocator { Page = 12 })],
            today: DateTimeOffset.Now);
        var passed = system.Contains("你是 NextThought", StringComparison.Ordinal)
                     && system.Contains("不要使用工具", StringComparison.Ordinal)
                     && user.Contains("为什么温度补偿", StringComparison.Ordinal)
                     && user.Contains("\"labMeeting\"", StringComparison.Ordinal)
                     && user.Contains("\"retrieved_material\"", StringComparison.Ordinal)
                     && system.Length > 2000;
        var report = new { passed, systemChars = system.Length, userChars = user.Length, user };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            }));
        }
        catch { }
        Console.WriteLine(passed ? "prompt-check: passed" : "prompt-check: failed");
        Close();
    }

    /// <summary>Exercises the ported answer-service rules and writes their outcomes.</summary>
    private async Task RunApiCheckAsync(string path)
    {
        await Task.Delay(100);
        var failures = new[]
        {
            (401, "", NextThought.Core.APIFailure.BadKey),
            (404, "model not found", NextThought.Core.APIFailure.BadModel),
            (429, "insufficient quota", NextThought.Core.APIFailure.OutOfCredit),
            (503, "", NextThought.Core.APIFailure.ServerBusy)
        };
        var passed = failures.All(item => NextThought.Core.APIFailureExplainer.Classify(item.Item1, item.Item2) == item.Item3)
                     && NextThought.Core.APIFailureExplainer.Classify(null, "", -1001) == NextThought.Core.APIFailure.Timeout
                     && NextThought.Core.APIProviders.Normalized(" HTTPS://API.Deepseek.com/v1/ ") == "https://api.deepseek.com/v1"
                     && NextThought.Core.APIProviders.Guide("https://api.deepseek.com/v1")?.SuggestedModel == "deepseek-chat"
                     && NextThought.Core.APIKeyInput.Masked("sk-abcdef123456").EndsWith("3456", StringComparison.Ordinal)
                     && NextThought.Core.AnswerBackendDefault.RawValue(null, codexInstalled: true) == NextThought.Core.AnswerBackendDefault.CodexAppServer;
        var report = new
        {
            passed,
            badKey = NextThought.Core.APIFailureExplainer.Message(NextThought.Core.APIFailure.BadKey),
            normalized = NextThought.Core.APIProviders.Normalized(" HTTPS://API.Deepseek.com/v1/ "),
            masked = NextThought.Core.APIKeyInput.Masked("sk-abcdef123456")
        };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "api-check: passed" : "api-check: failed");
        Close();
    }

    /// <summary>Exercises chunking, the semantic index and rank fusion in the shipped process.</summary>
    private async Task RunRetrievalCheckAsync(string path)
    {
        await Task.Delay(100);
        var document = new NextThought.Core.MaterialDocument
        {
            Id = "check",
            Name = "check.pdf",
            Blocks = [new NextThought.Core.DocumentBlock("p.1", "# 方法\n短一\n短二", 1)]
        };
        var chunks = NextThought.Core.ContextChunker.Chunks([document]);
        var index = new NextThought.Core.SemanticIndex();
        index.SetVector([1f, 0f], chunks[0].Fingerprint, 0);
        var ranking = index.RankedOrders([1f, 0f], chunks.Select(chunk => chunk.Fingerprint).ToList());
        var fused = NextThought.Core.HybridRanker.Fuse([0], [0]);
        var pages = new NextThought.Core.MaterialDocument
        {
            Id = "pages",
            Name = "slides.pdf",
            Blocks = [new NextThought.Core.DocumentBlock("p.1", "温度补偿方法", 1), new NextThought.Core.DocumentBlock("p.2", "残差分析", 2)]
        };
        var explicitPage = new NextThought.Core.HybridContextIndex([pages]).Retrieve("第1页讲了什么");
        var passed = chunks.Count == 1
                     && chunks[0].Text.Contains("方法", StringComparison.Ordinal)
                     && chunks[0].Page == 1
                     && ranking.Count == 1 && Math.Abs(ranking[0].Score - 1) < 1e-6
                     && fused.Count == 1 && fused[0].Order == 0
                     && explicitPage.Count > 0 && explicitPage[0].Page == 1;
        var report = new { passed, chunks = chunks.Count, fingerprint = chunks[0].Fingerprint, text = chunks[0].Text, score = ranking.Count > 0 ? ranking[0].Score : 0, explicitPage = explicitPage.Count > 0 ? explicitPage[0].Page : 0 };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "retrieval-check: passed" : "retrieval-check: failed");
        Close();
    }

    /// <summary>Exercises cloud STT provider config and message assembly in the shipped process.</summary>
    private async Task RunCloudCheckAsync(string path)
    {
        await Task.Delay(100);
        var soniox = NextThought.Core.CloudSTTProvider.Soniox.ConfigurationMessage("", "auto", ["NextThought"]);
        var assembler = new NextThought.Core.CloudTranscriptAssembler(NextThought.Core.CloudSTTProvider.Soniox);
        var updates = assembler.Consume("""{"tokens":[{"text":"你","is_final":true},{"text":"好","is_final":false}]}""");
        var resampled = NextThought.Core.PCMResampler.Upsample16kTo24k([0, 30, 60]);
        var passed = soniox is not null && soniox.Contains("language_hints", StringComparison.Ordinal)
                     && updates.Count == 1 && updates[0].Text == "你好" && !updates[0].IsFinal
                     && resampled.Length == 4
                     && NextThought.Core.CloudSTTProvider.Deepgram.AuthorizationHeader("k") == "Token k";
        var report = new { passed, configuration = soniox, live = updates.Count > 0 ? updates[0].Text : "", resampled = resampled.Length };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "cloud-check: passed" : "cloud-check: failed");
        Close();
    }

    /// <summary>Exercises the Codex protocol helpers, SSE parsing and model list in the shipped process.</summary>
    private async Task RunStreamCheckAsync(string path)
    {
        await Task.Delay(100);
        var parsed = NextThought.Core.CodexAppServerProtocol.Parse("""{"id":2,"result":{"thread":{"id":"t1"}}}""");
        var delta = new NextThought.Core.ChatCompletionStreamParser().Parse("""data: {"choices":[{"delta":{"content":"Hi"}}]}""");
        var ids = NextThought.Core.ModelListParser.Ids("""{"data":[{"id":"b"},{"id":"a"}]}""");
        var passed = parsed is { Kind: NextThought.Core.CodexInboundKind.Response, ResponseId: 2 }
                     && NextThought.Core.CodexAppServerProtocol.ThreadIdFromThreadStart(parsed.Result!) == "t1"
                     && delta is NextThought.Core.ChatDelta { Text: "Hi" }
                     && ids.SequenceEqual(["a", "b"])
                     && NextThought.Core.ChatCompletionStreamParser.Endpoint("https://api.example.com/v1") == "https://api.example.com/v1/chat/completions";
        var report = new { passed, thread = NextThought.Core.CodexAppServerProtocol.ThreadIdFromThreadStart(parsed?.Result ?? new System.Text.Json.Nodes.JsonObject()), models = ids };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "stream-check: passed" : "stream-check: failed");
        Close();
    }

    /// <summary>Runs the first-layer rules over the labeled corpus and reports recall/false triggers.</summary>
    private async Task RunQuestionCheckAsync(string path)
    {
        await Task.Delay(250);
        var report = NextThought.Core.QuestionDetectionEvaluator.Evaluate(NextThought.Core.QuestionEvalCorpus.All);
        var passed = NextThought.Core.QuestionEvalCorpus.All.Count >= 100
                     && report.Questions + report.Statements == NextThought.Core.QuestionEvalCorpus.All.Count
                     && report.Detected <= report.Questions
                     && report.FalseTriggers <= report.Statements;
        var payload = new
        {
            passed,
            samples = NextThought.Core.QuestionEvalCorpus.All.Count,
            report.Questions,
            report.Detected,
            report.Statements,
            report.FalseTriggers,
            recall = Math.Round(report.Recall, 3),
            falsePerHour = Math.Round(report.FalseTriggersPerHour, 2),
            missed = report.Missed.Take(5),
            falsePositives = report.FalsePositives.Take(5)
        };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "question-check: passed" : "question-check: failed");
        Close();
    }

    /// <summary>Exercises PDF text layout (furniture, headings, line joining) in the shipped process.</summary>
    private async Task RunPdfCheckAsync(string path)
    {
        await Task.Delay(100);
        var pages = Enumerable.Range(1, 5).Select(page =>
            $"ACME 年报\n甲{page}\n乙{page}\n丙{page}\n丁{page}\n戊{page}\n第 {page} 页").ToList();
        var furniture = NextThought.Core.PDFTextLayout.Furniture(pages);
        var pageText = $"ACME 年报\n摘要\n{new string('X', 60)}\n{new string('Y', 60)}\n{new string('Z', 60)}\n{new string('W', 60)}\n{new string('V', 60)}\n{new string('U', 60)}\n第 6 页";
        var markdown = NextThought.Core.PDFTextLayout.Markdown(pageText, furniture, numberedHeadings: false);
        var passed = furniture.Contains("acme 年报")
                     && !furniture.Contains("甲1")
                     && markdown.Contains("## 摘要", StringComparison.Ordinal)
                     && !markdown.Contains("ACME", StringComparison.Ordinal)
                     && !markdown.Contains("第 6 页", StringComparison.Ordinal);
        var report = new { passed, furniture = furniture.OrderBy(key => key).ToArray(), markdown };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "pdf-check: passed" : "pdf-check: failed");
        Close();
    }

    /// <summary>Exercises eye geometry, calibration and gaze readings in the shipped process.</summary>
    private async Task RunGazeCheckAsync(string path)
    {
        await Task.Delay(100);
        static NextThought.Core.EyeGeometry Eye(double pupilX = 5) => new(
            [new NextThought.Core.GazePoint(0, 2.5), new NextThought.Core.GazePoint(5, 0),
             new NextThought.Core.GazePoint(10, 2.5), new NextThought.Core.GazePoint(5, 5)],
            new NextThought.Core.GazePoint(pupilX, 2.5));
        static NextThought.Core.GazeFrame Frame(double pupilX, double time) =>
            new(Eye(pupilX), Eye(pupilX), 0, 0, time);
        var estimator = new NextThought.Core.GazeEstimator();
        for (var index = 0; index < 9; index++) estimator.AddCalibration(Frame(5 + index * 0.1, index));
        var calibrated = estimator.FinishCalibration();
        var onCamera = estimator.Ingest(Frame(5, 10));
        var away = estimator.Ingest(Frame(9, 11));
        var passed = NextThought.Core.GazeEstimator.Signature(Frame(5, 0)) is not null
                     && calibrated && estimator.IsCalibrated
                     && onCamera.State == NextThought.Core.GazeReading.StateValue.OnCamera
                     && away.State == NextThought.Core.GazeReading.StateValue.Away
                     && away.Deviation > 1;
        var report = new { passed, calibrated, baselineX = estimator.Baseline.X, onCamera = onCamera.State.ToString(), away = away.State.ToString(), deviation = Math.Round(away.Deviation, 3) };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "gaze-check: passed" : "gaze-check: failed");
        Close();
    }

    /// <summary>Builds a tiny tokenizer.json, then encodes and decodes with the ported BPE tokenizer.</summary>
    private async Task RunBpeCheckAsync(string path)
    {
        await Task.Delay(100);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var fixture = Path.Combine(directory ?? Path.GetTempPath(), "tokenizer-check.json");
        await File.WriteAllTextAsync(fixture, """
            {"model":{"type":"BPE","vocab":{"a":0,"b":1,"ab":2,"ä":3,"¸":4,"\u0143":5},"merges":[["a","b"]]},
             "added_tokens":[{"content":"<|endoftext|>","id":6}]}
            """);
        var tokenizer = new NextThought.Core.ByteLevelBPETokenizer(fixture,
            new NextThought.Core.ByteLevelBPETokenizer.Configuration(6, false, @"\S+"));
        var ascii = tokenizer.Encode("ab");
        var chinese = tokenizer.Encode("中");
        var passed = ascii.SequenceEqual([2])
                     && chinese.SequenceEqual([3, 4, 5])
                     && tokenizer.Decode(ascii) == "ab"
                     && tokenizer.Decode(chinese) == "中"
                     && tokenizer.TokenId("ab") == 2;
        var report = new { passed, ascii = ascii.ToArray(), chinese = chinese.ToArray(), decoded = tokenizer.Decode(chinese) };
        try
        {
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "bpe-check: passed" : "bpe-check: failed");
        Close();
    }

    /// <summary>Builds a glossary, expands a misheard search and reports the forms.</summary>
    private async Task RunGlossarySearchCheckAsync(string path)
    {
        await Task.Delay(100);
        var glossary = new NextThought.Core.Glossary([
            new NextThought.Core.GlossaryEntry("Kubernetes", variants: ["K8s"]),
            new NextThought.Core.GlossaryEntry("Open AI", variants: ["open-ai"])
        ]).Normalized();
        var search = glossary.Search("k8s 部署");
        var canonicalHit = glossary.Search("Kubernetes").FirstMatch("k8s cluster");
        var passed = search.Forms.Contains("Kubernetes 部署")
                     && search.Matches("Kubernetes 部署")
                     && canonicalHit is { Form: "K8s", IsMisheard: true }
                     && glossary.EntryMatching("open ai")?.Canonical == "Open AI";
        var report = new { passed, forms = search.Forms, misheard = canonicalHit?.IsMisheard ?? false, canonical = canonicalHit?.Form ?? "" };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "glossary-search-check: passed" : "glossary-search-check: failed");
        Close();
    }

    /// <summary>Reads lines from a stream, waits for a child process and awaits a shared task.</summary>
    private async Task RunProcessCheckAsync(string path)
    {
        await Task.Delay(100);
        var lines = new List<string>();
        using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("alpha\nbeta\r\ngamma")))
            await foreach (var line in NextThought.Core.ProcessLineReader.Lines(stream)) lines.Add(line);

        var status = -1;
        using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c exit 5",
            UseShellExecute = false,
            CreateNoWindow = true
        })!)
        using (var waiter = new NextThought.Core.ProcessExitWaiter(process))
            status = await waiter.WaitAsync();

        var shared = Task.FromResult(42);
        var value = await NextThought.Core.CancellableTask.Value(shared);
        var passed = lines.SequenceEqual(["alpha", "beta", "gamma"]) && status == 5 && value == 42;
        var report = new { passed, lines, status, value };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "process-check: passed" : "process-check: failed");
        Close();
    }

    /// <summary>Searches two archived-meeting field sets through the ported content search.</summary>
    private async Task RunSessionSearchCheckAsync(string path)
    {
        await Task.Delay(100);
        var glossary = new NextThought.Core.Glossary([
            new NextThought.Core.GlossaryEntry("Kubernetes", variants: ["K8s"])
        ]).Normalized();
        var terms = glossary.Search("Kubernetes");
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var documents = new[]
        {
            new NextThought.Core.SessionSearchDocument(first, [
                new NextThought.Core.SessionSearchField(NextThought.Core.SessionSearchFieldKind.Transcript,
                    "去年我们把服务迁到 Kubernetes 上，效果不错。", Location: "12:30", Id: "line:1")]),
            new NextThought.Core.SessionSearchDocument(second, [
                new NextThought.Core.SessionSearchField(NextThought.Core.SessionSearchFieldKind.Notes, "没有相关词", Id: "n")])
        };
        var results = NextThought.Core.SessionContentSearch.Search(documents, terms);
        var hit = results.FirstOrDefault();
        var passed = results.Count == 1
                     && hit is { SessionId: var id } && id == first
                     && hit.Hit.Kind == NextThought.Core.SessionSearchFieldKind.Transcript
                     && hit.Hit.MatchedForm == "Kubernetes"
                     && hit.Hit.Excerpt.Contains("Kubernetes", StringComparison.Ordinal)
                     && hit.Hit.Location == "12:30";
        var report = new { passed, hits = results.Count, kind = hit?.Hit.Kind.ToString() ?? "", excerpt = hit?.Hit.Excerpt ?? "" };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "session-search-check: passed" : "session-search-check: failed");
        Close();
    }

    /// <summary>Builds a series briefing from synthetic sessions and knowledge.</summary>
    private async Task RunSeriesBriefingCheckAsync(string path)
    {
        await Task.Delay(100);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var series = new NextThought.Core.WorkspaceSeries("series", "结构组会", DateTimeOffset.UtcNow,
            [first.ToString("D"), second.ToString("D")],
            [
                new NextThought.Core.WorkspaceLesson { Text = "置顶经验", Pinned = true, CreatedAt = DateTimeOffset.UtcNow.AddDays(-5) },
                new NextThought.Core.WorkspaceLesson { Text = "新经验", CreatedAt = DateTimeOffset.UtcNow.AddDays(-1) }
            ])
        {
            QuestionBank = [new NextThought.Core.QuestionBankItem { Question = "常见问题", Answer = "答案", TimesAsked = 4 }]
        };
        var sessions = new[]
        {
            new NextThought.Core.SessionMeta { Id = first, Title = "第一次", StartedAt = DateTimeOffset.UtcNow.AddDays(-2) },
            new NextThought.Core.SessionMeta { Id = second, Title = "第二次", StartedAt = DateTimeOffset.UtcNow.AddDays(-1) }
        };
        var briefing = NextThought.Core.SeriesBriefingBuilder.Build(series, sessions, meetingLimit: 1, knowledgeLimit: 2);
        var passed = briefing.SessionCount == 2
                     && briefing.LatestSessions.Count == 1 && briefing.LatestSessions[0].Title == "第二次"
                     && briefing.Lessons.Count == 2 && briefing.Lessons[0].Text == "置顶经验"
                     && briefing.QuestionBank.Count == 1 && briefing.QuestionBank[0].TimesAsked == 4;
        var report = new { passed, sessionCount = briefing.SessionCount, latest = briefing.LatestSessions.Select(s => s.Title), lessons = briefing.Lessons.Select(l => l.Text), questions = briefing.QuestionBank.Select(q => q.Question) };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "series-briefing-check: passed" : "series-briefing-check: failed");
        Close();
    }

    /// <summary>Searches prepared history snapshots and checks hit labels/navigation targets.</summary>
    private async Task RunHistorySearchCheckAsync(string path)
    {
        await Task.Delay(100);
        var found = Guid.NewGuid();
        var missing = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var terms = new NextThought.Core.Glossary([new NextThought.Core.GlossaryEntry("Kubernetes", variants: ["K8s"])])
            .Normalized().Search("Kubernetes");
        var snapshots = new Dictionary<Guid, NextThought.Core.SessionSearchSnapshot>
        {
            [found] = new([new NextThought.Core.SessionSearchField(NextThought.Core.SessionSearchFieldKind.Transcript,
                "Kubernetes 部署", Location: "12:00", Id: "line:1", LineId: lineId)])
        };
        var progress = NextThought.Core.SessionSearch.Search([found, missing], terms, snapshots);
        var hit = progress.Hits.GetValueOrDefault(found);
        var passed = progress.Checked == 2 && progress.Unreadable == 1
                     && hit is not null && hit.Label() == "转写"
                     && hit.Target() is NextThought.Core.SessionContentTarget.Transcript target && target.LineId == lineId
                     && hit.Snippet().Contains("Kubernetes", StringComparison.Ordinal);
        var report = new { passed, hits = progress.Hits.Count, unreadable = progress.Unreadable, label = hit?.Label() ?? "", target = hit?.Target().GetType().Name ?? "" };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "history-search-check: passed" : "history-search-check: failed");
        Close();
    }

    /// <summary>Validates a manifest, merges a template and maps legacy settings.</summary>
    private async Task RunBackupManifestCheckAsync(string path)
    {
        await Task.Delay(100);
        var manifest = new NextThought.Core.BackupManifest
        {
            Kind = NextThought.Core.BackupManifest.BackupKind.Full,
            AppVersion = "check",
            AppBuild = "check",
            WorkspaceCount = 2,
            SessionCount = 3
        };
        manifest.Validate();
        var template = new NextThought.Core.NotesTemplate("custom", "模板", []);
        var merge = NextThought.Core.BackupMerge.NotesTemplates([], [template]);
        var restored = NextThought.Core.SettingsSnapshot.ValuesForRestore(new Dictionary<string, object?>
        {
            ["autoApplyLessons"] = true,
            ["notExported"] = 1
        });
        var rejectedNewer = false;
        try
        {
            (manifest with { Version = 2 }).Validate();
        }
        catch (NextThought.Core.BackupManifest.ValidationException)
        {
            rejectedNewer = true;
        }
        var passed = merge.Added == 1 && merge.Templates.Count == 1
                     && restored.Count == 1 && restored["autoCollectKnowledgeCandidates"] is true
                     && !restored.ContainsKey("notExported") && rejectedNewer
                     && NextThought.Core.SettingsSnapshot.IsSecretAccount("answer.api.x")
                     && NextThought.Core.BackupMerge.SessionsToImport(new HashSet<Guid>(), []).Count == 0;
        var report = new { passed, added = merge.Added, restoredKeys = restored.Keys, rejectedNewer };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "backup-manifest-check: passed" : "backup-manifest-check: failed");
        Close();
    }

    /// <summary>Builds a meeting plan and checks the carry snapshot and effective mode.</summary>
    private async Task RunMeetingPlanCheckAsync(string path)
    {
        await Task.Delay(100);
        var series = new NextThought.Core.WorkspaceSeries("series", "组会", DateTimeOffset.UtcNow, ["s1"],
            [new NextThought.Core.WorkspaceLesson { Text = "经验" }])
        {
            QuestionBank = [new NextThought.Core.QuestionBankItem { Question = "问", Answer = "答" }]
        };
        var plan = new NextThought.Core.MeetingPlan
        {
            Mode = NextThought.Core.MeetingMode.Custom,
            CustomBaseMode = NextThought.Core.MeetingMode.Academic,
            Carry = NextThought.Core.MeetingCarry.Standard with { Review = false }
        };
        var prepared = plan.PreparedWorkspace(series);
        var passed = plan.EffectiveMode == NextThought.Core.MeetingMode.Academic
                     && prepared.QuestionBank.Count == 0
                     && prepared.Lessons.Count == 1
                     && series.QuestionBank.Count == 1;
        var report = new { passed, effectiveMode = plan.EffectiveMode.ToString(), lessons = prepared.Lessons.Count, questions = prepared.QuestionBank.Count };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "meeting-plan-check: passed" : "meeting-plan-check: failed");
        Close();
    }

    /// <summary>Builds an evidence reference and checks its provenance and support label.</summary>
    private async Task RunEvidenceCheckAsync(string path)
    {
        await Task.Delay(100);
        var lineId = Guid.NewGuid();
        var target = new NextThought.Core.SessionStructuredNotesTarget(NextThought.Core.NotesFormat.Outline, "sections[0]", "rev");
        var reference = new NextThought.Core.SessionEvidenceReference(
            "claim:1", "结论", "缓存命中率提升", NextThought.Core.SessionEvidenceSupport.Quoted,
            NextThought.Core.SessionEvidenceOrigin.StructuredNotes, LineId: lineId, Quote: "命中率提升",
            Location: "05:20", StructuredTarget: target, TranscriptSourceRevision: "t1",
            SourceStatus: NextThought.Core.SessionDerivedContentStatus.Current,
            ReviewDecision: NextThought.Core.SessionEvidenceDecision.Supported);
        var passed = reference.Support.Label() == "引文在所选转写版本中找到"
                     && reference.LineId == lineId
                     && reference.StructuredTarget == target
                     && reference.ReviewDecision == NextThought.Core.SessionEvidenceDecision.Supported
                     && !reference.ReviewIsStale
                     && NextThought.Core.SessionEvidenceSupport.Unlocated.Label().Contains("待核对");
        var report = new { passed, label = reference.Support.Label(), review = reference.ReviewDecision.ToString() };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "evidence-check: passed" : "evidence-check: failed");
        Close();
    }

    /// <summary>Writes and reloads user content, then checks the revision guard.</summary>
    private async Task RunUserContentCheckAsync(string path)
    {
        await Task.Delay(100);
        var directory = Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), "user-content");
        var saved = NextThought.Core.SessionUserContentStore.Save(new NextThought.Core.SessionUserContent
        {
            PersonalNotes = "我的笔记",
            Highlights = [new NextThought.Core.SessionHighlight { LineId = Guid.NewGuid(), OffsetSeconds = 30, Quote = "重点" }]
        }, directory);
        var loaded = NextThought.Core.SessionUserContentStore.Load(directory);
        var refused = false;
        try
        {
            NextThought.Core.SessionUserContentStore.Save(new NextThought.Core.SessionUserContent { PersonalNotes = "旧草稿" }, directory);
        }
        catch (NextThought.Core.SessionUserContentStore.ChangedElsewhereException)
        {
            refused = true;
        }
        var passed = saved.Revision == 1 && loaded.Revision == 1
                     && loaded.PersonalNotes == "我的笔记" && loaded.Highlights.Count == 1
                     && loaded.HasContent && refused;
        var report = new { passed, revision = loaded.Revision, refused, notes = loaded.PersonalNotes };
        try
        {
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "user-content-check: passed" : "user-content-check: failed");
        Close();
    }

    /// <summary>Stages workspace files, rolls them back, then runs the authorized transaction.</summary>
    private async Task RunWorkspaceDeletionCheckAsync(string path)
    {
        await Task.Delay(100);
        var directory = Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), "deletion");
        Directory.CreateDirectory(Path.Combine(directory, "data"));
        var file = Path.Combine(directory, "data", "a.txt");
        await File.WriteAllTextAsync(file, "A");
        var staging = Path.Combine(directory, "staging");
        var moves = NextThought.Core.WorkspaceDeletionFileStaging.Manifest([file], staging);
        NextThought.Core.WorkspaceDeletionFileStaging.Stage(moves);
        var staged = !File.Exists(file) && File.Exists(moves[0].Staged);
        NextThought.Core.WorkspaceDeletionFileStaging.Rollback(moves);
        var restored = File.Exists(file) && File.ReadAllText(file) == "A" && !Directory.Exists(staging);
        var steps = new List<string>();
        var ran = await NextThought.Core.WorkspaceDeletionTransaction.Execute(
            prepare: () => { steps.Add("prepare"); return Task.FromResult("plan"); },
            isAuthorized: _ => { steps.Add("authorize"); return true; },
            rollback: _ => { steps.Add("rollback"); return Task.CompletedTask; },
            commit: _ => { steps.Add("commit"); return Task.CompletedTask; },
            stage: _ => { steps.Add("stage"); return Task.CompletedTask; });
        var passed = staged && restored && ran && string.Join(",", steps) == "prepare,authorize,stage,authorize,commit";
        var report = new { passed, staged, restored, ran, steps };
        try
        {
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "workspace-deletion-check: passed" : "workspace-deletion-check: failed");
        Close();
    }

    /// <summary>Creates a document job and checks stage titles, progress and terminal states.</summary>
    private async Task RunDocumentJobCheckAsync(string path)
    {
        await Task.Delay(100);
        var job = NextThought.Core.DocumentJob.Create("ws", "slides.pptx") with
        {
            Stage = NextThought.Core.DocumentJob.StageValue.Embedding,
            Completed = 3,
            Total = 10
        };
        var passed = job.Provider == "system"
                     && job.Stage.Title() == "建立语义索引"
                     && job.ProgressText == "建立语义索引 3/10"
                     && !job.IsTerminal
                     && (NextThought.Core.DocumentJob.StageValue.Ready).Terminal()
                     && NextThought.Core.DocumentLimits.MaximumPages == 2000;
        var report = new { passed, stage = job.Stage.Title(), progress = job.ProgressText, terminal = job.IsTerminal };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "document-job-check: passed" : "document-job-check: failed");
        Close();
    }

    /// <summary>Pages are built the first time they are shown; hidden sections cost no controls.</summary>
    private void ShowSection()
    {
        var section = state.Section;
        if (!pages.TryGetValue(section, out var page))
        {
            page = section switch
            {
                AppSection.Home => new HomeView(),
                AppSection.Live => new LiveView(),
                AppSection.Notes => new NotesView(),
                AppSection.History => new HistoryView(),
                AppSection.Series => new SeriesView(),
                AppSection.Library => new LibraryView(),
                AppSection.Settings => new SettingsView(),
                _ => new HomeView()
            };
            pages[section] = page;
        }
        if (!ReferenceEquals(PageHost.Content, page)) PageHost.Content = page;
    }

    /// <summary>Measures the lazy page host: pages built at startup, then working set after visiting all.</summary>
    private async Task RunPageCheckAsync(string path)
    {
        await Task.Delay(150);
        var process = System.Diagnostics.Process.GetCurrentProcess();
        process.Refresh();
        var initialBytes = process.WorkingSet64;
        var initialPages = pages.Count;
        var names = new List<string>();
        foreach (var section in Enum.GetValues<AppSection>())
        {
            state.Section = section;
            await Task.Delay(60);
            if (PageHost.Content is Control page) names.Add(section + ":" + page.GetType().Name);
        }
        state.Section = AppSection.Home;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        process.Refresh();
        var allBytes = process.WorkingSet64;
        var passed = initialPages == 1 && pages.Count == Enum.GetValues<AppSection>().Length
                     && names.Count == pages.Count && names.All(name => name.Contains(':'));
        var report = new
        {
            passed,
            initialPages,
            pageCount = pages.Count,
            initialWorkingSetMiB = Math.Round(initialBytes / 1048576.0, 1),
            allPagesWorkingSetMiB = Math.Round(allBytes / 1048576.0, 1),
            pages = names
        };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "page-check: passed" : "page-check: failed");
        Close();
    }

    /// <summary>Writes archive marker/lines and checks receipts and leftover classification.</summary>
    private async Task RunArchivePersistenceCheckAsync(string path)
    {
        await Task.Delay(100);
        var directory = Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), "archive");
        Directory.CreateDirectory(directory);
        var marker = Path.Combine(directory, ".sessions-archive");
        NextThought.Core.SessionArchiveContentMarker.Record(marker);
        var transcript = Path.Combine(directory, "transcript.jsonl");
        var appender = NextThought.Core.SessionArchiveFileAppender.Live();
        appender.AppendLine("{\"text\":\"一\"}", transcript);
        appender.AppendLine("{\"text\":\"二\"}", transcript);
        appender.Replace(System.Text.Encoding.UTF8.GetBytes("{}"), Path.Combine(directory, "meta.json"));
        var receipts = new NextThought.Core.SessionTranslationWriteReceipts();
        receipts.RecordSubmittedRevision(3);
        var acknowledged = receipts.Acknowledge(3);
        var passed = NextThought.Core.SessionArchiveContentMarker.WasPreviouslyPopulated(marker)
                     && File.ReadAllLines(transcript).Length == 2
                     && File.ReadAllText(Path.Combine(directory, "meta.json")) == "{}"
                     && acknowledged && !receipts.HasPending
                     && NextThought.Core.SessionArchiveLeftover.IsLeftover(["notes.json"])
                     && !NextThought.Core.SessionArchiveLeftover.IsLeftover(["transcript.jsonl"]);
        var report = new { passed, lines = File.ReadAllLines(transcript).Length, acknowledged, leftover = true };
        try
        {
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "archive-persistence-check: passed" : "archive-persistence-check: failed");
        Close();
    }

    /// <summary>One process run over the portable Core features added since 0.47.0.</summary>
    private async Task RunAcceptanceCheckAsync(string path)
    {
        await Task.Delay(100);
        var checks = new List<(string Name, bool Passed)>();
        void Check(string name, bool passed) => checks.Add((name, passed));

        var glossary = new NextThought.Core.Glossary([
            new NextThought.Core.GlossaryEntry("Kubernetes", variants: ["K8s"])
        ]).Normalized();
        var search = glossary.Search("k8s 部署");
        Check("glossary-search", search.Forms.Contains("Kubernetes 部署") && search.Matches("Kubernetes 部署"));

        var fields = new[] { new NextThought.Core.SessionSearchField(NextThought.Core.SessionSearchFieldKind.Transcript, "Kubernetes 部署", Id: "l1") };
        Check("content-search", NextThought.Core.SessionContentSearch.FirstHit(fields, glossary.Search("Kubernetes")) is { Kind: NextThought.Core.SessionSearchFieldKind.Transcript });

        var sessionId = Guid.NewGuid();
        var progress = NextThought.Core.SessionSearch.Search([sessionId],
            glossary.Search("Kubernetes"), new Dictionary<Guid, NextThought.Core.SessionSearchSnapshot>
            {
                [sessionId] = new([new NextThought.Core.SessionSearchField(NextThought.Core.SessionSearchFieldKind.Transcript, "Kubernetes", Id: "l")])
            });
        Check("history-search", progress.Hits.Count == 1 && progress.Hits[sessionId].Label() == "转写");

        var series = new NextThought.Core.WorkspaceSeries("s", "系列", DateTimeOffset.UtcNow, [sessionId.ToString("D")],
            [new NextThought.Core.WorkspaceLesson { Text = "经验", Pinned = true }]);
        var briefing = NextThought.Core.SeriesBriefingBuilder.Build(series,
            [new NextThought.Core.SessionMeta { Id = sessionId, Title = "会议", StartedAt = DateTimeOffset.UtcNow }]);
        Check("series-briefing", briefing.SessionCount == 1 && briefing.Lessons.Count == 1);

        var itemSeries = series with
        {
            SeriesItems = [new NextThought.Core.WorkspaceSeriesItem
            {
                Kind = NextThought.Core.WorkspaceSeriesItem.KindValue.Decision,
                Text = "决定",
                SourceSessionId = sessionId.ToString("D"),
                EvidenceId = "e",
                ClaimRevision = "c",
                SourceRevision = "s"
            }]
        };
        var itemBriefing = NextThought.Core.SeriesBriefingBuilder.Build(itemSeries,
            [new NextThought.Core.SessionMeta { Id = sessionId, Title = "会议", StartedAt = DateTimeOffset.UtcNow }]);
        Check("series-items", itemBriefing.Decisions.Count == 1 && itemBriefing.OpenActions.Count == 0);

        var deletion = NextThought.Core.WorkspaceSessionDeletion.Apply(itemSeries, sessionId.ToString("D"));
        Check("session-deletion", deletion.RemovedSessions == 1
                                  && deletion.Series.SessionIds.Count == 0
                                  && deletion.Series.SourceDeletionTombstones!.Contains(sessionId.ToString("D"))
                                  && deletion.MarkedItems == 1
                                  && deletion.Series.SeriesItems[0].SourceMissing == true);

        var parsedSummary = NextThought.Core.SessionSummaryParser.ParseAll(
            "# 标题\n*复盘*\n# 问题\n1. 为什么？我的回答要点：因为\n# 经验\n- 先演练\n# 题库\n问：q\n答：a");
        Check("summary-parser", parsedSummary.Title == "复盘" && parsedSummary.Questions.Count == 1
                                && parsedSummary.Questions[0].AnswerGist == "因为"
                                && parsedSummary.Lessons.Count == 1 && parsedSummary.Bank.Count == 1);

        var exportProjection = new NextThought.Core.SessionTextProjection(Guid.NewGuid(), "rev", "t1",
            new NextThought.Core.SessionProjectionOptions(), [], []);
        var exported = NextThought.Core.SessionMarkdownExporter.Export("导出", "会议", DateTimeOffset.UtcNow,
            60, "系统音频", exportProjection, [new NextThought.Core.TranscriptLine { Text = "你好", Date = DateTimeOffset.UtcNow }]);
        Check("markdown-export", exported.StartsWith("# 导出", StringComparison.Ordinal)
                                  && exported.Contains("## 完整转录", StringComparison.Ordinal)
                                  && exported.Contains("你好", StringComparison.Ordinal));

        var stagePrompt = NextThought.Core.StageReviewPrompt.Make("系列", "会议", "", "",
            [new NextThought.Core.StageReviewPrompt.SessionDigest("场次", DateTimeOffset.UtcNow, "概要")]);
        Check("stage-review", stagePrompt.StartsWith("场景分区：系列（会议）", StringComparison.Ordinal)
                              && stagePrompt.Contains("## 各次会话记录", StringComparison.Ordinal)
                              && stagePrompt.Contains("### 场次", StringComparison.Ordinal)
                              && NextThought.Core.StageReviewPrompt.System.Contains("## 经验沉淀", StringComparison.Ordinal));

        var summaryPrompt = NextThought.Core.SessionSummaryPrompt.System("面试", "候选人", "回答具体");
        var summaryHeader = NextThought.Core.SessionSummaryPrompt.Header("会议", DateTimeOffset.UtcNow, 30, null, "", []);
        Check("summary-prompt", summaryPrompt.Contains("本次场景：面试", StringComparison.Ordinal)
                                 && summaryPrompt.Contains("## 题库更新", StringComparison.Ordinal)
                                 && summaryHeader.Contains("时长：30 分钟", StringComparison.Ordinal)
                                 && NextThought.Core.SessionSummaryPrompt.Chunks("a\nb", 100).Count == 1);

        var builtProjection = NextThought.Core.SessionProjectionBuilder.Build(Guid.NewGuid(), DateTimeOffset.UtcNow,
            [new NextThought.Core.TranscriptLine { Text = "Kubernetes", Date = DateTimeOffset.UtcNow }], []);
        Check("projection-builder", builtProjection.SearchableText.Any(text => text.Kind == NextThought.Core.SessionProjectionTextKind.Transcript)
                                   && builtProjection.Revision.Length == 64);

        var evidenceSupport = NextThought.Core.SessionEvidenceBuilder.Support("Kubernetes", null,
            [new NextThought.Core.TranscriptLine { Text = "Kubernetes 部署", Date = DateTimeOffset.UtcNow }]);
        var evidenceCurrent = NextThought.Core.SessionEvidenceBuilder.IsReviewCurrent(
            new NextThought.Core.SessionEvidenceReview(NextThought.Core.SessionEvidenceDecision.Supported, "c", "t", "l", DateTimeOffset.UtcNow),
            "c", "t", "l");
        Check("evidence-builder", evidenceSupport == NextThought.Core.SessionEvidenceSupport.Quoted && evidenceCurrent);

        var writeQueue = new NextThought.Core.SessionArchiveWriteQueue(maximumPendingEvents: 2);
        writeQueue.Enqueue("a", [1]);
        writeQueue.Enqueue("b", [2]);
        var overflowedWrite = writeQueue.Enqueue("c", [3]);
        writeQueue.Acknowledge(2);
        var acknowledgedLoss = writeQueue.AcknowledgeDataLossAndResume();
        Check("write-queue", !overflowedWrite.Queued && acknowledgedLoss
                             && writeQueue.Snapshot.AcknowledgedUnqueuedWriteCount == 1
                             && writeQueue.Enqueue("d", [4]).Queued);

        var projectedLines = NextThought.Core.SessionProjectionBuilder.Build(Guid.NewGuid(), DateTimeOffset.UtcNow,
            [new NextThought.Core.TranscriptLine { Text = "库伯奈提斯", Date = DateTimeOffset.UtcNow }], [],
            corrections: [new NextThought.Core.TranscriptCorrection { Line = 0, Original = "库伯奈提斯", Corrected = "Kubernetes" }]);
        var projectedExport = NextThought.Core.SessionMarkdownExporter.Export("导出", "会议", DateTimeOffset.UtcNow,
            0, "", projectedLines, []);
        Check("projected-transcript", projectedLines.Transcript.Count == 1
                                     && projectedExport.Contains("Kubernetes", StringComparison.Ordinal)
                                     && !projectedExport.Contains("库伯奈提斯", StringComparison.Ordinal));

        var notesPayload = new NextThought.Core.StructuredNotesPayload(NextThought.Core.NotesFormat.Outline,
            outline: new NextThought.Core.OutlineNotes(
                [new NextThought.Core.OutlineSection("结论", Points: [new NextThought.Core.OutlinePoint("先演练再上线")])]));
        var notesExport = NextThought.Core.SessionMarkdownExporter.Export("导出", "会议", DateTimeOffset.UtcNow, 0, "",
            new NextThought.Core.SessionTextProjection(Guid.NewGuid(), "r", "t",
                new NextThought.Core.SessionProjectionOptions(), [], []), [], notes: notesPayload);
        Check("notes-export", notesExport.Contains("## 纪要", StringComparison.Ordinal)
                              && notesExport.Contains("先演练再上线", StringComparison.Ordinal));

        var editorPayload = new NextThought.Core.StructuredNotesPayload(NextThought.Core.NotesFormat.Outline,
            outline: new NextThought.Core.OutlineNotes([new NextThought.Core.OutlineSection("旧标题")]));
        var editorTarget = new NextThought.Core.SessionStructuredNotesTarget(NextThought.Core.NotesFormat.Outline,
            "section:0", NextThought.Core.SessionNotesEditor.PayloadRevision(editorPayload));
        var editedPayload = NextThought.Core.SessionNotesEditor.Apply(editorPayload, editorTarget, "新标题");
        Check("notes-editor", editedPayload?.Outline?.Sections[0].Title == "新标题"
                               && NextThought.Core.SessionNotesEditor.Apply(editorPayload,
                                   editorTarget with { PayloadRevision = "stale" }, "x") is null);

        var evidenceNotes = new NextThought.Core.StructuredNotesPayload(NextThought.Core.NotesFormat.Outline,
            outline: new NextThought.Core.OutlineNotes(
                [new NextThought.Core.OutlineSection("结论", Points: [new NextThought.Core.OutlinePoint("先演练再上线")])]));
        var evidenceRefs = NextThought.Core.SessionNotesEvidence.References(evidenceNotes,
            [new NextThought.Core.TranscriptLine { Text = "先演练再上线", Date = DateTimeOffset.UtcNow }], "t1");
        Check("notes-evidence", evidenceRefs.Count == 1
                                && evidenceRefs[0].Support == NextThought.Core.SessionEvidenceSupport.Quoted
                                && !evidenceRefs[0].NeedsConfirmation
                                && evidenceRefs[0].StructuredTarget?.PayloadRevision == NextThought.Core.SessionNotesEditor.PayloadRevision(evidenceNotes));

        var experienceSeries = new NextThought.Core.WorkspaceSeries("s", "组会", DateTimeOffset.UtcNow, [],
            [new NextThought.Core.WorkspaceLesson { Text = "经验", Pinned = true }]);
        var summaryForWrite = new NextThought.Core.SessionSummary { Structured = editorPayload };
        var writtenSummary = NextThought.Core.SessionNotesEditor.UpdateSummary(summaryForWrite, editorTarget, "写回标题");
        Check("summary-writeback", experienceSeries.ExperienceNotes().Contains("经验", StringComparison.Ordinal)
                                   && writtenSummary?.Structured?.Outline?.Sections[0].Title == "写回标题");

        var materialSeries = new NextThought.Core.WorkspaceSeries("s", "组会", DateTimeOffset.UtcNow, [], []);
        var materialText = materialSeries.MaterialText([("role", "岗位")],
            new Dictionary<string, string> { ["role"] = "后端", ["other"] = "备注" });
        Check("workspace-material", materialText == "岗位：后端\nother：备注");

        var prepSystem = NextThought.Core.PreparationPrompt.System(NextThought.Core.MeetingMode.Interview);
        var prepOutput = NextThought.Core.PreparationPrompt.Output(NextThought.Core.MeetingMode.Academic);
        Check("preparation-prompt", prepSystem.Contains("面试", StringComparison.Ordinal)
                                    && prepSystem.Contains("候选人", StringComparison.Ordinal)
                                    && prepOutput.Contains("## 要讲清楚的重点", StringComparison.Ordinal)
                                    && prepOutput.Contains("## 可能被问到的问题", StringComparison.Ordinal));

        var interviewProfile = NextThought.Core.ModeProfile.For(NextThought.Core.MeetingMode.Interview);
        Check("mode-profiles", interviewProfile.Fields.Count == 6
                              && interviewProfile.Fields[0].Id == "domain"
                              && interviewProfile.DocumentCategories.Contains("简历")
                              && interviewProfile.TemplatePreferences.StartsWith("我是候选人", StringComparison.Ordinal));

        var slidePages = new[]
        {
            new NextThought.Core.SlidePage { Page = 1, PageCount = 2, DocumentName = "talk.pptx", Text = "缓存一致性 读写时序" }
        };
        var rankedSlides = NextThought.Core.SlideMatcher.Rank("缓存一致性 读写时序", slidePages);
        var slideDecision = NextThought.Core.SlideMatcher.Decide(rankedSlides);
        Check("slide-matching", rankedSlides.Count == 1
                                 && slideDecision.Kind == NextThought.Core.SlideDecisionKind.Page
                                 && slideDecision.Page?.Page == 1
                                 && NextThought.Core.SlideMatcher.IsDeck("talk.pptx", 2));

        var searchFieldsProjection = NextThought.Core.SessionProjectionBuilder.Build(Guid.NewGuid(), DateTimeOffset.UtcNow,
            [new NextThought.Core.TranscriptLine { Text = "Kubernetes", Date = DateTimeOffset.UtcNow }], []);
        var searchFields = NextThought.Core.SessionContentSearch.Fields(searchFieldsProjection);
        Check("content-fields", searchFields.Count == 1
                                && NextThought.Core.SessionContentSearch.FirstHit(searchFields, glossary.Search("Kubernetes")) is not null);

        var answerState = new NextThought.Core.AnswerState(Guid.NewGuid(), "问题");
        var searchDecision = NextThought.Core.SearchDecision.Parse("{\"needsSearch\":true,\"reason\":\"需要最新\"}", "问题");
        Check("answer-evidence", answerState.Phase == NextThought.Core.AnswerState.PhaseValue.Waiting
                                  && answerState.Evidence.RequestId == answerState.Id
                                  && searchDecision is { Search: true, Query: "问题" });

        var loadedMeta = new NextThought.Core.SessionMeta { Id = Guid.NewGuid(), Title = "会话", StartedAt = DateTimeOffset.UtcNow };
        var loadedSnapshot = NextThought.Core.SessionSearch.FromSession(new NextThought.Core.SessionData(loadedMeta,
            [new NextThought.Core.TranscriptLine { Text = "Kubernetes", Date = DateTimeOffset.UtcNow }], [], ""));
        var loadedHits = NextThought.Core.SessionSearch.Search([loadedMeta.Id], glossary.Search("Kubernetes"),
            new Dictionary<Guid, NextThought.Core.SessionSearchSnapshot> { [loadedMeta.Id] = loadedSnapshot });
        Check("session-snapshot", loadedSnapshot.Fields.Count > 0 && loadedHits.Hits.Count == 1);

        var snapshotCache = new NextThought.Core.SessionSearchCache(4_000);
        var cacheId = Guid.NewGuid();
        var cached = snapshotCache.Add(cacheId, "v1", new NextThought.Core.SessionSearchSnapshot(
            [new NextThought.Core.SessionSearchField(NextThought.Core.SessionSearchFieldKind.Transcript, "缓存", Id: "line:1")]));
        Check("session-cache", cached && snapshotCache.Get(cacheId) is not null
                              && snapshotCache.Bytes > 0 && snapshotCache.Count == 1);

        var metadataPath = Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), "meta.json");
        var metadataRead = NextThought.Core.SessionArchiveMetadataReader.Read(metadataPath,
            _ => "{}"u8.ToArray(), bytes => System.Text.Json.JsonDocument.Parse(bytes), _ => false);
        var legacyAnswers = NextThought.Core.LegacySessionAnswerMigration.Events(["答案"], DateTimeOffset.UtcNow);
        Check("archive-metadata", metadataRead is NextThought.Core.SessionArchiveMetadataReadResult<System.Text.Json.JsonDocument>.Success
                                  && legacyAnswers.Count == 1
                                  && legacyAnswers[0].Question.Contains("旧版回答", StringComparison.Ordinal));

        var preset = new NextThought.Core.SessionPreset
        {
            Name = "技术面",
            Mode = NextThought.Core.SessionMode.Interview,
            Documents = [new NextThought.Core.ContextDocument { Name = "简历", Pages = ["内容"] }]
        };
        Check("session-preset", preset.Mode.ToMeetingMode() == NextThought.Core.MeetingMode.Interview
                                && preset.EnabledDocumentCount == 1
                                && NextThought.Core.SessionPreset.TemplatePreferences(NextThought.Core.SessionMode.Interview)
                                    .StartsWith("我是候选人", StringComparison.Ordinal));

        var originalRoot = Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), "Originals");
        Directory.CreateDirectory(originalRoot);
        var originalFile = Path.Combine(originalRoot, "shared.pdf");
        await File.WriteAllTextAsync(originalFile, "x");
        var originalMoves = NextThought.Core.WorkspaceDeletionFileStaging.Manifest([originalFile],
            Path.Combine(originalRoot, "..", "staging-shared"));
        var deletingJob = NextThought.Core.DocumentJob.Create("w", "shared.pdf") with
        {
            OriginalPath = originalFile,
            SourceHash = "hash"
        };
        var keptMove = NextThought.Core.WorkspaceDeletionFileStaging.ExcludingSharedOriginals(originalMoves,
            [deletingJob], [deletingJob], new HashSet<string>(), originalRoot);
        var retainedMove = NextThought.Core.WorkspaceDeletionFileStaging.ExcludingSharedOriginals(originalMoves,
            [deletingJob], [NextThought.Core.DocumentJob.Create("w", "other.pdf") with { SourceHash = "hash" }],
            new HashSet<string>(), originalRoot);
        Check("shared-originals", keptMove.Count == 1 && retainedMove.Count == 0);

        var guessedMode = NextThought.Core.MeetingTypeGuesser.Guess("字节跳动 后端二面");
        var seriesCandidate = new NextThought.Core.SeriesMatcher.Candidate(Guid.NewGuid(), "字节跳动 · 后端",
            NextThought.Core.SessionMode.Interview, DateTimeOffset.UtcNow);
        var matchedSeries = NextThought.Core.SeriesMatcher.BestMatch("字节跳动 · 后端三面",
            NextThought.Core.SessionMode.Interview, [seriesCandidate]);
        Check("meeting-type", guessedMode == NextThought.Core.SessionMode.Interview
                               && matchedSeries == seriesCandidate.Id
                               && NextThought.Core.SeriesMatcher.BaseTitle("第 3 讲 线性代数") == "线性代数");

        var localSeries = new NextThought.Core.WorkspaceSeries("a", "本地系列", DateTimeOffset.UtcNow, ["s1"], []);
        var backupSeries = new NextThought.Core.WorkspaceSeries("a", "备份系列", DateTimeOffset.UtcNow.AddDays(-1), ["s2"], [])
        {
            SourceDeletionTombstones = ["s3"]
        };
        var mergeResult = NextThought.Core.BackupMerge.Workspaces([localSeries], [backupSeries]);
        Check("backup-merge", mergeResult.Merged.Count == 1
                              && mergeResult.Merged[0].Name == "本地系列"
                              && mergeResult.Merged[0].SessionIds.SequenceEqual(["s1", "s2"])
                              && mergeResult.Merged[0].SourceDeletionTombstones!.Contains("s3")
                              && mergeResult.Updated == 1);

        var notesFieldsProjection = NextThought.Core.SessionProjectionBuilder.Build(Guid.NewGuid(), DateTimeOffset.UtcNow,
            [new NextThought.Core.TranscriptLine { Text = "正文", Date = DateTimeOffset.UtcNow }], []);
        var notesFieldsPayload = new NextThought.Core.StructuredNotesPayload(NextThought.Core.NotesFormat.Outline,
            outline: new NextThought.Core.OutlineNotes(
                [new NextThought.Core.OutlineSection("结论", Points: [new NextThought.Core.OutlinePoint("灰度发布")])]));
        var notesFields = NextThought.Core.SessionContentSearch.Fields(notesFieldsProjection, notesFieldsPayload, [], "t1");
        Check("content-notes-fields", notesFields.Any(field => field.Kind == NextThought.Core.SessionSearchFieldKind.Notes
                                                               && field.Text == "灰度发布")
                                     && NextThought.Core.SessionContentSearch.FirstHit(notesFields, glossary.Search("灰度"))?.Kind
                                         == NextThought.Core.SessionSearchFieldKind.Notes);

        var carrySeries = new NextThought.Core.WorkspaceSeries("c", "系列", DateTimeOffset.UtcNow, [], [])
        {
            DocumentIds = ["doc-1"]
        };
        var carriedDocuments = NextThought.Core.MeetingCarry.Standard.WorkspaceSnapshot(carrySeries, new HashSet<string>());
        var droppedDocuments = (NextThought.Core.MeetingCarry.Standard with { Materials = false })
            .WorkspaceSnapshot(carrySeries, new HashSet<string>());
        Check("meeting-carry-materials", carriedDocuments.DocumentIds.Count == 1 && droppedDocuments.DocumentIds.Count == 0);

        Check("export-formats", NextThought.Core.SessionExportFormat.Markdown.FileExtension() == "md"
                                && NextThought.Core.SessionExportFormat.Docx.FileExtension() == "docx"
                                && NextThought.Core.SessionExportFormat.Docx.Title() == "Word 文档"
                                && NextThought.Core.SessionExportFormat.Vtt.Title() == "WebVTT 字幕");

        var evidenceItem = new NextThought.Core.WorkspaceSeriesItem
        {
            Kind = NextThought.Core.WorkspaceSeriesItem.KindValue.Decision,
            Text = "决定",
            SourceSessionId = sessionId.ToString("D"),
            EvidenceId = "e",
            ClaimRevision = "c",
            SourceRevision = "s",
            TranscriptSourceRevision = "t"
        };
        var evidenceSeries = series with { SeriesItems = [evidenceItem] };
        var supportedEvidence = new NextThought.Core.SessionEvidenceReference("e", "标题", "决定",
            NextThought.Core.SessionEvidenceSupport.Quoted, NextThought.Core.SessionEvidenceOrigin.TextNotes,
            ReviewDecision: NextThought.Core.SessionEvidenceDecision.Supported,
            ClaimRevision: "c", SourceRevision: "s", TranscriptSourceRevision: "t");
        var supportedBriefing = NextThought.Core.SeriesBriefingBuilder.Build(evidenceSeries,
            [new NextThought.Core.SessionMeta { Id = sessionId, Title = "会议", StartedAt = DateTimeOffset.UtcNow }],
            evidenceReferences: [supportedEvidence]);
        var staleBriefing = NextThought.Core.SeriesBriefingBuilder.Build(evidenceSeries,
            [new NextThought.Core.SessionMeta { Id = sessionId, Title = "会议", StartedAt = DateTimeOffset.UtcNow }],
            evidenceReferences: []);
        Check("briefing-evidence", supportedBriefing.Decisions.Count == 1
                                   && staleBriefing.Decisions.Count == 0
                                   && staleBriefing.StaleItems.Count == 1);

        var projectionNotesPayload = new NextThought.Core.StructuredNotesPayload(NextThought.Core.NotesFormat.Outline,
            outline: new NextThought.Core.OutlineNotes(
                [new NextThought.Core.OutlineSection("结论", Points: [new NextThought.Core.OutlinePoint("投影笔记")])]));
        var notesProjection = NextThought.Core.SessionProjectionBuilder.Build(Guid.NewGuid(), DateTimeOffset.UtcNow,
            [], [], notes: projectionNotesPayload);
        Check("projection-notes", notesProjection.Notes == projectionNotesPayload
                                  && notesProjection.SearchableText.Any(text => text.Kind == NextThought.Core.SessionProjectionTextKind.Notes
                                                                                 && text.Text.Contains("投影笔记", StringComparison.Ordinal)));

        var archivePath = Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), "acceptance.ntbackup");
        NextThought.Core.BackupArchive.Write(archivePath,
            new NextThought.Core.BackupManifest { WorkspaceCount = 1, SessionCount = 0 },
            new NextThought.Core.WorkspaceData([
                new NextThought.Core.WorkspaceSeries("a", "系列", DateTimeOffset.UtcNow, [], [])
            ]),
            new Dictionary<string, object?> { ["appearance"] = "dark", ["notExported"] = 1 });
        var archiveManifest = NextThought.Core.BackupArchive.ReadManifest(archivePath);
        Check("backup-archive", archiveManifest?.WorkspaceCount == 1 && File.Exists(archivePath));

        var follower = new NextThought.Core.SlideFollower([
            new NextThought.Core.SlidePage { Page = 1, PageCount = 2, Text = "缓存一致性 读写时序" }
        ]);
        var followedPage = follower.Observe("缓存一致性 读写时序");
        Check("slide-follower", followedPage?.Page == 1
                                && follower.LastDecision?.Kind == NextThought.Core.SlideDecisionKind.Page);

        Check("ocr-spacing", NextThought.Core.TextSimilarity.Terms("缓 存 一 致 性").Contains("缓存"));

        var documentsSeries = new NextThought.Core.WorkspaceSeries("d", "系列", DateTimeOffset.UtcNow, [], [])
        {
            Documents =
            [
                new NextThought.Core.ContextDocument { Name = "简历", Enabled = true },
                new NextThought.Core.ContextDocument { Name = "JD", Enabled = false }
            ]
        };
        Check("workspace-documents", documentsSeries.DocumentsOf().Count == 2
                                     && documentsSeries.DocumentsOf(enabledOnly: true).Count == 1);

        var prepSeries = new NextThought.Core.WorkspaceSeries("p", "系列", DateTimeOffset.UtcNow, [], [])
        {
            DocumentIds = ["doc-1", "doc-2"],
            Documents =
            [
                new NextThought.Core.ContextDocument { Id = "doc-1", Name = "简历" },
                new NextThought.Core.ContextDocument { Id = "doc-2", Name = "新资料" }
            ]
        };
        var prepPlan = new NextThought.Core.MeetingPlan
        {
            BaselineDocumentIds = new HashSet<string> { "doc-1" },
            Carry = NextThought.Core.MeetingCarry.Standard with { Transcript = true }
        };
        var prepResult = prepPlan.PreparedWorkspace(prepSeries,
            new NextThought.Core.ContextDocument { Id = "transcript", Name = "上一场转写" });
        Check("meeting-prepare", prepResult.Documents.Count == 2
                                 && prepResult.Documents.Any(document => document.Id == "doc-1")
                                 && prepResult.DocumentIds.Contains("transcript"));

        var queueDirectory = Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), "job-queue");
        var jobQueue = new NextThought.Core.DocumentJobQueue(queueDirectory);
        jobQueue.Clear();
        var queuedJob = NextThought.Core.DocumentJob.Create("ws", "a.pdf") with
        {
            Id = "acceptance-job", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        var queued = jobQueue.Enqueue(queuedJob);
        var reloadedQueue = new NextThought.Core.DocumentJobQueue(queueDirectory);
        Check("document-queue", queued && reloadedQueue.Count == 1 && reloadedQueue.Next()?.Id == "acceptance-job");

        var changeCandidate = new NextThought.Core.WorkspaceSeriesChangeCandidate
        {
            Id = "change-1",
            Kind = NextThought.Core.WorkspaceSeriesItem.KindValue.Action,
            Text = "补充压测",
            SourceSessionId = sessionId.ToString("D"),
            EvidenceId = "e1",
            TranscriptSourceRevision = "t1"
        };
        var (changedSeries, changeReceipt) = NextThought.Core.WorkspaceSeriesChangeMerger.Apply(
            series with { SeriesChangeCandidates = [changeCandidate] }, "change-1",
            NextThought.Core.WorkspaceSeriesChangeDecision.Add);
        Check("series-change", changeReceipt is not null
                               && changedSeries.SeriesItems.Count == 1
                               && changedSeries.SeriesChangeCandidates[0].Status
                                   == NextThought.Core.WorkspaceSeriesChangeCandidate.StatusValue.Accepted);

        var collectReference = new NextThought.Core.SessionEvidenceReference("notes:actions:1", "待办", "发布",
            NextThought.Core.SessionEvidenceSupport.Quoted, NextThought.Core.SessionEvidenceOrigin.TextNotes,
            LineId: Guid.NewGuid(), Kind: NextThought.Core.NotesSectionKind.Actions, Owner: "小王", Due: "周五",
            ClaimRevision: "c1", SourceRevision: "s1", TranscriptSourceRevision: "t1",
            ReviewDecision: NextThought.Core.SessionEvidenceDecision.Supported);
        var (collectedSeries, collectedIds) = NextThought.Core.WorkspaceSeriesChangeMerger.CollectCandidates(
            series, [collectReference], sessionId.ToString("D"));
        Check("series-change-collect", collectedIds.Count == 1
                                        && collectedSeries.SeriesChangeCandidates.Count == 1
                                        && collectedSeries.SeriesChangeCandidates[0].Kind
                                            == NextThought.Core.WorkspaceSeriesItem.KindValue.Action);

        var ownershipSeries = new NextThought.Core.WorkspaceSeries("owner", "系列", DateTimeOffset.UtcNow, [], [])
        {
            Documents = [new NextThought.Core.ContextDocument { Id = "owner-doc", Name = "资料" }]
        };
        var archivedDocuments = NextThought.Core.WorkspaceArchiveOwnership.Repair(ownershipSeries,
            [new NextThought.Core.MaterialDocument { Id = "owner-doc", Name = "资料.pdf" }],
            Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), "materials"));
        var ownershipUpdated = NextThought.Core.WorkspaceArchiveOwnership.ApplyMarkdownPaths(ownershipSeries, archivedDocuments);
        Check("archive-ownership", archivedDocuments.Count == 1
                                   && ownershipUpdated.Documents[0].MarkdownPath == archivedDocuments[0].MarkdownPath
                                   && File.Exists(archivedDocuments[0].MarkdownPath));

        var recordMeta = new NextThought.Core.SessionMeta
        {
            Id = Guid.NewGuid(), Title = "组会", StartedAt = DateTimeOffset.UtcNow.AddHours(-1)
        };
        var recordData = new NextThought.Core.SessionData(recordMeta,
            [new NextThought.Core.TranscriptLine { Text = "先演练", Date = recordMeta.StartedAt }],
            [new NextThought.Core.AnswerRecord
            {
                Id = Guid.NewGuid(), Date = recordMeta.StartedAt, Question = "怎么上线？", Answer = "先灰度"
            }],
            "摘要");
        var sessionRecord = NextThought.Core.SessionRecord.FromSessionData(recordData);
        Check("session-record", sessionRecord.Id == recordMeta.Id
                                && sessionRecord.ToSessionData().Answers.Count == 1
                                && sessionRecord.Summary == "摘要"
                                && sessionRecord.Transcript.Count == 1);

        var (webKey, webEnabled) = NextThought.Platform.WebSearchConfiguration.FromEnvironment(_ => null);
        Check("web-search-config", webKey is null && !webEnabled
                                   && NextThought.Platform.WebSearchConfiguration.CreateProvider(read: _ => null) is null);

        Check("web-search-setting", new NextThought.Core.AppSettings().WebSearch
                                     && !new NextThought.Core.AppSettings { WebSearch = false }.WebSearch);

        var manifest = new NextThought.Core.BackupManifest { WorkspaceCount = 1, SessionCount = 1 };
        var rejected = false;
        try { (manifest with { Version = 2 }).Validate(); } catch (NextThought.Core.BackupManifest.ValidationException) { rejected = true; }
        Check("backup-manifest", rejected && NextThought.Core.SettingsSnapshot.IsSecretAccount("answer.api.x"));

        var plan = new NextThought.Core.MeetingPlan { Mode = NextThought.Core.MeetingMode.Custom, CustomBaseMode = NextThought.Core.MeetingMode.Academic };
        Check("meeting-plan", plan.EffectiveMode == NextThought.Core.MeetingMode.Academic);

        Check("evidence", NextThought.Core.SessionEvidenceSupport.Quoted.Label().Contains("引文"));

        var temporary = Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), "acceptance");
        var saved = NextThought.Core.SessionUserContentStore.Save(new NextThought.Core.SessionUserContent { PersonalNotes = "笔记" }, temporary);
        Check("user-content", saved.Revision == 1 && NextThought.Core.SessionUserContentStore.Load(temporary).PersonalNotes == "笔记");

        var migrationRoot = Path.Combine(temporary, "migration-sessions");
        NextThought.Core.SessionArchiveMigrationPublisher.Publish(["m"], migrationRoot,
            (_, folder) =>
            {
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "meta.json"), "{}");
            },
            (_, _) => { });
        Check("migration-publish", File.Exists(Path.Combine(migrationRoot, "m", "meta.json"))
                                   && NextThought.Core.SessionArchiveMigrationPublisher.PendingRecoveryDirectory(migrationRoot) is null);


        var deleteRoot = Path.Combine(temporary, "delete");
        Directory.CreateDirectory(deleteRoot);
        var file = Path.Combine(deleteRoot, "a.txt");
        await File.WriteAllTextAsync(file, "A");
        var moves = NextThought.Core.WorkspaceDeletionFileStaging.Manifest([file], Path.Combine(deleteRoot, "staging"));
        NextThought.Core.WorkspaceDeletionFileStaging.Stage(moves);
        NextThought.Core.WorkspaceDeletionFileStaging.Rollback(moves);
        Check("workspace-deletion", File.Exists(file) && File.ReadAllText(file) == "A");

        var job = NextThought.Core.DocumentJob.Create("w", "a.pdf") with { Stage = NextThought.Core.DocumentJob.StageValue.Embedding, Completed = 1, Total = 2 };
        Check("document-job", job.ProgressText == "建立语义索引 1/2");

        var archive = Path.Combine(temporary, "archive");
        var transcript = Path.Combine(archive, "transcript.jsonl");
        NextThought.Core.SessionArchiveFileAppender.Live().AppendLine("{}", transcript);
        NextThought.Core.SessionArchiveContentMarker.Record(Path.Combine(archive, ".marker"));
        Check("archive", File.ReadAllLines(transcript).Length == 1 && NextThought.Core.SessionArchiveLeftover.IsLeftover(["notes.json"]));

        var ownedSeries = new NextThought.Core.WorkspaceSeries("own", "归档系列", DateTimeOffset.UtcNow, [], []);
        var owned = NextThought.Core.WorkspaceArchiveOwnership.Repair(ownedSeries,
            [new NextThought.Core.MaterialDocument { Id = "doc", Name = "资料" }], Path.Combine(temporary, "materials"));
        Check("archive-ownership", owned.Count == 1 && File.Exists(owned[0].MarkdownPath) && File.Exists(owned[0].MetadataPath));

        var zipformer = NextThought.Core.OnlineTransducerModel.LegacyBilingual(Path.Combine(temporary, "model"));
        var hotwords = NextThought.Core.ZipformerHotwords.Clean("Kubernetes，开源");
        Check("zipformer", zipformer.RequiredFiles.Count == 4
                           && zipformer.ModelType == "zipformer"
                           && hotwords.SequenceEqual(["Kubernetes", "开源"])
                           && NextThought.Core.ZipformerModelFiles.SampleRate == 16000);

        var sessionPaths = new NextThought.Platform.AppPaths(Path.Combine(temporary, "session-store"));
        var sessionStore = new NextThought.Platform.SessionStore(sessionPaths);
        var storedMeta = new NextThought.Core.SessionMeta
        {
            Id = Guid.NewGuid(),
            Title = "验收会议",
            StartedAt = DateTimeOffset.UtcNow,
            EndedAt = DateTimeOffset.UtcNow
        };
        await sessionStore.CreateAsync(storedMeta);
        await sessionStore.AppendTranscriptAsync(storedMeta, new NextThought.Core.TranscriptLine
        {
            Id = Guid.NewGuid(), Text = "Kubernetes 部署", Date = DateTimeOffset.UtcNow, StartedAt = DateTimeOffset.UtcNow, EndedAt = DateTimeOffset.UtcNow
        });
        await sessionStore.EndAsync(storedMeta);
        var stored = sessionStore.Load(storedMeta.Id);
        Check("session-store", stored.Meta.Title == "验收会议" && stored.Lines.Count == 1
                               && stored.Lines[0].Text == "Kubernetes 部署");

        var updateApp = Path.Combine(temporary, "app");
        Directory.CreateDirectory(updateApp);
        await File.WriteAllTextAsync(Path.Combine(updateApp, "NextThought.exe"), "old");
        var backup = NextThought.Core.UpdateBackup.Create(updateApp, Path.Combine(temporary, "backups"), "1.0.0");
        await File.WriteAllTextAsync(Path.Combine(updateApp, "NextThought.exe"), "new");
        NextThought.Core.UpdateBackup.Restore(backup.Path, updateApp);
        Check("update-backup", File.ReadAllText(Path.Combine(updateApp, "NextThought.exe")) == "old");

        var projection = new NextThought.Core.SessionTextProjection(sessionId, "rev", "t1",
            new NextThought.Core.SessionProjectionOptions(), [],
            [new NextThought.Core.SessionProjectedText("n", NextThought.Core.SessionProjectionTextKind.Notes, "n",
                SourceStatus: NextThought.Core.SessionDerivedContentStatus.Stale)]);
        Check("projection", projection.DerivedContentStatus == NextThought.Core.SessionDerivedContentStatus.Stale
                            && new NextThought.Core.SessionProjectedAnswer(Guid.NewGuid(), DateTimeOffset.UtcNow, "q", "a", "r",
                                NextThought.Core.AnswerCompletion.Interrupted).IsComplete == false);

        var passed = checks.All(check => check.Passed);
        var report = new
        {
            passed,
            total = checks.Count,
            failed = checks.Where(check => !check.Passed).Select(check => check.Name).ToList(),
            checks = checks.Select(check => new { check.Name, check.Passed })
        };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? $"acceptance-check: passed ({checks.Count})" : "acceptance-check: failed");
        Close();
    }


    /// <summary>Copies a program directory, replaces a file and restores the backup.</summary>
    private async Task RunUpdateBackupCheckAsync(string path)
    {
        await Task.Delay(100);
        var directory = Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), "update-backup");
        var app = Path.Combine(directory, "app");
        Directory.CreateDirectory(Path.Combine(app, "runtimes"));
        await File.WriteAllTextAsync(Path.Combine(app, "NextThought.exe"), "old");
        await File.WriteAllTextAsync(Path.Combine(app, "runtimes", "native.dll"), "native");
        var backup = NextThought.Core.UpdateBackup.Create(app, Path.Combine(directory, "backups"), "0.64.0");
        await File.WriteAllTextAsync(Path.Combine(app, "NextThought.exe"), "new");
        NextThought.Core.UpdateBackup.Restore(backup.Path, app);
        var passed = File.ReadAllText(Path.Combine(app, "NextThought.exe")) == "old"
                     && File.ReadAllText(Path.Combine(app, "runtimes", "native.dll")) == "native"
                     && NextThought.Core.UpdateBackup.List(Path.Combine(directory, "backups")).Count == 1;
        var report = new { passed, backup = Path.GetFileName(backup.Path), restored = "old" };
        try
        {
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "update-backup-check: passed" : "update-backup-check: failed");
        Close();
    }


    /// <summary>Writes the post-exit restore script and checks its wait/copy/start steps.</summary>
    private async Task RunUpdateRestoreCheckAsync(string path)
    {
        await Task.Delay(100);
        var backup = Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), "backup");
        var target = Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), "target");
        var executable = Path.Combine(target, "NextThought.exe");
        var script = NextThought.Core.UpdateRestore.Script(backup, target, executable, 1234);
        var passed = script.Contains("Wait-Process -Id 1234")
                     && script.Contains("Copy-Item -LiteralPath '" + backup + "'")
                     && script.Contains("Start-Process -FilePath '" + executable + "'");
        var report = new { passed, script = script.Replace("\r\n", "\n").Split('\n') };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "update-restore-check: passed" : "update-restore-check: failed");
        Close();
    }


    /// <summary>Reports the Authenticode status of a file and whether signatures are required.</summary>
    private async Task RunSignatureCheckAsync(string path)
    {
        await Task.Delay(100);
        var status = "Unknown";
        var error = "";
        try
        {
            status = await NextThought.Platform.Authenticode.StatusAsync(path);
        }
        catch (Exception exception)
        {
            error = exception.Message;
        }
        var passed = error.Length == 0 && status.Length > 0;
        var report = new
        {
            passed,
            status,
            valid = NextThought.Platform.Authenticode.IsValid(status),
            required = NextThought.Platform.Authenticode.IsRequired,
            error
        };
        try
        {
            var directory = Path.GetDirectoryName(path);
            var target = Path.Combine(directory ?? Path.GetTempPath(), "signature-check.json");
            await File.WriteAllTextAsync(target, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "signature-check: passed (" + status + ")" : "signature-check: failed");
        Close();
    }


    /// <summary>Shows slide text on screen, captures it, OCRs it and follows the matching page.</summary>
    private async Task RunSlideFollowCheckAsync(string path)
    {
        await Task.Delay(300);
        var passed = false;
        var error = "";
        var screenText = "";
        SlidePage? matched = null;
        var window = new Window
        {
            Width = 900,
            Height = 260,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Topmost = true,
            Content = new TextBlock
            {
                Text = "缓存一致性 读写时序 方案",
                FontSize = 56,
                Margin = new Thickness(40),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            }
        };
        try
        {
            window.Show();
            await Task.Delay(800);
            var capture = ScreenCapture.VirtualScreen();
            var imagePath = path + ".png";
            capture.Image.Save(imagePath, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            window.Close();
            var reader = new DocumentReader();
            var (blocks, warnings) = await reader.ReadAsync(imagePath);
            screenText = string.Join("\n", blocks.Select(block => block.Text));
            var pages = new[] { new SlidePage { Page = 1, PageCount = 2, DocumentName = "check.pptx", Text = "缓存一致性 读写时序 方案" } };
            var follower = new SlideFollower(pages);
            matched = follower.Observe(screenText);
            passed = matched?.Page == 1;
            if (!passed) error = warnings.Count > 0 ? string.Join("；", warnings) : "OCR 文本未匹配到幻灯片。";
        }
        catch (Exception exception)
        {
            error = exception.Message;
            try { window.Close(); } catch { }
        }
        var report = new { passed, ocrChars = screenText.Length, matchedPage = matched?.Page, error };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "slide-follow-check: passed" : "slide-follow-check: failed");
        Close();
    }


    /// <summary>Installs the streaming recognizer and (when enabled) the offline refinement model.</summary>
    private async Task RunPrepareModelsCheckAsync(string path)
    {
        await Task.Delay(300);
        var ids = new List<string>();
        var installed = new List<object>();
        var error = "";
        var seconds = 0.0;
        try
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var settings = state.Settings.Build();
            ids.Add(settings.LocalModel);
            if (settings.SecondPass) ids.Add("xasrOffline");
            var progress = new Progress<NextThought.Core.ModelProgress>(item => state.SetStatus(item.Message));
            foreach (var id in ids)
            {
                await state.Models.InstallAsync(id, progress);
                installed.Add(new
                {
                    id,
                    title = NextThought.Platform.ModelStore.Package(id).Title,
                    isInstalled = state.Models.IsInstalled(id),
                    directory = state.Models.DirectoryFor(id),
                    bytes = Directory.Exists(state.Models.DirectoryFor(id))
                        ? Directory.EnumerateFiles(state.Models.DirectoryFor(id), "*", SearchOption.AllDirectories)
                            .Sum(file => new FileInfo(file).Length)
                        : 0
                });
            }
            seconds = Math.Round(watch.Elapsed.TotalSeconds, 1);
        }
        catch (Exception exception)
        {
            error = exception.ToString();
        }
        var passed = error.Length == 0 && ids.Count > 0 && installed.Count == ids.Count;
        var report = new { passed, ids, installed, seconds, error, dataDirectory = state.Paths.Root };
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "prepare-models-check: passed" : "prepare-models-check: failed");
        Close();
    }


    /// <summary>Exercises the LAN companion channel without a phone: pairing token, snapshot, action and dedup.</summary>
    private async Task RunCompanionCheckAsync(string path)
    {
        await Task.Delay(300);
        var actions = new List<string>();
        var error = "";
        var passed = false;
        var unauthorized = 0;
        var duplicate = false;
        CompanionServer? server = null;
        try
        {
            server = new CompanionServer(System.Net.IPAddress.Loopback, action =>
            {
                lock (actions) actions.Add(action);
                return Task.FromResult(true);
            });
            server.Publish(new CompanionSnapshot(Status: "自检中", Question: "问题", Answer: "回答"));
            var pairing = server.PairingUrl[(server.PairingUrl.IndexOf("#p=", StringComparison.Ordinal) + 3)..];
            var padded = pairing.Replace('-', '+').Replace('_', '/');
            padded += new string('=', (4 - padded.Length % 4) % 4);
            using var pairingJson = System.Text.Json.JsonDocument.Parse(Convert.FromBase64String(padded));
            var token = pairingJson.RootElement.GetProperty("token").GetString()!;
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var baseUri = server.BaseUri.ToString().TrimEnd('/');
            using var snapshot = await client.GetAsync($"{baseUri}/snapshot?t={token}");
            var snapshotBody = await snapshot.Content.ReadAsStringAsync();
            var unauthorizedResponse = await client.GetAsync($"{baseUri}/snapshot");
            unauthorized = (int)unauthorizedResponse.StatusCode;
            var id = Guid.NewGuid().ToString("D");
            using var first = await client.PostAsync($"{baseUri}/action?id={id}&a=shorter&t={token}", new StringContent(""));
            using var second = await client.PostAsync($"{baseUri}/action?id={id}&a=shorter&t={token}", new StringContent(""));
            duplicate = second.StatusCode == System.Net.HttpStatusCode.NoContent && actions.Count == 1;
            using var snapshotJson = System.Text.Json.JsonDocument.Parse(snapshotBody);
            var snapshotOk = snapshotJson.RootElement.TryGetProperty("snapshot", out var stateElement)
                             && stateElement.TryGetProperty("status", out var statusElement)
                             && statusElement.GetString() == "自检中";
            passed = snapshot.IsSuccessStatusCode && snapshotOk
                     && first.StatusCode == System.Net.HttpStatusCode.NoContent && duplicate && unauthorized == 403;
        }
        catch (Exception exception)
        {
            error = exception.ToString();
        }
        finally
        {
            if (server is not null) await server.DisposeAsync();
        }
        var report = new { passed, actions, duplicate, unauthorized, error };
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "companion-check: passed" : "companion-check: failed");
        Close();
    }


    /// <summary>Shows the floating cue island and verifies the OS excludes it from capture.</summary>
    private async Task RunCaptureHideCheckAsync(string path)
    {
        await Task.Delay(300);
        var affinity = 0u;
        var passed = false;
        var error = "";
        var showActivated = true;
        var topmost = false;
        var onScreen = false;
        Window? probe = null;
        try
        {
            probe = new CueIslandWindow(state) { Width = 360, Height = 120 };
            probe.Show();
            await Task.Delay(600);
            affinity = WindowCaptureHiding.Affinity(probe);
            showActivated = probe.ShowActivated;
            topmost = probe.Topmost;
            onScreen = Screens.All.Any(screen => screen.Bounds.X <= probe.Position.X
                && probe.Position.X < screen.Bounds.X + screen.Bounds.Width
                && screen.Bounds.Y <= probe.Position.Y
                && probe.Position.Y < screen.Bounds.Y + screen.Bounds.Height);
            passed = affinity == 0x11 && !showActivated && topmost && onScreen && probe.IsVisible;
        }
        catch (Exception exception)
        {
            error = exception.ToString();
        }
        finally
        {
            probe?.Close();
        }
        var report = new { passed, affinity, showActivated, topmost, onScreen, error };
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }
        catch { }
        Console.WriteLine(passed ? "capture-hide-check: passed" : "capture-hide-check: failed");
        Close();
    }

    // The design-time constructor required by the XAML compiler for previews.







    public MainWindow() : this(new AppState()) { }

    private CueIslandWindow? island;
    private SubtitleBarWindow? subtitleBar;
    private GlobalHotkeys? hotkeys;
    private TeleprompterWindow? teleprompter;
    private TrayIntegration? tray;
    private bool hiddenAll;

    /// <summary>Ctrl+Alt shortcuts for the actions the Mac app binds to ⌘⇧ (plan §7 table).</summary>
    private void RegisterHotkeys()
    {
        hotkeys = new GlobalHotkeys(
        [
            new GlobalHotkeys.Binding("Ctrl+Alt+R 开始或停止记录", 0x52, ToggleRecording),
            new GlobalHotkeys.Binding("Ctrl+Alt+I 显示或隐藏提示岛", 0x49, ToggleCueIsland),
            new GlobalHotkeys.Binding("Ctrl+Alt+M 切换会中窗口", 0x4D, () => state.CycleLiveModeCommand.Execute(null)),
            new GlobalHotkeys.Binding("Ctrl+Alt+S 区域截图", 0x53, () => _ = CaptureRegionAsync()),
            new GlobalHotkeys.Binding("Ctrl+Alt+U 提词器", 0x55, ToggleTeleprompter),
            new GlobalHotkeys.Binding("Ctrl+Alt+\\ 隐藏或恢复所有窗口", 0xDC, ToggleHideAll),
            new GlobalHotkeys.Binding("Ctrl+Alt+N 唤起主窗口", 0x4E, RestoreMainWindow),
            new GlobalHotkeys.Binding("Ctrl+Alt+H 复制当前回答", 0x48, () => state.CopyAnswerCommand.Execute(null)),
            new GlobalHotkeys.Binding("Ctrl+Alt+G 重新生成上一题", 0x47, () => state.RegenerateAnswerCommand.Execute(null)),
            new GlobalHotkeys.Binding("Ctrl+Alt+K 把回答改短", 0x4B, () => state.ShorterAnswerCommand.Execute(null)),
            new GlobalHotkeys.Binding("Ctrl+Alt+X 取消当前回答", 0x58, () => state.CancelCommand.Execute(null)),
            new GlobalHotkeys.Binding("Ctrl+Alt+Left 上一条回答", 0x25, () => state.PreviousAnswerCommand.Execute(null)),
            new GlobalHotkeys.Binding("Ctrl+Alt+Right 下一条回答", 0x27, () => state.NextAnswerCommand.Execute(null)),
            new GlobalHotkeys.Binding("Ctrl+Alt+J 提词浮窗鼠标穿透", 0x4A, ToggleTeleprompterClickThrough),
            new GlobalHotkeys.Binding("Ctrl+Alt+W 窗口截图", 0x57, () => _ = CaptureWindowAsync()),
            new GlobalHotkeys.Binding("Ctrl+Alt+Space 暂停或恢复聆听", 0x20, () => state.TogglePauseListeningCommand.Execute(null))
        ]);
        if (hotkeys.Unregistered.Count > 0)
            state.SetStatus("以下全局快捷键被其他程序占用：" + string.Join("、", hotkeys.Unregistered) + "；界面按钮仍可使用。", true);
    }

    /// <summary>Tray icon: restore, hide everything while recording continues, record toggle, quit.</summary>
    private void SetupTray()
    {
        tray = new TrayIntegration(RestoreMainWindow, HideAllWindows, ToggleRecording, RequestExit);
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.Recording))
            {
                tray?.SetRecording(state.Recording);
                UpdateCueIslandForMeeting();
            }
            else if (e.PropertyName == nameof(AppState.Answering) || e.PropertyName == nameof(AppState.DetectedQuestion))
            {
                if (state.Recording && !hiddenAll) ShowCueIsland();
            }
        };
    }

    /// <summary>一键隐藏：主窗口与全部浮窗一起收起，会议继续记录（plan §9）。</summary>
    private void HideAllWindows()
    {
        hiddenAll = true;
        island?.Hide();
        subtitleBar?.Hide();
        teleprompter?.Hide();
        Hide();
        state.SetStatus("所有窗口已收起，会议继续记录。Ctrl+Alt+\\ 或托盘图标可恢复。");
    }

    private void ToggleHideAll()
    {
        if (hiddenAll)
        {
            hiddenAll = false;
            RestoreMainWindow();
            ApplyLiveMode(state.LiveMode);
            return;
        }
        HideAllWindows();
    }

    private void RequestExit()
    {
        ExitRequested = true;
        Close();
    }

    public bool ExitRequested { get; private set; }

    private void ToggleRecording()
    {
        if (state.Recording) { if (state.StopRecordingCommand.CanExecute(null)) state.StopRecordingCommand.Execute(null); }
        else if (state.StartRecordingCommand.CanExecute(null)) state.StartRecordingCommand.Execute(null);
        else state.SetStatus("还不能开始记录：请先在设置里准备语音组件。", true);
    }

    private void RestoreMainWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    /// <summary>
    /// 区域截图（方案第 9 节）：先隐藏自己的窗口再抓屏，框选后裁切，然后存入资料库并由
    /// Windows.Media.Ocr 读取文字。
    /// </summary>
    /// <summary>Capture the window that had focus before NextThought was minimized (Mac captureWindow).</summary>
    public async Task CaptureWindowAsync()
    {
        if (state.Working) { state.SetStatus("当前有任务在进行，稍后再截图。"); return; }
        var wasVisible = IsVisible;
        try
        {
            WindowState = WindowState.Minimized;
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, Avalonia.Threading.DispatcherPriority.Background);
            await Task.Delay(360);
            var bounds = ScreenCapture.ForegroundWindowBounds();
            if (bounds.Width <= 1 || bounds.Height <= 1) { state.SetStatus("没有找到可截图的窗口。", true); return; }
            var capture = ScreenCapture.VirtualScreen();
            if (wasVisible) { WindowState = WindowState.Normal; Show(); Activate(); }
            var cropped = ScreenCapture.Crop(capture, new Avalonia.PixelRect(
                bounds.X - capture.X, bounds.Y - capture.Y, bounds.Width, bounds.Height));
            var path = Path.Combine(Path.GetTempPath(), $"nextthought-window-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            cropped.Save(path, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            await state.Library.ImportAsync([path]);
            state.Section = AppSection.Library;
            state.SetStatus("窗口截图已存入资料库，并已用系统 OCR 读取其中的文字。");
        }
        catch (Exception ex)
        {
            if (wasVisible) { WindowState = WindowState.Normal; Show(); Activate(); }
            state.SetStatus("窗口截图失败：" + ex.Message, true);
        }
    }

    public async Task CaptureRegionAsync()
    {
        if (state.Working) { state.SetStatus("当前有任务在进行，稍后再截图。"); return; }
        var wasVisible = IsVisible;
        try
        {
            WindowState = WindowState.Minimized; // definitely off the screen, unlike a deferred Hide()
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, Avalonia.Threading.DispatcherPriority.Background);
            await Task.Delay(360);
            var capture = ScreenCapture.VirtualScreen();
            if (wasVisible) { WindowState = WindowState.Normal; Show(); Activate(); }
            var picker = new RegionCaptureWindow(capture);
            var cropped = await picker.ShowDialog<Avalonia.Media.Imaging.Bitmap?>(this);
            if (cropped is null) { state.SetStatus("已取消截图。"); return; }
            var path = Path.Combine(Path.GetTempPath(), $"nextthought-shot-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            cropped.Save(path, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            await state.Library.ImportAsync([path]);
            state.Section = AppSection.Library;
            state.SetStatus("截图已存入资料库，并已用系统 OCR 读取其中的文字。");
        }
        catch (Exception ex)
        {
            if (wasVisible) { WindowState = WindowState.Normal; Show(); Activate(); }
            state.SetStatus("截图失败：" + ex.Message, true);
        }
    }

    /// <summary>
    /// Unattended end-to-end check of the desktop transcription path: import a WAV, wait for the
    /// session to be archived, then record the outcome next to the input.
    /// </summary>
    private async Task RunImportWaveTestAsync(string wave)
    {
        var report = wave + ".json";
        try
        {
            await state.ImportWaveFileAsync(wave);
            var session = state.CurrentSession;
            var body = System.Text.Json.JsonSerializer.Serialize(new
            {
                importedAt = DateTimeOffset.Now,
                wave,
                sessionId = session?.Id.ToString("D"),
                title = session?.Title,
                transcriptRows = state.Transcript.Count,
                historyCount = state.History.Count,
                summaryState = session?.SummaryState,
                status = state.Status,
                dataRoot = state.Paths.Root
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(report, body);
        }
        catch (Exception ex)
        {
            await File.WriteAllTextAsync(report, "{\"error\":" + System.Text.Json.JsonSerializer.Serialize(ex.Message) + "}");
        }
        Close();
    }

    /// <summary>Unattended capture check: grab the screen, crop the centre, save it and OCR it.</summary>
    private async Task RunCaptureTestAsync(string path)
    {
        try
        {
            Hide();
            await Task.Delay(250);
            var capture = ScreenCapture.VirtualScreen();
            Show();
            var rect = new PixelRect(capture.Width / 4, capture.Height / 4, capture.Width / 2, capture.Height / 2);
            var cropped = ScreenCapture.Crop(capture, rect);
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            cropped.Save(path, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            await state.Library.ImportAsync([path]);
            var report = System.Text.Json.JsonSerializer.Serialize(new
            {
                capturedAt = DateTimeOffset.Now,
                screenWidth = capture.Width,
                screenHeight = capture.Height,
                cropWidth = rect.Width,
                cropHeight = rect.Height,
                saved = File.Exists(path),
                bytes = File.Exists(path) ? new FileInfo(path).Length : 0,
                materials = state.Library.Materials.Count
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(path + ".json", report);
        }
        catch (Exception ex)
        {
            await File.WriteAllTextAsync(path + ".json", "{\"error\":" + System.Text.Json.JsonSerializer.Serialize(ex.Message) + "}");
        }
        Close();
    }

    /// <summary>Floating teleprompter window (plan §9 提词器).</summary>
    public void ToggleTeleprompter()
    {
        if (teleprompter is { IsVisible: true })
        {
            // Hiding must also leave click-through, otherwise Ctrl+Alt+U cannot bring it back.
            teleprompter.DisableClickThrough();
            teleprompter.Hide();
            return;
        }
        if (teleprompter is null)
        {
            var window = new TeleprompterWindow(state);
            window.Closed += (_, _) => { if (ReferenceEquals(teleprompter, window)) teleprompter = null; };
            teleprompter = window;
        }
        teleprompter.Show();
        teleprompter.ApplyClickThrough(teleprompter.ClickThrough);
    }

    /// <summary>Show or hide the floating cue island (the Mac app's notch panel).</summary>
    private void ToggleTeleprompterClickThrough()
    {
        if (teleprompter is null || !teleprompter.IsVisible)
        {
            ToggleTeleprompter();
            if (teleprompter is null || !teleprompter.IsVisible) return;
        }
        teleprompter.ToggleClickThrough();
        state.SetStatus(teleprompter.ClickThrough ? "提词浮窗已开启鼠标穿透，Ctrl+Alt+J 可恢复。" : "提词浮窗已恢复鼠标操作。");
    }

    public void ToggleCueIsland()
    {
        if (island is { IsVisible: true }) { island.Hide(); return; }
        ShowCueIsland();
    }

    /// <summary>Meeting start shows the island automatically; stop hides it (W2).</summary>
    private void UpdateCueIslandForMeeting()
    {
        if (state.Recording)
        {
            if (!hiddenAll) ShowCueIsland();
        }
        else if (island is { IsVisible: true })
        {
            island.Hide();
        }
    }

    public void ShowCueIsland()
    {
        if (hiddenAll) return;
        island ??= new CueIslandWindow(state);
        if (!island.IsVisible) island.Show();
    }

    // ---- 双屏演讲（PresenterMode.swift） ----

    private bool presenterActive;

    /// <summary>The displays as PresenterLayout sees them (primary = the one with the camera).</summary>
    public IReadOnlyList<DisplayInfo> BuildDisplays()
    {
        try
        {
            return Screens.All.Select((screen, index) => new DisplayInfo(
                // Avalonia 12's Screen has no DisplayIndex; the origin is stable for a given topology.
                $"display:{screen.Bounds.X}x{screen.Bounds.Y}",
                screen.IsPrimary ? "主屏（摄像头）" : $"显示器 {index + 1}",
                screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height,
                screen.IsPrimary,
                // Avalonia does not expose the built-in panel; in the common laptop case it is the primary display.
                screen.IsPrimary)).ToList();
        }
        catch
        {
            return [];
        }
    }

    private void RefreshDisplays()
    {
        try
        {
            DisplayCatalog.Refresh(BuildDisplays());
            state.Settings.RefreshScreens();
        }
        catch
        {
            // The window may not have a screen manager yet during construction.
        }
        if (!presenterActive) return;
        var plan = CurrentPresenterPlan();
        if (!plan.CanActivate) ExitPresenterMode("外接屏已断开，双屏演讲已退出，主窗口已恢复。");
        else state.SetStatus("双屏演讲：" + plan.Summary + "；Ctrl+Alt+N 唤回主窗口。");
    }

    private PresenterPlan CurrentPresenterPlan() =>
        PresenterLayout.Resolve(DisplayCatalog.Displays, state.SettingsSnapshot.PresenterSlidesScreen);

    /// <summary>
    /// Mac PresenterMode.begin: with a second display the island carries the meeting on the
    /// camera display and the page steps aside. One display only: nothing is hidden, because
    /// hiding the page would hide the meeting itself.
    /// </summary>
    private void ApplyPresenterModeForRecording()
    {
        if (presenterActive || !state.SettingsSnapshot.PresenterMode) return;
        var plan = CurrentPresenterPlan();
        if (!plan.CanActivate)
        {
            state.SetStatus("双屏演讲：" + plan.Summary + "（会议窗口保持显示）");
            return;
        }
        presenterActive = true;
        if (island is not { IsVisible: true })
        {
            island ??= new CueIslandWindow(state);
            island.Show();
        }
        Hide();
        state.SetStatus($"双屏演讲：{plan.Summary}；Ctrl+Alt+N 唤回主窗口。");
    }

    private void ExitPresenterMode(string? message = null)
    {
        if (!presenterActive) return;
        presenterActive = false;
        Show();
        Activate();
        state.SetStatus(message ?? "双屏演讲已结束，主窗口已恢复。");
    }

    /// <summary>The subtitle bar follows the three-state live window mode from Core.</summary>
    private void ApplyLiveMode(LiveWindowMode mode)
    {
        if (mode == LiveWindowMode.Notes)
        {
            if (subtitleBar is { IsVisible: true }) subtitleBar.Hide();
            return;
        }
        if (mode == LiveWindowMode.Hidden)
        {
            if (subtitleBar is { IsVisible: true }) subtitleBar.Hide();
            return;
        }
        subtitleBar ??= new SubtitleBarWindow(state);
        if (!subtitleBar.IsVisible) subtitleBar.Show();
    }

    private void HandleInitialized()
    {
        if (state.StartupErrorText is { } error) state.SetStatus(error, true);
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
        island?.Close();
        subtitleBar?.Close();
        teleprompter?.Close();
        (Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown();
        Close();
    }
}
