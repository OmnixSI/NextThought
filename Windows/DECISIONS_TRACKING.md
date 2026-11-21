# 收尾决定执行跟踪（DECISIONS_TRACKING）

对应 `C:\Users\Thunderobot\Downloads\WINDOWS_FINAL_DECISIONS.md`（下称《决定》）§3 的逐页「修」条目。状态列：`未做` / `进行中` / `已修 <commit>`。
「接受」见 `ACCEPTED_DIFFERENCES.md`，「第二批」见 `KNOWN_GAPS.md`。

## §3.1 首页、外壳、组件

| 页面 | 条目 | owner | 状态 |
|---|---|---|---|
| home | 「开始」旁小进度点 | prepare-parity | 未做 |
| home | 编辑系列时推进 `updatedAt` | prepare-parity | 未做 |
| home | 内容区宽度与左右留白按 `HomeView.swift` 数值重量 | prepare-parity | 未做 |
| home | 输入框左边用当前场景图标 | prepare-parity | 未做 |
| shell | 错误横幅「重试保存」（与 live 9 一起） | island-parity | 未做 |
| shell | 会中窗口菜单做成三项选择并在菜单显示快捷键 | island-parity | 未做 |
| shell | 去掉窗口底部常驻文字条（演示模式/快捷键冲突） | island-parity | 未做 |
| components | `hover`/`snappy` 用到每个组件、`VoiceWave` 动 | island-parity | 未做 |
| components | 分段选择滑动块复用到组件 | island-parity | 未做 |
| components | `ScrollEdgeFade` 深色 | island-parity | 未做 |
| components | 公式和表格渲染（Core `LaTeXMath`） | island-parity | 未做 |
| components | 页面手写等价物全部换成组件（§6.1 硬性） | island-parity | 未做 |
| zoom | `textZoom` 并入偏好键注册表与 `SETTINGS_PARITY` | settings-parity | 未做 |
| zoom | 新内容立即按缩放显示（去 600ms 补缩放） | core-parity | 未做 |

## §3.2 提词岛和浮窗

| 页面 | 条目 | owner | 状态 |
|---|---|---|---|
| island | 5：悬停/钉住整块面板接管点击，收起整体穿透 | island-parity | 未做 |
| island | 6：主窗口在前台时不自动弹出提词 | island-parity | 未做 |
| island | 7：讲稿「手动」标签、持久化来源 | island-parity | 未做 |
| island | 8：看镜头提示接上（E10） | island-parity | 未做 |
| island | 9：屏幕共享时按 `shareAvoiding` 隐藏 | island-parity | 未做 |
| teleprompter | 1：「…」菜单补「先澄清」「追问」 | island-parity | 未做 |
| live-window-menu | 2：提示带快捷键；弹出层渲染到位图截图 | island-parity | 未做 |

## §3.3 会中

| 页面 | 条目 | owner | 状态 |
|---|---|---|---|
| live | 1 暂停/继续图标（补图标资源） | live-parity | 未做 |
| live | 2 回答服务未就绪黄点 | live-parity | 未做 |
| live | 4 原回答↔联网版切换（与 web-search 一起） | live-parity + core-parity | 未做 |
| live | 5 阴影用 Mac 值 | live-parity | 未做 |
| live | 6 「立即回答」高度 40、命中测试按 Mac | live-parity | 未做 |
| live | 7 焦点/选中状态键盘操作后截图 | live-parity | 未做 |
| live | 8 选中文字时固定滚动 | live-parity | 未做 |
| live | 9 归档保存状态行（保存中/受阻/未入队/重试） | live-parity | 未做 |
| live | 10 幻灯片区底色场景强调色 6% | live-parity | 未做 |
| live | 11 模型选择换 `MenuPicker` | live-parity | 未做 |
| live | 12 「我的笔记」逐元素对照 | live-parity | 未做 |
| live | 15 演示数据与 Mac 一致（最后一句未定稿、来源「Zoom · 后端工程师二面」、回声计数 1） | live-parity | 未做 |
| live | 16 虚线胶囊用虚线描边形状 | live-parity | 未做 |
| assistant | 空态说明/模块标题/预设名称 Mac 中文原句 | live-parity | 未做 |
| assistant | 检测条按阈值变色 | live-parity | 未做 |
| assistant | 候选问题队列 | live-parity | 未做 |
| assistant | 独立窗口气泡布局与尺寸按 Mac | live-parity | 未做 |
| assistant | 「合并回主窗口」Mac 原句 | live-parity | 未做 |
| insights | 1 | runtime-gaps | 未做 |
| insights | 2 逐页生成中转圈、出错橙色提示 | runtime-gaps | 未做 |
| insights | 3 已讲完变绿 | runtime-gaps | 未做 |
| insights | 5 去掉独立「屏幕与讲稿」入口 | runtime-gaps | 未做 |
| presenter | 2 读屏来源提供显示器和窗口列表 | island-parity | 未做 |
| presenter | 4 无备注时模型生成讲稿要点（生成中/清除缓存） | island-parity | 未做 |
| translation | 2 关闭开关立即清两路未定稿译文 | live-parity | 未做 |
| translation | 3 去掉自动打开提示、状态文案 Mac 原句 | live-parity | 未做 |
| translation | 6 | live-parity | 未做 |
| speakers | 2 只在说话人变化处显示名字 | runtime-gaps | 未做 |
| speakers | 3 会中页说话人条按 Mac | runtime-gaps | 未做 |
| speakers | 4 保存失败提示与重试 | runtime-gaps | 未做 |
| speakers | 5 标记用图标资源 | runtime-gaps | 未做 |
| screenshot-question | 图像作为模型输入（OpenAI image block / Codex 路径） | core-parity | 未做 |
| screenshot-question | 显示器友好名称 `QueryDisplayConfig`、逐显示器 | island-parity | 未做 |
| screenshot-question | ScreenRegionSheet 标题/提示/区域过小用 Mac 原句 | island-parity + localization | 未做 |
| screenshot-question | 去掉窗口选择器「刷新」按钮 | island-parity | 未做 |
| screenshot-question | 读屏用 `ScreenDelta` 帧去重/稳定等待/变化区域 | island-parity + core-parity | 未做 |
| web-search | 1 联网三态（`webSearchMode` 取值同 Mac） | core-parity + settings-parity | 未做 |
| web-search | 2 模型复核 3 秒超时、失败回退并记录 | core-parity | 未做 |
| web-search | 3 原回答↔联网版切换与按回答编号状态 | core-parity + live-parity | 未做 |
| web-search | 5 设置页分组按 Mac | settings-parity | 未做 |
| coach | 1 会中轻提示总开关 | live-parity + settings-parity | 未做 |
| coach | 2 灵敏度/校准仅开启后显示 | settings-parity | 未做 |

## §3.4 会后

| 页面 | 条目 | owner | 状态 |
|---|---|---|---|
| notes | 1–7 全部（结构化条目级编辑/确认、评价徽标、过期提示、读取问题条、归档状态条、AI 新版本、回答列表元数据、转写版本/查找/撤销、重点联动侧栏、我的笔记标签、空状态重试） | d-parity | 进行中 |
| MeetingNotesEditor | 1–4 + 重拍 5 | d-parity | 未做 |
| meeting-notes | 自动保存/离开保存/重试行/平铺重点（去 30 秒合并） | d-parity | 已修 d43f8226（其余待核对） |
| export | 2 + SessionExportSheet 2/3/4/5 | d-parity | 未做 |
| history | 2–8 全部 + 右栏内嵌会议页 | runtime-gaps | 进行中（内嵌页 823ad3e2） |
| knowledge-review | 1/2/3/6 + 4 打开可读全文 | d-parity | 已修 75b46a2e |

## §3.5 系列和资料

| 页面 | 条目 | owner | 状态 |
|---|---|---|---|
| series | 四条全部（左栏搜索/排序、行图标与统计顺序、三段标签、记录行时间与摘要） | runtime-gaps | 未做 |
| series-editor | 1–7 全部 | runtime-gaps | 未做 |
| library | 删除 Windows 独有资料页，并入系列详情 | runtime-gaps | 未做 |
| glossary | 1 间距 24/16；2 剪贴板预填；3 系列作用域显示条件 | island-parity | 未做 |
| skills | 行图标/摘要/系列计数；编辑弹层带取消；逐项勾选对照；去设置页「安装示例技能」 | settings-parity | 未做 |
| document-settings | 取消下载/准备、扫描本机组件与选目录、许可来源链接、多档模型、语义增强开关 | settings-parity | 未做 |

## §3.6 设置

| 页面 | 条目 | owner | 状态 |
|---|---|---|---|
| settings-structure | 4 条全部（记录每次会议开关、说话人与翻译移通用、语义检索只在高级一次、搜索关键词表） | settings-parity | 未做 |
| settings-pages | 12 处不一致按 §2 处理（位置/文案/缺项一律修） | settings-parity | 未做 |
| share-test | 1 设置页入口；2 配对二维码位置尺寸 | settings-parity | 未做 |
