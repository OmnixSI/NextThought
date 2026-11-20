using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using NextThought.Platform;
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
            if (e.PropertyName == nameof(NextThought.Desktop.SettingsViewModel.CueScreen) && IsVisible) MoveToTopCentre();
        };
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
            ApplySceneTint();
            springWidth = Width;
            targetWidth = Width;
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

    /// <summary>C5: the five shapes of the Mac island.</summary>
    public enum CuePhase { Compact, Hearing, Listening, Cue, Launcher }
    public CuePhase Phase { get; private set; } = CuePhase.Compact;
    private bool updatingPhase;
    private DispatcherTimer? springTimer;
    private double targetWidth;
    private double springWidth;
    private double springFrom;
    private DateTimeOffset springStarted;

    /// <summary>C9/D5: Motion.morph spring (response 0.5 s, damping fraction 0.82), sampled
    /// from the analytic solution by <see cref="Motion.Progress"/> on every frame. When the
    /// system asks for less motion the width snaps straight to its target.</summary>
    private void StartSpring()
    {
        if (springTimer is not null) return;
        springTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        springTimer.Tick += (_, _) => StepSpring();
        springTimer.Start();
    }

    private void SetTargetWidth(double width)
    {
        if (Math.Abs(targetWidth - width) < 0.1) return;
        targetWidth = width;
        if (springWidth <= 0) springWidth = Width;
        if (Motion.Reduce)
        {
            springTimer?.Stop();
            springTimer = null;
            springWidth = width;
            Width = Math.Max(120, width);
            return;
        }
        springFrom = springWidth;
        springStarted = DateTimeOffset.UtcNow;
        StartSpring();
    }

    private void StepSpring()
    {
        var elapsed = DateTimeOffset.UtcNow - springStarted;
        var duration = Motion.Duration(Motion.Kind.Morph);
        if (elapsed >= duration)
        {
            springWidth = targetWidth;
            springTimer?.Stop();
            springTimer = null;
            Width = Math.Max(120, springWidth);
            return;
        }
        var progress = Motion.Progress(Motion.Kind.Morph, elapsed);
        springWidth = springFrom + (targetWidth - springFrom) * progress;
        Width = Math.Max(120, springWidth);
    }

    private void ApplySceneTint()
    {
        var mode = SessionModes.Parse(state.CurrentSession?.Mode).ToMeetingMode();
        var tint = SceneTheme.For(mode).TintDark;
        InnerBorder.BorderBrush = Brush.Parse(tint);
    }

    /// <summary>C2/C3: hover state exposed to the self-check.</summary>
    public bool Hovering { get; private set; }
    public bool Collapsed { get; private set; } = true;
    public bool ClickThrough { get; private set; } = true;

/// <summary>C2: poll the pointer every 80 ms; only the area over the island takes clicks.</summary>
    private void StartTracking()
    {
        if (tracker is not null) return;
        tracker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        tracker.Tick += (_, _) => { UpdatePhase(); TrackPointer(); };
        tracker.Start();
    }

    /// <summary>C5: compact / hearing / listening / cue / launcher widths and content.</summary>
    private void UpdatePhase()
    {
        if (updatingPhase) return;
        updatingPhase = true;
        try
        {
            var phase = !state.Recording ? CuePhase.Launcher
                : state.AnswerText.Length > 0 ? CuePhase.Cue
                : state.HearingText.Length > 0 ? CuePhase.Hearing
                : state.DetectedQuestion.Length > 0 ? CuePhase.Listening
                : CuePhase.Compact;
            Phase = phase;
            var baseWidth = phase switch
            {
                CuePhase.Launcher => 360.0,
                CuePhase.Compact => 260.0,
                CuePhase.Hearing => 440.0,
                CuePhase.Listening => 480.0,
                _ => state.Settings.SelectedCueWidth?.Value switch
                {
                    "narrow" => 440.0, "wide" => 660.0, _ => 540.0
                }
            };
            if (!checkExpanded && phase is not CuePhase.Cue) SetTargetWidth(baseWidth);
            if (phase is CuePhase.Cue) SetTargetWidth(state.Settings.SelectedCueWidth?.Value switch
            {
                "narrow" => 440, "wide" => 660, _ => 540
            });
            ApplySceneTint();
            PhaseText.Text = phase switch
            {
                CuePhase.Launcher => "\u51C6\u5907\u5F00\u59CB\u4E00\u573A\u4F1A\u8BAE",
                CuePhase.Hearing => state.HearingText,
                CuePhase.Listening => "\u6B63\u5728\u542C\u2026",
                CuePhase.Compact => "\u63D0\u8BCD\u5C9B\u5DF2\u6536\u8D77\uFF0C\u9F20\u6807\u79FB\u4E0A\u6765\u5C31\u5C55\u5F00",
                _ => ""
            };
            PhaseText.IsVisible = phase is not CuePhase.Cue;
            if (phase is CuePhase.Cue)
            {
                HeadlineText.IsVisible = true;
                DetailText.IsVisible = true;
            }
            else if (phase is CuePhase.Compact or CuePhase.Launcher)
            {
                HeadlineText.IsVisible = false;
                DetailText.IsVisible = false;
                TranslationPanel.IsVisible = false;
            }
        }
        finally { updatingPhase = false; }
    }

    private void TrackPointer()
    {
        if (!IsVisible || TryGetPlatformHandle()?.Handle is not { } handle || handle == IntPtr.Zero) return;
        var expanded = Hovering || state.Settings.CueAlwaysOpen || checkExpanded;
        if (!Win32Native.GetCursorPos(out var point) || !Win32Native.GetWindowRect(handle, out var rect))
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
        ActionPanel.IsVisible = expanded;
        if (expanded)
        {
            HeadlineText.IsVisible = true;
            DetailText.IsVisible = true;
            FitHeight();
        }
        else
        {
            ActionPanel.IsVisible = false;
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
        var style = Win32Native.GetWindowLongPtr(handle, GwlExstyle).ToInt64();
        style = enabled ? style | WsExTransparent : style & ~WsExTransparent;
        Win32Native.SetWindowLongPtr(handle, GwlExstyle, new IntPtr(style));
    }

    private const long WsExNoActivate = 0x08000000L;
    private const long WsExToolWindow = 0x00000080L;

    /// <summary>C1: never steal focus from the meeting or the user's editor.</summary>
    private void ApplyNonActivatingStyle()
    {
        if (TryGetPlatformHandle()?.Handle is not { } handle || handle == IntPtr.Zero) return;
        var style = Win32Native.GetWindowLongPtr(handle, GwlExstyle).ToInt64();
        style |= WsExNoActivate | WsExToolWindow;
        Win32Native.SetWindowLongPtr(handle, GwlExstyle, new IntPtr(style));
    }

    /// <summary>QA: reads the current extended style for the self-check.</summary>
    public long ExtendedStyle => TryGetPlatformHandle()?.Handle is { } handle && handle != IntPtr.Zero
        ? Win32Native.GetWindowLongPtr(handle, GwlExstyle).ToInt64() : 0;

    public bool IsNonActivating => (ExtendedStyle & WsExNoActivate) != 0;

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

    /// <summary>C10: the chosen display, else the built-in/primary one (Mac prefers the camera screen).</summary>
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

    private void MoveToTopCentre()
    {
        if (SelectedScreen() is not { } screen) return;
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

    /// <summary>
    /// E31: switch the island through DemoMode.snapshotIsland phases. The Mac phase names
    /// (cue/open/compact/hearing/deck/launcher/bilingual/pinned/slide) are accepted directly;
    /// "collapsed" and "listening" are the Windows screenshot aliases of compact and cue.
    /// The copy comes from <see cref="NextThought.Platform.DemoIslands"/>, so the same data drives
    /// the demo and its evidence captures.
    /// </summary>
    public void ApplyStateForCheck(string state)
    {
        var legacyCollapsed = state == "collapsed";
        var normalized = state switch
        {
            "collapsed" => "compact",
            "listening" => "cue",
            _ => state
        };
        var phase = NextThought.Platform.DemoIslands.For(normalized);
        checkExpanded = phase.Phase is not NextThought.Platform.DemoIslandPhase.Compact;
        Width = phase.Phase switch
        {
            NextThought.Platform.DemoIslandPhase.Compact => 220,
            NextThought.Platform.DemoIslandPhase.Launcher => 360,
            _ => 540
        };
        if (phase.Question.Length > 0)
        {
            this.state.DetectedQuestion = phase.Question;
            QuestionText.Text = phase.Question;
        }
        if (phase.Answer.Length > 0) this.state.AnswerText = phase.Answer;
        if (phase.Translation.Length > 0) this.state.IslandTranslation = phase.Translation;
        if (phase.ExtraTranscriptLine is { Length: > 0 } heard) this.state.SystemPartial = heard;
        else if (phase.Phase is NextThought.Platform.DemoIslandPhase.Hearing) this.state.SystemPartial = "";
        if (legacyCollapsed) QuestionText.Text = "已收起";
        else if (state == "launcher") QuestionText.Text = "准备开始一场会议";
        HeadlineText.IsVisible = phase.Phase is not (NextThought.Platform.DemoIslandPhase.Compact
            or NextThought.Platform.DemoIslandPhase.Launcher);
        DetailText.IsVisible = phase.Phase is not (NextThought.Platform.DemoIslandPhase.Compact
            or NextThought.Platform.DemoIslandPhase.Launcher or NextThought.Platform.DemoIslandPhase.Deck);
        TranslationPanel.IsVisible = phase.Phase is NextThought.Platform.DemoIslandPhase.Bilingual
            or NextThought.Platform.DemoIslandPhase.Pinned;
    }
}
