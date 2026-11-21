using NextThought.Core;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// E15: drives the meeting page on an isolated archive — four tabs, evidence pane, real outline
    /// view, jump-to-transcript inside the page, personal notes/highlights, meeting info, and the
    /// regeneration guard for edited notes. Screenshots land in the output directory.
    /// </summary>
    private async Task RunNotesUiCheckAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var steps = new List<object>();
        var passed = false;
        string? error = null;
        void Step(string name, bool ok, string detail) => steps.Add(new { step = name, passed = ok, detail = detail });
        try
        {
            var noEditsAtStart = !state.HasEditedNotesForCurrentSession;
            var store = new SessionStore(state.Paths);
            var meta = new SessionMeta { Title = "E15 自检会议" };
            await store.CreateAsync(meta);
            await store.AppendTranscriptAsync(meta, new TranscriptLine
            {
                Date = meta.StartedAt.AddSeconds(10), StartedAt = meta.StartedAt.AddSeconds(10),
                EndedAt = meta.StartedAt.AddSeconds(15), Speaker = "remote", Text = "先确认缓存一致性的方案。"
            });
            await store.AppendTranscriptAsync(meta, new TranscriptLine
            {
                Date = meta.StartedAt.AddSeconds(40), StartedAt = meta.StartedAt.AddSeconds(40),
                EndedAt = meta.StartedAt.AddSeconds(46), Speaker = "me", Text = "我们决定先更新数据库再删缓存。"
            });
            await store.AppendAnswerAsync(meta, new AnswerRecord
            {
                Date = meta.StartedAt.AddSeconds(50), Question = "缓存怎么保证一致？",
                Answer = "先更新数据库再删缓存。", Model = "m", Automatic = true
            });
            await store.SaveSummaryAsync(meta, "## 摘要\n缓存一致性要在上线前确认。", "m");
            // Real structured outline inside summary.json, the way the notes pipeline writes it.
            var summaryPath = Path.Combine(store.Folder(meta.Id), "summary.json");
            var summaryNode = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(summaryPath))!.AsObject();
            var outline = new OutlineNotes(
                [new OutlineSection("缓存一致性", 10, [new OutlinePoint("先更新数据库再删缓存", 40,
                    [new TimedText("上线前确认", 45)])])],
                [new OutlineConcept("缓存一致性", "数据库与缓存的数据先后顺序", 10)],
                [new TimedText("确认回滚方案", 46)]);
            var payload = new StructuredNotesPayload(NotesFormat.Outline, outline: outline);
            summaryNode["structured"] = System.Text.Json.JsonSerializer.SerializeToNode(payload, JsonDefaults.ArchiveOptions);
            await File.WriteAllTextAsync(summaryPath, summaryNode.ToJsonString(JsonDefaults.ArchiveOptions));
            await store.EndAsync(meta);
            await state.RefreshHistoryAsync();
            var row = state.History.FirstOrDefault(item => item.Meta.Id == meta.Id);
            if (row is null) throw new InvalidOperationException("会议没有出现在历史列表里。");
            await state.SelectHistoryAsync(row.Meta);
            state.Section = AppSection.Notes;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(500);
            Step("会议页加载（四标签）", state.NotesTabIndex is >= 0 and <= 3 && state.CurrentSession?.Id == meta.Id,
                "标签索引 " + state.NotesTabIndex + "；证据 " + state.NotesEvidence.Count + " 条");
            await Task.Delay(500);
            CaptureVisual(this, Path.Combine(directory, "notes-light.png"));

            // Real outline view (OutlineView.swift), not the Markdown substitute.
            state.SetNotesFormatCommand.Execute(NotesFormat.Outline);
            var outlineDeadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < outlineDeadline && state.NotesOutlineRows.Count == 0) await Task.Delay(100);
            Step("真提纲视图", state.HasOutlineSections && state.NotesOutlineRows.Count > 0
                && state.NotesOutlineRows[0].Lines.Count > 0 && state.HasOutlineConcepts,
                "格式=" + state.SelectedNotesFormat + "；payload=" + (state.StructuredPayload is not null)
                + "；md=" + state.StructuredMarkdown.Length + "；章节 " + state.NotesOutlineRows.Count
                + "；概念 " + state.NotesOutlineConcepts.Count + "；待复习 " + state.NotesOutlineReview.Count);
            await Task.Delay(500);
            CaptureVisual(this, Path.Combine(directory, "notes-outline-light.png"));

            // Jump stays inside the meeting page and highlights the transcript line.
            state.ShowNotesTranscriptAt(40);
            await Task.Delay(400);
            var highlighted = state.Transcript.FirstOrDefault(item => item.IsHighlighted);
            Step("纪要内跳到转写并高亮", state.NotesTabIndex == 1 && highlighted is not null && state.HasNotesJumpNotice,
                state.NotesJumpNotice + "；高亮=" + (highlighted?.Text ?? ""));
            await Task.Delay(500);
            CaptureVisual(this, Path.Combine(directory, "notes-transcript-light.png"));

            // Personal notes + saved highlight with a jump link.
            state.NotesTabIndex = 3;
            state.PersonalNotesText = "课后要补：缓存回滚方案。";
            state.SaveNotesEditorCommand.Execute(null);
            state.HighlightLineCommand.Execute(state.Transcript.FirstOrDefault());
            await Task.Delay(300);
            Step("个人笔记与高亮", state.HasMeetingHighlights && state.PersonalNotesText.Length > 0,
                "高亮 " + state.MeetingHighlights.Count + " 条");
            await Task.Delay(500);
            CaptureVisual(this, Path.Combine(directory, "notes-personal-light.png"));

            // Meeting info sheet (SessionInfoView.swift).
            var infoWindow = new SessionInfoWindow { DataContext = state };
            infoWindow.Show();
            await Task.Delay(600);
            var groups = state.CurrentSessionInfoGroups();
            Step("会议信息分组", groups.Count >= 8 && groups.Any(group => group.Group == UiText.T("存储")),
                groups.Count + " 项");
            CaptureVisual(infoWindow, Path.Combine(directory, "notes-info-light.png"));
            infoWindow.Close();
            await Task.Delay(200);

            // ① Mac MeetingNotesEditor: sections/items with time captions and 「我已核实」.
            var structuredDoc = new StructuredNotesDoc("interview-review", "面试复盘", "zh", "后端二面",
            [
                new NotesSectionResult("impression", "一句话印象", NotesSectionKind.OneLine, false,
                    [new NotesItem { Text = "项目部分讲得扎实、有数字。", Time = "00:10" }]),
                new NotesSectionResult("questions", "逐题回放", NotesSectionKind.Questions, false,
                [
                    new NotesItem { Text = "如果删除缓存失败了怎么办？", Time = "18:22", Uncertain = true,
                        Answer = "重试队列 + binlog 异步删除。", Verdict = "good", Better = "先给结论。" },
                    new NotesItem { Text = "热点 key 过期时请求打到数据库怎么办？", Time = "27:05" }
                ])
            ]);
            state.ShowStructuredNotesEditorForCheck(structuredDoc);
            var structuredEditor = new NotesEditorWindow { DataContext = state };
            structuredEditor.Show();
            await Task.Delay(700);
            Step("结构化纪要逐条编辑与已核实", state.NotesEditorStructured
                && state.NotesEditorSections.Count == 2 && state.NotesEditorSections[1].Items.Count == 2
                && state.NotesEditorSections[1].Items[0].Uncertain,
                "sections=" + state.NotesEditorSections.Count);
            CaptureVisual(structuredEditor, Path.Combine(directory, "notes-editor-light.png"));
            Application.Current!.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
            await Task.Delay(400);
            CaptureVisual(structuredEditor, Path.Combine(directory, "notes-editor-dark.png"));
            Application.Current.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
            await Task.Delay(250);
            structuredEditor.Close();
            await Task.Delay(200);

            // 重新整理 asks before overwriting edited notes (NotesRegeneration.swift).
            var folder = store.Folder(meta.Id);
            var content = SessionUserContentStore.Load(folder);
            SessionUserContentStore.Save(content with { EditedSummary = new SessionSummary { Markdown = "我改过的纪要" } }, folder);
            var edited = state.HasEditedNotesForCurrentSession;
            Step("改过纪要时重新整理先确认", noEditsAtStart && edited, "无修订=" + noEditsAtStart + "；有修订=" + edited);
            passed = steps.All(step => (bool)step.GetType().GetProperty("passed")!.GetValue(step)!);
        }
        catch (Exception ex)
        {
            error = ex.ToString();
        }
        var report = new { passed, error, steps };
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "notes-ui-check.json"),
                System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }));
        }
        catch { }
        Console.WriteLine(passed ? "notes-ui-check: passed" : "notes-ui-check: failed " + (error ?? ""));
        var idle = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < idle && state.Working) await Task.Delay(100);
        Close();
    }
}
