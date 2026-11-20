namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunPromptCheckAsync(string path)
    {
        await Task.Delay(120);
        var (system, user) = AnswerPromptBuilder.MakeParts(
            "为什么温度补偿后残差仍集中在支座附近？",
            "对方：温度补偿后残差还是集中在支座附近。",
            "我做过连续刚构桥监测。",
            [new AnswerTurn("上一问", "上一答")],
            hasScreenshot: false,
            mode: NextThought.Core.MeetingMode.LabMeeting,
            style: "concise",
            excerpts: [new PromptExcerpt(new string('资', 900), "监测报告", new DocumentLocator { Page = 12 })],
            today: DateTimeOffset.Now);
        var passed = system.Contains("你是 NextThought", StringComparison.Ordinal)
                     && system.Contains("不要使用工具", StringComparison.Ordinal)
                     && user.Contains("为什么温度补偿", StringComparison.Ordinal)
                     && user.Contains("\"labMeeting\"", StringComparison.Ordinal)
                     && user.Contains("\"retrieved_material\"", StringComparison.Ordinal)
                     && system.Length > 2000;
        var report = new { passed, systemChars = system.Length, userChars = user.Length, user };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            }));
        }
        catch { }
        Console.WriteLine(passed ? "prompt-check: passed" : "prompt-check: failed");
        Close();
    }

    /// <summary>Exercises the ported answer-service rules and writes their outcomes.</summary>
}
