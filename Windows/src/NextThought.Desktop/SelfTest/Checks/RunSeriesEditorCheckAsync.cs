using NextThought.Core;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// E20: drives the real 系列 editor on an isolated data directory — field editor, material
    /// import/remove/undo, preparation queue summary with retry, editable lessons and question bank,
    /// archive and delete-with-records. Screenshots + JSON land in the output directory.
    /// </summary>
    private async Task RunSeriesEditorCheckAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var screenshotDirectory = Path.Combine(directory, "screenshots");
        Directory.CreateDirectory(screenshotDirectory);
        var steps = new List<object>();
        var passed = false;
        string? error = null;
        void Step(string name, bool ok, string detail) => steps.Add(new { step = name, passed = ok, detail = detail });
        try
        {
            var store = new SessionStore(state.Paths);
            var first = await CreateArchivedMeetingAsync(store, "E20 自检会议甲", "我们讨论了缓存一致性的方案",
                "缓存怎么保证一致？", "先更新数据库再删缓存");
            var second = await CreateArchivedMeetingAsync(store, "E20 自检会议乙", "上线时间定在周五", "什么时候上线？",
                "周五上线");
            var material = new MaterialDocument
            {
                Name = "岗位 JD.md",
                Extension = ".md",
                Blocks = [new DocumentBlock("p1", "负责高并发交易系统的缓存与一致性问题")]
            };
            var lesson = new WorkspaceLesson { Text = "先说结论，再说依据。", SourceSessionId = first.Id.ToString("D") };
            var question = new QuestionBankItem
            {
                Question = "缓存怎么保证一致？",
                Answer = "先更新数据库再删缓存。",
                SourceSessionId = first.Id.ToString("D")
            };
            var series = WorkspaceSeries.Create("E20 自检系列") with
            {
                SessionIds = [first.Id.ToString("D"), second.Id.ToString("D")],
                Lessons = [lesson],
                QuestionBank = [question],
                Fields = new Dictionary<string, string> { ["domain"] = "交易系统" },
                Preferences = "先说结论，再给两三点依据。",
                StageReview = "第一轮复盘：结论清楚，数据偏少。",
                StageReviewAt = DateTimeOffset.UtcNow
            };
            series = SeriesMaterials.Attach(series, material);
            new WorkspaceStore(state.Paths.Root).Save(new WorkspaceData([series]));
            // PreparationQueueSummary keeps failed and partial imports visible.
            var queue = new DocumentJobQueue(Path.Combine(state.Paths.Root, "DocumentJobs"));
            var failedJob = DocumentJob.Create(series.Id, "论文.pdf", "论文") with
            {
                Stage = DocumentJob.StageValue.Failed,
                Message = "OCR 组件缺失：请先安装组件，然后重试。"
            };
            queue.Enqueue(failedJob);
            state.ReloadWorkspaceForCheck();
            await state.RefreshHistoryAsync();
            state.FocusSeriesForCheck(series.Id);
            state.Section = AppSection.Series;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(400);
            state.LoadSeriesEditor();
            await Task.Delay(200);
            Step("系列编辑器加载", state.SeriesEditorName == "E20 自检系列" && state.SeriesEditorFieldRows.Count > 0,
                state.SeriesEditorName + "；字段 " + state.SeriesEditorFieldRows.Count + " 项；资料 " + state.SeriesEditorDocuments.Count + " 份");
            CaptureVisual(this, Path.Combine(screenshotDirectory, "series-light.png"));

            // 1) 字段编辑器：改一个字段再保存，写回 workspace.json。
            var domainRow = state.SeriesEditorFieldRows.FirstOrDefault(row => row.Id == "domain");
            if (domainRow is not null) domainRow.Value = "高并发交易系统";
            await Dispatcher.UIThread.InvokeAsync(() => state.SaveSeriesEditorCommand.Execute(null));
            var savedFields = new WorkspaceStore(state.Paths.Root).Load().Find(series.Id)?.Fields
                ?? new Dictionary<string, string>();
            savedFields.TryGetValue("domain", out var savedDomain);
            Step("字段编辑器保存", savedDomain == "高并发交易系统", "domain=" + savedDomain);

            // 2) 准备队列摘要：失败任务可见，可重试。
            state.RefreshSeriesPreparation();
            var summary = state.SeriesEditorPreparationSummary;
            var jobRow = state.SeriesEditorPreparationRows.FirstOrDefault();
            if (jobRow is not null) state.RetrySeriesPreparationCommand.Execute(jobRow);
            var retried = new DocumentJobQueue(Path.Combine(state.Paths.Root, "DocumentJobs")).All
                .FirstOrDefault(job => job.Id == failedJob.Id)?.Stage;
            Step("准备队列摘要与重试", summary.Contains("需处理") && retried == DocumentJob.StageValue.Queued,
                summary + "；重试后=" + retried);

            // 3) 资料导入（真实文件 → MaterialStore → 系列）、移除与撤销。
            var markdown = Path.Combine(Path.GetTempPath(), "nt-e20-" + Guid.NewGuid().ToString("N") + ".md");
            await File.WriteAllTextAsync(markdown, "# 项目资料\n\n缓存一致性要在上线前确认。\n");
            await state.ImportSeriesDocumentsAsync([markdown]);
            var afterImport = new WorkspaceStore(state.Paths.Root).Load().Find(series.Id);
            var imported = afterImport?.Documents.FirstOrDefault(document => document.Name.EndsWith(".md"));
            var importedRow = state.SeriesEditorDocuments.FirstOrDefault(document => document.Name.EndsWith(".md"));
            Step("资料导入到系列", imported is not null && afterImport!.DocumentIds.Contains(imported.Id),
                state.SeriesEditorImportStatus + "；文档 " + (afterImport?.Documents.Count ?? 0) + " 份");
            if (importedRow is not null) state.RemoveSeriesDocumentCommand.Execute(importedRow);
            var afterRemove = new WorkspaceStore(state.Paths.Root).Load().Find(series.Id);
            var removed = imported is not null && afterRemove is not null
                && afterRemove.Documents.All(document => document.Id != imported.Id)
                && afterRemove.DocumentIds.All(id => id != imported.Id);
            state.UndoRemoveSeriesDocumentCommand.Execute(null);
            var afterUndo = new WorkspaceStore(state.Paths.Root).Load().Find(series.Id);
            var restored = imported is not null && afterUndo is not null
                && afterUndo.Documents.Any(document => document.Id == imported.Id);
            Step("资料移除与撤销", removed && restored, "移除干净=" + removed + "；撤销恢复=" + restored);
            File.Delete(markdown);

            // 4) 经验与题库可编辑（保留历史）。
            state.UpdateSeriesLesson(lesson, "先说结论，再给三条依据。");
            var editedLesson = new WorkspaceStore(state.Paths.Root).Load().Find(series.Id)?.Lessons
                .FirstOrDefault(item => item.Id == lesson.Id);
            Step("经验可编辑并保留历史", editedLesson?.Text == "先说结论，再给三条依据。" && editedLesson.History.Count >= 1,
                (editedLesson?.Text ?? "") + "；历史 " + (editedLesson?.History.Count ?? 0) + " 条");
            state.UpdateSeriesQuestionBankItem(question, "缓存怎么保证一致性？", "先更新数据库，再删缓存。");
            var editedQuestion = new WorkspaceStore(state.Paths.Root).Load().Find(series.Id)?.QuestionBank
                .FirstOrDefault(item => item.Id == question.Id);
            Step("题库可编辑", editedQuestion?.Answer.Contains("先更新数据库") == true,
                editedQuestion?.Question ?? "");

            // 5) 截图：编辑器窗口（资料+复盘标签由截图和报告的步骤覆盖）。
            var editorWindow = new SeriesEditorWindow { DataContext = state };
            editorWindow.Show();
            await Task.Delay(700);
            CaptureVisual(editorWindow, Path.Combine(screenshotDirectory, "series-editor-light.png"));
            editorWindow.Close();
            await Task.Delay(200);

            // 6) 归档。
            state.ArchiveSeriesCommand.Execute(null);
            var archived = new WorkspaceStore(state.Paths.Root).Load().Find(series.Id)?.Archived == true;
            Step("系列归档", archived, "Archived=" + archived);

            // 7) 删除系列连同会议记录（Mac WorkspaceActions.deleteWorkspace）。
            var recordsBefore = new SessionStore(state.Paths).List().Count;
            await state.DeleteSeriesFromEditorAsync();
            var workspaceAfter = new WorkspaceStore(state.Paths.Root).Load();
            var recordsAfter = new SessionStore(state.Paths).List().Count;
            Step("删除系列连同会议记录", workspaceAfter.Find(series.Id) is null && recordsAfter < recordsBefore,
                "系列剩余 " + workspaceAfter.Series.Count + "；会议 " + recordsBefore + "→" + recordsAfter);
            passed = steps.All(step => (bool)step.GetType().GetProperty("passed")!.GetValue(step)!);
        }
        catch (Exception ex)
        {
            error = ex.ToString();
        }
        var report = new { passed, error, steps };
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "series-editor-check.json"),
                System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }));
        }
        catch { }
        Console.WriteLine(passed ? "series-editor-check: passed" : "series-editor-check: failed " + (error ?? ""));
        var idle = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < idle && state.Working) await Task.Delay(100);
        Close();
    }
}