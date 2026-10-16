# 决策模型（StartLux-Decision）

InterviewHacker 里很多判断原来靠正则和关键词规则：这句是不是在问我、问题说完没有、要不要联网、麦克风这句是不是扬声器回声、两道题是不是同一个问题。[StartLux-Decision](https://decision.startlux.com/)（[代码](https://github.com/StartLuxLabs/Startlux-Decision)）是专门做这类“选择题 / 是非题 / 打分题”的模型：一次前向计算读出每个选项的概率，不生成长文本，所以适合实时判断。

在 **设置 → 判断** 里可以下载并在这台 Mac 上运行，或连接已有的服务。默认关闭；关闭、没启动、超时或出错时，一切照旧按规则判断。

## 选哪个大小

只用 **Instant**（一次读出）版本。AutoThink 版会先“思考”几秒，不适合边听边判断。

| 大小 | 8-bit 磁盘 | 4-bit 磁盘 | 运行时内存（8-bit） | 准确度（官方 Decision Index） | 适合 |
| --- | --- | --- | --- | --- | --- |
| 0.8B | ≈ 0.9 GB | ≈ 0.6 GB | ≈ 2.5 GB | 53.8 | 只想试试；准确率明显低 |
| 2B | ≈ 2.2 GB | ≈ 1.4 GB | ≈ 3.5 GB | 59.1 | 8 GB 内存的 Mac |
| **4B** | **≈ 4.7 GB** | ≈ 2.9 GB | **≈ 6 GB** | **67.9** | **推荐：16–24 GB 内存** |
| 9B | ≈ 10 GB | ≈ 6 GB | ≈ 11.5 GB | 70.0 | 32 GB 以上内存 |
| 27B | ≈ 29 GB | ≈ 17 GB | ≈ 30 GB | 72.8 | 不推荐实时使用：太大、太慢 |

- 磁盘大小是按参数量估算的（v1 官方 GGUF：Q8 约 0.81 / 2.0 / 4.5 / 9.5 / 28.6 GB），以下载时显示的实际大小为准。另需约 1 GB 的 Python 运行环境。
- 4B 比 2B 准约 9 个点，只比 9B 低 2 个点，体积不到一半，和语音识别模型、回答引擎同时运行也宽裕，所以是默认推荐。应用会按这台 Mac 的内存自动选：< 12 GB → 2B，≥ 32 GB → 9B，其余 4B。
- 准确度来自官方英文基准。中文对话的效果请在设置里点 **评测**，用内置的中英文标注语料对比规则和模型。

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

“下载并启动”会：

1. 在 `~/Library/Application Support/InterviewHacker/decision/venv` 建 Python 环境（优先用 `uv`，否则用 Python 3.10 以上），安装 `startlux-decide[mlx]` 和 `huggingface_hub`；
2. 从 Hugging Face 下载 `startlux-models/StartLux-Decision-v1.1-<大小>-Instant-MLX-<8|4>bit`；
3. 启动 `startlux-decide serve --backend mlx --host 127.0.0.1 --port 8790 --think-mode never`，日志在同一目录的 `decision.log`。

之后每次打开应用会在后台自动启动。也可以在别的机器上运行 `startlux-decide serve`，在设置里选“连接已有的服务”并填地址。

接口：`GET /health`，`POST /v1/systemone`，请求体为 `state`（情境文字）和 `questions`（`choice` / `noul` / `score`），应用固定发送 `think_mode: "never"`。请求与解析在 `Sources/InterviewHackerCore/DecisionProtocol.swift`，可在 Linux 上单测。

## 许可与隐私

- 模型权重为 **CC BY-NC 4.0**，仅限非商业用途。
- 在本机运行时，对话文字只发给 127.0.0.1 上的服务。连接其他服务时，文字会发到那台机器；官方在线演示服务不在应用里使用。
