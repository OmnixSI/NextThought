namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunRetrievalCheckAsync(string path)
    {
        await Task.Delay(100);
        var document = new NextThought.Core.MaterialDocument
        {
            Id = "check",
            Name = "check.pdf",
            Blocks = [new NextThought.Core.DocumentBlock("p.1", "# 方法\n短一\n短二", 1)]
        };
        var chunks = NextThought.Core.ContextChunker.Chunks([document]);
        var index = new NextThought.Core.SemanticIndex();
        index.SetVector([1f, 0f], chunks[0].Fingerprint, 0);
        var ranking = index.RankedOrders([1f, 0f], chunks.Select(chunk => chunk.Fingerprint).ToList());
        var fused = NextThought.Core.HybridRanker.Fuse([0], [0]);
        var pages = new NextThought.Core.MaterialDocument
        {
            Id = "pages",
            Name = "slides.pdf",
            Blocks = [new NextThought.Core.DocumentBlock("p.1", "温度补偿方法", 1), new NextThought.Core.DocumentBlock("p.2", "残差分析", 2)]
        };
        var explicitPage = new NextThought.Core.HybridContextIndex([pages]).Retrieve("第1页讲了什么");
        var passed = chunks.Count == 1
                     && chunks[0].Text.Contains("方法", StringComparison.Ordinal)
                     && chunks[0].Page == 1
                     && ranking.Count == 1 && Math.Abs(ranking[0].Score - 1) < 1e-6
                     && fused.Count == 1 && fused[0].Order == 0
                     && explicitPage.Count > 0 && explicitPage[0].Page == 1;
        var report = new { passed, chunks = chunks.Count, fingerprint = chunks[0].Fingerprint, text = chunks[0].Text, score = ranking.Count > 0 ? ranking[0].Score : 0, explicitPage = explicitPage.Count > 0 ? explicitPage[0].Page : 0 };
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
        Console.WriteLine(passed ? "retrieval-check: passed" : "retrieval-check: failed");
        Close();
    }

    /// <summary>Exercises cloud STT provider config and message assembly in the shipped process.</summary>
}
