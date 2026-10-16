# 决策模型（Laya / StartLux-Decision）

InterviewHacker 里很多判断原来靠正则和关键词规则：这句是不是在问我、问题说完没有、要不要联网、麦克风这句是不是扬声器回声、两道题是不是同一个问题。决策模型专门回答这类“选择题 / 是非题 / 打分题”：一次计算读出每个选项的概率，不生成文字，所以适合边听边判断。

在 **设置 → 判断** 里可以下载并在这台 Mac 上运行，或连接已有的服务。默认关闭；关闭、没启动、超时或出错时，一切照旧按规则判断。

## 选哪个

| 模型 | 磁盘 | 运行内存 | 一次判断 | 许可 | 说明 |
| --- | --- | --- | --- | --- | --- |
| **Laya 多语言 322M（推荐）** | **≈ 0.7 GB** | **≈ 1 GB** | **≈ 7 ms**（M3 Max） | **Apache-2.0，可商用** | 双向编码器（mmBERT），100 多种语言 |
| StartLux 2B Instant 8-bit | ≈ 2.2 GB | ≈ 3.5 GB | 未公布 Mac 数据，估计几百毫秒 | CC BY-NC 4.0，非商业 | 基于大语言模型，常识更多 |
| StartLux 4B Instant 8-bit | ≈ 4.7 GB | ≈ 6 GB | 同上，更慢 | CC BY-NC 4.0，非商业 | |
| StartLux 9B Instant 8-bit | ≈ 10 GB | ≈ 11.5 GB | 同上，更慢 | CC BY-NC 4.0，非商业 | 32 GB 以上内存 |
| StartLux 0.8B / 27B | ≈ 0.9 / 29 GB | | | | 太弱 / 太大，不推荐 |

**默认推荐 Laya**，任何 Apple Silicon Mac 都适合：

- 实时判断每句话都要跑，0.9 秒内没结果就作废。Laya 在 M3 Max 上单个问题中位 7.4 ms（P95 7.8 ms），峰值显存 688 MiB；较慢的 M1 / M2 估计也在几十毫秒内（[laya-mlx 实测](https://github.com/mizorewww/laya-mlx)）。
- 体积不到 StartLux 4B 的六分之一，和语音识别模型、回答引擎一起跑没有压力。
- Apache-2.0，以后商用也没问题；StartLux 权重仅限非商业用途。
- 中文：社区的中文评测（[zh-decision-bench](https://github.com/CodyQin/zh-decision-bench)）里，多语言版在语音意图路由上准确率 0.88，客服分流、紧急程度等任务 0.55–0.67；中文是非题的置信度偏高（[Laya BENCHMARKS](https://github.com/NandhaKishorM/laya/blob/main/BENCHMARKS.md)）。

StartLux 的优势是常识：比如“这个问题要不要联网”需要知道哪些信息会过时，大语言模型更擅长。如果在 **评测** 里看到 Laya 在你的场景不够准，可以换成 StartLux 2B（8 GB 内存）或 4B（16 GB 以上）试试。StartLux 的官方 Decision Index（英文）：0.8B 53.8、2B 59.1、4B 67.9、9B 70.0、27B 72.8；只用 Instant 版，AutoThink 版会先“思考”几秒。

两者的中文效果都没有覆盖我们的具体任务，请以 **评测** 的结果为准：它用内置的中英文标注语料，对比规则和模型的准确率与耗时。

## 接管了哪些判断

| 判断 | 原来 | 现在 | 截止时间 |
| --- | --- | --- | --- |
| 这句是不是在问我 | `QuestionIntentClassifier` 打分 | 把握很大（≥ 0.85）仍立即回答；其余交给模型判断类型（提问 / 请求 / 追问 / 没说完 / 陈述 / 设备确认 / 自问自答）和“现在是否需要我回答” | 0.9 s |
| 是否在补充上一个问题 | 文字变长的启发式 | 模型判断“是否在补充或改说上一个问题”，是则改答完整问题 | 同上（同一次请求） |
| 规则漏掉的问题 | 无 | 规则认为不是问题的定稿句，模型很有把握（≥ 0.75）时补答 | 0.9 s |
| 要不要联网（自动模式） | `WebSearchNeed` 关键词 | 模型判断是否需要训练数据之后的新信息 | 1.2 s |
| 麦克风这句是不是我说的 | 文本重合 + 声学相关 | 疑似回声的句子再问模型，判定是我说的就恢复显示 | 1.5 s |
| 题库重复问题 | 词重合度 | 复盘后对相似度中等的题对（最多 24 对）问模型，同一问题就合并 | 4 s |

每种判断都可以在设置里单独关掉。

## 怎么运行

“下载并启动”会在 `~/Library/Application Support/InterviewHacker/decision` 里：

1. 建 Python 环境（优先用 `uv`，否则用 Python 3.11 以上）。Laya 装 [`laya-mlx`](https://pypi.org/project/laya-mlx/)（原生 MLX，不需要 PyTorch），放在 `laya-venv`；StartLux 装 `startlux-decide[mlx]`，放在 `venv`。
2. 从 Hugging Face 下载模型：Laya 为 `aac6fef/laya-multilingual-mlx`；StartLux 为 `startlux-models/StartLux-Decision-v1.1-<大小>-Instant-MLX-<8|4>bit`。
3. 在 127.0.0.1:8790 启动服务：Laya 用应用自带的小服务脚本 `laya_server.py`（只用 Python 标准库）；StartLux 用 `startlux-decide serve --backend mlx --think-mode never`。日志在 `decision.log`。

之后每次打开应用会在后台自动启动。也可以在别的机器上运行任何兼容的服务（`laya-serve`、`startlux-decide serve`），在设置里选“连接已有的服务”并填地址。

接口：`GET /health`，`POST /v1/systemone`，请求体为 `state`（情境文字）和 `questions`（`choice` / `noul` / `score`），Laya、StartLux 和 Jev 用的是同一种格式。请求与解析在 `Sources/InterviewHackerCore/DecisionProtocol.swift`，可在 Linux 上单测。Laya 多语言版最多读 1,024 个 token，所以用 Laya 时只带最近 3 句对话。

## 许可与隐私

- Laya：Apache-2.0（[Convai Innovations](https://github.com/NandhaKishorM/laya)；Mac 版 [laya-mlx](https://github.com/mizorewww/laya-mlx) 为社区移植，同样 Apache-2.0）。
- StartLux-Decision：权重 **CC BY-NC 4.0**，仅限非商业用途。
- 在本机运行时，对话文字只发给 127.0.0.1 上的服务。连接其他服务时，文字会发到那台机器。
