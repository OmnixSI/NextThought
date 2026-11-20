namespace NextThought.Core;

/// <summary>
/// E22: merge bookkeeping for the glossary manager (Mac GlossaryStore.merge/size). Counts entries
/// and variants, not just rows, so "已加入 N 项" reflects what actually became searchable.
/// </summary>
public static class GlossaryMerge
{
    public static int Size(Glossary glossary) =>
        glossary.Entries.Count + glossary.Entries.Sum(entry => entry.Variants.Count);

    /// <summary>How many entries/variants the merge added (never negative).</summary>
    public static int Count(Glossary before, Glossary after) => Math.Max(0, Size(after) - Size(before));
}
