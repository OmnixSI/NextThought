# Windows P2 预览版 0.2.0

P2-1 至 P2-5 的实现、验收和明确边界见 [P2_VALIDATION.md](P2_VALIDATION.md)。

- 左侧“资料库”导入资料；回答区勾选“参考资料库”，完成后选择引用并点击“查看来源”。
- 设置中勾选“使用本机 Codex CLI 账号”切换后端；先在终端安装并登录 Codex。模型留空采用账号默认模型。取消勾选后沿用已有 API 配置。
- 系统声音设置可选整个设备或具体应用进程树；麦克风开关独立。每次开始前重新选择应用，PID 不跨重启保存。
- Ctrl+Alt+N 恢复主窗，Ctrl+Alt+O 悬浮窗，Ctrl+Alt+R 开始/结束记录。快捷键冲突时使用界面按钮。主窗关闭仍正常退出；点击“收起到托盘”才隐藏。
- 左侧导出/导入 `.ntbackup`；导入前自动保留安全备份，相同 ID 保留现有项。Mac 专属字段和原文件保存在 Migration 原始档案，不等于全部 Mac 功能已实现。

验收 CLI 示例（在 Windows 源码目录运行，先生成合成样本并发布）：

```powershell
.\artifacts\NextThought-win-x64-0.2.0\tools\NextThought.Cli.exe documents-test --fixtures artifacts\p2\fixtures --data-root artifacts\p2\qa-data --json artifacts\p2\documents.json
.\artifacts\NextThought-win-x64-0.2.0\tools\NextThought.Cli.exe retrieval-test --fixtures artifacts\p2\fixtures --data-root artifacts\p2\qa-data --json artifacts\p2\retrieval.json --live
.\artifacts\NextThought-win-x64-0.2.0\tools\NextThought.Cli.exe codex-test --json artifacts\p2\codex.json
.\artifacts\NextThought-win-x64-0.2.0\tools\NextThought.Cli.exe process-audio-test --json artifacts\p2\process-audio.json
```

下面保留 P0/P1 使用说明与历史版本记录。

# NextThought Windows 预览版 0.1.1

原生 Windows 实现，基于上游 `af60a4def90c750872197f81ac8b91c726d05b63`。已通过 HECATE 实际单路/双路采音、DeepSeek 回答、分段纪要与重试，以及中断归档恢复验证。30 分钟受控回放单列记录，完整功能差异见 [PORTING_STATUS.md](PORTING_STATUS.md)。

## 直接运行

1. 将交付 ZIP **完整解压**，双击 `NextThought.exe` 或 `Launch.cmd`。程序自带 .NET 运行环境，不需要安装 Python、Node、CUDA 或 .NET SDK。当前验收平台为 Windows 11 x64。
2. 打开“设置”。本机已准备 X-ASR 480 ms 流式模型及整句精修模型；另一台机器首次使用时点击“准备语音组件”，下载约 270 MB 并校验 SHA-256。之后语音识别在 CPU 本地离线运行。
3. 先用“导入 WAV”验证识别。支持 `WaveFileReader` 可解码的 PCM / IEEE float WAV，自动转为单声道 16 kHz；不支持直接导入 MP3、M4A。当前导入文件按同一声音来源处理，没有说话人分离。
4. 开会时，在设置中选输出设备和麦克风，回到“会议记录”点击“开始记录”。线上会议通常选择系统输出设备混音及麦克风；线下会议可只选麦克风。结束后在“会后纪要”导出 Markdown。
5. 需要回答或纪要时，填写兼容 Chat Completions 的 API 地址、模型名称和自己的 Key，保存后检查连接。可使用支持此接口的本机服务。程序不含预付费服务或模型 Key；远程服务的费用由相应服务方收取。

“系统输出设备混音”包含所选设备上其他应用的声音。当前没有按会议进程筛选；应在会议前选对输出设备。设备拔插、睡眠和长时会议恢复仍待实机验收。自动回答及会后自动整理默认关闭。

HECATE 麦克风在本机验收时声音偏低，已使用软件内 4 倍增益完成固定句及句尾识别。在“设置 → 声音来源”可以调整 1–8 倍增益；其他设备从 1 倍开始调整。采音停止时会排空设备缓存再结束识别。自动问题会等本地发言结束后再回答；手动提问会清除待触发的自动问题。

回答服务现已按本机配置选择 DeepSeek 官方 `deepseek-flash`，推理强度为 `none`。纪要失败后再次整理会复用已完成片段；重新生成被取消时保留旧纪要。程序启动时会检查由本版本留下的中断会话，恢复完整行并保留修复前备份。另一台机器仍需自行填写服务与 Key。

## 数据和隐私

- 默认目录：`%LOCALAPPDATA%\NextThought`。`models` 为模型，`Sessions` 为会议，`settings.json` 为设置，`Credentials\answer-api.dpapi` 为 API 凭据。界面可直接打开数据目录。
- 原始音频只在内存中处理，不保存 WAV。转写、问题及回答会保存在本机。只有调用远程回答服务时，问题和相关转写才发送到所填服务。
- Key 用 Windows DPAPI 的当前用户范围加密，不进入普通设置文件。复制数据到另一台机器或另一个 Windows 账户后，需要重新输入 Key。
- 开发或隔离验证时，可在启动前设置环境变量 `NEXTTHOUGHT_DATA_DIR` 指向专用目录。设置只影响该进程及其子进程。
- 归档采用与上游对应的 `meta.json`、`transcript.jsonl`、`answers.jsonl`、`summary.json`。日期写为 UTC ISO 8601，界面显示本地时间。尚未实现 Mac 备份导入向导，也未验证跨平台完整往返。

## 源码与构建

目录结构：

```text
src/NextThought.Core       问题意图、候选跟踪、RMS VAD、数据结构
src/NextThought.Platform   WASAPI、Sherpa 原生桥接、模型安装、API、DPAPI、归档
src/NextThought.Windows    WPF 桌面窗口
src/NextThought.Cli        设备枚举、模型安装、WAV 转写诊断
tests/NextThought.Tests    上游问题语料和存储、流式接口回归
```

在 Windows PowerShell 中进入本文件所在目录，然后执行：

```powershell
py -3.12 scripts/bootstrap_sdk.py
.\scripts\build.ps1
.\scripts\build.ps1 -Publish
```

第一次构建需要网络。SDK 由微软官方下载并校验 SHA-512，放在项目 `.tools\dotnet`，不修改系统 PATH。脚本使用锁定的 NuGet 依赖，构建和测试通过后生成带版本号的 `artifacts\NextThought-win-x64-0.1.1` 以及 ZIP 和 SHA-256 文件。若系统已有匹配 SDK，也可使用它运行解决方案；`global.json` 固定 SDK 版本。

开发依赖：.NET SDK 10.0.403；C# 14 / WPF；Sherpa-ONNX 1.13.8；其 Windows 包中的 ONNX Runtime 1.28.2；NAudio.Core / Wasapi 2.2.1。所有识别模型与上游 SHA-256 保持一致。`xasr-bpe.vocab` 直接保留上游内嵌词表。

注意：Sherpa C API 的 Windows 字符串需要 UTF-8。本项目用显式 UTF-8 封送及 EXE 的 UTF-8 活动代码页清单处理中文路径。请通过 **EXE apphost** 启动识别程序；直接运行 `dotnet NextThought.Cli.dll` 会绕过该清单。

## 命令行输入、输出

发布包的 `tools\NextThought.Cli.exe` 无需另装运行环境。以下命令在发布包目录执行：

```powershell
# 输出本机系统播放端点、麦克风端点列表，不开始录音
& '.\tools\NextThought.Cli.exe' devices

# 输入模型选择及数据目录；下载、校验并安装模型
& '.\tools\NextThought.Cli.exe' install-models

# 输入 WAV；输出终端转写，以及含分段、推理耗时的 JSON 文件
& '.\tools\NextThought.Cli.exe' transcribe-wav 'D:\会议\样例.wav' --json '.\result.json'
```

开发环境的真实样例复现命令：

```powershell
py -3.12 scripts/prepare_test_models.py --data-root .cache/validation-data
& '.\src\NextThought.Cli\bin\Release\net10.0-windows\win-x64\NextThought.Cli.exe' transcribe-wav '.cache\test-audio\xasr480-0.wav' --data-root '.cache\validation-data' --json 'artifacts\asr\manual.json'
```

可选参数：`--no-refine` 关闭整句精修，`--model xasr160` 选择另一个流式模型，`--data-root` 指定数据目录。160 ms 模型通路已提供，当前验收使用 480 ms 模型。

## 当前验收与后续

已完成：Release 构建、37 项自动回归、4 段官方音频推理、模型安装、HECATE 单路/双路实采、真实 DeepSeek 回答和纪要、分段恢复、WASAPI 暂停故障注入及重新启动、独立进程异常退出后恢复、桌面 WAV 导入、历史与导出。测试报告在源码的 `artifacts/p1` 下。

验收范围限制：USB 物理拔插按用户要求跳过；真实系统休眠/唤醒、连续长时间硬件采音、其他麦克风与扬声器组合、160 ms 模型、干净 Windows 机器及完整 DPI/无障碍专项仍待验收。30 分钟受控 PCM 回放不能替代这些硬件测试。

待移植：Codex 后端、文档 OCR 与检索、声纹、翻译、手机伴侣、进程级声音过滤、备份迁移、托盘与悬浮窗、安装器与签名升级。当前 ZIP 为本地预览交付。

保留上游与第三方版权。上游没有顶层开源许可证，本地移植不改变其授权；第三方说明和许可证文本随包提供。
