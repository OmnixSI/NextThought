using System.Text.Json;
using System.Text.Json.Nodes;
using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// A6/§4.3–4.4 storage-layer checks that need the session store (Platform): upper-case folders and
/// identifiers, and the per-meeting meta keeping exactly the keys a Mac meta carried (no added
/// platform/inPerson defaults).
/// </summary>
public sealed class MacMetaSerializationTests
{
    private static string Temp(string tag) =>
        Path.Combine(Path.GetTempPath(), "nt-a6-" + tag + "-" + Guid.NewGuid().ToString("N"));

    private static readonly Guid LowerId = Guid.Parse("11111111-1111-4111-8111-111111111111");

    [Fact]
    public async Task SessionFolderAndMetaIdAreUppercase()
    {
        var root = Temp("session-id");
        try
        {
            var paths = new AppPaths(root);
            var store = new SessionStore(paths);
            await store.CreateAsync(new SessionMeta { Id = LowerId, Title = "A6" });
            var folder = Path.Combine(paths.Sessions, "11111111-1111-4111-8111-111111111111");
            Assert.True(Directory.Exists(folder));
            var raw = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "meta.json")))!.AsObject();
            Assert.Equal("11111111-1111-4111-8111-111111111111", raw["id"]!.GetValue<string>());
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public async Task MacMetaRewriteKeepsItsKeySetWithoutPlatformOrInPerson()
    {
        var root = Temp("meta-keys");
        try
        {
            var paths = new AppPaths(root);
            var folder = Path.Combine(paths.Sessions, "11111111-1111-4111-8111-111111111111");
            Directory.CreateDirectory(folder);
            var fixtureMeta = RepoFixture("session-meta.json");
            File.Copy(fixtureMeta, Path.Combine(folder, "meta.json"));
            var store = new SessionStore(paths);
            var meta = store.Load(LowerId).Meta;
            await store.SetSummaryStateAsync(meta, "ready");

            var keys = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "meta.json")))!.AsObject()
                .Select(pair => pair.Key).ToHashSet(StringComparer.Ordinal);
            var original = JsonNode.Parse(File.ReadAllText(fixtureMeta))!.AsObject()
                .Select(pair => pair.Key).ToHashSet(StringComparer.Ordinal);
            Assert.Equal(original, keys);
            Assert.DoesNotContain("platform", keys);
            Assert.DoesNotContain("inPerson", keys);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    private static string RepoFixture(string name)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = System.IO.Path.Combine(directory.FullName, "Windows", "tests", "Fixtures", "mac", name);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException(name);
    }
}
