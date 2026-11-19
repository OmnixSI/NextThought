using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop;

/// <summary>
/// Windows-only window geometry that must not leak into the shared settings.json
/// (plan §13: Windows-specific preferences use their own file and never enter backups).
/// </summary>
public sealed class WindowLayout
{
    public double TeleprompterX { get; set; } = double.NaN;
    public double TeleprompterY { get; set; } = double.NaN;
    public double TeleprompterWidth { get; set; } = 640;
    public double TeleprompterHeight { get; set; } = 320;
    public double TeleprompterOpacity { get; set; } = 0.92;
    public double TeleprompterFontSize { get; set; } = 26;
    public bool TeleprompterClickThrough { get; set; }
    public bool TeleprompterHiddenBackground { get; set; }

    private static string PathFor(AppPaths paths) => System.IO.Path.Combine(paths.Root, "win-ui.json");

    public static WindowLayout Load(AppPaths paths)
    {
        try
        {
            var file = PathFor(paths);
            if (!System.IO.File.Exists(file)) return new WindowLayout();
            return JsonSerializer.Deserialize<WindowLayout>(System.IO.File.ReadAllText(file), JsonDefaults.Options) ?? new WindowLayout();
        }
        catch
        {
            return new WindowLayout();
        }
    }

    public void Save(AppPaths paths)
    {
        try
        {
            System.IO.Directory.CreateDirectory(paths.Root);
            System.IO.File.WriteAllText(PathFor(paths), JsonSerializer.Serialize(this, JsonDefaults.Options));
        }
        catch
        {
            // Geometry is a convenience; never fail the app for it.
        }
    }
}
