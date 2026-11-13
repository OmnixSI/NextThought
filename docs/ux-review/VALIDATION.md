# 学术体验重构验证记录

本轮在当前 macOS 主机完成代码与原生预览验证。保留原有四个未提交文件的功能改动，没有覆盖 `/Applications/InterviewHacker.app`，没有录制真实会议或调用回答模型。

## 已执行的验证

- `swift test --disable-sandbox`：最终 **510 项通过**。包括无 AI 录制就绪、单场上下文隔离、1,005 场全文检索、32 MiB 缓存驱逐、保存刷新、资料副本所有权、用户内容持久化、纠错决定、损坏与失败写入，以及拒绝错误引用的边界测试。
- `scripts/build-ux-preview.sh`：发布配置构建、资源编译、独立预览打包与严格代码签名验证通过。
- `meeting-content`：真实归档验证仅笔记记录保留、人工修订保护、有效导出、同请求片段去重、失败保存草稿重试。
- `archive-ux`：真实备份往返验证个人笔记、重点、自定义模板、内容冲突、默认保留本机、显式恢复、相同内容重导入和恢复前恢复点；损坏 metadata 在应用前拒绝。
- `large-pdf`：实际 PDFKit/转换流水线生成并读取 400 页科研文本；检查所有物理页、约 74 万字符及缓存复用。补充纯扫描、原生页眉和扫描正文同页、充分文字层配图范围警告，以及缺组件等待分支。

首次应用级备份自检发现测试未初始化 NSApplication，已修复并通过；原生走查发现侧栏无障碍行合并、错误引用的无限最近匹配和 Demo 移除错误，均已纳入修复与复验。

## 原生界面检查

| 场景 | 检查内容 |
|---|---|
| 首页／准备 | 学术入口、继续项目、固定开始栏、AI 可选、就绪与资料范围 |
| 全局导航 | 三个工作区顶栏入口；独立设置窗口重复打开复用，保留项目与会议草稿 |
| 设置 | 六类目录保留 12 个目的地；中英文术语、纪要、模板、备份等检索定位；清空时取消定位和高亮 |
| 记录／纪要 | 纪要正文全文搜索、命中片段、分节编辑、人工版本标签与 AI 原稿入口 |
| 引用 | 就地证据栏与返回；无法定位时禁止猜用其他原话 |
| 项目／资料 | 三标签、原件与提取对照、缺原件说明、移除及撤销 |
| 窗口 | 默认 1120 pt；拖到 960 pt 窄窗时收起全局侧栏；系统缩放大窗口，无水平裁切 |
| 无障碍 | 检查实际 AX 名称、角色、选择状态；未声称完成真实 VoiceOver 用户研究 |

实际窄窗口包含系统工具栏，窗口截图约 960×698 pt；内容最小高度为 660 pt。现有字号、系统工具栏与窗口最小值一起决定外框高度。

## 性能结果的边界

首轮 release：400 页文字 PDF 首次提取约 0.68–0.78 秒、缓存约 0.02–0.03 秒，单扫描页混合文件约 0.86–1.27 秒。新增完整性分支后的 debug：400 页约 1.09 秒，缓存约 0.029 秒，同页扫描正文约 0.70 秒。最终 release：400 页首次 0.702 秒，缓存 0.022 秒，单扫描页混合文件 0.993 秒，同页页眉＋扫描正文 0.643 秒；文字与页定位完整，图像范围警告和 2 项依赖检查通过。

这些是本机合成样例；没有用户原来失败的 PDF，不能据此断言所有大型扫描文档都能在一秒内完成。扫描识别仍按页执行，单次同步 Vision 识别在页间响应取消；文件保持 512 MiB／2,000 页的明确容量限制。

## 剩余 P2 可执行清单

| 项目 | 后续操作与验收 |
|---|---|
| 热词与术语机制 | 在术语管理中解释两者作用范围并提供互相跳转；同一专名能找到配置入口 |
| 历史术语／英文复数 | 清理剩余“系列／会议”辅助文案和动态无障碍中文；补 0、1、多条英语用例 |
| 长期删除恢复 | 为项目永久删除增加独立回收机制；继续保留当前范围确认与资料移除撤销 |
| 图像／公式理解 | 当前充足文字页中的图像明确标记未读取范围；后续按图像区域增强识别并验证图表、公式的语义完整性 |
| 检索大规模容量 | 当前全量扫描有进度且无静默数量上限；超过当前合成容量时测量冷查询与内存，再决定是否加入持久索引 |
| 跨系统真机 | 本机完成兼容目标构建，macOS 14 真机、正式麦克风采集和真实服务故障恢复仍需对应环境验证 |

## 交付状态

本轮早期测试和发布构建日志保留在 `.build/ux-verified-tests.log`、`.build/ux-delivery-build.log`；最终组件验收日志见下节；自检与预览使用本仓库 `.local/` 下独立目录。可运行预览为 `.local/NextThoughtUXReview.app`，默认使用演示数据；其 Bundle ID、偏好、钥匙串服务与数据路径与正式版隔离，预览禁用自动更新。


最终补充：`meeting-content`、`archive-ux`、`large-pdf`、`structured-notes`、`fishbone-rendering` 五组应用级自检全部通过；鱼骨图宽/窄实际渲染中六类内容均可识别。最终原生复验通过 English `minutes` 搜索、AI optional、资料移除→Undo 恢复 4 份资料、超范围时间引用不显示无关原话，以及浅/深色界面。十二个设置目录逐一只读进入检查。预览停在浅色英文首页，正式版语言与外观未改变。


## v2.5.0 最终组件验收（2026-11-09）

用户已查看并明确同意合并发版。本轮独立设置窗口、下划线页签、模板值菜单、更新页面、按钮、输入与反馈状态已经纳入原生产品；HTML 概念仅保留设计历史。

- 最终本机 `swift test --disable-sandbox`：510 项通过；`swift build --disable-sandbox -c release` 通过（148.81 秒）。日志：`.build/settings-swift-tests.log`、`.build/settings-final-release-build.log`。
- 发布配置实际应用自检：data、meeting-content、archive-ux、large-pdf、session-export、session-backup-drain、session-edit-receipts、session-carry-context、source-integrity、settings-navigation 均通过。日志：`.build/settings-production-*.log`。
- 结构化纪要、鱼骨图渲染、资料准备分支另外通过应用自检。Codex transport、Legacy Office、7 项文档归一化、4 项发布资产测试及 3 项移动共享协议测试通过。
- 最终发布配置 PDF：400 页、742,606 字符，首次 0.767 秒、缓存 0.023 秒，扫描混合页 1.162 秒、同页页眉与扫描正文 0.641 秒；页定位与两项依赖保护通过。
- 设置的 general / updates / templates，中文与英文、浅色与深色、980×720 与 880×600 内容尺寸，共 24 张实际 SwiftUI/AppKit 渲染图。完整原图和机器验收结果在 `.local/settings-ui-final-zh-Hans/` 与 `.local/settings-ui-final-en/`；仓库 evidence 目录保留代表图。
- 原生交互实际检查：独立窗口复用、方向键切换页签与真实焦点移动、原生菜单当前值与 Escape 关闭、Command-F 聚焦搜索、AX 选择状态。
- 远端暴露的新版 SDK `CGPDFDictionaryRef` 类型问题已修复。无图形生命周期的命令行数据测试先补 AppKit 初始化；断言全部通过后退出阶段仍出现崩溃，因此仅自检入口改用刷新输出后 `_exit`，避免进程全局 AppKit 析构。自检仍等待各自持久化回执；正式应用入口不变。设置窗口自检另显式完成 AppKit 启动生命周期，以匹配命令行 CI 环境；独立测试身份在本机复验通过。

### 验证边界与剩余 P2

- 当前最终设置截图尺寸不等同于主窗口尺寸；上表 960 / 1120 检查属于本轮前期主流程检查。
- 尚未完成真实 VoiceOver 用户测试、中文输入法完整自动化、物理 macOS 14 设备和真实麦克风故障矩阵。不得将 AX 静态检查等同于这些验证。
- 后续收敛值菜单装饰箭头的 AX 表达；验证无重复可操作名称，并复验 VoiceOver 与键盘菜单。
- 当前 1,005 场合成记录验证无 200 条上限；进一步按真实库测量冷查询延迟与内存，再决定持久化索引。
- 没有用户此前失败的 PDF。大扫描文档、公式和图表语义完整性仍需真实样本；512 MiB / 2,000 页限制保持明确。


### 发布检查状态

最终设置窗口使用明确的 `contentMinSize`，自检对应检查内容区约束及可调整大小属性；避免混淆窗口外框最小值与内容最小值。新版 SDK 的宿主最小布局回写通过禁用自动 sizing、再设置内容约束修复。

源代码 `d90e776faad08fd21d0cf0e586bb657a098314f3` 的远端运行 [42613672380](https://github.com/OmnixSI/NextThought/actions/runs/42613672380) 未执行任何步骤：GitHub 拒绝启动任务，提示账户付款或支出额度问题。这不是测试通过，也不是代码测试失败。最后一轮有执行的构建与应用自检结果必须结合该限制阅读；用户随后明确要求本机构建并发布，本次不使用 Actions。README 的截图均为本地真实原生渲染，不包含真实会议资料。


### 本机正式发布路径

用户明确授权本机构建发版。最新源代码本机完整复验：510 项测试通过，发布配置构建通过（114.18 秒）；日志 `.build/settings-release-final-tests.log` 和 `.build/settings-release-final-build.log`。最终发布二进制的 13 组自检全部通过：data、meeting-content、archive-ux、large-pdf、structured-notes、fishbone-rendering、session-export、session-backup-drain、session-edit-receipts、session-carry-context、source-integrity、settings-navigation、document-setup。日志 `.build/settings-release-local-*.log`。

本次最终大 PDF 为 400 页 / 742,606 字符，首次 0.756 秒、缓存 0.023 秒、扫描混合页 1.165 秒、同页页眉加扫描正文 0.772 秒。仅代表合成样本。

iOS SDK 构建在此前远端 macos 作业通过；之后变更局限于 macOS 设置窗口、自检入口及文档，未更改移动端代码。本机仅有 Command Line Tools，无法把该历史结果表述为本机完整 iOS SDK 重跑。

为遵守本次不使用 Actions 的要求，合并与打标签期间暂时停用 Verify / Release 自动工作流；上传校验完成后恢复原先启用状态，未来触发策略不变。本机正式发布使用仓库打包与发布资产校验脚本，最终产物沿用 v2.4.1 的正式签名身份，而非预览的临时签名。


### 已发布结果

[PR #28](https://github.com/OmnixSI/NextThought/pull/28) 已合并，发布源提交 `72ddb004a195f4274736e9ba9ab28c23ba610656`。本机构建的 [v2.5.0](https://github.com/OmnixSI/NextThought/releases/tag/v2.5.0) 于 2026-11-09 12:12:22 UTC 发布并设为最新版，build 2500。

最终打包产物的 13 组自检再次全部通过（`.build/settings-artifact-*.log`）。上传后重新下载全部 5 个资产：SHA256 清单逐项通过，DMG 自身校验通过，解压应用严格签名验证通过，版本、build、源提交与更新清单一致；正式签名沿用 v2.4.1 的根证书指纹。完整校验回执见 [v2.5.0-release-verification.json](evidence/v2.5.0-release-verification.json)。README 顶部已展示实际新版界面与功能。

## v3.0.0 本机发布

GitHub Actions 因账户付款或额度问题无法启动任务，经用户明确同意，v3.0.0 在本机构建并发布，签名沿用历次正式发布的同一身份。[PR #29](https://github.com/OmnixSI/NextThought/pull/29) 合并后的发布源提交为 `d7f0331f3078301d5202adf89bd8417bebbfa66d`，[v3.0.0](https://github.com/OmnixSI/NextThought/releases/tag/v3.0.0) 于 2026-11-11 04:42:25 UTC 发布并设为最新版，build 3000。

发布前在 `main` 上 544 项测试通过；发布二进制的 12 组自检全部通过（data、session-export、session-backup-drain、session-edit-receipts、session-carry-context、meeting-content、archive-ux、large-pdf、structured-notes、source-integrity、settings-navigation、document-setup）。上传到草稿后重新下载全部资产：SHA256 清单逐项通过，DMG 校验通过，解压后的应用严格签名验证通过，版本、build、源提交与更新清单一致。打标签期间暂停了 Verify / Release 工作流，核验后已恢复。

这一版没有在真实会议中跑过：真实音频下的整场会议、摄像头眼神留意、真实整屏共享时提词岛的自动收回都未实测。iOS SDK 构建检查本机无法运行，移动端代码自 v2.5.0 起未改动。完整回执见 [v3.0.0-release-verification.json](evidence/v3.0.0-release-verification.json)。

## v3.1.0 本机发布

GitHub Actions 仍无法启动任务，v3.1.0 按 v3.0.0 的方式在本机构建并发布，签名沿用同一正式发布身份。发布源提交 `e5d8b32032a1c72f38e10437234631b540b55622`，[v3.1.0](https://github.com/OmnixSI/NextThought/releases/tag/v3.1.0) 于 2026-11-13T03:37:22Z 发布并设为最新版，build 3100。发布前 546 项测试通过；发布二进制 12 组自检全部通过；重新下载的全部资产校验和、DMG、应用严格签名、版本与更新清单均一致。双屏演讲与跟随幻灯片只用演示数据和窗口列表验证过，未在真实演讲与屏幕共享中实测。回执见 [v3.1.0-release-verification.json](evidence/v3.1.0-release-verification.json)。
