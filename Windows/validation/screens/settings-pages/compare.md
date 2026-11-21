# 设置六页 + 快速设置：Mac 3.4.0 图 ↔ Windows 逐元素对照（task-48）

- Mac 3.4.0 参考图（`Windows/validation/mac-screens/settings-pages/`，每页浅/深）：
  [scenes-light](../../mac-screens/settings-pages/scenes-light.png) / [scenes-dark](../../mac-screens/settings-pages/scenes-dark.png)、
  [answers-light](../../mac-screens/settings-pages/answers-light.png) / [answers-dark](../../mac-screens/settings-pages/answers-dark.png)、
  [presenter-light](../../mac-screens/settings-pages/presenter-light.png) / [presenter-dark](../../mac-screens/settings-pages/presenter-dark.png)、
  [notes-light](../../mac-screens/settings-pages/notes-light.png) / [notes-dark](../../mac-screens/settings-pages/notes-dark.png)、
  [general-light](../../mac-screens/settings-pages/general-light.png) / [general-dark](../../mac-screens/settings-pages/general-dark.png)、
  [advanced-light](../../mac-screens/settings-pages/advanced-light.png) / [advanced-dark](../../mac-screens/settings-pages/advanced-dark.png)；
  快速设置：[quick-settings-light](../../mac-screens/settings/quick-settings-light.png) / [quick-settings-dark](../../mac-screens/settings/quick-settings-dark.png)。
- Windows 最新批次（`--demo --screenshots`，未入库）：`validation/screens/current/settings-{scene,answer,coach,notes,general,advanced}-{light,dark}.png`、`quick-settings-*.png`。
- Mac 图尺寸 1600×1787（场景页，980 pt 高页），Windows 1120×820；高度不同，同屏可见卡片数不同，只比较元素顺序/文案/字体层级。

## 六页总览与差异

| Mac 3.4 页 | Windows 页 | 结论 |
|---|---|---|
| 场景 | `settings-scene-*.png` | 同页但**行结构与数据差异大**：Mac 上半部是 9 个场景逐段（每段 = 场景名 + 会中/回答/纪要模板/提词四列），Windows 是一张表（场景列 + 会中/回答/纪要模板/提词列），行顺序为 通用、组会、客户沟通、汇报/答辩、组会讨论、讲座/课程、小组讨论、面试、工作布置、自定义，和 Mac 顺序（面试、组会、汇报/答辩、小组作业、客户沟通、工作会议、讲座/课程、小组讨论、通用）不同，且多出「自定义/工作布置/组会讨论」行、部分行的纪要模板值与 Mac 不对应（例：Windows「组会」行模板=工作会议，Mac「组会」=组会/研究讨论）。未改，属设置数据/结构问题。 |
| 回答与资料 | `settings-answer-*.png` | Windows 该页只有「技能」「资料」两张卡；Mac 该页顶部有「回答服务」（API/Codex 分段、已登录状态、模型、测试回答）与「联网搜索」（联网 自动判断 + 说明），这两块在 Windows 被放到「高级」/独立卡片。大差异，记录。 |
| 提词与教练 | `settings-coach-*.png` | 结构沿用 settings-structure 原表（提词显示方式/双屏演讲/字幕条/快捷键/说话人与翻译）；Mac 图确认卡片顺序为 提词→双屏演讲→字幕条→快捷键；Windows 的「说话人与翻译」仍在教练页（Mac 在通用→说话人组），原表已记。 |
| 纪要 | `settings-notes-*.png` | Mac 卡片：会议自动收尾 / 自定义模板 / 纪要高级；Windows 同页含自动收尾与模板，纪要高级项在「高级」页，原表已记。 |
| 通用 | `settings-general-*.png` | 逐图核对：界面语言/Language、外观、设置清单、备份与恢复、权限、更新；Mac 3.4 同页还有「记录每次会议」开关（Windows AppSettings 无对应键，原表已记）、说话人组与术语入口在 Mac 通用页而 Windows 在别处。 |
| 高级 | `settings-advanced-*.png` | Mac：声音与识别 / 回答服务 / 资料组件 / 纪要 / 提词 / 手机伴侣 / 更新 / 数据与维护（含 SetupChecklist、减少视觉效果、置顶、签名）；Windows：声音来源、本机语音识别、云端识别、屏幕与讲稿、资料组件、回答服务（Codex）、手机伴侣、设置清单。原表差异仍有效。 |

## 快速设置

Mac 3.4 快速设置（`settings/quick-settings-*.png`）分两组：
1. **状态**：回答服务（绿点 已连接 · gpt-5.5）、声音（绿点 系统声音和麦克风已允许）、语音识别（绿点 本机模型已就绪）。
2. **偏好**：提词（提词岛/浮窗分段）、提词字号（滑杆 + 数值）、提词岛一直展开、双屏演讲（+ 屏幕说明）、回答语言（跟随问题/中文/English 分段）、英文回答附中文对照、临场教练、留意眼神、会后自动整理纪要、外观（跟随系统/浅色/深色）；底部 全部设置… ⌘, / v3.4.0 / 检查更新。

Windows 3.4 轮快速设置（`quick-settings-*.png`，340×723）：状态区是运行时状态文案（快捷键占用/流式模型/整句精修/音频来源），偏好区为 自动回答、字幕显示译文、提词岛一直展开、提词字号滑杆、提词（下拉）、双屏演讲、听到外语自动翻译、跟着讲话翻幻灯片、回答语言（auto）、英语回答附中文对照、提词宽度（标准），底部「全部设置」。

差异（大，未改）：
- Mac 的状态三行（回答服务/声音/语音识别，带绿点）在 Windows 里换成运行时提示行；Windows 没有版本号/检查更新行。
- Mac 的提词岛/浮窗分段、字号数值、外观分段、临场教练、留意眼神、会后自动整理纪要 在 Windows 提速设置里缺失（部分在设置页/别处）；Windows 有 自动回答/字幕显示译文/听到外语自动翻译/跟着讲话翻幻灯片/提词宽度 等 Mac 快速设置没有的项（属 E 系列功能不同）。
- 布局：Mac 720×1367（360 pt 宽），Windows 340×723，接近；字体层级一致。

## 自动验证

- `tests/NextThought.Tests/MacScreensIndexTests.cs`：Mac 参考索引/必需视图 compare.md/README 索引条目与脚本存在。
- 本目录 compare.md 由 task-48 生成；设置结构逻辑仍由 `settings-structure/compare.md` 与设置结构测试覆盖。
