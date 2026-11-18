namespace NextThought.Core;

/// <summary>
/// The check step shared by template notes and structured notes: a time cited by the model has to
/// be inside the meeting and near a transcript line. Ported from <c>NotesVerifier</c>
/// (NotesTemplates.swift); the due-date parts of that enum arrive with the template port.
/// </summary>
public static class NotesVerifier
{
    /// <summary>
    /// Whether a time cited by the model has words behind it: inside the meeting (plus 90 s of slack)
    /// and, when the transcript's line times are known, within <paramref name="tolerance"/> seconds
    /// of a line.
    /// </summary>
    public static bool IsSupported(int seconds, IReadOnlyList<int> lineOffsets, double duration, int tolerance = 45)
        => seconds <= Math.Max(duration, 0) + 90
           && (lineOffsets.Count == 0 || lineOffsets.Any(offset => Math.Abs(offset - seconds) <= tolerance));
}

/// <summary>
/// Keeps a structured notes payload drawable: drops nodes whose time no transcript line supports
/// (the same test as <see cref="NotesVerifier"/>), caps the sizes, removes repeats and tidies the
/// text. Ported from <c>StructuredNotesCleaner</c> (StructuredNotes.swift).
/// </summary>
public static class StructuredNotesCleaner
{
    private sealed class Support
    {
        public required IReadOnlyList<int> LineOffsets { get; init; }
        public required double Duration { get; init; }
        public required int Tolerance { get; init; }
        public required bool English { get; init; }
        public int Dropped { get; set; }

        public bool Supports(int? at) => at is { } seconds
            && NotesVerifier.IsSupported(seconds, LineOffsets, Duration, Tolerance);

        /// <summary>A node that states something needs a time with words behind it; else it is dropped.</summary>
        public int? Accept(int? at)
        {
            if (Supports(at)) return at;
            Dropped++;
            return null;
        }
    }

    /// <summary>The cleaned payload, or null when too little is left to draw (fewer than two nodes).</summary>
    public static StructuredNotesPayload? Clean(StructuredNotesPayload payload, IReadOnlyList<int> lineOffsets,
        double duration, int tolerance = 45)
    {
        var support = new Support
        {
            LineOffsets = lineOffsets,
            Duration = duration,
            Tolerance = tolerance,
            English = payload.IsEnglish
        };
        var result = payload with { Failed = null };
        switch (payload.Format)
        {
            case NotesFormat.Standard:
                return null;
            case NotesFormat.Fishbone:
                if (payload.Fishbone is not { } fishbone || CleanFishbone(fishbone, support) is not { } cleanedFishbone) return null;
                result = result with { Fishbone = cleanedFishbone, Outline = null, Viewpoints = null };
                break;
            case NotesFormat.Outline:
                if (payload.Outline is not { } outline || CleanOutline(outline, support) is not { } cleanedOutline) return null;
                result = result with { Outline = cleanedOutline, Fishbone = null, Viewpoints = null };
                break;
            case NotesFormat.Viewpoints:
                if (payload.Viewpoints is not { } viewpoints || CleanViewpoints(viewpoints, support) is not { } cleanedViewpoints) return null;
                result = result with { Viewpoints = cleanedViewpoints, Fishbone = null, Outline = null };
                break;
        }
        return result with { DroppedCount = payload.DroppedCount + support.Dropped };
    }

    // ---- text ----

    /// <summary>One line, trimmed, at most <paramref name="count"/> characters (the last one "…" when cut).</summary>
    private static string Limit(string text, int count)
    {
        var flat = text.Replace("\n", " ").Trim();
        if (flat.Length <= count) return flat;
        return flat[..Math.Max(1, count - 1)] + "…";
    }

    /// <summary>Letters and digits only, lowercased: "缓存过期！" and "缓存过期" are the same node.</summary>
    private static string Key(string text) => new(text.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    /// <summary>Supported, de-duplicated, capped list of statements.</summary>
    private static List<TimedText> CleanTimed(IReadOnlyList<TimedText> items, int textLimit, int cap,
        HashSet<string> seen, Support support)
    {
        var result = new List<TimedText>();
        foreach (var item in items)
        {
            if (result.Count >= cap) break;
            var text = Limit(item.Text, textLimit);
            if (text.Length == 0) continue;
            if (support.Accept(item.At) is not { } at) continue;
            if (!seen.Add(Key(text))) continue;
            result.Add(new TimedText(text, at));
        }
        return result;
    }

    // ---- fishbone ----

    private static FishboneNotes? CleanFishbone(FishboneNotes source, Support support)
    {
        var seen = new HashSet<string>();
        var categories = new List<FishboneCategory>();
        foreach (var category in source.Categories)
        {
            var name = Limit(category.Name, StructuredNotesLimits.NameCharacters);
            var causes = CleanTimed(category.Causes, StructuredNotesLimits.CauseCharacters,
                StructuredNotesLimits.CausesPerCategory, seen, support);
            if (causes.Count == 0) continue;
            var existing = categories.FindIndex(item => Key(item.Name) == Key(name));
            if (existing >= 0)
            {
                var merged = categories[existing].Causes.Concat(causes).Take(StructuredNotesLimits.CausesPerCategory).ToList();
                categories[existing] = categories[existing] with { Causes = merged };
            }
            else if (categories.Count < StructuredNotesLimits.Categories)
            {
                categories.Add(new FishboneCategory(name.Length == 0 ? support.English ? "Other" : "其他" : name, causes));
            }
        }
        if (categories.Sum(category => category.Causes.Count) < 2) return null;
        var head = Limit(source.Head, StructuredNotesLimits.HeadCharacters);
        return new FishboneNotes(head, support.Supports(source.HeadAt) ? source.HeadAt : null, categories);
    }

    // ---- outline ----

    private static OutlineNotes? CleanOutline(OutlineNotes source, Support support)
    {
        var budget = StructuredNotesLimits.OutlineNodes;
        var seen = new HashSet<string>();
        var sections = new List<OutlineSection>();
        foreach (var section in source.Sections)
        {
            if (sections.Count >= StructuredNotesLimits.Sections || budget <= 0) break;
            var points = new List<OutlinePoint>();
            foreach (var point in section.Points)
            {
                if (points.Count >= StructuredNotesLimits.PointsPerSection || budget <= 0) break;
                var text = Limit(point.Text, StructuredNotesLimits.PointCharacters);
                if (text.Length == 0) continue;
                if (support.Accept(point.At) is not { } at) continue;
                if (!seen.Add(Key(text))) continue;
                budget--;
                var room = Math.Min(StructuredNotesLimits.ChildrenPerPoint, Math.Max(0, budget));
                var children = CleanTimed(point.Children, StructuredNotesLimits.PointCharacters, room, seen, support);
                budget -= children.Count;
                points.Add(new OutlinePoint(text, at, children));
            }
            if (points.Count == 0) continue;
            var title = Limit(section.Title, StructuredNotesLimits.TitleCharacters);
            var fallback = support.English ? $"Section {sections.Count + 1}" : $"第 {sections.Count + 1} 部分";
            sections.Add(new OutlineSection(title.Length == 0 ? fallback : title,
                support.Supports(section.At) ? section.At : points[0].At, points));
        }

        var concepts = new List<OutlineConcept>();
        var terms = new HashSet<string>();
        foreach (var concept in source.Concepts)
        {
            if (concepts.Count >= StructuredNotesLimits.Concepts) break;
            var term = Limit(concept.Term, StructuredNotesLimits.TermCharacters);
            var meaning = Limit(concept.Meaning, StructuredNotesLimits.MeaningCharacters);
            if (term.Length == 0 || meaning.Length == 0) continue;
            if (support.Accept(concept.At) is not { } at) continue;
            if (!terms.Add(Key(term))) continue;
            concepts.Add(new OutlineConcept(term, meaning, at));
        }

        var review = CleanTimed(source.Review, StructuredNotesLimits.PointCharacters,
            StructuredNotesLimits.Review, seen, support);
        var pointCount = sections.Sum(section => section.Points.Count);
        if (pointCount + concepts.Count + review.Count < 2) return null;
        return new OutlineNotes(sections, concepts, review);
    }

    // ---- viewpoints ----

    private static ViewpointMatrix? CleanViewpoints(ViewpointMatrix source, Support support)
    {
        // Every side named in `parties` or in a position, once, in order of appearance.
        var declared = new List<string>();
        foreach (var party in source.Parties.Concat(source.Issues.SelectMany(issue => issue.Positions.Select(position => position.Party))))
        {
            var name = Limit(party, StructuredNotesLimits.PartyCharacters);
            if (name.Length == 0) continue;
            if (!declared.Any(existing => Key(existing) == Key(name))) declared.Add(name);
        }
        var known = declared;

        var seen = new HashSet<string>();
        var issues = new List<ViewpointIssue>();
        foreach (var issue in source.Issues)
        {
            if (issues.Count >= StructuredNotesLimits.Issues) break;
            var positions = new List<ViewpointPosition>();
            foreach (var position in issue.Positions)
            {
                var name = Limit(position.Party, StructuredNotesLimits.PartyCharacters);
                var party = known.FirstOrDefault(knownName => Key(knownName) == Key(name));
                if (party is null) continue;
                var text = Limit(position.Text, StructuredNotesLimits.ViewCharacters);
                if (text.Length == 0) continue;
                if (positions.Count(item => item.Party == party) >= StructuredNotesLimits.PositionsPerCell) continue;
                if (support.Accept(position.At) is not { } at) continue;
                if (!seen.Add(Key(party) + "|" + Key(text))) continue;
                positions.Add(new ViewpointPosition(party, text, at));
            }
            if (positions.Count == 0) continue;
            var title = Limit(issue.Title, StructuredNotesLimits.TitleCharacters);
            issues.Add(new ViewpointIssue(title.Length == 0 ? support.English ? $"Issue {issues.Count + 1}" : $"议题 {issues.Count + 1}" : title, positions));
        }

        // Columns: sides that said something, at most five.
        var used = known.Where(party => issues.Any(issue => issue.Positions.Any(position => position.Party == party))).ToList();
        var parties = used.Take(StructuredNotesLimits.Parties).ToList();
        issues = issues
            .Select(issue => new ViewpointIssue(issue.Title, issue.Positions.Where(position => parties.Contains(position.Party)).ToList()))
            .Where(issue => issue.Positions.Count > 0)
            .ToList();

        var consensus = CleanTimed(source.Consensus, StructuredNotesLimits.ViewCharacters,
            StructuredNotesLimits.ConsensusItems, seen, support);
        var disagreements = CleanTimed(source.Disagreements, StructuredNotesLimits.ViewCharacters,
            StructuredNotesLimits.ConsensusItems, seen, support);

        var owners = new List<ViewpointOwner>();
        foreach (var owner in source.Owners)
        {
            if (owners.Count >= StructuredNotesLimits.Owners) break;
            var task = Limit(owner.Task, StructuredNotesLimits.ViewCharacters);
            if (task.Length == 0) continue;
            if (support.Accept(owner.At) is not { } at) continue;
            var who = Limit(owner.Who, StructuredNotesLimits.PartyCharacters);
            if (!seen.Add("owner|" + Key(who) + "|" + Key(task))) continue;
            var due = Limit(owner.Due ?? "", 24);
            owners.Add(new ViewpointOwner(who, task, due.Length == 0 ? null : due, at));
        }

        var nodes = issues.Sum(issue => issue.Positions.Count) + consensus.Count + disagreements.Count + owners.Count;
        if (nodes < 2) return null;
        return new ViewpointMatrix(
            Limit(source.Topic, StructuredNotesLimits.HeadCharacters),
            support.Supports(source.TopicAt) ? source.TopicAt : null,
            parties, issues, consensus, disagreements, owners);
    }
}
