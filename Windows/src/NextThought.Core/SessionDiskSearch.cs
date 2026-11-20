using System.Text;
using System.Text.Json;

namespace NextThought.Core;

/// <summary>B22: loads archived sessions from disk for history search; materials are never decoded.</summary>
public sealed class DiskSessionSearch
{
    private readonly string root;
    private readonly int cacheLimit;
    private readonly Dictionary<Guid, SessionSearchSnapshot> cache = [];

    public DiskSessionSearch(string root, int cacheLimit = 64)
    {
        this.root = Path.GetFullPath(root);
        this.cacheLimit = Math.Max(1, cacheLimit);
    }

    public long CachedByteCount => cache.Values.Sum(snapshot => (long)snapshot.ByteCount);

    public Task<SessionSearchProgress> SearchAsync(IReadOnlyList<Guid> ids, GlossarySearch terms, long revision,
        CancellationToken cancellationToken = default)
    {
        var progress = new SessionSearchProgress { Total = ids.Count };
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress.Checked++;
            if (!TryLoad(id, out var snapshot, out var issue))
            {
                progress.Unreadable++;
                progress.Issues[id] = issue!;
                continue;
            }
            var hits = SessionContentSearch.Hits(snapshot.Fields, terms);
            if (hits.Count > 0)
            {
                progress.Hits[id] = hits[0];
                progress.HitLists[id] = hits;
            }
            Cache(id, snapshot);
        }
        return Task.FromResult(progress);
    }

    private void Cache(Guid id, SessionSearchSnapshot snapshot)
    {
        cache[id] = snapshot;
        while (cache.Count > 1 && CachedByteCount > cacheLimit && cache.Keys.First() is var oldest)
            cache.Remove(oldest);
    }

    private bool TryLoad(Guid id, out SessionSearchSnapshot snapshot, out string? issue)
    {
        snapshot = default!;
        issue = null;
        var folder = Path.Combine(root, id.ToString("D"));
        var transcript = Path.Combine(folder, "transcript.jsonl");
        var lines = new List<TranscriptLine>();
        if (File.Exists(transcript))
        {
            try { lines = JSONLines.Decode<TranscriptLine>(File.ReadAllBytes(transcript)).ToList(); }
            catch { issue = "transcript unreadable"; return false; }
        }
        var start = lines.Count > 0 ? lines.Min(line => line.Date) : DateTimeOffset.UnixEpoch;
        var meta = new SessionMeta { Id = id, StartedAt = start, Mode = "meeting" };
        var sessionRevision = SessionProjectionBuilder.TranscriptSourceRevision(id, start, lines, []);
        var projection = SessionProjectionBuilder.Build(id, start, lines, [], null);
        var fields = SessionContentSearch.Fields(projection).ToList();
        var userPath = Path.Combine(folder, SessionUserContent.FileName);
        if (File.Exists(userPath))
        {
            SessionUserContent? user;
            try
            {
                user = JsonSerializer.Deserialize<SessionUserContent>(File.ReadAllText(userPath), JsonDefaults.Options);
            }
            catch (JsonException) { issue = "user content unreadable"; return false; }
            if (user is not null)
            {
                if (user.PersonalNotes.Trim() is { Length: > 0 } notes)
                    fields.Add(new SessionSearchField(SessionSearchFieldKind.Notes, notes, Id: "user:personal-notes"));
                fields.AddRange(SessionSearchSnapshotBuilder.UserFields(user));
            }
        }
        var summaryPath = Path.Combine(folder, "summary.json");
        if (File.Exists(summaryPath))
        {
            try
            {
                var summary = JsonSerializer.Deserialize<SessionSummary>(File.ReadAllText(summaryPath), JsonDefaults.Options);
                if (summary is not null) fields.AddRange(SessionSearchSnapshotBuilder.SummaryFields(summary));
            }
            catch (JsonException) { issue = "summary unreadable"; return false; }
        }
        snapshot = new SessionSearchSnapshot(fields, issue is null ? null : [issue]);
        return true;
    }
}

/// <summary>B22: a searchable snapshot with the latest answer per request and the user's edits.</summary>
public static class SessionSearchSnapshotBuilder
{
    public static SessionSearchSnapshot Create(IReadOnlyList<TranscriptLine> lines,
        IReadOnlyList<SessionQuestionEvent> questions, IReadOnlyList<SessionAnswerEvent> answers,
        StructuredNotesDoc? notes, SessionSummary? summary, SessionUserContent? user = null)
    {
        var effectiveSummary = user?.EditedSummary ?? summary;
        var start = lines.Count > 0 ? lines.Min(line => line.Date) : DateTimeOffset.UnixEpoch;
        var fields = new List<SessionSearchField>();
        if (lines.Count > 0)
            fields.AddRange(SessionContentSearch.Fields(SessionProjectionBuilder.Build(Guid.Empty, start, lines, [])));
        foreach (var latest in answers.GroupBy(answer => answer.Id).Select(group => group.Last()))
            fields.Add(new SessionSearchField(SessionSearchFieldKind.Answer, latest.Answer,
                Id: "answer:" + latest.Id + ":body", SourceRevision: latest.Id));
        if (effectiveSummary is not null) fields.AddRange(SummaryFields(effectiveSummary));
        if (user is not null)
        {
            if (user.PersonalNotes.Trim() is { Length: > 0 } notesText)
                fields.Add(new SessionSearchField(SessionSearchFieldKind.Notes, notesText, Id: "user:personal-notes"));
            fields.AddRange(UserFields(user));
        }
        return new SessionSearchSnapshot(fields);
    }

    public static IEnumerable<SessionSearchField> SummaryFields(SessionSummary summary)
    {
        if (summary.Title.Trim().Length > 0)
            yield return new SessionSearchField(SessionSearchFieldKind.Notes, summary.Title,
                Id: "summary:" + Guid.Empty + ":title");
        if (summary.Markdown.Trim().Length > 0)
            yield return new SessionSearchField(SessionSearchFieldKind.Notes, summary.Markdown,
                Id: "summary:" + Guid.Empty + ":markdown");
        foreach (var payload in (summary.StructuredAll ?? []).Concat(summary.Structured is { } single
                     && !(summary.StructuredAll ?? []).Any(existing => existing.Format == single.Format) ? [single] : []))
        {
            if (!payload.HasContent) continue;
            var ordinal = 0;
            foreach (var (text, location) in StructuredTexts(payload))
            {
                if (string.IsNullOrWhiteSpace(text)) continue;
                ordinal++;
                yield return new SessionSearchField(SessionSearchFieldKind.Notes, text, location,
                    Id: "summary:structured:" + payload.Format.Raw() + ":" + ordinal,
                    SourceRevision: payload.TranscriptSourceRevision);
            }
        }
    }

    public static IEnumerable<SessionSearchField> UserFields(SessionUserContent user)
    {
        foreach (var highlight in user.Highlights)
            yield return new SessionSearchField(SessionSearchFieldKind.Notes, highlight.Quote,
                Id: "user:highlight:" + highlight.Id);
    }

    private static IEnumerable<(string Text, string? Location)> StructuredTexts(StructuredNotesPayload payload) =>
        payload.Format switch
        {
            NotesFormat.Fishbone when payload.Fishbone is { } fishbone =>
                new[] { (fishbone.Head, fishbone.HeadAt is { } at ? NotesTime.Text(at) : null) }
                    .Concat(fishbone.Categories.SelectMany(category => new[] { (category.Name, (string?)null) }
                        .Concat(category.Causes.Select(cause => (cause.Text, cause.At is { } time ? NotesTime.Text(time) : null))))),
            NotesFormat.Outline when payload.Outline is { } outline =>
                outline.Sections.SelectMany(section => new[] { (section.Title, section.At is { } at ? NotesTime.Text(at) : null) }
                    .Concat(section.Points.SelectMany(point => new[] { (point.Text, point.At is { } pointAt ? NotesTime.Text(pointAt) : null) }
                        .Concat(point.Children.Select(child => (child.Text, child.At is { } childAt ? NotesTime.Text(childAt) : null)))))),
            _ => []
        };
}

