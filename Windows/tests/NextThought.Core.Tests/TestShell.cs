using System.Diagnostics;

namespace NextThought.Core.Tests;

/// <summary>B24: test child processes must not hard-code cmd.exe; on Unix use /bin/sh.</summary>
public sealed record TestShellCommand(string FileName, string Arguments);

public static class TestShell
{
    public static TestShellCommand For(string windows, string unix) =>
        OperatingSystem.IsWindows()
            ? new TestShellCommand("cmd.exe", "/c " + windows)
            : new TestShellCommand("/bin/sh", "-c \"" + unix.Replace("\"", "\\\"") + "\"");
}
