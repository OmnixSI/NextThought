# §5 证据重拍（本机执行记录）

- 日期：2026-11-21 17:25
- 截图来源提交：`940a0280`（管线自动写入每份 `compare.md` 开头；后续冻结点重跑会更新）
- 构建：Release 自包含 exe（`%TEMP%\nt-k18-artifacts\...\NextThought.exe`），管线内部复制为私有
  `NTEvidence.exe` 运行，避免共享机 host-wide kill 清扫打断长批。
- 工具：`Windows/scripts/evidence-refresh.ps1`（删除旧图 + 浅/深/英矩阵 + live-states + compare 头 +
  深色对比度）。新自检：`--self-test=live-states`、`--self-test=dark-contrast`。

## 产物

| 内容 | 数量/位置 |
|---|---|
| 页面矩阵截图（8 页 × 浅/深/英 + notes 页签/settings 页/island/onboarding/zoom 等） | 132 张，分发到 `validation/screens/<页面>/`，原样快照 `validation/screens/current/` |
| 会中页状态矩阵（Mac live-states 20 状态 × 浅/深） | **40 张**，`validation/screens/live-states/<状态>-light|dark.png` + `report.json` |
| 深色对比度报告 | `validation/screens/contrast/report.json` |
| 汇总 | `validation/screens/evidence-refresh.json` |

## live-states 自检

`RunLiveStatesCheckAsync` 从 demo live 舞台出发，对 Mac 的 20 个状态逐个应用 Windows 页面能力里
最接近的状态并各截浅/深一张。**近似状态**（Windows 暂无 Mac QA 注入对应入口，已在 report.json 列出，
需要 owner 提供状态入口后重拍）：`save-warning`、`save-recovery`、`stale`、`stale-manual-edit`、
`selected-focus`、`selection`、`overlay`、`overlay-popover`、`cancel-late-callback`。其余 11 个状态
为精确驱动（normal/streaming/busy/focus/focus-busy/success/unavailable/streaming-reading-history/
reading-history/large-text/empty）。

## 深色对比度（文字/背景 < 4.5，逐控件）

自动检查覆盖 8 页深色界面。**prepare 的 9 条已修复**（`Evidence:` 提交：`SecondaryBrush` 改为按当前
主题变体解析资源，深色下用 `#A3A19B` 而不是浅色 `#6B6963`；重跑 `--self-test=dark-contrast` 后 prepare
页面 0 条失败）。**剩余 2 条已交 runtime owner**：

- **live（2 条）**：两个「问题」小胶囊文字颜色与背景同为 `#93aee0`，对比度 1（不可读）。
- 其余页面（home/notes/history/series/library/settings、prepare）深色文字均 ≥4.5，无白底块。

## 已知限制

- 本批用 **Release** 构建：`Controls`/`Typography` 样张与部分旧 `CorrectedTranscript` 单图仅
  `#if DEBUG` 产出，旧图被清理后这两个目录暂时为空，需 owner/后续在冻结点用相应自检或 Debug 构建补拍。
- `--screenshots` 本批 132 张（比历史 128 张多出 quick-settings/home-stats 等新增项）；英文矩阵
  由批内 `-en-light/-en-dark` 覆盖。
- live-states 自检写完截图后应用退出较慢（Close 后仍存活到超时），管线已改为「完整 40 张即接受」。
- 等 Lead 宣布冻结点后重跑 `evidence-refresh.ps1` 即更新提交行与全部图片。
