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

    [Fact]
    public async Task SearchCoversMoreThanOneThousandAndEvictionDoesNotLimitCoverage()
    {
        var root = TemporaryRoot();
        try
        {
            var ids = new List<Guid>();
            for (var index = 0; index < 1005; index++)
            {
                var id = Guid.NewGuid();
                ids.Add(id);
                var folder = Path.Combine(root, id.ToString("D"));
                Directory.CreateDirectory(folder);
                var line = new TranscriptLine
                {
                    Date = DateTimeOffset.UtcNow, Speaker = "remote",
                    Text = index == 1004 ? "\u7EC4\u4F1A\u8BA8\u8BBA\u7A00\u758F\u6CE8\u610F\u529B" : "\u7B2C " + index + " \u573A\u666E\u901A\u8BB0\u5F55"
                };
                File.WriteAllBytes(Path.Combine(folder, "transcript.jsonl"), JSONLines.Line(line));
                File.WriteAllText(Path.Combine(folder, "context.json"), "not a valid context");
            }
            var search = new DiskSessionSearch(root, 1024);
            var result = await search.SearchAsync(ids, new Glossary().Search("\u7A00\u758F\u6CE8\u610F\u529B"), 0);
            Assert.Equal(1005, result.Checked);
            Assert.Equal(0, result.Unreadable);
            Assert.NotNull(result.Hits[ids[^1]]);
            Assert.True(search.CachedByteCount <= 1024);
        }
        finally { TryDelete(root); }
    }

    [Fact]
    public async Task SavedUserNotesRefreshAndCarryExactTargets()
    {
        var root = TemporaryRoot();
        try
        {
            var id = Guid.NewGuid();
            var lineId = Guid.NewGuid();
            var folder = Path.Combine(root, id.ToString("D"));
            Directory.CreateDirectory(folder);
            var line = new TranscriptLine { Id = lineId, Date = DateTimeOffset.UtcNow, Speaker = "remote", Text = "\u6A21\u578B\u5BF9\u6BD4\u5B9E\u9A8C" };
            File.WriteAllBytes(Path.Combine(folder, "transcript.jsonl"), JSONLines.Line(line));
            var search = new DiskSessionSearch(root);
            var first = await search.SearchAsync([id], new Glossary().Search("\u6A21\u578B\u5BF9\u6BD4"), 0);
            Assert.Equal(new SessionContentTarget.Transcript(lineId), first.Hits[id].Target());

            var user = new SessionUserContent { PersonalNotes = "\u4E0B\u5468\u8865\u505A\u6D88\u878D\u5B9E\u9A8C", Revision = 1 };
            File.WriteAllBytes(Path.Combine(folder, SessionUserContent.FileName), JSONLines.Line(user));
            var next = await search.SearchAsync([id], new Glossary().Search("\u6D88\u878D\u5B9E\u9A8C"), 1);
            Assert.Equal(new SessionContentTarget.PersonalNotes(), next.Hits[id].Target());

            user = user with { PersonalNotes = "\u4E0B\u5468\u8865\u505A\u7A33\u5065\u6027\u5B9E\u9A8C", Revision = 2 };
            File.WriteAllBytes(Path.Combine(folder, SessionUserContent.FileName), JSONLines.Line(user));
            var removed = await search.SearchAsync([id], new Glossary().Search("\u6D88\u878D\u5B9E\u9A8C"), 2);
            Assert.Empty(removed.Hits);

            File.WriteAllText(Path.Combine(folder, SessionUserContent.FileName), "broken");
            var corrupted = await search.SearchAsync([id], new Glossary().Search("\u6A21\u578B"), 3);
            Assert.Equal(1, corrupted.Unreadable);
        }
        finally { TryDelete(root); }
    }

    [Fact]
    public void SearchUsesTheLatestEventForEachAnswerRequest()
    {
        var id = Guid.NewGuid();
        var partial = new SessionAnswerEvent
        {
            Id = id.ToString("D"), Date = DateTimeOffset.UtcNow, Question = "\u5B9E\u9A8C\u7ED3\u8BBA\u662F\u4EC0\u4E48",
            Answer = "\u65E7\u7247\u6BB5\u72EC\u6709\u63AA\u8F9E", Model = "test",
            WebAnswer = "\u65E7\u8054\u7F51\u7247\u6BB5\u72EC\u6709\u63AA\u8F9E", Completion = AnswerCompletion.Interrupted
        };
        var complete = new SessionAnswerEvent
        {
            Id = id.ToString("D"), Date = DateTimeOffset.UtcNow, Question = "\u5B9E\u9A8C\u7ED3\u8BBA\u662F\u4EC0\u4E48",
            Answer = "\u6700\u7EC8\u5B8C\u6574\u5B9E\u9A8C\u7ED3\u8BBA", Model = "test", Completion = AnswerCompletion.Complete
        };
        var snapshot = SessionSearchSnapshotBuilder.Create([], [], [partial, complete], null, null);
        Assert.Null(SessionContentSearch.FirstHit(snapshot.Fields, new Glossary().Search("\u65E7\u7247\u6BB5\u72EC\u6709\u63AA\u8F9E")));
        Assert.Null(SessionContentSearch.FirstHit(snapshot.Fields, new Glossary().Search("\u65E7\u8054\u7F51\u7247\u6BB5\u72EC\u6709\u63AA\u8F9E")));
        var hit = SessionContentSearch.FirstHit(snapshot.Fields, new Glossary().Search("\u6700\u7EC8\u5B8C\u6574\u5B9E\u9A8C\u7ED3\u8BBA"));
        Assert.Equal(new SessionContentTarget.Answer(id), hit?.Target());
        Assert.Single(snapshot.Fields.Where(field => field.Kind == SessionSearchFieldKind.Answer));
    }

    [Fact]
    public void SearchUsesEditedSummaryMarkdownAndStructuredPayload()
    {
        var date = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var generatedPayload = new StructuredNotesPayload(NotesFormat.Outline,
            outline: new OutlineNotes([new OutlineSection("AI\u539F\u7ED3\u6784\u56FE", null,
                [new OutlinePoint("AI\u65E7\u7ED3\u6784\u56FE\u72EC\u6709\u5185\u5BB9")])]));
        var editedPayload = new StructuredNotesPayload(NotesFormat.Outline,
            outline: new OutlineNotes([new OutlineSection("\u4EBA\u5DE5\u4FEE\u8BA2\u7ED3\u6784\u56FE", null,
                [new OutlinePoint("\u4EBA\u5DE5\u7ED3\u6784\u56FE\u72EC\u6709\u5185\u5BB9")])]));
        var generated = new SessionSummary
        {
            Title = "AI\u539F\u6458\u8981", Markdown = "AI\u65E7\u7EAA\u8981\u72EC\u6709\u5185\u5BB9", Questions = [],
            GeneratedAt = date, Model = "test", Structured = generatedPayload, StructuredAll = [generatedPayload]
        };
        var user = new SessionUserContent
        {
            EditedMarkdown = "\u65E7\u7248\u4EBA\u5DE5\u6587\u672C\u72EC\u6709\u5185\u5BB9",
            EditedSummary = new SessionSummary
            {
                Title = "\u4EBA\u5DE5\u4FEE\u8BA2\u6458\u8981", Markdown = "\u4EBA\u5DE5\u7EAA\u8981\u72EC\u6709\u5185\u5BB9",
                Questions = [], GeneratedAt = date, Model = "test",
                Structured = editedPayload, StructuredAll = [editedPayload]
            }
        };
        var snapshot = SessionSearchSnapshotBuilder.Create([], [], [], null, generated, user);
        foreach (var text in new[] { "\u4EBA\u5DE5\u7EAA\u8981\u72EC\u6709\u5185\u5BB9", "\u4EBA\u5DE5\u7ED3\u6784\u56FE\u72EC\u6709\u5185\u5BB9" })
            Assert.Equal(SessionSearchFieldKind.Notes,
                SessionContentSearch.FirstHit(snapshot.Fields, new Glossary().Search(text))?.Kind);
        foreach (var text in new[] { "AI\u65E7\u7EAA\u8981\u72EC\u6709\u5185\u5BB9", "AI\u65E7\u7ED3\u6784\u56FE\u72EC\u6709\u5185\u5BB9", "\u65E7\u7248\u4EBA\u5DE5\u6587\u672C\u72EC\u6709\u5185\u5BB9" })
            Assert.Null(SessionContentSearch.FirstHit(snapshot.Fields, new Glossary().Search(text)));
    }

    [Fact]
    public void OldCopiedProjectOwnsArchivesBeforeOriginalIsDeleted()
    {
        var root = TemporaryRoot();
        try
        {
            var originalId = Guid.NewGuid();
            var folder = Path.Combine(root, "Materials", originalId.ToString("D"));
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "paper.md"), "\u624B\u5DE5\u6838\u5BF9\u540E\u7684\u63D0\u53D6\u5185\u5BB9");
            var document = new ContextDocument
            {
                Name = "paper.pdf", Pages = ["\u65E7\u7684\u5185\u8054\u5185\u5BB9"],
                MarkdownPath = Path.Combine("Materials", originalId.ToString("D"), "paper.md")
            };
            var copy = new WorkspaceSeries(Guid.NewGuid().ToString("D"), "\u7814\u7A76\u526F\u672C", DateTimeOffset.UtcNow, [], [])
            {
                Documents = [document]
            };
            var repaired = WorkspaceArchiveOwnership.Repair(copy, root);
            Assert.Contains(copy.Id, repaired.Documents[0].MarkdownPath);
            Directory.Delete(folder, true);
            Assert.Equal("\u624B\u5DE5\u6838\u5BF9\u540E\u7684\u63D0\u53D6\u5185\u5BB9",
                File.ReadAllText(Path.Combine(root, repaired.Documents[0].MarkdownPath!)));
            var again = WorkspaceArchiveOwnership.Repair(repaired, root);
            Assert.Equal(repaired.Documents[0].MarkdownPath, again.Documents[0].MarkdownPath);

            var missing = new WorkspaceSeries(Guid.NewGuid().ToString("D"), "\u65E7\u7248\u8D44\u6599", DateTimeOffset.UtcNow, [], [])
            {
                Documents = [document]
            };
            var rebuilt = WorkspaceArchiveOwnership.Repair(missing, root);
            Assert.Equal("\u65E7\u7684\u5185\u8054\u5185\u5BB9",
                File.ReadAllText(Path.Combine(root, rebuilt.Documents[0].MarkdownPath!)));
        }
        finally { TryDelete(root); }
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
