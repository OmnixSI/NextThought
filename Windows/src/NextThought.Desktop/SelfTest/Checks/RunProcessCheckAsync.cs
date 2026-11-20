namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunProcessCheckAsync(string path)
    {
        await Task.Delay(100);
        var lines = new List<string>();
        using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("alpha\nbeta\r\ngamma")))
            await foreach (var line in NextThought.Core.ProcessLineReader.Lines(stream)) lines.Add(line);

        var status = -1;
        using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c exit 5",
            UseShellExecute = false,
            CreateNoWindow = true
        })!)
        using (var waiter = new NextThought.Core.ProcessExitWaiter(process))
            status = await waiter.WaitAsync();

        var shared = Task.FromResult(42);
        var value = await NextThought.Core.CancellableTask.Value(shared);
        var passed = lines.SequenceEqual(["alpha", "beta", "gamma"]) && status == 5 && value == 42;
        var report = new { passed, lines, status, value };
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
        Console.WriteLine(passed ? "process-check: passed" : "process-check: failed");
        Close();
    }

    /// <summary>Searches two archived-meeting field sets through the ported content search.</summary>
}
