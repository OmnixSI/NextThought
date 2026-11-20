using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated one-for-one from Tests/NextThoughtCoreTests/ProductArchiveUXTests.swift: history
/// search covers more than a thousand sessions and its cache never bounds coverage, user notes and
/// edited summaries are the searchable text, a copied project owns its archives, template merging
/// defaults to local, and restore conflicts compare content rather than file dates.
/// The search tests are skipped because the Windows port keeps the disk snapshot loader, its
/// revision-keyed cache and the snapshot-through-user-content initializer app-side.
/// </summary>
public sealed class ProductArchiveUXTestsParityTests
{
    private static string TemporaryRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "archive-ux-" + Guid.NewGuid().ToString("D"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (var directory in Directory.EnumerateDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }

    /// <summary>A symbolic link when the account may create one, otherwise a directory junction.</summary>
    private static void CreateDirectoryLink(string link, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(link, target);
            return;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            // Windows without Developer Mode forbids symbolic links; a junction reaches the same
            // files through a path of a different length and needs no privilege.
        }
        using var process = System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            });
        process!.WaitForExit();
        if (process.ExitCode != 0 || !Directory.Exists(link))
            throw new InvalidOperationException("无法创建目录链接：" + link);
    }

    private static void DeleteLink(string link)
    {
        if (Directory.Exists(link)) Directory.Delete(link, recursive: false);
    }

    private static void TryDelete(string directory)
    {
        try { Directory.Delete(directory, recursive: true); } catch { }
    }

    [Fact(Skip = "missing API: SessionSearch(root:cacheLimit:) disk snapshot search (search(ids:terms:revision:), cachedByteCount)")]
    public void SearchCoversMoreThanOneThousandAndEvictionDoesNotLimitCoverage()
    {
        // Source: 1005 session folders with transcript.jsonl (one special line at index 1004) and an
        // invalid context.json that search must never decode; SessionSearch(root:cacheLimit: 1024)
        // then expects checked == 1005, unreadable == 0, a hit for the last id, cachedByteCount <= 1024.
    }

    [Fact(Skip = "missing API: SessionSearch(root:cacheLimit:) search(ids:terms:revision:) and SessionSearchSnapshot(meta:lines:questions:answers:notes:summary:user:)")]
    public void SavedUserNotesRefreshAndCarryExactTargets()
    {
        // Source: a transcript hit has target .transcript(lineID); after user-content.json gains
        // personalNotes "下周补做消融实验" (revision 1) the hit is .personalNotes; revision 2 renames the
        // note so the old term finds nothing; a corrupted file makes unreadable == 1.
    }

    [Fact(Skip = "missing API: SessionSearchSnapshot(meta:lines:questions:answers:notes:summary:user:) — the latest event per answer request is collapsed there")]
    public void SearchUsesTheLatestEventForEachAnswerRequest()
    {
        // Source: two SessionAnswerEvent values share one id (interrupted old text, then a complete
        // answer). Old answer/web text must not be found, the final text hits .answer(id), and
        // snapshot.fields has exactly one .answer field. The Windows snapshot carries fields only;
        // no initializer collapses answer events.
    }

    [Fact(Skip = "missing API: SessionSearchSnapshot(meta:lines:questions:answers:notes:summary:user:) with effective summary and edited Markdown")]
    public void SearchUsesEditedSummaryMarkdownAndStructuredPayload()
    {
        // Source: generated summary/structured notes and user.editedMarkdown must not be searchable;
        // user.editedSummary Markdown and its structured outline payload must be (kind .notes).
        // The Windows snapshot carries fields only; SessionUserContent never feeds it.
    }

    [Fact(Skip = "missing API: WorkspaceArchiveOwnership.repair(Workspace, dataRoot:) with inline ContextDocument.pages fallback")]
    public void OldCopiedProjectOwnsArchivesBeforeOriginalIsDeleted()
    {
        // Source: a copied Workspace whose document still points at the original's
        // Materials/<originalID>/paper.md gets its own Materials/<copyID>/<documentID>.md, keeps
        // updatedAt, reuses the extracted text, stays idempotent, and materializes the inline pages
        // when the original archive is missing. The port only repairs a series from MaterialDocument
        // blocks and returns WorkspaceArchivedDocument values.
    }

    [Fact]
    public void TemplateMergeDefaultsToLocalAndPreferencesIncludeNotes()
    {
        var local = new NotesTemplate("custom-test", "本机模板", []);
        var incoming = new NotesTemplate("custom-test", "备份模板", []);
        var kept = BackupMerge.NotesTemplates([local], [incoming]);
        Assert.Equal(new[] { local }, kept.Templates);
        Assert.Equal(1, kept.Kept);

        var replaced = BackupMerge.NotesTemplates([local], [incoming], new HashSet<string> { incoming.Id });
        Assert.Equal(new[] { incoming }, replaced.Templates);
        Assert.Equal(1, replaced.Replaced);

        var keys = SettingsSnapshot.ExportedKeys.ToHashSet();
        Assert.All(
            new[] { "customNotesTemplates", "notesTemplateByType", "notesLanguage", "notesModelID", "notesReasoning" },
            key => Assert.Contains(key, keys));
    }

    [Fact]
    public void RestoreConflictUsesContentRatherThanFileDates()
    {
        var root = TemporaryRoot();
        try
        {
            var file = Path.Combine(root, SessionUserContent.FileName);
            File.WriteAllText(file, "my notes");
            var first = BackupContent.Fingerprint(root);
            File.SetLastWriteTimeUtc(file, DateTimeOffset.FromUnixTimeSeconds(1).UtcDateTime);
            Assert.Equal(first, BackupContent.Fingerprint(root));
            File.WriteAllText(file, "revised notes");
            var second = BackupContent.Fingerprint(root);
            Assert.Equal(BackupImportStatus.Conflict, BackupContent.Status(first, second));
            Assert.Equal(BackupImportStatus.Identical, BackupContent.Status(first, first));
            Assert.Equal(BackupImportStatus.New, BackupContent.Status(null, second));
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public void FingerprintIsTheSameThroughASymlinkedPath()
    {
        var root = TemporaryRoot();
        var link = Path.Combine(root, "l");
        try
        {
            var real = Path.Combine(root, "real", "Sessions", "meeting-with-a-long-name");
            Directory.CreateDirectory(Path.Combine(real, "nested"));
            File.WriteAllText(Path.Combine(real, SessionUserContent.FileName), "notes");
            File.WriteAllText(Path.Combine(real, "nested", "transcript.jsonl"), "line");
            // The same folder reached through a link whose path has a different length.
            CreateDirectoryLink(link, Path.Combine(root, "real"));
            var linked = Path.Combine(link, "Sessions", "meeting-with-a-long-name");
            Assert.Equal(BackupContent.Fingerprint(real), BackupContent.Fingerprint(linked));
            // A copy elsewhere with the same content is identical too.
            var copy = Path.Combine(root, "copy");
            CopyDirectory(real, copy);
            Assert.Equal(BackupContent.Fingerprint(linked), BackupContent.Fingerprint(copy));
        }
        finally
        {
            DeleteLink(link);
            TryDelete(root);
        }
    }
}
