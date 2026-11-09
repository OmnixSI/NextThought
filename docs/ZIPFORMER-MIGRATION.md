# Zipformer 迁移与本机验证

日期：2026-10-15

## 实现

- 本机默认引擎现为 Sherpa-ONNX C API 的在线 Zipformer transducer。Sherpa-ONNX 固定为 `1.13.8`，SwiftPM 同时静态链接 ONNX Runtime `1.28.2`；应用无需 Python、Homebrew、独立动态库或服务进程。
- 使用官方清单中的 `csukuangfj/sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20`，适用于在线流式中英识别。上游把中、英文都列为支持语言，并提供在线麦克风用法。
- 权重仓库固定在 Hugging Face 修订 `98590b7ed6443e77b714204da2757d75e1a642f4`，许可证 Apache-2.0。文件 SHA-256、Sherpa 与 ONNX Runtime 许可记录在 [`Resources/ThirdPartyNotices.txt`](../Resources/ThirdPartyNotices.txt)。模型只在首次下载时获取，放在用户 Application Support 中，不打进应用包。
- 在线认知器由系统音频和麦克风两个独立流共享只读权重；PCM 按 16 kHz 单声道以 100 ms 块送入，Int16 转为 Float。C API 排入串行后台队列，临时字幕使用每次返回的完整假设替换。只使用现有 700 ms VAD 作为句尾信号，Zipformer 自带端点关闭；句尾结束旧在线流并排空，创建新流，停止时排空并提交尾句。
- 专业词设置通过 Sherpa transducer 的 modified beam search / BPE hotwords 作为提示输入。实测词项仍会被误识别，界面和文档不承诺准确率提升。
- 首次启动时把已保存的 `whisper` 本地引擎选项迁移到 Zipformer。OpenAI 转写的旧 Whisper 模型标识迁移为空模型并采用当前服务默认值；Soniox、Deepgram 中含相同字样的自定义模型名保留。历史 Whisper 模型目录及全局缓存不扫描、不加载、不删除。

官方说明：[在线 Zipformer 模型清单](https://k2-fsa.github.io/sherpa/onnx/pretrained_models/online-transducer/zipformer-transducer-models.html)、[模型仓库固定版本](https://huggingface.co/csukuangfj/sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20/tree/98590b7ed6443e77b714204da2757d75e1a642f4)、[Sherpa hotwords 指南](https://k2-fsa.github.io/sherpa/onnx/hotwords/)、[Sherpa SwiftPM v1.13.8](https://github.com/k2-fsa/sherpa-onnx/tree/v1.13.8)。

## 识别回放结果

2026-10-15，在本机 Apple Silicon 上运行 `ZipformerRuntime` 的真实 ONNX 推理。项目内 `Tools/Validation/Fixtures/{en,zh,mixed}.wav` 是合成 WAV。另临时下载了三条上游公开测试语音，均为 16 kHz 单声道 Int16 PCM，只用于本次验证，未放进项目或应用包：

- 英文 `test_wavs/0.wav`，6.625 秒，来自 [Sherpa-ONNX 英文 streaming Zipformer 仓库](https://huggingface.co/csukuangfj/sherpa-onnx-streaming-zipformer-en-2023-02-21/tree/27a97ca82b8c24718494836ee4d6ca94a3783e1c)，SHA-256 `6bc58a4efdf20daac252b6b1502632601a71efe0308f6757dc1eda34891a7e4f`。
- 中文 `DEV_T0000000000.wav`，5.612 秒，来自 [WenetSpeech streaming Zipformer 仓库](https://huggingface.co/k2-fsa/icefall-asr-zipformer-wenetspeech-streaming-small/tree/26e0e0501af3b8b32da2e2a600ae350e114111d2/test_wavs)，SHA-256 `668bf8df51a10027b84d5d8816a1ce11ae93545538dc05cfe2aa6811d399c250`。
- 中英混说 `test_wavs/1.wav`，5.100 秒，来自[双语 Zipformer 原始模型仓库](https://huggingface.co/csukuangfj/k2fsa-zipformer-bilingual-zh-en-t/tree/70a41b5022876b134f4c1322a721b7dc8d12eb1a/test_wavs)，SHA-256 `8bfb42c963e623ebab31b81ff4404867d07d3102507c87ac14577c4c61663b8c`。识别文本与 [Sherpa 在线模型文档示例](https://k2-fsa.github.io/sherpa/onnx/pretrained_models/online-transducer/zipformer-transducer-models.html)相同。

分块按 100 ms 节奏回放的结果：

| 输入 | 音频长度 | 最终输出 | PCM 转换与 C API 累计执行时间 / RTF | 首次临时文本 | 末音频块至 final |
| --- | ---: | --- | ---: | ---: | ---: |
| 合成英文 | 9.945 s | `local spee tridic addition sixty locs improved squary st` | 0.889 s / 0.089 | 0.148 s | 停止后 0.931 s |
| 合成中文 | 6.361 s | `这是一个本地语音识别测试请解释数据库所引如何提升查询速度` | 0.386 s / 0.061 | 0.094 s | 追加静音触发 VAD 后共 1.372 s |
| 合成中英混说 | 7.545 s | `这个 system design需要考虑 cash invlidation和 day to days consistency请解释一下 treat of` | 0.710 s / 0.094 | 0.066 s | 追加静音触发 VAD 后共 1.223 s |
| 上游英文语音 `0.wav` | 6.625 s | `AFTER EARLY NIGHTFALL THE YELLOW LAMPS WOULD LIGHT UP HERE AND THERE THE SQUALID QUARTER OF THE BROTH` | 0.605 s / 0.091 | 0.135 s | 追加静音触发 VAD 后共 0.729 s |
| 上游中文语音 `DEV_T0000000000.wav` | 5.612 s | `对我做了介绍那么我想说的是呢大家如果对我的研究感兴趣呢` | 0.524 s / 0.093 | 0.094 s | 追加静音触发 VAD 后共 0.890 s |
| 上游中英混说语音 `1.wav` | 5.100 s | `这是第一种第二种叫呃与 ALWAYS ALWAYS什么意思啊` | 0.518 s / 0.102 | 0.132 s | 追加静音触发 VAD 后共 1.050 s |
| 合成短句（3 秒裁切） | 3.000 s | 有 final 输出 | 0.264 s / 0.088 | 未计时（未实时节奏回放） | 停止时提交尾句 |
| 长回放（英文、中文、混说样本串接） | 24.251 s | 有 final 输出 | 1.678 s / 0.069 | 未计时（未实时节奏回放） | 停止时提交尾句 |

“首次临时文本”是回调交付相对 PCM 样本时间戳的间隔，不是屏幕绘制延迟；累计执行时间包括 Int16 到 Float 转换、C API 接收音频/解码调用的测量区间，不含后台队列排队时间。句尾样本在主音频之后补入静音，最终时间同时含 VAD 的 700 ms 门限和结束解码/调度时间。实际回放还覆盖了系统音频/麦克风双流、设备切换时提交旧流并继续解码新流、重复停止不重复提交、静音不产出文本。测试直连相同 PCM/VAD/推理路径，没有启动 ScreenCaptureKit 或物理麦克风，因此本次没有测量实际设备采集延迟或 UI 绘制延迟。

## 质量限制

上游英文和中文语音都有对应文字输出；英文例句末尾的 `BROTHELS` 被截成 `BROTH`，模型对同一合成英文 WAV 的识别明显较差。中英混说公开样本保留了中文和 `ALWAYS`，合成技术词样本仍把 `cache invalidation`、`database consistency`、`trade-off` 识别错。当前证据能确认在线流式运行、独立双路状态、静音过滤、短尾/停止尾句和设备重置提交行为，以及本机处理速度快于输入速度；**不能据此声称英文或中英混说达到可用的通用准确率**。没有在实际会议、噪声条件或各种麦克风/系统音源上测试，也没有测长时间内存曲线。

运行同样的本机验证：

```sh
IH_ZIPFORMER_MODEL_DIR="$HOME/Library/Application Support/NextThought/models/sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20" \
  swift test --disable-sandbox --filter ZipformerStreamingIntegrationTests
```

另外设置 `IH_ZIPFORMER_PUBLIC_AUDIO_DIR` 指向含有 `english.wav`、`chinese.wav`、`mixed.wav` 三个 16 kHz 单声道 Int16 WAV 的目录，可启用可选公开语音回放。无模型环境时，这项本机模型集成测试直接返回；CI 仍运行其它 Swift 测试。
