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

    [ObservableProperty] private string baseUrl = "https://api.openai.com/v1";
    [ObservableProperty] private string model = "";
    [ObservableProperty] private string notesModel = "";
    [ObservableProperty] private string reasoningEffort = "";
    [ObservableProperty] private string apiKey = "";
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
    [ObservableProperty] private string codexModel = "";
    [ObservableProperty] private string autoEndMode = "prompt";
    private string autoEndMinutes = "10";

    // 双屏演讲（PresenterMode.swift）：开关、幻灯片所在屏、以及给设置页看的一行说明。
    [ObservableProperty] private bool presenterMode;
    [ObservableProperty] private string presenterSlidesScreen = "";
    [ObservableProperty] private ScreenChoice? selectedSlidesScreen;

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
        CodexModel = settings.CodexModel;
        PresenterMode = settings.PresenterMode;
        PresenterSlidesScreen = settings.PresenterSlidesScreen;
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
            HasApiKey = false;
            KeyStatus = "原凭据无法由当前用户解密，请重新输入密钥。";
        }
        catch (Exception)
        {
            HasApiKey = false;
        }
        SelectedProcess = Processes.FirstOrDefault();
        RefreshScreens();
    }

    public AppSettings Build() => new()
    {
        BaseUrl = BaseUrl.Trim(),
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
        SeparateSpeakers = SeparateSpeakers,
        TranslateSpeech = TranslateSpeech,
        UseMaterials = UseMaterials,
        UseCodex = UseCodex,
        CodexModel = CodexModel.Trim(),
        PresenterMode = PresenterMode,
        PresenterSlidesScreen = SelectedSlidesScreen?.Id ?? PresenterSlidesScreen,
        MeetingAutoEndMode = SelectedAutoEndMode?.Value ?? "prompt",
        MeetingAutoEndMinutes = int.TryParse(AutoEndMinutes, out var minutes) ? Math.Clamp(minutes, 1, 240) : MeetingEndWatch.Config.DefaultMinutes,
        CaptureProcessId = SelectedProcess?.Id ?? 0,
        CaptureProcessStartTicks = SelectedProcess?.StartTicks ?? 0
    };

    public async Task SaveAsync()
    {
        var settings = Build();
        if (settings.BaseUrl.Length > 0) _ = ChatCompletionClient.Endpoint(settings.BaseUrl);
        state.SettingsStore.SaveApiKey(ApiKey.Trim());
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
