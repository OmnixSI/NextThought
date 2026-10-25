# InterviewHacker 重新设计（2026-10）

这一组文档是 InterviewHacker 新版界面和交互的完整设计，包括视觉语言、使用流程、中英文和实现计划。**代码还没有按它修改**，改代码时以这里为准。

## 先读哪个

| 文档 | 内容 |
|---|---|
| [01-visual-language.md](01-visual-language.md) | 视觉语言“纸与墨”：颜色、字体、间距、圆角、动效、文案、图标 |
| [02-scenarios-and-flows.md](02-scenarios-and-flows.md) | **核心**：真实使用场景，会前 / 会中 / 会后的流程，系列，会议类型，声音来源，会后纪要，会中每个功能放在哪 |
| [03-language.md](03-language.md) | 中英文切换（整个 App）、英文文案规范、术语表、实现方案 |
| [04-implementation-plan.md](04-implementation-plan.md) | **改代码按这份做**：逐项对照现有文件 |
| [05-decisions.md](05-decisions.md) | 做过的决定、放弃的方案和原因 |

## 设计变量和组件

- `design-system/tokens.json`：所有设计变量（颜色分浅色和深色、字体、间距、尺寸、圆角、阴影）。`tokens.css` 由它生成。
- `design-system/components.css`：组件和页面示意的样式。
- `design-system/components/<名字>/README.md`：每个组件和页面的使用说明。

## 看页面长什么样

- **截图**：`screenshots/`，每个页面有中文浅色、中文深色、英文浅色三张。
- **可交互的预览**：用浏览器打开 `previews/<名字>.html`。页面右上角可以切换 中文 / EN；系统是深色模式时自动显示深色。

| 页面 | 预览 | 截图 |
|---|---|---|
| 首页 | `previews/HomePage.html` | `screenshots/HomePage-zh-light.png` |
| 准备页 | `previews/PreparePage.html` | `screenshots/PreparePage-zh-light.png` |
| 会中 · 提词 | `previews/LiveSessionPage.html` | `screenshots/LiveSessionPage-zh-light.png` |
| 会中 · 只记录 | `previews/LiveNotesPage.html` | `screenshots/LiveNotesPage-zh-light.png` |
| 会后 · 工作会议纪要 | `previews/SummaryPage.html` | `screenshots/SummaryPage-zh-light.png` |
| 会后 · 面试复盘 | `previews/ReviewPage.html` | `screenshots/ReviewPage-zh-light.png` |

这些预览是 HTML 示意，用来对照 SwiftUI 的实现，不是 App 代码本身。衬线标题在 Safari 里显示为 New York，在其他浏览器里会回落到别的衬线字体。

## 在线版

claude.ai 上的设计系统 artifact “InterviewHacker”与这里的内容同步，默认只有所有者能看。
