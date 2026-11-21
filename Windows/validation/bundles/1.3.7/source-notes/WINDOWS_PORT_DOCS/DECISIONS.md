# NextThought Windows 移植 · 决策与发现记录

本文件记录移植过程中确认的事实、偏离方案的原因，以及新增的资源。
（方案全文见 `docs/plan_v2.txt`，由 `NextThought Windows 移植方案 v2.pdf` 提取。）

## 1. 环境事实（2026-11-18，本机）

| 项目 | 结果 |
| --- | --- |
| 仓库 | `D:\NextThought开发\src\NextThought`，分支 `port/windows-native`，提交 `01606e2`（Windows 0.4.0 基线） |
| 基线构建 | `dotnet build NextThought.Windows.sln -c Release`：**0 警告 0 错误** |
| 基线测试 | `dotnet test tests/NextThought.Tests`：**79/79 通过** |
| 基线可执行文件 | 自包含发布，`--startup-check` 报告 `passed=true`，冷启动 **0.405 秒** |
| .NET SDK | 由 `Windows/scripts/bootstrap_sdk.py` 安装到 `Windows/.tools/dotnet`，版本 **10.0.403**（官方 SHA-512 校验通过） |
| Visual Studio / C++ 生成工具 | **未安装** → 暂时无法 Native AOT（需要 MSVC 链接器），按方案第 2 节启用回退：`PublishReadyToRun + SelfContained`；源码结构不变，拿到生成工具后可直接打开 AOT |
| Inno Setup | 未安装，`scripts/bootstrap_installer.ps1` 可下载固定版本 7.1.1 并校验签名 |
| Python | 使用 DSH 自带 3.12；已安装 pymupdf 用于读取方案 PDF |

## 2. Avalonia 12 的适配结论（方案第 2、8 节）

Avalonia 12.1.6 已发布，与方案一致，但 API 与 11.x 有差异，已在代码中落实：

| 方案写法 | Avalonia 12 实际 |
| --- | --- |
| `ExtendClientAreaChromeHints` | 属性已移除；改用 `WindowDecorations="BorderOnly"` + 自绘标题栏按钮，并用 `WindowDecorationProperties.SetElementRole` 注册最小化/最大化/关闭 |
| `ComboBox.DisplayMemberPath` | 已移除；改用 `ItemTemplate` |
| `TextBox.Watermark` | 改为 `PlaceholderText` |
| `TextBox.VerticalScrollBarVisibility` | 改为 `ScrollViewer.VerticalScrollBarVisibility` |
| `IClipboard.SetTextAsync` | 已移除；改用 `DataTransfer` + `DataTransferItem.CreateText` |
| `ControlTheme` 承载按钮外观 | Fluent 主题会覆盖模板内 `ContentPresenter` 的前景色，按钮改为**样式内联模板**（`Style Selector="Button.primary"` 等），主按钮文字用显式 `TextBlock` 着色 |

另外：`DataObject`/`DataFormats` 已过时，`Application.OnExit` 不再是可重写方法（改用 `IClassicDesktopStyleApplicationLifetime.Exit`）。

## 3. 用户提供的 Swift 工具箱：能力与边界

2026-11-18 本机已通过 WinGet 安装 **Swift 6.4.2**（`x86_64-unknown-windows-msvc`），
`swiftc` 可编译并运行 Foundation 程序。实测结论：

- **不能**用它直接构建 NextThought 的界面：`Sources/NextThought` 里有 2.7 万行 SwiftUI + AppKit，
  Windows 上没有这两个框架，也没有对应的 AppKit 端口。
- **能**用它编译 `Sources/NextThoughtCore` 的大部分代码。实测：把 80 个 Core 文件里的
  - 4 个平台绑定文件（`DocumentJobs.swift` = SQLite3、`ProcessLineReader.swift` = Darwin、
    `ScrollFollowInputClassifier.swift` = CoreGraphics、`ZipformerStreaming.swift` = SherpaOnnx）
  - 7 个 `import CryptoKit` 文件
  去掉后，**其余 70 个文件在 Windows Swift 6.4.2 上整体编译通过**（`swiftc -O`）。
  剩余 10 个文件需要：`CryptoKit` → 用 `apple/swift-crypto`（模块 `Crypto`，API 同名）做一个同名
  shim target；SQLite3 → 用 Windows SDK 的 `winsqlite3` 或 `sqlite3` 包；Darwin/CoreGraphics/Sherpa 各自替代。

  这带来的价值与方案第 6 节直接相关：**不必等 Mac，就能在 Windows 上运行真正的 Swift Core 来生成
  共享夹具**（规范 JSON 字节、修订哈希、伴侣加密向量、问题语料判定），C# 移植逐条对齐。
  这件事列为后续阶段的 "fixtures-gen" 工具，不阻塞界面主线。

## 4. 界面实现（P4 阶段进行中）

新增工程 `Windows/src/NextThought.Desktop`（Avalonia 12.1.6 + CommunityToolkit.Mvvm 8.4.2），
目标框架 `net10.0-windows10.0.19041.0`，输出 `NextThought.exe`。

已实现：

- 设计令牌（`Styles/Theme.axaml`）：`Ink` 全色板（浅/深两套）、`Typo` 字号、`Space` 间距与圆角，
  与 `DesignSystem.swift` 逐项对应；表面类 `paperCard` / `aiBlock` / `floatingSurface`。
- 外壳：自绘 52 高标题栏（应用标记、版本、页面副标题、窗口按钮）、220 宽侧栏（五个区域 + 最近会议 + 数据目录）、
  底部状态条（状态文字、进度、取消）。
- 五个页面：首页、会议（实时转写 + 参考回答 + 提问）、纪要（Markdown 渲染）、资料库（导入/筛选/预览）、设置（声音、识别、
  说话人与翻译、检索、回答服务、数据与关于）。
- `MarkdownText` 控件：逐行解析标题、要点、编号、待办、引用、分隔线、代码块，行内处理粗体/斜体/行内代码/链接。
- 演示模式：`--demo` 填充内置样本会议（结构组会议的 10 条转写、回答、纪要），`--section=Live` 等用于逐页截图，
  `--dark` 切深色。数据不写入用户目录，是方案第 8 节「演示模式先行」的第一步。
- 应用层入口 `AppState`：按 WPF 基线的行为逐段移植（开始/结束记录、流式转写、问题候选、自动回答、纪要、导出、
  备份/恢复、导入 WAV、历史加载、状态机），服务仍复用 Platform 的 `AudioCaptureService`、`LiveSpeechEngine`、
  `SessionStore`、`ChatCompletionClient`、`CodexAnswerClient`、`SummaryService`、`RetrievalService`、`BackupService`。

## 5. 与方案的偏离（需在后续阶段收敛）

1. 目前 `NextThought.Desktop` 直接引用 `NextThought.Platform`，尚未落地方案第 3 节的
   `NextThought.App`（应用层）与端口接口。`AppState` 已是应用层雏形，后续按端口清单拆出。
2. Native AOT 未开启（缺 MSVC），按方案 R1 回退为自包含发布（`PublishReadyToRun` 在 .NET 10
   crossgen2 上崩溃，暂用纯自包含）。
3. 提词器浮窗、区域截图、手机伴侣、双屏演讲、同声传译上岛尚未移植（方案 P5/P7）。
4. Core 的 80 个文件仍是基线 14 个文件的精简重写版；已逐文件移植 `SubtitleBar`，其余按方案 P1
   分批进行，可用第 3 节的 Windows Swift 生成夹具做对照。

## 6. 第二轮实机验证（2026-11-18 下午）

### 6.1 真实语音识别（本机模型，不是回放）

用 `scripts/prepare_test_models.py` 下载并校验 X-ASR 480 ms 与整句精修模型（约 300 MB，SHA-256 与
`Resources/models.json` 一致），再用 `NextThought.Cli transcribe-wav` 跑官方样例：

```
xasr480-0.wav  音频 10.05 秒
输出: 昨天是 monday / Today is the day after tomorrow 是星期三.
推理 0.78 秒（RTF 0.078），含整句精修；进程总耗时 6.1 秒（含模型加载）
```

结论：sherpa-onnx 1.13.8 + ONNX Runtime 1.28.2 绑定、UTF-8 中文路径、整句精修在 .NET 10
自包含产物上都正常，与方案第 7 节「已有」一致。

### 6.2 浮窗与全局快捷键

| 能力 | 实现 | 验证 |
| --- | --- | --- |
| 提词岛 | `Views/CueIslandWindow.axaml`：无边框、透明、置顶、不激活、顶部居中；问题 + 三行回答 + 状态点 | 截图 `qa\shots\v05-island-final.png` |
| 字幕条 | `Views/SubtitleBarWindow.axaml` + Core `SubtitleBar`：居中一句、译文更小、保持 4 秒后 0.5 秒淡出 | 截图 `qa\shots\v05-subtitles3.png` |
| 三态切换 | `LiveWindowMode`（纪要窗口/字幕条/隐藏）循环，界面按钮 + Ctrl+Alt+M | 单元测试 12 项 |
| 全局快捷键 | `GlobalHotkeys.cs`：独立消息泵线程 + `RegisterHotKey`（hwnd=0），`WM_HOTKEY` 回投 UI 线程 | 四个组合注册成功（`qa\ui-data\Logs\hotkeys.log`），实测 Ctrl+Alt+O 弹出提词岛 |

注意：**用 HWND_MESSAGE 消息窗口注册全局热键会失败**（`RegisterHotKey` 返回 1408
ERROR_HOTKEY_ALREADY_REGISTERED，即使没有别的程序占用）；改为在消息泵线程上以线程级热键
（hwnd 为 0）注册才成功。后续移植其余快捷键按此实现。

### 6.3 界面截图（demo 数据，浅色）

`qa\shots\v05m-*.png`：首页、会议、纪要、资料库、设置。与 Mac 的差异见第 5 节与
`Windows/PORTING_STATUS.md`。

## 7. 第三轮：提词器与区域截图（0.7.0）

### 7.1 提词器浮窗

- 窗口：无边框、置顶、可拖动、可缩放；`win-ui.json` 保存位置/大小/字号/透明度/穿透状态。
- 鼠标穿透：`GetWindowLongPtr/SetWindowLongPtr` 改 `GWL_EXSTYLE`，加
  `WS_EX_LAYERED (0x80000) | WS_EX_TRANSPARENT (0x20)`。实测扩展样式：关闭 `0x280008`、
  开启 `0x280028`，说明位生效。
- 已知限制：穿透后无法用鼠标点回「穿透」按钮，只能再按 Ctrl+Alt+T 显示/隐藏提词器或重开，
  与方案第 9 节「控制按钮放在另一个小窗口」的 Mac 做法不同，后续可把工具条拆成独立窗口。

### 7.2 区域截图

实现顺序：最小化主窗口 → 等一帧（`DispatcherPriority.Background`）→ 再等 360 ms →
`BitBlt` 复制虚拟屏幕 → 还原窗口 → 在普通窗口里拖选 → 裁切 → 存 PNG → 导入资料库（OCR）。

两个实测教训：

1. **`Hide()` 之后立刻抓屏会把自己的窗口也拍进去**：Avalonia 的 `Hide()` 是投递到平台线程的，
   300 ms 也不保证已生效；改成 `WindowState = Minimized` 再抓，窗口必定不在屏幕上。
2. **`BitBlt` 在 gdi32.dll，不在 user32.dll**：第一次调用报「Unable to find an entry point」，
   改正后 3000×2000 抓屏正常。

抓屏本身用 GDI DIB（32bpp、负高度表示自上而下），像素直接写进 Avalonia `WriteableBitmap`，
不依赖 `System.Drawing.Common`；裁切按行 `Marshal.Copy`。截图导入后由 Windows.Media.Ocr 取字，
实测得到 1 段中文文本（有 OCR 常见的字间空格，属系统识别特性）。

### 7.3 构建稳定性

打包脚本里加了「先删 Desktop 的 obj/bin」这一步：MSBuild 子节点被 XAML 编译器崩溃拖垮后，
残留中间产物会让后续 `dotnet build` 直接 `MSB4166` 或 `0xC0000005`，清理后一次通过。

## 8. 第四轮：DPI、深色外观与 Core 批次

### 8.1 「界面被放大截断」是截图工具的锅，不是界面

清理重建后截图突然变成「内容放大、右侧被切掉」。查 `--startup-check` 新增的诊断：
`renderScaling=1.75`、`screenBounds="0, 0, 3000, 2000"`、窗口 `1120×820` DIP。
真实窗口是 1984×1448 物理像素，但截图脚本（PowerShell 进程是 DPI 不感知的）拿到的
`GetWindowRect` 是虚拟化坐标 1134×827，于是只截到了窗口左上角。

修法：两个截图脚本在调用任何 GDI 之前执行
`SetProcessDpiAwarenessContext(-4)`（PER_MONITOR_AWARE_V2）。修完后截图 1984×1448，
浅色与深色界面都完整（`qa\shots\v07-light-home2.png`、`v07-dark-home2.png`）。

结论：**界面本身在 175% 缩放下布局正确**；此前的「截断」是测试工具的问题，已修正工具，
并在启动自检里长期记录窗口尺寸与缩放，避免再次误判。

### 8.2 深色外观

`--dark` 使用同一套令牌的 Dark 字典（`Styles/Theme.axaml`）：paper `#121110`、surface `#1B1B19`、
ink `#ECEBE8`、hairline `#1CECEBE8`。截图确认首页、侧栏、卡片、按钮对比度正常；
其余页面尚未逐页截图对照（记入待办）。

### 8.3 Core 批次 A（续）

本轮把三个自包含文件逐文件移植并接入运行时：

| Swift | C# | 测试 | 是否接入 |
| --- | --- | ---: | --- |
| `AudioTimeline.swift` | `Core/AudioTimeline.cs` | 10 | 否（等识别器/声纹调用方） |
| `TranscriptDeduplicator.swift` | `Core/TranscriptDeduplicator.cs` | 8 | 否（等识别器暴露句尾时间） |
| `EchoSuppression.swift` | `Core/EchoSuppression.cs` | 8 | 是：`AppState.IsTextEcho` 用 `EchoTextFilter.IsEcho` 标记麦克风回声 |

`CrossTalkMonitor`（声学回声）已移植并有「麦克风跟随系统包络即判定回声」的单元测试，
但采集端还没有把两路 PCM 同时喂给它，属于「已实现、未接入」。

### 8.4 桌面端整条链路实测（不是 CLI）

新增不弹文件框的自检开关 `--import-wave=<wav>`：它调用与界面「导入 WAV」按钮完全相同的
`AppState.ImportWaveFileAsync`，跑完后把会话号、转写行数、状态写到 `<wav>.json` 再退出。

用已校验的 X-ASR 480 ms + 整句精修模型跑 `xasr480-1.wav`：

```
sessionId      30e36088-42e9-4c44-9cec-191c06858caf
transcript.jsonl 1 行：这是第一种， 第二种叫呃与 always， always 什么意思啊？
meta.json      audioSource=imported-wav, recognizer=xasr480+refine, platform=windows
status         录音已转写并归档，未保存原始音频。
```

也就是说 **界面 → AppState → LiveSpeechEngine/SpeechRecognizerSession → SessionStore 归档**
这条链路在真实模型上跑通，不只是 CLI 诊断。

第一次跑到空数据目录时转写行数为 0、`meta.json` 已建：原因是模型不在该数据目录，异常被
`RunWorkAsync` 收进状态栏。为此在报告里加了 `status` 字段，避免以后把「模型缺失」误判成
「转写坏了」。

## 9. 第五轮：托盘、自动收尾与一个隐蔽的绑定坑（0.9.0）

### 9.1 ComboBox 的 SelectedItem 会写回 null

自动收尾接好之后实测一直不生效：日志里 `minutes=10`，而 `settings.json` 写的是 1。
加了一行诊断输出 `raw=`（`Settings.AutoEndMinutes` 的字符串值）后看到它是空串。

原因：主窗口启动时会把五个页面都构造出来（`IsVisible` 只是隐藏），设置页的
`ComboBox SelectedItem="{Binding Settings.AutoEndMinutes}"` 在 `ItemsSource` 还没就绪时
把 `SelectedItem` 置空，TwoWay 绑定就把 `null` 写进了字符串属性；`int.TryParse("")` 失败，
于是永远退回默认 10 分钟。

修法：该属性改为手写，忽略空写入（保留上一个有效值）。同类「字符串 + ComboBox」绑定都应按
这个方式处理。修复后日志变为 `minutes=1 raw=1`，60 秒无发言触发 `NobodySpoke`。

### 9.2 自动收尾的验收记录

```
autoend tick=15 mode=end minutes=1 raw=1 selected=end said=False quiet=19s reason=
autoend tick=45 mode=end minutes=1 raw=1 selected=end said=False quiet=49s reason=
autoend tick=56 mode=end minutes=1 raw=1 selected=end said=False quiet=0s  reason=NobodySpoke
meta.json: "endedAt":"2026-11-18T09:20:52Z"（会话在 60 秒静音后被归档）
```

`quiet=0s` 出现在触发那一行，是因为 `MeetingEndWatch` 在返回原因后把静音起点重置为当前时间，
这样「没能真的停下来」时不会立刻再次触发——与 Swift 版一致。

### 9.3 无音频/无设备环境的注意

桌面端录音链路依赖真实音频设备；本机设备存在（CLI `devices` 能列出 4 个输出、3 个麦克风），
但连续多次「启动录音→结束」的自动化测试里出现过一次进程直接消失（无托管异常日志，疑似原生层
崩溃）。后续在有空音频线与稳定环境的机器上，应把「开始/结束记录 20 次」加入稳定性用例。

Core 测试总数 38，全部通过；基线 79 项测试继续通过。

## 10. 第六轮：结构化纪要的两道关（0.11.0 / 0.12.0）

### 10.1 先能解析，再谈好看

模型返回的 JSON 不会规规矩矩：外面套一层 ```` ```json ````、末尾多一个逗号、原因写成纯字符串、
整份塞在 `{"fishbone": …}` 里、时间写成 `"12:41"` 或 `761` 或直接写进正文的 `"[05:20]"`。
`StructuredNotesParser` 一律容忍，缺字段就当没有——**宁可少画，不要画错**。

### 10.2 时间必须在转写里有依据

只有解析不够：模型很容易编一个看起来合理的时间点。`StructuredNotesCleaner` 与模板纪要用同一个
`NotesVerifier.isSupported` —— 时间点在会议时长 +90 秒内，且 45 秒内有一条转写，否则整条丢弃。
剩余不足两条就整份不画，状态栏直接说「缺少可核对的转写依据」。

这条规则在演示数据上是个例外：演示的鱼骨图没有对应转写，所以演示路径不经过核对，只在「生成」时核对。

### 10.3 鱼骨图为什么先画脊线

`FishboneView` 用 Canvas 手工排：一条脊线、上下交替的肋骨、右侧带头部的问题，每条原因贴在肋骨旁。
不用绘图库，是因为文字要能换行、能点、能用与正文相同的字体；代价是位置由估算的槽宽决定，
类别超过 6 个（Core 已限制为 6）会变拥挤。这条取舍写在 `PORTING_STATUS.md` 的未完成项旁边。

## 11. 第七轮：系列知识库与合并（0.20.0）

### 11.1 「系列」承担 Mac 的 Workspace 角色

Mac 的 `Workspace` 既是场景（模式、材料、回答要求），也是知识的容器；会议通过 `workspaceID` 归属。
Windows 版此前把同名概念做成了「系列」（会议分组 + 一句话经验），两者不是一一对应。

本轮的选择：**知识挂在系列上，字段名与 Mac 的 Codable 对齐**
（`sourceSessionID`、`sourceSessionIDs`、`answerRevision`、`knowledgeCandidates` 等），
`seriesItems`（决定/待办）与 `SeriesBriefing` 暂不移植。这样旧 `workspace.json` 能读、
与 Mac 侧文件字段名也能对上；代价是「一个会议只能属于一个系列」，与 Mac 的 workspace 语义仍有差距，
记在 `CORE_PARITY.md` 的 Workspace 行。

### 11.2 候选必须经过确认才生效

模板里早就有 `NotesCarry.Lesson/Bank`，但一直没人消费。现在 `NotesKnowledge.Extract` 把带这些标记的
章节转成经验与题库条目，`KnowledgeMerger.CollectCandidates` 只入队、不写库；系列页的
「待确认」页签逐条 采纳 / 替换回答 / 忽略，`ApplyCandidate` 返回回执并写回 `workspace.json`。

三条硬规则来自 Swift，测试逐条覆盖：

- 候选 id 由内容决定：同一份纪要重复整理不会重复入队，回执也随候选保留；
- 替换答案要求 `baseQuestionID` + `baseAnswerRevision` 仍然匹配，题库改过就拒绝并提示重新整理；
- 旧答案进 `answerHistory`，`answerRevision` 加一，来源计数只在真的新增会议时加 `timesAsked`。

### 11.3 稳定 id 与时间的近似

`KnowledgeMerger.StableDigest` 与 Swift 完全一致（FNV-1a 正反两遍、32 位十六进制）；
版本 id 用「SHA-256 前 16 字节 + UUID v5 位」派生，同一内容得到同一 id。
唯一不逐位一致的是时间：Swift 取 `Date.timeIntervalSince1970` 的 Double 位模式，
C# 侧用毫秒换算成 Double 再取位；这只影响 id 的输入，不影响同一台机器上的稳定性。

### 11.4 两个界面坑（Avalonia 12 / Fluent）

1. **TabItem 字号**：Fluent 的页签标题比本页其余字号大一倍多；在 `SeriesView` 里局部覆盖
   `TabItem` 与 `/template/ ContentPresenter` 的 `FontSize`，才回到 13。
2. **自定义按钮模板里字符串内容不可见**：`Button.primary` 的模板用 `ContentPresenter` 显示文字，
   `Content="记下"` 这种字符串在该模板下渲染成「黑底黑字」。改成内嵌
   `<TextBlock Foreground="{DynamicResource OnInk}">` 显式给前景色即可（同页的「采纳」按钮同样处理）；
   旁边 `Content="替换回答"` 的 ghost 按钮不受影响。

### 11.5 离线自检不碰模型

`--knowledge-check=<json>` 只用模板内置的样例回复，走完
解析 → 抽取 → 入队 → 采纳 → 落盘 → 重新加载 → 提示词包含经验 的全链路，
最后删掉自检系列；10 项断言全过（`qa\knowledge-0200\knowledge.json`）。
这样验收知识库不需要 API key，也不会往用户数据里留测试会议。

## 12. 第八轮：逐字同传（0.21.0）

### 12.1 协调器留在 Core，网络与界面都不进去

`LiveTranslationRevisionGate` 与 `LiveTranslationPartialThrottle` 直接照 Swift 移植；
但「每个 partial 要不要发请求、结果回来给谁」如果写进 `AppState`，就只能靠真实 API 才测得了。
所以加了 `LiveTranslationCoordinator`：调用方喂 partial 快照（带单调时间），拿到请求描述去发；
结果回来调用 `Complete(revision, text)`，过期/乱序的直接丢。网络与 UI 都在外面，
Core 测试能覆盖乱序完成、限流、换通道、整句作废这些真正容易出错的地方。

### 12.2 限流用固定间隔，不用 debounce

长句子如果一直 debounce，会一直不发请求；Swift 的写法是「每 0.7 秒最多一次」。
`Observe` 里每个新快照都会 `Revise`（拿到新 revision），但只有 throttle 放行时才发请求，
所以「被限流的那一版」也有 revision，晚到的结果仍然过不了 `Accept`。

### 12.3 整句落地时的边界

final 一到就 `Invalidate`：在途的 partial 译文全部作废，字幕条改用 `transcript.jsonl` 里的整句译文；
提词岛保留最后一句译文（不清空），否则两句话之间会闪一下空行。
`--demo --island` 用固定译文验收这一行，截图见 `qa\shots\v21-island-translation.png`。

### 12.4 浮窗截图要按标题找窗口

`PrintWindow` 只会拿到主窗口；提词岛 / 字幕条是独立窗口，`MainWindowHandle` 指不到。
`tools/shot.ps1` 新增 `-WindowTitle`：用 `EnumWindows` + `GetWindowText` 找可见窗口，
这次 980×269 的提词岛截图就是这么来的。

## 13. 第九轮：双屏演讲的窗口规则（0.22.0）

### 13.1 先做能测的那一半

Mac 的 `PresenterMode` 有两块：窗口规则（岛在哪、页面藏不藏、幻灯片屏不出现窗口）和
「读幻灯片屏 + 按页匹配讲稿」。后者依赖 `LiveInsights` 的页面匹配，Windows 还没移植；
本机也只有一块活动显示器（WMI 里挂了三块面板，但只有一个显示路径）。

所以这一轮把窗口规则抽成 Core 的 `PresenterLayout` 纯函数，布局用例覆盖一块 / 两块 / 多块屏、
指定屏、指定屏不存在、内置屏与主屏的优先级；窗口副作用用 `--presenter-check` 注入**合成第二块屏**验证，
真实单屏则断言「拒绝启用」。这样做的边界写清楚了：合成屏证明的是代码路径，不等于投影仪实测。

### 13.2 屏幕标识用原点，不用序号

Avalonia 12 的 `Screen` 没有 `DisplayIndex`，所以设置里保存的 `presenterSlidesScreen` 是
`display:<X>x<Y>`（显示原点）。同一套拓扑下稳定，换接口/换摆放会变，但失效时会落回「第一块非摄像头屏」，
不会把窗口放错地方。摄像头屏用「主屏」代表内置屏——Avalonia 也拿不到「内置面板」这个属性。

### 13.3 一个被自检抓到的老 bug

`Window.Hide()` / `Show()` 会**再次触发 `Opened`**。提词岛和「一键隐藏」本来就会走这条路，
于是每次恢复窗口都会重跑 `InitializeAsync` 和 `SetupTray`（托盘图标会越积越多）。
自检第二次运行时才暴露出来；现在 `Opened` 只执行一次，托盘也只建一次。

顺带修了退出：关闭主窗口时如果提词岛还开着，`OnLastWindowClose` 不会触发，进程会留在后台。
现在 `OnClosing` 先关浮窗，再调用应用生命周期的 `Shutdown()`。

### 13.4 幻灯片截图留给下一轮

`showsSlideNotes`、`insights.screenWatch`、`LiveInsights` 的页面匹配都还没接；
设置页那行说明了这一点。下一轮做伴侣中转时，这条路仍然需要先移植 `LiveInsights`。

## 14. 第十轮：伴侣中转（0.23.0）

### 14.1 中继协议只有四个动作

`CompanionRelay`（Node）只做这些事：`POST /rooms` 建房、WSS `/socket` 加入、转发 `state`/`command`/`ack` 三种
密文帧、`POST /rooms/<id>/close` 关房。房间 8 小时过期、最多 100 间、每间 6 个 socket、25 包/秒。
桌面端因此不需要把状态机做大：断线重连、只发最新一帧、命令去重就够。

### 14.2 加密逐字节照搬 Swift

HKDF 的 salt 是房间号、info 是 `ih-companion-v1/<kind>`，AES-GCM 的 AAD 是 `房间号/kind`，
`body = ciphertext || tag`、nonce 12 字节 base64。测试先用 RFC 5869 的官方向量钉死 HKDF，
真正的跨语言验证放在端到端自检里：C# 加密的状态由 Node 的 `crypto.hkdfSync` + `createDecipheriv` 解开，
Node 加密的命令再由 C# 解开。这比两边各自写一份「自洽」的测试更能说明问题。

### 14.3 Windows 上起本地中继的坑

`CompanionRelay/src/server.ts` 只在 `process.argv[1]?.endsWith('/server.js')` 时监听，
Windows 的路径是反斜杠，永远不匹配，所以 `node dist/server.js` 什么都不做。
本地自检用 `qa/relay-host.mjs` 直接 `import { createRelay }` 再 `listen`；生产走 Docker/`npm start` 不受影响。

### 14.4 局域网与中转共用一套动作与去重

两条通道都走 `HandleCompanionActionAsync`，命令 id 各自去重（Mac 也是「跨通道去重」在应用层）。
中转失败不影响局域网：设置里留空中转地址就仍然只开局域网；两者都没配时给出明确提示，而不是假装开启。

### 14.5 蓝牙为什么只标记、不实现

Windows 的 `GattServiceProvider` 广播时不能自定义名字，而手机端仍按 `IH-` 前缀过滤（读代码所得，
未实机确认）。按方案，先在设置页与文档里标为不可用，等手机端改成按服务标识扫描后再接；
这样不会让用户拿到一个「开关打开但连不上」的通道。

## 15. 第十一轮：应用内更新（0.24.0）

### 15.1 为什么用安装包，而不是替换目录

Mac 版直接替换 `.app` 再重启；Windows 上运行中的 exe/dll 被占用，自己搬文件很容易留下半新半旧的状态。
Inno Setup 的升级流程已经处理「关闭旧进程、覆盖文件、保留数据目录」，所以应用内更新只负责
**把正确的安装包下载下来、校验、交给安装器**。

### 15.2 信任根是发布里的 SHA-256

下载后按发布附带的 `<setup>.sha256`（或 `SHA256SUMS.txt`）校验，失败就删除文件并报错。
安装包目前没有 Authenticode 签名，所以这一步只保证「下载没有损坏/被替换成别的字节」，
不保证来源身份——发布源仍是 GitHub。这条写进了 README 的限制，等签名流水线就绪后再加一层。

### 15.3 自检要能反复跑

真的启动安装器会把当前构建替换掉，自检就没法重复。`NEXTTHOUGHT_UPDATE_LAUNCH` 存在时，
`InstallUpdateAsync` 把 `installer`、`sha256`、静默参数写进标记文件就返回，不启动安装器；
自检报告据此判断「下载 + 校验 + 命令」三步是否都完成。反例（校验值被篡改）会得到
`passed=false`、`installerExists=false`，下载文件已被删除。

### 15.4 一条容易写错的判定

第一版自检用「标记文件存在 + 报告可安装」判定通过，结果篡改校验值的反例也报了 passed：
上一轮留下的标记文件还在。改成由 `UpdateInstallSucceeded` 显式记录本次是否成功，
并在自检开始时删掉旧标记——这类「跨运行残留」是自检脚本最常见的假阳性来源。

## 16. 第十二轮：内存目标为什么没到（0.25.0）

### 16.1 先量，再改

`--startup-check` 新增 `privateMiB`、`gcHeapMiB`、`gcCommittedMiB`、`threads`。第一次测出来：
工作集 166–171 MiB、私有 186–190 MiB、**托管堆只有 15 MiB**、线程 118。也就是说，
应用自己分配的对象不是大头；改 GC 参数不会有多少空间。

### 16.2 逐项试过，逐项回退

| 尝试 | 结果 | 处理 |
| --- | --- | --- |
| GC `ConserveMemory=5`、`RetainVM=false`、`ServerGC=false` | 工作集 ±1 MiB，和默认值一样 | 只保留 workstation/concurrent 的显式声明，其余删除 |
| 去掉 `Avalonia.Fonts.Inter` | 工作集约 −4 MiB，但拉丁字形改走系统字体 | 回退（视觉一致性优先） |
| 延迟音频设备/进程枚举 | 启动快约 0.2 秒，私有提交反而 +30 MiB；保存的设备选择要额外处理 | 回退 |
| `PublishTrimmed`（partial） | 启动即崩（0xC0000409 / 无启动报告） | 回退 |
| 空闲 `EmptyWorkingSet` | 工作集报成 0.8 MiB，私有不变，下次交互全部缺页 | 删除 |

### 16.3 为什么拒绝 0.8 MiB

把工作集裁剪后报告 0.8 MiB 可以让「目标 150 MiB」在数字上成立，但那是把页面换出去，
不是减少占用；用户一操作就要全部换回来。测量值必须能代表真实占用，所以这条不做，
README 里直接写「未达到 150 MiB」和三个真实数字。

### 16.4 后续方向

真正下探需要换 UI 技术栈或 Native AOT；本机没有 MSVC 生成工具，Native AOT 装不上。
先把测量留在自检里，等工具链或框架升级后再做专项，不在功能轮里反复折腾。

### 16.5 0.88.0：软件渲染把目标做实了

Avalonia 默认走 GPU 合成。试了同一个二进制加 `NEXTTHOUGHT_SOFTWARE_RENDER=1` 走 Skia 软件渲染：
空闲工作集 **121.7 MiB**、私有 **52.0 MiB**、线程 **29**；GPU 同条件下 140.6 / 100.9 / 51。
页面全部访问后软件渲染 149.5 MiB，仍在 150 MiB 以内，启动也更快（3.6 秒 vs 负载下 15 秒）。

这个应用以文字为主、动画少，软件渲染没有可见差异（主页与纪要页截图已验证），因此 0.88.0 起
**默认软件渲染**，`NEXTTHOUGHT_GPU_RENDER=1` 可切回 GPU 排查显示问题。目标 150 MiB 由此达标，
不再是「未达到」。

## 17. 第十三轮：资料术语进热词，顺带修一个「热词没生效」（0.28.0）

### 17.1 术语表原来只对导入有效

把 `MaterialTerms` 接进识别链路时先查了热词怎么走，发现：
实时会议的 `new LiveSpeechEngine(Models, settings)` 只把 `settings.Vocabulary` 交给识别器，
`GlossaryHotwords()` 只出现在「导入 WAV」和「校对提示词」两处。
也就是说 **术语表对实时会议一直没生效**——README 却写着「识别时作为热词」。
修法：`LiveSpeechEngine` 增加可选的 `extraHotwords`，开始记录时传 `RecognitionHotwords()`；
导入路径也改用同一个方法。

### 17.2 合并顺序与缓存

`RecognitionHotwords()` = 术语表（人手维护，优先）+ 资料术语（自动抽取），大小写不敏感去重。
资料术语最多 24 个，按「文档 sha + 块数」做指纹缓存；导入新资料时 `RefreshMaterialTerms()`
清空缓存，下一场会议才重新抽。
`MaterialTerms` 只取更像术语的 token（全大写缩写、词中有大写、字母夹数字），并且
「只出现一次的术语在有重复术语时会被丢掉」——这条是照 Swift 的，测试里也有对应断言。

### 17.3 自检怎么证明

`--hotwords-check` 不碰麦克风：写一份含 XL-MIMO/TSIR/DeepMIMO/RQ2 的 txt → `Library.ImportAsync`
→ 再写术语表 Kubernetes → 读 `RecognitionHotwords()`。实测得到
`[Kubernetes, XL-MIMO, TSIR, DeepMIMO, RQ2]`，既证明资料抽取，也证明与术语表合并。
真实识别是否因此更准，需要后续用带术语的音频做对照，这里只证明「热词确实进了识别器」。
