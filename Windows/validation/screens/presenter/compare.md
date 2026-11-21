> 截图来源：提交 `940a0280`（2026-11-21 17:24，最终构建；浅/深/英与状态截图均由该提交生成）。



# 讲稿与屏幕观察对照（E9）

Mac 参照：`Sources/NextThought/AppState+Insights.swift`（startScreenWatch / screenRead / followSlide / followSpeech / prepareSlideNotes）、`LiveInsightsState.swift`（slideSource / slideIsManual / manualSlideAnchor / slideNotes / generatingNotes）、`PresenterMode.swift`、`SlideNotesCache.swift`、`SettingsPresenter.swift`、`InsightSettingsView.swift`、`ScreenContextService.swift`、`ScreenText`（NextThoughtCore/LiveInsights.swift）。
Windows 参照：`Windows/src/NextThought.Desktop/AppState.ScreenWatch.cs`、`AppState.SlideFollow.cs`、`AppState.Prepare.cs`、`AppState.Live.cs`、`Platform/ScreenOcrReader.cs`、`Core/SlideFollow.cs`、`Core/LiveInsights.cs`、`Core/SlideDeck.cs`、`Views/SettingsView.axaml`（视频与讲稿 → 屏幕与讲稿卡）。

## 行为对照

| # | Mac 行为 | Windows 实现 | 状态 |
|---|---|---|---|
| 1 | `startScreenWatch`：仅会中（未暂停、未停止）读取；默认关闭；演示模式直接跳过；失败后 `max(interval,20)` 秒重试；间隔最小 1 秒 | `StartScreenWatchTimer` 500ms tick：`InsightScreenWatch && Recording && !ListeningPaused && !Stopping && !Demo`；`screenWatchNext = now + max(1, InsightScreenInterval)`；失败 `screenWatchRetryAfter = now+20s` | ✅ 一致 |
| 2 | 每次读屏重新判定（像素未变也要重新评估问题/说话人变化），无新判断时保留页码 | 原来按 SHA256 变化才跟页；本次改为**每次 OCR 后都分类 + 跟页**（hash 仅留作诊断） | ✅ 本次补齐 |
| 3 | `readScreen` 失败：`读不到选定画面：… 请检查来源和屏幕录制权限。`；诊断记录 `insight.screen_failed` | `ScreenWatchError = ex.Message`、状态「读屏失败，20 秒后重试。」；无诊断文件（Windows 有日志文件，未单独记事件） | ⚠ 文案未用 Mac 原句（Windows 权限模型不同），无诊断事件名 |
| 4 | 跟页：`followSlide(text)` 用 SlideMatcher 排名 + Decide + 上一页保持；手动翻页 `slideIsManual` 时保持手动页，直到屏幕出现不同幻灯片才恢复自动 | `Core/SlideFollow.cs` `SlideFollowController.ObserveScreen`：Rank→Decide→Ambiguous 时若当前页仍高分则保留；manual 时 `ManualAnchor` 相同页保持，出现不同页恢复 `Source=Screen` | ✅ |
| 5 | 跟页失败/无匹配：`slideSeenAt` 20 秒宽限（演示 120 秒），超时清空当前页 | 同：`grace = presenting ? 120 : 20`，`SeenAt` 超时清空；`PresenterLayoutActive` 作为 presenting 判定 | ✅ |
| 6 | `followSpeech`：只用自己的转写句；候选为当前页 +1/+2（无当前页时全 deck）；带讲稿备注的页才参与；>=16 字符；`SlideMatcher.spokenPage` 匹配；命中即 `hold(source:.speech)`（覆盖手动状态） | `AppState.SlideFollow.FollowSpeechForLine`：`line.Speaker == "me"` + `InsightFollowSlides` + 会中未暂停；取 `StartedAt >= slideSince` 的本人句拼接；>=16 字符；`ObserveSpeech` 候选 +1/+2、`SlideMatcher.SpokenPage`；命中 `Hold(Speech)` | ✅ 本次补齐（原来只有按标题包含的 `TryFollowSlide`） |
| 7 | 手动翻页 `stepSlide`：同 deck 页号 ±delta；写入 manual anchor | `StepSlide(delta)` → `SlideFollowController.Step`；`SlideSource.Manual`；UI 命令仍为上一页/下一页 | ✅ |
| 8 | `prepareSlideNotes`：讲稿备注（speaker notes）逐句作为要点（isScript），无备注时模型生成；生成前查 `SlideNotesCache`（指纹：模式 + 页文本），命中直接复用 | `PrepareSlideNotes`：`SlideNotes.Script(page,next)`（有备注即逐句）否则页面前 4 行要点；`SlideNotesCacheStore.Fingerprint(mode+text)` 查缓存，未命中生成后落盘 `%LOCALAPPDATA%\NextThought\SlideNotes\<documentId>.json`；当前页/跟页切换时调用；cue 里显示为编号要点 + 「→ 下一页标题」 | ✅ 语义对齐；⚠ 无「无备注时由回答模型生成」路径（Mac 用在线模型生成），Windows 用本地要点，避免额外模型往返 |
| 9 | 讲稿要点生成中状态 `generatingNotes` / 缓存清理 | Windows 无生成中状态（本地即时生成）；缓存按页指纹失效，无显式清理按钮 | ⚠ 无 generatingNotes/清理入口 |
| 10 | 屏幕分类 `ScreenKind`（题目/代码/幻灯片/文档/聊天/视频通话/其他）与相关性（`heuristicRelevance`，>=0.5 相关） | `Core/ScreenClassification.Classify`：本地启发式映射同一组类别；`NoteScreenObservation` 记录 `ScreenWatchKind`、`ScreenWatchRelevance`（0.6/0.2，同 Mac）、`ScreenWatchObservation`（如 `slides · 0.60`）；设置在「屏幕与讲稿」卡显示一行 | ✅ 类别一致（本地规则替代 Mac 的模型分类，见未解决） |
| 11 | 屏幕来源：目标显示器/窗口/区域（`screenTargetID`/`screenRegion`），自身窗口排除，强制刷新 | `InsightScreenSource` = display/window/region；window 用前台窗口 bounds（无窗口列表选择）、region 用「框选区域」命令；`ScreenCapture.ExcludeOwnWindowsFromCapture`；`ReadScreenNowCommand` 强制读一次 | ⚠ 窗口为「前台窗口」近似，无 Mac 的目标窗口列表/显示器选择列表 |
| 12 | 演示模式跳过读屏 | `StartupOptions.Demo` 已跳过；自检手动读屏仍可用 | ✅ |
| 13 | 读屏文字只进内存、单帧交 Vision OCR | `ScreenOcrReader` 单帧 PNG 交 `Windows.Media.Ocr`；文字不落盘；界面显示「正在截屏读取屏幕…」「已读屏 · N 字」 | ✅（真实屏幕观察实测需本机运行，见未解决） |
| 14 | 设置：`跟随演示文稿`（insightFollowSlides）开关、屏幕观察开关、间隔、来源、区域、立即识别 | 高级→屏幕与讲稿卡：读取完整来源/来源下拉/框选区域/立即识别/自动跟随开关？实际：`跟随演示文稿`=`InsightFollowSlides`、`屏幕观察`=`InsightScreenWatch`、间隔、来源、区域、立即识别、本次新增观察行 | ✅ 现有 UI；`FollowSlides` 旧字段不再参与跟页（Mac 只有一个 insightFollowSlides） |

## 点按记录

`--self-test=slide-follow`（原有）×本次扩充的纯规则部分：

1. 用探针窗口截图 + Windows OCR，`SlideFollower.Observe` 匹配第 1 页（真实屏幕 OCR 路径，报告 `slide-follow.json`）。
2. `SlideFollowController`：屏幕命中 → 页 1；手动 +1 → 页 2 且 `Source=Manual`；屏幕仍是旧页 → 保持页 2；屏幕换到页 3 → 页 3、恢复 `Source=Screen`。
3. 宽限：无匹配 4 秒保留当前页、40 秒后清空；presenting 宽限 120 秒保留。
4. 跟读：<16 字符不动；有讲稿备注时，命中脚本句 → 下一页（`Source=Speech`）；无备注 deck 不跟读（同 Mac）。
5. 缓存：保存要点 → 同指纹读回；指纹不同返回空；损坏 JSON 忽略；模式不同指纹不同。
6. 分类：代码/幻灯片/题目/长文文档样本各自命中；`ScreenKind.Raw()` 与 Mac 原始值一致（video_call 等）。

## 截图

`validation/screens/presenter/` 下含屏幕与讲稿卡（高级页）与提词/双屏（提词与教练页）浅/深/英截图；真实读屏状态（正在截屏/已读屏/观察行）需本机开启「屏幕观察」并开始记录后截图，见未解决。

## 未解决

- 真实屏幕观察（读屏 + 跟页）未在本轮实机点过：需要本机图形会话 + 已导入 PPTX + 打开「屏幕观察」并开始记录；自检里只有探针窗口 OCR 跟页与纯规则测试。
- 窗口来源为「前台窗口」近似；Mac 有显示器/窗口列表选择；区域选择已有。
- 屏幕分类用本地启发式（Mac 用屏幕上下文模型分类，题目/聊天等边界样本可能不同）；未接入回答上下文（`screenForQuestion` 的 reference 判定/新鲜度未移植，现有回答仍用 AppState 问答侧屏幕状态）。
- 讲稿要点「无备注时由模型生成」未做（Windows 用页面前几行）；无 generatingNotes 状态与缓存清理按钮。
- 暂停跟随：Mac 没有独立开关，靠手动翻页暂停；Windows 同（手动翻页即手动 anchor）。Assistant 会话的「暂停跟随/新增 N 句」属 E4/E6 的 LiveView 主体，按边界未动。
- 设置页跟随开关同时控制读屏跟页与跟读（与 Mac insightFollowSlides 一致）；旧 `FollowSlides` 字段已不参与运行路径，留在设置模型里待后续清理。
