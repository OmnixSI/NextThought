# K12 音频设备切换（本机实测部分）

- 日期：2026-11-21 · 版本：0.9.0（`windows/parity`）· 机器：Windows 11 10.0.26200
- 步骤：`NextThought.exe --demo --self-test=audio-switch`（隔离数据目录）。
- 该自检启动 `AudioCaptureService`（系统音频），读取当前默认设备，切换到第二台设备，1.5 秒后再次读取 `ActiveDeviceId(AudioSource.System)`。
- 结果：**passed**。`before={0.0.0.00000000}.{b5d68a17…}` → `after={0.0.0.00000000}.{0f22f946…}`，切换期间收到 **23** 帧，`error` 为空（`audio-switch.json`）。
- 设备：本机 `Win32_SoundDevice` 共 8 个 OK 设备（USB Audio ×2、Realtek、Intel 智音、NVIDIA 虚拟/HDMI、UU 远程虚拟音频等）。
- 仍留给人（按任务书 K12 原文：「拔出和睡眠留给人」）：运行中拔出音频设备、系统睡眠/唤醒后的采音恢复；步骤见 `RELEASE.md §9 K12`。
