namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunPdfCheckAsync(string path)
    {
        await Task.Delay(100);
        var pages = Enumerable.Range(1, 5).Select(page =>
            $"ACME 年报\n甲{page}\n乙{page}\n丙{page}\n丁{page}\n戊{page}\n第 {page} 页").ToList();
        var furniture = NextThought.Core.PDFTextLayout.Furniture(pages);
        var pageText = $"ACME 年报\n摘要\n{new string('X', 60)}\n{new string('Y', 60)}\n{new string('Z', 60)}\n{new string('W', 60)}\n{new string('V', 60)}\n{new string('U', 60)}\n第 6 页";
        var markdown = NextThought.Core.PDFTextLayout.Markdown(pageText, furniture, numberedHeadings: false);
        var passed = furniture.Contains("acme 年报")
                     && !furniture.Contains("甲1")
                     && markdown.Contains("## 摘要", StringComparison.Ordinal)
                     && !markdown.Contains("ACME", StringComparison.Ordinal)
                     && !markdown.Contains("第 6 页", StringComparison.Ordinal);
        var report = new { passed, furniture = furniture.OrderBy(key => key).ToArray(), markdown };
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
        Console.WriteLine(passed ? "pdf-check: passed" : "pdf-check: failed");
        Close();
    }

    /// <summary>Exercises eye geometry, calibration and gaze readings in the shipped process.</summary>
}
