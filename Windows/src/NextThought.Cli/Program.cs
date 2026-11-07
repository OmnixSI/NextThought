using System.Diagnostics;
using System.Text;
using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;

Console.OutputEncoding = Encoding.UTF8;
var arguments = args.ToList();
string? Option(string name) { var index = arguments.IndexOf(name); return index >= 0 && index+1 < arguments.Count ? arguments[index+1] : null; }
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
try
{
    var paths = new AppPaths(Option("--data-root")); var models = new ModelStore(paths);
    switch (arguments.FirstOrDefault())
    {
        case "devices":
            Console.WriteLine(JsonSerializer.Serialize(new { system = AudioCaptureService.Devices(true), microphone = AudioCaptureService.Devices(false) }, JsonDefaults.Options)); break;
        case "wait-device":
        {
            var until = DateTimeOffset.UtcNow.AddSeconds(int.Parse(Option("--seconds") ?? "90")); var name = Option("--name") ?? "HECATE";
            while (DateTimeOffset.UtcNow < until)
            {
                if (AudioCaptureService.Devices(true).Any(d => d.Name.Contains(name, StringComparison.OrdinalIgnoreCase)) && AudioCaptureService.Devices(false).Any(d => d.Name.Contains(name, StringComparison.OrdinalIgnoreCase)))
                { Console.WriteLine("Device reconnected: " + name); return 0; }
                await Task.Delay(300, cancellation.Token);
            }
            throw new TimeoutException("未在等待时间内检测到设备重新连接。");
        }
        case "capture-test":
            return await AudioDiagnostics.CaptureAsync(paths, Option, arguments, cancellation.Token);
        case "service-test":
            return await ServiceDiagnostics.TestAsync(paths, Option, arguments, cancellation.Token);
        case "meeting-replay-test":
            return await MeetingReplayDiagnostics.RunAsync(paths, Option, cancellation.Token);
        case "summary-test":
            return await SummaryDiagnostics.TestAsync(paths, Option, cancellation.Token);
        case "summarize-test-session":
        {
            var archiveStore = new SessionStore(new AppPaths(Option("--session-root") ?? throw new ArgumentException("Missing --session-root")));
            var sessionId = Option("--session-id") is { } value ? Guid.Parse(value) : archiveStore.List().First().Id;
            var archived = archiveStore.Load(sessionId);
            if (archived.Meta.AudioSource is not ("controlled-pcm-replay" or "synthetic-text-fixture")) throw new ArgumentException("此验收命令只处理受控测试会话。");
            if (!archived.Meta.EndedAt.HasValue) throw new InvalidOperationException("测试会议仍在进行，尚不能整理纪要。");
            var credentials = new SettingsStore(paths); var configured = credentials.Load();
            var answerOptions = new AnswerOptions(configured.BaseUrl, configured.NotesModel.Length > 0 ? configured.NotesModel : configured.Model,
                credentials.ReadApiKey(), configured.ReasoningEffort, 2000);
            var markdown = await new SummaryService(archiveStore, new ChatCompletionClient()).GenerateAsync(sessionId, answerOptions, ct: cancellation.Token);
            var reloaded = archiveStore.Load(sessionId); var output = Option("--json") ?? throw new ArgumentException("Missing --json");
            var passed = reloaded.Meta.SummaryState == "ready" && reloaded.Summary == markdown && reloaded.Meta.TranscriptCount == reloaded.Lines.Count;
            await archiveStore.ExportMarkdownAsync(sessionId, Path.ChangeExtension(output, ".md"), cancellation.Token);
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { testedAt = DateTimeOffset.Now, passed, sessionId,
                transcriptCount = reloaded.Lines.Count, answers = reloaded.Answers.Count, model = answerOptions.Model, markdown }, JsonDefaults.Options));
            Console.WriteLine("Completed controlled meeting summary: " + (passed ? "PASS" : "FAIL")); return passed ? 0 : 2;
        }
        case "crash-fixture":
        {
            var fixturePaths = new AppPaths(Option("--session-root") ?? throw new ArgumentException("Missing --session-root"));
            var fixtureStore = new SessionStore(fixturePaths); var fixture = new SessionMeta { Title = "P1 进程中断恢复验收", AudioSource = "synthetic-fixture" };
            await fixtureStore.CreateAsync(fixture); using var lease = fixtureStore.AcquireLease(fixture.Id);
            await fixtureStore.AppendTranscriptAsync(fixture, new TranscriptLine { Date = fixture.StartedAt, Text = "已经完整写入的第一句" });
            await fixtureStore.AppendTranscriptAsync(fixture, new TranscriptLine { Date = fixture.StartedAt.AddSeconds(1), Text = "已经完整写入的第二句" });
            await fixtureStore.SetSummaryStateAsync(fixture, "pending");
            await File.AppendAllTextAsync(Path.Combine(fixtureStore.Folder(fixture.Id), "transcript.jsonl"), "{\"text\":\"interrupted");
            Console.WriteLine("Expected abrupt exit; fixture=" + fixture.Id); Console.Out.Flush(); Environment.Exit(86); return 86;
        }
        case "recover-test":
        {
            var fixtureStore = new SessionStore(new AppPaths(Option("--session-root") ?? throw new ArgumentException("Missing --session-root")));
            var recovered = await fixtureStore.RecoverInterruptedAsync(cancellation.Token);
            var repeated = await fixtureStore.RecoverInterruptedAsync(cancellation.Token);
            var passed = recovered.Count == 1 && recovered[0].Error == null && recovered[0].RepairedFiles == 1
                && recovered[0].TranscriptCount == 2 && repeated.Count == 0;
            var report = JsonSerializer.Serialize(new { testedAt = DateTimeOffset.Now, passed, recovered, repeated = repeated.Count }, JsonDefaults.Options);
            await File.WriteAllTextAsync(Option("--json") ?? throw new ArgumentException("Missing --json"), report);
            Console.WriteLine(report); return passed ? 0 : 2;
        }
        case "restart-capture-test":
        {
            var name = Option("--system-name") ?? "HECATE";
            var selected = AudioCaptureService.Devices(true).First(d => d.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
            var restartSettings = new AppSettings { CaptureSystem = true, CaptureMicrophone = false, SystemDeviceId = selected.Id };
            var failed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously); var ticks = 0; var phase = 0; long firstFrames = 0, secondFrames = 0;
            await using var capture = new AudioCaptureService(async token => { if (++ticks == 10) await Task.Delay(1800, token); });
            capture.Failed += message => failed.TrySetResult(message);
            capture.FrameReady += _ => { if (phase == 0) firstFrames++; else secondFrames++; };
            capture.Start(restartSettings); var firstError = await failed.Task.WaitAsync(TimeSpan.FromSeconds(8), cancellation.Token);
            await capture.DisposeAsync(); phase = 1; capture.Start(restartSettings); await Task.Delay(1500, cancellation.Token); await capture.DisposeAsync();
            var passed = firstError.Contains("休眠或采音暂停") && firstFrames > 0 && secondFrames > 0 && capture.LastError == null;
            var report = JsonSerializer.Serialize(new { testedAt = DateTimeOffset.Now, passed, firstError, firstFrames, secondFrames, rawAudioSaved = false, asrEnabled = false }, JsonDefaults.Options);
            await File.WriteAllTextAsync(Option("--json") ?? throw new ArgumentException("Missing --json"), report); Console.WriteLine(report); return passed ? 0 : 2;
        }
        case "install-models":
            var progress = new Progress<ModelProgress>(p => Console.WriteLine(p.Message));
            await models.InstallAsync("xasr480", progress, cancellation.Token);
            if (!arguments.Contains("--no-refine")) await models.InstallAsync("xasrOffline", progress, cancellation.Token);
            break;
        case "transcribe-wav":
            if (arguments.Count < 2 || !File.Exists(arguments[1])) throw new ArgumentException("请指定存在的 WAV 文件。");
            var settings = new AppSettings { LocalModel = Option("--model") ?? "xasr480", SecondPass = !arguments.Contains("--no-refine") };
            var clock = Stopwatch.StartNew(); var lines = new List<TranscriptUpdate>();
            using (var recognizer = new SpeechRecognizerSession(models, settings))
            {
                recognizer.Updated += update => { if (update.IsFinal) { lines.Add(update); Console.WriteLine(update.Text); } };
                recognizer.Warning += message => Console.Error.WriteLine(message);
                recognizer.TranscribeWave(arguments[1], AudioSource.System, cancellation.Token);
                var report = new { file = Path.GetFileName(arguments[1]), model = settings.LocalModel, refined = settings.SecondPass,
                    elapsedSeconds = clock.Elapsed.TotalSeconds, audioSeconds = recognizer.AudioSeconds, inferenceSeconds = recognizer.InferenceSeconds,
                    rtf = recognizer.InferenceSeconds/Math.Max(.001, recognizer.AudioSeconds), lines };
                var json = JsonSerializer.Serialize(report, JsonDefaults.Options);
                if (Option("--json") is { } output) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!); await File.WriteAllTextAsync(output, json, cancellation.Token); }
                Console.WriteLine(json);
                if (lines.Count == 0) return 2;
            }
            break;
        default:
            Console.WriteLine("NextThought Windows validation CLI\n  devices\n  install-models [--no-refine] [--data-root path]\n  transcribe-wav file.wav [--no-refine] [--json report.json] [--data-root path]"); break;
    }
    return 0;
}
catch (OperationCanceledException) { Console.Error.WriteLine("已取消。"); return 130; }
catch (Exception ex) { Console.Error.WriteLine(ex.ToString()); return 1; }
