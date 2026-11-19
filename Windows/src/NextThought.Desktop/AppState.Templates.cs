using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>按模板整理纪要（NotesTemplates）：章节由模板决定，模型只填内容。</summary>
public partial class AppState
{
    [ObservableProperty] private NotesTemplateKind templateKind = NotesTemplateKind.General;
    [ObservableProperty] private bool templating;

    public IReadOnlyList<NotesTemplateChoice> TemplateChoices { get; } = BuildChoices();

    private static List<NotesTemplateChoice> BuildChoices() =>
        NotesTemplateKinds.Selectable.Select(kind => new NotesTemplateChoice(kind, NotesTemplate.BuiltIn(kind).Name)).ToList();

    public NotesTemplateChoice? SelectedTemplate
    {
        get => TemplateChoices.FirstOrDefault(choice => choice.Kind == TemplateKind);
        set { if (value is not null) TemplateKind = value.Kind; }
    }

    partial void OnTemplateKindChanged(NotesTemplateKind value)
    {
        OnPropertyChanged(nameof(SelectedTemplate));
        OnPropertyChanged(nameof(TemplateIsDefault));
    }

    /// <summary>True when the selected template is the one this scene recommends.</summary>
    public bool TemplateIsDefault => TemplateKind == MeetingTypeDefaults.TemplateFor(SelectedMode);

    /// <summary>The scene decides the template until the user picks another one.</summary>
    private void ApplyDefaultTemplate()
    {
        TemplateKind = MeetingTypeDefaults.TemplateFor(SelectedMode);
    }

    [RelayCommand]
    private void ResetTemplate() => TemplateKind = MeetingTypeDefaults.TemplateFor(SelectedMode);

    [RelayCommand(CanExecute = nameof(CanTemplate))]
    private async Task SummarizeWithTemplateAsync()
    {
        if (CurrentSession is not { } meta || currentLines.Count == 0) return;
        CancelRequest();
        var generation = ++requestEpoch;
        var cancellation = new CancellationTokenSource();
        requestCancellation = cancellation;
        var template = NotesTemplate.BuiltIn(TemplateKind);
        Answering = Templating = true;
        ProgressIndeterminate = true;
        Section = AppSection.Notes;
        RaiseBusy();
        try
        {
            await Task.WhenAll(writes.ToArray());
            var transcript = BuildNumberedTranscript(meta);
            // A very long meeting is cut here; the message says so instead of failing silently.
            const int maxCharacters = 12000;
            var truncated = transcript.Length > maxCharacters;
            if (truncated) transcript = transcript[..maxCharacters];
            var header = $"# {meta.Title}\n\n会议时间：{meta.StartedAt.ToLocalTime():yyyy-MM-dd HH:mm} · 模板：{template.Name}";
            var settings = Settings.Build();
            var system = NotesPrompt.OrganizeSystem(template, english: false, inPerson: !settings.CaptureSystem);
            var user = NotesPrompt.OrganizeUser(header, transcript);
            SetStatus($"正在按「{template.Name}」整理…");
            var reply = await CurrentClient().AnswerAsync(AnswerOptions(true), system, user, null, cancellation.Token, 240);
            if (generation != requestEpoch) return;
            var doc = NotesJson.Parse(reply, template);
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
            SetStatus(truncated
                ? $"已按「{template.Name}」整理（会议较长，只整理了前 {maxCharacters} 字）。"
                : $"已按「{template.Name}」整理，可导出 Markdown。");
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

public sealed record NotesTemplateChoice(NotesTemplateKind Kind, string Title)
{
    public override string ToString() => Title;
}
