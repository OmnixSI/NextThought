using CommunityToolkit.Mvvm.ComponentModel;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// E21: one pending decision/action proposal (KnowledgeReviewSection.swift SeriesChangeCandidateCard).
/// The owner and due fields are edited before accepting; the matching items let the user replace an
/// existing decision/action, keeping its old revision.
/// </summary>
public sealed partial class SeriesChangeRow : ObservableObject
{
    public SeriesChangeRow(WorkspaceSeriesChangeCandidate candidate, IReadOnlyList<WorkspaceSeriesItem> items,
        bool stale = false)
    {
        staleOverride = stale;
        Candidate = candidate;
        MatchingItems = items
            .Where(item => item.Kind == candidate.Kind)
            .OrderByDescending(item => item.EffectiveUpdatedAt)
            .Select(item => new SeriesTargetRow(item))
            .ToList();
        owner = candidate.Owner ?? "";
        due = candidate.Due ?? "";
    }

    public WorkspaceSeriesChangeCandidate Candidate { get; }
    public string Id => Candidate.Id;
    public string Text => Candidate.Text;
    public bool IsAction => Candidate.Kind == WorkspaceSeriesItem.KindValue.Action;
    public string KindLabel => UiText.T(IsAction ? "行动建议" : "决定建议");
    public string ActionHint => UiText.T("确认负责人和期限；期限会保留原话，不会自动改写相对日期。");
    public string OwnerPlaceholder => UiText.T("负责人（未确认可留空）");
    public string DuePlaceholder => UiText.T("期限原话（例如：下周一，悉尼时间）");
    public string SourceQuote => Candidate.SourceQuote ?? "";
    public bool HasSourceQuote => SourceQuote.Length > 0;
    public string SourceLine => HasSourceQuote
        ? UiText.Format("依据：“%@” · %@", SourceQuote, Candidate.Location ?? "")
        : "";
    private readonly bool staleOverride;

    /// <summary>Mac checks the reviewed evidence revisions against the current transcript.</summary>
    public bool IsStale => staleOverride || Candidate.SourceMissing
        || string.IsNullOrEmpty(Candidate.TranscriptSourceRevision);
    public bool IsCurrent => !IsStale;

    public IReadOnlyList<SeriesTargetRow> MatchingItems { get; }
    public bool HasMatchingItems => MatchingItems.Count > 0;
    public string UpdateHeading => UiText.T("更新已有内容（保留旧版本）");

    [ObservableProperty] private string owner;
    [ObservableProperty] private string due;

    public string AddLabel => IsAction
        ? UiText.T("作为新行动加入系列")
        : UiText.T("作为新决定加入系列");
}

/// <summary>E21: one accepted item of the series that a replacement proposal can update.</summary>
public sealed record SeriesTargetRow(WorkspaceSeriesItem Item)
{
    public string Text => Item.Text;
    public string UpdateLabel => UiText.T("更新");
}
