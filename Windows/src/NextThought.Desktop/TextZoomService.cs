using System.Runtime.CompilerServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Documents;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NextThought.Core;
using NextThought.Desktop.Views;

namespace NextThought.Desktop;

/// <summary>
/// J7: the global text zoom (Mac <c>TextZoom</c>, DesignSystem.swift). Ctrl + / Ctrl − / Ctrl 0 walk
/// the six fixed steps; every page font is rescaled through <see cref="TextZoom.Scaled"/>
/// (multiply, round away from zero), the factor is saved under the Mac key <c>textZoom</c>, and the
/// cue island and the answer window keep their own size buttons (Mac: "the cue island and the
/// answer window keep their own size"). Spacing, radii and window sizes are not touched.
///
/// The pages set their font sizes in styles and in per-control values, so the zoom cannot live in a
/// single inherited property: each control that carries its own size keeps the size it was made
/// with, and the setter writes the scaled size. Controls that inherit simply follow their ancestor.
/// New content (a page switched in, a dialog opened) is picked up by a short timer while a zoom
/// other than 1 is in effect.
/// </summary>
public static class TextZoomService
{
    /// <summary>The Mac preference key the factor is saved under.</summary>
    public const string PreferenceKey = TextZoom.PreferenceKey;

    /// <summary>The preference file next to settings.json (Windows-only storage of the Mac key).</summary>
    public const string PreferenceFile = "TextZoom.json";

    private static readonly ConditionalWeakTable<Control, StrongBox<double>> BaseSizes = new();
    /// <summary>Buttons whose fixed hit height had to grow for the zoomed text (restored at 100%).</summary>
    private static readonly ConditionalWeakTable<Control, StrongBox<double>> BaseMinHeights = new();
    private static readonly List<WeakReference<Control>> BumpedMinHeights = [];
    private static readonly DispatcherTimer Watcher = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private static TextZoom zoom = new();
    private static string? dataRoot;
    private static Action<double>? saveCallback;

    static TextZoomService()
    {
        Watcher.Tick += (_, _) =>
        {
            // Only while a size other than the default is in effect: new rows/pages/dialogs are
            // rescaled within a frame or two of appearing.
            if (Math.Abs(zoom.Factor - 1) > 0.0001) ApplyAll();
            else Watcher.Stop();
        };
    }

    /// <summary>The current text size multiplier.</summary>
    public static double Factor => zoom.Factor;

    public static bool CanGrow => zoom.CanGrow;

    public static bool CanShrink => zoom.CanShrink;

    /// <summary>Loads the saved factor (Mac key <c>textZoom</c>) and starts watching for new content.</summary>
    public static void Configure(string root)
    {
        dataRoot = root;
        saveCallback = Save;
        zoom = new TextZoom(Load(root), saveCallback);
        if (Math.Abs(zoom.Factor - 1) > 0.0001)
        {
            Watcher.Start();
            ApplyAll();
        }
    }

    /// <summary>Ctrl +: one step larger (no-op at the top).</summary>
    public static void Grow() => Step(zoom.Grow);

    /// <summary>Ctrl −: one step smaller (no-op at the bottom).</summary>
    public static void Shrink() => Step(zoom.Shrink);

    /// <summary>Ctrl 0: back to the size the app came with.</summary>
    public static void Reset() => Step(zoom.Reset);

    private static void Step(Action change)
    {
        var before = zoom.Factor;
        change();
        if (Math.Abs(before - zoom.Factor) < 0.0001) return;
        if (Math.Abs(zoom.Factor - 1) > 0.0001) Watcher.Start();
        RestoreMinHeights();
        ApplyAll();
    }

    /// <summary>Scales a point size the way the Mac does (Mac Font.zoomed).</summary>
    public static double Scaled(double size) => zoom.Scaled(size);

    /// <summary>The cue island and the answer window carry their own 小/大 buttons, not the page zoom.</summary>
    public static bool IsExcluded(Window window) =>
        window is CueIslandWindow or TeleprompterWindow;

    /// <summary>True for the two windows that keep their own size (Mac: they have their own 小/大 buttons).</summary>
    public static bool Unscaled(Window window) => IsExcluded(window);

    /// <summary>True for the two windows that keep their own size (used by the self-check).</summary>
    public static bool RequiresOwnSize(Type windowType) =>
        windowType == typeof(CueIslandWindow) || windowType == typeof(TeleprompterWindow);

    /// <summary>Rescales every open window once (pages, dialogs and the subtitle bar).</summary>
    public static void ApplyAll()
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                return;
            foreach (var window in desktop.Windows.ToArray())
            {
                if (IsExcluded(window)) continue;
                Apply(window);
                if (zoom.Factor > 1.001) FitZoomedText(window);
            }
            if (zoom.Factor <= 1.001) RestoreMinHeights();
        }
        catch
        {
            // A window closing mid-walk is harmless: the next tick picks up the rest.
        }
    }

    /// <summary>Rescales one window's text (also used by the self-check on a known control).</summary>
    public static void Apply(Window window)
    {
        if (!window.IsVisible) return;
        Walk(window, null, false, 0);
    }

    private static void Walk(Visual visual, Control? parent, bool hasInherited, double inherited)
    {
        var currentInherited = inherited;
        var currentHasInherited = hasInherited;
        if (visual is Control control)
        {
            var effective = TextElement.GetFontSize(control);
            var ownValue = !double.IsNaN(effective) && (!hasInherited || Math.Abs(effective - inherited) > 0.001);
            if (ownValue)
            {
                var box = BaseSizes.GetOrCreateValue(control);
                if (box.Value <= 0) box.Value = effective;
                var scaled = zoom.Scaled(box.Value);
                if (Math.Abs(effective - scaled) > 0.001) TextElement.SetFontSize(control, scaled);
                currentInherited = scaled;
                currentHasInherited = true;
            }
            else if (control is Window && !hasInherited)
            {
                // The default text size of the window follows the zoom too.
                var box = BaseSizes.GetOrCreateValue(control);
                if (box.Value <= 0) box.Value = effective;
                var scaled = zoom.Scaled(box.Value);
                if (Math.Abs(effective - scaled) > 0.001) TextElement.SetFontSize(control, scaled);
                currentInherited = scaled;
                currentHasInherited = true;
            }
            parent = control;
        }
        foreach (var child in visual.GetVisualChildren())
            Walk(child, parent, currentHasInherited, currentInherited);
    }

    /// <summary>The size a control was made with, and the size it renders at now (self-check probe).</summary>
    public static (double Base, double Actual)? Probe(Window window)
    {
        try
        {
            if (!window.IsVisible) return null;
            foreach (var control in window.GetVisualDescendants().OfType<Control>())
            {
                if (!BaseSizes.TryGetValue(control, out var box) || box.Value <= 0) continue;
                return (box.Value, TextElement.GetFontSize(control));
            }
        }
        catch
        {
            // No window to probe yet.
        }
        return null;
    }

    /// <summary>
    /// A zoomed font is taller than the fixed hit height some buttons were laid out with (the scene
    /// chips use the 32 pt Mac hit target). Rather than truncating, the button grows just enough for
    /// its text; going back to 100% restores the Mac hit height.
    /// </summary>
    private static void FitZoomedText(Window window)
    {
        try
        {
            window.UpdateLayout();
            foreach (var text in window.GetVisualDescendants().OfType<TextBlock>())
            {
                if (!text.IsEffectivelyVisible || text.Bounds.Height <= 0) continue;
                text.Measure(Size.Infinity);
                var overflow = text.DesiredSize.Height - text.Bounds.Height;
                if (overflow <= 1) continue;
                for (var ancestor = text.GetVisualParent(); ancestor is not null; ancestor = ancestor.GetVisualParent())
                {
                    if (ancestor is not Button button) continue;
                    var box = BaseMinHeights.GetOrCreateValue(button);
                    if (box.Value <= 0)
                    {
                        box.Value = button.MinHeight;
                        BumpedMinHeights.Add(new WeakReference<Control>(button));
                    }
                    var needed = button.Bounds.Height + overflow + 2;
                    if (needed > button.MinHeight) button.MinHeight = needed;
                    break;
                }
            }
        }
        catch
        {
            // Layout not ready yet: the next tick retries.
        }
    }

    private static void RestoreMinHeights()
    {
        foreach (var reference in BumpedMinHeights.ToArray())
        {
            if (!reference.TryGetTarget(out var control))
            {
                BumpedMinHeights.Remove(reference);
                continue;
            }
            if (BaseMinHeights.TryGetValue(control, out var box)) control.MinHeight = box.Value;
        }
        BumpedMinHeights.Clear();
    }

    // ---- persistence -------------------------------------------------------------------------

    private static string PreferencePath() => Path.Combine(dataRoot ?? AppContext.BaseDirectory, PreferenceFile);

    private static double Load(string root)
    {
        try
        {
            var path = Path.Combine(root, PreferenceFile);
            if (!File.Exists(path)) return 1;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty(PreferenceKey, out var value) && value.TryGetDouble(out var saved)
                ? saved
                : 1;
        }
        catch
        {
            return 1;
        }
    }

    private static void Save(double factor)
    {
        try
        {
            var path = PreferencePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(new Dictionary<string, double> { [PreferenceKey] = factor }));
        }
        catch
        {
            // A failed save only means the size is not remembered; the text still scales.
        }
    }

    /// <summary>Reads what is on disk (self-check: the factor really persists across launches).</summary>
    public static double ReadPersisted() => dataRoot is { } root ? Load(root) : 1;

    /// <summary>Sets a factor without writing the preference (screenshot batch / self-check).</summary>
    public static void SetFactorForCheck(double factor)
    {
        zoom = new TextZoom(factor, null);
        RestoreMinHeights();
        if (Math.Abs(factor - 1) > 0.0001) Watcher.Start();
        else Watcher.Stop();
        ApplyAll();
    }

    /// <summary>Writes the current factor through the normal preference callback (self-check).</summary>
    public static void SaveForCheck()
    {
        if (saveCallback is not null) saveCallback(zoom.Factor);
    }

    /// <summary>Reloads the saved factor after the self-check wrote one.</summary>
    public static void Reload()
    {
        if (dataRoot is not { } root) return;
        saveCallback = Save;
        zoom = new TextZoom(Load(root), saveCallback);
        RestoreMinHeights();
        ApplyAll();
        Watcher.Stop();
        if (Math.Abs(zoom.Factor - 1) > 0.0001) Watcher.Start();
    }
}
