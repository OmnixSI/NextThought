using System.Text.Json;
using Avalonia.Controls;
using Avalonia.VisualTree;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// E21: drives the knowledge review surface on an isolated archive — series change cards
    /// (decision/action with owner+due and replace-keeping-history), knowledge candidate cards
    /// (matching question/lesson), the editor's records tab and the material review sheet.
    /// </summary>
    private async Task RunKnowledgeReviewCheckAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var steps = new List<object>();
        var passed = false;
        string? error = null;
        void Step(string name, bool ok, string detail) => steps.Add(new { step = name, passed = ok, detail = detail });
        try
        {
            var store = new SessionStore(state.Paths);
            var first = new SessionMeta { Title = "E21 复核会议甲" };
            await store.CreateAsync(first);
            await store.AppendTranscriptAsync(first, new TranscriptLine
            {
                Date = first.StartedAt.AddSeconds(10), StartedAt = first.StartedAt.AddSeconds(10),
                Speaker = "remote", Text = "上线前必须确认缓存一致性。"
            });
            await store.SaveSummaryAsync(first, "## 摘要\n缓存一致性要在上线前确认。", "m");
            await store.EndAsync(first);
            var second = new SessionMeta { Title = "E21 复核会议乙" };
            await store.CreateAsync(second);
            await store.SaveSummaryAsync(second, "## 摘要\n补充回滚方案。", "m");
            await store.EndAsync(second);

            var existingLesson = new WorkspaceLesson { Text = "先说结论，再给依据。" };
            var existingQuestion = new QuestionBankItem { Question = "缓存怎么保证一致？", Answer = "先更新数据库再删缓存。" };
            var existingAction = new WorkspaceSeriesItem
            {
                Kind = WorkspaceSeriesItem.KindValue.Action, Text = "灰度发布", Owner = "我", Due = "下周一",
                SourceSessionId = first.Id.ToString("D"), EvidenceId = "e-action", ClaimRevision = "c1",
                SourceRevision = "s1", TranscriptSourceRevision = "t1", Revision = 1
            };
            var series = WorkspaceSeries.Create("E21 知识复核") with
            {
                SessionIds = [first.Id.ToString("D"), second.Id.ToString("D")],
                Lessons = [existingLesson],
                QuestionBank = [existingQuestion],
                SeriesItems = [existingAction],
                SeriesChangeCandidates =
                [
                    new WorkspaceSeriesChangeCandidate
                    {
                        Kind = WorkspaceSeriesItem.KindValue.Decision, Text = "上线前必须确认缓存一致性",
                        SourceSessionId = first.Id.ToString("D"), EvidenceId = "e-decision", ClaimRevision = "c2",
                        SourceRevision = "s2", SourceQuote = "上线前必须确认缓存一致性。", Location = "00:10",
                        TranscriptSourceRevision = "t2"
                    },
                    new WorkspaceSeriesChangeCandidate
                    {
                        Kind = WorkspaceSeriesItem.KindValue.Action, Text = "补充回滚方案",
                        SourceSessionId = second.Id.ToString("D"), EvidenceId = "e-action-new", ClaimRevision = "c3",
                        SourceRevision = "s3", SourceQuote = "补充回滚方案。", Location = "00:05",
                        TranscriptSourceRevision = "t3"
                    },
                    new WorkspaceSeriesChangeCandidate
                    {
                        Kind = WorkspaceSeriesItem.KindValue.Action, Text = "灰度发布改到 Kubernetes",
                        SourceSessionId = second.Id.ToString("D"), EvidenceId = "e-action-replace", ClaimRevision = "c4",
                        SourceRevision = "s4", SourceQuote = "灰度发布改到 Kubernetes。", Location = "00:20",
                        TranscriptSourceRevision = "t4"
                    }
                ],
                KnowledgeCandidates =
                [
                    new WorkspaceKnowledgeCandidate
                    {
                        Id = "kc-question", Kind = WorkspaceKnowledgeKind.Question, Text = "缓存怎么保证一致？",
                        ProposedAnswer = "先更新数据库，再删缓存；上线前压测。",
                        SourceSessionId = first.Id.ToString("D"),
                        BaseQuestionId = existingQuestion.Id, BaseAnswerRevision = 0
                    },
                    new WorkspaceKnowledgeCandidate
                    {
                        Id = "kc-lesson", Kind = WorkspaceKnowledgeKind.Lesson, Text = "先给结论，再给依据。",
                        SourceSessionId = second.Id.ToString("D")
                    },
                    new WorkspaceKnowledgeCandidate
                    {
                        Id = "kc-reject", Kind = WorkspaceKnowledgeKind.Lesson, Text = "复盘要写行动项。",
                        SourceSessionId = second.Id.ToString("D")
                    }
                ]
            };
            new WorkspaceStore(state.Paths.Root).Save(new WorkspaceData([series]));
            state.ReloadWorkspaceForCheck();
            await state.RefreshHistoryAsync();
            state.FocusSeriesForCheck(series.Id);
            state.Section = AppSection.Series;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(500);
            Step("系列页加载候选与记录", state.SeriesChangeRows.Count == 3 && state.SeriesCandidates.Count == 3
                && state.SeriesRecords.Count == 2,
                "变更 " + state.SeriesChangeRows.Count + "；知识 " + state.SeriesCandidates.Count
                + "；记录 " + state.SeriesRecords.Count);
            await Task.Delay(400);
            CaptureVisual(this, Path.Combine(directory, "series-change-light.png"));
            if (this.GetVisualDescendants().OfType<TabControl>().FirstOrDefault(tabs => tabs.Name == "Tabs") is { } seriesTabs)
            {
                seriesTabs.SelectedIndex = 1; // 待确认（知识候选）标签
                await Task.Delay(500);
                CaptureVisual(this, Path.Combine(directory, "series-knowledge-light.png"));
                seriesTabs.SelectedIndex = 0;
            }

            // 1) 行动建议：确认负责人/期限后作为新行动加入。
            var actionRow = state.SeriesChangeRows.First(row => row.IsAction);
            actionRow.Owner = "Shiyin";
            actionRow.Due = "下周一（悉尼时间）";
            state.ReviewSeriesChange(actionRow, WorkspaceSeriesChangeDecision.Add, null);
            var saved = new WorkspaceStore(state.Paths.Root).Load().Find(series.Id)!;
            var addedAction = saved.SeriesItems.FirstOrDefault(item => item.Text == "补充回滚方案");
            Step("行动建议确认负责人/期限后加入", addedAction is { Owner: "Shiyin" }
                && addedAction.Due == "下周一（悉尼时间）" && addedAction.ActionState == WorkspaceSeriesItem.ActionStateValue.Open,
                "Owner=" + addedAction?.Owner + "；Due=" + addedAction?.Due);

            // 2) 决定建议：作为新决定加入。
            var decisionRow = state.SeriesChangeRows.First(row => !row.IsAction);
            state.ReviewSeriesChange(decisionRow, WorkspaceSeriesChangeDecision.Add, null);
            saved = new WorkspaceStore(state.Paths.Root).Load().Find(series.Id)!;
            Step("决定建议作为新决定加入", saved.SeriesItems.Any(item =>
                    item.Kind == WorkspaceSeriesItem.KindValue.Decision && item.Text == "上线前必须确认缓存一致性"),
                "决定数 " + saved.SeriesItems.Count(item => item.Kind == WorkspaceSeriesItem.KindValue.Decision));

            // 3) 更新已有行动：保留旧版本（revision+1、history 1 条）。
            var target = new SeriesTargetRow(saved.SeriesItems.First(item => item.Kind == WorkspaceSeriesItem.KindValue.Action
                                                                          && item.Text == "灰度发布"));
            state.ReviewSeriesChange(state.SeriesChangeRows.First(row => row.IsAction && row.HasMatchingItems),
                WorkspaceSeriesChangeDecision.Replace, target);
            saved = new WorkspaceStore(state.Paths.Root).Load().Find(series.Id)!;
            var replaced = saved.SeriesItems.First(item => item.Id == target.Item.Id);
            Step("更新已有内容保留旧版本", replaced.Revision == 2 && replaced.History.Count == 1
                && replaced.Text == "灰度发布改到 Kubernetes",
                "revision=" + replaced.Revision + "；history=" + replaced.History.Count
                + "；targetRev=" + target.Item.Revision + "；status=" + state.Status);

            // 4) 同一个系列的复核队列消费完后不再有待确认（忽略路径由 Core 测试覆盖）。
            saved = new WorkspaceStore(state.Paths.Root).Load().Find(series.Id)!;
            Step("复核队列消费完", saved.SeriesChangeCandidates.All(candidate =>
                    candidate.Status != WorkspaceSeriesChangeCandidate.StatusValue.Pending)
                && state.SeriesChangeRows.Count == 0,
                "剩余待确认 " + saved.SeriesChangeCandidates.Count(candidate =>
                    candidate.Status == WorkspaceSeriesChangeCandidate.StatusValue.Pending)
                + "；页面剩余 " + state.SeriesChangeRows.Count);

            // 5) 知识候选：已有相似问题 → 采用建议并保留旧版；新经验 → 加入经验库。
            state.RefreshSeriesLessonsForCheck();
            var questionRow = state.SeriesCandidates.First(row => row.IsQuestion);
            Step("知识候选显示系列当前答法", questionRow.HasMatchingQuestion
                && questionRow.MatchingQuestionText == "缓存怎么保证一致？", questionRow.MatchingAnswerText);
            state.ReviewCandidate(questionRow, WorkspaceKnowledgeDecision.ReplaceAnswer);
            saved = new WorkspaceStore(state.Paths.Root).Load().Find(series.Id)!;
            var bank = saved.QuestionBank.First(item => item.Question == "缓存怎么保证一致？");
            Step("采用建议并保留旧版", bank.Answer.Contains("压测") && (bank.AnswerHistory?.Count ?? 0) >= 1,
                "answer=" + bank.Answer + "；历史 " + (bank.AnswerHistory?.Count ?? 0));
            state.RefreshSeriesLessonsForCheck();
            var lessonRow = state.SeriesCandidates.First(row => row.Text.Contains("行动项"));
            state.ReviewCandidate(lessonRow, WorkspaceKnowledgeDecision.Add);
            saved = new WorkspaceStore(state.Paths.Root).Load().Find(series.Id)!;
            Step("加入经验库", saved.Lessons.Any(lesson => lesson.Text == "复盘要写行动项。"),
                "经验数 " + saved.Lessons.Count);
            state.RefreshSeriesLessonsForCheck();
            var rejectKnowledge = state.SeriesCandidates.First(row => row.Text.Contains("先给结论"));
            state.ReviewCandidate(rejectKnowledge, WorkspaceKnowledgeDecision.Reject);
            saved = new WorkspaceStore(state.Paths.Root).Load().Find(series.Id)!;
            Step("忽略知识候选", saved.KnowledgeCandidates is { } knowledge
                && knowledge.All(candidate => candidate.Status != WorkspaceKnowledgeStatus.Pending),
                "剩余待确认 " + (saved.KnowledgeCandidates?.Count(candidate =>
                    candidate.Status == WorkspaceKnowledgeStatus.Pending) ?? 0));

            // 6) 记录标签：查看跳转到会议页。
            var record = state.SeriesRecords.First();
            await state.OpenSeriesRecordAsync(record);
            await Task.Delay(400);
            Step("记录标签打开会议页", state.Section == AppSection.Notes && state.CurrentSession?.Id == record.Meta.Id,
                "section=" + state.Section + "；会话=" + state.CurrentSession?.Title);

            // 7) 资料复核：警告、提取预览、备注写入 material-review.json。
            var materialId = Guid.NewGuid().ToString("N");
            var materialFolder = Path.Combine(state.Paths.Root, "Materials", materialId);
            Directory.CreateDirectory(materialFolder);
            var material = new MaterialDocument
            {
                Id = materialId, Name = "扫描件.pdf", Extension = ".pdf",
                Warnings = ["第 3 页的扫描件 OCR 可信度偏低"],
                Blocks = [new DocumentBlock("p.1", "第一页正文"), new DocumentBlock("p.3", "扫描不清晰的表格")]
            };
            await File.WriteAllTextAsync(Path.Combine(materialFolder, "document.json"),
                JsonSerializer.Serialize(material, JsonDefaults.Options));
            await File.WriteAllBytesAsync(Path.Combine(materialFolder, "source.pdf"), [0x25, 0x50, 0x44, 0x46]);
            state.Library.Refresh();
            state.Library.SelectedMaterial = state.Library.Materials.First(item => item.Id == materialId);
            state.LoadMaterialReview();
            await Task.Delay(200);
            Step("资料复核：警告与提取预览", state.HasMaterialReviewWarnings
                && state.MaterialReviewPreview.Contains("扫描不清晰的表格"), "警告 " + state.MaterialReviewWarnings.Count + " 条");
            state.MaterialReviewNote = "第 3 页需要人工核对";
            state.SaveMaterialReviewCommand.Execute(null);
            var persisted = MaterialReviewNotes.NoteFor(state.Paths.Root, materialId);
            Step("复核备注写入 material-review.json", persisted == "第 3 页需要人工核对", persisted);
            var reviewWindow = new MaterialReviewWindow { DataContext = state };
            reviewWindow.Show();
            await Task.Delay(600);
            CaptureVisual(reviewWindow, Path.Combine(directory, "material-review-light.png"));
            Application.Current!.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
            await Task.Delay(350);
            CaptureVisual(reviewWindow, Path.Combine(directory, "material-review-dark.png"));
            Application.Current.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
            await Task.Delay(250);
            reviewWindow.Close();
            await Task.Delay(200);
            passed = steps.All(step => (bool)step.GetType().GetProperty("passed")!.GetValue(step)!);
        }
        catch (Exception ex)
        {
            error = ex.ToString();
        }
        var report = new { passed, error, steps };
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "knowledge-review-check.json"),
                JsonSerializer.Serialize(report, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }));
        }
        catch { }
        Console.WriteLine(passed ? "knowledge-review-check: passed" : "knowledge-review-check: failed " + (error ?? ""));
        var idle = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < idle && state.Working) await Task.Delay(100);
        Close();
    }
}
