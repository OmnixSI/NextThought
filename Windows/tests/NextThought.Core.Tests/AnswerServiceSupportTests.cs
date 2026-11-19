using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for answer-service defaults, providers, key input and failure explanations.</summary>
public sealed class AnswerServiceSupportTests
{
    [Fact]
    public void DefaultBackendKeepsSavedChoicesAndPrefersCodexWhenInstalled()
    {
        Assert.Equal(AnswerBackendDefault.CodexExec, AnswerBackendDefault.RawValue("codexExec", codexInstalled: false));
        Assert.Equal(AnswerBackendDefault.CodexAppServer, AnswerBackendDefault.RawValue(null, codexInstalled: true));
        Assert.Equal(AnswerBackendDefault.OpenAICompatible, AnswerBackendDefault.RawValue(null, codexInstalled: false));
        Assert.Equal(AnswerBackendDefault.CodexAppServer, AnswerBackendDefault.RawValue("nonsense", codexInstalled: true));
    }

    [Fact]
    public void ProvidersNormaliseAndRecogniseAddresses()
    {
        Assert.Equal("https://api.deepseek.com/v1", APIProviders.Normalized(" HTTPS://API.Deepseek.com/v1/ "));
        Assert.Equal("deepseek-chat", APIProviders.Guide("https://api.deepseek.com/v1/")!.SuggestedModel);
        Assert.Equal(0, APIProviders.OrderFor("https://api.deepseek.com/v1"));
        Assert.Equal(int.MaxValue, APIProviders.OrderFor("https://example.com/v1"));
        Assert.True(APIProviders.IsLocal("http://localhost:11434/v1"));
        Assert.True(APIProviders.IsLocal("http://127.0.0.1:1234/v1"));
        Assert.False(APIProviders.IsLocal("https://api.openai.com/v1"));
        Assert.Null(APIProviders.Guide("https://example.com/v1"));
    }

    [Fact]
    public void SwitchingProvidersSelectsOrReusesPresets()
    {
        var active = Guid.NewGuid();
        var other = Guid.NewGuid();
        var presets = new[]
        {
            new APIProviderPresetRef(active, "https://api.deepseek.com/v1"),
            new APIProviderPresetRef(other, "https://api.openai.com/v1")
        };
        Assert.Equal(APIProviderSwitch.Select, APIProviderSwitchPlanner.Plan("https://api.deepseek.com/v1/", presets, active, false));
        Assert.Equal(APIProviderSwitch.Select, APIProviderSwitchPlanner.Plan("https://api.openai.com/v1", presets, active, false));
        Assert.Equal(APIProviderSwitch.ReuseActive, APIProviderSwitchPlanner.Plan("https://api.groq.com/openai/v1", presets, active, true));
        Assert.Equal(APIProviderSwitch.AddNew, APIProviderSwitchPlanner.Plan("https://api.groq.com/openai/v1", presets, active, false));
    }

    [Fact]
    public void KeysAreCleanedAndMasked()
    {
        Assert.Equal("sk-abcdef123456", APIKeyInput.Clean("  sk-abcdef123456  "));
        Assert.Null(APIKeyInput.Clean("short"));
        Assert.Null(APIKeyInput.Clean("has space in key"));
        Assert.Null(APIKeyInput.Clean("密钥不是ASCII字符密钥"));
        Assert.Null(APIKeyInput.Clean(new string('a', 401)));
        Assert.Equal("sk-••••••••3456", APIKeyInput.Masked("sk-abcdef123456"));
        Assert.Equal("••••••••", APIKeyInput.Masked("short"));
    }

    [Theory]
    [InlineData(401, "", APIFailure.BadKey)]
    [InlineData(403, "", APIFailure.Forbidden)]
    [InlineData(402, "", APIFailure.OutOfCredit)]
    [InlineData(404, "", APIFailure.BadAddress)]
    [InlineData(404, "model not found", APIFailure.BadModel)]
    [InlineData(400, "invalid api key", APIFailure.BadKey)]
    [InlineData(400, "model unsupported", APIFailure.BadModel)]
    [InlineData(429, "", APIFailure.RateLimited)]
    [InlineData(429, "insufficient quota", APIFailure.OutOfCredit)]
    [InlineData(503, "", APIFailure.ServerBusy)]
    [InlineData(418, "", APIFailure.Rejected)]
    public void HttpStatusesMapToCauses(int status, string body, APIFailure expected)
    {
        Assert.Equal(expected, APIFailureExplainer.Classify(status, body));
    }

    [Theory]
    [InlineData(-1001, APIFailure.Timeout)]
    [InlineData(-1003, APIFailure.CannotReachHost)]
    [InlineData(-1004, APIFailure.CannotReachHost)]
    [InlineData(-1005, APIFailure.Offline)]
    [InlineData(-1009, APIFailure.Offline)]
    [InlineData(-1202, APIFailure.SecureConnection)]
    [InlineData(-1, APIFailure.Unknown)]
    public void UrlErrorsMapToCauses(int code, APIFailure expected)
    {
        Assert.Equal(expected, APIFailureExplainer.Classify(null, "", code));
    }

    [Fact]
    public void MessagesAreActionable()
    {
        Assert.Contains("Key", APIFailureExplainer.Message(APIFailure.BadKey));
        Assert.Contains("地址", APIFailureExplainer.Message(APIFailure.BadAddress));
        Assert.Contains("模型", APIFailureExplainer.Message(APIFailure.BadModel));
        Assert.Equal("服务商拒绝了这次请求。", APIFailureExplainer.Message(APIFailure.Rejected));
        Assert.Equal("检查没有通过。", APIFailureExplainer.Message(APIFailure.Unknown));
        Assert.Contains("模型列表", APIFailureExplainer.ModelListMessage(404));
        Assert.Equal(APIFailureExplainer.Message(APIFailure.BadKey), APIFailureExplainer.ModelListMessage(401));
    }

    [Fact]
    public void ScriptTimelineCountsVisibleItemsAndProgress()
    {
        var times = new[] { 0.0, 2.5, 5.0 };
        Assert.Equal(0, ScriptTimeline.VisibleCount(times, -1));
        Assert.Equal(2, ScriptTimeline.VisibleCount(times, 2.5));
        Assert.Equal(3, ScriptTimeline.VisibleCount(times, 10));
        Assert.Equal(0.5, ScriptTimeline.Progress(5, 10));
        Assert.Equal(1, ScriptTimeline.Progress(20, 10));
        Assert.Equal(0, ScriptTimeline.Progress(-1, 10));
        Assert.Equal(1, ScriptTimeline.Progress(5, 0));
    }
}
