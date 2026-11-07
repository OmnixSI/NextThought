using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using NextThought.Core;

namespace NextThought.Platform;

public sealed class ChatCompletionClient(HttpClient? http = null)
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
    public async Task<string> AnswerAsync(AnswerOptions options, string system, string user, Action<string>? update = null, CancellationToken ct = default, int timeoutSeconds = 90)
    {
        var uri = Endpoint(options.BaseUrl);
        if (string.IsNullOrWhiteSpace(options.Model)) throw new ArgumentException("请在设置中填写回答模型名称。");
        if (!uri.IsLoopback && string.IsNullOrWhiteSpace(options.ApiKey)) throw new ArgumentException("请在设置中填写 API Key。");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        var body = new Dictionary<string, object> { ["model"] = options.Model.Trim(), ["stream"] = true,
            ["messages"] = new[] { new { role = "system", content = system }, new { role = "user", content = user } } };
        if (options.ReasoningEffort.Length > 0) body["reasoning_effort"] = options.ReasoningEffort;
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
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
            var text = new StringBuilder(); var data = new StringBuilder(); var done = false;
            void Consume()
            {
                var payload = data.ToString().Trim(); data.Clear();
                if (payload.Length == 0) return;
                if (payload == "[DONE]") { done = true; return; }
                using var json = JsonDocument.Parse(payload); var root = json.RootElement;
                if (root.TryGetProperty("error", out var error)) throw new InvalidOperationException(error.ValueKind == JsonValueKind.String ? error.GetString() : error.ToString());
                if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0) return;
                var choice = choices[0];
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
            if (text.Length == 0) throw new InvalidOperationException("模型未返回文本，请检查模型名称与接口能力。");
            return text.ToString();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new TimeoutException($"模型请求超过 {timeoutSeconds} 秒，已取消。"); }
    }
}
