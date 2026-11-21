namespace NextThought.Core;

/// <summary>
/// task-62/①: item-level edits of the Mac MeetingNotesEditor. The document is the one the notes
/// generation produced or the user last saved (user-content.json editedNotes); the editor works on
/// indices because sections and items keep their order, and only the touched item changes.
/// </summary>
public static class StructuredNotesEditing
{
    public static StructuredNotesDoc EditItem(StructuredNotesDoc doc, int section, int item, string text)
    {
        if (section < 0 || section >= doc.Sections.Count) return doc;
        var sectionValue = doc.Sections[section];
        if (item < 0 || item >= sectionValue.Items.Count) return doc;
        var items = sectionValue.Items.ToList();
        items[item] = items[item] with { Text = text };
        var sections = doc.Sections.ToList();
        sections[section] = sectionValue with { Items = items };
        return doc with { Sections = sections };
    }

    /// <summary>Mac 「我已核实」: the item is no longer marked as needing confirmation.</summary>
    public static StructuredNotesDoc Confirm(StructuredNotesDoc doc, int section, int item)
    {
        if (section < 0 || section >= doc.Sections.Count) return doc;
        var sectionValue = doc.Sections[section];
        if (item < 0 || item >= sectionValue.Items.Count) return doc;
        var items = sectionValue.Items.ToList();
        items[item] = items[item] with { Uncertain = false };
        var sections = doc.Sections.ToList();
        sections[section] = sectionValue with { Items = items };
        return doc with { Sections = sections };
    }

    /// <summary>Mac MeetingNotesEditor save: 修改保存在独立的用户版本中, the generated notes stay untouched.</summary>
    public static SessionUserContent WithEditedNotes(SessionUserContent content, StructuredNotesDoc notes) =>
        content with
        {
            EditedNotes = notes,
            EditedMarkdown = null,
            BasedOnGeneratedAt = content.BasedOnGeneratedAt ?? DateTimeOffset.UtcNow
        };
}
