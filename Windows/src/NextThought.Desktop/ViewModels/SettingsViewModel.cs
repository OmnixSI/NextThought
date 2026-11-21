using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Security.Cryptography;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop;

/// <summary>E27: one saved API provider as the settings page lists it (Mac <c>APIPreset</c>).</summary>
public sealed record ApiPresetChoice(string Id, string Name)
{
    public override string ToString() => Name;
}

/// <summary>E27: one reasoning-strength choice of the active API provider (Mac <c>APIPreset.efforts</c>).</summary>
public sealed record ApiEffortChoice(string Value, string Title)
{
    public override string ToString() => UiText.T(Title);
}

/// <summary>E26b: the cue surface (Mac <c>cueSurface</c>): 提词岛 or 浮窗.</summary>
public sealed record CueSurfaceChoice(string Value, string Title)
{
    public override string ToString() => UiText.T(Title);
}

/// <summary>E23: how documents are read (Mac <c>DocumentOCRProvider</c>).</summary>
public sealed record DocumentProviderChoice(string Value, string Title)
{
    public override string ToString() => UiText.T(Title);
}

/// <summary>
/// Editable draft of <see cref="AppSettings"/> plus the device, process and model choices
/// the settings page offers. Mirrors the settings surface of the Mac app; the keys and
/// defaults are the ones the Windows baseline already persists.
/// </summary>
public partial class SettingsViewModel(AppState state) : ObservableObject
{
    private readonly AppState state = state;

    private static readonly ModelChoice[] ModelChoiceValues =
    [
        new("xasr480", "X-ASR 中英 · 480 ms（推荐）"),
        new("xasr160", "X-ASR 中英 · 160 ms")
    ];

    private static readonly EffortChoice[] EffortChoiceValues =
    [
        new("", "默认，不指定"),
        new("none", "none（关闭思考）"),
        new("low", "low"),
        new("medium", "medium"),
        new("high", "high")
    ];

    private static readonly ProviderPreset[] ProviderPresetsValues =
    [
        new("OpenAI", "https://api.openai.com/v1", "gpt-5.2"),
        new("DeepSeek", "https://api.deepseek.com/v1", "deepseek-chat"),
        new("OpenRouter", "https://openrouter.ai/api/v1", "openai/gpt-4o-mini"),
        new("Ollama (local)", "http://127.0.0.1:11434/v1", "qwen2.5:7b"),
        new("LM Studio (local)", "http://127.0.0.1:1234/v1", "local-model"),
        new("自定义", "", "")
    ];

    /// <summary>G8: a new list per access so ComboBox items re-render on a language switch.</summary>
    public IReadOnlyList<ProviderPreset> ProviderPresets => ProviderPresetsValues.ToArray();

    [ObservableProperty] private ProviderPreset? selectedProviderPreset;

    partial void OnSelectedProviderPresetChanged(ProviderPreset? value)
    {
        if (value is null || value.BaseUrl.Length == 0) return;
        BaseUrl = value.BaseUrl;
        if (value.Model.Length > 0) Model = value.Model;
    }

    // MARK: - E27 回答服务设置：预设、推理强度、快速模式、引擎测试、GitHub 访问

    private List<APIPreset> apiPresets = [];

    public ObservableCollection<ApiPresetChoice> ApiPresetChoices { get; } = [];

    [ObservableProperty] private ApiPresetChoice? selectedApiPreset;

    [ObservableProperty] private string apiModelsStatus = "";

    public ObservableCollection<string> ApiModelChoices { get; } = [];

    public IReadOnlyList<ApiEffortChoice> ApiEffortChoices =>
        APIPreset.Efforts.Select(effort => new ApiEffortChoice(effort.Value, effort.Title)).ToArray();

    [ObservableProperty] private ApiEffortChoice? selectedApiEffort;

    [ObservableProperty] private bool codexFastMode;

    public string CodexFastLabel => UiText.T("约 1.5 倍速度 · 用量更多");

    /// <summary>False for the API backend: Fast is a Codex service tier only.</summary>
    public bool CodexFastAvailable => UseCodex;

    [ObservableProperty] private string githubRepository = "OmnixSI/NextThought";
    [ObservableProperty] private string githubToken = "";
    [ObservableProperty] private string githubTokenStatus = "";
    public string GithubTokenUrl => "https://github.com/settings/personal-access-tokens/new";

    /// <summary>E27: the name typed in the preset manager (new preset / rename).</summary>
    [ObservableProperty] private string apiPresetName = "";

    /// <summary>E27: Mac APIPresetManager disables 删除 when only one preset is left.</summary>
    public bool CanDeleteApiPreset => ApiPresetChoices.Count > 1;

    // MARK: - E23 文档读取（Mac DocumentModelSettings / DocumentFeatureSetup）

    private static readonly DocumentProviderChoice[] DocumentProviderChoiceValues =
    [
        new("system", "系统识别"),
        new("paddle", "本机增强"),
        new("codex", "Codex 代读")
    ];
    public IReadOnlyList<DocumentProviderChoice> DocumentProviderChoices => DocumentProviderChoiceValues.ToArray();

    [ObservableProperty] private DocumentProviderChoice? selectedDocumentProvider;
    [ObservableProperty] private bool documentFallbackToVision = true;
    [ObservableProperty] private string codexOcrModel = "gpt-6-luna";
    [ObservableProperty] private string codexOcrEffort = "medium";
    [ObservableProperty] private string codexOcrTimeout = "600";
    public IReadOnlyList<string> CodexOcrEffortChoices => ["low", "medium", "high", "xhigh"];
    private bool documentProviderWasSelected;

    partial void OnSelectedDocumentProviderChanged(DocumentProviderChoice? value)
    {
        if (string.IsNullOrEmpty(value?.Value)) return;
        documentProviderWasSelected = true;
        state.RefreshDocumentComponents();
        OnPropertyChanged(nameof(DocumentProviderHelp));
        OnPropertyChanged(nameof(ShowCodexDocumentSettings));
    }

    public bool ShowCodexDocumentSettings => SelectedDocumentProvider?.Value == "codex";

    /// <summary>Mac DocumentReadingSettings help under the provider picker.</summary>
    public string DocumentProviderHelp => SelectedDocumentProvider?.Value switch
    {
        "paddle" => UiText.T("适合扫描件、表格和公式，在本机计算，占一些内存。"),
        "codex" => UiText.T("读取原文件，不用下载识别模型。需要已登录的 Codex，占用账号额度。"),
        _ => UiText.T("Mac 自带，离线、无需下载。普通文字够用；复杂表格和公式可能丢失结构。")
    };

    private void LoadDocumentSettings(AppSettings settings)
    {
        SelectedDocumentProvider = DocumentProviderChoices.FirstOrDefault(choice => choice.Value == settings.DocumentOcrProvider)
                                   ?? DocumentProviderChoices[0];
        documentProviderWasSelected = settings.DocumentProviderWasSelected;
        DocumentFallbackToVision = settings.OcrFallbackToVision;
        CodexOcrModel = settings.CodexOcrModel;
        CodexOcrEffort = settings.CodexOcrEffort;
        CodexOcrTimeout = settings.CodexOcrTimeout.ToString("0");
        state.RefreshDocumentComponents();
    }

    /// <summary>E27: reads the saved API presets and the GitHub access state into the page model.</summary>
    private void LoadApiPresets()
    {
        AppSettings settings;
        try { settings = state.SettingsStore.Load(); }
        catch { settings = new AppSettings(); }
        settings = APIPresetLogic.EnsureLoaded(settings);
        apiPresets = settings.ApiPresets.ToList();
        RefreshApiPresetChoices(settings.ActiveApiPresetId);
        SelectedApiEffort = ApiEffortChoices.FirstOrDefault(choice => choice.Value == APIPresetLogic.ActiveEffort(settings))
                            ?? ApiEffortChoices[0];
        CodexFastMode = settings.CodexFastMode == true;
        if (SelectedApiPreset is not null) ApplyApiPresetToFields(SelectedApiPreset);
    }

    private void RefreshApiPresetChoices(string? activeId)
    {
        var previous = activeId ?? selectedApiPreset?.Id;
        ApiPresetChoices.Clear();
        foreach (var preset in apiPresets) ApiPresetChoices.Add(new ApiPresetChoice(preset.Id, preset.Name));
        selectedApiPreset = ApiPresetChoices.FirstOrDefault(choice => choice.Id == previous) ?? ApiPresetChoices.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedApiPreset));
        OnPropertyChanged(nameof(CanDeleteApiPreset));
    }

    private void ApplyApiPresetToFields(ApiPresetChoice choice)
    {
        var preset = apiPresets.FirstOrDefault(item => item.Id == choice.Id);
        if (preset is null) return;
        BaseUrl = preset.BaseUrl;
        Model = preset.Model;
        SelectedApiEffort = ApiEffortChoices.FirstOrDefault(item => item.Value == preset.Effort) ?? ApiEffortChoices[0];
        try { ApiKey = state.SettingsStore.ReadSecret(preset.KeyAccount); }
        catch (CryptographicException) { apiKeyReadFailed = true; }
        catch (Exception) { apiKeyReadFailed = true; }
        HasApiKey = ApiKey.Length > 0;
        ApiModelChoices.Clear();
        ApiModelsStatus = "";
    }

    partial void OnSelectedApiPresetChanged(ApiPresetChoice? value)
    {
        if (value is not null)
        {
            ApiPresetName = value.Name;
            ApplyApiPresetToFields(value);
        }
    }

    /// <summary>E27: 新建预设 from the settings page (name typed in the manager, current address).</summary>
    [RelayCommand]
    private void AddApiPresetFromDraft()
    {
        var name = ApiPresetName.Trim();
        if (name.Length == 0) name = UiText.T("默认");
        AddApiPreset(name, BaseUrl);
    }

    /// <summary>E27: 重命名预设 from the settings page (name typed in the manager).</summary>
    [RelayCommand]
    private void RenameActiveApiPresetFromDraft()
    {
        var name = ApiPresetName.Trim();
        if (name.Length > 0) RenameActiveApiPreset(name);
    }

    /// <summary>E27: deletes the active preset after the page's confirmation dialog.</summary>
    public void DeleteActiveApiPresetConfirmed() => DeleteActiveApiPreset();

    /// <summary>E27: 获取列表 chose a model; the model name is what the API request sends.</summary>
    [RelayCommand]
    private void ChooseApiModel(string? id)
    {
        if (id is { Length: > 0 }) Model = id;
    }

    partial void OnCodexFastModeChanged(bool value) => OnPropertyChanged(nameof(CodexFastAvailable));

    /// <summary>E27: writes the current address/model/effort back into the active preset list.</summary>
    private List<APIPreset> EditedApiPresets()
    {
        var presets = apiPresets.ToList();
        var activeId = selectedApiPreset?.Id;
        var index = presets.FindIndex(preset => preset.Id == activeId);
        if (index >= 0)
        {
            presets[index] = presets[index] with
            {
                BaseUrl = BaseUrl.Trim(),
                Model = Model.Trim(),
                Effort = SelectedApiEffort?.Value ?? "default"
            };
        }
        return presets;
    }

    /// <summary>E27: adds a preset with a unique name (Mac <c>addAPIPreset</c>) and activates it.</summary>
    public void AddApiPreset(string name, string baseUrl)
    {
        var settings = state.SettingsStore.Load();
        settings = APIPresetLogic.EnsureLoaded(settings) with { ApiPresets = EditedApiPresets(), ActiveApiPresetId = selectedApiPreset?.Id ?? "" };
        settings = APIPresetLogic.StoreActive(settings, SelectedApiEffort?.Value ?? "default");
        var (next, _) = APIPresetLogic.Add(settings, name, baseUrl);
        PersistApiPresets(next);
    }

    /// <summary>E27: Mac <c>renameAPIPreset</c> for the active preset.</summary>
    public void RenameActiveApiPreset(string name)
    {
        if (selectedApiPreset is null) return;
        var settings = state.SettingsStore.Load();
        settings = APIPresetLogic.EnsureLoaded(settings) with { ApiPresets = EditedApiPresets(), ActiveApiPresetId = selectedApiPreset.Id };
        PersistApiPresets(APIPresetLogic.Rename(settings, selectedApiPreset.Id, name));
    }

    /// <summary>E27: Mac <c>deleteAPIPreset</c>; deleting the active preset activates the first left.</summary>
    public void DeleteActiveApiPreset()
    {
        if (selectedApiPreset is null) return;
        var settings = state.SettingsStore.Load();
        settings = APIPresetLogic.EnsureLoaded(settings) with { ApiPresets = EditedApiPresets(), ActiveApiPresetId = selectedApiPreset.Id };
        var (next, removedAccount) = APIPresetLogic.Delete(settings, selectedApiPreset.Id);
        if (removedAccount is not null)
        {
            try { state.SettingsStore.SaveSecret(removedAccount, ""); }
            catch { }
        }
        PersistApiPresets(next);
    }

    private void PersistApiPresets(AppSettings settings)
    {
        _ = state.SettingsStore.SaveAsync(settings);
        apiPresets = settings.ApiPresets.ToList();
        RefreshApiPresetChoices(settings.ActiveApiPresetId);
        if (SelectedApiPreset is not null) ApplyApiPresetToFields(SelectedApiPreset);
    }

    /// <summary>E27: Mac <c>fetchAPIModels</c> — GET {base}/models and list the ids.</summary>
    [RelayCommand]
    private async Task FetchApiModelsAsync()
    {
        ApiModelsStatus = "正在获取…";
        var (ids, status) = await ChatCompletionClient.FetchModelsAsync(BaseUrl, ApiKey);
        ApiModelChoices.Clear();
        foreach (var id in ids) ApiModelChoices.Add(id);
        ApiModelsStatus = status;
    }

    /// <summary>E27: a saved alternative to the fixed provider template list (Mac APIProviderSwitch presets).</summary>
    [RelayCommand]
    private void SelectApiPreset(string? id)
    {
        var choice = ApiPresetChoices.FirstOrDefault(item => item.Id == id);
        if (choice is not null) SelectedApiPreset = choice;
    }

    /// <summary>E27: GitHub access section (Mac GitHubAccessSection): repository + token + create link.</summary>
    private void LoadGitHubAccess()
    {
        try { githubRepository = state.SettingsStore.Load().UpdateRepository; }
        catch { githubRepository = "OmnixSI/NextThought"; }
        if (githubRepository.Length == 0) githubRepository = "OmnixSI/NextThought";
        RefreshGithubTokenStatus();
        OnPropertyChanged(nameof(GithubRepository));
    }

    private void RefreshGithubTokenStatus()
    {
        var token = "";
        try { token = state.SettingsStore.ReadGithubToken(); }
        catch { }
        GithubTokenStatus = token.Length > 0 ? UiText.T("已保存访问令牌") : UiText.T("未保存访问令牌");
    }

    [RelayCommand]
    private void SaveGithubToken()
    {
        var token = GithubToken.Trim();
        if (token.Length == 0) return;
        state.SettingsStore.SaveGithubToken(token);
        GithubToken = "";
        RefreshGithubTokenStatus();
    }

    [RelayCommand]
    private void ClearGithubToken()
    {
        state.SettingsStore.SaveGithubToken("");
        GithubToken = "";
        RefreshGithubTokenStatus();
    }

    /// <summary>E27: writes the active preset's API key into its own credential account.</summary>
    private void SaveActivePresetSecret()
    {
        var preset = apiPresets.FirstOrDefault(item => item.Id == selectedApiPreset?.Id) ?? apiPresets.FirstOrDefault();
        if (preset is null) return;
        if (apiKeyReadFailed && ApiKey.Trim().Length == 0) return;
        try { state.SettingsStore.SaveSecret(preset.KeyAccount, ApiKey.Trim()); }
        catch { }
    }

    [ObservableProperty] private string baseUrl = "https://api.openai.com/v1";
    [ObservableProperty] private string model = "";
    [ObservableProperty] private string notesModel = "";
    [ObservableProperty] private string reasoningEffort = "";
    [ObservableProperty] private string apiKey = "";
    private bool apiKeyReadFailed;
    [ObservableProperty] private bool hasApiKey;
    [ObservableProperty] private string keyStatus = "";
    [ObservableProperty] private string localModel = "xasr480";
    [ObservableProperty] private bool secondPass = true;
    [ObservableProperty] private bool captureSystem = true;
    [ObservableProperty] private bool captureMicrophone = true;
    [ObservableProperty] private double microphoneGain = 1;
    [ObservableProperty] private string vocabulary = "";
    [ObservableProperty] private bool automaticAnswers = true;
    [ObservableProperty] private bool autoSummarize = true;
    [ObservableProperty] private bool useSemanticRetrieval;
    [ObservableProperty] private bool separateSpeakers;
    [ObservableProperty] private bool translateSpeech;
    [ObservableProperty] private bool useMaterials;
    [ObservableProperty] private bool useCodex;
    [ObservableProperty] private bool webSearch;
    [ObservableProperty] private string codexModel = "";
    /// <summary>G8: Mac <c>appLanguagePreference</c> choices — 跟随系统 / 简体中文 / English.</summary>
    public static readonly (string Value, string Key)[] UiLanguageOptions =
    [
        (UiTextSource.SystemPreference, "跟随系统"),
        (UiTextSource.ChinesePreference, "简体中文"),
        (UiTextSource.EnglishPreference, "English")
    ];
    public IReadOnlyList<string> UiLanguages => UiLanguageOptions.Select(option => LanguageLabel(option.Value)).ToArray();
    [ObservableProperty] private string uiLanguage = "跟随系统";

    public static string LanguageLabel(string preference) => preference switch
    {
        UiTextSource.ChinesePreference => UiText.T("简体中文"),
        UiTextSource.EnglishPreference => "English",
        _ => UiText.T("跟随系统")
    };

    public static string LanguagePreference(string? label) =>
        UiLanguageOptions.Select(option => option.Value)
            .FirstOrDefault(value => string.Equals(LanguageLabel(value), label, StringComparison.Ordinal))
        ?? UiTextSource.NormalizePreference(label);

    /// <summary>G8: the stored Mac <c>appLanguagePreference</c> value behind the picker label.</summary>
    public string Language => LanguagePreference(UiLanguage);

    /// <summary>G8: the picker labels are interface text too, so re-render them when the language changes.</summary>
    public void NotifyLanguageChanged()
    {
        var value = LanguagePreference(UiLanguage);
        OnPropertyChanged(nameof(UiLanguages));
        UiLanguage = LanguageLabel(value);
        OnPropertyChanged(nameof(CloudSttChoices));
        OnPropertyChanged(nameof(CloudSttLanguages));
        OnPropertyChanged(nameof(AppearanceChoices));
        OnPropertyChanged(nameof(CueWidthChoices));
        OnPropertyChanged(nameof(AutoEndModes));
        OnPropertyChanged(nameof(ProviderPresets));
        OnPropertyChanged(nameof(ModelChoicesList));
        OnPropertyChanged(nameof(EffortChoicesList));
        OnPropertyChanged(nameof(InsightSourceLabels));
        OnPropertyChanged(nameof(SelectedInsightSourceLabel));
        OnPropertyChanged(nameof(SelectedCloudStt));
        OnPropertyChanged(nameof(SelectedCloudLanguage));
        OnPropertyChanged(nameof(SelectedAppearance));
        OnPropertyChanged(nameof(SelectedCueWidth));
        OnPropertyChanged(nameof(CueSurfaceChoices));
        var cueSurfaceValue = SelectedCueSurface?.Value ?? "island";
        SelectedCueSurface = CueSurfaceChoices.FirstOrDefault(choice => choice.Value == cueSurfaceValue) ?? CueSurfaceChoices[0];
        OnPropertyChanged(nameof(SelectedAutoEndMode));
        OnPropertyChanged(nameof(SelectedModelChoice));
        OnPropertyChanged(nameof(SelectedEffort));
        OnPropertyChanged(nameof(SelectedProviderPreset));
        OnPropertyChanged(nameof(SelectedSystemDevice));
        OnPropertyChanged(nameof(SelectedMicrophoneDevice));
        OnPropertyChanged(nameof(SelectedProcess));
        OnPropertyChanged(nameof(PresenterSummary));
        OnPropertyChanged(nameof(SavedHint));
        OnPropertyChanged(nameof(CodexFastLabel));
        OnPropertyChanged(nameof(DocumentProviderChoices));
        OnPropertyChanged(nameof(SelectedDocumentProvider));
        OnPropertyChanged(nameof(DocumentProviderHelp));
        OnPropertyChanged(nameof(ShowCodexDocumentSettings));
        OnPropertyChanged(nameof(ApiEffortChoices));
        OnPropertyChanged(nameof(SelectedApiEffort));
        OnPropertyChanged(nameof(ApiPresetChoices));
        OnPropertyChanged(nameof(SelectedApiPreset));
        RefreshGithubTokenStatus();
        OnPropertyChanged(nameof(GithubTokenStatus));
        OnPropertyChanged(nameof(ApiModelsStatus));
        KeyStatus = UiText.Translate(KeyStatus, UiTextSource.Instance.IsEnglish);
        CloudSttKeyStatus = CloudSttKey.Length == 0 ? UiText.T("未填写") : UiText.T("已加密保存");
        TavilyKeyStatus = UiText.T("密钥只保存在当前 Windows 用户；也可以继续用 TAVILY_API_KEY 环境变量。");
        RefreshScreens();
    }

    private static readonly CloudSttChoice[] CloudSttChoicesValues =
    [
        new("", "本地（默认）"),
        new("soniox", "Soniox · 中英混说"),
        new("openai", "OpenAI Realtime"),
        new("deepgram", "Deepgram Nova-3")
    ];

    /// <summary>G8: a new list per access so ComboBox items re-render on a language switch.</summary>
    public IReadOnlyList<CloudSttChoice> CloudSttChoices => CloudSttChoicesValues.ToArray();
    private static readonly CloudLanguageChoice[] CloudSttLanguagesValues =
    [
        new("auto", "自动"),
        new("zh", "中文"),
        new("en", "英文")
    ];

    /// <summary>G8: a new list per access so ComboBox items re-render on a language switch.</summary>
    public IReadOnlyList<CloudLanguageChoice> CloudSttLanguages => CloudSttLanguagesValues.ToArray();
    [ObservableProperty] private CloudSttChoice? selectedCloudStt;
    [ObservableProperty] private string cloudSttModel = "";
    [ObservableProperty] private string cloudSttKey = "";
    [ObservableProperty] private string cloudSttEndpointValue = "";
    [ObservableProperty] private string cloudSttKeyStatus = "";
    [ObservableProperty] private CloudLanguageChoice? selectedCloudLanguage;

    partial void OnCloudSttKeyChanged(string value) =>
        CloudSttKeyStatus = value.Length == 0 ? NextThought.Core.UiText.T("未填写") : NextThought.Core.UiText.T("已加密保存");

    partial void OnSelectedCloudSttChanged(CloudSttChoice? value)
    {
        if (CloudSttModel.Length > 0) return;
        CloudSttModel = value?.Value switch
        {
            "soniox" => "stt-rt-v5",
            "openai" => "gpt-live-transcribe",
            "deepgram" => "nova-3",
            _ => ""
        };
    }

    private static readonly AppearanceOption[] AppearanceChoicesValues =
    [
        new("system", "跟随系统"),
        new("light", "浅色"),
        new("dark", "深色")
    ];

    /// <summary>G8: a new list per access so ComboBox items re-render on a language switch.</summary>
    public IReadOnlyList<AppearanceOption> AppearanceChoices => AppearanceChoicesValues.ToArray();
    [ObservableProperty] private AppearanceOption? selectedAppearance;
    private static readonly CueWidthOption[] CueWidthChoicesValues =
    [
        new("narrow", "窄"),
        new("regular", "标准"),
        new("wide", "宽")
    ];

    /// <summary>G8: a new list per access so ComboBox items re-render on a language switch.</summary>
    public IReadOnlyList<CueWidthOption> CueWidthChoices => CueWidthChoicesValues.ToArray();
    [ObservableProperty] private CueWidthOption? selectedCueWidth;
    [ObservableProperty] private bool cueAlwaysOpen;

    /// <summary>E26b: Mac quick settings 提词 — 提词岛 (island) or 浮窗 (window).</summary>
    private static readonly CueSurfaceChoice[] CueSurfaceChoiceValues =
    [
        new("island", "提词岛"),
        new("window", "浮窗")
    ];

    /// <summary>G8: a new list per access so the selected label re-renders on a language switch.</summary>
    public IReadOnlyList<CueSurfaceChoice> CueSurfaceChoices => CueSurfaceChoiceValues.ToArray();

    [ObservableProperty] private CueSurfaceChoice? selectedCueSurface;

    /// <summary>E26b: Mac <c>presenterSlideNotes</c> — show this slide's notes on the island while presenting.</summary>
    [ObservableProperty] private bool presenterSlideNotes = true;

    /// <summary>E26: Mac quick settings 提词字号 slider (14–36, default 22).</summary>
    [ObservableProperty] private double teleprompterFontSize = 22;
    [ObservableProperty] private double? subtitleBarX;
    [ObservableProperty] private double? subtitleBarY;
    [ObservableProperty] private bool capturePrivacy = true;
    [ObservableProperty] private bool teleprompterHideBackground;
    [ObservableProperty] private string cueScreen = "";
    [ObservableProperty] private int subtitleWidth = 920;
    [ObservableProperty] private int subtitleFontSize = 24;
    [ObservableProperty] private bool subtitleShowTranslation = true;
    [ObservableProperty] private bool autoCheckUpdates = true;
    [ObservableProperty] private string autoEndMode = "prompt";
    private string autoEndMinutes = "10";

    // 双屏演讲（PresenterMode.swift）：开关、幻灯片所在屏、以及给设置页看的一行说明。
    [ObservableProperty] private bool presenterMode;
    [ObservableProperty] private string presenterSlidesScreen = "";
    [ObservableProperty] private ScreenChoice? selectedSlidesScreen;

    /// <summary>Optional HTTPS relay origin; empty keeps the companion on the LAN channel only.</summary>
    [ObservableProperty] private string companionRelayUrl = "";
    [ObservableProperty] private string onboardingCompletedVersion = "";
    public IReadOnlyList<string> AnswerLanguageChoices { get; } = ["auto", "zh", "en"];
    [ObservableProperty] private string answerLanguage = "auto";
    [ObservableProperty] private bool answerGloss = true;
    [ObservableProperty] private bool followSlides;
    [ObservableProperty] private bool insightScreenWatch;
    [ObservableProperty] private double insightScreenInterval = 1;
    [ObservableProperty] private bool insightFollowSlides = true;
    [ObservableProperty] private string insightScreenSource = "display";
    [ObservableProperty] private string insightScreenRegion = "";
    public IReadOnlyList<string> InsightScreenSources { get; } = ["display", "window", "region"];
    private static readonly string[] InsightSourceLabelKeys = ["显示器", "窗口", "区域"];
    public IReadOnlyList<string> InsightSourceLabels => InsightSourceLabelKeys.Select(UiText.T).ToArray();
    public IReadOnlyList<double> InsightIntervals { get; } = [1, 2, 4, 8, 15];
    public string SelectedInsightSourceLabel
    {
        get => UiText.T(InsightScreenSource switch { "window" => "窗口", "region" => "区域", _ => "显示器" });
        set
        {
            var key = InsightSourceLabelKeys.FirstOrDefault(candidate => UiText.T(candidate) == value) ?? value;
            InsightScreenSource = key switch { "窗口" => "window", "区域" => "region", _ => "display" };
            OnPropertyChanged();
        }
    }
    [ObservableProperty] private bool autoTranslateForeign;
    /// <summary>E11/Mac UserDefaults "liveTranslation": null until the user touches 同声传译.</summary>
    [ObservableProperty] private bool? liveTranslation;
    /// <summary>E4/Mac webSearchMode: off / auto / always.</summary>
    [ObservableProperty] private string webSearchMode = "auto";
    [ObservableProperty] private string tavilyKey = "";
    [ObservableProperty] private string tavilyKeyStatus = "";

    public ObservableCollection<ScreenChoice> ScreenChoices { get; } = [];

    /// <summary>G8: the Mac presenter summary is localized where the settings page shows it.</summary>
    public string PresenterSummary
    {
        get
        {
            if (!PresenterMode) return UiText.T(PresenterLayout.Off);
            var plan = PresenterLayout.Resolve(DisplayCatalog.Displays, PresenterSlidesScreen);
            if (!plan.CanActivate || plan.Slides is null || plan.Camera is null) return UiText.T(plan.Summary);
            return UiText.Format("幻灯片在 %@，提示在 %@", plan.Slides.Name, plan.Camera.Name);
        }
    }

    partial void OnPresenterModeChanged(bool value) => OnPropertyChanged(nameof(PresenterSummary));

    partial void OnPresenterSlidesScreenChanged(string value) => OnPropertyChanged(nameof(PresenterSummary));

    partial void OnSelectedSlidesScreenChanged(ScreenChoice? value)
    {
        if (value is not null) PresenterSlidesScreen = value.Id;
    }

    /// <summary>Rebuilds the display picker after the window refreshed its topology.</summary>
    public void RefreshScreens()
    {
        var previous = SelectedSlidesScreen?.Id ?? PresenterSlidesScreen;
        ScreenChoices.Clear();
        foreach (var choice in DisplayCatalog.Choices) ScreenChoices.Add(choice);
        if (ScreenChoices.Count == 0) ScreenChoices.Add(new ScreenChoice("", "自动（第一块外接屏）"));
        SelectedSlidesScreen = ScreenChoices.FirstOrDefault(choice => choice.Id == previous) ?? ScreenChoices[0];
        OnPropertyChanged(nameof(PresenterSummary));
    }

    /// <summary>ComboBox SelectedItem writes null before its items exist; keep the last good value.</summary>
    public string AutoEndMinutes
    {
        get => autoEndMinutes;
        set => SetProperty(ref autoEndMinutes, string.IsNullOrEmpty(value) ? autoEndMinutes : value);
    }
    [ObservableProperty] private AutoEndChoice? selectedAutoEndMode;
    [ObservableProperty] private string lastMeetingTitle = "";
    [ObservableProperty] private DeviceChoice? selectedSystemDevice;
    [ObservableProperty] private DeviceChoice? selectedMicrophoneDevice;
    [ObservableProperty] private ProcessChoice? selectedProcess;
    [ObservableProperty] private ModelChoice? selectedModelChoice;
    [ObservableProperty] private EffortChoice? selectedEffort;
    private DateTime? savedHintAt;
    /// <summary>E2/G9: generated at read time so a language switch re-renders the saved time too.</summary>
    public string SavedHint => savedHintAt is { } time
        ? UiText.Format("已保存 · %@", time.ToString("HH:mm:ss"))
        : "";

    public ObservableCollection<DeviceChoice> SystemDevices { get; } = [];
    public ObservableCollection<DeviceChoice> MicrophoneDevices { get; } = [];
    public ObservableCollection<ProcessChoice> Processes { get; } = [];
    public static IReadOnlyList<ModelChoice> ModelChoices => ModelChoiceValues;
    public IReadOnlyList<ModelChoice> ModelChoicesList => ModelChoiceValues.ToArray();
    public static IReadOnlyList<EffortChoice> EffortChoices => EffortChoiceValues;
    public IReadOnlyList<EffortChoice> EffortChoicesList => EffortChoiceValues.ToArray();
    private static readonly AutoEndChoice[] AutoEndModesValues =
    [
        new("prompt", "静音后先提示，再结束（推荐）"),
        new("end", "静音后直接结束"),
        new("off", "不自动结束")
    ];

    /// <summary>G8: a new list per access so ComboBox items re-render on a language switch.</summary>
    public IReadOnlyList<AutoEndChoice> AutoEndModes => AutoEndModesValues.ToArray();
    public IReadOnlyList<string> AutoEndMinuteChoices { get; } = ["5", "10", "20", "30"];

    partial void OnSelectedModelChoiceChanged(ModelChoice? value)
    {
        if (value is null) return;
        LocalModel = value.Id;
        state.RefreshModelState();
    }

    partial void OnSelectedEffortChanged(EffortChoice? value)
    {
        if (value is not null) ReasoningEffort = value.Value;
    }

    partial void OnSelectedAutoEndModeChanged(AutoEndChoice? value)
    {
        if (value is not null) AutoEndMode = value.Value;
        OnPropertyChanged(nameof(AutoEndCanEndByItself));
    }

    /// <summary>E29: Mac shows 多久没人说话算结束 only when the meeting can end by itself.</summary>
    public bool AutoEndCanEndByItself => (SelectedAutoEndMode?.Value ?? "prompt") != "off";

    partial void OnSecondPassChanged(bool value) => state.RefreshModelState();
    partial void OnCaptureSystemChanged(bool value) => state.RefreshModelState();
    partial void OnCaptureMicrophoneChanged(bool value) => state.RefreshModelState();

    public void LoadFromStore()
    {
        AppSettings settings;
        try { settings = state.SettingsStore.Load(); }
        catch { settings = new AppSettings(); }
        UiLanguage = LanguageLabel(UiTextSource.NormalizePreference(settings.Language));
        // G5b: Mac key reducedEffects drives Motion.Reduce together with the system animation setting.
        Motion.AppReducedEffects = settings.ReducedEffects;
        SelectedAppearance = AppearanceChoices.FirstOrDefault(choice => choice.Value == settings.Appearance) ?? AppearanceChoices[0];
        SelectedCueWidth = CueWidthChoices.FirstOrDefault(choice => choice.Value == settings.CueWidth) ?? CueWidthChoices[1];
        CueAlwaysOpen = settings.CueAlwaysOpen;
        SelectedCueSurface = CueSurfaceChoices.FirstOrDefault(choice => choice.Value == settings.CueSurface) ?? CueSurfaceChoices[0];
        PresenterSlideNotes = settings.PresenterSlideNotes;
        TeleprompterFontSize = settings.TeleprompterFontSize;
        SubtitleBarX = settings.SubtitleBarX;
        SubtitleBarY = settings.SubtitleBarY;
        CapturePrivacy = settings.CapturePrivacy;
        TeleprompterHideBackground = settings.TeleprompterHideBackground;
        CueScreen = settings.CueScreen;
        SubtitleWidth = Math.Clamp(settings.SubtitleWidth, 520, 1400);
        SubtitleFontSize = Math.Clamp(settings.SubtitleFontSize, 16, 44);
        SubtitleShowTranslation = settings.SubtitleShowTranslation;
        AutoCheckUpdates = settings.AutoCheckUpdates;
        SelectedCloudStt = CloudSttChoices.FirstOrDefault(choice => choice.Value == settings.CloudStt) ?? CloudSttChoices[0];
        CloudSttModel = settings.CloudSttModel;
        SelectedCloudLanguage = CloudSttLanguages.FirstOrDefault(choice => choice.Value == settings.CloudSttLanguage) ?? CloudSttLanguages[0];
        CloudSttKey = state.SettingsStore.ReadCloudSttKey();
        CloudSttKeyStatus = CloudSttKey.Length == 0 ? UiText.T("未填写") : UiText.T("已加密保存");
        CloudSttEndpointValue = settings.CloudSttEndpoint;
        BaseUrl = settings.BaseUrl;
        Model = settings.Model;
        NotesModel = settings.NotesModel;
        ReasoningEffort = settings.ReasoningEffort;
        LocalModel = settings.LocalModel;
        SecondPass = settings.SecondPass;
        CaptureSystem = settings.CaptureSystem;
        CaptureMicrophone = settings.CaptureMicrophone;
        MicrophoneGain = double.IsFinite(settings.MicrophoneGain) ? Math.Clamp(settings.MicrophoneGain, 1, 8) : 1;
        Vocabulary = settings.Vocabulary;
        AutomaticAnswers = settings.AutomaticAnswers;
        AutoSummarize = settings.AutoSummarize;
        UseSemanticRetrieval = settings.UseSemanticRetrieval;
        SeparateSpeakers = settings.SeparateSpeakers;
        TranslateSpeech = settings.TranslateSpeech;
        UseMaterials = settings.UseMaterials;
        UseCodex = settings.UseCodex;
        WebSearch = settings.WebSearch;
        CodexModel = settings.CodexModel;
        PresenterMode = settings.PresenterMode;
        PresenterSlidesScreen = settings.PresenterSlidesScreen;
        CompanionRelayUrl = settings.CompanionRelayUrl;
        OnboardingCompletedVersion = settings.OnboardingCompletedVersion;
        AnswerLanguage = settings.AnswerLanguage;
        AnswerGloss = settings.AnswerGloss;
        AutoTranslateForeign = settings.AutoTranslateForeign;
        LiveTranslation = settings.LiveTranslation;
        WebSearchMode = settings.WebSearchMode;
        FollowSlides = settings.FollowSlides;
        InsightScreenWatch = settings.InsightScreenWatch;
        InsightScreenInterval = Math.Max(1, settings.InsightScreenInterval);
        InsightFollowSlides = settings.InsightFollowSlides;
        InsightScreenSource = settings.InsightScreenSource;
        InsightScreenRegion = settings.InsightScreenRegion;
        AutoEndMode = settings.MeetingAutoEndMode;
        AutoEndMinutes = settings.MeetingAutoEndMinutes.ToString();
        SelectedAutoEndMode = AutoEndModes.FirstOrDefault(x => x.Value == settings.MeetingAutoEndMode) ?? AutoEndModes[0];
        LastMeetingTitle = settings.LastMeetingTitle;
        SelectedModelChoice = ModelChoices.FirstOrDefault(x => x.Id == settings.LocalModel) ?? ModelChoices[0];
        SelectedEffort = EffortChoices.FirstOrDefault(x => x.Value == settings.ReasoningEffort) ?? EffortChoices[0];
        try
        {
            ApiKey = state.SettingsStore.ReadApiKey();
            HasApiKey = ApiKey.Length > 0;
            KeyStatus = HasApiKey ? UiText.T("密钥已保存（当前 Windows 用户加密）") : UiText.T("尚未保存密钥；本机服务可不填。");
        }
        catch (CryptographicException)
        {
            apiKeyReadFailed = true;
            HasApiKey = false;
            KeyStatus = UiText.T("原凭据无法由当前用户解密，请重新输入密钥。");
        }
        catch (Exception)
        {
            apiKeyReadFailed = true;
            HasApiKey = false;
            KeyStatus = UiText.T("密钥读取失败，已保留原密钥；如需更换请直接输入新密钥。");
        }
        SelectedProcess = Processes.FirstOrDefault();
        LoadApiPresets();
        LoadGitHubAccess();
        LoadDocumentSettings(settings);
        RefreshScreens();
    }

    public AppSettings Build()
    {
        // G1/G5/G6: save on top of what is on disk, so fields this page does not edit (scene
        // defaults, Mac keys Windows has no control for, unknown Mac keys) are not dropped.
        AppSettings disk;
        try { disk = state.SettingsStore.Load(); }
        catch { disk = new AppSettings(); }
        return new AppSettings
        {
        BaseUrl = BaseUrl.Trim(),
        Language = LanguagePreference(UiLanguage),
        Appearance = SelectedAppearance?.Value ?? "system",
        CueWidth = SelectedCueWidth?.Value ?? "regular",
        CueAlwaysOpen = CueAlwaysOpen,
        CueSurface = SelectedCueSurface?.Value ?? "island",
        PresenterSlideNotes = PresenterSlideNotes,
        SubtitleBarX = SubtitleBarX,
        SubtitleBarY = SubtitleBarY,
        CapturePrivacy = CapturePrivacy,
        TeleprompterHideBackground = TeleprompterHideBackground,
        CueScreen = CueScreen,
        SubtitleWidth = Math.Clamp(SubtitleWidth, 520, 1400),
        SubtitleFontSize = Math.Clamp(SubtitleFontSize, 16, 44),
        SubtitleShowTranslation = SubtitleShowTranslation,
        AutoCheckUpdates = AutoCheckUpdates,
        CloudStt = SelectedCloudStt?.Value ?? "",
        CloudSttModel = CloudSttModel.Trim(),
        CloudSttLanguage = SelectedCloudLanguage?.Value ?? "auto",
        CloudSttEndpoint = CloudSttEndpointValue.Trim(),
        Model = Model.Trim(),
        NotesModel = NotesModel.Trim(),
        // E27: API reasoning strength belongs to the active preset (Codex keeps its own picker).
        ReasoningEffort = UseCodex
            ? ReasoningEffort
            : (SelectedApiEffort?.Value == "default" ? "" : SelectedApiEffort?.Value ?? ""),
        LocalModel = SelectedModelChoice?.Id ?? "xasr480",
        SecondPass = SecondPass,
        CaptureSystem = CaptureSystem,
        CaptureMicrophone = CaptureMicrophone,
        MicrophoneGain = MicrophoneGain,
        SystemDeviceId = SelectedSystemDevice?.Id,
        MicrophoneDeviceId = SelectedMicrophoneDevice?.Id,
        Vocabulary = Vocabulary,
        LastMeetingTitle = state.MeetingTitle == "新会议" ? LastMeetingTitle : state.MeetingTitle,
        AutomaticAnswers = AutomaticAnswers,
        AutoSummarize = AutoSummarize,
        UseSemanticRetrieval = UseSemanticRetrieval,
        WebSearch = WebSearch,
        SeparateSpeakers = SeparateSpeakers,
        TranslateSpeech = TranslateSpeech,
        UseMaterials = UseMaterials,
        UseCodex = UseCodex,
        CodexModel = CodexModel.Trim(),
        // E27: saved providers, the active preset and the GitHub access settings.
        ApiPresets = EditedApiPresets(),
        ActiveApiPresetId = selectedApiPreset?.Id ?? "",
        CodexFastMode = CodexFastMode ? true : null,
        UpdateRepository = GithubRepository.Trim(),
        // E23: how documents are read (Mac DocumentModelSettings keys).
        DocumentOcrProvider = SelectedDocumentProvider?.Value ?? "system",
        DocumentProviderWasSelected = documentProviderWasSelected,
        OcrFallbackToVision = DocumentFallbackToVision,
        CodexOcrModel = CodexOcrModel.Trim().Length == 0 ? "gpt-6-luna" : CodexOcrModel.Trim(),
        CodexOcrEffort = CodexOcrEffort is "low" or "medium" or "high" or "xhigh" ? CodexOcrEffort : "medium",
        CodexOcrTimeout = double.TryParse(CodexOcrTimeout, out var codexOcrSeconds)
            ? Math.Clamp(codexOcrSeconds, 30, 3600)
            : 600,
        PresenterMode = PresenterMode,
        PresenterSlidesScreen = SelectedSlidesScreen?.Id ?? PresenterSlidesScreen,
        CompanionRelayUrl = CompanionRelayUrl.Trim(),
        OnboardingCompletedVersion = OnboardingCompletedVersion,
        AnswerLanguage = AnswerLanguage,
        AnswerGloss = AnswerGloss,
        AutoTranslateForeign = AutoTranslateForeign,
        LiveTranslation = LiveTranslation,
        WebSearchMode = WebSearchMode,
        FollowSlides = FollowSlides,
        InsightScreenWatch = InsightScreenWatch,
        InsightScreenInterval = Math.Max(1, InsightScreenInterval),
        InsightFollowSlides = InsightFollowSlides,
        InsightScreenSource = InsightScreenSource,
        InsightScreenRegion = InsightScreenRegion,
        MeetingAutoEndMode = SelectedAutoEndMode?.Value ?? "prompt",
        MeetingAutoEndMinutes = int.TryParse(AutoEndMinutes, out var minutes) ? Math.Clamp(minutes, 1, 240) : MeetingEndWatch.Config.DefaultMinutes,
        CaptureProcessId = SelectedProcess?.Id ?? 0,
        CaptureProcessStartTicks = SelectedProcess?.StartTicks ?? 0,
        Shortcuts = new Dictionary<string, string>(state.ShortcutOverrides),
        ContextSidebarHidden = state.ContextSidebarHidden,
        // G5b: fields this page has no control for yet keep their on-disk / Mac-key values.
        TeleprompterOpacity = disk.TeleprompterOpacity,
        TeleprompterFontSize = Math.Clamp(TeleprompterFontSize, 14, 36),
        ReducedEffects = disk.ReducedEffects,
        SceneDefaults = disk.SceneDefaults,
        AdditionalProperties = disk.AdditionalProperties
    };
    }

    public async Task SaveAsync()
    {
        var settings = Build();
        if (settings.BaseUrl.Length > 0) _ = ChatCompletionClient.Endpoint(settings.BaseUrl);
        // A failed read leaves ApiKey empty; never let "save settings" delete the stored key.
        if (!apiKeyReadFailed || ApiKey.Trim().Length > 0)
        {
            state.SettingsStore.SaveApiKey(ApiKey.Trim());
            apiKeyReadFailed = false;
        }
        await state.SettingsStore.SaveAsync(settings);
        // E27: the key belongs to the active API preset (Mac keeps one Keychain account per preset).
        SaveActivePresetSecret();
        // G5b: apply the Mac reducedEffects key to the motion tokens immediately after a save.
        Motion.AppReducedEffects = settings.ReducedEffects;
        HasApiKey = ApiKey.Trim().Length > 0;
        KeyStatus = HasApiKey ? UiText.T("密钥已保存（当前 Windows 用户加密）") : UiText.T("尚未保存密钥；本机服务可不填。");
        LastMeetingTitle = settings.LastMeetingTitle;
        savedHintAt = DateTime.Now;
        OnPropertyChanged(nameof(SavedHint));
    }

    public async Task RefreshDevicesAsync()
    {
        var (system, microphone) = await Task.Run(() => (AudioCaptureService.Devices(true), AudioCaptureService.Devices(false)));
        var preferred = Build();
        SystemDevices.Clear();
        foreach (var device in system) SystemDevices.Add(new DeviceChoice(device.Id, device.Name));
        MicrophoneDevices.Clear();
        foreach (var device in microphone) MicrophoneDevices.Add(new DeviceChoice(device.Id, device.Name));
        SelectedSystemDevice = SystemDevices.FirstOrDefault(x => x.Id == preferred.SystemDeviceId) ?? SystemDevices.FirstOrDefault();
        SelectedMicrophoneDevice = MicrophoneDevices.FirstOrDefault(x => x.Id == preferred.MicrophoneDeviceId) ?? MicrophoneDevices.FirstOrDefault();
    }

    public void RefreshProcesses()
    {
        var choices = new List<ProcessChoice> { new(0, 0, "整个输出设备混音") }; // key, localized by ToString
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.Id != Environment.ProcessId && process.MainWindowHandle != IntPtr.Zero && process.MainWindowTitle.Length > 0)
                        choices.Add(new(process.Id, process.StartTime.ToUniversalTime().Ticks, process.ProcessName + " · " + process.MainWindowTitle));
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
            }
        }
        var previous = SelectedProcess?.Id ?? 0;
        Processes.Clear();
        foreach (var choice in choices) Processes.Add(choice);
        SelectedProcess = Processes.FirstOrDefault(x => x.Id == previous) ?? Processes[0];
    }
}
