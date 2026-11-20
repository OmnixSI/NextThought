using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// E22: drives 术语和人名 on an isolated archive — global/series scopes, layered find with the
    /// recorded misheard spelling (MisheardMark), transcript find inside the meeting page,
    /// AddToGlossarySheet flow, paste import, material terms and tidy/export.
    /// </summary>
    private async Task RunGlossaryUiCheckAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var steps = new List<object>();
        var passed = false;
        string? error = null;
        void Step(string name, bool ok, string detail) => steps.Add(new { step = name, passed = ok, detail = detail });
        try
        {
            var store = new SessionStore(state.Paths);
            var meta = new SessionMeta { Title = "E22 术语自检会议" };
            await store.CreateAsync(meta);
            await store.AppendTranscriptAsync(meta, new TranscriptLine
            {
                Date = meta.StartedAt.AddSeconds(5), StartedAt = meta.StartedAt.AddSeconds(5),
                Speaker = "remote", Text = "库伯尼提斯的发布顺序要确认。"
            });
            await store.AppendTranscriptAsync(meta, new TranscriptLine
            {
                Date = meta.StartedAt.AddSeconds(15), StartedAt = meta.StartedAt.AddSeconds(15),
                Speaker = "me", Text = "我们聊了定单链路的回滚。"
            });
            await store.SaveSummaryAsync(meta, "## 摘要\n缓存一致性要确认。", "m");
            await store.EndAsync(meta);

            var material = new MaterialDocument
            {
                Name = "交易系统资料.md",
                Extension = ".md",
                Blocks = [new DocumentBlock("p.1", "交易系统关注吞吐量与订单链路的延迟。")]
            };
            var series = WorkspaceSeries.Create("E22 术语系列") with
            {
                SessionIds = [meta.Id.ToString("D")],
                Glossary = new Glossary([new GlossaryEntry("订单链路", GlossaryKind.Term, ["定单链路"])])
            };
            series = SeriesMaterials.Attach(series, material);
            new WorkspaceStore(state.Paths.Root).Save(new WorkspaceData([series]));
            var globalGlossary = new Glossary([
                new GlossaryEntry("Kubernetes", GlossaryKind.Term, ["K8s", "库伯尼提斯"]),
                new GlossaryEntry("缓存一致性")
            ]);
            new GlossaryStore(state.Paths.Root).Save(globalGlossary);

            state.LoadGlossary();
            state.ReloadWorkspaceForCheck();
            await state.RefreshHistoryAsync();
            state.FocusSeriesForCheck(series.Id);
            await state.SelectHistoryAsync(state.History.First(row => row.Meta.Id == meta.Id).Meta);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(400);
            Step("全局术语加载", state.GlossaryRows.Count == 2 && !state.GlossarySeriesScope,
                "全局 " + state.GlossaryRows.Count + " 条");

            // 1) 查找：敲规范写法，找到听错写法的行并显示 MisheardMark。
            state.GlossaryQuery = "Kubernetes";
            await Task.Delay(300);
            Step("在转写中查找（听错写法也会找出来）", state.GlossaryTranscriptHits.Count > 0
                && state.GlossaryTranscriptHits[0].IsMisheard
                && state.GlossaryTranscriptHits[0].MisheardMarkText.Contains("库伯尼提斯"),
                state.GlossaryTranscriptHits.Count + " 处；" + state.GlossaryTranscriptHits.FirstOrDefault()?.MisheardMarkText);

            // 2) 会议页转写查找（TranscriptFindField）：系列术语叠加后也能找到听错的行。
            state.TranscriptFindQuery = "订单链路";
            await Task.Delay(300);
            Step("会议页转写查找 + 系列术语叠加", state.TranscriptFindHits.Count > 0
                && state.TranscriptFindHits[0].IsMisheard, state.TranscriptFindCountText + "；"
                + state.TranscriptFindHits.FirstOrDefault()?.MisheardMarkText);

            // 3) 系列作用域：编辑系列条目并保存进 workspace。
            state.GlossarySeriesScope = true;
            await Task.Delay(300);
            Step("切换到当前系列作用域", state.GlossaryRows.Count == 1
                && state.GlossaryRows[0].Canonical == "订单链路", "系列 " + state.GlossaryRows.Count + " 条");
            if (state.GlossaryRows.Count > 0)
            {
                state.BeginGlossaryEntry(state.GlossaryRows[0]);
                state.GlossaryEditorNote = "交易系统";
                state.SaveGlossaryEntryCommand.Execute(null);
            }
            var saved = new WorkspaceStore(state.Paths.Root).Load().Find(series.Id)!;
            Step("保存系列术语（含备注）", saved.Glossary is { } seriesGlossary
                && seriesGlossary.Entries.Any(entry => entry.Canonical == "订单链路" && entry.Note == "交易系统"),
                "系列条目 " + (saved.Glossary?.Entries.Count ?? 0));

            // 4) AddToGlossarySheet 流程：从转写行加入全局术语。
            state.BeginAddToGlossary("我们聊了定单链路的回滚。");
            state.GlossaryDraftCanonical = "订单链路";
            state.GlossaryDraftHeard = "定单链路";
            state.GlossaryDraftSeriesOnly = false;
            var added = state.CommitAddToGlossary();
            state.LoadGlossary();
            Step("从转写加入术语表", added && state.EffectiveGlossary().Entries.Any(entry =>
                    entry.Canonical == "订单链路" && entry.Variants.Contains("定单链路")),
                "全局 " + state.GlossaryRows.Count + " 条");

            // 5) 批量粘贴 + 整理 + 导出。
            state.BeginGlossaryPaste();
            state.GlossarySeriesScope = false;
            state.GlossaryPasteText = "吞吐量 | 吞吐 | 术语\nP99 延迟 | P99 | 术语\n";
            state.ImportGlossaryPastedCommand.Execute(null);
            Step("批量粘贴导入", state.EffectiveGlossary().Entries.Any(entry => entry.Canonical == "吞吐量"),
                state.GlossaryStatusText ?? "");
            state.TidyGlossariesCommand.Execute(null);
            var exported = state.ExportGlossaryText();
            Step("整理与导出文字", exported.Contains("Kubernetes") && exported.Contains("吞吐量"),
                exported.Split('\n').Length + " 行");

            // 6) 资料术语叠加进有效术语（Mac adding(materialTerms)）。
            Step("资料术语叠加生效", state.EffectiveGlossary().Entries.Any(entry => entry.Canonical == "吞吐量"),
                "有效术语 " + state.EffectiveGlossary().Entries.Count + " 条");

            // 截图：术语窗口、加入术语表弹窗、会议页转写查找。
            var glossaryWindow = new GlossaryWindow { DataContext = state };
            glossaryWindow.Show();
            await Task.Delay(800);
            CaptureVisual(glossaryWindow, Path.Combine(directory, "glossary-light.png"));
            state.BeginAddToGlossary("我们聊了定单链路的回滚。");
            var sheet = new AddToGlossarySheet { DataContext = state };
            sheet.Show();
            await Task.Delay(600);
            CaptureVisual(sheet, Path.Combine(directory, "add-to-glossary-light.png"));
            sheet.Close();
            glossaryWindow.Close();
            await Task.Delay(200);
            state.Section = AppSection.Notes;
            state.NotesTabIndex = 1;
            state.TranscriptFindQuery = "订单链路";
            await Task.Delay(700);
            CaptureVisual(this, Path.Combine(directory, "notes-transcript-find-light.png"));
            passed = steps.All(step => (bool)step.GetType().GetProperty("passed")!.GetValue(step)!);
        }
        catch (Exception ex)
        {
            error = ex.ToString();
        }
        var report = new { passed, error, steps };
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "glossary-check.json"),
                System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }));
        }
        catch { }
        Console.WriteLine(passed ? "glossary-check: passed" : "glossary-check: failed " + (error ?? ""));
        var idle = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < idle && state.Working) await Task.Delay(100);
        Close();
    }
}
