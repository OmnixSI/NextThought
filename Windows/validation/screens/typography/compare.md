> 截图来源：提交 `940a0280`（2026-11-21 17:24，最终构建；浅/深/英与状态截图均由该提交生成）。



# 字体样张与 §5.1 / §5.5 / §5.7 对照

对照基准：Mac v3.4.0（`f015e8c6`），令牌取自 `Sources/NextThought/DesignSystem.swift`
（`Typo` 69–78 行、`Ink` 25–63 行、`Space` 165–176 行）；界面参考截图
`Windows/validation/mac-screens/live-states/component-samples-light.png`、
`Windows/validation/mac-screens/home/home-light.png`。

Windows 样张：`typography-light.png`、`typography-dark.png`（本目录；窗口 900×1020，演示数据，
每级字号一行中文一行英文）。应用截图条件：窗口 1120×820、100% 缩放。

## 1. 字体与字重（§5.1）

本机复核发现的可变字体问题：`NotoSansSC-VF` 的默认实例是 **Thin (wght=100)**、
`NotoSerifSC-VF` 默认 **ExtraLight (200)**，`InterVariable` 的内部族名是
`Inter Variable Text`（主题里写的 `Inter Variable` 匹配不到）——三者叠加就是「文字偏细」。
Avalonia 12 不会按 `FontWeight` 实例化可变字体，因此按任务书改为**每个用到的字重一个静态字体文件**
（由随包 VF 在对应 wght 实例化；已删除四个 VF 文件）：

| 字重 | 西文 | 中文 |
| --- | --- | --- |
| Regular 400 | `Inter-Regular.ttf`（族 `Inter`） | `NotoSansSC-Regular.otf`（族 `Noto Sans SC`） |
| Medium 500 | `Inter-Medium.ttf` | `NotoSansSC-Medium.otf` |
| SemiBold 600 | `Inter-SemiBold.ttf` | `NotoSansSC-SemiBold.otf` |
| Serif Medium 500 | `SourceSerif4-Medium.ttf`（族 `Source Serif 4`） | `NotoSerifSC-Medium.otf`（族 `Noto Serif SC`） |

`Styles/Theme.axaml` 的 `SansFamily` / `SansMediumFamily` / `SansSemiBoldFamily` / `SerifFamily`
每个资源都把**同字重的西文+中文放在同一条 FontFamily 链**里，所以一行混排不会两种字重；
`Program.cs` 用 `FontManagerOptions` 把旧的纯族名（`Inter`、`Noto Sans SC`、`Source Serif 4`、
`Noto Serif SC`）映射到这些随包字体，`MarkdownText` 等直接写族名的控件也走同一套字体。

逐级对照（Mac `Typo` 值 vs Windows `Theme.axaml` 实际）：

| 级别 | Mac 值 | Windows 值（样式 + 字体） | 结论 |
| --- | --- | --- | --- |
| hero | 38 / medium / serif | `TextBlock.hero` 38 + `SerifFamily`（Source Serif 4 / Noto Serif SC Medium）+ Medium | 一致 |
| display | 30 / medium / serif | `TextBlock.display` 30 + `SerifFamily` + Medium | 一致 |
| section | 19 / medium / serif | `TextBlock.section` 19 + `SerifFamily` + Medium | 一致 |
| prompt | 22 / medium | `TextBlock.prompt` 22 + `SansMediumFamily` + Medium | 一致 |
| title | 15 / semibold | `TextBlock.title` 15 + `SansSemiBoldFamily` + SemiBold | 一致 |
| body | 15 / regular | `TextBlock.body` 15 + `SansFamily`（Regular 静态字重，不设 FontWeight） | 一致 |
| label | 13 / medium | `TextBlock.label` 13 + `SansMediumFamily` + Medium | 一致 |
| caption | 12 / regular | `TextBlock.caption` 12 + `SansFamily` | 一致 |
| overline | 11 / semibold | `TextBlock.overline` 11 + `SansSemiBoldFamily` + SemiBold + 0.66 字距 | 一致 |
| code | 13 / monospaced | `TextBlock.code` 13 + `MonoFamily`（Cascadia Mono / Consolas） | 一致 |

样张页里每级都同时排中文和英文（如 hero 一行「交付之前，先把话说清楚。」+ 一行
"Say it clearly before the meeting."），与 Mac 组件样张/首页截图的标题、正文、标签逐级目测：
**笔画粗细一致**；半粗的 title/overline 与正文的对比在两边相同；中文和西文在同一行里粗细一致
（静态同字重配对）。西文/中文字形本身是 Inter + 思源黑体（任务规定），Mac 用系统 San Francisco +
宋/黑；字形不同但字重、字号、行距、颜色一致，按 §5.1 的验收口径算一致。

## 2. 文字颜色（§5.1）

| 令牌 | Mac 浅色 | Mac 深色 | Windows 浅色 | Windows 深色 | 结论 |
| --- | --- | --- | --- | --- | --- |
| ink | #1C1B19 | #ECEBE8 | `InkBrush` #1C1B19 | #ECEBE8 | 一致 |
| secondary | #6B6963 | #A3A19B | `Secondary` #6B6963 | #A3A19B | 一致 |
| tertiary | #706E68 | #95938C | `Tertiary` #706E68 | #95938C | 一致 |

正文/标题只用 `InkBrush`，说明文字用 `Secondary`，占位与装饰用 `Tertiary`；`Theme.axaml` 里
`TextBlock.*` 样式的 `Foreground` 只出现这三个资源，**没有用 Opacity 叠出更淡的颜色**
（样张页最后一组 ink/secondary/tertiary 三行可见深浅关系与 Mac 一致）。

## 3. 滚动条（§5.5）

| 项目 | Mac | Windows `Theme.axaml` | 结论 |
| --- | --- | --- | --- |
| 占位 | 不占布局宽度（overlay） | Fluent `ScrollViewer` 的 overlay 模板 + 无固定列 | 一致 |
| 自动隐藏 | 滚动/悬停时出现，否则消失 | `ScrollBar` `AllowAutoHide=True`、`HideDelay=1.5s` | 一致 |
| 粗细 | 细 | `Width=8`、`MinWidth=8` | 一致 |
| 轨道 | 无箭头、无底色 | `Background=Transparent`，无箭头样式 | 一致 |

（滚动条只在滚动/悬停时出现，静态截图里不可见；用运行中的应用滚动长列表目测出现/消失，
行为与 §5.5 要求相同。）

## 4. 宽度、圆角、阴影（§5.7）

| 项目 | Mac | Windows | 结论 |
| --- | --- | --- | --- |
| 内容最大宽度 | `Space.content = 720` | `Content = 720`；新增 `Control.contentColumn` 样式（MaxWidth=720、居中） | 一致 |
| 圆角 | 22 / 14 / 8 | `RadiusLarge` 22、`RadiusMedium` 14、`RadiusSmall` 8；卡片样式引用 | 一致 |
| 浮起阴影 | `floatShadow`：浅 #1C1B19 12%，深 #000 50%，radius 16 / y 12 | 浅 `0 12 16 0 #1F1C1B19`、深 `0 12 16 0 #80000000` | 一致 |
| 贴地阴影 | `contactShadow`：浅 7%、深 40%，radius 1 / y 1；`ambientShadow`：浅 6%、深 32%，radius 14 / y 8 | 浅 `0 1 2 0 #121C1B19` + `0 8 14 0 #0F1C1B19`；深 `0 1 2 0 #66000000` + `0 8 14 0 #52000000`（组合成 `PaperCardShadow`） | 一致 |
| 阴影资源形式 | 每主题一套 | `ContactShadow` / `AmbientShadow` / `FloatShadow` 三个 `BoxShadows` 资源，浅深 dictionaries 各一套；`PaperCardShadow`/`FloatShadowValue` 引用同样的值 | 一致 |

## 5. 结论

**§5.1 / §5.5 / §5.7 逐条一致。**

范围外（由后续页面任务负责，这里只记录现状）：首页内容列仍是 `HomeView.axaml` 里写死的
`MaxWidth="760"`（§6.4 首页任务改为 720）；`MarkdownText` 内部用自己的族名，已通过
`FontManagerOptions.FontFamilyMappings` 映射到同一套静态字体，字重表现与样张一致。

## 6. 证据文件

- 本目录：`typography-light.png`、`typography-dark.png`（字体样张，900×1020）
- 对照：`Windows/validation/mac-screens/live-states/component-samples-light.png`、
  `Windows/validation/mac-screens/home/home-light.png`/`home-dark.png`
- 测试：`Windows/tests/NextThought.Tests/DesignSystemTypographyTests.cs`、
  `DesignSystemScrollLayoutTests.cs`（字号/字重/字体资源/颜色/滚动条/宽度/圆角/阴影逐项断言）
