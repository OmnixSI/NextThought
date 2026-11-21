# K2 不抢焦点 · 本机实测记录

- 日期：2026-11-21
- 系统：Windows 11（10.0.26200），主屏 1488×880，交互式桌面（Session 1）
- 被测版本：`NextThought 0.9.0+…`（Release 自包含 exe）
- 测试脚本：`Windows/scripts/realmachine/k2-focus.ps1`
- 数据目录：隔离 `%TEMP%\nt-k2-*`；跑完 `Stop-Process` 结束应用与记事本

## 方法

1. 结束残留的记事本进程，启动 `notepad.exe`，用 `SetForegroundWindow`（受前台锁时用
   `AttachThreadInput` + 点击）把它放到前台，记录前台句柄。
2. 启动应用 `--demo --self-test=capture-hide`（打开真实的提词岛窗口；该窗口 `ShowActivated=false`、
   `Topmost`、不在任务栏，属于防截屏/不抢焦点那组窗口）。
3. 等提词岛出现后，每次都先把记事本拉回前台，再用 `SendInput`（`mouse_event`）点击提词岛的
   左/中/右三个位置，比较点击前后的 `GetForegroundWindow()`：**必须一直是记事本句柄**。
4. 焦点仍在记事本时用 `SendInput` 键入 `K2FocusTest 123`，再发送 Ctrl+A / Ctrl+C，从剪贴板读回：
   文字必须出现在记事本里（说明模拟键入没有被提词岛抢走）。
5. 全程结束后再次确认前台仍是记事本、应用进程没有因为点击而变成前台。

## 结果：通过

`report.json` 记录 7 个步骤全部通过：

| 步骤 | 结果 |
|---|---|
| 记事本在前台 | ✅ foreground = 记事本句柄 |
| 提词岛出现 | ✅ 记录窗口矩形 |
| 点击提词岛 (中/左/右) ×3 | ✅ before/after 都是记事本句柄 |
| 键入进入记事本 | ✅ 剪贴板读回 `K2FocusTest 123` |
| 整个过程中应用未成为前台 | ✅ foreground 仍是记事本 |

证据：`report.json`、`island-visible.png`（点击前提词岛在记事本上方）、`after-clicks.png`（点击后）。

## 说明

- 应用的热键 `Ctrl+Alt+M`（开始记录）在本机没有让提词岛出现：演示模式没有加载语音/识别模型，
  `StartRecordingCommand.CanExecute` 为 false。因此本项用 `--self-test=capture-hide` 打开的**真实
  CueIslandWindow** 验证「点提词岛不把前台从记事本抢走」；记录/会议中的岛行为留给人按 RELEASE.md
  §9 K2 的步骤复验（结果记到本目录）。
- 点击岛上的具体按钮动作不是本项断言对象（K3 验证按钮是否可点、K4 验证关闭与隐藏）。
