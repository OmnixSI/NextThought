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

public enum AppSection { Home, Live, Notes, History, Series, Library, Settings }

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
    public event Action? ShortcutsChanged;
    public event Action<bool>? LanguageChanged;
    public IReadOnlyList<string> SubtitleExportModes { get; } = ["双语", "原文", "译文"];
    [ObservableProperty] private string subtitleExportMode = "双语";
    [ObservableProperty] private Avalonia.Media.Imaging.Bitmap? companionQr;
    [ObservableProperty] private string meetingTitle = "新会议";
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
    public ObservableCollection<TranscriptRow> Transcript { get; }
    public ObservableCollection<RecentMeetingRow> History { get; }
    public ObservableCollection<RecentMeetingRow> FilteredHistory { get; } = [];
    public ObservableCollection<SourceCitation> Sources { get; }
    public ObservableCollection<CompanionAddress> CompanionAddresses { get; } = [];

    public string CompanionToggleLabel => CompanionRunning ? "关闭局域网手机伴侣" : "开启局域网手机伴侣";
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

    [RelayCommand]
    private async Task SetNotesFormatAsync(NotesFormat format)
    {
        SelectedNotesFormat = format;
        StructuredMarkdown = "";
        OnPropertyChanged(nameof(HasStructuredNotes));
        OnPropertyChanged(nameof(NotesFormatIsStandard));
        OnPropertyChanged(nameof(NotesFormatIsFishbone));
        OnPropertyChanged(nameof(NotesFormatIsOutline));
        OnPropertyChanged(nameof(NotesFormatIsViewpoints));
        OnPropertyChanged(nameof(StructuredHint));
        if (CurrentSession is { } session) await LoadStructuredAsync(session, format);
    }

    private string StructuredPath(SessionMeta session, NotesFormat format) =>
        Path.Combine(Sessions.Folder(session.Id), $"structured-{format.Raw()}.md");

    private string StructuredJsonPath(SessionMeta session, NotesFormat format) =>
        Path.Combine(Sessions.Folder(session.Id), $"structured-{format.Raw()}.json");

    private async Task LoadStructuredAsync(SessionMeta session, NotesFormat format)
    {
        if (!format.IsStructured()) return;
        StructuredPayload = null;
        try
        {
            var summary = Path.Combine(Sessions.Folder(session.Id), "summary.json");
            if (File.Exists(summary))
            {
                try
                {
                    using var document = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(summary));
                    if (document.RootElement.TryGetProperty("structured", out var structured))
                    {
                        // Mac writes StructuredNotesPayload directly; 0.5.4-0.5.4 wrote {payload:...}.
                        var payloadElement = structured.TryGetProperty("payload", out var legacyPayload) ? legacyPayload : structured;
                        if (payloadElement.TryGetProperty("format", out var savedFormat) && savedFormat.GetString() == format.Raw())
                        {
                            StructuredPayload = System.Text.Json.JsonSerializer.Deserialize<StructuredNotesPayload>(payloadElement.GetRawText(), JsonDefaults.Options);
                            if (structured.TryGetProperty("markdown", out var savedMarkdown) && savedMarkdown.GetString() is { } text)
                                StructuredMarkdown = text;
                        }
                    }
                }
                catch (System.Text.Json.JsonException) { }
            }
            var path = StructuredPath(session, format);
            if (StructuredPayload is null && File.Exists(path)) StructuredMarkdown = await File.ReadAllTextAsync(path);
            // The JSON keeps the drawing available; the Markdown alone cannot be turned back into one.
            var json = StructuredJsonPath(session, format);
            if (File.Exists(json))
            {
                try
                {
                    StructuredPayload = System.Text.Json.JsonSerializer.Deserialize<StructuredNotesPayload>(
                        await File.ReadAllTextAsync(json), JsonDefaults.Options);
                }
                catch (System.Text.Json.JsonException)
                {
                    StructuredPayload = null;
                }
            }
            OnPropertyChanged(nameof(HasStructuredNotes));
        }
        catch
        {
            // A missing or unreadable file is the same as "not generated yet".
        }
    }

    [RelayCommand(CanExecute = nameof(CanGenerateStructured))]
    private async Task GenerateStructuredNotesAsync()
    {
        if (CurrentSession is not { } session || !SelectedNotesFormat.IsStructured()) return;
        if (currentLines.Count == 0) { SetStatus("这场会议还没有转写，无法整理。", true); return; }
        var format = SelectedNotesFormat;
        CancelRequest();
        var generation = ++requestEpoch;
        var cancellation = new CancellationTokenSource();
        requestCancellation = cancellation;
        Answering = true;
        ProgressIndeterminate = true;
        RaiseBusy();
        try
        {
            SetStatus($"正在整理成{format.Title(false)}…");
            var transcript = BuildNumberedTranscript(session);
            var english = AnswerLanguage.Spoken(transcript) == "English";
            var header = english
                ? $"# {session.Title}\n\nMeeting: {session.StartedAt.ToLocalTime():yyyy-MM-dd HH:mm} · Scene: {SceneTitle}"
                : $"# {session.Title}\n\n会议时间：{session.StartedAt.ToLocalTime():yyyy-MM-dd HH:mm} · 场景：{SceneTitle}";
            var system = StructuredNotesPrompt.System(format, english);
            var user = StructuredNotesPrompt.User(header, transcript);
            var reply = await CurrentClient().AnswerAsync(AnswerOptions(true), system, user, null, cancellation.Token, 180);
            if (generation != requestEpoch) return;
            var payload = StructuredNotesParser.Parse(reply, format, english, fallbackTitle: session.Title, model: AnswerOptions(true).Model);
            if (payload is null)
            {
                SetStatus("整理结果不是可用的 JSON，稍后可重试。", true);
                return;
            }
            // 核对：时间点必须在会议内、且附近有转写行，找不到依据的节点丢弃（NotesVerifier / Cleaner）。
            var cleaned = StructuredNotesCleaner.Clean(payload, LineOffsets(session), MeetingSeconds(session));
            if (cleaned is null)
            {
                SetStatus($"整理出的{format.Title(false)}缺少可核对的转写依据，已丢弃；可重试，或改用文字纪要。", true);
                return;
            }
            payload = cleaned;
            StructuredPayload = payload;
            StructuredMarkdown = StructuredNotesMarkdown.Render(payload);
            OnPropertyChanged(nameof(HasStructuredNotes));
            try
            {
                // Mac keeps structured notes inside summary.json; write there as well as the legacy cache.
                var summaryPath = Path.Combine(Sessions.Folder(session.Id), "summary.json");
                System.Text.Json.Nodes.JsonObject summary;
                try
                {
                    summary = (File.Exists(summaryPath)
                        ? System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(summaryPath)) as System.Text.Json.Nodes.JsonObject
                        : null) ?? new System.Text.Json.Nodes.JsonObject();
                }
                catch (System.Text.Json.JsonException) { summary = new System.Text.Json.Nodes.JsonObject(); }
                // Mac stores a StructuredNotesPayload directly (and all formats in structuredAll).
                var payloadNode = System.Text.Json.Nodes.JsonNode.Parse(
                    System.Text.Json.JsonSerializer.Serialize(payload, JsonDefaults.Options));
                summary["structured"] = payloadNode;
                var all = summary["structuredAll"] as System.Text.Json.Nodes.JsonArray
                    ?? new System.Text.Json.Nodes.JsonArray();
                for (var index = all.Count - 1; index >= 0; index--)
                    if (all[index]?["format"]?.GetValue<string>() == format.Raw()) all.RemoveAt(index);
                all.Add(payloadNode!.DeepClone());
                summary["structuredAll"] = all;
                if (payload.TranscriptSourceRevision is { Length: > 0 } sourceRevision)
                    summary["transcriptSourceRevision"] = sourceRevision;
                summary["title"] ??= session.Title;
                summary["markdown"] ??= StructuredMarkdown;
                await File.WriteAllTextAsync(summaryPath, summary.ToJsonString(JsonDefaults.ArchiveOptions));
                await File.WriteAllTextAsync(StructuredPath(session, format), StructuredMarkdown);
                await File.WriteAllTextAsync(StructuredJsonPath(session, format),
                    System.Text.Json.JsonSerializer.Serialize(payload, JsonDefaults.Options));
            }
            catch
            {
                // The view is still useful without the cached copy.
            }
            SetStatus(payload.DroppedCount > 0
                ? $"已整理成{format.Title(false)}；{payload.DroppedCount} 条找不到原话依据的内容已丢弃。"
                : $"已整理成{format.Title(false)}，可复制或导出。");
        }
        catch (OperationCanceledException) { if (generation == requestEpoch) SetStatus("整理已取消。"); }
        catch (Exception ex) { if (generation == requestEpoch) SetStatus(ex.Message, true); }
        finally
        {
            cancellation.Dispose();
            if (generation == requestEpoch)
            {
                requestCancellation = null;
                Answering = false;
                ProgressIndeterminate = false;
                RaiseBusy();
            }
        }
    }

    private bool CanGenerateStructured() => IsIdle && !Answering && CurrentSession != null
        && SelectedNotesFormat.IsStructured() && currentLines.Count > 0;

    /// <summary>Seconds from the meeting start of each transcript line (the evidence check needs them).</summary>
    private IReadOnlyList<int> LineOffsets(SessionMeta session) =>
        currentLines.Select(line => Math.Max(0, (int)((line.StartedAt ?? line.Date) - session.StartedAt).TotalSeconds)).ToList();

    /// <summary>How long the meeting ran, at least as long as its last line.</summary>
    private static double MeetingSeconds(SessionMeta session, IReadOnlyList<int>? offsets = null)
    {
        var end = session.EndedAt ?? DateTimeOffset.UtcNow;
        var seconds = Math.Max(0, (end - session.StartedAt).TotalSeconds);
        if (offsets is { Count: > 0 }) seconds = Math.Max(seconds, offsets[^1]);
        return seconds;
    }

    /// <summary>"[12:41] 说话人：…" lines, the format the structured prompts expect.</summary>
    private string BuildNumberedTranscript(SessionMeta session)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var line in currentLines)
        {
            var offset = Math.Max(0, (int)((line.StartedAt ?? line.Date) - session.StartedAt).TotalSeconds);
            var speaker = line.Speaker == "remote" ? "对方" : line.Speaker == "room" ? "现场" : "我";
            builder.Append('[').Append(NotesTime.Text(offset)).Append("] ").Append(speaker).Append('：').AppendLine(line.Text);
        }
        return builder.ToString();
    }

    // ---- 九个场景（Core ScenePreset 的界面映射） ----

    [ObservableProperty] private MeetingMode selectedMode = MeetingMode.Meeting;

    public IReadOnlyList<SceneChoice> Scenes { get; } =
    [
        new(MeetingMode.Interview, "面试", "被问、要答得清楚"),
        new(MeetingMode.Academic, "汇报 / 答辩", "讲给别人听"),
        new(MeetingMode.LabMeeting, "组会", "讲进展，听意见"),
        new(MeetingMode.GroupProject, "小组作业", "分工与对齐"),
        new(MeetingMode.Client, "客户沟通", "先听需求再回应"),
        new(MeetingMode.Meeting, "工作会议", "结论与待办"),
        new(MeetingMode.Lesson, "讲座 / 课程", "主要听，记重点"),
        new(MeetingMode.Discussion, "小组讨论", "来回交换观点"),
        new(MeetingMode.General, "通用", "什么都可以")
    ];

    public SceneChoice? SelectedScene
    {
        get => Scenes.FirstOrDefault(scene => scene.Mode == SelectedMode);
        set { if (value is not null) SelectedMode = value.Mode; }
    }

    public string SceneTitle => SelectedScene?.Title ?? "通用";

    /// <summary>The cue surface layout this scene asks for (key points vs. a script).</summary>
    public CueStyle SceneCueStyle => ScenePreset.For(SelectedMode).CueStyle;

    [RelayCommand]
    private void SetScene(MeetingMode mode)
    {
        SelectedMode = mode;
        OnPropertyChanged(nameof(SelectedScene));
        OnSceneChanged();
    }

    private void OnSceneChanged()
    {
        ApplyDefaultTemplate();
        OnPropertyChanged(nameof(SceneTitle));
        OnPropertyChanged(nameof(SceneCueStyle));
        OnPropertyChanged(nameof(SceneSummary));
        OnPropertyChanged(nameof(IslandLines));
        OnPropertyChanged(nameof(IslandHeadline));
        OnPropertyChanged(nameof(IslandDetail));
    }

    /// <summary>What the picker says about the chosen scene.</summary>
    public string SceneSummary => SelectedScene is { } scene
        ? $"当前场景：{scene.Title} · {scene.Subtitle}" + (SceneCueStyle == CueStyle.Script ? " · 提词按句" : " · 提词按要点")
        : "当前场景：通用";

    /// <summary>Cue lines for the island: key points or a script, per the scene.</summary>
    public IReadOnlyList<CueLine> IslandLines => string.IsNullOrWhiteSpace(PlainAnswer)
        ? []
        : CueScript.Lines(PlainAnswer, SceneCueStyle, 3);

    public string IslandHeadline => IslandLines.Count > 0 ? IslandLines[0].Spoken : "";
    public string IslandDetail => IslandLines.Count > 1 ? string.Join("　", IslandLines.Skip(1).Select(line => line.Spoken)) : "";

    [RelayCommand]
    private void SetHistoryFilter(string filter) => HistoryFilter = filter;

    public AppSettings SettingsSnapshot => Settings.Build();

    public bool IsIdle => !Working && !Recording && !Stopping;
    public bool CanCancel => Working || Answering;
    public bool BusyOrRecording => Working || Recording;
    public bool OnHome => Section == AppSection.Home;
    public bool OnLive => Section == AppSection.Live;
    public bool OnNotes => Section == AppSection.Notes;
    public bool OnLibrary => Section == AppSection.Library;
    public bool OnHistory => Section == AppSection.History;
    public bool OnSeries => Section == AppSection.Series;
    public bool OnSettings => Section == AppSection.Settings;
    public bool HasTranscript => Transcript.Count > 0;
    public string StatsText => Settings.UiLanguage == "English"
        ? Recording ? $"Recording · {currentLines.Count} lines" : $"{currentLines.Count} lines · {(CurrentSession?.AnswerCount ?? 0)} answers"
        : Recording ? $"正在记录 · {currentLines.Count} 条转写" : $"{currentLines.Count} 条转写 · {(CurrentSession?.AnswerCount ?? 0)} 条回答";
    public string Greeting => Settings.UiLanguage == "English" ? DateTime.Now.Hour switch
    {
        < 6 => "Late night. Want to keep a thought?",
        < 11 => "Good morning. What should we capture today?",
        < 14 => "Good noon. The meeting goes on.",
        < 18 => "Good afternoon. Keep the discussion.",
        _ => "Good evening. What conclusions are worth keeping today?"
    } : DateTime.Now.Hour switch
    {
        < 6 => "夜深了。要把想法留下来吗？",
        < 11 => "早上好。今天要记下什么？",
        < 14 => "中午好。会议还在继续。",
        < 18 => "下午好。把讨论留下来。",
        _ => "晚上好。今天有哪些值得留下的结论？"
    };

    partial void OnSectionChanged(AppSection value)
    {
        OnPropertyChanged(nameof(OnHome));
        OnPropertyChanged(nameof(OnLive));
        OnPropertyChanged(nameof(OnNotes));
        OnPropertyChanged(nameof(OnLibrary));
        OnPropertyChanged(nameof(OnHistory));
        OnPropertyChanged(nameof(OnSeries));
        OnPropertyChanged(nameof(OnSettings));
    }

    partial void OnHistoryQueryChanged(string value) => ApplyHistoryFilter();
    partial void OnHistoryFilterChanged(string value)
    {
        ApplyHistoryFilter();
        OnPropertyChanged(nameof(HistoryFilterIsAll));
        OnPropertyChanged(nameof(HistoryFilterHasSummary));
        OnPropertyChanged(nameof(HistoryFilterUnfiled));
    }

    partial void OnWorkingChanged(bool value) => RaiseBusy();
    partial void OnRecordingChanged(bool value) { RaiseBusy(); PublishCompanionSnapshot(); }
    partial void OnStoppingChanged(bool value) => RaiseBusy();
    partial void OnAnsweringChanged(bool value) { RaiseBusy(); OnPropertyChanged(nameof(CanCancel)); }
    partial void OnSummarizingChanged(bool value) => RaiseBusy();
    partial void OnCurrentSessionChanged(SessionMeta? value) => OnPropertyChanged(nameof(StatsText));
    partial void OnDetectedQuestionChanged(string value)
    {
        PublishCompanionSnapshot();
        if (!Recording || string.IsNullOrWhiteSpace(value) || CurrentSession is not { } meta) return;
        _ = Sessions.AppendQuestionAsync(meta.Id, new SessionQuestionEvent
        {
            Date = DateTimeOffset.UtcNow, Text = value, Intent = "detected", Disposition = "candidate"
        }).ContinueWith(task => _ = task.Exception, TaskScheduler.Default);
    }
    partial void OnLiveModeChanged(LiveWindowMode value) => OnPropertyChanged(nameof(LiveModeLabel));
    partial void OnAnswerTextChanged(string value)
    {
        OnPropertyChanged(nameof(PlainAnswer));
        PublishCompanionSnapshot();
        OnPropertyChanged(nameof(IslandLines));
        OnPropertyChanged(nameof(IslandHeadline));
        OnPropertyChanged(nameof(IslandDetail));
    }

    /// <summary>Answer text without Markdown markers, for the single-line cue island.</summary>
    public string PlainAnswer => MarkdownPlain.Strip(AnswerText);

    private void RaiseBusy()
    {
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(BusyOrRecording));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(StatsText));
        StartRecordingCommand.NotifyCanExecuteChanged();
        StopRecordingCommand.NotifyCanExecuteChanged();
        AskCommand.NotifyCanExecuteChanged();
        SummarizeCommand.NotifyCanExecuteChanged();
        SaveSettingsCommand.NotifyCanExecuteChanged();
        NewMeetingCommand.NotifyCanExecuteChanged();
        ImportWaveCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
        ExportDocxCommand.NotifyCanExecuteChanged();
        ExportPdfCommand.NotifyCanExecuteChanged();
        ExportSrtCommand.NotifyCanExecuteChanged();
        ExportVttCommand.NotifyCanExecuteChanged();
        GenerateStructuredNotesCommand.NotifyCanExecuteChanged();
        SummarizeWithTemplateCommand.NotifyCanExecuteChanged();
        ProofreadTranscriptCommand.NotifyCanExecuteChanged();
        BackupCommand.NotifyCanExecuteChanged();
        RestoreCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void Navigate(string section)
    {
        if (Enum.TryParse<AppSection>(section, out var value)) Section = value;
    }

    public void SetStatus(string text, bool error = false)
    {
        Status = UiText.Translate(text, Settings.UiLanguage == "English");
        StatusIsError = error;
    }

    /// <summary>Header subtitle; the fixed ones follow the interface language.</summary>
    private void SetPageSubtitle(string text) =>
        PageSubtitle = UiText.Translate(text, Settings.UiLanguage == "English");

    public async Task InitializeAsync()
    {
        var clock = Stopwatch.StartNew();
        var timings = new List<string>();
        void Mark(string step)
        {
            timings.Add($"{step}:{clock.ElapsedMilliseconds}ms");
            clock.Restart();
        }
        try
        {
            Settings.LoadFromStore();
            UiText.EnglishUi = Settings.UiLanguage == "English";
            Mark("settings");
            RefreshProcesses();
            RefreshCompanionAddresses();
            Mark("processes");
            await Settings.RefreshDevicesAsync();
            Mark("devices");
            MeetingTitle = string.IsNullOrWhiteSpace(Settings.LastMeetingTitle)
                ? (UiText.EnglishUi ? "New meeting" : "新会议")
                : Settings.LastMeetingTitle;
            var recovered = await Sessions.RecoverInterruptedAsync();
            Mark("recover");
            RefreshHistory();
            LoadWorkspace();
            LoadGlossary();
            LoadSkills();
            LoadCustomTemplates();
            Mark("history");
            LatchCompanionCommandState();
            initialized = true;
            Ready = true;
            RefreshModelState();
            Library.Refresh();
            Mark("library");
            if (StartupOptions.Demo)
            {
                LoadDemo();
                SetStatus("演示模式：内置样本数据，不会写入你的资料。");
            }
            else
            {
                SetStatus(recovered.Count > 0
                    ? $"已处理 {recovered.Count} 场中断会议；可在历史中继续查看或整理纪要。"
                    : ModelReady ? "语音已就绪。可以开始记录，或先导入 WAV 验证。" : "先到设置准备语音组件；未连接回答服务时也可以只记录。");
            }
            if (StartupOptions.Companion && SelectedCompanionAddress is not null) await ToggleCompanionAsync();
            if (StartupOptions.Section is { } requested) Section = requested;
        }
        catch (Exception ex)
        {
            startupError = ex.Message;
            StartupErrorText = ex.ToString();
            initialized = true;
            Ready = true;
            SetStatus(ex.Message, true);
        }
        Mark("state");
        InitTimings = string.Join(" ", timings);
        RaiseBusy();
        OnInitialized();
    }

    public string InitTimings { get; private set; } = "";

    public event Action? Initialized;
    private void OnInitialized() => Initialized?.Invoke();

    private DispatcherTimer? demoFeeder;
    private int demoIndex = 10;

    /// <summary>Demo only: keeps a half-sentence moving so the subtitle bar has live activity.</summary>
    private void StartDemoFeeder()
    {
        var lines = DemoContent.ExtraLines();
        demoFeeder = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        demoFeeder.Tick += (_, _) =>
        {
            var line = lines[demoIndex % lines.Count];
            demoIndex++;
            SystemPartial = "系统 · " + line.Text[..Math.Min(12, line.Text.Length)];
            DispatcherTimer.RunOnce(() =>
            {
                SystemPartial = "";
                currentLines.Add(line);
                Transcript.Add(new TranscriptRow(line));
                while (Transcript.Count > 400) Transcript.RemoveAt(0);
                OnPropertyChanged(nameof(StatsText));
            }, TimeSpan.FromMilliseconds(1400));
        };
        demoFeeder.Start();
    }

    /// <summary>Fills the interface with the built-in sample meeting; never touches the data directory.</summary>
    public void LoadDemo()
    {
        MeetingTitle = "结构组会 · 连续刚构桥监测";
        SetPageSubtitle("演示数据 · 不会写入你的资料");
        DetectedQuestion = DemoContent.Question;
        AnswerText = DemoContent.Answer;
        SummaryText = DemoContent.Summary;
        // 同传上岛：演示模式给一句固定译文，没有模型和密钥也能验收岛上那一行。
        IslandTranslation = DemoContent.IslandTranslation;
        RetrievalInfo = "关键词 + 语义 · 3 段 · 0.42 秒";
        currentLines.Clear();
        Transcript.Clear();
        foreach (var line in DemoContent.Lines())
        {
            currentLines.Add(line);
            Transcript.Add(new TranscriptRow(line));
        }
        History.Clear();
        HomeRecent.Clear();
        foreach (var meeting in DemoContent.Meetings())
        {
            History.Add(new RecentMeetingRow(meeting));
            if (HomeRecent.Count < 5) HomeRecent.Add(new RecentMeetingRow(meeting));
        }
        CurrentSession = DemoContent.Meetings()[0];
        Sources.Clear();
        Sources.Add(new SourceCitation("监测方案 v3 · 第 2 页", "demo-scheme", "监测方案 v3.pdf", "demo",
            "第 2 页 · 3.1", "支座刚度的取值需要通过敏感性分析标定。", 2));
        Sources.Add(new SourceCitation("温度补偿记录 · 第 1 页", "demo-notes", "温度补偿记录.docx", "demo",
            "第 1 页", "小波分解后残差约占原始信号 12%。", 1));
        OnPropertyChanged(nameof(HasTranscript));
        OnPropertyChanged(nameof(IsHistoryEmpty));
        ApplyHistoryFilter();
        OnPropertyChanged(nameof(StatsText));
        ModelStatusText = "演示模式：未加载识别模型";
        var demoFishbone = StructuredNotesParser.Parse(DemoContent.FishboneJson, NotesFormat.Fishbone, fallbackTitle: "结构组会");
        var demoViewpoints = StructuredNotesParser.Parse(DemoContent.ViewpointsJson, NotesFormat.Viewpoints, fallbackTitle: "结构组会");
        // The demo follows --notes-format, so each drawn format can be reviewed without a model.
        var demoPayload = StartupOptions.NotesFormat == NotesFormat.Viewpoints ? demoViewpoints : demoFishbone;
        if (demoPayload is not null)
        {
            SelectedNotesFormat = demoPayload.Format;
            StructuredPayload = demoPayload;
            StructuredMarkdown = StructuredNotesMarkdown.Render(demoPayload);
            OnPropertyChanged(nameof(HasStructuredNotes));
            OnPropertyChanged(nameof(NotesFormatIsStandard));
            OnPropertyChanged(nameof(NotesFormatIsFishbone));
            OnPropertyChanged(nameof(NotesFormatIsOutline));
            OnPropertyChanged(nameof(NotesFormatIsViewpoints));
            OnPropertyChanged(nameof(StructuredHint));
        }
        // History now holds the demo meetings: the series page lists them by id.
        RefreshSeries();
        StartDemoFeeder();
        if (StartupOptions.Subtitles) LiveMode = LiveWindowMode.Subtitles;
    }

    public void RefreshModelState()
    {
        var settings = Settings.Build();
        var streaming = Models.IsInstalled(settings.LocalModel);
        var refine = Models.IsInstalled("xasrOffline");
        ModelReady = streaming && (!settings.SecondPass || refine);
        ModelStatusText = $"流式模型：{(streaming ? "已就绪" : "未安装或待修复")}　整句精修：{(refine ? "已就绪" : "未安装")}";
        AudioSourceText = settings.CaptureSystem && settings.CaptureMicrophone
            ? "所选系统输出设备混音 + 麦克风 · 在设置中调整"
            : settings.CaptureSystem ? "所选系统输出设备混音 · 不采集麦克风"
            : settings.CaptureMicrophone ? "麦克风 · 适合线下会议" : "尚未选择声音来源，请到设置中选择。";
        StartRecordingCommand.NotifyCanExecuteChanged();
        ImportWaveCommand.NotifyCanExecuteChanged();
    }

    public void RefreshProcesses() => Settings.RefreshProcesses();

    [RelayCommand]
    private void ReloadProcesses()
    {
        Settings.RefreshProcesses();
        SetStatus("已刷新可采集的应用程序列表。");
    }

    public void RefreshHistory(Guid? select = null)
    {
        var list = Sessions.List();
        History.Clear();
        foreach (var item in list) History.Add(new RecentMeetingRow(item));
        if (select is { } id)
        {
            var match = list.FirstOrDefault(x => x.Id == id);
            if (match is not null) CurrentSession ??= match;
        }
        HomeRecent.Clear();
        foreach (var item in list.Take(5)) HomeRecent.Add(new RecentMeetingRow(item));
        OnPropertyChanged(nameof(IsHistoryEmpty));
        ApplyHistoryFilter();
    }

    /// <summary>
    /// Search and status filter for the history page (plan §15 历史：搜索、筛选、批量删除).
    /// Batch delete is not ported yet; the filter and search are.
    /// </summary>
    private void ApplyHistoryFilter()
    {
        var query = HistoryQuery.Trim();
        IEnumerable<RecentMeetingRow> rows = History;
        if (HistoryFilter == "有纪要") rows = rows.Where(row => row.HasSummary);
        else if (HistoryFilter == "未整理") rows = rows.Where(row => !row.HasSummary);
        if (query.Length > 0)
        {
            rows = rows.Where(row =>
                row.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.Meta.LocalStartedAt.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.Meta.Mode.Contains(query, StringComparison.OrdinalIgnoreCase));
        }
        FilteredHistory.Clear();
        foreach (var row in rows) FilteredHistory.Add(row);
        OnPropertyChanged(nameof(HistorySummary));
        OnPropertyChanged(nameof(IsHistoryViewEmpty));
    }

    public bool IsHistoryEmpty => History.Count == 0;

    public ObservableCollection<RecentMeetingRow> HomeRecent { get; } = [];

    private void OnCandidateTimer()
    {
        var now = DateTimeOffset.UtcNow;
        ApplyCandidate(tracker.Tick(now));
        if (!Settings.AutomaticAnswers || !Recording || Stopping) { automaticScheduler.Clear(); return; }
        if (Answering && !lastRequestAutomatic) return;
        var speaking = microphoneLevel >= .012 || now - lastMicrophoneSpeech < TimeSpan.FromMilliseconds(600);
        if (automaticScheduler.Tick(now, speaking) is { } question) _ = AnswerAsync(question.FullText, true);
    }

    private void ApplyCandidate(CandidateDecision decision)
    {
        if (decision.Candidate is not { } candidate || decision.Kind is CandidateKind.None or CandidateKind.Updated) return;
        DetectedQuestion = candidate.FullText;
        if (decision.Kind == CandidateKind.Trigger && Settings.AutomaticAnswers && Recording && !Stopping)
        {
            automaticScheduler.Enqueue(candidate, DateTimeOffset.UtcNow);
            if (candidate.Supersedes.HasValue && Answering && lastRequestAutomatic) CancelRequest();
        }
    }

    private static bool IsNewMeetingTitle(string title) => title is "新会议" or "New meeting";

    private SessionMeta NewMeta(AppSettings settings, string? title = null) => new()
    {
        Title = string.IsNullOrWhiteSpace(title ?? settings.LastMeetingTitle)
            ? (UiText.EnglishUi ? "Meeting " : "会议 ") + DateTime.Now.ToString("MM-dd HH:mm")
            : (title ?? settings.LastMeetingTitle),
        Mode = ModeKey(SelectedMode),
        AudioSource = settings.CaptureSystem && settings.CaptureProcessId > 0
            ? settings.CaptureMicrophone ? "process-tree+microphone" : "process-tree"
            : settings.CaptureSystem ? settings.CaptureMicrophone ? "system+microphone" : "system" : "microphone",
        InPerson = !settings.CaptureSystem,
        Recognizer = settings.LocalModel + (settings.SecondPass ? "+refine" : ""),
        AnswerModel = settings.Model
    };

    /// <summary>SessionMode raw value as the Mac archive writes it (LabMeeting → "labMeeting").</summary>
    private static string ModeKey(MeetingMode mode)
    {
        var name = mode.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartRecordingAsync()
    {
        if (Working || Recording || closing) return;
        Working = true;
        RaiseBusy();
        SessionMeta? meta = null;
        try
        {
            await Settings.SaveAsync();
            // Endpoints are enumerated lazily; make sure a saved device choice is applied.
            await Settings.RefreshDevicesAsync();
            var settings = Settings.Build();
            CancelRequest();
            ResetDisplay();
            meta = NewMeta(settings, string.IsNullOrWhiteSpace(MeetingTitle) || IsNewMeetingTitle(MeetingTitle) ? null : MeetingTitle);
            MeetingTitle = meta.Title;
            var context = new SessionContextSnapshot
            {
                Mode = meta.Mode,
                Style = "concise",
                Preferences = "",
                Profile = "",
                EffectiveMode = meta.Mode,
                Documents = Library.DocumentsForRetrieval().Select(document => new ContextDocument
                {
                    Id = document.Id,
                    Name = document.Name,
                    Pages = document.Blocks.Select(block => block.Text).ToList(),
                    Enabled = true
                }).ToList(),
                Glossary = glossary
            };
            await Sessions.CreateAsync(meta, context);
            CurrentSession = meta;
            sessionLease = Sessions.AcquireLease(meta.Id);
            BeginAutoEnd(meta);
            var generation = ++epoch;
            SetStatus("正在加载本地语音模型…");
            if (settings.SeparateSpeakers) voiceTagger = await Task.Run(() => new MeetingVoiceTagger(Paths, settings.CaptureSystem ? AudioSource.System : AudioSource.Microphone));
            if (settings.TranslateSpeech) translator = new(CurrentClient(), AnswerOptions());
            BeginLiveTranslation();
            engine = await Task.Run(() => new LiveSpeechEngine(Models, settings, RecognitionHotwords()));
            if (closing) throw new OperationCanceledException("程序正在退出，已取消开始记录。");
            engine.Updated += update => Dispatcher.UIThread.Post(() => { if (generation == epoch) ReceiveTranscript(meta, update, true); });
            engine.Failed += message => Dispatcher.UIThread.Post(() => HandleAudioFailure(message));
            engine.Warning += message => Dispatcher.UIThread.Post(() => SetStatus(message, true));
            var ownedEngine = engine;
            capture = new AudioCaptureService();
            capture.FrameReady += frame => { voiceTagger?.Add(frame); ownedEngine.Accept(frame); };
            capture.Failed += message => Dispatcher.UIThread.Post(() => HandleAudioFailure(message));
            capture.LevelChanged += (source, level) => Dispatcher.UIThread.Post(() =>
            {
                if (source == AudioSource.System) systemLevel = level; else microphoneLevel = level;
                LevelText = $"系统 {systemLevel * 100:F0} · 麦克风 {microphoneLevel * 100:F0}";
            });
            await Task.Run(() => capture.Start(settings));
            if (capture.LastError is { } captureError) throw new IOException(captureError);
            Recording = true;
            ListeningPaused = false;
            candidateTimer.Start();
            SetPageSubtitle("进行中 · 转写持续保存在本机");
            Section = AppSection.Live;
            RefreshHistory(meta.Id);
            SetStatus(settings.CaptureProcessId > 0 ? "正在记录所选应用及其子进程的声音。" : "正在记录。系统声音为所选输出设备混音。");
        }
        catch (Exception ex)
        {
            if (capture != null) { await capture.DisposeAsync(); capture = null; }
            if (engine != null) { await engine.DisposeAsync(); engine = null; }
            voiceTagger?.Dispose(); voiceTagger = null;
            translator?.Dispose(); translator = null;
            ResetLiveTranslation();
            if (meta != null) await Sessions.EndAsync(meta);
            SetStatus(ex.Message, true);
        }
        finally
        {
            if (!Recording) { sessionLease?.Dispose(); sessionLease = null; }
            Working = false;
            RaiseBusy();
        }
    }

    private bool CanStart() => IsIdle && !Answering && ModelReady;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private Task StopRecordingAsync() => StopRecordingCoreAsync();

    private bool CanStop() => Recording && !Stopping;

    private async Task StopRecordingCoreAsync(string? reason = null)
    {
        if (Stopping || CurrentSession is null || !Recording) return;
        Stopping = true;
        candidateTimer.Stop();
        automaticScheduler.Clear();
        RaiseBusy();
        var meta = CurrentSession;
        try
        {
            if (capture != null) { await capture.DisposeAsync(); capture = null; }
            if (engine != null) { await engine.DisposeAsync(); engine = null; }
            await Task.Delay(30);
            translator?.Cancel();
            await Task.WhenAll(writes.ToArray());
            translator?.Dispose(); translator = null;
            ResetLiveTranslation();
            voiceTagger?.Dispose(); voiceTagger = null;
            await Sessions.EndAsync(meta);
            ++epoch;
            SystemPartial = MicrophonePartial = "";
            SetPageSubtitle("已结束 · 会议已归档");
            SetStatus(reason == null ? "会议已保存，可以整理纪要。" : reason + "；已经记录的内容已保存。", reason != null);
        }
        catch (Exception ex) { SetStatus("会议结束时遇到问题：" + ex.Message, true); }
        finally
        {
            translator?.Dispose(); translator = null;
            ResetLiveTranslation();
            voiceTagger?.Dispose(); voiceTagger = null;
            sessionLease?.Dispose(); sessionLease = null;
            StopAutoEnd();
            Recording = Stopping = false;
            ListeningPaused = false;
            RefreshHistory(meta.Id);
            RaiseBusy();
        }
        if (reason == null && !closing && Settings.AutoSummarize && CurrentSession?.Id == meta.Id) await SummarizeCoreAsync(meta);
    }

    private void HandleAudioFailure(string message)
    {
        if (!Recording || Stopping) return;
        SetStatus(message, true);
        _ = StopRecordingCoreAsync(message);
    }

    private void ReceiveTranscript(SessionMeta? meta, TranscriptUpdate update, bool automatic)
    {
        if (meta is null) return;
        // Pause drops updates from this moment; audio capture keeps running, so resuming is instant.
        if (ListeningPaused) return;
        if (update.Source == AudioSource.Microphone && update.Text.Length > 0) lastMicrophoneSpeech = DateTimeOffset.UtcNow;
        if (!update.IsFinal)
        {
            if (update.Source == AudioSource.System)
            {
                SystemPartial = "系统 · " + update.Text;
                if (automatic && Recording && !Stopping) ApplyCandidate(tracker.Ingest(update.Text, false, update.Id, DateTimeOffset.UtcNow));
            }
            else MicrophonePartial = "麦克风 · " + update.Text;
            RequestPartialTranslation(update.Source, update.Text);
            return;
        }
        if (update.Source == AudioSource.System)
        {
            SystemPartial = "";
            SystemPartialTranslation = "";
            remoteLiveTranslation?.Invalidate();
        }
        else
        {
            MicrophonePartial = "";
            MicrophonePartialTranslation = "";
            microphoneLiveTranslation?.Invalidate();
        }
        var echo = IsTextEcho(update, currentLines, meta.InPerson);
        // 术语表：识别错的写法改回正确写法（原话只在内存里更正，识别器本身的输出不动）。
        var text = Canonicalize(update.Text);
        var line = new TranscriptLine
        {
            Id = update.Id, Date = update.Start, StartedAt = update.Start, EndedAt = update.End,
            Speaker = update.Source == AudioSource.System ? "remote" : meta.InPerson ? "room" : "me",
            Text = text, SuspectedEcho = echo ? true : null
        };
        currentLines.Add(line);
        Transcript.Add(new TranscriptRow(line));
        while (Transcript.Count > 400) Transcript.RemoveAt(0);
        var write = Sessions.AppendTranscriptAsync(meta, line);
        writes.Add(write);
        if (voiceTagger is { } tagger && line.Speaker != "me") writes.Add(TagVoiceAsync(meta, line, tagger));
        if (translator is { } translationService) writes.Add(TranslateLineAsync(meta, line, translationService, write));
        _ = write.ContinueWith(task => Dispatcher.UIThread.Post(() => HandleAudioFailure("会议写入失败：" + task.Exception!.GetBaseException().Message)), TaskContinuationOptions.OnlyOnFaulted);
        if (automatic && update.Source == AudioSource.System && Recording && !Stopping) ApplyCandidate(tracker.Ingest(update.Text, true, update.Id, DateTimeOffset.UtcNow));
        NoteSpeechHeard(line);
        OnPropertyChanged(nameof(StatsText));
        OnPropertyChanged(nameof(HasTranscript));
    }

    /// <summary>
    /// 逐字同传：把一次识别快照交给限流的翻译协调器（LiveTranslationCoordinator）。
    /// 只有最新一版译文会显示，过期结果由协调器丢弃；翻译失败不影响转写。
    /// </summary>
    private void RequestPartialTranslation(AudioSource source, string text)
    {
        if (!Recording || translator is not { } service) return;
        var coordinator = source == AudioSource.System ? remoteLiveTranslation : microphoneLiveTranslation;
        if (coordinator is null) return;
        var request = coordinator.Observe(source == AudioSource.System ? "remote" : "me", text, Environment.TickCount64 / 1000.0);
        if (request is null) return;
        _ = Task.Run(async () =>
        {
            try
            {
                var translation = await service.TranslateAsync(Guid.NewGuid(), request.Text).ConfigureAwait(false);
                if (translation is null || !coordinator.Complete(request.Revision, translation.Text)) return;
                var value = coordinator.Translation;
                Dispatcher.UIThread.Post(() =>
                {
                    if (source == AudioSource.System) { SystemPartialTranslation = value; IslandTranslation = value; }
                    else MicrophonePartialTranslation = value;
                });
            }
            catch (Exception)
            {
                // 同传是附加信息：失败时保留原文，不打断会议。
            }
        });
    }

    /// <summary>Prepares the two live-translation channels (called when a translator is created).</summary>
    private void BeginLiveTranslation()
    {
        remoteLiveTranslation = new LiveTranslationCoordinator();
        microphoneLiveTranslation = new LiveTranslationCoordinator();
        SystemPartialTranslation = MicrophonePartialTranslation = IslandTranslation = "";
    }

    private void ResetLiveTranslation()
    {
        remoteLiveTranslation = null;
        microphoneLiveTranslation = null;
        SystemPartialTranslation = MicrophonePartialTranslation = IslandTranslation = "";
    }

    // ---- 会议自动收尾（AppState+AutoEnd.swift → MeetingEndWatch in Core） ----

    [ObservableProperty] private string autoEndNotice = "";
    public bool HasAutoEndNotice => AutoEndNotice.Length > 0;
    partial void OnAutoEndNoticeChanged(string value) => OnPropertyChanged(nameof(HasAutoEndNotice));

    private void BeginAutoEnd(SessionMeta meta)
    {
        var settings = Settings.Build();
        autoEnd = new MeetingEndWatch(
            MeetingEndWatch.Config.FromSettings(settings.MeetingAutoEndMode, settings.MeetingAutoEndMinutes), meta.StartedAt);
        sawSpeech = false;
        lastSpeechEnd = null;
        AutoEndNotice = "";
        autoEndTimer?.Stop();
        autoEndTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        coach = new SpeakingCoach(ScenePreset.For(SelectedMode).Coaching);
        coachSummary = new CoachSummaryAccumulator();
        coachTicks = 0;
        lastNudge = null;
        autoEndTimer.Tick += (_, _) => AutoEndTick();
        autoEndTimer.Start();
    }

    private void StopAutoEnd()
    {
        autoEndTimer?.Stop();
        autoEndTimer = null;
        autoEnd = null;
        coach = null;
        coachSummary = null;
        lastNudge = null;
        AutoEndNotice = "";
    }

    private void NoteSpeechHeard(TranscriptLine line)
    {
        if (!Recording || autoEnd is null) return;
        if (string.IsNullOrWhiteSpace(line.Text)) return;
        sawSpeech = true;
        var end = line.EndedAt ?? line.Date;
        lastSpeechEnd = lastSpeechEnd is { } previous && previous > end ? previous : end;
        autoEnd.Heard(DateTimeOffset.UtcNow);
        AutoEndNotice = "";
        if (coach is not null && CurrentSession is { } session)
        {
            var start = Math.Max(0, ((line.StartedAt ?? line.Date) - session.StartedAt).TotalSeconds);
            var finish = Math.Max(start, (end - session.StartedAt).TotalSeconds);
            var segment = new SpokenSegment(line.Speaker != "remote", start, finish, line.Text);
            coach.Ingest(segment);
            coachSummary?.Ingest(segment);
        }
    }

    [ObservableProperty] private string coachHint = "";
    public bool HasCoachHint => CoachHint.Length > 0;
    partial void OnCoachHintChanged(string value) => OnPropertyChanged(nameof(HasCoachHint));

    /// <summary>临场教练（Scene 里的 SpeakingCoach）：每 5 秒看一次语速、占比与独白时长。</summary>
    private void CoachTick(DateTimeOffset now)
    {
        if (coach is null || CurrentSession is not { } session) return;
        coachTicks++;
        if (coachTicks % 5 != 0) return;
        var snapshot = coach.Snapshot((now - session.StartedAt).TotalSeconds);
        coachSummary?.Observe(snapshot);
        if (snapshot.Nudge is not { } nudge) return;
        if (lastNudge == nudge && (now - lastNudgeAt).TotalSeconds < 90) return;
        lastNudge = nudge;
        lastNudgeAt = now;
        coachSummary?.Record(nudge);
        CoachHint = nudge switch
        {
            CoachNudge.WrapUp => "说了很久了，收个尾，把话交回去。",
            CoachNudge.SlowDown => "语速偏快，适当放慢并加停顿。",
            CoachNudge.LetThemTalk => "这段时间你讲得偏多，留点空间给对方。",
            CoachNudge.SpeakUp => "对方讲得比较多，可以主动补充一句。",
            _ => ""
        };
        if (CoachHint.Length > 0) SetStatus(CoachHint);
    }

    [RelayCommand]
    private void DismissCoach() => CoachHint = "";

    private int autoEndTicks;

    private void AutoEndTick()
    {
        if (!Recording || Stopping || autoEnd is null || CurrentSession is not { } meta) return;
        autoEndTicks++;
        var now = DateTimeOffset.UtcNow;
        CoachTick(now);
        var settings = Settings.Build();
        var reason = autoEnd.Tick(now, sawSpeech, settings.MeetingAutoEndMode == "off");
        if (reason is not null || autoEndTicks % 15 == 0)
            GlobalHotkeys.Log($"autoend tick={autoEndTicks} mode={settings.MeetingAutoEndMode} minutes={settings.MeetingAutoEndMinutes} raw={Settings.AutoEndMinutes} selected={Settings.SelectedAutoEndMode?.Value} said={sawSpeech} quiet={Math.Round((now - autoEnd.QuietSince).TotalSeconds)}s reason={reason}");
        if (reason is { } value)
        {
            // The record ends where the talking ended, not minutes of silence later.
            if (value == MeetingEndWatch.Reason.Silence)
                meta.EndedAt = MeetingEndWatch.EndTime(lastSpeechEnd, meta.StartedAt, now);
            var message = value == MeetingEndWatch.Reason.Silence
                ? "长时间没有新的发言，会议已自动结束。"
                : "这场会议没有任何发言，已自动停止。";
            AutoEndNotice = message;
            StopAutoEnd();
            _ = StopRecordingCoreAsync(message);
            return;
        }
        if (autoEnd.Remaining(now) is { } remaining)
        {
            AutoEndNotice = $"{(int)Math.Ceiling(remaining)} 秒后自动结束；说话或点「继续记录」可取消。";
            SetStatus(AutoEndNotice);
        }
    }

    [RelayCommand]
    private void KeepRecording()
    {
        autoEnd?.KeepGoing(DateTimeOffset.UtcNow);
        AutoEndNotice = "";
        SetStatus("继续记录。");
    }

    /// <summary>
    /// Text-level echo backstop (Core EchoTextFilter, ported from EchoSuppression.swift): a
    /// microphone line that repeats a nearby system line is the speakers being re-recorded.
    /// </summary>
    private static bool IsTextEcho(TranscriptUpdate update, IReadOnlyList<TranscriptLine> lines, bool inPerson)
    {
        if (inPerson || update.Source != AudioSource.Microphone) return false;
        var remote = lines.Where(line => line.Speaker == "remote").TakeLast(12)
            .Select(line => new TimedLine(line.Text, line.StartedAt ?? line.Date, line.EndedAt ?? line.Date))
            .ToList();
        return EchoTextFilter.IsEcho(update.Text, update.Start, update.End, remote, 0);
    }

    private async Task TagVoiceAsync(SessionMeta meta, TranscriptLine line, MeetingVoiceTagger tagger)
    {
        try
        {
            var voice = await tagger.LabelAsync(line.StartedAt ?? line.Date, line.EndedAt ?? line.Date);
            if (voice is null) return;
            await Sessions.AppendVoiceTagAsync(meta.Id, line.Id, voice.Value);
            if (CurrentSession?.Id != meta.Id) return;
            var index = currentLines.FindIndex(l => l.Id == line.Id);
            if (index < 0) return;
            var tagged = currentLines[index] with { Voice = voice };
            currentLines[index] = tagged;
            var row = index - (currentLines.Count - Transcript.Count);
            if (row >= 0 && row < Transcript.Count) Transcript[row] = new TranscriptRow(tagged);
        }
        catch (Exception ex) { SetStatus("说话人区分暂不可用，转写仍保留：" + ex.Message, true); }
    }

    private async Task TranslateLineAsync(SessionMeta meta, TranscriptLine line, MeetingTranslator service, Task? originalWrite = null, CancellationToken ct = default)
    {
        try
        {
            var translation = await service.TranslateAsync(line.Id, line.Text, ct);
            if (translation is null) return;
            if (originalWrite != null) await originalWrite;
            await Sessions.AppendTranslationAsync(meta.Id, translation, ct);
            if (CurrentSession?.Id != meta.Id) return;
            var index = currentLines.FindIndex(l => l.Id == line.Id && l.Text == line.Text);
            if (index < 0) return;
            var translated = currentLines[index] with { Translation = translation };
            currentLines[index] = translated;
            var row = index - (currentLines.Count - Transcript.Count);
            if (row >= 0 && row < Transcript.Count) Transcript[row] = new TranscriptRow(translated);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) { SetStatus("译文未生成，原文已保留，可稍后补译：" + ex.Message, true); }
    }

    [RelayCommand(CanExecute = nameof(CanTranslate))]
    private async Task TranslateMissingAsync()
    {
        if (CurrentSession is not { } meta || currentLines.Count == 0) return;
        var pending = currentLines.Where(l => l.Translation is null).ToArray();
        await RunWorkAsync(async ct =>
        {
            using var service = new MeetingTranslator(CurrentClient(), AnswerOptions());
            foreach (var line in pending)
            {
                ct.ThrowIfCancellationRequested();
                await TranslateLineAsync(meta, line, service, ct: ct);
            }
            SetStatus("补译已结束；有译文的句子已分别保存。未成功的句子可再次补译。");
        });
    }

    private bool CanTranslate() => IsIdle && !Answering && currentLines.Count > 0;

    public AnswerOptions AnswerOptions(bool notes = false)
    {
        var settings = Settings.Build();
        if (settings.UseCodex) return new("https://codex.local", settings.CodexModel.Length > 0 ? settings.CodexModel : "default", "");
        return new(settings.BaseUrl, notes && settings.NotesModel.Length > 0 ? settings.NotesModel : settings.Model, Settings.ApiKey.Trim(), settings.ReasoningEffort);
    }

    public IAnswerClient CurrentClient() => Settings.UseCodex ? new CodexAnswerClient() : Api;

    private IWebSearchProvider? webSearchProvider;
    private bool webSearchResolved;

    private IWebSearchProvider? WebSearch()
    {
        if (webSearchResolved) return webSearchProvider;
        webSearchResolved = true;
        try { webSearchProvider = WebSearchConfiguration.CreateProvider(); }
        catch { webSearchProvider = null; }
        return webSearchProvider;
    }

    [RelayCommand(CanExecute = nameof(CanAsk))]
    private async Task AskAsync()
    {
        var question = QuestionInput.Trim();
        if (question.Length == 0) { SetStatus("请先输入问题。"); return; }
        await AnswerAsync(question, false);
    }

    private bool CanAsk() => !Working && !Stopping;

    public async Task AnswerAsync(string question, bool automatic, bool check = false)
    {
        if (!automatic && !check) automaticScheduler.Clear();
        CancelRequest();
        var generation = ++requestEpoch;
        var cancellation = new CancellationTokenSource();
        requestCancellation = cancellation;
        Answering = true;
        ProgressIndeterminate = true;
        lastRequestAutomatic = automatic;
        var meta = CurrentSession;
        var options = AnswerOptions();
        var clock = Stopwatch.StartNew();
        try
        {
            if (!check) { DetectedQuestion = question; if (automatic) { } else QuestionInput = ""; AnswerText = ""; }
            var context = string.Join("\n", currentLines.Where(x => x.SuspectedEcho != true).TakeLast(40).Select(x => $"[{x.Date.ToLocalTime():HH:mm:ss}] {x.Speaker}：{x.Text}"));
            if (context.Length > 18000) context = context[^18000..];
            SetStatus(check ? "正在检查回答服务…" : "正在生成参考回答…");
            var progress = new Progress<string>(text =>
            {
                if (generation == requestEpoch && !check) AnswerText = text;
            });
            RetrievalResult? retrieved = null;
            if (!check && Settings.UseMaterials)
            {
                SetStatus("正在检索相关资料；首次使用时生成本地索引…");
                retrieved = await Retrieval.SearchAsync(question, Settings.UseSemanticRetrieval, cancellation.Token,
                    documents: Library.DocumentsForRetrieval());
                if (generation != requestEpoch) return;
                RetrievalInfo = $"{retrieved.Method} · {retrieved.Sources.Count} 段 · {retrieved.Seconds:F2} 秒" + (retrieved.Warning is null ? "" : " · " + retrieved.Warning);
            }
            else if (!check) RetrievalInfo = "未使用资料库";
            var sources = retrieved?.Sources ?? [];
            if (!check) { Sources.Clear(); }
            IReadOnlyList<WebResult> web = [];
            if (!check && Settings.WebSearch && WebSearchNeed.Assess(question).NeedsSearch && WebSearch() is { } searchProvider)
            {
                SetStatus("正在联网核实…");
                var webResponse = await searchProvider.SearchAsync(question, 5, cancellation.Token);
                if (webResponse.Error is null) web = webResponse.Results;
                else SetStatus(webResponse.Error);
            }
            var result = await CurrentClient().AnswerAsync(options,
                check ? "严格按用户要求回复。" : BuildAnswerSystemPrompt(meta),
                check ? question : BuildAnswerUserPrompt(meta, context, sources, question, web),
                text => ((IProgress<string>)progress).Report(text), cancellation.Token);
            if (generation != requestEpoch) return;
            if (!check)
            {
                var cited = KeywordIndex.Cited(result, sources);
                Sources.Clear();
                foreach (var citation in cited) Sources.Add(citation);
                AnswerText = result;
                companionHistory.Add((question, result));
                companionHistoryIndex = companionHistory.Count - 1;
                if (meta is null)
                {
                    meta = NewMeta(Settings.Build(), question.Length > 40 ? question[..40] : question);
                    meta.AudioSource = "text-question";
                    await Sessions.CreateAsync(meta);
                    await Sessions.EndAsync(meta);
                    CurrentSession = meta;
                }
                meta.AnswerCount += 1;
                await Sessions.AppendAnswerAsync(meta, new AnswerRecord
                {
                    Question = question, Answer = result, Model = options.Model,
                    Seconds = clock.Elapsed.TotalSeconds, Automatic = automatic, Sources = cited
                });
                RefreshHistory();
            }
            tracker.HasPriorAnswer = true;
            SetStatus(check ? "回答服务连接成功：" + result : "回答已完成。会议内的回答已归档。");
        }
        catch (OperationCanceledException) { if (generation == requestEpoch) SetStatus("回答已取消。"); }
        catch (Exception ex) { if (generation == requestEpoch) SetStatus(ex.Message, true); }
        finally
        {
            cancellation.Dispose();
            if (generation == requestEpoch)
            {
                requestCancellation = null;
                Answering = false;
                ProgressIndeterminate = false;
                RaiseBusy();
            }
        }
    }

    private const string AnswerPrompt =
        "你是会议中的参考助手。根据已提供的转写回答当前问题，使用简洁中文；问题为英文时可用英文。" +
        "区分事实与建议，不编造未提供的文件、数据、决定或经历。参考资料库中的具体事实必须有对应摘录支持并标注引用；" +
        "资料和转写均未提供所需事实时，明确说明资料不足，不猜测金额、日期、人员或参数。";

    /// <summary>
    /// System prompt plus the accepted lessons of this meeting's series (Mac
    /// Workspace.experienceNotes): pinned first, then newest, capped at 2,000 characters.
    /// </summary>
    private string BuildAnswerSystemPrompt(SessionMeta? meta)
    {
        var experience = ExperiencePromptFor(meta);
        var skills = SkillPrompt(meta);
        var prompt = experience.Length == 0
            ? AnswerPrompt
            : AnswerPrompt + "\n\n这个系列已经确认的经验（优先遵守）：\n" + experience;
        return skills.Length == 0 ? prompt : prompt + "\n\n用户加载的技能（按适用部分执行）：\n" + skills;
    }

    /// <summary>User prompt with the series question bank as extra reference material.</summary>
    private IReadOnlyList<SlidePageInfo> slides = [];
    private int slideIndex = -1;

    public string SlideStatus => slides.Count == 0
        ? "未导入 PPT"
        : slideIndex < 0 ? $"{slides.Count} 页" : $"第 {slides[slideIndex].Number} 页 / {slides.Count}";

    private SlidePageInfo? CurrentSlide
    {
        get
        {
            if (slides.Count == 0) slides = SlideDecks.FromDocuments(Library.DocumentsForRetrieval());
            return slideIndex >= 0 && slideIndex < slides.Count ? slides[slideIndex] : null;
        }
    }

    [RelayCommand]
    private void PreviousSlide() => MoveSlide(-1);

    [RelayCommand]
    private void NextSlide() => MoveSlide(1);

    private void MoveSlide(int delta)
    {
        if (slides.Count == 0) slides = SlideDecks.FromDocuments(Library.DocumentsForRetrieval());
        if (slides.Count == 0) { SetStatus("还没有导入 PPTX，导入后可翻页。", true); return; }
        slideIndex = Math.Clamp(slideIndex + delta, 0, slides.Count - 1);
        OnPropertyChanged(nameof(SlideStatus));
        SetStatus($"幻灯片：{SlideStatus} · {slides[slideIndex].Text[..Math.Min(80, slides[slideIndex].Text.Length)]}");
    }

    private string BuildAnswerUserPrompt(SessionMeta? meta, string transcript, IReadOnlyList<SourceCitation> sources, string question,
        IReadOnlyList<WebResult>? web = null)
    {
        var slide = CurrentSlide is { } page ? $"\n当前幻灯片（第 {page.Number} 页，{page.DocumentName}）：\n{page.Text}\n" : "";
        var bank = QuestionBankPromptFor(meta);
        var bankBlock = bank.Length == 0 ? "" : $"\n这个系列的题库（历次复盘确认过的回答）：\n{bank}\n";
        var webBlock = web is { Count: > 0 }
            ? "\n联网搜索结果（可能过时，引用时标注链接）：\n" + string.Join("\n", web.Select(result =>
                $"- {result.Title}（{result.Url}）：{result.Snippet}")) + "\n"
            : "";
        return $"最近的会议转写：\n{transcript}\n\n参考资料：\n{KeywordIndex.Context(sources)}\n{slide}{bankBlock}{webBlock}\n当前问题：{question}";
    }

    [RelayCommand(CanExecute = nameof(CanSummarize))]
    private async Task SummarizeAsync()
    {
        if (CurrentSession is { } meta) await SummarizeCoreAsync(meta);
    }

    private bool CanSummarize() => IsIdle && !Answering && CurrentSession != null && currentLines.Count > 0;

    public async Task SummarizeCoreAsync(SessionMeta meta)
    {
        if (Recording || Working || Summarizing) return;
        CancelRequest();
        var generation = ++requestEpoch;
        var cancellation = new CancellationTokenSource();
        requestCancellation = cancellation;
        Answering = Summarizing = true;
        ProgressIndeterminate = true;
        Section = AppSection.Notes;
        RaiseBusy();
        try
        {
            await Task.WhenAll(writes.ToArray());
            var options = AnswerOptions(true);
            var progress = new Progress<SummaryProgress>(p =>
            {
                if (generation != requestEpoch) return;
                SummaryText = p.Text;
                SetStatus($"{p.Stage} · {p.Completed}/{p.Total}");
            });
            var markdown = await new SummaryService(Sessions, CurrentClient()).GenerateAsync(meta.Id, options,
                p => ((IProgress<SummaryProgress>)progress).Report(p), cancellation.Token);
            if (generation == requestEpoch)
            {
                SummaryText = markdown;
                SetStatus("纪要已保存，可导出 Markdown。");
            }
        }
        catch (OperationCanceledException)
        {
            if (generation == requestEpoch) SetStatus("纪要整理已取消；原始转写和已完成片段已保留，可继续整理。");
        }
        catch (Exception ex)
        {
            if (generation == requestEpoch) SetStatus(ex.Message, true);
        }
        finally
        {
            cancellation.Dispose();
            Summarizing = false;
            if (generation == requestEpoch)
            {
                requestCancellation = null;
                Answering = false;
                ProgressIndeterminate = false;
                RefreshHistory(meta.Id);
                RaiseBusy();
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync()
    {
        if (CurrentSession is null || PickSaveFile is null) return;
        var name = Regex.Replace(CurrentSession.Title, "[<>:\"/\\\\|?*]", "_") + ".md";
        if (await PickSaveFile("Markdown (*.md)|*.md", name) is not { } file) return;
        try
        {
            await Task.WhenAll(writes.ToArray());
            await Sessions.ExportMarkdownAsync(CurrentSession.Id, file);
            SetStatus("会议已导出。");
        }
        catch (Exception ex) { SetStatus(ex.Message, true); }
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportDocxAsync()
    {
        if (CurrentSession is null || PickSaveFile is null) return;
        var name = Regex.Replace(CurrentSession.Title, "[<>:\"/\\|?*]", "_") + ".docx";
        if (await PickSaveFile("Word 文档 (*.docx)|*.docx", name) is not { } file) return;
        try
        {
            await Task.WhenAll(writes.ToArray());
            await Sessions.ExportDocxAsync(CurrentSession.Id, file);
            SetStatus("会议已导出为 Word 文档。");
        }
        catch (Exception ex) { SetStatus(ex.Message, true); }
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportPdfAsync()
    {
        if (CurrentSession is null || PickSaveFile is null) return;
        var name = Regex.Replace(CurrentSession.Title, "[<>:\"/\\|?*]", "_") + ".pdf";
        if (await PickSaveFile("PDF (*.pdf)|*.pdf", name) is not { } file) return;
        try
        {
            await Task.WhenAll(writes.ToArray());
            await Sessions.ExportPdfAsync(CurrentSession.Id, file);
            SetStatus("会议已导出为 PDF。");
        }
        catch (Exception ex) { SetStatus(ex.Message, true); }
    }

    private void LoadShortcutOverrides()
    {
        try { shortcutOverrides = new Dictionary<string, string>(SettingsStore.Load().Shortcuts ?? []); }
        catch { shortcutOverrides = []; }
        ShortcutText = BuildShortcutText();
    }

    private string BuildShortcutText()
    {
        var lines = new List<string>();
        foreach (var action in ShortcutKeys.Actions)
        {
            var binding = ShortcutKeys.ParseBinding(shortcutOverrides.GetValueOrDefault(action.Id), action.Default);
            lines.Add(action.Id + "=" + ShortcutKeys.Format(binding.Modifiers, binding.Key));
        }
        return string.Join("\n", lines);
    }

    /// <summary>The configured key for an action, or its built-in default.</summary>
    public uint ShortcutKey(string id, uint fallback) =>
        ShortcutKeys.ParseBinding(shortcutOverrides.GetValueOrDefault(id), fallback).Key;

    public (uint Modifiers, uint Key) ShortcutBinding(string id, uint fallbackKey) =>
        ShortcutKeys.ParseBinding(shortcutOverrides.GetValueOrDefault(id), fallbackKey);

    [RelayCommand]
    private async Task SaveShortcutsAsync()
    {
        try
        {
            var known = ShortcutKeys.Actions.Select(action => action.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var map = new Dictionary<string, string>();
            foreach (var raw in ShortcutText.Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                var separator = line.IndexOf('=');
                if (separator <= 0) throw new FormatException("快捷键行要写成 动作=按键，例如 record=R。");
                var id = line[..separator].Trim();
                if (!known.Contains(id)) throw new FormatException("未知快捷键动作：" + id);
                var binding = ShortcutKeys.ParseBinding(line[(separator + 1)..], 0);
                if (binding.Key == 0) throw new FormatException("无法识别按键：" + line[(separator + 1)..].Trim());
                map[id] = ShortcutKeys.Format(binding.Modifiers, binding.Key);
            }
            var settings = SettingsStore.Load();
            settings.Shortcuts = map;
            await SettingsStore.SaveAsync(settings);
            shortcutOverrides = map;
            ShortcutText = BuildShortcutText();
            ShortcutsChanged?.Invoke();
            SetStatus("全局快捷键已保存并重新注册。");
        }
        catch (Exception ex) { SetStatus("快捷键保存失败：" + ex.Message, true); }
    }

    private bool CanExport() => !Working && !Recording && CurrentSession != null;
    [RelayCommand(CanExecute = nameof(CanExportSubtitles))]
    private Task ExportSrtAsync() => ExportSubtitlesAsync(SessionSubtitleFormat.Srt);

    [RelayCommand(CanExecute = nameof(CanExportSubtitles))]
    private Task ExportVttAsync() => ExportSubtitlesAsync(SessionSubtitleFormat.Vtt);

    /// <summary>SRT/VTT from the archived transcript (SessionSubtitleExporter in Core).</summary>
    private async Task ExportSubtitlesAsync(SessionSubtitleFormat format)
    {
        if (CurrentSession is not { } meta || PickSaveFile is null) return;
        var extension = format == SessionSubtitleFormat.Srt ? "srt" : "vtt";
        var name = Regex.Replace(meta.Title, "[<>:\"/\\\\|?*]", "_") + "." + extension;
        if (await PickSaveFile($"{extension.ToUpperInvariant()} 字幕 (*.{extension})|*.{extension}", name) is not { } file) return;
        try
        {
            var content = SubtitleExportMode switch
            {
                "原文" => SessionSubtitleContent.Original,
                "译文" => SessionSubtitleContent.Translation,
                _ => SessionSubtitleContent.Bilingual
            };
            var export = SessionSubtitleExporter.Export(meta, currentLines, format, content);
            await File.WriteAllTextAsync(file, export.Text, new System.Text.UTF8Encoding(false));
            var estimated = export.EstimatedTimingCount > 0 ? $"，其中 {export.EstimatedTimingCount} 条按文字长度估算结束时间" : "";
            SetStatus(UiText.EnglishUi
                ? $"Subtitles exported ({extension.ToUpperInvariant()}){estimated}."
                : $"字幕已导出（{extension.ToUpperInvariant()}）{estimated}。");
        }
        catch (Exception ex) { SetStatus(ex.Message, true); }
    }

    private bool CanExportSubtitles() => !Working && CurrentSession != null && currentLines.Count > 0;

    /// <summary>The current session's transcript lines (read-only, for exports and self-checks).</summary>
    public IReadOnlyList<TranscriptLine> CurrentLines => currentLines;

    /// <summary>SRT/VTT for the current session without a file dialog (self-check path).</summary>
    public SessionSubtitleExport ExportCurrentSubtitles(SessionSubtitleFormat format) =>
        CurrentSession is { } meta
            ? SessionSubtitleExporter.Export(meta, currentLines, format)
            : new SessionSubtitleExport("", 0, 0);

    [RelayCommand(CanExecute = nameof(CanImportWave))]
    private async Task ImportWaveAsync()
    {
        if (PickOpenFile is null) return;
        if (await PickOpenFile("WAV 录音 (*.wav)|*.wav") is not { } file) return;
        await ImportWaveFileAsync(file);
    }

    /// <summary>Transcribe a WAV into a new archived session (also used by the unattended QA switch).</summary>
    public async Task ImportWaveFileAsync(string file)
    {
        await RunWorkAsync(async ct =>
        {
            await Settings.SaveAsync();
            await Settings.RefreshDevicesAsync();
            var settings = Settings.Build();
            CancelRequest();
            ResetDisplay();
            var meta = NewMeta(settings, Path.GetFileNameWithoutExtension(file));
            meta.AudioSource = "imported-wav";
            await Sessions.CreateAsync(meta);
            using var lease = Sessions.AcquireLease(meta.Id);
            CurrentSession = meta;
            var generation = ++epoch;
            MeetingTitle = meta.Title;
            SetPageSubtitle("正在本机读取录音…");
            Section = AppSection.Live;
            SetStatus("正在加载模型并转写 WAV…");
            try
            {
                if (settings.SeparateSpeakers) voiceTagger = await Task.Run(() => new MeetingVoiceTagger(Paths, AudioSource.System), ct);
                if (settings.TranslateSpeech) translator = new(CurrentClient(), AnswerOptions());
                BeginLiveTranslation();
                var tagger = voiceTagger;
                await Task.Run(() =>
                {
                    using var recognizer = new SpeechRecognizerSession(Models, settings, RecognitionHotwords());
                    recognizer.FrameObserved += frame => tagger?.Add(frame);
                    recognizer.Updated += update => Dispatcher.UIThread.Post(() => { if (generation == epoch) ReceiveTranscript(meta, update, false); });
                    recognizer.TranscribeWave(file, AudioSource.System, ct);
                }, ct);
                SetStatus("录音已转写并归档，未保存原始音频。");
            }
            finally
            {
                await Task.WhenAll(writes.ToArray());
                translator?.Dispose(); translator = null;
                ResetLiveTranslation();
                voiceTagger?.Dispose(); voiceTagger = null;
                await Sessions.EndAsync(meta);
                ++epoch;
                SystemPartial = MicrophonePartial = "";
                SetPageSubtitle("录音转写 · 已归档");
                RefreshHistory(meta.Id);
            }
        });
    }

    private bool CanImportWave() => IsIdle && !Answering && ModelReady;

    [RelayCommand(CanExecute = nameof(CanBackup))]
    private async Task BackupAsync()
    {
        if (PickSaveFile is null) return;
        if (await PickSaveFile("NextThought 备份|*.ntbackup", "NextThought-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".ntbackup") is not { } file) return;
        await RunWorkAsync(async ct =>
        {
            await Task.WhenAll(writes.ToArray());
            await Task.Run(() => new BackupService(Paths).ExportAsync(file, ct), ct);
            SetStatus("备份已导出：包含会议与资料；不包含 API Key、语音模型和设备设置。");
        });
    }

    private bool CanBackup() => IsIdle && !Answering;

    [RelayCommand(CanExecute = nameof(CanRestore))]
    private async Task RestoreAsync()
    {
        if (PickOpenFile is null) return;
        if (await PickOpenFile("NextThought 备份|*.ntbackup;*.ihbackup") is not { } file) return;
        await RunWorkAsync(async ct =>
        {
            var result = await Task.Run(() => new BackupService(Paths).ImportAsync(file, ct), ct);
            RefreshHistory();
            Library.Refresh();
            SetStatus($"已导入 {result.SessionsAdded} 场会议、{result.MaterialsAdded} 份资料、{result.WorkspacesAdded} 个系列" + (result.SettingsRestored ? "，并恢复设置" : "") + $"；{result.ConflictsSkipped} 个重复项保留现有版本。导入前备份：{result.SafetyBackup}。");
        });
    }

    private bool CanRestore() => IsIdle && !Answering;

    [RelayCommand(CanExecute = nameof(CanSaveSettings))]
    private async Task SaveSettingsAsync()
    {
        try
        {
            await Settings.SaveAsync();
            LanguageChanged?.Invoke(Settings.UiLanguage == "English");
            ApplyAppearance();
            RefreshModelState();
            SetStatus("设置已保存，密钥已用当前 Windows 用户加密。");
        }
        catch (Exception ex) { SetStatus(ex.Message, true); }
    }

    private bool CanSaveSettings() => IsIdle;

    private void ApplyAppearance()
    {
        if (Avalonia.Application.Current is not { } app) return;
        app.RequestedThemeVariant = Settings.Build().Appearance switch
        {
            "dark" => Avalonia.Styling.ThemeVariant.Dark,
            "light" => Avalonia.Styling.ThemeVariant.Light,
            _ => Avalonia.Styling.ThemeVariant.Default
        };
    }

    [RelayCommand]
    private async Task CheckServiceAsync() => await AnswerAsync("请只回复：连接成功", false, true);

    [RelayCommand]
    private void Cancel() { workCancellation?.Cancel(); requestCancellation?.Cancel(); translator?.Cancel(); }

    /// <summary>纪要窗口 → 字幕条 → 隐藏 → 纪要窗口 … (LiveWindowMode in Core).</summary>
    [RelayCommand]
    private void CycleLiveMode() => LiveMode = LiveMode.Next();

    public string LiveModeLabel => LiveMode.Title();

    [RelayCommand]
    private void TogglePauseListening()
    {
        if (!Recording)
        {
            SetStatus("开始记录后才能暂停聆听。");
            return;
        }
        ListeningPaused = !ListeningPaused;
        SetStatus(ListeningPaused ? "聆听已暂停；恢复后从当前时刻继续记录。" : "聆听已恢复。");
    }

    [RelayCommand]
    private async Task ForceAnswerAsync()
    {
        if (Answering) return;
        var question = !string.IsNullOrWhiteSpace(DetectedQuestion)
            ? DetectedQuestion
            : currentLines.LastOrDefault()?.Text.Trim() ?? "";
        if (question.Length == 0)
        {
            SetStatus("还没有可回答的转写。");
            return;
        }
        await AnswerAsync(question, false);
    }

    [RelayCommand]
    private async Task RegenerateAnswerAsync()
    {
        if (Answering) return;
        await AnswerAsync(string.IsNullOrWhiteSpace(DetectedQuestion) ? "请总结当前讨论" : DetectedQuestion, false);
    }

    [RelayCommand]
    private async Task ShorterAnswerAsync()
    {
        if (Answering) return;
        await AnswerAsync("请把上一条回答压缩到三句话以内，只保留结论。", false);
    }

    [RelayCommand]
    private void PreviousAnswer() => ShowCompanionHistory(-1);

    [RelayCommand]
    private void NextAnswer() => ShowCompanionHistory(1);

    [RelayCommand]
    private void CopyAnswer()
    {
        if (AnswerText.Length == 0) return;
        _ = CopyText?.Invoke(AnswerText);
        SetStatus("回答已复制。");
    }

    [RelayCommand]
    private void OpenData() => OpenExternal(Paths.Root);

    [RelayCommand(CanExecute = nameof(CanNewMeeting))]
    private void NewMeeting()
    {
        if (Recording || Working) return;
        CancelRequest();
        CurrentSession = null;
        ResetDisplay();
        MeetingTitle = UiText.EnglishUi ? "New meeting" : "新会议";
        SetPageSubtitle("选择声音来源，开始记录。");
        Section = AppSection.Live;
        RaiseBusy();
    }

    private bool CanNewMeeting() => IsIdle && !Answering;

    public void OpenMaterial(SourceCitation citation) => Library.OpenCitation(citation);

    public void OpenExternal(string target)
    {
        try
        {
            var psi = target.StartsWith("http", StringComparison.OrdinalIgnoreCase) || target.StartsWith("mailto", StringComparison.OrdinalIgnoreCase)
                ? new ProcessStartInfo(target) { UseShellExecute = true }
                : Directory.Exists(target)
                    ? new ProcessStartInfo("explorer.exe", target) { UseShellExecute = true }
                    : new ProcessStartInfo(target) { UseShellExecute = true };
            Process.Start(psi);
        }
        catch (Exception ex) { SetStatus(ex.Message, true); }
    }

    public async Task SelectHistoryAsync(SessionMeta? meta)
    {
        if (!initialized || Recording || Working || meta is null) return;
        var generation = ++selectionEpoch;
        CancelRequest();
        try
        {
            var data = await Task.Run(() => Sessions.Load(meta.Id));
            if (generation != selectionEpoch) return;
            CurrentSession = data.Meta;
            var lines = data.Lines;
            try
            {
                var userContent = SessionUserContentStore.Load(Sessions.Folder(data.Meta.Id));
                if (userContent.CorrectionDecisions.Count > 0)
                {
                    var corrections = userContent.Corrections([], lines);
                    if (corrections.Count > 0) lines = NotesCorrections.Apply(corrections, lines);
                }
            }
            catch (Exception) { /* a damaged user-content file leaves the raw transcript visible */ }
            currentLines.Clear();
            currentLines.AddRange(lines);
            Transcript.Clear();
            foreach (var line in lines.TakeLast(400)) Transcript.Add(new TranscriptRow(line));
            SystemPartial = MicrophonePartial = "";
            MeetingTitle = data.Meta.Title;
            PageSubtitle = (UiText.EnglishUi ? "Meeting archived · " : "会议归档 · ") + data.Meta.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            Sources.Clear();
            foreach (var source in data.Answers.LastOrDefault()?.Sources ?? []) Sources.Add(source);
            RetrievalInfo = data.Answers.LastOrDefault()?.Sources?.Count > 0 ? "历史回答的引用已保存，可查看来源。" : "";
            AnswerText = data.Answers.LastOrDefault()?.Answer ?? "";
            DetectedQuestion = data.Answers.LastOrDefault()?.Question ?? UiText.T("可以继续对这场会议提问。");
            SummaryText = data.Summary.Length > 0 ? data.Summary : UiText.T("这场会议还没有纪要，点击上方整理。");
            // Each scene recommends a notes format (plan §8 纪要格式); the cached copy is loaded with it.
            var mode = Enum.TryParse<MeetingMode>(data.Meta.Mode, true, out var parsed) ? parsed : MeetingMode.Meeting;
            SelectedNotesFormat = NotesFormats.Recommended(mode);
            StructuredMarkdown = "";
            OnPropertyChanged(nameof(HasStructuredNotes));
            OnPropertyChanged(nameof(NotesFormatIsStandard));
            OnPropertyChanged(nameof(NotesFormatIsFishbone));
            OnPropertyChanged(nameof(NotesFormatIsOutline));
            OnPropertyChanged(nameof(NotesFormatIsViewpoints));
            OnPropertyChanged(nameof(StructuredHint));
            await LoadStructuredAsync(data.Meta, SelectedNotesFormat);
            Section = AppSection.Live;
            RefreshHistory(meta.Id);
            RaiseBusy();
            OnPropertyChanged(nameof(HasTranscript));
            OnPropertyChanged(nameof(StatsText));
        }
        catch (Exception ex) { SetStatus(ex.Message, true); }
    }

    public void ResetDisplay()
    {
        ++selectionEpoch;
        Transcript.Clear();
        currentLines.Clear();
        writes.Clear();
        SystemPartial = MicrophonePartial = "";
        SystemPartialTranslation = MicrophonePartialTranslation = IslandTranslation = "";
        RetrievalInfo = "";
        Sources.Clear();
        AnswerText = "";
        SummaryText = "结束会议后，按转写整理总结、决定、待办和未决问题。";
        DetectedQuestion = "手动提问，或开启自动回答。";
        tracker = new QuestionCandidateTracker { Mode = MeetingMode.Meeting };
        automaticScheduler = new AutomaticAnswerScheduler();
        lastMicrophoneSpeech = default;
        systemLevel = microphoneLevel = 0;
        LevelText = "";
        OnPropertyChanged(nameof(HasTranscript));
        OnPropertyChanged(nameof(StatsText));
    }

    private void CancelRequest()
    {
        ++requestEpoch;
        var previous = requestCancellation;
        requestCancellation = null;
        previous?.Cancel();
        Answering = false;
    }

    private async Task RunWorkAsync(Func<CancellationToken, Task> operation)
    {
        if (Working || Recording) return;
        Working = true;
        workCancellation = new CancellationTokenSource();
        RaiseBusy();
        try
        {
            workTask = operation(workCancellation.Token);
            await workTask;
        }
        catch (OperationCanceledException) { SetStatus("已取消，已经记录的内容仍保存在本机。"); }
        catch (Exception ex) { SetStatus(ex.Message, true); }
        finally
        {
            workTask = null;
            workCancellation.Dispose();
            workCancellation = null;
            Working = false;
            Progress = 0;
            RefreshModelState();
            RaiseBusy();
        }
    }

    public Task RunWork(Func<CancellationToken, Task> operation) => RunWorkAsync(operation);

    [RelayCommand]
    private async Task PrepareModelsAsync()
    {
        await RunWorkAsync(async ct =>
        {
            var settings = Settings.Build();
            await Settings.SaveAsync();
            var progress = new Progress<ModelProgress>(p => { Progress = p.Fraction; SetStatus(p.Message); });
            await Models.InstallAsync(settings.LocalModel, progress, ct);
            if (settings.SecondPass) await Models.InstallAsync("xasrOffline", progress, ct);
            SetStatus("模型已下载并校验，可以离线记录。");
        });
    }

    [RelayCommand]
    private async Task PrepareSpeakersAsync() => await RunWorkAsync(async ct =>
    {
        var progress = new Progress<ModelProgress>(p => { Progress = p.Fraction; SetStatus(p.Message); });
        await new SpeakerModelStore(Paths).InstallAsync(progress, ct);
        SetStatus("声纹模型已准备，可在设置中开启并保存。");
    });

    [RelayCommand]
    private async Task PrepareRetrievalAsync() => await RunWorkAsync(async ct =>
    {
        var progress = new Progress<ModelProgress>(p => { Progress = p.Fraction; SetStatus(p.Message); });
        await new EmbeddingModelStore(Paths).InstallAsync(progress, ct);
        SetStatus("语义检索模型已准备，勾选语义检索并保存设置后使用。");
    });

    private void LatchCompanionCommandState() => OnPropertyChanged(nameof(CompanionRunning));

    // ---- 手机伴侣（方案第 12 节，局域网通道；平台层 CompanionServer 已验收） ----

    private readonly List<(string Question, string Answer)> companionHistory = [];
    private int companionHistoryIndex = -1;
    private CompanionServer? companion;
    private CompanionRelayClient? relay;

    [RelayCommand]
    private void RefreshCompanionAddresses()
    {
        CompanionAddresses.Clear();
        foreach (var address in CompanionServer.Addresses()) CompanionAddresses.Add(address);
        SelectedCompanionAddress = CompanionAddresses.FirstOrDefault();
        CompanionInfo = CompanionAddresses.Count == 0
            ? "没有找到可用的局域网 IPv4 地址；请先连接 Wi-Fi 或以太网。"
            : "默认关闭；开启后手机与电脑需在同一可信网络。首次监听时 Windows 防火墙会询问，请只允许专用网络。";
    }

    [RelayCommand(CanExecute = nameof(CanToggleCompanion))]
    private async Task ToggleCompanionAsync()
    {
        if (companion is not null || relay is not null)
        {
            if (companion is not null) { await companion.DisposeAsync(); companion = null; }
            if (relay is not null) { await relay.DisposeAsync(); relay = null; }
            CompanionRunning = false;
            CompanionQr = null;
            CompanionLink = "";
            SetStatus("手机伴侣已关闭。");
            return;
        }
        var relayUrl = Settings.Build().CompanionRelayUrl.Trim();
        var address = SelectedCompanionAddress;
        if (address is null && relayUrl.Length == 0)
        {
            SetStatus("请先选择一个局域网地址，或在设置里填写中转地址。", true);
            return;
        }
        try
        {
            if (address is not null)
            {
                companion = new CompanionServer(address.Address, HandleCompanionActionAsync);
                CompanionLink = companion.PairingUrl;
                CompanionInfo = $"已在 {address.Address}:{companion.BaseUri.Port} 监听 · 配对链接相当于临时遥控密钥，只在可信网络使用。";
            }
            if (relayUrl.Length > 0) await StartRelayAsync(relayUrl);
            CompanionRunning = true;
            CompanionQr = BuildQr(CompanionLink);
            PublishCompanionSnapshot();
            SetStatus(relayUrl.Length > 0 ? "手机伴侣已开启（中转通道）；手机扫码即可查看当前问题与回答。" : "手机伴侣已开启；手机扫码即可查看当前问题与回答。");
        }
        catch (Exception ex)
        {
            if (companion is not null) { await companion.DisposeAsync(); companion = null; }
            if (relay is not null) { await relay.DisposeAsync(); relay = null; }
            CompanionRunning = false;
            SetStatus("手机伴侣启动失败：" + ex.Message, true);
        }
    }

    /// <summary>Optional encrypted relay: the pairing link goes through the relay origin, not the LAN URL.</summary>
    private async Task StartRelayAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var baseUri)) throw new InvalidOperationException("中转地址不是有效的 URL。");
        relay = new CompanionRelayClient(CurrentCompanionSnapshot, HandleCompanionActionAsync,
            message => Dispatcher.UIThread.Post(() => CompanionInfo = message));
        var pairing = await relay.StartAsync(baseUri, new CompanionPairing(), allowLoopback: false);
        CompanionLink = pairing.Url(baseUri).AbsoluteUri;
        CompanionInfo = "中转已连接 · 端到端加密。配对链接走中转，不必在同一局域网。";
    }

    private bool CanToggleCompanion() => !ShuttingDown;

    /// <summary>QA: seed history so the companion self-check can exercise the real previous/next handler.</summary>
    public void SeedCompanionHistoryForCheck()
    {
        companionHistory.Clear();
        companionHistory.Add(("自检问题", "自检回答"));
        companionHistoryIndex = 0;
        DetectedQuestion = "自检问题";
        AnswerText = "自检回答";
    }

    public Task<bool> HandleCompanionActionForCheckAsync(string action) => HandleCompanionActionAsync(action);

    private async Task<bool> HandleCompanionActionAsync(string action)
    {
        // Network threads must not touch UI-bound state; marshal and await on the UI thread.
        if (Dispatcher.UIThread.CheckAccess()) return await HandleCompanionActionCoreAsync(action);
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(async () =>
        {
            try { completion.SetResult(await HandleCompanionActionCoreAsync(action)); }
            catch (Exception exception) { completion.SetException(exception); }
        });
        return await completion.Task;
    }

    private async Task<bool> HandleCompanionActionCoreAsync(string action)
    {
        switch (action)
        {
            case "answer":
                await AnswerAsync(string.IsNullOrWhiteSpace(DetectedQuestion) ? "请总结当前讨论" : DetectedQuestion, false);
                return true;
            case "regenerate":
                await AnswerAsync(string.IsNullOrWhiteSpace(DetectedQuestion) ? "请总结当前讨论" : DetectedQuestion, false);
                return true;
            case "shorter":
                await AnswerAsync("请把上一条回答压缩到三句话以内，只保留结论。", false);
                return true;
            case "example":
                await AnswerAsync("请针对上一条回答给出一个具体例子。", false);
                return true;
            case "english":
                await AnswerAsync("请用英文重写上一条回答，保留事实与结构。", false);
                return true;
            case "chinese":
                await AnswerAsync("请用中文重写上一条回答，保留事实与结构。", false);
                return true;
            case "previous":
                return ShowCompanionHistory(-1);
            case "next":
                return ShowCompanionHistory(1);
            case "cancel":
                CancelRequest();
                return true;
            default:
                return false;
        }
    }

    private bool ShowCompanionHistory(int delta)
    {
        if (companionHistory.Count == 0) return false;
        var index = Math.Clamp(companionHistoryIndex + delta, 0, companionHistory.Count - 1);
        if (index == companionHistoryIndex) return true;
        companionHistoryIndex = index;
        var item = companionHistory[index];
        DetectedQuestion = item.Question;
        AnswerText = item.Answer;
        PublishCompanionSnapshot();
        return true;
    }

    private CompanionSnapshot CurrentCompanionSnapshot() => new(
        Status: Recording ? "正在记录" : Answering ? "正在生成回答" : "待机",
        Question: DetectedQuestion,
        Answer: PlainAnswer,
        Answering: Answering,
        Candidate: DetectedQuestion,
        SessionID: CurrentSession?.Id.ToString("D"),
        Sources: Sources.Count == 0 ? null : string.Join("、", Sources.Take(3).Select(x => x.Label)));

    private void PublishCompanionSnapshot()
    {
        var snapshot = CurrentCompanionSnapshot();
        companion?.Publish(snapshot);
        relay?.Publish(snapshot);
    }

    private static Avalonia.Media.Imaging.Bitmap? BuildQr(string text)
    {
        try
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
            var png = new PngByteQRCode(data).GetGraphic(6);
            return new Avalonia.Media.Imaging.Bitmap(new MemoryStream(png));
        }
        catch
        {
            return null;
        }
    }

    public bool ShuttingDown => closing;

    public async Task ShutdownAsync()
    {
        if (closing) return;
        closing = true;
        workCancellation?.Cancel();
        CancelRequest();
        if (Recording) await StopRecordingCoreAsync();
        if (workTask != null) { try { await workTask; } catch { } }
        try
        {
            await Task.WhenAll(writes.ToArray());
            await Settings.SaveAsync();
        }
        catch { }
        if (relay is not null) { await relay.DisposeAsync(); relay = null; }
        if (companion is not null) { await companion.DisposeAsync(); companion = null; }
        Retrieval.Dispose();
        allowClose = true;
    }

    public bool AllowClose => allowClose;
}
