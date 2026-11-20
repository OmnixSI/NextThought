namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunMeetingPlanCheckAsync(string path)
    {
        await Task.Delay(100);
        var series = new NextThought.Core.WorkspaceSeries("series", "组会", DateTimeOffset.UtcNow, ["s1"],
            [new NextThought.Core.WorkspaceLesson { Text = "经验" }])
        {
            QuestionBank = [new NextThought.Core.QuestionBankItem { Question = "问", Answer = "答" }]
        };
        var plan = new NextThought.Core.MeetingPlan
        {
            Mode = NextThought.Core.MeetingMode.Custom,
            CustomBaseMode = NextThought.Core.MeetingMode.Academic,
            Carry = NextThought.Core.MeetingCarry.Standard with { Review = false }
        };
        var prepared = plan.PreparedWorkspace(series);
        var passed = plan.EffectiveMode == NextThought.Core.MeetingMode.Academic
                     && prepared.QuestionBank.Count == 0
                     && prepared.Lessons.Count == 1
                     && series.QuestionBank.Count == 1;
        var report = new { passed, effectiveMode = plan.EffectiveMode.ToString(), lessons = prepared.Lessons.Count, questions = prepared.QuestionBank.Count };
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
        Console.WriteLine(passed ? "meeting-plan-check: passed" : "meeting-plan-check: failed");
        Close();
    }

    /// <summary>Builds an evidence reference and checks its provenance and support label.</summary>
}
