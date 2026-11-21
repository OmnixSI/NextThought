# 会中笔记（MeetingUserNotes.swift ↔ Views/MeetingUserNotes.axaml）逐元素对照

- Mac 标准：`Sources/NextThought/MeetingUserNotes.swift` 的 `MeetingUserNotes` 与 `AppState.toggleHighlight`；写入文件 `user-content.json`（A2 的 `SessionUserContent`）。
- Windows 对应物：`Views/MeetingUserNotes.axaml(.cs)`（E8 新建，同名类型）、`Views/NotesView.axaml` 的「个人笔记」标签页（现在直接承载该控件）、`AppState.MeetingNotes.cs`（单草稿、保存状态、高亮开关、合并段落、user-content 写入）、`Core/MeetingHighlightSegments.cs`（分段合并算法）、`Core/NotesTime`（时间标签）。
- 截图：本轮收尾不启动应用，浅/深图由 Lead 的 verify 批次生成后放入本目录。

## 元素对照

| Mac | Windows | 结论 |
| --- | --- | --- |
| 标题「我的笔记」+ 保存状态（正在保存… 进度 / 尚未保存 / 已保存在本机 ✓） | 同：`MeetingNotesStatusText`（`正在保存…`/`尚未保存`/`已保存在本机`，未保存用 Caution 色）+ 保存中 ProgressBar | 一致（Mac 的「已保存在本机」带勾图标，Windows 用文字；无读取失败「重试」状态，因 Windows 同步读取失败会直接落回空笔记并写日志） |
| 说明「记下自己的想法、别人的反馈和待查的问题。AI 重新整理不会覆盖这里。」 | 同句（Mac 字符串表键） | 一致 |
| `TextEditor` 编辑个人笔记，自动保存到本机 | `TextBox` 绑定唯一草稿 `MeetingNoteDraft`（`Height=180`），失焦/按钮保存；`SaveMeetingNotes` 写 `PersonalNotes` 进 user-content.json | 基本一致：Mac 每次编辑自动（防抖）保存，Windows 点「保存个人笔记」或失焦时保存（E16 的自动保存草稿机制未复用，见未解决项） |
| `重点 · N` overline + 高亮行（★ + `[mm:ss]` 原话，点击跳回原话） | 同：`MeetingHighlightCountText`（`重点 · %lld`）+ 合并段落列表（段落 = 时间范围 + 行内 ★ `[mm:ss]` 原话），点击走 `OpenMeetingHighlightCommand`→`JumpToMeetingHighlight` 跳回转写/会议页 | 一致；Mac 平铺列出全部高亮，Windows 按 30 秒间隔**合并成段落**（任务要求的分段合并，Core 可测） |
| `AppState.toggleHighlight`：同一入口添加/移除高亮 | `HighlightLineCommand` 改为开关：已存在则移除（`row.IsHighlighted=false`），否则添加；每次增删后重算段落并写 user-content.json | 一致（E8 前只加不删） |
| `highlights` 通过 `archive.updateUserContent` 读取/写入；`personalNotes` 同一 `user-content.json` | `SessionUserContentStore.Load/Save`，字段 `PersonalNotes` + `Highlights`；`LoadMeetingNotes` 在打开会议/演示时装载并刷新段落列表 | 一致（依赖 A2 的存储已存在） |
| 引文：高亮行原文 + 时间，点击定位 | 同（★ + `[mm:ss]` + 原文；`NotesTime.Text` 格式与 Mac `NotesTime.text` 一致） | 一致 |
| `onDisappear` 时持久化未保存草稿 | Windows 保存按钮/失焦保存；页面切换不强制保存（草稿仍在内存，切换回来还在） | 部分一致（见未解决项） |

## 点按/行为验证（结构测试与可测部分）

- 本轮限制不启动应用，未做 `--demo` 实机点按；可测行为由 `MeetingNotesParityTests`（7 项）+ `MeetingHighlightSegmentsTests`（6 项）锁定：视图/控件同名存在、个人笔记页承载、草稿/保存状态绑定、重点计数与引文跳转、合并算法（空、相邻合并、间隔分段、排序/空引文、可配置间隔）、高亮开关与 user-content 字段。
- 已实现行为：打开会议 → `LoadMeetingNotes` 装载笔记与高亮（并计算分段）；转写行「高亮」→ 添加并写入 user-content.json；再次点同一行 → 移除并写入；重点引文点击 → 跳回原话；保存按钮 → 写 PersonalNotes+Highlights，状态变「已保存在本机」；编辑后状态变「尚未保存」。

## 未解决/已知差异

- Mac 的编辑自动保存（debounced `editPersonalNotes`）+ 页面消失时持久化未做；Windows 目前按钮/失焦保存，草稿在内存保留。
- 读取失败「重试」行、`正在保存…` 的服务端保存队列状态（Mac `archive.savingUserContent`）未接，因为 Windows 存储是同步本地写。
- 「分段合并」按 30 秒间隔把相邻高亮合成段落（Mac 源里 `MeetingUserNotes` 是平铺列表，无现成分段实现；FIX_TASKS 的「分段合并未做」按任务要求落地为可测的合并段落展示；如后续 Mac 有专门分段规则再对齐）。
- 浅/深截图待 verify 批次。

## 自动验证

- `tests/NextThought.Tests/MeetingNotesParityTests.cs`：7 项。
- `tests/NextThought.Core.Tests/MeetingHighlightSegmentsTests.cs`：6 项。
