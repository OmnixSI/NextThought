using System.Diagnostics;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from ProcessExitWaiterTests.swift (exitBeforeAwaitIsRecorded): a child process
/// that has already exited before the await is still recorded, twenty times in a row.
/// </summary>
public sealed class ProcessExitWaiterParityTests
{
    [Fact]
    public async Task ExitBeforeAwaitIsRecorded()
    {
        var command = TestShell.For("exit 0", "exit 0");
        for (var iteration = 0; iteration < 20; iteration++)
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = command.FileName,
                Arguments = command.Arguments,
                UseShellExecute = false,
                CreateNoWindow = true
            })!;
            // Swift installs the waiter before run(); EnableRaisingEvents needs a started process.
            using var completion = new ProcessExitWaiter(process);
            await Task.Delay(10);
            Assert.Equal(0, await completion.WaitAsync());
        }
    }
}
