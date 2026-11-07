using NextThought.Core;

namespace NextThought.Platform;

public interface IAnswerClient
{
    Task<string> AnswerAsync(AnswerOptions options, string system, string user, Action<string>? update = null,
        CancellationToken ct = default, int timeoutSeconds = 90);
}
