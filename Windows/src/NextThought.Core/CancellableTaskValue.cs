namespace NextThought.Core;

/// <summary>
/// Waits for a shared task without making an individual cancelled caller wait for it, and
/// without cancelling the shared task other callers will reuse (port of CancellableTaskValue).
/// </summary>
public static class CancellableTask
{
    public static async Task<T> Value<T>(Task<T> task, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await task.WaitAsync(cancellationToken);
    }
}
