using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop.Views.Island;

/// <summary>
/// C5–C9: everything the cue island shows and the interaction state that decides which of its
/// presentations is in play. A verbatim port of <c>CueIslandView</c> (CueIsland.swift) onto the
/// Windows app state; the window (<see cref="CueIslandWindow"/>) owns the frame, the pointer
/// tracking and the morph, this model owns the phase grammar and its copy.
/// </summary>
public sealed class CueIslandViewModel : INotifyPropertyChanged
{
    /// <summary>The Mac demo-notch metrics: Windows draws the same notch outline from the top edge.</summary>
    public static IslandMetrics Metrics => IslandMetrics.Notch;

    /// <summary>Top bar height of the notch row (CueIslandView.bar).</summary>
    public double BarHeight => Metrics.BarHeight;

    /// <summary>Space the window keeps around the island for its soft drop shadow.</summary>
    public const double ShadowPadding = 20;
    public const double ShadowBottomPadding = 22;

    // ---- interaction state (mirrors CueIslandModel) ----------------------------------------

    private readonly AppState state;
    private readonly DispatcherTimer secondTimer;
    private readonly DispatcherTimer settleTimer;
    private bool attached;
    private string answerKey = "";
    private DateTimeOffset answerShownAt = DateTimeOffset.Now;
    private readonly HashSet<int> covered = [];

    public CueIslandViewModel(AppState state)
    {
        this.state = state;
        ForceAnswerCommand = state.ForceAnswerCommand;
        TogglePauseCommand = state.TogglePauseListeningCommand;
        EndMeetingCommand = state.StopRecordingCommand;
        KeepRecordingCommand = state.KeepRecordingCommand;
        ToggleRecordOnlyCommand = new RelayCommand(() => state.SetAnswerMode(!state.Settings.AutomaticAnswers));
        PreviousAnswerCommand = new RelayCommand(() => state.MoveAnswerHistory(-1));
        NextAnswerCommand = new RelayCommand(() => state.MoveAnswerHistory(1));
        ShorterCommand = new RelayCommand(() => _ = state.RefineAsync(AnswerRefinement.Shorter));
        ExampleCommand = new RelayCommand(() => _ = state.RefineAsync(AnswerRefinement.Example));
        SmallerFontCommand = new RelayCommand(() => StepFont(-1));
        LargerFontCommand = new RelayCommand(() => StepFont(1));
        OpenMainWindowCommand = new RelayCommand(() => state.ShowWorkbenchRequested?.Invoke());
        TogglePinCommand = new RelayCommand(TogglePin);
        ShowPreviousCueCommand = new RelayCommand(() =>
        {
            CollapsedKey = null;
            ForceCue = true;
            Refresh();
        });
        CollapseCommand = new RelayCommand(HideAnswer);
        EnableTranslationCommand = new RelayCommand(() => state.CueIslandEnableTranslation());
        StepSlideCommand = new RelayCommand<string>(step =>
        {
            if (int.TryParse(step, out var delta)) state.StepSlideFromIsland(delta);
        });

        secondTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        secondTimer.Tick += (_, _) =>
        {
            if (!attached) return;
            RefreshClock();
            if (Phase is CueIslandPhase.AutoEnd or CueIslandPhase.Deck or CueIslandPhase.Slide) Refresh();
        };
        settleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        settleTimer.Tick += (_, _) =>
        {
            if (!attached) return;
            RefreshCoverage();
            Settle();
        };

        state.PropertyChanged += OnStateChanged;
        state.Settings.PropertyChanged += OnSettingsChanged;
    }

    public void Attach()
    {
        if (attached) return;
        attached = true;
        secondTimer.Start();
        settleTimer.Start();
        Refresh();
    }

    public void Detach()
    {
        attached = false;
        secondTimer.Stop();
        settleTimer.Stop();
        state.PropertyChanged -= OnStateChanged;
        state.Settings.PropertyChanged -= OnSettingsChanged;
    }

    // ---- the Mac CueIslandModel ------------------------------------------------------------

    /// <summary>Pointer rests on the island: controls appear and clicks land (track(), 0.22 s).</summary>
    public bool Hovering
    {
        get => hovering;
        private set => Set(ref hovering, value);
    }
    private bool hovering;

    /// <summary>The deck stays open after a click on its bar (CueIslandModel.pinned).</summary>
    public bool Pinned
    {
        get => pinned;
        private set => Set(ref pinned, value);
    }
    private bool pinned;

    /// <summary>Kept open through the meeting (Mac alwaysOpen || presenting).</summary>
    public bool KeepsOpen => state.Settings.CueAlwaysOpen || state.PresenterLayoutActive;

    /// <summary>Outside a meeting the island is the notch's launcher entry point.</summary>
    public bool LauncherEnabled => true;

    /// <summary>Screenshots and the self-check draw on the Mac desk colour.</summary>
    public bool Backdrop
    {
        get => backdrop;
        set => Set(ref backdrop, value);
    }
    private bool backdrop;

    /// <summary>The answer the user (or time) put away; a new answer opens the island again.</summary>
    public string? CollapsedKey
    {
        get => collapsedKey;
        set
        {
            if (!Set(ref collapsedKey, value)) return;
            Refresh();
        }
    }
    private string? collapsedKey;

    /// <summary>The user asked for the last cue although the answer was put away.</summary>
    public bool ForceCue
    {
        get => forceCue;
        set => Set(ref forceCue, value);
    }
    private bool forceCue;

    /// <summary>Force a phase for the self-check/screenshots (null: the real phase grammar).</summary>
    public CueIslandPhase? ForcedPhase
    {
        get => forcedPhase;
        set
        {
            if (!Set(ref forcedPhase, value)) return;
            Refresh();
        }
    }
    private CueIslandPhase? forcedPhase;

    /// <summary>The auto-end reminder's countdown for the scripted capture (Mac demoAutoEnd).</summary>
    public int? DemoAutoEndSeconds
    {
        get => demoAutoEndSeconds;
        set
        {
            if (!Set(ref demoAutoEndSeconds, value)) return;
            Refresh();
        }
    }
    private int? demoAutoEndSeconds;

    /// <summary>The clock the scripted capture shows ("12:36" in the Mac screenshots).</summary>
    public string? DemoClock
    {
        get => demoClock;
        set
        {
            if (!Set(ref demoClock, value)) return;
            RefreshClock();
        }
    }
    private string? demoClock;

    /// <summary>A pinned deck is clicked on its bar, or put away by a click outside.</summary>
    public void TogglePinned()
    {
        if (Phase is CueIslandPhase.Compact or CueIslandPhase.Deck or CueIslandPhase.Hearing
            or CueIslandPhase.Launcher)
            Pinned = !Pinned;
        else if (Phase is CueIslandPhase.Cue or CueIslandPhase.Slide or CueIslandPhase.Listening)
        {
            if (KeepsOpen && state.CueIslandRunning) Pinned = !Pinned;
        }
        Refresh();
    }

    /// <summary>The bar was clicked: pin (or unpin) the deck.</summary>
    public void BarClicked() => TogglePinned();

    /// <summary>Hides the deck again (a click outside the island, and the self-check).</summary>
    public void ClearPinned()
    {
        if (!pinned) return;
        Pinned = false;
        Refresh();
    }

    public void SetHovering(bool value)
    {
        if (Hovering == value) return;
        Hovering = value;
        Refresh();
    }

    /// <summary>The pointer left the whole window long enough: puts away a pinned deck (track()).</summary>
    public void UnpinFromOutside()
    {
        if (!pinned) return;
        Pinned = false;
        Refresh();
    }

    public void Pin()
    {
        Pinned = true;
        Refresh();
    }

    /// <summary>The Mac hover bump: the island leans towards the pointer before it opens.</summary>
    public bool HoverBump => Phase == CueIslandPhase.Compact && hovering && !Motion.Reduce;

    private void TogglePin()
    {
        state.Settings.CueAlwaysOpen = !state.Settings.CueAlwaysOpen;
        try { _ = state.Settings.SaveAsync(); } catch { }
        Refresh();
    }

    private void HideAnswer()
    {
        CollapsedKey = AnswerKey;
        ForceCue = false;
    }

    private void StepFont(int delta)
    {
        state.Settings.TeleprompterFontSize = Math.Clamp(state.Settings.TeleprompterFontSize + delta, 14, 36);
        try { _ = state.Settings.SaveAsync(); } catch { }
        Refresh();
    }

    /// <summary>Starts a meeting from a launcher chip (Mac startFromIsland(scene:)).</summary>
    public void StartScene(string modeKey)
    {
        if (!Enum.TryParse<MeetingMode>(modeKey, out var mode)) return;
        state.StartFromCueIsland(mode);
    }

    // ---- the phase grammar (CueIslandView.phase) -------------------------------------------

    public CueIslandPhase Phase
    {
        get => phase;
        private set => Set(ref phase, value);
    }
    private CueIslandPhase phase = CueIslandPhase.Hidden;

    private string AnswerKey => state.CueIslandDisplayedAnswer.Length
                                + "|" + (state.AnswerHistoryCursor?.ToString() ?? "-")
                                + "|" + state.CueIslandDisplayedQuestion.Length;

    private bool Peeking => state.CueIslandRunning && state.CueIslandAutoEndDue;

    private CueIslandPhase ComputePhase()
    {
        if (ForcedPhase is { } forced) return forced;
        // Nobody is talking: the meeting ends soon unless the user says otherwise.
        if (state.CueIslandRunning && (Peeking || DemoAutoEndSeconds is not null)) return CueIslandPhase.AutoEnd;
        var hasAnswer = state.CueIslandHasAnswer;
        if (state.CueIslandRunning && KeepsOpen)
        {
            if (Pinned) return CueIslandPhase.Deck;
            // The other side has the floor: what they are saying comes before the script. An
            // answer on show gives way only to a real sentence, and only while it is being said.
            var theirs = state.CueIslandSystemPartial.Trim().Length;
            if (hasAnswer && CollapsedKey != AnswerKey)
            {
                // Pinned open nothing gives way: what they are saying goes above the answer.
                return (theirs >= 40 || state.CueIslandUnansweredTurn) && !state.CueIslandAnswering
                       && !state.Settings.CueAlwaysOpen
                    ? CueIslandPhase.Listening
                    : CueIslandPhase.Cue;
            }
            if (theirs >= 12
                || (state.CueIslandTheirFloor && state.CueIslandMicrophonePartial.Trim().Length == 0))
                return CueIslandPhase.Listening;
            var script = state.CueIslandPresenting && state.CueIslandPresenterShowsSlideNotes;
            if (script && state.CueIslandHasSlide) return CueIslandPhase.Slide;
            if (state.CueIslandHasCandidate) return CueIslandPhase.Hearing;
            // Presenting from PowerPoint's own notes: nothing to show until someone asks.
            if (state.CueIslandPresenting && !script && !hasAnswer && !state.Settings.CueAlwaysOpen)
                return CueIslandPhase.Compact;
            return CueIslandPhase.Cue;
        }
        // With the answer put away (or said), a new answer opens the island again.
        if (hasAnswer && CollapsedKey != AnswerKey) return CueIslandPhase.Cue;
        if (state.CueIslandRunning)
        {
            if (hovering || pinned) return CueIslandPhase.Deck;
            if (state.CueIslandHasCandidate) return CueIslandPhase.Hearing;
            return CueIslandPhase.Compact;
        }
        return (hovering || pinned) && LauncherEnabled && Metrics.HasNotch ? CueIslandPhase.Launcher : CueIslandPhase.Hidden;
    }

    // ---- layout ----------------------------------------------------------------------------

    /// <summary>Closed outlines are the notch's; the open ones relax (C9).</summary>
    public bool Idle => Phase is CueIslandPhase.Compact or CueIslandPhase.Hidden;

    public double TargetContentWidth => IslandLayout.ContentWidth(Phase, Metrics, state.CueIslandWidthChoice,
        coachEar: ShowCoach);

    public double Flare => IslandLayout.Flare(Idle, Metrics);

    public double CornerRadiusValue => IslandLayout.Radius(Idle, Metrics);

    public double FrameWidth => IslandLayout.FrameWidth(TargetContentWidth, Idle, Metrics);

    /// <summary>C5/C9: the scene-pigment edge along the lower border (2 pt while the coach calls).</summary>
    public IBrush EdgeBrush
    {
        get => edgeBrush;
        private set => Set(ref edgeBrush, value);
    }
    private IBrush edgeBrush = Brushes.Transparent;

    public double EdgeWidth => ShowCoach ? IslandLayout.CautionEdgeWidth : IslandLayout.EdgeWidth;

    // ---- colours ---------------------------------------------------------------------------

    public Color TintColor => Color.Parse(SceneTheme.For(state.SelectedMode).TintDark);

    public IBrush TintBrush => new SolidColorBrush(TintColor);

    public IBrush TintSoftBrush => new SolidColorBrush(TintColor, 0.18);

    public IBrush CautionBrush => new SolidColorBrush(Color.Parse("#F0B24A"));

    public IBrush SealBrush => new SolidColorBrush(Color.Parse("#F07055"));

    public IBrush BackdropBrush => new SolidColorBrush(Color.Parse("#E4E1DA"));

    // ---- top bar (CueIslandView.bar) -------------------------------------------------------

    public bool ShowPulse => Phase is not (CueIslandPhase.Launcher or CueIslandPhase.Hidden);

    public IBrush StatusDotBrush => state.CueIslandRunning && !state.CueIslandPaused
        ? TintBrush
        : new SolidColorBrush(Colors.White, 0.35);

    public bool WaveActive => state.CueIslandRunning && !state.CueIslandPaused;

    public double WaveLevel => Math.Max(state.CueIslandSystemLevel, state.CueIslandMicrophoneLevel);

    public IBrush WaveTint => new SolidColorBrush(Colors.White, 0.85);

    public string ClockText
    {
        get => clockText;
        private set => Set(ref clockText, value);
    }
    private string clockText = "";

    public bool ShowClock => clockText.Length > 0 && !ShowCoach;

    /// <summary>The slide page "4/12" on the ear while presenting (CueIslandView.coachEar).</summary>
    public string SlidePageLabel
    {
        get => slidePageLabel;
        private set => Set(ref slidePageLabel, value);
    }
    private string slidePageLabel = "";

    public bool ShowSlidePage => slidePageLabel.Length > 0;

    public string CoachText
    {
        get => coachText;
        private set => Set(ref coachText, value);
    }
    private string coachText = "";

    public bool ShowCoach => coachText.Length > 0;

    /// <summary>The coach ear's symbol (Fluent glyph; CueIslandView.earLabel symbol).</summary>
    public string CoachIcon
    {
        get => coachIcon;
        private set => Set(ref coachIcon, value);
    }
    private string coachIcon = "\uEFC8";

    /// <summary>Mac SF Symbol name → the bundled Fluent glyph (Styles/Icons.axaml mapping).</summary>
    private static string CoachGlyph(string symbol) => symbol switch
    {
        "moon.zzz" => "\uF87E",
        "bubble.left" => "\uF308",
        "waveform" => "\uE9DD",
        _ => "\uEFC8"
    };

    public IBrush CoachBrush => CautionBrush;

    // ---- 对方在说: hearing (live activity) and listening (their turn) ------------------------

    public bool ShowHearing
    {
        get => showHearing;
        private set => Set(ref showHearing, value);
    }
    private bool showHearing;

    public string HearingText
    {
        get => hearingText;
        private set => Set(ref hearingText, value);
    }
    private string hearingText = "";

    public string HearingTranslation
    {
        get => hearingTranslation;
        private set => Set(ref hearingTranslation, value);
    }
    private string hearingTranslation = "";

    public bool ShowHearingTranslation => hearingTranslation.Length > 0;

    public bool ShowListening
    {
        get => showListening;
        private set => Set(ref showListening, value);
    }
    private bool showListening;

    public string ListeningText
    {
        get => listeningText;
        private set => Set(ref listeningText, value);
    }
    private string listeningText = "";

    public string ListeningTranslation
    {
        get => listeningTranslation;
        private set => Set(ref listeningTranslation, value);
    }
    private string listeningTranslation = "";
    private bool hasListeningTranslation;

    public bool ShowListeningTranslation => hasListeningTranslation;

    /// <summary>With interpretation off the listening panel offers "开启同声传译".</summary>
    public bool ShowEnableTranslation => !state.CueIslandLiveTranslation;

    // ---- cue ------------------------------------------------------------------------------

    public bool ShowCue
    {
        get => showCue;
        private set => Set(ref showCue, value);
    }
    private bool showCue;

    public string CueQuestion
    {
        get => cueQuestion;
        private set => Set(ref cueQuestion, value);
    }
    private string cueQuestion = "";

    public bool ShowCueQuestion => cueQuestion.Length > 0;

    /// <summary>What was asked, in my own language (the 问： line of a bilingual answer).</summary>
    public string CueAsked
    {
        get => cueAsked;
        private set => Set(ref cueAsked, value);
    }
    private string cueAsked = "";

    public bool ShowCueAsked => cueAsked.Length > 0;

    public IReadOnlyList<CueRowItem> CueRows
    {
        get => cueRows;
        private set => Set(ref cueRows, value);
    }
    private IReadOnlyList<CueRowItem> cueRows = [];

    public string CueEmptyText
    {
        get => cueEmptyText;
        private set => Set(ref cueEmptyText, value);
    }
    private string cueEmptyText = "";

    public bool ShowCueEmpty => cueEmptyText.Length > 0;

    public bool ShowThinking
    {
        get => showThinking;
        private set => Set(ref showThinking, value);
    }
    private bool showThinking;

    /// <summary>The strip above an answer while the other side starts a new turn (Mac heard).</summary>
    public bool ShowHeardStrip
    {
        get => showHeardStrip;
        private set => Set(ref showHeardStrip, value);
    }
    private bool showHeardStrip;

    public string HeardStripText
    {
        get => heardStripText;
        private set => Set(ref heardStripText, value);
    }
    private string heardStripText = "";

    public string HeardStripTranslation
    {
        get => heardStripTranslation;
        private set => Set(ref heardStripTranslation, value);
    }
    private string heardStripTranslation = "";

    public bool ShowHeardStripTranslation => heardStripTranslation.Length > 0;

    // ---- slide notes ----------------------------------------------------------------------

    public bool ShowSlide
    {
        get => showSlide;
        private set => Set(ref showSlide, value);
    }
    private bool showSlide;

    public string SlideHeader
    {
        get => slideHeader;
        private set => Set(ref slideHeader, value);
    }
    private string slideHeader = "";

    public bool ShowSlideSteps => hovering || KeepsOpen;

    public bool SlidePrevEnabled => state.CueIslandSlidePrevEnabled;

    public bool SlideNextEnabled => state.CueIslandSlideNextEnabled;

    public IReadOnlyList<CueRowItem> SlideRows
    {
        get => slideRows;
        private set => Set(ref slideRows, value);
    }
    private IReadOnlyList<CueRowItem> slideRows = [];

    public string SlideTransition
    {
        get => slideTransition;
        private set => Set(ref slideTransition, value);
    }
    private string slideTransition = "";

    public bool ShowSlideTransition => slideTransition.Length > 0;

    public string SlideEmptyText
    {
        get => slideEmptyText;
        private set => Set(ref slideEmptyText, value);
    }
    private string slideEmptyText = "正在准备这一页的要点…";

    public bool ShowSlideEmpty => slideRows.Count == 0;

    // ---- deck -----------------------------------------------------------------------------

    public bool ShowDeck
    {
        get => showDeck;
        private set => Set(ref showDeck, value);
    }
    private bool showDeck;

    public string DeckTitle
    {
        get => deckTitle;
        private set => Set(ref deckTitle, value);
    }
    private string deckTitle = "";

    public string DeckSceneTag
    {
        get => deckSceneTag;
        private set => Set(ref deckSceneTag, value);
    }
    private string deckSceneTag = "";

    public string DeckModeText
    {
        get => deckModeText;
        private set => Set(ref deckModeText, value);
    }
    private string deckModeText = "";

    public bool ShowDeckWarning
    {
        get => showDeckWarning;
        private set => Set(ref showDeckWarning, value);
    }
    private bool showDeckWarning;

    public bool ShowDeckHeard
    {
        get => showDeckHeard;
        private set => Set(ref showDeckHeard, value);
    }
    private bool showDeckHeard;

    public string DeckHeardSpeaker
    {
        get => deckHeardSpeaker;
        private set => Set(ref deckHeardSpeaker, value);
    }
    private string deckHeardSpeaker = "";

    public string DeckHeardText
    {
        get => deckHeardText;
        private set => Set(ref deckHeardText, value);
    }
    private string deckHeardText = "";

    public string DeckHeardTranslation
    {
        get => deckHeardTranslation;
        private set => Set(ref deckHeardTranslation, value);
    }
    private string deckHeardTranslation = "";

    public bool ShowDeckHeardTranslation => deckHeardTranslation.Length > 0;

    public string DeckStats
    {
        get => deckStats;
        private set => Set(ref deckStats, value);
    }
    private string deckStats = "";

    public bool ShowDeckStats => deckStats.Length > 0;

    /// <summary>After an answer was put away the deck offers the last cue again (Mac 上一条).</summary>
    public bool ShowDeckPrevious
    {
        get => showDeckPrevious;
        private set => Set(ref showDeckPrevious, value);
    }
    private bool showDeckPrevious;

    // ---- launcher -------------------------------------------------------------------------

    public bool ShowLauncher
    {
        get => showLauncher;
        private set => Set(ref showLauncher, value);
    }
    private bool showLauncher;

    public IReadOnlyList<LauncherSceneItem> LauncherScenes
    {
        get => launcherScenes;
        private set => Set(ref launcherScenes, value);
    }
    private IReadOnlyList<LauncherSceneItem> launcherScenes = [];

    public bool LauncherBusy => state.Working || state.Stopping;

    // ---- font sizes (CueIslandView reads state.teleprompterFontSize) ------------------------

    /// <summary>Their words while listening: max(13, size × 0.62).</summary>
    public double ListeningTextSize => Math.Max(13, state.CueIslandFontSize * 0.62);

    /// <summary>The interpretation under their words: size × 0.8.</summary>
    public double ListeningTranslationSize => state.CueIslandFontSize * 0.8;

    /// <summary>The heard strip above an answer: max(13, size × 0.62).</summary>
    public double HeardStripTextSize => Math.Max(13, state.CueIslandFontSize * 0.62);

    public double HeardStripTranslationSize => state.CueIslandFontSize * 0.72;

    /// <summary>What was asked, in my own language: max(13, size × 0.62).</summary>
    public double AskedTextSize => Math.Max(13, state.CueIslandFontSize * 0.62);

    public double EmptyTextSize => state.CueIslandFontSize * 0.8;

    public double SlideTransitionSize => state.CueIslandFontSize * 0.66;

    // ---- deck button labels (CueIslandView.deck) -------------------------------------------

    public string DeckModeActionLabel => state.CueIslandAutomaticAnswers
        ? UiText.T("改为只记录")
        : UiText.T("改为自动回答");

    public string DeckPauseLabel => state.CueIslandPaused ? UiText.T("继续") : UiText.T("暂停");

    /// <summary>Fluent pause/play glyph (pause_24_regular / play_24_regular of the bundled font).</summary>
    public string DeckPauseIcon => state.CueIslandPaused ? "\uE991" : "\uE8CD";

    /// <summary>Hidden phases draw nothing (CueIslandView: phase == .hidden has no island).</summary>
    public bool ShowIsland => Phase != CueIslandPhase.Hidden || backdrop;

    // ---- auto-end reminder -----------------------------------------------------------------

    public bool ShowAutoEnd
    {
        get => showAutoEnd;
        private set => Set(ref showAutoEnd, value);
    }
    private bool showAutoEnd;

    public string AutoEndText
    {
        get => autoEndText;
        private set => Set(ref autoEndText, value);
    }
    private string autoEndText = "";

    public bool ShowAutoEndActions => !(state.Working || state.Stopping);

    // ---- hover controls (CueIslandView.controls) -------------------------------------------

    public bool ShowControls => hovering && Phase is CueIslandPhase.Cue or CueIslandPhase.Slide
        or CueIslandPhase.Listening;

    public string HistoryPosition => state.HistoryPositionText;

    public bool ShowHistoryControls => state.HistoryPositionText.Length > 0;

    public bool ShowCollapse => !KeepsOpen;

    // ---- commands -------------------------------------------------------------------------

    public ICommand ForceAnswerCommand { get; }
    public ICommand TogglePauseCommand { get; }
    public ICommand EndMeetingCommand { get; }
    public ICommand KeepRecordingCommand { get; }
    public ICommand ToggleRecordOnlyCommand { get; }
    public ICommand PreviousAnswerCommand { get; }
    public ICommand NextAnswerCommand { get; }
    public ICommand ShorterCommand { get; }
    public ICommand ExampleCommand { get; }
    public ICommand SmallerFontCommand { get; }
    public ICommand LargerFontCommand { get; }
    public ICommand OpenMainWindowCommand { get; }
    public ICommand TogglePinCommand { get; }
    public ICommand ShowPreviousCueCommand { get; }
    public ICommand CollapseCommand { get; }
    public ICommand EnableTranslationCommand { get; }
    public ICommand StepSlideCommand { get; }

    // ---- refresh ---------------------------------------------------------------------------

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!attached) return;
        if (e.PropertyName is not (null or nameof(AppState.Recording) or nameof(AppState.AnswerText)
            or nameof(AppState.DetectedQuestion) or nameof(AppState.SystemPartial) or nameof(AppState.MicrophonePartial)
            or nameof(AppState.Answering) or nameof(AppState.ListeningPaused) or nameof(AppState.IslandTranslation)
            or nameof(AppState.SystemPartialTranslation) or nameof(AppState.LiveTranslationEnabled)
            or nameof(AppState.PresenterLayoutActive) or nameof(AppState.CandidateQuestion)
            or nameof(AppState.CoachHint) or nameof(AppState.HistoryPositionText) or nameof(AppState.AnswerHistoryCursor)
            or nameof(AppState.AutoEndNotice) or nameof(AppState.SlidePointRows) or nameof(AppState.CueIslandSlidePage)
            or nameof(AppState.SelectedMode) or nameof(AppState.PlainAnswer) or nameof(AppState.DisplayedAnswerText)
            or nameof(AppState.DisplayedQuestionText)))
            return;
        Refresh();
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!attached) return;
        if (e.PropertyName is not (null or nameof(SettingsViewModel.CueAlwaysOpen)
            or nameof(SettingsViewModel.SelectedCueWidth) or nameof(SettingsViewModel.TeleprompterFontSize)
            or nameof(SettingsViewModel.AutomaticAnswers) or nameof(SettingsViewModel.PresenterSlideNotes)
            or nameof(SettingsViewModel.InsightFollowSlides)))
            return;
        Refresh();
    }

    private void RefreshClock()
    {
        var clock = DemoClock
                    ?? (Phase is CueIslandPhase.Launcher or CueIslandPhase.Hidden ? "" : state.CueIslandClockText);
        if (ClockText != clock) ClockText = clock;
        if (ShowClock != (clock.Length > 0 && !ShowCoach)) OnPropertyChanged(nameof(ShowClock));
    }

    /// <summary>Recomputes every binding of the island; cheap enough (a few dozen fields).</summary>
    public void Refresh()
    {
        var key = AnswerKey;
        if (key != answerKey)
        {
            answerKey = key;
            answerShownAt = DateTimeOffset.Now;
            covered.Clear();
            ForceCue = false;
        }

        var newPhase = ComputePhase();
        var phaseChanged = newPhase != Phase;
        if (phaseChanged) Phase = newPhase;

        var autoEndSeconds = DemoAutoEndSeconds ?? state.CueIslandAutoEndSeconds;
        var coach = state.CueIslandCoachEnabled ? state.CueIslandCoachHint : "";
        CoachText = coach;
        CoachIcon = CoachGlyph(state.CueIslandCoachSymbol);

        var slidePage = state.CueIslandPresenting && state.CueIslandHasSlide
            ? state.CueIslandSlidePage + "/" + state.CueIslandSlidePageCount
            : "";
        SlidePageLabel = slidePage;

        var showingAnswer = state.CueIslandHasAnswer;
        var cueLines = state.CueIslandCueLines();
        var theirTurn = state.CueIslandTheirTurn;
        var heard = state.CueIslandLastHeard;

        ShowHearing = newPhase == CueIslandPhase.Hearing && state.CueIslandHasCandidate;
        HearingText = ShowHearing ? state.CueIslandCandidateText : "";
        HearingTranslation = ShowHearing && state.CueIslandLiveTranslation ? state.CueIslandPartialTranslation : "";

        ShowListening = newPhase == CueIslandPhase.Listening;
        ListeningText = theirTurn.Text;
        ListeningTranslation = theirTurn.Translation;
        hasListeningTranslation = theirTurn.Translation.Length > 0;

        ShowCue = newPhase == CueIslandPhase.Cue;
        CueQuestion = ShowCue ? state.CueIslandQuestion : "";
        CueAsked = ShowCue ? state.CueIslandAsked : "";
        ShowThinking = state.CueIslandAnswering;
        CueRows = ShowCue ? BuildCueRows(cueLines) : [];
        CueEmptyText = !ShowCue ? "" : cueLines.Count > 0 ? "" : state.CueIslandAnswering
            ? UiText.T("正在想…")
            : state.CueIslandPresenting && state.CueIslandPresenterShowsSlideNotes
                ? UiText.T("被问到时，回答会出现在这里；翻到已放进资料的幻灯片时，这里显示这一页的要点。")
                : UiText.T("被问到时，回答会出现在这里。");
        var theirs = state.CueIslandSystemPartial.Trim().Length;
        ShowHeardStrip = ShowCue && state.Settings.CueAlwaysOpen && state.CueIslandRunning
                         && !state.CueIslandAnswering && cueLines.Count > 0
                         && (theirs >= 12 || state.CueIslandUnansweredTurn);
        HeardStripText = ShowHeardStrip ? theirTurn.Text : "";
        HeardStripTranslation = ShowHeardStrip ? theirTurn.Translation : "";

        ShowSlide = newPhase == CueIslandPhase.Slide;
        SlideHeader = ShowSlide ? state.CueIslandSlideHeader : "";
        var slidePoints = ShowSlide ? state.CueIslandSlidePoints : [];
        SlideRows = ShowSlide
            ? slidePoints.Select((point, index) => new CueRowItem
            {
                Kind = CueLine.Kind.Point,
                Rest = point.Text,
                Said = point.Done,
                Current = point.Next,
                Expanded = true,
                TextSize = (double)state.CueIslandFontSize,
                MarkerBrush = TintBrush
            }).ToList()
            : [];
        SlideTransition = ShowSlide ? state.CueIslandSlideTransition : "";
        SlideEmptyText = UiText.T("正在准备这一页的要点…");

        ShowDeck = newPhase == CueIslandPhase.Deck;
        DeckTitle = ShowDeck ? state.CueIslandSeriesName : "";
        DeckSceneTag = ShowDeck ? state.CueIslandSceneName : "";
        DeckModeText = ShowDeck ? state.CueIslandModeText : "";
        ShowDeckWarning = ShowDeck && state.CueIslandModeWarning;
        ShowDeckHeard = ShowDeck && heard is not null;
        DeckHeardSpeaker = heard is { } heardLine
            ? heardLine.IsMe ? UiText.T("我") : UiText.T("对方")
            : "";
        DeckHeardText = heard?.Text ?? "";
        DeckHeardTranslation = heard?.Translation ?? "";
        DeckStats = ShowDeck ? state.CueIslandCoachStats : "";
        ShowDeckPrevious = ShowDeck && (collapsedKey is not null) && state.CueIslandDisplayedAnswer.Length > 0;

        ShowLauncher = newPhase == CueIslandPhase.Launcher;
        if (ShowLauncher && launcherScenes.Count == 0)
            LauncherScenes = BuildLauncher();

        ShowAutoEnd = newPhase == CueIslandPhase.AutoEnd;
        AutoEndText = ShowAutoEnd
            ? DemoAutoEndSeconds is { } demo ? UiText.Format("好像没人说话了，%lld 秒后结束记录", demo) : state.CueIslandAutoEndNotice
            : "";

        // C9: the scene pigment runs along the lower edge; caution overrides it.
        var edgeColor = ShowCoach ? Color.Parse("#F0B24A") : TintColor;
        EdgeBrush = new LinearGradientBrush
        {
            StartPoint = new Avalonia.RelativePoint(0.5, 0.35, Avalonia.RelativeUnit.Relative),
            EndPoint = new Avalonia.RelativePoint(0.5, 1, Avalonia.RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0, edgeColor.R, edgeColor.G, edgeColor.B), 0),
                new GradientStop(Color.FromArgb(217, edgeColor.R, edgeColor.G, edgeColor.B), 1)
            }
        };

        RefreshClock();
        foreach (var name in new[]
                 {
                     nameof(Idle), nameof(TargetContentWidth), nameof(Flare), nameof(CornerRadiusValue),
                     nameof(FrameWidth), nameof(EdgeWidth), nameof(TintBrush), nameof(TintSoftBrush),
                     nameof(TintColor), nameof(ShowPulse), nameof(StatusDotBrush), nameof(WaveActive),
                     nameof(WaveLevel), nameof(ShowSlidePage), nameof(ShowCoach), nameof(CoachBrush),
                     nameof(ShowHearingTranslation), nameof(ShowListeningTranslation), nameof(ShowEnableTranslation),
                     nameof(ShowCueQuestion), nameof(ShowCueAsked), nameof(ShowCueEmpty), nameof(ShowThinking),
                     nameof(ShowHeardStrip), nameof(ShowHeardStripTranslation), nameof(ShowSlideSteps),
                     nameof(SlidePrevEnabled), nameof(SlideNextEnabled), nameof(ShowSlideTransition),
                     nameof(ShowSlideEmpty), nameof(ShowDeckWarning), nameof(ShowDeckHeard),
                     nameof(ShowDeckHeardTranslation), nameof(ShowDeckStats), nameof(ShowDeckPrevious),
                     nameof(ShowAutoEndActions), nameof(ShowControls), nameof(ShowHistoryControls),
                     nameof(ShowCollapse), nameof(HistoryPosition), nameof(KeepsOpen), nameof(ShowClock),
                     nameof(HoverBump), nameof(LauncherBusy), nameof(ShowAutoEndActions), nameof(ShowIsland),
                     nameof(ListeningTextSize), nameof(ListeningTranslationSize), nameof(HeardStripTextSize),
                     nameof(HeardStripTranslationSize), nameof(AskedTextSize), nameof(EmptyTextSize),
                     nameof(SlideTransitionSize), nameof(DeckModeActionLabel), nameof(DeckPauseLabel), nameof(DeckPauseIcon)
                 })
            OnPropertyChanged(name);
    }

    private IReadOnlyList<CueRowItem> BuildCueRows(IReadOnlyList<CueLine> lines)
    {
        if (lines.Count == 0) return [];
        var size = (double)state.CueIslandFontSize;
        var style = state.CueIslandCueStyle;
        var glossed = lines.Any(line => line.Gloss.Length > 0);
        var raw = lines;
        var visible = Window(raw, out var next);
        var expanded = hovering || KeepsOpen || style == CueStyle.Script || glossed;
        return visible.Select(line => new CueRowItem
        {
            Kind = line.Type,
            Head = line.Head,
            Rest = line.Text,
            Gloss = line.Gloss,
            Said = covered.Contains(line.Id),
            Current = line.Id == next && !state.CueIslandAnswering,
            Expanded = expanded,
            TextSize = size,
            MarkerBrush = TintBrush
        }).ToList();
    }

    /// <summary>
    /// The lines around the next one to say (CueIslandView.window): what was just said stays for a
    /// beat above it, what follows waits below.
    /// </summary>
    private IReadOnlyList<CueLine> Window(IReadOnlyList<CueLine> lines, out int? next)
    {
        next = lines.FirstOrDefault(line => !covered.Contains(line.Id))?.Id;
        if (lines.Count == 0) return [];
        var glossed = lines.Any(line => line.Gloss.Length > 0);
        var limit = glossed
            ? state.Settings.CueAlwaysOpen ? (ShowHeardStrip ? 4 : 5) : 3
            : state.CueIslandCueStyle == CueStyle.Script ? 4 : 6;
        if (lines.Count <= limit) return lines;
        var focus = next is { } id
            ? lines.ToList().FindIndex(line => line.Id == id)
            : lines.Count - 1;
        if (focus < 0) focus = lines.Count - 1;
        var start = Math.Min(Math.Max(0, focus - 1), lines.Count - limit);
        return lines.Skip(start).Take(limit).ToList();
    }

    private IReadOnlyList<LauncherSceneItem> BuildLauncher()
    {
        var items = new List<LauncherSceneItem>();
        foreach (var choice in state.Scenes)
            items.Add(new LauncherSceneItem
            {
                Name = choice.Title,
                ModeKey = choice.Mode.ToString(),
                Tint = new SolidColorBrush(Color.Parse(SceneTheme.For(choice.Mode).TintDark))
            });
        return items;
    }

    /// <summary>Refresh the ticks of the lines already said (Mac refreshCoverage).</summary>
    private void RefreshCoverage()
    {
        if (!state.CueIslandRunning || state.CueIslandAnswering || state.AnswerHistoryCursor is not null) return;
        var speech = string.Join(" ", state.CueIslandMySpeechSince(answerShownAt.AddSeconds(-2)).Select(line => line.Text));
        if (speech.Length < 4) return;
        var lines = state.CueIslandCueLines();
        var said = lines.Where(line => line.Spoken.Length >= 5 && AnswerPoints.HeuristicCovered(line.Spoken, speech))
            .Select(line => line.Id);
        var changed = false;
        foreach (var id in said)
            if (covered.Add(id)) changed = true;
        if (changed) Refresh();
    }

    /// <summary>Puts a finished answer away once it has been said, or when it has gone stale.</summary>
    private void Settle()
    {
        if (state.CueIslandAnswering || state.CueIslandDisplayedAnswer.Length == 0 || backdrop) return;
        var key = answerKey;
        if (key != AnswerKey) return;
        var all = state.CueIslandCueLines();
        var finished = all.Count > 0 && all.All(line => covered.Contains(line.Id));
        var age = (DateTimeOffset.Now - answerShownAt).TotalSeconds;
        if (!hovering && ((finished && age > 6) || age > 180))
        {
            CollapsedKey = key;
        }
    }

    // ---- INotifyPropertyChanged ------------------------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
