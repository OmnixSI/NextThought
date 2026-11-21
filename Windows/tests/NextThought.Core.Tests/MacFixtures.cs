using System.Text;
using System.Text.Json;
using NextThought.Core;

namespace NextThought.Core.Tests;

/// <summary>
/// Locates the Mac fixtures committed under <c>Windows/tests/Fixtures/mac</c>. The path is relative
/// to the repository root, so a test run outside the repository simply skips the fixture tests
/// instead of failing (each test documents that with an early return).
/// </summary>
internal static class MacFixtures
{
    public static string RepoRoot { get; } = FindRoot();

    public static string Directory { get; } = System.IO.Path.Combine(FindRoot(), "Windows", "tests", "Fixtures", "mac");

    public static bool Available => System.IO.File.Exists(FixturePath("README.md"))
                                   && System.IO.File.Exists(FixturePath("revisions.json"));

    public static string FixturePath(string name) => System.IO.Path.Combine(Directory, name);

    public static string SessionPath(string folder, string name) => System.IO.Path.Combine(Directory, folder, name);

    public static string Text(string path) => System.IO.File.ReadAllText(path, Encoding.UTF8);

    public static T Json<T>(string path) =>
        JsonSerializer.Deserialize<T>(Text(path), JsonDefaults.ArchiveOptions)
        ?? throw new InvalidOperationException($"could not decode {path}");

    public static IReadOnlyList<T> Lines<T>(string path) =>
        JSONLines.Decode<T>(System.IO.File.ReadAllBytes(path), JsonDefaults.ArchiveOptions);

    public static JsonDocument Document(string path) => JsonDocument.Parse(Text(path));

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (System.IO.File.Exists(System.IO.Path.Combine(directory.FullName, "Windows", "src", "NextThought.Core", "NextThought.Core.csproj")))
                return directory.FullName;
        }
        return AppContext.BaseDirectory;
    }
}
