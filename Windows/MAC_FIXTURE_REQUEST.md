# Mac 数据夹具请求（#79 数据层双向恢复）

## 目的

Windows 侧已经能读写 Mac 的键名、ISO-8601 日期和枚举值，但真实双向恢复还没有验收。请群主在 Mac 上导出以下夹具，我们拿到后执行：

`Mac 导出 → Windows 导入 → Windows 再导出 → Mac 重新打开`

## 一、运行自检（必须先做）

在 Mac 上打开终端，从 app 包内运行；`SelfTest` 要求在 `NextThought.app` 内启动：

```bash
APP=/Applications/NextThought.app
SCRATCH="$HOME/nt-fixtures/data"
EXPORTS="$HOME/nt-fixtures/exports"
mkdir -p "$SCRATCH" "$EXPORTS"

cd "$APP/Contents/MacOS"
NEXTTHOUGHT_DATA_DIR="$SCRATCH" NEXTTHOUGHT_SELFTEST_EXPORT_DIRECTORY="$EXPORTS/session-export" ./NextThought --self-test session-export

NEXTTHOUGHT_DATA_DIR="$SCRATCH" ./NextThought --self-test session-backup-drain
```

说明：

- `session-export` 验证 Markdown/DOCX/PDF/SRT/VTT 导出，产物在 `$EXPORTS/session-export`；**它不是 `.ntbackup`**。
- `session-backup-drain` 验证最终翻译写盘后才生成自动备份，会在 `$SCRATCH/Backups/RestorePoints` 下生成 `.ntrestore` 恢复点。
- 两个命令都成功后再做下面的 `.ntbackup` 导出，不要只发截图。

## 二、需要导出的 `.ntbackup`

在 Mac 应用里用 **数据 → 备份 → 导出全部**（不勾选“包含凭据/API Key”），把文件命名为：

```
$HOME/nt-fixtures/ntbackup/all-scenarios.ntbackup
```

如果 Mac 应用只能分别导出，请按场景各导出一份：

| 场景 | 文件 | 必须包含 |
|---|---|---|
| A. 纯文本场次 | `01-text-only.ntbackup` | 至少 1 场会议、2 条纯文本转写、纪要；不要求翻译/声纹 |
| B. 多说话人 + 声纹 | `02-voiceprints.ntbackup` | 至少 1 场会议、3 个不同说话人、`voices.jsonl`/说话人标记 |
| C. 术语库 | `03-glossary.ntbackup` | 非空 `Glossary/glossary.json`，含术语和人名；最好有一场使用这些词的会议 |
| D. 增量备份链 | `04-incremental-chain.ntbackup` | 至少 3 个 `.ntrestore` 恢复点 + 对应 `Objects/`；或提供整个 `Backups/` 目录的压缩包 |

每个 `.ntbackup` 都必须是 **`includesSecrets=false`**，不能包含 `secrets.plist`、Keychain 内容或 API Key。

## 三、一并提供

1. `$SCRATCH` 整个目录的压缩包（包含 `Sessions/`、`Workspaces/`、`Glossary/`、`Backups/`）。
2. 每份 `.ntbackup` 的 SHA-256。
3. 环境说明：
   - Mac 型号、macOS 版本；
   - `NextThought` 版本与 build（菜单“关于”或 `Info.plist` 的 `CFBundleShortVersionString`/`CFBundleVersion`）；
   - 是否 Apple Silicon / Rosetta。
4. `session-export` 和 `session-backup-drain` 的完整终端输出，保存为 `selftest-output.txt`。

## 四、交付方式

把 `nt-fixtures/` 打成一个 ZIP：

```
nt-mac-fixtures-<macOS版本>-<日期>.zip
```

发给我；不要只发截图或单个 JSON 文件。我们收到后会：

- 用 Windows `BackupService` 导入；
- 再导出新的 `.ntbackup`；
- 在 Mac 上重新打开/导入，逐字段核对会话、系列、术语、经验、题库、声纹、恢复点。

## 五、当前阻塞

没有真实 Mac 夹具时，Windows 侧只能做本机回环和人工构造 JSON 测试，不能声称“Mac 双向恢复通过”。本任务等到这批夹具后再验收。

## A15：在 Mac 上运行 MacCodec emit

在仓库根目录执行（只运行命令行工具，不构建/安装/签名应用）：

```bash
swift run --package-path Windows/tools/MacCodecMac emit --output Windows/tests/Fixtures/mac
```

产物应包含：`session-meta.json`、`transcript.jsonl`、`translations.jsonl`、`questions.jsonl`、`summary.json`、`user-content.json`、`workspace.json`（含一条被替换过的系列条目）、`skills.json`、`templates.json`、`glossary.json`、`backup-manifest.json`、`revisions.json`（A11 三个修订号样本与期望值）。

把这些文件提交到 `windows/parity` 分支的 `Windows/tests/Fixtures/mac/`，并注明 Mac 型号、macOS 版本、NextThought 版本/build。Windows 侧只运行：

```powershell
Windows\.tools\dotnet\dotnet.exe run --project Windows/tools/MacCodecCheck/MacCodecCheck.csproj -- --fixtures Windows/tests/Fixtures/mac
```

依赖 7 个 CryptoKit 文件和 4 个平台文件的类型不在 Windows check 范围内，工具会逐项打印跳过原因。
