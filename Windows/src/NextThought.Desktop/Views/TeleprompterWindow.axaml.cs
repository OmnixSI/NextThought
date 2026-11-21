using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop.Views;

/// <summary>
/// E29 / §6.7: the answer window — the free-floating teleprompter, ported from
/// TeleprompterController.swift (TeleprompterView). A non-activating, resizable, draggable floating
/// layer that never takes keyboard focus; click-through is available (with the small always
/// interactive handle window so it can be turned off again), opacity is a setting, and the bar can
/// park at the three Mac placements: camera (top centre), bottom, right. The cue island keeps its
/// own size buttons; this window has its own 小/大 buttons too (Mac: they keep their own size).
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
    private readonly ScrollFollowState follow = new();
    private readonly DispatcherTimer headerTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private TeleprompterHandleWindow? clickHandle;
    private bool through;
    private bool programmaticScroll;
    private bool previewing;

    public TeleprompterWindow() : this(new AppState()) { }

    public TeleprompterWindow(AppState state)
    {
        this.state = state;
        InitializeComponent();
        DataContext = state;
        geometry = WindowLayout.Load(state.Paths);
        Width = geometry.TeleprompterWidth;
        Height = geometry.TeleprompterHeight;
        ApplyAppearance();
        ApplyFont();
        Opened += (_, _) =>
        {
            ApplyAppearance();
            ApplyFont();
            RestorePosition();
            WindowCaptureHiding.Apply(this, WindowCaptureHiding.PrivacyEnabled);
            ApplyNonActivatingStyle();
            ApplyClickThrough(through);
            UpdateHeaderAndFooter();
            headerTimer.Start();
        };
        headerTimer.Tick += (_, _) => UpdateHeaderAndFooter();
        PositionChanged += (_, _) =>
        {
            Remember();
            clickHandle?.PlaceNear(Position);
        };
        Resized += (_, _) => Remember();
        Closed += (_, _) =>
        {
            headerTimer.Stop();
            through = false;
            ApplyClickThrough(false);
            clickHandle?.Close();
            clickHandle = null;
            Remember();
        };
        BodyScroll.ScrollChanged += (_, _) => OnBodyScrolled();
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AppState.PlainAnswer) or nameof(AppState.DisplayedAnswerText)
                or nameof(AppState.Answering) or nameof(AppState.DisplayedQuestionText)
                or nameof(AppState.DetectedQuestion) or nameof(AppState.HistoryPositionText)
                or nameof(AppState.Recording) or nameof(AppState.ListeningPaused))
            {
                UpdateHeaderAndFooter();
                if (e.PropertyName is nameof(AppState.PlainAnswer) or nameof(AppState.DisplayedAnswerText)) OnAnswerChanged();
            }
        };
        state.Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.TeleprompterHideBackground)) ApplyAppearance();
            if (e.PropertyName == nameof(SettingsViewModel.TeleprompterFontSize)) ApplyFont();
            if (e.PropertyName == nameof(SettingsViewModel.TeleprompterHideBackground))
                ApplyAppearance();
        };
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>Mac applyAppearance(): with the background hidden the window stays opaque and only the chrome goes clear.</summary>
    private void ApplyAppearance()
    {
        if (state.Settings.TeleprompterHideBackground)
        {
            Opacity = 1;
            Surface.Background = Brushes.Transparent;
            Surface.BorderBrush = Brushes.Transparent;
            Surface.BorderThickness = new Thickness(0);
            HiddenBackgroundBorder.IsVisible = true;
        }
        else
        {
            Surface.Background = (IBrush?)this.FindResource("SurfaceRaised") ?? Brush.Parse("#F2F0EB");
            Surface.BorderBrush = (IBrush?)this.FindResource("Hairline") ?? Brush.Parse("#331C1B19");
            Surface.BorderThickness = new Thickness(0.5);
            HiddenBackgroundBorder.IsVisible = false;
            Opacity = Math.Clamp(state.SettingsSnapshot.TeleprompterOpacity, 0.35, 1);
        }
    }

    /// <summary>The header and body follow the teleprompter font size; the body line spacing is 45 % of it.</summary>
    private void ApplyFont()
    {
        var size = Math.Clamp(state.Settings.TeleprompterFontSize, 14, 36);
        BodyText.BaseFontSize = size;
        EmptyHint.FontSize = size * 0.75;
    }

    private void ApplyNonActivatingStyle()
    {
        if (TryGetPlatformHandle()?.Handle is not { } handle || handle == IntPtr.Zero) return;
        var style = Win32Native.GetWindowLongPtr(handle, GwlExstyle).ToInt64() | WsExNoActivate | WsExToolWindow;
        Win32Native.SetWindowLongPtr(handle, GwlExstyle, new IntPtr(style));
    }

    public void HideForAllWindows()
    {
        Hide();
        clickHandle?.Hide();
    }

    // ---- header / body / footer (TeleprompterView) ------------------------------------------

    /// <summary>"● 正在听  <question>" plus position, size, click-through and close controls.</summary>
    private void UpdateHeaderAndFooter()
    {
        if (previewing) return;
        var live = state.Recording && !state.ListeningPaused;
        StatusDot.Background = (IBrush?)this.FindResource(live ? "Seal" : "Tertiary") ?? Brushes.Gray;
        StatusText.Text = !state.Recording ? UiText.T("未在听")
            : state.ListeningPaused ? UiText.T("已暂停")
            : UiText.T("正在听");

        var question = state.DisplayedQuestionText.Length > 0 ? state.DisplayedQuestionText : state.DetectedQuestion;
        question = AnswerRefinements.OriginalQuestion(question);
        QuestionText.Text = question.Length > 0 ? question : state.LiveCandidateText;
        QuestionText.IsVisible = QuestionText.Text.Length > 0;
        AnsweringProgress.IsVisible = state.Answering;

        var position = state.HistoryPositionText;
        HistoryPrevButton.IsVisible = position.Length > 0;
        HistoryNextButton.IsVisible = position.Length > 0;
        HistoryPosition.IsVisible = position.Length > 0;
        HistoryPosition.Text = position;
        ThroughIcon.Text = through
            ? (string?)this.FindResource("IconIslandCursorOff") ?? ""
            : (string?)this.FindResource("IconIslandCursor") ?? "";
        Footer.IsVisible = !through;

        var answer = state.DisplayedAnswerText;
        BodyText.Text = answer;
        BodyText.IsVisible = answer.Length > 0;
        EmptyHint.IsVisible = answer.Length == 0;
        EmptyHint.Text = state.Answering
            ? UiText.T("正在生成…")
            : UiText.Format("回答会显示在这里。你也可以按 %@ 手动请求回答。", state.ForceAnswerShortcut);
        ShorterButton.IsEnabled = answer.Length > 0 && !state.Answering;
        ExampleButton.IsEnabled = ShorterButton.IsEnabled;
        EnglishButton.IsEnabled = ShorterButton.IsEnabled;
        MoreLookupItem.IsEnabled = answer.Length > 0;
        MoreCopyItem.IsEnabled = answer.Length > 0;
        MoreRegenerateItem.IsEnabled = !state.Answering && question.Length > 0;
    }

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

    // ---- header commands ---------------------------------------------------------------------

    private void HistoryPrev_Click(object? sender, RoutedEventArgs e) => state.MoveAnswerHistory(-1);

    private void HistoryNext_Click(object? sender, RoutedEventArgs e) => state.MoveAnswerHistory(1);

    private void Smaller_Click(object? sender, RoutedEventArgs e) => SetFont(-2);

    private void Larger_Click(object? sender, RoutedEventArgs e) => SetFont(2);

    /// <summary>Mac: state.teleprompterFontSize clamped 14–36, saved with the answer settings.</summary>
    private void SetFont(double delta)
    {
        state.Settings.TeleprompterFontSize = Math.Clamp(state.Settings.TeleprompterFontSize + delta, 14, 36);
        ApplyFont();
        try { _ = state.Settings.SaveAsync(); } catch { }
        Remember();
    }

    private void Through_Click(object? sender, RoutedEventArgs e) => ToggleClickThrough();

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    private void Refine_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string name } && Enum.TryParse<AnswerRefinement>(name, true, out var refinement))
            _ = state.RefineAsync(refinement);
    }

    private void More_Click(object? sender, RoutedEventArgs e) => MoreButton.Flyout?.ShowAt(MoreButton);

    private void Regenerate_Click(object? sender, RoutedEventArgs e) => state.RegenerateAnswerCommand.Execute(null);

    private void Lookup_Click(object? sender, RoutedEventArgs e) => state.LookUpDisplayedAnswerCommand.Execute(null);

    private void Copy_Click(object? sender, RoutedEventArgs e) => state.CopyAnswerCommand.Execute(null);

    private void Workbench_Click(object? sender, RoutedEventArgs e) => state.ShowWorkbenchRequested?.Invoke();

    private void Stop_Click(object? sender, RoutedEventArgs e) => state.CancelCommand.Execute(null);

    private void Answer_Click(object? sender, RoutedEventArgs e) => state.ForceAnswerCommand.Execute(null);

    // ---- placement ---------------------------------------------------------------------------

    /// <summary>Mac place(): camera = top centre (8 below the visible top), bottom = 24 above the bottom, right = 16 from the right, vertically centred.</summary>
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
                screen.Bounds.Y + Math.Max(0, screen.Bounds.Height - height - (int)(24 * scale))),
            "right" => new PixelPoint(
                screen.Bounds.X + Math.Max(0, screen.Bounds.Width - width - (int)(16 * scale)),
                screen.Bounds.Y + Math.Max(0, (screen.Bounds.Height - height) / 2)),
            _ => new PixelPoint(
                screen.Bounds.X + Math.Max(0, (screen.Bounds.Width - width) / 2),
                screen.Bounds.Y + (int)(8 * scale))
        };
        Remember();
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
        MoveTo("camera");
    }

    private void Remember()
    {
        geometry.TeleprompterX = Position.X;
        geometry.TeleprompterY = Position.Y;
        geometry.TeleprompterWidth = Width;
        geometry.TeleprompterHeight = Height;
        geometry.TeleprompterFontSize = state.Settings.TeleprompterFontSize;
        geometry.TeleprompterOpacity = state.SettingsSnapshot.TeleprompterOpacity;
        geometry.TeleprompterClickThrough = through;
        geometry.Save(state.Paths);
    }

    // ---- click-through -----------------------------------------------------------------------

    /// <summary>WS_EX_LAYERED | WS_EX_TRANSPARENT makes the whole window click-through (plan §9).</summary>
    public void ApplyClickThrough(bool enabled)
    {
        through = enabled;
        if (TryGetPlatformHandle()?.Handle is not { } handle || handle == IntPtr.Zero) return;
        var style = Win32Native.GetWindowLongPtr(handle, GwlExstyle).ToInt64();
        style |= WsExLayered;
        style = enabled ? style | WsExTransparent : style & ~WsExTransparent;
        Win32Native.SetWindowLongPtr(handle, GwlExstyle, new IntPtr(style));
        UpdateClickHandle(enabled);
        if (IsVisible) UpdateHeaderAndFooter();
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

    // ---- screenshot/self-check hooks ---------------------------------------------------------

    /// <summary>E29: a scripted answer and placement for the three-position evidence capture.</summary>
    public void ShowPreview(string answer, string question = "", bool answering = false, bool clickThrough = false)
    {
        previewing = true;
        headerTimer.Stop();
        ApplyFont();
        BodyText.Text = answer;
        BodyText.IsVisible = answer.Length > 0;
        EmptyHint.IsVisible = answer.Length == 0;
        EmptyHint.Text = answering ? UiText.T("正在生成…")
            : UiText.Format("回答会显示在这里。你也可以按 %@ 手动请求回答。", state.ForceAnswerShortcut);
        StatusDot.Background = (IBrush?)this.FindResource("Seal") ?? Brushes.Gray;
        StatusText.Text = UiText.T("正在听");
        QuestionText.Text = question;
        QuestionText.IsVisible = question.Length > 0;
        AnsweringProgress.IsVisible = answering;
        Footer.IsVisible = !clickThrough;
        ThroughIcon.Text = (string?)this.FindResource(clickThrough ? "IconIslandCursorOff" : "IconIslandCursor") ?? "";
        Opacity = state.Settings.TeleprompterHideBackground ? 1 : Math.Clamp(state.SettingsSnapshot.TeleprompterOpacity, 0.35, 1);
    }

    public void ClearPreview()
    {
        previewing = false;
        UpdateHeaderAndFooter();
        headerTimer.Start();
    }

    /// <summary>QA: whether the body is drawn over the raised surface (Mac: background shown).</summary>
    public bool SurfaceVisible => HiddenBackgroundBorder.IsVisible == false;

    public double BodyFontSize => BodyText.BaseFontSize;
}
