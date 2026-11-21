# K1 防截屏 · 本机实测记录

- 日期：2026-11-21
- 系统：Windows 11（10.0.26200），主屏 1488×880，交互式桌面会话（Session 1）
- 被测版本：`NextThought 0.9.0+1bb5682ef69b2a11903dfbb6f074d4d1d53c4786`（Release，自包含 exe，SHA-256 前缀 `DBC85BB37B0BADE0`）
- 测试脚本：`Windows/scripts/realmachine/k1-capture-hide.ps1`（GDI）+ `Windows/tools/realmachine/WgcCapture`（Windows.Graphics.Capture）
- 数据目录：每次运行使用独立 `%TEMP%\nt-k1-*`（`NEXTTHOUGHT_DATA_DIR`），跑完全部结束进程

## 方法

1. **OS 基线**：先用一个独立的 magenta WinForms 窗口调用 `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE=0x11)`，确认本机 GDI `BitBlt`（`CopyFromScreen`）不会把它截进画面（`osProbe.excluded=true`，magenta 像素 0/3744）。
2. **GDI**：分别在 `capturePrivacy=true/false` 的隔离数据目录启动应用：
   - `--demo` 常驻主窗口，等 `GetWindowDisplayAffinity == 0x11/0x0` 且矩形稳定后截整屏；
   - `--demo --self-test=capture-hide` 依次打开「提词岛 / 字幕条 / 回答浮窗」三个探针窗口，轮询到窗口 + 亲和性正确时立即截整屏。
   - 保存整屏 PNG 与窗口区域裁切 PNG，并记录窗口矩形、亲和性、区域亮度和方差。
3. **Windows.Graphics.Capture**：同一个开/关对照下启动 `--demo`，用 `WgcCapture.exe <png>` 通过 `Direct3D11CaptureFramePool` 抓主屏一帧并存 PNG，同样裁主窗口区域。
4. 断言：
   - 开设置时四个窗口亲和性都是 `0x11`，主窗口/提词岛/字幕条/回答浮窗在 GDI 截图里的区域与关闭设置时**明显不同**（开=后方内容，关=窗口自身内容，`meanAbsDiff` 阈值 12/255）；
   - 关设置时亲和性都是 `0x0`，窗口出现在截图里；
   - WGC 主窗口开/关区域差异同样超过阈值。

## 结果：通过

| 截屏方式 | privacy=on | privacy=off | 开/关区域 meanAbsDiff | 结论 |
|---|---|---|---|---|
| GDI（BitBlt） | 主窗口/提词岛/字幕条/回答浮窗 各 1 张，亲和性均 `0x11` | 各 1 张，亲和性均 `0x0` | 主窗口 21.2（阈值 12）；提词岛 196.9；字幕条 211.7；回答浮窗 148.6 | 通过 |
| Windows.Graphics.Capture | 主窗口 1 帧，亲和性 `0x11`，区域 stdDev 12.57（后方内容） | 主窗口 1 帧，亲和性 `0x0`，区域 stdDev 59.32（窗口内容） | 17.15（阈值 12） | 通过 |

- 主窗口 ON 区域 stdDev 29.36（后方内容），OFF 63.63（应用界面）；提词岛/字幕条/回答浮窗同理。
- 证据文件：`report.json`（全部矩形/亲和性/亮度/方差/图片路径）、`on/` 与 `off/` 下 GDI 整屏与窗口裁切图、`wgc-on/` 与 `wgc-off/` 下 WGC 整屏与主窗口裁切图。

## 复现

```powershell
# 1) 构建应用与 WGC 工具
Windows/.tools/dotnet/dotnet.exe build Windows/src/NextThought.Desktop -c Release --artifacts-path %TEMP%\nt-home-artifacts
Windows/.tools/dotnet/dotnet.exe build Windows/tools/realmachine/WgcCapture -c Release --artifacts-path %TEMP%\nt-wgc-artifacts
# 2) 运行 K1 脚本（自动完成开/关两轮并写证据）
Set-ExecutionPolicy -Scope Process Bypass -Force
Windows/scripts/realmachine/k1-capture-hide.ps1 `
  -Exe %TEMP%\nt-home-artifacts\bin\NextThought.Desktop\release_win-x64\NextThought.exe
```

## 备注

- 探针窗口由已有 `--self-test=capture-hide` 打开（该自检原本只断言 WDA 亲和性/不激活/置顶/不在任务栏等，不截图）；本项补上了真实截屏证据。
- 截图里的「下方内容」是本机桌面/其它窗口（测试环境没有第二个可见应用），断言用「开/关同区域差异 + 亲和性」而不是固定像素。
- 未覆盖：在 Zoom/Teams/腾讯会议里由对方看到的画面（K13，需要第二台设备，见 RELEASE.md「本机实测」给人步骤）。
