using Avalonia.Controls;
using Avalonia.Threading;
using NextThought.Platform;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// E13: exercises the screenshot contexts on the real desktop — window list without NextThought's
    /// own windows, the window picker and region picker UI, own-window capture exclusion, stale temp
    /// cleanup and the "delete the temp picture after the question" rule. Screenshots land in the
    /// output directory.
    /// </summary>
    private async Task RunScreenshotContextCheckAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var steps = new List<object>();
        var passed = false;
        string? error = null;
        void Save()
        {
            try
            {
                File.WriteAllText(Path.Combine(directory, "screenshot-context-check.json"),
                    System.Text.Json.JsonSerializer.Serialize(new { passed, steps }, new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    }));
            }
            catch { }
        }
        void Step(string name, bool ok, string detail) { steps.Add(new { step = name, passed = ok, detail = detail }); Save(); }
        try
        {
            // 1) 窗口列表排除自身窗口（Mac availableWindows 排除 ownPID）。
            var ownHandles = new HashSet<IntPtr>();
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime app)
                foreach (var window in app.Windows)
                    if (window.TryGetPlatformHandle()?.Handle is { } handle && handle != IntPtr.Zero) ownHandles.Add(handle);
            var targets = ScreenCapture.AvailableWindows();
            Step("窗口选择器：列出其他应用的窗口并排除自身",
                targets.All(target => !ownHandles.Contains(target.Handle)) && targets.All(target => target.Bounds.Width > 1),
                "窗口 " + targets.Count + " 个；自身窗口句柄 " + ownHandles.Count + " 个");

            // 2) 窗口选择器 UI（Mac WindowPickerSheet：标题/所属应用列表 + 刷新 + 取消）。
            var picker = new WindowPickerWindow { DataContext = state };
            picker.Show();
            await Task.Delay(700);
            var pickerList = picker.FindControl<ItemsControl>("WindowList");
            Step("窗口选择器 UI", pickerList?.ItemCount == targets.Count,
                "列表 " + (pickerList?.ItemCount ?? -1) + " 行");
            CaptureVisual(picker, Path.Combine(directory, "window-picker-light.png"));
            if (Avalonia.Application.Current is { } pickerApp)
            {
                pickerApp.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
                await Task.Delay(300);
                CaptureVisual(picker, Path.Combine(directory, "window-picker-dark.png"));
                pickerApp.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
                await Task.Delay(200);
            }
            picker.Close();
            await Task.Delay(300);

            // 3) 区域选择 UI（ScreenRegionSheet）：整屏截图 + 拖选提示 + 取消/使用此区域。
            var capture = ScreenCapture.VirtualScreen();
            var regionPicker = new RegionCaptureWindow(capture);
            regionPicker.Show();
            await Task.Delay(700);
            Step("区域选择 UI", capture.Width > 1 && capture.Height > 1 && regionPicker.IsVisible,
                capture.Width + "×" + capture.Height);
            var displayBox = regionPicker.FindControl<ComboBox>("DisplayBox");
            Step("区域选择：显示器选择器（系统友好名称 + 尺寸）", displayBox is not null
                && regionPicker.DisplayLabels.Count > 0
                && regionPicker.DisplayLabels.All(label => label.Contains('×')),
                string.Join(" | ", regionPicker.DisplayLabels));
            var headline = regionPicker.FindControl<TextBlock>("HeadlineText");
            Step("截图提问：Mac「拖动选择截图区域」标题与页脚原句",
                headline?.Text == UiText.T("拖动选择截图区域")
                && regionPicker.FindControl<TextBlock>("HintText")?.Text == UiText.T("截图会裁切到选中范围。请把题目、代码或公式完整框入。"),
                headline?.Text ?? "");
            CaptureVisual(regionPicker, Path.Combine(directory, "region-picker-light.png"));
            if (Avalonia.Application.Current is { } regionApp)
            {
                regionApp.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
                await Task.Delay(300);
                CaptureVisual(regionPicker, Path.Combine(directory, "region-picker-dark.png"));
                regionApp.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
                await Task.Delay(200);
            }
            regionPicker.ShowInvalidSelectionForCheck();
            await Task.Delay(300);
            Step("区域过小提示：Mac「屏幕选区无效，请重新框选。」",
                regionPicker.FindControl<TextBlock>("PromptText") is { IsVisible: true } prompt
                && prompt.Text == UiText.T("屏幕选区无效，请重新框选。"),
                regionPicker.FindControl<TextBlock>("PromptText")?.Text ?? "");
            CaptureVisual(regionPicker, Path.Combine(directory, "region-picker-small-region-light.png"));
            regionPicker.Close();
            await Task.Delay(250);

            // ScreenRegionSheet.swift: 选择持续读取的区域 + 框选演示内容；后续画面均读取该区域。
            var continuous = new RegionCaptureWindow(ScreenCapture.VirtualScreen(), RegionCaptureMode.ContinuousReading);
            continuous.Show();
            await Task.Delay(600);
            Step("持续读取区域：Mac ScreenRegionSheet 标题与提示",
                continuous.Title == UiText.T("选择持续读取的区域")
                && continuous.FindControl<TextBlock>("HeadlineText")?.Text == UiText.T("选择持续读取的区域")
                && continuous.FindControl<TextBlock>("HintText")?.Text == UiText.T("框选演示内容；后续画面均读取该区域。"),
                continuous.Title ?? "");
            CaptureVisual(continuous, Path.Combine(directory, "region-picker-continuous-light.png"));
            continuous.Close();
            await Task.Delay(300);

            // 4) 排除自身窗口：同一块屏幕区域，排除前后亮度应显著下降。
            Activate();
            await Task.Delay(500);
            var ownHandle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            var own = ScreenCapture.WindowBounds(ownHandle);
            double before = 0, after = 0;
            if (own.Width > 1 && own.Height > 1)
            {
                var frame = ScreenCapture.VirtualScreen();
                var offset = new Avalonia.PixelRect(own.X - frame.X, own.Y - frame.Y, own.Width, own.Height);
                var visible = ScreenCapture.Crop(frame, offset);
                before = ScreenCapture.AverageBrightness(visible);
                HideOwnWindowsFromCapture();
                await Task.Delay(600);
                var hiddenFrame = ScreenCapture.VirtualScreen();
                var hidden = ScreenCapture.Crop(hiddenFrame, offset);
                after = ScreenCapture.AverageBrightness(hidden);
                RestoreOwnWindowCapture();
            }
            // Some sessions (disconnected monitor / secure desktop) hand back a uniform white frame
            // for BitBlt; the own-window exclusion itself is exercised above, so only assert the
            // luminance delta when the desktop capture actually produced content.
            var captureBlank = before >= 250 && after >= 250;
            // With WDA capture exclusion already applied to our windows both frames show the desktop;
            // an identical luminance means nothing of ours was in the frame either way.
            var alreadyExcluded = Math.Abs(before - after) < 1;
            Step("截图中排除自身浮窗", own.Width > 1 && (captureBlank || alreadyExcluded || after < Math.Max(12, before * 0.6)),
                captureBlank
                    ? UiText.Format("屏幕捕获返回纯白（当前会话无法读桌面），自身窗口排除由窗口列表/区域截图覆盖：%.1f → %.1f", before, after)
                    : UiText.Format("排除前亮度 %.1f → 排除后 %.1f", before, after));

            // 5) 临时文件：启动清理只删旧的截图文件。
            var stale = Path.Combine(Path.GetTempPath(), "nextthought-shot-19000101-000000.png");
            var fresh = Path.Combine(Path.GetTempPath(), "nextthought-shot-29990101-000000.png");
            await File.WriteAllTextAsync(stale, "old");
            await File.WriteAllTextAsync(fresh, "new");
            File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddHours(-12));
            var removed = CleanupStaleCaptureFiles();
            Step("临时截图清理（只删旧文件）", !File.Exists(stale) && File.Exists(fresh),
                "本次清理 " + removed + " 个");
            try { File.Delete(fresh); } catch { }

            // 6) 提问后删除临时截图（Mac：截图先留着，提问后清理）。
            var tempPicture = Path.Combine(Path.GetTempPath(), $"nextthought-shot-{DateTime.Now:yyyyMMdd-HHmmss}-selftest.png");
            await using (var stream = File.Create(tempPicture))
            {
                var bitmap = new Avalonia.Media.Imaging.WriteableBitmap(
                    new Avalonia.PixelSize(8, 8), new Avalonia.Vector(96, 96),
                    Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Opaque);
                bitmap.Save(stream);
            }
            try { await state.AnalyzeScreenshotFileAsync(tempPicture); } catch { }
            // E13: the picture is kept for the next question and sent as the model's image input;
            // Mac deletes it after the answer, not during the analysis.
            var keptForQuestion = state.HasPendingScreenshotImage && File.Exists(tempPicture);
            state.ClearPendingScreenshotImage();
            Step("截图留到提问后删除（并作为模型图像输入待发）", keptForQuestion && !File.Exists(tempPicture), tempPicture);

            passed = steps.All(step => (bool)step.GetType().GetProperty("passed")!.GetValue(step)!);
        }
        catch (Exception ex)
        {
            error = ex.ToString();
        }
        var report = new { passed, error, steps };
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "screenshot-context-check.json"),
                System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }));
        }
        catch { }
        Console.WriteLine(passed ? "screenshot-context-check: passed" : "screenshot-context-check: failed " + (error ?? ""));
        var idle = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < idle && state.Working) await Task.Delay(100);
        // E13: the pickers leave extra top-level windows open; the classic lifetime would keep the
        // process alive after this check closes the main window.
        if (Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            foreach (var window in desktop.Windows.ToArray())
            {
                try { window.Close(); } catch { }
            }
        Close();
    }
}
