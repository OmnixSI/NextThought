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

    [Fact]
    public async Task BackupExportIdentifiersCanBeLowercasedAndReimported()
    {
        var source = Temp("uuid-src");
        var copy = Temp("uuid-copy");
        var archive = Path.Combine(Temp("uuid-zip"), "export.ntbackup");
        var rewritten = Path.Combine(Temp("uuid-rewritten"), "lowercase.ntbackup");
        Directory.CreateDirectory(Path.GetDirectoryName(archive)!);
        Directory.CreateDirectory(Path.GetDirectoryName(rewritten)!);
        try
        {
            var sourcePaths = new AppPaths(source);
            var fixture = Path.Combine(RepoWindows(), "tests", "Fixtures", "mac-backup",
                "nt-mac-fixtures-26.5.2-20261121.zip");
            var expanded = Path.Combine(Temp("uuid-fixture"), "fixtures");
            Directory.CreateDirectory(expanded);
            System.IO.Compression.ZipFile.ExtractToDirectory(fixture, expanded);
            var macArchive = Path.Combine(expanded, "nt-fixtures", "ntbackup", "all-scenarios.ntbackup");
            await new BackupService(sourcePaths).ImportAsync(macArchive);
            await new BackupService(sourcePaths).ExportAsync(archive);

            // §4.3: lower-case every identifier in values and in entry names, then import.
            static string LowerIds(string text) =>
                System.Text.RegularExpressions.Regex.Replace(text,
                    "[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}",
                    match => match.Value.ToLowerInvariant());
            using (var input = System.IO.Compression.ZipFile.OpenRead(archive))
            using (var output = System.IO.Compression.ZipFile.Open(rewritten, System.IO.Compression.ZipArchiveMode.Create))
            {
                var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
                string? manifestJson = null;
                foreach (var entry in input.Entries)
                {
                    if (entry.FullName == "manifest.json")
                    {
                        using var manifestReader = new StreamReader(entry.Open());
                        manifestJson = LowerIds(manifestReader.ReadToEnd());
                        continue;
                    }
                    var name = LowerIds(entry.FullName);
                    byte[] bytes;
                    using (var sourceStream = entry.Open())
                    using (var buffer = new MemoryStream())
                    {
                        sourceStream.CopyTo(buffer);
                        bytes = buffer.ToArray();
                    }
                    var isText = name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                                 || name.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase)
                                 || name.EndsWith(".plist", StringComparison.OrdinalIgnoreCase);
                    if (isText) bytes = System.Text.Encoding.UTF8.GetBytes(LowerIds(System.Text.Encoding.UTF8.GetString(bytes)));
                    hashes[name] = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
                    var target = output.CreateEntry(name);
                    using var targetStream = target.Open();
                    targetStream.Write(bytes);
                }
                // The manifest is content-addressed: keep the fileHashes truthful after lower-casing.
                if (manifestJson is not null)
                {
                    var node = JsonNode.Parse(manifestJson)!.AsObject();
                    if (node.ContainsKey("fileHashes"))
                    {
                        var fileHashes = new JsonObject();
                        foreach (var (name, hash) in hashes) fileHashes[name] = hash;
                        node["fileHashes"] = fileHashes;
                    }
                    using var writer = new StreamWriter(output.CreateEntry("manifest.json").Open());
                    writer.Write(node.ToJsonString());
                }
            }

            var result = await new BackupService(new AppPaths(copy)).ImportAsync(rewritten);
            Assert.Equal(4, result.WorkspacesAdded);
            Assert.Equal(4, result.SessionsAdded);
            var sourceData = new WorkspaceStore(source).Load();
            var copyData = new WorkspaceStore(copy).Load();
            Assert.Equal(sourceData.Series.Count, copyData.Series.Count);
            Assert.Equal(sourceData.Series.Sum(series => series.Lessons.Count),
                copyData.Series.Sum(series => series.Lessons.Count));
            Assert.Equal(sourceData.Series.Sum(series => series.QuestionBank.Count),
                copyData.Series.Sum(series => series.QuestionBank.Count));
            Assert.All(Directory.GetFiles(Path.Combine(copy, "Workspaces"), "*.json"),
                file => Assert.Equal(Path.GetFileNameWithoutExtension(file).ToUpperInvariant(),
                    Path.GetFileNameWithoutExtension(file)));
            Assert.All(Directory.GetDirectories(new AppPaths(copy).Sessions),
                folder => Assert.Equal(Path.GetFileName(folder).ToUpperInvariant(), Path.GetFileName(folder)));
        }
        finally
        {
            foreach (var root in new[] { source, copy })
                try { Directory.Delete(root, true); } catch { }
        }
    }

    private static string RepoWindows()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Windows", "tests", "Fixtures", "mac-backup");
            if (Directory.Exists(candidate)) return Path.Combine(directory.FullName, "Windows");
        }
        throw new DirectoryNotFoundException("repository root");
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
