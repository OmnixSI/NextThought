using System.Diagnostics;
using System.Text.Json;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NextThought.Core;
using NextThought.Platform;

internal static class ProcessAudioDiagnostics
{
    public static async Task<int> ToneAsync(Func<string, string?> option, CancellationToken ct)
    {
        using var output = new WasapiOut();
        output.Init(new SignalGenerator(48000, 2) { Frequency = double.Parse(option("--frequency")!), Gain = .04, Type = SignalGeneratorType.Sin }.ToWaveProvider()); output.Play();
        await Task.Delay(int.Parse(option("--tone-ms") ?? "10000"), ct); output.Stop(); await Task.Delay(7000, ct); return 0;
    }
    public static async Task<int> RunAsync(Func<string, string?> option, CancellationToken ct)
    {
        Process Start(int frequency, int duration)
        {
            var info = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "NextThought.Cli.exe")) { UseShellExecute = false, CreateNoWindow = true };
            foreach (var a in new[] { "test-tone", "--frequency", frequency.ToString(), "--tone-ms", duration.ToString() }) info.ArgumentList.Add(a);
            return Process.Start(info)!;
        }
        using var target = Start(440, 3500); using var other = Start(880, 15000);
        try
        {
            await Task.Delay(800, ct); var frames = new List<short[]>(); string? error = null;
            await using var capture = new AudioCaptureService(); capture.FrameReady += f => frames.Add(f.Samples); capture.Failed += e => error = e;
            capture.Start(new AppSettings { CaptureSystem = true, CaptureMicrophone = false, CaptureProcessId = target.Id, CaptureProcessStartTicks = target.StartTime.ToUniversalTime().Ticks });
            await Task.Delay(6500, ct); await capture.DisposeAsync();
            var first = frames.Skip(3).Take(15).SelectMany(x => x).ToArray(); var last = frames.TakeLast(15).SelectMany(x => x).ToArray();
            double Amplitude(short[] pcm, double frequency)
            {
                double real = 0, imaginary = 0;
                for (int i = 0; i < pcm.Length; i++) { real += pcm[i] / 32768.0 * Math.Cos(2 * Math.PI * frequency * i / 16000); imaginary += pcm[i] / 32768.0 * Math.Sin(2 * Math.PI * frequency * i / 16000); }
                return 2 * Math.Sqrt(real * real + imaginary * imaginary) / Math.Max(1, pcm.Length);
            }
            var included440 = Amplitude(first, 440); var excluded880 = Amplitude(first, 880); var tailRms = Math.Sqrt(last.Average(s => Math.Pow(s / 32768.0, 2)));
            var passed = error is null && frames.Count >= 60 && included440 > .005 && included440 > excluded880 * 20 && tailRms < .0002;
            await File.WriteAllTextAsync(option("--json")!, JsonSerializer.Serialize(new { testedAt = DateTimeOffset.Now, passed, frames = frames.Count, included440, excluded880, tailRms, error, rawAudioSaved = false, microphoneEnabled = false }, JsonDefaults.Options), ct);
            Console.WriteLine("Process audio isolation: " + (passed ? "PASS" : "FAIL")); return passed ? 0 : 2;
        }
        finally { if (!target.HasExited) target.Kill(true); if (!other.HasExited) other.Kill(true); }
    }
}
