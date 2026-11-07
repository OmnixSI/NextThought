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
