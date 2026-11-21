namespace NextThought.Core;

/// <summary>
/// Resolves evidence to a transcript line without turning a missing passage into the nearest
/// unrelated utterance (port of SessionTranscriptLocator.swift).
/// </summary>
public static class SessionTranscriptLocator
{
    public const double TimestampToleranceSeconds = 3;

    public static int? Index(Guid? lineId, int? offsetSeconds, DateTimeOffset startedAt, IReadOnlyList<TranscriptLine> lines)
    {
        if (lineId is { } id)
        {
            var exact = -1;
            for (var index = 0; index < lines.Count; index++)
                if (lines[index].Id == id) { exact = index; break; }
            if (exact >= 0) return exact;
        }
        if (offsetSeconds is not { } offset || offset < 0) return null;
        var requested = startedAt.AddSeconds(offset);
        (int Index, double Distance, double Anchor)? best = null;
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            double distance;
            var start = line.StartedAt;
            var end = line.EndedAt;
            if (start is { } from && end is { } to && from <= to)
            {
                distance = requested < from ? (from - requested).TotalSeconds
                    : requested > to ? (requested - to).TotalSeconds
                    : 0;
            }
            else
            {
                // Older archives have only the timestamp printed beside the transcript line.
                distance = Math.Abs((line.Date - requested).TotalSeconds);
            }
            if (distance > TimestampToleranceSeconds) continue;
            var anchor = Math.Abs((line.Date - requested).TotalSeconds);
            if (best is null || distance < best.Value.Distance
                || (Math.Abs(distance - best.Value.Distance) < 1e-9 && anchor < best.Value.Anchor))
                best = (index, distance, anchor);
        }
        return best?.Index;
    }
}
