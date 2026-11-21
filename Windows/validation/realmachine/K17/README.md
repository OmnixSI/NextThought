# K17 PPTX 读屏跟页 · 本机实测记录（发现阻断问题）

- 日期：2026-11-21
- 系统：Windows 11（10.0.26200），主屏 1488×880，交互式桌面（Session 1）
- 方法：运行应用自检 `--demo --self-test=slide-follow`（`RunSlideFollowCheckAsync`）。该自检会打开一个
  置顶的“幻灯片”探针窗口（大字号「缓存一致性 读写时序 方案」），截取屏幕该区域、走 Windows 系统 OCR /
  页面匹配，并检查规则分类与缓存。
- 环境：隔离数据目录 `%TEMP%\nt-k17-*`，OCR 模型按首次运行模型集 junction 复用本机
  `%LOCALAPPDATA%\NextThought\models`（paddleocr-vl + pp-doclayout）。

## 结果：失败（需要修）

`report.json`：

- `ocrChars: 347`，但 `matchedPage: null`；
- `screenExcerpt` 抓到的是**本机终端/桌面文字**（“你是谁 / NextThought Windows 专用项目 / 0 / 303M tok …”），
  不是探针窗口里的「缓存一致性 读写时序 方案」；
- `rulesPassed: false`（`seen=1 manual=2 held=2 resumed=2 speech=1 grace=False/False`），
  `classifyPassed: false`；`cachePassed: true`；
- `error`: Windows 系统 OCR 的说明文字。

## 判断

1. **截屏区域/探针窗口位置问题**：自检的 crop 用的是 `window.Position` 与 `RenderScaling`，在本机
   1488×880 / 150% 缩放、Avalonia Topmost 探针窗口下，裁到的区域不是探针内容（OCR 读到终端文字），
   导致跟页匹配必然失败。这是 `RunSlideFollowCheckAsync` 的自检代码问题（`SelfTest/Checks` 属可写范围），
   与真实 PPTX 跟页功能无关。
2. 规则分类（`rulesPassed`）同时失败，需要按 Mac 的跟页规则复核。

## 结论与下一步

- K17 的 PPTX 导入/读屏跟页**尚未能给出通过证据**：先修 `RunSlideFollowCheckAsync` 的探针窗口截屏
  （用 `window.TryGetPlatformHandle()` 的窗口矩形代替 `Position`尺 或改为应用内渲染探针，不经屏幕截图），
  再补一个“导入演示 PPTX → 读屏记录 → 前台翻页”的端到端脚本（用 python-pptx 生成 fixture，翻页用
  SendInput / 键盘 PageDown，跟页断言用 `SlideFollow` 的当前页与讲稿要点变化）。
- 证据：`report.json`、`slide-follow.png`（自检截到的那一帧）、`runner.out.txt`。
- 未 push；该问题建议开给会中页/文档 owner 修，修好后本项重跑一次即可出通过证据。

> 注：本目录证据与 K10 一起落在提交 `bca906b3`（当时共享树 HEAD 已被他人推进，未重写历史）；本提交只补这一行说明。
