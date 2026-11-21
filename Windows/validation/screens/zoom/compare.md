> 截图来源：提交 `746068e8`（2026-11-21 16:50，最终构建；浅/深/英与状态截图均由该提交生成）。

# 文字缩放（J7 · Mac TextZoom / DesignSystem.swift ↔ Windows TextZoomService）

- 日期：2026-11-21（windows/parity）
- Mac 基准：v3.4.0 `f015e8c6`，`Sources/NextThought/DesignSystem.swift` 的 `TextZoom`（六档 0.9/1/1.1/1.2/1.3/1.45、`Font.zoomed`、`textZoom` 键）与菜单快捷键 ⌘+ / ⌘− / ⌘0
- Windows 证据：本目录 `zoom-<档位>-<home|live>-<light|dark>.png`（1120×820，100% 缩放；`--demo --screenshots` 生成）
- 自检：`--self-test zoom` → 12/12 通过：三档缩放按 `TextZoom.Scaled` 取整生效（13→12/16/19）、三档均无截断、重置恢复、`textZoom` 写盘后重读仍是该档、提词岛与回答浮窗不跟全局、新窗口内容即时按当前档缩放（无 600 ms 定时器）

## 对照

| 项 | Mac 值 | Windows 现状 | 结论 |
|---|---|---|---|
| 档位 | 0.9 / 1 / 1.1 / 1.2 / 1.3 / 1.45 | `TextZoom.Steps`（Core，同一组值） | 一致 |
| 快捷键 | ⌘+ / ⌘− / ⌘0 | Ctrl+OemPlus/Add、Ctrl+OemMinus/Subtract、Ctrl+D0/NumPad0（主窗口 Tunnel KeyDown） | 一致（平台键位对应） |
| 生效方式 | `Font.zoomed(size:)` = 乘系数后 `.toNearestOrAwayFromZero` | 页面每个自带字号的控件保存基准值，按 `TextZoom.Scaled` 取整后写回；继承字号的控件跟随祖先；不缩间距/圆角/窗口尺寸 | 一致 |
| 持久化 | `UserDefaults` 键 `textZoom` | 数据目录 `TextZoom.json` 中同名键 `textZoom`（Windows 侧存储，重启读取） | 一致（存储介质按平台） |
| 提词岛 / 回答浮窗 | 保留自己的大小按钮，不跟全局 | `TextZoomService.IsExcluded`：`CueIslandWindow`、`TeleprompterWindow` 跳过 | 一致 |
| 三档无截断 | 是 | 自检 0.9/1.2/1.45 各 0 条截断（`LocalizationAudit.CollectTruncations`）；固定高度按钮（首页场景胶囊等）在缩放时按文字需要增高，回到 100% 恢复 Mac 32 pt 命中高度 | 一致 |
| 重启保留 | 是 | 自检写入 1.2 → `Reload()` 仍为 1.2（真实启动时 `Configure` 读同一文件） | 一致 |
| 双屏显示器稳定 1.5 s | `PresenterMode.displaysChanged` 等 1.5 s 再处理 | `Screens.Changed` → `QueuePresenterDisplayChange()` 1.5 s 防抖后再刷新/进入/退出双屏演讲 | 一致 |
| 叫回主窗口后本场不再自动隐藏 | `pageWanted`（本次会议有效，新会议清零） | `presenterPageWanted`：`RestoreMainWindow()` 时置位；`ApplyPresenterModeForRecording` 不再 `Hide()`；会议开始/结束清零 | 一致 |

## 未解决项

1. ~~`textZoom` 尚未并入注册表~~：E25 已完成——`textZoom` 在 114 键 `MacPreferences`/`MacPreferenceMap` 中（默认 1.0，SettingsStore 以 settings.json 同名键读写），`SETTINGS_PARITY.md` 行与 `RecordSessionsAndTextZoomPreferenceTests` 覆盖；旧 `TextZoom.json` 仅作迁移兜底。
2. ~~新出现的内容由 600 ms 定时器补缩放~~：task-81 的 zoom 2 已完成——`TextZoomService` 改为窗口列表事件 + 每个窗口布局事件驱动的合并应用（无定时器），新内容在挂到可视树后的同一帧内按当前缩放显示；自检新增 `new-content-scales-immediately`（新窗口 15pt → 1.45 档 22pt）并通过；结构性测试断言源码不再含 `DispatcherTimer`/`Watcher`/600 ms。
3. 已接受差别 zoom 3（缩放步进外的个别内容近似，见 `ACCEPTED_DIFFERENCES.md`）。

## 结论

逐条一致；另有 1 条已裁决的差别（zoom 3，已在 `ACCEPTED_DIFFERENCES.md` 记录）。
