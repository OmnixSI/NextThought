using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.VisualTree;
using Avalonia.Threading;
using NextThought.Core;
using NextThought.Desktop.Views.Island;
using NextThought.Platform;

namespace NextThought.Desktop.Views;

/// <summary>
/// §6.6 / C5–C9: the cue island. A borderless, always-on-top, non-activating window that hangs
/// from the top edge of the screen (like the Mac notch) and shows one of the six presentations of
/// CueIsland.swift. The window never takes keyboard focus, clicks pass through it while it is put
/// away, only the island's controls take clicks while it is open, and the outline and width morph
/// with the Mac <c>Motion.morph</c> spring (nothing animates when Windows asks for less motion).
/// </summary>
public partial class CueIslandWindow : Window
{
    // -- interaction -------------------------------------------------------------------------
    private const int GwlExstyle = -20;
    private const long WsExTransparent = 0x00000020L;
    private const long WsExNoActivate = 0x08000000L;
    private const long WsExToolWindow = 0x00000080L;
    private readonly AppState state;
    private DispatcherTimer? tracker;
    private DispatcherTimer? springTimer;
    private DateTimeOffset? pointerEntered;
    private DateTimeOffset? pointerLeft;
    private bool trackingSuspended;
    private bool leftButtonWasDown;
    private bool clickThrough = true;

    // -- the morph ---------------------------------------------------------------------------
    /// <summary>The Mac demo-notch metrics: the island grows out of the top edge.</summary>
    private static readonly IslandMetrics Metrics = IslandMetrics.Notch;

    private double contentWidth = IslandLayout.EarsWidth(IslandMetrics.Notch);
    private double expand; // 0: closed (notch), 1: open
    private double fromContentWidth;
    private double fromExpand;
    private double targetContentWidth;
    private double targetExpand;
    private DateTimeOffset springStarted;
    private bool springing;

    /// <summary>The screenshot/demo capture width: the Mac island screenshots are 800 pt wide.</summary>
    private const double PreviewWidth = 800;

    /// <summary>Set only by the owner at real shutdown; otherwise Alt+F4 hides the island.</summary>
    public bool AllowClose { get; set; }

    /// <summary>The island's model, for the self-check and the screenshot batch.</summary>
    public CueIslandViewModel Model => model;

    private readonly CueIslandViewModel model;

    public CueIslandWindow() : this(new AppState()) { }

    public CueIslandWindow(AppState state)
    {
        this.state = state;
        model = new CueIslandViewModel(state);
        InitializeComponent();
        DataContext = model;
        // The island is a non-activating tool panel: it never takes focus from the meeting app.
        ShowActivated = false;
        Focusable = false;
        Topmost = true;
        model.PropertyChanged += OnModelChanged;
        state.Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.SelectedCueWidth)) SyncLayout();
            if (e.PropertyName == nameof(SettingsViewModel.CueScreen) && IsVisible) PositionAtTop();
        };
        Closing += (_, e) =>
        {
            if (AllowClose) return;
            e.Cancel = true;
            Hide();
            model.SetHovering(false);
            model.ClearPinned();
        };
        Opened += (_, _) =>
        {
            ApplyNonActivatingStyle();
            // Outside a meeting the island is the notch's launcher entry point (Mac toggle()):
            // opening it by hand shows the scene launcher until a scene is picked.
            if (!state.Recording && !model.Backdrop) model.Pin();
            model.Attach();
            model.Refresh();
            SyncLayout();
            ApplyFrame();
            if (!model.Backdrop) PositionAtTop();
            StartTracking();
        };
        Closed += (_, _) =>
        {
            tracker?.Stop();
            tracker = null;
            springTimer?.Stop();
            springTimer = null;
            model.Detach();
        };
    }

    // ---- the model drives the frame ---------------------------------------------------------

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CueIslandViewModel.Phase) or nameof(CueIslandViewModel.TargetContentWidth)
            or nameof(CueIslandViewModel.ShowIsland) or nameof(CueIslandViewModel.HoverBump)
            or nameof(CueIslandViewModel.EdgeWidth) or nameof(CueIslandViewModel.Idle))
            SyncLayout();
    }

    /// <summary>Starts the Mac morph (or jumps to the final frame when Windows asks for fewer animations).</summary>
    private void SyncLayout()
    {
        targetContentWidth = model.TargetContentWidth;
        targetExpand = model.Idle ? 0 : 1;
        if (Motion.Reduce)
        {
            contentWidth = targetContentWidth;
            expand = targetExpand;
            StopSpring();
            ApplyFrame();
            return;
        }
        if (!springing && Math.Abs(contentWidth - targetContentWidth) < 0.5 && Math.Abs(expand - targetExpand) < 0.001)
        {
            ApplyFrame();
            return;
        }
        fromContentWidth = contentWidth;
        fromExpand = expand;
        springStarted = DateTimeOffset.UtcNow;
        springing = true;
        if (springTimer is null)
        {
            springTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            springTimer.Tick += (_, _) => StepSpring();
            springTimer.Start();
        }
    }

    private void StopSpring()
    {
        springing = false;
        springTimer?.Stop();
        springTimer = null;
    }

    /// <summary>C9: the morph spring (response 0.5 s, damping 0.82), the same token as the Mac.</summary>
    private void StepSpring()
    {
        var elapsed = DateTimeOffset.UtcNow - springStarted;
        var duration = Motion.Duration(Motion.Kind.Morph);
        var progress = elapsed >= duration ? 1 : Motion.Progress(Motion.Kind.Morph, elapsed);
        contentWidth = fromContentWidth + (targetContentWidth - fromContentWidth) * progress;
        expand = Math.Clamp(fromExpand + (targetExpand - fromExpand) * progress, 0, 1);
        ApplyFrame();
        if (elapsed < duration) return;
        contentWidth = targetContentWidth;
        expand = targetExpand;
        StopSpring();
        ApplyFrame();
    }

    /// <summary>Paints the outline and the window extent from the current morph values.</summary>
    private void ApplyFrame()
    {
        var flare = IslandLayout.Flare(expand < 0.5, Metrics);
        var radius = IslandLayout.Radius(expand < 0.5, Metrics);
        var frameWidth = contentWidth + 2 * flare;
        Frame.Flare = flare;
        Frame.CornerRadiusValue = radius;
        Frame.Width = frameWidth;
        Body.Margin = new Thickness(flare, 0, flare, 0);
        ApplyHoverBump();
        double windowWidth;
        if (model.Backdrop) windowWidth = PreviewWidth;
        else windowWidth = frameWidth + 2 * CueIslandViewModel.ShadowPadding;
        if (Math.Abs(Width - windowWidth) > 0.1) Width = windowWidth;
        if (IsVisible && !model.Backdrop) PositionAtTop();
    }

    /// <summary>The Mac hover bump: the island leans towards the pointer before it opens (×1.04).</summary>
    private void ApplyHoverBump()
    {
        var scale = model.HoverBump ? 1.04 : 1.0;
        if (Frame.RenderTransform is ScaleTransform existing && Math.Abs(existing.ScaleX - scale) < 0.001) return;
        Frame.RenderTransformOrigin = new RelativePoint(0.5, 0, RelativeUnit.Relative);
        Frame.RenderTransform = new ScaleTransform(scale, scale);
    }

    private void ApplyNonActivatingStyle()
    {
        if (TryGetPlatformHandle()?.Handle is not { } handle || handle == IntPtr.Zero) return;
        var style = Win32Native.GetWindowLongPtr(handle, GwlExstyle).ToInt64();
        style |= WsExNoActivate | WsExToolWindow;
        Win32Native.SetWindowLongPtr(handle, GwlExstyle, new IntPtr(style));
    }

    /// <summary>QA: the current extended style; the self-check asserts WS_EX_NOACTIVATE is set.</summary>
    public long ExtendedStyle => TryGetPlatformHandle()?.Handle is { } handle && handle != IntPtr.Zero
        ? Win32Native.GetWindowLongPtr(handle, GwlExstyle).ToInt64()
        : 0;

    public bool IsNonActivating => (ExtendedStyle & WsExNoActivate) != 0;

    /// <summary>The window lets the pointer through right now (C2/C3).</summary>
    public bool ClickThrough => clickThrough;

    public bool Hovering => model.Hovering;

    public bool Collapsed => model.Idle;

    /// <summary>The animated content width, for the self-check.</summary>
    public double IslandContentWidth => contentWidth;

    public double IslandFrameWidth => Frame.Bounds.Width;

    public double IslandFlare => Frame.Flare;

    public double IslandCornerRadius => Frame.CornerRadiusValue;

    // ---- pointer tracking -------------------------------------------------------------------

    private Screen? SelectedScreen()
    {
        var selected = state.Settings.CueScreen;
        if (!string.IsNullOrEmpty(selected))
        {
            var match = Screens.All.FirstOrDefault(screen => screen.DisplayName == selected);
            if (match is not null) return match;
        }
        return Screens.Primary ?? Screens.All.FirstOrDefault();
    }

    private void PositionAtTop()
    {
        if (SelectedScreen() is not { } screen) return;
        var scale = screen.Scaling;
        var width = (int)(Width * scale);
        var x = screen.Bounds.X + Math.Max(0, (screen.Bounds.Width - width) / 2);
        Position = new PixelPoint(x, screen.Bounds.Y);
    }

    private void StartTracking()
    {
        if (tracker is not null) return;
        tracker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        tracker.Tick += (_, _) => Track();
        tracker.Start();
    }

    /// <summary>
    /// C2/C3: poll the pointer. Resting on the island for 0.22 s opens it; leaving for 0.2 s puts it
    /// away. Clicks pass through the window unless the pointer is over the bar or one of the island's
    /// buttons — the rest of an open island stays click-through, as on the Mac.
    /// </summary>
    private void Track()
    {
        if (!IsVisible) return;
        if (trackingSuspended) return;
        if (!Win32Native.GetCursorPos(out var cursor)) return;
        var island = IslandScreenRect();
        if (island is null) return;
        var inside = island.Value.Contains(new Point(cursor.X, cursor.Y));
        var now = DateTimeOffset.UtcNow;
        if (inside)
        {
            pointerLeft = null;
            pointerEntered ??= now;
            if (!model.Hovering && now - pointerEntered >= TimeSpan.FromSeconds(IslandLayout.HoverOpenSeconds))
                model.SetHovering(true);
        }
        else
        {
            pointerEntered = null;
            if (model.Hovering && !model.KeepsOpen)
            {
                pointerLeft ??= now;
                if (now - pointerLeft >= TimeSpan.FromSeconds(IslandLayout.HoverCloseSeconds))
                    model.SetHovering(false);
            }
        }

        var leftDown = IslandInput.LeftButtonDown;
        // A click outside puts a pinned deck away (Mac track(): "A click outside puts a pinned deck away").
        if (model.Pinned && leftDown && !leftButtonWasDown && !inside) model.UnpinFromOutside();
        leftButtonWasDown = leftDown;

        var wantsClicks = CursorOverControl(cursor);
        SetClickThrough(!wantsClicks);
    }

    /// <summary>The island's own rectangle on screen (not the shadow padding).</summary>
    private Rect? IslandScreenRect()
    {
        if (Frame.Bounds.Width <= 0 || Frame.Bounds.Height <= 0) return null;
        var topLeft = Frame.PointToScreen(new Point(0, 0));
        var bottomRight = Frame.PointToScreen(new Point(Frame.Bounds.Width, Frame.Bounds.Height));
        return new Rect(topLeft.X, topLeft.Y, Math.Max(1, bottomRight.X - topLeft.X),
            Math.Max(1, bottomRight.Y - topLeft.Y)).Inflate(6);
    }

    /// <summary>
    /// Whether the pointer is over something that should take a click. The bar is one region; every
    /// visible button inside the island is another. Everything else stays click-through (C6).
    /// </summary>
    private bool CursorOverControl(Win32Native.NativePoint cursor)
    {
        var point = new Point(cursor.X, cursor.Y);
        foreach (var control in InteractiveControls())
        {
            if (!control.IsEffectivelyVisible) continue;
            if (control.Bounds.Width <= 0 || control.Bounds.Height <= 0) continue;
            var topLeft = control.PointToScreen(new Point(0, 0));
            var bottomRight = control.PointToScreen(new Point(control.Bounds.Width, control.Bounds.Height));
            var rect = new Rect(topLeft.X, topLeft.Y, Math.Max(1, bottomRight.X - topLeft.X),
                Math.Max(1, bottomRight.Y - topLeft.Y));
            if (rect.Contains(point)) return true;
        }
        return false;
    }

    private IEnumerable<Control> InteractiveControls()
    {
        yield return BarRow;
        foreach (var button in new[]
                 {
                     DeckForceAnswerButton, DeckPreviousAnswerButton, DeckRecordOnlyButton, DeckPauseButton,
                     DeckEndButton, KeepRecordingButton, EndNowButton, LauncherOpenWindowButton,
                     EnableTranslationButton, HistoryPrevButton, HistoryNextButton, ShorterButton, ExampleButton,
                     FontSmallerButton, FontLargerButton, OpenWindowButton, PinButton, CollapseButton,
                     SlidePrevButton, SlideNextButton
                 })
            yield return button;
        if (LauncherScenesHost.ItemCount > 0)
        {
            foreach (var child in LauncherScenesHost.GetVisualDescendants().OfType<Button>()) yield return child;
        }
    }

    private void SetClickThrough(bool enabled)
    {
        if (clickThrough == enabled) return;
        clickThrough = enabled;
        if (TryGetPlatformHandle()?.Handle is not { } handle || handle == IntPtr.Zero) return;
        var style = Win32Native.GetWindowLongPtr(handle, GwlExstyle).ToInt64();
        style = enabled ? style | WsExTransparent : style & ~WsExTransparent;
        Win32Native.SetWindowLongPtr(handle, GwlExstyle, new IntPtr(style));
    }

    // ---- clicks ------------------------------------------------------------------------------

    private void Bar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        e.Handled = true;
        model.BarClicked();
    }

    private void LauncherScene_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string modeKey }) return;
        e.Handled = true;
        model.StartScene(modeKey);
    }

    // ---- screenshot / self-check states ------------------------------------------------------

    /// <summary>Leaves the screenshot state and returns to live tracking.</summary>
    public void ClearStateForCheck()
    {
        trackingSuspended = false;
        model.Backdrop = false;
        state.CueIslandDemoAnswerReady(null);
        model.ForcedPhase = null;
        model.DemoClock = null;
        model.DemoAutoEndSeconds = null;
        model.ClearPinned();
        model.SetHovering(false);
        model.Refresh();
        SyncLayout();
    }

    /// <summary>The pointer is where the self-check's hover-dwell assertions need it (C2/C3).</summary>
    public void SetHoveringForCheck(bool hovering) => model.SetHovering(hovering);

    /// <summary>Forces the system reduced-motion path so the self-check can assert the snap (C9).</summary>
    public void SetReducedMotionForCheck(bool reduce)
    {
        Motion.AppReducedEffects = reduce;
        SyncLayout();
    }

    public void SetPinnedForCheck(bool pinned)
    {
        if (pinned) model.Pin();
        else model.ClearPinned();
        SyncLayout();
    }
}

/// <summary>The visible controls the island's click-through logic probes (partial-class helper).</summary>
internal static class Numeric
{
    public static Rect Inflate(this Rect rect, double amount) =>
        new(rect.X - amount, rect.Y - amount, rect.Width + 2 * amount, rect.Height + 2 * amount);
}
