using System.Text;
using System.Text.Json.Nodes;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the Codex app-server protocol, stream parser and model list.</summary>
public sealed class AnswerStreamingTests
{
    private static string TextOf(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    private static JsonObject Json(string text) => JsonNode.Parse(text)!.AsObject();

    [Fact]
    public void RequestsAndNotificationsAreNewlineDelimitedJson()
    {
        var request = TextOf(CodexAppServerProtocol.Request(3, "thread/start", new JsonObject { ["model"] = "m" }));
        Assert.EndsWith("\n", request);
        var parsed = Json(request);
        Assert.Equal(3, parsed["id"]!.GetValue<int>());
        Assert.Equal("thread/start", parsed["method"]!.GetValue<string>());
        Assert.Equal("m", parsed["params"]!["model"]!.GetValue<string>());

        var notification = Json(TextOf(CodexAppServerProtocol.Notification("initialized")));
        Assert.Null(notification["params"]);
        var withParams = Json(TextOf(CodexAppServerProtocol.Notification("x", new JsonObject { ["a"] = 1 })));
        Assert.Equal(1, withParams["params"]!["a"]!.GetValue<int>());
    }

    [Fact]
    public void UnsupportedServerRequestsGetADeclineOrAnError()
    {
        Assert.Equal("decline", Json(TextOf(CodexAppServerProtocol.Reply(7, "item/commandExecution/requestApproval")))["result"]!["decision"]!.GetValue<string>());
        Assert.Equal("denied", Json(TextOf(CodexAppServerProtocol.Reply(7, "applyPatchApproval")))["result"]!["decision"]!.GetValue<string>());
        Assert.Equal("decline", Json(TextOf(CodexAppServerProtocol.Reply(7, "mcpServer/elicitation/request")))["result"]!["action"]!.GetValue<string>());
        var unknown = Json(TextOf(CodexAppServerProtocol.Reply(7, "unknown/method")));
        Assert.Equal(-32601, unknown["error"]!["code"]!.GetValue<int>());
        Assert.Equal(7, unknown["id"]!.GetValue<int>());
    }

    [Fact]
    public void ThreadAndTurnParamsCarryTheOptions()
    {
        var thread = CodexAppServerProtocol.ThreadStartParams("gpt", "high", "C:\\work", fast: true, webSearch: true);
        Assert.Equal("fast", thread["serviceTier"]!.GetValue<string>());
        Assert.Equal("never", thread["approvalPolicy"]!.GetValue<string>());
        Assert.Equal("live", thread["config"]!["web_search"]!.GetValue<string>());
        Assert.False(thread["config"]!["features.shell_tool"]!.GetValue<bool>());
        Assert.Null(CodexAppServerProtocol.ThreadStartParams("gpt", "low", "c")["serviceTier"]);

        var turn = CodexAppServerProtocol.TurnStartParams("t", "hello", "img.png", "high");
        Assert.Equal(2, turn["input"]!.AsArray().Count);
        Assert.Equal("localImage", turn["input"]!.AsArray()[1]!["type"]!.GetValue<string>());
        Assert.Single(CodexAppServerProtocol.TurnStartParams("t", "hi", null, "low")["input"]!.AsArray());
    }

    [Fact]
    public void ParseDistinguishesResponsesNotificationsAndServerRequests()
    {
        var response = CodexAppServerProtocol.Parse("""{"id":2,"result":{"thread":{"id":"t1"}}}""");
        Assert.Equal(CodexInboundKind.Response, response!.Kind);
        Assert.Equal(2, response.ResponseId);
        Assert.Equal("t1", CodexAppServerProtocol.ThreadIdFromThreadStart(response.Result!));

        var error = CodexAppServerProtocol.Parse("""{"id":3,"error":{"message":"bad"}}""");
        Assert.Equal("bad", error!.Error);

        var notification = CodexAppServerProtocol.Parse("""{"method":"turn/completed","params":{}}""");
        Assert.Equal(CodexInboundKind.Notification, notification!.Kind);
        Assert.Equal("turn/completed", notification.Method);

        var request = CodexAppServerProtocol.Parse("""{"id":9,"method":"execCommandApproval","params":{}}""");
        Assert.Equal(CodexInboundKind.ServerRequest, request!.Kind);
        Assert.Equal(9, request.ServerRequestId!.GetValue<int>());

        Assert.Null(CodexAppServerProtocol.Parse("not json"));
        Assert.Null(CodexAppServerProtocol.Parse("""{"no":"id"}"""));
    }

    [Fact]
    public void LoginAndIdsAreExtracted()
    {
        Assert.True(CodexAppServerProtocol.IsLoggedIn(Json("""{"account":{"plan":"pro"}}""")));
        Assert.True(CodexAppServerProtocol.IsLoggedIn(Json("""{"requiresOpenaiAuth":false}""")));
        Assert.False(CodexAppServerProtocol.IsLoggedIn(Json("""{"requiresOpenaiAuth":true}""")));
        Assert.Equal("turn1", CodexAppServerProtocol.TurnIdFromTurnStart(Json("""{"turn":{"id":"turn1"}}""")));
        Assert.Equal("t2", CodexAppServerProtocol.ThreadIdFromThreadStart(Json("""{"threadId":"t2"}""")));
    }

    [Fact]
    public void TurnEventsMapEveryNotificationKind()
    {
        var delta = CodexAppServerProtocol.TurnEvent("item/agentMessage/delta", Json("""{"delta":"Hi","itemId":"i1"}"""), null);
        Assert.Equal(new CodexDelta("i1", "Hi"), delta);

        var completed = CodexAppServerProtocol.TurnEvent("item/completed",
            Json("""{"item":{"type":"agentMessage","id":"i1","text":"done","phase":"final_answer"}}"""), null);
        Assert.Equal(new CodexAgentMessageCompleted("i1", "done", "final_answer"), completed);

        var search = CodexAppServerProtocol.TurnEvent("item/started",
            Json("""{"item":{"type":"webSearch","id":"w1","action":{"queries":["a","b"]}}}"""), null);
        Assert.Equal(new CodexWebSearch("w1", false, 2), search);

        Assert.Equal(new CodexRetrying("retry"), CodexAppServerProtocol.TurnEvent("error",
            Json("""{"error":{"message":"retry"},"willRetry":true}"""), null));
        Assert.Equal(new CodexCompleted("failed", "boom"), CodexAppServerProtocol.TurnEvent("error",
            Json("""{"error":{"message":"boom"}}"""), null));
        Assert.Equal(new CodexCompleted("completed", null), CodexAppServerProtocol.TurnEvent("turn/completed",
            Json("""{"turn":{"status":"completed"}}"""), null));

        Assert.Null(CodexAppServerProtocol.TurnEvent("item/agentMessage/delta",
            Json("""{"delta":"x","turnId":"other"}"""), "mine"));
        Assert.Null(CodexAppServerProtocol.TurnEvent("unknown", Json("{}"), null));
    }

    [Fact]
    public void AccumulatorPrefersTheFinalAnswerItem()
    {
        var accumulator = new AgentMessageAccumulator();
        accumulator.Apply(new CodexDelta("i1", "Hel"));
        accumulator.Apply(new CodexDelta("i1", "lo"));
        Assert.Equal("Hello", accumulator.Text);
        accumulator.Apply(new CodexAgentMessageCompleted("i2", "final", "final_answer"));
        Assert.Equal("final", accumulator.Text);
        accumulator.Apply(new CodexAgentMessageCompleted("i3", "other", null));
        Assert.Equal("final", accumulator.Text);
    }

    [Fact]
    public void StreamParserReadsDeltasDoneAndErrors()
    {
        var parser = new ChatCompletionStreamParser();
        Assert.Equal(new ChatDelta("Hi"), parser.Parse("""data: {"choices":[{"delta":{"content":"Hi"}}]}"""));
        Assert.Equal(new ChatDelta("full"), parser.Parse("""data: {"choices":[{"message":{"content":"full"}}]}"""));
        Assert.Equal(new ChatDone(), parser.Parse("data: [DONE]"));
        Assert.Equal(new ChatFailure("boom"), parser.Parse("""{"error":{"message":"boom"}}"""));
        Assert.Equal(new ChatFailure("nope"), parser.Parse("""data: {"error":"nope"}"""));
        Assert.Null(parser.Parse("data: {\"choices\":[]}"));
        Assert.Null(parser.Parse(": keepalive"));
        Assert.Null(parser.Parse("data: not json"));
    }

    [Fact]
    public void RequestBodySendsImageAndReasoningEffort()
    {
        var plain = Json(TextOf(ChatCompletionStreamParser.Body("m", "sys", "user", null, 0.2)));
        Assert.Equal(0.2, plain["temperature"]!.GetValue<double>(), 5);
        Assert.Null(plain["reasoning_effort"]);

        var reasoning = Json(TextOf(ChatCompletionStreamParser.Body("m", "sys", "user", null, 0.2, "high")));
        Assert.Equal("high", reasoning["reasoning_effort"]!.GetValue<string>());
        Assert.Null(reasoning["temperature"]);

        var image = Json(TextOf(ChatCompletionStreamParser.Body("m", "sys", "user", "QUJD")));
        var content = image["messages"]![1]!["content"]!.AsArray();
        Assert.Equal("image_url", content[1]!["type"]!.GetValue<string>());
        Assert.Equal("data:image/jpeg;base64,QUJD", content[1]!["image_url"]!["url"]!.GetValue<string>());
    }

    [Fact]
    public void EndpointCompletesThePathAndRejectsRemotePlainHttp()
    {
        Assert.Equal("https://api.example.com/v1/chat/completions", ChatCompletionStreamParser.Endpoint("https://api.example.com/v1"));
        Assert.Equal("https://api.example.com/v1/chat/completions", ChatCompletionStreamParser.Endpoint("https://api.example.com/v1/"));
        Assert.Equal("https://api.example.com/v1/chat/completions", ChatCompletionStreamParser.Endpoint("https://api.example.com/v1/chat/completions"));
        Assert.StartsWith("http://localhost:11434/", ChatCompletionStreamParser.Endpoint("http://localhost:11434/v1"));
        Assert.Null(ChatCompletionStreamParser.Endpoint("http://api.example.com/v1"));
        Assert.Null(ChatCompletionStreamParser.Endpoint(""));
        Assert.Null(ChatCompletionStreamParser.Endpoint("ftp://example.com"));
    }

    [Fact]
    public void ModelListParsesBothShapesAndSorts()
    {
        var ids = ModelListParser.Ids("""{"data":[{"id":"b"},{"id":"a"},{"id":"a"}]}""");
        Assert.Equal(["a", "b"], ids);
        var named = ModelListParser.Ids("""{"models":[{"name":"z"},{"id":"y"}]}""");
        Assert.Equal(["y", "z"], named);
        Assert.Empty(ModelListParser.Ids("not json"));
        Assert.Empty(ModelListParser.Ids("""{"data":[]}"""));
    }
}
