using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

namespace NextThought.Desktop.Views;

/// <summary>Drag a rectangle over a captured screenshot, then crop it to the selection.</summary>
public partial class RegionCaptureWindow : Window
{
    private readonly ScreenCapture.Capture capture;
    private Point? origin;
    private Rect selection;

    public RegionCaptureWindow() : this(ScreenCapture.VirtualScreen()) { }

    public RegionCaptureWindow(ScreenCapture.Capture capture)
    {
        this.capture = capture;
        InitializeComponent();
        Opened += (_, _) => UiTranslation.Apply(this, NextThought.Core.UiText.EnglishUi);
        ScreenImage.Source = capture.Image;
        CanvasHost.PointerPressed += OnPressed;
        CanvasHost.PointerMoved += OnMoved;
        CanvasHost.PointerReleased += OnReleased;
        Opened += (_, _) =>
        {
            HintText.Text = $"已捕获 {capture.Width}×{capture.Height} 像素（{capture.X},{capture.Y}）。拖动选择区域，然后点「使用此区域」。";
        };
    }

    /// <summary>The displayed image rectangle inside the host (Uniform fit, letterboxed).</summary>
    private Rect ImageRect
    {
        get
        {
            var host = CanvasHost.Bounds;
            if (capture.Width <= 0 || capture.Height <= 0) return default;
            var scale = Math.Min(host.Width / capture.Width, host.Height / capture.Height);
            var width = capture.Width * scale;
            var height = capture.Height * scale;
            return new Rect((host.Width - width) / 2, (host.Height - height) / 2, width, height);
        }
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(CanvasHost).Properties.IsLeftButtonPressed is false) return;
        origin = Clamp(e.GetPosition(CanvasHost));
        selection = new Rect(origin.Value, origin.Value);
        ApplySelection();
        e.Pointer.Capture(CanvasHost);
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (origin is null) return;
        var current = Clamp(e.GetPosition(CanvasHost));
        selection = new Rect(origin.Value, current).Normalize();
        ApplySelection();
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        origin = null;
        e.Pointer.Capture(null);
    }

    private Point Clamp(Point point)
    {
        var rect = ImageRect;
        return new Point(Math.Clamp(point.X, rect.X, rect.Right), Math.Clamp(point.Y, rect.Y, rect.Bottom));
    }

    private void ApplySelection()
    {
        var visible = selection.Width >= 8 && selection.Height >= 8;
        Selection.IsVisible = visible;
        AcceptButton.IsEnabled = visible;
        if (visible)
        {
            Canvas.SetLeft(Selection, selection.X);
            Canvas.SetTop(Selection, selection.Y);
            Selection.Width = selection.Width;
            Selection.Height = selection.Height;
            var rect = ImageRect;
            var scale = rect.Width / capture.Width;
            SizeText.Text = $"选区 {Math.Round(selection.Width / scale)}×{Math.Round(selection.Height / scale)} 像素";
        }
        else
        {
            SizeText.Text = "";
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);

    private void Accept_Click(object? sender, RoutedEventArgs e)
    {
        var rect = ImageRect;
        if (rect.Width <= 0) { Close(null); return; }
        var scale = rect.Width / capture.Width;
        var x = (int)Math.Round((selection.X - rect.X) / scale);
        var y = (int)Math.Round((selection.Y - rect.Y) / scale);
        var width = (int)Math.Round(selection.Width / scale);
        var height = (int)Math.Round(selection.Height / scale);
        if (width < 4 || height < 4) { Close(null); return; }
        Close(ScreenCapture.Crop(capture, new PixelRect(x, y, width, height)));
    }
}
