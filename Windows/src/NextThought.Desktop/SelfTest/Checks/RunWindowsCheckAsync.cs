using Avalonia.Controls;
using Avalonia.Threading;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop.Views;

/// <summary>
/// §6.7 second batch evidence: drives the four windows that have no Mac screenshots — subtitle bar,
/// answer window (three placements), export sheet, backup restore review — so the contract
/// assertions run on the real controls and the comparison documents get light/dark captures.
/// </summary>
public partial class MainWindow
{
    private sealed record WindowsCheckStep(string Name, bool Passed, string Detail);

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "App.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        return AppContext.BaseDirectory;
    }

    private async Task RunWindowsCheckAsync(string path)
    {
        await Task.Delay(400);
        var steps = new List<WindowsCheckStep>();
        var root = RepoRoot();
        string Evidence(params string[] parts) => Path.Combine([root, "Windows", "validation", "screens", .. parts]);
        void Check(string name, bool passed, string detail) => steps.Add(new WindowsCheckStep(name, passed, detail));
        async Task Pump()
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(220);
        }

        try
        {
            state.LoadDemo(DemoStages.Live);
            await Pump();

            // ---- E14: subtitle bar, line + translation, shared badge, auto-end reminder ---------
            Directory.CreateDirectory(Evidence("subtitle-bar"));
            var subtitle = new SubtitleBarWindow(state) { Persistent = true };
            subtitle.Show();
            await Pump();
            subtitle.ShowPreview("如果删除缓存失败了怎么办？", "If deleting the cache fails, what do we do?", "remote");
            await Pump();
            var primary = subtitle.FindControl<TextBlock>("PrimaryText");
            var secondary = subtitle.FindControl<TextBlock>("SecondaryText");
            Check("e14-line", primary?.IsEffectivelyVisible == true && secondary?.IsEffectivelyVisible == true
                               && subtitle.CardWidth <= 900,
                $"{primary?.Text} / {secondary?.Text} width={subtitle.CardWidth:0}");
            foreach (var dark in new[] { false, true })
            {
                if (Avalonia.Application.Current is { } app)
                    app.RequestedThemeVariant = dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
                await Pump();
                CaptureVisual(subtitle, Path.Combine(Evidence("subtitle-bar"), "subtitle-bar-" + (dark ? "dark" : "light") + ".png"));
            }
            subtitle.ShowPreview("", "", autoEnd: true);
            await Pump();
            var autoEnd = subtitle.FindControl<TextBlock>("AutoEndText");
            Check("e14-autoend", autoEnd?.IsEffectivelyVisible == true && autoEnd.Text?.Contains("42") == true, autoEnd?.Text ?? "");
            CaptureVisual(subtitle, Path.Combine(Evidence("subtitle-bar"), "subtitle-bar-autoend-light.png"));
            subtitle.AllowClose = true;
            subtitle.Close();

            // ---- E29: answer window, the three Mac placements ----------------------------------
            Directory.CreateDirectory(Evidence("teleprompter"));
            var teleprompter = new TeleprompterWindow(state) { Width = 640, Height = 300 };
            teleprompter.Show();
            await Pump();
            teleprompter.ShowPreview(DemoContent.Answer, DemoContent.Question);
            await Pump();
            var positions = new List<(string Name, int X, int Y)>();
            foreach (var placement in new[] { "camera", "bottom", "right" })
            {
                teleprompter.MoveTo(placement);
                await Pump();
                positions.Add((placement, teleprompter.Position.X, teleprompter.Position.Y));
                CaptureVisual(teleprompter, Path.Combine(Evidence("teleprompter"),
                    "teleprompter-" + placement + "-light.png"));
            }
            Check("e29-placements",
                positions.Count == 3 && positions.Select(item => item.X + "," + item.Y).Distinct().Count() == 3,
                string.Join(" | ", positions.Select(item => item.Name + "=" + item.X + "," + item.Y)));
            Check("e29-body", teleprompter.FindControl<MarkdownText>("BodyText")?.Text.Length > 0
                              && teleprompter.SurfaceVisible,
                "markdown answer shown on the raised surface");
            if (Avalonia.Application.Current is { } darkApp)
                darkApp.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
            await Pump();
            teleprompter.MoveTo("camera");
            await Pump();
            CaptureVisual(teleprompter, Path.Combine(Evidence("teleprompter"), "teleprompter-camera-dark.png"));
            teleprompter.ClearPreview();
            teleprompter.Close();

            // ---- E21: export sheet (a real archived meeting, SRT source preview) ---------------
            Directory.CreateDirectory(Evidence("export"));
            var store = new SessionStore(state.Paths);
            var meta = new SessionMeta { Title = "导出窗口 · 缓存一致性复盘" };
            await store.CreateAsync(meta);
            await store.AppendTranscriptAsync(meta, new TranscriptLine
            {
                Date = meta.StartedAt.AddSeconds(10),
                StartedAt = meta.StartedAt.AddSeconds(10),
                EndedAt = meta.StartedAt.AddSeconds(15),
                Speaker = "remote",
                Text = "那数据库和缓存同时更新的时候，你们是怎么保证一致性的？"
            });
            await store.AppendAnswerAsync(meta, new AnswerRecord
            {
                Date = meta.StartedAt.AddSeconds(20),
                Question = "缓存怎么保证一致？",
                Answer = "先更新数据库再删缓存，删除失败进重试队列。",
                Model = "m",
                Automatic = true
            });
            await store.SaveSummaryAsync(meta, "## 摘要\n一致性方案上线前确认。", "m");
            await store.EndAsync(meta);
            await state.RefreshHistoryAsync();
            if (state.History.FirstOrDefault(item => item.Meta.Id == meta.Id) is { } row)
                await state.SelectHistoryAsync(row.Meta);
            var export = new SessionExportSheet { DataContext = state };
            export.Show();
            await Pump();
            export.SelectForCheck(3, 0, 0); // SRT: source text preview is deterministic
            await Pump();
            var preview = export.FindControl<TextBox>("PreviewBox");
            var format = export.FindControl<ComboBox>("FormatBox");
            Check("e21-preview", preview?.Text?.Contains("-->") == true && format?.ItemCount == 5,
                "SRT preview with arrows; 5 formats");
            foreach (var dark in new[] { false, true })
            {
                if (Avalonia.Application.Current is { } app)
                    app.RequestedThemeVariant = dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
                await Pump();
                CaptureVisual(export, Path.Combine(Evidence("export"), "export-" + (dark ? "dark" : "light") + ".png"));
            }
            export.Close();

            // ---- E22: backup restore review (a real recovery point) ---------------------------
            Directory.CreateDirectory(Evidence("backup-review"));
            state.LoadBackupPreferences();
            await state.RefreshBackupsAsync();
            await state.BackupNowCommand.ExecuteAsync(null);
            await Task.Delay(500);
            var point = state.Backups.FirstOrDefault();
            if (point is null) throw new InvalidOperationException("没有生成恢复点，无法复核。");
            var prepared = await state.PrepareBackupImportAsync(point.File.Path);
            if (prepared is null) throw new InvalidOperationException("恢复点无法准备成复核。");
            var review = new BackupRestoreReviewView(prepared, state);
            var reviewWindow = new Window
            {
                Title = UiText.T("导入与恢复预览"),
                Width = 820,
                Height = 640,
                MinWidth = 700,
                MinHeight = 540,
                Background = this.FindResource("Paper") as Avalonia.Media.IBrush,
                Content = review
            };
            reviewWindow.Show();
            await Pump();
            var model = review.DataContext as BackupReviewModel;
            Check("e22-review", model is not null && model.NewCount == prepared.NewSessions
                                && model.ConflictCount == prepared.ConflictSessions
                                && model.TemplateCount == prepared.Templates.Count,
                model is null ? "no model" : $"new={model.NewCount} identical={model.IdenticalCount} conflict={model.ConflictCount} templates={model.TemplateCount}");
            foreach (var dark in new[] { false, true })
            {
                if (Avalonia.Application.Current is { } app)
                    app.RequestedThemeVariant = dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
                await Pump();
                CaptureVisual(reviewWindow, Path.Combine(Evidence("backup-review"),
                    "backup-review-" + (dark ? "dark" : "light") + ".png"));
            }
            reviewWindow.Close();
            prepared.Discard();

            if (Avalonia.Application.Current is { } lightApp)
                lightApp.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
            state.Recording = false;
        }
        catch (Exception error)
        {
            steps.Add(new WindowsCheckStep("exception", false, error.ToString()));
        }
        var report = new
        {
            passed = steps.Count > 0 && steps.All(step => step.Passed),
            scope = "§6.7 second batch: subtitle bar, answer window placements, export sheet, backup review",
            steps
        };
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
        Console.WriteLine(report.passed ? "windows-check: passed" : "windows-check: failed");
        Close();
    }
}
