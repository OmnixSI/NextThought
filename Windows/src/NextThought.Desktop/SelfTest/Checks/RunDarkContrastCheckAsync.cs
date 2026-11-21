using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NextThought.Core;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// §5.5: the dark-theme contrast audit. For every page, walks the live visual tree in the dark
    /// theme, takes each TextBlock's foreground and the effective (alpha-composited) background
    /// behind it, and reports the ones whose WCAG contrast is below 4.5. Decorative icon glyphs
    /// (private-use area) are skipped; hidden templates and zero-size parts are ignored. Also
    /// reports near-white background blocks in dark mode.
    /// </summary>
    private async Task RunDarkContrastCheckAsync(string path)
    {
        await Task.Delay(300);
        var report = new List<object>();
        var failures = 0;
        var sections = new[]
        {
            (AppSection.Home, "home"), (AppSection.Prepare, "prepare"), (AppSection.Live, "live"),
            (AppSection.Notes, "notes"), (AppSection.History, "history"), (AppSection.Series, "series"),
            (AppSection.Library, "library"), (AppSection.Settings, "settings")
        };
        try
        {
            if (Avalonia.Application.Current is { } app)
                app.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
            state.ApplyUiLanguage(false);
            foreach (var (section, name) in sections)
            {
                state.Section = section;
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                await Task.Delay(320);
                TextZoomService.ApplyAll();
                var pageFailures = new List<object>();
                var whiteBlocks = new List<string>();
                var root = PageHost.Content as Visual;
                if (root is null)
                {
                    report.Add(new { page = name, error = "no page content", passed = false });
                    failures++;
                    continue;
                }
                foreach (var text in root.GetVisualDescendants().OfType<TextBlock>())
                {
                    if (!text.IsEffectivelyVisible || string.IsNullOrWhiteSpace(text.Text)) continue;
                    if (text.Bounds.Width < 4 || text.Bounds.Height < 4) continue;
                    if (IsIconGlyph(text.Text)) continue;
                    var foreground = ActualColor(text.Foreground);
                    if (foreground is null || foreground.Value.A < 200) continue;
                    var background = Backdrop(text);
                    if (background is null) continue;
                    var ratio = Contrast(foreground.Value, background.Value);
                    if (ratio < 4.5)
                    {
                        pageFailures.Add(new
                        {
                            text = text.Text!.Length > 40 ? text.Text[..40] : text.Text,
                            foreground = foreground.Value.ToString(),
                            background = background.Value.ToString(),
                            ratio = Math.Round(ratio, 2)
                        });
                        failures++;
                    }
                }
                foreach (var border in root.GetVisualDescendants().OfType<Border>())
                {
                    if (!border.IsEffectivelyVisible || border.Bounds.Width < 8 || border.Bounds.Height < 8) continue;
                    if (border.Background is not SolidColorBrush brush || brush.Color.A < 240) continue;
                    var color = brush.Color;
                    if (color.R > 235 && color.G > 235 && color.B > 235)
                        whiteBlocks.Add((border.Name ?? border.GetType().Name) + " " + (int)border.Bounds.Width + "x" + (int)border.Bounds.Height);
                }
                report.Add(new { page = name, failures = pageFailures, whiteBlocks, passed = pageFailures.Count == 0 && whiteBlocks.Count == 0 });
            }
            if (Avalonia.Application.Current is { } reset)
                reset.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
        }
        catch (Exception error)
        {
            report.Add(new { page = "error", error = error.ToString(), passed = false });
            failures++;
        }
        var payload = new
        {
            passed = failures == 0,
            failures,
            pages = report,
            note = "对比度 = WCAG 相对亮度比；背景为从最近不透明祖先逐层把半透明层合成后的有效颜色；跳过图标字形与隐藏模板。"
        };
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(payload.passed ? "dark-contrast-check: passed" : "dark-contrast-check: failed");
        Close();
    }

    private static bool IsIconGlyph(string? text)
    {
        if (string.IsNullOrEmpty(text)) return true;
        foreach (var character in text)
        {
            var code = (int)character;
            if (code >= 0xE000 && code <= 0xF8FF) return true;
            if (code >= 0x1F000 && code <= 0x1FAFF) return true;
        }
        return false;
    }

    private static Color? ActualColor(IBrush? brush) => brush is SolidColorBrush solid ? solid.Color : null;

    /// <summary>Walks up collecting solid backgrounds, then composites them over the first opaque one.</summary>
    private static Color? Backdrop(Visual visual)
    {
        var layers = new List<Color>();
        Color? baseColor = null;
        foreach (var ancestor in visual.GetVisualAncestors())
        {
            IBrush? brush = ancestor switch
            {
                Border border => border.Background,
                Panel panel => panel.Background,
                Window window => window.Background,
                _ => null
            };
            if (brush is not SolidColorBrush solid || solid.Color.A == 0) continue;
            if (solid.Color.A == 255)
            {
                baseColor = solid.Color;
                break;
            }
            layers.Add(solid.Color);
        }
        if (baseColor is null)
        {
            baseColor = Avalonia.Application.Current?.Resources["Surface"] is SolidColorBrush surface
                ? surface.Color
                : Color.Parse("#0F0E0D");
        }
        var effective = baseColor.Value;
        for (var index = layers.Count - 1; index >= 0; index--)
        {
            var layer = layers[index];
            double alpha = layer.A / 255.0;
            effective = Color.FromArgb(255,
                (byte)Math.Round(layer.R * alpha + effective.R * (1 - alpha)),
                (byte)Math.Round(layer.G * alpha + effective.G * (1 - alpha)),
                (byte)Math.Round(layer.B * alpha + effective.B * (1 - alpha)));
        }
        return effective;
    }

    private static double Contrast(Color left, Color right)
    {
        var a = Luminance(left);
        var b = Luminance(right);
        var lighter = Math.Max(a, b);
        var darker = Math.Min(a, b);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance(Color color)
    {
        static double Channel(byte value)
        {
            var channel = value / 255.0;
            return channel <= 0.03928 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }
}
