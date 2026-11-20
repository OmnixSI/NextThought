using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using NextThought.Core;

namespace NextThought.Desktop.Views;

/// <summary>
/// Floating teleprompter (plan §9): 640×300 default, resizable, draggable by its background,
/// always on top, with font size, opacity and click-through controls. Geometry and the
/// per-window preferences are stored in win-ui.json, never in settings.json or backups.
/// </summary>
public partial class TeleprompterWindow : Window
{
    private const int GwlExstyle = -20;
    private const long WsExLayered = 0x00080000;
    private const long WsExTransparent = 0x00000020;

    private readonly AppState state;
    private readonly WindowLayout geometry;
    private bool through;
    private bool dragBarHidden;
    private TeleprompterHandleWindow? clickHandle;

    public TeleprompterWindow() : this(new AppState()) { }

    public TeleprompterWindow(AppState state)
    {
        this.state = state;
        InitializeComponent();
        Opened += (_, _) => UiTranslation.Apply(this, NextThought.Core.UiText.EnglishUi);
        DataContext = state;
        geometry = WindowLayout.Load(state.Paths);
        Width = geometry.TeleprompterWidth;
        Height = geometry.TeleprompterHeight;
        Opacity = geometry.TeleprompterOpacity;
        BodyText.FontSize = geometry.TeleprompterFontSize;
        through = geometry.TeleprompterClickThrough;
        Opened += (_, _) =>
        {
            RestorePosition();
            WindowCaptureHiding.Apply(this, WindowCaptureHiding.PrivacyEnabled);
            ApplyClickThrough(through);
        };
        PositionChanged += (_, _) =>
        {
            Remember();
            clickHandle?.PlaceNear(Position);
        };
        Resized += (_, _) => Remember();
        Closed += (_, _) =>
        {
            through = false;
            ApplyClickThrough(false);
            clickHandle?.Close();
            clickHandle = null;
            Remember();
        };
        DragBar.DoubleTapped += (_, _) => ToggleControls();
    }

    private void RestorePosition()
    {
        if (!double.IsNaN(geometry.TeleprompterX) && !double.IsNaN(geometry.TeleprompterY))
        {
            var saved = new PixelPoint((int)geometry.TeleprompterX, (int)geometry.TeleprompterY);
            if (Screens.All.Any(screen => saved.X >= screen.Bounds.X - 40 && saved.X < screen.Bounds.X + screen.Bounds.Width
                                          && saved.Y >= screen.Bounds.Y - 40 && saved.Y < screen.Bounds.Y + screen.Bounds.Height))
            {
                Position = saved;
                return;
            }
        }
        if (Screens.Primary is { } screen)
        {
            var scale = screen.Scaling;
            Position = new PixelPoint(
                screen.Bounds.X + (int)((screen.Bounds.Width - (Width * scale)) / 2),
                screen.Bounds.Y + (int)(screen.Bounds.Height - (Height * scale) - 120 * scale));
        }
    }

    private void Remember()
    {
        geometry.TeleprompterX = Position.X;
        geometry.TeleprompterY = Position.Y;
        geometry.TeleprompterWidth = Width;
        geometry.TeleprompterHeight = Height;
        geometry.TeleprompterOpacity = Opacity;
        geometry.TeleprompterFontSize = BodyText.FontSize;
        geometry.TeleprompterClickThrough = through;
        geometry.Save(state.Paths);
    }

    private void ToggleControls()
    {
        dragBarHidden = !dragBarHidden;
        DragBar.IsVisible = !dragBarHidden;
        ModeText.Text = dragBarHidden ? "双击恢复控件" : "拖动可移动 · 双击隐藏控件";
    }

    private void DragBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (through) return;
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }

    private void Smaller_Click(object? sender, RoutedEventArgs e) => SetFont(-2);

    private void Larger_Click(object? sender, RoutedEventArgs e) => SetFont(2);

    private void SetFont(double delta)
    {
        BodyText.FontSize = Math.Clamp(BodyText.FontSize + delta, 12, 64);
        BodyText.LineHeight = BodyText.FontSize * 1.45;
        Remember();
    }

    private void Fade_Click(object? sender, RoutedEventArgs e) => SetOpacity(-0.08);

    private void Solid_Click(object? sender, RoutedEventArgs e) => SetOpacity(0.08);

    private void SetOpacity(double delta)
    {
        Opacity = Math.Clamp(Opacity + delta, 0.25, 1);
        Remember();
    }

    private void Through_Click(object? sender, RoutedEventArgs e)
    {
        through = !through;
        ApplyClickThrough(through);
        ModeText.Text = through ? "鼠标穿透中 · 快捷键 Ctrl+Alt+T 切回" : "拖动可移动 · 双击隐藏控件";
        Remember();
    }

    /// <summary>WS_EX_LAYERED | WS_EX_TRANSPARENT makes the whole window click-through (plan §9).</summary>
    public void ApplyClickThrough(bool enabled)
    {
        through = enabled;
        if (TryGetPlatformHandle()?.Handle is not { } handle || handle == IntPtr.Zero) return;
        var style = GetWindowLongPtr(handle, GwlExstyle).ToInt64();
        style |= WsExLayered;
        style = enabled ? style | WsExTransparent : style & ~WsExTransparent;
        SetWindowLongPtr(handle, GwlExstyle, new IntPtr(style));
        UpdateClickHandle(enabled);
    }

    private void UpdateClickHandle(bool enabled)
    {
        if (!enabled)
        {
            clickHandle?.Hide();
            return;
        }
        if (clickHandle is null)
        {
            clickHandle = new TeleprompterHandleWindow();
            clickHandle.RestoreRequested += DisableClickThrough;
            clickHandle.Show();
        }
        else if (!clickHandle.IsVisible) clickHandle.Show();
        clickHandle.PlaceNear(Position);
    }

    /// <summary>QA: the separate control strip is visible while click-through is on.</summary>
    public bool ClickThroughHandleVisible => clickHandle?.IsVisible == true;

    public bool ClickThrough => through;

    /// <summary>F7-style toggle from the global hotkey; also persists the choice.</summary>
    public void ToggleClickThrough()
    {
        ApplyClickThrough(!through);
        Remember();
    }

    /// <summary>Leaves click-through mode and persists it (used by the close/hide paths).</summary>
    public void DisableClickThrough()
    {
        through = false;
        ApplyClickThrough(false);
        Remember();
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
}
