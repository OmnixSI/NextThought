# 本地语音识别模型调研（2026-10）

目标：在 Apple Silicon Mac 上本地运行，**实时性**（边说边出字、说完 0.3–0.8 秒内定稿）、**准确率**（普通话、英语、中英混说）都尽量好，并且能从 Swift 应用里直接调用。

## 结论

| 用途 | 选择 | 原因 |
| --- | --- | --- |
| 流式（边说边出字） | **X-ASR zh-en 流式 Zipformer**（480 ms 块，带标点） | 2026-06 发布，约 100 万小时训练，自带标点与大小写，中英混说明显好于 2023 版；与原来的 Zipformer 用同一套 sherpa-onnx 接口，替换成本最低 |
| 整句精修（第二遍） | **X-ASR zh-en 离线 Zipformer**（带标点） | 与流式模型同一词表；看到整句后再识别，WenetSpeech 会议集 CER 7.06%（流式 9.11%），本机 RTF ≈ 0.04 |
| 更高准确率（可选，未内置） | Qwen3-ASR-0.6B / FireRedASR2 | 准确率更高，但约 0.9–1.2 GB、解码更慢，两路同时用对 CPU 压力大 |

应用现在默认就是 **X-ASR 流式 + X-ASR 整句精修** 的两遍识别（设置 → 识别 → 本地模型）。旧的 2023 双语模型仍可选，方便已下载的用户。

## 实测（本仓库测试音频，sherpa-onnx 1.13.8）

| 音频 | 旧版 2023 双语 Zipformer | X-ASR 流式 480 ms | X-ASR 离线（精修） |
| --- | --- | --- | --- |
| 中英混说 `mixed.wav` | 这个 SYSTEM DESIGN需要考虑 CASH INVLIDATION和 DAY TO DAYS CONSISTENCY请解释一下 TREAT OF | 这个 system design 需要考虑 cache invalidation 和 database consistency，请解释一下 trade off | 同左，并带句号 |
| 中文 `zh.wav` | …请解释数据库**所引**如何提升查询速度 | …请解释数据库**索引**如何提升查询速度 | 同左 |
| sherpa 中英测试句 | 昨天是 MONDAY TODAY IS LIVE AFTER TOMORROW是星期三 | 昨天是 Monday，today is 礼拜二，the day after tomorrow 是星期三 | 同左 |
| 课堂录音 | 第一句是个什么时态加了 ES是一般现在时… | 第一句是个什么时态？加了 yes 是一般现代时，… | 第一句是个什么时态？加了 E S 是… |

要点：

- 旧模型的英文全部大写、经常拼错（CASH INVLIDATION），而且**没有标点**，问题识别只能靠词汇规则；X-ASR 会输出 “？”，问句结尾直接可判。
- 流式模型在句尾需要补一点静音才能吐出最后几个字（块编码器的右侧上下文）。应用在每句定稿时会补 0.4 秒静音，并用离线模型重识别整句，句尾的问题不再被截断。
- 速度（x86 CPU，2 线程）：流式 RTF ≈ 0.10–0.13，离线 RTF ≈ 0.03–0.05；Apple Silicon 更快。

## 候选模型一览

| 模型 | 流式 | 体积 | 准确率（公开数据） | sherpa-onnx | 许可 |
| --- | --- | --- | --- | --- | --- |
| **X-ASR zh-en**（SJTU/SII，2026-06） | 原生，160/480/960/1920 ms | 0.16B，int8 约 134 MB | 流式 480 ms：LibriSpeech 2.99/7.36，WenetSpeech net/meeting 7.46/9.11；离线：2.56/5.56，5.83/7.06 | ✅ 标准 transducer | Apache-2.0 |
| Qwen3-ASR 0.6B / 1.7B（2026-01） | 仅 vLLM 后端分块重解码 | int8 约 940 MB | 0.6B：WenetSpeech 5.97/6.88，AISHELL-2 3.15 | ✅ 1.12.34+（0.6B） | Apache-2.0 |
| FireRedASR2（2026-02） | 否 | CTC 740 MB / AED 1.18 GB | AED：AISHELL-1 0.57，WenetSpeech 4.57/4.53 | ✅ CTC 1.12.27+ | Apache-2.0 |
| Fun-ASR-Nano-2512 | 否 | 约 950 MB | WenetSpeech 6.01/6.60，AISHELL-1 1.80 | ✅ 1.12.20+ | Apache-2.0 |
| SenseVoice-Small | 否（VAD + 重解码可模拟） | 228 MB | WenetSpeech 5.73/6.47，LibriSpeech 3.16/7.21 | ✅ | 自定义开源 |
| Whisper large-v3 / turbo | 否（WhisperKit 可模拟流式） | 0.8–1.5 GB | WenetSpeech 11.89/18.39（中文会议差） | ✅ | MIT |
| Nemotron-3.5 ASR 流式 0.6B | 原生 | 约 600 MB | 中文无官方数据，第三方测 FLEURS-zh 约 18% | ✅ 1.13.3+ | NVIDIA |
| Parakeet / Canary | — | — | 不支持中文 | — | — |
| Kyutai STT | 原生 | — | 只有英 / 法 | — | — |
| Apple SpeechAnalyzer（macOS 26） | 原生 | 系统 | 未公开；一次只能一种语言，不擅长中英混说 | — | — |

为什么之前 Whisper 不准：Whisper 在中文会议语料上的 CER 是 X-ASR 离线的 2–3 倍，而且不是流式，长静音段还会“幻听”。

## 抢话 / 两路音频

应用本来就是**多路输入**：系统声音（会议对方，用 ScreenCaptureKit 只采所选 App 窗口的声音）和麦克风（我）各自独立识别，同时说话不会混在一起。真正的问题是**扬声器回声**：不戴耳机时，对方的声音从扬声器出来又被麦克风收进去，于是

- 转录里出现“我”在复述对方的话；
- 更糟的是应用以为“我正在说话”，暂停了自动回答。

这次的处理：

1. **回声检测（默认开）**：比对两路音频的响度包络（20 ms 帧、1.6 s 窗口、±0.3–0.5 s 延迟搜索）。麦克风如果跟着系统声音的节奏走就是回声；你自己说话时节奏不同，即使和对方同时说也能分开。合成测试中回声识别率 100%，独立说话误判 0–2%。
2. **文字兜底**：麦克风这一句和对方几秒内说的话大部分重合，也当作回声丢弃。
3. **等我说完再回答**：对方提问时你正在说话（且不是回声），回答会在你停下后 6 秒内自动给出。
4. **Apple 语音处理（可选，实验）**：在麦克风输入上开启系统的回声消除与降噪；输入、输出不是同一设备时可能不生效。

多位远端同时说话属于同一路系统声音。sherpa-onnx 只有离线说话人分离（pyannote + 声纹聚类），实时分离目前不值得它的开销；需要时可以在会后对记录做说话人分离。戴耳机仍然是最干净的方案。

## 来源

- sherpa-onnx（Swift API、CHANGELOG、X-ASR 导出流程）：https://github.com/k2-fsa/sherpa-onnx
- X-ASR：https://github.com/Gilgamesh-J/X-ASR
- Qwen3-ASR：https://github.com/QwenLM/Qwen3-ASR
- FireRedASR2S：https://github.com/FireRedTeam/FireRedASR2S
- Fun-ASR：https://github.com/FunAudioLLM/Fun-ASR
- icefall multi_zh-hans 结果：https://github.com/k2-fsa/icefall/blob/master/egs/multi_zh-hans/ASR/RESULTS.md
