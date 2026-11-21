using System.Text.Json;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop;

public partial class AppState
{
    [ObservableProperty] private string coachSummaryText = "";

    // ---- E10 看镜头提醒 (Mac LiveCoach + GazeCamera) ------------------------------------------

    private GazeCamera? gazeCamera;
    private readonly GazeAttention gazeAttention = new();
    private bool gazeEventsHooked;

    /// <summary>Camera status line under the settings card: idle, running, denied or calibrated.</summary>
    [ObservableProperty] private string gazeStatusText = "";

    /// <summary>Windows privacy settings (or no device) refuse the camera; the card links to them.</summary>
    [ObservableProperty] private bool isGazePermissionDenied;

    /// <summary>Mac LiveCoach.shouldLookUp: away from the lens mid-sentence long enough to cue.</summary>
    [ObservableProperty] private bool gazeShouldLookUp;

    [ObservableProperty] private bool gazeCalibrating;

    /// <summary>Share of tracked time looking at the lens, once ten seconds of frames were seen.</summary>
    [ObservableProperty] private double? gazeContactRatio;

    /// <summary>The pose learned by the last calibration is on disk for this seat/camera.</summary>
    [ObservableProperty] private bool gazeCalibrated;

    public bool IsGazeWatching => gazeCamera?.IsRunning == true;

    private static double GazeSensitivityTolerance(string sensitivity) => sensitivity switch
    {
        "relaxed" => 0.17,
        "strict" => 0.09,
        _ => 0.12
    };

    private bool CoachWatchesGazeSaved()
    {
        try { return SettingsStore.LoadMacValue("coachWatchesGaze") is true; }
        catch { return false; }
    }

    private string CoachGazeSensitivitySaved()
    {
        try
        {
            return SettingsStore.LoadMacValue("coachGazeSensitivity") is string { Length: > 0 } sensitivity
                ? sensitivity
                : "standard";
        }
        catch { return "standard"; }
    }

    /// <summary>Meeting start: honour the saved 留意眼神 setting (off by default).</summary>
    public void ApplyCoachGazeSettings()
    {
        if (CoachWatchesGazeSaved() && Recording) _ = StartGazeWatchingAsync();
        else if (!CoachWatchesGazeSaved()) _ = StopGazeWatchingAsync();
    }

    /// <summary>Settings card: persist the Mac keys at once (Mac toggles apply immediately).</summary>
    public async Task ApplyCoachGazeDraftAsync(bool watchesGaze, string sensitivity)
    {
        try
        {
            await SettingsStore.SaveMacPreferencesAsync(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["coachWatchesGaze"] = watchesGaze,
                ["coachGazeSensitivity"] = sensitivity
            });
        }
        catch { }
        if (watchesGaze) await StartGazeWatchingAsync();
        else await StopGazeWatchingAsync();
    }

    private async Task StartGazeWatchingAsync()
    {
        if (!Recording) return;
        var assets = FaceLandmarker.FindAssetDirectory();
        if (assets is null)
        {
            GazeStatusText = UiText.T("没有找到看镜头模型（assets/face-landmarker）。");
            return;
        }
        gazeCamera ??= new GazeCamera(assets);
        if (!gazeEventsHooked)
        {
            gazeCamera.Reading += OnGazeReading;
            gazeCamera.RunningChanged += OnGazeRunningChanged;
            gazeCamera.CalibrationCompleted += OnGazeCalibrationCompleted;
            gazeEventsHooked = true;
        }
        var baseline = new GazeBaselineStore(Paths).Load();
        gazeCamera.RestoreBaseline(baseline);
        GazeCalibrated = baseline is not null || gazeCamera.IsCalibrated;
        gazeCamera.Tolerance = GazeSensitivityTolerance(CoachGazeSensitivitySaved());
        var started = await gazeCamera.StartAsync();
        OnPropertyChanged(nameof(IsGazeWatching));
        IsGazePermissionDenied = !started && (gazeCamera.PermissionDenied ||
            WindowsPermissions.Camera() == WindowsPermissions.PermissionState.Denied);
        GazeStatusText = started
            ? (GazeCalibrated ? UiText.T("正在看镜头（已校准）") : UiText.T("正在看镜头；先校准一次更准。"))
            : (IsGazePermissionDenied
                ? UiText.T("摄像头还没有允许。")
                : UiText.T("摄像头打不开；本机没有可用摄像头。"));
    }

    public async Task StopGazeWatchingAsync()
    {
        if (gazeCamera is not null) await gazeCamera.StopAsync();
        OnPropertyChanged(nameof(IsGazeWatching));
        gazeAttention.Reset();
        GazeShouldLookUp = false;
        GazeContactRatio = null;
        GazeCalibrating = false;
        if (gazeCamera is { PermissionDenied: false }) GazeStatusText = "";
    }

    private void OnGazeRunningChanged(bool running)
    {
        if (!running && GazeCameraIsDenied())
            IsGazePermissionDenied = true;
    }

    private bool GazeCameraIsDenied() =>
        gazeCamera?.PermissionDenied == true || WindowsPermissions.Camera() == WindowsPermissions.PermissionState.Denied;

    private void OnGazeReading(GazeReading reading, double? contactRatio)
    {
        var cue = gazeAttention.Ingest(reading, DateTimeOffset.UtcNow);
        Dispatcher.UIThread.Post(() =>
        {
            GazeContactRatio = contactRatio;
            if (GazeShouldLookUp == cue) return;
            GazeShouldLookUp = cue;
            OnPropertyChanged(nameof(CueIslandCoachHint));
            OnPropertyChanged(nameof(CueIslandCoachSymbol));
        });
    }

    private void OnGazeCalibrationCompleted(GazeSignature baseline)
    {
        new GazeBaselineStore(Paths).Save(baseline);
        Dispatcher.UIThread.Post(() =>
        {
            GazeCalibrated = true;
            GazeStatusText = UiText.T("已校准");
        });
    }

    /// <summary>Mac LiveCoach.ingest: one of our own finished lines keeps the speaking window open.</summary>
    public void NoteGazeSpeech() => gazeAttention.Heard(DateTimeOffset.UtcNow);

    [RelayCommand]
    private async Task CalibrateGazeAsync()
    {
        if (gazeCamera is not { IsRunning: true })
        {
            GazeStatusText = UiText.T("先打开「留意眼神」并允许摄像头。");
            return;
        }
        GazeCalibrating = true;
        GazeStatusText = UiText.T("正在校准，请看着摄像头…");
        var success = await gazeCamera.CalibrateAsync(TimeSpan.FromSeconds(2));
        GazeCalibrating = false;
        GazeStatusText = success
            ? UiText.T("已校准")
            : UiText.T("没有看清眼睛，请正对摄像头再试一次。");
    }

    // ---- coach summary ------------------------------------------------------------------------

    /// <summary>E10: fold the meeting transcript into the coach summary and keep it beside the notes.</summary>
    public void BuildCoachSummary()
    {
        if (CurrentSession is not { } meta) return;
        var accumulator = new CoachSummaryAccumulator();
        var rows = Transcript.ToList();
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var start = Math.Max(0, (row.Date - meta.StartedAt).TotalSeconds);
            var end = index + 1 < rows.Count
                ? Math.Max(start, (rows[index + 1].Date - meta.StartedAt).TotalSeconds)
                : start + Math.Max(2, row.Text.Length / 4.0);
            accumulator.Ingest(new SpokenSegment(row.Speaker == "me", start, end, row.Text));
        }
        var summary = accumulator.Summary();
        if (summary.IsEmpty) { CoachSummaryText = ""; return; }
        CoachSummaryText = Format(summary);
        try
        {
            var path = Path.Combine(Sessions.Folder(meta.Id), "coach-summary.json");
            File.WriteAllText(path, JsonSerializer.Serialize(summary, JsonDefaults.Options));
        }
        catch { }
    }


    private void LoadCoachSummary(Guid sessionId)
    {
        try
        {
            var path = Path.Combine(Sessions.Folder(sessionId), "coach-summary.json");
            if (!File.Exists(path)) { CoachSummaryText = ""; return; }
            var summary = JsonSerializer.Deserialize<CoachSummary>(File.ReadAllText(path), JsonDefaults.Options);
            CoachSummaryText = summary is null || summary.IsEmpty ? "" : Format(summary);
        }
        catch { CoachSummaryText = ""; }
    }

    private static string Format(CoachSummary summary)
    {
        var parts = new List<string>();
        if (summary.Pace is { } pace) parts.Add(UiText.Format("语速 %.0f 单位/分钟", pace));
        if (summary.TalkShare is { } share) parts.Add(UiText.Format("发言占比 %.0f%%", share * 100));
        if (summary.LongestMonologue >= CoachSummaryAccumulator.MinimumMonologue) parts.Add(UiText.Format("最长独白 %.0f 秒", summary.LongestMonologue));
        if (summary.EyeContact is { } contact) parts.Add(UiText.Format("看镜头 %.0f%%", contact * 100));
        return string.Join(" · ", parts);
    }
}
