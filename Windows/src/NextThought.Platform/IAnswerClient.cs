using NextThought.Core;

namespace NextThought.Platform;

public interface IAnswerClient
{
    /// <summary>
    /// Answers one question. <paramref name="imagePath"/> is the screenshot the user asked about
    /// (Mac AnswerRequest.imageURL): OpenAI-compatible services receive it as an image content
    /// block, the Codex path hands it to the model as a local image attachment.
    /// </summary>
    Task<string> AnswerAsync(AnswerOptions options, string system, string user, Action<string>? update = null,
        CancellationToken ct = default, int timeoutSeconds = 90, string? imagePath = null);
}
