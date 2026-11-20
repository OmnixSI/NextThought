using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace NextThought.Desktop.Views;

/// <summary>
/// The floating cue island. A borderless, always-on-top, non-activating window that shows the
/// question in play and the head of the answer, centred at the top of the primary screen.
/// </summary>
public partial class CueIslandWindow : Window
{
    private readonly AppState state;
    /// <summary>Set only by the owner at real shutdown; otherwise Alt+F4 hides the island.</summary>
    public bool AllowClose { get; set; }
    public CueIslandWindow() : this(new AppState()) { }

    public CueIslandWindow(AppState state)
    {
        InitializeComponent();
        this.state = state;
        // C1: a non-activating tool panel; WS_EX_NOACTIVATE keeps the foreground app focused.
        ShowActivated = false;
        Focusable = false;
        Topmost = true;
        ApplyCueWidth();
        state.Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NextThought.Desktop.SettingsViewModel.SelectedCueWidth)) ApplyCueWidth();
        };
        Opened += (_, _) => UiTranslation.Apply(this, NextThought.Core.UiText.EnglishUi);
        Closing += (_, e) =>
        {
            if (AllowClose) return;
            e.Cancel = true;
            Hide();
        };
        DataContext = state;
        Opened += (_, _) =>
        {
            ApplyNonActivatingStyle();
            MoveToTopCentre();
            WindowCaptureHiding.Apply(this, WindowCaptureHiding.PrivacyEnabled);
            StartTracking();
            ApplyExpanded(state.Settings.CueAlwaysOpen || !state.Recording, forCheck: false);
        };
        state.Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.CueAlwaysOpen))
                ApplyExpanded(state.Settings.CueAlwaysOpen || !state.Recording, forCheck: false);
        };
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AppState.DetectedQuestion) or nameof(AppState.AnswerText)) FitHeight();
        };
    }

    private const int GwlExstyle = -20;
    private const long WsExTransparent = 0x00000020L;
    private DispatcherTimer? tracker;
    private DateTimeOffset? pointerEntered;
    private DateTimeOffset? pointerLeft;
    private bool checkExpanded;

    /// <summary>C2/C3: hover state exposed to the self-check.</summary>
    public bool Hovering { get; private set; }
    public bool Collapsed { get; private set; } = true;
    public bool ClickThrough { get; private set; } = true;

    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);

    /// <summary>C2: poll the pointer every 80 ms; only the area over the island takes clicks.</summary>
    private void StartTracking()
    {
        if (tracker is not null) return;
        tracker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        tracker.Tick += (_, _) => TrackPointer();
        tracker.Start();
    }

    private void TrackPointer()
    {
        if (!IsVisible || TryGetPlatformHandle()?.Handle is not { } handle || handle == IntPtr.Zero) return;
        var expanded = Hovering || state.Settings.CueAlwaysOpen || checkExpanded;
        if (!GetCursorPos(out var point) || !GetWindowRect(handle, out var rect))
        {
            ApplyExpanded(expanded, checkExpanded);
            return;
        }
        var inside = point.X >= rect.Left - 6 && point.X <= rect.Right + 6
                     && point.Y >= rect.Top - 6 && point.Y <= rect.Bottom + 6;
        var now = DateTimeOffset.UtcNow;
        if (inside)
        {
            pointerLeft = null;
            pointerEntered ??= now;
            if (!expanded && now - pointerEntered >= TimeSpan.FromSeconds(0.22)) ApplyExpanded(true, checkExpanded);
        }
        else
        {
            pointerEntered = null;
            if (expanded && !state.Settings.CueAlwaysOpen && !checkExpanded)
            {
                pointerLeft ??= now;
                if (now - pointerLeft >= TimeSpan.FromSeconds(0.2)) ApplyExpanded(false, false);
            }
        }
        SetClickThrough(!(Hovering || state.Settings.CueAlwaysOpen || checkExpanded));
    }

    /// <summary>C3/C4: expand on hover or the always-open setting; collapse after the pointer leaves.</summary>
    private void ApplyExpanded(bool expanded, bool forCheck)
    {
        checkExpanded = forCheck && expanded;
        Hovering = expanded;
        Collapsed = !expanded;
        if (expanded)
        {
            HeadlineText.IsVisible = true;
            DetailText.IsVisible = true;
            FitHeight();
        }
        else
        {
            DetailText.IsVisible = false;
            TranslationPanel.IsVisible = false;
            FitHeight();
        }
        SetClickThrough(!expanded);
    }

    private void SetClickThrough(bool enabled)
    {
        ClickThrough = enabled;
        if (TryGetPlatformHandle()?.Handle is not { } handle || handle == IntPtr.Zero) return;
        var style = GetWindowLongPtr(handle, GwlExstyle).ToInt64();
        style = enabled ? style | WsExTransparent : style & ~WsExTransparent;
        SetWindowLongPtr(handle, GwlExstyle, new IntPtr(style));
    }

    private const long WsExNoActivate = 0x08000000L;
    private const long WsExToolWindow = 0x00000080L;

    /// <summary>C1: never steal focus from the meeting or the user's editor.</summary>
    private void ApplyNonActivatingStyle()
    {
        if (TryGetPlatformHandle()?.Handle is not { } handle || handle == IntPtr.Zero) return;
        var style = GetWindowLongPtr(handle, GwlExstyle).ToInt64();
        style |= WsExNoActivate | WsExToolWindow;
        SetWindowLongPtr(handle, GwlExstyle, new IntPtr(style));
    }

    /// <summary>QA: reads the current extended style for the self-check.</summary>
    public long ExtendedStyle => TryGetPlatformHandle()?.Handle is { } handle && handle != IntPtr.Zero
        ? GetWindowLongPtr(handle, GwlExstyle).ToInt64() : 0;

    public bool IsNonActivating => (ExtendedStyle & WsExNoActivate) != 0;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);

    private void ApplyCueWidth()
    {
        Width = state.Settings.SelectedCueWidth?.Value switch
        {
            "narrow" => 440,
            "wide" => 660,
            _ => 540
        };
        if (IsVisible) MoveToTopCentre();
    }

    private void MoveToTopCentre()
    {
        if (Screens.Primary is not { } screen) return;
        var scale = screen.Scaling;
        var widthPixels = (int)(Width * scale);
        var x = screen.Bounds.X + Math.Max(0, (screen.Bounds.Width - widthPixels) / 2);
        var y = screen.Bounds.Y + (int)(4 * scale);
        Position = new PixelPoint(x, y);
    }

    private void FitHeight()
    {
        if (!IsVisible) return;
        InvalidateMeasure();
    }

    protected override void OnPointerPressed(Avalonia.Input.PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }

    /// <summary>D1 evidence: switch the island through the five Mac visual states.</summary>
    public void ApplyStateForCheck(string state)
    {
        checkExpanded = state != "collapsed";
        Width = state switch { "collapsed" => 220, "launcher" => 360, _ => 540 };
        switch (state)
        {
            case "collapsed":
                QuestionText.Text = "已收起";
                HeadlineText.IsVisible = false;
                DetailText.IsVisible = false;
                TranslationPanel.IsVisible = false;
                break;
            case "launcher":
                QuestionText.Text = "准备开始一场会议";
                HeadlineText.IsVisible = false;
                DetailText.IsVisible = false;
                TranslationPanel.IsVisible = false;
                break;
            case "deck":
                HeadlineText.IsVisible = true;
                DetailText.IsVisible = false;
                TranslationPanel.IsVisible = false;
                break;
            case "bilingual":
                HeadlineText.IsVisible = true;
                DetailText.IsVisible = true;
                TranslationPanel.IsVisible = true;
                break;
            default:
                QuestionText.Text = "温度补偿之后残差还剩多少，和有限元对得上吗？";
                HeadlineText.IsVisible = true;
                DetailText.IsVisible = true;
                TranslationPanel.IsVisible = false;
                break;
        }
    }
}
