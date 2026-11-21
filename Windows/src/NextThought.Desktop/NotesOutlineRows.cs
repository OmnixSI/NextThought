using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>E15: one numbered section of the real outline view (OutlineView.swift).</summary>
public sealed record NotesOutlineRow(int Number, string Title, int? At, IReadOnlyList<NotesOutlineLineRow> Lines)
{
    public string AtText => At is { } at ? TimeText(at) : "";
    public bool HasAt => At is not null;
    public static string TimeText(int seconds) => $"{seconds / 60:00}:{seconds % 60:00}";
}

/// <summary>E15: a point or sub-point of an outline section, with its transcript time.</summary>
public sealed record NotesOutlineLineRow(string Text, int? At, bool Secondary)
{
    public string AtText => At is { } at ? NotesOutlineRow.TimeText(at) : "";
    public bool HasAt => At is not null;
    public string Prefix => Secondary ? "–" : "•";
}

/// <summary>E15: 关键概念 row (term + meaning + time).</summary>
public sealed record NotesOutlineConceptRow(string Term, string Meaning, int? At)
{
    public string AtText => At is { } at ? NotesOutlineRow.TimeText(at) : "";
    public bool HasAt => At is not null;
}

/// <summary>E15: 待复习 row (text + time).</summary>
public sealed record NotesReviewRow(string Text, int? At)
{
    public string AtText => At is { } at ? NotesOutlineRow.TimeText(at) : "";
    public bool HasAt => At is not null;
}
