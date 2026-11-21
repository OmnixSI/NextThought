using System.Text.Json;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// J6 Core data layer, checked against the Mac app code it ports:
///  - speaker reassignment of one sentence ("我是哪个？" / right-click 改成我 / 某个人 / 另一个人) is
///    <c>AppState.assignLine</c> + <c>SessionArchive.assignVoice</c> in Sources/NextThought/SpeakerID.swift:
///    a voices.jsonl tag is appended and meta.json's voiceNames/meVoices change; transcript.jsonl is
///    never touched.
///  - "改这一句的文字" is <c>AppState.editLine</c> in Sources/NextThought/AppState+Notes.swift: user-content.json
///    gets correctionDecisions (the whole sentence becomes a correction, inner proofreading fixes are
///    reverted, saving the recognised text again removes the edit); transcript.jsonl is never touched.
///  - re-organizing the notes reads <c>SessionDetail.effectiveLines</c> (SessionArchive.swift), i.e. the
///    transcript with the correction decisions applied, and the A11 source revision is computed over it.
/// These helpers are Core-side; the Desktop/Platform wiring stays with the page owner.
/// </summary>
public sealed class SessionLineEditsTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);

    private static TranscriptLine Line(string text, int voice, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Date = Start,
        Speaker = "remote",
        Text = text,
        Voice = voice
    };

    private static SessionMeta Meta(params int[] meVoices) => new()
    {
        Id = Guid.NewGuid(),
        StartedAt = Start,
        Mode = "meeting",
        MeVoices = meVoices.Length == 0 ? null : meVoices,
        VoiceNames = new Dictionary<string, string> { ["2"] = "小王" }
    };

    // --- speaker reassignment (SpeakerID.swift assignLine / mergeVoice) --------------------------

    [Fact]
    public void AssignLineToMeReusesTheMarkedVoiceOrCreatesOne()
    {
        var line = Line("这一页讲缓存", 1);
        var meta = Meta(3);
        var (updated, tag) = SessionSpeakerEdits.AssignLine(meta, [line], line.Id, LineSpeaker.Me);
        Assert.Equal(line.Id, tag.Id);
        Assert.Equal(3, tag.Voice);
        Assert.Equal(new[] { 3 }, updated.MeVoices);

        var unmarked = Meta();
        var (created, createdTag) = SessionSpeakerEdits.AssignLine(unmarked, [line], line.Id, LineSpeaker.Me);
        // SpeakerLabels.newVoice: the highest voice in use + 1 (meta.voiceNames has voice 2).
        Assert.Equal(3, createdTag.Voice);
        Assert.Contains(3, created.MeVoices!);
    }

    [Fact]
    public void AssignLineToSomeoneNewNamesTheNewVoice()
    {
        var first = Line("第一句", 1);
        var second = Line("第二句", 1);
        var meta = Meta(1);
        var (updated, tag) = SessionSpeakerEdits.AssignLine(meta, [first, second], second.Id,
            LineSpeaker.SomeoneNew);
        Assert.Equal(second.Id, tag.Id);
        Assert.Equal(3, tag.Voice);
        Assert.Equal("新说话人", updated.VoiceNames!["3"]);
        Assert.Equal(new[] { 1 }, updated.MeVoices);
    }

    [Fact]
    public void AssignLineToAChosenVoiceLeavesThePeopleAlone()
    {
        var line = Line("这一页讲缓存", 1);
        var meta = Meta();
        var (updated, tag) = SessionSpeakerEdits.AssignLine(meta, [line], line.Id, LineSpeaker.FromVoice(7));
        Assert.Equal(7, tag.Voice);
        Assert.Equal(meta.MeVoices, updated.MeVoices);
        Assert.Equal(meta.VoiceNames, updated.VoiceNames);
    }

    [Fact]
    public void LaterVoiceTagsWinWhenTheTranscriptIsReadBack()
    {
        var line = Line("原始转写", 1);
        var kept = Line("没有改过的句子", 4);
        var tags = new[]
        {
            new SessionVoiceTag(line.Id, 2),
            new SessionVoiceTag(line.Id, 6)
        };
        var effective = SessionSpeakerEdits.ApplyVoiceTags([line, kept], tags);
        Assert.Equal(6, effective[0].Voice);
        Assert.Equal(4, effective[1].Voice);
        Assert.Equal("原始转写", effective[0].Text);
    }

    [Fact]
    public void MergeVoicesRetagsEverySentenceAndMovesTheNameAndMeMark()
    {
        var first = Line("第一句", 2);
        var second = Line("第二句", 2);
        var other = Line("另一句", 5);
        var overridden = Line("标签改到二", 5);
        var tags = new[] { new SessionVoiceTag(overridden.Id, 2) };
        var meta = Meta(2);
        var (merged, newTags) = SessionSpeakerEdits.MergeVoices(meta, [first, second, other, overridden],
            tags, from: 2, into: 5);
        Assert.Equal(new[] { first.Id, second.Id, overridden.Id }.OrderBy(id => id),
            newTags.Select(tag => tag.Id).OrderBy(id => id));
        Assert.All(newTags, tag => Assert.Equal(5, tag.Voice));
        Assert.Equal("小王", merged.VoiceNames!["5"]);
        Assert.False(merged.VoiceNames!.ContainsKey("2"));
        Assert.Equal(new[] { 5 }, merged.MeVoices);
    }

    [Fact]
    public void VoiceNameAndMeMarkFollowTheMacMetaKeys()
    {
        var named = SessionSpeakerEdits.SetVoiceName(Meta() with { VoiceNames = null }, 4, "  李工 ");
        Assert.Equal("李工", named.VoiceNames!["4"]);
        var cleared = SessionSpeakerEdits.SetVoiceName(named, 4, "   ");
        Assert.Null(cleared.VoiceNames);
        var me = SessionSpeakerEdits.SetMeVoice(Meta(), 4, true);
        Assert.Equal(new[] { 4 }, me.MeVoices);
        var notMe = SessionSpeakerEdits.SetMeVoice(me, 4, false);
        Assert.Null(notMe.MeVoices);
    }

    // --- "改这一句的文字" (AppState+Notes.swift editLine / toggleCorrection) ---------------------

    [Fact]
    public void EditLineReplacesTheWholeSentenceAndRevertsInnerFixes()
    {
        var first = Line("研究的是 Mamba 结构", 1);
        var second = Line("开始实验", 1, Guid.NewGuid());
        var content = new SessionUserContent
        {
            CorrectionDecisions =
            [
                new SessionCorrectionDecision(null, first.Id, "Mamba", "曼巴", false),
                new SessionCorrectionDecision(null, second.Id, "开始", "开始吧", false)
            ]
        };
        var generated = new[]
        {
            new TranscriptCorrection { Id = "inner", Line = 0, LineId = first.Id, Original = "Mamba", Corrected = "曼巴" }
        };
        var edited = SessionTextEdits.EditLine(content, generated, [first, second], first.Id,
            "研究的是 Mamba 结构", "研究了 Mamba 架构");

        var decisions = edited.CorrectionDecisions;
        // The other sentence keeps its decision; this one gets the reverted inner fix and the
        // whole-sentence replacement (Mac editLine).
        Assert.Equal(3, decisions.Count);
        Assert.Equal(second.Id, decisions[0].LineId);
        var inner = Assert.Single(decisions, decision => decision.LineId == first.Id && decision.Reverted);
        Assert.Equal("Mamba", inner.Original);
        Assert.Equal("曼巴", inner.Corrected);
        var whole = Assert.Single(decisions, decision => decision.LineId == first.Id && !decision.Reverted);
        Assert.Equal("研究的是 Mamba 结构", whole.Original);
        Assert.Equal("研究了 Mamba 架构", whole.Corrected);
    }

    [Fact]
    public void SavingTheRecognisedTextAgainRemovesTheEdit()
    {
        var line = Line("研究的是 Mamba 结构", 1);
        var content = new SessionUserContent
        {
            CorrectionDecisions = [new SessionCorrectionDecision(null, line.Id, "研究的是 Mamba 结构", "改过的整句", false)]
        };
        var reverted = SessionTextEdits.EditLine(content, [], [line], line.Id, "研究的是 Mamba 结构",
            "  研究的是 Mamba 结构  ");
        Assert.Empty(reverted.CorrectionDecisions);
        // Blank input is ignored, as on the Mac.
        var blank = SessionTextEdits.EditLine(content, [], [line], line.Id, "研究的是 Mamba 结构", "   ");
        Assert.Equal(content.CorrectionDecisions, blank.CorrectionDecisions);
    }

    [Fact]
    public void ToggleCorrectionFlipsTheUserDecisionBothWays()
    {
        var line = Line("用库伯奈提斯部署", 1);
        var generated = new[]
        {
            new TranscriptCorrection { Id = "fix", Line = 0, LineId = line.Id, Original = "库伯奈提斯", Corrected = "Kubernetes" }
        };
        var content = new SessionUserContent();
        var reverted = SessionTextEdits.ToggleCorrection(content, generated, [line], "fix");
        var decision = Assert.Single(reverted.CorrectionDecisions);
        Assert.True(decision.Reverted);
        Assert.Equal("库伯奈提斯", decision.Original);
        Assert.Equal("Kubernetes", decision.Corrected);
        // The reverted decision still shows as the raw text.
        var effective = reverted.Corrections(generated, [line]);
        Assert.True(effective[0].Reverted);
        Assert.Equal(line.Text, NotesCorrections.Apply(effective, [line])[0].Text);
        // Toggling again re-applies it.
        var applied = SessionTextEdits.ToggleCorrection(reverted, effective, [line], "fix");
        var again = Assert.Single(applied.CorrectionDecisions);
        Assert.False(again.Reverted);
        Assert.Equal(decision.Id, again.LineId == line.Id ? decision.Id : again.Id);
    }

    // --- re-organizing the notes uses the edited text (SessionArchive.effectiveLines) ------------

    [Fact]
    public void EffectiveLinesFeedTheNotesAndTheSourceRevision()
    {
        var first = Line("研究的是 Mamba 结构", 1);
        var second = Line("开始实验", 1);
        var content = new SessionUserContent
        {
            CorrectionDecisions = [new SessionCorrectionDecision(null, first.Id, first.Text, "研究表明了 Mamba 结构", false)]
        };
        var effective = SessionTextEdits.EffectiveLines(content, [], [first, second]);
        Assert.Equal("研究表明了 Mamba 结构", effective[0].Text);
        Assert.Equal("开始实验", effective[1].Text);

        var notesInput = SessionSummaryPrompt.TranscriptText(effective, Start);
        Assert.Contains("研究表明了 Mamba 结构", notesInput);
        Assert.DoesNotContain(first.Text, notesInput);

        var meta = Meta();
        var before = SessionProjectionBuilder.TranscriptSourceRevision(meta, [first, second], []);
        var corrections = content.Corrections([], [first, second]);
        var after = SessionProjectionBuilder.TranscriptSourceRevision(meta, [first, second], corrections);
        Assert.NotEqual(before, after);
        var projection = SessionProjectionBuilder.Build(meta.Id, Start, [first, second], [], corrections: corrections, meta: meta);
        Assert.Equal("研究表明了 Mamba 结构", projection.Transcript[0].CorrectedText);
    }

    // --- files: transcript.jsonl must stay byte-identical, edits in the Mac files ----------------

    [Fact]
    public void TranscriptBytesStayUnchangedWhenSpeakerAndTextAreEdited()
    {
        var folder = Directory.CreateTempSubdirectory("nt-j6-").FullName;
        try
        {
            var transcriptPath = Path.Combine(folder, "transcript.jsonl");
            var voicesPath = Path.Combine(folder, "voices.jsonl");
            var metaPath = Path.Combine(folder, "meta.json");
            var first = Line("研究的是 Mamba 结构", 1);
            var second = Line("开始实验", 1);
            var lines = new[] { first, second };
            File.WriteAllBytes(transcriptPath, [.. lines.SelectMany(line => JSONLines.Line(line))]);
            var originalTranscript = File.ReadAllBytes(transcriptPath);
            var meta = Meta();
            // The meeting record as the app stores it: meta.json + user-content.json + voices.jsonl.
            File.WriteAllText(metaPath, JsonSerializer.Serialize(meta, JsonDefaults.ArchiveOptions));

            var (editedMeta, tag) = SessionSpeakerEdits.AssignLine(meta, lines, second.Id, LineSpeaker.SomeoneNew);
            File.AppendAllBytes(voicesPath, JSONLines.Line(tag));
            File.WriteAllText(metaPath, JsonSerializer.Serialize(editedMeta, JsonDefaults.ArchiveOptions));

            var content = SessionTextEdits.EditLine(new SessionUserContent(), [], lines, first.Id, first.Text,
                "研究表明了 Mamba 结构");
            var saved = SessionUserContentStore.Save(content, folder);

            Assert.Equal(originalTranscript, File.ReadAllBytes(transcriptPath));
            Assert.Equal(1, saved.Revision);

            // Same file names and Mac keys the Mac writes.
            var userContentJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "user-content.json")));
            var decision = Assert.Single(userContentJson.RootElement.GetProperty("correctionDecisions").EnumerateArray());
            Assert.Equal(first.Id, Guid.Parse(decision.GetProperty("lineID").GetString()!));
            Assert.Equal(first.Text, decision.GetProperty("original").GetString());
            Assert.Equal("研究表明了 Mamba 结构", decision.GetProperty("corrected").GetString());
            Assert.False(decision.GetProperty("reverted").GetBoolean());

            var voicesJson = Assert.Single(File.ReadAllLines(voicesPath).Where(text => text.Length > 0));
            var voiceNode = JsonDocument.Parse(voicesJson).RootElement;
            Assert.Equal(second.Id, Guid.Parse(voiceNode.GetProperty("id").GetString()!));
            Assert.Equal(3, voiceNode.GetProperty("voice").GetInt32());
            Assert.Equal(new[] { "id", "voice" }, voiceNode.EnumerateObject().Select(property => property.Name));

            var storedMeta = JsonSerializer.Deserialize<SessionMeta>(File.ReadAllText(metaPath), JsonDefaults.Options)!;
            Assert.Equal("新说话人", storedMeta.VoiceNames!["3"]);

            // Round trip: reading everything back reproduces the edits and the raw transcript.
            var reloaded = SessionUserContentStore.Load(folder);
            var reloadedDecisions = JsonSerializer.Serialize(reloaded, SessionUserContentStore.Options);
            Assert.Contains("研究表明了 Mamba 结构", reloadedDecisions);
            var effective = SessionTextEdits.EffectiveLines(reloaded, [], lines);
            Assert.Equal("研究表明了 Mamba 结构", effective[0].Text);
            var tags = JSONLines.Decode<SessionVoiceTag>(File.ReadAllBytes(voicesPath));
            var assigned = SessionSpeakerEdits.ApplyVoiceTags(
                JSONLines.Decode<TranscriptLine>(File.ReadAllBytes(transcriptPath)), tags);
            Assert.Equal(3, assigned[1].Voice);
            Assert.Equal("开始实验", assigned[1].Text);
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch { /* best effort */ }
        }
    }
}
