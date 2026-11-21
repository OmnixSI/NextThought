using System.Diagnostics;

namespace NextThought.Core;

/// <summary>
/// Restores a pre-update backup after this process exits (the running executable cannot be
/// overwritten in place), then starts the restored program again.
/// </summary>
public static class UpdateRestore
{
    public static string Script(string backupPath, string targetDirectory, string executablePath, int processId)
    {
        static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
        return $$"""
            $ErrorActionPreference = 'Stop'
            try { Wait-Process -Id {{processId}} -Timeout 60 -ErrorAction SilentlyContinue } catch { }
            if (Test-Path -LiteralPath {{Quote(targetDirectory)}}) {
                Get-ChildItem -LiteralPath {{Quote(backupPath)}} -Force | Copy-Item -Destination {{Quote(targetDirectory)}} -Recurse -Force
            } else {
                Copy-Item -LiteralPath {{Quote(backupPath)}} -Destination {{Quote(targetDirectory)}} -Recurse -Force
            }
            Start-Process -FilePath {{Quote(executablePath)}}
            """;
    }

    /// <summary>Writes the restore script and starts it detached; the caller should then exit.</summary>
    public static string Launch(string backupPath, string targetDirectory, string executablePath, int processId)
    {
        var script = Path.Combine(Path.GetTempPath(), "nextthought-restore-" + Guid.NewGuid().ToString("N") + ".ps1");
        File.WriteAllText(script, Script(backupPath, targetDirectory, executablePath, processId));
        Process.Start(new ProcessStartInfo("powershell.exe",
            "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + script + "\"")
        {
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
        return script;
    }
}
