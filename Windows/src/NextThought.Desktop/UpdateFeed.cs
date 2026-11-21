using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>Reads a release feed from an HTTP(S) URL or a local file (the QA path).</summary>
public static class UpdateFeed
{
    public const string Default = NextThought.Platform.UpdateFeedPolicy.Default;

    /// <summary>
    /// The feed is fixed to HTTPS in normal runs. R8: only debug builds may read the QA override
    /// environment variables, so the formal package's binary has no QA switch at all.
    /// </summary>
    public static string Source => SourceFor("");

    /// <summary>E27: Mac <c>updateRepository</c> chooses the GitHub repository of the feed.</summary>
    public static string SourceFor(string repository) =>
#if DEBUG
        NextThought.Platform.UpdateFeedPolicy.ResolveSourceFor(repository,
            Environment.GetEnvironmentVariable("NEXTTHOUGHT_UPDATE_FEED"),
            Environment.GetEnvironmentVariable("NEXTTHOUGHT_QA") == "1");
#else
        NextThought.Platform.UpdateFeedPolicy.ResolveSourceFor(repository, null, false);
#endif

    public static Task<string> ReadAsync(string source, string? token, CancellationToken cancellationToken = default) =>
        NextThought.Platform.UpdateFeedPolicy.ReadAsync(source, token, cancellationToken);
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
    public static Task<NextThought.Platform.UpdateReport> RunAsync(string source, UpdateChannel channel,
        string? token = null, CancellationToken cancellationToken = default) =>
        NextThought.Platform.UpdateChecker.RunAsync(source, channel, CurrentVersion, CurrentBuild, token, cancellationToken);
}
