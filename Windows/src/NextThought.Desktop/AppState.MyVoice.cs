using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop;

public partial class AppState
{
    /// <summary>E12: sentences read during my-voice enrollment, copied from Mac MyVoice.swift.</summary>
    private static readonly string[] VoicePrompts =
    [
            "今天上午我们先把上周的进展过一遍，然后讨论接下来两周的安排。",
            "这个问题我的理解是，先确认需求，再决定用哪种方案去做。",
            "Let me walk you through what I did on the project and why.",
            "如果时间允许，我想再补充一下数据和风险这两部分。"
    ];

    private const double TakeSeconds = 7.0;
    private const double MinSpeechSeconds = 3.0;
    private readonly List<float[]> myVoiceTakes = [];
    private VoiceProfile? myVoiceProfile;

    [ObservableProperty] private bool myVoiceSetupVisible;
    [ObservableProperty] private bool myVoiceRecording;
    [ObservableProperty] private double myVoiceProgress;
    [ObservableProperty] private string myVoicePrompt = "";
    [ObservableProperty] private string myVoiceMessage = "";
    [ObservableProperty] private string myVoiceStatus = "";
    [ObservableProperty] private bool myVoiceHasProfile;
    [ObservableProperty] private bool myVoiceModelAvailable;
    [ObservableProperty] private bool myVoiceIsWorking;

    // ---- E12/Mac MyVoiceSettings + MyVoiceSetupPanel surface ----------------------------

    public int MyVoiceTakeIndex => Math.Min(myVoiceTakes.Count + 1, VoicePrompts.Length);
    public int MyVoiceTotalTakes => VoicePrompts.Length;

    public string MyVoiceTakeHint =>
        UiText.Format("第 %lld / %lld 句，点“开始”后用平时开会的声音读出来", MyVoiceTakeIndex, MyVoiceTotalTakes);

    public string MyVoiceRecordLabel => MyVoiceRecording
        ? UiText.T("正在听…")
        : MyVoiceIsWorking ? UiText.T("处理中…") : UiText.T("开始");

    public bool MyVoiceCanStart => MyVoiceSetupVisible && !MyVoiceRecording && !MyVoiceIsWorking && MyVoiceModelAvailable;
    public bool MyVoiceCanEnroll => !Recording && MyVoiceModelAvailable;
    public bool MyVoiceMeetingRunning => Recording;
    public bool MyVoiceModelMissing => !MyVoiceModelAvailable;
    public bool MyVoiceOldModel => myVoiceProfile is { } profile && profile.Model != SpeakerEmbedder.ModelId;

    public string MyVoiceSummaryLine => MyVoiceHasProfile
        ? UiText.T("已录入") + " · " + (myVoiceProfile?.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd") ?? "")
        : UiText.T("还没有录入");

    partial void OnMyVoiceRecordingChanged(bool value) => NotifyMyVoiceUi();
    partial void OnMyVoiceSetupVisibleChanged(bool value) => NotifyMyVoiceUi();
    partial void OnMyVoiceIsWorkingChanged(bool value) => NotifyMyVoiceUi();
    partial void OnMyVoiceHasProfileChanged(bool value) => NotifyMyVoiceUi();
    partial void OnMyVoiceStatusChanged(string value) => NotifyMyVoiceUi();
    partial void OnMyVoiceMessageChanged(string value) => OnPropertyChanged(nameof(HasMyVoiceMessage));

    public bool HasMyVoiceMessage => MyVoiceMessage.Length > 0;

    private void NotifyMyVoiceUi()
    {
        OnPropertyChanged(nameof(MyVoiceTakeIndex));
        OnPropertyChanged(nameof(MyVoiceTakeHint));
        OnPropertyChanged(nameof(MyVoiceRecordLabel));
        OnPropertyChanged(nameof(MyVoiceCanStart));
        OnPropertyChanged(nameof(MyVoiceCanEnroll));
        OnPropertyChanged(nameof(MyVoiceMeetingRunning));
        OnPropertyChanged(nameof(MyVoiceModelMissing));
        OnPropertyChanged(nameof(MyVoiceOldModel));
        OnPropertyChanged(nameof(MyVoiceSummaryLine));
    }

    private string MyVoiceFile => Path.Combine(Paths.Root, "Voiceprint", "me.json");

    private void LoadMyVoice()
    {
        MyVoiceModelAvailable = new SpeakerModelStore(Paths).IsInstalled;
        myVoiceProfile = null;
        try
        {
            if (File.Exists(MyVoiceFile))
                myVoiceProfile = JsonSerializer.Deserialize<VoiceProfile>(File.ReadAllText(MyVoiceFile), JsonDefaults.Options);
        }
        catch { myVoiceProfile = null; }
        MyVoiceHasProfile = myVoiceProfile is not null;
        MyVoiceStatus = myVoiceProfile is null
            ? UiText.T("还没有录入")
            : myVoiceProfile.Model == SpeakerEmbedder.ModelId
                ? UiText.T("已录入 · ") + myVoiceProfile.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd")
                : UiText.T("已录入（来自旧模型的声纹，需要重新录入）");
        MyVoicePrompt = VoicePrompts[0];
    }

    [RelayCommand]
    private void BeginMyVoiceSetup()
    {
        if (Recording) { SetStatus("会议进行中不能录入。"); return; }
        if (!MyVoiceModelAvailable) { MyVoiceMessage = "先准备声纹组件（约 26 MB），再录入我的声音。"; return; }
        myVoiceTakes.Clear();
        MyVoiceMessage = "";
        MyVoiceSetupVisible = true;
        MyVoiceProgress = 0;
        MyVoicePrompt = VoicePrompts[0];
    }

    [RelayCommand]
    private void CancelMyVoiceSetup()
    {
        MyVoiceSetupVisible = false;
        MyVoiceRecording = false;
        MyVoiceProgress = 0;
        MyVoiceMessage = "";
        myVoiceTakes.Clear();
    }

    [RelayCommand]
    private async Task RecordMyVoiceTakeAsync()
    {
        if (!MyVoiceSetupVisible || MyVoiceRecording || Working) return;
        if (!MyVoiceModelAvailable) { MyVoiceMessage = "先准备声纹组件（约 26 MB），再录入我的声音。"; return; }
        MyVoiceRecording = true;
        MyVoiceMessage = "";
        try
        {
            var samples = await VoiceRecorder.RecordAsync(TimeSpan.FromSeconds(TakeSeconds), fraction => MyVoiceProgress = fraction);
            MyVoiceProgress = 0;
            var floats = samples.Select(value => value / 32768f).ToArray();
            var trimmed = AudioTimeline.TrimSilence(floats);
            if (trimmed.Length / 16000.0 < MinSpeechSeconds)
            {
                MyVoiceMessage = "没听清，声音有点小或太短。靠近麦克风，再读一次。";
                return;
            }
            var pcm = trimmed.Select(value => (short)Math.Clamp((int)Math.Round(value * 32768), short.MinValue, short.MaxValue)).ToArray();
            MyVoiceIsWorking = true;
            try
            {
                using var embedder = new SpeakerEmbedder(Paths);
                var vector = embedder.Embed(pcm);
                if (vector is null)
                {
                    MyVoiceMessage = "没听清，再读一次。";
                    return;
                }
                myVoiceTakes.Add(vector);
                if (myVoiceTakes.Count >= VoicePrompts.Length) FinishMyVoiceSetup();
                else MyVoicePrompt = VoicePrompts[myVoiceTakes.Count];
            }
            finally { MyVoiceIsWorking = false; NotifyMyVoiceUi(); }
        }
        catch (Exception ex) { MyVoiceMessage = "录入失败：" + ex.Message; }
        finally { MyVoiceRecording = false; }
    }

    private void FinishMyVoiceSetup()
    {
        var result = VoiceEnrollment.Make(myVoiceTakes);
        if (result.Profile is null)
        {
            MyVoiceMessage = result.Failure == VoiceEnrollmentFailure.TooFewClips
                ? "录到的声音不够，请重新来一遍。"
                : "几次录音听起来不像同一个人，请在安静的地方，只由你一个人重新来一遍。";
            myVoiceTakes.Clear();
            MyVoicePrompt = VoicePrompts[0];
            return;
        }
        var profile = result.Profile with { Model = SpeakerEmbedder.ModelId };
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MyVoiceFile)!);
            File.WriteAllText(MyVoiceFile, JsonSerializer.Serialize(profile, JsonDefaults.Options));
            myVoiceProfile = profile;
            MyVoiceHasProfile = true;
            MyVoiceSetupVisible = false;
            MyVoiceStatus = "已录入 · " + profile.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd");
            MyVoiceMessage = "";
            myVoiceTakes.Clear();
            SaveSpeakerSettingsEnabled();
        }
        catch (Exception ex) { MyVoiceMessage = "保存失败：" + ex.Message; myVoiceTakes.Clear(); }
    }

    private void SaveSpeakerSettingsEnabled()
    {
        try
        {
            if (!Settings.SeparateSpeakers) Settings.SeparateSpeakers = true;
        }
        catch { }
    }

    [RelayCommand]
    private void DeleteMyVoice()
    {
        try { if (File.Exists(MyVoiceFile)) File.Delete(MyVoiceFile); } catch { }
        myVoiceProfile = null;
        MyVoiceHasProfile = false;
        MyVoiceStatus = UiText.T("还没有录入");
        MyVoiceMessage = "";
    }

}
