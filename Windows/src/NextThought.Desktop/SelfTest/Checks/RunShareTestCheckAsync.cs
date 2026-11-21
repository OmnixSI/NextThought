using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using NextThought.Desktop.Views.Controls;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>E13: 共享可见性测试 (ShareTestSheet) + QRCodeView evidence for §6.7.</summary>
    private async Task RunShareTestCheckAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var steps = new List<object>();
        var passed = false;
        string? error = null;
        void Step(string name, bool ok, string detail) => steps.Add(new { step = name, passed = ok, detail });
        async Task Pump()
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(260);
        }
        try
        {
            var captured = await state.RunShareVisibilityTestAsync();
            Step("共享可见性测试：整屏抓取（含自身浮窗，不隐藏窗口）", captured && state.HasShareTestImage,
                state.ShareTestImage is { } bitmap ? bitmap.PixelSize.Width + "×" + bitmap.PixelSize.Height : "无图像");

            var sheet = new ShareTestWindow { DataContext = state };
            sheet.Show();
            await Pump();
            var image = sheet.FindControl<Image>("ShareImage");
            Step("ShareTestSheet UI：标题 + ScreenCaptureKit 说明 + 整屏截图 + 关闭", image?.Source is not null,
                "图像 " + (image?.Source is Bitmap shot ? shot.PixelSize.Width + "×" + shot.PixelSize.Height : "无"));
            CaptureVisual(sheet, Path.Combine(directory, "share-test-light.png"));
            if (Avalonia.Application.Current is { } app)
            {
                app.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
                await Pump();
                CaptureVisual(sheet, Path.Combine(directory, "share-test-dark.png"));
                app.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
                await Pump();
            }
            sheet.Close();

            // QRCodeView (SettingsView.swift:547 companion pairing card): the control draws the
            // address QR on the white rounded card the Mac view defines.
            var qr = new QRCodeView { Text = "http://127.0.0.1:8791", Width = 200, Height = 200 };
            var qrWindow = new Window
            {
                Title = UiText.T("共享可见性测试"),
                Width = 280, Height = 280,
                Background = this.FindResource("Paper") as Avalonia.Media.IBrush,
                Content = new Border { Padding = new Avalonia.Thickness(40), Child = qr }
            };
            qrWindow.Show();
            await Pump();
            Step("QRCodeView：地址二维码白底圆角卡片", qr.Content is Border { Child: Image { Source: not null } }, "200×200");
            CaptureVisual(qrWindow, Path.Combine(directory, "qr-code-light.png"));
            qrWindow.Close();
            await Pump();
            state.ClearShareTestImage();
            passed = steps.All(step => (bool)step.GetType().GetProperty("passed")!.GetValue(step)!);
        }
        catch (Exception ex)
        {
            error = ex.ToString();
        }
        var report = new { passed, error, steps };
        try
        {
            File.WriteAllText(Path.Combine(directory, "share-test-check.json"),
                System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }));
        }
        catch { }
        Console.WriteLine(passed ? "share-test-check: passed" : "share-test-check: failed " + (error ?? ""));
        var idle = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < idle && state.Working) await Task.Delay(100);
        Close();
    }
}
