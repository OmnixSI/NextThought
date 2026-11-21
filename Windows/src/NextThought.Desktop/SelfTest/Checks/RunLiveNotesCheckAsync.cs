using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// J8/④: <c>--self-test=live-notes</c> — the Mac 3.4.0 <c>SelfTest.testLiveNotes</c> entry. The
    /// Mac version feeds a real transcript through the answer service; the self-check here drives the
    /// same pieces without a model: the demo notes install into the live page, the update schedule,
    /// the prompt, and the reply parsing (including the settled topics and a broken reply).
    /// </summary>
    private async Task RunLiveNotesCheckAsync(string path)
    {
        await Task.Delay(300);
        var steps = new List<object>();
        var passed = false;
        try
        {
            state.LoadDemo();
            await Task.Delay(100);
            state.ApplyDemoStage(DemoStages.LiveNotes);
            await PumpAsync();

            void Step(string name, bool ok, object? detail = null) => steps.Add(new { step = name, ok, detail });

            // 1) the demo script (Mac NEXTTHOUGHT_DEMO_LIVE_NOTES / installDemo) shows kept notes.
            Step("demo notes are on", state.LiveNotesEnabled);
            Step("demo notes have content", state.LiveNotesHasContent);
            Step("two topics", state.LiveNotesTopicRows.Count == 2, state.LiveNotesTopicRows.Count);
            Step("summary", state.LiveNotesSummaryText.Length > 0);
            Step("fresh marks", state.LiveNotesTopicRows.SelectMany(row => row.Points).Any(point => point.Fresh));
            Step("decisions and todos", state.LiveNotesDecisionRows.Count == 1 && state.LiveNotesTodoRows.Count == 1);

            // 2) the schedule (Mac LiveNotesSchedule): first notes early, then every 45 s of new lines.
            var empty = new LiveNotes();
            Step("first notes after three lines", LiveNotesSchedule.IsDue(empty, LiveNotesSchedule.FirstLines, 0));
            Step("not before", !LiveNotesSchedule.IsDue(empty, 2, 0));
            Step("first wait", LiveNotesSchedule.IsDue(empty, 2, LiveNotesSchedule.FirstWait + 1));
            var covered = new LiveNotes { Summary = "已有纪要", Covered = 5 };
            Step("no new lines means no update", !LiveNotesSchedule.IsDue(covered, 5, LiveNotesSchedule.Interval * 2));
            Step("interval", !LiveNotesSchedule.IsDue(covered, 6, LiveNotesSchedule.Interval - 1)
                              && LiveNotesSchedule.IsDue(covered, 6, LiveNotesSchedule.Interval));

            // 3) the prompt carries the notes, the new lines and the language.
            var lines = new[]
            {
                new LiveNotesPrompt.Line(0, "老师", "先说说你的项目。"),
                new LiveNotesPrompt.Line(20, "我", "我负责订单服务的缓存层。")
            };
            var prompt = LiveNotesPrompt.User(covered, lines, "简体中文", "面试");
            Step("prompt language", prompt.Contains("纪要语言：简体中文"));
            Step("prompt carries new lines", prompt.Contains("我负责订单服务的缓存层。"));
            Step("prompt carries frozen topics", prompt.Contains("已有纪要") || prompt.Contains("更早的议题"));

            // 4) parsing lays settled topics back in front and takes the model's new ones.
            var previous = new LiveNotes
            {
                Summary = "旧纪要",
                Covered = 6,
                Topics =
                [
                    new LiveNotes.Topic("已经定稿的议题", ["旧要点"]),
                    new LiveNotes.Topic("旧议题二", ["旧要点二"]),
                    new LiveNotes.Topic("旧议题三", ["旧要点三"])
                ],
                Decisions = ["旧决定"],
                Todos = [new LiveNotes.Todo("旧待办", "我")]
            };
            const string reply = """
                {"summary":"新纪要","topics":[{"title":"已经定稿的议题","points":["改写"]},{"title":"新议题","points":["新要点"]}],"decisions":["新决定"],"todos":[{"text":"新待办","owner":"对方"}]}
                """;
            var parsed = LiveNotesPrompt.Parse(reply, previous, 9);
            Step("reply parsed", parsed is not null);
            if (parsed is not null)
            {
                // The settled topic comes back from the previous notes, the re-sent copy is dropped.
                Step("settled topic kept", parsed.Topics.Count == 2 && parsed.Topics[0].Title == "已经定稿的议题" && parsed.Topics[0].Points[0] == "旧要点");
                Step("new topic appended", parsed.Topics[^1].Title == "新议题");
                Step("summary updated", parsed.Summary == "新纪要");
                Step("decisions and todos", parsed.Decisions.Count == 1 && parsed.Todos.Count == 1 && parsed.Todos[0].Owner == "对方");
                Step("covered lines", parsed.Covered == 9);
            }
            Step("broken reply keeps the old notes", LiveNotesPrompt.Parse("{\"summary\":\"\"}", previous, 9) is null);
            Step("not json keeps the old notes", LiveNotesPrompt.Parse("没有 JSON", previous, 9) is null);

            passed = steps.All(step => (bool)step.GetType().GetProperty("ok")!.GetValue(step)!);
            await WriteReportAsync(path, new { passed, steps, enabled = state.LiveNotesEnabled, status = state.LiveNotesStatusText });
            Console.WriteLine(passed ? "live-notes-check: passed" : "live-notes-check: failed");
        }
        catch (Exception error)
        {
            await WriteReportAsync(path + ".error.txt", new { passed = false, error = error.ToString() });
            Console.Error.WriteLine("live-notes-check: " + error);
        }
        finally
        {
            Close();
        }
    }
}
