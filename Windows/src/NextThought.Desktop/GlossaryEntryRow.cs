using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>E22: one glossary entry in the glossary window.</summary>
public sealed record GlossaryEntryRow(GlossaryEntry Entry, Action<GlossaryEntry> Remove)
{
    public string Canonical => Entry.Canonical;
    public string Variants => string.Join(" · ", Entry.Variants);
    public bool HasVariants => Entry.Variants.Count > 0;
    public string MisheardBadge => HasVariants ? "误听标记" : "";
    public bool IsPerson => Entry.Kind == GlossaryKind.Person;
    public string VariantsLine => HasVariants ? "→ " + string.Join("、", Entry.Variants) : "";
    public string Note => Entry.Note ?? "";
}
