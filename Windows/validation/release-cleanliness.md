# 发布前清理（R8 / §10.8）证据

本文件记录 NextThought for Windows 0.9.0 发布前清理的每一项检查、做法和结果。所有检查都由脚本或测试
自动化，可重复执行；命令全部在 `Windows/` 目录下运行。

## 1. 正式包不含 QA 开关、额外启动参数和样张页

脚本：`scripts/release_cleanliness.py`（`release.ps1` 在 publish 之后自动调用，命中即中止发布）。

检查方法：对 publish 出来的 payload 逐文件做 UTF-8/ASCII 与 UTF-16LE 字节搜索（跳过 `.pdb`/文档），
命中以下任一标记即失败：

- QA 逃生开关：`NEXTTHOUGHT_QA`、`NEXTTHOUGHT_GPU_RENDER`、`NEXTTHOUGHT_UPDATE_LAUNCH`、`--update-check=`
- `--self-test` / `--demo` / `--screenshots` 之外的启动参数：`--demo-churn`、`--demo-voices`、`--voices`、`--onboarding`
- 调试样张页：`ControlSampleWindow`、`TypographySampleWindow`、`ComponentSamplesWindow`、`LiveComponentSamplesWindow`

结果（0.9.0 的 publish payload，2026-11-21）：

```
release cleanliness: checked 247 payload files
release cleanliness: PASS (no QA switch, debug startup switch or sample page in the payload)
```

配套改动：

- `StartupOptions` 在正式构建里只解析 `--demo`、`--screenshots`、`--self-test` 三个参数；`--onboarding`
  在 `#if DEBUG` 内。demo 证据用的 voices/churn 改为 Mac 同款环境变量，不再是命令行参数。
- `UpdateFeedPolicy`（Platform，随包发布）不含任何 QA 环境变量名：它接收显式传入的
  override + 是否允许的布尔值；只有 Desktop 的 `UpdateFeed` 在 `#if DEBUG` 内读取
  `NEXTTHOUGHT_UPDATE_FEED` / `NEXTTHOUGHT_QA`，正式包无法被环境变量改更新源。
- 样张页 `ControlSampleWindow`、`ComponentSamplesWindow`、`TypographySampleWindow`、
  `LiveComponentSamplesWindow` 的类包在 `#if DEBUG`，并在 `NextThought.Desktop.csproj` 里用
  `<AvaloniaXaml Remove="…" Condition="'$(Configuration)' != 'Debug'">` 从 Release 资源中移除；
  打开它们的自检/截图代码同样在 `#if DEBUG` 内（Release 的自检记录一行“仅调试构建”）。
- `release.ps1` 的 publish 使用 `-p:RestoreLockedMode=true`，发布不再改动仓库里的 lock 文件；
  Core/Platform lock 增加 RID 需要的 `win-x64` 运行时节。

## 2. 日志不含密钥 / 令牌 / 配对码

脚本：`scripts/release_log_secret_check.ps1`。

方法：在隔离数据目录里把假 API Key、假 GitHub 令牌、假配对码写进所有“看起来是密钥”的设置字段，
跑 `--self-test=startup` 和 `--self-test=settings-presets` 两条主要流程（均退出码 0），然后扫描该目录下
所有日志/派生文件（排除作为输入的 settings.json），同时用常见令牌格式
（`sk-…`、`github_pat_…`、`ghp_…`、`tvy…`）做正则匹配。

结果（2026-11-21，`validation/release-cleanliness/log-secret-check.json`）：

```json
"passed": true, "scannedFiles": 2, "violations": []
```

## 3. 占位 / 临时 / TODO / FIXME / HACK

扫描：对 `src/**/*.cs|*.axaml` 搜索「占位」「临时」「TODO」「FIXME」「HACK」。结果记录在
`validation/release-cleanliness/source-scan.json`。

共 13 处命中，全部是**正常文案/测试术语**，不是未完成代码：

- Mac 字符串表/Windows 表的正常文案：`临时专业词`、`临时签名（ad-hoc）`、`中转地址…相当于临时遥控密钥`；
- 设计系统说明里的 `tertiary：占位与装饰`（token 用途说明）；
- 截图自检里的「临时截图」「过期后删除临时截图」等测试步骤名/注释。

`TODO`、`FIXME`、`HACK` 实际为 **0** 处；没有需要记入 `OPEN_QUESTIONS.md` 的清理项。

## 4. 没有被用到的 Core 文件 / 界面文件

扫描：`validation/release-cleanliness/source-scan.json`。

- 未被产品代码或测试引用的 Core 文件：**0**
- 未被引用（XAML 文件名/类型名不出现在其它源码或测试里）的 Desktop 界面文件：**0**

## 5. 默认设置与 Mac 相同

Core 测试 `SettingsParityDefaultsTests` 覆盖 113 个 Mac 设置键的默认值（与 Mac 3.4.0 对照）。

结果：`SettingsParityDefaultsTests` **9/9 通过**（2026-11-21）。

## 6. 其它发布前检查

- 截图/样张：控件样张页与字体样张页只在 Debug 构建出现（见第 1 节）；发布说明、README 与
  `RELEASE.md` 记录了未实机验证的 K11–K16 项。
- 更新拉取请求 #32 的说明由 Lead 维护，不在此提交。
- 完整的 `verify.ps1 -SkipScreenshots`（两套测试、UTC Core 一遍、Mac fixture 40/40）在 R8 改动后重跑，
  结果见提交说明与 Lead 的 CI run；`release.ps1 -Version 0.9.0` 的干净工作区全量由 Lead 在本机执行。

## 7. 复跑命令

```powershell
cd Windows
# 发布 payload 清洁检查（release.ps1 会自动跑；也可手动指定已 publish 的目录）
python scripts/release_cleanliness.py --payload artifacts\release\.stage\payload
# 日志密钥检查（需要先构建 Desktop）
./scripts/release_log_secret_check.ps1
# 源码清理扫描（占位/TODO 与未用文件）
python ..\..\..\..\..\..\Users\Thunderobot\AppData\Local\Temp\r8_source_scan.py   # 见 source-scan.json 的生成说明
# 默认值
dotnet test tests/NextThought.Core.Tests -c Release --filter "FullyQualifiedName~SettingsParityDefaultsTests"
```

`source-scan.json` 由一次性扫描脚本生成（可用仓库内的 `scripts/scan_views.py`/`scripts/core_usage.py`
辅助复现）；本轮结果已固定在 `source-scan.json`。
