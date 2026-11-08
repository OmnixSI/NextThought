using System.Diagnostics;
using System.Text.Json;
using NAudio.Wave;
using NextThought.Core;
using NextThought.Platform;

internal static class SpeakerDiagnostics
{
    public static short[] Read(string file)
    {
        using var reader = new WaveFileReader(file);
        if (reader.WaveFormat.SampleRate != 16000 || reader.WaveFormat.Channels != 1 || reader.WaveFormat.BitsPerSample != 16) throw new InvalidDataException("声纹验收只接受 16 kHz 单声道 PCM16。");
        var bytes = new byte[reader.Length]; reader.ReadExactly(bytes); var samples = new short[bytes.Length / 2]; Buffer.BlockCopy(bytes, 0, samples, 0, bytes.Length); return samples;
    }
    public static async Task<int> RunAsync(AppPaths paths, Func<string, string?> option, CancellationToken ct)
    {
        var fixtures = option("--fixtures")!; var clock = Stopwatch.StartNew(); using var model = new SpeakerEmbedder(paths); var groups = new VoiceGroups();
        var labels = new Dictionary<string, int>(); var vectors = new Dictionary<string, float[]>(); var details = new List<object>();
        foreach (var speaker in new[] { "fangjun", "leijun", "liudehua" })
        {
            var pcm = Read(Path.Combine(fixtures, speaker + "-sr-1.wav")); var vector = model.Embed(pcm, ct)!;
            var assigned = groups.Assign(vector, pcm.Length / 16000.0); if (!assigned.HasValue) throw new InvalidDataException("公开参考片段不足以建立说话人分组。");
            labels[speaker] = assigned.Value; vectors[speaker] = vector;
        }
        int correct = 0, unknown = 0, wrong = 0;
        foreach (var speaker in labels.Keys)
        foreach (var suffix in new[] { "-sr-2.wav", "-test-sr-1.wav", "-test-sr-2.wav" })
        {
            var file = speaker + suffix; var pcm = Read(Path.Combine(fixtures, file)); var vector = model.Embed(pcm, ct)!; var assigned = groups.Assign(vector, pcm.Length / 16000.0);
            var ok = assigned == labels[speaker]; if (ok) correct++; else if (assigned is null) unknown++; else wrong++;
            details.Add(new { file, assigned, expected = labels[speaker], correct = ok, similarities = vectors.ToDictionary(p => p.Key, p => VoiceGroups.Similarity(vector, p.Value)), seconds = pcm.Length / 16000.0 });
        }
        var shortRejected = model.Embed(new short[4000], ct) is null; var silenceRejected = model.Embed(new short[48000], ct) is null;
        var passed = correct >= 7 && wrong == 0 && shortRejected && silenceRejected;
        await File.WriteAllTextAsync(option("--json")!, JsonSerializer.Serialize(new { testedAt = DateTimeOffset.Now, passed, correct, unknown, wrong, total = 9, shortRejected, silenceRejected, elapsedSeconds = clock.Elapsed.TotalSeconds,
            model = SpeakerEmbedder.ModelId, modelHash = SpeakerEmbedder.Sha256, dimension = 256, privacy = "Public fixtures only; no microphone or persistent user voiceprint", details }, JsonDefaults.Options), ct);
        Console.WriteLine($"Speaker grouping: {correct}/9 correct, {unknown} unknown, {wrong} wrong; {(passed ? "PASS" : "FAIL")}"); return passed ? 0 : 2;
    }
}
