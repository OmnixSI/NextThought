# 屏幕共享时自动收回提词岛（Windows 侧）

## 背景

Mac 侧从 macOS 15.4 起无法让本应用窗口逃出 ScreenCaptureKit；群主确认的方案是：
共享整屏时把提词岛收回刘海。Windows 侧不能依赖 macOS 的 `sharingType`，改为检测
共享窗口后自动收回，共享结束再恢复。

## Windows 实现

- `NextThought.Platform.ScreenShareDetector`：枚举可见顶层窗口，按标题和窗口类识别
  常见会议应用的共享窗口。
  - 标题标记：`screen sharing`、`sharing screen`、`screen share`、`is sharing`、
    `共享屏幕`、`共享中`、`正在共享`、`屏幕共享`。
  - 窗口类标记：`ZPShareWnd`、`TeamsShareWindow`、`WebexShare`、`ShareWnd`、
    包含 `ScreenShare` 的类。
  - 跳过标题包含 `NextThought` 的自身窗口。
- `MainWindow` 每 2 秒轮询：
  - 检测到共享且正在记录、岛可见 → 淡出并收回岛，状态栏提示；
  - 共享结束且此前是自动收回的 → 恢复岛；
  - 共享期间手动显示岛会被拒绝并提示“屏幕共享中”。
- 不使用 `sharingType`；这是 Windows 侧的最佳努力方案。

## 限制

- 基于窗口标题/类与 2 秒轮询，**不是系统级共享状态**；不同会议软件版本可能改名。
- 只在应用进程内生效；如果会议软件把共享窗口做成无标题子窗口，可能漏检。
- 无法像 Windows `WDA_EXCLUDEFROMCAPTURE` 那样保证不被捕获；本方案是 macOS 15.4
  行为的对齐，不是“隐藏窗口”的完整替代。

## 验证

- `--demo --share-hide-check` 实测：`passed=true, hidden=true, restored=true, width=660`。
- `ScreenShareDetectorTests` 8 项通过（标题/窗口类识别；含误报排除）。
