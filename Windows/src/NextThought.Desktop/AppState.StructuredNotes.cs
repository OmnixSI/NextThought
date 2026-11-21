namespace NextThought.Desktop;

public partial class AppState
{

    // ---- 九个场景（Core ScenePreset 的界面映射） ----

    [ObservableProperty] private MeetingMode selectedMode = MeetingMode.Meeting;

    /// <summary>The cue surface layout this scene asks for (key points vs. a script).</summary>
    public CueStyle SceneCueStyle => ScenePreset.For(SelectedMode).CueStyle;

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
        ? UiText.Format("当前场景：%@ · %@", scene.Title, scene.Subtitle) + (SceneCueStyle == CueStyle.Script ? UiText.T(" · 提词按句") : UiText.T(" · 提词按要点"))
        : "当前场景：通用";

    /// <summary>Cue lines for the island: key points or a script, per the scene.</summary>
    public IReadOnlyList<CueLine> IslandLines => string.IsNullOrWhiteSpace(PlainAnswer)
        ? []
        : CueScript.Lines(PlainAnswer, SceneCueStyle, 3);

    partial void OnHistoryFilterChanged(string value)
    {
        ApplyHistoryFilter();
        OnPropertyChanged(nameof(HistoryFilterIsAll));
        OnPropertyChanged(nameof(HistoryFilterHasSummary));
        OnPropertyChanged(nameof(HistoryFilterUnfiled));
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

    /// <summary>Header subtitle; the fixed ones follow the interface language.</summary>
    private void SetPageSubtitle(string text) =>
        PageSubtitle = UiText.Translate(text, UiTextSource.Instance.IsEnglish);

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
            UiTextSource.Instance.SetPreference(Settings.Language);
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
            SetInterruptedNotice(recovered.Count);
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
                Library.NotifyDemoLoaded();
                SetStatus("演示模式：内置样本数据，不会写入你的资料。");
            }
            else
            {
                SetStatus(recovered.Count > 0
                    ? UiText.Format("已处理 %lld 场中断会议；可在历史中继续查看或整理纪要。", recovered.Count)
                    : ModelReady ? "语音已就绪。可以开始记录，或先导入 WAV 验证。" : "先到设置准备语音组件；未连接回答服务时也可以只记录。");
            }
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

    /// <summary>Jump from a notes timestamp back to the live transcript.</summary>
    public void JumpToTranscript(int seconds)
    {
        Section = AppSection.Live;
        Dispatcher.UIThread.Post(() => TranscriptJumpRequested?.Invoke(Math.Max(0, seconds)));
    }

    /// <summary>
    /// Search and status filter for the history page (plan §15 历史：搜索、筛选、批量删除).
    /// Batch delete is not ported yet; the filter and search are.
    /// </summary>
    partial void OnHistoryDateFilterChanged(string value) => ApplyHistoryFilter();

    private void ApplyHistoryFilter()
    {
        var query = HistoryQuery.Trim();
        IEnumerable<RecentMeetingRow> rows = History;
        // §6.7 E1: Mac historyWorkspaceFilter — a series scope narrows the list before search/filters.
        if (HistorySeriesFilter is { Length: > 0 } seriesScope)
            rows = rows.Where(row => string.Equals(
                workspace.SeriesOfSession(row.Meta.Id.ToString())?.Id, seriesScope, StringComparison.OrdinalIgnoreCase));
        if (MatchesFilter(HistoryFilter, "有纪要")) rows = rows.Where(row => row.HasSummary);
        else if (MatchesFilter(HistoryFilter, "未整理")) rows = rows.Where(row => !row.HasSummary);
        var now = DateTimeOffset.Now;
        var today = now.Date;
        if (MatchesFilter(HistoryDateFilter, "今天")) rows = rows.Where(row => row.Meta.StartedAt.ToLocalTime().Date == today);
        else if (MatchesFilter(HistoryDateFilter, "近 7 天")) rows = rows.Where(row => row.Meta.StartedAt.ToLocalTime() >= now.AddDays(-7));
        else if (MatchesFilter(HistoryDateFilter, "近 30 天")) rows = rows.Where(row => row.Meta.StartedAt.ToLocalTime() >= now.AddDays(-30));
        else if (MatchesFilter(HistoryDateFilter, "本年度")) rows = rows.Where(row => row.Meta.StartedAt.ToLocalTime().Year == now.Year);
        double Duration(RecentMeetingRow row) => (row.Meta.EndedAt ?? row.Meta.StartedAt).Subtract(row.Meta.StartedAt).TotalSeconds;
        if (MatchesFilter(HistoryLengthFilter, "1 分钟以内")) rows = rows.Where(row => Duration(row) < 60);
        else if (MatchesFilter(HistoryLengthFilter, "1–10 分钟")) rows = rows.Where(row => Duration(row) >= 60 && Duration(row) < 600);
        else if (MatchesFilter(HistoryLengthFilter, "10 分钟以上")) rows = rows.Where(row => Duration(row) >= 600);
        if (query.Length > 0)
        {
            // E18: metadata matches stay available without disk access; content-search matches
            // (transcript/answer/notes…) keep the row visible as soon as the scan publishes them.
            rows = rows.Where(row =>
                row.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.Meta.LocalStartedAt.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.Meta.Mode.Contains(query, StringComparison.OrdinalIgnoreCase)
                || historyHitsBySession.ContainsKey(row.Meta.Id));
        }
        if (MatchesFilter(HistorySort, "最早优先")) rows = rows.OrderBy(row => row.Meta.StartedAt);
        else if (MatchesFilter(HistorySort, "时长最长")) rows = rows.OrderByDescending(Duration);
        else if (MatchesFilter(HistorySort, "时长最短")) rows = rows.OrderBy(Duration);
        else if (MatchesFilter(HistorySort, "问题最多")) rows = rows.OrderByDescending(row => row.Meta.QuestionCount);
        else rows = rows.OrderByDescending(row => row.Meta.StartedAt);
        FilteredHistory.Clear();
        foreach (var row in rows) FilteredHistory.Add(row);
        HistoryGroups.Clear();
        foreach (var group in FilteredHistory.GroupBy(row =>
                 {
                     var series = workspace.SeriesOfSession(row.Meta.Id.ToString());
                     return (Id: series?.Id, Name: series?.Name ?? UiText.T("未归入系列"));
                 }))
        {
            // E18: the series checkbox state survives list rebuilds.
            var seriesRow = group.Key.Id is { } id ? workspace.Find(id) : null;
            var groupRow = new HistoryGroupRow(group.Key.Name, group.Key.Id, group.ToList())
            {
                IsSelected = group.Key.Id is { } selectedId && SelectedHistorySeries.Contains(selectedId),
                ModeTitle = seriesRow is null ? "" : UiText.T(SessionModes.Parse(seriesRow.Mode).Title())
            };
            HistoryGroups.Add(groupRow);
        }
        OnPropertyChanged(nameof(HistorySummary));
        OnPropertyChanged(nameof(IsHistoryViewEmpty));
        NotifyHistoryScopeState();
    }

    /// <summary>SessionMode raw value as the Mac archive writes it (LabMeeting → "labMeeting").</summary>
    private static string ModeKey(MeetingMode mode)
    {
        var name = mode.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
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
        // E10: honour 留意眼神 (off by default) for the meeting that is starting now.
        ApplyCoachGazeSettings();
        autoEndTimer.Tick += (_, _) => AutoEndTick();
        autoEndTimer.Start();
    }

    private void StopAutoEnd()
    {
        autoEndTimer?.Stop();
        autoEndTimer = null;
        autoEnd = null;
        // E10: the Mac LiveCoach.end() bookends the eye-contact share into the meeting summary
        // and lets go of the camera; do both before the coach state goes away.
        coachSummary?.ObserveEyeContact(GazeContactRatio);
        _ = StopGazeWatchingAsync();
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
            // E10: our own finished line keeps the eye-contact speaking window open (Mac ingest).
            if (line.Speaker != "remote") NoteGazeSpeech();
        }
    }

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

    /// <summary>User prompt with the series question bank as extra reference material.</summary>
    private IReadOnlyList<SlidePageInfo> slides = [];

    /// <summary>The configured key for an action, or its built-in default.</summary>
    public uint ShortcutKey(string id, uint fallback) =>
        ShortcutKeys.ParseBinding(shortcutOverrides.GetValueOrDefault(id), fallback).Key;

    /// <summary>SRT/VTT from the archived transcript (SessionSubtitleExporter in Core).</summary>
    private async Task ExportSubtitlesAsync(SessionSubtitleFormat format)
    {
        if (CurrentSession is not { } meta || PickSaveFile is null) return;
        var extension = format == SessionSubtitleFormat.Srt ? "srt" : "vtt";
        var name = Regex.Replace(meta.Title, "[<>:\"/\\\\|?*]", "_") + "." + extension;
        if (await PickSaveFile(UiText.Format("%@ 字幕 (*.%@)|*.%@", extension.ToUpperInvariant(), extension, extension), name) is not { } file) return;
        try
        {
            var content = subtitleExportKey switch
            {
                "原文" => SessionSubtitleContent.Original,
                "译文" => SessionSubtitleContent.Translation,
                _ => SessionSubtitleContent.Bilingual
            };
            var export = SessionSubtitleExporter.Export(meta, currentLines, format, content);
            await File.WriteAllTextAsync(file, export.Text, new System.Text.UTF8Encoding(false));
            var estimated = export.EstimatedTimingCount > 0 ? UiText.Format("，其中 %lld 条按文字长度估算结束时间", export.EstimatedTimingCount) : "";
            SetStatus(UiText.Format("字幕已导出（%@）%@。", extension.ToUpperInvariant(), estimated));
        }
        catch (Exception ex) { SetStatus(ex.Message, true); }
    }

    /// <summary>The current session's transcript lines (read-only, for exports and self-checks).</summary>
    public IReadOnlyList<TranscriptLine> CurrentLines => currentLines;

    /// <summary>SRT/VTT for the current session without a file dialog (self-check path).</summary>
    public SessionSubtitleExport ExportCurrentSubtitles(SessionSubtitleFormat format) =>
        CurrentSession is { } meta
            ? SessionSubtitleExporter.Export(meta, currentLines, format)
            : new SessionSubtitleExport("", 0, 0);

    /// <summary>Transcribe a WAV into a new archived session (also used by the unattended QA switch).</summary>
    public async Task ImportWaveFileAsync(string file)
    {
        await RunWorkAsync(async ct =>
        {
            await Settings.SaveAsync();
            SettingsStore.SaveCloudSttKey(Settings.CloudSttKey);
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
                if (settings.CloudStt.Length > 0)
                {
                    var provider = settings.CloudStt switch
                    {
                        "soniox" => CloudSTTProvider.Soniox,
                        "openai" => CloudSTTProvider.OpenAI,
                        "deepgram" => CloudSTTProvider.Deepgram,
                        _ => (CloudSTTProvider?)null
                    } ?? throw new InvalidOperationException("未知的云端识别服务。");
                    SetStatus(UiText.EnglishUi ? "Transcribing the WAV in the cloud…" : "正在通过云端识别转写 WAV…");
                    var endpoint = CloudSttEndpointPolicy.FromSetting(settings.CloudSttEndpoint);
                    var updates = await CloudSttClient.TranscribeWavAsync(
                        file, provider, SettingsStore.ReadCloudSttKey(), settings.CloudSttModel,
                        settings.CloudSttLanguage, RecognitionHotwords(), null, endpoint, ct);
                    foreach (var update in updates)
                    {
                        var start = meta.StartedAt.AddMilliseconds(Math.Max(0, update.StartMs ?? 0));
                        var end = update.EndMs is { } endMs ? meta.StartedAt.AddMilliseconds(Math.Max(0, endMs)) : (DateTimeOffset?)null;
                        var cloudLine = new TranscriptLine
                        {
                            Id = Guid.NewGuid(), Date = start, StartedAt = start, EndedAt = end,
                            Speaker = "remote", Text = Canonicalize(update.Text)
                        };
                        currentLines.Add(cloudLine);
                        Transcript.Add(new TranscriptRow(cloudLine));
                        writes.Add(Sessions.AppendTranscriptAsync(meta, cloudLine));
                    }
                    if (currentLines.Count == 0) throw new CloudTranscriptException("云端没有返回可用的文字。");
                    OnPropertyChanged(nameof(StatsText));
                    OnPropertyChanged(nameof(HasTranscript));
                    SetStatus(UiText.EnglishUi ? "WAV transcribed by the cloud and archived; raw audio was not kept." : "录音已通过云端转写并归档，未保存原始音频。");
                }
                else
                {
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

    /// <summary>纪要窗口 → 字幕条 → 隐藏 → 纪要窗口 … (LiveWindowMode in Core).</summary>
    [RelayCommand]
    private void CycleLiveMode() => LiveMode = LiveMode.Next();

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
            if (data.SkippedLines > 0) SetStatus(UiText.Format("已跳过 %lld 行损坏记录，其余内容仍可查看。", data.SkippedLines));
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
            try { LoadMeetingNotes(SessionUserContentStore.Load(Sessions.Folder(data.Meta.Id))); } catch { }
            LoadCoachSummary(data.Meta.Id);
            LoadSpeakerNames(data.Meta.Id);
            SystemPartial = MicrophonePartial = "";
            MeetingTitle = data.Meta.Title;
            PageSubtitle = (UiText.EnglishUi ? "Meeting archived · " : "会议归档 · ") + data.Meta.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            Sources.Clear();
            foreach (var source in data.Answers.LastOrDefault()?.Sources ?? []) Sources.Add(source);
            RetrievalInfo = data.Answers.LastOrDefault()?.Sources?.Count > 0 ? "历史回答的引用已保存，可查看来源。" : "";
            await LoadSessionScreenshotsAsync(data.Meta.Id);
            if (generation != selectionEpoch) return;
            LoadNotesEditor();
            SelectedSessionAnswers.Clear();
            CurrentAnswerHistory.Clear();
            foreach (var answer in data.Answers) { SelectedSessionAnswers.Add(answer); CurrentAnswerHistory.Add(answer); }
            // E18: 归档概览 and 回答列表 for the selected meeting.
            RefreshHistoryOverview();
            RefreshHistoryHitsForSelection();
            AnswerText = data.Answers.LastOrDefault()?.Answer ?? "";
            DetectedQuestion = data.Answers.LastOrDefault()?.Question ?? UiText.T("可以继续对这场会议提问。");
            SummaryText = data.Summary.Length > 0 ? data.Summary : UiText.T("这场会议还没有纪要，点击上方整理。");
            // Each scene recommends a notes format (plan §8 纪要格式); the cached copy is loaded with it.
            var mode = Enum.TryParse<MeetingMode>(data.Meta.Mode, true, out var parsed) ? parsed : MeetingMode.Meeting;
            SelectedMode = mode;
            tracker.Mode = mode;
            SelectedNotesFormat = NotesFormats.Recommended(mode);
            StructuredMarkdown = "";
            OnPropertyChanged(nameof(HasStructuredNotes));
            OnPropertyChanged(nameof(NotesFormatIsStandard));
            OnPropertyChanged(nameof(NotesFormatIsFishbone));
            OnPropertyChanged(nameof(NotesFormatIsOutline));
            OnPropertyChanged(nameof(NotesFormatIsViewpoints));
            OnPropertyChanged(nameof(StructuredHint));
            await LoadStructuredAsync(data.Meta, SelectedNotesFormat);
            NotifyMeetingPageProperties();
            BuildNotesEvidence(lines, data.Meta.Id);
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
        tracker = new QuestionCandidateTracker { Mode = SelectedMode };
        automaticScheduler = new AutomaticAnswerScheduler();
        lastMicrophoneSpeech = default;
        systemLevel = microphoneLevel = 0;
        UpdateAudioLevels(0, 0);
        LevelText = "";
        OnPropertyChanged(nameof(HasTranscript));
        OnPropertyChanged(nameof(StatsText));
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

    /// <summary>QA: seed history so the companion self-check can exercise the real previous/next handler.</summary>
    public void SeedCompanionHistoryForCheck()
    {
        companionHistory.Clear();
        companionHistory.Add(("自检问题", "自检回答"));
        companionHistoryIndex = 0;
        DetectedQuestion = "自检问题";
        AnswerText = "自检回答";
    }

    // MARK: E15 会议页（MeetingPage / MeetingTab / NotesProgressLine / SessionEvidencePane）

    /// <summary>MeetingPage keeps the jump inside the meeting page: 纪要 → 转写 tab, not the live page.</summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private int notesTabIndex;
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private int notesJumpSeconds = -1;

    public bool HasSessionAnswers => SelectedSessionAnswers.Count > 0;
    public bool HasMeetingHighlights => MeetingHighlights.Count > 0;
    public bool HasOutlineSections => StructuredPayload?.Outline.Sections.Count > 0;
    public bool HasOutlineConcepts => StructuredPayload?.Outline.Concepts.Count > 0;
    public bool HasOutlineReview => StructuredPayload?.Outline.Review.Count > 0;

    /// <summary>MeetingPage's tabs(): 纪要/复盘, 转写, 我的笔记, 会中回答 only when there are answers.</summary>
    public string NotesTabTitle => "";

    // OutlineView.swift: numbered sections → points → sub-points, then 关键概念 and 待复习.
    public ObservableCollection<NotesOutlineRow> NotesOutlineRows { get; } = [];
    public ObservableCollection<NotesOutlineConceptRow> NotesOutlineConcepts { get; } = [];
    public ObservableCollection<NotesReviewRow> NotesOutlineReview { get; } = [];

    /// <summary>MeetingPage's alert copy depends on whether the notes have user edits.</summary>
    public bool HasEditedNotesForCurrentSession
    {
        get
        {
            if (CurrentSession is not { } meta) return false;
            try
            {
                var content = SessionUserContentStore.Load(Sessions.Folder(meta.Id));
                return content.EditedNotes is not null || content.EditedSummary is not null
                    || content.EditedMarkdown is not null;
            }
            catch { return false; }
        }
    }
}
