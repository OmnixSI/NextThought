# InterviewHacker

InterviewHacker 是一个原生 SwiftUI macOS 悬浮助手，用于会议、面试和网课。它采集用户选择的屏幕或窗口系统音频，在本机进行中英文语音识别，再通过本机 Codex CLI 回答问题。也支持截取窗口或屏幕区域后提问。不包含模拟面试、面试评分、训练或题库。

## 数据处理位置

- 系统音频在本机识别，不保存原始音频。
- 转录文本、当前问题、导入资料和用户主动截取的图片会交给 Codex CLI，并由 Codex 服务处理。应用不调用 OpenAI API，也不要求 API Key。
- Codex 复用本机 CLI 登录态。每次回答调用都传入 `gpt-5.6-luna` 和 `model_reasoning_effort=none`，忽略用户全局配置，不修改 `config.toml`。请求使用 `--sandbox read-only`，工作目录为独立临时目录；会议内容、资料和图片均作为不可信数据处理。
- 本机 Codex CLI 的 JSONL 输出目前只观察到完成事件，没有文本增量事件；界面会显示“正在生成”，收到完整回答后再展示，不冒称逐 token 流式输出。

## 语音识别

应用包包含 Apple Silicon `whisper-server` 常驻运行时，普通用户不需要安装 Homebrew、Python 或 Whisper。识别模型单独下载到：

`~/Library/Application Support/InterviewHacker/models/ggml-large-v3-turbo-q5_0.bin`

模型详情：

- 名称：Whisper `large-v3-turbo-q5_0`，多语言模型，支持中文和英文；默认自动识别，也可在设置选择中文或英文。
- 大小：574,041,195 字节（约 547 MiB）。
- SHA-1：`e050f7970618a659205450ad97eb95a18d69c9ee`。
- 来源：[ggerganov/whisper.cpp 模型仓库](https://huggingface.co/ggerganov/whisper.cpp)。运行时和权重为 MIT 许可；权重不包含在应用包内。
- 首次启动应用后，在“设置 → 本地语音识别”下载模型。界面显示进度，可取消、重试、检查完整性和删除模型。

ScreenCaptureKit 请求 48 kHz 双声道系统音频，应用用 `AVAudioConverter` 转成 16 kHz 单声道 Int16。音频缓冲、VAD 和分段在专用队列处理；模型驻留在本机辅助进程中，片段复用已加载模型，不逐段启动和加载。VAD 使用短前缓冲、静音收尾和有界转录队列；连续语音达到上限时带重叠切段，临时结果不会重复写入最终会话。

## 首次运行

1. 安装并登录 ChatGPT/Codex 桌面客户端，确认本机 Codex CLI 可用。
2. 打开 InterviewHacker 的设置页，刷新 Codex 状态并下载本地语音模型。首次下载需要网络。
3. 回到助手页，选择会议窗口或整个屏幕，再点击“开始辅助”。
4. macOS 首次请求屏幕与系统音频权限时，选择允许。若之前拒绝，到“系统设置 → 隐私与安全性 → 屏幕与系统音频录制”允许 InterviewHacker，再回应用刷新目标或重新启动辅助。
5. 可手动输入问题；也可开启规则型自动问题检测。点击窗口截图或区域截图后，可预览、取消或交给 Codex 分析。

模型、Codex CLI、目标音源和权限都就绪后，助手页会显示“可以开始”。没有 Codex 登录、模型校验失败或没有应用内识别运行时时，开始按钮会保持禁用。

## 构建与运行

已验证的目标是 Apple Silicon arm64、macOS 14 或更新版本。应用不要求终端或 Homebrew；从源码构建需要 Swift 6、Apple Command Line Tools 和 CMake。Whisper.cpp v1.9.4 源码固定在 `Vendor/whisper.cpp`，构建脚本会编译并把常驻运行时打进 app：

```sh
DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer ./scripts/build-app.sh
open dist/InterviewHacker.app
```

生成文件：

- `dist/InterviewHacker.app`
- `dist/InterviewHacker-macOS.zip`

首次构建会创建 `InterviewHacker Local Development` 自签名开发身份，保存在被 Git 忽略的 `.local-signing/` 目录。后续构建复用相同证书，为 `com.interviewhacker.app` 和辅助程序写入固定的 designated requirement；代码变化会改变 CDHash，但不改变签名身份和权限匹配条件。不要删除或重新生成 `.local-signing/`，否则 macOS 会把它当作不同应用，可能再次要求隐私授权。此身份只用于这台 Mac 的本地开发与验证，不是 Apple Development 或 Developer ID 证书；应用没有 Developer ID 签名或公证，不适合公开分发。私钥不会放入应用包或 ZIP。

构建脚本把静态链接的识别服务、开源许可和第三方声明放进 App 包；模型权重在首次运行时下载。

## 功能与限制

- 支持开始、暂停、恢复、停止，屏幕/窗口系统音频选择，手动提问、规则型自动检测、最近 5 分钟转录上下文、资料导入、会话保存/导出/删除和固定快捷键。
- 自动检测覆盖常见中英文问句、解释请求和追问；不对每个词调用 Codex。检测使用启发式规则，仍可能漏掉或误判问题。
- 窗口截图先在应用内列出可见窗口，按窗口 ID 捕获；区域截图通过 ScreenCaptureKit 读取所选显示器，再在应用内预览、拖动裁切。捕获时隐藏助手窗，完成或取消后恢复窗口。图片会按比例缩小到最长边 2,400 像素并以 JPEG 质量 0.9 交给 Codex；代码截图要求模型先核对相关行是否清楚，看不清时请求更清晰图片。
- 麦克风独立采集、多人说话人识别、自定义快捷键和扫描版 PDF OCR 尚未实现。
- 系统音频和截图仍受 macOS 隐私授权控制。用户拒绝权限后，应用不会绕过系统授权。

## 验证记录

验证日期：2026-10-14。设备为 MacBook Air、Apple M4、16 GB 内存、macOS 26.5.2；Xcode 26.0.1、Swift 6.2、Codex CLI `0.162.0-alpha.17.2`。工程最低部署版本为 macOS 14，构建产物为 arm64。

### 构建、签名和权限

- `swift test --disable-sandbox --scratch-path .build/swift-tests`：26 项测试全部通过，覆盖 VAD、跨片段问题合并与去重、5 分钟上下文、提示词 JSON 转义和长度上限、Codex JSONL 事件解析。
- `scripts/build-app.sh` 完成 Release 构建；`codesign --verify --deep --strict` 验证通过，app 和 `Contents/Helpers/whisper-server` 均为 arm64；`dist/InterviewHacker-macOS.zip` 通过 `unzip -t`。
- 当前签名身份指纹为 `5B8B4BBACA96E69F133B309314530534304E9092`，designated requirement 固定为应用标识 `com.interviewhacker.app` 加证书指纹。源代码修改前的 CDHash 是 `f4dd7b529267abba59afd29f2aa2ee8a394a7fb6`；区域截图版本为 `6791370b69c72529f6f3d23967ecaec5a84f48ba`；本次提示词版本为 `5b13d0365fd69d86ad81d9cf7344dcfb76141613`。重启后的最终构建记录 `permission.checked: granted=true`，并读取 28 个捕获目标；没有重置 TCC 权限。
- 这是本机自签名开发版本（没有 Apple Team ID、Developer ID 签名或公证）。保留 `.local-signing/` 才能在此 Mac 上复用代码签名身份和隐私授权。将应用复制到另一台 Mac 或删除本地密钥链后不保证能直接启动或复用权限。

### 本地语音识别和系统音频

权重是多语言 `large-v3-turbo-q5_0`，SHA-1 校验通过。模型文件 574,041,195 字节，缓存于 `~/Library/Application Support/InterviewHacker/models/`。Whisper.cpp v1.9.4 的运行时在最终 app 包内，服务常驻并复用已加载模型。运行日志报告 `Apple M4`、`using MTL0 backend`，确认这次推理实际启动 Metal 后端；不是仅凭编译参数推断。

- 首次 Metal 初始化/模型服务就绪耗时 32.73 秒；之后同机重启观察到 0.53–0.80 秒。首次耗时包括 Metal shader 初始化，不能代表普通启动。
- 一个已采样的 Whisper 服务 RSS 快照约 758–802 MiB；CPU-only 样本的一次 `ps` 快照约 103% CPU。未测峰值内存、GPU 利用率或长时间会议的资源曲线。
- WAV 文件测试使用合成音频，通过本地 whisper.cpp 服务识别：英文 9.94 秒音频耗时 7.95 秒；中文 6.36 秒耗时 9.00 秒；中英混合 7.55 秒耗时 7.54–8.39 秒。英文的 `database index` 曾误为 `database syntax`，中文“数据库索引”也曾出现同音转写错误。没有计算准确率。
- 应用内的真实系统音频测试由合成 WAV 播放器输出，ScreenCaptureKit 实际输入为 48 kHz、双声道、32-bit float、非交错；应用转换为 16 kHz、单声道、16-bit PCM。不是麦克风录音。更早的中英文测试中，4.65 秒和 5.65 秒片段分别识别耗时 11.77 秒、11.68 秒；中文 7.65 秒片段耗时 13.32 秒。最终签名版的中英混合片段（VAD 得到 8.65 秒）识别耗时 9.85 秒，随后 Codex 用 6.12 秒返回自动回答。系统音频从开始采集、VAD、Whisper、自动问题检测到回答展示已端到端成功。
- 分段设置限制单段最长 12 秒，连续语音保留 0.5 秒重叠，等待队列上限为 3。实测英文和中文转写慢于音频播放速度；在较长或连续语音下可能积压或丢弃旧段，这是当前 MVP 的主要限制。

### Codex 问答和截图

最终包中打开设置页后，应用显示 Codex CLI 已登录并检测到上述版本；模型设置页显示本地模型存在且通过完整性校验。实际打包 app 成功回答手动中文问题，Codex CLI 用时 3.77 秒。自动混合语音问题回答用时 6.12 秒；请求日志确认模型 `gpt-5.6-luna`、推理强度 `none`，退出码 0，并只收到完整 `item.completed:agent_message` 事件，所以 UI 显示“正在整理回答”后呈现完整结果。取消中的 CLI 请求退出码为 15，旧答案保持显示，没有被晚到结果覆盖。

- 已生成 `validation/fixtures/binary-search.png` 和 `math.png`，分辨率为 1600×1100。最终包通过应用内窗口列表选择并捕获了合成二分查找题图；预览可读，记录的截图尺寸为 1624×1250。应用将图像缩放到最长边不超过 2,400 像素并以 JPEG 质量 0.9 交给 Codex。回答准确定位 `left = mid` 未推进边界导致死循环，给出 `left = mid + 1`、可运行修复和 `O(log n)`/`O(1)` 复杂度；Codex CLI 用时 6.25 秒。
- 暂停、恢复和停止在最终签名版上逐项操作，界面状态正确变化；开始后实际采集系统音频成功，停止后释放采集。窗口选择器可取消，截图卡片也可清除。另对一个 Codex 请求点了取消，旧回答保留。
- 最终版区域截图在应用内显示显示器预览；实际拖动选择、取消、裁切预览和 Codex 图片回答均已执行。一次测试从桌面上的合成二分查找题缩略图裁得 426×283 像素，Codex CLI 用时 4.12 秒，正确指出 `left = mid` 可能不推进边界并给出 `left = mid + 1`。同一低分辨率裁切在较早提示词下曾误报缩进问题；提示词加入“先核对相关代码行，无法辨认时不推测”后重测得到正确回答。窗口截图的 1624×1250 全图问答另已通过。区域截图这次验证来源是桌面缩略图，未测原尺寸 1600×1100 图片在区域选择器中的清晰度。
- 没有连接第二台显示器；多显示器坐标行为未实测。会话保存/导出/删除、模型重新下载取消与断网重试、音源消失后的恢复也未在这次回归中逐项验证。

提示词原文、截图默认问题、合成音频台词和复现说明见 [`validation/real-time-answer-prompts.md`](validation/real-time-answer-prompts.md)。测试 WAV 为合成语音，不能当作真人录音准确率评估；设备、样本时长和测量阶段均如上注明。

## 未完成能力与发布限制

- 区域截图完整交互已实测；本机仅有一个显示器，多显示器与不同缩放比例尚未实测。原尺寸区域图的可读性仍需补测。
- 持续系统音频转录慢于实时语速，长时间会议可能排队或丢弃旧段；当前没有可报告的通用准确率。
- 独立麦克风采集、多说话人识别、自定义快捷键、扫描版 PDF OCR 尚未实现。
- 本地自签名应用只适合此开发 Mac 上验证。正式分发需要 Developer ID 签名、公证和另一台干净 Mac 的安装测试。

本机运行步骤：确认 ChatGPT/Codex 客户端已登录；打开 `dist/InterviewHacker.app`；首次运行在设置中确认 Codex 状态并下载本地模型；在 macOS 隐私与安全性中允许“屏幕与系统音频录制”；回助手页开始辅助。重建后使用相同工作区、`.local-signing/` 和 `scripts/build-app.sh`，本次验证已确认系统权限保持有效。
