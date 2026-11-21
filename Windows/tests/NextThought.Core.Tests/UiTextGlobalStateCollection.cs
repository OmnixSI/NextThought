using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// The language tests and the assertions that read the UI language share the process-wide
/// <c>UiText.EnglishUi</c> flag. Keep them in one non-parallel collection: xUnit runs different
/// collections in parallel, which let a language switch from one class leak into another and made
/// <c>MeetingOutcomeTests</c> fail only occasionally (English wording instead of Chinese).
/// </summary>
[CollectionDefinition("UiTextGlobalState", DisableParallelization = true)]
public sealed class UiTextGlobalStateCollection
{
}
