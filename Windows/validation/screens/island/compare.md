# 提词岛（CueIsland.swift ↔ CueIslandWindow.axaml）：Mac 3.4.0 图对照（task-48）

- Mac 3.4.0 参考图（`Windows/validation/mac-screens/island/`，浅/深）：
  [cue](../../mac-screens/island/cue-light.png) / [cue-dark](../../mac-screens/island/cue-dark.png)、
  [hearing](../../mac-screens/island/hearing-light.png) / [hearing-dark](../../mac-screens/island/hearing-dark.png)、
  [deck](../../mac-screens/island/deck-light.png) / [deck-dark](../../mac-screens/island/deck-dark.png)、
  [launcher](../../mac-screens/island/launcher-light.png) / [launcher-dark](../../mac-screens/island/launcher-dark.png)、
  [pinned](../../mac-screens/island/pinned-light.png) / [pinned-dark](../../mac-screens/island/pinned-dark.png)、
  [autoend](../../mac-screens/island/autoend-light.png) / [autoend-dark](../../mac-screens/island/autoend-dark.png)。
- Windows 最新批次（`--demo --screenshots`，未入库）：`validation/screens/current/island-{collapsed,hearing,listening,cue,launcher,deck,bilingual}.png`（每个状态一张，不分浅/深；尺寸随状态）。
- 代码：`Sources/NextThought/CueIsland.swift` ↔ `Windows/src/NextThought.Desktop/Views/CueIslandWindow.axaml(.cs)`；E6 已加双语 gloss 行（`IslandHeadlineGloss`/`IslandDetailGloss`）。

| Mac 3.4 状态 | Windows 对应 | 结论 |
|---|---|---|
| cue 参考回答（浅色浮岛，问题行 + 回答要点） | `island-cue.png` | 结构一致：参考回答头、问题行、要点行；Windows 还显示题注/依据行，尺寸/圆角/阴影按 Windows 主题实现 |
| hearing 对方在说（波形/正在听提示） | `island-hearing.png` | 一致：波形、状态文案、对方说话时收起答案 |
| deck 会中面板 | `island-deck.png` | 一致：面板/入口行；Mac 3.4 的 deck 是提词岛展开后的面板，Windows 由同一个窗口状态机画 |
| launcher 无会议启动器 | `island-launcher.png` | 一致：启动器入口（开始/继续/打开设置） |
| pinned 钉住展开（双语） | **缺** | Windows 提词岛没有「钉住展开」状态，也没有钉住时的双语对照布局（E6 的双语 gloss 只在 cue/deck 行下显示）。记录为未实现。 |
| autoend 自动结束提醒 | **缺** | Windows 没有自动结束/到时提醒的提词岛状态。记录为未实现。 |

其它差异（记录）：Mac 3.4 每张图都有浅/深两版，Windows 的提词岛截图按当前主题各截一张，未成对；Mac 图尺寸 720–1000 pt 宽不等，Windows 岛宽 460/520 等由设置 `CueIslandWidth` 决定，未逐状态对齐像素位置（窗口锚点/阴影随平台）。Windows 额外状态 collapsed/listening/bilingual 为 Windows 截图批次的补充状态，Mac 3.4 没有对应图（Mac 的 compact/open/slide 三态在 Mac README 里注明未收录）。

## 自动验证

- E6 的 `AnswerLanguageParityTests` 覆盖提词岛 gloss 绑定；`CueIslandWindow` 状态机由 `--demo --self-test=live-ui` 的 47 项检查覆盖（含 island 状态切换）。
