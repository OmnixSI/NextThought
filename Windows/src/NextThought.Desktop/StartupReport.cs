using System.Text.Json;
using System.Text.Json.Serialization;

namespace NextThought.Desktop;

/// <summary>Source-generated startup report, so the AOT build can write it without reflection.</summary>
public sealed record StartupScreen(string Id, string Name, string Bounds, bool Primary);

public sealed record StartupPresenter(
    bool Enabled, string? Preferred, bool CanActivate, string? Camera, string? Slides, string Summary, bool Active);

public sealed record StartupReport(
    DateTimeOffset TestedAt,
    bool Passed,
    string Scope,
    double Seconds,
    string Section,
    bool Ready,
    string? Error,
    int HistoryCount,
    int TranscriptCount,
    string InitTimings,
    IReadOnlyList<string> Features,
    IReadOnlyList<StartupScreen> Screens,
    StartupPresenter Presenter,
    string? Version,
    string Framework,
    double WorkingSetMiB,
    double PrivateMiB,
    double GcHeapMiB,
    double GcCommittedMiB,
    int Threads,
    string? Process,
    double WindowWidth,
    double WindowHeight,
    double ClientWidth,
    double ClientHeight,
    double RenderScaling,
    double? ScreenScaling,
    string? ScreenBounds);

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(StartupReport))]
public partial class StartupJsonContext : JsonSerializerContext
{
}
