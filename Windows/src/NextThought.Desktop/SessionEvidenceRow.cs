using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>E15: one notes evidence row with its support state.</summary>
public sealed record SessionEvidenceRow(SessionEvidenceReference Reference, string ContextText = "", bool Located = false)
{
    /// <summary>Mac MeetingEvidenceSidebar: ±2 transcript lines around the cited line, ▸ on it.</summary>
    public bool HasContext => ContextText.Length > 0;
    public string MissingText => UiText.T("此原话无法定位。已有笔记仍保留，请查看完整转写核实。");
    public bool HasMissing => Reference.LineId is not null && !Located;
    public string Title => Reference.Title;
    public string Claim => Reference.Claim;
    public string Location => Reference.Location ?? "";
    public string SupportLabel => Reference.Support.Label();
    public bool NeedsConfirmation => Reference.NeedsConfirmation;
    public string Badge => Reference.NeedsConfirmation ? "待确认" : "引文支持";
    public SessionStructuredNotesTarget? Target => Reference.StructuredTarget;
}
