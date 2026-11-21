using System.Net;
using System.Text;
using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E13 (决议 §3.3 screenshot-question): asking about a screenshot sends the picture itself, not
/// only its OCR text. The OpenAI-compatible path must carry an image content block (Mac
/// ChatCompletionStreamParser.body imageJPEGBase64); the Codex path uses the protocol localImage
/// attachment (covered by AnswerStreamingTests). A plain question keeps the string content shape.
/// </summary>
public sealed class ChatCompletionImageInputTests
{
    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            var sse = "data: {\"choices\":[{\"delta\":{\"content\":\"ok\"},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sse, Encoding.UTF8, "text/event-stream")
            };
        }
    }

    private static async Task<string> AskAsync(string? imagePath)
    {
        var handler = new CaptureHandler();
        var client = new ChatCompletionClient(new HttpClient(handler));
        var options = new AnswerOptions("http://localhost:11434/v1", "local-model", "test-key");
        var answer = await client.AnswerAsync(options, "system prompt", "这张图里是什么？", imagePath: imagePath);
        Assert.Equal("ok", answer);
        Assert.NotNull(handler.Body);
        return handler.Body!;
    }

    [Fact]
    public async Task ScreenshotIsSentAsAnImageContentBlockWithTheText()
    {
        var path = Path.Combine(Path.GetTempPath(), "nt-e13-" + Guid.NewGuid().ToString("N") + ".png");
        await File.WriteAllBytesAsync(path, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01]);
        try
        {
            using var document = JsonDocument.Parse(await AskAsync(path));
            var messages = document.RootElement.GetProperty("messages");
            Assert.Equal("system prompt", messages[0].GetProperty("content").GetString());
            var content = messages[1].GetProperty("content");
            Assert.Equal(JsonValueKind.Array, content.ValueKind);
            Assert.Equal("text", content[0].GetProperty("type").GetString());
            Assert.Equal("这张图里是什么？", content[0].GetProperty("text").GetString());
            Assert.Equal("image_url", content[1].GetProperty("type").GetString());
            var url = content[1].GetProperty("image_url").GetProperty("url").GetString()!;
            Assert.StartsWith("data:image/png;base64,", url, StringComparison.Ordinal);
            Assert.Equal(Convert.ToBase64String(await File.ReadAllBytesAsync(path)), url["data:image/png;base64,".Length..]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ATextOnlyQuestionKeepsThePlainStringContent()
    {
        using var document = JsonDocument.Parse(await AskAsync(null));
        var content = document.RootElement.GetProperty("messages")[1].GetProperty("content");
        Assert.Equal(JsonValueKind.String, content.ValueKind);
        Assert.Equal("这张图里是什么？", content.GetString());
    }

    [Fact]
    public void TheCodexPathPassesTheImageAsALocalImageAttachment()
    {
        var root = FindRoot();
        var codex = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Platform", "CodexAnswerClient.cs"));
        Assert.Contains("imagePath", codex);
        Assert.Contains("CodexAppServerProtocol.TurnStartParams", codex);
        var protocol = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Core", "AnswerStreaming.cs"));
        Assert.Contains("localImage", protocol);
        Assert.Contains("imagePath", protocol);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "App.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }
}
