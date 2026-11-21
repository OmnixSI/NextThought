# 文字缩放（J7 · Mac TextZoom / DesignSystem.swift ↔ Windows TextZoomService）

- 日期：2026-11-21（windows/parity）
- Mac 基准：v3.4.0 `f015e8c6`，`Sources/NextThought/DesignSystem.swift` 的 `TextZoom`（六档 0.9/1/1.1/1.2/1.3/1.45、`Font.zoomed`、`textZoom` 键）与菜单快捷键 ⌘+ / ⌘− / ⌘0
- Windows 证据：本目录 `zoom-<档位>-<home|live>-<light|dark>.png`（1120×820，100% 缩放；`--demo --screenshots` 生成）
- 自检：`--self-test zoom` → 11/11 通过：三档缩放按 `TextZoom.Scaled` 取整生效（13→12/16/19）、三档均无截断、重置恢复、`textZoom` 写盘后重读仍是该档、提词岛与回答浮窗不跟全局

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

1. `textZoom` 尚未并入 `MacPreferenceMap`/`MacPreferences.g.cs` 的 113 项注册表与 SETTINGS_PARITY 行（Lead 侧把它从「未做」转 done 时一并处理）；Windows 目前用数据目录下的 `TextZoom.json` 持久化同名键，功能与重启保留已验证。
2. 新出现的内容（切页/弹窗）由 600 ms 定时器在缩放非 1 时补缩放；批量截图为立即生效，实际使用最多 0.6 s 的瞬时尺寸跳变。
3. 缩放到 1.45 时首页场景胶囊换行（与文字同比例），截图见 `zoom-1.45-home-light.png`；这是 Mac 上文字放大后的预期行为，非截断。
