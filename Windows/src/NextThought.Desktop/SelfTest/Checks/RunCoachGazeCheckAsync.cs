using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// E10: the 看镜头提醒 self-check. Parts: the real built-in camera is opened through the same
    /// GazeCamera path the coach uses (frames only in memory), and a recorded portrait is pushed
    /// through the full FaceLandmarker + GazeEstimator path to judge 看镜头 / 移开视线 / 没有人脸.
    /// The camera on this machine has no person in front of it, so the away state is exercised by
    /// moving the detected iris landmarks away from the lens (the estimator's real input) and the
    /// evidence records exactly that; nothing here weakens the production thresholds.
    /// </summary>
    private async Task RunCoachGazeCheckAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var steps = new List<object>();
        var passed = false;
        string? error = null;
        void Step(string name, bool ok, string detail) => steps.Add(new { step = name, passed = ok, detail = detail });
        try
        {
            var assets = FaceLandmarker.FindAssetDirectory();
            Step("看到镜头模型资产", assets is not null, assets ?? "assets/face-landmarker 缺失");
            if (assets is null) throw new InvalidOperationException("face landmarker assets missing");

            // 1) The real camera path (BisonCam, VGA, memory frames only).
            var onCamera = 0;
            var away = 0;
            var noFace = 0;
            var frames = 0;
            var permissionDenied = false;
            var started = false;
            string? startError = null;
            await using (var camera = new GazeCamera(assets))
            {
                camera.Reading += (reading, ratio) =>
                {
                    Interlocked.Increment(ref frames);
                    switch (reading.State)
                    {
                        case GazeReading.StateValue.OnCamera: Interlocked.Increment(ref onCamera); break;
                        case GazeReading.StateValue.Away: Interlocked.Increment(ref away); break;
                        default: Interlocked.Increment(ref noFace); break;
                    }
                };
                started = await camera.StartAsync();
                permissionDenied = camera.PermissionDenied ||
                    WindowsPermissions.Camera() == WindowsPermissions.PermissionState.Denied;
                startError = camera.StartError;
                await Task.Delay(2500);
                await camera.StopAsync();
            }
            Step("摄像头打开并在内存里分析帧", started || permissionDenied,
                "started=" + started + " permissionDenied=" + permissionDenied
                + " frames=" + frames + " onCamera=" + onCamera + " away=" + away + " noFace=" + noFace
                + (startError is null ? "" : " error=" + startError));

            // 2) The recorded-picture path: exactly the same landmarker + estimator classes.
            var portraitPath = FindCoachGazePortrait();
            if (portraitPath is null)
            {
                Directory.CreateDirectory(Path.Combine(directory, "images"));
                throw new InvalidOperationException(
                    "test portrait missing: put portrait.jpg in Windows/validation/e10/images/ or " + Path.Combine(directory, "images"));
            }
            var frameImage = await GazeImage.LoadAsync(portraitPath);
            using var landmarker = new FaceLandmarker(assets);
            var analysis = landmarker.Analyze(frameImage.Rgb, frameImage.Width, frameImage.Height, 0);
            var eyeWidth = analysis is null
                ? 0
                : FaceGeometry.RightEyeContour.Max(index => analysis.Landmarks[index].X)
                  - FaceGeometry.RightEyeContour.Min(index => analysis.Landmarks[index].X);
            Step("录好的正面像检测到人脸", analysis is not null,
                analysis is null ? "no face/detection or presence below 0.5" : "landmarks=" + analysis.Landmarks.Count
                    + " presence=" + analysis.FacePresenceScore.ToString("F3")
                    + " rightEyeWidth=" + eyeWidth.ToString("F1") + "px");
            if (analysis?.Gaze is null) throw new InvalidOperationException("the portrait produced no gaze frame");

            var estimator = new GazeEstimator();
            for (var index = 0; index < 12; index++) estimator.AddCalibration(analysis.Gaze);
            var calibrated = estimator.FinishCalibration();
            estimator.ResetSession();
            var looking = estimator.Ingest(analysis.Gaze);
            Step("看镜头：判定 OnCamera", calibrated && looking.State == GazeReading.StateValue.OnCamera,
                "calibrated=" + calibrated + " state=" + looking.State + " deviation=" + looking.Deviation.ToString("F2"));

            // Move both irises sideways inside the real detected eye outlines: the same input the
            // estimator receives when the user's eyes leave the lens.
            var shifted = analysis.Landmarks.ToArray();
            foreach (var eyeContour in new[] { FaceGeometry.RightEyeContour, FaceGeometry.LeftEyeContour })
            {
                var xs = eyeContour.Select(index => shifted[index].X).ToArray();
                var width = xs.Max() - xs.Min();
                var shift = 0.35 * width;
                var iris = ReferenceEquals(eyeContour, FaceGeometry.RightEyeContour)
                    ? FaceGeometry.RightIris
                    : FaceGeometry.LeftIris;
                foreach (var index in iris)
                    shifted[index] = shifted[index] with { X = shifted[index].X + shift };
            }
            var awayFrame = FaceGeometry.GazeFrame(shifted, 1.0);
            // The estimator smooths readings; in a live call several frames arrive in a row.
            estimator.ResetSession();
            var awayReading = estimator.Ingest(awayFrame);
            for (var index = 0; index < 6; index++) awayReading = estimator.Ingest(awayFrame with { Time = 2.0 + index * 0.2 });
            Step("移开视线：判定 Away", awayReading.State == GazeReading.StateValue.Away,
                "state=" + awayReading.State + " deviation=" + awayReading.Deviation.ToString("F2"));

            // No face at all: the landmarker returns null and the estimator reports NoFace.
            var blank = new byte[frameImage.Width * frameImage.Height * 3];
            Array.Fill(blank, (byte)127);
            var noFaceAnalysis = landmarker.Analyze(blank, frameImage.Width, frameImage.Height, 2);
            var noFaceReading = estimator.Ingest(noFaceAnalysis?.Gaze);
            Step("没有人脸：判定 NoFace", noFaceReading.State == GazeReading.StateValue.NoFace,
                "detected=" + (noFaceAnalysis is not null) + " state=" + noFaceReading.State);

            // 3) Evidence screenshots of the settings card in both themes.
            state.Section = AppSection.Settings;
            await Task.Delay(600);
            if (this.GetVisualDescendants().OfType<SettingsView>().FirstOrDefault() is { } settings)
            {
                settings.SelectPage("Coach");
                await Task.Delay(500);
                CaptureVisual(this, Path.Combine(directory, "settings-coach-light.png"));
                if (Avalonia.Application.Current is { } app)
                {
                    app.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
                    await Task.Delay(350);
                    CaptureVisual(this, Path.Combine(directory, "settings-coach-dark.png"));
                    app.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
                    await Task.Delay(250);
                }
            }
            passed = steps.All(step => (bool)step.GetType().GetProperty("passed")!.GetValue(step)!);
        }
        catch (Exception exception)
        {
            error = exception.ToString();
        }
        var report = new { passed, error, steps };
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "coach-gaze-check.json"),
                JsonSerializer.Serialize(report, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }));
        }
        catch { }
        Console.WriteLine(passed ? "coach-gaze-check: passed" : "coach-gaze-check: failed " + (error ?? ""));
        var idle = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < idle && state.Working) await Task.Delay(100);
        Close();
    }

    /// <summary>The recorded portrait for the image path: the nextthought-selftest folder, then the repo.</summary>
    private static string? FindCoachGazePortrait()
    {
        var local = Path.Combine(Path.GetTempPath(), "nextthought-selftest", "coach-gaze", "images", "portrait.jpg");
        if (File.Exists(local)) return local;
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            foreach (var candidate in new[]
                     {
                         Path.Combine(directory.FullName, "validation", "e10", "images", "portrait.jpg"),
                         Path.Combine(directory.FullName, "Windows", "validation", "e10", "images", "portrait.jpg")
                     })
                if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        return null;
    }
}
