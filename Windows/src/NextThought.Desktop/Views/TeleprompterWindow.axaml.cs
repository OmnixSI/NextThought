using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using NextThought.Core;
using Avalonia.Media;

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
    private const long WsExNoActivate = 0x08000000;
    private const long WsExToolWindow = 0x00000080;

    private readonly AppState state;
    private readonly WindowLayout geometry;
    private bool through;
    private bool dragBarHidden;
    private TeleprompterHandleWindow? clickHandle;
    private readonly ScrollFollowState follow = new();
    private bool programmaticScroll;

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
        BodyText.FontSize = geometry.TeleprompterFontSize;
        through = geometry.TeleprompterClickThrough;
        ApplyAppearance();
        state.Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.TeleprompterHideBackground)) ApplyAppearance();
        };
        Opened += (_, _) =>
        {
            ApplyAppearance();
            RestorePosition();
            WindowCaptureHiding.Apply(this, WindowCaptureHiding.PrivacyEnabled);
            ApplyNonActivatingStyle();
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
        BodyScroll.ScrollChanged += (_, _) => OnBodyScrolled();
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.PlainAnswer)) OnAnswerChanged();
        };
        AddHandler(KeyDownEvent, OnPreviewKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    /// <summary>C15: the lower bound is 0.35; hiding the background keeps the window opaque and clears only the shell.</summary>
    private void ApplyAppearance()
    {
        if (state.Settings.TeleprompterHideBackground)
        {
            Opacity = 1;
            Shell.Background = Brushes.Transparent;
            Shell.BorderBrush = Brush.Parse("#CCECEBE8");
            Shell.BorderThickness = new Avalonia.Thickness(1.5);
        }
        else
        {
            Shell.Background = Brush.Parse("#E6121110");
            Shell.BorderBrush = Brush.Parse("#33ECEBE8");
            Shell.BorderThickness = new Avalonia.Thickness(0.5);
            Opacity = Math.Clamp(geometry.TeleprompterOpacity, 0.35, 1);
        }
    }

    /// <summary>C17: a non-activating tool window like the cue island.</summary>
    private void ApplyNonActivatingStyle()
    {
        if (TryGetPlatformHandle()?.Handle is not { } handle || handle == IntPtr.Zero) return;
        var style = GetWindowLongPtr(handle, GwlExstyle).ToInt64() | WsExNoActivate | WsExToolWindow;
        SetWindowLongPtr(handle, GwlExstyle, new IntPtr(style));
    }

    /// <summary>C17: hide-all must also take the click-through strip.</summary>
    public void HideForAllWindows()
    {
        Hide();
        clickHandle?.Hide();
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
        if (!state.Settings.TeleprompterHideBackground) geometry.TeleprompterOpacity = Opacity;
        geometry.TeleprompterFontSize = BodyText.FontSize;
        geometry.TeleprompterClickThrough = through;
        geometry.Save(state.Paths);
    }

    /// <summary>C16: follow the live edge until the reader scrolls away or selects text.</summary>
    private void OnAnswerChanged()
    {
        follow.ReceivedNewContent();
        if (follow.IsFollowing) ScrollToLatest();
        FollowButton.IsVisible = !follow.IsFollowing;
    }

    private void ScrollToLatest()
    {
        programmaticScroll = true;
        BodyScroll.ScrollToEnd();
        Dispatcher.UIThread.Post(() => programmaticScroll = false);
    }

    private void OnBodyScrolled()
    {
        if (programmaticScroll) return;
        var atEnd = BodyScroll.Offset.Y >= Math.Max(0, BodyScroll.Extent.Height - BodyScroll.Viewport.Height) - 2;
        if (atEnd) follow.ReachedLatest(); else follow.UserScrolledAway();
        FollowButton.IsVisible = !follow.IsFollowing;
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (ScrollFollowInputClassifier.IsScrollNavigationKeyCode((ushort)e.Key)) follow.UserScrolledAway();
    }

    private void Follow_Click(object? sender, RoutedEventArgs e)
    {
        follow.ReturnToLatest();
        FollowButton.IsVisible = false;
        ScrollToLatest();
    }

    private void Workbench_Click(object? sender, RoutedEventArgs e) => state.ShowWorkbenchRequested?.Invoke();

    private void Refine_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string name } && Enum.TryParse<AnswerRefinement>(name, true, out var refinement))
            _ = state.RefineAsync(refinement);
    }

    /// <summary>C14: the three Mac placements: screen top centre, bottom centre, right side.</summary>
    public void MoveTo(string placement)
    {
        if (Screens.Primary is not { } screen) return;
        var scale = screen.Scaling;
        var width = (int)(Width * scale);
        var height = (int)(Height * scale);
        Position = placement switch
        {
            "bottom" => new PixelPoint(
                screen.Bounds.X + Math.Max(0, (screen.Bounds.Width - width) / 2),
                screen.Bounds.Y + Math.Max(0, screen.Bounds.Height - height - (int)(80 * scale))),
            "right" => new PixelPoint(
                screen.Bounds.X + Math.Max(0, screen.Bounds.Width - width - (int)(24 * scale)),
                screen.Bounds.Y + Math.Max(0, (screen.Bounds.Height - height) / 2)),
            _ => new PixelPoint(
                screen.Bounds.X + Math.Max(0, (screen.Bounds.Width - width) / 2),
                screen.Bounds.Y + (int)(8 * scale))
        };
        Remember();
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
        if (state.Settings.TeleprompterHideBackground) return;
        Opacity = Math.Clamp(Opacity + delta, 0.35, 1);
        geometry.TeleprompterOpacity = Opacity;
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
