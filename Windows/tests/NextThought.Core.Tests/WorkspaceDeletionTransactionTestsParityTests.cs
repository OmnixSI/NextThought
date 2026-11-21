using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated one-for-one from Tests/NextThoughtCoreTests/WorkspaceDeletionTransactionTests.swift:
/// a read failure during a suspended preparation cancels the deletion before any artifact is
/// removed, and a shared original is revalidated when a new job or workspace references it while
/// the deletion is in flight. The SQLite journal/store recovery cases are skipped because the
/// Windows port keeps the durable job store app-side (document jobs use a JSON queue instead).
/// </summary>
public sealed class WorkspaceDeletionTransactionTestsParityTests
{
    private static string TemporaryRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "workspace-delete-" + Guid.NewGuid().ToString("D"));
        Directory.CreateDirectory(root);
        return root;
    }

    [Fact]
    public async Task ReadFailureDuringSuspendedArtifactPreparationCancelsBeforeFileDeletion()
    {
        var artifact = Path.Combine(Path.GetTempPath(),
            "workspace-delete-artifact-" + Guid.NewGuid().ToString("D"));
        File.WriteAllText(artifact, "keep me");
        try
        {
            var archiveReadable = true;
            var stagedJobRowsExist = true;
            var committed = false;

            var didDelete = await WorkspaceDeletionTransaction.Execute(
                prepare: async () =>
                {
                    // This models a reversible queue-row staging step. No document artifact is
                    // removed until the post-await authorization check.
                    stagedJobRowsExist = false;
                    await Task.Yield();
                    archiveReadable = false; // read failure arrives during preparation's suspension
                    return artifact;
                },
                isAuthorized: _ => archiveReadable,
                rollback: _ =>
                {
                    stagedJobRowsExist = true;
                    return Task.CompletedTask;
                },
                commit: artifactUrl =>
                {
                    File.Delete(artifactUrl);
                    committed = true;
                    return Task.CompletedTask;
                });

            Assert.False(didDelete);
            Assert.False(committed);
            Assert.True(stagedJobRowsExist);
            Assert.True(File.Exists(artifact));
        }
        finally
        {
            try { File.Delete(artifact); } catch { }
        }
    }

    [Fact]
    public async Task InjectedSynchronousMoveFailureRestoresEveryWorkspaceResource()
    {
        var root = Path.Combine(Path.GetTempPath(), "wsdel-" + Guid.NewGuid().ToString("N"));
        var staging = Path.Combine(root, ".stage");
        Directory.CreateDirectory(root);
        try
        {
            var names = new[] { "Sessions/session", "Materials/workspace", "Index/workspace", "Workspaces/workspace.json", "Preparation/job" };
            var sources = names.Select(name => Path.Combine(root, name.Replace('/', Path.DirectorySeparatorChar))).ToList();
            foreach (var source in sources)
            {
                if (source.EndsWith(".json", StringComparison.Ordinal))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(source)!); File.WriteAllText(source, "payload");
                }
                else
                {
                    Directory.CreateDirectory(source); File.WriteAllText(Path.Combine(source, "payload"), Path.GetFileName(source));
                }
            }
            var moves = WorkspaceDeletionFileStaging.Manifest(sources, staging);
            var job = DocumentJob.Create(Guid.NewGuid().ToString("D"), "source.pdf");
            var journal = new WorkspaceDeletionJournal { WorkspaceId = job.WorkspaceId, StagingRoot = staging, Jobs = [job], Moves = moves };
            DocumentJobStore? store = null;
            var moved = 0; var committed = false;
            await Assert.ThrowsAsync<IOException>(async () =>
                await WorkspaceDeletionTransaction.Execute(
                    prepare: () => { var opened = new DocumentJobStore(Path.Combine(root, "jobs.sqlite")); opened.Save(job); opened.StageWorkspaceDeletion(journal); store = opened; return Task.FromResult(journal); },
                    isAuthorized: _ => true,
                    rollback: _ => { WorkspaceDeletionFileStaging.Rollback(moves, staging); store?.RestoreWorkspaceDeletionRows(journal.TransactionId); return Task.CompletedTask; },
                    stage: _ =>
                    {
                        WorkspaceDeletionFileStaging.Stage(moves, (source, staged) =>
                        {
                            if (++moved == 3) throw new IOException("injected");
                            if (Directory.Exists(source)) Directory.Move(source, staged); else File.Move(source, staged);
                        });
                        return Task.CompletedTask;
                    },
                    commit: _ => { committed = true; return Task.CompletedTask; }));

            Assert.False(committed);
            Assert.Equal(3, moved);
            Assert.Equal([job.Id], store!.All().Select(item => item.Id));
            Assert.Empty(store.WorkspaceDeletionJournals());
            foreach (var source in sources) Assert.True(Directory.Exists(source) || File.Exists(source));
            Assert.False(Directory.Exists(staging));
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public void SharedOriginalIsRevalidatedWhenNewJobOrWorkspaceReferencesArrive()
    {
        var root = TemporaryRoot();
        try
        {
            var originals = Path.Combine(root, "Originals");
            var original = Path.Combine(originals, "shared.pdf");
            var stagingRoot = Path.Combine(root, ".workspace-delete");
            Directory.CreateDirectory(originals);
            File.WriteAllText(original, "shared source");

            var deletingJob = DocumentJob.Create(Guid.NewGuid().ToString("D"), "shared.pdf") with
            {
                OriginalPath = original,
                SourceHash = "shared-hash"
            };
            var moves = WorkspaceDeletionFileStaging.Manifest([original], stagingRoot);
            var initiallyUnshared = WorkspaceDeletionFileStaging.ExcludingSharedOriginals(
                moves, [deletingJob], [deletingJob], new HashSet<string>(), originals);
            Assert.Equal(moves, initiallyUnshared);

            var newJob = DocumentJob.Create(Guid.NewGuid().ToString("D"), "copy.pdf") with
            {
                OriginalPath = Path.Combine(root, "elsewhere", "copy.pdf"),
                SourceHash = "shared-hash"
            };
            var sharedByNewJob = WorkspaceDeletionFileStaging.ExcludingSharedOriginals(
                moves, [deletingJob], [deletingJob, newJob], new HashSet<string>(), originals);
            Assert.Empty(sharedByNewJob);

            var sharedByNewWorkspace = WorkspaceDeletionFileStaging.ExcludingSharedOriginals(
                moves, [deletingJob], [deletingJob], new HashSet<string> { "shared-hash" }, originals);
            Assert.Empty(sharedByNewWorkspace);
            WorkspaceDeletionFileStaging.Stage(sharedByNewWorkspace);
            Assert.True(File.Exists(original));
            Assert.False(Directory.Exists(stagingRoot));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task RestartRecoveryRestoresQueueRowsAndPartiallyMovedWorkspaceFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "wsdel-" + Guid.NewGuid().ToString("N"));
        var database = Path.Combine(root, "jobs.sqlite");
        var staging = Path.Combine(root, ".crash");
        try
        {
            var names = new[] { "Sessions/session", "Materials/workspace", "Index/workspace", "Workspaces/workspace.json", "Preparation/job", "Originals/source.pdf" };
            var sources = names.Select(name => Path.Combine(root, name.Replace('/', Path.DirectorySeparatorChar))).ToList();
            foreach (var source in sources)
            {
                if (source.EndsWith(".json", StringComparison.Ordinal) || source.EndsWith(".pdf", StringComparison.Ordinal))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(source)!); File.WriteAllText(source, "payload");
                }
                else { Directory.CreateDirectory(source); File.WriteAllText(Path.Combine(source, "payload"), "payload"); }
            }
            var job = DocumentJob.Create(Guid.NewGuid().ToString("D"), "source.pdf");
            var journal = new WorkspaceDeletionJournal { WorkspaceId = job.WorkspaceId, StagingRoot = staging, Jobs = [job], Moves = WorkspaceDeletionFileStaging.Manifest(sources, staging) };
            var store = new DocumentJobStore(database); store.Save(job); store.StageWorkspaceDeletion(journal);
            Directory.CreateDirectory(staging);
            foreach (var move in journal.Moves.Take(3))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(move.Staged)!);
                if (Directory.Exists(move.Source)) Directory.Move(move.Source, move.Staged); else File.Move(move.Source, move.Staged);
            }
            DocumentJobStore.RecoverWorkspaceDeletionTransactions(database);
            var restarted = new DocumentJobStore(database);
            Assert.Equal([job.Id], restarted.All().Select(item => item.Id));
            Assert.Empty(restarted.WorkspaceDeletionJournals());
            foreach (var source in sources) Assert.True(Directory.Exists(source) || File.Exists(source));
            Assert.False(Directory.Exists(staging));
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public async Task RestartRecoveryFinishesCommittedDeletionCleanup()
    {
        var root = Path.Combine(Path.GetTempPath(), "wsdel-" + Guid.NewGuid().ToString("N"));
        var database = Path.Combine(root, "jobs.sqlite");
        var staging = Path.Combine(root, ".committed");
        var workspace = Path.Combine(root, "Workspaces", "workspace.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(workspace)!); File.WriteAllText(workspace, "workspace");
            var job = DocumentJob.Create(Guid.NewGuid().ToString("D"), "source.pdf");
            var moves = WorkspaceDeletionFileStaging.Manifest([workspace], staging);
            var journal = new WorkspaceDeletionJournal { WorkspaceId = job.WorkspaceId, StagingRoot = staging, Jobs = [job], Moves = moves };
            var store = new DocumentJobStore(database); store.Save(job); store.StageWorkspaceDeletion(journal);
            WorkspaceDeletionFileStaging.Stage(moves);
            DocumentJobStore.MarkWorkspaceDeletionCommitted(journal.TransactionId, database);
            DocumentJobStore.RecoverWorkspaceDeletionTransactions(database);
            var restarted = new DocumentJobStore(database);
            Assert.Empty(restarted.All());
            Assert.Empty(restarted.WorkspaceDeletionJournals());
            Assert.False(File.Exists(workspace));
            Assert.False(Directory.Exists(staging));
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
