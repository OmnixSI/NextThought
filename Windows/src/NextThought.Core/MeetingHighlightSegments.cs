namespace NextThought.Core;

/// <summary>
/// E8: consecutive highlights of one meeting merged into the "重点" paragraphs shown under the
/// personal notes. Two highlights belong to the same paragraph when they are at most
/// <c>gapSeconds</c> apart; the paragraph keeps the first and last speaking time.
/// </summary>
public static class MeetingHighlightSegments
{
    public sealed record Segment(int StartSeconds, int EndSeconds, IReadOnlyList<SessionHighlight> Highlights)
    {
        public string RangeText => StartSeconds == EndSeconds
            ? NotesTime.Text(StartSeconds)
            : NotesTime.Text(StartSeconds) + "–" + NotesTime.Text(EndSeconds);
    }

    public static IReadOnlyList<Segment> Join(IEnumerable<SessionHighlight> highlights, int gapSeconds = 30)
    {
        var ordered = highlights
            .Where(highlight => highlight.Quote.Trim().Length > 0)
            .OrderBy(highlight => highlight.OffsetSeconds)
            .ToList();
        var segments = new List<Segment>();
        var current = new List<SessionHighlight>();
        var start = 0;
        var end = 0;
        foreach (var highlight in ordered)
        {
            if (current.Count == 0)
            {
                current.Add(highlight);
                start = end = highlight.OffsetSeconds;
                continue;
            }
            if (highlight.OffsetSeconds - end <= gapSeconds)
            {
                current.Add(highlight);
                end = Math.Max(end, highlight.OffsetSeconds);
                continue;
            }
            segments.Add(new Segment(start, end, current.ToList()));
            current = [highlight];
            start = end = highlight.OffsetSeconds;
        }
        if (current.Count > 0) segments.Add(new Segment(start, end, current));
        return segments;
    }
}
