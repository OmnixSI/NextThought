using System.Text.RegularExpressions;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E27: the answer-service settings model (API presets, reasoning strength, fast mode, engine test,
/// GitHub access) matches the Mac sources and is reachable from the settings page model. The view
/// is not instantiated here (no Avalonia), so this locks the source contract.
/// </summary>
public sealed class ApiPresetSettingsTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml.cs")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root(), relative));

    [Fact]
    public void ApiPresetModelMatchesTheMacSource()
    {
        var windows = Read(Path.Combine("Windows", "src", "NextThought.Core", "APIPresets.cs"));
        var swift = Read(Path.Combine("Sources", "NextThought", "APIPresets.swift"));
        // Mac APIPreset fields (JSON shape kept in the Windows record).
        foreach (var field in new[] { "id", "name", "baseURL", "model", "effort", "keyAccount" })
            Assert.Contains($"var {field}", swift);
        foreach (var property in new[]
                 {
                     "public string Id", "public string Name", "public string BaseUrl",
                     "public string Model", "public string Effort", "public string KeyAccount"
                 })
            Assert.Contains(property, windows);
        Assert.Contains("[JsonPropertyName(\"baseURL\")]", windows);
        // Mac effort titles and provider templates, verbatim.
        foreach (var effort in new[] { "默认（不指定）", "minimal · 最快", "high · 最强" })
        {
            Assert.Contains(effort, swift);
            Assert.Contains(effort, windows);
        }
        foreach (var template in new[]
                 {
                     "OpenAI", "DeepSeek", "通义千问", "Moonshot Kimi", "智谱 GLM", "火山方舟 豆包",
                     "Groq", "OpenRouter", "本机 Ollama", "本机 LM Studio"
                 })
        {
            Assert.Contains(template, swift);
            Assert.Contains(template, windows);
        }
        // Mac rules ported: unique names, keep the last preset, macOS status sentences.
        foreach (var rule in new[] { "UniqueName", "EnsureLoaded", "Delete", "Rename", "Select", "EffortParameter", "ModelListEndpoint" })
            Assert.Contains(rule, windows);
        Assert.Contains("共 {count} 个模型", windows);
        Assert.Contains("服务没有返回模型列表，请手动填写模型 ID", windows);
    }

    [Fact]
    public void SettingsPageModelExposesThePresetActions()
    {
        var viewModel = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "ViewModels", "SettingsViewModel.cs"));
        foreach (var member in new[]
                 {
                     "ApiPresetChoices", "SelectedApiPreset", "SelectedApiEffort", "ApiEffortChoices",
                     "ApiModelChoices", "ApiModelsStatus", "AddApiPreset", "RenameActiveApiPreset",
                     "DeleteActiveApiPreset", "FetchApiModelsAsync", "SaveActivePresetSecret",
                     "CodexFastMode", "CodexFastLabel", "GithubRepository", "SaveGithubToken", "ClearGithubToken"
                 })
            Assert.Contains(member, viewModel);
        Assert.Contains("APIPresetLogic.EnsureLoaded", viewModel);
        Assert.Contains("ChatCompletionClient.FetchModelsAsync", viewModel);
        Assert.Contains("SaveSecret", viewModel);
        // The presets and the active id travel in settings.json under the Mac keys.
        Assert.Contains("ApiPresets = EditedApiPresets()", viewModel);
        Assert.Contains("ActiveApiPresetId", viewModel);
    }

    [Fact]
    public void EngineTestAndGithubAccessAreReachable()
    {
        var state = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "AppState.Models.cs"));
        Assert.Contains("TestAnswerEngineAsync", state);
        Assert.Contains("EngineTestState", state);
        Assert.Contains("这是连通性测试。只回复两个字：收到。", state);
        Assert.Contains("EngineTestPassed", state);
        var store = Read(Path.Combine("Windows", "src", "NextThought.Platform", "SettingsStore.cs"));
        Assert.Contains("ReadSecret", store);
        Assert.Contains("SaveSecret", store);
        Assert.Contains("ReadGithubToken", store);
        // No plaintext credential file is introduced.
        Assert.DoesNotContain("File.WriteAllText(secretFile", store);
    }

    [Fact]
    public void TheModelListFetchUsesTheOpenAiCompatibleEndpoint()
    {
        var client = Read(Path.Combine("Windows", "src", "NextThought.Platform", "ChatCompletionClient.cs"));
        Assert.Contains("FetchModelsAsync", client);
        Assert.Contains("ModelListParser.Ids", client);
        Assert.Contains("ModelListMessage", client);
        var core = Read(Path.Combine("Windows", "src", "NextThought.Core", "APIPresets.cs"));
        Assert.Contains("/models", core);
    }

    [Fact]
    public void TheAnswerPageWiresThePresetManagerAndEngineTest()
    {
        var view = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml"));
        foreach (var member in new[]
                 {
                     "Settings.ApiPresetChoices", "Settings.SelectedApiPreset", "Settings.ApiPresetName",
                     "Settings.AddApiPresetFromDraftCommand", "Settings.RenameActiveApiPresetFromDraftCommand",
                     "Settings.FetchApiModelsCommand", "Settings.ApiModelChoices", "Settings.ApiEffortChoices",
                     "Settings.SelectedApiEffort", "TestAnswerEngineCommand", "EngineTestLabel",
                     "Settings.GithubRepository", "SaveGithubToken_Click", "CreateGithubToken_Click",
                     "{loc:Tr 获取列表}", "{loc:Tr 检查连接}", "{loc:Tr 快速模式}", "{loc:Tr 推理强度}"
                 })
            Assert.Contains(member, view);
        var code = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml.cs"));
        Assert.Contains("DeleteApiPreset_Click", code);
        Assert.Contains("ConfirmDialog", code);
    }

    [Fact]
    public void TheUpdaterReadsTheRepositoryAndTheToken()
    {
        var update = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "AppState.Update.cs"));
        Assert.Contains("UpdateAccess", update);
        Assert.Contains("SettingsStore.ReadGithubToken", update);
        Assert.Contains("UpdateFeed.SourceFor", update);
        Assert.Contains("UpdateCheck.RunAsync(feed, UpdateChannel, token)", update);
        Assert.Contains("UpdateInstaller.PrepareAsync", update);
        var feed = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "UpdateFeed.cs"));
        Assert.Contains("SourceFor", feed);
        Assert.Contains("string? token", feed);
    }

    [Fact]
    public void TheFeedUrlFollowsTheSavedRepository()
    {
        Assert.Equal("https://api.github.com/repos/OmnixSI/NextThought/releases",
            UpdateFeedPolicy.DefaultFor("OmnixSI/NextThought"));
        Assert.Equal("https://api.github.com/repos/Org/Repo/releases", UpdateFeedPolicy.DefaultFor("Org/Repo"));
        Assert.Equal(UpdateFeedPolicy.Default, UpdateFeedPolicy.DefaultFor(""));
        Assert.Equal(UpdateFeedPolicy.Default, UpdateFeedPolicy.DefaultFor("Org"));
        Assert.Equal(UpdateFeedPolicy.Default, UpdateFeedPolicy.DefaultFor("A/B/C"));
        Assert.Equal(UpdateFeedPolicy.Default, UpdateFeedPolicy.DefaultFor("Bad Repo/Name"));
        Assert.Equal("https://api.github.com/repos/Org/Repo/releases",
            UpdateFeedPolicy.ResolveSourceFor("Org/Repo", _ => null));
        Assert.Equal("https://example.test/feed",
            UpdateFeedPolicy.ResolveSourceFor("Org/Repo", name => name switch
            {
                UpdateFeedPolicy.QaVariable => "1",
                "NEXTTHOUGHT_UPDATE_FEED" => "https://example.test/feed",
                _ => null
            }));
    }

    [Fact]
    public void TheNewLabelsComeFromTheMacStringTable()
    {
        var catalogue = Read(Path.Combine("Windows", "src", "NextThought.Core", "UiTextMac.g.cs"));
        foreach (var key in new[]
                 {
                     "约 1.5 倍速度 · 用量更多", "快速模式", "获取列表", "重命名预设",
                     "删除预设“%@”？", "已保存访问令牌", "未保存访问令牌", "仓库",
                     "粘贴访问令牌（github_pat_…）"
                 })
            Assert.Contains($"[\"{key}\"]", catalogue);
    }
}
