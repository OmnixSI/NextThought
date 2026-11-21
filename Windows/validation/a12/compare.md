# A12: real Mac settings.plist check

## Fixture

| Item | Value |
| --- | --- |
| Mac backup fixture | `Windows/tests/Fixtures/mac-backup/nt-mac-fixtures-26.5.2-20261121.zip` |
| ZIP SHA-256 | `afbc3eb281bc2a9a5fc449a40709edc85c28a20f8fbe47f9dbd595710b4366d8`（与 `mac-backup/README.md` 一致） |
| Inner archive | `nt-fixtures/ntbackup/all-scenarios.ntbackup`（ZIP） |
| settings.plist | `NextThought-Backup/settings.plist`（598 bytes，SHA-256 `6d406b31ea10505d5bf2df27a530b2c92adb9c89ebc07cc2a31e1bdec5aa5725`） |

解压方式：外层 ZIP → 临时目录；`.ntbackup` 用内置 ZIP 读取；忽略 `__MACOSX/` 与 `._*`。Mac 备份是 2026-11-21、NextThought 3.4.0 build 3400 在 macOS 26.5.2 / Apple Silicon 上导出的（见 README）。

## 真实 plist 内容（逐键）

```xml
<key>activeWorkspaceID</key><string>AD139925-ECF7-4DEF-9207-63578DBFF79B</string>
<key>apiPresets</key><data>（base64 JSON，见下）</data>
<key>transcriptionBackend</key><string>zipformer</string>
```

- 键集合只有 3 个：Mac 导出的是 `AppPreferences.dictionaryRepresentation()` 里实际存在的键（`DataBackup.exportAll` → `SettingsSnapshot.filter(...)` → `PropertyListSerialization`），没有把默认值全写一遍。
- 类型：`activeWorkspaceID` = `<string>`、`transcriptionBackend` = `<string>`、`apiPresets` = **`<data>`**（Mac UserDefaults 存 JSON Data）。三者都是 `MacPreferences` 注册表里的已知键（`activeWorkspaceID` String、`transcriptionBackend` String 枚举 `zipformer/apple/cloud`、`apiPresets` Array）。
- `apiPresets` 的 base64 解码后是一段 JSON（字段顺序与 Mac 的 APIPreset Codable 一致）：

```json
[{"effort":"default","id":"846ADF47-227C-465A-A122-9769BA4C4E09","model":"",
  "keyAccount":"answer.api","name":"OpenAI","baseURL":"https://api.openai.com/v1"}]
```

- plist 不含 `secrets.plist`、不含任何 API Key/令牌；`keyAccount` 只是钥匙串账号名（`answer.api`），不是密钥。

## 核对结果（`Platform/MacSettingsPlist.cs`）

| 核对点 | 结果 |
| --- | --- |
| 真实 plist 能读入 | ✅ `Apply` 读出 3 个键，无异常 |
| 键集合/类型/默认值 | ✅ 3 键都是注册表已知键、类型与 Mac 一致；缺键按 Mac 语义保留 Windows 默认（Mac plist 没写的键不导入） |
| `apiPresets` base64 JSON | ✅ 本次修复：`<data>` 解码 base64 → JSON → 对象图后走正常映射；导出也改回 Mac 的 `<data>` 形式（base64 JSON）。原先 `<data>` 落成 base64 字符串导致预设丢失、导出成 `<array>` |
| `webSearchMode` 映射 | ✅ off → `WebSearch=false`；auto/always → `WebSearch=true`（夹具未含该键，用合成 plist 验证）。⚠ Windows 里另有 `AppSettings.WebSearchMode` 字符串，映射目前只写 bool，导出恒为 `off/auto`（`always` 会被归一成 `auto`），见未解决 |
| Mac 独有键原样保留（G6） | ✅ `activeWorkspaceID` 未映射，`Apply` 存进 `AdditionalProperties`，`Write` 回写同一 `<string>`；`customShortcuts` 等 Mac 独有键同样保留（合成用例）；未知 `<data>` 用 `{"$data": base64}` 哨兵保留类型、回写成 `<data>` 而不是 `<string>` |
| 导出再导入逐键往返 | ✅ 真实夹具：Apply → Write → Apply，`MacPreferenceMap.MappedKeys` 每个键值相等（`MacPreferences.ValuesEqual`），`activeWorkspaceID` 两轮都保留，`apiPresets` JSON 字段一致 |
| 不写密钥 | ✅ 导出过滤 `apiKey/api_key/secret/password/token` 命名键；`Apply` 同样不把这类键存进设置模型；真实夹具导出文本无 `apiKey`/`secret`；合成 `sk-hidden` 用例验证不落盘 |
| 导入空 plist / 非 plist | 保持原行为：无 `<dict>` 或 XML 异常返回原设置 |

本次对 `MacSettingsPlist` 的改动：
1. `ElementToValue` 增加 `<data>`：base64（忽略换行/空白）→ UTF-8 JSON → 对象图；非 JSON data 保留 base64 文本。
2. `Write`：`apiPresets` 用 `<data>`（JSON base64）；已知但未映射的导出键优先写导入时保留的原始值，否则写该键的 **Mac 默认值**（不再写成空字符串，类型正确）；仍输出 `SettingsSnapshot.ExportedKeys` 全集合（Windows 既有行为，比 Mac「只写存在的键」多，但 Mac 导入时按正常偏好读取，不影响语义）。
3. `<data>` 未知键哨兵 `{"$data": …}`，保证 G6 往返时字节串类型不丢。
4. 密钥键过滤（导入与导出两侧），保证「不写密钥」。

## 测试

`Windows/tests/NextThought.Tests/MacSettingsPlistTests.cs`（夹具存在才跑；缺失时控制台打印 SKIP 原因并 return，xunit 2.9.3 无运行时 skip API）：

1. `RealMacBackupPlistHasTheKeysAndTypesThisPortMaps`：夹具 3 键集合/类型/值逐字；`apiPresets` `<data>` 解码字段逐字；Apply 后 `CloudStt=""`（zipformer）、`ApiPresets` 单条字段、`activeWorkspaceID` 原始保留。
2. `RealMacBackupPlistExportsBackToTheSameKeysAndValues`：导出键集合 = `SettingsSnapshot.ExportedKeys`；`activeWorkspaceID`/`transcriptionBackend` 值一致；`apiPresets` 仍 `<data>` 且 JSON 含 OpenAI 字段；二次 Apply 后所有映射键逐键相等、Mac 独有键保留；导出文本不含 apiKey/secret。
3. `KeyMaterialIsNeverImportedOrExported`：`apiKey`/`secretToken` 不导入；未知 `<data>` 以 `$data` 保留并回写成 `<data>`；导出不含 `sk-hidden`。
4. `WebSearchModeAndMacOnlyShortcutsKeepTheirMeaning`：off/always → bool 映射；`customShortcuts` 保留并回写。
5. 原有 3 个 plist 测试 + 相关 `MacSettingsTests`/`SettingsParityTable`/`Backup*` 一起 41/41 绿。

## 未解决 / 后续

- `webSearchMode` 的 `always` 目前在导出时退化为 `auto`，且 `Apply` 未写 `AppSettings.WebSearchMode`（`MacPreferenceMap` 不在 A12 允许改动范围）；要逐值往返需另派任务改 `MacPreferenceMap`。
- Windows 导出的 plist 键集合是 `SettingsSnapshot.ExportedKeys`（所有可导出键，含默认值），Mac 只写实际存在的键；导入 Mac 时行为一致，但若要「导出文件与 Mac 逐字节同构」需要再改导出策略（会动 `SettingsParityTableTests` 的断言）。
- 真实 Mac 端「把 Windows 导出的 settings.plist 导回 Mac 并打开」无法在本机验证（A16/人工）。
- 其它 `.ntbackup`（系列导出、增量链）不在 A12 范围，交 A16。
- 夹具缺失时测试打印 SKIP 说明并跳过（当前夹具已提交，测试实际执行）。
