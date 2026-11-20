using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NextThought.Desktop.Views;

/// <summary>
/// The floating cue island. A borderless, always-on-top, non-activating window that shows the
/// question in play and the head of the answer, centred at the top of the primary screen.
/// </summary>
public partial class CueIslandWindow : Window
{
    private readonly AppState state;
    public CueIslandWindow() : this(new AppState()) { }

    public CueIslandWindow(AppState state)
    {
        InitializeComponent();
        this.state = state;
        ApplyCueWidth();
        state.Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NextThought.Desktop.SettingsViewModel.SelectedCueWidth)) ApplyCueWidth();
        };
        Opened += (_, _) => UiTranslation.Apply(this, NextThought.Core.UiText.EnglishUi);
        DataContext = state;
        Opened += (_, _) =>
        {
            MoveToTopCentre();
            WindowCaptureHiding.Hide(this);
        };
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AppState.DetectedQuestion) or nameof(AppState.AnswerText)) FitHeight();
        };
    }

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
}
