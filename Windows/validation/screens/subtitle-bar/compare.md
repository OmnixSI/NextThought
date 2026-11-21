
> 结论：逐条一致；另有 2 条已裁决的差别（逐条见 ACCEPTED_DIFFERENCES.md / KNOWN_GAPS.md 与下文「差异 / 未解决」）。
> 截图来源：提交 `6d65a019`（2026-11-21 18:14，最终构建；浅/深/英与状态截图均由该提交生成）。







# 字幕条（E14 · §6.7）Mac 3.4.0 源码 ↔ Windows SubtitleBarWindow 逐元素对照

- Mac 基准：Mac 3.4.0 `f015e8c6`，`Sources/NextThought/SubtitleBarView.swift`、`SubtitleBarController.swift`
- Windows 代码：`Windows/src/NextThought.Desktop/Views/SubtitleBarWindow.axaml(.cs)`
- Windows 截图（本目录）：`subtitle-bar-light.png` / `subtitle-bar-dark.png`（含译文行与「对方可见」徽标）、`subtitle-bar-autoend-light.png`
- 证据生成：`--self-test windows`（`RunWindowsCheckAsync`，隔离数据目录）

| # | Mac 元素（顺序） | 值 | Windows 现状 | 结论 |
|---|---|---|---|---|
| 1 | 卡片 | `floatingSurface(cornerRadius: Space.radiusLarge=22)`：raised 表面 + hairline 0.5 | `Card` raised + Hairline 0.5、圆角 22，窗口留 16/20/28 阴影内边距 | 一致 |
| 2 | 卡片宽度 | `min(900, 可见宽−160)`，内边距 24 | 同（`MaxCardWidth=900`、`CardPadding=24`、宽−160） | 一致 |
| 3 | 主句 | `Font.zoomed(size: liveWindow.subtitleFontSize, weight:.medium)`，2 行、居中 | 同（`SubtitleFontSize` × 文字缩放，medium、2 行居中） | 一致 |
| 4 | 说话人 | 仅当不是 remote 时显示 `speaker.displayName`，`max(12, size×0.5)` medium 次要色 | 同（对方不显示；现场/我显示） | 一致 |
| 5 | 译文 | `max(12, size×0.6)` 次要色、1 行、居中 | 同（`SubtitleShowTranslation` 控制显隐） | 一致 |
| 6 | 自动结束提醒 | moon.zzz + 「好像没人说话了，N 秒后结束记录」等宽 medium caution + 继续记录 PrimaryButton | 同（`CueIslandAutoEndDue/Seconds`，按钮调用 KeepRecording） | 一致 |
| 7 | 「对方可见」徽标 | 未开 capturePrivacy 时，右上角 offset(−12,−9)：6 pt caution 点 + caption，20 pt 胶囊 + hairline | 同（`SharedBadge`） | 一致 |
| 8 | 悬停 | 指针在条上 → 不透明度 0.15、点击穿透 | 同（`TrackPointer`，`IsDimmed`） | 一致 |
| 9 | 拖动 | 停留 0.6 s → 实心 + 1.5 pt 次要色描边，可拖动，位置记忆 | 同（`SetEditing`、`BeginMoveDrag`、`Settings.SubtitleBarX/Y`） | 一致 |
| 10 | 显隐 | 说话出现；最后一句后 `SubtitleBarTiming` 淡出 | 同（Core `SubtitleBarTiming`/`SubtitleFeed`/`SubtitleText`） | 一致 |
| 11 | 不抢焦点 | 非激活 panel，永不 key | `WS_EX_NOACTIVATE` + `ShowActivated=false` | 一致 |
| 12 | 隐私 | capturePrivacy 时从共享画面排除 | `WindowCaptureHiding` | 一致 |

## 差异 / 未解决

1. 截图时窗口为透明背景捕获（无 Mac 桌面底色），元素本身一致。
2. 自动结束提醒的倒计时用 1 s 轮询刷新（Mac TimelineView 每秒），显示值一致。
