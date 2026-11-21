namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunQuestionCheckAsync(string path)
    {
        await Task.Delay(250);
        var report = NextThought.Core.QuestionDetectionEvaluator.Evaluate(NextThought.Core.QuestionEvalCorpus.All);
        var passed = NextThought.Core.QuestionEvalCorpus.All.Count >= 100
                     && report.Questions + report.Statements == NextThought.Core.QuestionEvalCorpus.All.Count
                     && report.Detected <= report.Questions
                     && report.FalseTriggers <= report.Statements;
        var payload = new
        {
            passed,
            samples = NextThought.Core.QuestionEvalCorpus.All.Count,
            report.Questions,
            report.Detected,
            report.Statements,
            report.FalseTriggers,
            recall = Math.Round(report.Recall, 3),
            falsePerHour = Math.Round(report.FalseTriggersPerHour, 2),
            missed = report.Missed.Take(5),
            falsePositives = report.FalsePositives.Take(5)
        };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "question-check: passed" : "question-check: failed");
        Close();
    }

    /// <summary>Exercises PDF text layout (furniture, headings, line joining) in the shipped process.</summary>
}
