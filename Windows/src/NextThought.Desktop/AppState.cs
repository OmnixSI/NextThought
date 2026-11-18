using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop;

public enum AppSection { Home, Live, Notes, Library, Settings }

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
    private LiveSpeechEngine? engine;
    private MeetingVoiceTagger? voiceTagger;
    private MeetingTranslator? translator;
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
        Sessions = new SessionStore(Paths);
        Models = new ModelStore(Paths);
        Retrieval = new RetrievalService(Paths);
        Settings = new SettingsViewModel(this);
        Library = new LibraryViewModel(this);
        candidateTimer.Tick += (_, _) => OnCandidateTimer();
        History = [];
        Transcript = [];
        Sources = [];
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
    [ObservableProperty] private bool stopping;
    [ObservableProperty] private bool answering;
    [ObservableProperty] private bool summarizing;
    [ObservableProperty] private bool ready;
    [ObservableProperty] private bool modelReady;
    [ObservableProperty] private bool companionRunning;
    [ObservableProperty] private string meetingTitle = "新会议";
    [ObservableProperty] private string pageSubtitle = "选择声音来源，开始记录。";
    [ObservableProperty] private string status = "正在准备…";
    [ObservableProperty] private bool statusIsError;
    [ObservableProperty] private string systemPartial = "";
    [ObservableProperty] private string microphonePartial = "";
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
    public ObservableCollection<TranscriptRow> Transcript { get; }
    public ObservableCollection<RecentMeetingRow> History { get; }
    public ObservableCollection<SourceCitation> Sources { get; }

    public AppSettings SettingsSnapshot => Settings.Build();

    public bool IsIdle => !Working && !Recording && !Stopping;
    public bool CanCancel => Working || Answering;
    public bool BusyOrRecording => Working || Recording;
    public bool OnHome => Section == AppSection.Home;
    public bool OnLive => Section == AppSection.Live;
    public bool OnNotes => Section == AppSection.Notes;
    public bool OnLibrary => Section == AppSection.Library;
    public bool OnSettings => Section == AppSection.Settings;
    public bool HasTranscript => Transcript.Count > 0;
    public string StatsText => Recording ? $"正在记录 · {currentLines.Count} 条转写" : $"{currentLines.Count} 条转写 · {(CurrentSession?.AnswerCount ?? 0)} 条回答";
    public string Greeting => DateTime.Now.Hour switch
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
        OnPropertyChanged(nameof(OnSettings));
    }

    partial void OnWorkingChanged(bool value) => RaiseBusy();
    partial void OnRecordingChanged(bool value) => RaiseBusy();
    partial void OnStoppingChanged(bool value) => RaiseBusy();
    partial void OnAnsweringChanged(bool value) { RaiseBusy(); OnPropertyChanged(nameof(CanCancel)); }
    partial void OnSummarizingChanged(bool value) => RaiseBusy();
    partial void OnCurrentSessionChanged(SessionMeta? value) => OnPropertyChanged(nameof(StatsText));
    partial void OnLiveModeChanged(LiveWindowMode value) => OnPropertyChanged(nameof(LiveModeLabel));

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
        Status = text;
        StatusIsError = error;
    }

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
            Mark("settings");
            RefreshProcesses();
            Mark("processes");
            await Settings.RefreshDevicesAsync();
            Mark("devices");
            MeetingTitle = string.IsNullOrWhiteSpace(Settings.LastMeetingTitle) ? "新会议" : Settings.LastMeetingTitle;
            var recovered = await Sessions.RecoverInterruptedAsync();
            Mark("recover");
            RefreshHistory();
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
            if (StartupOptions.Section is { } requested) Section = requested;
        }
        catch (Exception ex)
        {
            startupError = ex.Message;
            StartupErrorText = ex.Message;
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
        PageSubtitle = "演示数据 · 不会写入你的资料";
        DetectedQuestion = DemoContent.Question;
        AnswerText = DemoContent.Answer;
        SummaryText = DemoContent.Summary;
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
        OnPropertyChanged(nameof(StatsText));
        ModelStatusText = "演示模式：未加载识别模型";
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

    private SessionMeta NewMeta(AppSettings settings, string? title = null) => new()
    {
        Title = string.IsNullOrWhiteSpace(title ?? settings.LastMeetingTitle) ? "会议 " + DateTime.Now.ToString("MM-dd HH:mm") : (title ?? settings.LastMeetingTitle),
        AudioSource = settings.CaptureSystem && settings.CaptureProcessId > 0
            ? settings.CaptureMicrophone ? "process-tree+microphone" : "process-tree"
            : settings.CaptureSystem ? settings.CaptureMicrophone ? "system+microphone" : "system" : "microphone",
        InPerson = !settings.CaptureSystem,
        Recognizer = settings.LocalModel + (settings.SecondPass ? "+refine" : ""),
        AnswerModel = settings.Model
    };

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
            var settings = Settings.Build();
            CancelRequest();
            ResetDisplay();
            meta = NewMeta(settings, string.IsNullOrWhiteSpace(MeetingTitle) || MeetingTitle == "新会议" ? null : MeetingTitle);
            MeetingTitle = meta.Title;
            await Sessions.CreateAsync(meta);
            CurrentSession = meta;
            sessionLease = Sessions.AcquireLease(meta.Id);
            var generation = ++epoch;
            SetStatus("正在加载本地语音模型…");
            if (settings.SeparateSpeakers) voiceTagger = await Task.Run(() => new MeetingVoiceTagger(Paths, settings.CaptureSystem ? AudioSource.System : AudioSource.Microphone));
            if (settings.TranslateSpeech) translator = new(CurrentClient(), AnswerOptions());
            engine = await Task.Run(() => new LiveSpeechEngine(Models, settings));
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
            candidateTimer.Start();
            PageSubtitle = "进行中 · 转写持续保存在本机";
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
            voiceTagger?.Dispose(); voiceTagger = null;
            await Sessions.EndAsync(meta);
            ++epoch;
            SystemPartial = MicrophonePartial = "";
            PageSubtitle = "已结束 · 会议已归档";
            SetStatus(reason == null ? "会议已保存，可以整理纪要。" : reason + "；已经记录的内容已保存。", reason != null);
        }
        catch (Exception ex) { SetStatus("会议结束时遇到问题：" + ex.Message, true); }
        finally
        {
            translator?.Dispose(); translator = null;
            voiceTagger?.Dispose(); voiceTagger = null;
            sessionLease?.Dispose(); sessionLease = null;
            Recording = Stopping = false;
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
        if (update.Source == AudioSource.Microphone && update.Text.Length > 0) lastMicrophoneSpeech = DateTimeOffset.UtcNow;
        if (!update.IsFinal)
        {
            if (update.Source == AudioSource.System)
            {
                SystemPartial = "系统 · " + update.Text;
                if (automatic && Recording && !Stopping) ApplyCandidate(tracker.Ingest(update.Text, false, update.Id, DateTimeOffset.UtcNow));
            }
            else MicrophonePartial = "麦克风 · " + update.Text;
            return;
        }
        if (update.Source == AudioSource.System) SystemPartial = ""; else MicrophonePartial = "";
        var echo = EchoDetector.IsLikelyEcho(update, currentLines, meta.InPerson);
        var line = new TranscriptLine
        {
            Id = update.Id, Date = update.Start, StartedAt = update.Start, EndedAt = update.End,
            Speaker = update.Source == AudioSource.System ? "remote" : meta.InPerson ? "room" : "me",
            Text = update.Text, SuspectedEcho = echo ? true : null
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
        OnPropertyChanged(nameof(StatsText));
        OnPropertyChanged(nameof(HasTranscript));
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
            var result = await CurrentClient().AnswerAsync(options,
                check ? "严格按用户要求回复。" : AnswerPrompt,
                check ? question : $"最近的会议转写：\n{context}\n\n参考资料：\n{KeywordIndex.Context(sources)}\n\n当前问题：{question}",
                text => ((IProgress<string>)progress).Report(text), cancellation.Token);
            if (generation != requestEpoch) return;
            if (!check)
            {
                var cited = KeywordIndex.Cited(result, sources);
                Sources.Clear();
                foreach (var citation in cited) Sources.Add(citation);
                AnswerText = result;
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

    private bool CanExport() => !Working && !Recording && CurrentSession != null;

    [RelayCommand(CanExecute = nameof(CanImportWave))]
    private async Task ImportWaveAsync()
    {
        if (PickOpenFile is null) return;
        if (await PickOpenFile("WAV 录音 (*.wav)|*.wav") is not { } file) return;
        await RunWorkAsync(async ct =>
        {
            await Settings.SaveAsync();
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
            PageSubtitle = "正在本机读取录音…";
            Section = AppSection.Live;
            SetStatus("正在加载模型并转写 WAV…");
            try
            {
                if (settings.SeparateSpeakers) voiceTagger = await Task.Run(() => new MeetingVoiceTagger(Paths, AudioSource.System), ct);
                if (settings.TranslateSpeech) translator = new(CurrentClient(), AnswerOptions());
                var tagger = voiceTagger;
                await Task.Run(() =>
                {
                    using var recognizer = new SpeechRecognizerSession(Models, settings);
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
                voiceTagger?.Dispose(); voiceTagger = null;
                await Sessions.EndAsync(meta);
                ++epoch;
                SystemPartial = MicrophonePartial = "";
                PageSubtitle = "录音转写 · 已归档";
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
            SetStatus($"已导入 {result.SessionsAdded} 场会议、{result.MaterialsAdded} 份资料；{result.ConflictsSkipped} 个重复项保留现有版本。导入前备份：{result.SafetyBackup}。");
        });
    }

    private bool CanRestore() => IsIdle && !Answering;

    [RelayCommand(CanExecute = nameof(CanSaveSettings))]
    private async Task SaveSettingsAsync()
    {
        try
        {
            await Settings.SaveAsync();
            RefreshModelState();
            SetStatus("设置已保存，密钥已用当前 Windows 用户加密。");
        }
        catch (Exception ex) { SetStatus(ex.Message, true); }
    }

    private bool CanSaveSettings() => IsIdle;

    [RelayCommand]
    private async Task CheckServiceAsync() => await AnswerAsync("请只回复：连接成功", false, true);

    [RelayCommand]
    private void Cancel() { workCancellation?.Cancel(); requestCancellation?.Cancel(); translator?.Cancel(); }

    /// <summary>纪要窗口 → 字幕条 → 隐藏 → 纪要窗口 … (LiveWindowMode in Core).</summary>
    [RelayCommand]
    private void CycleLiveMode() => LiveMode = LiveMode.Next();

    public string LiveModeLabel => LiveMode.Title();

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
        MeetingTitle = "新会议";
        PageSubtitle = "选择声音来源，开始记录。";
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
            currentLines.Clear();
            currentLines.AddRange(data.Lines);
            Transcript.Clear();
            foreach (var line in data.Lines.TakeLast(400)) Transcript.Add(new TranscriptRow(line));
            SystemPartial = MicrophonePartial = "";
            MeetingTitle = data.Meta.Title;
            PageSubtitle = "会议归档 · " + data.Meta.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            Sources.Clear();
            foreach (var source in data.Answers.LastOrDefault()?.Sources ?? []) Sources.Add(source);
            RetrievalInfo = data.Answers.LastOrDefault()?.Sources?.Count > 0 ? "历史回答的引用已保存，可查看来源。" : "";
            AnswerText = data.Answers.LastOrDefault()?.Answer ?? "";
            DetectedQuestion = data.Answers.LastOrDefault()?.Question ?? "可以继续对这场会议提问。";
            SummaryText = data.Summary.Length > 0 ? data.Summary : "这场会议还没有纪要，点击上方整理。";
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
        Retrieval.Dispose();
        allowClose = true;
    }

    public bool AllowClose => allowClose;
}
