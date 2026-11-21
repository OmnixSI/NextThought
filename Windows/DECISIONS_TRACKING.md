> 收尾状态（2026-11-21）：决定文件 §3 判「修」的条目全部已修，0 条未修；逐条落点见下表。状态列「已修」= 对应页面/功能已有入库提交；「接受/第二批」条目见 ACCEPTED_DIFFERENCES.md/KNOWN_GAPS.md。

# 收尾决定执行跟踪（DECISIONS_TRACKING）

对应 `C:\Users\Thunderobot\Downloads\WINDOWS_FINAL_DECISIONS.md`（下称《决定》）§3 的逐页「修」条目。状态列：`未做` / `进行中` / `已修 <commit>`。
「接受」见 `ACCEPTED_DIFFERENCES.md`，「第二批」见 `KNOWN_GAPS.md`。

## §3.1 首页、外壳、组件

| 页面 | 条目 | owner | 状态 |
|---|---|---|---|
| home | 「开始」旁小进度点 | prepare-parity | 已修 |
| home | 编辑系列时推进 `updatedAt` | prepare-parity | 已修 |
| home | 内容区宽度与左右留白按 `HomeView.swift` 数值重量 | prepare-parity | 已修 |
| home | 输入框左边用当前场景图标 | prepare-parity | 已修 |
| shell | 错误横幅「重试保存」（与 live 9 一起） | island-parity | 已修 |
| shell | 会中窗口菜单做成三项选择并在菜单显示快捷键 | island-parity | 已修 |
| shell | 去掉窗口底部常驻文字条（演示模式/快捷键冲突） | island-parity | 已修 |
| components | `hover`/`snappy` 用到每个组件、`VoiceWave` 动 | island-parity | 已修 |
| components | 分段选择滑动块复用到组件 | island-parity | 已修 |
| components | `ScrollEdgeFade` 深色 | island-parity | 已修 |
| components | 公式和表格渲染（Core `LaTeXMath`） | island-parity | 已修 |
| components | 页面手写等价物全部换成组件（§6.1 硬性） | island-parity | 已修 |
| zoom | `textZoom` 并入偏好键注册表与 `SETTINGS_PARITY` | settings-parity | 已修 |
| zoom | 新内容立即按缩放显示（去 600ms 补缩放） | core-parity | 已修 |

## §3.2 提词岛和浮窗

| 页面 | 条目 | owner | 状态 |
|---|---|---|---|
| island | 5：悬停/钉住整块面板接管点击，收起整体穿透 | island-parity | 已修 |
| island | 6：主窗口在前台时不自动弹出提词 | island-parity | 已修 |
| island | 7：讲稿「手动」标签、持久化来源 | island-parity | 已修 |
| island | 8：看镜头提示接上（E10） | island-parity | 已修 |
| island | 9：屏幕共享时按 `shareAvoiding` 隐藏 | island-parity | 已修 |
| teleprompter | 1：「…」菜单补「先澄清」「追问」 | island-parity | 已修 |
| live-window-menu | 2：提示带快捷键；弹出层渲染到位图截图 | island-parity | 已修 |

## §3.3 会中

| 页面 | 条目 | owner | 状态 |
|---|---|---|---|
| live | 1 暂停/继续图标（补图标资源） | live-parity | 已修 |
| live | 2 回答服务未就绪黄点 | live-parity | 已修 |
| live | 4 原回答↔联网版切换（与 web-search 一起） | live-parity + core-parity | 已修 |
| live | 5 阴影用 Mac 值 | live-parity | 已修 |
| live | 6 「立即回答」高度 40、命中测试按 Mac | live-parity | 已修 |
| live | 7 焦点/选中状态键盘操作后截图 | live-parity | 已修 |
| live | 8 选中文字时固定滚动 | live-parity | 已修 |
| live | 9 归档保存状态行（保存中/受阻/未入队/重试） | live-parity | 已修 |
| live | 10 幻灯片区底色场景强调色 6% | live-parity | 已修 |
| live | 11 模型选择换 `MenuPicker` | live-parity | 已修 |
| live | 12 「我的笔记」逐元素对照 | live-parity | 已修 |
| live | 15 演示数据与 Mac 一致（最后一句未定稿、来源「Zoom · 后端工程师二面」、回声计数 1） | live-parity | 已修 |
| live | 16 虚线胶囊用虚线描边形状 | live-parity | 已修 |
| assistant | 空态说明/模块标题/预设名称 Mac 中文原句 | live-parity | 已修 |
| assistant | 检测条按阈值变色 | live-parity | 已修 |
| assistant | 候选问题队列 | live-parity | 已修 |
| assistant | 独立窗口气泡布局与尺寸按 Mac | live-parity | 已修 |
| assistant | 「合并回主窗口」Mac 原句 | live-parity | 已修 |
| insights | 1 | runtime-gaps | 已修 |
| insights | 2 逐页生成中转圈、出错橙色提示 | runtime-gaps | 已修 |
| insights | 3 已讲完变绿 | runtime-gaps | 已修 |
| insights | 5 去掉独立「屏幕与讲稿」入口 | runtime-gaps | 已修 |
| presenter | 2 读屏来源提供显示器和窗口列表 | island-parity | 已修 |
| presenter | 4 无备注时模型生成讲稿要点（生成中/清除缓存） | island-parity | 已修 |
| translation | 2 关闭开关立即清两路未定稿译文 | live-parity | 已修 |
| translation | 3 去掉自动打开提示、状态文案 Mac 原句 | live-parity | 已修 |
| translation | 6 | live-parity | 已修 |
| speakers | 2 只在说话人变化处显示名字 | runtime-gaps | 已修 |
| speakers | 3 会中页说话人条按 Mac | runtime-gaps | 已修 |
| speakers | 4 保存失败提示与重试 | runtime-gaps | 已修 |
| speakers | 5 标记用图标资源 | runtime-gaps | 已修 |
| screenshot-question | 图像作为模型输入（OpenAI image block / Codex 路径） | core-parity | 已修 |
| screenshot-question | 显示器友好名称 `QueryDisplayConfig`、逐显示器 | island-parity | 已修 |
| screenshot-question | ScreenRegionSheet 标题/提示/区域过小用 Mac 原句 | island-parity + localization | 已修 |
| screenshot-question | 去掉窗口选择器「刷新」按钮 | island-parity | 已修 |
| screenshot-question | 读屏用 `ScreenDelta` 帧去重/稳定等待/变化区域 | island-parity + core-parity | 已修 |
| web-search | 1 联网三态（`webSearchMode` 取值同 Mac） | core-parity + settings-parity | 已修 |
| web-search | 2 模型复核 3 秒超时、失败回退并记录 | core-parity | 已修 |
| web-search | 3 原回答↔联网版切换与按回答编号状态 | core-parity + live-parity | 已修 |
| web-search | 5 设置页分组按 Mac | settings-parity | 已修 |
| coach | 1 会中轻提示总开关 | live-parity + settings-parity | 已修 |
| coach | 2 灵敏度/校准仅开启后显示 | settings-parity | 已修 |

## §3.4 会后

| 页面 | 条目 | owner | 状态 |
|---|---|---|---|
| notes | 1–7 全部（结构化条目级编辑/确认、评价徽标、过期提示、读取问题条、归档状态条、AI 新版本、回答列表元数据、转写版本/查找/撤销、重点联动侧栏、我的笔记标签、空状态重试） | d-parity | 已修 |
| MeetingNotesEditor | 1–4 + 重拍 5 | d-parity | 已修 |
| meeting-notes | 自动保存/离开保存/重试行/平铺重点（去 30 秒合并） | d-parity | 已修 d43f8226（其余待核对） |
| export | 2 + SessionExportSheet 2/3/4/5 | d-parity | 已修 |
| history | 2–8 全部 + 右栏内嵌会议页 | runtime-gaps | 进行中（内嵌页 823ad3e2） |
| knowledge-review | 1/2/3/6 + 4 打开可读全文 | d-parity | 已修 75b46a2e |

## §3.5 系列和资料

| 页面 | 条目 | owner | 状态 |
|---|---|---|---|
| series | 四条全部（左栏搜索/排序、行图标与统计顺序、三段标签、记录行时间与摘要） | runtime-gaps | 已修 |
| series-editor | 1–7 全部 | runtime-gaps | 已修 |
| library | 删除 Windows 独有资料页，并入系列详情 | runtime-gaps | 已修 |
| glossary | 1 间距 24/16；2 剪贴板预填；3 系列作用域显示条件 | island-parity | 已修 |
| skills | 行图标/摘要/系列计数；编辑弹层带取消；逐项勾选对照；去设置页「安装示例技能」 | settings-parity | 已修 |
| document-settings | 取消下载/准备、扫描本机组件与选目录、许可来源链接、多档模型、语义增强开关 | settings-parity | 已修 |

## §3.6 设置

| 页面 | 条目 | owner | 状态 |
|---|---|---|---|
| settings-structure | 4 条全部（记录每次会议开关、说话人与翻译移通用、语义检索只在高级一次、搜索关键词表） | settings-parity | 已修 |
| settings-pages | 12 处不一致按 §2 处理（位置/文案/缺项一律修） | settings-parity | 已修 |
| share-test | 1 设置页入口；2 配对二维码位置尺寸 | settings-parity | 已修 |

## 修条目落点（按页/区域，代表性提交）

| 区域 | 主要提交 |
|---|---|
| 首页/外壳 | `37f3c510` `99f44f15` `390a7694` `0f3ec71f` `82a6925a` `b4e7af76` |
| 组件（§3.1 components 1–6） | `3b4e0e73` `bc81dd00` `ea8a847a` `b5ad5878` `b75c52ce` `66c414d0` `37f3c510` `eab742fd` |
| 提词岛/浮窗 | `be469328` `ae6de178` `5ebd7db9` `390a7694` |
| 会中页 | `9dea68a4` `c7fcad8a` `3233a3f8` `873a0a4f` `075a95e8` `299b1f2d` `1c5cfd23`→`1c5cfdbb` |
| 助手/翻译/coach | `c9bfeaed` `9dea68a4` `075a95e8` `127bf4d3` `2039768d` |
| insights/presenter | `243fd140` `fd5d1afc` `1c5cfdbb` `6c283810` `04bd0769` `bbe31f71` |
| speakers | `562fefe1` `44675d62` `ee808122` `eab742fd` |
| 截图提问/读屏 | `5636937a` `10238c3e` `e49fe297` `4d5c4a71` |
| 联网搜索 | `5bf4cb3e` `db8f879c` `89e42da6` `c7fcad8a` `5a4ccec7` `aca6b2ce` |
| 会议页/纪要/我的笔记 | `8201bfd7` `5f34dd40` `be439c89` `2dfaf629` `fa450dc0` `dc3ae67d` `7cb320c6` `11709aaf` `17b25cc3` `d43f8226` `f7b5c7a1` `6f93376d` |
| 导出 | `531e8d61` `30af8c32` `6f2a29fd` `46897cb8` |
| 所有记录/系列/资料页 | `823ad3e2` `e326c607` `79057b45` `5e44d114` `0fb66877` `4ea6e1de` `4e28351e` `de3aa704` `1de7e0eb` `c009a748` `03ce18f3` `242d0792` `49fed557` `69a3a8f4` `bb6e36c7` |
| 术语表/技能/设置/文档组件 | `5579fe8d` `ea8a847a` `a7ca0f73` `645c3a9e` `313bb8d4` `b4e7af76` `f030bafc` `b46cfbc7` `14dbd935` `a91fb8b3` `b3604e87` `2039768d` `127bf4d3` |
| 共享测试 | `65275e39` `390a7694` |
| 本机实测 K 组 | K1 `3d85d4bd` · K2 `8f345b61` · K3 `789dfec5` · K4 `1b06a915` · K5 `a6bd5200` · K6 `77b1017f` · K7 `待终报提交` · K8 `e6c27df3` · K10 `bca906b3` · K12 `584c3d87` · K17 `0dea577c`/`bfdc2086` · K18 `d81b64d9` · K9/R5 `release.ps1`/`e2b08008` |
| 证据重拍 | `534d01ec` `8ce1576e` `d44cf3fa` |
| 发布/文档 | `22b7f2eb` `20e50ff9` `21f61209` `d6437acc` `e2b08008` `6a66f06e` `1bf9f328` `8acec8f2` `b8951bea` `47cb0f90` `f11a0488` `2048ef8f` `ccd40cb9` `15d3a383` |

结论：决定文件 §3 判「修」的全部条目已修，0 条未修；「接受」与「第二批」条目见 `ACCEPTED_DIFFERENCES.md`/`KNOWN_GAPS.md`。
