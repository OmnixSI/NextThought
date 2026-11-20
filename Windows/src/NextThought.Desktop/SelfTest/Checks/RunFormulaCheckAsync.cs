namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunFormulaCheckAsync(string path)
    {
        await Task.Delay(150);
        const string input = """
            答案：面积为 $\frac{1}{2}$，函数为 $x^2$。
            $$\sum_{i=1}^{n} i$$
            代码 `$a$` 保持原样。
            """;
        var converted = NextThought.Core.LaTeXMath.Markdown(input);
        var view = new MarkdownText { Text = input };
        var rendered = ReadText(view.Content);

        static string ReadText(object? node) => node switch
        {
            SelectableTextBlock block => block.Inlines is { Count: > 0 }
                ? string.Concat(block.Inlines.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text))
                : block.Text ?? "",
            TextBlock block => block.Inlines is { Count: > 0 }
                ? string.Concat(block.Inlines.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text))
                : block.Text ?? "",
            Border border => ReadText(border.Child),
            Panel panel => string.Join(" ", panel.Children.Select(ReadText)),
            _ => ""
        };
        var passed = converted.Contains("½", StringComparison.Ordinal)
                     && converted.Contains("x²", StringComparison.Ordinal)
                     && converted.Contains("∑ᵢ₌₁ⁿ i", StringComparison.Ordinal)
                     && converted.Contains("`$a$`", StringComparison.Ordinal)
                     && rendered.Contains("x²", StringComparison.Ordinal)
                     && rendered.Contains("½", StringComparison.Ordinal);
        var report = new { passed, converted, rendered };
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
        Console.WriteLine(passed ? "formula-check: passed" : "formula-check: failed");
        Close();
    }

    /// <summary>
    /// Creates a restore point with the real store, restores it into a temp folder and checks
    /// dedupe/prune on a second identical snapshot.
    /// </summary>
}
