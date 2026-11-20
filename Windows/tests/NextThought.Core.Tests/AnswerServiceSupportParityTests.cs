using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Port of AnswerServiceSupportTests.swift (16 Swift Testing tests): connecting an answer
/// service — default backend, providers, key input, failures and the sample clock. Inputs and
/// expected values are the ones from the Swift source.
/// </summary>
public sealed class AnswerServiceSupportParityTests
{
    // MARK: Default backend

    [Fact]
    public void FreshInstallWithoutCodexStartsWithAPI()
    {
        Assert.Equal("openAICompatible", AnswerBackendDefault.RawValue(null, false));
        Assert.Equal("codexAppServer", AnswerBackendDefault.RawValue(null, true));
    }

    [Fact]
    public void SavedChoiceIsNeverChanged()
    {
        foreach (var saved in new[] { "codexAppServer", "codexExec", "openAICompatible" })
        {
            Assert.Equal(saved, AnswerBackendDefault.RawValue(saved, false));
            Assert.Equal(saved, AnswerBackendDefault.RawValue(saved, true));
        }
        // An unknown stored value counts as "no choice".
        Assert.Equal("openAICompatible", AnswerBackendDefault.RawValue("retired", false));
        Assert.Equal("codexAppServer", AnswerBackendDefault.RawValue("", true));
    }

    // MARK: Providers

    [Fact]
    public void AddressesAreComparedWithoutCaseOrTrailingSlash()
    {
        Assert.Equal("https://api.deepseek.com/v1", APIProviders.Normalized("  HTTPS://API.DeepSeek.com/v1/ "));
        Assert.Equal("deepseek-chat", APIProviders.Guide("https://api.deepseek.com/v1/")!.SuggestedModel);
        Assert.Null(APIProviders.Guide("https://example.invalid/v1"));
    }

    [Fact]
    public void ChineseProvidersComeBeforeOpenAI()
    {
        var deepseek = APIProviders.OrderFor("https://api.deepseek.com/v1");
        var qwen = APIProviders.OrderFor("https://dashscope.aliyuncs.com/compatible-mode/v1");
        var openAI = APIProviders.OrderFor("https://api.openai.com/v1");
        var ollama = APIProviders.OrderFor("http://localhost:11434/v1");
        Assert.True(deepseek == 0 && qwen == 1);
        Assert.True(deepseek < openAI && qwen < openAI && openAI < ollama);
        Assert.Equal(int.MaxValue, APIProviders.OrderFor("https://example.invalid/v1"));
    }

    [Fact]
    public void OnlyKnownKeyPagesAreListed()
    {
        Assert.Equal("https://platform.openai.com/api-keys", APIProviders.Guide("https://api.openai.com/v1")!.KeyPageUrl);
        Assert.Equal("https://platform.deepseek.com/api_keys", APIProviders.Guide("https://api.deepseek.com/v1")!.KeyPageUrl);
        Assert.Equal("https://platform.moonshot.cn/console/api-keys", APIProviders.Guide("https://api.moonshot.cn/v1")!.KeyPageUrl);
        Assert.Equal("https://open.bigmodel.cn/usercenter/apikeys", APIProviders.Guide("https://open.bigmodel.cn/api/paas/v4")!.KeyPageUrl);
        Assert.Equal("https://openrouter.ai/keys", APIProviders.Guide("https://openrouter.ai/api/v1")!.KeyPageUrl);
        Assert.Equal("https://console.groq.com/keys", APIProviders.Guide("https://api.groq.com/openai/v1")!.KeyPageUrl);
        // Not certain: left out.
        Assert.Null(APIProviders.Guide("https://dashscope.aliyuncs.com/compatible-mode/v1")!.KeyPageUrl);
        Assert.Null(APIProviders.Guide("https://ark.cn-beijing.volces.com/api/v3")!.KeyPageUrl);
    }

    [Fact]
    public void LocalServicesNeedNoKey()
    {
        Assert.True(APIProviders.IsLocal("http://localhost:11434/v1"));
        Assert.True(APIProviders.IsLocal(" http://127.0.0.1:8080/v1 "));
        Assert.False(APIProviders.IsLocal("https://api.openai.com/v1"));
        Assert.False(APIProviders.IsLocal("not a url"));
        Assert.True(APIProviders.Guide("http://localhost:1234/v1")!.IsLocal);
    }

    // MARK: Provider switch

    /// <summary>
    /// Swift's plan returns <c>.select(PresetRef)</c>, so it can assert which saved preset wins.
    /// The C# <see cref="APIProviderSwitch"/> result carries no preset id, so the same five
    /// decisions are asserted through <see cref="APIProviderSwitchPlanner.Plan"/>.
    /// </summary>
    [Fact]
    public void PickingAProviderReusesAnEmptyPresetOrAddsOne()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        APIProviderPresetRef[] presets =
        [
            new(first, "https://api.openai.com/v1"),
            new(second, "https://api.deepseek.com/v1/")
        ];
        // Already saved: select it, even with a trailing slash.
        Assert.Equal(APIProviderSwitch.Select, APIProviderSwitchPlanner.Plan("https://api.deepseek.com/v1", presets, first, true));
        // The active one is already the target.
        Assert.Equal(APIProviderSwitch.Select, APIProviderSwitchPlanner.Plan("https://API.openai.com/v1", presets, first, true));
        // New provider: an untouched active preset is reused, a used one is kept.
        Assert.Equal(APIProviderSwitch.ReuseActive, APIProviderSwitchPlanner.Plan("https://api.moonshot.cn/v1", presets, first, true));
        Assert.Equal(APIProviderSwitch.AddNew, APIProviderSwitchPlanner.Plan("https://api.moonshot.cn/v1", presets, first, false));
        Assert.Equal(APIProviderSwitch.AddNew, APIProviderSwitchPlanner.Plan("https://api.moonshot.cn/v1", [], null, false));
    }

    // MARK: Key input

    [Fact]
    public void PastedKeysAreCleanedAndSentencesRejected()
    {
        Assert.Equal("sk-abc123def456", APIKeyInput.Clean("  sk-abc123def456\n"));
        Assert.Equal("sk-short", APIKeyInput.Clean("sk-short"));        // exactly 8 characters
        Assert.Null(APIKeyInput.Clean("sk-abc1"));                      // 7 characters
        Assert.Null(APIKeyInput.Clean(""));
        Assert.Null(APIKeyInput.Clean("my key is sk-abc123def456"));
        Assert.Null(APIKeyInput.Clean("sk-abc123\nsk-def456"));
        Assert.Null(APIKeyInput.Clean("这里是我的密钥abcdefgh"));
        Assert.Null(APIKeyInput.Clean(new string('a', 401)));
        Assert.NotNull(APIKeyInput.Clean(new string('a', 400)));
    }

    [Fact]
    public void MaskedKeyShowsOnlyTheEnds()
    {
        Assert.Equal("sk-" + new string('•', 8) + "1234", APIKeyInput.Masked("sk-abcdefgh1234"));
        Assert.Equal(new string('•', 8), APIKeyInput.Masked("short"));
        Assert.Equal("sk-" + new string('•', 8) + "1234", APIKeyInput.Masked("  sk-abcdefgh1234 "));
    }

    // MARK: Failures

    [Fact]
    public void HttpStatusesMapToPlainCauses()
    {
        Assert.Equal(APIFailure.BadKey, APIFailureExplainer.Classify(401));
        Assert.Equal(APIFailure.Forbidden, APIFailureExplainer.Classify(403));
        Assert.Equal(APIFailure.OutOfCredit, APIFailureExplainer.Classify(402));
        Assert.Equal(APIFailure.BadAddress, APIFailureExplainer.Classify(404));
        Assert.Equal(APIFailure.BadAddress, APIFailureExplainer.Classify(404, "Not Found"));
        Assert.Equal(APIFailure.RateLimited, APIFailureExplainer.Classify(429, "Rate limit reached for requests"));
        Assert.Equal(APIFailure.ServerBusy, APIFailureExplainer.Classify(500));
        Assert.Equal(APIFailure.ServerBusy, APIFailureExplainer.Classify(503));
        // Swift compares with .rejected(418); the C# APIFailure has no associated status code.
        Assert.Equal(APIFailure.Rejected, APIFailureExplainer.Classify(418));
        Assert.Equal(APIFailure.Unknown, APIFailureExplainer.Classify(null));
    }

    [Fact]
    public void BodiesSeparateAWrongModelFromAWrongAddress()
    {
        const string missing = """{"error":{"message":"The model `gpt-9` does not exist","code":"model_not_found"}}""";
        Assert.Equal(APIFailure.BadModel, APIFailureExplainer.Classify(404, missing));
        Assert.Equal(APIFailure.BadModel, APIFailureExplainer.Classify(400, "model xyz is not supported"));
        Assert.Equal(APIFailure.BadModel, APIFailureExplainer.Classify(404, """{"error":"model_not_found"}"""));
        Assert.Equal(APIFailure.Rejected, APIFailureExplainer.Classify(400, "max_tokens is too large"));
        Assert.Equal(APIFailure.BadKey, APIFailureExplainer.Classify(400, "Invalid API key provided"));
        Assert.Equal(APIFailure.OutOfCredit, APIFailureExplainer.Classify(429, "You exceeded your current quota"));
        Assert.Equal(APIFailure.OutOfCredit, APIFailureExplainer.Classify(429, "账户余额不足"));
    }

    [Fact]
    public void NetworkErrorsMapToPlainCauses()
    {
        Assert.Equal(APIFailure.Timeout, APIFailureExplainer.Classify(null, "", -1001));
        Assert.Equal(APIFailure.Offline, APIFailureExplainer.Classify(null, "", -1009));
        Assert.Equal(APIFailure.Offline, APIFailureExplainer.Classify(null, "", -1005));
        Assert.Equal(APIFailure.CannotReachHost, APIFailureExplainer.Classify(null, "", -1003));
        Assert.Equal(APIFailure.CannotReachHost, APIFailureExplainer.Classify(null, "", -1004));
        Assert.Equal(APIFailure.SecureConnection, APIFailureExplainer.Classify(null, "", -1200));
        Assert.Equal(APIFailure.SecureConnection, APIFailureExplainer.Classify(null, "", -1202));
        Assert.Equal(APIFailure.Unknown, APIFailureExplainer.Classify(null, "", -999));
        // A network error wins over a stale status.
        Assert.Equal(APIFailure.Timeout, APIFailureExplainer.Classify(200, "", -1001));
    }

    [Fact]
    public void EveryCauseHasItsOwnSentence()
    {
        APIFailure[] causes =
        [
            APIFailure.BadKey, APIFailure.Forbidden, APIFailure.BadAddress, APIFailure.CannotReachHost,
            APIFailure.BadModel, APIFailure.OutOfCredit, APIFailure.RateLimited, APIFailure.ServerBusy,
            APIFailure.Timeout, APIFailure.Offline, APIFailure.SecureConnection, APIFailure.Rejected, APIFailure.Unknown
        ];
        var sentences = causes.Select(APIFailureExplainer.Message).ToList();
        Assert.All(sentences, sentence => Assert.NotEmpty(sentence));
        Assert.Equal(causes.Length, sentences.Distinct().Count());
        Assert.Contains("Key", APIFailureExplainer.Message(APIFailure.BadKey));
        Assert.Contains("地址", APIFailureExplainer.Message(APIFailure.BadAddress));
        Assert.Contains("网络", APIFailureExplainer.Message(APIFailure.Timeout));
    }

    [Fact]
    public void ModelListFailuresDoNotBlameTheAddress()
    {
        Assert.Equal(APIFailureExplainer.Message(APIFailure.BadKey), APIFailureExplainer.ModelListMessage(401));
        Assert.Contains("没有提供模型列表", APIFailureExplainer.ModelListMessage(404));
        Assert.Contains("没有提供模型列表", APIFailureExplainer.ModelListMessage(405));
        Assert.DoesNotContain("地址不对", APIFailureExplainer.ModelListMessage(404));
    }

    // MARK: Sample clock

    [Fact]
    public void ScriptItemsAppearAtTheirTime()
    {
        var times = new[] { 0.6, 4.0, 9.0 };
        Assert.Equal(0, ScriptTimeline.VisibleCount(times, 0));
        Assert.Equal(1, ScriptTimeline.VisibleCount(times, 0.6));
        Assert.Equal(2, ScriptTimeline.VisibleCount(times, 8.99));
        Assert.Equal(3, ScriptTimeline.VisibleCount(times, 100));
        Assert.Equal(0, ScriptTimeline.VisibleCount([], 5));
        Assert.Equal(0, ScriptTimeline.VisibleCount(times, -3));
    }

    [Fact]
    public void ProgressIsClamped()
    {
        Assert.Equal(0.5, ScriptTimeline.Progress(15, 30));
        Assert.Equal(0, ScriptTimeline.Progress(-1, 30));
        Assert.Equal(1, ScriptTimeline.Progress(45, 30));
        Assert.Equal(1, ScriptTimeline.Progress(1, 0));
    }
}
