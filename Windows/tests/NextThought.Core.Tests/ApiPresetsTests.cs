using System.Text.Json;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// E27: the saved OpenAI-compatible providers (port of APIPresets.swift). The rules are pure so
/// they are exercised without the settings page or the network.
/// </summary>
public sealed class ApiPresetsTests
{
    [Fact]
    public void FirstRunTurnsTheCurrentConfigurationIntoAPreset()
    {
        var settings = APIPresetLogic.EnsureLoaded(new AppSettings
        {
            BaseUrl = "https://api.deepseek.com/v1",
            Model = "deepseek-chat"
        });
        var preset = Assert.Single(settings.ApiPresets);
        Assert.Equal("DeepSeek", preset.Name);
        Assert.Equal("https://api.deepseek.com/v1", preset.BaseUrl);
        Assert.Equal("deepseek-chat", preset.Model);
        Assert.Equal("default", preset.Effort);
        Assert.Equal("answer.api", preset.KeyAccount);
        Assert.Equal(preset.Id, settings.ActiveApiPresetId);
    }

    [Fact]
    public void UnknownProviderFallsBackToTheDefaultName()
    {
        var settings = APIPresetLogic.EnsureLoaded(new AppSettings { BaseUrl = "https://example.test/v1" });
        Assert.Equal("默认", Assert.Single(settings.ApiPresets).Name);
        Assert.Equal("默认", APIPresetLogic.PresetNameFor("not a url"));
    }

    [Fact]
    public void LoadingKeepsExistingPresetsAndRepairsTheActiveId()
    {
        var first = new APIPreset(APIPreset.NewId(), "A", "https://a.test/v1");
        var second = new APIPreset(APIPreset.NewId(), "B", "https://b.test/v1");
        var settings = APIPresetLogic.EnsureLoaded(new AppSettings
        {
            ApiPresets = [first, second],
            ActiveApiPresetId = ""
        });
        Assert.Equal(first.Id, settings.ActiveApiPresetId);
        var pinned = APIPresetLogic.EnsureLoaded(settings with { ActiveApiPresetId = second.Id });
        Assert.Equal(second.Id, pinned.ActiveApiPresetId);
        var unknown = APIPresetLogic.EnsureLoaded(settings with { ActiveApiPresetId = "not-a-preset" });
        Assert.Equal(first.Id, unknown.ActiveApiPresetId);
    }

    [Fact]
    public void AddingAPresetUsesAUniqueNameAndActivatesIt()
    {
        var settings = APIPresetLogic.EnsureLoaded(new AppSettings { BaseUrl = "https://api.openai.com/v1" });
        var (withSecond, second) = APIPresetLogic.Add(settings, "OpenAI", "https://api.groq.com/openai/v1");
        Assert.Equal("OpenAI 2", second.Name);
        Assert.Equal(second.Id, withSecond.ActiveApiPresetId);
        Assert.Equal("https://api.groq.com/openai/v1", withSecond.BaseUrl);
        var (withThird, third) = APIPresetLogic.Add(withSecond, "OpenAI", "https://localhost:1234/v1");
        Assert.Equal("OpenAI 3", third.Name);
        Assert.Equal(3, withThird.ApiPresets.Count);
    }

    [Fact]
    public void RenameTrimsAndIgnoresEmptyOrUnknown()
    {
        var settings = APIPresetLogic.EnsureLoaded(new AppSettings());
        var id = settings.ApiPresets[0].Id;
        var renamed = APIPresetLogic.Rename(settings, id, "  我的服务  ");
        Assert.Equal("我的服务", renamed.ApiPresets[0].Name);
        Assert.Same(settings, APIPresetLogic.Rename(settings, id, "   "));
        Assert.Same(settings, APIPresetLogic.Rename(settings, "missing", "X"));
    }

    [Fact]
    public void SelectingAPresetAppliesItsAddressAndModelAndStoresTheEdits()
    {
        var first = new APIPreset(APIPreset.NewId(), "A", "https://a.test/v1", "model-a");
        var second = new APIPreset(APIPreset.NewId(), "B", "https://b.test/v1", "model-b");
        var settings = new AppSettings
        {
            ApiPresets = [first, second],
            ActiveApiPresetId = first.Id,
            BaseUrl = "https://edited.test/v1",
            Model = "edited-model"
        };
        var switched = APIPresetLogic.Select(settings, second.Id);
        Assert.Equal(second.Id, switched.ActiveApiPresetId);
        Assert.Equal("https://b.test/v1", switched.BaseUrl);
        Assert.Equal("model-b", switched.Model);
        // The old preset keeps the field edits that were made while it was active.
        var old = switched.ApiPresets.First(preset => preset.Id == first.Id);
        Assert.Equal("https://edited.test/v1", old.BaseUrl);
        Assert.Equal("edited-model", old.Model);
    }

    [Fact]
    public void StoringTheActivePresetWritesTheEditedFields()
    {
        var settings = APIPresetLogic.EnsureLoaded(new AppSettings());
        settings = settings with { BaseUrl = "https://new.test/v1", Model = "new-model" };
        var stored = APIPresetLogic.StoreActive(settings, "high");
        var active = Assert.Single(stored.ApiPresets);
        Assert.Equal("https://new.test/v1", active.BaseUrl);
        Assert.Equal("new-model", active.Model);
        Assert.Equal("high", active.Effort);
        Assert.Equal("high", APIPresetLogic.ActiveEffort(stored));
        Assert.Equal("high", APIPresetLogic.EffortParameter(stored));
    }

    [Fact]
    public void DefaultEffortSendsNoParameter()
    {
        var settings = APIPresetLogic.EnsureLoaded(new AppSettings());
        Assert.Null(APIPresetLogic.EffortParameter(settings));
        Assert.Equal("default", APIPresetLogic.ActiveEffort(settings));
    }

    [Fact]
    public void DeleteKeepsAtLeastOnePresetAndRemovesItsCredentialAccount()
    {
        var first = new APIPreset(APIPreset.NewId(), "A", "https://a.test/v1");
        var second = new APIPreset(APIPreset.NewId(), "B", "https://b.test/v1");
        var settings = new AppSettings { ApiPresets = [first, second], ActiveApiPresetId = first.Id };

        var (unchanged, noAccount) = APIPresetLogic.Delete(new AppSettings
        {
            ApiPresets = [first],
            ActiveApiPresetId = first.Id
        }, first.Id);
        Assert.Single(unchanged.ApiPresets);
        Assert.Null(noAccount);

        var (next, account) = APIPresetLogic.Delete(settings, first.Id);
        Assert.Equal(first.KeyAccount, account);
        Assert.Equal(second.Id, next.ActiveApiPresetId);
        Assert.Equal(second.Id, Assert.Single(next.ApiPresets).Id);

        var (stillActive, otherAccount) = APIPresetLogic.Delete(settings, second.Id);
        Assert.Equal(second.KeyAccount, otherAccount);
        Assert.Equal(first.Id, stillActive.ActiveApiPresetId);
        Assert.Single(stillActive.ApiPresets);
    }

    [Fact]
    public void ModelListEndpointSitsNextToTheChatEndpoint()
    {
        Assert.Equal("https://api.openai.com/v1/models",
            APIPresetLogic.ModelListEndpoint("https://api.openai.com/v1")!.AbsoluteUri);
        Assert.Equal("https://api.openai.com/v1/models",
            APIPresetLogic.ModelListEndpoint("https://api.openai.com/v1/chat/completions")!.AbsoluteUri);
        Assert.Equal("http://localhost:1234/v1/models",
            APIPresetLogic.ModelListEndpoint("http://localhost:1234/v1")!.AbsoluteUri);
        Assert.Null(APIPresetLogic.ModelListEndpoint("not a url"));
        Assert.Null(APIPresetLogic.ModelListEndpoint("ftp://example.test/v1"));
    }

    [Fact]
    public void ModelListStatusUsesTheMacSentences()
    {
        Assert.Equal("共 12 个模型", APIPresetLogic.ModelListStatus(12));
        Assert.Equal("服务没有返回模型列表，请手动填写模型 ID", APIPresetLogic.ModelListStatus(0));
    }

    [Fact]
    public void PresetsSerializeWithTheMacFieldNames()
    {
        var ids = new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J" };
        var settings = APIPresetLogic.EnsureLoaded(new AppSettings { BaseUrl = "https://api.openai.com/v1", Model = "gpt" });
        settings = settings with
        {
            ApiPresets = settings.ApiPresets.Select(preset => preset with { Id = ids[0], KeyAccount = "answer.api" }).ToList(),
            ActiveApiPresetId = ids[0]
        };
        var json = JsonSerializer.Serialize(settings, JsonDefaults.Options);
        Assert.Contains("\"apiPresets\"", json, StringComparison.Ordinal);
        Assert.Contains("\"baseURL\":", json, StringComparison.Ordinal);
        Assert.Contains("\"activeApiPresetId\"", json, StringComparison.OrdinalIgnoreCase);
        var roundTripped = JsonSerializer.Deserialize<AppSettings>(json, JsonDefaults.Options)!;
        var preset = Assert.Single(roundTripped.ApiPresets);
        Assert.Equal("https://api.openai.com/v1", preset.BaseUrl);
        Assert.Equal("answer.api", preset.KeyAccount);
        Assert.Equal("gpt", preset.Model);
    }
}
