namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunAcceptanceCheckAsync(string path)
    {
        await Task.Delay(100);
        var checks = new List<(string Name, bool Passed)>();
        void Check(string name, bool passed) => checks.Add((name, passed));

        var glossary = new NextThought.Core.Glossary([
            new NextThought.Core.GlossaryEntry("Kubernetes", variants: ["K8s"])
        ]).Normalized();
        var search = glossary.Search("k8s 部署");
        Check("glossary-search", search.Forms.Contains("Kubernetes 部署") && search.Matches("Kubernetes 部署"));

        var fields = new[] { new NextThought.Core.SessionSearchField(NextThought.Core.SessionSearchFieldKind.Transcript, "Kubernetes 部署", Id: "l1") };
        Check("content-search", NextThought.Core.SessionContentSearch.FirstHit(fields, glossary.Search("Kubernetes")) is { Kind: NextThought.Core.SessionSearchFieldKind.Transcript });

        var sessionId = Guid.NewGuid();
        var progress = NextThought.Core.SessionSearch.Search([sessionId],
            glossary.Search("Kubernetes"), new Dictionary<Guid, NextThought.Core.SessionSearchSnapshot>
            {
                [sessionId] = new([new NextThought.Core.SessionSearchField(NextThought.Core.SessionSearchFieldKind.Transcript, "Kubernetes", Id: "l")])
            });
        Check("history-search", progress.Hits.Count == 1 && progress.Hits[sessionId].Label() == "转写");

        var series = new NextThought.Core.WorkspaceSeries("s", "系列", DateTimeOffset.UtcNow, [sessionId.ToString("D")],
            [new NextThought.Core.WorkspaceLesson { Text = "经验", Pinned = true }]);
        var briefing = NextThought.Core.SeriesBriefingBuilder.Build(series,
            [new NextThought.Core.SessionMeta { Id = sessionId, Title = "会议", StartedAt = DateTimeOffset.UtcNow }]);
        Check("series-briefing", briefing.SessionCount == 1 && briefing.Lessons.Count == 1);

        var itemSeries = series with
        {
            SeriesItems = [new NextThought.Core.WorkspaceSeriesItem
            {
                Kind = NextThought.Core.WorkspaceSeriesItem.KindValue.Decision,
                Text = "决定",
                SourceSessionId = sessionId.ToString("D"),
                EvidenceId = "e",
                ClaimRevision = "c",
                SourceRevision = "s"
            }]
        };
        var itemBriefing = NextThought.Core.SeriesBriefingBuilder.Build(itemSeries,
            [new NextThought.Core.SessionMeta { Id = sessionId, Title = "会议", StartedAt = DateTimeOffset.UtcNow }]);
        Check("series-items", itemBriefing.Decisions.Count == 1 && itemBriefing.OpenActions.Count == 0);

        var deletion = NextThought.Core.WorkspaceSessionDeletion.Apply(itemSeries, sessionId.ToString("D"));
        Check("session-deletion", deletion.RemovedSessions == 1
                                  && deletion.Series.SessionIds.Count == 0
                                  && deletion.Series.SourceDeletionTombstones!.Contains(sessionId.ToString("D"))
                                  && deletion.MarkedItems == 1
                                  && deletion.Series.SeriesItems[0].SourceMissing == true);

        var parsedSummary = NextThought.Core.SessionSummaryParser.ParseAll(
            "# 标题\n*复盘*\n# 问题\n1. 为什么？我的回答要点：因为\n# 经验\n- 先演练\n# 题库\n问：q\n答：a");
        Check("summary-parser", parsedSummary.Title == "复盘" && parsedSummary.Questions.Count == 1
                                && parsedSummary.Questions[0].AnswerGist == "因为"
                                && parsedSummary.Lessons.Count == 1 && parsedSummary.Bank.Count == 1);

        var exportProjection = new NextThought.Core.SessionTextProjection(Guid.NewGuid(), "rev", "t1",
            new NextThought.Core.SessionProjectionOptions(), [], []);
        var exported = NextThought.Core.SessionMarkdownExporter.Export("导出", "会议", DateTimeOffset.UtcNow,
            60, "系统音频", exportProjection, [new NextThought.Core.TranscriptLine { Text = "你好", Date = DateTimeOffset.UtcNow }]);
        Check("markdown-export", exported.StartsWith("# 导出", StringComparison.Ordinal)
                                  && exported.Contains("## 完整转录", StringComparison.Ordinal)
                                  && exported.Contains("你好", StringComparison.Ordinal));

        var stagePrompt = NextThought.Core.StageReviewPrompt.Make("系列", "会议", "", "",
            [new NextThought.Core.StageReviewPrompt.SessionDigest("场次", DateTimeOffset.UtcNow, "概要")]);
        Check("stage-review", stagePrompt.StartsWith("场景分区：系列（会议）", StringComparison.Ordinal)
                              && stagePrompt.Contains("## 各次会话记录", StringComparison.Ordinal)
                              && stagePrompt.Contains("### 场次", StringComparison.Ordinal)
                              && NextThought.Core.StageReviewPrompt.System.Contains("## 经验沉淀", StringComparison.Ordinal));

        var summaryPrompt = NextThought.Core.SessionSummaryPrompt.System("面试", "候选人", "回答具体");
        var summaryHeader = NextThought.Core.SessionSummaryPrompt.Header("会议", DateTimeOffset.UtcNow, 30, null, "", []);
        Check("summary-prompt", summaryPrompt.Contains("本次场景：面试", StringComparison.Ordinal)
                                 && summaryPrompt.Contains("## 题库更新", StringComparison.Ordinal)
                                 && summaryHeader.Contains("时长：30 分钟", StringComparison.Ordinal)
                                 && NextThought.Core.SessionSummaryPrompt.Chunks("a\nb", 100).Count == 1);

        var builtProjection = NextThought.Core.SessionProjectionBuilder.Build(Guid.NewGuid(), DateTimeOffset.UtcNow,
            [new NextThought.Core.TranscriptLine { Text = "Kubernetes", Date = DateTimeOffset.UtcNow }], []);
        Check("projection-builder", builtProjection.SearchableText.Any(text => text.Kind == NextThought.Core.SessionProjectionTextKind.Transcript)
                                   && builtProjection.Revision.Length == 64);

        var evidenceSupport = NextThought.Core.SessionEvidenceBuilder.Support("Kubernetes", null,
            [new NextThought.Core.TranscriptLine { Text = "Kubernetes 部署", Date = DateTimeOffset.UtcNow }]);
        var evidenceCurrent = NextThought.Core.SessionEvidenceBuilder.IsReviewCurrent(
            new NextThought.Core.SessionEvidenceReview(NextThought.Core.SessionEvidenceDecision.Supported, "c", "t", "l", DateTimeOffset.UtcNow),
            "c", "t", "l");
        Check("evidence-builder", evidenceSupport == NextThought.Core.SessionEvidenceSupport.Quoted && evidenceCurrent);

        var writeQueue = new NextThought.Core.SessionArchiveWriteQueue(maximumPendingEvents: 2);
        writeQueue.Enqueue("a", [1]);
        writeQueue.Enqueue("b", [2]);
        var overflowedWrite = writeQueue.Enqueue("c", [3]);
        writeQueue.Acknowledge(2);
        var acknowledgedLoss = writeQueue.AcknowledgeDataLossAndResume();
        Check("write-queue", !overflowedWrite.Queued && acknowledgedLoss
                             && writeQueue.Snapshot.AcknowledgedUnqueuedWriteCount == 1
                             && writeQueue.Enqueue("d", [4]).Queued);

        var projectedLines = NextThought.Core.SessionProjectionBuilder.Build(Guid.NewGuid(), DateTimeOffset.UtcNow,
            [new NextThought.Core.TranscriptLine { Text = "库伯奈提斯", Date = DateTimeOffset.UtcNow }], [],
            corrections: [new NextThought.Core.TranscriptCorrection { Line = 0, Original = "库伯奈提斯", Corrected = "Kubernetes" }]);
        var projectedExport = NextThought.Core.SessionMarkdownExporter.Export("导出", "会议", DateTimeOffset.UtcNow,
            0, "", projectedLines, []);
        Check("projected-transcript", projectedLines.Transcript.Count == 1
                                     && projectedExport.Contains("Kubernetes", StringComparison.Ordinal)
                                     && !projectedExport.Contains("库伯奈提斯", StringComparison.Ordinal));

        var notesPayload = new NextThought.Core.StructuredNotesPayload(NextThought.Core.NotesFormat.Outline,
            outline: new NextThought.Core.OutlineNotes(
                [new NextThought.Core.OutlineSection("结论", Points: [new NextThought.Core.OutlinePoint("先演练再上线")])]));
        var notesExport = NextThought.Core.SessionMarkdownExporter.Export("导出", "会议", DateTimeOffset.UtcNow, 0, "",
            new NextThought.Core.SessionTextProjection(Guid.NewGuid(), "r", "t",
                new NextThought.Core.SessionProjectionOptions(), [], []), [], notes: notesPayload);
        Check("notes-export", notesExport.Contains("## 纪要", StringComparison.Ordinal)
                              && notesExport.Contains("先演练再上线", StringComparison.Ordinal));

        var editorPayload = new NextThought.Core.StructuredNotesPayload(NextThought.Core.NotesFormat.Outline,
            outline: new NextThought.Core.OutlineNotes([new NextThought.Core.OutlineSection("旧标题")]));
        var editorTarget = new NextThought.Core.SessionStructuredNotesTarget(NextThought.Core.NotesFormat.Outline,
            "section:0", NextThought.Core.SessionNotesEditor.PayloadRevision(editorPayload));
        var editedPayload = NextThought.Core.SessionNotesEditor.Apply(editorPayload, editorTarget, "新标题");
        Check("notes-editor", editedPayload?.Outline?.Sections[0].Title == "新标题"
                               && NextThought.Core.SessionNotesEditor.Apply(editorPayload,
                                   editorTarget with { PayloadRevision = "stale" }, "x") is null);

        var evidenceNotes = new NextThought.Core.StructuredNotesPayload(NextThought.Core.NotesFormat.Outline,
            outline: new NextThought.Core.OutlineNotes(
                [new NextThought.Core.OutlineSection("结论", Points: [new NextThought.Core.OutlinePoint("先演练再上线")])]));
        var evidenceRefs = NextThought.Core.SessionNotesEvidence.References(evidenceNotes,
            [new NextThought.Core.TranscriptLine { Text = "先演练再上线", Date = DateTimeOffset.UtcNow }], "t1");
        Check("notes-evidence", evidenceRefs.Count == 1
                                && evidenceRefs[0].Support == NextThought.Core.SessionEvidenceSupport.Quoted
                                && !evidenceRefs[0].NeedsConfirmation
                                && evidenceRefs[0].StructuredTarget?.PayloadRevision == NextThought.Core.SessionNotesEditor.PayloadRevision(evidenceNotes));

        var experienceSeries = new NextThought.Core.WorkspaceSeries("s", "组会", DateTimeOffset.UtcNow, [],
            [new NextThought.Core.WorkspaceLesson { Text = "经验", Pinned = true }]);
        var summaryForWrite = new NextThought.Core.SessionSummary { Structured = editorPayload };
        var writtenSummary = NextThought.Core.SessionNotesEditor.UpdateSummary(summaryForWrite, editorTarget, "写回标题");
        Check("summary-writeback", experienceSeries.ExperienceNotes().Contains("经验", StringComparison.Ordinal)
                                   && writtenSummary?.Structured?.Outline?.Sections[0].Title == "写回标题");

        var materialSeries = new NextThought.Core.WorkspaceSeries("s", "组会", DateTimeOffset.UtcNow, [], []);
        var materialText = materialSeries.MaterialText([("role", "岗位")],
            new Dictionary<string, string> { ["role"] = "后端", ["other"] = "备注" });
        Check("workspace-material", materialText == "岗位：后端\nother：备注");

        var prepSystem = NextThought.Core.PreparationPrompt.System(NextThought.Core.MeetingMode.Interview);
        var prepOutput = NextThought.Core.PreparationPrompt.Output(NextThought.Core.MeetingMode.Academic);
        Check("preparation-prompt", prepSystem.Contains("面试", StringComparison.Ordinal)
                                    && prepSystem.Contains("候选人", StringComparison.Ordinal)
                                    && prepOutput.Contains("## 要讲清楚的重点", StringComparison.Ordinal)
                                    && prepOutput.Contains("## 可能被问到的问题", StringComparison.Ordinal));

        var interviewProfile = NextThought.Core.ModeProfile.For(NextThought.Core.MeetingMode.Interview);
        Check("mode-profiles", interviewProfile.Fields.Count == 6
                              && interviewProfile.Fields[0].Id == "domain"
                              && interviewProfile.DocumentCategories.Contains("简历")
                              && interviewProfile.TemplatePreferences.StartsWith("我是候选人", StringComparison.Ordinal));

        var slidePages = new[]
        {
            new NextThought.Core.SlidePage { Page = 1, PageCount = 2, DocumentName = "talk.pptx", Text = "缓存一致性 读写时序" }
        };
        var rankedSlides = NextThought.Core.SlideMatcher.Rank("缓存一致性 读写时序", slidePages);
        var slideDecision = NextThought.Core.SlideMatcher.Decide(rankedSlides);
        Check("slide-matching", rankedSlides.Count == 1
                                 && slideDecision.Kind == NextThought.Core.SlideDecisionKind.Page
                                 && slideDecision.Page?.Page == 1
                                 && NextThought.Core.SlideMatcher.IsDeck("talk.pptx", 2));

        var searchFieldsProjection = NextThought.Core.SessionProjectionBuilder.Build(Guid.NewGuid(), DateTimeOffset.UtcNow,
            [new NextThought.Core.TranscriptLine { Text = "Kubernetes", Date = DateTimeOffset.UtcNow }], []);
        var searchFields = NextThought.Core.SessionContentSearch.Fields(searchFieldsProjection);
        Check("content-fields", searchFields.Count == 1
                                && NextThought.Core.SessionContentSearch.FirstHit(searchFields, glossary.Search("Kubernetes")) is not null);

        var answerState = new NextThought.Core.AnswerState(Guid.NewGuid(), "问题");
        var searchDecision = NextThought.Core.SearchDecision.Parse("{\"needsSearch\":true,\"reason\":\"需要最新\"}", "问题");
        Check("answer-evidence", answerState.Phase == NextThought.Core.AnswerState.PhaseValue.Waiting
                                  && answerState.Evidence.RequestId == answerState.Id
                                  && searchDecision is { Search: true, Query: "问题" });

        var loadedMeta = new NextThought.Core.SessionMeta { Id = Guid.NewGuid(), Title = "会话", StartedAt = DateTimeOffset.UtcNow };
        var loadedSnapshot = NextThought.Core.SessionSearch.FromSession(new NextThought.Core.SessionData(loadedMeta,
            [new NextThought.Core.TranscriptLine { Text = "Kubernetes", Date = DateTimeOffset.UtcNow }], [], ""));
        var loadedHits = NextThought.Core.SessionSearch.Search([loadedMeta.Id], glossary.Search("Kubernetes"),
            new Dictionary<Guid, NextThought.Core.SessionSearchSnapshot> { [loadedMeta.Id] = loadedSnapshot });
        Check("session-snapshot", loadedSnapshot.Fields.Count > 0 && loadedHits.Hits.Count == 1);

        var snapshotCache = new NextThought.Core.SessionSearchCache(4_000);
        var cacheId = Guid.NewGuid();
        var cached = snapshotCache.Add(cacheId, "v1", new NextThought.Core.SessionSearchSnapshot(
            [new NextThought.Core.SessionSearchField(NextThought.Core.SessionSearchFieldKind.Transcript, "缓存", Id: "line:1")]));
        Check("session-cache", cached && snapshotCache.Get(cacheId) is not null
                              && snapshotCache.Bytes > 0 && snapshotCache.Count == 1);

        var metadataPath = Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), "meta.json");
        var metadataRead = NextThought.Core.SessionArchiveMetadataReader.Read(metadataPath,
            _ => "{}"u8.ToArray(), bytes => System.Text.Json.JsonDocument.Parse(bytes), _ => false);
        var legacyAnswers = NextThought.Core.LegacySessionAnswerMigration.Events(["答案"], DateTimeOffset.UtcNow);
        Check("archive-metadata", metadataRead is NextThought.Core.SessionArchiveMetadataReadResult<System.Text.Json.JsonDocument>.Success
                                  && legacyAnswers.Count == 1
                                  && legacyAnswers[0].Question.Contains("旧版回答", StringComparison.Ordinal));

        var preset = new NextThought.Core.SessionPreset
        {
            Name = "技术面",
            Mode = NextThought.Core.SessionMode.Interview,
            Documents = [new NextThought.Core.ContextDocument { Name = "简历", Pages = ["内容"] }]
        };
        Check("session-preset", preset.Mode.ToMeetingMode() == NextThought.Core.MeetingMode.Interview
                                && preset.EnabledDocumentCount == 1
                                && NextThought.Core.SessionPreset.TemplatePreferences(NextThought.Core.SessionMode.Interview)
                                    .StartsWith("我是候选人", StringComparison.Ordinal));

        var originalRoot = Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), "Originals");
        Directory.CreateDirectory(originalRoot);
        var originalFile = Path.Combine(originalRoot, "shared.pdf");
        await File.WriteAllTextAsync(originalFile, "x");
        var originalMoves = NextThought.Core.WorkspaceDeletionFileStaging.Manifest([originalFile],
            Path.Combine(originalRoot, "..", "staging-shared"));
        var deletingJob = NextThought.Core.DocumentJob.Create("w", "shared.pdf") with
        {
            OriginalPath = originalFile,
            SourceHash = "hash"
        };
        var keptMove = NextThought.Core.WorkspaceDeletionFileStaging.ExcludingSharedOriginals(originalMoves,
            [deletingJob], [deletingJob], new HashSet<string>(), originalRoot);
        var retainedMove = NextThought.Core.WorkspaceDeletionFileStaging.ExcludingSharedOriginals(originalMoves,
            [deletingJob], [NextThought.Core.DocumentJob.Create("w", "other.pdf") with { SourceHash = "hash" }],
            new HashSet<string>(), originalRoot);
        Check("shared-originals", keptMove.Count == 1 && retainedMove.Count == 0);

        var guessedMode = NextThought.Core.MeetingTypeGuesser.Guess("字节跳动 后端二面");
        var seriesCandidate = new NextThought.Core.SeriesMatcher.Candidate(Guid.NewGuid(), "字节跳动 · 后端",
            NextThought.Core.SessionMode.Interview, DateTimeOffset.UtcNow);
        var matchedSeries = NextThought.Core.SeriesMatcher.BestMatch("字节跳动 · 后端三面",
            NextThought.Core.SessionMode.Interview, [seriesCandidate]);
        Check("meeting-type", guessedMode == NextThought.Core.SessionMode.Interview
                               && matchedSeries == seriesCandidate.Id
                               && NextThought.Core.SeriesMatcher.BaseTitle("第 3 讲 线性代数") == "线性代数");

        var localSeries = new NextThought.Core.WorkspaceSeries("a", "本地系列", DateTimeOffset.UtcNow, ["s1"], []);
        var backupSeries = new NextThought.Core.WorkspaceSeries("a", "备份系列", DateTimeOffset.UtcNow.AddDays(-1), ["s2"], [])
        {
            SourceDeletionTombstones = ["s3"]
        };
        var mergeResult = NextThought.Core.BackupMerge.Workspaces([localSeries], [backupSeries]);
        Check("backup-merge", mergeResult.Merged.Count == 1
                              && mergeResult.Merged[0].Name == "本地系列"
                              && mergeResult.Merged[0].SessionIds.SequenceEqual(["s1", "s2"])
                              && mergeResult.Merged[0].SourceDeletionTombstones!.Contains("s3")
                              && mergeResult.Updated == 1);

        var notesFieldsProjection = NextThought.Core.SessionProjectionBuilder.Build(Guid.NewGuid(), DateTimeOffset.UtcNow,
            [new NextThought.Core.TranscriptLine { Text = "正文", Date = DateTimeOffset.UtcNow }], []);
        var notesFieldsPayload = new NextThought.Core.StructuredNotesPayload(NextThought.Core.NotesFormat.Outline,
            outline: new NextThought.Core.OutlineNotes(
                [new NextThought.Core.OutlineSection("结论", Points: [new NextThought.Core.OutlinePoint("灰度发布")])]));
        var notesFields = NextThought.Core.SessionContentSearch.Fields(notesFieldsProjection, notesFieldsPayload, [], "t1");
        Check("content-notes-fields", notesFields.Any(field => field.Kind == NextThought.Core.SessionSearchFieldKind.Notes
                                                               && field.Text == "灰度发布")
                                     && NextThought.Core.SessionContentSearch.FirstHit(notesFields, glossary.Search("灰度"))?.Kind
                                         == NextThought.Core.SessionSearchFieldKind.Notes);

        var carrySeries = new NextThought.Core.WorkspaceSeries("c", "系列", DateTimeOffset.UtcNow, [], [])
        {
            DocumentIds = ["doc-1"]
        };
        var carriedDocuments = NextThought.Core.MeetingCarry.Standard.WorkspaceSnapshot(carrySeries, new HashSet<string>());
        var droppedDocuments = (NextThought.Core.MeetingCarry.Standard with { Materials = false })
            .WorkspaceSnapshot(carrySeries, new HashSet<string> { "doc-1" });
        Check("meeting-carry-materials", carriedDocuments.DocumentIds.Count == 1 && droppedDocuments.DocumentIds.Count == 0);

        Check("export-formats", NextThought.Core.SessionExportFormat.Markdown.FileExtension() == "md"
                                && NextThought.Core.SessionExportFormat.Docx.FileExtension() == "docx"
                                && NextThought.Core.SessionExportFormat.Docx.Title() == "Word 文档"
                                && NextThought.Core.SessionExportFormat.Vtt.Title() == "WebVTT 字幕");

        var docxPath = Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), "acceptance.docx");
        NextThought.Core.DocxWriter.Write("# 验收\n\n## 摘要\n- 条目", docxPath);
        var docxOk = new FileInfo(docxPath).Length > 0;
        using (var docx = System.IO.Compression.ZipFile.OpenRead(docxPath))
            docxOk &= docx.GetEntry("word/document.xml") is not null;
        Check("export-docx", docxOk);

        var pdfPath = Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), "acceptance.pdf");
        NextThought.Platform.PdfWriter.Write("# 验收\n\n- 条目", pdfPath);
        var pdfOk = new FileInfo(pdfPath).Length > 0
                    && File.ReadAllBytes(pdfPath).Take(4).SequenceEqual("%PDF"u8.ToArray());
        using (var pdf = UglyToad.PdfPig.PdfDocument.Open(pdfPath))
            pdfOk &= string.Concat(pdf.GetPages().Select(page => page.Text)).Contains("验收");
        Check("export-pdf", pdfOk);

        var evidenceItem = new NextThought.Core.WorkspaceSeriesItem
        {
            Kind = NextThought.Core.WorkspaceSeriesItem.KindValue.Decision,
            Text = "决定",
            SourceSessionId = sessionId.ToString("D"),
            EvidenceId = "e",
            ClaimRevision = "c",
            SourceRevision = "s",
            TranscriptSourceRevision = "t"
        };
        var evidenceSeries = series with { SeriesItems = [evidenceItem] };
        var supportedEvidence = new NextThought.Core.SessionEvidenceReference("e", "标题", "决定",
            NextThought.Core.SessionEvidenceSupport.Quoted, NextThought.Core.SessionEvidenceOrigin.TextNotes,
            ReviewDecision: NextThought.Core.SessionEvidenceDecision.Supported,
            ClaimRevision: "c", SourceRevision: "s", TranscriptSourceRevision: "t");
        var supportedBriefing = NextThought.Core.SeriesBriefingBuilder.Build(evidenceSeries,
            [new NextThought.Core.SessionMeta { Id = sessionId, Title = "会议", StartedAt = DateTimeOffset.UtcNow }],
            evidenceReferences: [supportedEvidence]);
        var staleBriefing = NextThought.Core.SeriesBriefingBuilder.Build(evidenceSeries,
            [new NextThought.Core.SessionMeta { Id = sessionId, Title = "会议", StartedAt = DateTimeOffset.UtcNow }],
            evidenceReferences: []);
        Check("briefing-evidence", supportedBriefing.Decisions.Count == 1
                                   && staleBriefing.Decisions.Count == 0
                                   && staleBriefing.StaleItems.Count == 1);

        var projectionNotesPayload = new NextThought.Core.StructuredNotesPayload(NextThought.Core.NotesFormat.Outline,
            outline: new NextThought.Core.OutlineNotes(
                [new NextThought.Core.OutlineSection("结论", Points: [new NextThought.Core.OutlinePoint("投影笔记")])]));
        var notesProjection = NextThought.Core.SessionProjectionBuilder.Build(Guid.NewGuid(), DateTimeOffset.UtcNow,
            [], [], notes: projectionNotesPayload);
        Check("projection-notes", notesProjection.Notes == projectionNotesPayload
                                  && notesProjection.SearchableText.Any(text => text.Kind == NextThought.Core.SessionProjectionTextKind.Notes
                                                                                 && text.Text.Contains("投影笔记", StringComparison.Ordinal)));

        var archivePath = Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), "acceptance.ntbackup");
        NextThought.Core.BackupArchive.Write(archivePath,
            new NextThought.Core.BackupManifest { WorkspaceCount = 1, SessionCount = 0 },
            new NextThought.Core.WorkspaceData([
                new NextThought.Core.WorkspaceSeries("a", "系列", DateTimeOffset.UtcNow, [], [])
            ]),
            new Dictionary<string, object?> { ["appearance"] = "dark", ["notExported"] = 1 });
        var archiveManifest = NextThought.Core.BackupArchive.ReadManifest(archivePath);
        Check("backup-archive", archiveManifest?.WorkspaceCount == 1 && File.Exists(archivePath));

        var follower = new NextThought.Core.SlideFollower([
            new NextThought.Core.SlidePage { Page = 1, PageCount = 2, Text = "缓存一致性 读写时序" }
        ]);
        var followedPage = follower.Observe("缓存一致性 读写时序");
        Check("slide-follower", followedPage?.Page == 1
                                && follower.LastDecision?.Kind == NextThought.Core.SlideDecisionKind.Page);

        Check("ocr-spacing", NextThought.Core.TextSimilarity.Terms("缓 存 一 致 性").Contains("缓存"));

        var documentsSeries = new NextThought.Core.WorkspaceSeries("d", "系列", DateTimeOffset.UtcNow, [], [])
        {
            Documents =
            [
                new NextThought.Core.ContextDocument { Name = "简历", Enabled = true },
                new NextThought.Core.ContextDocument { Name = "JD", Enabled = false }
            ]
        };
        Check("workspace-documents", documentsSeries.DocumentsOf().Count == 2
                                     && documentsSeries.DocumentsOf(enabledOnly: true).Count == 1);

        var prepSeries = new NextThought.Core.WorkspaceSeries("p", "系列", DateTimeOffset.UtcNow, [], [])
        {
            DocumentIds = ["doc-1", "doc-2"],
            Documents =
            [
                new NextThought.Core.ContextDocument { Id = "doc-1", Name = "简历" },
                new NextThought.Core.ContextDocument { Id = "doc-2", Name = "新资料" }
            ]
        };
        var prepPlan = new NextThought.Core.MeetingPlan
        {
            BaselineDocumentIds = new HashSet<string> { "doc-1" },
            Carry = NextThought.Core.MeetingCarry.Standard with { Materials = false, Transcript = true }
        };
        var prepResult = prepPlan.PreparedWorkspace(prepSeries,
            new NextThought.Core.ContextDocument { Id = "transcript", Name = "上一场转写" });
        Check("meeting-prepare", prepResult.Documents.Count == 2
                                 && prepResult.Documents.Any(document => document.Id == "doc-2")
                                 && prepResult.DocumentIds.Contains("transcript"));

        var queueDirectory = Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), "job-queue");
        var jobQueue = new NextThought.Core.DocumentJobQueue(queueDirectory);
        jobQueue.Clear();
        var queuedJob = NextThought.Core.DocumentJob.Create("ws", "a.pdf") with
        {
            Id = "acceptance-job", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        var queued = jobQueue.Enqueue(queuedJob);
        var reloadedQueue = new NextThought.Core.DocumentJobQueue(queueDirectory);
        Check("document-queue", queued && reloadedQueue.Count == 1 && reloadedQueue.Next()?.Id == "acceptance-job");

        var changeCandidate = new NextThought.Core.WorkspaceSeriesChangeCandidate
        {
            Id = "change-1",
            Kind = NextThought.Core.WorkspaceSeriesItem.KindValue.Action,
            Text = "补充压测",
            SourceSessionId = sessionId.ToString("D"),
            EvidenceId = "e1",
            TranscriptSourceRevision = "t1"
        };
        var (changedSeries, changeReceipt) = NextThought.Core.WorkspaceSeriesChangeMerger.Apply(
            series with { SeriesChangeCandidates = [changeCandidate] }, "change-1",
            NextThought.Core.WorkspaceSeriesChangeDecision.Add);
        Check("series-change", changeReceipt is not null
                               && changedSeries.SeriesItems.Count == 1
                               && changedSeries.SeriesChangeCandidates[0].Status
                                   == NextThought.Core.WorkspaceSeriesChangeCandidate.StatusValue.Accepted);

        var collectReference = new NextThought.Core.SessionEvidenceReference("notes:actions:1", "待办", "发布",
            NextThought.Core.SessionEvidenceSupport.Quoted, NextThought.Core.SessionEvidenceOrigin.TextNotes,
            LineId: Guid.NewGuid(), Kind: NextThought.Core.NotesSectionKind.Actions, Owner: "小王", Due: "周五",
            ClaimRevision: "c1", SourceRevision: "s1", TranscriptSourceRevision: "t1",
            ReviewDecision: NextThought.Core.SessionEvidenceDecision.Supported);
        var (collectedSeries, collectedIds) = NextThought.Core.WorkspaceSeriesChangeMerger.CollectCandidates(
            series, [collectReference], sessionId.ToString("D"));
        Check("series-change-collect", collectedIds.Count == 1
                                        && collectedSeries.SeriesChangeCandidates.Count == 1
                                        && collectedSeries.SeriesChangeCandidates[0].Kind
                                            == NextThought.Core.WorkspaceSeriesItem.KindValue.Action);

        var ownershipSeries = new NextThought.Core.WorkspaceSeries("owner", "系列", DateTimeOffset.UtcNow, [], [])
        {
            Documents = [new NextThought.Core.ContextDocument { Id = "owner-doc", Name = "资料" }]
        };
        var archivedDocuments = NextThought.Core.WorkspaceArchiveOwnership.Repair(ownershipSeries,
            [new NextThought.Core.MaterialDocument { Id = "owner-doc", Name = "资料.pdf" }],
            Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), "materials"));
        var ownershipUpdated = NextThought.Core.WorkspaceArchiveOwnership.ApplyMarkdownPaths(ownershipSeries, archivedDocuments);
        Check("archive-ownership", archivedDocuments.Count == 1
                                   && ownershipUpdated.Documents[0].MarkdownPath == archivedDocuments[0].MarkdownPath
                                   && File.Exists(archivedDocuments[0].MarkdownPath));

        var recordMeta = new NextThought.Core.SessionMeta
        {
            Id = Guid.NewGuid(), Title = "组会", StartedAt = DateTimeOffset.UtcNow.AddHours(-1)
        };
        var recordData = new NextThought.Core.SessionData(recordMeta,
            [new NextThought.Core.TranscriptLine { Text = "先演练", Date = recordMeta.StartedAt }],
            [new NextThought.Core.AnswerRecord
            {
                Id = Guid.NewGuid(), Date = recordMeta.StartedAt, Question = "怎么上线？", Answer = "先灰度"
            }],
            "摘要");
        var sessionRecord = NextThought.Core.SessionRecord.FromSessionData(recordData);
        Check("session-record", sessionRecord.Id == recordMeta.Id
                                && sessionRecord.ToSessionData().Answers.Count == 1
                                && sessionRecord.Summary == "摘要"
                                && sessionRecord.Transcript.Count == 1);

        var (webKey, webEnabled) = NextThought.Platform.WebSearchConfiguration.FromEnvironment(_ => null);
        Check("web-search-config", webKey is null && !webEnabled
                                   && NextThought.Platform.WebSearchConfiguration.CreateProvider(read: _ => null) is null);

        Check("web-search-setting", new NextThought.Core.AppSettings().WebSearch
                                     && !new NextThought.Core.AppSettings { WebSearch = false }.WebSearch);

        var manifest = new NextThought.Core.BackupManifest { WorkspaceCount = 1, SessionCount = 1 };
        var rejected = false;
        try { (manifest with { Version = 2 }).Validate(); } catch (NextThought.Core.BackupManifest.ValidationException) { rejected = true; }
        Check("backup-manifest", rejected && NextThought.Core.SettingsSnapshot.IsSecretAccount("answer.api.x"));

        var plan = new NextThought.Core.MeetingPlan { Mode = NextThought.Core.MeetingMode.Custom, CustomBaseMode = NextThought.Core.MeetingMode.Academic };
        Check("meeting-plan", plan.EffectiveMode == NextThought.Core.MeetingMode.Academic);

        Check("evidence", NextThought.Core.SessionEvidenceSupport.Quoted.Label().Contains("引文"));

        var temporary = Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), "acceptance");
        var saved = NextThought.Core.SessionUserContentStore.Save(new NextThought.Core.SessionUserContent { PersonalNotes = "笔记" }, temporary);
        Check("user-content", saved.Revision == 1 && NextThought.Core.SessionUserContentStore.Load(temporary).PersonalNotes == "笔记");

        var migrationRoot = Path.Combine(temporary, "migration-sessions");
        NextThought.Core.SessionArchiveMigrationPublisher.Publish(["m"], migrationRoot,
            (_, folder) =>
            {
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "meta.json"), "{}");
            },
            (_, _) => { });
        Check("migration-publish", File.Exists(Path.Combine(migrationRoot, "m", "meta.json"))
                                   && NextThought.Core.SessionArchiveMigrationPublisher.PendingRecoveryDirectory(migrationRoot) is null);


        var deleteRoot = Path.Combine(temporary, "delete");
        Directory.CreateDirectory(deleteRoot);
        var file = Path.Combine(deleteRoot, "a.txt");
        await File.WriteAllTextAsync(file, "A");
        var moves = NextThought.Core.WorkspaceDeletionFileStaging.Manifest([file], Path.Combine(deleteRoot, "staging"));
        NextThought.Core.WorkspaceDeletionFileStaging.Stage(moves);
        NextThought.Core.WorkspaceDeletionFileStaging.Rollback(moves);
        Check("workspace-deletion", File.Exists(file) && File.ReadAllText(file) == "A");

        var job = NextThought.Core.DocumentJob.Create("w", "a.pdf") with { Stage = NextThought.Core.DocumentJob.StageValue.Embedding, Completed = 1, Total = 2 };
        Check("document-job", job.ProgressText == "建立语义索引 1/2");

        var archive = Path.Combine(temporary, "archive");
        var transcript = Path.Combine(archive, "transcript.jsonl");
        NextThought.Core.SessionArchiveFileAppender.Live().AppendLine("{}", transcript);
        NextThought.Core.SessionArchiveContentMarker.Record(Path.Combine(archive, ".marker"));
        Check("archive", File.ReadAllLines(transcript).Length == 1 && NextThought.Core.SessionArchiveLeftover.IsLeftover(["notes.json"]));

        var ownedSeries = new NextThought.Core.WorkspaceSeries("own", "归档系列", DateTimeOffset.UtcNow, [], []);
        var owned = NextThought.Core.WorkspaceArchiveOwnership.Repair(ownedSeries,
            [new NextThought.Core.MaterialDocument { Id = "doc", Name = "资料" }], Path.Combine(temporary, "materials"));
        Check("archive-ownership", owned.Count == 1 && File.Exists(owned[0].MarkdownPath) && File.Exists(owned[0].MetadataPath));

        var zipformer = NextThought.Core.OnlineTransducerModel.LegacyBilingual(Path.Combine(temporary, "model"));
        var hotwords = NextThought.Core.ZipformerHotwords.Clean("Kubernetes，开源");
        Check("zipformer", zipformer.RequiredFiles.Count == 4
                           && zipformer.ModelType == "zipformer"
                           && hotwords.SequenceEqual(["Kubernetes", "开源"])
                           && NextThought.Core.ZipformerModelFiles.SampleRate == 16000);

        var sessionPaths = new NextThought.Platform.AppPaths(Path.Combine(temporary, "session-store"));
        var sessionStore = new NextThought.Platform.SessionStore(sessionPaths);
        var storedMeta = new NextThought.Core.SessionMeta
        {
            Id = Guid.NewGuid(),
            Title = "验收会议",
            StartedAt = DateTimeOffset.UtcNow,
            EndedAt = DateTimeOffset.UtcNow
        };
        await sessionStore.CreateAsync(storedMeta);
        await sessionStore.AppendTranscriptAsync(storedMeta, new NextThought.Core.TranscriptLine
        {
            Id = Guid.NewGuid(), Text = "Kubernetes 部署", Date = DateTimeOffset.UtcNow, StartedAt = DateTimeOffset.UtcNow, EndedAt = DateTimeOffset.UtcNow
        });
        await sessionStore.EndAsync(storedMeta);
        var stored = sessionStore.Load(storedMeta.Id);
        Check("session-store", stored.Meta.Title == "验收会议" && stored.Lines.Count == 1
                               && stored.Lines[0].Text == "Kubernetes 部署");

        var updateApp = Path.Combine(temporary, "app");
        Directory.CreateDirectory(updateApp);
        await File.WriteAllTextAsync(Path.Combine(updateApp, "NextThought.exe"), "old");
        var backup = NextThought.Core.UpdateBackup.Create(updateApp, Path.Combine(temporary, "backups"), "1.0.0");
        await File.WriteAllTextAsync(Path.Combine(updateApp, "NextThought.exe"), "new");
        NextThought.Core.UpdateBackup.Restore(backup.Path, updateApp);
        Check("update-backup", File.ReadAllText(Path.Combine(updateApp, "NextThought.exe")) == "old");

        var projection = new NextThought.Core.SessionTextProjection(sessionId, "rev", "t1",
            new NextThought.Core.SessionProjectionOptions(), [],
            [new NextThought.Core.SessionProjectedText("n", NextThought.Core.SessionProjectionTextKind.Notes, "n",
                SourceStatus: NextThought.Core.SessionDerivedContentStatus.Stale)]);
        Check("projection", projection.DerivedContentStatus == NextThought.Core.SessionDerivedContentStatus.Stale
                            && new NextThought.Core.SessionProjectedAnswer(Guid.NewGuid(), DateTimeOffset.UtcNow, "q", "a", "r",
                                NextThought.Core.AnswerCompletion.Interrupted).IsComplete == false);

        var passed = checks.All(check => check.Passed);
        var report = new
        {
            passed,
            total = checks.Count,
            failed = checks.Where(check => !check.Passed).Select(check => check.Name).ToList(),
            checks = checks.Select(check => new { check.Name, check.Passed })
        };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? $"acceptance-check: passed ({checks.Count})" : "acceptance-check: failed");
        Close();
    }


    /// <summary>Copies a program directory, replaces a file and restores the backup.</summary>
}
