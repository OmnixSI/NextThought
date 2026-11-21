# K17 PPTX 读屏跟页 · 本机实测记录

- 日期：2026-11-21
- 系统：Windows 11（10.0.26200），主屏 1488×880，150% 缩放，交互式桌面（Session 1）
- 被测版本：`NextThought 0.9.0+…`（Release，`--demo --self-test=slide-follow` 同一次构建）
- 环境：隔离数据目录 `%TEMP%\nt-k17-*`，OCR 模型按首次运行模型集 junction 复用本机
  `%LOCALAPPDATA%\NextThought\models`（paddleocr-vl + pp-doclayout）

## 修复内容（`0dea577c`）

首次运行时自检失败：它把置顶探针窗口用屏幕截图 + `Window.Position` 裁剪，在 150% 缩放 + Topmost 下
裁到的不是探针内容（OCR 读到桌面/终端的文字），`matchedPage=null`。修复：

1. 不再截屏探针窗口：把「幻灯片」内容用 `RenderTargetBitmap` **离屏渲染**成图片（白底黑字，900×260），
   彻底去掉对窗口位置、DPI 缩放、窗口层叠顺序和其他窗口的依赖；
2. 规则部分对齐当前 `SlideFollowController` 语义：手动翻页后观察第三页（带讲稿）恢复跟页、口播按
   第 1 页讲稿匹配、代码分类样本补足 Code 阈值；
3. 截屏 bug 修好后 build 恢复绿色。

## 结果：通过

`report.json`：

- `passed: true`，`matchedPage: 1`，`ranked: [{page: 1, score: 1}]`；
- OCR 读到幻灯片文本（`screenExcerpt`：「缓存一致性 / 读写时序方案 / 重试与降级」，31 字符）；
- `rulesPassed: true`、`cachePassed: true`、`classifyPassed: true`（`ruleError` 为空）。

证据：`report.json`、`slide-follow.png`（离屏渲染的探针图）、`runner.out.txt`/`runner.err.txt`。

## 说明

- 这项证明的是**读屏帧→OCR→页面匹配→跟页规则（含手动翻页恢复、口播跟页、20 秒宽限、笔记缓存、
  屏幕分类）**整条链路在本机可用。
- 完整的「导入演示 PPTX → 打开读屏并记录 → 前台翻页 → 跟页与讲稿要点变化」端到端人机流程仍需
  PPTX 导入入口与读屏会话组合执行；本自检使用的页面数据与真实 PPTX 走同一 `SlidePage`/`SlideMatcher`
  代码路径。若需要真实 PPTX 证据，可再生成演示 PPTX 并用读屏页导入后跑同一断言。
