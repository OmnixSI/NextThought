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
            "zoom" => "RunTextZoomCheckAsync",
            "windows" => "RunWindowsCheckAsync",
            "share-test" => "RunShareTestCheckAsync",
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
        try
        {
            var result = method.Invoke(this, [output]);
            if (result is Task task) await task;
        }
        catch (Exception error)
        {
            var inner = error is System.Reflection.TargetInvocationException { InnerException: { } cause } ? cause : error;
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            await File.WriteAllTextAsync(output + ".error.txt", inner.ToString());
            Console.Error.WriteLine("self-test " + normalized + ": " + inner);
        }
        finally
        {
            Close();
        }
    }
}
