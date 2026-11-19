using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>One step of the offline knowledge self-check.</summary>
public sealed record KnowledgeCheckStep(string Name, bool Passed, string? Detail = null);

/// <summary>
/// Result of the offline knowledge check: notes → review candidates → user decision →
/// workspace.json → prompt text. No answer service and no microphone involved.
/// </summary>
public sealed record KnowledgeCheckReport
{
    public DateTimeOffset TestedAt { get; init; } = DateTimeOffset.Now;

    public bool Passed { get; init; }

    public string Scope { get; init; } = "offline knowledge loop; template notes to accepted knowledge and prompt text";

    public IReadOnlyList<KnowledgeCheckStep> Steps { get; init; } = [];

    public int CandidatesQueued { get; init; }

    public int Lessons { get; init; }

    public int Bank { get; init; }

    public string? PromptExcerpt { get; init; }
}

/// <summary>
/// Proves the Mac "越用越懂你" loop on a real machine without an API key: a template summary
/// carries lessons and questions, the series page queues them, the user's decision is saved,
/// a fresh load still has it, and the answer prompt now contains the accepted lesson.
/// </summary>
public static class KnowledgeCheck
{
    public static KnowledgeCheckReport Run(AppState state)
    {
        var steps = new List<KnowledgeCheckStep>();
        var queued = 0;
        var lessons = 0;
        var bank = 0;
        string? prompt = null;
        string? seriesId = null;
        try
        {
            var template = NotesTemplate.BuiltIn(NotesTemplateKind.Interview);
            var reply = """
                {"title": "知识自检", "sections": [
                  {"id": "questions", "items": [
                    {"text": "为什么用 Redis", "answer": "快", "verdict": "improve", "better": "因为读多写少，且能接受短暂不一致"}
                  ]},
                  {"id": "improve", "items": ["把实验和论证分开讲"]},
                  {"id": "focus", "items": ["下一轮可能追问缓存一致性"]}
                ]}
                """;
            var doc = NotesJson.Parse(reply, template);
            if (doc is null)
            {
                steps.Add(new KnowledgeCheckStep("parse-notes", false, "模板回复无法解析"));
                return Report(steps, 0, 0, 0, null);
            }

            var (extractedLessons, extractedBank) = NotesKnowledge.Extract(doc, template);
            steps.Add(new KnowledgeCheckStep("notes-carry", extractedLessons.Count >= 2 && extractedBank.Count >= 1,
                $"{extractedLessons.Count} 条经验、{extractedBank.Count} 个问题"));

            var sessionId = Guid.NewGuid().ToString("D");
            var name = "知识自检 " + DateTime.Now.ToString("HHmmss");
            seriesId = state.CreateSeriesForCheck(name, sessionId);
            steps.Add(new KnowledgeCheckStep("create-series", seriesId.Length > 0, name));
            if (seriesId.Length == 0) return Report(steps, 0, 0, 0, null);

            var series = state.FindSeriesForCheck(seriesId)!;
            var (collected, added) = KnowledgeMerger.CollectCandidates(series, extractedLessons, extractedBank, sessionId);
            queued = added.Count;
            state.ReplaceSeriesForCheck(collected);
            state.FocusSeriesForCheck(seriesId);
            steps.Add(new KnowledgeCheckStep("queue-review", state.SeriesCandidates.Count == queued && queued >= 2,
                $"{queued} 条待确认"));

            var lessonRow = state.SeriesCandidates.FirstOrDefault(row => !row.IsQuestion && row.Text.Contains("把实验和论证分开讲", StringComparison.Ordinal))
                            ?? state.SeriesCandidates.FirstOrDefault(row => !row.IsQuestion);
            steps.Add(new KnowledgeCheckStep("lesson-candidate", lessonRow is not null));
            if (lessonRow is not null) state.ReviewCandidate(lessonRow, WorkspaceKnowledgeDecision.Add);
            state.FocusSeriesForCheck(seriesId);
            lessons = state.SeriesLessons.Count;
            steps.Add(new KnowledgeCheckStep("accept-lesson", lessons == 1, $"{lessons} 条经验"));

            prompt = state.ExperiencePromptForSession(sessionId);
            steps.Add(new KnowledgeCheckStep("prompt-carries-lesson", prompt.Contains("把实验和论证分开讲", StringComparison.Ordinal),
                prompt.Replace("\n", " / ")));

            var questionRow = state.SeriesCandidates.FirstOrDefault(row => row.IsQuestion);
            steps.Add(new KnowledgeCheckStep("question-candidate", questionRow is not null));
            if (questionRow is not null) state.ReviewCandidate(questionRow, WorkspaceKnowledgeDecision.Add);
            state.FocusSeriesForCheck(seriesId);
            bank = state.SeriesBank.Count;
            steps.Add(new KnowledgeCheckStep("accept-question", bank == 1, $"{bank} 个问题"));

            // Round-trip: what a new launch would read back from disk.
            state.ReloadWorkspaceForCheck();
            state.FocusSeriesForCheck(seriesId);
            var reloaded = state.FindSeriesForCheck(seriesId);
            lessons = reloaded?.Lessons.Count ?? 0;
            bank = reloaded?.QuestionBank?.Count ?? 0;
            steps.Add(new KnowledgeCheckStep("reload-keeps-knowledge", lessons >= 1 && bank >= 1, $"{lessons} 条经验、{bank} 个问题"));
        }
        catch (Exception ex)
        {
            steps.Add(new KnowledgeCheckStep("exception", false, ex.Message));
        }
        finally
        {
            if (seriesId is { Length: > 0 })
            {
                try
                {
                    state.RemoveSeriesForCheck(seriesId);
                    state.ReloadWorkspaceForCheck();
                    steps.Add(new KnowledgeCheckStep("cleanup", state.FindSeriesForCheck(seriesId) is null));
                }
                catch (Exception ex)
                {
                    steps.Add(new KnowledgeCheckStep("cleanup", false, ex.Message));
                }
            }
        }
        return Report(steps, queued, lessons, bank, prompt);
    }

    private static KnowledgeCheckReport Report(IReadOnlyList<KnowledgeCheckStep> steps, int queued, int lessons, int bank, string? prompt) => new()
    {
        Passed = steps.Count > 0 && steps.All(step => step.Passed),
        Steps = steps,
        CandidatesQueued = queued,
        Lessons = lessons,
        Bank = bank,
        PromptExcerpt = prompt
    };
}
