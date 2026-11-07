# NextThought Windows 预览版 0.1.0

第一阶段的原生 Windows 实现，基于上游 `af60a4def90c750872197f81ac8b91c726d05b63`。当前打通录音导入、本地中英转写、会议归档及导出；系统声音与麦克风采集、在线回答和纪要代码已接入，仍需实际会议验收。完整功能差异见 [PORTING_STATUS.md](PORTING_STATUS.md)。

## 直接运行

1. 将交付 ZIP **完整解压**，双击 `NextThought.exe` 或 `Launch.cmd`。程序自带 .NET 运行环境，不需要安装 Python、Node、CUDA 或 .NET SDK。当前验收平台为 Windows 11 x64。
2. 打开“设置”。本机已准备 X-ASR 480 ms 流式模型及整句精修模型；另一台机器首次使用时点击“准备语音组件”，下载约 270 MB 并校验 SHA-256。之后语音识别在 CPU 本地离线运行。
3. 先用“导入 WAV”验证识别。支持 `WaveFileReader` 可解码的 PCM / IEEE float WAV，自动转为单声道 16 kHz；不支持直接导入 MP3、M4A。当前导入文件按同一声音来源处理，没有说话人分离。
4. 开会时，在设置中选输出设备和麦克风，回到“会议记录”点击“开始记录”。线上会议通常选择系统输出设备混音及麦克风；线下会议可只选麦克风。结束后在“会后纪要”导出 Markdown。
5. 需要回答或纪要时，填写兼容 Chat Completions 的 API 地址、模型名称和自己的 Key，保存后检查连接。可使用支持此接口的本机服务。程序不含预付费服务或模型 Key；远程服务的费用由相应服务方收取。

“系统输出设备混音”包含所选设备上其他应用的声音。当前没有按会议进程筛选；应在会议前选对输出设备。设备拔插、睡眠和长时会议恢复仍待实机验收。自动回答及会后自动整理默认关闭。

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

第一次构建需要网络。SDK 由微软官方下载并校验 SHA-512，放在项目 `.tools\dotnet`，不修改系统 PATH。脚本使用锁定的 NuGet 依赖，构建和测试通过后生成 `artifacts\NextThought-win-x64` 以及 ZIP 和 SHA-256 文件。若系统已有匹配 SDK，也可使用它运行解决方案；`global.json` 固定 SDK 版本。

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

已完成：Release 构建、自动回归、4 段官方模型测试音频的真实 ONNX 推理、模型安装器、设备枚举、桌面 WAV 导入、历史重载、Markdown 导出。测试 JSON、TRX 和样例导出在源码的 `artifacts` 下；具体记录见移植状态文档。

待验收：真实双路采音与回声、长时间会议、160 ms 模型、真实服务 Key 的回答及纪要、另一台干净 Windows 机器、DPI 和无障碍完整测试。

待移植：Codex 后端、文档 OCR 与检索、声纹、翻译、手机伴侣、进程级声音过滤、备份迁移、托盘与悬浮窗、安装器与签名升级。当前 ZIP 为本地预览交付。

保留上游与第三方版权。上游没有顶层开源许可证，本地移植不改变其授权；第三方说明和许可证文本随包提供。
