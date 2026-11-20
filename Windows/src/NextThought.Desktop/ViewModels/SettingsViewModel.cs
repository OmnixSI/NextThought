using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Security.Cryptography;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop;

/// <summary>
/// Editable draft of <see cref="AppSettings"/> plus the device, process and model choices
/// the settings page offers. Mirrors the settings surface of the Mac app; the keys and
/// defaults are the ones the Windows baseline already persists.
/// </summary>
public partial class SettingsViewModel(AppState state) : ObservableObject
{
    private readonly AppState state = state;

    public static IReadOnlyList<ModelChoice> ModelChoices { get; } =
    [
        new("xasr480", "X-ASR 中英 · 480 ms（推荐）"),
        new("xasr160", "X-ASR 中英 · 160 ms")
    ];

    public static IReadOnlyList<EffortChoice> EffortChoices { get; } =
    [
        new("", "默认，不指定"),
        new("none", "none（关闭思考）"),
        new("low", "low"),
        new("medium", "medium"),
        new("high", "high")
    ];

    public IReadOnlyList<ProviderPreset> ProviderPresets { get; } =
    [
        new("OpenAI", "https://api.openai.com/v1", "gpt-5.2"),
        new("DeepSeek", "https://api.deepseek.com/v1", "deepseek-chat"),
        new("OpenRouter", "https://openrouter.ai/api/v1", "openai/gpt-4o-mini"),
        new("Ollama (local)", "http://127.0.0.1:11434/v1", "qwen2.5:7b"),
        new("LM Studio (local)", "http://127.0.0.1:1234/v1", "local-model"),
        new(UiText.T("自定义"), "", "")
    ];

    [ObservableProperty] private ProviderPreset? selectedProviderPreset;

    partial void OnSelectedProviderPresetChanged(ProviderPreset? value)
    {
        if (value is null || value.BaseUrl.Length == 0) return;
        BaseUrl = value.BaseUrl;
        if (value.Model.Length > 0) Model = value.Model;
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
    [ObservableProperty] private bool automaticAnswers;
    [ObservableProperty] private bool autoSummarize;
    [ObservableProperty] private bool useSemanticRetrieval;
    [ObservableProperty] private bool separateSpeakers;
    [ObservableProperty] private bool translateSpeech;
    [ObservableProperty] private bool useMaterials;
    [ObservableProperty] private bool useCodex;
    [ObservableProperty] private bool webSearch;
    [ObservableProperty] private string codexModel = "";
    public IReadOnlyList<string> UiLanguages { get; } = ["中文", "English"];
    [ObservableProperty] private string uiLanguage = "中文";

    public IReadOnlyList<CloudSttChoice> CloudSttChoices { get; } =
    [
        new("", "本地（默认）"),
        new("soniox", "Soniox · 中英混说"),
        new("openai", "OpenAI Realtime"),
        new("deepgram", "Deepgram Nova-3")
    ];
    public IReadOnlyList<CloudLanguageChoice> CloudSttLanguages { get; } =
    [
        new("auto", "自动"),
        new("zh", "中文"),
        new("en", "英文")
    ];
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

    public IReadOnlyList<AppearanceOption> AppearanceChoices { get; } =
    [
        new("system", "跟随系统"),
        new("light", "浅色"),
        new("dark", "深色")
    ];
    [ObservableProperty] private AppearanceOption? selectedAppearance;
    public IReadOnlyList<CueWidthOption> CueWidthChoices { get; } =
    [
        new("narrow", "窄"),
        new("regular", "标准"),
        new("wide", "宽")
    ];
    [ObservableProperty] private CueWidthOption? selectedCueWidth;
    [ObservableProperty] private bool cueAlwaysOpen;
    [ObservableProperty] private double? subtitleBarX;
    [ObservableProperty] private double? subtitleBarY;
    [ObservableProperty] private int subtitleWidth = 920;
    [ObservableProperty] private int subtitleFontSize = 22;
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
    [ObservableProperty] private bool answerGloss;
    [ObservableProperty] private bool followSlides;
    [ObservableProperty] private bool insightScreenWatch;
    [ObservableProperty] private double insightScreenInterval = 1;
    [ObservableProperty] private bool insightFollowSlides = true;
    [ObservableProperty] private string insightScreenSource = "display";
    [ObservableProperty] private string insightScreenRegion = "";
    public IReadOnlyList<string> InsightScreenSources { get; } = ["display", "window", "region"];
    public IReadOnlyList<string> InsightSourceLabels { get; } = ["显示器", "窗口", "区域"];
    public IReadOnlyList<double> InsightIntervals { get; } = [1, 2, 4, 8, 15];
    public string SelectedInsightSourceLabel
    {
        get => InsightScreenSource switch { "window" => "窗口", "region" => "区域", _ => "显示器" };
        set
        {
            InsightScreenSource = value switch { "窗口" => "window", "区域" => "region", _ => "display" };
            OnPropertyChanged();
        }
    }
    [ObservableProperty] private bool autoTranslateForeign;
    [ObservableProperty] private string tavilyKey = "";
    [ObservableProperty] private string tavilyKeyStatus = "密钥只保存在当前 Windows 用户；也可以继续用 TAVILY_API_KEY 环境变量。";

    public ObservableCollection<ScreenChoice> ScreenChoices { get; } = [];

    public string PresenterSummary => PresenterLayout.Summary(DisplayCatalog.Displays, PresenterSlidesScreen, PresenterMode);

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
    [ObservableProperty] private string savedHint = "";

    public ObservableCollection<DeviceChoice> SystemDevices { get; } = [];
    public ObservableCollection<DeviceChoice> MicrophoneDevices { get; } = [];
    public ObservableCollection<ProcessChoice> Processes { get; } = [];
    public IReadOnlyList<ModelChoice> ModelChoicesList => ModelChoices;
    public IReadOnlyList<EffortChoice> EffortChoicesList => EffortChoices;
    public IReadOnlyList<AutoEndChoice> AutoEndModes { get; } =
    [
        new("prompt", "静音后先提示，再结束（推荐）"),
        new("end", "静音后直接结束"),
        new("off", "不自动结束")
    ];
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
    }

    partial void OnSecondPassChanged(bool value) => state.RefreshModelState();
    partial void OnCaptureSystemChanged(bool value) => state.RefreshModelState();
    partial void OnCaptureMicrophoneChanged(bool value) => state.RefreshModelState();

    public void LoadFromStore()
    {
        AppSettings settings;
        try { settings = state.SettingsStore.Load(); }
        catch { settings = new AppSettings(); }
        UiLanguage = UiText.IsEnglish(settings.Language) ? "English" : "中文";
        SelectedAppearance = AppearanceChoices.FirstOrDefault(choice => choice.Value == settings.Appearance) ?? AppearanceChoices[0];
        SelectedCueWidth = CueWidthChoices.FirstOrDefault(choice => choice.Value == settings.CueWidth) ?? CueWidthChoices[1];
        CueAlwaysOpen = settings.CueAlwaysOpen;
        SubtitleBarX = settings.SubtitleBarX;
        SubtitleBarY = settings.SubtitleBarY;
        SubtitleWidth = Math.Clamp(settings.SubtitleWidth, 520, 1400);
        SubtitleFontSize = Math.Clamp(settings.SubtitleFontSize, 16, 36);
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
            KeyStatus = HasApiKey ? "密钥已保存（当前 Windows 用户加密）" : "尚未保存密钥；本机服务可不填。";
        }
        catch (CryptographicException)
        {
            apiKeyReadFailed = true;
            HasApiKey = false;
            KeyStatus = "原凭据无法由当前用户解密，请重新输入密钥。";
        }
        catch (Exception)
        {
            apiKeyReadFailed = true;
            HasApiKey = false;
            KeyStatus = "密钥读取失败，已保留原密钥；如需更换请直接输入新密钥。";
        }
        SelectedProcess = Processes.FirstOrDefault();
        RefreshScreens();
    }

    public AppSettings Build() => new()
    {
        BaseUrl = BaseUrl.Trim(),
        Language = UiLanguage == "English" ? "en" : "zh",
        Appearance = SelectedAppearance?.Value ?? "system",
        CueWidth = SelectedCueWidth?.Value ?? "regular",
        CueAlwaysOpen = CueAlwaysOpen,
        SubtitleBarX = SubtitleBarX,
        SubtitleBarY = SubtitleBarY,
        SubtitleWidth = Math.Clamp(SubtitleWidth, 520, 1400),
        SubtitleFontSize = Math.Clamp(SubtitleFontSize, 16, 36),
        SubtitleShowTranslation = SubtitleShowTranslation,
        AutoCheckUpdates = AutoCheckUpdates,
        CloudStt = SelectedCloudStt?.Value ?? "",
        CloudSttModel = CloudSttModel.Trim(),
        CloudSttLanguage = SelectedCloudLanguage?.Value ?? "auto",
        CloudSttEndpoint = CloudSttEndpointValue.Trim(),
        Model = Model.Trim(),
        NotesModel = NotesModel.Trim(),
        ReasoningEffort = ReasoningEffort,
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
        PresenterMode = PresenterMode,
        PresenterSlidesScreen = SelectedSlidesScreen?.Id ?? PresenterSlidesScreen,
        CompanionRelayUrl = CompanionRelayUrl.Trim(),
        OnboardingCompletedVersion = OnboardingCompletedVersion,
        AnswerLanguage = AnswerLanguage,
        AnswerGloss = AnswerGloss,
        AutoTranslateForeign = AutoTranslateForeign,
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
        ContextSidebarHidden = state.ContextSidebarHidden
    };

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
        HasApiKey = ApiKey.Trim().Length > 0;
        KeyStatus = HasApiKey ? "密钥已保存（当前 Windows 用户加密）" : "尚未保存密钥；本机服务可不填。";
        LastMeetingTitle = settings.LastMeetingTitle;
        SavedHint = "已保存 · " + DateTime.Now.ToString("HH:mm:ss");
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
        var choices = new List<ProcessChoice> { new(0, 0, "整个输出设备混音") };
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
