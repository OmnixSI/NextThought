# 设置对照（SETTINGS_PARITY）

Mac 六页设置共 **113** 个偏好键。本表由 `Windows/scripts/settings_parity.py` 从 Swift 源码（UserDefaults/AppStorage/SceneSettings key/exportedKeys 的并集）提取；当前 **113 / 113**。键名、默认值、取值范围与「真正生效」尚未逐个对齐，因此 G5 仍未完成。

| 键名 | 默认类型 | 默认值 | Mac 设置视图 | Windows 状态 |
|---|---|---|---|---:|
| `AppleLanguages` | 未提取 | 未提取 | 未核对 | 未做 |
| `activeAPIPresetID` | 未提取 | 未提取 | 未核对 | 未做 |
| `activeWorkspaceID` | 未提取 | 未提取 | 未核对 | 未做 |
| `advancedSettingsEnabled` | 未提取 | 未提取 | 未核对 | 未做 |
| `answerBackend` | 未提取 | 未提取 | 未核对 | 未做 |
| `answerGloss` | Bool | true { | 未核对 | 未做 |
| `answerLanguage` | string | "auto" { | 未核对 | 未做 |
| `answerPreferences` | 未提取 | 未提取 | 未核对 | 未做 |
| `answerStyle` | 未提取 | 未提取 | 未核对 | 未做 |
| `apiBaseURL` | string | "https://api.openai.com/v1" | 未核对 | 未做 |
| `apiModel` | string | "" | 未核对 | 未做 |
| `apiPresets` | 未提取 | 未提取 | 未核对 | 未做 |
| `apiVisionEnabled` | 未提取 | 未提取 | 未核对 | 未做 |
| `appearance` | string | (UserDefaults.standard.object(forKey: "darkMode") as? Bool == true ? "dark" : "system") | 未核对 | 未做 |
| `assistantLayout` | 未提取 | 未提取 | 未核对 | 未做 |
| `autoApplyLessons` | 未提取 | 未提取 | 未核对 | 未做 |
| `autoBackup` | Bool | true | 未核对 | 未做 |
| `autoCollectKnowledgeCandidates` | Bool | UserDefaults.standard.object(forKey: "autoApplyLessons") as? Bool ?? true | 未核对 | 未做 |
| `autoSummarize` | Bool | true | 未核对 | 未做 |
| `automaticAnswers` | 未提取 | 未提取 | 未核对 | 未做 |
| `automaticUpdateChecks` | 未提取 | 未提取 | 未核对 | 未做 |
| `capturePrivacy` | Bool | true | 未核对 | 未做 |
| `cloudConsent` | 未提取 | 未提取 | 未核对 | 未做 |
| `cloudModel` | 未提取 | 未提取 | 未核对 | 未做 |
| `cloudProvider` | 未提取 | 未提取 | 未核对 | 未做 |
| `coachEnabled` | Bool | true { | 未核对 | 未做 |
| `coachGazeSensitivity` | 未提取 | 未提取 | 未核对 | 未做 |
| `coachWatchesGaze` | Bool | false { | 未核对 | 未做 |
| `codexFastMode` | 未提取 | 未提取 | 未核对 | 未做 |
| `codexModelID` | string | "" | 未核对 | 未做 |
| `codexOCREffort` | string | "medium" | 未核对 | 未做 |
| `codexOCRModel` | string | "gpt-6-luna" | 未核对 | 未做 |
| `codexOCRTimeout` | Double | 600 | 未核对 | 未做 |
| `codexReasoning` | string | "low" | 未核对 | 未做 |
| `companionPort` | Int | 8_765 | 未核对 | 未做 |
| `companionRelayURL` | string | "" | 未核对 | 未做 |
| `cueAlwaysOpen` | 未提取 | 未提取 | 未核对 | 未做 |
| `cueAvoidsSharing` | Bool | true { | 未核对 | 未做 |
| `cueLauncher` | Bool | true { | 未核对 | 未做 |
| `cueScreen` | string | "" { | 未核对 | 未做 |
| `cueStyle` | 未提取 | 未提取 | 未核对 | 未做 |
| `cueSurface` | 未提取 | 未提取 | 未核对 | 未做 |
| `cueWidth` | 未提取 | 未提取 | 未核对 | 未做 |
| `customNotesTemplates` | 未提取 | 未提取 | 未核对 | 未做 |
| `customShortcuts` | 未提取 | 未提取 | 未核对 | 未做 |
| `darkMode` | 未提取 | 未提取 | 未核对 | 未做 |
| `demoNotes` | 未提取 | 未提取 | 未核对 | 未做 |
| `demoOnboarding` | 未提取 | 未提取 | 未核对 | 未做 |
| `demoSettings` | 未提取 | 未提取 | 未核对 | 未做 |
| `documentOCRProvider` | 未提取 | 未提取 | 未核对 | 未做 |
| `documentProviderWasSelected` | 未提取 | 未提取 | 未核对 | 未做 |
| `echoSuppression` | Bool | true | 未核对 | 未做 |
| `globalSkillIDs` | 未提取 | 未提取 | 未核对 | 未做 |
| `insightAnswerProgress` | Bool | true | 未核对 | 未做 |
| `insightFollowSlides` | Bool | true | 未核对 | 未做 |
| `insightScreenInterval` | Double | 1 | 未核对 | 未做 |
| `insightScreenRegion` | 未提取 | 未提取 | 未核对 | 未做 |
| `insightScreenTarget` | string | "" | 未核对 | 未做 |
| `insightScreenWatch` | 未提取 | 未提取 | 未核对 | 未做 |
| `keepOnTop` | Bool | true | 未核对 | 未做 |
| `keepRecognizerWarm` | Bool | true | 未核对 | 未做 |
| `lastScene` | 未提取 | 未提取 | 未核对 | 未做 |
| `liveTranslation` | 未提取 | 未提取 | 未核对 | 未做 |
| `localOCRModelID` | string | ModelCatalog.paddleOCR.id | 未核对 | 未做 |
| `localStreamingModel` | 未提取 | 未提取 | 未核对 | 未做 |
| `meetingVenue` | 未提取 | 未提取 | 未核对 | 未做 |
| `microphoneEnabled` | Bool | true | 未核对 | 未做 |
| `microphoneVoiceProcessing` | 未提取 | 未提取 | 未核对 | 未做 |
| `notesLanguage` | string | "auto" | 未核对 | 未做 |
| `notesModelID` | string | "" | 未核对 | 未做 |
| `notesReasoning` | string | "high" | 未核对 | 未做 |
| `notesTemplateByType` | 未提取 | 未提取 | 未核对 | 未做 |
| `ocrFallbackToVision` | Bool | true | 未核对 | 未做 |
| `onboardingCompleted` | 未提取 | 未提取 | 未核对 | 未做 |
| `onboardingGuideCompletedVersion` | 未提取 | 未提取 | 未核对 | 未做 |
| `onboardingGuideDeferredVersion` | 未提取 | 未提取 | 未核对 | 未做 |
| `onboardingGuideDismissedVersion` | 未提取 | 未提取 | 未核对 | 未做 |
| `onboardingGuideEnabled` | 未提取 | 未提取 | 未核对 | 未做 |
| `onboardingGuideLayout` | 未提取 | 未提取 | 未核对 | 未做 |
| `onboardingGuideStartedVersion` | 未提取 | 未提取 | 未核对 | 未做 |
| `onboardingResumeRowDismissedVersion` | 未提取 | 未提取 | 未核对 | 未做 |
| `pinnedWorkspaceIDs` | 未提取 | 未提取 | 未核对 | 未做 |
| `presenterMode` | 未提取 | 未提取 | 未核对 | 未做 |
| `presenterSlideNotes` | Bool | true { | 未核对 | 未做 |
| `presenterSlidesScreen` | string | "" { | 未核对 | 未做 |
| `profile` | 未提取 | 未提取 | 未核对 | 未做 |
| `projectLibrarySort` | 未提取 | 未提取 | 未核对 | 未做 |
| `quickNotesWorkspaceID` | 未提取 | 未提取 | 未核对 | 未做 |
| `quickStartStep` | 未提取 | 未提取 | 未核对 | 未做 |
| `recognitionLanguage` | string | "auto" | 未核对 | 未做 |
| `recognitionVocabulary` | string | "" | 未核对 | 未做 |
| `recordSessions` | Bool | true | 未核对 | 未做 |
| `recordingNoticeShown` | 未提取 | 未提取 | 未核对 | 未做 |
| `reducedEffects` | 未提取 | 未提取 | 未核对 | 未做 |
| `sceneOverrides` | 未提取 | 未提取 | 未核对 | 未做 |
| `secondPassEnabled` | Bool | true | 未核对 | 未做 |
| `selectedTargetID` | string | "" | 未核对 | 未做 |
| `semanticRetrievalEnabled` | Bool | false | 未核对 | 未做 |
| `sessionMode` | 未提取 | 未提取 | 未核对 | 未做 |
| `settingsGroup` | 未提取 | 未提取 | 未核对 | 未做 |
| `simplifiedInterface` | 未提取 | 未提取 | 未核对 | 未做 |
| `subtitleBarFontSize` | Double | 24 | 未核对 | 未做 |
| `teleprompterFontSize` | Double | 22 | 未核对 | 未做 |
| `teleprompterHideBackground` | Bool | false | 未核对 | 未做 |
| `teleprompterOpacity` | Double | 1.0 | 未核对 | 未做 |
| `transcriptionBackend` | 未提取 | 未提取 | 未核对 | 未做 |
| `updateChannel` | 未提取 | 未提取 | 未核对 | 未做 |
| `updateRepository` | 未提取 | 未提取 | 未核对 | 未做 |
| `verifyUncertainWithModel` | 未提取 | 未提取 | 未核对 | 未做 |
| `webSearchMode` | 未提取 | 未提取 | 未核对 | 未做 |
| `webSearchProvider` | 未提取 | 未提取 | 未核对 | 未做 |
| `workspaceEditorFocusSection` | 未提取 | 未提取 | 未核对 | 未做 |
| `workspaceMigrationCompleted` | 未提取 | 未提取 | 未核对 | 未做 |

复算提取数：

```bash
python Windows/scripts/settings_parity.py
```
