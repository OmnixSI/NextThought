# D1 / D6 / D8 差异清单（当前）

对照源：`docs/screenshots/v3/home.png`、`docs/screenshots/v3/home-dark.png`、
`docs/screenshots/history-light.png`、`materials-light.png`、`settings-simple-light.png`、
`docs/validation/v2.4.1/live-session-ui/synthetic-component-samples-light-1200.png`。

## D1 截图与文案

仍缺：
1. Mac 脚本的“准备 / 回顾 / 工作纪要”页面，Windows 没有对应视图，无法截图。
2. 演示数据会议标题已接近 Mac（ACL 2026 / 周一客户例会 / 字节后端二面），但转写、回答、纪要正文仍不是 Mac 截图逐字文本。
3. 没有逐页的差异清单（本文件只到页面/区块级别）。

已有：
- 7 页 × 中英 × 浅深 + 7 个提词岛状态，共 45 张证据。
- 5 组 Mac/Windows 并排图。

## D6 首页

并排：`compare-home-light.png`、`compare-home-dark.png`。

仍差：
1. 标题行：Mac 居中胶囊更窄，Windows 胶囊字号/间距偏大；Mac 左上无品牌区，Windows 保留品牌+版本。
2. 首页场景区：Mac 选中“面试”时是紫色 tint 13%，Windows 演示数据默认“工作会议”，显示为灰色 tint；选中态逻辑已实现。
3. 首页 greeting：Mac 是“11月11日 星期三 / 下午好。接下来是什么场合？”，Windows 仍是“晚上好。今天有哪些值得留下的结论？/ 把讨论留下来”。
4. “接着上一场”行：Mac 有场景类型与 0 场记录/2 份资料，Windows 只有“1 场会议 · 0 条经验”，缺资料数。
5. “最近记录”行：Mac 有日期/时间，Windows 只有标题/统计。

## D8 控件样张

并排：`compare-controls-light.png`。

仍差：
1. 卡片高度/垂直间距与 Mac 不一致（Windows 卡片更矮，行间更紧）。
2. Mac 的 DataState loading 是转圈，Windows 是横条 ProgressBar。
3. Mac 的 NewContentPill 在 Composer 上方且更大，Windows 的 pill 偏小。
4. Mac 的 ActionButton 有真实 hover/pressed 光标态；Windows 只是样张静态类。
5. 字体、字号、卡片阴影强度未逐项对比。

结论：三项目前都不能标“已验证”；D1 仍缺页面，D6/D8 只差视觉收口。
