namespace NextThought.Core;

/// <summary>
/// Who a sentence is given to by hand (port of the Mac <c>LineSpeaker</c> enum in
/// Sources/NextThought/SpeakerID.swift: <c>.me</c> / <c>.voice(Int)</c> / <c>.someoneNew</c>).
/// </summary>
public readonly record struct LineSpeaker
{
    public enum KindValue
    {
        Me,
        Voice,
        SomeoneNew
    }

    public KindValue Kind { get; private init; }
    public int Voice { get; private init; }

    public static LineSpeaker Me => new() { Kind = KindValue.Me };

    public static LineSpeaker SomeoneNew => new() { Kind = KindValue.SomeoneNew };

    public static LineSpeaker FromVoice(int voice) => new() { Kind = KindValue.Voice, Voice = voice };
}

/// <summary>
/// J6 Core data layer for speaker reassignment, mirroring the Mac app code in
/// Sources/NextThought/SpeakerID.swift:
/// <c>AppState.assignLine</c> / <c>SessionArchive.assignVoice</c> (one sentence moves to the user,
/// a chosen voice, or someone new), <c>AppState.mergeVoices</c> / <c>SessionArchive.mergeVoice</c>,
/// <c>SessionArchive.setVoiceName</c> / <c>setMeVoice</c>.
///
/// The archive keeps the raw transcript untouched: a change is a <c>voices.jsonl</c> tag
/// (<see cref="SessionVoiceTag"/>, Mac keys id/voice, a later tag wins) plus meta.json's
/// <c>voiceNames</c> / <c>meVoices</c>. These helpers return the updated pieces; the platform
/// appends the tag and writes meta.json through its normal store path.
/// </summary>
public static class SessionSpeakerEdits
{
    /// <summary>The name a Mac gives a new person when one sentence is moved to "someone new".</summary>
    public const string NewSpeakerName = "新说话人";

    /// <summary>Mac assignVoice: which voice one line goes to, and the meta changes that go with it.</summary>
    public static (SessionMeta Meta, SessionVoiceTag Tag) AssignLine(
        SessionMeta meta, IReadOnlyList<TranscriptLine> lines, Guid lineId, LineSpeaker target)
    {
        var meVoices = new SortedSet<int>(meta.MeVoices ?? []);
        var names = meta.VoiceNames is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(meta.VoiceNames, StringComparer.Ordinal);
        int voice;
        switch (target.Kind)
        {
            case LineSpeaker.KindValue.Voice:
                voice = target.Voice;
                break;
            case LineSpeaker.KindValue.Me:
                // Reuse the voice already marked as the user; otherwise make the next free one.
                voice = meVoices.Count > 0
                    ? meVoices.Min
                    : SpeakerLabels.NewVoice(lines, meta.VoiceNames, meVoices);
                meVoices.Add(voice);
                break;
            default:
                voice = SpeakerLabels.NewVoice(lines, meta.VoiceNames, meVoices);
                names[voice.ToString(System.Globalization.CultureInfo.InvariantCulture)] =
                    SpeakerLabels.CleanName(NewSpeakerName) ?? NewSpeakerName;
                break;
        }
        var updated = meta with
        {
            VoiceNames = names.Count == 0 ? null : names,
            MeVoices = meVoices.Count == 0 ? null : meVoices.ToArray()
        };
        return (updated, new SessionVoiceTag(lineId, voice));
    }

    /// <summary>
    /// Mac mergeVoice: every sentence of <paramref name="from"/> becomes <paramref name="into"/>
    /// (a later voice tag wins over the raw line), and the name and me mark move along.
    /// </summary>
    public static (SessionMeta Meta, IReadOnlyList<SessionVoiceTag> Tags) MergeVoices(
        SessionMeta meta, IReadOnlyList<TranscriptLine> lines, IReadOnlyList<SessionVoiceTag> existingTags,
        int from, int into)
    {
        if (from == into) return (meta, []);
        var effective = ApplyVoiceTags(lines, existingTags);
        var tags = effective.Where(line => line.Voice == from)
            .Select(line => new SessionVoiceTag(line.Id, into)).ToList();
        return (meta with
        {
            VoiceNames = SpeakerLabels.Merged(meta.VoiceNames, from, into),
            MeVoices = SpeakerLabels.MergedMe(meta.MeVoices, from, into)
        }, tags);
    }

    /// <summary>Mac setVoiceName: a name (nil or blank clears it) kept in meta.json's voiceNames.</summary>
    public static SessionMeta SetVoiceName(SessionMeta meta, int voice, string? name)
    {
        var names = meta.VoiceNames is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(meta.VoiceNames, StringComparer.Ordinal);
        var key = voice.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var cleaned = name is null ? null : SpeakerLabels.CleanName(name);
        if (cleaned is null) names.Remove(key);
        else names[key] = cleaned;
        return meta with { VoiceNames = names.Count == 0 ? null : names };
    }

    /// <summary>Mac setMeVoice: the "this is me" mark kept in meta.json's meVoices (sorted).</summary>
    public static SessionMeta SetMeVoice(SessionMeta meta, int voice, bool isMe)
    {
        var voices = new SortedSet<int>(meta.MeVoices ?? []);
        if (isMe) voices.Add(voice); else voices.Remove(voice);
        return meta with { MeVoices = voices.Count == 0 ? null : voices.ToArray() };
    }

    /// <summary>
    /// The transcript as read back: voices.jsonl tags override the line's own voice, later tags
    /// winning (SessionArchive merges the file the same way).
    /// </summary>
    public static List<TranscriptLine> ApplyVoiceTags(
        IReadOnlyList<TranscriptLine> lines, IReadOnlyList<SessionVoiceTag> tags)
    {
        var voices = new Dictionary<Guid, int>();
        foreach (var tag in tags) voices[tag.Id] = tag.Voice;
        return lines.Select(line => voices.TryGetValue(line.Id, out var voice)
            ? line with { Voice = voice }
            : line).ToList();
    }
}

/// <summary>
/// J6 Core data layer for hand-editing one transcript sentence, mirroring
/// Sources/NextThought/AppState+Notes.swift and the <c>SessionDetail.effectiveLines</c> composition
/// of Sources/NextThought/SessionArchive.swift.
///
/// The recognised text stays in transcript.jsonl; the edit is a correction over the whole sentence
/// in user-content.json's <c>correctionDecisions</c>, so it shows in the corrected transcript,
/// search, exports, and in the notes the next time they are organised. Saving the recognised text
/// again takes the edit away.
/// </summary>
public static class SessionTextEdits
{
    /// <summary>Mac SessionDetail.effectiveLines: the transcript with the user decisions applied.</summary>
    public static List<TranscriptLine> EffectiveLines(
        SessionUserContent content, IReadOnlyList<TranscriptCorrection> generatedCorrections,
        IReadOnlyList<TranscriptLine> lines) =>
        NotesCorrections.Apply(content.Corrections(generatedCorrections, lines), lines);

    /// <summary>Mac SessionDetail.effectiveNotes' corrections: user decisions over the model's fixes.</summary>
    public static IReadOnlyList<TranscriptCorrection> EffectiveCorrections(
        SessionUserContent content, IReadOnlyList<TranscriptCorrection> generatedCorrections,
        IReadOnlyList<TranscriptLine> lines) =>
        content.Corrections(generatedCorrections, lines);

    /// <summary>Mac AppState.editLine: rewrite one sentence by hand (transcript.jsonl never changes).</summary>
    public static SessionUserContent EditLine(
        SessionUserContent content, IReadOnlyList<TranscriptCorrection> generatedCorrections,
        IReadOnlyList<TranscriptLine> lines, Guid lineId, string original, string text)
    {
        var corrected = text.Trim();
        if (corrected.Length == 0) return content;
        var index = -1;
        for (var position = 0; position < lines.Count; position++)
            if (lines[position].Id == lineId) { index = position; break; }
        if (index < 0) return content;
        // The proofreading fixes inside this sentence: the whole-sentence edit stands in for them
        // (and they come back when the edit is taken away).
        var inside = content.Corrections(generatedCorrections, lines)
            .Where(correction => correction.Line == index && correction.Original != original)
            .ToList();
        var decisions = content.CorrectionDecisions
            .Where(decision => decision.LineId != lineId).ToList();
        if (corrected != original)
        {
            foreach (var fix in inside)
                decisions.Add(new SessionCorrectionDecision(
                    Guid.NewGuid().ToString("D"), lineId, fix.Original, fix.Corrected, true));
            decisions.Add(new SessionCorrectionDecision(
                Guid.NewGuid().ToString("D"), lineId, original, corrected, false));
        }
        return content with { CorrectionDecisions = decisions };
    }

    /// <summary>Mac AppState.toggleCorrection: undo or redo one proofreading fix.</summary>
    public static SessionUserContent ToggleCorrection(
        SessionUserContent content, IReadOnlyList<TranscriptCorrection> effectiveCorrections,
        IReadOnlyList<TranscriptLine> lines, string correctionId)
    {
        var correction = effectiveCorrections.FirstOrDefault(item => item.Id == correctionId);
        if (correction is null || correction.Line < 0 || correction.Line >= lines.Count) return content;
        var line = lines[correction.Line];
        var decisions = content.CorrectionDecisions
            .Where(decision => !(decision.LineId == line.Id && decision.Original == correction.Original))
            .ToList();
        decisions.Add(new SessionCorrectionDecision(
            Guid.NewGuid().ToString("D"), line.Id, correction.Original, correction.Corrected,
            !correction.Reverted));
        return content with { CorrectionDecisions = decisions };
    }
}
