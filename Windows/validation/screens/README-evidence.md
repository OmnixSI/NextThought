# §5 证据重拍（冻结版本）

- 冻结点：代码冻结后最终构建，**截图来源提交 `6d65a019`**（每份 `compare.md` 开头已自动写入该 hash）。
- 构建：`Windows/src/NextThought.Desktop/bin/Release/net10.0-windows10.0.19041.0/win-x64/NextThought.exe`
  同源 Release 构建（管线内复制为私有 `NTEvidence.exe` 运行，避免共享机 kill 清扫打断长批）。
- 工具：`Windows/scripts/evidence-refresh.ps1`（删旧图/规范命名/compare 头/深色对比度）；
  新自检：`--self-test=live-states`、`--self-test=dark-contrast`。
- `live/live-notes-light.png`、`live/live-notes-dark.png` 为 Lead 生成；管线保留规则已生效，本轮从 live-shots 暂存恢复后再提交。

## 数字

| 内容 | 数量/位置 |
|---|---|
| 页面矩阵截图（8 页 × 浅/深/英 + notes 页签、settings 六页、island、onboarding 4 步、zoom 3 档等） | **132 张**，分发到 `validation/screens/<页面>/`，原样副本 `screens/current/` |
| 会中页状态矩阵（Mac live-states 20 状态 × 浅/深） | **40 张** → `screens/live-states/<状态>-light|dark.png` |
| 深色对比度 | **8/8 页 0 条 <4.5、0 个白底块**（`screens/contrast/report.json`） |
| 汇总 | `screens/evidence-refresh.json`（commit=6d65a019、batch=132、liveStates=true、contrastFailures=0） |

## 本轮修复（证据工具，不改 UI）

1. `RunDarkContrastCheckAsync`：背景刷 alpha 合成修正——场景 tint 刷常是“不透明颜色 + `Opacity=0.13`”，
   之前把它当实色 `#93AEE0`，对白字算出 1.88 的假失败；现在按 `颜色 alpha × 刷 Opacity` 逐层合成到
   主题 Paper（深色 `#1B1B19`），live「问题」胶囊实测为深色胶囊底 + Ink 文字，对比度达标。
2. `SecondaryBrush`（prepare 页）按当前主题变体取色：深色未选中胶囊文字从浅色 `#6B6963` 改为
   `#A3A19B`（`d44cf3fa`）。
3. 管线对比度失败解析改为正则取 `"failures": N`，并保留 `live-notes-*` 手工图。

## live-states 说明

40 张齐全；11 个状态为精确驱动（normal/streaming/busy/focus/focus-busy/success/unavailable/
streaming-reading-history/reading-history/large-text/empty），**9 个为近似**（save-warning/
save-recovery/stale/stale-manual-edit/selected-focus/selection/overlay/overlay-popover/
cancel-late-callback），已在 `live-states/report.json` 的 `approximatedStates` 列出，等 owner
提供状态入口后重拍。

## DEBUG 产物（已补拍）

- `controls/controls-light|dark.png`、`typography/typography-light|dark.png`：用 **Debug 构建**的
  `--demo --screenshots` 补拍。
- `CorrectedTranscript/corrected-transcript-{light,dark,en-light,en-dark}.png`：同一 Debug 批补拍。
- 方法可复现：Debug 构建 → `--demo --screenshots=<dir>` → 拷入对应目录。
