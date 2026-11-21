# 阶段 2 §5.2 / §5.4 / §5.6 收尾证据

> 说明：会中页（LiveView / InsightsPanel）的文件在任务过程中被划归 live-parity（task-60）整页重写，§5.6 的会中页控件/箭头修复由他落地；本目录的截图在他落地后重拍。

## 页面截图（`--demo --screenshots`，隔离数据目录 `%TEMP%\nt-settings-parity`）

| 文件 | 页面 / 内容 |
| --- | --- |
| `home-light.png` / `home-dark.png` | 标题行（首页）：中间胶囊 + 右侧图标，无 Logo、无版本徽标、无演示提示、无「设置」文字 |
| `history-light.png` / `history-dark.png` | 所有记录：胶囊选中项为白色圆角块；标题行右侧出现上下文侧栏开关 |
| `series-light.png` / `series-dark.png` | 系列：右侧出现上下文侧栏开关 |
| `live-light.png` / `live-dark.png` | 会中页：设计系统控件的折叠区（历史回答/截图由 live-parity 重写） |
| `settings-shortcuts-light.png` / `settings-shortcuts-dark.png` | 设置 → 提词与教练页（含「全局快捷键」组）：快捷键冲突只在此页标出（底部状态栏不再常驻冲突提示；取自 settings-coach 截图更名） |

## 新增自动检查（`tests/NextThought.Tests`）

1. `DesignSystemControlScanTests.EveryScreenControlUsesTheDesignSystem`
   遍历 `Views/**/*.axaml`（跳过 `Styles/`、`bin/`、`obj/`）：`ComboBox/Button/TextBox/CheckBox/ToggleSwitch` 必须有 `Styles/*.axaml` 里的默认样式，或元素 `Classes` 命中一个已定义的 class 样式；`Expander` 没有设计系统组件，出现即失败（会中页原来的「历史回答 / 截图」正是系统 `Expander`）。
2. `IconGlyphCoverageTests.EveryIconCodepointExistsInTheBundledIconFont`
   解析随包 `Assets/Fonts/FluentSystemIcons-Regular.ttf` 的真实 `cmap`（format 4/12），比对：
   `Styles/Icons.axaml` 每个图标码点、源码里每个 `&#x....;` 字面量、每个 `{StaticResource IconXxx}` 引用是否已定义。缺字/未定义资源即失败（会中页翻页箭头原用 `Content="图标字符串"`，按默认字体渲染出方框）。

## 「故意改坏 → 检查失败 → 恢复」实测记录

1. **设计系统检查**
   - 改坏：在 `Views/MainWindow.axaml` 临时插入 `<ToggleSwitch Content="break the design-system check" />`（ToggleSwitch 没有设计系统默认样式/class）。
   - 运行：`dotnet test tests/NextThought.Tests --filter FullyQualifiedName~DesignSystemControlScanTests`
   - 看到（exit 1）：
     ```
     未套用设计系统样式的控件（§5.6）：
     MainWindow.axaml:57: ToggleSwitch is not styled by the design system (no default style and no known class)
     ```
   - 恢复：删除该元素后同一过滤 **2/2 绿**（与 IconGlyphCoverage 合跑 9/9 绿，含 MacScreensIndexTests）。

2. **图标码点检查**
   - 改坏：在 `Styles/Icons.axaml` 临时加 `<x:String x:Key="IconBreakCheck">&#x1F600;</x:String>`（U+1F600 不在随包图标字体里）。
   - 运行：`dotnet test tests/NextThought.Tests --filter FullyQualifiedName~IconGlyphCoverageTests`
   - 看到（exit 1）：
     ```
     图标码点缺字（§5.6）：
     IconBreakCheck: U+1F600 is not in the icon font
     Icons.axaml: U+1F600 is not in the icon font
     ```
   - 恢复：删除该行后过滤 **2/2 绿**。
   - 第一次尝试用 `&#xE000;` 时检查仍是绿的——说明 E000 本来就在字体里，换成确实不存在的 U+1F600 才证明检查真的会拦。

两次改坏都只存在于临时编辑，已恢复；本目录与提交里没有 `ToggleSwitch` 破例元素和 `IconBreakCheck`。

## 未解决 / 说明

- 设计系统检查是**源码级 XAML 遍历**（测试工程不引用 Desktop 可执行项目，无法实例化控件树）；它等价于「所有页面控件树」的提交时检查，默认样式表来自 `Styles/Controls.axaml` 的真实 selector。
- `Button.caption` 三颗自绘窗口按钮保持原实现（最右）。
- 会中页截图与 §5.6 的 LiveView/InsightsPanel 修复以 live-parity 的 task-60 提交为准；本任务只保留标题行、§5.4 和两项自动检查。
