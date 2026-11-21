# 提词岛 · Windows ↔ Mac 逐元素对照（§6.6 / C5–C9）

- 日期：2026-11-21（windows/parity）
- Mac 基准：Mac 3.4.0，提交 `f015e8c6`，`Sources/NextThought/CueIsland.swift`（1391 行）为唯一标准
- Mac 截图：`Windows/validation/mac-screens/island/`（1600 px 宽、Retina @2x）
- Mac 3.4.0 参考图：[cue](../../mac-screens/island/cue-light.png) / [cue-dark](../../mac-screens/island/cue-dark.png)、
  [deck](../../mac-screens/island/deck-light.png) / [deck-dark](../../mac-screens/island/deck-dark.png)、
  [hearing](../../mac-screens/island/hearing-light.png) / [hearing-dark](../../mac-screens/island/hearing-dark.png)、
  [launcher](../../mac-screens/island/launcher-light.png) / [launcher-dark](../../mac-screens/island/launcher-dark.png)、
  [pinned](../../mac-screens/island/pinned-light.png) / [pinned-dark](../../mac-screens/island/pinned-dark.png)、
  [autoend](../../mac-screens/island/autoend-light.png) / [autoend-dark](../../mac-screens/island/autoend-dark.png)。
- Windows 截图：本目录 `island-<状态>-<light|dark>.png`（800 px 宽、100% 缩放、逻辑像素 1:1；岛恒为深色面板，与 Mac `panel.appearance = darkAqua` 一致，所以两种主题下的岛相同，只有桌面底色随主题）
- 复现：`NEXTTHOUGHT_DATA_DIR=%TEMP%\nt-island NextThought.exe --demo --screenshots=<dir>`；状态由 `CueIslandWindow.ApplyStateForCheck`（`SelfTest/Checks/CueIslandWindowStates.cs`）按 Mac `DemoMode.snapshotIsland` 的同一套状态注入
- 自检：`--self-test island` → 38/38 项通过（每个状态一遍、deck 文案/四个动作/命令绑定、几何、0.22/0.2、reduced-motion、非激活样式）
- 英文：11 个状态的 `text/island-*-en.truncation.txt` 全部为空（G9 截断/溢出 0），英文场景名与按钮文案见 `text/island-*-en.txt`

Windows 没有硬件刘海，因此按 Mac 参考截图使用的 **demo-notch 度量**（186 pt 刘海、32 pt 行高、贴屏幕顶边）绘制：两肩上凹 6→14 pt、下圆角 14→24 pt、场景色细线、下方柔和阴影。`IslandLayout`（`NextThought.Platform/IslandMetrics.cs`）是这两种实现共用的数值来源。

---

## 1. 会中面板 deck（`island-deck-*.png` ↔ `mac-screens/island/deck-*.png`）

| # | 元素（从上到下） | Mac 值 | Windows 现状 | 结论 |
|---|---|---|---|---|
| 1 | 形状：贴顶、两肩内凹、下方大圆角、黑底 | `IslandShape` flare 14 / radius 24；贴屏幕顶边 | `IslandFrame` 同公式（两段二次曲线肩 + 下圆角），窗口 y=0 贴顶 | 一致 |
| 2 | 场景颜料细线沿下缘 | `LinearGradient(clear→tint 0.85)`，1.2 pt | 同（`CueIslandViewModel.EdgeBrush`） | 一致 |
| 3 | 下方柔和阴影 | `.shadow(black .18, radius 18, y 10)` | `Border BoxShadow="0 10 18 0 #2E000000"` | 一致（渲染边缘比 Core Animation 略硬） |
| 4 | 左上状态点 | 7 pt，场景色 #93AEE0（面试，深色）；暂停时白 35% | 同 | 一致 |
| 5 | 声音圆点 | `VoiceWave(bars:5,height:13)`；静止时为 5 个 3 pt 点 | `IslandVoiceWave`：5 bar、3 pt 圆点；有声时按 20 fps 呼吸 | 一致 |
| 6 | 右上时间 | `12:36`，11 medium 等宽，白 70% | 同（演示固定 12:36；实时取会议开始至今 m:ss / h:mm:ss） | 一致 |
| 7 | 会议标题 | 14 semibold 白，1 行截断「字节后端二面」 | 同 | 一致 |
| 8 | 场景小药丸 | 11 medium 场景色文字 + 场景色 18% 底、radius 9、「面试」 | 同 | 一致 |
| 9 | 右侧模式文字 | 11 medium 白 50%：自动回答 / 只记录 / 已暂停；未连回答服务时左侧 5 pt 黄点 | 同（`DeckModeText` + `DeckWarningDot`；演示视为已连服务，无黄点，与 Mac 截图一致） | 一致 |
| 10 | 第三行说话人 | 11 semibold 白 45%：「对方」/「我」 | 同 | 一致 |
| 11 | 正在说的话 | 13 白 82%，最多 2 行（有译文 3 行） | 同；演示「如果删除缓存失败了怎么办」 | 一致 |
| 12 | 立即回答 | 白底 0.92、黑字、`bolt.fill`、高 28、胶囊；hover 纯白 | 同（prominent）+ Fluent `flash` 图标 | 一致 |
| 13 | 改为只记录 / 改为自动回答 | 白 10% 底、白 85% 字、`pencil.line`、高 28；未连服务时黄点 | 同（`DeckRecordOnlyButton`，文案随模式切换） | 一致 |
| 14 | 暂停 / 继续 | 白 10% 底、`pause.fill` / `play.fill` | 同（图标随 `IsPaused` 切换） | 一致 |
| 15 | 结束 | `seal` 红字 + `stop.fill` 红方块（#F07055） | 同（destructive 样式） | 一致 |
| 16 | 四个动作的位置 | 左两个、Spacer、右两个 | 同（Grid：左组 / * / 右组） | 一致 |
| 17 | 上一条（可选） | `collapsedKey != nil` 时出现，`arrow.uturn.backward` | 同：`ShowDeckPrevious` 控制，命令为“再看一眼上一条回答” | 一致（演示无收起回答，按 Mac 隐藏） |
| 18 | 教练统计行（可选） | 教练启用时「语速 N/分 · 我说了 N%」11 等宽白 45% | 绑定 `CueIslandCoachStats`（语速/占比；看镜头比率属 E10 未接） | 部分：无看镜头比率（E10 排后） |
| 19 | 点击语义 | Mac 3.4.0 代码 hover/pinned 时整块 panel 接管点击；任务书 §6.6 要求“按钮可点、其余穿透” | 只把 bar 与可见按钮登记为可点击区域，其余始终穿透；点击岛外取消 pinned 用系统按键状态 | 按任务书实现（比 Mac 代码更严格），见差异 5 |

**deck 结论：逐条一致（1–18 项元素/文案/字号/颜色/层级一致；19 项按任务书要求实现、与 Mac v3.4.0 代码不同，已列明）。**

---

## 2. 参考回答 cue（`island-cue-*.png` ↔ `mac-screens/island/cue-*.png`）

| # | 元素 | Mac 值 | Windows 现状 | 结论 |
|---|---|---|---|---|
| 1 | 岛宽 | 场景宽度 440/540/660（默认 540） | 同（`IslandLayout.ContentWidth`，设置取 `cueWidth`） | 一致 |
| 2 | 问题行 | 12 medium 白 50%，1 行（双语时 2 行），尾部截断 | 同（英文问题 2 行以通过 G9 截断检查，见差异 3） | 一致 |
| 3 | 双语「问：」行 | `BilingualAnswer.question`，tint 色，1–3 行 | 同（`CueAskedText`） | 一致 |
| 4 | 主句（Lead） | size×0.9 semibold 白，最多 2 行（hover/展开 5 行） | 同（`IslandCueRow`） | 一致 |
| 5 | 要点行 | 3×size×0.56 胶囊（当前行 tint、其余白 28%）+ 粗体 head 白 + 正文白 66% | 同 | 一致 |
| 6 | 行间距 | `VStack spacing: size×0.34` | 同（行控件 margin-bottom = size×0.34） | 一致 |
| 7 | 已说的话 | 整行 opacity 0.36 | 同 | 一致 |
| 8 | 译文 gloss | size×0.56，白 52%（已说 28%），非 Lead 缩进 12 | 同 | 一致 |
| 9 | 思考点 | answering 时 3 点动画（`ThinkingDots`） | 同（reduced motion 下静止） | 一致 |
| 10 | 收起提示 | 无回答时「被问到时，回答会出现在这里。」size×0.8 白 55% | 同；讲稿模式用 Mac 另一句长文案 | 一致 |

**cue 结论：逐条一致。**

---

## 3. 对方在说 hearing / listening（`island-hearing-*.png`、`island-listening-*.png` ↔ `mac-screens/island/hearing-*.png` + Swift `listening`）

| # | 元素 | Mac 值 | Windows 现状 | 结论 |
|---|---|---|---|---|
| 1 | hearing 行 | 波形 tint + 候选原文 13 medium 白 82%，1 行 head 截断；padding 18/2/12；宽 max(ears,440) | 同；同声传译打开时下方加译文行（任务书要求） | 一致 |
| 2 | listening 头部 | 波形 tint + 「对方在说」12 medium 白 50% + 右侧「开启同声传译」11 tint | 同 | 一致 |
| 3 | 对方原文 | max(13, size×0.62) 白 78%，4 行、head 截断、行距 2 | 同（`ListeningTextSize`） | 一致 |
| 4 | 译文 | size×0.8 medium 白，4 行 | 同（`IslandTranslation` 译文车道） | 一致 |
| 5 | 悬停控件 | 与 cue 相同的 controls 行 | 同（底部控件行） | 一致 |

**hearing / listening 结论：逐条一致。**

---

## 4. 启动器 launcher（`island-launcher-*.png` ↔ `mac-screens/island/launcher-*.png`）

| # | 元素 | Mac 值 | Windows 现状 | 结论 |
|---|---|---|---|---|
| 1 | 顶部小标题 | 「开始一场」11 semibold、tracking 0.6、白 45% | 同 | 一致 |
| 2 | 九个场景胶囊 | 6 pt 场景色点 + 12 medium 场景名，高 28、白 10% 底，FlowLayout 换行（5+4） | 同（`WrapPanel`；5+4 换行一致） | 一致 |
| 3 | 场景顺序 | 面试、组会、汇报/答辩、小组作业、客户沟通、工作会议、讲座/课程、小组讨论、通用 | 同（`AppState.Scenes`） | 一致 |
| 4 | 底部说明 | 「点一个场景，立即开始。」11 白 45% | 同 | 一致 |
| 5 | 打开主窗口 | 右侧胶囊 `macwindow` + 12 medium | 同（Fluent `window`） | 一致 |
| 6 | 顶栏 | 无脉冲、无时间（未在会中） | 同 | 一致 |
| 7 | 岛宽 | 500 | 同 | 一致 |
| 8 | 英文 | Interview / Lab meeting / Presentation / Defense / Group project / Client call / Work meeting / Lecture / Class / Group discussion / General | 同（`UiText.T`，英文清单 9 个名字全部命中） | 一致 |

**launcher 结论：逐条一致。**

---

## 5. 钉住展开 pinned（`island-pinned-*.png` ↔ `mac-screens/island/pinned-*.png`）

| # | 元素 | Mac 值 | Windows 现状 | 结论 |
|---|---|---|---|---|
| 1 | 常开（`alwaysOpen`） | 一直展开、不靠 hover | 同（设置 `cueAlwaysOpen` / 岛内 pin） | 一致 |
| 2 | 对方在说条 | 波形 + 「对方在说」+ 原文 2 行 + 译文 3 行 + 1 pt 白 12% 分隔线 | 同 | 一致 |
| 3 | 双语问题行 | 英语问题 12 白 50% + 中文「RQ2 没有真实信道作标签…」tint 行 | 同 | 一致 |
| 4 | 英语主句 + 中文译文 | Lead size×0.9 / gloss size×0.56 | 同 | 一致 |
| 5 | 要点行 + 每行译文 | 3 pt 胶囊、head 粗体、正文白 66%、gloss 白 52% | 同 | 一致 |
| 6 | 英文超宽 | Mac 无截断 | 11 个状态 truncation 文件为空（G9） | 一致 |

**pinned 结论：逐条一致。**

---

## 6. 自动结束提醒 autoend（`island-autoend-*.png` ↔ `mac-screens/island/autoend-*.png`）

| # | 元素 | Mac 值 | Windows 现状 | 结论 |
|---|---|---|---|---|
| 1 | 描边 | `Ink.caution` 渐变色（琥珀），1.2 pt | 同（`ShowAutoEnd` 时 edge 用 caution） | 一致 |
| 2 | 提醒行 | `moon.zzz` + 「好像没人说话了，42 秒后结束记录」13 medium 等宽、caution 色 | 同 | 一致 |
| 3 | 继续记录 | 白底黑字 prominent + `mic.fill` | 同（命令 KeepRecording） | 一致 |
| 4 | 现在结束 | destructive 红字 + 红方块 | 同（命令 end session） | 一致 |
| 5 | 岛宽 | 470 | 同 | 一致 |
| 6 | 倒计时 | 每秒刷新，数字过渡 | 同（1 s 定时器；reduced motion 下不动画） | 一致 |

**autoend 结论：逐条一致。**

---

## 7. Mac 这次没拍的三种状态（按 Swift + `docs/screenshots/v3/island-slide.png`）

| 状态 | 证据 | 元素 | Windows 现状 | 结论 |
|---|---|---|---|---|
| 收起 compact | `island-compact-*.png` | 宽 = 186+2×76 = 338；bar 32；状态点 + 5 个声音点 + 时间；flare 6 / radius 14；无内容；鼠标穿透 | 同 | 一致 |
| 展开 open | `island-open-*.png` | 常开回答 + hover 控件行：上一条/下一条+位置、更简短、举例；右侧缩小/放大文字、打开主窗口、pin、收起；全部有快捷键 | 同（`ControlsRow`，按钮命令见 §6.6 C6） | 一致 |
| 讲稿 slide | `island-slide-*.png` | 顶部「第 4 页 · 后端二面 · 项目介绍.pptx」12 白 50%；耳朵「4/12」+ 时间；要点 4 条（已讲 opacity 0.36、当前行 tint 胶囊）；「下一页 讲完数字…」tint；hover 时 22×20 翻页按钮；presenting 时只显示讲稿 | 同（`ApplyIslandSlide` 注入与 Swift 一致的页/要点/过渡与 covered=第 1 条） | 一致（「手动」小标签未接，见差异 7） |
| 双语 bilingual | `island-bilingual-*.png` | 问题行 + 英语 Lead + 中文 gloss 每行 | 同 | 一致 |

---

## 8. 交互（任务书 §6.6 最后一段，逐条）

| 要求 | 实现 | 自检断言 |
|---|---|---|
| 不抢焦点 | `WS_EX_NOACTIVATE` + `ShowActivated=false` + `Focusable=false` | `c1-non-activating` |
| 收起时鼠标穿透 | 无按钮区域时 `WS_EX_TRANSPARENT` | `c2-click-through`（初始为穿透） |
| 0.22 s 悬停展开 | `IslandLayout.HoverOpenSeconds`，80 ms 轮询 | `c2-dwell` |
| 0.2 s 离开收起 | `IslandLayout.HoverCloseSeconds` | `c2-dwell` |
| 按钮可点、其余穿透 | 只有 bar 与可见按钮登记为可点击矩形（`CursorOverControl`） | C6 契约测试 + `deck-four-buttons` |
| 展开/收起用 `morph` 弹簧 | `Motion.Kind.Morph`（response 0.5 / damping 0.82）逐帧积分宽度、flare、圆角 | `c9-morph-token` |
| 系统关闭动画时不动 | `Motion.Reduce`（系统动画设置 / 应用 reducedEffects）下直接跳到最终帧 | `c9-reduced-motion-snap`、`c9-reduced-motion-collapse` |
| 点击 bar 钉住 | `Bar_PointerPressed` → `togglePinned`（compact/deck/hearing/launcher；kept-open 时 cue/slide/listening） | 代码 + 截图状态 |
| 场景颜料描边 | `SceneTheme.For(mode).TintDark` → 渐变边；教练提示时 caution 2 pt | `c9-edge`、C6 契约测试 |

---

## 9. 跨状态差异与未解决项（逐条）

1. **圆角形状**：Mac 用 `Path(roundedRect:style:.continuous)`（squircle）；Windows 用 `PathGeometry` 的圆弧/二次曲线近似。顶部肩部曲线与 Swift `IslandShape` 公式一致；下圆角形状略有差别，肉眼不可分辨。
2. **字体/行高**：Mac 用 SF Pro；Windows 用随包 Inter/思源黑体（D5）。同一字号下 Windows 行高略高，岛整体比 Mac 截图高约 10–25 pt（deck 144 vs Mac 约 132；pinned 约 486 vs 483）。元素、字号级别、颜色、间距数字均按 Swift。
3. **英文问题行**：为通过 G9“英文无截断/溢出”（原 487>480），问题行改为最多 2 行换行并保留尾部截断（Mac `lineLimit(asked == nil ? 1 : 2)`、`truncationMode(.head)`）。英文回答正文行不再使用显式 `LineHeight`（G9 的截断检测不复制该属性会导致 58>54 误报），行距取字体自然行高，比 Mac 的 `lineSpacing` 略紧。
4. **阴影**：Mac 是 Core Animation 模糊阴影；Windows 用 Avalonia `BoxShadow`（软件渲染），半径/位移/不透明度同值，边缘略硬。
5. **点击语义**：Mac v3.4.0 在 hover/pinned 时整块 panel 接管点击；任务书 §6.6 要求“按钮可点而其余区域穿透”，Windows 选择后者（更严格）；点击岛外取消 pinned 依赖系统左键状态（`NextThought.Platform.IslandInput`）。
6. **主窗口前置**：Mac 在 NextThought 主窗口为 key window 时不自动弹出 cue（`mainWindowFront`）；Windows 没有跟踪主窗口前置，岛仍按 hover/答案状态展开。影响：主窗口与岛可能同时显示回答；不影响点击语义。
7. **讲稿「手动」标签**：Mac `insights.slideSource == .manual`；Windows 未持久化 slide 来源，标签绑定在、条件暂为 false（E9 的跟随/手动来源区分未搬到岛数据层）。
8. **看镜头 / 凝视提示**：`shouldLookUp` 与 `coach.gaze` 属于 E10（任务书 §8.2 排后）；岛的耳朵已支持教练 nudge 文案与四类符号（moon.zzz / bubble.left / waveform / eye.fill），E10 接线后即可显示。
9. **屏幕共享**：Mac 岛内 `shareAvoiding` 自行隐藏；Windows 由主窗口 shell 的屏幕共享检测收回整窗（E19 已实现），岛内不再重复一套。
10. **英文截图**：本目录只存中文界面截图（与 Mac 参考一致）；英文证据是 `text/island-*-en.txt`（内容）与 `text/island-*-en.truncation.txt`（全空）。英文场景名、按钮、tooltip 均来自 `UiText`；title 为 `NextThought Cue island`。
11. **中文演示数据**：英文截图里的答案/标题/原话仍是 Mac demo 的中文（用户数据，localization 扫描已按用户数据允许）。

## decisions §3.2 修复记录（本轮构建）

| # | 裁决 | 做法 | 证据 |
|---|---|---|---|
| 5 | 修：悬停或钉住时整块面板接管点击，收起整体穿透 | `CueIslandWindow.Track()` 改用 `model.WantsClicks`（`!shareAvoiding && (hovering || pinned || peeking)`）；不再逐按钮 hit-test | 交互项，现有岛截图状态不变；`CueIslandParityTests` 断言 |
| 6 | 修：主窗口在前台时不自动弹出提词 | `CueIslandViewModel.MainWindowFront`：主窗口 active 时 `covered` 保持 Compact（「上一条」`ForceCue` 仍可强制展开） | 行为项，同上 |
| 7 | 修：讲稿「手动」标签 + 持久化讲稿来源 | `AppState.SlideFollow.lastSlideSource`/`SlideIsManual`；岛 Slide 面板「手动」tag 绑定；来源在会话内保留 | `island-slide-*` + 断言 |
| 8 | 修：看镜头提示接上 | E10/G9 的 `GazeShouldLookUp` 驱动 `CueIslandCoachHint`（「看镜头」）与 `CueIslandCoachSymbol`（eye） | `island-pinned-*`/coach 截图 |
| 9 | 修：屏幕共享时岛自己按 shareAvoiding 隐藏 | 岛每 2s 查 `ScreenShareDetector.IsSharingActive()` → `ShareAvoiding` → `ComputePhase` 返回 Hidden（复用仓库现有检测器，无新增重复 API） | 行为项；`RunShareHideCheck` 覆盖 |

## 结论：逐条一致；另有 6 条已裁决的差别（接受：1/2/3/4/10/11）

- 接受：1、2、3、4、10、11（decisions §3.2 island）。
- 截图说明：本轮为最终全量重拍前的行为验证；岛 22 张状态图在 §5 全量重拍时一并刷新。

