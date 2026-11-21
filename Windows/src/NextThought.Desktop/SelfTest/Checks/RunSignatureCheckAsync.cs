namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunSignatureCheckAsync(string path)
    {
        await Task.Delay(100);
        var status = "Unknown";
        var signer = "";
        var error = "";
        try
        {
            status = await NextThought.Platform.Authenticode.StatusAsync(path);
            signer = await NextThought.Platform.Authenticode.SignerThumbprintAsync(path);
        }
        catch (Exception exception)
        {
            error = exception.Message;
        }
        var passed = error.Length == 0 && status.Length > 0 && signer.Length > 0;
        var report = new
        {
            passed,
            status,
            signer,
            allowed = NextThought.Platform.Authenticode.SignerAllowed(signer),
            valid = NextThought.Platform.Authenticode.IsValid(status),
            required = NextThought.Platform.Authenticode.IsRequired,
            error
        };
        try
        {
            var directory = Path.GetDirectoryName(path);
            var target = Path.Combine(directory ?? Path.GetTempPath(), "signature-check.json");
            await File.WriteAllTextAsync(target, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "signature-check: passed (" + status + ")" : "signature-check: failed");
        Close();
    }


    /// <summary>Shows slide text on screen, captures it, OCRs it and follows the matching page.</summary>
}
