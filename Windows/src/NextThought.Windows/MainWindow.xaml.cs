using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Windows;

public partial class MainWindow : Window
{
    private DesktopFeatures? desktop;
    private sealed record ProcessChoice(int Id, long StartTicks, string Title) { public override string ToString() => Title; }
    private readonly AppPaths paths = new();
    private readonly SettingsStore settingsStore;
    private readonly SessionStore sessions;
    private readonly ModelStore models;
    private readonly ChatCompletionClient api = new();
    private readonly ObservableCollection<TranscriptRow> transcript = [];
    private readonly List<Task> writes = [];
    private readonly List<TranscriptLine> currentLines = [];
    private readonly DispatcherTimer candidateTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private QuestionCandidateTracker tracker = new();
    private AutomaticAnswerScheduler automaticScheduler = new();
    private bool lastRequestAutomatic;
    private DateTimeOffset lastMicrophoneSpeech;
    private AudioCaptureService? capture;
    private LiveSpeechEngine? engine;
    private SessionMeta? currentSession;
    private SessionMeta? recordingSession;
    private FileStream? sessionLease;
    private CancellationTokenSource? workCancellation, requestCancellation;
    private Task? workTask, startupTask;
    private bool ready, busy, recording, stopping, answering, summarizing, closing, allowClose, modelReady;
    private long epoch, requestEpoch, selectionEpoch;
    private double systemLevel, microphoneLevel;
    private sealed record ModelChoice(string Id, string Title) { public override string ToString() => Title; }
    private sealed record TranscriptRow(string Label, string Text);

    public MainWindow()
    {
        InitializeComponent(); settingsStore = new(paths); sessions = new(paths); models = new(paths);
        TranscriptList.ItemsSource = transcript;
        LocalModelBox.ItemsSource = new[]{new ModelChoice("xasr480", "X-ASR 中英 · 480 ms（推荐）"), new ModelChoice("xasr160", "X-ASR 中英 · 160 ms")};
        candidateTimer.Tick += (_, _) =>
        {
            var now = DateTimeOffset.UtcNow; ApplyCandidate(tracker.Tick(now));
            if (AutomaticBox.IsChecked != true || !recording || stopping) { automaticScheduler.Clear(); return; }
            if (answering && !lastRequestAutomatic) return;
            var speaking = microphoneLevel >= .012 || now - lastMicrophoneSpeech < TimeSpan.FromMilliseconds(600);
            if (automaticScheduler.Tick(now, speaking) is { } question) _ = AnswerAsync(question.FullText, true);
        };
    }
    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            desktop = new DesktopFeatures(this, () => $"{PageTitle.Text}\n{AudioSourceText.Text}\n\n{currentLines.LastOrDefault()?.Text}\n\n{AnswerText.Text}", () => { if (recording) _ = StopRecordingAsync(); else Start_Click(this, new RoutedEventArgs()); });
            RefreshProcesses_Click(this, new RoutedEventArgs());
            var settings = settingsStore.Load();
            MeetingTitleBox.Text = string.IsNullOrWhiteSpace(settings.LastMeetingTitle) ? "新会议" : settings.LastMeetingTitle;
            UseCodexBox.IsChecked = settings.UseCodex; CodexModelBox.Text = settings.CodexModel;
            BaseUrlBox.Text = settings.BaseUrl; ModelBox.Text = settings.Model; NotesModelBox.Text = settings.NotesModel;
            try { ApiKeyBox.Password = settingsStore.ReadApiKey(); } catch (System.Security.Cryptography.CryptographicException) { SetStatus("原凭据无法由当前用户解密，请重新输入 API Key。"); }
            UseMaterialsBox.IsChecked = settings.UseMaterials; AutomaticBox.IsChecked = settings.AutomaticAnswers; AutoSummaryBox.IsChecked = settings.AutoSummarize;
            SystemEnabledBox.IsChecked = settings.CaptureSystem; MicEnabledBox.IsChecked = settings.CaptureMicrophone; RefineBox.IsChecked = settings.SecondPass;
            MicGainSlider.Value = double.IsFinite(settings.MicrophoneGain) ? Math.Clamp(settings.MicrophoneGain, 1, 8) : 1;
            LocalModelBox.SelectedIndex = settings.LocalModel == "xasr160" ? 1 : 0;
            VocabularyBox.Text = settings.Vocabulary;
            EffortBox.SelectedItem = EffortBox.Items.Cast<ComboBoxItem>().FirstOrDefault(x => (x.Tag as string) == settings.ReasoningEffort) ?? EffortBox.Items[0];
            var recovered = await sessions.RecoverInterruptedAsync();
            await RefreshDevicesAsync(settings); RefreshHistory(); ready = true; RefreshModelState(); UpdateState();
            SetStatus(recovered.Count > 0 ? $"已处理 {recovered.Count} 场中断会议；可在历史中继续查看或整理纪要。" :
                modelReady ? "语音已就绪。可以开始记录，或先导入 WAV 验证。" : "先到设置准备语音组件；未连接回答服务时也可以只记录。");
        }
        catch (Exception ex) { ready = true; SetStatus(ex.Message, true); UpdateState(); }
    }
    private AppSettings ReadSettings() => new()
    {
        UseCodex = UseCodexBox.IsChecked == true, CodexModel = CodexModelBox.Text.Trim(),
        BaseUrl = BaseUrlBox.Text.Trim(), Model = ModelBox.Text.Trim(), NotesModel = NotesModelBox.Text.Trim(),
        ReasoningEffort = (EffortBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "",
        LocalModel = (LocalModelBox.SelectedItem as ModelChoice)?.Id ?? "xasr480", SecondPass = RefineBox.IsChecked == true,
        CaptureProcessId = (ProcessBox.SelectedItem as ProcessChoice)?.Id ?? 0, CaptureProcessStartTicks = (ProcessBox.SelectedItem as ProcessChoice)?.StartTicks ?? 0,
        CaptureSystem = SystemEnabledBox.IsChecked == true, CaptureMicrophone = MicEnabledBox.IsChecked == true,
        MicrophoneGain = MicGainSlider.Value,
        SystemDeviceId = (SystemDeviceBox.SelectedItem as AudioDeviceInfo)?.Id, MicrophoneDeviceId = (MicDeviceBox.SelectedItem as AudioDeviceInfo)?.Id,
        UseMaterials = UseMaterialsBox.IsChecked == true, Vocabulary = VocabularyBox.Text, AutomaticAnswers = AutomaticBox.IsChecked == true, AutoSummarize = AutoSummaryBox.IsChecked == true,
        LastMeetingTitle = MeetingTitleBox.Text.Trim()
    };
    private async Task SaveSettingsAsync()
    {
        var settings = ReadSettings();
        if (settings.BaseUrl.Length > 0) _ = ChatCompletionClient.Endpoint(settings.BaseUrl);
        settingsStore.SaveApiKey(ApiKeyBox.Password.Trim()); await settingsStore.SaveAsync(settings);
    }
    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    { try { await SaveSettingsAsync(); SetStatus("设置已保存，Key 已用当前 Windows 用户加密。"); } catch (Exception ex) { SetStatus(ex.Message, true); } }
    private async void CheckService_Click(object sender, RoutedEventArgs e)
    { await AnswerAsync("请只回复：连接成功", false, true); }
    private void Options_Changed(object sender, RoutedEventArgs e) { if (ready) { RefreshModelState(); UpdateState(); } }
    private void LocalModel_Changed(object sender, SelectionChangedEventArgs e) { if (ready) { RefreshModelState(); UpdateState(); } }
    private void RefreshModelState()
    {
        var settings = ReadSettings(); var streaming = models.IsInstalled(settings.LocalModel); var refine = models.IsInstalled("xasrOffline");
        modelReady = streaming && (!settings.SecondPass || refine);
        ModelStatusText.Text = $"流式模型：{(streaming ? "已就绪" : "未安装或待修复")}　整句精修：{(refine ? "已就绪" : "未安装")}";
        AudioSourceText.Text = settings.CaptureSystem && settings.CaptureMicrophone ? "所选系统输出设备混音 + 麦克风 · 在设置中调整" : settings.CaptureSystem ? "所选系统输出设备混音 · 不采集麦克风" : settings.CaptureMicrophone ? "麦克风 · 适合线下会议" : "尚未选择声音来源，请到设置中选择。";
    }
    private async Task RefreshDevicesAsync(AppSettings? preferred = null)
    {
        preferred ??= ReadSettings();
        var devices = await Task.Run(() => (System: AudioCaptureService.Devices(true), Microphone: AudioCaptureService.Devices(false)));
        SystemDeviceBox.ItemsSource = devices.System; MicDeviceBox.ItemsSource = devices.Microphone;
        SystemDeviceBox.SelectedItem = devices.System.FirstOrDefault(x => x.Id == preferred.SystemDeviceId) ?? devices.System.FirstOrDefault();
        MicDeviceBox.SelectedItem = devices.Microphone.FirstOrDefault(x => x.Id == preferred.MicrophoneDeviceId) ?? devices.Microphone.FirstOrDefault();
    }
    private async void RefreshDevices_Click(object sender, RoutedEventArgs e)
    { try { await RefreshDevicesAsync(); SetStatus("声音设备已刷新。"); } catch (Exception ex) { SetStatus(ex.Message, true); } }
    private void SetStatus(string text, bool error = false)
    { StatusText.Text = text; StatusText.Foreground = error ? System.Windows.Media.Brushes.Firebrick : (System.Windows.Media.Brush)FindResource("Muted"); }
    private void UpdateState()
    {
        if (!ready) return;
        var idle = !busy && !recording && !stopping;
        StartButton.IsEnabled = idle && !answering && modelReady && (SystemEnabledBox.IsChecked == true || MicEnabledBox.IsChecked == true);
        StopButton.IsEnabled = recording && !stopping; ImportButton.IsEnabled = idle && !answering && modelReady;
        NewButton.IsEnabled = idle && !answering; HistoryList.IsEnabled = idle && !answering; PrepareButton.IsEnabled = idle && !answering;
        SaveButton.IsEnabled = idle; CheckServiceButton.IsEnabled = idle && !answering;
        MeetingTitleBox.IsReadOnly = !idle; SystemEnabledBox.IsEnabled = idle; MicEnabledBox.IsEnabled = idle;
        ProcessBox.IsEnabled = idle; SystemDeviceBox.IsEnabled = idle; MicDeviceBox.IsEnabled = idle; LocalModelBox.IsEnabled = idle; RefineBox.IsEnabled = idle; VocabularyBox.IsReadOnly = !idle;
        MicGainSlider.IsEnabled = idle;
        UseCodexBox.IsEnabled = idle; CodexModelBox.IsReadOnly = !idle;
        BaseUrlBox.IsReadOnly = !idle; ModelBox.IsReadOnly = !idle; NotesModelBox.IsReadOnly = !idle; ApiKeyBox.IsEnabled = idle; EffortBox.IsEnabled = idle;
        AskButton.IsEnabled = !busy && !stopping && !summarizing; SummaryButton.IsEnabled = idle && !answering && currentSession != null && currentLines.Count > 0;
        BackupButton.IsEnabled = RestoreButton.IsEnabled = idle && !answering;
        ExportButton.IsEnabled = !busy && !recording && currentSession != null;
        CancelButton.IsEnabled = busy || answering; WorkProgress.IsIndeterminate = answering;
        StatsText.Text = recording ? $"正在记录 · {currentLines.Count} 条转写" : $"{currentLines.Count} 条转写 · {(currentSession?.AnswerCount ?? 0)} 条回答";
        TranscriptEmpty.Visibility = transcript.Count == 0 && !recording ? Visibility.Visible : Visibility.Collapsed;
    }
    private async Task RunWorkAsync(Func<CancellationToken, Task> operation)
    {
        if (busy || recording) return;
        busy = true; workCancellation = new(); UpdateState();
        try { workTask = operation(workCancellation.Token); await workTask; }
        catch (OperationCanceledException) { SetStatus("已取消，已经记录的内容仍保存在本机。"); }
        catch (Exception ex) { SetStatus(ex.Message, true); }
        finally { workTask = null; workCancellation.Dispose(); workCancellation = null; busy = false; WorkProgress.Value = 0; RefreshModelState(); UpdateState(); }
    }
    private async void PrepareModels_Click(object sender, RoutedEventArgs e)
    {
        await RunWorkAsync(async ct =>
        {
            var settings = ReadSettings(); await SaveSettingsAsync();
            var progress = new Progress<ModelProgress>(p => { WorkProgress.Value = p.Fraction; SetStatus(p.Message); });
            await models.InstallAsync(settings.LocalModel, progress, ct);
            if (settings.SecondPass) await models.InstallAsync("xasrOffline", progress, ct);
            SetStatus("模型已下载并校验，可以离线记录。");
        });
    }
    private async void Backup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "NextThought 备份|*.ntbackup", FileName = "NextThought-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".ntbackup" };
        if (dialog.ShowDialog(this) != true) return;
        await RunWorkAsync(async ct => { await Task.WhenAll(writes.ToArray()); await Task.Run(() => new BackupService(paths).ExportAsync(dialog.FileName, ct)); SetStatus("备份已导出：包含会议与资料；不包含 API Key、语音模型和设备设置。"); });
    }
    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "NextThought 备份|*.ntbackup;*.ihbackup" };
        if (dialog.ShowDialog(this) != true) return;
        await RunWorkAsync(async ct => { var result = await Task.Run(() => new BackupService(paths).ImportAsync(dialog.FileName, ct)); RefreshHistory(); SetStatus($"已导入 {result.SessionsAdded} 场会议、{result.MaterialsAdded} 份资料；{result.ConflictsSkipped} 个重复项保留现有版本。导入前备份：{result.SafetyBackup}。Mac 其余数据保存在 Migration 原始备份中；连接设置沿用本机。"); });
    }
    private void Tray_Click(object sender, RoutedEventArgs e) { if (desktop != null) Hide(); }
    private void Overlay_Click(object sender, RoutedEventArgs e) => desktop?.ToggleOverlay();
    private void RefreshProcesses_Click(object sender, RoutedEventArgs e)
    {
        var choices = new List<ProcessChoice> { new(0, 0, "整个输出设备混音") };
        foreach (var process in Process.GetProcesses())
        {
            using (process) try { if (process.Id != Environment.ProcessId && process.MainWindowHandle != IntPtr.Zero && process.MainWindowTitle.Length > 0) choices.Add(new(process.Id, process.StartTime.ToUniversalTime().Ticks, process.ProcessName + " · " + process.MainWindowTitle)); } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        }
        ProcessBox.ItemsSource = choices; ProcessBox.SelectedIndex = 0;
    }
    private void Materials_Click(object sender, RoutedEventArgs e) => new MaterialWindow(paths) { Owner = this }.ShowDialog();
    private void ViewSource_Click(object sender, RoutedEventArgs e)
    { if (SourcesBox.SelectedItem is SourceCitation source) new MaterialWindow(paths, source) { Owner = this }.ShowDialog(); else SetStatus("当前回答尚无资料引用。"); }
    private void ResetDisplay()
    {
        ++selectionEpoch; transcript.Clear(); currentLines.Clear(); writes.Clear(); SystemPartial.Text = MicrophonePartial.Text = "";
        SourcesBox.ItemsSource = null; AnswerText.Text = ""; SummaryText.Text = "结束会议后，按转写整理总结、决定、待办和未决问题。";
        DetectedQuestion.Text = "手动提问，或开启自动回答。"; tracker = new(); automaticScheduler = new();
        lastMicrophoneSpeech = default; systemLevel = microphoneLevel = 0; LevelText.Text = "";
    }
    private void NewMeeting_Click(object sender, RoutedEventArgs e)
    {
        if (recording || busy) return; CancelRequest(); currentSession = null; ResetDisplay(); HistoryList.SelectedItem = null;
        MeetingTitleBox.Text = "新会议"; PageTitle.Text = "会议工作台"; PageSubtitle.Text = "选择声音来源，开始记录。"; MainTabs.SelectedIndex = 0; UpdateState();
    }
    private SessionMeta NewMeta(AppSettings settings, string? title = null) => new()
    {
        Title = string.IsNullOrWhiteSpace(title ?? settings.LastMeetingTitle) ? "会议 " + DateTime.Now.ToString("MM-dd HH:mm") : (title ?? settings.LastMeetingTitle),
        AudioSource = settings.CaptureSystem && settings.CaptureProcessId > 0 ? (settings.CaptureMicrophone ? "process-tree+microphone" : "process-tree") : settings.CaptureSystem ? (settings.CaptureMicrophone ? "system+microphone" : "system") : "microphone",
        InPerson = !settings.CaptureSystem, Recognizer = settings.LocalModel + (settings.SecondPass ? "+refine" : ""), AnswerModel = settings.Model
    };
    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        startupTask = StartRecordingAsync();
        try { await startupTask; } finally { startupTask = null; }
    }
    private async Task StartRecordingAsync()
    {
        if (busy || recording || closing) return;
        busy = true; UpdateState(); SessionMeta? meta = null;
        try
        {
            await SaveSettingsAsync(); var settings = ReadSettings(); CancelRequest(); ResetDisplay();
            meta = NewMeta(settings); await sessions.CreateAsync(meta); currentSession = recordingSession = meta;
            sessionLease = sessions.AcquireLease(meta.Id);
            var generation = ++epoch; SetStatus("正在加载本地语音模型…");
            engine = await Task.Run(() => new LiveSpeechEngine(models, settings));
            if (closing) throw new OperationCanceledException("程序正在退出，已取消开始记录。");
            engine.Updated += update => Dispatcher.BeginInvoke(() => { if (generation == epoch) ReceiveTranscript(meta, update, true); });
            engine.Failed += message => Dispatcher.BeginInvoke(() => HandleAudioFailure(message));
            engine.Warning += message => Dispatcher.BeginInvoke(() => SetStatus(message, true));
            capture = new(); capture.FrameReady += frame => engine?.Accept(frame);
            capture.Failed += message => Dispatcher.BeginInvoke(() => HandleAudioFailure(message));
            capture.LevelChanged += (source, level) => Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                if (source == AudioSource.System) systemLevel = level; else microphoneLevel = level;
                LevelText.Text = $"系统 {systemLevel*100:F0} · 麦克风 {microphoneLevel*100:F0}";
            });
            await Task.Run(() => capture.Start(settings));
            if (capture.LastError is { } captureError) throw new IOException(captureError);
            recording = true; candidateTimer.Start();
            PageTitle.Text = meta.Title; PageSubtitle.Text = "进行中 · 转写持续保存在本机"; RefreshHistory(meta.Id);
            AudioSourceText.Text = settings.CaptureProcessId > 0 ? "应用进程树声音" + (settings.CaptureMicrophone ? " + 麦克风" : " · 不采集麦克风") : AudioSourceText.Text;
            SetStatus(settings.CaptureProcessId > 0 ? "正在记录所选应用及其子进程的声音。" : "正在记录。系统声音为所选输出设备混音。");
        }
        catch (Exception ex)
        {
            if (capture != null) { await capture.DisposeAsync(); capture = null; }
            if (engine != null) { await engine.DisposeAsync(); engine = null; }
            if (meta != null) await sessions.EndAsync(meta);
            recordingSession = null; SetStatus(ex.Message, true);
        }
        finally
        {
            if (!recording) { sessionLease?.Dispose(); sessionLease = null; recordingSession = null; }
            busy = false; UpdateState();
        }
    }
    private void HandleAudioFailure(string message)
    {
        if (!recording || stopping) return;
        SetStatus(message, true); _ = StopRecordingAsync(message);
    }
    private async void Stop_Click(object sender, RoutedEventArgs e) => await StopRecordingAsync();
    private async Task StopRecordingAsync(string? reason = null)
    {
        if (stopping || recordingSession == null) return;
        stopping = true; candidateTimer.Stop(); automaticScheduler.Clear(); UpdateState(); var meta = recordingSession;
        try
        {
            if (capture != null) { await capture.DisposeAsync(); capture = null; }
            if (engine != null) { await engine.DisposeAsync(); engine = null; }
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            await Task.WhenAll(writes.ToArray()); await sessions.EndAsync(meta);
            ++epoch; SystemPartial.Text = MicrophonePartial.Text = "";
            PageSubtitle.Text = "已结束 · 会议已归档"; SetStatus(reason == null ? "会议已保存，可以整理纪要。" : reason + "；已经记录的内容已保存。", reason != null);
        }
        catch (Exception ex) { SetStatus("会议结束时遇到问题：" + ex.Message, true); }
        finally { sessionLease?.Dispose(); sessionLease = null; recording = stopping = false; recordingSession = null; RefreshHistory(meta.Id); UpdateState(); }
        if (reason == null && !closing && AutoSummaryBox.IsChecked == true && currentSession?.Id == meta.Id) await SummarizeAsync(meta);
    }
    private void ReceiveTranscript(SessionMeta meta, TranscriptUpdate update, bool automatic)
    {
        if (update.Source == AudioSource.Microphone && update.Text.Length > 0) lastMicrophoneSpeech = DateTimeOffset.UtcNow;
        if (!update.IsFinal)
        {
            if (update.Source == AudioSource.System) { SystemPartial.Text = "系统 · " + update.Text; if (automatic && recording && !stopping) ApplyCandidate(tracker.Ingest(update.Text, false, update.Id, DateTimeOffset.UtcNow)); }
            else MicrophonePartial.Text = "麦克风 · " + update.Text;
            return;
        }
        if (update.Source == AudioSource.System) SystemPartial.Text = ""; else MicrophonePartial.Text = "";
        var echo = EchoDetector.IsLikelyEcho(update, currentLines, meta.InPerson);
        var line = new TranscriptLine { Id = update.Id, Date = update.Start, StartedAt = update.Start, EndedAt = update.End,
            Speaker = update.Source == AudioSource.System ? "remote" : meta.InPerson ? "room" : "me", Text = update.Text, SuspectedEcho = echo ? true : null };
        currentLines.Add(line); transcript.Add(Row(line)); if (transcript.Count > 400) transcript.RemoveAt(0);
        TranscriptList.ScrollIntoView(transcript[^1]);
        var write = sessions.AppendTranscriptAsync(meta, line); writes.Add(write);
        _ = write.ContinueWith(task => Dispatcher.BeginInvoke(() => HandleAudioFailure("会议写入失败：" + task.Exception!.GetBaseException().Message)), TaskContinuationOptions.OnlyOnFaulted);
        if (automatic && update.Source == AudioSource.System && recording && !stopping) ApplyCandidate(tracker.Ingest(update.Text, true, update.Id, DateTimeOffset.UtcNow));
        UpdateState();
    }
    private static TranscriptRow Row(TranscriptLine line) => new($"{line.Date.ToLocalTime():HH:mm:ss} · {(line.Speaker == "remote" ? "系统声音" : line.Speaker == "room" ? "现场" : "麦克风")}{(line.SuspectedEcho == true ? " · 可能回声" : "")}", line.Text);
    private void ApplyCandidate(CandidateDecision decision)
    {
        if (decision.Candidate is not { } candidate || decision.Kind is CandidateKind.None or CandidateKind.Updated) return;
        DetectedQuestion.Text = candidate.FullText;
        if (decision.Kind == CandidateKind.Trigger && AutomaticBox.IsChecked == true && recording && !stopping)
        {
            automaticScheduler.Enqueue(candidate, DateTimeOffset.UtcNow);
            if (candidate.Supersedes.HasValue && answering && lastRequestAutomatic) CancelRequest();
        }
    }
    private async void ImportWave_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Filter = "WAV 录音 (*.wav)|*.wav", CheckFileExists = true };
        if (picker.ShowDialog(this) != true) return;
        await RunWorkAsync(async ct =>
        {
            await SaveSettingsAsync(); var settings = ReadSettings(); CancelRequest(); ResetDisplay();
            var meta = NewMeta(settings, Path.GetFileNameWithoutExtension(picker.FileName)); meta.AudioSource = "imported-wav";
            await sessions.CreateAsync(meta); using var lease = sessions.AcquireLease(meta.Id); currentSession = meta; var generation = ++epoch;
            PageTitle.Text = meta.Title; PageSubtitle.Text = "正在本机读取录音…"; SetStatus("正在加载模型并转写 WAV…");
            try
            {
                await Task.Run(() =>
                {
                    using var recognizer = new SpeechRecognizerSession(models, settings);
                    recognizer.Updated += update => Dispatcher.BeginInvoke(() => { if (generation == epoch) ReceiveTranscript(meta, update, false); });
                    recognizer.TranscribeWave(picker.FileName, AudioSource.System, ct);
                }, ct);
                SetStatus("录音已转写并归档，未保存原始音频。");
            }
            finally
            {
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle); await Task.WhenAll(writes.ToArray());
                await sessions.EndAsync(meta); ++epoch; SystemPartial.Text = MicrophonePartial.Text = "";
                PageSubtitle.Text = "录音转写 · 已归档"; RefreshHistory(meta.Id);
            }
        });
    }
    private void CancelRequest() { ++requestEpoch; var previous = requestCancellation; requestCancellation = null; previous?.Cancel(); answering = false; }
    private AnswerOptions AnswerOptions(bool notes = false)
    {
        var settings = ReadSettings(); if (settings.UseCodex) return new("https://codex.local", settings.CodexModel.Length > 0 ? settings.CodexModel : "default", "");
        return new(settings.BaseUrl, notes && settings.NotesModel.Length > 0 ? settings.NotesModel : settings.Model, ApiKeyBox.Password.Trim(), settings.ReasoningEffort);
    }
    private IAnswerClient CurrentClient() => UseCodexBox.IsChecked == true ? new CodexAnswerClient() : api;
    private async void Ask_Click(object sender, RoutedEventArgs e)
    { var question = QuestionInput.Text.Trim(); if (question.Length == 0) { SetStatus("请先输入问题。"); QuestionInput.Focus(); return; } await AnswerAsync(question, false); }
    private async Task AnswerAsync(string question, bool automatic, bool check = false)
    {
        if (!automatic && !check) automaticScheduler.Clear();
        CancelRequest(); var generation = ++requestEpoch; var cancellation = new CancellationTokenSource(); requestCancellation = cancellation; answering = true; UpdateState();
        lastRequestAutomatic = automatic;
        var meta = currentSession; var options = AnswerOptions(); var clock = Stopwatch.StartNew();
        try
        {
            if (!check) { DetectedQuestion.Text = question; AnswerText.Text = ""; }
            var context = string.Join("\n", currentLines.Where(x => x.SuspectedEcho != true).TakeLast(40).Select(x => $"[{x.Date.ToLocalTime():HH:mm:ss}] {x.Speaker}：{x.Text}"));
            if (context.Length > 18000) context = context[^18000..];
            SetStatus(check ? "正在检查回答服务…" : "正在生成参考回答…");
            var progress = new Progress<string>(text => { if (generation == requestEpoch && !check) { AnswerText.Text = text; AnswerText.ScrollToEnd(); } });
            var sources = !check && UseMaterialsBox.IsChecked == true
                ? await Task.Run(() => new KeywordIndex(new MaterialStore(paths).List()).Search(question), cancellation.Token) : [];
            if (!check) SourcesBox.ItemsSource = null;
            var result = await CurrentClient().AnswerAsync(options,
                check ? "严格按用户要求回复。" : "你是会议中的参考助手。根据已提供的转写回答当前问题，使用简洁中文；问题为英文时可用英文。区分事实与建议，不编造未提供的文件、数据、决定或经历。",
                check ? question : $"最近的会议转写：\n{context}\n\n参考资料：\n{KeywordIndex.Context(sources)}\n\n当前问题：{question}", text => ((IProgress<string>)progress).Report(text), cancellation.Token);
            if (generation != requestEpoch) return;
            if (!check)
            {
                var cited = KeywordIndex.Cited(result, sources); SourcesBox.ItemsSource = cited; SourcesBox.SelectedIndex = 0;
                if (meta is null)
                {
                    meta = NewMeta(ReadSettings(), question.Length > 40 ? question[..40] : question);
                    meta.AudioSource = "text-question"; await sessions.CreateAsync(meta); await sessions.EndAsync(meta); currentSession = meta;
                }
                await sessions.AppendAnswerAsync(meta, new AnswerRecord { Question = question, Answer = result, Model = options.Model, Seconds = clock.Elapsed.TotalSeconds, Automatic = automatic, Sources = cited });
                RefreshHistory();
            }
            tracker.HasPriorAnswer = true; SetStatus(check ? "回答服务连接成功：" + result : "回答已完成。会议内的回答已归档。");
        }
        catch (OperationCanceledException) { if (generation == requestEpoch) SetStatus("回答已取消。"); }
        catch (Exception ex) { if (generation == requestEpoch) SetStatus(ex.Message, true); }
        finally { cancellation.Dispose(); if (generation == requestEpoch) { requestCancellation = null; answering = false; UpdateState(); } }
    }
    private async void Summarize_Click(object sender, RoutedEventArgs e) { if (currentSession != null) await SummarizeAsync(currentSession); }
    private async Task SummarizeAsync(SessionMeta meta)
    {
        if (recording || busy || summarizing) return;
        CancelRequest(); var generation = ++requestEpoch; var cancellation = new CancellationTokenSource(); requestCancellation = cancellation; answering = summarizing = true; UpdateState(); MainTabs.SelectedIndex = 1;
        try
        {
            await Task.WhenAll(writes.ToArray());
            var options = AnswerOptions(true);
            var progress = new Progress<SummaryProgress>(p =>
            {
                if (generation != requestEpoch) return;
                SummaryText.Text = p.Text; SetStatus($"{p.Stage} · {p.Completed}/{p.Total}");
            });
            var markdown = await new SummaryService(sessions, CurrentClient()).GenerateAsync(meta.Id, options,
                p => ((IProgress<SummaryProgress>)progress).Report(p), cancellation.Token);
            if (generation == requestEpoch) { SummaryText.Text = markdown; SetStatus("纪要已保存，可导出 Markdown。"); }
        }
        catch (OperationCanceledException)
        {
            if (generation == requestEpoch) SetStatus("纪要整理已取消；原始转写和已完成片段已保留，可继续整理。");
        }
        catch (Exception ex)
        {
            if (generation == requestEpoch) SetStatus(ex.Message, true);
        }
        finally { cancellation.Dispose(); summarizing = false; if (generation == requestEpoch) { requestCancellation = null; answering = false; UpdateState(); RefreshHistory(meta.Id); } }
    }
    private void RefreshHistory(Guid? select = null)
    {
        var list = sessions.List(); HistoryList.ItemsSource = list;
        if (select.HasValue) HistoryList.SelectedItem = list.FirstOrDefault(x => x.Id == select.Value);
    }
    private async void History_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ready || recording || busy || HistoryList.SelectedItem is not SessionMeta meta) return;
        var generation = ++selectionEpoch; CancelRequest();
        try
        {
            var data = await Task.Run(() => sessions.Load(meta.Id)); if (generation != selectionEpoch) return;
            currentSession = data.Meta; currentLines.Clear(); currentLines.AddRange(data.Lines); transcript.Clear();
            foreach (var line in data.Lines.TakeLast(400)) transcript.Add(Row(line));
            SystemPartial.Text = MicrophonePartial.Text = ""; MeetingTitleBox.Text = data.Meta.Title; PageTitle.Text = data.Meta.Title;
            PageSubtitle.Text = "会议归档 · " + data.Meta.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            SourcesBox.ItemsSource = data.Answers.LastOrDefault()?.Sources; SourcesBox.SelectedIndex = 0;
            AnswerText.Text = data.Answers.LastOrDefault()?.Answer ?? ""; DetectedQuestion.Text = data.Answers.LastOrDefault()?.Question ?? "可以继续对这场会议提问。";
            SummaryText.Text = data.Summary.Length > 0 ? data.Summary : "这场会议还没有纪要，点击上方整理。";
            UpdateState();
        }
        catch (Exception ex) { SetStatus(ex.Message, true); }
    }
    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (currentSession == null) return;
        var file = new SaveFileDialog { Filter = "Markdown (*.md)|*.md", FileName = Regex.Replace(currentSession.Title, "[<>:\"/\\\\|?*]", "_") + ".md" };
        if (file.ShowDialog(this) != true) return;
        try { await Task.WhenAll(writes.ToArray()); await sessions.ExportMarkdownAsync(currentSession.Id, file.FileName); SetStatus("会议已导出。"); }
        catch (Exception ex) { SetStatus(ex.Message, true); }
    }
    private void CopyAnswer_Click(object sender, RoutedEventArgs e)
    { try { if (AnswerText.Text.Length > 0) { Clipboard.SetText(AnswerText.Text); SetStatus("回答已复制。"); } } catch (Exception ex) { SetStatus(ex.Message, true); } }
    private void OpenData_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("explorer.exe", paths.Root) { UseShellExecute = true });
    private void Cancel_Click(object sender, RoutedEventArgs e) { workCancellation?.Cancel(); requestCancellation?.Cancel(); }
    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (allowClose) return;
        e.Cancel = true; if (closing) return; closing = true;
        workCancellation?.Cancel(); CancelRequest();
        if (startupTask != null) { try { await startupTask; } catch { } }
        if (recording) await StopRecordingAsync();
        if (workTask != null) { try { await workTask; } catch { } }
        try { await Task.WhenAll(writes.ToArray()); await SaveSettingsAsync(); } catch { }
        desktop?.Dispose(); desktop = null; allowClose = true; Close();
    }
}
