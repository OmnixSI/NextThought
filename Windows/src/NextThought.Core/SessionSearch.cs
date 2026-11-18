namespace NextThought.Core;

/// <summary>Where a search hit points when the user opens it (port of SessionContentTarget).</summary>
public abstract record SessionContentTarget
{
    public sealed record Transcript(Guid LineId) : SessionContentTarget;
    public sealed record Answer(Guid AnswerId) : SessionContentTarget;
    public sealed record Notes(Guid? NotesId) : SessionContentTarget;
    public sealed record PersonalNotes : SessionContentTarget;
    public sealed record Highlight(Guid HighlightId) : SessionContentTarget;
}

/// <summary>Navigation and display conveniences for archived-meeting hits.</summary>
public static class SessionSearchHits
{
    private const string PersonalNotesId = "user:personal-notes";
    private const string HighlightPrefix = "user:highlight:";

    public static SessionContentTarget? UserContentTarget(this SessionSearchHit hit)
    {
        if (hit.Id == PersonalNotesId) return new SessionContentTarget.PersonalNotes();
        if (hit.Id is { } id && id.StartsWith(HighlightPrefix, StringComparison.Ordinal)
            && Guid.TryParse(id[HighlightPrefix.Length..], out var highlight))
            return new SessionContentTarget.Highlight(highlight);
        return null;
    }

    public static SessionContentTarget Target(this SessionSearchHit hit)
    {
        if (hit.UserContentTarget() is { } user) return user;
        if (hit.LineId is { } lineId) return new SessionContentTarget.Transcript(lineId);
        var components = hit.Id?.Split(':') ?? [];
        var uuid = components.Select(component => Guid.TryParse(component, out var parsed) ? parsed : (Guid?)null)
            .LastOrDefault(value => value is not null);
        if (components.Length > 0 && components[0] == "answer" && uuid is { } answer)
            return new SessionContentTarget.Answer(answer);
        return new SessionContentTarget.Notes(components.Length > 0 && components[0] == "notes" ? uuid : null);
    }

    public static string Snippet(this SessionSearchHit hit) => hit.Excerpt;

    public static string? MisheardForm(this SessionSearchHit hit) => hit.IsMisheard ? hit.MatchedForm : null;

    public static string Label(this SessionSearchHit hit)
    {
        if (hit.Id == PersonalNotesId) return "个人笔记";
        if (hit.Id?.StartsWith(HighlightPrefix, StringComparison.Ordinal) == true) return "重点标记";
        return hit.Kind switch
        {
            SessionSearchFieldKind.Transcript => "转写",
            SessionSearchFieldKind.CorrectedTranscript => "校对转写",
            SessionSearchFieldKind.Translation => "译文",
            SessionSearchFieldKind.Question => "问题",
            SessionSearchFieldKind.Answer => "会中回答",
            _ => "纪要"
        };
    }
}

public sealed class SessionSearchProgress
{
    public int Checked { get; set; }
    public int Total { get; init; }
    public int Unreadable { get; set; }
    public Dictionary<Guid, SessionSearchHit> Hits { get; } = [];
    public Dictionary<Guid, IReadOnlyList<SessionSearchHit>> HitLists { get; } = [];
    public Dictionary<Guid, string> Issues { get; } = [];
}

/// <summary>Searchable text only: the canonical projection without materials or screenshots.</summary>
public sealed record SessionSearchSnapshot(IReadOnlyList<SessionSearchField> Fields, IReadOnlyList<string>? ReadIssues = null)
{
    public int ByteCount => Fields.Sum(item =>
        System.Text.Encoding.UTF8.GetByteCount(item.Text)
        + (item.AlternateText is { } alternate ? System.Text.Encoding.UTF8.GetByteCount(alternate) : 0)
        + (item.Id is { } id ? System.Text.Encoding.UTF8.GetByteCount(id) : 0)
        + 160);
}

/// <summary>
/// Cacheless history search over prepared snapshots (port of the matching part of SessionSearch;
/// the disk snapshot loader and byte-bounded cache are app-side in this port).
/// </summary>
public static class SessionSearch
{
    public static SessionSearchProgress Search(
        IReadOnlyList<Guid> ids, GlossarySearch terms, IReadOnlyDictionary<Guid, SessionSearchSnapshot> snapshots,
        IReadOnlyDictionary<Guid, GlossarySearch>? termsBySession = null,
        Action<SessionSearchProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = new SessionSearchProgress { Total = ids.Count };
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!snapshots.TryGetValue(id, out var snapshot))
            {
                result.Unreadable++;
                result.Issues[id] = "找不到这场会议的归档。";
            }
            else
            {
                var hits = SessionContentSearch.Hits(snapshot.Fields, termsBySession?.GetValueOrDefault(id) ?? terms);
                if (hits.Count > 0)
                {
                    result.Hits[id] = hits[0];
                    result.HitLists[id] = hits;
                }
                if (snapshot.ReadIssues is { Count: > 0 } issues)
                {
                    result.Unreadable++;
                    result.Issues[id] = string.Join("\n", issues);
                }
            }
            result.Checked++;
            if (result.Checked % 20 == 0 || result.Checked == result.Total) progress?.Invoke(result);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }
}
