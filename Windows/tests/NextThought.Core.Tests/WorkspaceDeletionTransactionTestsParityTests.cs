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

    [Fact(Skip = "missing API: DocumentJobStore (Save, StageWorkspaceDeletion, RestoreWorkspaceDeletionRows, All, WorkspaceDeletionJournals) and WorkspaceDeletionFileStaging.Stage(moves, move:)")]
    public void InjectedSynchronousMoveFailureRestoresEveryWorkspaceResource()
    {
        // Source: five workspace resources (session/material/index archives, workspace record and a
        // preparation directory) plus a staged job row; the third injected move throws
        // CocoaError(.fileWriteUnknown). The transaction must fail, not commit, move exactly three
        // sources, restore every payload and leave no staging root. The Windows port has no journal
        // store to stage/restore rows and Stage takes no injected mover.
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

    [Fact(Skip = "missing API: DocumentJobStore and DocumentJobStore.RecoverWorkspaceDeletionTransactions")]
    public void RestartRecoveryRestoresQueueRowsAndPartiallyMovedWorkspaceFiles()
    {
        // Source: six workspace resources, a saved job and a staged deletion journal, of which the
        // first three moves happened before the crash. After RecoverWorkspaceDeletionTransactions a
        // new store reports the job, no journals, every payload restored and no staging root.
    }

    [Fact(Skip = "missing API: DocumentJobStore.MarkWorkspaceDeletionCommitted and DocumentJobStore.RecoverWorkspaceDeletionTransactions")]
    public void RestartRecoveryFinishesCommittedDeletionCleanup()
    {
        // Source: the deletion was committed after staging the workspace record. Recovery must drop
        // the job and journal, delete the staged workspace file and remove the staging root.
    }
}
