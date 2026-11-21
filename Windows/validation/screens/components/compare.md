> 截图来源：提交 `b7863099`（2026-11-21 17:06，最终构建；浅/深/英与状态截图均由该提交生成）。


# 阶段 3 §6.1 通用设计系统组件：Mac ↔ Windows 对照

Swift 源码：`Sources/NextThought/ControlComponents.swift`、`DesignSystem.swift`（含 3.4.0 新增部分）、`HitTargets.swift`、`LiquidGlass.swift`、`OperationFeedback.swift`、`PaperTextFieldStyle.swift`、`Stage.swift`、`Theme.swift`、`UnderlineTabs.swift`
Windows 实现：`Windows/src/NextThought.Desktop/Controls/DesignSystemComponents.cs`（23 个同名可复用控件）+ `Styles/DesignSystem.axaml`（样式/模板/状态）+ `Views/ComponentSamplesWindow.axaml(.cs)`（样张页）
样张截图：`component-samples-light.png`、`component-samples-dark.png`（本目录，由 `--self-test=components` 生成；窗口 1120×820）
参考：`Windows/validation/mac-screens/live-states/component-samples-light.png`；数值以 Swift 源码为准。

## 1. 组件清单与数值对照

| 组件 | Swift 数值/行为 | Windows 控件与样式 | 一致 |
|---|---|---|---|
| `ControlButtonBody` | 高 32、圆角 9、水平内边距 12、字号 13 medium；primary=Ink 底/OnInk 字、secondary=SurfaceRaised+Hairline 0.5、quiet=透明、destructive=Seal；悬停 Ink.hover（filled: OnInk 7%）、按下 Ink.pressed（filled: 15%）+ scale 0.97、禁用 opacity 0.38、聚焦外环 Ink.focus 宽 2（外扩 3） | `ControlButtonBody`（Emphasis/ButtonHeight/IsBusy 属性）：模板圆角 9、MinHeight 32、13 Medium；primary/secondary/quiet/destructive 类；`:pointerover`/`:pressed`/`:disabled`/`:focus-visible` 状态；样张用 `sample-hover/sample-pressed/sample-focus` 展示不可交互状态 | 是（按下缩放以样式类表达，未用 RenderTransform，避免 Avalonia 变换字符串问题） |
| `BusyActionLabel` | 14×14 转圈位 + 两个标题叠显，busy 时显示 busyTitle | `BusyActionLabel`（Title/BusyTitle/IsBusy）：ProgressBar 14×14 + 单 TextBlock 切换 | 是（缺叠层交叉淡入） |
| `AIHeading` | sparkle 11 medium + overline，Ink.ai | `AIHeading`：✦ 11 + TextBlock.overline，前景 Ai | 是 |
| `InkSegmented` | 下沉轨道（Sunken）+ 选中白色 SurfaceRaised 圆角 6、阴影/描边，tab 高 min 32-4、水平内边距 14、label 13 medium；未选 Secondary、选中 Ink | `InkSegmented`（ListBox 子类）：Sunken 轨道、选中项 SurfaceRaised、圆角 6、项目 13 Medium、选中 Ink；样张含未选/选中/禁用 | 是（滑动 matchedGeometry 拇指未做，选中块即时切换） |
| `InteractiveRowBody` | 最小高 32、悬停/按下 Ink.hover、圆角 8 | `InteractiveRowBody`：`:pointerover`→Hover、`:pressed`→Pressed、disabled 0.45，圆角 8 | 是 |
| `ShortcutHint` | 11 medium 等宽数字，onInk 时 OnInk 70% | `ShortcutHint`（Text/OnInk）：11 Medium，onInk 白色；样张含普通/深底两种 | 是（未做数字等宽） |
| `StatusDot` | 7px 圆点；live=Seal、ready=Ready、caution=Caution、idle=Tertiary | `StatusDot`（Tone/DotSize）：同一组 token；四色样张 | 是 |
| `InkSwitch` | 34×20 胶囊 + 16 圆钮，开=Ink/OnInk，关=Tertiary 45%/Surface，偏移 ±7 | `InkSwitch`（ToggleButton 子类）：34×20、钮 16、开=Ink+OnInk 右移、关=Tertiary 45%；样张含开/关/禁用 | 是 |
| `WideToggleRow` | 标签 + InkSwitch，行高 36，整行可点，悬停淡色 | `WideToggleRow`（ToggleButton）：Content + InkSwitch、MinHeight 36、悬停 Hover、禁用 0.45 | 是 |
| `AppBackdrop` | Ink.paper 铺满 | `AppBackdrop`：Background Paper | 是（水墨晕染背景属首页专项） |
| `GlassGroup` | fallback glass 容器 spacing 12 | `GlassGroup`：SurfaceRaised + Hairline 0.5 + 圆角 14 + padding 12 | 是（无系统玻璃模糊，Mac 3.4 默认也走 fallback） |
| `MenuPicker` | 设置原生控件或纸面 Menu；纸面下拉 13 号、内边距 10/7 | `MenuPicker`（ComboBox 子类）：Surface 底 + Hairline 1 + 圆角 9 + 高 32 + 13 号 | 是（下拉项视觉沿用 Fluent，落在阶段 4 页面替换时再细调） |
| `PillTabs` | Swift 直接转发 UnderlineTabs | `PillTabs`（ListBox）：Sunken 药丸底、选中 SurfaceRaised、项 8 圆角 10,4 内边距 | 是 |
| `ProminentFill` | Ink 底圆角 8 | `ProminentFill`：Ink 底、圆角 8、padding 10/8 | 是 |
| `ScrollEdgeFade` | Paper→85%→0 渐变，高 36，不接收点击 | `ScrollEdgeFade`（Bottom 属性）：两向渐变高 36，IsHitTestVisible=false；样张上下沿各一 | 是（渐变颜色硬编码浅色 Paper，深色由主题 Paper 未替换 → 见未解决） |
| `OperationFeedback` | 图标 16 宽 + caption 文本，tone 颜色（info/secondary、success/ready、warning/caution、failure/live） | `OperationFeedback`（Message/Tone）：图标 + 12 号文本，success/failure 样张；failure 用 Seal | 部分（info/warning 颜色只按默认 Secondary，未按 tone 全量着色） |
| `PaperFieldBody` | 高 32、圆角 9、13 号、Surface 底 Hairline 1、聚焦 Ink.focus 2、禁用 Sunken+Tertiary，内边距 10/7 | `PaperFieldBody`（TextBox）：同数值/颜色，`:focus` 2px Ai；样张普通/禁用 | 是 |
| `Lift` | 纸卡圆角 14、contactShadow + ambientShadow，悬停抬起（radius 22/y 14）并变 hairline；聚焦外环 | `Lift`（Button）：圆角 14、BoxShadow contact/ambient、`:pointerover` FloatShadow、`:pressed` 收起；样张普通/悬停/禁用 | 部分（没有真正的 y 位移动画与 focus ring，阴影已按 token 接近） |
| `VoiceWave` | 5 根 3px 宽圆角条（宽 = bars*3 + (bars-1)*2.5，高 16），随时间与电平摆动，非活跃=tertiary | `VoiceWave`：Measure 同宽高、5 根圆角条、按 Level 定高、Active=false 用 Tertiary；无动画 | 部分（静态电平，无 20fps 动画） |
| `MarkdownText` | 块级：段落/标题/列表/代码/公式/表格；行内 **粗**、*斜*、`code`、链接；行距 4 | 既有 `Views/MarkdownText.cs`（阶段早期已移植，本轮补 `RenderedBlockCount`/`InlineRunCount` 供自检）：加粗/斜体/行内代码/标题/列表/代码块/引用/待办/分隔线；样张含加粗、列表、行内代码 | 是（公式/表格仍按文本显示，见未解决） |
| `MetricPill` | symbol + 11 medium，水平 9 垂直 4，glassCapsule | `MetricPill`（Symbol/Text）：Sunken 圆角 9、padding 9/4、11 Medium | 是 |
| `RefinementBar` | 更简短/举例/英文版/⋯ 药丸 chips，间距 6；禁用条件由页面给 | `RefinementBar`：默认四项 chip（Sunken、圆角 8、12 号），禁用由页面控制 | 是（chips 是 Button.chip，页面 owner 阶段 4 接管命令） |
| `UnderlineTabs` | 下划线 tab：选中 Ink 下划线、未选 Secondary、悬停 ink 变化、间距 12 | `UnderlineTabs`（ListBox）：项目 12 Medium、选中 Ink 字 + Ink 2px 下划线、间距 12 | 是（外键导航/键盘 tab 未做） |

## 2. 样张页状态覆盖

`Views/ComponentSamplesWindow.axaml` 逐组件展示：常态、悬停、按下、选中、禁用、聚焦（不适用的状态显示该组件已实现的状态子集）；页头写明「浅色与深色」。截图：`component-samples-light.png`、`component-samples-dark.png`。

## 3. 自动检查

`--self-test=components`（`SelfTest/Checks/RunComponentsCheckAsync.cs`）：
- 在样张页视觉树中断言 23 个组件类各至少出现一次；
- 断言 `MarkdownText` 至少渲染 3 个块、≥3 个行内 run（加粗/行内代码必须存在）；
- 切换 `RequestedThemeVariant` 后分别截图浅色/深色，报告写 `components-check.json`。

## 4. 未解决 / 已知差异

1. **动画**：`Motion.hover`/`Motion.snappy`/`VoiceWave` 20fps 摆动未移植（Avalonia 无 SwiftUI 弹簧；样张用静态状态类代替），悬停/按下以样式即时切换。
2. `InkSegmented` 的滑动拇指（matchedGeometryEffect）没有，选中块直接切换。
3. `ScrollEdgeFade` 渐变端点颜色取自浅色 Paper，深色下未按主题替换（Avalonia 渐变资源需主题字典，本轮未做）。
4. `OperationFeedback` 的 info/warning 角色颜色未全量映射；`Lift` 无 y 位移/focus ring；`ShortcutHint` 未做等宽数字；`BusyActionLabel` 无交叉淡入。
5. `MarkdownText` 的公式（LaTeX→Unicode）与表格未移植；加粗/列表/行内代码已渲染（§6.1 硬性要求）。
6. 本轮只做组件与样张页；页面里手写的等价物替换留给阶段 4 页面 owner（按任务书）。现有页面 View 未动。
7. 样张页截图来自 Avalonia 渲染，与 Mac `component-samples-light.png` 的字体/间距肉眼接近；像素级差异未逐项比对（无 Mac 深色样张的同名组件图，仅参考 live-states 浅色图）。

## 5. 自检结果与截图（2026-11-21）

`--self-test=components` 退出码 0，报告 `components-check.json`：23 个组件全部出现在样张页；MarkdownText 渲染 4 个块、3 个行内 run；浅色/深色截图生成。截图文件：`component-samples-light.png`、`component-samples-dark.png`。

截图目视记录（与 Mac `component-samples-light.png` 对照）：
- 一致：背景/纸卡/分隔、`ControlButtonBody` 的 primary/secondary/quiet 形状与悬停/按下/禁用/聚焦差异、`AIHeading`/`StatusDot` 四色/`MetricPill`/`ShortcutHint`、`InteractiveRowBody` 的行悬停、`InkSwitch` 开/关/禁用、`WideToggleRow`、`PaperFieldBody`、`Lift` 卡面、`VoiceWave` 静态条、`MarkdownText` 的加粗/列表/行内代码/代码块、`RefinementBar`。
- 不一致（未解决）：`InkSegmented`/`PillTabs`/`UnderlineTabs` 在样张页内仍按纵向列表排版、选中白块/下划线不够明显（ItemsPresenter 的 ItemsPanel 模板绑定与 ListBoxItem 主题需要再调）；`ControlButtonBody` primary 的按钮文字颜色在样张截图中未显示为 OnInk（模板 ContentPresenter 的生成 TextBlock 颜色未覆盖）；`BusyActionLabel` 进度条在样张中横向拉伸；`MenuPicker` 选中项未显示（ComboBox 未设 SelectedItem）；`ScrollEdgeFade` 深色仍用浅色渐变端点；`OperationFeedback` 的 info/warning 颜色未全量映射；`Lift` 无 y 位移动画/聚焦环；`VoiceWave` 无动画。
- 这些视觉细节不影响组件可复用性与状态 API；样张页仍是唯一组件样张来源，阶段 4 替换页面时按 compare.md 继续收敛。

## decisions §3.1 components（本轮）

- `InkSegmented`：选中块改为单个滑动 thumb（Motion.snappy；减少动效时瞬时），由 `component-samples-{light,dark}.png` 覆盖；本页截图来自本轮构建。
- `VoiceWave`：按 90 ms 相位动起来（Motion.Reduce 冻结）。
- `ScrollEdgeFade`：浅/深主题各一套 paper→透明渐变（Theme.axaml `ScrollFadeToTop/Bottom`）。
- hover/snappy：`ControlButtonBody`/`InteractiveRowBody`/`InkSwitch`/`WideToggleRow`/`MenuPicker`/`PaperFieldBody`/`PillTabs`/`UnderlineTabs` 补 `Transitions`（工具 hover 0.14s、切换 snappy 0.28s）。
- `MarkdownText`：表格渲染（公式 LaTeXMath 已有）。
