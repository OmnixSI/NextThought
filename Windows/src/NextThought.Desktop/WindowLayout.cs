using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop;

/// <summary>
/// Windows-only window geometry. G5b: the cue window's opacity and answer font size are Mac
/// preferences (<c>teleprompterOpacity</c> / <c>teleprompterFontSize</c>), so they live in the
/// shared settings.json with the Mac defaults (1.0 / 22) and slider ranges (0.35–1 / 14–36);
/// only geometry, position and click-through stay in this Windows-only file
/// (plan §13: Windows-specific preferences never enter backups).
/// </summary>
public sealed class WindowLayout
{
    public double TeleprompterX { get; set; } = double.NaN;
    public double TeleprompterY { get; set; } = double.NaN;
    public double TeleprompterWidth { get; set; } = 640;
    public double TeleprompterHeight { get; set; } = 320;
    /// <summary>Mirror of the Mac <c>teleprompterOpacity</c> key; persisted in settings.json.</summary>
    public double TeleprompterOpacity { get; set; } = 1.0;
    /// <summary>Mirror of the Mac <c>teleprompterFontSize</c> key; persisted in settings.json.</summary>
    public double TeleprompterFontSize { get; set; } = 22.0;
    public bool TeleprompterClickThrough { get; set; }
    public bool TeleprompterHiddenBackground { get; set; }

    private static string PathFor(AppPaths paths) => System.IO.Path.Combine(paths.Root, "win-ui.json");

    public static WindowLayout Load(AppPaths paths)
    {
        var hadWindowsFile = System.IO.File.Exists(PathFor(paths));
        var layout = new WindowLayout();
        if (hadWindowsFile)
        {
            try
            {
                var text = System.IO.File.ReadAllText(PathFor(paths));
                layout = JsonSerializer.Deserialize<WindowLayout>(text, JsonDefaults.Options) ?? new WindowLayout();
            }
            catch
            {
                layout = new WindowLayout();
            }
        }
        AppSettings settings;
        try { settings = new SettingsStore(paths).Load(); }
        catch { settings = new AppSettings(); }
        // G5b migration: an older build kept the opacity/font size in win-ui.json only. The first
        // time the Mac keys are missing from settings.json, carry the window's values over.
        if (hadWindowsFile && !SettingsHaveTeleprompterValues(paths))
        {
            settings.TeleprompterOpacity = NormalizeOpacity(layout.TeleprompterOpacity);
            settings.TeleprompterFontSize = NormalizeFontSize(layout.TeleprompterFontSize);
            _ = new SettingsStore(paths).SaveAsync(settings);
        }
        layout.TeleprompterOpacity = NormalizeOpacity(settings.TeleprompterOpacity);
        layout.TeleprompterFontSize = NormalizeFontSize(settings.TeleprompterFontSize);
        return layout;
    }

    public void Save(AppPaths paths)
    {
        // Geometry is a convenience; never fail the app for it.
        try
        {
            System.IO.Directory.CreateDirectory(paths.Root);
            var text = JsonSerializer.Serialize(this, JsonDefaults.Options);
            System.IO.File.WriteAllText(PathFor(paths), text);
        }
        catch
        {
        }
        // G5b: opacity and font size are the Mac keys; persist them to settings.json.
        try
        {
            var store = new SettingsStore(paths);
            var settings = store.Load();
            var opacity = NormalizeOpacity(TeleprompterOpacity);
            var fontSize = NormalizeFontSize(TeleprompterFontSize);
            if (Math.Abs(settings.TeleprompterOpacity - opacity) < 1e-9
                && Math.Abs(settings.TeleprompterFontSize - fontSize) < 1e-9) return;
            settings.TeleprompterOpacity = opacity;
            settings.TeleprompterFontSize = fontSize;
            _ = store.SaveAsync(settings);
        }
        catch
        {
            // A settings write failure must not break moving the window.
        }
    }

    /// <summary>Mac slider range for teleprompterOpacity is 0.35–1 (Swift: Slider 0.35...1).</summary>
    public static double NormalizeOpacity(double value) =>
        double.IsFinite(value) ? Math.Clamp(value, 0.35, 1) : 1.0;

    /// <summary>Mac slider range for teleprompterFontSize is 14–36 (Swift: Slider 14...36).</summary>
    public static double NormalizeFontSize(double value) =>
        double.IsFinite(value) ? Math.Clamp(value, 14, 36) : 22.0;

    private static bool SettingsHaveTeleprompterValues(AppPaths paths)
    {
        try
        {
            if (!System.IO.File.Exists(paths.Settings)) return false;
            var text = System.IO.File.ReadAllText(paths.Settings);
            return text.Contains("\"teleprompterOpacity\"", StringComparison.Ordinal)
                || text.Contains("\"teleprompterFontSize\"", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }
}
