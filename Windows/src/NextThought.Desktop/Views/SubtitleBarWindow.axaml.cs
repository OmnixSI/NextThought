using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using NextThought.Core;

namespace NextThought.Desktop.Views;

/// <summary>
/// The floating subtitle bar: a slim, always-on-top, non-activating strip that shows the
/// sentence being spoken (and its translation), then fades out when nobody speaks.
/// The timing and text fitting come from <see cref="SubtitleFeed"/> in Core
/// (ported from SubtitleBar.swift).
/// </summary>
public partial class SubtitleBarWindow : Window
{
    private static readonly SubtitleBarTiming Timing = SubtitleBarTiming.Standard;
    private readonly AppState state;
    private readonly SubtitleFeed feed = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private bool hiddenByTiming;

    public SubtitleBarWindow() : this(new AppState()) { }

    public SubtitleBarWindow(AppState state)
    {
        InitializeComponent();
        DataContext = state;
        this.state = state;
        Opened += (_, _) =>
        {
            PlaceAtBottom();
            WindowCaptureHiding.Hide(this);
            timer.Start();
        };
        timer.Tick += (_, _) => Tick();
        Closed += (_, _) => timer.Stop();
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AppState.Recording) && !state.Recording) Hide();
        };
    }

    private void PlaceAtBottom()
    {
        if (Screens.Primary is not { } screen) return;
        var scale = screen.Scaling;
        var widthPixels = (int)(Width * scale);
        var x = screen.Bounds.X + Math.Max(0, (screen.Bounds.Width - widthPixels) / 2);
        var y = screen.Bounds.Y + screen.Bounds.Height - (int)(150 * scale);
        Position = new PixelPoint(x, y);
    }

    private void Tick()
    {
        var now = DateTimeOffset.UtcNow;
        var inPerson = state.CurrentSession?.InPerson == true;
        var channels = new List<SubtitleChannel>
        {
            new("remote", Strip(state.SystemPartial), state.SystemPartialTranslation),
            new(inPerson ? "room" : "me", Strip(state.MicrophonePartial), state.MicrophonePartialTranslation)
        };
        var lastFinal = state.Transcript.Count > 0 ? state.Transcript[^1] : null;
        var final = lastFinal is null ? null : new SubtitleLine(lastFinal.Speaker, lastFinal.Text, lastFinal.TranslationRaw);
        feed.Update(channels, final, now);

        var opacity = Timing.OpacityAt(now, feed.LastActivity);
        if (opacity <= 0.01)
        {
            if (!hiddenByTiming) { hiddenByTiming = true; IsVisible = false; }
            return;
        }
        hiddenByTiming = false;
        if (!IsVisible) Show();
        Opacity = opacity;

        if (feed.Line is not { } line)
        {
            PrimaryText.Text = "";
            SecondaryText.Text = "";
            return;
        }
        var display = SubtitleText.DisplayFor(line, PrimaryUnits, SecondaryUnits);
        PrimaryText.Text = display.Primary;
        SecondaryText.Text = display.Secondary;
        SecondaryText.IsVisible = display.Secondary.Length > 0;
        SpeakerText.Text = display.Speaker == "remote" ? "对方" : display.Speaker == "room" ? "现场" : "我";
        LiveDot.Opacity = display.IsPartial ? 1 : 0.35;
    }

    // Width budget in half-em units, from the window width at the design font size.
    private int PrimaryUnits => Math.Max(12, (int)((Width - 80) / 11));
    private int SecondaryUnits => Math.Max(12, (int)((Width - 80) / 7));

    private static string Strip(string partial)
    {
        if (string.IsNullOrEmpty(partial)) return "";
        var separator = partial.IndexOf(" · ", StringComparison.Ordinal);
        return separator >= 0 ? partial[(separator + 3)..] : partial;
    }

    private DispatcherTimer Timer => timer;
}
