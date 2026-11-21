using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace NextThought.Core;

/// <summary>
/// Reads newline-delimited lines from a stream on its own blocking reader (port of
/// ProcessLineReader): a process's idle pipe never blocks another process's reads.
/// </summary>
public static class ProcessLineReader
{
    public static async IAsyncEnumerable<string> Lines(Stream stream, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
        var pump = Task.Run(() =>
        {
            try
            {
                var buffer = new byte[64 * 1024];
                var bytes = new List<byte>();
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (var index = 0; index < read; index++)
                    {
                        var value = buffer[index];
                        if (value == (byte)'\n')
                        {
                            if (bytes.Count > 0 && bytes[^1] == (byte)'\r') bytes.RemoveAt(bytes.Count - 1);
                            channel.Writer.TryWrite(System.Text.Encoding.UTF8.GetString(bytes.ToArray()));
                            bytes.Clear();
                        }
                        else
                        {
                            bytes.Add(value);
                        }
                    }
                }
                if (bytes.Count > 0) channel.Writer.TryWrite(System.Text.Encoding.UTF8.GetString(bytes.ToArray()));
                channel.Writer.TryComplete();
            }
            catch (Exception exception)
            {
                channel.Writer.TryComplete(exception);
            }
        }, CancellationToken.None);

        try
        {
            await foreach (var line in channel.Reader.ReadAllAsync(cancellationToken)) yield return line;
        }
        finally
        {
            await pump;
        }
    }
}

/// <summary>
/// Records a child process's completion before it can exit unnoticed (port of
/// ProcessExitWaiter): sync and async waits always see the final status.
/// </summary>
public sealed class ProcessExitWaiter : IDisposable
{
    private readonly System.Diagnostics.Process process;
    private readonly EventHandler exitedHandler;
    private readonly TaskCompletionSource<int> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ProcessExitWaiter(System.Diagnostics.Process process)
    {
        this.process = process;
        exitedHandler = (_, _) => Complete();
        process.EnableRaisingEvents = true;
        process.Exited += exitedHandler;
        if (process.HasExited) Complete();
    }

    private void Complete()
    {
        try
        {
            completion.TrySetResult(process.ExitCode);
        }
        catch (InvalidOperationException)
        {
            completion.TrySetResult(-1);
        }
    }

    /// <summary>The exit status, waiting up to the timeout; null when it is still running.</summary>
    public int? WaitSynchronously(TimeSpan timeout)
    {
        if (completion.Task.IsCompleted) return completion.Task.Result;
        return process.WaitForExit((int)Math.Max(0, timeout.TotalMilliseconds)) ? completion.Task.Result : null;
    }

    public async Task<int> WaitAsync(CancellationToken cancellationToken = default) =>
        await completion.Task.WaitAsync(cancellationToken);

    public void Dispose()
    {
        process.Exited -= exitedHandler;
        process.Dispose();
    }
}
