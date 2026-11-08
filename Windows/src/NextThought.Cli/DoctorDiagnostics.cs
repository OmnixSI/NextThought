using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;

internal static class DoctorDiagnostics
{
    [DllImport("kernel32.dll")] private static extern uint GetACP();
    private sealed record Check(string Name, string Status, string Detail);
    public static async Task<int> RunAsync(AppPaths components, Func<string, string?> option, CancellationToken ct)
    {
        var output = Path.GetFullPath(option("--json") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "NextThought-验证结果", "doctor.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(output)!); var checks = new List<Check>();
        void Add(string name, bool passed, string detail) => checks.Add(new(name, passed ? "passed" : "failed", detail));
        Add("Windows baseline", OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041) && RuntimeInformation.OSArchitecture == Architecture.X64 && RuntimeInformation.ProcessArchitecture == Architecture.X64,
            RuntimeInformation.OSDescription + "; OS=" + RuntimeInformation.OSArchitecture + "; process=" + RuntimeInformation.ProcessArchitecture);
        Add("UTF-8 native paths", GetACP() == 65001, "Active code page=" + GetACP());
        var application = Directory.GetParent(AppContext.BaseDirectory.TrimEnd('\\', '/'))!.FullName;
        var mainExe = Path.Combine(application, "NextThought.exe");
        if (File.Exists(mainExe) && File.Exists(Path.Combine(application, "release-manifest.json")))
        {
            try
            {
                var version = FileVersionInfo.GetVersionInfo(mainExe).ProductVersion!.Split('+')[0];
                var manifest = await DeploymentStore.VerifyDirectoryAsync(application, version, ct);
                checks.Add(new("Release integrity", "passed", manifest.Version + " / " + manifest.Revision + " / " + manifest.Signing));
            }
            catch (Exception ex) { checks.Add(new("Release integrity", "failed", ex.Message)); }
        }
        else checks.Add(new("Release integrity", "warning", "This is not a complete published package; no release manifest was found."));
        foreach (var library in new[] { "sherpa-onnx-c-api.dll", "onnxruntime.dll" })
        {
            try { var handle = NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, library)); NativeLibrary.Free(handle); checks.Add(new(library, "passed", "Native load succeeded")); }
            catch (Exception ex) { checks.Add(new(library, "failed", ex.GetType().Name + ": " + ex.Message)); }
        }
        try
        {
            var render = AudioCaptureService.Devices(true).Count; var capture = AudioCaptureService.Devices(false).Count;
            checks.Add(new("Audio endpoint enumeration", "passed", $"Playback endpoints={render}; microphone endpoints={capture}; no recording started"));
        }
        catch (Exception ex) { checks.Add(new("Audio endpoint enumeration", option("--headless") == "true" ? "warning" : "failed", ex.Message)); }
        try
        {
            var languages = Windows.Media.Ocr.OcrEngine.AvailableRecognizerLanguages.Select(l => l.LanguageTag).ToArray();
            checks.Add(new("Windows OCR", languages.Length > 0 ? "passed" : "warning", string.Join(", ", languages)));
        }
        catch (Exception ex) { checks.Add(new("Windows OCR", "warning", ex.Message)); }
        bool storagePassed = false;
        try
        {
            var smoke = new AppPaths(Path.Combine(Path.GetDirectoryName(output)!, "公开验收 空间", Guid.NewGuid().ToString("N")));
            var store = new SessionStore(smoke); var session = new SessionMeta { Title = "公开合成：中文路径与归档验证", AudioSource = "synthetic-text-fixture" };
            await store.CreateAsync(session, ct); var row = new TranscriptLine { Id = Guid.NewGuid(), Date = DateTimeOffset.UtcNow, Speaker = "remote", Text = "中文路径、备份和归档自检；不包含实际会议。" };
            await store.AppendTranscriptAsync(session, row, ct); await store.EndAsync(session, ct);
            var backup = Path.Combine(smoke.Root, "公开备份.ntbackup"); await new BackupService(smoke).ExportAsync(backup, ct);
            var restored = new AppPaths(Path.Combine(smoke.Root, "恢复目录")); await new BackupService(restored).ImportAsync(backup, ct);
            storagePassed = new SessionStore(restored).Load(session.Id).Lines.Single().Text == row.Text;
            Add("Chinese path and backup roundtrip", storagePassed, "Synthetic fixture only; actual user archives were not modified");
        }
        catch (Exception ex) { checks.Add(new("Chinese path and backup roundtrip", "failed", ex.Message)); }
        var models = new ModelStore(components);
        var modelStatus = ModelStore.Catalog.Select(p => new { p.Id, installed = models.IsInstalled(p.Id) }).ToArray();
        var asrReady = models.IsInstalled("xasr480") && models.IsInstalled("xasrOffline"); bool? asrPassed = null;
        if (asrReady && option("--audio") is { } audio)
        {
            try
            {
                using var recognizer = new SpeechRecognizerSession(models, new AppSettings { LocalModel = "xasr480", SecondPass = true });
                var finals = 0; recognizer.Updated += u => { if (u.IsFinal && !string.IsNullOrWhiteSpace(u.Text)) finals++; };
                recognizer.TranscribeWave(audio, AudioSource.System, ct); asrPassed = finals > 0;
                Add("Public WAV inference", asrPassed.Value, $"Final lines={finals}; CPU inference={recognizer.InferenceSeconds:F2}s; no hardware capture");
            }
            catch (Exception ex) { asrPassed = false; checks.Add(new("Public WAV inference", "failed", ex.Message)); }
        }
        else checks.Add(new("Public WAV inference", "warning", asrReady ? "No public WAV was supplied" : "Speech models are not ready. Prepare speech components in Settings, then run validation again."));
        var passed = checks.All(c => c.Status != "failed");
        var result = new { testedAt = DateTimeOffset.Now, passed, scope = "Base application dependencies and synthetic data; model download and physical microphone capture are separate acceptance items",
            framework = RuntimeInformation.FrameworkDescription, logicalProcessors = Environment.ProcessorCount, externalDotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT") is not null,
            headless = option("--headless") == "true", profilePathHasNonAscii = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).Any(c => c > 127), reportPathHasNonAscii = output.Any(c => c > 127),
            storagePassed, asrReady, asrPassed, models = modelStatus, checks };
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(result, JsonDefaults.Options), ct);
        var markdown = new StringBuilder("# NextThought Windows 自检结果\n\n").AppendLine($"时间：{DateTimeOffset.Now:O}\n\n基础自检：{(passed ? "通过" : "未通过")}；语音模型就绪：{asrReady}；公开 WAV 推理：{asrPassed?.ToString() ?? "未执行"}\n\n| 检查 | 状态 | 结果 |\n|---|---|---|");
        foreach (var check in checks) markdown.AppendLine($"| {check.Name} | {check.Status} | {check.Detail.Replace('|', '／').Replace('\n', ' ')} |");
        markdown.AppendLine("\n未采集麦克风或系统声音，未调用回答服务，报告不包含 API Key、设备 ID 或实际会议内容。基础自检通过不等于模型下载或全部硬件验收通过。增强 OCR 的独立 Python 环境仍需另行安装。\n");
        await File.WriteAllTextAsync(Path.ChangeExtension(output, ".md"), markdown.ToString(), ct);
        Console.WriteLine($"Doctor {(passed ? "PASS" : "FAIL")}: {output}"); return passed ? 0 : 2;
    }
}
