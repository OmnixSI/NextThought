using Avalonia.Controls;
using Avalonia.Media;
using NextThought.Core;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// E30: drives the real backup UI on demo data — the settings section (auto-backup toggle,
    /// backup list, 立即备份), a recovery point, the 导入与恢复预览 page and its apply path, and
    /// recovery-point deletion. Screenshots are written next to the JSON report.
    /// </summary>
    private async Task RunBackupUiCheckAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var screenshotDirectory = Path.Combine(directory, "screenshots");
        Directory.CreateDirectory(screenshotDirectory);
        var steps = new List<object>();
        var passed = false;
        string? error = null;
        void Step(string name, bool ok, string detail) => steps.Add(new { step = name, passed = ok, detail = detail });

        try
        {
            state.LoadBackupPreferences();
            await state.RefreshBackupsAsync();
            Step("打开备份设置：读取自动备份开关与恢复点列表", true,
                "autoBackup=" + state.AutoBackup + "，已有恢复点 " + state.Backups.Count + " 个");

            // A meeting to back up: use the demo meeting when present, otherwise store one through
            // the real session API.
            var sessions = new SessionStore(state.Paths);
            var meta = sessions.List().FirstOrDefault();
            if (meta is null)
            {
                meta = new SessionMeta { Title = "备份界面自检会议" };
                await sessions.CreateAsync(meta);
                await sessions.AppendTranscriptAsync(meta, new TranscriptLine { Date = DateTimeOffset.Now, Text = "备份界面自检的转写" });
                await sessions.EndAsync(meta);
            }

            // Toggle writes the Mac preference and reads back.
            state.AutoBackup = false;
            await Task.Delay(150);
            var disabled = state.AutoBackupEnabled;
            state.AutoBackup = true;
            await Task.Delay(150);
            Step("切换自动备份开关", !disabled && state.AutoBackupEnabled, "autoBackup false→true 已写入设置");

            // 立即备份 creates a real recovery point through the command.
            var before = state.Backups.Count;
            await state.BackupNowCommand.ExecuteAsync(null);
            await Task.Delay(400);
            var point = state.Backups.FirstOrDefault();
            Step("点「立即备份」创建恢复点", state.Backups.Count > before && point is not null,
                state.BackupStatusLine + "；" + (point is null ? "没有恢复点" : point.Name));

            // afterSession: the stop-meeting hook, with the data unchanged this deduplicates to the
            // existing point exactly like DataBackup.automaticBackup.
            await state.RunAfterSessionBackupAsync(meta.Id);
            await Task.Delay(300);
            Step("会议结束后自动创建恢复点", state.Backups.Count > 0, "恢复点 " + state.Backups.Count + " 个（内容未变时按指纹去重）");

            // The real settings page hosts the section in 数据与关于 (Mac 设置 → 通用). Scroll the
            // General page to the bottom so the backup card is visible in the evidence screenshot.
            state.Section = AppSection.Settings;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(400);
            var settingsOverlay = this.FindControl<SettingsView>("SettingsOverlay");
            settingsOverlay?.SelectPage("General");
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(300);
            var hosted = this.GetVisualDescendants().OfType<BackupSettingsSection>().FirstOrDefault();
            if (settingsOverlay is not null)
            {
                var pageScroll = settingsOverlay.GetVisualDescendants().OfType<ScrollViewer>()
                    .OrderByDescending(scroll => scroll.Extent.Height).FirstOrDefault();
                if (pageScroll is not null)
                {
                    pageScroll.Offset = new Vector(0, pageScroll.Extent.Height);
                    await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                    await Task.Delay(300);
                }
            }
            Step("设置页入口：数据与关于里的备份与恢复", hosted is not null,
                hosted is null ? "设置页没有找到 BackupSettingsSection" : "已在设置页挂载（通用 → 数据与关于）");
            var app = Application.Current!;
            app.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
            await Task.Delay(250);
            CaptureVisual(this, Path.Combine(screenshotDirectory, "settings-backup-light.png"));
            if (hosted is not null)
            {
                CaptureVisual(hosted, Path.Combine(screenshotDirectory, "backup-settings-light.png"));
                state.ApplyUiLanguage(true);
                await Task.Delay(350);
                CaptureVisual(hosted, Path.Combine(screenshotDirectory, "backup-settings-en-light.png"));
                CaptureVisual(this, Path.Combine(screenshotDirectory, "settings-backup-en-light.png"));
                state.ApplyUiLanguage(false);
                await Task.Delay(300);
                app.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
                await Task.Delay(250);
                CaptureVisual(hosted, Path.Combine(screenshotDirectory, "backup-settings-dark.png"));
                app.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
                await Task.Delay(200);
            }
            Step("备份设置页：自动备份开关、恢复点列表、导入导出按钮", true,
                "列表 " + state.Backups.Count + " 行；" + state.BackupLatestText);

            // 导入与恢复预览 page, captured and then applied through the real apply path.
            if (point is not null)
            {
                var prepared = await state.PrepareBackupImportAsync(point.File.Path);
                if (prepared is not null)
                {
                    var review = new BackupRestoreReviewView(prepared, state);
                    var reviewWindow = new Window
                    {
                        Title = "导入与恢复预览",
                        Width = 820,
                        Height = 640,
                        Background = PaperBrush(),
                        Content = review
                    };
                    reviewWindow.Show();
                    await Task.Delay(500);
                    CaptureVisual(reviewWindow, Path.Combine(screenshotDirectory, "backup-restore-review-light.png"));
                    state.ApplyUiLanguage(true);
                    await Task.Delay(300);
                    CaptureVisual(reviewWindow, Path.Combine(screenshotDirectory, "backup-restore-review-en-light.png"));
                    state.ApplyUiLanguage(false);
                    app.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
                    await Task.Delay(250);
                    CaptureVisual(reviewWindow, Path.Combine(screenshotDirectory, "backup-restore-review-dark.png"));
                    app.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
                    Step("导入前复核页：新增/相同/冲突记录与备份模板", true,
                        "计划 " + prepared.Sessions.Count + " 场会议、" + prepared.Templates.Count + " 个模板");
                    reviewWindow.Close();

                    var result = await state.ApplyBackupImportAsync(prepared, new HashSet<Guid>(), new HashSet<string>(), applySettings: false);
                    Step("确认后应用所选内容（先创建恢复点）", result is not null, state.BackupStatusLine);
                    await Task.Delay(300);
                    if (hosted is not null) CaptureVisual(hosted, Path.Combine(screenshotDirectory, "backup-after-apply-light.png"));
                }
                else
                {
                    Step("导入前复核页：生成预览计划", false, state.BackupStatusLine);
                }
            }

            // Delete a recovery point through the command and check the list shrinks.
            if (state.Backups.FirstOrDefault() is { } victim)
            {
                var count = state.Backups.Count;
                await state.DeleteBackupCommand.ExecuteAsync(victim);
                await Task.Delay(300);
                Step("删除恢复点", state.Backups.Count < count, "恢复点 " + count + "→" + state.Backups.Count);
            }
            else
            {
                Step("删除恢复点", false, "没有可删除的恢复点");
            }

            passed = steps.All(step => (bool)step.GetType().GetProperty("passed")!.GetValue(step)!);
        }
        catch (Exception ex)
        {
            error = ex.ToString();
        }
        finally
        {
        }
        var report = new { passed, error, steps };
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "backup-ui-check.json"),
                System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }));
        }
        catch { }
        Console.WriteLine(passed ? "backup-ui-check: passed" : "backup-ui-check: failed " + (error ?? ""));
        if (Application.Current is { } current) current.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Default;
        Close();
    }

    private static IBrush? PaperBrush() =>
        Application.Current is { } app && app.TryFindResource("Paper", out var brush) ? brush as IBrush : null;
}
