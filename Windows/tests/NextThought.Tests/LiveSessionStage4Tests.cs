using System.Text.RegularExpressions;
using NextThought.Core;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// 任务书 §6.5 / E4（阶段 4）: the live page (LiveSessionView.swift) is rebuilt element by element
/// against the Mac 3.4.0 reference screenshots (mac-screens/live/live-*.png and the twenty
/// mac-screens/live-states/ states): the single-row LiveTopBar, the flat transcript column with its
/// vertical divider, the three-tab right column with the raised answer card, and the LiveComposer.
/// These assertions read the committed XAML and state files: they exist to make the old two-row /
/// card / dropdown page fail loudly before the rebuild.
/// </summary>
public sealed class LiveSessionStage4Tests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Views", "LiveView.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Desktop(params string[] parts) =>
        Path.Combine([Root(), "Windows", "src", "NextThought.Desktop", .. parts]);

    private static string Read(params string[] parts) => File.ReadAllText(Desktop(parts));

    private static string Live() => Read("Views", "LiveView.axaml");

    private static int Count(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0;
             index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
            count++;
        return count;
    }

    // ------------------------------------------------------------------------------------
    // 顶部条: one row, Mac LiveTopBar expandedControls (recording timer, scene tag, title
    // block, mode segmented, teleprompter + more icon buttons, bordered pause and end).
    // ------------------------------------------------------------------------------------

    [Fact]
    public void TheTopBarIsOneRowWithTheMacExpandedControls()
    {
        var live = Live();
        // One row, 52 pt, hairline underneath (LiveTopBar.frame(height: 52) + bottom hairline).
        Assert.Contains("x:Name=\"LiveTopBar\"", live);
        Assert.Contains("Height=\"52\"", live);
        // Recording timer: on-air dot, elapsed, 正在聆听 / 已暂停 / 未在聆听.
        Assert.Contains("{Binding IsLiveOnAir}", live);
        Assert.Contains("{Binding RecordingElapsedText}", live);
        Assert.Contains("{Binding LiveListeningStatus}", live);
        // Scene tag and the title block (title + series name under it).
        Assert.Contains("controls:SceneTag", live);
        Assert.Contains("{Binding MeetingTitle}", live);
        Assert.Contains("{Binding LiveSeriesName}", live);
        // The title is a text block now, not the old editable text box, and it is not clipped.
        Assert.DoesNotContain("<TextBox Text=\"{Binding MeetingTitle}\"", live);
        Assert.DoesNotMatch(new Regex("MeetingTitle[^>]*TextTrimming"), live);
        // The old page subtitle (dem-data notice) is not part of the Mac top bar.
        Assert.DoesNotContain("PageSubtitle", live);
        // The mode switch is the Mac InkSegmented: a sunken container, a white selected block.
        Assert.Contains("x:Name=\"LiveModeSegmented\"", live);
        Assert.Contains("Classes=\"seg-container\"", live);
        Assert.Contains("Classes=\"seg\"", live);
        Assert.Contains("{loc:Tr 自动回答}", live);
        Assert.Contains("{loc:Tr 只记录}", live);
        Assert.Contains("AnswerModeAuto_Click", live);
        Assert.Contains("AnswerModeRecord_Click", live);
        // Two icon buttons: the answer window (teleprompter) and the more menu.
        Assert.Contains("x:Name=\"LiveTeleprompterButton\"", live);
        Assert.Contains("IconRectangleTopthirdInsetFilled", live);
        Assert.Contains("x:Name=\"LiveMoreMenuButton\"", live);
        Assert.Contains("IconEllipsis", live);
        // Bordered pause and end (Mac SecondaryButtonStyle), pause only while listening.
        Assert.Contains("x:Name=\"LivePauseButton\"", live);
        Assert.Contains("{Binding LivePauseButtonText}", live);
        Assert.Contains("TogglePauseListeningCommand", live);
        Assert.Contains("x:Name=\"LiveEndButton\"", live);
        Assert.Contains("{loc:Tr 结束}", live);
        Assert.Contains("StopRecordingCommand", live);
        // The model name is gone from the top bar (it lives in 回答设置 inside the more menu).
        Assert.DoesNotContain("LiveAnswerModelLabel", live);
    }

    // ------------------------------------------------------------------------------------
    // 左栏: flat (no card, no shadow), one vertical line against the right column, the
    // Mac transcript rows (对方 / 我, question tag, echo strikethrough + 是我说的, partial
    // lines in the secondary colour), the candidate strip and the status row with the lock.
    // ------------------------------------------------------------------------------------

    [Fact]
    public void TheTranscriptColumnIsFlatAndCarriesTheMacParts()
    {
        var live = Live();
        Assert.Contains("x:Name=\"LiveTranscriptColumn\"", live);
        Assert.Contains("x:Name=\"LiveColumnDivider\"", live);
        Assert.Contains("Background=\"{DynamicResource Hairline}\"", live);
        // No card and no shadow on either column: the old page wrapped both in Border.card.
        Assert.Equal(0, Count(live, "Classes=\"card\""));
        Assert.DoesNotContain("BoxShadow=\"{DynamicResource PaperCardShadow}\"", live[..live.IndexOf("x:Name=\"AnswerCard\"", StringComparison.Ordinal)]);
        // Header: 实时对话 overline and the back-to-live follow button.
        Assert.Contains("{loc:Tr 实时对话}", live);
        Assert.Contains("ReturnLiveToFollowCommand", live);
        Assert.Contains("IconTrayAndArrowDown", live);
        // Rows: the speaker column is 对方 / 我, not a timestamp label.
        Assert.Contains("{Binding SpeakerName}", live);
        Assert.DoesNotContain("Text=\"{Binding Label}\"", live);
        Assert.Contains("{Binding IsQuestion}", live);
        Assert.Contains("{loc:Tr 问题}", live);
        // Echo row: strikethrough and 是我说的.
        Assert.Contains("TextDecorations=\"Strikethrough\"", live);
        Assert.Contains("{loc:Tr 是我说的}", live);
        Assert.Contains("MarkNotEcho_Click", live);
        // Utterances still being recognised are secondary and at the end.
        Assert.Contains("{Binding SystemPartial}", live);
        Assert.Contains("{Binding MicrophonePartial}", live);
        // The old explanation line is gone.
        Assert.DoesNotContain("系统声音与麦克风分别处理", live);
        // Candidate strip: 可能的问题 + 回答这句.
        Assert.Contains("{loc:Tr 可能的问题}", live);
        Assert.Contains("{Binding LiveCandidateText}", live);
        Assert.Contains("AnswerCandidate_Click", live);
        // Status row: source, engine, echo count, privacy lock.
        Assert.Contains("{Binding AudioSourceText}", live);
        Assert.Contains("{Binding LiveAudioEngineLabel}", live);
        Assert.Contains("{Binding EchoDroppedText}", live);
        Assert.Contains("IconLock", live);
        var state = Read("AppState.LivePage.cs");
        Assert.Contains("MarkNotEcho", state);
        // J6: the speaker column is generated from an observable name so voice names update in place.
        var transcriptRowSource = Read("TranscriptRow.cs");
        Assert.Contains("[ObservableProperty] private string speakerName", transcriptRowSource);
        Assert.Contains("RestoreEcho", transcriptRowSource);
    }

    // ------------------------------------------------------------------------------------
    // 右栏: 实时纪要 / 问答 / 我的笔记 in that order, the 建议回答 header with the history
    // navigation, the question in the secondary colour, the raised white rounded answer
    // card filling the rest of the column, and the refinement row + evidence line at its
    // foot. 历史回答 and 截图 are not dropdowns any more: Mac puts them in the header
    // navigation and in the composer.
    // ------------------------------------------------------------------------------------

    [Fact]
    public void TheRightColumnHasTheThreeMacTabsAndTheRaisedAnswerCard()
    {
        var live = Live();
        Assert.Contains("x:Name=\"LiveRightColumn\"", live);
        var liveNotes = live.IndexOf("{loc:Tr 实时纪要}", StringComparison.Ordinal);
        var answers = live.IndexOf("{loc:Tr 问答}", StringComparison.Ordinal);
        var notes = live.IndexOf("{loc:Tr 我的笔记}", StringComparison.Ordinal);
        Assert.True(liveNotes >= 0 && answers > liveNotes && notes > answers,
            "right-column tabs must run 实时纪要, 问答, 我的笔记");
        Assert.Contains("x:Name=\"LiveNotesTab\"", live);
        Assert.Contains("x:Name=\"AnswerTab\"", live);
        Assert.Contains("x:Name=\"NotesTab\"", live);
        Assert.Contains("x:Name=\"LiveNotesPanel\"", live);
        Assert.Contains("x:Name=\"AnswerPanel\"", live);
        Assert.Contains("x:Name=\"NotesPanel\"", live);
        // 建议回答 header: sparkle icon, title, previous / 第几条 · 共几条 / next.
        Assert.Contains("IconSparkle", live);
        Assert.Contains("{Binding AnswerColumnTitle}", live);
        Assert.Contains("PreviousAnswerCommand", live);
        Assert.Contains("{Binding HistoryPositionText}", live);
        Assert.Contains("NextAnswerCommand", live);
        // The question under the header uses the secondary colour.
        Assert.Contains("{Binding DisplayedQuestionText}", live);
        // Raised answer card: surface, radius 22, contact + ambient shadow, hairline.
        Assert.Contains("x:Name=\"AnswerCard\"", live);
        Assert.Contains("CornerRadius=\"{DynamicResource RadiusLarge}\"", live);
        Assert.Contains("{DynamicResource Surface}", live);
        Assert.Contains("PaperCardShadow", live);
        // The answer text is the Mac MarkdownText at size 18.
        Assert.Contains("<views:MarkdownText Text=\"{Binding DisplayedAnswerText}\" BaseFontSize=\"18\"", live);
        // Foot of the card: 更简短 / 举例 / 英文版 pills, the "…" menu, then 依据.
        Assert.Contains("{loc:Tr 更简短}", live);
        Assert.Contains("{loc:Tr 举例}", live);
        Assert.Contains("{loc:Tr 英文版}", live);
        Assert.Contains("IconEllipsis", live);
        Assert.Contains("{loc:Tr 依据：}", live);
        Assert.Contains("{Binding EvidenceSummary}", live);
        // The two dropdown-looking sections are gone from the answer column.
        Assert.DoesNotContain("Expander", live);
        Assert.DoesNotContain("HistoryDisclosure", live);
        Assert.DoesNotContain("ScreenshotDisclosure", live);
        Assert.DoesNotContain("{loc:Tr 历史回答}", live);
        Assert.DoesNotContain("{loc:Tr 截图}", live);
    }

    [Fact]
    public void TheLiveNotesTabShowsTheMacEmptyState()
    {
        var live = Live();
        Assert.Contains("x:Name=\"LiveNotesPanel\"", live);
        Assert.Contains("{Binding LiveNotesStatusText}", live);
        Assert.Contains("{Binding LiveNotesHintText}", live);
        Assert.Contains("{Binding LiveNotesButtonText}", live);
        Assert.Contains("EnableLiveNotes_Click", live);
        var state = Read("AppState.LivePage.cs");
        Assert.Contains("实时纪要没有开", state);
        Assert.Contains("还没有连接回答服务，写不了实时纪要", state);
        Assert.Contains("说上几句之后，这里会出现这场会的要点：在谈什么、定了什么、谁要做什么。", state);
        Assert.Contains("开启实时纪要", state);
        Assert.Contains("现在更新", state);
    }

    // ------------------------------------------------------------------------------------
    // J6: 说话人 — the Mac SpeakerStrip above the transcript (people capsules + 「我是哪个？」),
    // voice names in the speaker column, and the right-click 「这句是谁说的」 assignment. The
    // recognised transcript.jsonl is never rewritten: a change is a voices.jsonl tag + meta.json.
    // ------------------------------------------------------------------------------------

    [Fact]
    public void TheLivePageHasTheMacSpeakerStripAndLineSpeakerMenu()
    {
        var live = Live();
        Assert.Contains("x:Name=\"LiveSpeakerStrip\"", live);
        Assert.Contains("{Binding LiveSpeakerStripVisible}", live);
        Assert.Contains("{Binding SpeakerPeople}", live);
        Assert.Contains("SpeakerChip_Click", live);
        Assert.Contains("x:Name=\"LiveWhoAmIButton\"", live);
        Assert.Contains("{loc:Tr 我是哪个？}", live);
        Assert.Contains("{Binding LiveAsksForMe}", live);
        Assert.Contains("WhoAmI_Click", live);
        Assert.Contains("TranscriptRow_PointerPressed", live);
        var code = Read("Views", "LiveView.axaml.cs");
        Assert.Contains("这句是谁说的", code);
        Assert.Contains("AssignLiveLineSpeakerAsync", code);
        Assert.Contains("LineSpeaker.Me", code);
        Assert.Contains("LineSpeaker.SomeoneNew", code);
        var state = Read("AppState.LivePage.cs");
        Assert.Contains("public bool LiveSpeakerStripVisible", state);
        Assert.Contains("public bool LiveAsksForMe", state);
        Assert.Contains("public void RefreshLiveSpeakerNames", state);
        Assert.Contains("public async Task AssignLiveLineSpeakerAsync", state);
        Assert.Contains("SessionSpeakerEdits.AssignLine", state);
        // The assignment goes through the archive tag + meta.json only.
        Assert.Contains("Sessions.AppendVoiceTagAsync", state);
        Assert.Contains("Sessions.SetVoiceLabelAsync", state);
        Assert.DoesNotContain("AppendTranscriptAsync", state);
        var row = Read("TranscriptRow.cs");
        Assert.Contains("public void ApplyLiveIdentity", row);
        Assert.Contains("public void MoveToVoice", row);
        Assert.Contains("[ObservableProperty] private string speakerName", row);
        Assert.Contains("[ObservableProperty] private int? voice", row);
    }

    [Fact]
    public void SpeakerAssignmentLeavesTheRecognisedTranscriptUntouched()
    {
        var line = new TranscriptLine
        {
            Id = Guid.NewGuid(),
            Speaker = "remote",
            Text = "如果删除缓存失败了怎么办",
            Date = DateTimeOffset.UtcNow,
            Voice = null
        };
        var lines = new List<TranscriptLine> { line };
        var before = System.Text.Json.JsonSerializer.Serialize(lines);
        var meta = new SessionMeta { Id = Guid.NewGuid(), Title = "演示", StartedAt = DateTimeOffset.UtcNow };
        var (updated, tag) = SessionSpeakerEdits.AssignLine(meta, lines, line.Id, LineSpeaker.Me);
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(lines));
        Assert.Equal(line.Id, tag.Id);
        Assert.True(updated.MeVoices?.Contains(tag.Voice) == true);
        var (merged, tags) = SessionSpeakerEdits.MergeVoices(updated, lines, [tag], tag.Voice, tag.Voice);
        Assert.Empty(tags);
        Assert.Equal(updated, merged);
        var applied = SessionSpeakerEdits.ApplyVoiceTags(lines, [tag]);
        Assert.Equal(tag.Voice, applied[0].Voice);
        Assert.Equal(line.Text, applied[0].Text);
    }

    // ------------------------------------------------------------------------------------
    // 底部输入条: input, shortcut hint, screenshot button, send, and the 立即回答 button.
    // ------------------------------------------------------------------------------------

    [Fact]
    public void TheComposerIsTheMacInputRow()
    {
        var live = Live();
        Assert.Contains("x:Name=\"LiveInputRow\"", live);
        Assert.Contains("{Binding LiveComposerPlaceholder}", live);
        Assert.Contains("{Binding CanSubmitLiveComposer}", live);
        Assert.Contains("Composer_KeyDown", live);
        Assert.Contains("Composer_Send_Click", live);
        // Mac ShortcutHint "⌘L" — Windows Ctrl+L.
        Assert.Contains("Ctrl+L", live);
        Assert.Contains("IconCameraViewfinder", live);
        Assert.Contains("{loc:Tr 框选区域}", live);
        Assert.Contains("{loc:Tr 选择窗口}", live);
        Assert.Contains("IconArrowUpCircleFill", live);
        Assert.Contains("{loc:Tr 立即回答}", live);
        Assert.Contains("ForceAnswerCommand", live);
        Assert.Contains("{Binding ForceAnswerShortcut}", live);
        Assert.Contains("assistant:ScreenshotCard", live);
    }

    [Fact]
    public void TheLiveSelfTestExercisesTheStage4Surfaces()
    {
        var check = Read("SelfTest", "Checks", "RunLiveUiCheckAsync.cs");
        Assert.Contains("LiveNotesTab", check);
        Assert.Contains("MarkNotEcho", check);
        Assert.Contains("LiveShowsInsightsPanel", check);
        Assert.Contains("live-notes-light.png", check);
        Assert.Contains("SpeakerName", check);
    }

    [Fact]
    public void TheStage4ScreenshotsAndCompareFileExist()
    {
        var directory = Path.Combine(Root(), "Windows", "validation", "screens", "live");
        Assert.True(File.Exists(Path.Combine(directory, "light.png")), "light.png");
        Assert.True(File.Exists(Path.Combine(directory, "dark.png")), "dark.png");
        var compare = File.ReadAllText(Path.Combine(directory, "compare.md"));
        Assert.Contains("§6.5", compare);
        Assert.Contains("LiveTopBar", compare);
        Assert.Contains("LiveComposer", compare);
    }
}
