namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunSeriesBriefingCheckAsync(string path)
    {
        await Task.Delay(100);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var series = new NextThought.Core.WorkspaceSeries("series", "结构组会", DateTimeOffset.UtcNow,
            [first.ToString("D"), second.ToString("D")],
            [
                new NextThought.Core.WorkspaceLesson { Text = "置顶经验", Pinned = true, CreatedAt = DateTimeOffset.UtcNow.AddDays(-5) },
                new NextThought.Core.WorkspaceLesson { Text = "新经验", CreatedAt = DateTimeOffset.UtcNow.AddDays(-1) }
            ])
        {
            QuestionBank = [new NextThought.Core.QuestionBankItem { Question = "常见问题", Answer = "答案", TimesAsked = 4 }]
        };
        var sessions = new[]
        {
            new NextThought.Core.SessionMeta { Id = first, Title = "第一次", StartedAt = DateTimeOffset.UtcNow.AddDays(-2) },
            new NextThought.Core.SessionMeta { Id = second, Title = "第二次", StartedAt = DateTimeOffset.UtcNow.AddDays(-1) }
        };
        var briefing = NextThought.Core.SeriesBriefingBuilder.Build(series, sessions, meetingLimit: 1, knowledgeLimit: 2);
        var passed = briefing.SessionCount == 2
                     && briefing.LatestSessions.Count == 1 && briefing.LatestSessions[0].Title == "第二次"
                     && briefing.Lessons.Count == 2 && briefing.Lessons[0].Text == "置顶经验"
                     && briefing.QuestionBank.Count == 1 && briefing.QuestionBank[0].TimesAsked == 4;
        var report = new { passed, sessionCount = briefing.SessionCount, latest = briefing.LatestSessions.Select(s => s.Title), lessons = briefing.Lessons.Select(l => l.Text), questions = briefing.QuestionBank.Select(q => q.Question) };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "series-briefing-check: passed" : "series-briefing-check: failed");
        Close();
    }

    /// <summary>Searches prepared history snapshots and checks hit labels/navigation targets.</summary>
}
