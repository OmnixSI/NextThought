using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>A row in the recent-meetings list: a saved meeting plus its compact summary line.</summary>
public sealed record SeriesRow(string Id, string Name, int Count, int LessonCount, string LastTitle, string LastLesson, int PendingCount = 0, int DocumentCount = 0)
{
    public string Meta
    {
        get
        {
            var parts = new List<string> { UiText.Meetings(Count) };
            if (DocumentCount > 0) parts.Add(UiText.Materials(DocumentCount));
            if (PendingCount > 0) parts.Add(UiText.Pending(PendingCount));
            else if (LessonCount > 0) parts.Add(UiText.Lessons(LessonCount));
            return string.Join(" · ", parts);
        }
    }
    public string Preview => LastLesson.Length > 0 ? LastLesson : LastTitle;
}


/// <summary>One history section when meetings are grouped by series (E18: selectable for batch delete).</summary>
public sealed class HistoryGroupRow : System.ComponentModel.INotifyPropertyChanged
{
    public HistoryGroupRow(string name, string? seriesId, IReadOnlyList<RecentMeetingRow> rows)
    {
        Name = name;
        SeriesId = seriesId;
        Rows = rows;
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public string Name { get; }
    public string? SeriesId { get; }
    public IReadOnlyList<RecentMeetingRow> Rows { get; }
    public bool CanManage => SeriesId is not null;

    private bool isSelected;
    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected == value) return;
            isSelected = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
}

public sealed record RecentMeetingRow(SessionMeta Meta) : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private bool isSelected;
    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected == value) return;
            isSelected = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    private SessionSearchHit? searchHit;

    /// <summary>E18: the first content-search hit of this meeting (the row shows where it came from).</summary>
    public SessionSearchHit? SearchHit
    {
        get => searchHit;
        private set
        {
            searchHit = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(SearchHit)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(HasSearchHit)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(SearchHitLine)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(SearchHitExcerpt)));
        }
    }

    public void ApplySearchHit(SessionSearchHit? hit) => SearchHit = hit;

    public bool HasSearchHit => SearchHit is not null;

    /// <summary>HistoryView.searchHitKindLabel + location: e.g. 「回答 · 00:20」.</summary>
    public string SearchHitLine
    {
        get
        {
            if (SearchHit is not { } hit) return "";
            var kind = HistoryHitRow.KindLabel(hit.Kind);
            return hit.Location is { Length: > 0 } location ? kind + " · " + location : kind;
        }
    }

    public string SearchHitExcerpt => SearchHit?.Excerpt ?? "";

    public string Title => string.IsNullOrWhiteSpace(Meta.Title) ? UiText.T("未命名会议") : Meta.Title;

    public string Subtitle
    {
        get
        {
            var parts = new List<string> { Meta.LocalStartedAt };
            if (Meta.TranscriptCount > 0) parts.Add(UiText.Transcripts(Meta.TranscriptCount));
            if (Meta.AnswerCount > 0) parts.Add(UiText.Answers(Meta.AnswerCount));
            if (Meta.SummaryState == "ready") parts.Add(UiText.T("有纪要"));
            return string.Join(" · ", parts);
        }
    }

    public bool HasSummary => Meta.SummaryState == "ready";
}

/// <summary>E18: one content-search hit shown by the history hit navigator (HistoryView.swift).</summary>
public sealed record HistoryHitRow(SessionSearchHit Hit)
{
    public string KindLabelText => KindLabel(Hit.Kind);
    public string LocationText => Hit.Location ?? "";
    public string Line => LocationText.Length > 0 ? KindLabelText + " · " + LocationText : KindLabelText;
    public string Excerpt => Hit.Excerpt;

    /// <summary>HistoryView.searchHitKindLabel(_:).</summary>
    public static string KindLabel(SessionSearchFieldKind kind) => kind switch
    {
        SessionSearchFieldKind.Transcript => UiText.T("原话"),
        SessionSearchFieldKind.CorrectedTranscript => UiText.T("校正原话"),
        SessionSearchFieldKind.Translation => UiText.T("译文"),
        SessionSearchFieldKind.Question => UiText.T("问题"),
        SessionSearchFieldKind.Answer => UiText.T("回答"),
        _ => UiText.T("纪要")
    };
}

/// <summary>E18: one archived answer in the history overview (HistoryView SessionAnswersList).</summary>
public sealed record HistoryAnswerRow(AnswerRecord Answer, Guid SessionId)
{
    public string Question => Answer.Question;
    public string AnswerText => Answer.Answer;

    /// <summary>SessionAnswersList: a fragment without answer text is marked incomplete.</summary>
    public bool IsComplete => Answer.Answer.Trim().Length > 0;

    public string AutomaticLabel => UiText.T(Answer.Automatic ? "自动" : "手动");

    public string MetaLine
    {
        get
        {
            var parts = new List<string> { Answer.Date.ToLocalTime().ToString("HH:mm:ss"), Answer.Model };
            if (Answer.FirstTokenSeconds is { } first) parts.Add(UiText.Format("首字 %.1fs", first));
            return string.Join(" · ", parts);
        }
    }

    public bool HasScreenshot => Answer.ScreenshotPath is { Length: > 0 };
    public string ScreenshotPath => Answer.ScreenshotPath ?? "";
}

/// <summary>
/// One queued knowledge suggestion (WorkspaceKnowledgeCandidate) as the series page shows it.
/// Nothing here is active until the user accepts or rejects it.
/// </summary>
public sealed record KnowledgeCandidateRow(WorkspaceKnowledgeCandidate Candidate)
{
    public string Text => Candidate.Text;

    public string? Answer => string.IsNullOrWhiteSpace(Candidate.ProposedAnswer) ? null : Candidate.ProposedAnswer;

    public bool HasAnswer => Answer is not null;

    public bool IsQuestion => Candidate.Kind == WorkspaceKnowledgeKind.Question;

    /// <summary>Only a question has a stored answer that a review can replace.</summary>
    public bool CanReplace => IsQuestion;

    public string KindLabel => UiText.T(IsQuestion ? "问题" : "经验");

    public string Hint => UiText.T(IsQuestion
        ? "题库里已有相似问题时，「替换」会用新回答覆盖旧的，旧回答留在历史里"
        : "采纳后进入这个系列的经验，下次回答会带上它");
}

/// <summary>One reviewed question and its best answer.</summary>
public sealed record QuestionBankRow(QuestionBankItem Item)
{
    public string Question => Item.Question;

    public string Answer => Item.Answer;

    public bool HasAnswer => !string.IsNullOrWhiteSpace(Item.Answer);

    public string Meta => UiText.Asked(Item.TimesAsked);
}

/// <summary>
/// E20 (WorkspaceBasicTab field editor): one mode field of the series, edited in place. The id is
/// the Mac ModeField id used as the Fields dictionary key.
/// </summary>
public sealed class SeriesEditorFieldRow : System.ComponentModel.INotifyPropertyChanged
{
    public SeriesEditorFieldRow(ModeField field, string value, bool custom = false)
    {
        Id = field.Id;
        Title = field.Title;
        Placeholder = field.Placeholder;
        Long = field.Long;
        Custom = custom;
        this.value = value;
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; }
    public string Title { get; }
    public string Placeholder { get; }
    public bool Long { get; }
    /// <summary>A field the mode profile does not know (kept from an older series or a Mac custom type).</summary>
    public bool Custom { get; }
    public double EditorHeight => Long ? 92 : 36;

    private string value;
    public string Value
    {
        get => value;
        set
        {
            if (this.value == value) return;
            this.value = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Value)));
        }
    }
}

/// <summary>E20 (PreparationQueueSummary): one document-preparation job of the current series.</summary>
public sealed record SeriesPreparationRow(DocumentJob Job)
{
    public string FileName => Job.FileName;
    public string ProgressText => Job.ProgressText;
    public string Message => Job.Message;
    public bool HasMessage => Job.Message.Length > 0;
    public bool HasTotal => Job.Total is > 0 && !Job.IsTerminal;
    public double Completed => Job.Completed;
    public double Total => Job.Total ?? 0;
    public bool NeedsAttention => Job.Stage is DocumentJob.StageValue.Failed or DocumentJob.StageValue.Warning;
    public bool CanRetry => Job.Stage is DocumentJob.StageValue.Failed or DocumentJob.StageValue.Warning
        or DocumentJob.StageValue.Cancelled;
    public bool CanCancel => !Job.IsTerminal;
    public string Id => Job.Id;
}
