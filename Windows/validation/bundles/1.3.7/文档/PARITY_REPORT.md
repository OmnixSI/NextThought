# NextThought Windows 移植 · 进度报告（1.3.7）

这份报告给「现在能用到什么、还差什么」一个直接答案，细节见 `README.md`、
`src/NextThought/Windows/PORTING_STATUS.md` 与 `CORE_PARITY.md`。

## 一、能直接用的（安装包 dist\NextThought-1.3.7-windows-x64-setup.exe）

| 能力 | 状态 | 证据 |
| --- | --- | --- |
| 双路采音（系统声 + 麦克风）与本机识别 | 可用，真实音频验证过 | `qa\asr\xasr480-0.json`（RTF 0.078） |
| 会议记录 → 转写 → 归档（JSON 与 Mac 兼容） | 可用 | `qa\models-data\Sessions\...\transcript.jsonl` |
| 会后纪要（Markdown）、导出 | 可用 | 纪要页与 `summary.md` |
| 九个场景与提词样式、提词岛、字幕条、提词器 | 可用 | `qa\shots\v10-*.png`、`v06-teleprompter2.png` |
| 七个全局快捷键、托盘、一键隐藏 | 可用 | `v08-hidden.png`、`v08-tray-overflow.png` |
| 会议自动收尾 | 可用，实测触发 | `qa\autoend-data7\...\meta.json` |
| 结构化纪要（鱼骨图 / 大纲 / 观点对照） | 可用，逐条核对 + 绘图 | `v12-fishbone-drawn3.png`、`v13-notes-viewpoints3.png` |
| 九种纪要模板、按模板整理 | 可用 | `v18-notes-proofread3.png` |
| 校对转写（人名/术语） | 可用，改动留档 `corrections.json` | Core 779 项测试 |
| 到期日解析（周四 → 11 月 19 日（周四）） | 可用 | Core 779 项测试 |
| 系列与一句话经验 | 可用 | `v14-series4.png` |
| **系列知识库（经验 / 题库 / 待确认候选）** | 可用，离线全链路自检 | `v20-series-candidates2.png`、`qa\knowledge-0200\knowledge.json` |
| **逐字同传（字幕条 + 提词岛）** | 可用；门控与界面已验证，真实译文需回答服务 | `v21-island-translation.png`、Core 779 项测试 |
| **双屏演讲（窗口规则）** | 可用；单屏拒绝、双屏收起主窗口并留下提词岛 | `presenter-check.json`、`v22-settings-presenter-enabled.png` |
| **伴侣中转（HTTPS/WSS，端到端加密）** | 可用；本地中继 + 模拟手机端到端通过 | `qa\relay-check-0230\relay-check.json`、`phone.json` |
| **应用内更新（下载 / SHA-256 / 静默安装）** | 可用；篡改校验值会拒绝并删除下载 | `qa\update-install-0230\update-install.json`、`update-install-badhash.json` |
| **SRT / VTT 字幕导出** | 可用；纪要页导出，估算时间会报数 | `qa\subtitle-export-0270\demo.srt` / `.vtt` |
| **公式渲染（LaTeX → Unicode）** | 可用；答案与纪要里的公式转为可读 Unicode | `qa\formula-0320\formula-check.json` |
| 术语表 + 资料术语（识别热词 + 转写纠正） | 可用；`--hotwords-check` 验证合并结果 | `v15-settings-glossary6.png`、`qa\hotwords-0280\hotwords-check.json` |
| 检查更新（正式 / main 频道） | 可用（只提示并打开下载页） | `qa\update-report.json` |
| 局域网手机伴侣、区域截图 OCR、资料库检索 | 可用 | `v05-subtitles3.png`、`v06-capture-picker3.png` |

## 二、还差的（按影响排序）

1. **伴侣真机与蓝牙**：局域网 HTTP 已用本机回环实测（`--companion-check` 覆盖配对 token、快照、动作、去重、
   无 token 403），中转已用本地中继 + 模拟手机验证；公网 TLS 与真实手机仍未联调，BLE 在手机端取消名字过滤前不可用。
2. **AOT 音频**：AOT 探针（`NEXTTHOUGHT_DISABLE_AUDIO_DEVICES=1`）启动 0.53–0.65 秒、73.3 MiB、
   私有 35.7 MiB；但 NAudio 的设备枚举在 AOT 下不可用，要正式 AOT 需替换音频层。
3. **Authenticode 签名**：属于发布前事项，本阶段跳过、不作为阻塞条件；自签名链路已验证，正式 EV/OV 证书到位后启用。
4. **联网搜索真实检索**：Tavily 已接入并在设置页可关；真实检索需要 `TAVILY_API_KEY` 与网络。

**已完成**：Core 80/80 全部移植；真实双屏 `--presenter-check` 通过（2 块 2560×1440）；
`xasr480` + `xasrOffline` 模型已安装并由安装版复验；779 项 Core + 83 项基线测试；
1.3.7 安装版验收 60/60、门禁 17/17、工作集 108.6 MiB。

## 三、怎么自己验一遍

```powershell
# 构建 + 测试
dotnet build src\NextThought\Windows\NextThought.Windows.sln -c Release
dotnet test  src\NextThought\Windows\tests\NextThought.Core.Tests -c Release
dotnet test  src\NextThought\Windows\tests\NextThought.Tests -c Release

# 知识库全链路（不连模型、不开麦克风，10 项断言）
NextThought.exe --knowledge-check=D:\qa\knowledge.json

# 双屏演讲窗口行为（合成第二块屏）
NextThought.exe --presenter-check=D:\qa\presenter.json

# 中转端到端（先起 CompanionRelay，再用模拟手机）
set NEXTTHOUGHT_RELAY_URL=http://127.0.0.1:8787
NextThought.exe --relay-check=D:\qa\relay.json

# 字幕导出（演示数据，写出 SRT/VTT 与检查报告）
NextThought.exe --demo --subtitle-export-check=D:\qa\subtitles

# 资料术语进热词（导入一份文档，再合并术语表）
NextThought.exe --hotwords-check=D:\qa\hotwords

# 公式渲染（真实 MarkdownText 控件）
NextThought.exe --formula-check=D:\qa\formula.json

# 增量备份恢复点
NextThought.exe --backup-check=D:\qa\backup

# 装一次并自检
dist\NextThought-1.3.7-windows-x64-setup.exe /VERYSILENT /DIR=D:\NT-test
D:\NT-test\NextThought.exe --startup-check=D:\NT-test\startup.json
```

基线 79 项 + Core 779 项测试；安装包自检与安装后自检都会通过。
自检报告同时列出该构建的能力清单（`features`，32 项），一份 JSON 既证明能启动，也说明带了什么。
