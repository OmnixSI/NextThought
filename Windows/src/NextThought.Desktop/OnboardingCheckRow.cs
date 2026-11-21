namespace NextThought.Desktop;

/// <summary>E3: one row of the first-run setup checklist.</summary>
public sealed record OnboardingCheckRow(string Label, string State, string Detail, bool Ready);
