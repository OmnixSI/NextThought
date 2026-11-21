using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NextThought.Core;

namespace NextThought.Desktop.Views.Home;

/// <summary>
/// E1: the home composer (HomeView.swift StartComposer) — scene glyph, title field that guesses
/// the scene, scene pills with the selected one washed in its own pigment, the promise and its
/// three moments, and the start / prepare actions.
///
/// The five Mac transitions are here too: the selection capsule slides between chips
/// (matchedGeometryEffect), chips scale to 0.96 while pressed (SceneChipStyle), the glyph scales in
/// from 0.8, the promise/moments blur-replace, and the card stroke takes the scene tint at 45%
/// while the title is focused.
/// </summary>
public partial class StartComposer : UserControl
{
    private AppState? state;
    private bool syncing;
    /// <summary>Mac scenePinned: after picking a scene by hand the title no longer guesses it.</summary>
    private bool scenePinned;
    private Button? selectedChip;
    private bool laidOut;
    private bool hasCapsulePosition;
    private double capsuleLeft;
    private double capsuleTop;
    private CancellationTokenSource? selectionAnimation;
    private CancellationTokenSource? promiseAnimation;
    /// <summary>True only while a click/typing caused the scene change (Mac animates those).</summary>
    private bool userSceneChange;

    public StartComposer()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            laidOut = true;
            SyncSceneSelection(animate: false);
        };
        DataContextChanged += (_, _) => Attach();
        TitleBox.LostFocus += (_, _) => GuessFromTitle();
        // Mac: .animation(Motion.hover, value: titleFocused) on the card stroke.
        TitleBox.GotFocus += (_, _) => UpdateTitleFocus();
        TitleBox.LostFocus += (_, _) => UpdateTitleFocus();
        ActualThemeVariantChanged += (_, _) => SyncSceneSelection(animate: false);
        ChipHost.SizeChanged += (_, _) => SyncSceneSelection(animate: false);
    }

    private void Attach()
    {
        if (state is not null) state.PropertyChanged -= OnStatePropertyChanged;
        state = DataContext as AppState;
        if (state is not null) state.PropertyChanged += OnStatePropertyChanged;
        selectedChip = null;
        SyncSceneSelection(animate: false);
    }

    private void OnStatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppState.SelectedScene) or nameof(AppState.SelectedMode))
        {
            // Demo/series loading also changes the scene; only a person's click or typing animates.
            SyncSceneSelection(animate: userSceneChange);
            userSceneChange = false;
        }
    }

    private void Title_Changed(object? sender, TextChangedEventArgs e)
    {
        if (syncing) return;
        if (string.IsNullOrWhiteSpace(TitleBox.Text)) scenePinned = false;
        GuessFromTitle();
    }

    private void GuessFromTitle()
    {
        if (state is null || syncing || scenePinned) return;
        syncing = true;
        try
        {
            // E1: typing a title guesses the scene; the highlight follows the state below.
            userSceneChange = true;
            if (!state.GuessSceneFromTitle(TitleBox.Text)) userSceneChange = false;
        }
        finally
        {
            syncing = false;
        }
    }

    private void Scene_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SceneChoice scene } || state is null) return;
        scenePinned = true;
        userSceneChange = true;
        state.SetSceneCommand.Execute(scene.Mode);
        SyncSceneSelection(animate: userSceneChange);
        userSceneChange = false;
    }

    private void Prepare_Click(object? sender, RoutedEventArgs e)
    {
        if (state is not null) state.StartMeetingFromHome(startNow: false, state.SelectedMode);
    }

    private void Start_Click(object? sender, RoutedEventArgs e)
    {
        if (state is not null) state.StartMeetingFromHome(startNow: true, state.SelectedMode);
    }

    /// <summary>
    /// Mac StartComposer.body: the selected chip carries a scene-pigment capsule that slides between
    /// chips (matchedGeometryEffect), the glyph scales in and the promise blur-replaces on each
    /// scene change, and the dot row takes the new pigment.
    /// </summary>
    private void SyncSceneSelection(bool animate = true)
    {
        if (state is null) return;
        var dark = TopLevel.GetTopLevel(this)?.ActualThemeVariant == ThemeVariant.Dark;
        var tint = SceneTheme.For(state.SelectedMode).Tint(dark);
        var tintBrush = new SolidColorBrush(Color.Parse(tint));
        var secondary = Application.Current?.FindResource("Secondary") as IBrush ?? Brushes.Gray;
        Button? chip = null;
        foreach (var button in this.GetVisualDescendants().OfType<Button>())
        {
            if (!button.Classes.Contains("scene") || button.Tag is not SceneChoice scene) continue;
            var selected = scene.Mode == state.SelectedMode;
            button.Classes.Set("selected", selected);
            foreach (var text in button.GetVisualDescendants().OfType<TextBlock>())
                text.Foreground = selected ? new SolidColorBrush(Color.Parse(SceneTheme.For(scene.Mode).Tint(dark))) : secondary;
            if (selected) chip = button;
        }
        foreach (var dot in MomentsList.GetVisualDescendants().OfType<Ellipse>())
            dot.Fill = tintBrush;
        if (chip is null) return;
        SelectionCapsule.Background = new SolidColorBrush(Color.Parse(tint), 0.13);
        SelectionCapsule.IsVisible = true;
        var previous = selectedChip;
        var changed = !ReferenceEquals(chip, previous);
        // Only a real (person-triggered) scene change plays the Mac transitions; opening the page
        // and demo/series state loads settle instantly.
        var animateChange = animate && changed && previous is not null && laidOut;
        selectedChip = chip;
        SlideSelection(chip, animate: animateChange);
        if (!animateChange) return;
        AnimateGlyph();
        BlurReplacePromise();
    }

    /// <summary>
    /// Mac matchedGeometryEffect(id: "selection"): one capsule that travels from the old chip to the
    /// new one (Motion.snappy), instead of every chip painting its own background.
    /// </summary>
    private async void SlideSelection(Button chip, bool animate)
    {
        if (SelectionLayer is null || SelectionCapsule is null) return;
        var origin = chip.TranslatePoint(new Point(0, 0), ChipHost);
        if (origin is not { } point || chip.Bounds.Width <= 0) return;
        var left = point.X;
        var top = point.Y;
        var width = chip.Bounds.Width;
        var height = chip.Bounds.Height;
        SelectionCapsule.Width = width;
        SelectionCapsule.Height = height;
        if (!animate || !laidOut || Motion.Reduce)
        {
            Canvas.SetLeft(SelectionCapsule, left);
            Canvas.SetTop(SelectionCapsule, top);
            capsuleLeft = left;
            capsuleTop = top;
            hasCapsulePosition = true;
            return;
        }
        selectionAnimation?.Cancel();
        selectionAnimation = new CancellationTokenSource();
        var token = selectionAnimation.Token;
        var fromLeft = hasCapsulePosition ? capsuleLeft : left;
        var fromTop = hasCapsulePosition ? capsuleTop : top;
        hasCapsulePosition = true;
        capsuleLeft = left;
        capsuleTop = top;
        var animation = new Animation
        {
            Duration = Motion.Duration(Motion.Kind.Snappy),
            Easing = Motion.Easing(Motion.Kind.Snappy),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters =
                    {
                        new Setter(Canvas.LeftProperty, fromLeft),
                        new Setter(Canvas.TopProperty, fromTop)
                    }
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters =
                    {
                        new Setter(Canvas.LeftProperty, left),
                        new Setter(Canvas.TopProperty, top)
                    }
                }
            }
        };
        try
        {
            await animation.RunAsync(SelectionCapsule, token);
        }
        catch (OperationCanceledException)
        {
        }
        if (!token.IsCancellationRequested)
        {
            Canvas.SetLeft(SelectionCapsule, left);
            Canvas.SetTop(SelectionCapsule, top);
        }
    }

    /// <summary>Mac SceneGlyph: .id(scene).transition(.scale(scale: 0.8).combined(with: .opacity)).</summary>
    private async void AnimateGlyph()
    {
        if (SceneGlyphControl is null || Motion.Reduce) return;
        var scale = new ScaleTransform(0.8, 0.8);
        SceneGlyphControl.RenderTransform = scale;
        SceneGlyphControl.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        SceneGlyphControl.Opacity = 0;
        var duration = Motion.Duration(Motion.Kind.Snappy);
        var started = DateTimeOffset.UtcNow;
        try
        {
            await Task.Yield();
            SceneGlyphControl.Opacity = 1;
            while (true)
            {
                var elapsed = DateTimeOffset.UtcNow - started;
                var progress = Math.Clamp(Motion.Progress(Motion.Kind.Snappy, elapsed), 0, 1);
                scale.ScaleX = 0.8 + 0.2 * progress;
                scale.ScaleY = 0.8 + 0.2 * progress;
                if (progress >= 1 || elapsed > duration + TimeSpan.FromMilliseconds(700)) break;
                await Task.Delay(16);
            }
        }
        finally
        {
            scale.ScaleX = 1;
            scale.ScaleY = 1;
            SceneGlyphControl.Opacity = 1;
        }
    }

    /// <summary>Mac: promise + moments are .transition(.blurReplace) — a short blur+fade replace.</summary>
    private async void BlurReplacePromise()
    {
        if (PromisePanel is null) return;
        promiseAnimation?.Cancel();
        promiseAnimation = new CancellationTokenSource();
        var token = promiseAnimation.Token;
        if (Motion.Reduce)
        {
            PromisePanel.Opacity = 1;
            PromisePanel.Effect = null;
            return;
        }
        var blur = new BlurEffect { Radius = 8 };
        PromisePanel.Effect = blur;
        PromisePanel.Opacity = 0;
        var duration = Motion.Duration(Motion.Kind.Expand);
        var started = DateTimeOffset.UtcNow;
        try
        {
            while (!token.IsCancellationRequested)
            {
                var elapsed = DateTimeOffset.UtcNow - started;
                if (elapsed > duration + TimeSpan.FromMilliseconds(700)) break;
                var progress = Motion.Progress(Motion.Kind.Expand, elapsed);
                var visible = Math.Clamp(progress, 0, 1);
                PromisePanel.Opacity = visible;
                blur.Radius = 8 * (1 - visible);
                if (progress >= 1) break;
                await Task.Delay(16, token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            PromisePanel.Opacity = 1;
            PromisePanel.Effect = null;
        }
    }

    private void Title_FocusChanged(object? sender, RoutedEventArgs e) => UpdateTitleFocus();
    /// <summary>
    /// Mac StartComposer.overlay: strokeBorder(titleFocused ? scene.tint.opacity(0.45) : Ink.hairline,
    /// lineWidth: titleFocused ? 1 : 0.5), animated with Motion.hover (BrushTransition in XAML).
    /// </summary>
    private void UpdateTitleFocus()
    {
        if (ComposerCard is null || state is null) return;
        if (TitleBox.IsFocused)
        {
            var dark = TopLevel.GetTopLevel(this)?.ActualThemeVariant == ThemeVariant.Dark;
            var tint = SceneTheme.For(state.SelectedMode).Tint(dark);
            ComposerCard.BorderBrush = new SolidColorBrush(Color.Parse(tint), 0.45);
            ComposerCard.BorderThickness = new Thickness(1);
        }
        else
        {
            ComposerCard.BorderBrush = Application.Current?.FindResource("Hairline") as IBrush;
            ComposerCard.BorderThickness = new Thickness(0.5);
        }
    }
}
