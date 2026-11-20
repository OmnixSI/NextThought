using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E30: recovery points (DataBackup.automaticBackup / listBackups) and the two-phase import
/// (DataBackup.prepareImport → review plan → applyImport) are exercised against a real temporary
/// data directory, with no UI involved.
/// </summary>
public sealed class BackupRecoveryTests
{
    private static SessionMeta NewSession(AppPaths paths, string title, params string[] lines)
    {
        var store = new SessionStore(paths);
        var meta = new SessionMeta { Title = title };
        store.CreateAsync(meta).GetAwaiter().GetResult();
        foreach (var text in lines)
            store.AppendTranscriptAsync(meta, new TranscriptLine { Date = meta.StartedAt.AddSeconds(1), Text = text }).GetAwaiter().GetResult();
        store.EndAsync(meta).GetAwaiter().GetResult();
        return meta;
    }

    private static string TempRoot(string name) =>
        Path.Combine(Path.GetTempPath(), name + "-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RecoveryPointListsItsDateSizeAndReason()
    {
        var root = TempRoot("nt-recovery-list");
        try
        {
            var paths = new AppPaths(Path.Combine(root, "data"));
            NewSession(paths, "周会", "讨论上线时间");
            var service = new BackupService(paths);
            var point = await service.CreateRecoveryPointAsync("session");
            Assert.True(File.Exists(point.Path));
            Assert.EndsWith(".ntrestore", point.Path);
            Assert.Equal("session", point.Reason);
            Assert.True(point.Size > 0);
            Assert.True(DateTimeOffset.UtcNow - point.Date < TimeSpan.FromMinutes(1));

            var listed = service.ListBackups();
            var entry = Assert.Single(listed);
            Assert.Equal(point.Path, entry.Path);
            Assert.Equal("会议结束", entry.ReasonTitle);
            Assert.True(entry.IsRecoveryPoint);

            // The same content deduplicates to the point that already exists.
            var again = await service.CreateRecoveryPointAsync("manual");
            Assert.Equal(point.Path, again.Path);
            Assert.Single(service.ListBackups());
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public async Task RecoveryPointRestoresThroughTheReviewedImport()
    {
        var root = TempRoot("nt-recovery-restore");
        try
        {
            var paths = new AppPaths(Path.Combine(root, "data"));
            var meta = NewSession(paths, "面试", "先确认范围", "我来跟进");
            var service = new BackupService(paths);
            var point = await service.CreateRecoveryPointAsync("manual");

            // Lose the meeting locally, then restore it from the recovery point.
            await new SessionStore(paths).DeleteAsync(meta.Id);
            Assert.Empty(new SessionStore(paths).List());
            using var prepared = await service.PrepareImportAsync(point.Path);
            var plan = Assert.Single(prepared.Sessions);
            Assert.Equal(meta.Id, plan.Id);
            Assert.Equal(BackupImportStatus.New, plan.Status);
            var result = await service.ApplyPreparedImportAsync(prepared, new HashSet<Guid> { meta.Id }, new HashSet<string>(), applySettings: false);
            Assert.Equal(1, result.SessionsAdded);
            Assert.Equal(2, new SessionStore(paths).Load(meta.Id).Lines.Count);
            Assert.True(File.Exists(result.SafetyBackup));

            // Importing the same point again now sees an identical local copy.
            using var second = await service.PrepareImportAsync(point.Path);
            Assert.Equal(BackupImportStatus.Identical, Assert.Single(second.Sessions).Status);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public async Task ImportPlanReportsNewIdenticalAndConflictAndAppliesTheChoice()
    {
        var root = TempRoot("nt-import-plan");
        try
        {
            var source = new AppPaths(Path.Combine(root, "source"));
            var meta = NewSession(source, "客户会", "旧版本");
            var archive = Path.Combine(root, "backup.ntbackup");
            await new BackupService(source).ExportAsync(archive);

            var target = new AppPaths(Path.Combine(root, "target"));
            var service = new BackupService(target);
            using (var fresh = await service.PrepareImportAsync(archive))
            {
                Assert.Equal(1, fresh.NewSessions);
                Assert.Equal(BackupImportStatus.New, Assert.Single(fresh.Sessions).Status);
                var applied = await service.ApplyPreparedImportAsync(fresh, new HashSet<Guid>(), new HashSet<string>(), applySettings: false);
                Assert.Equal(1, applied.SessionsAdded);
            }
            using (var identical = await service.PrepareImportAsync(archive))
                Assert.Equal(BackupImportStatus.Identical, Assert.Single(identical.Sessions).Status);

            // A local change makes the record a conflict; the review page can choose the backup copy.
            await new SessionStore(target).AppendTranscriptAsync(meta, new TranscriptLine { Date = DateTimeOffset.UtcNow, Text = "本机较新的内容" });
            using (var conflict = await service.PrepareImportAsync(archive))
            {
                Assert.Equal(1, conflict.ConflictSessions);
                var kept = await service.ApplyPreparedImportAsync(conflict, new HashSet<Guid>(), new HashSet<string>(), applySettings: false);
                Assert.Equal(1, kept.SessionsKept);
                Assert.Equal(0, kept.SessionsReplaced);
            }
            Assert.Contains(new SessionStore(target).Load(meta.Id).Lines, line => line.Text == "本机较新的内容");

            using (var conflict = await service.PrepareImportAsync(archive))
            {
                var replaced = await service.ApplyPreparedImportAsync(conflict, new HashSet<Guid> { meta.Id }, new HashSet<string>(), applySettings: false);
                Assert.Equal(1, replaced.SessionsReplaced);
                Assert.Equal(0, replaced.SessionsKept);
            }
            Assert.DoesNotContain(new SessionStore(target).Load(meta.Id).Lines, line => line.Text == "本机较新的内容");
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public async Task DeletingARecoveryPointRemovesItFromTheList()
    {
        var root = TempRoot("nt-recovery-delete");
        try
        {
            var paths = new AppPaths(Path.Combine(root, "data"));
            NewSession(paths, "周会", "一条转写");
            var service = new BackupService(paths);
            var point = await service.CreateRecoveryPointAsync("manual");
            var store = new IncrementalBackupStore(service.BackupsRoot);
            Assert.True(store.StorageBytes() > 0);
            service.DeleteRecoveryPoint(point);
            Assert.Empty(service.ListBackups());
            Assert.False(File.Exists(point.Path));
            // Pruning released the objects only this point referenced.
            Assert.Equal(0, store.StorageBytes());
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public void AutoBackupPreferenceUsesTheMacKeyAndDefault()
    {
        var preference = MacPreferences.Find("autoBackup");
        Assert.NotNull(preference);
        Assert.Equal(MacPreferenceKind.Bool, preference.Kind);
        Assert.Equal(true, preference.Default);
    }
}
