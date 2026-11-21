using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Threading;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop.Views;

/// <summary>
/// E14 / §6.7: the floating subtitle bar — the "字幕条" mode of the live window, ported from
/// SubtitleBarView.swift + SubtitleBarController.swift. A non-activating panel above other windows
/// and never key: it appears when somebody speaks, fades a few seconds after the last word, is
/// click-through, turns almost transparent while the pointer is over it (so what is underneath
/// stays visible), and after resting on it for 0.6 s becomes solid and draggable. The auto-end
/// reminder keeps it up with the countdown and a 继续记录 button. The timing and the text fitting
/// come from Core (SubtitleBarTiming / SubtitleFeed / SubtitleText, the Swift ports).
/// </summary>
public partial class SubtitleBarWindow : Window
{
    // Mac SubtitleBarController.
    private const double MaxCardWidth = 900;
    private const double CardPadding = 24; // Space.s6
    private const double HoverDimSeconds = 0.6;
    private static readonly SubtitleBarTiming Timing = SubtitleBarTiming.Standard;

    private readonly AppState state;
    private readonly SubtitleFeed feed = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private bool hiddenByTiming;
    private bool dimmed;
    private bool editing;
    private bool autoEndShown;
    private double stillSeconds;
    private double cardWidth = 800;
    private double baseFont = 22;
    private Win32Native.NativePoint lastMouse;
    private bool preview;

    private const int GwlExstyle = -20;
    private const long WsExTransparent = 0x00000020L;
    private const long WsExNoActivate = 0x08000000L;
    private const long WsExToolWindow = 0x00000080L;

    /// <summary>Set only by the owner at real shutdown; otherwise Alt+F4 hides the bar.</summary>
    public bool AllowClose { get; set; }

    /// <summary>C13: QA state for the self-check.</summary>
    public bool IsDimmed => dimmed;
    public bool IsEditing => editing;
    public bool IsClickThrough { get; private set; }

    /// <summary>True while the user explicitly chose the subtitles mode; no idle fade-out.</summary>
    public bool Persistent { get; set; }

    /// <summary>The card width (the window is wider by the shadow insets).</summary>
    public double CardWidth => cardWidth;

    public double AppliedWidth => Width;

    public double AppliedFontSize => PrimaryText.FontSize;

    public SubtitleBarWindow() : this(new AppState()) { }

    public SubtitleBarWindow(AppState state)
    {
        InitializeComponent();
        DataContext = state;
        this.state = state;
        Opened += (_, _) =>
        {
            ApplyNonActivatingStyle();
            ApplyLayout();
            ApplyPrivacy();
            timer.Start();
        };
        timer.Tick += (_, _) => { Tick(); TrackPointer(); };
        Closed += (_, _) => timer.Stop();
        Closing += (_, e) =>
        {
            if (AllowClose) return;
            e.Cancel = true;
            HideForUser();
        };
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.Recording) && !state.Recording) HideForUser();
            if (e.PropertyName is nameof(AppState.AutoEndNotice) or nameof(AppState.Recording)) ApplyLayout();
        };
        state.Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SettingsViewModel.SubtitleFontSize) or nameof(SettingsViewModel.SubtitleWidth)
                or nameof(SettingsViewModel.SubtitleShowTranslation) or nameof(SettingsViewModel.CapturePrivacy))
            {
                ApplyLayout();
                ApplyPrivacy();
            }
        };
    }

    private void ApplyNonActivatingStyle()
    {
        if (TryGetPlatformHandle()?.Handle is not { } handle || handle == IntPtr.Zero) return;
        var style = Win32Native.GetWindowLongPtr(handle, GwlExstyle).ToInt64() | WsExNoActivate | WsExToolWindow;
        Win32Native.SetWindowLongPtr(handle, GwlExstyle, new IntPtr(style));
    }

    /// <summary>Mac applyPrivacy: excluded from screen sharing like every other window.</summary>
    private void ApplyPrivacy() => WindowCaptureHiding.Apply(this, WindowCaptureHiding.PrivacyEnabled);

    /// <summary>
    /// Mac place(): card width min(900, visible width − 160), bottom centre (or the remembered
    /// spot), room for the shadow around it. The text sizes follow the subtitle font size and the
    /// app's text zoom (Mac Font.zoomed in SubtitleBarView).
    /// </summary>
    private void ApplyLayout()
    {
        if (Screens.Primary is not { } screen && Screens.All.Count == 0) return;
        var target = Screens.Primary ?? Screens.All[0];
        var visibleWidth = target.Bounds.Width / Math.Max(1, target.Scaling);
        var wanted = Math.Min(MaxCardWidth, Math.Max(200, visibleWidth - 160));
        if (Math.Abs(cardWidth - wanted) > 0.5)
        {
            cardWidth = wanted;
            Width = cardWidth + 40;
        }
        baseFont = Math.Clamp(state.Settings.SubtitleFontSize, 16, 36);
        var size = TextZoomService.Scaled(baseFont);
        PrimaryText.FontSize = size;
        PrimaryText.LineHeight = size * 1.45;
        SecondaryText.FontSize = TextZoomService.Scaled(Math.Max(12, baseFont * 0.6));
        SpeakerLabel.FontSize = TextZoomService.Scaled(Math.Max(12, baseFont * 0.5));
        AutoEndText.FontSize = TextZoomService.Scaled(Math.Max(14, baseFont * 0.75));
        AutoEndIcon.FontSize = TextZoomService.Scaled(Math.Max(13, baseFont * 0.6));
        SharedBadge.IsVisible = !state.SettingsSnapshot.CapturePrivacy;
        if (!preview) PlaceAtBottom();
    }

    private void PlaceAtBottom()
    {
        if (Screens.Primary is not { } screen) return;
        var scale = screen.Scaling;
        var widthPixels = (int)(Width * scale);
        var heightPixels = (int)(Bounds.Height * scale);
        var x = screen.Bounds.X + Math.Max(0, (screen.Bounds.Width - widthPixels) / 2);
        var y = screen.Bounds.Y + Math.Max(0, screen.Bounds.Height - heightPixels - (int)(24 * scale));
        if (state.Settings.SubtitleBarX is { } savedX && state.Settings.SubtitleBarY is { } savedY)
        {
            var saved = new PixelPoint((int)savedX, (int)savedY);
            if (Screens.All.Any(item => item.Bounds.Contains(saved)))
            {
                Position = saved;
                return;
            }
        }
        Position = new PixelPoint(x, y);
    }

    /// <summary>C13: back to the default bottom-centre spot and forget the saved position.</summary>
    public void ResetPosition()
    {
        state.Settings.SubtitleBarX = null;
        state.Settings.SubtitleBarY = null;
        _ = state.Settings.SaveAsync();
        PlaceAtBottom();
    }

    // ---- pointer: dim, then draggable ------------------------------------------------

    /// <summary>Mac updatePointer(): dim while the pointer is over the bar, solid and draggable after 0.6 s.</summary>
    private void TrackPointer()
    {
        if (!IsVisible || TryGetPlatformHandle()?.Handle is not { } handle || handle == IntPtr.Zero) return;
        if (!Win32Native.GetCursorPos(out var mouse) || !Win32Native.GetWindowRect(handle, out var rect)) return;
        var inside = mouse.X >= rect.Left - 4 && mouse.X <= rect.Right + 4
                     && mouse.Y >= rect.Top - 4 && mouse.Y <= rect.Bottom + 4;
        var moved = Math.Abs(mouse.X - lastMouse.X) > 2 || Math.Abs(mouse.Y - lastMouse.Y) > 2;
        lastMouse = mouse;

        // The reminder's button needs a solid bar that takes clicks, without any dwell.
        if (autoEndShown)
        {
            stillSeconds = 0;
            SetDimmed(false);
            SetClickThrough(false);
            return;
        }
        if (!editing) SetClickThrough(true);
        if (editing)
        {
            if (!inside && !preview) SetEditing(false);
            return;
        }
        if (!inside)
        {
            stillSeconds = 0;
            SetDimmed(false);
            return;
        }
        SetDimmed(true);
        stillSeconds = moved ? 0 : stillSeconds + 0.1;
        if (stillSeconds < HoverDimSeconds) return;
        SetDimmed(false);
        SetEditing(true);
    }

    private void SetDimmed(bool on)
    {
        if (dimmed == on) return;
        dimmed = on;
        Opacity = on ? 0.15 : 1;
    }

    private void SetEditing(bool on)
    {
        if (editing == on) return;
        editing = on;
        EditingOutline.IsVisible = on;
        Opacity = 1;
        if (!on) stillSeconds = 0;
    }

    private void SetClickThrough(bool enabled)
    {
        IsClickThrough = enabled;
        if (TryGetPlatformHandle()?.Handle is not { } handle || handle == IntPtr.Zero) return;
        var style = Win32Native.GetWindowLongPtr(handle, GwlExstyle).ToInt64();
        style = enabled ? style | WsExTransparent : style & ~WsExTransparent;
        Win32Native.SetWindowLongPtr(handle, GwlExstyle, new IntPtr(style));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (editing && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!editing) return;
        state.Settings.SubtitleBarX = Position.X;
        state.Settings.SubtitleBarY = Position.Y;
        _ = state.Settings.SaveAsync();
    }

    protected override void OnDoubleTapped(TappedEventArgs e)
    {
        base.OnDoubleTapped(e);
        if (editing) ResetPosition();
    }

    // ---- following the transcript ----------------------------------------------------

    public void HideForUser()
    {
        hiddenByTiming = false;
        timer.Stop();
        SetClickThrough(true);
        Hide();
    }

    /// <summary>Re-showing the bar after a user hide must restart its timer.</summary>
    public void ResumeAfterUserShow()
    {
        if (!timer.IsEnabled) timer.Start();
    }

    /// <summary>Mac tick(): refresh the feed, hide/order the bar, fit the height, update the pointer.</summary>
    private void Tick()
    {
        if (preview) return;
        var now = DateTimeOffset.UtcNow;
        Refresh(now);

        // C13/K4: while the user explicitly chose the 字幕条 mode (Persistent) the bar stays up like
        // Mac's subtitle mode; only the automatic "somebody is speaking" mode hides after the idle
        // delay. Without this the timer restarted by ResumeAfterUserShow hid a re-shown bar again.
        var phase = editing || autoEndShown || Persistent
            ? SubtitleBarTiming.Phase.Visible
            : Timing.PhaseAt(now, feed.LastActivity);
        if (phase == SubtitleBarTiming.Phase.Hidden)
        {
            if (!hiddenByTiming) { hiddenByTiming = true; IsVisible = false; }
            return;
        }
        if (!IsVisible)
        {
            if (!hiddenByTiming && !Persistent) return;
            Show();
            ApplyLayout();
        }
        hiddenByTiming = false;
        Opacity = dimmed ? 0.15 : 1;
    }

    private void Refresh(DateTimeOffset now)
    {
        var inPerson = state.CurrentSession?.InPerson == true;
        var channels = new List<SubtitleChannel>
        {
            new("remote", Strip(state.SystemPartial), state.SystemPartialTranslation),
            new(inPerson ? "room" : "me", Strip(state.MicrophonePartial), state.MicrophonePartialTranslation)
        };
        var lastFinal = state.Transcript.Count > 0 ? state.Transcript[^1] : null;
        var final = lastFinal is null ? null : new SubtitleLine(lastFinal.Speaker, lastFinal.Text, lastFinal.TranslationRaw);
        feed.Update(channels, final, now);

        ApplyAutoEnd();
        if (autoEndShown) return;
        if (feed.Line is not { } line) return;
        var inner = Math.Max(200, cardWidth - 2 * CardPadding);
        var fontSize = (double)Math.Max(14, baseFont);
        var tag = line.Speaker == "remote" ? 0 : 8;
        var primaryUnits = (int)(2 * inner / (fontSize * 0.5) * 0.8 - tag);
        var secondaryUnits = (int)(inner / (fontSize * 0.6 * 0.5) * 0.8);
        var display = SubtitleText.DisplayFor(line, Math.Max(12, primaryUnits), Math.Max(12, secondaryUnits));
        ShowLine(display);
    }

    /// <summary>The auto-end reminder: countdown + 继续记录, staying up until it is withdrawn.</summary>
    private void ApplyAutoEnd()
    {
        var due = state.CueIslandAutoEndDue;
        if (due != autoEndShown)
        {
            autoEndShown = due;
            AutoEndRow.IsVisible = due;
            LineStack.IsVisible = !due;
            if (due) SetClickThrough(false);
        }
        if (!due) return;
        AutoEndText.Text = state.CueIslandAutoEndNotice;
        if (!IsVisible && !hiddenByTiming) Show();
        hiddenByTiming = false;
        IsVisible = true;
    }

    private void ShowLine(SubtitleDisplay display)
    {
        PrimaryText.Text = display.Primary;
        var other = display.Speaker is not ("remote" or "");
        SpeakerLabel.Text = display.Speaker == "room" ? UiText.T("现场") : UiText.T("我");
        SpeakerLabel.IsVisible = other;
        SecondaryText.Text = display.Secondary;
        SecondaryText.IsVisible = state.Settings.SubtitleShowTranslation && display.Secondary.Length > 0;
    }

    private void KeepRecording_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => state.CueIslandKeepRecording();

    // ---- screenshot/self-check hooks --------------------------------------------------

    /// <summary>E14: one scripted line (and its translation) for the evidence capture.</summary>
    public void ShowPreview(string primary, string secondary, string speaker = "remote", bool autoEnd = false)
    {
        preview = true;
        timer.Stop();
        ApplyLayout();
        SharedBadge.IsVisible = !state.SettingsSnapshot.CapturePrivacy;
        if (autoEnd)
        {
            autoEndShown = true;
            AutoEndRow.IsVisible = true;
            LineStack.IsVisible = false;
            AutoEndText.Text = UiText.Format("好像没人说话了，%lld 秒后结束记录", 42);
            SetClickThrough(false);
        }
        else
        {
            autoEndShown = false;
            AutoEndRow.IsVisible = false;
            LineStack.IsVisible = true;
            ShowLine(new SubtitleDisplay(primary, secondary, speaker, IsPartial: false));
        }
        Opacity = 1;
        IsVisible = true;
    }

    /// <summary>Leaves the preview and returns to following the transcript.</summary>
    public void ClearPreview()
    {
        preview = false;
        autoEndShown = false;
        AutoEndRow.IsVisible = false;
        LineStack.IsVisible = true;
        ResumeAfterUserShow();
    }

    private int PrimaryUnits => Math.Max(12, (int)((cardWidth - 2 * CardPadding) / (baseFont * 0.5) * 1.6));
    private int SecondaryUnits => Math.Max(12, (int)((cardWidth - 2 * CardPadding) / (baseFont * 0.6 * 0.5) * 0.8));

    private static string Strip(string partial)
    {
        if (string.IsNullOrEmpty(partial)) return "";
        var separator = partial.IndexOf(" · ", StringComparison.Ordinal);
        return separator >= 0 ? partial[(separator + 3)..] : partial;
    }
}
