using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Runtime.InteropServices;

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
            WindowCaptureHiding.Hide(this);
        };
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AppState.DetectedQuestion) or nameof(AppState.AnswerText)) FitHeight();
        };
    }

    private const int GwlExstyle = -20;
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
