using System.Globalization;

namespace NextThought.Core;

/// <summary>
/// Matches a claim's quotation/time to saved transcript versions and decides whether a review is
/// still current (port of the evidence-builder helpers; note/structured-payload extraction stays
/// app-side until the full notes model is ported).
/// </summary>
public static partial class SessionEvidenceBuilder
{
    /// <summary>The support level a reference can claim from the saved transcript, strongest first.</summary>
    public static SessionEvidenceSupport Support(string? quote, string? location, IReadOnlyList<TranscriptLine> lines)
    {
        if (quote is { Length: > 0 })
        {
            var trimmed = quote.Trim();
            if (lines.Any(line => line.Text.Contains(trimmed, StringComparison.OrdinalIgnoreCase))) return SessionEvidenceSupport.Quoted;
            return SessionEvidenceSupport.QuoteNotFound;
        }
        if (location is { Length: > 0 } && Seconds(location) is { } seconds && lines.Count > 0)
        {
            var start = lines.Min(line => line.Date);
            var nearest = lines.MinBy(line => Math.Abs((line.Date - start).TotalSeconds - seconds));
            if (nearest is not null && Math.Abs((nearest.Date - start).TotalSeconds - seconds) <= 45)
                return SessionEvidenceSupport.TimeOnly;
        }
        return SessionEvidenceSupport.Unlocated;
    }

    /// <summary>A review survives only while all three revisions it saw are unchanged.</summary>
    public static bool IsReviewCurrent(SessionEvidenceReview? review, string claimRevision,
        string transcriptSourceRevision, string lineSourceRevision) =>
        review is not null
        && review.ClaimRevision == claimRevision
        && review.TranscriptSourceRevision == transcriptSourceRevision
        && review.LineSourceRevision == lineSourceRevision;

    /// <summary>Attaches a review, marking it stale when the claim or its sources changed since.</summary>
    public static SessionEvidenceReference WithReview(SessionEvidenceReference reference, SessionEvidenceReview review,
        string claimRevision, string transcriptSourceRevision, string lineSourceRevision) =>
        reference with
        {
            ReviewDecision = review.Decision,
            ReviewIsStale = !IsReviewCurrent(review, claimRevision, transcriptSourceRevision, lineSourceRevision),
            ClaimRevision = claimRevision,
            TranscriptSourceRevision = transcriptSourceRevision
        };

    /// <summary>Parses "mm:ss", "h:mm:ss" or plain seconds; null when malformed.</summary>
    public static double? Seconds(string location)
    {
        var parts = location.Trim().Split(':');
        if (parts.Length is < 1 or > 3) return null;
        double total = 0;
        foreach (var part in parts)
        {
            if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < 0) return null;
            total = total * 60 + value;
        }
        return total;
    }
}
