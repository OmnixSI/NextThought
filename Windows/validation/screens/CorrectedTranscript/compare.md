# 校正原话（CorrectedTranscript）逐元素对照

## 出处

- Mac 参照：`Sources/NextThought/NotesPageView.swift:1212-1327` 的 `CorrectedTranscript`；在 Notes 页的「转写」标签内呈现（`NotesPageView.swift:96-98`）。查找结果里的类型文案「校正原话」见 `Sources/NextThought/HistoryView.swift:1069`。
- Windows 对应：`Windows/src/NextThought.Desktop/Views/CorrectedTranscriptView.axaml` + `.axaml.cs`（类型名 `CorrectedTranscript`），入口为 `Views/NotesView.axaml`「转写」标签内的「校正原话」按钮；数据与动作在 `AppState.Proofread.cs`（`RefreshCorrectedTranscript` / `ToggleCorrectedTranscriptFix` / `OpenCorrectedTranscript`）。
- 截图：`light.png`、`dark.png`（`--demo --screenshots` 生成，窗口 880×640；演示模式使用 Mac 演示会议的数据 + 2 条演示用校对决定，仅内存）。

## 逐元素对照

| Mac 元素 | Mac 出处 | Windows 实现 | 结论 |
|---|---|---|---|
| Notes →「转写」标签即校正原话界面 | NotesPageView.swift:81-98 | 「转写」标签内按钮「校正原话」，在同一标签中打开窗口（单屏适配） | 一致（承载形式差异见下） |
| 转写版本切换：校正转写 / 原始转写 | NotesPageView.swift:1241-1249 | 两道 chip 单选，绑定 `CorrectedTranscriptShowCorrected/ShowOriginal` | 一致 |
| 查找框「在转写里查找（听错的写法也会找出来）」 | GlossaryView.swift:424-444（TranscriptFindField） | 同一句文案的 TextBox + 「清空搜索」按钮 | 一致 |
| 查找匹配数「N 句」 | NotesPageView.swift:1248 | `UiText.Format("%lld 句", matches)` | 一致 |
| 每行：时间 / 说话人 / 正文（校正版应用更正） | NotesPageView.swift:1250-1284 | 同结构三列；正文按校正/原始版本切换 | 一致 |
| 校正处虚线下划线 + 悬停提示「原来识别为：%@（右键可以撤销）」 | NotesPageView.swift:1264-1267、1322-1326 | 正文为校正后文本；每条更正单独一行动作按钮（Mac 右键菜单的等价入口） | 部分：操作入口由右键改为可见按钮 |
| 右键菜单「撤销校对：corrected → original」/「恢复校对：original → corrected」 | NotesPageView.swift:1286-1293 | 每行 `撤销校对：%@ → %@` / `恢复校对：%@ → %@` 按钮，`ToggleCorrectedTranscriptFix` 切换 user-content.json 里的决定 | 一致（交互改按钮） |
| 说明「已校对 %lld 处识别错误」 | 校对状态文案（Localizable.xcstrings） | 窗口副标题，统计未撤销的更正数 | 一致 |
| 空态「这场会议没有转写。」 | NotesPageView.swift:1239 | 同句空态 | 一致 |
| 转写上下文菜单「标为重点」「加入术语表…」 | NotesPageView.swift:1295-1299 | 未做 | 缺口：属 E8（高亮）/E22（术语表）范围 |
| 说话人条 SpeakerStrip、误听标记 MisheardMark、译文与「待更新」 | NotesPageView.swift:1231-1236、1268-1279 | 未做（只显示说话人角色「我/对方/现场」） | 缺口：属 E12（说话人）/E22（术语表）范围 |

## 与 Mac 的承载形式差异

Mac 的 `CorrectedTranscript` 是 Notes 页「转写」标签里的内嵌 pane；Windows 版同一标签里先显示转写列表，并用「校正原话」按钮打开独立窗口显示同一视图（单屏/窗口适配）。文案、版本切换、查找、逐条撤销与空态与 Mac 相同。

## 数据行为

- 原始 `transcript.jsonl` 不被改写；撤销/恢复只改 `user-content.json` 的 `CorrectionDecisions.Reverted`（与 Mac 的非破坏性行为一致）。
- 窗口打开时从原始转写 + 用户决定重建投影；切换版本/查找只影响显示。
- 演示模式（`--demo`）没有可写的会议目录，窗口用 Mac 演示会议的内存数据 + 2 条演示用决定（`DemoCorrectedTranscriptCorrections`，仅内存、不落盘），用于截图。
