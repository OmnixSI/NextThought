namespace NextThought.Desktop;

public partial class AppState
{

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

    public bool IsIdle => !Working && !Recording && !Stopping;

    public bool BusyOrRecording => Working || Recording;

    public bool HasTranscript => Transcript.Count > 0;

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

    partial void OnRecordingChanged(bool value) { RaiseBusy(); PublishCompanionSnapshot(); OnPropertyChanged(nameof(ContextSidebarVisible)); }

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

    [RelayCommand(CanExecute = nameof(CanStop))]
    private Task StopRecordingAsync() => StopRecordingCoreAsync();

    private bool CanStop() => Recording && !Stopping;

    private void HandleAudioFailure(string message)
    {
        if (!Recording || Stopping) return;
        SetStatus(message, true);
        _ = StopRecordingCoreAsync(message);
    }

    private void ResetLiveTranslation()
    {
        remoteLiveTranslation = null;
        microphoneLiveTranslation = null;
        SystemPartialTranslation = MicrophonePartialTranslation = IslandTranslation = "";
    }

    // ---- 会议自动收尾（AppState+AutoEnd.swift → MeetingEndWatch in Core） ----

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
}
