using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>Reads a release feed from an HTTP(S) URL or a local file (the QA path).</summary>
public static class UpdateFeed
{
    public const string Default = NextThought.Platform.UpdateFeedPolicy.Default;

    /// <summary>
    /// The feed is fixed to HTTPS in normal runs; overrides only work with
    /// NEXTTHOUGHT_QA=1 and never over plain HTTP.
    /// </summary>
    public static string Source => NextThought.Platform.UpdateFeedPolicy.ResolveSource(Environment.GetEnvironmentVariable);

    public static Task<string> ReadAsync(string source, CancellationToken cancellationToken = default) =>
        NextThought.Platform.UpdateFeedPolicy.ReadAsync(source, cancellationToken);
}

/// <summary>The update check itself: read the feed, pick a candidate, compare with this build.</summary>
public static class UpdateCheck
{
    public static string CurrentVersion
    {
        get
        {
            var version = typeof(UpdateCheck).Assembly.GetName().Version ?? new Version(0, 0, 0);
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    public static int CurrentBuild => (typeof(UpdateCheck).Assembly.GetName().Version ?? new Version(0, 0, 0)).Revision;

    /// <summary>
    /// The check runs in <see cref="NextThought.Platform.UpdateChecker"/> so the fake-release
    /// directory flow is testable; this app supplies its own assembly version and build.
    /// </summary>
    public static Task<NextThought.Platform.UpdateReport> RunAsync(string source, UpdateChannel channel, CancellationToken cancellationToken = default) =>
        NextThought.Platform.UpdateChecker.RunAsync(source, channel, CurrentVersion, CurrentBuild, cancellationToken);
}
