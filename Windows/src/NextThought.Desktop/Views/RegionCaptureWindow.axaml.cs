using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

/// <summary>Which Mac sheet this window is: RegionScreenshotPicker or ScreenRegionSheet.</summary>
public enum RegionCaptureMode
{
    Screenshot,
    ContinuousReading
}

/// <summary>
/// E13 / §6.7: RegionScreenshotPicker.swift — drag a rectangle over the captured screen, crop to it.
/// The 显示器 picker switches the frozen frame to another display (Mac Picker "显示器 …·W×H"); changing
/// the display clears the selection. The caption and 取消 / 使用此区域 follow Mac's layout.
/// </summary>
public partial class RegionCaptureWindow : Window
{
    private readonly ScreenCapture.Capture source;
    private readonly RegionCaptureMode mode;
    private readonly List<(string Label, PixelRect Rect)> displays = [];
    private ScreenCapture.Capture capture;
    private Point? origin;
    private bool switching;
    private Rect selection;

    public PixelRect? SelectedScreenRect { get; private set; }

    public RegionCaptureWindow() : this(ScreenCapture.VirtualScreen()) { }

    public RegionCaptureWindow(ScreenCapture.Capture capture, RegionCaptureMode mode = RegionCaptureMode.Screenshot)
    {
        source = capture;
        this.mode = mode;
        this.capture = capture;
        InitializeComponent();
        // ScreenRegionSheet.swift uses its own headline/caption; RegionScreenshotPicker.swift has
        // the screenshot wording (the area-too-small prompt is ScreenshotService's error sentence).
        if (mode == RegionCaptureMode.ContinuousReading)
        {
            Title = UiText.T("选择持续读取的区域");
            HeadlineText.Text = UiText.T("选择持续读取的区域");
            HintText.Text = UiText.T("框选演示内容；后续画面均读取该区域。");
        }
        else
        {
            HintText.Text = UiText.T("截图会裁切到选中范围。请把题目、代码或公式完整框入。");
        }
        ScreenImage.Source = capture.Image;
        CanvasHost.PointerPressed += OnPressed;
        CanvasHost.PointerMoved += OnMoved;
        CanvasHost.PointerReleased += OnReleased;
        BuildDisplays();
    }

    private void BuildDisplays()
    {
        displays.Clear();
        var index = 0;
        foreach (var screen in Screens.All)
        {
            index++;
            var bounds = screen.Bounds;
            var left = Math.Max(bounds.X, source.X);
            var top = Math.Max(bounds.Y, source.Y);
            var right = Math.Min(bounds.X + bounds.Width, source.X + source.Width);
            var bottom = Math.Min(bounds.Y + bounds.Height, source.Y + source.Height);
            if (right - left < 2 || bottom - top < 2) continue;
            var friendly = FriendlyNameFor(screen.DisplayName, index);
            displays.Add(($"{friendly} · {right - left}×{bottom - top}",
                new PixelRect(left, top, right - left, bottom - top)));
        }
        if (displays.Count == 0) displays.Add(($"1 · {source.Width}×{source.Height}",
            new PixelRect(source.X, source.Y, source.Width, source.Height)));
        switching = true;
        DisplayBox.ItemsSource = displays.Select(display => display.Label).ToList();
        DisplayBox.SelectedIndex = 0;
        switching = false;
    }

    /// <summary>decisions §3.3: the system friendly monitor name, falling back to the adapter name.</summary>
    private static string FriendlyNameFor(string? displayName, int index)
    {
        var names = NextThought.Platform.MonitorNames.FriendlyNames();
        if (!string.IsNullOrEmpty(displayName))
        {
            var match = names.FirstOrDefault(item => string.Equals(item.Adapter, displayName, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(match.Friendly)) return match.Friendly;
        }
        if (index <= names.Count && !string.IsNullOrEmpty(names[index - 1].Friendly)) return names[index - 1].Friendly;
        var raw = displayName ?? "";
        var trimmed = raw.StartsWith("\\\\.\\", StringComparison.Ordinal) ? raw[4..] : raw;
        return trimmed.Length > 0 ? trimmed : UiText.Format("显示器 %lld", index);
    }

    /// <summary>Mac onChange(of: selectedDisplayID): switching displays clears the selection.</summary>
    private void Display_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (switching || DisplayBox.SelectedIndex < 0 || DisplayBox.SelectedIndex >= displays.Count) return;
        var rect = displays[DisplayBox.SelectedIndex].Rect;
        capture = new ScreenCapture.Capture(rect.X, rect.Y, rect.Width, rect.Height,
            ScreenCapture.Crop(source, new PixelRect(rect.X - source.X, rect.Y - source.Y, rect.Width, rect.Height)));
        ScreenImage.Source = capture.Image;
        origin = null;
        selection = default;
        ApplySelection();
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
        PromptText.IsVisible = false;
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
        if (!visible) return;
        Canvas.SetLeft(Selection, selection.X);
        Canvas.SetTop(Selection, selection.Y);
        Selection.Width = selection.Width;
        Selection.Height = selection.Height;
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);

    /// <summary>The Mac error sentence, shown in place instead of silently closing.</summary>
    private void ShowInvalidSelection()
    {
        PromptText.Text = UiText.T("屏幕选区无效，请重新框选。");
        PromptText.IsVisible = true;
        AcceptButton.IsEnabled = false;
        origin = null;
        selection = default;
        ApplySelection();
    }

    /// <summary>Evidence hook: show the Mac small-region prompt without a real too-small drag.</summary>
    public void ShowInvalidSelectionForCheck() => ShowInvalidSelection();

    /// <summary>Evidence hook: what the display picker lists (friendly names + size).</summary>
    public IReadOnlyList<string> DisplayLabels => displays.Select(display => display.Label).ToList();

    public RegionCaptureMode Mode => mode;

    private void Accept_Click(object? sender, RoutedEventArgs e)
    {
        var rect = ImageRect;
        if (rect.Width <= 0) { ShowInvalidSelection(); return; }
        var scale = rect.Width / capture.Width;
        var x = (int)Math.Round((selection.X - rect.X) / scale);
        var y = (int)Math.Round((selection.Y - rect.Y) / scale);
        var width = (int)Math.Round(selection.Width / scale);
        var height = (int)Math.Round(selection.Height / scale);
        // Mac ScreenshotService: 屏幕选区无效，请重新框选。 (a click or a too-small frame)
        if (width < 4 || height < 4) { ShowInvalidSelection(); return; }
        SelectedScreenRect = new PixelRect(capture.X + x, capture.Y + y, width, height);
        Close(ScreenCapture.Crop(capture, new PixelRect(x, y, width, height)));
    }
}
