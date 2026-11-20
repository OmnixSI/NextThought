using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>E15: one notes evidence row with its support state.</summary>
public sealed record SessionEvidenceRow(SessionEvidenceReference Reference)
{
    public string Title => Reference.Title;
    public string Claim => Reference.Claim;
    public string Location => Reference.Location ?? "";
    public string SupportLabel => Reference.Support.Label();
    public bool NeedsConfirmation => Reference.NeedsConfirmation;
    public string Badge => Reference.NeedsConfirmation ? "待确认" : "引文支持";
}
