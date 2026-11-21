namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunBpeCheckAsync(string path)
    {
        await Task.Delay(100);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var fixture = Path.Combine(directory ?? Path.GetTempPath(), "tokenizer-check.json");
        await File.WriteAllTextAsync(fixture, """
            {"model":{"type":"BPE","vocab":{"a":0,"b":1,"ab":2,"ä":3,"¸":4,"\u0143":5},"merges":[["a","b"]]},
             "added_tokens":[{"content":"<|endoftext|>","id":6}]}
            """);
        var tokenizer = new NextThought.Core.ByteLevelBPETokenizer(fixture,
            new NextThought.Core.ByteLevelBPETokenizer.Configuration(6, false, @"\S+"));
        var ascii = tokenizer.Encode("ab");
        var chinese = tokenizer.Encode("中");
        var passed = ascii.SequenceEqual([2])
                     && chinese.SequenceEqual([3, 4, 5])
                     && tokenizer.Decode(ascii) == "ab"
                     && tokenizer.Decode(chinese) == "中"
                     && tokenizer.TokenId("ab") == 2;
        var report = new { passed, ascii = ascii.ToArray(), chinese = chinese.ToArray(), decoded = tokenizer.Decode(chinese) };
        try
        {
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "bpe-check: passed" : "bpe-check: failed");
        Close();
    }

    /// <summary>Builds a glossary, expands a misheard search and reports the forms.</summary>
}
