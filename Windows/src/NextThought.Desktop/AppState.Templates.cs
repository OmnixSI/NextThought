using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>按模板整理纪要（NotesTemplates）：章节由模板决定，模型只填内容。</summary>
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
        TemplateKind = MeetingTypeDefaults.TemplateFor(SelectedMode);
    }

    [RelayCommand]
    private void ResetTemplate() => TemplateKind = MeetingTypeDefaults.TemplateFor(SelectedMode);

    private void LoadCustomTemplates()
    {
        templateStore = new NotesTemplateStore(Paths.Root);
        var custom = templateStore.Load();
        foreach (var template in custom)
            if (TemplateChoices.All(choice => choice.Template.Id != template.Id))
                TemplateChoices.Add(new NotesTemplateChoice(template, null));
        CustomTemplateText = NotesTemplateStore.ToText(custom);
        CustomTemplateStatus = custom.Count == 0 ? "尚无自定义模板。" : $"{custom.Count} 个自定义模板。";
        RefreshEditableTemplates();
    }

    [RelayCommand]
    private void SaveCustomTemplates()
    {
        try
        {
            var imported = NotesTemplateStore.ParseText(CustomTemplateText);
            foreach (var choice in TemplateChoices.Where(choice => choice.Kind is null).ToList()) TemplateChoices.Remove(choice);
            foreach (var template in imported) TemplateChoices.Add(new NotesTemplateChoice(template, null));
            templateStore ??= new NotesTemplateStore(Paths.Root);
            templateStore.Save(imported);
            CustomTemplateStatus = imported.Count == 0 ? "自定义模板已清空。" : $"已保存 {imported.Count} 个自定义模板。";
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
        CustomTemplateStatus = $"已导入模板，共 {TemplateChoices.Count(choice => choice.Kind is null)} 个自定义模板。";
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
        SetStatus(NextThought.Core.UiText.EnglishUi ? $"Template \"{name}\" saved." : $"模板「{name}」已保存。");
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
            var english = AnswerLanguage.Spoken(transcript) == "English";
            var template = SelectedTemplate?.Template ?? NotesTemplate.BuiltIn(TemplateKind, english);
            var header = english
                ? $"# {meta.Title}\n\nMeeting: {meta.StartedAt.ToLocalTime():yyyy-MM-dd HH:mm} · Template: {template.Name}"
                : $"# {meta.Title}\n\n会议时间：{meta.StartedAt.ToLocalTime():yyyy-MM-dd HH:mm} · 模板：{template.Name}";
            var settings = Settings.Build();
            var system = NotesPrompt.OrganizeSystem(template, english, inPerson: !settings.CaptureSystem, meetingDate: meta.StartedAt);
            var chunks = NotesPrompt.Chunks(transcript);
            var reply = "";
            if (chunks.Count == 1)
            {
                reply = await CurrentClient().AnswerAsync(AnswerOptions(true), system,
                    NotesPrompt.OrganizeUser(header, transcript), null, cancellation.Token, 240);
            }
            else
            {
                var partials = new List<string>();
                for (var index = 0; index < chunks.Count; index++)
                {
                    if (generation != requestEpoch) return;
                    SetStatus($"正在按《{template.Name}》整理…（第 {index + 1}/{chunks.Count} 段）");
                    partials.Add(await CurrentClient().AnswerAsync(AnswerOptions(true), system,
                        NotesPrompt.PartialUser(header, chunks[index], index, chunks.Count), null,
                        cancellation.Token, 240));
                }
                SetStatus($"正在按《{template.Name}》合并分段结果…");
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
            var markdown = NotesMarkdown.Render(doc);
            SummaryText = markdown;
            await Sessions.SaveSummaryAsync(meta, markdown, AnswerOptions(true).Model, cancellation.Token);
            meta.SummaryState = "saved";
            // The template marks sections that carry into the series (NotesCarry): queue them for review.
            var (lessons, bank) = NotesKnowledge.Extract(doc, template);
            CollectFromNotes(meta, doc, template);
            if (lessons.Count + bank.Count > 0)
                await Sessions.SaveSummaryKnowledgeAsync(meta, lessons, bank, appliedToWorkspace: false, cancellation.Token);
            SetStatus($"已按《{template.Name}》整理，可导出 Markdown。");
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

    private bool CanTemplate() => IsIdle && !Answering && CurrentSession is not null && currentLines.Count > 0;
}

public sealed record NotesTemplateChoice(NotesTemplate Template, NotesTemplateKind? Kind)
{
    public string Title => Template.Name;
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
