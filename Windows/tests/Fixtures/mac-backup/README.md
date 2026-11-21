# Mac 备份夹具（#79 数据层双向恢复）

`nt-mac-fixtures-26.5.2-20261121.zip` 是按 `Windows/MAC_FIXTURE_REQUEST.md` 第一到第三节在 Mac 上导出的整包，解开后是 `nt-fixtures/`。

ZIP SHA-256：`afbc3eb281bc2a9a5fc449a40709edc85c28a20f8fbe47f9dbd595710b4366d8`

各份备份的 SHA-256：

```
89c60715cbf17225d51b3effa3ac133d2e2bb0c18369860fff2f2b23f6d2d507  01-text-only.ntbackup
1261f7ba36468854bf31dd77a40f5fc049f2fa2058ce4c5e45917f613feaef86  02-voiceprints.ntbackup
f7b902112dd294f7bca3213c0ae52bb5fbe0c3ddf0dbcd11c3dfb2c92fe72c74  03-glossary.ntbackup
78637d64b3dcdaf77351ee45272b70c65fdf875cf9167b763a496593ace18f56  all-scenarios.ntbackup
fc03b2bdda8fd3bd2d2dfa0ca2febccc8fc3e25c2505bd7137ff88af919bc8fe  04-incremental-chain-Backups.zip
```

## 环境

- Mac：Mac14,10（MacBook Pro，Apple M2 Pro），Apple Silicon 原生运行，不经 Rosetta
- macOS 26.5.2（25F84）
- NextThought 3.4.0 build 3400，源码提交 f015e8c6（main：3.4.0 加 `backup-fixtures` 自检）
- 应用副本 bundle id 改为 `com.nextthought.app.fixtures`、临时签名，偏好设置域是新建的空域；不碰正式安装的应用和真实会议记录

## 怎么生成的

```bash
cd NextThought.app/Contents/MacOS
NEXTTHOUGHT_DATA_DIR="$SCRATCH" NEXTTHOUGHT_SELFTEST_EXPORT_DIRECTORY="$EXPORTS/session-export" ./NextThought --self-test session-export
NEXTTHOUGHT_DATA_DIR="$SCRATCH" ./NextThought --self-test session-backup-drain
NEXTTHOUGHT_DATA_DIR="$SCRATCH" ./NextThought --self-test backup-fixtures --output "$HOME/nt-fixtures/ntbackup"
```

`backup-fixtures` 用应用自己的存储类（WorkspaceStore、SessionArchive、GlossaryStore、SkillLibrary）写入三个系列各一场会议，每写完一个系列调一次 `DataBackup.automaticBackup` 生成恢复点，最后用 数据 → 备份 背后的 `DataBackup.exportAll` / `exportWorkspace` 导出。会议内容都是编的。每份导出都用 `prepareImport` 读回，确认 `includesSecrets=false` 且没有 `secrets.plist`。

## 内容

| 文件 | 内容 |
|---|---|
| `ntbackup/all-scenarios.ntbackup` | 全部导出（kind=full）：4 个系列、4 场会议、全局术语表、技能、笔记模板、设置 |
| `ntbackup/01-text-only.ntbackup` | 系列导出（kind=workspace）：纯文本组会，2 句转写、纪要、个人笔记，无翻译无声纹 |
| `ntbackup/02-voiceprints.ntbackup` | 系列导出：分工会，3 个说话人（我、声音 1「李工」、声音 2），含 `voices.jsonl` 和 `meta.voiceNames` |
| `ntbackup/03-glossary.ntbackup` | 系列导出：识别模型例会，系列术语表 X-ASR，会议里用到 Zipformer、Sherpa-ONNX、王老师；挂了一个技能 |
| `ntbackup/04-incremental-chain-Backups.zip` | 整个 `Backups/` 目录：4 个 `.ntrestore` 恢复点（session-backup-drain 1 个 + 夹具 A/B/C 各 1 个）和 `Objects/` |
| `ntbackup/SHA256SUMS.txt` | 上面五个文件的 SHA-256 |
| `data/` | `$SCRATCH` 整个目录：`Sessions/`、`Workspaces/`、`Glossary/`、`Backups/`、`Skills/` 等 |
| `exports/session-export/` | session-export 自检的 Markdown / DOCX / PDF / SRT / VTT |
| `selftest-output.txt` | session-export 和 session-backup-drain 的完整终端输出（本机路径换成了 `$SCRATCH` / `$EXPORTS`） |

## 导入时要注意

- 全局术语表只在全部导出（`all-scenarios`）里；系列导出按 Mac 的设计只带系列自己的术语表（`Workspace.glossary`）和挂在系列上的技能。
- Mac 打的 ZIP 里有 `__MACOSX/` 和 `._*` AppleDouble 条目，Mac 导入时会忽略，Windows 导入也要忽略。
- 设置在 `settings.plist`（XML plist），`apiPresets` 是 base64 的 JSON 数据，不含密钥。
