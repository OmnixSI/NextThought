using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// 会议纪要：模板、结构化整理与纪要页导航（port of Sources/NextThought/AppState+Notes.swift）。
/// H2: this partial lives beside its Swift counterpart; the code was moved verbatim from
/// AppState.Templates.cs and AppState.StructuredNotes.cs.
/// </summary>
public partial class AppState
{
    [ObservableProperty] private NotesTemplateKind templateKind = NotesTemplateKind.General;
    [ObservableProperty] private string selectedTemplateId = NotesTemplateKind.General.Raw();
    [ObservableProperty] private bool templating;
    [ObservableProperty] private string customTemplateText = "";
    [ObservableProperty] private string customTemplateStatus = "";
    public ObservableCollection<NotesTemplateChoice> EditableTemplates { get; } = [];
    public ObservableCollection<TemplateSectionRow> EditSections { get; } = [];
    [ObservableProperty] private NotesTemplateChoice? editTemplateChoice;
    [ObservableProperty] private string editTemplateName = "";

    private NotesTemplateStore? templateStore;

    public ObservableCollection<NotesTemplateChoice> TemplateChoices { get; } = BuildChoices();

    private static ObservableCollection<NotesTemplateChoice> BuildChoices() =>
        new(NotesTemplateKinds.Selectable.Select(kind => new NotesTemplateChoice(NotesTemplate.BuiltIn(kind), kind)));

    public NotesTemplateChoice? SelectedTemplate
    {
        get => TemplateChoices.FirstOrDefault(choice => choice.Template.Id == SelectedTemplateId);
        set
        {
            if (value is null) return;
            SelectedTemplateId = value.Template.Id;
            if (value.Kind is { } kind) TemplateKind = kind;
        }
    }

    partial void OnSelectedTemplateIdChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedTemplate));
        OnPropertyChanged(nameof(TemplateIsDefault));
    }

    partial void OnTemplateKindChanged(NotesTemplateKind value)
    {
        SelectedTemplateId = value.Raw();
        OnPropertyChanged(nameof(TemplateIsDefault));
    }

    /// <summary>True when the selected template is the one this scene recommends.</summary>
    public bool TemplateIsDefault =>
        SelectedTemplate?.Kind is { } kind && kind == MeetingTypeDefaults.TemplateFor(SelectedMode);

    /// <summary>The scene decides the template until the user picks another one.</summary>
    private void ApplyDefaultTemplate()
    {
        var preferred = DefaultTemplateIdFor(SelectedMode);
        if (TemplateChoices.FirstOrDefault(choice => choice.Template.Id == preferred) is { } template)
        {
            SelectedTemplateId = template.Template.Id;
            if (template.Kind is { } preferredKind) TemplateKind = preferredKind;
            return;
        }
        TemplateKind = MeetingTypeDefaults.TemplateFor(SelectedMode);
    }

    [RelayCommand]
    private void ResetTemplate() => ApplyDefaultTemplate();

    private void LoadCustomTemplates()
    {
        templateStore = new NotesTemplateStore(Paths.Root);
        var custom = templateStore.Load();
        foreach (var template in custom)
            if (TemplateChoices.All(choice => choice.Template.Id != template.Id))
                TemplateChoices.Add(new NotesTemplateChoice(template, null));
        CustomTemplateText = NotesTemplateStore.ToText(custom);
        CustomTemplateStatus = custom.Count == 0 ? UiText.T("尚无自定义模板。") : UiText.Format("%lld 个自定义模板。", custom.Count);
        RefreshEditableTemplates();
    }

    [RelayCommand]
    private void SaveCustomTemplates()
    {
        try
        {
            var imported = NotesTemplateStore.ParseText(CustomTemplateText);
            templateStore ??= new NotesTemplateStore(Paths.Root);
            imported = NotesTemplateStore.PreserveIdentity(imported, templateStore.Load());
            foreach (var choice in TemplateChoices.Where(choice => choice.Kind is null).ToList()) TemplateChoices.Remove(choice);
            foreach (var template in imported) TemplateChoices.Add(new NotesTemplateChoice(template, null));
            templateStore.Save(imported);
            CustomTemplateStatus = imported.Count == 0 ? UiText.T("自定义模板已清空。") : UiText.Format("已保存 %lld 个自定义模板。", imported.Count);
            RefreshEditableTemplates();
            SetStatus(CustomTemplateStatus);
        }
        catch (Exception ex) { CustomTemplateStatus = "模板保存失败：" + ex.Message; SetStatus(CustomTemplateStatus, true); }
    }

    /// <summary>Adds imported custom templates (same id replaces) and persists them.</summary>
    public void AddTemplates(IEnumerable<NotesTemplate> templates)
    {
        templateStore ??= new NotesTemplateStore(Paths.Root);
        foreach (var template in templates)
        {
            var existing = TemplateChoices.FirstOrDefault(choice => choice.Template.Id == template.Id);
            if (existing is not null) TemplateChoices.Remove(existing);
            TemplateChoices.Add(new NotesTemplateChoice(template, null));
        }
        templateStore.Save(TemplateChoices.Where(choice => choice.Kind is null).Select(choice => choice.Template).ToList());
        CustomTemplateText = NotesTemplateStore.ToText(TemplateChoices.Where(choice => choice.Kind is null).Select(choice => choice.Template).ToList());
        CustomTemplateStatus = UiText.Format("已导入模板，共 %lld 个自定义模板。", TemplateChoices.Count(choice => choice.Kind is null));
        RefreshEditableTemplates();
        SetStatus(CustomTemplateStatus);
    }

    private void RefreshEditableTemplates()
    {
        EditableTemplates.Clear();
        foreach (var choice in TemplateChoices.Where(choice => choice.Kind is null))
            EditableTemplates.Add(choice);
    }

    partial void OnEditTemplateChoiceChanged(NotesTemplateChoice? value)
    {
        if (value is null) return;
        EditTemplateName = value.Template.Name;
        EditSections.Clear();
        foreach (var section in value.Template.Sections)
            EditSections.Add(new TemplateSectionRow(section));
    }

    [RelayCommand]
    private void NewTemplate()
    {
        EditTemplateChoice = null;
        EditTemplateName = "新模板";
        EditSections.Clear();
        EditSections.Add(new TemplateSectionRow("议题", "写下讨论的主题。"));
    }

    [RelayCommand]
    private void AddTemplateSection()
    {
        EditSections.Add(new TemplateSectionRow("新章节", "写这一节要整理什么。"));
    }

    public void RemoveTemplateSection(TemplateSectionRow row) => EditSections.Remove(row);

    public void MoveTemplateSection(TemplateSectionRow row, int delta)
    {
        var index = EditSections.IndexOf(row);
        var target = Math.Clamp(index + delta, 0, EditSections.Count - 1);
        if (index >= 0 && target != index) EditSections.Move(index, target);
    }

    [RelayCommand]
    private void SaveEditedTemplate()
    {
        var name = EditTemplateName.Trim();
        if (name.Length == 0) { SetStatus("模板需要名字。", true); return; }
        var sections = EditSections
            .Where(row => row.Title.Trim().Length > 0 && row.Instruction.Trim().Length > 0)
            .Select((row, index) => new NotesSectionSpec(
                string.IsNullOrEmpty(row.Id) ? "section-" + (index + 1) : row.Id,
                row.Title.Trim(), row.Instruction.Trim(), row.Kind, Carry: row.Carry))
            .ToList();
        if (sections.Count == 0) { SetStatus("模板至少需要一个有标题和要求的章节。", true); return; }
        var id = EditTemplateChoice?.Template.Id ?? "custom-" + Guid.NewGuid().ToString("N");
        var template = new NotesTemplate(id, name, sections);
        AddTemplates([template]);
        EditTemplateChoice = EditableTemplates.FirstOrDefault(choice => choice.Template.Id == id);
        SetStatus(UiText.Format("模板「%@」已保存。", name));
    }

    [RelayCommand(CanExecute = nameof(CanTemplate))]
    private async Task SummarizeWithTemplateAsync()
    {
        if (CurrentSession is not { } meta || currentLines.Count == 0) return;
        CancelRequest();
        var generation = ++requestEpoch;
        var cancellation = new CancellationTokenSource();
        requestCancellation = cancellation;
        Answering = Templating = true;
        ProgressIndeterminate = true;
        Section = AppSection.Notes;
        RaiseBusy();
        try
        {
            await Task.WhenAll(writes.ToArray());
            var transcript = BuildNumberedTranscript(meta);
            // E18: 纪要语言 from the meeting menu forces the summary language for this run.
            var english = summaryLanguageOverride ?? (AnswerLanguage.Spoken(transcript) == "English");
            var template = SelectedTemplate?.Template ?? NotesTemplate.BuiltIn(TemplateKind, english);
            var header = english
                ? $"# {meta.Title}\n\nMeeting: {meta.StartedAt.ToLocalTime():yyyy-MM-dd HH:mm} · Template: {template.Name}"
                : UiText.Format("# %@\n\n会议时间：%@ · 模板：%@", meta.Title, meta.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), template.Name);
            var settings = Settings.Build();
            var answers = string.Join("\n\n", SelectedSessionAnswers.TakeLast(40)
                .Where(answer => !string.IsNullOrWhiteSpace(answer.Answer))
                .Select(answer => "Q\uFF1A" + Truncate(answer.Question, 300) + "\nA\uFF1A" + Truncate(answer.Answer, 600)));
            var system = NotesPrompt.OrganizeSystem(template, english, inPerson: !settings.CaptureSystem, meetingDate: meta.StartedAt);
            var chunks = NotesPrompt.Chunks(transcript);
            var reply = "";
            if (chunks.Count == 1)
            {
                reply = await CurrentClient().AnswerAsync(AnswerOptions(true), system,
                    NotesPrompt.OrganizeUser(header, transcript, answers), null, cancellation.Token, 240);
            }
            else
            {
                var partials = new List<string>();
                for (var index = 0; index < chunks.Count; index++)
                {
                    if (generation != requestEpoch) return;
                    SetStatus(UiText.Format("正在按《%@》整理…（第 %lld/%lld 段）", template.Name, index + 1, chunks.Count));
                    partials.Add(await CurrentClient().AnswerAsync(AnswerOptions(true), system,
                        NotesPrompt.PartialUser(header, chunks[index], index, chunks.Count), null,
                        cancellation.Token, 240));
                }
                SetStatus(UiText.Format("正在按《%@》合并分段结果…", template.Name));
                reply = await CurrentClient().AnswerAsync(AnswerOptions(true), system,
                    NotesPrompt.MergeUser(header, partials), null, cancellation.Token, 240);
            }
            if (generation != requestEpoch) return;
            var doc = NotesJson.Parse(reply, template, english ? "en" : "zh");
            if (doc is null)
            {
                SetStatus("这次回复不是可用的 JSON，可以重试；原来的纪要保持不变。", true);
                return;
            }
            // 「周四」「下周一前」按会议日期换成具体日期（模糊说法保持原样）。
            doc = NotesDueDates.Apply(doc, meta.StartedAt);
            var duration = meta.EndedAt is { } ended ? (ended - meta.StartedAt).TotalSeconds : 0;
            var facts = NotesMarkdown.Facts(doc, meta.StartedAt, duration, meta.InPerson, english);
            var title = string.IsNullOrWhiteSpace(meta.Title) ? (doc.Title.Length > 0 ? doc.Title : template.Name) : meta.Title.Trim();
            var markdown = NotesMarkdown.Render(doc, title, facts);
            SummaryText = markdown;
            await Sessions.SaveSummaryAsync(meta, markdown, AnswerOptions(true).Model, doc, cancellation.Token);
            meta.SummaryState = "ready";
            // The template marks sections that carry into the series (NotesCarry): queue them for review.
            var (lessons, bank) = NotesKnowledge.Extract(doc, template);
            CollectFromNotes(meta, doc, template);
            if (lessons.Count + bank.Count > 0)
                await Sessions.SaveSummaryKnowledgeAsync(meta, lessons, bank, appliedToWorkspace: false, cancellation.Token);
            SetStatus(UiText.Format("已按《%@》整理，可导出 Markdown。", template.Name));
        }
        catch (OperationCanceledException)
        {
            if (generation == requestEpoch) SetStatus("整理已取消。");
        }
        catch (Exception ex)
        {
            if (generation == requestEpoch) SetStatus(ex.Message, true);
        }
        finally
        {
            cancellation.Dispose();
            if (generation == requestEpoch)
            {
                requestCancellation = null;
                Answering = Templating = false;
                ProgressIndeterminate = false;
                RaiseBusy();
            }
        }
    }

    private static string Truncate(string value, int limit) =>
        value.Length <= limit ? value : value[..limit];

    private bool CanTemplate() => IsIdle && !Answering && CurrentSession is not null && currentLines.Count > 0;

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
        NotifyMeetingPageProperties();
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
            SetStatus(UiText.Format("正在整理成%@…", format.Title(false)));
            var transcript = BuildNumberedTranscript(session);
            var english = AnswerLanguage.Spoken(transcript) == "English";
            var header = english
                ? $"# {session.Title}\n\nMeeting: {session.StartedAt.ToLocalTime():yyyy-MM-dd HH:mm} · Scene: {SceneTitle}"
                : UiText.Format("# %@\n\n会议时间：%@ · 场景：%@", session.Title, session.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), SceneTitle);
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
                SetStatus(UiText.Format("整理出的%@缺少可核对的转写依据，已丢弃；可重试，或改用文字纪要。", format.Title(false)), true);
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
                ? UiText.Format("已整理成%@；%lld 条找不到原话依据的内容已丢弃。", format.Title(false), payload.DroppedCount)
                : UiText.Format("已整理成%@，可复制或导出。", format.Title(false)));
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

    /// <summary>Jump from a notes timestamp / evidence / outline line to the transcript inside this page.</summary>
    public void ShowNotesTranscriptAt(int? seconds)
    {
        NotesTabIndex = 1;
        NotesJumpSeconds = seconds ?? 0;
        var target = NotesJumpSeconds;
        TranscriptHighlightSeconds = target;
        foreach (var row in Transcript) row.IsHighlighted = false;
        if (Transcript.Count == 0) return;
        TranscriptRow? nearest = null;
        var best = int.MaxValue;
        foreach (var row in Transcript)
        {
            var offset = (int)Math.Abs((row.Date - (CurrentSession?.StartedAt ?? row.Date)).TotalSeconds - target);
            if (offset >= best) continue;
            best = offset;
            nearest = row;
        }
        if (nearest is not null) nearest.IsHighlighted = true;
        NotesJumpRequested?.Invoke(target);
    }

    /// <summary>Played by the view: scroll the transcript list to the jumped line.</summary>
    public event Action<int>? NotesJumpRequested;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private int transcriptHighlightSeconds = -1;

    public string NotesJumpNotice => NotesJumpSeconds < 0
        ? ""
        : UiText.Format("已跳到原话 · %@", NotesOutlineRow.TimeText(NotesJumpSeconds));
    public bool HasNotesJumpNotice => NotesJumpSeconds >= 0;

    partial void OnNotesJumpSecondsChanged(int value)
    {
        OnPropertyChanged(nameof(NotesJumpNotice));
        OnPropertyChanged(nameof(HasNotesJumpNotice));
    }

    /// <summary>SessionInfoView.swift: 会议 / 纪要 / 存储 / 归档 groups for the current meeting.</summary>
    public IReadOnlyList<(string Group, string Label, string Value)> CurrentSessionInfoGroups()
    {
        if (CurrentSession is not { } meta) return [];
        var ended = meta.EndedAt;
        var duration = ended is { } end ? end - meta.StartedAt : DateTimeOffset.Now - meta.StartedAt;
        var players = Transcript.Select(row => row.Label.Split(" · ")[0]).Distinct().ToList();
        var speakers = rowsSpeakerLabels();
        var folder = Sessions.Folder(meta.Id);
        var size = 0L;
        try
        {
            if (Directory.Exists(folder))
                size = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                    .Sum(path => { try { return new FileInfo(path).Length; } catch { return 0L; } });
        }
        catch { }
        var storage = size >= 1024 * 1024
            ? UiText.Format("%.1f MB", size / 1024.0 / 1024.0)
            : UiText.Format("%lld KB", size / 1024);
        var notes = CurrentSession?.SummaryState switch
        {
            "ready" => UiText.T("已有纪要"),
            "pending" => Summarizing ? UiText.T("正在整理纪要") : UiText.T("整理已中断，可重试"),
            "failed" => UiText.Format("整理失败：%@", CurrentSession.SummaryError ?? UiText.T("原因不可用")),
            _ => UiText.T("没有纪要")
        };
        return
        [
            (UiText.T("会议"), UiText.T("开始时间"), meta.LocalStartedAt),
            (UiText.T("会议"), UiText.T("时长"), DurationText(duration)),
            (UiText.T("会议"), UiText.T("发言人"), speakers),
            (UiText.T("会议"), UiText.T("模式"), meta.Mode),
            (UiText.T("纪要"), UiText.T("状态"), notes),
            (UiText.T("存储"), UiText.T("存储位置"), folder),
            (UiText.T("存储"), UiText.T("存储大小"), storage),
            (UiText.T("归档"), UiText.T("写入状态"), UiText.Format("已读取归档；%lld 项待写入", writes.Count))
        ];
    }

    private string rowsSpeakerLabels()
    {
        var names = new List<string>();
        foreach (var row in Transcript)
        {
            var label = row.Speaker switch
            {
                "remote" => UiText.T("对方"),
                "room" => UiText.T("现场"),
                _ => UiText.T("我")
            };
            if (!names.Contains(label)) names.Add(label);
        }
        return names.Count > 0 ? string.Join("、", names) : UiText.T("暂无可识别发言人");
    }

    /// <summary>Personal-notes tab: a saved highlight jumps to its transcript line.</summary>
    public void JumpToMeetingHighlight(NextThought.Core.SessionHighlight highlight) =>
        ShowNotesTranscriptAt(highlight.OffsetSeconds);

    /// <summary>Evidence pane: jump by line id (preferred) or by the stored mm:ss location.</summary>
    public void JumpToEvidenceLine(Guid? lineId, string? location)
    {
        if (lineId is { } id && Transcript.FirstOrDefault(row => row.Id == id) is { } target)
        {
            var offset = (int)Math.Max(0, (target.Date - (CurrentSession?.StartedAt ?? target.Date)).TotalSeconds);
            ShowNotesTranscriptAt(offset);
            return;
        }
        if (location is not null && TimeSpan.TryParse(location, out var span))
        {
            ShowNotesTranscriptAt((int)span.TotalSeconds);
            return;
        }
        ShowNotesTranscriptAt(0);
    }

    /// <summary>E15 evidence pane rows notify these when the projection changes.</summary>
    public void NotifyMeetingPageProperties()
    {
        OnPropertyChanged(nameof(HasSessionAnswers));
        OnPropertyChanged(nameof(HasMeetingHighlights));
        OnPropertyChanged(nameof(HasOutlineSections));
        OnPropertyChanged(nameof(HasOutlineConcepts));
        OnPropertyChanged(nameof(HasOutlineReview));
        OnPropertyChanged(nameof(HasEditedNotesForCurrentSession));
        RebuildNotesOutline();
    }

    private void RebuildNotesOutline()
    {
        NotesOutlineRows.Clear();
        NotesOutlineConcepts.Clear();
        NotesOutlineReview.Clear();
        if (StructuredPayload?.Outline is not { } outline) return;
        var number = 0;
        foreach (var section in outline.Sections)
        {
            number++;
            var lines = new List<NotesOutlineLineRow>();
            foreach (var point in section.Points)
            {
                lines.Add(new NotesOutlineLineRow(point.Text, point.At, false));
                foreach (var child in point.Children) lines.Add(new NotesOutlineLineRow(child.Text, child.At, true));
            }
            NotesOutlineRows.Add(new NotesOutlineRow(number, section.Title, section.At, lines));
        }
        foreach (var concept in outline.Concepts)
            NotesOutlineConcepts.Add(new NotesOutlineConceptRow(concept.Term, concept.Meaning, concept.At));
        foreach (var review in outline.Review)
            NotesOutlineReview.Add(new NotesReviewRow(review.Text, review.At));
    }

}

public sealed record NotesTemplateChoice(NotesTemplate Template, NotesTemplateKind? Kind)
{
    public string Title => UiText.T(Template.Name);
    public override string ToString() => Title;
}


/// <summary>One editable section in the custom-template editor.</summary>
public sealed partial class TemplateSectionRow : ObservableObject
{
    public TemplateSectionRow(string title, string instruction)
    {
        Title = title;
        Instruction = instruction;
    }

    public TemplateSectionRow(NotesSectionSpec section)
    {
        Id = section.Id;
        Title = section.Title;
        Instruction = section.Instruction;
        Kind = section.Kind;
        Carry = section.Carry;
    }

    public string Id { get; } = "";
    [ObservableProperty] private string title = "";
    [ObservableProperty] private string instruction = "";
    public NotesSectionKind Kind { get; } = NotesSectionKind.Items;
    public NotesCarry Carry { get; } = NotesCarry.None;
}
