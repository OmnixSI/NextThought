using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using NextThought.Desktop;

namespace NextThought.Desktop.Views;

/// <summary>
/// E4 / ContentView.swift LevelMeters: two 4 pt capsules, the other side (system audio) in the
/// secondary colour and me (microphone) in ink. Read at 8 Hz from the live audio level state, so
/// audio never drives the UI directly; the echo case (macOS only) falls back to the ink colour.
/// </summary>
public class LevelMeters : UserControl
{
    private readonly Rectangle systemFill = new() { RadiusX = 2, RadiusY = 2 };
    private readonly Rectangle microphoneFill = new() { RadiusX = 2, RadiusY = 2 };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(125) };

    public LevelMeters()
    {
        var barHeight = Height > 0 ? Height : 16;
        var root = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 3 };
        root.Children.Add(Meter(systemFill, barHeight));
        root.Children.Add(Meter(microphoneFill, barHeight));
        Content = root;
        timer.Tick += (_, _) => Refresh();
        AttachedToVisualTree += (_, _) =>
        {
            theme = null;
            Refresh();
            timer.Start();
        };
        DetachedFromVisualTree += (_, _) => timer.Stop();
    }

    private Border theme = null!;

    private Border Meter(Rectangle fill, double barHeight) => new()
    {
        Width = 4,
        Height = barHeight,
        CornerRadius = new CornerRadius(2),
        Background = Brush("Sunken"),
        ClipToBounds = true,
        Child = fill
    };

    private static IBrush Brush(string key) =>
        Application.Current?.TryFindResource(key, out var value) == true && value is IBrush brush ? brush : Brushes.Gray;

    private void Refresh()
    {
        if (DataContext is not AppState state) return;
        var height = Height > 0 ? Height : 16;
        SetFill(systemFill, state.ShellSystemLevel, Brush("Secondary"), height);
        SetFill(microphoneFill, state.ShellMicrophoneLevel, Brush("InkBrush"), height);
    }

    private static void SetFill(Rectangle fill, double level, IBrush brush, double height)
    {
        var value = Math.Clamp(level, 0, 1);
        fill.Height = Math.Max(2, value * height);
        fill.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom;
        fill.Fill = brush;
    }
}
