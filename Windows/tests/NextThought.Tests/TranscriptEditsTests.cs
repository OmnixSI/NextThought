using System.Security.Cryptography;
using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// task-62/E16: the meeting page's 转写 right-click actions use core-parity's J6 data layer. The
/// recognised transcript.jsonl must stay byte for byte untouched: a speaker change appends a
/// voices.jsonl tag and writes meta.json voiceNames/meVoices, a hand edit writes a whole-sentence
/// correction decision into user-content.json.
/// </summary>
public sealed class TranscriptEditsTests
{
    private static string NewRoot() =>
        Path.Combine(Path.GetTempPath(), "nt-e16-edits-" + Guid.NewGuid().ToString("N"));

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static async Task<(AppPaths Paths, SessionStore Store, SessionMeta Meta, IReadOnlyList<TranscriptLine> Lines)> SeedAsync()
    {
        var paths = new AppPaths(NewRoot());
        var store = new SessionStore(paths);
        var started = DateTimeOffset.UtcNow;
        var meta = new SessionMeta
        {
            Id = Guid.NewGuid(), Title = "E16", Mode = "meeting", StartedAt = started
        };
        await store.CreateAsync(meta);
        var lines = new List<TranscriptLine>();
        for (var index = 0; index < 4; index++)
        {
            var line = new TranscriptLine
            {
                Id = Guid.NewGuid(),
                Speaker = index % 2 == 0 ? "remote" : "me",
                Date = started.AddSeconds(index * 5),
                Text = "第 " + (index + 1) + " 句原话"
            };
            lines.Add(line);
            await store.AppendTranscriptAsync(meta, line);
        }
        await store.EndAsync(meta with { EndedAt = started.AddMinutes(1) });
        return (paths, store, meta, lines);
    }

    [Fact]
    public async Task SpeakerAssignmentLeavesTheRecognisedTranscriptUntouched()
    {
        var (paths, store, meta, lines) = await SeedAsync();
        try
        {
            var transcriptPath = Path.Combine(store.Folder(meta.Id), "transcript.jsonl");
            var before = Hash(transcriptPath);
            var line = lines.Where(item => item.Speaker == "remote").First();

            // 这句是谁说的 → 新说话人 (Mac assignLine + a voices.jsonl tag).
            var (updated, tag) = SessionSpeakerEdits.AssignLine(meta, lines, line.Id, LineSpeaker.SomeoneNew);
            await store.AppendVoiceTagAsync(meta.Id, line.Id, tag.Voice);
            var name = updated.VoiceNames is { } names
                       && names.TryGetValue(tag.Voice.ToString(System.Globalization.CultureInfo.InvariantCulture), out var value)
                ? value
                : null;
            await store.SetVoiceLabelAsync(meta.Id, tag.Voice, name, updated.MeVoices?.Contains(tag.Voice) == true);

            Assert.Equal(before, Hash(transcriptPath));
            var reloaded = store.Load(meta.Id);
            Assert.Equal(tag.Voice, reloaded.Lines.First(item => item.Id == line.Id).Voice);
            Assert.Equal(SessionSpeakerEdits.NewSpeakerName, reloaded.Meta.VoiceNames?[tag.Voice.ToString()]);

            // 这句是我说的 (me) reuses/creates the user voice and marks it.
            var (meMeta, meTag) = SessionSpeakerEdits.AssignLine(reloaded.Meta, reloaded.Lines, line.Id, LineSpeaker.Me);
            await store.AppendVoiceTagAsync(meta.Id, line.Id, meTag.Voice);
            await store.SetVoiceLabelAsync(meta.Id, meTag.Voice, null, true);
            Assert.Equal(before, Hash(transcriptPath));
            var afterMe = store.Load(meta.Id);
            Assert.Contains(meTag.Voice, afterMe.Meta.MeVoices ?? []);
            Assert.Equal(meTag.Voice, afterMe.Lines.First(item => item.Id == line.Id).Voice);
        }
        finally { try { Directory.Delete(paths.Root, true); } catch { } }
    }

    [Fact]
    public async Task HandEditingASentenceOnlyWritesAUserContentDecision()
    {
        var (paths, store, meta, lines) = await SeedAsync();
        try
        {
            var transcriptPath = Path.Combine(store.Folder(meta.Id), "transcript.jsonl");
            var folder = store.Folder(meta.Id);
            var userContentPath = Path.Combine(folder, "user-content.json");
            var before = Hash(transcriptPath);
            var line = lines[0];

            var content = SessionUserContentStore.Load(folder);
            var updated = SessionTextEdits.EditLine(content, [], lines, line.Id, line.Text, "改好的这一句");
            SessionUserContentStore.Save(updated, folder);

            Assert.Equal(before, Hash(transcriptPath));
            Assert.True(File.Exists(userContentPath));
            var effective = SessionTextEdits.EffectiveLines(updated, [], store.Load(meta.Id).Lines);
            Assert.Equal("改好的这一句", effective.First(item => item.Id == line.Id).Text);

            // Taking the edit away returns the recognised text.
            var cleared = SessionTextEdits.EditLine(updated, [], store.Load(meta.Id).Lines, line.Id, "改好的这一句", line.Text);
            var restored = SessionTextEdits.EffectiveLines(cleared, [], store.Load(meta.Id).Lines);
            Assert.Equal(line.Text, restored.First(item => item.Id == line.Id).Text);
            Assert.Equal(before, Hash(transcriptPath));
        }
        finally { try { Directory.Delete(paths.Root, true); } catch { } }
    }

    [Fact]
    public async Task ToggleCorrectionRevertsAndRestoresAProofreadingFix()
    {
        var (paths, store, meta, lines) = await SeedAsync();
        try
        {
            var folder = store.Folder(meta.Id);
            var transcriptPath = Path.Combine(folder, "transcript.jsonl");
            var before = Hash(transcriptPath);
            var generated = new[]
            {
                new TranscriptCorrection { Id = "fix-1", Line = 0, LineId = lines[0].Id, Original = lines[0].Text, Corrected = "校对后的一句" }
            };
            var content = SessionUserContentStore.Load(folder);
            var reverted = SessionTextEdits.ToggleCorrection(content, generated, lines, "fix-1");
            Assert.Equal(before, Hash(transcriptPath));
            Assert.Equal(lines[0].Text,
                SessionTextEdits.EffectiveLines(reverted, generated, lines).First(item => item.Id == lines[0].Id).Text);
        }
        finally { try { Directory.Delete(paths.Root, true); } catch { } }
    }

    [Fact]
    public void TheNotesTranscriptRowsOfferTheMacLineSpeakerMenu()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Windows", "src", "NextThought.Desktop", "Views", "NotesView.axaml")))
            root = root.Parent;
        Assert.NotNull(root);
        var view = File.ReadAllText(Path.Combine(root!.FullName, "Windows", "src", "NextThought.Desktop", "Views", "NotesView.axaml"));
        Assert.Contains("TranscriptRowMenu_Opening", view);
        Assert.Contains("改这一句", view);
        Assert.Contains("这句是谁说的", view);
        var state = File.ReadAllText(Path.Combine(root.FullName, "Windows", "src", "NextThought.Desktop", "AppState.TranscriptEdits.cs"));
        Assert.Contains("SessionSpeakerEdits.AssignLine", state);
        Assert.Contains("SessionTextEdits.EditLine", state);
    }
}
