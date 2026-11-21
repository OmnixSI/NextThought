# Windows 导出夹具：`all-scenarios-from-windows.ntbackup`

这份夹具是 §4.5.5 要求的「导入 all-scenarios 后不做任何修改，立即从 Windows 导出」的产物，供人在 Mac 上导入核对。

| 项 | 值 |
|---|---|
| 文件 | `all-scenarios-from-windows.ntbackup`（ZIP，31,869 字节） |
| SHA-256 | `6e8d961fe962d76cbab1417f1bd3d42364752acac0c357ff05ff704c3340b92c` |
| Windows 构建 | 提交 `af6807d5`（本提交的源码树，含 `a254e825` A6 最终去掉 `Glossary.IsEmpty`、`62bbb117` A6 UUID 大写/去多余键、`f2103503` A11 修订号、`8b6c09a7` A16 导入核对、`8a20b528` A16 导出格式）；Windows 版本 `0.5.145`，manifest `appBuild` = `windows` |
| 来源 | `Windows/tests/Fixtures/mac-backup/nt-mac-fixtures-26.5.2-20261121.zip` 里的 `nt-fixtures/ntbackup/all-scenarios.ntbackup`（SHA-256 `78637d64b3dcdaf77351ee45272b70c65fdf875cf9167b763a496593ace18f56`） |
| 生成方式 | 空数据目录 → `BackupService.ImportAsync(all-scenarios.ntbackup)`（SessionsAdded=4、WorkspacesAdded=4、SettingsRestored=true）→ 不改任何数据 → `BackupService.ExportAsync(本文件)` |
| 验证 | `Windows/tests/NextThought.Tests/MacBackupParityTests.WindowsExportMatchesTheMacArchiveFileByFile`（逐文件比较，见下） |

## 与原备份逐文件比较

ZIP 内的 `__MACOSX/`、以 `._` 开头的 AppleDouble 条目和目录条目不算文件（Mac 与 Windows 导入都忽略它们）。
原备份有效文件 30 个；Windows 导出有效文件 33 个 = 30 个原文件 + 3 个 Windows 额外文件；**缺失 0 个**。

### 1. 逐字节相同（21 个）

四场会议在所有导出的数据文件上都与 Mac 原文件逐字节相同（UUID 大小写、日期格式、JSONL 紧凑行、双语/译文行、声纹与个人笔记都在内）：

```
Sessions/152680E6-C210-4352-8214-7D067572A123/{context.json,meta.json,questions.jsonl,summary.json,transcript.jsonl,user-content.json}
Sessions/567F53B1-7C1E-464C-ABA7-0F64A0E7C9C0/{context.json,meta.json,questions.jsonl,summary.json,transcript.jsonl}
Sessions/F18D66C2-71D1-4629-8066-AD27104B74E8/{context.json,meta.json,transcript.jsonl,translations.jsonl}
Sessions/F6C4BC41-02C8-43E1-925C-6BB76804E708/{context.json,meta.json,questions.jsonl,summary.json,transcript.jsonl,voices.jsonl}
```

场次顺序按 fixtures 的标题：`152680E6…` 周三组会、`567F53B1…` 识别模型例会、`F18D66C2…` final translation backup self-test、`F6C4BC41…` 分工会。

### 2. 仅键顺序或空白不同（7 个）

都是 JSON，解析后内容逐键相同，仅对象键顺序与缩进不同：

```
Workspaces/4622DF4B-C35A-4381-9B1E-6AA29FFFF295.json
Workspaces/9776CA32-9225-40C6-BE7A-15B84121A7BB.json
Workspaces/AD139925-ECF7-4DEF-9207-63578DBFF79B.json
Workspaces/EDCA7C6D-475A-4954-BFC4-554DE2322933.json
glossary.json
note-templates.json
skills.json
```

### 3. 导出元数据/格式（2 个，非数据实质差别）

| 文件 | 相同 | 不同 |
|---|---|---|
| `manifest.json` | `format=nextthought-backup`、`version=1`、`kind=full`、`includesSecrets=false`、`workspaceCount=4`、`sessionCount=4` | 导出时间 `createdAt`；Windows 版本 `appVersion/appBuild`（Mac 原文件是 `3.4.0`/`3400`）；Windows 扩展字段 `windowsExtensionVersion=1`、`fileHashes`（A13 的完整性清单，Mac 导出没有） |
| `settings.plist` | 原有 3 个键解码后完全相同：`activeWorkspaceID`、`apiPresets`（base64 JSON 内容相同，仅键顺序不同）、`transcriptionBackend` | Windows 会写出全部 78 个 Mac 偏好键（多 75 个默认值），XML 缩进/声明行格式不同；原 3 个键的值不变 |

### 4. Windows 额外文件（3 个，Mac 导入忽略）

| 文件 | 说明 |
|---|---|
| `settings.json` | Windows 自己的设置文件；Mac 导入只读 `settings.plist`，忽略它 |
| `Glossary/glossary.json` | Windows 术语表存储路径的原生副本；与根目录 `glossary.json` 内容一致，Mac 只读根目录文件 |
| `Migration/78637d64…ntbackup` | A13 导入时把原始备份保留在 `Migration/`（迁移存档），随导出一起带出；Mac 忽略未知目录 |

### 5. 实质差别：0

- 同一份数据文件 28 个（21 逐字节 + 7 键顺序/空白），没有键丢失、没有键新增、没有值改变；UUID 全部大写（`62bbb117` A6 修复后逐字节可证）。
- 缺失文件 0；只有 3 个 Windows 专有/迁移文件额外存在，Mac 导入路径不会读取它们。
- `manifest.json`、`settings.plist` 的差异全部是导出元数据/格式/默认值扩展，已在上表逐项列出。

## 在 Mac 上核对

1. 在 Mac NextThought 里选择这份 `.ntbackup` 导入（预览 → 应用）。
2. 期望：4 个系列、4 场会议；会议标题为「周三组会 / 分工会 / 识别模型例会 / final translation backup self-test」；`01/02/03` 里的术语表、声纹、个人笔记与 Mac 原备份一致。
3. 打开其中任意一场，检查转写行、译文、声纹人名的显示。
4. 若 Mac 对 Windows 扩展字段或额外文件给出警告，记录到 `Windows/OPEN_QUESTIONS.md` 由人处理（本机无法执行 Mac 侧导入）。

## 复现

```powershell
# 重新生成（仅 A16 维护者需要）：
#   1. 在干净提交（8a20b528 或之后的 A16 提交）的 worktree 里 build Windows/src/NextThought.Platform
#   2. 从 fixtures/mac-backup/...zip 解出 all-scenarios.ntbackup
#   3. 用 BackupService.ImportAsync → ExportAsync 导出
#   4. 跑 dotnet test --filter FullyQualifiedName~MacBackupParityTests
```
