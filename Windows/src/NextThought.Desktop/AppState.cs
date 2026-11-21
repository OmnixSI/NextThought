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
    /// <summary>C14: the teleprompter footer's workbench button.</summary>
    public Action? ShowWorkbenchRequested { get; set; }

    /// <summary>F19: lets the global UI exception logger show a status line.</summary>
    public static AppState? Current { get; private set; }

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
    private CloudSttSession? cloudSttMicrophone;
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
        Current = this;
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
        LoadOnboarding();
        candidateTimer.Tick += (_, _) => OnCandidateTimer();
        History = [];
        Transcript = [];
        Sources = [];
        LanguageChanged += english =>
        {
            UiTextSource.Instance.SetEnglish(english);
            Settings.NotifyLanguageChanged();
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
            OnPropertyChanged(nameof(SubtitleExportModes));
            OnPropertyChanged(nameof(SubtitleExportMode));
            OnPropertyChanged(nameof(DocumentRuntimeStatusText));
            OnPropertyChanged(nameof(DocumentOcrModelsStatusText));
            OnPropertyChanged(nameof(DocumentEmbeddingStatusText));
            OnPropertyChanged(nameof(DocumentProviderStatus));
            OnPropertyChanged(nameof(DocumentProviderNeedsSetup));
            OnPropertyChanged(nameof(DocumentProviderReady));
            DocumentRuntimeDetail = UiText.Translate(DocumentRuntimeDetail, english);
            OnPropertyChanged(nameof(DocumentRuntimeDetail));
            OnPropertyChanged(nameof(WebStatusText));
            OnPropertyChanged(nameof(LevelText));
            OnPropertyChanged(nameof(RetrievalInfo));
            OnPropertyChanged(nameof(ModelStatusText));
            OnPropertyChanged(nameof(CompanionInfo));
            OnPropertyChanged(nameof(SkillsStatus));
            OnPropertyChanged(nameof(CustomTemplateStatus));
            foreach (var row in Transcript) row.RefreshLanguage();
            HistoryFilter = UiText.Translate(HistoryFilter, english);
            HistoryDateFilter = UiText.Translate(HistoryDateFilter, english);
            HistoryLengthFilter = UiText.Translate(HistoryLengthFilter, english);
            HistorySort = UiText.Translate(HistorySort, english);
            OnPropertyChanged(nameof(Scenes));
            OnPropertyChanged(nameof(SceneTitle));
            OnPropertyChanged(nameof(LiveModeLabel));
            LanguageGeneration++;
            RefreshModelState();
            CompanionInfo = UiText.Translate(CompanionInfo, english);
            MyVoiceStatus = UiText.Translate(MyVoiceStatus, english);
            OnPropertyChanged(nameof(SelectedScene));
            OnPropertyChanged(nameof(HistoryDateChoices));
            OnPropertyChanged(nameof(HistoryLengthChoices));
            OnPropertyChanged(nameof(HistorySortChoices));
            OnPropertyChanged(nameof(HistoryDateFilter));
            OnPropertyChanged(nameof(HistoryLengthFilter));
            OnPropertyChanged(nameof(HistorySort));
            OnPropertyChanged(nameof(TemplateChoices));
            OnPropertyChanged(nameof(GlossaryEmptyText));
            OnPropertyChanged(nameof(StructuredHint));
            OnPropertyChanged(nameof(SummaryText));
            OnPropertyChanged(nameof(AudioSourceText));
            OnPropertyChanged(nameof(DetectedQuestion));
            OnPropertyChanged(nameof(ShortcutText));
            if (meetingTitleRaw is "新会议" or "New meeting")
            {
                meetingTitleRaw = "新会议";
                OnPropertyChanged(nameof(MeetingTitle));
            }
            OnPropertyChanged(nameof(GlossaryStatus));
            Settings.RefreshProcesses();
            var templateId = SelectedTemplateId;
            TemplateChoices.Clear();
            foreach (var choice in BuildChoices()) TemplateChoices.Add(choice);
            LoadCustomTemplates();
            SelectedTemplateId = templateId;
            LoadSceneSettings();
            LoadGlossary();
            LoadCustomTemplates();
            LoadSkills();
            // The demo materials live only in memory; a disk refresh would clear them.
            if (!StartupOptions.Demo) Library.Refresh();
            Library.NotifyLanguageChanged();
            // The demo library is in memory, so its status line is rebuilt rather than translated
            // (a format string cannot be mapped back through the table).
            if (StartupOptions.Demo && Library.Materials.Count > 0) Library.NotifyDemoLoaded();
            else Library.ImportStatus = UiText.Translate(Library.ImportStatus, english);
            LoadMyVoice();
            RaiseOnboardingStep();
            OnPropertyChanged(nameof(OnboardingVoiceStatus));
            OnPropertyChanged(nameof(SetupChecklistRows));
            RaiseOnboardingResume();
            StartScreenWatchTimer();
            _ = RefreshHistoryAsync();
            // The demo batch keeps its in-memory rows, so the session rows are told to refresh the
            // computed language-dependent fields (RecentMeetingRow.Subtitle etc.) themselves.
            foreach (var row in History) row.NotifyLanguageChanged();
            foreach (var row in HomeRecent) row.NotifyLanguageChanged();
            foreach (var row in FilteredHistory) row.NotifyLanguageChanged();
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

    /// <summary>G8: bumped on a language switch so views can rebuild code-generated rows.</summary>
    [ObservableProperty] private int languageGeneration;
    [ObservableProperty] private string versionText = typeof(AppState).Assembly.GetName().Version!.ToString(3);
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
    private static readonly string[] SubtitleExportKeys = ["双语", "原文", "译文"];

    /// <summary>The picker labels follow the interface language; the stored value stays the Chinese key.</summary>
    public IReadOnlyList<string> SubtitleExportModes => SubtitleExportKeys.Select(UiText.T).ToArray();

    private string subtitleExportKey = "双语";

    public string SubtitleExportMode
    {
        get => UiText.T(subtitleExportKey);
        set
        {
            var key = SubtitleExportKeys.FirstOrDefault(candidate => UiText.T(candidate) == value) ?? value;
            if (key == subtitleExportKey) return;
            subtitleExportKey = key;
            OnPropertyChanged();
        }
    }
    [ObservableProperty] private Avalonia.Media.Imaging.Bitmap? companionQr;
    private string meetingTitleRaw = "新会议";

    /// <summary>G8: the untitled placeholder follows the interface language, even when demo setup resets it.</summary>
    public string MeetingTitle
    {
        get => meetingTitleRaw is "新会议" or "New meeting" ? UiText.T("新会议") : meetingTitleRaw;
        set
        {
            if (meetingTitleRaw == value) return;
            meetingTitleRaw = value;
            OnPropertyChanged();
        }
    }
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

    /// <summary>C5: the partial words the island shows while it hears someone.</summary>
    public string HearingText => SystemPartial.Length > 0 ? SystemPartial
        : MicrophonePartial.Length > 0 ? MicrophonePartial : "";

    partial void OnSystemPartialChanged(string value) => OnPropertyChanged(nameof(HearingText));
    partial void OnMicrophonePartialChanged(string value) => OnPropertyChanged(nameof(HearingText));
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
    private static readonly string[] HistoryDateKeys = ["全部时间", "今天", "近 7 天", "近 30 天", "本年度"];
    private static readonly string[] HistoryLengthKeys = ["全部时长", "1 分钟以内", "1–10 分钟", "10 分钟以上"];
    private static readonly string[] HistorySortKeys = ["最新优先", "最早优先", "时长最长", "时长最短", "问题最多"];
    public IReadOnlyList<string> HistoryDateChoices => HistoryDateKeys.Select(UiText.T).ToArray();
    public IReadOnlyList<string> HistoryLengthChoices => HistoryLengthKeys.Select(UiText.T).ToArray();
    public IReadOnlyList<string> HistorySortChoices => HistorySortKeys.Select(UiText.T).ToArray();
    public ObservableCollection<TranscriptRow> Transcript { get; }
    public ObservableCollection<RecentMeetingRow> History { get; }
    public ObservableCollection<RecentMeetingRow> FilteredHistory { get; } = [];
    public ObservableCollection<WebResult> WebResults { get; } = [];
    public string WebStatusText => WebResults.Count == 0 ? UiText.T("未联网") : UiText.Format("联网来源 %lld 条", WebResults.Count);

    public System.Collections.ObjectModel.ObservableCollection<AnswerRecord> CurrentAnswerHistory { get; } = [];

    public ObservableCollection<SourceCitation> Sources { get; }
    public ObservableCollection<CompanionAddress> CompanionAddresses { get; } = [];

    public string CompanionToggleLabel => UiText.T(CompanionRunning ? "关闭局域网手机伴侣" : "开启局域网手机伴侣");
    public string HistorySummary => UiText.EnglishUi
        ? $"{FilteredHistory.Count} shown · {History.Count} total"
        : UiText.Format("%lld 场会议 · 共 %lld 场", FilteredHistory.Count, History.Count);
    public bool IsHistoryViewEmpty => FilteredHistory.Count == 0;
    public bool HistoryFilterIsAll => MatchesFilter(HistoryFilter, "全部");
    public bool HistoryFilterHasSummary => MatchesFilter(HistoryFilter, "有纪要");
    public bool HistoryFilterUnfiled => MatchesFilter(HistoryFilter, "未整理");

    /// <summary>G8: filter values may arrive as the Chinese key or as the translated label.</summary>
    internal static bool MatchesFilter(string? value, string key) =>
        value == key || value == UiText.T(key);

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
        ? UiText.T("文字纪要由模板整理；鱼骨图、大纲与观点对照是同一场会议的另一种看法。")
        : UiText.Format("将调用回答服务把转写整理成「%@」；每条都带原话时间，找不到依据的不写。", SelectedNotesFormat.Title(false));

}
