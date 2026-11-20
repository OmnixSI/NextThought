namespace NextThought.Desktop;

public partial class AppState
{

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
                            StructuredPayload = System.Text.Json.JsonSerializer.Deserialize<StructuredNotesPayload>(payloadElement.GetRawText(), JsonDefaults.ArchiveOptions);
                            if (structured.TryGetProperty("markdown", out var savedMarkdown) && savedMarkdown.GetString() is { } text)
                                StructuredMarkdown = text;
                        }
                    }
                    if (StructuredPayload is null && document.RootElement.TryGetProperty("structuredAll", out var allStructured)
                        && allStructured.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var item in allStructured.EnumerateArray())
                        {
                            if (item.TryGetProperty("payload", out var nested)) item.TryGetProperty("format", out _);
                            var candidate = item.TryGetProperty("payload", out var payloadNode) ? payloadNode : item;
                            if (candidate.TryGetProperty("format", out var itemFormat) && itemFormat.GetString() == format.Raw())
                            {
                                StructuredPayload = System.Text.Json.JsonSerializer.Deserialize<StructuredNotesPayload>(candidate.GetRawText(), JsonDefaults.ArchiveOptions);
                                break;
                            }
                        }
                    }
                }
                catch (System.Text.Json.JsonException) { }
            }
            if (StructuredPayload is null)
            {
                var notesPath = Path.Combine(Sessions.Folder(session.Id), "notes.json");
                if (File.Exists(notesPath))
                {
                    try
                    {
                        using var notes = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(notesPath));
                        if (notes.RootElement.TryGetProperty("payload", out var notesPayload)
                            && notesPayload.TryGetProperty("format", out var notesFormat) && notesFormat.GetString() == format.Raw())
                            StructuredPayload = System.Text.Json.JsonSerializer.Deserialize<StructuredNotesPayload>(
                                notesPayload.GetRawText(), JsonDefaults.ArchiveOptions);
                    }
                    catch (System.Text.Json.JsonException) { }
                }
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
                        await File.ReadAllTextAsync(json), JsonDefaults.ArchiveOptions);
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
                    System.Text.Json.JsonSerializer.Serialize(payload, JsonDefaults.ArchiveOptions));
                summary["structured"] = payloadNode;
                var all = summary["structuredAll"] as System.Text.Json.Nodes.JsonArray
                    ?? new System.Text.Json.Nodes.JsonArray();
                for (var index = all.Count - 1; index >= 0; index--)
                    if (all[index]?["format"]?.GetValue<string>() == format.Raw()) all.RemoveAt(index);
                all.Add(payloadNode!.DeepClone());
                summary["structuredAll"] = all;
                if (payload.TranscriptSourceRevision is { Length: > 0 } sourceRevision)
                    summary["transcriptSourceRevision"] = sourceRevision;
                var generatedAt = payload.GeneratedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);
                summary["title"] ??= session.Title;
                summary["markdown"] ??= StructuredMarkdown;
                summary["questions"] ??= new System.Text.Json.Nodes.JsonArray();
                summary["generatedAt"] = generatedAt;
                summary["model"] = payload.Model;
                await File.WriteAllTextAsync(summaryPath, summary.ToJsonString(JsonDefaults.ArchiveOptions));
                await File.WriteAllTextAsync(StructuredPath(session, format), StructuredMarkdown);
                await File.WriteAllTextAsync(StructuredJsonPath(session, format),
                    System.Text.Json.JsonSerializer.Serialize(payload, JsonDefaults.ArchiveOptions));
                // Mac's notes.json is a StructuredNotes document; keep Windows' payload beside
                // the Mac fields so both sides can read the file.
                var notes = new System.Text.Json.Nodes.JsonObject
                {
                    ["templateID"] = "general",
                    ["templateName"] = format.Title(false),
                    ["language"] = payload.Language,
                    ["title"] = session.Title,
                    ["sections"] = new System.Text.Json.Nodes.JsonArray(),
                    ["corrections"] = new System.Text.Json.Nodes.JsonArray(),
                    ["droppedCount"] = payload.DroppedCount,
                    ["generatedAt"] = generatedAt,
                    ["model"] = payload.Model,
                    ["transcriptSourceRevision"] = payload.TranscriptSourceRevision,
                    ["payload"] = payloadNode!.DeepClone()
                };
                await File.WriteAllTextAsync(Path.Combine(Sessions.Folder(session.Id), "notes.json"),
                    notes.ToJsonString(JsonDefaults.ArchiveOptions));
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
        ? $"当前场景：{scene.Title} · {scene.Subtitle}" + (SceneCueStyle == CueStyle.Script ? " · 提词按句" : " · 提词按要点")
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
        workspace = workspace.AddSeries("ACL 2026 口头报告")
            .AddSeries("周一客户例会 · 华东区")
            .AddSeries("字节后端二面");
        var demoMeetings = DemoContent.Meetings();
        for (var i = 0; i < demoMeetings.Count && i < workspace.Series.Count; i++)
            workspace = workspace.Assign(demoMeetings[i].Id.ToString(), workspace.Series[i].Id);
        RefreshSeries();
        Sources.Clear();
        CurrentAnswerHistory.Clear();
        foreach (var shot in CurrentScreenshots) shot.Dispose();
        CurrentScreenshots.Clear();
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
        var demoPayload = demoFishbone;
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
        if (HistoryFilter == "有纪要") rows = rows.Where(row => row.HasSummary);
        else if (HistoryFilter == "未整理") rows = rows.Where(row => !row.HasSummary);
        var now = DateTimeOffset.Now;
        var today = now.Date;
        rows = HistoryDateFilter switch
        {
            "今天" => rows.Where(row => row.Meta.StartedAt.ToLocalTime().Date == today),
            "近 7 天" => rows.Where(row => row.Meta.StartedAt.ToLocalTime() >= now.AddDays(-7)),
            "近 30 天" => rows.Where(row => row.Meta.StartedAt.ToLocalTime() >= now.AddDays(-30)),
            "本年度" => rows.Where(row => row.Meta.StartedAt.ToLocalTime().Year == now.Year),
            _ => rows
        };
        double Duration(RecentMeetingRow row) => (row.Meta.EndedAt ?? row.Meta.StartedAt).Subtract(row.Meta.StartedAt).TotalSeconds;
        rows = HistoryLengthFilter switch
        {
            "1 分钟以内" => rows.Where(row => Duration(row) < 60),
            "1–10 分钟" => rows.Where(row => Duration(row) >= 60 && Duration(row) < 600),
            "10 分钟以上" => rows.Where(row => Duration(row) >= 600),
            _ => rows
        };
        if (query.Length > 0)
        {
            rows = rows.Where(row =>
                row.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.Meta.LocalStartedAt.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.Meta.Mode.Contains(query, StringComparison.OrdinalIgnoreCase));
        }
        rows = HistorySort switch
        {
            "最早优先" => rows.OrderBy(row => row.Meta.StartedAt),
            "时长最长" => rows.OrderByDescending(Duration),
            "时长最短" => rows.OrderBy(Duration),
            "问题最多" => rows.OrderByDescending(row => row.Meta.QuestionCount),
            _ => rows.OrderByDescending(row => row.Meta.StartedAt)
        };
        FilteredHistory.Clear();
        foreach (var row in rows) FilteredHistory.Add(row);
        HistoryGroups.Clear();
        foreach (var group in FilteredHistory.GroupBy(row =>
                 {
                     var series = workspace.SeriesOfSession(row.Meta.Id.ToString());
                     return (Id: series?.Id, Name: series?.Name ?? "未归入系列");
                 }))
            HistoryGroups.Add(new HistoryGroupRow(group.Key.Name, group.Key.Id, group.ToList()));
        OnPropertyChanged(nameof(HistorySummary));
        OnPropertyChanged(nameof(IsHistoryViewEmpty));
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

    /// <summary>
    /// System prompt plus the accepted lessons of this meeting's series (Mac
    /// Workspace.experienceNotes): pinned first, then newest, capped at 2,000 characters.
    /// </summary>
    private string BuildAnswerSystemPrompt(SessionMeta? meta)
    {
        var experience = ExperiencePromptFor(meta);
        var skills = SkillPrompt(meta);
        // Use the ported AnswerPromptBuilder instructions (safety, citation, style) instead of
        // the old three-sentence prompt; series experience and skills stay as extra sections.
        var prompt = AnswerPromptBuilder.Instructions(Settings.WebSearch);
        if (Settings.AnswerLanguage == "zh") prompt += "\n\n回答语言：中文。";
        else if (Settings.AnswerLanguage == "en") prompt += "\n\n回答语言：English。";
        if (Settings.AnswerGloss) prompt += "\n\n英语回答时，在每句下用 Markdown 引用行 `> 译：中文意思` 附中文对照。";
        if (SelectedMode == MeetingMode.Academic && Settings.UseMaterials
            && AnswerPromptBuilder.WholeMaterialOf(DocumentsForRetrieval()) is { } whole)
            prompt += "\n\n" + AnswerPromptBuilder.WholeMaterial(whole);
        if (experience.Length > 0) prompt += "\n\n这个系列已经确认的经验（优先遵守）：\n" + experience;
        return skills.Length == 0 ? prompt : prompt + "\n\n用户加载的技能（按适用部分执行）：\n" + skills;
    }

    /// <summary>User prompt with the series question bank as extra reference material.</summary>
    private IReadOnlyList<SlidePageInfo> slides = [];

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

    /// <summary>The configured key for an action, or its built-in default.</summary>
    public uint ShortcutKey(string id, uint fallback) =>
        ShortcutKeys.ParseBinding(shortcutOverrides.GetValueOrDefault(id), fallback).Key;

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
            SystemPartial = MicrophonePartial = "";
            MeetingTitle = data.Meta.Title;
            PageSubtitle = (UiText.EnglishUi ? "Meeting archived · " : "会议归档 · ") + data.Meta.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            Sources.Clear();
            foreach (var source in data.Answers.LastOrDefault()?.Sources ?? []) Sources.Add(source);
            RetrievalInfo = data.Answers.LastOrDefault()?.Sources?.Count > 0 ? "历史回答的引用已保存，可查看来源。" : "";
            LoadSessionScreenshots(data.Meta.Id);
            LoadNotesEditor();
            SelectedSessionAnswers.Clear();
            CurrentAnswerHistory.Clear();
            foreach (var answer in data.Answers) { SelectedSessionAnswers.Add(answer); CurrentAnswerHistory.Add(answer); }
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
}
