# A16 双向恢复验证：Mac 备份夹具 → Windows 导入 / Windows 导出回读 / 恢复点语义

夹具来源：`Windows/tests/Fixtures/mac-backup/`（Mac 3.4.0 build 3400 真实导出，README 列出了 ZIP 与 5 个备份文件的 SHA-256）
ZIP：`nt-mac-fixtures-26.5.2-20261121.zip`，SHA-256 `afbc3eb281bc2a9a5fc449a40709edc85c28a20f8fbe47f9dbd595710b4366d8`（与 README 一致，测试断言）
测试：`Windows/tests/NextThought.Tests/MacBackupFixturesTests.cs`（7 个，全部通过）、`Windows/tests/NextThought.Core.Tests/MacRestorePointCompatTests.cs`（8 个，全部通过）

## 1. 夹具核对

| 文件 | SHA-256 | 结果 |
|---|---|---|
| ZIP 整包 | README 所载值 | 一致（`FixtureZipAndSumsMatchTheReadme`） |
| 01-text-only.ntbackup | 89c60715…d507 | 一致 |
| 02-voiceprints.ntbackup | 1261f7ba…ef86 | 一致 |
| 03-glossary.ntbackup | f7b90211…2c74 | 一致 |
| all-scenarios.ntbackup | 78637d64…8f56 | 一致 |
| 04-incremental-chain-Backups.zip | fc03b2bd…c8fe | 一致 |

Mac 打包用 `ditto --sequesterRsrc --keepParent`：ZIP 带 `__MACOSX/` 与 `._*` AppleDouble 项、根目录是 `NextThought-Backup/`。Windows 导入的 `ExtractArchive` 逐条白名单校验（非法路径/符号链接/压缩比上限），`LocateRoot` 同时在根目录与单层子目录找 `manifest.json`，因此两种布局都接受；AppleDouble 项不会被当作备份内容（它们不在备份根目录下）。

## 2. Mac 全量备份 → Windows 导入（逐字段）

`MacFullArchiveImportsEveryScenarioFieldByField`：`BackupService.ImportAsync(all-scenarios.ntbackup)` 到空 Windows 数据目录后断言：
- `SessionsAdded=4`、`WorkspacesAdded=4`、`SettingsRestored=true`；
- 4 场会议的标题、模式、`voiceNames`/`meVoices`、转写逐行文本、`summary.json` 的 markdown 与夹具 `data/Sessions/` 逐字段一致；
- 4 个系列的 `id`/`name`/`preferences`/`skillIDs` 数量、系列级 `glossary`（X-ASR）与夹具 `data/Workspaces/*.json` 一致；
- 全局术语表（Zipformer / Sherpa-ONNX / 王老师）、技能库 1 条、模板（空）与 `data/Glossary`、`data/Skills` 一致；
- Mac `settings.plist`（XML plist，含 `apiPresets` base64 JSON）由 `MacSettingsPlist.Apply` 合并；`includesSecrets=false` 时无 secrets.plist，`Migration/` 保留原始归档。

## 3. 单个系列导出

`EachMacSeriesExportImportsItsSessionAndWorkspace`（01/02/03 各一次）：导入后恰好 1 个系列 + 1 场会议；系列名与夹具一致；会议标题/voiceNames/meVoices 与 `data/Sessions/<id>/meta.json` 一致；02 有 `voices.jsonl`（声纹标记落到 `TranscriptLine.Voice`）；03 系列术语表含 X-ASR。

## 4. Windows 导出 → 回读

`WindowsExportStaysMacReadableAndReimportsFieldByField`：把 all-scenarios 导入 Windows 数据目录后用 `BackupService.ExportAsync` 再导出：
- 归档含 `manifest.json`、`settings.plist`、`glossary.json`、`skills.json`、`note-templates.json`、4 个 `Sessions/<id>/meta.json`、4 个 `Workspaces/<id>.json`；无 `__MACOSX`；
- manifest：`format=nextthought-backup`、`version=1`、`sessionCount=4`、`workspaceCount=4`、`includesSecrets=false`（Mac `locateRoot`/`manifest.validate` 接受扁平布局，现有 Windows 导出已是扁平根，与 Mac 的根目录+manifest 约定兼容）；
- 再导入到第二个 Windows 数据目录，逐字段比较：4 场会议的标题/voiceNames/meVoices/summary/逐行 (text, speaker, voice)、4 个系列 (id, name, preferences)、全局术语表 canonical 集合全部一致。

## 5. 恢复点（E30/A14）

`MacRecoveryPointsListRestoreAndKeepTheE30AndA14Semantics`：把夹具 `data/Backups/` 整体移到 Windows 数据目录后：
- `BackupService.ListBackups()` 识别 4 个 `.ntrestore`（`IsRecoveryPoint`），原因集合 `{session, 夹具 A, 夹具 B, 夹具 C}`，按创建时间倒序；
- `IncrementalBackupStore.Restore` 恢复“夹具 A”：**发现 Mac 3.4.0 的恢复点键带 `Sessions/essions/<id>/…` 前缀**（Mac 在目录 URL 末尾带斜杠时 `dropFirst(path.count+1)` 多切了首字符）。Windows 新增 `NormaliseLegacyPath`，恢复后落到 `Sessions/<id>/…`，不会产生 `Sessions/essions` 目录；同时 `manifest.json`、`Workspaces/` 等根文件正确恢复；
- 恢复点可被 Windows `ImportAsync` 导入（点文件必须在同库 `Backups/RestorePoints` 下，与 Mac 一样只恢复本库的点）；
- E30：删除一个点后列表少 1；其余点引用的 Object 全部保留；
- A14：写一个**不可解析**的 manifest（文本里带 64 位哈希）后删除另一个点触发 prune，该哈希对应 Object 被保护、不会删除。

## 6. 发现/修复的格式差异

1. **Mac 恢复点路径前缀错误**（`Sessions/essions/…`、`Originals/riginals/…`）：Windows `IncrementalBackupStore.Restore` 增加 `NormaliseLegacyPath`，恢复时映射回真实目录（`MacRestorePointCompatTests` 覆盖 4 种前缀与 Mac 秒级日期）。
2. Windows 导出布局与 Mac 导入约定核对：Mac `DataBackup.locateRoot` 接受根目录 `manifest.json`，`applyImport` 读根下 `settings.plist`/`glossary.json`/`skills.json`/`note-templates.json`/`Sessions`/`Workspaces`——Windows 当前扁平导出均满足，无需改动；`settings.json` 是 Windows 额外文件，Mac 忽略。
3. Mac `manifest.json` 无 `fileHashes`（Windows 扩展字段），Windows 导入按“缺省则不校验哈希”处理；A12 的 `windowsExtensionVersion` 只在存在时校验。

## 7. 未解决 / 待 verify

- Mac 端真实回读 Windows 导出无法在本机执行：以 Mac 源码 `DataBackup.prepareImport/locateRoot/applyImport` 的读取路径做静态核对 + Windows 回读；实机 Mac 回读仍需一次人工验证（Mac 上选择 Windows 导出的 .ntbackup → 预览 → 导入）。
- 夹具恢复点只覆盖 `Sessions`/`Originals` 两类；`Materials`/`Migration` 的前缀兼容映射是防御性加入（未由夹具触发）。
- 夹具里的 `note-templates.json` 为空数组；自定义模板的 Mac→Windows→Mac 双向未由本夹具覆盖（A13 已有非空模板测试）。
- Windows 导出 manifest 的 `appVersion/appBuild` 仍是 Windows 自己的值（Mac 导入只校验 format/version），未改。
- 恢复点导入要求点文件位于本库 `Backups/RestorePoints`（与 Mac 相同），跨机器迁移要迁整个 `Backups/` 目录；已在测试里按此流程验证。

## 8. §4.5 复核（task-52，2026-11-21 阶段 1）

测试文件：`Windows/tests/NextThought.Tests/MacBackupParityTests.cs`（13 项；12 通过，win-export 项在导出夹具生成前 Skip）。
全部走正式路径 `BackupService.ImportAsync` / `IncrementalBackupStore.List+Restore`，不另写读写实现。

### 8.1 四份备份导入数量表（与 mac-backup/README.md 对照）

| 备份 | 系列 | 会议 | 转写 | 纪要 | 个人笔记 | 翻译 | 声纹 | 术语表 | 技能 | 模板 | 设置 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| all-scenarios | 4 | 4 | 4 场逐行与夹具一致 | 4 份 | 随夹具 | 1 场有 translations.jsonl（final translation self-test） | 1 场有 voices.jsonl | 全局 3 条（Zipformer / Sherpa-ONNX / 王老师） | 1 条 | 归档内 note-templates.json（夹具为空数组，导入 0 个用户模板） | 恢复（apiPresets 1 条 OpenAI 预设） |
| 01-text-only | 1 | 1 | 2 句 | 有（# 周三组会…） | 「记得补消融实验的表格。」 | 无 | 无 | 无（系列导出不带全局术语表） | 无 | 无 | 无（系列导出不带 settings.plist） |
| 02-voiceprints | 1 | 1 | 4 句 | 有（分工会） | 无 | 无 | 有：声音 1=「李工」、声音 2；共 3 个说话人（含我） | 无 | 无 | 无 | 无 |
| 03-glossary | 1 | 1 | 2 句 | 有（识别模型例会） | 无 | 无 | 无 | 系列术语表 X-ASR | 系列挂 C4911013…（技能库 1 条「夹具 · 先给日期」） | 无 | 无 |

### 8.2 ZIP 包装 / AppleDouble

- 四份归档都带顶层 `NextThought-Backup/`、`__MACOSX/`、`._*` 项；`ExtractArchive` 只把合法路径解到 staging，`LocateRoot` 在根与单层子目录找 `manifest.json`（顶层目录布局可导入）。
- 导入 all-scenarios 后扫描整个 Windows 数据目录：无 `._*` 文件、无 `__MACOSX` 目录、无 `NextThought-Backup` 目录残留。

### 8.3 settings.plist apiPresets

`MacSettingsPlist.Apply` 将 plist 的 base64 `<data>` 解成 JSON 再映射：OpenAI / baseURL `https://api.openai.com/v1` / keyAccount `answer.api` / id `846ADF47-227C-465A-A122-9769BA4C4E09`；真实导入后 `SettingsStore.Load()` 中同一条。

### 8.4 增量备份链

`04-incremental-chain-Backups.zip` 解压后（忽略 `._*` 伴随文件）`IncrementalBackupStore.List()` 得 4 个恢复点，按 Mac 秒级 `createdAt` 排序：session / 夹具 A / 夹具 B / 夹具 C；逐个 `Restore` 到独立目录，会议数 **1 → 2 → 3 → 4** 递增，`manifest.sessionCount` 相同；旧路径前缀 `Sessions/essions/<id>` 被 `NormaliseLegacyPath` 纠正，恢复结果无 `Sessions/essions` 残留。

### 8.5 Windows 导出（win-export）—— 待 runtime-gaps §4.3/4.4 后生成

按 §4.5.5，all-scenarios 导入后立即从 Windows 导出，提交 `Windows/tests/Fixtures/win-export/all-scenarios-from-windows.ntbackup` 与同目录 `README.md`（逐文件：逐字节相同 / 仅键序或空白不同 / 实质差别）。导出字节依赖 runtime-gaps 正在做的 §4.3（UUID 大写）与 §4.4（去掉多写出的键）序列化修复；这些修复进 HEAD 前生成的导出会有实质差别（如新增键、UUID 小写），不能作为提交夹具。比较由 `Windows/scripts/compare_mac_backup_export.py` + `MacBackupParityTests` 的 win-export 项共同保证，实质差别必须为 0。

### 8.6 未解决 / 待 verify（A16）

- win-export 夹具尚未生成与提交（等待 §4.3/4.4 序列化提交进 HEAD）。
- Mac 端导入 Windows 导出仍需人做（任务书第 12 节「需要人做的事」）；本机只验证到 Windows 导入→导出→逐文件比较。
- 夹具里 note-templates.json 为空数组，自定义模板的双向已由 A13 其它测试覆盖，不在本夹具范围。
- Windows 导出 manifest 的 appVersion/appBuild 仍是 Windows 自己的值（Mac 导入只校验 format/version，见第 6 节）。
