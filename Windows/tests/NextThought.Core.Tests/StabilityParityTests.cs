using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from StabilityTests.swift: companion commands retried across transports are accepted
/// once, the content-addressed backup store is idempotent/verified and a failed snapshot never
/// prunes the last recovery point, and stored answers keep their evidence and still decode without it.
/// </summary>
public sealed class StabilityParityTests
{
    [Fact]
    public void CompanionCommandsRetryExactlyOnceAcrossTransports()
    {
        var cache = new CompanionCommandCache();
        var id = Guid.NewGuid().ToString();
        var date = DateTimeOffset.UtcNow;
        var first = cache.Accept(id, date);
        var duplicate = cache.Accept(id, date.AddSeconds(2));
        var invalid = cache.Accept("bad");
        var expired = cache.Accept(id, date.AddSeconds(301));
        Assert.True(first);
        Assert.False(duplicate);
        Assert.False(invalid);
        Assert.True(expired);
    }

    [Fact]
    public void SnapshotFrameCarriesOrderingAndFreshness()
    {
        var snapshot = new CompanionSnapshot("\u5C31\u7EEA", "\u65B0\u95EE\u9898", "", true, "", "",
            AnswerId: "new", Phase: "generating");
        var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var envelope = CompanionEnvelope.Create("epoch", 9, snapshot, now);
        var text = Encoding.UTF8.GetString(envelope.Sse());
        Assert.Contains("id: epoch:9\n", text);
        Assert.Contains("\"answerID\":\"new\"", text);
        var encoded = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonDefaults.Options);
        var decoded = JsonSerializer.Deserialize<CompanionEnvelope>(encoded, JsonDefaults.Options)!;
        Assert.Equal(envelope, decoded);
    }

    [Fact]
    public void IncrementalBackupDeduplicatesRecoversAndRejectsCorruption()
    {
        var root = Directory.CreateTempSubdirectory("nt-parity-backup-").FullName;
        try
        {
            var store = new IncrementalBackupStore(root);
            var files = new Dictionary<string, byte[]>
            {
                ["Workspaces/a.json"] = Encoding.UTF8.GetBytes("workspace"),
                ["Sessions/a/meta.json"] = Encoding.UTF8.GetBytes("session")
            };
            var first = store.Create(files, reason: "session");
            var duplicate = store.Create(files, reason: "update");
            Assert.Equal(first, duplicate);
            var merged = new Dictionary<string, byte[]>(files)
            {
                ["Sessions/b/meta.json"] = Encoding.UTF8.GetBytes("next")
            };
            var second = store.Create(merged, reason: "session");
            Assert.NotEqual(first, second);
            Assert.Equal(3, Directory.GetFiles(Path.Combine(root, "Objects")).Length);
            var recovered = Path.Combine(root, "recovered");
            store.Restore(first, recovered);
            Assert.Equal(files["Workspaces/a.json"], File.ReadAllBytes(Path.Combine(recovered, "Workspaces", "a.json")));
            var objectPath = Path.Combine(root, "Objects", IncrementalBackupStore.Digest(Encoding.UTF8.GetBytes("workspace")));
            File.WriteAllBytes(objectPath, Encoding.UTF8.GetBytes("broken"));
            Assert.Throws<InvalidDataException>(() =>
            {
                store.Restore(first, Path.Combine(root, "bad"));
            });
            Assert.False(Directory.Exists(Path.Combine(root, "bad")));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void FailedBackupNeverPrunesTheLastRecoveryPoint()
    {
        var root = Directory.CreateTempSubdirectory("nt-parity-backup-failed-").FullName;
        try
        {
            var store = new IncrementalBackupStore(root);
            var first = store.Create(new Dictionary<string, byte[]> { ["a"] = Encoding.UTF8.GetBytes("safe") },
                reason: "manual", budget: 0);
            Assert.Throws<InvalidDataException>(() =>
            {
                store.Create(new Dictionary<string, byte[]> { ["../escape"] = [] }, reason: "import", budget: 0);
            });
            Assert.True(File.Exists(first));
            var second = store.Create(new Dictionary<string, byte[]> { ["a"] = Encoding.UTF8.GetBytes("new") },
                reason: "session", budget: 0);
            Assert.True(File.Exists(second));
            Assert.True(store.StorageBytes() > 0);
            Assert.Single(Directory.GetFiles(Path.Combine(root, "RestorePoints")));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void SessionPersistsEvidenceAndDecodesOlderAnswers()
    {
        var id = Guid.NewGuid();
        var evidence = new EvidenceBundle(id)
        {
            Notes = ["屏幕 OCR 降级为文字"],
            Search = new SearchDecision(true, "当前价格", "price", "rule")
        };
        var record = new SessionAnswerEvent
        {
            Id = id.ToString("D"),
            Date = DateTimeOffset.UtcNow,
            Question = "价格？",
            Answer = "待核实",
            Model = "test",
            Automatic = false,
            Evidence = evidence
        };
        var encoded = JsonSerializer.Serialize(record, JsonDefaults.Options);
        var decoded = JsonSerializer.Deserialize<SessionAnswerEvent>(encoded, JsonDefaults.Options)!;
        Assert.NotNull(decoded.Evidence);
        // Swift compares the whole EvidenceBundle; the C# record holds lists whose equality is
        // reference-based, so the same members are compared one by one.
        Assert.Equal(evidence.RequestId, decoded.Evidence!.RequestId);
        Assert.Equal(evidence.Notes.Count, decoded.Evidence.Notes.Count);
        Assert.Equal(evidence.Notes[0], decoded.Evidence.Notes[0]);
        Assert.Equal(evidence.Search, decoded.Evidence.Search);
        var older = JsonNode.Parse(encoded)!.AsObject();
        Assert.True(older.Remove("evidence"));
        var withoutEvidence = JsonSerializer.Deserialize<SessionAnswerEvent>(older.ToJsonString(), JsonDefaults.Options)!;
        Assert.Null(withoutEvidence.Evidence);
    }

    [Fact]
    public void DirectoryEnumerationErrorCannotPublishOrPruneARecoveryPoint()
    {
        // The Swift test makes a directory unreadable with POSIX mode 0o000. Windows has no POSIX
        // mode bits, so the equivalent snapshot failure is a file inside the source directory that
        // is held open with no sharing: reading it fails while the recovery point is being built.
        var root = Directory.CreateTempSubdirectory("nt-parity-backup-dir-").FullName;
        try
        {
            var source = Path.Combine(root, "source");
            var lockedDirectory = Path.Combine(source, "locked");
            Directory.CreateDirectory(lockedDirectory);
            var original = Path.Combine(lockedDirectory, "original.txt");
            File.WriteAllText(original, "must not disappear");

            var store = new IncrementalBackupStore(Path.Combine(root, "backups"));
            var first = store.Create(new Dictionary<string, byte[]> { ["safe"] = Encoding.UTF8.GetBytes("previous") },
                reason: "manual");
            using (new FileStream(original, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.ThrowsAny<IOException>(() =>
                {
                    store.Create(new Dictionary<string, byte[]> { ["safe"] = Encoding.UTF8.GetBytes("changed") },
                        new Dictionary<string, string> { ["Originals"] = source }, reason: "session", keep: 1);
                });
            }

            var points = Directory.GetFiles(Path.Combine(store.Root, "RestorePoints"));
            Assert.Equal(Path.GetFullPath(first), Assert.Single(points));
            Assert.Equal("must not disappear", File.ReadAllText(original));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }
}
