using NextThought.Core;

namespace NextThought.Core;

/// <summary>
/// E22: the effective glossary layers the global table, the meeting's series table and the terms
/// found in that series' materials (Mac effectiveGlossary + adding(materialTerms)).
/// </summary>
public static class GlossaryMaterialTerms
{
    /// <summary>Adds material terms that no entry already covers (canonical spellings only).</summary>
    public static Glossary WithTerms(Glossary glossary, IReadOnlyList<string> terms)
    {
        if (terms.Count == 0) return glossary;
        var known = glossary.Entries.Select(entry => Glossary.StrictKey(entry.Canonical)).ToHashSet(StringComparer.Ordinal);
        var additions = new List<GlossaryEntry>();
        foreach (var term in terms)
        {
            var text = term.Trim();
            if (text.Length < 2) continue;
            if (!known.Add(Glossary.StrictKey(text))) continue;
            additions.Add(new GlossaryEntry(text));
        }
        return additions.Count == 0 ? glossary : glossary.Merging(new Glossary(additions));
    }
}
