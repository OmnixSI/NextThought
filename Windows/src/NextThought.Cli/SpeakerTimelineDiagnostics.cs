using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;

internal static class SpeakerTimelineDiagnostics
{
    public static async Task<int> RunAsync(AppPaths paths, Func<string, string?> option, CancellationToken ct)
    {
        using var tagger = new MeetingVoiceTagger(new AppPaths(), AudioSource.System); var store = new SessionStore(paths);
        var meta = new SessionMeta { Title = "P3 公开声纹时间线验收", AudioSource = "synthetic-speaker-fixture" }; await store.CreateAsync(meta, ct);
        var origin = DateTimeOffset.UtcNow; long start = 0; var result = new List<object>(); var labels = new List<int?>();
        foreach (var name in new[] { "fangjun-sr-1.wav", "leijun-sr-1.wav", "liudehua-sr-1.wav", "fangjun-sr-2.wav" })
        {
            var samples = SpeakerDiagnostics.Read(Path.Combine(option("--fixtures")!, name));
            for (int offset = 0; offset < samples.Length; offset += 1600) tagger.Add(new AudioFrame(AudioSource.System, samples.AsSpan(offset, Math.Min(1600, samples.Length - offset)).ToArray(), start + offset, origin));
            var line = new TranscriptLine { Date = origin.AddSeconds(start / 16000.0), StartedAt = origin.AddSeconds(start / 16000.0), EndedAt = origin.AddSeconds((start + samples.Length) / 16000.0), Speaker = "remote", Text = "公开合成编号验证 " + result.Count };
            await store.AppendTranscriptAsync(meta, line, ct); var voice = await tagger.LabelAsync(line.StartedAt.Value, line.EndedAt.Value, ct); labels.Add(voice);
            if (voice.HasValue) await store.AppendVoiceTagAsync(meta.Id, line.Id, voice.Value, ct);
            result.Add(new { file = name, line = line.Id, voice, start = start / 16000.0, end = (start + samples.Length) / 16000.0 }); start += samples.Length;
        }
        await store.EndAsync(meta, ct); var data = store.Load(meta.Id); await store.ExportMarkdownAsync(meta.Id, Path.ChangeExtension(option("--json"), ".md")!, ct);
        var passed = labels.SequenceEqual(new int?[] { 0, 1, 2, 0 }) && data.Lines.Select(l => l.Voice).SequenceEqual(labels)
            && !Directory.EnumerateFiles(store.Folder(meta.Id)).Any(f => Path.GetExtension(f) is ".wav" or ".bin");
        await File.WriteAllTextAsync(option("--json")!, JsonSerializer.Serialize(new { testedAt = DateTimeOffset.Now, passed, sessionId = meta.Id, labels, persistedAudio = false, persistedEmbeddings = false, result }, JsonDefaults.Options), ct);
        Console.WriteLine("Speaker timeline/archive/export: " + (passed ? "PASS" : "FAIL")); return passed ? 0 : 2;
    }
}
