> 截图来源：提交 `940a0280`（2026-11-21 17:24，最终构建；浅/深/英与状态截图均由该提交生成）。



# E12 说话人与我的声音：Mac ↔ Windows 对照

对照代码：`Sources/NextThought/SpeakerViews.swift`（318 行全文：SpeakerLayout/SpeakerMark/SpeakerTag/SpeakerStrip/SpeakerEditor/MeEditor/SpeakerSettingsSection/MyVoiceSettings）、`Sources/NextThought/MyVoiceViews.swift`（113 行全文：MyVoiceSetupPanel/OnboardingVoicePage）、`Sources/NextThought/MyVoice.swift`（prompts/beginSetup/recordTake/cancelSetup/删除）、`Sources/NextThought/SpeakerID.swift` 的 setVoiceName/renameVoice/mergeVoices/setMeVoice、`Sources/NextThoughtCore/SpeakerClustering.swift` 的 SpeakerLabels
对照基准：Mac v3.3.0（`5ddb9e5`）；Mac 无对应截图，按源码逐元素对照
Windows 代码：`AppState.Speakers.cs`（重写）、`SpeakerRow.cs`、`Views/SpeakerEditorWindow.axaml(.cs)`（重写）、`AppState.MyVoice.cs`、`Views/MyVoiceWindow.axaml(.cs)`（重写）、`Views/NotesView.axaml(.cs)`（转写标签加说话人条）、`Platform/SessionStore.cs`（新增 SetVoiceLabelAsync/MergeVoicesAsync）、`NextThought.Core/SpeakerClustering.cs` 既有 `SpeakerLabels`
Windows 截图：`Windows/validation/screens/speakers/`（自检 `speakers` 生成；本轮共享树被其他队友在途改动阻塞，截图改为 verify 阶段补跑）

## 1. 说话人条与标签（SpeakerLayout / SpeakerStrip / SpeakerTag）

| Mac | Windows | 结论 |
|---|---|---|
| `SpeakerLayout`：voiceNumbers/lineNumbers/names/starts/people/mine，0 号是用户（不编号），未匹配行显示对方/现场 | Core 既有 `SpeakerLabels`（VoiceNumbers/Numbers/Names/MeLines/RunStarts/PromptLabels/PromptNote），Desktop 用当前会议 meta 的 voiceNames/meVoices 计算 | 一致（复用已有 Core） |
| SpeakerMark：用户=person 图标；其他=circle/square/triangle/diamond，未匹配=空心圆 | `SpeakerPersonRow.Mark`：●/■/▲/◆，用户用 ● + 「我」；无空心圆（未匹配行不是 person 条） | 基本一致（纯文字形状，无 SF Symbol 图标） |
| 每人一个胶囊（编号+名字/我）+「按声音区分」小字，点击弹编辑器 | `Views/NotesView.axaml` 转写标签顶部新增 `TranscriptSpeakerStrip`（WrapPanel 胶囊：Mark+DisplayLabel，Tooltip）+「按声音区分」；点击打开 `SpeakerEditorWindow` 并选中该人 | 一致（出现位置：Notes 转写标签；实时页主体由 d-parity E6 在改，未动） |
| SpeakerTag：只在说话人变化处显示名字/我/对方 | 转写行标签：`TranscriptRow.ApplySpeakerLabel` 把 `说话人 N` 换成名字或「我」（保留时间/系统声音/回声标签）；无“只在变化处显示”的折叠（每行都带标签，与 E15 现状一致） | 部分（标签内容一致；未做 run 起始折叠） |
| 无 Mac：会议进行中实时条 | LiveView 说话人入口仍为按钮（主体归 d-parity E6） | 部分 |

## 2. 说话人编辑器（SpeakerEditor / MeEditor）

| Mac | Windows | 结论 |
|---|---|---|
| 选一个人：标题（Mark+名字+句数）、名字输入框、这是我、并入其他人的 chips、取消/完成 | `SpeakerEditorWindow` 重写：顶部胶囊条选人；编辑区 Mark+名字+句数；名字输入框；「这是我」勾选；「其实是同一个人」+ 每个其他人一个「并入 X」chip；取消/完成 | 一致（Windows 是独立窗口，Mac 是 popover） |
| MeEditor：这个声音被当作你自己…不是我 / 好 | 选中被标成「我」的人时显示 MeEditor 卡片（同文案 + 句数）+「不是我」/「好」 | 一致 |
| rename/merge 写入会话 meta（voiceNames/meVoices，Mac session meta） | E12 新增 `SessionStore.SetVoiceLabelAsync`/`MergeVoicesAsync` 写 `meta.json` 的 `voiceNames`/`meVoices`；兼容旧 `speakers.json`（只读回退 + 批量应用时仍写一份） | 一致（Mac 兼容；旧数据可迁移） |
| merge：所有声纹标签移到目标（新标签生效）、名字与 me 标记跟随、transcript.jsonl 原文不动 | `MergeVoicesAsync` 向 `voices.jsonl` 追加目标 voice 标签（后写覆盖）；`SpeakerLabels.Merged` 搬名字；meVoices 同步移动；transcript.jsonl 字节不变（单测断言） | 一致 |
| 保存回执失败提示 | Windows 直接 await 写入，失败写入 `SpeakerEditorStatus` | 部分（无 Mac 的异步持久化回执链） |

## 3. 我的声音设置（MyVoiceSettings / MyVoice.swift）

| Mac | Windows | 结论 |
|---|---|---|
| 已录入 · 日期；重新录入；删除（confirmationDialog 二次确认） | `MyVoiceWindow` 重写：`MyVoiceSummaryLine`（已录入 · 日期 / 还没有录入）+ 重新录入 + 删除（`ConfirmDialog` 二次确认，文案同 Mac） | 一致 |
| 还没有录入 → 「录入我的声音」（会议进行中/无模型禁用） | 同按钮 + `MyVoiceCanEnroll`（会议中、无模型禁用）；会议中提示「会议进行中不能录入。」 | 一致 |
| 旧模型声纹提示「这份声音来自旧版本的模型，需要重新录入。」；无模型提示 | 同（`MyVoiceOldModel`/`MyVoiceModelMissing`） | 一致 |
| 说明：读 4 句话约 40 秒、只存一串数字、不上传不进备份可删除、录入后发言标「我」 | 同文案 | 一致 |
| 录入后自动打开“区分说话人”设置（onChange isSaved） | `FinishMyVoiceSetup` → `SaveSpeakerSettingsEnabled()`（Settings.SeparateSpeakers=true） | 一致 |

## 4. 录入面板（MyVoiceSetupPanel）

| Mac | Windows | 结论 |
|---|---|---|
| 「第 N / 4 句，点“开始”后用平时开会的声音读出来」 | `MyVoiceTakeIndex`/`MyVoiceTakeHint` 同文案 | 一致 |
| 当前句子卡片、录音进度条 | 同（卡片 + ProgressBar，录音时显示） | 一致 |
| 取消 / 开始按钮；录音中「正在听…」、处理中「处理中…」且禁用 | `MyVoiceRecordLabel`（正在听…/处理中…/开始）+ `MyVoiceCanStart` 禁用；`MyVoiceIsWorking` 覆盖声纹计算期间 | 一致 |
| 四句 prompts（两中一英一中，Mac MyVoice.prompts 原句） | `VoicePrompts` 同四句；全程 7 秒/段、静音裁剪、最短 3 秒（Mac 参数不同，见未解决） | 一致 |
| 取消要真正停止录音/清空 | `CancelMyVoiceSetupCommand` 清空 takes/消息/进度 | 一致 |
| 保存失败/声纹不一致/太短提示 | 同三类提示（录到的不够 / 不像同一个人 / 没听清） | 一致 |

## 5. 自检点按记录（`speakers`，隔离数据目录）

自检 `RunSpeakersCheckAsync` 已加入：种子会议 3 个声音（陈老师/我的声音/另一人，各 5–6 句）→ 说话人条人数与名字 → 命名写 meta.json → 转写标签显示名字 → 合并（声纹标记跟随、名字保留）→ 说话人窗口/我的声音窗口截图 → 录入面板可打开（不实际录音）。

**本轮运行状态（如实记录）**：首轮运行（exit 0）发现场景只有两个声音（其中一个是「我」），按 Mac 规则单对单不编号，条上只剩「我」，断言写错；已修正为 3 人场景（2 个非我声音才编号）。修正后**未能重跑**：共享树被其他队友正在编辑的 `AppState.Prepare.cs`/`AppState.ScreenWatch.cs` 挡住编译（报错均为其 WIP，不是 E12 文件）。E12 代码曾于 08:49 私有隔离构建 `-p:OutputPath` **0 错误**通过。自检与截图列为 verify 阶段补跑，状态「已实现、未验证」。

## 6. 未解决 / 待补

1. 真实麦克风录音未在自动化中执行（需要麦克风/声纹组件）；录音参数（Mac 每段时长/最小时长）与 Mac 的 MicrophoneCaptureService 未逐参数对齐。Mac 每次录音约 10 秒、Windows `TakeSeconds=7`，未实测比较。
2. SpeakerTag 未做「只在说话人变化处显示名字」的 run 起始折叠（Windows 每行都有标签，与现有 E15 转写呈现一致）。
3. 实时页（LiveView）说话人条主体由 d-parity E6 在改，本轮只在 Notes 转写标签加条；实时页仍为「说话人」按钮入口（Mac 实时转写上方同款条）。
4. 说话人保存为同步写 meta.json，无 Mac 的异步保存回执/失败重试提示链；旧 `speakers.json` 仅作只读回退与批量应用时的兼容副本。
5. 未做逐显示器/颜色之外的更多 Mark 视觉（Mac 用 SF Symbols 图标；Windows 用几何字符）。
6. 无 Mac 对应截图；`speakers` 自检与截图待树恢复后补跑。

## 7. task-62 阶段 4 §6.3 复核（2026-11-21）

- 对照：`SpeakerViews.swift`（SpeakerLayout/SpeakerMark/SpeakerStrip/SpeakerEditor/MeEditor/SpeakerSettingsSection/MyVoiceSettings）、`MyVoiceViews.swift`（MyVoiceSetupPanel/OnboardingVoicePage）、`MyVoice.swift`；Mac 无对应截图，按源码逐元素对照。
- Windows：`Views/SpeakerEditorWindow.axaml(.cs)`、`Views/MyVoiceWindow.axaml(.cs)`、`Views/NotesView.axaml` 转写标签说话人条、`AppState.Speakers.cs`、`AppState.MyVoice.cs`。
- 截图：`Windows/validation/screens/current/` 暂无 speakers 产物；自检 `--self-test=speakers` 生成 `speaker-strip-light.png` / `my-voice-light.png` 到 `%TEMP%/nextthought-selftest/speakers/`，本轮共享树多次被并行构建打断（“No precompiled XAML”），待构建稳定后拷入本目录并补浅/深两版。
- 本轮修复（E12）：Mac 在没有安装声纹组件时也打开四句录入面板（只是禁用“开始”按钮并提示先准备组件）；Windows 之前直接 return 不显示面板，导致 `speakers` 自检的「我的声音：录入流程可打开」失败。已改为始终打开面板，`MyVoiceCanStart` 仍要求 `MyVoiceModelAvailable`。
- 待复核/未解决：自检在共享树稳定后需重跑确认 `speakers-check: passed`；说话人条在实时页的呈现属 live-parity；Mac popover vs Windows 独立窗口为平台差异；shallow/deep 截图待补。

## task-62 / E12 闭环：说话人编辑器 / MeEditor / 我的声音窗口（2026-11-21）

- 自检：`--self-test=speakers` **passed**（`speakers-check.json` 随本目录入库，6 步全过）。
- 截图（本目录，1120 宽的窗口位图，浅/深各一组）：
  - `speaker-strip-light.png` / `speaker-strip-dark.png`（SpeakerEditorWindow：顶部人名单胶囊 + 选中人编辑区 + 命名/这是我/合并选择）
  - `my-voice-light.png` / `my-voice-dark.png`（MyVoiceWindow：我的声音设置 + 四句录入面板）
- 本轮修复：
  1. **E12 主修复**（`0e121444`）：声纹组件未安装时 Mac 也打开四句录入面板（只禁用“开始”并提示先准备组件）；Windows 之前直接 return，现已始终打开，`MyVoiceCanStart` 仍要求组件可用。
  2. **自检前置**：`--demo` 首页演示会议处于录音态（Mac 规则：会议进行中不能录入），自检在「我的声音」步前先 `StopRecordingCommand` 并等 `Recording=false`，之后面板 `visible=True`、`takes=4`。
  3. **浅/深两张窗口截图**：自检对 SpeakerEditorWindow 与 MyVoiceWindow 分别切 Light/Dark 后各截一张（`Application.RequestedThemeVariant`）。
- 逐元素结论（对照 `SpeakerViews.swift` / `MyVoiceViews.swift` / `MyVoice.swift`，Mac 无对应截图，按源码）：
  | # | Mac | Windows | 结论 |
  |---|---|---|---|
  | 1 | SpeakerEditor：一人一胶囊（Mark+名字/句数），点选编辑 | SpeakerEditorWindow 顶部 `SpeakerPeople` 胶囊条，点选后 `SelectedSpeaker` 更新 | 逐条一致 |
  | 2 | 名字输入 +「这是我」勾选 + 句数 | 名字 TextBox + 这是我 CheckBox + 句数 caption；写入 meta.json voiceNames/meVoices | 逐条一致 |
  | 3 | 「其实是同一个人」合并 chips + 取消/完成 | `SpeakerMergeChoices` chips（每个其他人一个，合并即 MergeVoices）+ 取消/完成 | 逐条一致 |
  | 4 | MeEditor：这个声音被当作你自己…不是 / 好 | 选中被标成我的人时显示 MeEditor 卡片（同文案 + 不是/好） | 逐条一致 |
  | 5 | MyVoiceSettings：已录入/还没有录入、开始录入/重新录入、删除 | MyVoiceWindow 同状态（`MyVoiceSummaryLine`、开始/重新录入、删除、模型缺失提示） | 逐条一致 |
  | 6 | MyVoiceSetupPanel：四句、第 N/4 句提示、开始/处理中/正在听、进度、取消 | 同名同结构（`VoicePrompts` 4 句、`MyVoiceTakeHint`、`MyVoiceRecordLabel`、进度条、取消）；本轮验证无组件时也打开 | 逐条一致 |
  | 7 | 说话人条在转写页顶部，点胶囊开编辑器 | `NotesView` 转写标签 `TranscriptSpeakerStrip`（已存在）+ 编辑器打开 | 逐条一致（E12 既有实现） |
- 未解决/记录：
  - 真实麦克风录入（四句话音、声纹组件下载）未在自检执行，标注为“需真机麦克风”；`speakers-check.json` 最后一步如实记录；
  - Mac 是 popover，Windows 是独立窗口（平台差异，交互等价）；
  - 引导里的我的声音页（OnboardingVoicePage）属 E3 owner，不在 E12 范围。

> 对照基准：Mac 3.4.0（`f015e8c6`）；Mac 无对应截图时按 Swift 源码逐元素（见上表）。

## 8. 决议 §3.3 speakers 2/3/4/5 收尾（2026-11-21，当前 HEAD 构建）

- **2 说话人标签只在变化处显示**：`TranscriptRow.RefreshSpeakerLabels` 计算 `ShowSpeakerLabel`（首行或与上一行 speaker 不同才 true），实时页与 Notes 转写模板都按它隐藏名字（`LiveView.axaml` 的 `SpeakerName`、`NotesView.axaml` 的 `Label`）；其余标签（时间/回声/系统声）不受影响。
- **3 实时页说话人条**：`LiveView.axaml` 的 `LiveSpeakerStrip`（`SpeakerPeople` 胶囊 + `SpeakerChip_Click` 打开编辑器 + `LiveWhoAmIButton`「我是哪个？」虚线胶囊 + `LiveSpeakerStripHint`）与 Mac SpeakerStrip 同结构；`--self-test=speakers` passed。
- **4 保存失败提示与重试**：`SpeakerEditorWindow` 在 `HasSpeakerSaveError` 时显示橙色状态行 +「重试」按钮（`RetrySpeakerSaveCommand`），由布尔失败标记驱动，不再匹配中文状态文本（`562fefe1`）。
- **5 标记用图标资源**：`SpeakerPersonRow.MarkGlyph` 使用随包 Fluent System Icons 字形（`IconPersonFill`/`IconCircle`/`IconSquare`/`IconTriangle`/`IconDiamond`，即 person/circle/square/triangle/diamond 的 24px regular 字形），实时页胶囊、Notes 说话人条与说话人编辑器统一 `FontFamily="{StaticResource IconFont}"`，不再用 ●■▲◆ 几何字符。
- **截图/自检**：`--self-test=speakers` 在隔离数据目录 **passed**，本目录 `speaker-strip-{light,dark}.png`、`my-voice-{light,dark}.png`、`speakers-check.json` 已重跑更新。
- 仍留：真实麦克风四句录入（需真机）与 Mac popover→独立窗口（平台差异）两条件同 §6/§7。

## 9. E12 speakers 3：实时页说话人条按 Mac（本轮 HEAD 复核）

| # | Mac SpeakerStrip（`SpeakerViews.swift`） | Windows `LiveView.axaml` `LiveSpeakerStrip` | 结论 |
|---|---|---|---|
| 1 | 只显示已认出的声音；每人一个 sunken 胶囊（SpeakerMark + 名字，24pt） | `SpeakerPeople` 胶囊 + `MarkGlyph` + `DisplayLabel`，24pt | 逐条一致 |
| 2 | 点胶囊打开 SpeakerEditor / MeEditor | `SpeakerChip_Click` 打开 `SpeakerEditorWindow`（MeEditor 分支同在窗口内） | 逐条一致（popover→独立窗口为平台差异） |
| 3 | 没认出「我」时虚线「我是哪个？」胶囊，选择后标成我 | `LiveWhoAmIButton` + `controls:DashedCapsule` + 「我是哪个？」+ `WhoAmI_Click` | 逐条一致 |
| 4 | 右侧提示「点名字改名、合并或标成我」 | `LiveSpeakerStripHint` 同文案 | 逐条一致 |
| 5 | `layout.isShown` 控制显示 | `LiveSpeakerStripVisible`（`SpeakerPeople.Count > 0`） | 逐条一致 |

`--self-test=speakers` 在隔离数据目录 passed（含说话人条人数/命名/合并/标签断言）；截图 `speaker-strip-{light,dark}.png` 由 §5 最终构建重拍。结论：**逐条一致；另有 1 条已裁决差别（Mac popover vs Windows 独立窗口）**。
