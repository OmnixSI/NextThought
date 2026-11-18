using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NextThought.Core;

/// <summary>
/// Applies a user edit to one generated structured-note node without replacing other views
/// (port of SessionEvidenceEditor). Paths use colon-separated segments, e.g.
/// <c>section:0:point:1</c> or <c>category:0:cause:1</c>.
/// </summary>
public static class SessionNotesEditor
{
    public static readonly JsonSerializerOptions RevisionOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static string PayloadRevision(StructuredNotesPayload payload) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, RevisionOptions))));

    /// <summary>Returns the edited payload, or null when the target no longer matches or the path is invalid.</summary>
    public static StructuredNotesPayload? Apply(StructuredNotesPayload payload, SessionStructuredNotesTarget target, string text)
    {
        if (PayloadRevision(payload) != target.PayloadRevision || payload.Format != target.Format) return null;
        var parts = target.Path.Split(':');
        return payload.Format switch
        {
            NotesFormat.Fishbone => EditFishbone(payload, parts, text),
            NotesFormat.Outline => EditOutline(payload, parts, text),
            NotesFormat.Viewpoints => EditViewpoints(payload, parts, text),
            _ => null
        };
    }

    private static StructuredNotesPayload? EditFishbone(StructuredNotesPayload payload, string[] parts, string text)
    {
        if (payload.Fishbone is not { } notes) return null;
        if (parts is ["head"]) return payload with { Fishbone = notes with { Head = text } };
        if (parts is ["category", var categoryText, "cause", var causeText]
            && int.TryParse(categoryText, out var category) && int.TryParse(causeText, out var cause)
            && category >= 0 && category < notes.Categories.Count
            && cause >= 0 && cause < notes.Categories[category].Causes.Count)
        {
            var categories = notes.Categories.ToList();
            var causes = categories[category].Causes.ToList();
            causes[cause] = causes[cause] with { Text = text };
            categories[category] = categories[category] with { Causes = causes };
            return payload with { Fishbone = notes with { Categories = categories } };
        }
        return null;
    }

    private static StructuredNotesPayload? EditOutline(StructuredNotesPayload payload, string[] parts, string text)
    {
        if (payload.Outline is not { } notes) return null;
        var sections = notes.Sections.ToList();
        switch (parts)
        {
            case ["section", var sectionText] when int.TryParse(sectionText, out var section)
                                                   && section >= 0 && section < sections.Count:
                sections[section] = sections[section] with { Title = text };
                return payload with { Outline = notes with { Sections = sections } };

            case ["section", var sectionText, "point", var pointText]
                when int.TryParse(sectionText, out var section) && int.TryParse(pointText, out var point)
                     && section >= 0 && section < sections.Count
                     && point >= 0 && point < sections[section].Points.Count:
            {
                var points = sections[section].Points.ToList();
                points[point] = points[point] with { Text = text };
                sections[section] = sections[section] with { Points = points };
                return payload with { Outline = notes with { Sections = sections } };
            }

            case ["section", var sectionText, "point", var pointText, "child", var childText]
                when int.TryParse(sectionText, out var section) && int.TryParse(pointText, out var point)
                     && int.TryParse(childText, out var child)
                     && section >= 0 && section < sections.Count
                     && point >= 0 && point < sections[section].Points.Count
                     && child >= 0 && child < sections[section].Points[point].Children.Count:
            {
                var points = sections[section].Points.ToList();
                var children = points[point].Children.ToList();
                children[child] = children[child] with { Text = text };
                points[point] = points[point] with { Children = children };
                sections[section] = sections[section] with { Points = points };
                return payload with { Outline = notes with { Sections = sections } };
            }

            case ["concept", var conceptText, var field]
                when int.TryParse(conceptText, out var concept) && concept >= 0 && concept < notes.Concepts.Count:
            {
                var concepts = notes.Concepts.ToList();
                concepts[concept] = field switch
                {
                    "term" => concepts[concept] with { Term = text },
                    "meaning" => concepts[concept] with { Meaning = text },
                    _ => null!
                };
                return concepts[concept] is null ? null : payload with { Outline = notes with { Concepts = concepts } };
            }

            case ["review", var reviewText]
                when int.TryParse(reviewText, out var review) && review >= 0 && review < notes.Review.Count:
            {
                var items = notes.Review.ToList();
                items[review] = items[review] with { Text = text };
                return payload with { Outline = notes with { Review = items } };
            }
        }
        return null;
    }

    private static StructuredNotesPayload? EditViewpoints(StructuredNotesPayload payload, string[] parts, string text)
    {
        if (payload.Viewpoints is not { } notes) return null;
        var issues = notes.Issues.ToList();
        switch (parts)
        {
            case ["topic"]:
                return payload with { Viewpoints = notes with { Topic = text } };

            case ["issue", var issueText, "position", var positionText, var field]
                when int.TryParse(issueText, out var issue) && int.TryParse(positionText, out var position)
                     && issue >= 0 && issue < issues.Count && position >= 0 && position < issues[issue].Positions.Count
                     && field is "party" or "text":
            {
                var positions = issues[issue].Positions.ToList();
                positions[position] = field == "party"
                    ? positions[position] with { Party = text }
                    : positions[position] with { Text = text };
                issues[issue] = issues[issue] with { Positions = positions };
                return payload with { Viewpoints = notes with { Issues = issues } };
            }

            case ["consensus", var indexText] when int.TryParse(indexText, out var index)
                                                   && index >= 0 && index < notes.Consensus.Count:
            {
                var items = notes.Consensus.ToList();
                items[index] = items[index] with { Text = text };
                return payload with { Viewpoints = notes with { Consensus = items } };
            }

            case ["disagreement", var indexText] when int.TryParse(indexText, out var index)
                                                      && index >= 0 && index < notes.Disagreements.Count:
            {
                var items = notes.Disagreements.ToList();
                items[index] = items[index] with { Text = text };
                return payload with { Viewpoints = notes with { Disagreements = items } };
            }

            case ["owner", var indexText, var field]
                when int.TryParse(indexText, out var index) && index >= 0 && index < notes.Owners.Count
                     && field is "who" or "task":
            {
                var items = notes.Owners.ToList();
                items[index] = field == "who" ? items[index] with { Who = text } : items[index] with { Task = text };
                return payload with { Viewpoints = notes with { Owners = items } };
            }
        }
        return null;
    }
}
