#!/usr/bin/env python3
"""Regenerate Windows/VIEW_PARITY.md from Sources/NextThought/*.swift.

Run from the repository root (or anywhere; paths are resolved from this file):
    python Windows/scripts/scan_views.py

Task ids come from Windows/FIX_TASKS.md. The Windows mapping is page-level:
subviews keep their page file in the mapping but are marked 未做 until the
individual view is actually ported.
"""
from __future__ import annotations
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "Sources" / "NextThought"
OUT = ROOT / "Windows" / "VIEW_PARITY.md"
TASKS = ROOT / "Windows" / "FIX_TASKS.md"

fallback = {
    "AppLanguage.swift": "E25", "AppNavigationSidebar.swift": "E4", "AutoEndViews.swift": "E4",
    "ControlComponents.swift": "C9", "FishboneView.swift": "E15", "HitTargets.swift": "C9",
    "LiquidGlass.swift": "C9", "LiveSessionAcceptanceComponentSamples.swift": "E4",
    "LiveWindowController.swift": "E4", "NotesFormatBar.swift": "E15",
    "NotesSettingsView.swift": "E15", "OperationFeedback.swift": "C9",
    "PaperTextFieldStyle.swift": "C9", "RegionScreenshotPicker.swift": "E13",
    "SettingsAdvanced.swift": "E25", "SettingsStatusRows.swift": "E25",
    "ShortcutSettingsView.swift": "E25", "Stage.swift": "C9",
    "StructuredNotesView.swift": "E15", "SubtitleBarSettingsView.swift": "C12",
    "SubtitleBarView.swift": "C12", "Theme.swift": "C9", "UnderlineTabs.swift": "C9",
    "ViewpointMatrixView.swift": "E15",
}
winmap = {
    "HomeView.swift": "Views/HomeView.axaml",
    "LiveSessionView.swift": "Views/LiveView.axaml",
    "NotesPageView.swift": "Views/NotesView.axaml",
    "HistoryView.swift": "Views/HistoryView.axaml",
    "WorkspacesView.swift": "Views/SeriesView.axaml",
    "SettingsView.swift": "Views/SettingsView.axaml",
    "CueIsland.swift": "Views/CueIslandWindow.axaml",
    "SubtitleBarController.swift": "Views/SubtitleBarWindow.axaml",
    "TeleprompterController.swift": "Views/TeleprompterWindow.axaml",
    "RegionScreenshotPicker.swift": "Views/RegionCaptureWindow.axaml",
    "ContentView.swift": "Views/MainWindow.axaml",
    "AppNavigationSidebar.swift": "Views/MainWindow.axaml",
}
top_views = {
    "HomeView": "HomeView.swift",
    "LiveSessionView": "LiveSessionView.swift",
    "NotesPageView": "NotesPageView.swift",
    "HistoryView": "HistoryView.swift",
    "WorkspacesView": "WorkspacesView.swift",
    "SettingsView": "SettingsView.swift",
    "CueIslandView": "CueIsland.swift",
    "SubtitleBarView": "SubtitleBarController.swift",
    "TeleprompterView": "TeleprompterController.swift",
}

# Views that now have a Windows counterpart beyond the page-level mapping. Each entry is
# (Windows file, screenshot directory); the directory is relative to Windows/validation/screens.
# Keep this list to views that were actually ported and reviewed in a task; everything else
# stays 未做 until its own task lands.
IMPLEMENTED = {
    ("HomeView.swift", "HomeView"): ("Views/HomeView.axaml", "home"),
    ("HomeView.swift", "StartComposer"): ("Views/Home/StartComposer.axaml", "home"),
    ("HomeView.swift", "ContinueSeries"): ("Views/Home/ContinueSeries.axaml", "home"),
    ("HomeView.swift", "HomeScreenshotCard"): ("Views/Home/HomeScreenshotCard.axaml", "home"),
    ("HomeView.swift", "RecentMeetings"): ("Views/Home/RecentMeetings.axaml", "home"),
    ("Stage.swift", "InkWash"): ("Views/Controls/InkWash.cs", "home"),
    ("Stage.swift", "SceneGlyph"): ("Views/Controls/SceneGlyph.cs", "home"),
    ("Stage.swift", "SceneTag"): ("Views/Controls/SceneTag.cs", "live"),
    ("NotesPageView.swift", "MeetingPage"): ("Views/NotesView.axaml", "notes"),
    ("NotesPageView.swift", "NotesBody"): ("Views/NotesView.axaml", "notes"),
    ("NotesPageView.swift", "NotesSectionView"): ("Views/NotesView.axaml", "notes"),
    ("NotesPageView.swift", "NotesProgressLine"): ("Views/NotesView.axaml", "notes"),
    ("NotesPageView.swift", "SessionEvidencePane"): ("Views/NotesView.axaml", "notes"),
    ("NotesPageView.swift", "NotesItemEditSheet"): ("Views/NotesView.axaml", "notes"),
    ("NotesPageView.swift", "ConfirmBadge"): ("Views/NotesView.axaml", "notes"),
    ("NotesPageView.swift", "VerdictBadge"): ("Views/NotesView.axaml", "notes"),
    ("NotesPageView.swift", "AIHeadingText"): ("Views/NotesView.axaml", "notes"),
    ("NotesPageView.swift", "TimeLink"): ("Views/NotesView.axaml", "notes"),
    ("NotesPageView.swift", "CorrectedTranscript"): ("Views/CorrectedTranscriptView.axaml", "CorrectedTranscript"),
    ("OutlineView.swift", "OutlineView"): ("Views/NotesView.axaml", "notes"),
    ("SessionInfoView.swift", "SessionInfoView"): ("Views/NotesView.axaml", "notes"),
    ("MeetingNotesEditor.swift", "MeetingNotesEditor"): ("Views/NotesEditorWindow.axaml", "MeetingNotesEditor"),
    ("MeetingNotesEditor.swift", "MeetingEvidenceSidebar"): ("Views/NotesEditorWindow.axaml", "MeetingNotesEditor"),
    ("HistoryView.swift", "ArchivedScreenshot"): ("Views/HistoryView.axaml", "history"),
    ("HistoryView.swift", "HistoryFilterPicker"): ("Views/HistoryView.axaml", "history"),
    ("HistoryView.swift", "MeetingArchiveOverview"): ("Views/HistoryView.axaml", "history"),
    ("HistoryView.swift", "SeriesManagementMenuItems"): ("Views/HistoryView.axaml", "history"),
    ("HistoryView.swift", "SessionAnswersList"): ("Views/HistoryView.axaml", "history"),
    ("HistoryView.swift", "SessionRow"): ("Views/HistoryView.axaml", "history"),
    ("SessionExportUI.swift", "SessionExportSheet"): ("Views/SessionExportSheet.axaml", "SessionExportSheet"),
    ("PrepareView.swift", "PrepareView"): ("Views/PrepareView.axaml", "prepare"),
    ("PrepareView.swift", "PreparationStartBar"): ("Views/PrepareView.axaml", "prepare"),
    ("PrepareView.swift", "MaterialsActions"): ("Views/PrepareView.axaml", "prepare"),
    ("WorkspacesView.swift", "WorkspaceEditor"): ("Views/SeriesView.axaml", "series"),
    ("WorkspacesView.swift", "FieldEditor"): ("Views/SeriesView.axaml", "series"),
    ("WorkspacesView.swift", "WorkspaceBasicTab"): ("Views/SeriesView.axaml", "series"),
    ("WorkspacesView.swift", "WorkspaceRequirementsTab"): ("Views/SeriesView.axaml", "series"),
    ("WorkspacesView.swift", "WorkspaceDocumentsTab"): ("Views/SeriesView.axaml", "series"),
    ("WorkspacesView.swift", "ReviewTab"): ("Views/SeriesView.axaml", "series"),
    ("WorkspacesView.swift", "KnowledgeTab"): ("Views/SeriesView.axaml", "series"),
    ("WorkspacesView.swift", "ExperienceLessonRow"): ("Views/SeriesView.axaml", "series"),
    ("WorkspacesView.swift", "BankRow"): ("Views/SeriesView.axaml", "series"),
    ("WorkspacesView.swift", "PreparationQueueSummary"): ("Views/SeriesView.axaml", "series"),
    ("WorkspacesView.swift", "WorkspaceList"): ("Views/SeriesView.axaml", "series"),
    ("WorkspacesView.swift", "WorkspaceSectionHeading"): ("Views/SeriesView.axaml", "series"),
    ("WorkspacesView.swift", "WorkspaceSingleLineField"): ("Views/SeriesView.axaml", "series"),
    ("WorkspacesView.swift", "WorkspaceTextArea"): ("Views/SeriesView.axaml", "series"),
    ("WorkspacesView.swift", "WorkspaceSkillsSection"): ("Views/SeriesView.axaml", "series"),
    ("SettingsPages.swift", "SceneRow"): ("Views/SettingsView.axaml", "settings-pages"),
    ("SettingsPages.swift", "SceneSettingsPage"): ("Views/SettingsView.axaml", "settings-pages"),
    ("QuickSettings.swift", "QuickSettingsPanel"): ("Views/QuickSettingsPanel.axaml", "settings-pages"),
    ("QuickSettings.swift", "QuickStatusRow"): ("Views/QuickSettingsPanel.axaml", "settings-pages"),
    ("AnswerServiceViews.swift", "APIPresetManager"): ("Views/SettingsView.axaml", "settings-pages"),
    ("AnswerServiceViews.swift", "APIEffortRow"): ("Views/SettingsView.axaml", "settings-pages"),
    ("SettingsView.swift", "CodexFastModeToggle"): ("Views/SettingsView.axaml", "settings-pages"),
    ("SettingsView.swift", "CodexReasoningPicker"): ("Views/SettingsView.axaml", "settings-pages"),
    ("SettingsView.swift", "EngineTestRow"): ("Views/SettingsView.axaml", "settings-pages"),
    ("SettingsView.swift", "GitHubAccessSection"): ("Views/SettingsView.axaml", "settings-pages"),
    ("SettingsView.swift", "LanguagePicker"): ("Views/SettingsView.axaml", "settings-pages"),
    ("SettingsView.swift", "SkillEditor"): ("Views/SettingsView.axaml", "settings-pages"),
    ("SettingsView.swift", "SkillSettings"): ("Views/SettingsView.axaml", "settings-pages"),
    ("SettingsView.swift", "SettingsPageView"): ("Views/SettingsView.axaml", "settings-pages"),
    ("SettingsView.swift", "SettingsPageContent"): ("Views/SettingsView.axaml", "settings-pages"),
    ("SettingsGeneralData.swift", "BackupSettingsSection"): ("Views/BackupSettingsSection.axaml", "backup"),
    ("SettingsGeneralData.swift", "PermissionSettingsSection"): ("Views/SettingsView.axaml", "settings-pages"),
    ("InsightSettingsView.swift", "InsightSettings"): ("Views/SettingsView.axaml", "settings-pages"),
    ("Onboarding.swift", "OnboardingView"): ("Views/OnboardingView.axaml", "onboarding"),
    ("Onboarding.swift", "GuideRow"): ("Views/OnboardingView.axaml", "onboarding"),
    ("Onboarding.swift", "CueIslandMock"): ("Views/OnboardingView.axaml", "onboarding"),
    ("Onboarding.swift", "SetupRow"): ("Views/OnboardingView.axaml", "onboarding"),
    ("BackupRestoreReview.swift", "BackupRestoreReviewView"): ("Views/BackupRestoreReviewView.axaml", "backup"),
    # Round 3: live page, answer modules, glossary, knowledge review, capture and web search.
    ("LiveSessionView.swift", "LiveSessionView"): ("Views/LiveView.axaml", "live"),
    ("LiveSessionView.swift", "LiveTopBar"): ("Views/LiveView.axaml", "live"),
    ("LiveSessionView.swift", "RecordingTimer"): ("Views/LiveView.axaml", "live"),
    ("LiveSessionView.swift", "PauseResumeButton"): ("Views/LiveView.axaml", "live"),
    ("LiveSessionView.swift", "AnswerSettingsMenu"): ("Views/LiveView.axaml", "live"),
    ("LiveSessionView.swift", "QuestionMark"): ("Views/LiveView.axaml", "live"),
    ("LiveSessionView.swift", "CandidateStrip"): ("Views/LiveView.axaml", "live"),
    ("LiveSessionView.swift", "NotesOnlyColumn"): ("Views/LiveView.axaml", "live"),
    ("LiveSessionView.swift", "WebStatusLine"): ("Views/LiveView.axaml", "live"),
    ("LiveSessionView.swift", "WebSources"): ("Views/LiveView.axaml", "live"),
    ("LiveSessionView.swift", "RefinementRow"): ("Views/LiveView.axaml", "live"),
    ("LiveSessionView.swift", "AnswerMoreMenu"): ("Views/LiveView.axaml", "live"),
    ("LiveSessionView.swift", "EvidenceLine"): ("Views/LiveView.axaml", "live"),
    ("LiveSessionView.swift", "EvidenceDetails"): ("Views/LiveView.axaml", "live"),
    ("LiveSessionView.swift", "LiveComposer"): ("Views/LiveView.axaml", "live"),
    ("LiveSessionView.swift", "PhoneStatusIcon"): ("Views/LiveView.axaml", "live"),
    ("LiveSessionView.swift", "ScreenStatusIcon"): ("Views/LiveView.axaml", "live"),
    ("LiveSessionView.swift", "AnswerColumn"): ("Views/LiveView.axaml", "live"),
    ("LiveSessionView.swift", "RightColumn"): ("Views/LiveView.axaml", "live"),
    ("LiveSessionView.swift", "TranscriptColumn"): ("Views/LiveView.axaml", "live"),
    ("LiveSessionView.swift", "TranscriptLine"): ("Views/LiveView.axaml", "live"),
    ("LiveSessionView.swift", "TranscriptStatus"): ("Views/LiveView.axaml", "live"),
    ("LiveSessionView.swift", "PartialLines"): ("Views/LiveView.axaml", "live"),
    ("LiveSessionView.swift", "SpeakerLabel"): ("Views/LiveView.axaml", "live"),
    ("LiveSessionView.swift", "AnswerText"): ("Views/LiveView.axaml", "live"),
    ("AssistantView.swift", "AnswerHistoryPanel"): ("Views/Assistant/AnswerHistoryPanel.axaml", "assistant"),
    ("AssistantView.swift", "DetectionStrip"): ("Views/Assistant/DetectionStrip.axaml", "assistant"),
    ("AssistantView.swift", "ConfidenceBar"): ("Views/Assistant/DetectionStrip.axaml", "assistant"),
    ("AssistantView.swift", "ScreenshotCard"): ("Views/Assistant/ScreenshotCard.axaml", "assistant"),
    ("AssistantView.swift", "AnswerBody"): ("Views/LiveView.axaml", "live"),
    ("AssistantView.swift", "AnswerEvidenceDetails"): ("Views/LiveView.axaml", "live"),
    ("AssistantView.swift", "AnswerFooter"): ("Views/LiveView.axaml", "live"),
    ("AssistantView.swift", "AnswerHeader"): ("Views/LiveView.axaml", "live"),
    ("AssistantView.swift", "AnswerPanel"): ("Views/LiveView.axaml", "live"),
    ("AssistantView.swift", "AssistantModuleContent"): ("Views/DetachedModuleWindow.axaml", "assistant"),
    ("AssistantView.swift", "TranscriptStatusLine"): ("Views/LiveView.axaml", "live"),
    ("AssistantView.swift", "WebLookupStrip"): ("Views/LiveView.axaml", "live"),
    ("AssistantView.swift", "WebSourcesList"): ("Views/LiveView.axaml", "live"),
    ("DetachedModules.swift", "DetachedModuleView"): ("Views/DetachedModuleWindow.axaml", "assistant"),
    ("GlossaryView.swift", "GlossarySettingsEntry"): ("Views/GlossaryWindow.axaml", "glossary"),
    ("GlossaryView.swift", "GlossaryView"): ("Views/GlossaryWindow.axaml", "glossary"),
    ("GlossaryView.swift", "MisheardMark"): ("Views/GlossaryWindow.axaml", "glossary"),
    ("GlossaryView.swift", "TranscriptFindField"): ("Views/GlossaryWindow.axaml", "glossary"),
    ("GlossaryView.swift", "AddToGlossarySheet"): ("Views/AddToGlossarySheet.axaml", "glossary"),
    ("KnowledgeReviewSection.swift", "KnowledgeCandidateCard"): ("Views/SeriesView.axaml", "knowledge-review"),
    ("KnowledgeReviewSection.swift", "KnowledgeReviewSection"): ("Views/SeriesView.axaml", "knowledge-review"),
    ("KnowledgeReviewSection.swift", "SeriesChangeCandidateCard"): ("Views/SeriesView.axaml", "knowledge-review"),
    ("ProjectLibrarySupport.swift", "MaterialReviewSheet"): ("Views/MaterialReviewWindow.axaml", "knowledge-review"),
    ("ProjectLibrarySupport.swift", "WorkspaceRecordsTab"): ("Views/SeriesView.axaml", "knowledge-review"),
    ("ScreenRegionSheet.swift", "ScreenRegionSheet"): ("Views/RegionCaptureWindow.axaml", "screenshot-context"),
    ("ContentView.swift", "WindowPickerSheet"): ("Views/WindowPickerWindow.axaml", "screenshot-context"),
    ("AnswerServiceViews.swift", "WebSearchSettings"): ("Views/SettingsView.axaml", "web-search"),
    ("AnswerServiceViews.swift", "WebSearchAdvanced"): ("Views/SettingsView.axaml", "web-search"),
    ("HomeView.swift", "OnboardingResumeRow"): ("Views/Home/OnboardingResumeRow.axaml", "home"),
    # Round 6: insights, meeting user notes, translation lanes, speakers, document settings.
    ("InsightsPanel.swift", "InsightsPanel"): ("Views/InsightsPanel.axaml", "insights"),
    ("InsightsPanel.swift", "SlideSection"): ("Views/InsightsPanel.axaml", "insights"),
    ("InsightsPanel.swift", "ScreenSection"): ("Views/InsightsPanel.axaml", "insights"),
    ("InsightsPanel.swift", "AnswerProgressStrip"): ("Views/InsightsPanel.axaml", "insights"),
    ("InsightsPanel.swift", "ScreenWatchOffHint"): ("Views/InsightsPanel.axaml", "insights"),
    ("MeetingUserNotes.swift", "MeetingUserNotes"): ("Views/MeetingUserNotes.axaml", "meeting-notes"),
    ("LiveTranslation.swift", "LiveTranslationToggle"): ("Views/LiveView.axaml", "translation"),
    ("LiveTranslation.swift", "LiveTranslationHosts"): ("Views/LiveView.axaml", "translation"),
    ("LiveTranslation.swift", "TranslationLaneHost"): ("Views/LiveView.axaml", "translation"),
    ("SpeakerViews.swift", "SpeakerEditor"): ("Views/SpeakerEditorWindow.axaml", "speakers"),
    ("SpeakerViews.swift", "MeEditor"): ("Views/SpeakerEditorWindow.axaml", "speakers"),
    ("SpeakerViews.swift", "MyVoiceSettings"): ("Views/MyVoiceWindow.axaml", "speakers"),
    ("SpeakerViews.swift", "SpeakerStrip"): ("Views/NotesView.axaml", "speakers"),
    ("MyVoiceViews.swift", "MyVoiceSetupPanel"): ("Views/MyVoiceWindow.axaml", "speakers"),
    ("MyVoiceViews.swift", "OnboardingVoicePage"): ("Views/OnboardingVoiceWindow.axaml", "onboarding"),
    ("DocumentModelSettings.swift", "DocumentReadingSettings"): ("Views/DocumentReadingSettings.axaml", "document-settings"),
    ("DocumentModelSettings.swift", "DocumentComponentsAdvanced"): ("Views/DocumentComponentsAdvanced.axaml", "document-settings"),
    ("DocumentModelSettings.swift", "DocumentChoiceSummary"): ("Views/DocumentChoiceSummary.axaml", "document-settings"),
    ("DocumentModelSettings.swift", "ManagedModelRow"): ("Views/ManagedModelRow.axaml", "document-settings"),
    ("AssistantView.swift", "ConversationPanel"): ("Views/Assistant/ConversationPanel.axaml", "assistant"),
    ("AssistantView.swift", "ConversationBubble"): ("Views/Assistant/ConversationBubble.axaml", "assistant"),
    ("AssistantView.swift", "EmptyConversationHint"): ("Views/Assistant/EmptyConversationHint.axaml", "assistant"),
    ("AssistantView.swift", "PartialBubbles"): ("Views/Assistant/PartialBubbles.axaml", "assistant"),    ("CueIsland.swift", "Chip"): ("Views/CueIslandWindow.axaml", "island"),
    ("CueIsland.swift", "CueRow"): ("Views/CueIslandWindow.axaml", "island"),
    ("CueIsland.swift", "IslandAction"): ("Views/CueIslandWindow.axaml", "island"),
    ("CueIsland.swift", "IslandButton"): ("Views/CueIslandWindow.axaml", "island"),
    ("CueIsland.swift", "ThinkingDots"): ("Views/CueIslandWindow.axaml", "island"),
    ("SubtitleBarView.swift", "SubtitleBarView"): ("Views/SubtitleBarWindow.axaml", "subtitle-bar"),
    ("SubtitleBarSettingsView.swift", "SubtitleBarSettingsSection"): ("Views/SettingsView.axaml", "settings-pages"),
    ("RegionScreenshotPicker.swift", "RegionScreenshotPicker"): ("Views/RegionCaptureWindow.axaml", "screenshot-question"),
    ("RegionScreenshotPicker.swift", "RegionSelectionCanvas"): ("Views/RegionCaptureWindow.axaml", "screenshot-question"),
    ("ContentView.swift", "ContentView"): ("Views/MainWindow.axaml", "final-shell"),
    ("ContentView.swift", "MainShell"): ("Views/MainWindow.axaml", "final-shell"),
    ("ContentView.swift", "SectionStack"): ("Views/MainWindow.axaml", "final-shell"),
    ("ContentView.swift", "PulseDot"): ("Views/MainWindow.axaml", "final-shell"),
    ("SessionExportUI.swift", "SessionExportSheet"): ("Views/SessionExportSheet.axaml", "export"),
    ("BackupRestoreReview.swift", "BackupRestoreReviewView"): ("Views/BackupRestoreReviewView.axaml", "backup-review"),
    ("ContentView.swift", "QRCodeView"): ("Views/Controls/QrCodeView.cs", "share-test"),
    ("ContentView.swift", "ShareTestSheet"): ("Views/ShareTestWindow.axaml", "share-test"),

}

def task_map() -> dict[str, str]:
    result: dict[str, str] = {}
    for line in TASKS.read_text(encoding="utf-8").splitlines():
        if not line.startswith("|"):
            continue
        cells = line.split("|")
        if len(cells) < 4:
            continue
        tid = cells[1].strip().replace("<br>", "")
        if not re.fullmatch(r"[CE]\d+", tid):
            continue
        for cell in cells[2:]:
            for name in re.findall(r"([A-Za-z0-9_+]+\.swift)", cell.replace("<br>", "")):
                result.setdefault(name, tid)
    return result

def main() -> None:
    tasks = task_map()
    rows = []
    pattern = re.compile(r"\b(?:struct|class)\s+(\w+)(?:\s*<[^>{}]*>)?\s*:\s*(?:SwiftUI\.)?View\b")
    for path in sorted(SRC.glob("*.swift")):
        text = path.read_text(encoding="utf-8", errors="ignore")
        for match in pattern.finditer(text):
            name = match.group(1)
            rel = path.relative_to(ROOT).as_posix()
            tid = tasks.get(path.name) or fallback.get(path.name, "E4")
            windows = winmap.get(path.name, "—")
            explicit = IMPLEMENTED.get((path.name, name))
            if explicit is not None:
                windows, shot_dir = explicit
                status = "已实现、未验证"
                shot = f"`Windows/validation/screens/{shot_dir}/`"
            else:
                implemented = top_views.get(name) == path.name
                status = "已实现、未验证" if implemented else "未做"
                shot = f"`Windows/validation/screens/{Path(windows).stem}/`" if windows != "—" and implemented else "—"
            rows.append((rel, name, tid, windows, status, shot))
    rows.sort(key=lambda row: (row[2], row[0], row[1]))
    text = [
        "# Windows / Mac 视图对照（VIEW_PARITY）", "",
        "由 `Windows/scripts/scan_views.py` 从 `Sources/NextThought/` 扫描生成，共 **%d** 个 `: View` 类型。" % len(rows),
        "状态只用第 4 节四态；没有实机人工对照，因此没有「已验证」。",
        "页面级对应文件按 Mac 页面文件映射；子视图只有在 `IMPLEMENTED` 表里真正移植并出过截图的才写「已实现、未验证」，其余保持「未做」。", "",
        "| Swift 文件 | 视图名 | 任务编号 | Windows 对应文件 | 状态 | 截图路径 |",
        "|---|---|---|---|---|---|",
    ]
    for row in rows:
        text.append("| `%s` | `%s` | %s | %s | %s | %s |" % row)
    text += ["", "总计：**%d** 个视图；已有 Windows 页面 **%d** 个，其余 **%d** 个待做。" % (
        len(rows), sum(1 for r in rows if r[4] == "已实现、未验证"), sum(1 for r in rows if r[4] != "已实现、未验证"))]
    OUT.write_text("\n".join(text) + "\n", encoding="utf-8")
    print("wrote", OUT, "rows", len(rows))

if __name__ == "__main__":
    main()
