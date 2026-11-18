using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>A row in the recent-meetings list: a saved meeting plus its compact summary line.</summary>
public sealed record SeriesRow(string Id, string Name, int Count, int LessonCount, string LastTitle, string LastLesson, int PendingCount = 0)
{
    public string Meta => PendingCount > 0
        ? $"{Count} 场会议 · {LessonCount} 条经验 · {PendingCount} 条待确认"
        : $"{Count} 场会议 · {LessonCount} 条经验";
    public string Preview => LastLesson.Length > 0 ? LastLesson : LastTitle;
}

public sealed record RecentMeetingRow(SessionMeta Meta)
{
    public string Title => string.IsNullOrWhiteSpace(Meta.Title) ? "未命名会议" : Meta.Title;

    public string Subtitle
    {
        get
        {
            var parts = new List<string> { Meta.LocalStartedAt };
            if (Meta.TranscriptCount > 0) parts.Add($"{Meta.TranscriptCount} 条转写");
            if (Meta.AnswerCount > 0) parts.Add($"{Meta.AnswerCount} 条回答");
            if (Meta.SummaryState is "saved" or "complete") parts.Add("有纪要");
            return string.Join(" · ", parts);
        }
    }

    public bool HasSummary => Meta.SummaryState is "saved" or "complete";
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

    public string KindLabel => IsQuestion ? "问题" : "经验";

    public string Hint => IsQuestion
        ? "题库里已有相似问题时，「替换」会用新回答覆盖旧的，旧回答留在历史里"
        : "采纳后进入这个系列的经验，下次回答会带上它";
}

/// <summary>One reviewed question and its best answer.</summary>
public sealed record QuestionBankRow(QuestionBankItem Item)
{
    public string Question => Item.Question;

    public string Answer => Item.Answer;

    public bool HasAnswer => !string.IsNullOrWhiteSpace(Item.Answer);

    public string Meta => Item.TimesAsked > 1 ? $"被问到 {Item.TimesAsked} 次" : "被问到 1 次";
}
