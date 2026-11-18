using System.Diagnostics;

namespace NextThought.Platform;

/// <summary>
/// Authenticode status of a downloaded installer. The updater can require a valid signature
/// (NEXTTHOUGHT_REQUIRE_SIGNATURE=1) before launching it; unsigned QA builds still install when
/// the switch is absent, but their status is reported rather than assumed.
/// </summary>
public static class Authenticode
{
    public static async Task<string> StatusAsync(string path, CancellationToken cancellationToken = default)
    {
        var escaped = path.Replace("'", "''");
        var script = "(Get-AuthenticodeSignature -LiteralPath '" + escaped + "').Status";
        var start = new ProcessStartInfo("powershell.exe",
            "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + script + "\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("无法启动签名检查。");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var output = await process.StandardOutput.ReadToEndAsync(timeout.Token);
        try { await process.WaitForExitAsync(timeout.Token); } catch (OperationCanceledException) { try { process.Kill(); } catch { } throw; }
        var status = output.Trim();
        return status.Length == 0 ? "Unknown" : status;
    }

    /// <summary>Only a fully trusted, unmodified signature counts.</summary>
    public static bool IsValid(string status) => string.Equals(status, "Valid", StringComparison.OrdinalIgnoreCase);

    public static bool IsRequired =>
        Environment.GetEnvironmentVariable("NEXTTHOUGHT_REQUIRE_SIGNATURE") == "1";
}
