# K3 鼠标穿透 · 本机实测记录

- 日期：2026-11-21
- 系统：Windows 11（10.0.26200），主屏 1488×880，交互式桌面（Session 1）
- 脚本：`Windows/scripts/realmachine/k3-click-through.ps1` + `k3-underlay.ps1`
- 被测版本：`NextThought 0.9.0+…`（Release 自包含 exe，最终构建）
- 数据目录：每次尝试独立 `%TEMP%\nt-k3-*`；结束 `Stop-Process`

## 方法

1. `k3-underlay.ps1` 起一个**整屏无边框 WinForms 窗口**（Topmost，记录每一次鼠标左键按下到
   `underlay-clicks.log`）。它是岛窗口下面那一层；岛窗口也是 Topmost 且后显示，正好在它之上。
2. 应用以 `--demo --self-test=capture-hide` 运行，打开真实的 `CueIslandWindow`（演示模式可显示岛的
   收起/展开状态，不需要语音模型或真实会议——裁决 §6）。脚本等岛窗口出现后立即操作：
   - **场景 A（收起/透明区域）**：光标先移到远处，再依次点击岛窗口四角（不在面板/按钮上）。
     `SetClickThrough(!wantsClicks)` 生效时点击穿过岛窗口，underlay 的点击计数必须 +1。
     本次命中矩形 `(658,0)-(1018,120)` 的右下透明角 `(1006,108)`，underlayClicks `0→1`。
   - **场景 B（展开/面板收到）**：光标移到岛面板上等 0.42 s（超过 0.22 s 的悬停展开时间），再点击
     面板；underlay 计数必须不变，且岛区域像素要变化（面板/按钮有动作）。
     本次岛 `(658,0)-(1048,244)`，点 `(853,26)`，underlayClicks `1→1`，岛区域 meanAbsDiff **48.44**。
3. 每个场景最多重试 8 次（岛窗口生命周期短）；两次都一次命中。

## 结果：通过

| 场景 | 现象 | 结论 |
|---|---|---|
| A 收起/透明区域 | 点击岛右下透明角 → underlay `0→1` | 穿透 ✅ |
| B 展开/面板 | 悬停后点面板 → underlay 不变 `1→1`，岛像素 diff 48.44 | 面板收到 ✅ |

证据：`report.json`（含步骤明细、underlay 进程状态）、`underlay-clicks.log`、
`scenario-a-pass-through.png`、`scenario-b-before-a1.png` / `scenario-b-after-a1.png`。

## 说明

- 「钉住」状态与场景 B 同属 `wantsClicks=true` 的窗口（`CursorOverControl` 覆盖 bar/面板/按钮），
  截图批的 `island` 各状态也由同一 `ApplyStateForCheck` 驱动；本次用真实的悬停展开路径验证。
- 岛窗口由 `capture-hide` 自检打开，但它是产品里的同一个 `CueIslandWindow` 类（
  `ShowActivated=false` / Topmost / 无任务栏 / `SetClickThrough` 逻辑），因此穿透结论适用真实会议；
  真实会议里的岛交互仍建议按 RELEASE.md §9 K3 由人补一次观感确认。
