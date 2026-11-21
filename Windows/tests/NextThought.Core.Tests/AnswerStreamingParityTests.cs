using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Port of AnswerStreamingTests.swift (12 Swift Testing tests): Codex app-server wire format,
/// model-list and SSE parsing, secrets and the prompt/refinement contract.
/// </summary>
public sealed class AnswerStreamingParityTests
{
    [Fact]
    public void AppServerRequestsAreSingleLineWithoutJSONRPCField()
    {
        var data = CodexAppServerProtocol.Request(3, "turn/start",
            CodexAppServerProtocol.TurnStartParams("T", "hi\nthere", "/tmp/a.jpg", "low"));
        var text = Encoding.UTF8.GetString(data);
        Assert.EndsWith("\n", text);
        Assert.DoesNotContain("\n", text[..^1]);
        Assert.DoesNotContain("jsonrpc", text);
        var json = JsonNode.Parse(text)!.AsObject();
        var parameters = json["params"]!.AsObject();
        var input = parameters["input"]!.AsArray();
        Assert.Equal(2, input.Count);
        Assert.Equal("localImage", input[1]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void ThreadStartIsReadOnlyEphemeralAndNeverAsks()
    {
        var parameters = CodexAppServerProtocol.ThreadStartParams("m", "low", "/tmp/x");
        Assert.Equal("read-only", parameters["sandbox"]!.GetValue<string>());
        Assert.Equal("never", parameters["approvalPolicy"]!.GetValue<string>());
        Assert.True(parameters["ephemeral"]!.GetValue<bool>());
        Assert.False(parameters["config"]!["features.shell_tool"]!.GetValue<bool>());
    }

    /// <summary>
    /// Fast mode: "fast" on both requests; standard speed is an explicit JSON null so a model
    /// whose default tier is Fast also runs at standard speed.
    /// </summary>
    [Fact]
    public void ServiceTierIsFastOrExplicitNull()
    {
        var fast = CodexAppServerProtocol.Request(1, "thread/start",
            CodexAppServerProtocol.ThreadStartParams("m", "low", "/tmp/x", fast: true));
        var fastText = Encoding.UTF8.GetString(fast);
        Assert.Contains("\"serviceTier\":\"fast\"", fastText);
        var standard = CodexAppServerProtocol.Request(2, "turn/start",
            CodexAppServerProtocol.TurnStartParams("T", "q", null, "low"));
        var standardText = Encoding.UTF8.GetString(standard);
        Assert.Contains("\"serviceTier\":null", standardText);
        var turn = CodexAppServerProtocol.TurnStartParams("T", "q", null, "low", fast: true);
        Assert.Equal("fast", turn["serviceTier"]!.GetValue<string>());
    }

    [Fact]
    public void ReasoningEffortReplacesTemperature()
    {
        var withEffort = JsonNode.Parse(Encoding.UTF8.GetString(
            ChatCompletionStreamParser.Body("m", "s", "u", null, reasoningEffort: "low")))!.AsObject();
        Assert.Equal("low", withEffort["reasoning_effort"]!.GetValue<string>());
        Assert.Null(withEffort["temperature"]);
        var plain = JsonNode.Parse(Encoding.UTF8.GetString(
            ChatCompletionStreamParser.Body("m", "s", "u", null)))!.AsObject();
        Assert.Null(plain["reasoning_effort"]);
        Assert.Equal(0.3, plain["temperature"]!.GetValue<double>());
    }

    [Fact]
    public void ParsesModelLists()
    {
        const string openAI = """{"object":"list","data":[{"id":"gpt-5.5"},{"id":"deepseek-chat"},{"id":"gpt-5.5"}]}""";
        Assert.Equal(["deepseek-chat", "gpt-5.5"], ModelListParser.Ids(openAI));
        const string local = """{"models":[{"name":"qwen3:8b"}]}""";
        Assert.Equal(["qwen3:8b"], ModelListParser.Ids(local));
        Assert.Empty(ModelListParser.Ids("oops"));
    }

    [Fact]
    public void PresetKeysAreSecrets()
    {
        Assert.True(SettingsSnapshot.IsSecretAccount("answer.api.4F1C9A2E-0000-0000-0000-000000000000"));
        Assert.True(SettingsSnapshot.IsSecretAccount("github.token"));
        Assert.False(SettingsSnapshot.IsSecretAccount("selectedTargetID"));
        Assert.Contains("apiPresets", SettingsSnapshot.ExportedKeys);
    }

    [Fact]
    public void ClassifiesInboundMessages()
    {
        var response = CodexAppServerProtocol.Parse("""{"id":2,"result":{"thread":{"id":"T1"}}}""");
        Assert.NotNull(response);
        Assert.Equal(CodexInboundKind.Response, response!.Kind);
        Assert.Equal(2, response.ResponseId);
        Assert.Null(response.Error);
        Assert.Equal("T1", CodexAppServerProtocol.ThreadIdFromThreadStart(response.Result!));

        var serverRequest = CodexAppServerProtocol.Parse(
            """{"id":9,"method":"item/commandExecution/requestApproval","params":{}}""");
        Assert.NotNull(serverRequest);
        Assert.Equal(CodexInboundKind.ServerRequest, serverRequest!.Kind);
        Assert.Equal("item/commandExecution/requestApproval", serverRequest.Method);
        var reply = Encoding.UTF8.GetString(CodexAppServerProtocol.Reply(9, serverRequest.Method!));
        Assert.Contains("decline", reply);

        var failure = CodexAppServerProtocol.Parse("""{"id":4,"error":{"code":-32600,"message":"Not initialized"}}""");
        Assert.NotNull(failure);
        Assert.Equal(CodexInboundKind.Response, failure!.Kind);
        Assert.Equal("Not initialized", failure.Error);
    }

    [Fact]
    public void AccumulatesDeltasAndPrefersFinalAnswer()
    {
        var accumulator = new AgentMessageAccumulator();
        (string Method, JsonObject Params)[] events =
        [
            ("item/agentMessage/delta", new JsonObject { ["turnId"] = "U", ["itemId"] = "c", ["delta"] = "thinking" }),
            ("item/agentMessage/delta", new JsonObject { ["turnId"] = "U", ["itemId"] = "m", ["delta"] = "Hel" }),
            ("item/agentMessage/delta", new JsonObject { ["turnId"] = "U", ["itemId"] = "m", ["delta"] = "lo" }),
            ("item/agentMessage/delta", new JsonObject { ["turnId"] = "OTHER", ["itemId"] = "m", ["delta"] = "XX" })
        ];
        foreach (var (method, parameters) in events)
            if (CodexAppServerProtocol.TurnEvent(method, parameters, "U") is { } turnEvent) accumulator.Apply(turnEvent);
        Assert.Equal("Hello", accumulator.Text);

        var completed = CodexAppServerProtocol.TurnEvent("item/completed", new JsonObject
        {
            ["turnId"] = "U",
            ["item"] = new JsonObject
            {
                ["type"] = "agentMessage", ["id"] = "c", ["text"] = "Final", ["phase"] = "final_answer"
            }
        }, "U");
        accumulator.Apply(completed!);
        Assert.Equal("Final", accumulator.Text);
        Assert.Equal<CodexTurnEvent?>(new CodexCompleted("interrupted", null), CodexAppServerProtocol.TurnEvent(
            "turn/completed", new JsonObject { ["turn"] = new JsonObject { ["id"] = "U", ["status"] = "interrupted" } }, "U"));
        Assert.Equal<CodexTurnEvent?>(new CodexRetrying("busy"), CodexAppServerProtocol.TurnEvent(
            "error", new JsonObject { ["error"] = new JsonObject { ["message"] = "busy" }, ["willRetry"] = true }, "U"));
    }

    [Fact]
    public void LoginDetection()
    {
        Assert.True(CodexAppServerProtocol.IsLoggedIn(new JsonObject
        {
            ["account"] = new JsonObject { ["type"] = "chatgpt" },
            ["requiresOpenaiAuth"] = true
        }));
        Assert.False(CodexAppServerProtocol.IsLoggedIn(new JsonObject
        {
            ["account"] = null,
            ["requiresOpenaiAuth"] = true
        }));
    }

    [Fact]
    public void ParsesChatCompletionSSE()
    {
        var parser = new ChatCompletionStreamParser();
        Assert.Equal("你好", Assert.IsType<ChatDelta>(
            parser.Parse("""data: {"choices":[{"delta":{"content":"你好"}}]}""")).Text);
        Assert.IsType<ChatDone>(parser.Parse("data: [DONE]"));
        Assert.Null(parser.Parse(": keep-alive"));
        Assert.Null(parser.Parse("""data: {"choices":[{"delta":{"role":"assistant"}}]}"""));
        Assert.Equal("bad key", Assert.IsType<ChatFailure>(
            parser.Parse("""{"error":{"message":"bad key"}}""")).Message);
    }

    [Fact]
    public void EndpointRulesRefuseCleartextRemoteHosts()
    {
        Assert.Equal("https://api.deepseek.com/v1/chat/completions", ChatCompletionStreamParser.Endpoint("https://api.deepseek.com/v1"));
        Assert.Equal("http://localhost:11434/v1/chat/completions", ChatCompletionStreamParser.Endpoint("http://localhost:11434/v1/"));
        Assert.Null(ChatCompletionStreamParser.Endpoint("http://example.com/v1"));
        Assert.Null(ChatCompletionStreamParser.Endpoint(""));
    }

    [Fact]
    public void PromptPartsMatchSinglePromptAndRefinementsKeepQuestion()
    {
        var (system, user) = AnswerPromptBuilder.MakeParts("q", "c", "", [], false);
        Assert.Equal(AnswerPromptBuilder.Instructions(), system);
        Assert.Equal(AnswerPromptBuilder.Make("q", "c", "", [], false), system + "\n\n" + user);
        Assert.Contains("什么是索引", AnswerRefinement.Shorter.QuestionFor("什么是索引"));
    }
}

/// <summary>Translated from the OfficeImportTests struct in AnswerStreamingTests.swift.</summary>
public sealed class OfficeImportParityTests
{
    [Fact]
    public void ExtractsSlideParagraphs()
    {
        const string xml = """<p:sld><a:p><a:r><a:t>Redis &amp; 缓存</a:t></a:r><a:r><a:t> 设计</a:t></a:r></a:p><a:p><a:r><a:t>第二段</a:t></a:r></a:p><a:p></a:p></p:sld>""";
        Assert.Equal("Redis & 缓存 设计\n第二段", OfficeXMLText.Extract(xml));
    }

    [Fact]
    public void ExtractsWordParagraphsWithAttributes()
    {
        const string xml = """<w:body><w:p><w:r><w:t xml:space="preserve">Hello </w:t></w:r><w:r><w:t>world</w:t></w:r></w:p></w:body>""";
        Assert.Equal("Hello world", OfficeXMLText.Extract(xml));
    }

    [Fact]
    public void OrdersSlidesNumerically()
    {
        Assert.Equal(["ppt/slides/slide1.xml", "ppt/slides/slide2.xml", "ppt/slides/slide10.xml"],
            OfficeXMLText.NumericOrder(["ppt/slides/slide10.xml", "ppt/slides/slide2.xml", "ppt/slides/slide1.xml"]));
    }

    [Fact]
    public void PresetRoundTrips()
    {
        var preset = new SessionPreset
        {
            Name = "后端面试",
            Mode = SessionMode.Interview,
            Style = AnswerStyle.Concise,
            Preferences = SessionPreset.TemplatePreferences(SessionMode.Interview),
            Profile = "p",
            Documents = [new ContextDocument { Name = "cv.md", Pages = ["Swift"] }]
        };
        var decoded = JsonSerializer.Deserialize<SessionPreset>(JsonSerializer.Serialize(preset))!;
        Assert.Equal(preset.Id, decoded.Id);
        Assert.Equal(preset.Name, decoded.Name);
        Assert.Equal(SessionMode.Interview, decoded.Mode);
        Assert.Equal(AnswerStyle.Concise, decoded.Style);
        Assert.Equal(preset.Preferences, decoded.Preferences);
        Assert.Equal(preset.Profile, decoded.Profile);
        Assert.Equal(preset.UpdatedAt, decoded.UpdatedAt);
        var document = Assert.Single(decoded.Documents);
        Assert.Equal(preset.Documents[0].Id, document.Id);
        Assert.Equal("cv.md", document.Name);
        Assert.Equal(["Swift"], document.Pages);
        Assert.True(document.Enabled);
        Assert.Null(document.Category);
        Assert.Null(document.SourceFormat);
        Assert.Null(document.Content);
        Assert.Equal(preset.Documents[0].CharacterCount, document.CharacterCount);
    }
}

/// <summary>Translated from the RefinementTests struct in AnswerStreamingTests.swift.</summary>
public sealed class RefinementParityTests
{
    [Fact]
    public void ChainedRefinementsDoNotNest()
    {
        var once = AnswerRefinement.Shorter.QuestionFor("什么是索引");
        var twice = AnswerRefinement.English.QuestionFor(once);
        Assert.Equal(2, twice.Split("原问题：").Length);
        Assert.EndsWith("什么是索引", twice);
        Assert.Equal("什么是索引", AnswerRefinements.OriginalQuestion(twice));
    }
}
