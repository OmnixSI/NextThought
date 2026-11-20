using System.Reflection;

namespace NextThought.Desktop.Views;

/// <summary>H1: one named entry point for every check that used to have its own --xxx-check flag.</summary>
public partial class MainWindow
{
    public async Task RunNamedSelfTestAsync(string name)
    {
        var normalized = name.Trim().TrimStart('-').Replace("_", "-");
        var methodName = normalized switch
        {
            "startup" => "WriteStartupReportAsync",
            "subtitle-export" => "RunSubtitleExportCheckAsync",
            "backup" => "RunBackupCheckAsync",
            "acceptance" => "RunAcceptanceCheckAsync",
            _ => "Run" + string.Concat(normalized.Split('-', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..])) + "CheckAsync"
        };
        var method = GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new ArgumentException("unknown self-test: " + name);
        var output = Path.Combine(Path.GetTempPath(), "nextthought-selftest", normalized);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var result = method.Invoke(this, [output]);
        if (result is Task task) await task;
        Close();
    }
}
