using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;
using QRCoder;
using NextThought.Platform;

namespace NextThought.Desktop;

public enum AppSection { Home, Prepare, Live, Notes, History, Series, Library, Settings }

/// <summary>
/// Application state and meeting orchestration. Ported from the WPF window
/// (MainWindow.xaml.cs) so behaviour matches the Mac reference: capture, streaming
/// recognition, question tracking, answering, summary and archival stay the same,
/// only the presentation layer changes.
/// </summary>
public partial class AppState : ObservableObject
{
    private readonly List<Task> writes = [];
    private readonly List<TranscriptLine> currentLines = [];
    private readonly DispatcherTimer candidateTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private QuestionCandidateTracker tracker = new();
    private AutomaticAnswerScheduler automaticScheduler = new();
    private bool lastRequestAutomatic;
    private DateTimeOffset lastMicrophoneSpeech;
    private AudioCaptureService? capture;
    private MeetingEndWatch? autoEnd;
    private SpeakingCoach? coach;
    private CoachSummaryAccumulator? coachSummary;
    private int coachTicks;
    private CoachNudge? lastNudge;
    private DateTimeOffset lastNudgeAt;
    private DispatcherTimer? autoEndTimer;
    private bool sawSpeech;
    private DateTimeOffset? lastSpeechEnd;
    private LiveSpeechEngine? engine;
    private CloudSttSession? cloudStt;
    private MeetingVoiceTagger? voiceTagger;
    private MeetingTranslator? translator;
    // 逐字同传：每一路只保留最新一版译文，过期结果不覆盖当前句子（LiveTranslationCoordinator）。
    private LiveTranslationCoordinator? remoteLiveTranslation;
    private LiveTranslationCoordinator? microphoneLiveTranslation;
    private FileStream? sessionLease;
    private CancellationTokenSource? workCancellation, requestCancellation;
    private Task? workTask;
    private bool closing, allowClose, initialized;
    private long epoch, requestEpoch, selectionEpoch;
    private double systemLevel, microphoneLevel;
    private string? startupError;

    public AppState()
    {
        Paths = new AppPaths();
        SettingsStore = new SettingsStore(Paths);
        LoadShortcutOverrides();
        Sessions = new SessionStore(Paths);
        Models = new ModelStore(Paths);
        Retrieval = new RetrievalService(Paths);
        Settings = new SettingsViewModel(this);
        Library = new LibraryViewModel(this);
        try { ContextSidebarHidden = SettingsStore.Load().ContextSidebarHidden; } catch { }
        LoadSceneSettings();
        candidateTimer.Tick += (_, _) => OnCandidateTimer();
        History = [];
        Transcript = [];
        Sources = [];
        LanguageChanged += english =>
        {
            UiText.EnglishUi = english;
            PageSubtitle = UiText.Translate(PageSubtitle, english);
            OnPropertyChanged(nameof(StatsText));
            OnPropertyChanged(nameof(Greeting));
            OnPropertyChanged(nameof(DateLine));
            OnPropertyChanged(nameof(GreetingHeadline));
            OnPropertyChanged(nameof(HistorySummary));
            OnPropertyChanged(nameof(SeriesSummary));
            OnPropertyChanged(nameof(SelectedSeriesMeta));
            OnPropertyChanged(nameof(SeriesLessonsHeader));
            OnPropertyChanged(nameof(SeriesCandidatesHeader));
            OnPropertyChanged(nameof(SeriesBankHeader));
            OnPropertyChanged(nameof(SlideStatus));
            OnPropertyChanged(nameof(CompanionToggleLabel));
            Library.NotifyLanguageChanged();
            RefreshHistory();
            RefreshSeries();
        };
    }

    public AppPaths Paths { get; }
    public SettingsStore SettingsStore { get; }
    public SessionStore Sessions { get; }
    public ModelStore Models { get; }
    public RetrievalService Retrieval { get; }
    public ChatCompletionClient Api { get; } = new();
    public SettingsViewModel Settings { get; }
    public LibraryViewModel Library { get; }

    /// <summary>Set by the shell: save-file picker, open-file picker, clipboard writer, dialog host.</summary>
    public Func<string, string, Task<string?>>? PickSaveFile { get; set; }
    public Func<string, Task<string?>>? PickOpenFile { get; set; }
    public Func<string, Task>? CopyText { get; set; }
    public Func<string, string, Task>? Notify { get; set; }

    [ObservableProperty] private AppSection section = AppSection.Home;
    [ObservableProperty] private string versionText = "WINDOWS  " + typeof(AppState).Assembly.GetName().Version!.ToString(3);
    [ObservableProperty] private bool working;
    [ObservableProperty] private bool recording;
    [ObservableProperty] private bool listeningPaused;
    [ObservableProperty] private bool stopping;
    [ObservableProperty] private bool answering;
    [ObservableProperty] private bool summarizing;
    [ObservableProperty] private bool ready;
    [ObservableProperty] private bool modelReady;
    [ObservableProperty] private bool companionRunning;
    [ObservableProperty] private CompanionAddress? selectedCompanionAddress;
    [ObservableProperty] private string companionInfo = "默认关闭；手机与电脑需在同一可信网络。只发送当前问题、回答与状态，不发送资料、完整转写、音频或密钥。";
    [ObservableProperty] private string companionLink = "";
    [ObservableProperty] private string shortcutText = "";
    private Dictionary<string, string> shortcutOverrides = [];
    public IReadOnlyDictionary<string, string> ShortcutOverrides => shortcutOverrides;
    public event Action? ShortcutsChanged;
    public event Action<bool>? LanguageChanged;
    public event Action<int>? TranscriptJumpRequested;
    public IReadOnlyList<string> SubtitleExportModes { get; } = ["双语", "原文", "译文"];
    [ObservableProperty] private string subtitleExportMode = "双语";
    [ObservableProperty] private Avalonia.Media.Imaging.Bitmap? companionQr;
    [ObservableProperty] private string meetingTitle = "新会议";
    public System.Collections.ObjectModel.ObservableCollection<SessionEvidenceRow> NotesEvidence { get; } = [];

    private void BuildNotesEvidence(IReadOnlyList<TranscriptLine> lines, Guid sessionId)
    {
        NotesEvidence.Clear();
        if (StructuredPayload is not { } payload) return;
        try
        {
            foreach (var reference in SessionNotesEvidence.References(payload, lines, "", sessionId))
                NotesEvidence.Add(new SessionEvidenceRow(reference));
        }
        catch { }
    }


    [ObservableProperty] private string pageSubtitle = "选择声音来源，开始记录。";
    [ObservableProperty] private string status = "正在准备…";
    [ObservableProperty] private bool statusIsError;
    [ObservableProperty] private string systemPartial = "";
    [ObservableProperty] private string microphonePartial = "";
    [ObservableProperty] private string systemPartialTranslation = "";
    [ObservableProperty] private string microphonePartialTranslation = "";
    [ObservableProperty] private string islandTranslation = "";

    public bool HasIslandTranslation => IslandTranslation.Length > 0;

    partial void OnIslandTranslationChanged(string value) => OnPropertyChanged(nameof(HasIslandTranslation));
    [ObservableProperty] private string detectedQuestion = "手动提问，或开启自动回答。";
    [ObservableProperty] private string answerText = "";
    [ObservableProperty] private string questionInput = "";
    [ObservableProperty] private string summaryText = "结束会议后，按转写整理总结、决定、待办和未决问题。";
    [ObservableProperty] private string retrievalInfo = "";
    [ObservableProperty] private string levelText = "";
    [ObservableProperty] private string audioSourceText = "在设置中选择声音来源";
    [ObservableProperty] private string modelStatusText = "";
    [ObservableProperty] private double progress;
    [ObservableProperty] private bool progressIndeterminate;
    [ObservableProperty] private SessionMeta? currentSession;
    [ObservableProperty] private LiveWindowMode liveMode = LiveWindowMode.Notes;
    [ObservableProperty] private string? startupErrorText;
    [ObservableProperty] private string historyQuery = "";
    [ObservableProperty] private string historyFilter = "全部";
    [ObservableProperty] private string historyDateFilter = "全部时间";
    [ObservableProperty] private string historyLengthFilter = "全部时长";
    [ObservableProperty] private string historySort = "最新优先";
    public IReadOnlyList<string> HistoryDateChoices { get; } = ["全部时间", "今天", "近 7 天", "近 30 天", "本年度"];
    public IReadOnlyList<string> HistoryLengthChoices { get; } = ["全部时长", "1 分钟以内", "1–10 分钟", "10 分钟以上"];
    public IReadOnlyList<string> HistorySortChoices { get; } = ["最新优先", "最早优先", "时长最长", "时长最短", "问题最多"];
    public ObservableCollection<TranscriptRow> Transcript { get; }
    public ObservableCollection<RecentMeetingRow> History { get; }
    public ObservableCollection<RecentMeetingRow> FilteredHistory { get; } = [];
    public ObservableCollection<SourceCitation> Sources { get; }
    public ObservableCollection<CompanionAddress> CompanionAddresses { get; } = [];

    public string CompanionToggleLabel => UiText.T(CompanionRunning ? "关闭局域网手机伴侣" : "开启局域网手机伴侣");
    public string HistorySummary => UiText.EnglishUi
        ? $"{FilteredHistory.Count} shown · {History.Count} total"
        : $"{FilteredHistory.Count} 场会议 · 共 {History.Count} 场";
    public bool IsHistoryViewEmpty => FilteredHistory.Count == 0;
    public bool HistoryFilterIsAll => HistoryFilter == "全部";
    public bool HistoryFilterHasSummary => HistoryFilter == "有纪要";
    public bool HistoryFilterUnfiled => HistoryFilter == "未整理";

    // ---- 结构化纪要（Core StructuredNotes：鱼骨图 / 要点大纲 / 观点对照） ----

    [ObservableProperty] private NotesFormat selectedNotesFormat = NotesFormat.Standard;
    [ObservableProperty] private string structuredMarkdown = "";
    [ObservableProperty] private StructuredNotesPayload? structuredPayload;
    public bool HasFishboneDrawing => StructuredPayload?.Fishbone is { Categories.Count: > 0 };
    public bool HasViewpointMatrix => StructuredPayload?.Viewpoints is { Issues.Count: > 0 } || StructuredPayload?.Viewpoints is { Consensus.Count: > 0 };
    /// <summary>The Markdown copy is only shown when there is no drawing to stand in for it.</summary>
    public bool ShowStructuredMarkdown => HasStructuredNotes && !HasFishboneDrawing && !HasViewpointMatrix;
    partial void OnStructuredPayloadChanged(StructuredNotesPayload? value)
    {
        OnPropertyChanged(nameof(HasFishboneDrawing));
        OnPropertyChanged(nameof(HasViewpointMatrix));
        OnPropertyChanged(nameof(ShowStructuredMarkdown));
    }
    partial void OnStructuredMarkdownChanged(string value) => OnPropertyChanged(nameof(ShowStructuredMarkdown));
    public bool HasStructuredNotes => StructuredMarkdown.Length > 0;
    public bool NotesFormatIsStandard => SelectedNotesFormat == NotesFormat.Standard;
    public bool NotesFormatIsFishbone => SelectedNotesFormat == NotesFormat.Fishbone;
    public bool NotesFormatIsOutline => SelectedNotesFormat == NotesFormat.Outline;
    public bool NotesFormatIsViewpoints => SelectedNotesFormat == NotesFormat.Viewpoints;
    public string StructuredHint => SelectedNotesFormat == NotesFormat.Standard
        ? "文字纪要由模板整理；鱼骨图、大纲与观点对照是同一场会议的另一种看法。"
        : $"将调用回答服务把转写整理成「{SelectedNotesFormat.Title(false)}」；每条都带原话时间，找不到依据的不写。";

}
