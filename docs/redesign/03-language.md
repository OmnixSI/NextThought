# 中英文切换 · Language

## 这是整个 App 的语言

这里说的是**整个软件的系统语言**，不只是设计稿：首页、准备页、会中界面、提词器、纪要、设置、菜单栏、系统通知、错误提示、新手引导、手机伴侣的网页，全部跟着切换。目的是让海外同事和用户可以直接用英文版。

## 用户怎么切换

- 位置：**设置 → 通用 → 语言**，三个选项：**跟随系统 / 简体中文 / English**。默认跟随系统：Mac 的系统语言是英文，第一次打开就是英文。
- 切换后，界面里所有文字、日期和时间格式、纪要的小节标题都换成对应语言。
- 会议进行中不能切换。切换时 App 会提示“需要重新打开 App”，并自动重新打开（原因见下方“实现方案”）。

## 海外用户要额外照顾的地方

- **语音识别**：现在的本地模型是中英双语的（X-ASR zh-en），英文会议能用。其他语言（日语、西班牙语等）要靠云端识别或 Apple 语音识别，在“识别”设置里选。
- **会议 App**：除了腾讯会议、飞书，还要优先识别 Zoom、Google Meet、Microsoft Teams、Webex。
- **示例和默认值**：英文界面里的示例换成英文场景（例如 “Google · SWE onsite”），不要直接翻译中文的例子。
- **会议类型**：英文名见下方术语表；“组会”译作 Lab meeting。

## 哪些跟着变，哪些不变

| 内容 | 跟界面语言走吗 | 说明 |
|---|---|---|
| 界面文字、菜单、提示 | 是 | |
| 纪要（总结、决定、待办） | 默认是 | 纪要页“…”里可以单独改语言 |
| 建议回答 | **否** | 跟着对话的语言走：英文面试就用英文回答。“英文版 / 中文版”改写照样可用 |
| 语音识别语言 | 否 | 在“识别”设置里单独选：中文、英文、中英混合 |
| 用户自己的资料和场景名 | 否 | 原样保留 |

## 英文文案规范

- 句首大写（sentence case），不要每个词都大写：`Answer now`，不写 `Answer Now`。
- 动作用 1–3 个词的动词短语：`Start`、`Record now`、`Prepare`、`End`、`Copy notes`。
- 语气和中文一致：简短、平静，不用感叹号，不用 “Oops”。
- 状态写成“现在怎样 · 下一步”：`Answer engine not connected · Connect`。
- 英文更长，按钮和标签要留出约 30% 的宽度余量；不要用固定宽度截断。

## 术语表

| 中文 | English |
|---|---|
| 首页 | Home |
| 所有会议 | Meetings |
| 资料夹 | Library |
| 设置 | Settings |
| 新会议 | New meeting |
| 立即记录 | Record now |
| 准备这场会议 | Prepare |
| 开始 / 结束 | Start / End |
| 暂停 / 已暂停 | Pause / Paused |
| 提词 / 只记录 | Prompt / Notes only |
| 提词器 | Teleprompter |
| 建议回答 | Suggested answer |
| 立即回答 | Answer now |
| 更简短 · 举例 · 英文版 | Shorter · Example · In English |
| 重新生成 | Regenerate |
| 联网核查 / 联网版 | Checking the web / Web version |
| 看原回答 | Show original |
| 依据 | Based on |
| 快速模式 | Fast mode |
| 回答设置 | Answer settings |
| 回答长度：要点 / 详细 | Length: Brief / Detailed |
| 实时对话 | Live transcript |
| 对方 / 我 | Them / Me |
| 问题 | Question |
| 纪要 | Notes |
| 一句话总结 | In one line |
| 决定 / 待办 | Decisions / Action items |
| 复盘 | Review |
| 存进题库 | Save to question bank |
| AI 读到的重点 | What AI took away |
| 可能会问 | Likely questions |
| 已读懂 / 读取中 | Read / Reading |
| 开始前检查 | Before you start |
| 声音 / 回答 / 手机 | Audio / Answers / Phone |
| 已连接手机 | Phone connected |
| 通用 · 面试 · 汇报 / 答辩 · 组会 | General · Interview · Presentation / Defense · Lab meeting |
| 工作会议 · 客户沟通 · 讲座 / 课程 · 小组讨论 · 自定义 | Work meeting · Client call · Lecture / Class · Group discussion · Custom |
| 系列 | Series |
| 接着上一场 | Continue from last time |
| 从以前的会议带入… | Bring in from a past meeting… |

## 字体

- 英文界面同样使用 SF Pro。会议标题用衬线体 New York，字号和行高与中文相同。
- 英文正文行高可以略收（14/21），其余层级不变。

## 实现方案与工作量（对照现有代码）

- **现状**：界面文字直接写在代码里，约 2,600 处中文，分布在 95 个 Swift 文件中。另外 Core 模块里还有一些枚举标题（例如联网模式的“关闭 / 自动判断 / 每次都查”）。
- **做法**：
  1. 用 Xcode 的 **String Catalog**（`Localizable.xcstrings`）。
     - SwiftUI 里 `Text("开始")` 这类写法会自动被当作翻译键。
     - 存在变量里、拼接出来的字符串要改成 `String(localized:)`。
     - Core 模块的字符串用 `bundle: .module`。
  2. App 内切换语言：写入 `AppleLanguages`，然后自动重新打开 App。这是 macOS 上最稳的做法；要做到“立即生效、不重开”，需要自己实现一套查找逻辑，成本高，第一版不做。
  3. AI 的提示词：增加一句“用对话所用的语言回答”；纪要的提示词按界面语言选择模板。
  4. 识别提问的规则（`QuestionDetector`）已经覆盖中英两种语言，不受影响。
- **工作量**：主要是逐个文件替换字符串和翻译，量大但机械。分两步做：第一步里新写的界面（首页、准备页、会中、纪要）从一开始就中英文一起交付；第二步再把剩下的旧界面（设置等）迁过去，完成整个 App 的英文版。
