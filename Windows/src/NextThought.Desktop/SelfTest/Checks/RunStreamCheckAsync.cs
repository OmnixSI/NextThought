namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunStreamCheckAsync(string path)
    {
        await Task.Delay(100);
        var parsed = NextThought.Core.CodexAppServerProtocol.Parse("""{"id":2,"result":{"thread":{"id":"t1"}}}""");
        var delta = new NextThought.Core.ChatCompletionStreamParser().Parse("""data: {"choices":[{"delta":{"content":"Hi"}}]}""");
        var ids = NextThought.Core.ModelListParser.Ids("""{"data":[{"id":"b"},{"id":"a"}]}""");
        var passed = parsed is { Kind: NextThought.Core.CodexInboundKind.Response, ResponseId: 2 }
                     && NextThought.Core.CodexAppServerProtocol.ThreadIdFromThreadStart(parsed.Result!) == "t1"
                     && delta is NextThought.Core.ChatDelta { Text: "Hi" }
                     && ids.SequenceEqual(["a", "b"])
                     && NextThought.Core.ChatCompletionStreamParser.Endpoint("https://api.example.com/v1") == "https://api.example.com/v1/chat/completions";
        var report = new { passed, thread = NextThought.Core.CodexAppServerProtocol.ThreadIdFromThreadStart(parsed?.Result ?? new System.Text.Json.Nodes.JsonObject()), models = ids };
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
        Console.WriteLine(passed ? "stream-check: passed" : "stream-check: failed");
        Close();
    }

    /// <summary>Runs the first-layer rules over the labeled corpus and reports recall/false triggers.</summary>
}
