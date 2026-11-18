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
    private bool closing;
    private bool openedOnce;

    public MainWindow(AppState state)
    {
        this.state = state;
        InitializeComponent();
        DataContext = state;
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
            if (e.PropertyName == nameof(AppState.LiveMode)) ApplyLiveMode(state.LiveMode);
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
            var report = new
            {
                testedAt = DateTimeOffset.Now,
                passed,
                scope = "Avalonia 12 startup self-check; layout and interactive acceptance are measured separately",
                seconds = Math.Round(Program.Clock.Elapsed.TotalSeconds, 3),
                section = state.Section.ToString(),
                ready = state.Ready,
                error = state.StartupErrorText,
                historyCount = state.History.Count,
                transcriptCount = state.Transcript.Count,
                initTimings = state.InitTimings,
                // What this build actually carries, so a self-check report is also a capability report.
                features = new[]
                {
                    "capture-dual", "asr-local", "sessions", "summary-markdown", "scenes-9",
                    "cue-island", "subtitles", "teleprompter", "region-capture", "hotkeys-7",
                    "tray", "hide-all", "auto-end", "notes-templates-9", "notes-fishbone",
                    "notes-outline", "notes-viewpoints", "proofread", "due-dates", "series",
                    "glossary", "update-check", "companion-lan",
                    "workspace-knowledge", "knowledge-merge", "question-bank", "live-translation", "translation-gate",
                    "presenter-mode", "companion-relay", "update-install", "material-hotwords"
                },
                screens = BuildDisplays().Select(display => new
                {
                    id = display.Id,
                    name = display.Name,
                    bounds = display.Bounds,
                    primary = display.IsPrimary
                }),
                presenter = new
                {
                    enabled = state.SettingsSnapshot.PresenterMode,
                    preferred = state.SettingsSnapshot.PresenterSlidesScreen,
                    canActivate = presenterPlan.CanActivate,
                    camera = presenterPlan.Camera?.Name,
                    slides = presenterPlan.Slides?.Name,
                    summary = presenterPlan.Summary,
                    active = presenterActive
                },
                version = typeof(AppState).Assembly.GetName().Version?.ToString(3),
                framework = Environment.Version.ToString(),
                workingSetMiB = Math.Round(Environment.WorkingSet / 1048576.0, 1),
                privateMiB = Math.Round(System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 / 1048576.0, 1),
                gcHeapMiB = Math.Round(GC.GetTotalMemory(false) / 1048576.0, 1),
                gcCommittedMiB = Math.Round(GC.GetGCMemoryInfo().TotalCommittedBytes / 1048576.0, 1),
                threads = System.Diagnostics.Process.GetCurrentProcess().Threads.Count,
                process = Environment.ProcessPath,
                windowWidth = Width,
                windowHeight = Height,
                clientWidth = ClientSize.Width,
                clientHeight = ClientSize.Height,
                renderScaling = RenderScaling,
                screenScaling = Screens.Primary?.Scaling,
                screenBounds = Screens.Primary?.Bounds.ToString()
            };
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            passed = false;
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

            var single = PresenterLayout.Resolve(real, null);
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
            new GlobalHotkeys.Binding("Ctrl+Alt+O 显示或隐藏提词岛", 0x4F, ToggleCueIsland),
            new GlobalHotkeys.Binding("Ctrl+Alt+M 纪要窗口/字幕条/隐藏", 0x4D, () => state.CycleLiveModeCommand.Execute(null)),
            new GlobalHotkeys.Binding("Ctrl+Alt+S 区域截图", 0x53, () => _ = CaptureRegionAsync()),
            new GlobalHotkeys.Binding("Ctrl+Alt+T 提词器", 0x54, ToggleTeleprompter),
            new GlobalHotkeys.Binding("Ctrl+Alt+\\ 隐藏或恢复所有窗口", 0xDC, ToggleHideAll),
            new GlobalHotkeys.Binding("Ctrl+Alt+N 唤起主窗口", 0x4E, RestoreMainWindow)
        ]);
        if (hotkeys.Unregistered.Count > 0)
            state.SetStatus("以下全局快捷键被其他程序占用：" + string.Join("、", hotkeys.Unregistered) + "；界面按钮仍可使用。", true);
    }

    /// <summary>Tray icon: restore, hide everything while recording continues, record toggle, quit.</summary>
    private void SetupTray()
    {
        tray = new TrayIntegration(RestoreMainWindow, HideAllWindows, ToggleRecording, RequestExit);
        state.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(AppState.Recording)) tray?.SetRecording(state.Recording); };
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
        if (teleprompter is { IsVisible: true }) { teleprompter.Hide(); return; }
        teleprompter ??= new TeleprompterWindow(state);
        teleprompter.Show();
        teleprompter.ApplyClickThrough(teleprompter.ClickThrough);
    }

    /// <summary>Show or hide the floating cue island (the Mac app's notch panel).</summary>
    public void ToggleCueIsland()
    {
        if (island is { IsVisible: true }) { island.Hide(); return; }
        island ??= new CueIslandWindow(state);
        island.Show();
        island.Activate();
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
