using CompanionSnapshot = NextThought.Platform.CompanionSnapshot;
namespace NextThought.Desktop;

public partial class AppState
{

    public bool CanCancel => Working || Answering;

    public string StatsText => Settings.UiLanguage == "English"
        ? Recording ? $"Recording · {currentLines.Count} lines" : $"{currentLines.Count} lines · {(CurrentSession?.AnswerCount ?? 0)} answers"
        : Recording ? $"正在记录 · {currentLines.Count} 条转写" : $"{currentLines.Count} 条转写 · {(CurrentSession?.AnswerCount ?? 0)} 条回答";

    partial void OnAnsweringChanged(bool value) { RaiseBusy(); OnPropertyChanged(nameof(CanCancel)); }

    partial void OnDetectedQuestionChanged(string value)
    {
        PublishCompanionSnapshot();
        if (!Recording || string.IsNullOrWhiteSpace(value) || CurrentSession is not { } meta) return;
        _ = Sessions.AppendQuestionAsync(meta.Id, new SessionQuestionEvent
        {
            Date = DateTimeOffset.UtcNow, Text = value, Intent = "detected", Disposition = "candidate"
        }).ContinueWith(task => _ = task.Exception, TaskScheduler.Default);
    }


    public bool HasBilingualQuestion => BilingualQuestionText.Length > 0;

    public string BilingualQuestionText
    {
        get
        {
            if (!NextThought.Core.BilingualAnswer.IsBilingual(AnswerText)) return "";
            var question = NextThought.Core.BilingualAnswer.Question(AnswerText);
            return string.IsNullOrWhiteSpace(question) ? "" : "问：" + question;
        }
    }

    partial void OnAnswerTextChanged(string value)
    {
        OnPropertyChanged(nameof(PlainAnswer));
        PublishCompanionSnapshot();
        OnPropertyChanged(nameof(IslandLines));
        OnPropertyChanged(nameof(IslandHeadline));
        OnPropertyChanged(nameof(IslandDetail));
        OnPropertyChanged(nameof(BilingualQuestionText));
        OnPropertyChanged(nameof(HasBilingualQuestion));
    }

    private DateTimeOffset recordingStartedAt;

    public string CandidateQuestion => tracker.Current is { } candidate ? candidate.FullText : "";

    public double CandidateConfidence => tracker.Current?.Assessment.Score ?? 0;

    public string CandidateConfidenceLabel => tracker.Current is { } candidate
        ? $"{candidate.Assessment.Score * 100:F0}%"
        : "—";

    public string RecordingElapsedText => Recording
        ? (DateTimeOffset.Now - recordingStartedAt).ToString(@"hh\:mm\:ss")
        : "00:00:00";

    private void OnCandidateTimer()
    {
        if (Recording) OnPropertyChanged(nameof(RecordingElapsedText));
        OnPropertyChanged(nameof(CandidateQuestion));
        OnPropertyChanged(nameof(CandidateConfidence));
        OnPropertyChanged(nameof(CandidateConfidenceLabel));
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

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartRecordingAsync()
    {
        if (Working || Recording || closing) return;
        Working = true;
        RaiseBusy();
        SessionMeta? meta = null;
        try
        {
            var planSeries = SelectedSeries is { } selectedSeriesRow ? workspace.Find(selectedSeriesRow.Id)
                : CurrentSession is { } currentMeta ? workspace.SeriesOfSession(currentMeta.Id.ToString()) : null;
            BeginPreparedMeeting(planSeries);
            var sceneDefaults = SceneDefaultsFor(SelectedMode);
            Settings.AutomaticAnswers = sceneDefaults.LiveMode == "自动回答";
            Settings.SelectedCueWidth = Settings.CueWidthChoices.FirstOrDefault(choice =>
                choice.Value == (sceneDefaults.CueStyle == "逐句" ? "wide" : "regular")) ?? Settings.CueWidthChoices[1];
            TemplateKind = sceneDefaults.NotesTemplate switch
            {
                "学术汇报" => NextThought.Core.NotesTemplateKind.Academic,
                "工作会议" => NextThought.Core.NotesTemplateKind.Work,
                _ => NextThought.Core.NotesTemplateKind.General
            };
            SelectedTemplateId = TemplateKind.Raw();
            await Settings.SaveAsync();
            // F7: a failed DPAPI read leaves an empty view; do not overwrite the stored key with it.
            if (!SettingsStore.CloudSttKeyReadFailed) SettingsStore.SaveCloudSttKey(Settings.CloudSttKey);
            // Endpoints are enumerated lazily; make sure a saved device choice is applied.
            await Settings.RefreshDevicesAsync();
            var settings = Settings.Build();
            CancelRequest();
            ResetDisplay();
            meta = NewMeta(settings, string.IsNullOrWhiteSpace(MeetingTitle) || IsNewMeetingTitle(MeetingTitle) ? null : MeetingTitle);
            MeetingTitle = meta.Title;
            // F20: building the context copies material text; keep it off the UI thread.
            var context = await Task.Run(() => new SessionContextSnapshot
            {
                Mode = meta.Mode,
                Style = sceneDefaults.AnswerStyle == "详细" ? "detailed" : "concise",
                Preferences = "",
                Profile = "",
                EffectiveMode = meta.Mode,
                Documents = PreparedDocuments(),
                Glossary = glossary
            });
            await Sessions.CreateAsync(meta, context);
            CurrentSession = meta;
            sessionLease = Sessions.AcquireLease(meta.Id);
            BeginAutoEnd(meta);
            var generation = ++epoch;
            SetStatus("正在加载本地语音模型…");
            if (settings.SeparateSpeakers) voiceTagger = await Task.Run(() => new MeetingVoiceTagger(Paths, settings.CaptureSystem ? AudioSource.System : AudioSource.Microphone));
            if (settings.TranslateSpeech) translator = new(CurrentClient(), AnswerOptions());
            BeginLiveTranslation();
            var cloudSource = settings.CaptureSystem ? AudioSource.System : AudioSource.Microphone;
            LiveSpeechEngine? ownedEngine = null;
            if (settings.CloudStt.Length == 0)
            {
                engine = await Task.Run(() => new LiveSpeechEngine(Models, settings, RecognitionHotwords()));
                if (closing) throw new OperationCanceledException("程序正在退出，已取消开始记录。");
                engine.Updated += update => Dispatcher.UIThread.Post(() => { if (generation == epoch) ReceiveTranscript(meta, update, true); });
                engine.Failed += message => Dispatcher.UIThread.Post(() => HandleAudioFailure(message));
                engine.Warning += message => Dispatcher.UIThread.Post(() => SetStatus(message, true));
                ownedEngine = engine;
            }
            else
            {
                try
                {
                    var cloudProvider = settings.CloudStt switch
                    {
                        "soniox" => CloudSTTProvider.Soniox,
                        "openai" => CloudSTTProvider.OpenAI,
                        "deepgram" => CloudSTTProvider.Deepgram,
                        _ => throw new InvalidOperationException("未知的云端识别服务。")
                    };
                    var endpoint = CloudSttEndpointPolicy.FromSetting(settings.CloudSttEndpoint);
                    cloudStt = await CloudSttSession.StartAsync(cloudProvider, SettingsStore.ReadCloudSttKey(),
                        settings.CloudSttModel, settings.CloudSttLanguage, RecognitionHotwords(), endpoint);
                    cloudStt.Updated += update => Dispatcher.UIThread.Post(() =>
                    {
                        if (generation != epoch) return;
                        var start = meta.StartedAt.AddMilliseconds(Math.Max(0, update.StartMs ?? 0));
                        var end = meta.StartedAt.AddMilliseconds(Math.Max(0, update.EndMs ?? ((update.StartMs ?? 0) + 500)));
                        ReceiveTranscript(meta, new TranscriptUpdate(Guid.NewGuid(), cloudSource, update.Text,
                            update.IsFinal, start, end), true);
                    });
                    cloudStt.Failed += message => Dispatcher.UIThread.Post(async () =>
                    {
                        if (cloudStt is not { } failed) return;
                        cloudStt = null;
                        try { await failed.DisposeAsync(); } catch { }
                        if (!Recording) return;
                        if (ModelReady)
                        {
                            try
                            {
                                engine = await Task.Run(() => new LiveSpeechEngine(Models, settings, RecognitionHotwords()));
                                engine.Updated += update => Dispatcher.UIThread.Post(() => { if (generation == epoch) ReceiveTranscript(meta, update, true); });
                                engine.Failed += failure => Dispatcher.UIThread.Post(() => HandleAudioFailure(failure));
                                engine.Warning += warning => Dispatcher.UIThread.Post(() => SetStatus(warning, true));
                                ownedEngine = engine;
                                SetStatus("云端识别中断，已回退本机识别：" + message, true);
                                return;
                            }
                            catch (Exception localError)
                            {
                                message += "；本机回退失败：" + localError.Message;
                            }
                        }
                        HandleAudioFailure("云端识别中断：" + message);
                    });
                    if (settings.CaptureSystem && settings.CaptureMicrophone)
                    {
                        cloudSttMicrophone = await CloudSttSession.StartAsync(cloudProvider, SettingsStore.ReadCloudSttKey(),
                            settings.CloudSttModel, settings.CloudSttLanguage, RecognitionHotwords(), endpoint);
                        cloudSttMicrophone.Updated += update => Dispatcher.UIThread.Post(() =>
                        {
                            if (generation != epoch) return;
                            var start = meta.StartedAt.AddMilliseconds(Math.Max(0, update.StartMs ?? 0));
                            var end = meta.StartedAt.AddMilliseconds(Math.Max(0, update.EndMs ?? ((update.StartMs ?? 0) + 500)));
                            ReceiveTranscript(meta, new TranscriptUpdate(Guid.NewGuid(), AudioSource.Microphone, update.Text,
                                update.IsFinal, start, end), true);
                        });
                        cloudSttMicrophone.Failed += message => Dispatcher.UIThread.Post(() => SetStatus("麦克风云端识别中断：" + message, true));
                    }
                    SetStatus("云端识别已连接；转写发送到所选服务。");
                }
                catch (Exception cloudError)
                {
                    cloudStt = null;
                    if (ModelReady)
                    {
                        engine = await Task.Run(() => new LiveSpeechEngine(Models, settings, RecognitionHotwords()));
                        engine.Updated += update => Dispatcher.UIThread.Post(() => { if (generation == epoch) ReceiveTranscript(meta, update, true); });
                        engine.Failed += message => Dispatcher.UIThread.Post(() => HandleAudioFailure(message));
                        engine.Warning += message => Dispatcher.UIThread.Post(() => SetStatus(message, true));
                        ownedEngine = engine;
                        SetStatus("云端识别启动失败，已回退本机识别：" + cloudError.Message, true);
                    }
                    else
                    {
                        throw new InvalidOperationException("云端识别启动失败，且本机语音模型未就绪：" + cloudError.Message, cloudError);
                    }
                }
            }

            capture = new AudioCaptureService();
            capture.FrameReady += frame =>
            {
                voiceTagger?.Add(frame);
                ownedEngine?.Accept(frame);
                if (!ListeningPaused)
                {
                    if (frame.Source == AudioSource.System && cloudStt is { } cloud)
                        _ = cloud.SendAsync(frame.Samples);
                    else if (frame.Source == AudioSource.Microphone && cloudSttMicrophone is { } mic)
                        _ = mic.SendAsync(frame.Samples);
                    else if (frame.Source == cloudSource && cloudStt is { } fallback)
                        _ = fallback.SendAsync(frame.Samples);
                }
            };
            capture.Failed += message => Dispatcher.UIThread.Post(() => HandleAudioFailure(message));
            capture.LevelChanged += (source, level) => Dispatcher.UIThread.Post(() =>
            {
                if (source == AudioSource.System) systemLevel = level; else microphoneLevel = level;
                LevelText = $"系统 {systemLevel * 100:F0} · 麦克风 {microphoneLevel * 100:F0}";
            });
            await Task.Run(() => capture.Start(settings));
            if (capture.LastError is { } captureError) throw new IOException(captureError);
            recordingStartedAt = DateTimeOffset.Now;
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
            if (cloudStt != null) { await cloudStt.DisposeAsync(); cloudStt = null; }
            if (cloudSttMicrophone != null) { await cloudSttMicrophone.DisposeAsync(); cloudSttMicrophone = null; }
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

    private bool CanStart() => IsIdle && !Answering && (ModelReady || Settings.SelectedCloudStt is { Value.Length: > 0 });

    private async Task StopRecordingCoreAsync(string? reason = null)
    {
        BuildCoachSummary();
        if (Stopping || CurrentSession is null || !Recording) return;
        Stopping = true;
        candidateTimer.Stop();
        automaticScheduler.Clear();
        RaiseBusy();
        var meta = CurrentSession;
        try
        {
            if (capture != null) { await capture.DisposeAsync(); capture = null; }
            if (cloudStt != null) { await cloudStt.DisposeAsync(); cloudStt = null; }
            if (cloudSttMicrophone != null) { await cloudSttMicrophone.DisposeAsync(); cloudSttMicrophone = null; }
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
        else if (Settings.AutoTranslateForeign && LooksForeign(line.Text))
        {
            using var onDemand = new MeetingTranslator(CurrentClient(), AnswerOptions());
            writes.Add(TranslateLineAsync(meta, line, onDemand, write));
        }
        _ = write.ContinueWith(task => Dispatcher.UIThread.Post(() => HandleAudioFailure("会议写入失败：" + task.Exception!.GetBaseException().Message)), TaskContinuationOptions.OnlyOnFaulted);
        if (automatic && update.Source == AudioSource.System && Recording && !Stopping) ApplyCandidate(tracker.Ingest(update.Text, true, update.Id, DateTimeOffset.UtcNow));
        NoteSpeechHeard(line);
        TryFollowSlide(text);
        OnPropertyChanged(nameof(StatsText));
        OnPropertyChanged(nameof(HasTranscript));
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

    private static bool LooksForeign(string text)
    {
        var letters = text.Count(char.IsLetter);
        if (letters < 4) return false;
        var cjk = text.Count(character => character is >= '\u4E00' and <= '\u9FFF');
        return cjk * 5 < letters;
    }

    /// <summary>E13: make a fresh screenshot the subject of the composer question.</summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string screenContextText = "";

    public void PromptAboutScreenshot(string? screenText = null)
    {
        ScreenContextText = (screenText ?? "").Trim();
        QuestionInput = "请解释这张截图，指出关键信息、数字或风险。";
        Section = AppSection.Live;
        SetStatus(ScreenContextText.Length > 0 ? "截图与 OCR 文本已就绪；可直接发送。" : "截图已就绪；可直接发送，或补充你的问题。");
    }

    private bool CanTranslate() => IsIdle && !Answering && currentLines.Count > 0;

    public AnswerOptions AnswerOptions(bool notes = false)
    {
        var settings = Settings.Build();
        if (settings.UseCodex) return new("https://codex.local", settings.CodexModel.Length > 0 ? settings.CodexModel : "default", "");
        return new(settings.BaseUrl, notes && settings.NotesModel.Length > 0 ? settings.NotesModel : settings.Model, Settings.ApiKey.Trim(), settings.ReasoningEffort);
    }


    private IReadOnlyList<ContextDocument> PreparedDocuments()
    {
        if (ActivePreparedDocuments is { } prepared)
        {
            var carried = prepared.ToList();
            if (PreviousTranscriptContext() is { } extra && carried.All(document => document.Id != extra.Id)) carried.Add(extra);
            return carried;
        }
        var documents = Library.DocumentsForRetrieval().Select(document => new ContextDocument
        {
            Id = document.Id,
            Name = document.Name,
            Pages = document.Blocks.Select(block => block.Text).ToList(),
            Enabled = true
        }).ToList();
        if (PreviousTranscriptContext() is { } previous) documents.Add(previous);
        return documents;
    }


    public IAnswerClient CurrentClient() => Settings.UseCodex ? new CodexAnswerClient() : Api;

    [RelayCommand(CanExecute = nameof(CanAsk))]
    private async Task AskAsync()
    {
        var question = QuestionInput.Trim();
        if (question.Length == 0) { SetStatus("请先输入问题。"); return; }
        await AnswerAsync(question, false);
    }

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
                    documents: DocumentsForRetrieval());
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
                WebResults.Clear();
                foreach (var entry in web) WebResults.Add(entry);
                OnPropertyChanged(nameof(WebStatusText));
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
                var answerRecord = new AnswerRecord
                {
                    Question = question, Answer = result, Model = options.Model,
                    Seconds = clock.Elapsed.TotalSeconds, Automatic = automatic, Sources = cited
                };
                await Sessions.AppendAnswerAsync(meta, answerRecord);
                CurrentAnswerHistory.Add(answerRecord);
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

    private string BuildAnswerUserPrompt(SessionMeta? meta, string transcript, IReadOnlyList<SourceCitation> sources, string question,
        IReadOnlyList<WebResult>? web = null)
    {
        var slide = CurrentSlide is { } page ? $"\n当前幻灯片（第 {page.Number} 页，{page.DocumentName}）：\n{page.Text}\n" : "";
        if (ScreenContextText.Length > 0) slide += $"\n刚截取的屏幕 OCR 文本：\n{ScreenContextText[..Math.Min(ScreenContextText.Length, 3000)]}\n";
        var bank = QuestionBankPromptFor(meta);
        var bankBlock = bank.Length == 0 ? "" : $"\n这个系列的题库（历次复盘确认过的回答）：\n{bank}\n";
        var webBlock = web is { Count: > 0 }
            ? "\n联网搜索结果（可能过时，引用时标注链接）：\n" + string.Join("\n", web.Select(result =>
                $"- {result.Title}（{result.Url}）：{result.Snippet}")) + "\n"
            : "";
        return $"最近的会议转写：\n{transcript}\n\n参考资料：\n{KeywordIndex.Context(sources)}\n{slide}{bankBlock}{webBlock}\n当前问题：{question}";
    }

    private bool CanSummarize() => IsIdle && !Answering && CurrentSession != null && currentLines.Count > 0;

    private bool CanImportWave() => IsIdle && !Answering && (ModelReady || Settings.SelectedCloudStt is { Value.Length: > 0 });

    private bool CanBackup() => IsIdle && !Answering;

    private bool CanRestore() => IsIdle && !Answering;

    [RelayCommand]
    private async Task CheckServiceAsync() => await AnswerAsync("请只回复：连接成功", false, true);

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
    private async Task LookUpDisplayedAnswerAsync()
    {
        if (AnswerText.Length == 0) { SetStatus("还没有可查证的回答。"); return; }
        if (WebSearch() is not { } provider) { SetStatus("联网未配置：先在设置里保存 Tavily 密钥。", true); return; }
        SetStatus("正在查证当前回答…");
        try
        {
            var question = DetectedQuestion.Length > 0 ? DetectedQuestion : "核对上一条回答的事实";
            var response = await provider.SearchAsync(question + " " + AnswerText[..Math.Min(AnswerText.Length, 300)], 5);
            if (response.Error is not null) { SetStatus(response.Error, true); return; }
            WebResults.Clear();
            foreach (var entry in response.Results) WebResults.Add(entry);
            OnPropertyChanged(nameof(WebStatusText));
            SetStatus($"查证完成：{response.Results.Count} 条联网来源。");
        }
        catch (Exception ex) { SetStatus("联网查证失败：" + ex.Message, true); }
    }

    [RelayCommand]
    private async Task RegenerateAnswerAsync()
    {
        if (Answering) return;
        await AnswerAsync(string.IsNullOrWhiteSpace(DetectedQuestion) ? "请总结当前讨论" : DetectedQuestion, false);
    }

    [RelayCommand]
    private async Task ShorterAnswerAsync() => await RefineAsync(AnswerRefinement.Shorter);

    /// <summary>C14: the one-tap refinements of the Mac teleprompter footer.</summary>
    public async Task RefineAsync(AnswerRefinement refinement)
    {
        if (Answering) return;
        var original = string.IsNullOrWhiteSpace(DetectedQuestion) ? QuestionInput : DetectedQuestion;
        await AnswerAsync(refinement.QuestionFor(original), false);
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

    private void CancelRequest()
    {
        ++requestEpoch;
        var previous = requestCancellation;
        requestCancellation = null;
        previous?.Cancel();
        Answering = false;
    }

    private readonly List<(string Question, string Answer)> companionHistory = [];

    private async Task<bool> HandleCompanionActionCoreAsync(string action)
    {
        switch (action)
        {
            case "answer":
                await AnswerAsync(string.IsNullOrWhiteSpace(DetectedQuestion) ? "请总结当前讨论" : DetectedQuestion, false);
                return true;
            case "regenerate":
                // F11: regenerate is not just another answer; ask for a different version.
                await AnswerAsync("请基于当前讨论重新生成一版与上一版不同的回答；" +
                    (string.IsNullOrWhiteSpace(DetectedQuestion) ? "请总结当前讨论。" : DetectedQuestion), false);
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
        Position: companionHistory.Count == 0 ? "" : $"{companionHistoryIndex + 1} / {companionHistory.Count}",
        SessionID: CurrentSession?.Id.ToString("D"),
        Sources: Sources.Count == 0 ? null : string.Join("、", Sources.Take(3).Select(x => x.Label)),
        AnswerID: CurrentSession is { } meta ? $"{meta.Id:N}:{meta.AnswerCount}" : null,
        Phase: Answering ? "generating" : Recording ? "recording" : "idle");

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

    /// <summary>E24: when the current meeting belongs to a series, retrieve only that series' documents.</summary>
    public IReadOnlyList<NextThought.Core.MaterialDocument> DocumentsForRetrieval()
    {
        if (ActiveCarry?.Materials == false) return [];
        var documents = Library.DocumentsForRetrieval();
        var sessionId = CurrentSession?.Id.ToString();
        if (sessionId is null) return documents;
        var series = workspace.SeriesOfSession(sessionId);
        return series is null ? documents : NextThought.Core.RetrievalScope.ForSeries(documents, series.DocumentIds);
    }
}
