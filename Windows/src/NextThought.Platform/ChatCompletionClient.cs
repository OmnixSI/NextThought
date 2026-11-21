using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NextThought.Core;

namespace NextThought.Platform;

public sealed class ChatCompletionClient(HttpClient? http = null) : IAnswerClient
{
    private readonly HttpClient client = http ?? new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    public static Uri Endpoint(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri) || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0
            || (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)))
            throw new ArgumentException("请填写 HTTPS API 地址，本机服务可使用 http://localhost。");
        var path = uri.AbsolutePath.TrimEnd('/');
        if (!path.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)) path += (path.Length == 0 ? "/v1" : "") + "/chat/completions";
        return new UriBuilder(uri) { Path = path }.Uri;
    }

    /// <summary>E27: GET {base}/models — most OpenAI-compatible services list their models there (Mac fetchAPIModels).</summary>
    public static async Task<(IReadOnlyList<string> Ids, string Status)> FetchModelsAsync(
        string baseUrl, string apiKey, HttpClient? http = null, CancellationToken ct = default)
    {
        var endpoint = APIPresetLogic.ModelListEndpoint(baseUrl);
        if (endpoint is null) return ([], UiText.T("API 地址无效"));
        using var client = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            if (!string.IsNullOrWhiteSpace(apiKey))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
            using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return ([], UiText.T(APIFailureExplainer.ModelListMessage((int)response.StatusCode)));
            var ids = ModelListParser.Ids(body);
            return (ids, ids.Count > 0
                ? UiText.Format("共 %lld 个模型", ids.Count)
                : UiText.T("服务没有返回模型列表，请手动填写模型 ID"));
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return ([], UiText.T(APIFailureExplainer.Message(APIFailure.Timeout)));
        }
        catch (HttpRequestException)
        {
            return ([], UiText.T(APIFailureExplainer.Message(APIFailure.CannotReachHost)));
        }
    }
    public async Task<string> AnswerAsync(AnswerOptions options, string system, string user, Action<string>? update = null, CancellationToken ct = default, int timeoutSeconds = 90, string? imagePath = null)
    {
        var uri = Endpoint(options.BaseUrl);
        if (string.IsNullOrWhiteSpace(options.Model)) throw new ArgumentException("请在设置中填写回答模型名称。");
        if (!uri.IsLoopback && string.IsNullOrWhiteSpace(options.ApiKey)) throw new ArgumentException("请在设置中填写 API Key。");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        // E13: a screenshot question carries the picture itself (Mac AnswerRequest.imageURL ->
        // ChatCompletionStreamParser.body imageJPEGBase64), not just its OCR text.
        JsonNode content = user;
        if (!string.IsNullOrEmpty(imagePath))
        {
            var bytes = await File.ReadAllBytesAsync(imagePath, deadline.Token).ConfigureAwait(false);
            var mime = Path.GetExtension(imagePath).ToLowerInvariant() is ".jpg" or ".jpeg" ? "image/jpeg" : "image/png";
            content = new JsonArray
            {
                new JsonObject { ["type"] = "text", ["text"] = user },
                new JsonObject
                {
                    ["type"] = "image_url",
                    ["image_url"] = new JsonObject { ["url"] = "data:" + mime + ";base64," + Convert.ToBase64String(bytes) }
                }
            };
        }
        var body = new JsonObject
        {
            ["model"] = options.Model.Trim(),
            ["stream"] = true,
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "system", ["content"] = system },
                new JsonObject { ["role"] = "user", ["content"] = content }
            }
        };
        if (options.ReasoningEffort.Length > 0) body["reasoning_effort"] = options.ReasoningEffort;
        if (options.MaxOutputTokens.HasValue) body["max_tokens"] = Math.Clamp(options.MaxOutputTokens.Value, 1, 32000);
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Content = new StringContent(body.ToJsonString(new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }), Encoding.UTF8, "application/json");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        if (!string.IsNullOrWhiteSpace(options.ApiKey)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey.Trim());
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode)
            {
                await using var errorStream = await response.Content.ReadAsStreamAsync(deadline.Token);
                var buffer = new byte[4096]; var count = await errorStream.ReadAsync(buffer, deadline.Token);
                var message = Encoding.UTF8.GetString(buffer, 0, count);
                if (options.ApiKey.Length > 0) message = message.Replace(options.ApiKey, "[key]");
                throw new HttpRequestException($"模型服务返回 HTTP {(int)response.StatusCode}：{message[..Math.Min(message.Length, 400)]}", null, response.StatusCode);
            }
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var text = new StringBuilder(); var data = new StringBuilder(); var done = false; string? finishReason = null;
            void Consume()
            {
                var payload = data.ToString().Trim(); data.Clear();
                if (payload.Length == 0) return;
                if (payload == "[DONE]") { done = true; return; }
                using var json = JsonDocument.Parse(payload); var root = json.RootElement;
                if (root.TryGetProperty("error", out var error))
                {
                    var message = error.ValueKind == JsonValueKind.String ? error.GetString() ?? "模型服务错误" : error.ToString();
                    if (options.ApiKey.Length > 0) message = message.Replace(options.ApiKey, "[key]");
                    throw new InvalidOperationException(message);
                }
                if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0) return;
                var choice = choices[0];
                if (choice.TryGetProperty("finish_reason", out var finish) && finish.ValueKind == JsonValueKind.String) finishReason = finish.GetString();
                if (choice.TryGetProperty("delta", out var delta) && delta.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                { text.Append(content.GetString()); update?.Invoke(text.ToString()); }
                else if (choice.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var whole) && whole.ValueKind == JsonValueKind.String)
                { text.Clear(); text.Append(whole.GetString()); update?.Invoke(text.ToString()); }
                if (text.Length > 1_000_000) throw new InvalidDataException("模型响应超过文本上限。");
            }
            while (!done && await reader.ReadLineAsync(deadline.Token) is { } line)
            {
                if (line.Length > 4_000_000) throw new InvalidDataException("模型事件过大。");
                if (line.Length == 0) Consume();
                else if (line.StartsWith("data:", StringComparison.Ordinal)) data.AppendLine(line[5..].TrimStart());
                else if (line.StartsWith('{')) { data.Append(line); Consume(); }
            }
            if (!done) Consume();
            if (finishReason == "length") throw new InvalidDataException("模型输出达到长度上限，本次内容尚未完整，请缩短输入或重试。");
            if (finishReason == "content_filter") throw new InvalidDataException("模型服务中止了本次输出，请调整输入后重试。");
            if (!done && finishReason is not "stop") throw new IOException("模型连接在完整响应前中断，本次内容未作为完整回答保存，请重试。");
            if (string.IsNullOrWhiteSpace(text.ToString())) throw new InvalidOperationException("模型未返回文本，请检查模型名称与接口能力。");
            return text.ToString();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new TimeoutException($"模型请求超过 {timeoutSeconds} 秒，已取消。"); }
    }
}
