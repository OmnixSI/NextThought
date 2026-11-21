# 备份复核（E22 · §6.7）Mac 3.4.0 源码 ↔ Windows BackupRestoreReviewView 逐元素对照

- Mac 基准：Mac 3.4.0 `f015e8c6`，`Sources/NextThought/BackupRestoreReview.swift`
- Windows 代码：`Windows/src/NextThought.Desktop/Views/BackupRestoreReviewView.axaml(.cs)`、宿主 `BackupSettingsSection`/`RunBackupUiCheck`
- Windows 截图（本目录）：`backup-review-light.png` / `backup-review-dark.png`（真实恢复点 → `PrepareBackupImportAsync` → 复核窗口；`--self-test backup-ui` 与 `--self-test windows` 均 passed，两套图一致）
- 证据生成：`--self-test windows`（真实恢复点 → `PrepareBackupImportAsync` → 复核窗口）

| # | Mac 元素（顺序） | 值 | Windows 现状 | 结论 |
|---|---|---|---|---|
| 1 | 窗口 | 820×640、min 700×540、可缩放、标题「导入与恢复预览」、sheet 挂在父窗口 | 同（宿主窗口 820×640/700×540，`ShowDialog`） | 一致 |
| 2 | 布局 | padding 24、spacing 16、paper 底 | 同（DockPanel Margin 24、Spacing 16、Paper 底） | 一致 |
| 3 | 标题/说明 | 「导入与恢复预览」section；body 次要色「新增内容会导入；相同内容会跳过…」 | 同 | 一致 |
| 4 | 统计行 | 新增记录/相同记录/冲突记录/备份模板：大数字 section + caption 标签，间距 16 | 同（`NewCount/IdenticalCount/ConflictCount/TemplateCount`） | 一致 |
| 5 | 说明行 | caption 次要色「系列按修改时间合并；应用前会先创建本机恢复点…」 | 同（已改为 Mac 原文；旧 Windows 写「系列」） | 一致 |
| 6 | 记录行 | 标题（可选中）+ 状态行（冲突 caution，否则次要色）；冲突右侧「使用备份」勾选；行 padding 12、12 间距、sunken 底、圆角 8 | 同 | 一致 |
| 7 | 模板段 | 「自定义纪要模板」+ 行（模板行无状态）；无内容时「此备份没有会议记录或自定义模板…」 | 同（`HasTemplates`/`IsEmpty`） | 一致 |
| 8 | 偏好开关 | 「同时导入偏好设置」 | 同 | 一致 |
| 9 | 失败 | caution 可选中文字，Mac 原文「恢复未全部完成：…」 | 同 | 一致 |
| 10 | 底部 | 左：处理中 ProgressView +「正在恢复，请保留此窗口…」；右：取消、应用所选内容（busy 时禁用） | 同（取消在应用左）；顺序一致 | 一致 |
| 11 | 状态文案 | 新增 · 将导入 / 内容相同 · 跳过 / 内容不同 · 默认保留本机 | 同（`BackupReviewRow.StatusText`） | 一致 |
| 12 | 忙碌 | 应用时整页禁用、窗口不可关 | 同（`Busy` 绑定 + 宿主 closing 保护） | 一致 |

## 差异 / 未解决

1. 取消按钮已按 Mac SecondaryButtonStyle 实现（32 pt、hairline 边、surface 底、hover sunken），与 Mac 一致。
2. 截图使用临时恢复点数据（新增 1 场、无冲突/模板）；冲突行的视觉由同一模板渲染，数据取决于真实备份内容。
