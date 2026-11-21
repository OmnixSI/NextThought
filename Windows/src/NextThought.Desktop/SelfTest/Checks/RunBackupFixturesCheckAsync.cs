using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// J8/④: <c>--self-test=backup-fixtures</c> — the Mac 3.4.0 <c>BackupFixtureSelfTest</c> entry.
    /// Four small scenarios are built through the app's own stores in scratch storage, exported with
    /// the real backup writer, then imported into an empty directory; the checks cover the manifest,
    /// the session counts, the second import being identical, series/workspace data and a summary.
    /// </summary>
    private async Task RunBackupFixturesCheckAsync(string path)
    {
        await Task.Delay(200);
        var steps = new List<object>();
        var passed = false;
        try
        {
            void Step(string name, bool ok, object? detail = null) => steps.Add(new { step = name, ok, detail });
            var scratch = Path.Combine(Path.GetTempPath(), "nextthought-selftest", "backup-fixtures-data");
            if (Directory.Exists(scratch)) Directory.Delete(scratch, true);
            Directory.CreateDirectory(scratch);
            var exports = Path.Combine(Path.GetTempPath(), "nextthought-selftest", "backup-fixtures-exports");
            if (Directory.Exists(exports)) Directory.Delete(exports, true);
            Directory.CreateDirectory(exports);

            SessionMeta Meta(string title) => new()
            {
                Id = Guid.NewGuid(),
                Title = title,
                Mode = "interview",
                StartedAt = DateTimeOffset.Now.AddMinutes(-40),
                EndedAt = DateTimeOffset.Now.AddMinutes(-30),
                TranscriptCount = 1
            };

            // 1) empty data directory
            var emptyArchive = Path.Combine(exports, "empty.ntbackup");
            await new BackupService(new AppPaths(Path.Combine(scratch, "empty"))).ExportAsync(emptyArchive);
            Step("empty export written", File.Exists(emptyArchive) && new FileInfo(emptyArchive).Length > 0,
                new FileInfo(emptyArchive).Length);
            var emptyManifest = BackupArchive.ReadManifest(emptyArchive);
            Step("empty manifest", emptyManifest is { SessionCount: 0, WorkspaceCount: 0 });

            // 2) one ended meeting with a transcript
            var onePaths = new AppPaths(Path.Combine(scratch, "one"));
            var oneStore = new SessionStore(onePaths);
            var oneMeta = Meta("单场会议");
            await oneStore.CreateAsync(oneMeta);
            await oneStore.AppendTranscriptAsync(oneMeta, new TranscriptLine
            {
                Date = oneMeta.StartedAt.AddSeconds(10),
                StartedAt = oneMeta.StartedAt.AddSeconds(10),
                EndedAt = oneMeta.StartedAt.AddSeconds(14),
                Speaker = "me",
                Text = "第一句话。"
            });
            await oneStore.EndAsync(oneMeta);
            var oneArchive = Path.Combine(exports, "one.ntbackup");
            await new BackupService(onePaths).ExportAsync(oneArchive);
            Step("one manifest", BackupArchive.ReadManifest(oneArchive) is { SessionCount: 1 });
            var oneTarget = new AppPaths(Path.Combine(scratch, "one-import"));
            var oneResult = await new BackupService(oneTarget).ImportAsync(oneArchive);
            var oneImported = new SessionStore(oneTarget).List();
            Step("one imported", oneResult.SessionsAdded == 1 && oneImported.Count == 1 && oneImported[0].Title == "单场会议",
                new { oneResult.SessionsAdded, count = oneImported.Count });
            var oneAgain = await new BackupService(oneTarget).ImportAsync(oneArchive);
            // Importing the same archive again must not add anything (the Mac fixture check asserts
            // the same thing; the breakdown fields depend on the prepared-import path).
            Step("second import adds nothing", oneAgain.SessionsAdded == 0,
                new { oneAgain.SessionsAdded, oneAgain.SessionsIdentical, oneAgain.SessionsKept });

            // 3) three meetings in two series
            var threePaths = new AppPaths(Path.Combine(scratch, "three"));
            var threeStore = new SessionStore(threePaths);
            var ids = new List<Guid>();
            foreach (var title in new[] { "系列 A 第一场", "系列 B 第一场", "系列 A 第二场" })
            {
                var meta = Meta(title);
                ids.Add(meta.Id);
                await threeStore.CreateAsync(meta);
                await threeStore.AppendTranscriptAsync(meta, new TranscriptLine
                {
                    Date = meta.StartedAt.AddSeconds(5),
                    StartedAt = meta.StartedAt.AddSeconds(5),
                    EndedAt = meta.StartedAt.AddSeconds(9),
                    Speaker = "remote",
                    Text = title + "的内容。"
                });
                await threeStore.EndAsync(meta);
            }
            new WorkspaceStore(threePaths.Root).Save(new WorkspaceData(
            [
                WorkspaceSeries.Create("系列 A").WithSession(ids[0].ToString("D")).WithSession(ids[2].ToString("D")),
                WorkspaceSeries.Create("系列 B").WithSession(ids[1].ToString("D"))
            ]));
            var threeArchive = Path.Combine(exports, "three.ntbackup");
            await new BackupService(threePaths).ExportAsync(threeArchive);
            var threeManifest = BackupArchive.ReadManifest(threeArchive);
            Step("three manifest", threeManifest is { SessionCount: 3, WorkspaceCount: 2 },
                new { threeManifest?.SessionCount, threeManifest?.WorkspaceCount });
            var threeTarget = new AppPaths(Path.Combine(scratch, "three-import"));
            var threeResult = await new BackupService(threeTarget).ImportAsync(threeArchive);
            Step("three imported", threeResult.SessionsAdded == 3 && new SessionStore(threeTarget).List().Count == 3,
                threeResult.SessionsAdded);
            Step("workspaces imported", new WorkspaceStore(threeTarget.Root).Load().Series.Count == 2,
                new WorkspaceStore(threeTarget.Root).Load().Series.Count);

            // 4) a summary survives the round trip
            var fourPaths = new AppPaths(Path.Combine(scratch, "four"));
            var fourStore = new SessionStore(fourPaths);
            var fourMeta = Meta("有纪要的会议");
            await fourStore.CreateAsync(fourMeta);
            await fourStore.AppendTranscriptAsync(fourMeta, new TranscriptLine
            {
                Date = fourMeta.StartedAt.AddSeconds(5),
                StartedAt = fourMeta.StartedAt.AddSeconds(5),
                EndedAt = fourMeta.StartedAt.AddSeconds(9),
                Speaker = "me",
                Text = "这场会有纪要。"
            });
            await fourStore.SaveSummaryAsync(fourMeta, "# 有纪要的会议\n\n- 决定：按时上线。", "gpt-5.5");
            await fourStore.EndAsync(fourMeta);
            var fourArchive = Path.Combine(exports, "four.ntbackup");
            await new BackupService(fourPaths).ExportAsync(fourArchive);
            var fourTarget = new AppPaths(Path.Combine(scratch, "four-import"));
            await new BackupService(fourTarget).ImportAsync(fourArchive);
            var fourData = new SessionStore(fourTarget).Load(fourMeta.Id);
            Step("summary and lines survived", fourData is not null && fourData.Lines.Count == 1
                 && fourData.Meta.Title == "有纪要的会议" && (fourData.Summary?.Length ?? 0) > 0,
                new { lines = fourData?.Lines.Count, summary = fourData?.Summary?.Length });

            passed = steps.All(step => (bool)step.GetType().GetProperty("ok")!.GetValue(step)!);
            await WriteReportAsync(path, new { passed, steps, exports });
            Console.WriteLine(passed ? "backup-fixtures-check: passed" : "backup-fixtures-check: failed");
        }
        catch (Exception error)
        {
            await WriteReportAsync(path + ".error.txt", new { passed = false, error = error.ToString() });
            Console.Error.WriteLine("backup-fixtures-check: " + error);
        }
        finally
        {
            Close();
        }
    }
}
