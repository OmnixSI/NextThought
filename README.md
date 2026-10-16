<div align="center">

# InterviewHacker

**面试、学术会议、商务会议、网课的实时 AI 提词助手 · 原生 macOS**

听对方说话 → 实时转写 → 自动判断“这是不是在问我” → 逐字流式给出可以直接说出口的回答<br>
每个场景一套资料，会后自动复盘，经验和题库越用越准

![macOS 14+](https://img.shields.io/badge/macOS-14%2B-black)
![Apple Silicon](https://img.shields.io/badge/Apple%20Silicon-arm64-blue)
![本地识别](https://img.shields.io/badge/%E8%AF%AD%E9%9F%B3%E8%AF%86%E5%88%AB-%E6%9C%AC%E6%9C%BA%E7%A6%BB%E7%BA%BF-34c759)

**[⬇️ 下载最新版本](../../releases/latest)** · [界面导览](#界面导览) · [安装](#安装) · [快速上手（新手引导）](#快速上手) · [场景与模式](#场景与模式) · [复盘与自我迭代](#复盘与自我迭代) · [数据与备份](#数据与备份) · [隐私](#数据去向)

<br>

<picture><source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/assistant-dark.png"><img src="docs/screenshots/assistant-light.png" width="760" alt="InterviewHacker 主界面：实时转录、问题识别与流式回答"></picture>

</div>

---

## 最近更新

- **联网搜索（v1.5）**：先立刻给出快速回答；问到价格、新闻、版本、现任人员、“今天 / 最近”这类需要最新信息的问题时，在后台联网（Codex 实时搜索或 Tavily），查好后自动换成联网版，可随时切回原回答。
- **模块可拆可合（v1.5）**：助手页的 实时对话 / 当前问题 / 建议回答 / 回答记录 可以拖动位置、调整大小、隐藏，或拖出窗口变成独立小窗，拖回来就合并。
- **决策模型（下一版）**：可选下载 [Laya](https://github.com/NandhaKishorM/laya)（多语言 322M，约 0.7 GB，Apache-2.0）在本机运行，或换成 [StartLux-Decision](https://decision.startlux.com/)，接管原来靠正则规则做的判断：这句是不是在问我、问题是否说完、要不要联网、麦克风这句是不是回声、题库里是不是重复问题。Laya 在 Apple Silicon 上约 10 毫秒一次，超时自动按规则处理。见 [docs/DECISION-MODEL.md](docs/DECISION-MODEL.md)。
- **屏幕感知与讲稿跟随（下一版）**：讲 PPT 时自动认出当前页，给出这一页的要点、过渡句和可能被问的问题，讲过的要点自动打勾；屏幕上出现题目时判断是否相关并一键回答。还会提示回答讲到哪一句、按问题类型调整回答、记下待办与重点、对方收尾时提醒复盘。
- **API 预设 + 技能（v1.5）**：保存多家服务商（地址、Key、模型、思考强度）一键切换；导入自己的 SKILL.md 全局或按场景使用。
- **回答更像人话（v1.5）**：按“照着说出口”来写，第一人称、短句，不再是提纲和 AI 套话；你说的话被误判为回声时会灰色显示，点“是我说的”恢复。
- v1.4：新手引导、设置左右布局、Codex Fast 模式、切换页面不闪烁。v1.3：场景分区与五种模式、会后复盘与自我迭代、数据备份。

完整说明见 [更新日志](CHANGELOG.md)。

## 界面导览

顶栏四个页面：**助手 · 场景 · 记录 · 设置**。开始 / 暂停 / 停止、计时和双路音量表常驻在右上角，在任何页面都能控制。

### ① 助手：模块随你摆（见顶部截图）

助手页由四个模块组成：**实时对话、当前问题与候选、建议回答、回答记录**。鼠标移到模块顶部会出现把手：拖动可以换位置（也能换到另一栏），**拖出窗口就变成独立小窗**，把小窗拖回主窗口松开就合并回来；模块之间、两栏之间的空隙可以拖动调整大小。输入框旁的 **布局** 按钮可以显示 / 隐藏模块、一键弹出，或换成“回答在左”“单栏上下排列”“只看回答”等预设。布局会被记住。


- **实时对话**：对方和我分两侧显示，被识别为问题的句子标上“问题”；虚线气泡是对方正在说、还没说完的话。悬停任意一句可以“回答这句”。和对方刚说的话几乎一样的麦克风文字会当作扬声器回声灰色显示、不参与回答，点“是我说的”即可恢复。
- **顶部一行**：当前场景（如“字节后端二面”）和音频来源都可以直接切换；下面一行是识别状态，包括识别引擎、实时率 RTF、整句精修耗时，以及已忽略的回声段数。
- **右侧问题条**：显示识别到的问题类型（明确提问 / 请求 / 追问）和置信度。对方补充了后半句时，会自动改答完整的问题。
- **建议回答**：逐字流式输出，写成可以直接说出口的口语。需要最新信息时上方出现“正在联网…”，查好后自动换成联网版（带 [1] [2] 来源），可切回原回答；也可以点 **联网查一下**。下方一键调整（更简短 / 展开 / 举例 / STAR / 英文版 / 中文版 / 澄清…）。
- **回答记录**：本场所有回答按时间排列，点一下就在建议回答里回看。
- **底部输入框**：可以手动输入问题，或截图提问；**Fast** 按钮切换 Codex Fast 模式；**立即回答**（⌘⇧Return）会立刻回答最新的问题。

### ② 提词器：放在摄像头下方，看着镜头就能读

<picture><source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/teleprompter-dark.png"><img src="docs/screenshots/teleprompter-light.png" width="640" alt="提词器"></picture>

- 半透明悬浮窗，显示聆听状态、首字耗时、当前问题和回答。可以翻页、调字号，开启鼠标穿透后不挡操作。
- 常用调整和“回答 / 取消 / 复制”都在窗上；⌘⇧\ 一键隐藏全部窗口。

### ③ 场景：每场面试、每次报告各用一套

<picture><source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/materials-dark.png"><img src="docs/screenshots/materials-light.png" width="720" alt="场景页：资料与要求"></picture>

- **左栏**：按模式分组列出所有场景（面试 / 学术会议 / 商务会议…），每个场景显示资料数、题库题数和会话数。点 ＋ 新建，右键可复制或删除。
- **资料与要求**：填写当前模式的专属信息（面试是应聘公司、岗位、面试官 / HR、我的亮点、期望与底线），按分类上传资料（简历、JD、八股、面经…），设置回答要求。
- 右上角可以切换模式、选择回答模板，或用“预测问题”在会前生成可能被问到的问题。

### ④ 经验库与题库：越用越懂你

<picture><source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/workspace-knowledge-dark.png"><img src="docs/screenshots/workspace-knowledge-light.png" width="720" alt="经验库与题库"></picture>

- **经验库**：每次会后复盘提炼出的“下次怎么做更好”。可以置顶（优先交给模型）、删除，也可以手动添加。
- **题库**：对方问过的问题和改进后的回答，记录被问次数，支持搜索。下次遇到同类问题时，优先采用这里准备好的答案。
- 顶部开关控制回答时是否参考这里的内容。

### ⑤ 复盘：看清进步和薄弱点

<picture><source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/workspace-review-dark.png"><img src="docs/screenshots/workspace-review-light.png" width="720" alt="场景复盘"></picture>

- 顶部统计这个场景累计的会话、问题、经验和题库数量。
- **阶段复盘**：把多次会话放在一起分析，给出总体进展、高频问题（附次数）和下一步准备清单。
- 下方列出这个场景的所有会话，点“查看”跳到记录页。

### ⑥ 记录：每次会话的完整档案

<picture><source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/history-dark.png"><img src="docs/screenshots/history-light.png" width="720" alt="会话记录"></picture>

- 每次会话自动保存，可以按场景筛选。
- 每条记录有四个视图：**摘要**（标题、摘要、完整问题清单含我的回答要点、关键信息、待办、复盘建议）、**问题**、**转录**（可搜索）、**提词**。
- 可以重命名、复制或导出 Markdown、在 Finder 中显示；“已加入场景经验库”表示这次的复盘已经进了经验库和题库。

### ⑦ 设置：基础项放最前，细节收进“高级选项”

- 七个分组：**基础 · 识别 · 回答 · 技能 · 提词器 · 数据 · 更多**。**基础** 页是“使用前检查”：权限、识别模型、回答引擎、当前场景和本机签名逐项显示状态，缺什么点一下就能补上；另外只有音频来源和外观。
- 识别、回答等页面只露出主要选项，不常用的（整句精修、回声处理、问题识别细节、Codex 运行方式、GitHub 令牌…）收在 **高级选项** 里，展开状态会被记住。
- 窗口每次打开都恢复到截图里的最佳尺寸（1120×820，屏幕小时自动缩小并居中）；菜单 窗口 → 恢复最佳窗口大小（⌥⌘0）。

**设置 → 回答：引擎、API 预设、联网搜索**

<picture><source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/settings-answers-dark.png"><img src="docs/screenshots/settings-answers-light.png" width="720" alt="设置：回答"></picture>

- **API 预设**：保存多家服务商（OpenAI、DeepSeek、通义、Kimi、智谱、豆包、OpenRouter、本机 Ollama…），每个预设有自己的地址、Key（钥匙串）、模型和思考强度（`reasoning_effort`），“获取模型列表”直接从服务商拉取可选模型。
- **联网搜索**：关闭 / 自动判断 / 每次都查；搜索方式选 Codex 实时搜索（ChatGPT 账号）或 Tavily（填 Key，结果交给当前回答引擎整理并标注来源）。
- **设置 → 判断**：下载并运行决策模型（默认 Laya 多语言版，也可选 StartLux 2B–27B），选择交给它的判断，用内置语料对比规则和模型的准确率与耗时。

**设置 → 技能：加载自己的 SKILL.md**

<picture><source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/settings-skills-dark.png"><img src="docs/screenshots/settings-skills-light.png" width="720" alt="设置：技能"></picture>

- 导入一个或多个 Markdown 文件（支持 SKILL.md 的 `name` / `description` 头），或从模板新建。勾选“所有场景”的技能每次回答都用；其他技能在 **场景 → 资料与要求 → 技能** 里按场景勾选，比如面试场景用“系统设计答题框架”，学术会议用“答辩应对”。
- 技能会随备份和场景导出一起保存。

**设置 → 数据：换模型、换接口、更新都不丢**

<picture><source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/settings-data-dark.png"><img src="docs/screenshots/settings-data-light.png" width="720" alt="设置：数据"></picture>

- **会话记录与复盘**：可以分别开关记录、会后摘要和自我迭代。
- **导出与导入**：一个 `.ihbackup` 文件包含全部场景、记录和设置，可选是否包含 API Key；导入只合并、不删除现有数据。
- **自动备份**：每天一次，更新前和导入前也会备份；可以从任意一份备份合并恢复。

<sub>截图由 GitHub Actions 在 macOS 上用演示数据自动生成（`INTERVIEWHACKER_DEMO=1`，见 `.github/workflows/screenshots.yml`），会随你的系统切换深色 / 浅色。</sub>

## 功能

| | |
| --- | --- |
| 🎧 **双路收音** | 分别采集会议窗口的系统声音（对方）和麦克风（我），两路独立识别，抢话也不混；自动识别并忽略扬声器回声，我在说话时等我说完再出回答 |
| ⚡ **两遍本地识别** | 默认离线 X-ASR 中英流式模型边说边出字（带标点、中英混说），每句说完再用离线模型整句精修；也可选 Apple Speech（macOS 26）或云端流式（Soniox / OpenAI / Deepgram）。调研见 [ASR 模型调研](docs/ASR-MODELS-2026-10.md) |
| 🧠 **问题识别** | 区分陈述、未说完、提问、请求（“说一下你对…的理解”“walk me through…”）、追问；去掉口头禅、排除自问自答和“能听到吗”；问句结尾约 0.6 秒内开始回答，对方补充后半句时自动改答完整问题 |
| 🌐 **联网搜索** | 先给快速回答，需要最新信息时后台联网（Codex 实时搜索 / Tavily），查好后自动换成带来源的联网版 |
| 🧩 **模块可拆可合** | 助手页模块可拖动、调整大小、隐藏，拖出窗口变成独立小窗，拖回来合并；多套预设布局 |
| 🧠 **技能** | 导入自己的 SKILL.md，全局或按场景启用，让回答更贴合你的说话方式和答题套路 |
| ✍️ **流式回答** | 常驻 Codex 服务或任意 OpenAI 兼容 API（DeepSeek / 通义 / Kimi / Groq / Ollama…）逐字输出，显示首字耗时；Codex 支持 **Fast 模式**（约 1.5 倍速度，与 Codex 客户端相同），输入框旁一键切换 |
| 🗂 **场景分区** | 五种模式，每个场景单独保存结构化信息、分类资料、回答要求、经验库和题库，一键切换 |
| 📄 **资料检索** | PDF 含扫描件 OCR、Word、PPT、Excel、图片在本机解析，按问题检索相关片段并标注 [分类] 文件名与页码 |
| 📝 **会话记录与摘要** | 自动保存完整转录、识别到的问题、提词和当时的资料；结束后生成标题、摘要、**完整问题清单（含我的回答要点）**、待办与复盘，可导出 Markdown |
| 🔁 **复盘与自我迭代** | 按模式复盘，经验和题库自动并入场景，下次回答参考；阶段复盘找出高频问题和反复出现的薄弱点 |
| 💾 **数据与备份** | 所有数据在本机一个文件夹；自动备份；一键导出 / 导入（.ihbackup），也可单独导出某个场景 |
| 🔄 **自动更新** | 定期检查 GitHub Releases，后台下载并校验，点“重启以更新”即可；可绑定本机签名，更新后不再重新授权 |
| 🪄 **一键调整** | 更简短、展开、举例、STAR、英文版、澄清问题、我可以反问；会前“预测可能的问题” |
| 🪟 **提词器** | 半透明悬浮窗，可放在摄像头下方，鼠标穿透，历史翻页，一键隐藏全部窗口 |
| 📱 **手机伴侣** | 扫码即用，手机实时显示回答并可遥控，屏幕共享完全看不到 |
| 📸 **截图提问** | 窗口或区域截图，适合代码题、数学题、幻灯片 |
| ⌨️ **全局快捷键** | 全部可自定义，见[下表](#快捷键) |

## 场景与模式

“场景”页按模式分组管理所有场景。每个场景是一套独立的上下文：例如“字节后端二面”和“阿里一面”各用各的简历版本、JD 和面经，“ACL 口头报告”放论文和讲稿。开始辅助前在助手页顶部切换即可，回答只使用当前场景的内容。

| 模式 | 可以填写的信息 | 资料分类 | 复盘重点 |
| --- | --- | --- | --- |
| **面试** | 应聘公司、岗位、面试官 / HR、我的亮点、期望与底线 | 简历 / 岗位 JD / 公司信息 / HR / 面试官 / 八股 / 面经 / 项目资料 | 逐题评价回答、更好的说法、被追问的薄弱点、下一轮要准备的内容 |
| **学术会议** | 报告题目、会议与场次、核心论点、已知局限、预计会被问的问题 | 论文 / PPT / 幻灯片 / 讲稿 / 补充材料 / 附录 / 相关工作 / 审稿意见 | 听众和主持人的每个问题与回答质量、讲得不清楚的部分、论文或 PPT 要补的内容 |
| **商务会议** | 对方公司 / 参会人、会议目标、我方立场与底线、关键数字 | 公司 / 产品资料 / 对方信息 / 议程 / 报价 / 合同 / 会议纪要 | 决定事项、各方承诺与截止时间、未解决的分歧、谈判表现 |
| **课程** | 课程、学习目标 | 课件 / 笔记 / 作业 / 题目 / 教材 | 知识点、老师强调的重点、布置的任务、还没弄懂的地方 |
| **通用** | 场景说明 | 资料 | 讨论要点、对方关心的问题、可以做得更好的地方 |

<sub>切换模式时，未改动过的回答要求会自动换成新模式的模板。</sub>

## 复盘与自我迭代

```mermaid
flowchart LR
    A["开会 / 面试"] --> B["自动记录<br/>转录 · 问题 · 提词 · 当时的资料"]
    B --> C["会后复盘<br/>（按模式）"]
    C --> D["经验沉淀"] --> E["场景经验库"]
    C --> F["题库更新<br/>改进版回答"] --> G["场景题库"]
    E --> H["下次回答自动参考"]
    G --> H
    B -. 多次会话 .-> I["阶段复盘<br/>高频问题 · 反复的薄弱点 · 下一步准备"]
```

- 复盘结果默认自动并入会话所属场景（设置 → 数据 可关闭）；也可以在记录页手动“加入场景经验库”。
- 经验和题目会去重合并，相近的问题（“怎么”和“如何”这类说法差异）只保留一条并更新答案。
- 场景页的“经验库与题库”可以手动添加、编辑、删除；置顶的经验会优先交给模型。

## 数据与备份

- 所有数据都在 `~/Library/Application Support/InterviewHacker`：`Workspaces/`（场景）、`Sessions/`（会话记录）、`Backups/`（自动备份）、`models/`（识别模型）。更新应用、更换识别模型或回答接口都不会改动它们。
- **自动备份**：每天一次，安装更新前、导入前各一次，保留最近 10 份；删除的场景也会在 `Backups/deleted-workspaces` 留一份。
- **导出 / 导入**：设置 → 数据 →“导出全部”生成一个 `.ihbackup` 文件（可选是否包含 API Key）；换电脑或重装后“导入”即可。导入只合并、不删除，同一场景以较新的修改为准。
- 旧版本的回答要求、背景说明、预设和会话记录在首次打开 v1.3 时自动迁移。

## 安装

1. 到 **[Releases](../../releases)** 下载 `InterviewHacker-<版本>-macOS-arm64.dmg`，打开后把 InterviewHacker 拖进“应用程序”。（`main-latest` 是 main 分支的最新自动构建，正式版本见带版本号的 Release。）
2. 第一次打开：应用未经 Apple 公证，macOS 会拦截。打开 **系统设置 → 隐私与安全性**，在页面底部点 **“仍要打开”**。
   也可以在终端执行：`xattr -dr com.apple.quarantine /Applications/InterviewHacker.app`
3. 按提示允许 **屏幕与系统音频录制** 和 **麦克风**。授权后如系统要求重启应用，照做即可。

> 需要 Apple Silicon（M 系列芯片）和 macOS 14 或更新版本。Apple Speech 流式识别需要 macOS 26。

### 更新（不用每次重新授权）

**应用内自动更新（推荐）**：设置 → 更多 → 自动更新。应用启动后和每 6 小时检查一次 GitHub Releases，发现新版本就在后台下载并校验 SHA-256 与签名，顶栏出现“重启以更新”，点一下即替换并重新打开。仓库是私有的，需要在同一页的“高级选项 → GitHub 访问”填一个只读访问令牌（fine-grained token，只勾选本仓库的 Contents: Read-only），或在终端 `gh auth login` 过。

**绑定本机签名（一次即可）**：设置 → 基础 → 使用前检查 → 本机签名 “绑定”（或 设置 → 更多 → 权限与签名）。应用会在本机生成一个只属于这台 Mac 的代码签名证书并重新签名自己；之后自动更新下载的每个新版本都会用同一证书重新签名，macOS 认为它是同一个应用，录屏和麦克风权限一直有效。绑定后需要再授权一次，系统可能请求一次登录密码以信任该证书。

也可以用命令行更新（需要已登录的 [GitHub CLI](https://cli.github.com)）：

```sh
./scripts/install-latest.sh
```

它通过 `gh` 下载最新版、校验 SHA-256、替换 `/Applications/InterviewHacker.app` 并重新打开。这样下载的文件不带浏览器的“来自互联网”标记，**不会再弹“仍要打开”**。

**为什么浏览器下载的版本每次都要重新授权？** macOS 的录屏 / 麦克风权限绑定在应用的**签名身份**上。没有固定证书时，CI 只能临时（ad-hoc）签名，每个版本的签名都不同，系统就把它当成新应用。在自己的 Mac 上运行一次：

```sh
./scripts/setup-release-signing.sh
```

它会把本机的固定签名身份（与 `scripts/build-app.sh` 本地构建共用）保存到仓库的 Actions Secrets，之后发布的每个版本都用同一身份签名，**升级后权限保持不变**。如需完全免除 Gatekeeper 提示，可加入 Apple Developer Program 并配置 Developer ID 与公证（见 [开发文档](docs/DEVELOPMENT.md#签名与公证)）。

## 快速上手

第一次打开会出现 **新手引导**，按顺序完成 4 步就能用：

<picture><source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/onboarding-welcome-dark.png"><img src="docs/screenshots/onboarding-welcome-light.png" width="640" alt="新手引导"></picture>

1. **系统权限**：允许“屏幕与系统音频录制”（采集会议里对方的声音）和麦克风（识别你自己的话）。建议顺手“绑定本机签名”，以后更新不用重新授权。
2. **语音识别**：推荐本机识别，点“下载”（约 270 MB，只需一次，来自 sherpa-onnx 官方发布并校验 SHA-256）。也可选 Apple Speech（macOS 26）或云端识别。
3. **回答引擎**：有 ChatGPT 账号选 **Codex**（选模型、推理强度建议 `low`，想更快就打开 **Fast 模式**）；否则选 **API Key**，点“常用”选服务商并填模型和 Key。点 **测试回答** 确认能用。
4. **第一个场景**：选模式（面试 / 学术会议 / 商务会议…）起个名字，之后在“场景”页放入简历、JD、论文等资料。

然后在 **助手** 页选择会议窗口作为音频来源，点 **开始**：对方提问会自动出回答，随时按 **⌘⇧Return** 立即回答；按 **⌘⇧O** 打开提词器。结束后点 **停止**，记录页会出现摘要和问题清单，复盘自动进入场景的经验库与题库。

引导可以随时跳过，之后在 **设置 → 基础** 查看“使用前检查”或重新打开引导。

<picture><source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/onboarding-engine-dark.png"><img src="docs/screenshots/onboarding-engine-light.png" width="640" alt="回答引擎设置与 Fast 模式"></picture>

### 快捷键

| 快捷键 | 动作 |
| --- | --- |
| ⌘⇧Return | 立即回答（输入框 → 最新问题 → 最近转写） |
| ⌘⇧O | 显示 / 隐藏提词器 |
| ⌘⇧\ | 一键隐藏全部窗口（再按恢复） |
| ⌘⇧K | 提词器鼠标穿透 |
| ⌘⇧1 | 把回答改短 |
| ⌃⌘← / ⌃⌘→ | 上一条 / 下一条回答 |
| ⌘⇧Y | 复制回答 |
| ⌘⇧M / ⌘⇧P | 开始停止 / 暂停恢复 |
| ⌘⇧Esc / ⌘⇧R | 取消 / 重新生成 |
| ⌘⇧A / ⌘⇧S | 窗口截图 / 区域截图 |

### 屏幕共享时怎么看回答

- **共享单个窗口或浏览器标签页**：对方看不到提词器，最推荐。
- **共享整个屏幕**：设置里的“尽力从屏幕共享中排除”对很多软件有效，但 macOS 没有能保证排除录屏的公开接口，macOS 15 起部分整屏共享仍可能拍到。用 **设置 → 提词器 → 共享可见性测试** 自己确认；最稳妥是开 **手机伴侣** 用手机看。

## 数据去向

| 数据 | 去向 |
| --- | --- |
| 原始音频 | 默认只在本机识别，不保存。选择云端识别并勾选同意后，实时上传到你选的服务商 |
| 转录、问题、检索到的资料片段、截图、启用的技能 | 发送给你选的回答引擎（Codex 或你配置的 API） |
| 联网搜索 | 开启后，需要最新信息的问题会交给 Codex 联网搜索，或发送到 Tavily 搜索（只发问题文本） |
| 判断模型（可选） | 拿不准的一句话 / 问题和前几句转录发送到你填写的判断模型接口 |
| 导入的资料 | 在本机解析，随所属场景保存在本机 |
| API Key（每个预设、Tavily、判断模型） | 系统钥匙串 |
| 手机伴侣 | 默认关闭；只同步当前问题和回答，链接含一次性随机配对码，局域网明文 HTTP |
| 会话记录、场景、备份 | 全部在本机 `~/Library/Application Support/InterviewHacker`（不含音频）；可在 设置 → 数据 关闭记录、导出 / 导入或删除 |
| 会后摘要 | 默认开启：会话结束后把这次的转录发给你配置的回答引擎生成摘要与问题清单；可关闭 |
| 更新检查 | 访问 api.github.com 读取本仓库的 Releases；访问令牌保存在钥匙串 |

请遵守会议、课程、考试和面试方的规则，并在录音和转写前取得参与者同意。本软件不宣称“无法被检测”。

## 常见问题

<details>
<summary><b>每次更新都要重新允许录屏 / 麦克风？</b></summary>

macOS 把权限绑定在应用的签名身份上。最简单：在 设置 → 基础 的“本机签名”一行点“绑定”，再用应用内更新，以后都不用重新授权。维护者也可以运行一次 `./scripts/setup-release-signing.sh`，让发布的版本都用固定签名。详见上方[更新](#更新不用每次重新授权)。
</details>

<details>
<summary><b>点“开始”要等很久？</b></summary>

本机模型首次下载约 270 MB，随后会从 Application Support 加载并在后台预热；识别耗时、RTF 和整句精修耗时显示在对话区顶部。设置 → 识别 → 高级选项 里可关闭后台预加载以减少空闲时的内存占用。
</details>

<details>
<summary><b>摘戴耳机、切换 AirPods 后会断吗？</b></summary>

不会。麦克风会自动跟随系统默认输入设备，系统音频中断后自动重连；麦克风暂时不可用时，仍会继续识别对方的声音。
</details>

<details>
<summary><b>手机伴侣提示 “Address already in use”？</b></summary>

新版本会在首选端口被占用时自动换用下一个空闲端口。也可以在 设置 → 更多 → 手机伴侣 修改首选端口并点“重新生成链接”。手机和 Mac 需要连同一个 Wi‑Fi。
</details>

<details>
<summary><b>共享屏幕时对方会看到提词器吗？</b></summary>

只共享会议窗口或浏览器标签页时看不到。共享整个屏幕时，macOS 没有能保证排除的公开接口，部分软件仍可能拍到——用 设置 → 提词器 → 共享可见性测试 自己确认，或改用手机伴侣。
</details>

<details>
<summary><b>没有 Codex 账号能用吗？</b></summary>

可以。设置 → 回答 选“API Key”，填 DeepSeek、通义千问、Kimi、Groq、OpenRouter 的 Key，或本机 Ollama / LM Studio 地址。
</details>

## 文档

- [开发与发布](docs/DEVELOPMENT.md)：项目结构、构建、打包、签名与公证、发布流程、`--self-test` 自检
- [用自己的 Mac 当构建机](docs/SELF-HOSTED-RUNNER.md)：`./scripts/setup-ci-runner.sh` 一键注册，构建 / 测试 / 发布不再消耗 GitHub 的 macOS 分钟数
- [决策模型（Laya / StartLux-Decision）](docs/DECISION-MODEL.md)：各模型占用、推荐、接管了哪些判断、许可
- [v1.3.0 发布说明](docs/releases/v1.3.0.md)
- [本地语音识别模型调研（2026-10）](docs/ASR-MODELS-2026-10.md)：X-ASR / Qwen3-ASR / FireRedASR2 / SenseVoice / Whisper 对比与实测，回声与抢话处理
- [第二轮改进说明](docs/IMPROVEMENTS-2026-10-14-round2.md)：流式回答、问题识别、云端识别、手机伴侣等设计与验收清单
- [第一轮代码审计](docs/AUDIT-2026-10-14.md) · [历史验证记录](docs/VALIDATION-2026-10-14.md) · [更新日志](CHANGELOG.md)
- 问题反馈与功能建议：[Issues](../../issues/new/choose)

## 从源码构建

需要 Apple Silicon 和 Xcode（Swift 6）：

```sh
swift test --disable-sandbox     # 运行测试
./scripts/build-app.sh           # 生成 dist/InterviewHacker.app 与 ZIP
open dist/InterviewHacker.app
```

详见 [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)。

## 致谢

本地语音识别使用 [Sherpa-ONNX](https://github.com/k2-fsa/sherpa-onnx) C API 和 Apache-2.0 的 [X-ASR](https://github.com/Gilgamesh-J/X-ASR) 中英 Zipformer 模型（流式 + 离线）；ONNX Runtime 与模型的版本、来源和许可证见 [`Resources/ThirdPartyNotices.txt`](Resources/ThirdPartyNotices.txt)。
