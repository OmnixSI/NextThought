using NextThought.Core;

namespace NextThought.Platform;

/// <summary>E12: a short 16 kHz mono microphone take for my-voice enrollment.</summary>
public static class VoiceRecorder
{
    public static async Task<short[]> RecordAsync(TimeSpan duration, Action<double>? progress = null, CancellationToken ct = default)
    {
        var samples = new List<short>();
        var gate = new object();
        await using var capture = new AudioCaptureService();
        capture.FrameReady += frame =>
        {
            if (frame.Source != AudioSource.Microphone) return;
            lock (gate) samples.AddRange(frame.Samples);
        };
        capture.Failed += _ => { };
        var settings = new AppSettings { CaptureSystem = false, CaptureMicrophone = true, MicrophoneGain = 1 };
        capture.Start(settings);
        var started = DateTimeOffset.UtcNow;
        try
        {
            while ((DateTimeOffset.UtcNow - started) < duration)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Invoke(Math.Min(1, (DateTimeOffset.UtcNow - started).TotalSeconds / duration.TotalSeconds));
                await Task.Delay(100, ct);
            }
        }
        finally
        {
            await capture.DisposeAsync();
        }
        progress?.Invoke(1);
        lock (gate) return samples.ToArray();
    }
}
