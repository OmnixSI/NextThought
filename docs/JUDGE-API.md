# 判断模型（JEV）接口约定

InterviewHacker 里有两类“快速判断”，平时由本地规则完成；规则拿不准时，可以交给一个外部判断模型（设置 → 回答 → 高级选项 → 判断模型（JEV））。不接判断模型时一切照常，只是边界情况由规则决定。

| 任务 | `task` | 什么时候会问 |
| --- | --- | --- |
| 这句话是不是在问我 | `question_detection` | 本地问题识别给出“可能是问题”（不确定）且开启了自动回答 |
| 这个问题要不要联网 | `web_search_need` | 联网模式为“自动判断”，本地规则得分在 0.35–0.6 之间 |

每次判断有 **1.5 秒** 的上限；超时、出错或回复无法解析时，按本地规则处理，不会拖慢回答。

## 传输

标准的 OpenAI 兼容 **Chat Completions（流式 SSE）**：

```
POST {接口地址}/chat/completions
Authorization: Bearer {Key}        // 可留空
{
  "model": "{模型 ID}",
  "stream": true,
  "temperature": 0.3,
  "messages": [
    {"role": "system", "content": "<固定说明，见下>"},
    {"role": "user",   "content": "<任务 JSON，见下>"}
  ]
}
```

同时也支持非流式实现：只要最终拼出的文本里包含一个 JSON 对象即可。

## 任务 JSON（user 消息）

```json
{
  "task": "web_search_need",
  "version": 1,
  "text": "现在比特币多少钱？",
  "context": "[对方 · 系统声音] 我们聊聊最近的行情……",
  "mode": "interview",
  "rule_score": 0.46,
  "today": "2026-10-16 星期五"
}
```

| 字段 | 说明 |
| --- | --- |
| `task` | `question_detection` 或 `web_search_need` |
| `version` | 协议版本，目前为 `1` |
| `text` | 待判断的一句话 / 问题（≤ 2000 字） |
| `context` | 之前几句转录（≤ 1200 字），`[对方 · 系统声音]` / `[我 · 麦克风]` 标注说话人；`web_search_need` 时可能为空 |
| `mode` | `interview` / `academic` / `meeting` / `lesson` / `general` |
| `rule_score` | 本地规则对同一问题的得分（0–1），仅供参考 |
| `today` | 今天的日期 |

## 回复

回复文本中包含 **一个 JSON 对象**（前后可以有其他文字或 ```json 代码块，取第一个 `{` 到最后一个 `}`）：

```json
{"answer": true, "confidence": 0.92, "query": "比特币 今日价格"}
```

| 字段 | 说明 |
| --- | --- |
| `answer` | 必填，`true` / `false`（也接受 `"yes"`/`"no"`、`1`/`0`）；兼容键名 `is_question`、`needs_web_search` |
| `confidence` | 可选，0–1，缺省按 0.7；`question_detection` 低于 0.5 时不自动回答 |
| `query` | 可选，`web_search_need` 为真时建议的搜索词（预留，当前版本仍用原问题搜索） |

只回复 `yes` / `no` 也能被识别（置信度按 0.6）。

## 固定的 system 说明

见 `Sources/InterviewHackerCore/JudgeProtocol.swift` 中的 `JudgeProtocol.system`，自己的模型可以忽略它、直接按任务 JSON 处理。

## 测试

设置页的“测试”按钮会发送一个 `web_search_need` 任务（“今天美元兑人民币汇率是多少？”），显示耗时和判断结果。
