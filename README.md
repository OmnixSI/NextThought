<div align="center">

# InterviewHacker

**会议、面试、网课的实时 AI 提词助手 · 原生 macOS**

听对方说话 → 实时转写 → 自动判断“这是不是在问我” → 逐字流式给出可以直接说出口的回答

[![Verify](https://github.com/ooooooomygosh/InterviewHacker/actions/workflows/verify.yml/badge.svg)](https://github.com/ooooooomygosh/InterviewHacker/actions/workflows/verify.yml)
![macOS 14+](https://img.shields.io/badge/macOS-14%2B-black)
![Apple Silicon](https://img.shields.io/badge/Apple%20Silicon-arm64-blue)

**[⬇️ 下载最新版本](https://github.com/ooooooomygosh/InterviewHacker/releases)** · [使用说明](#快速上手) · [隐私](#数据去向) · [开发文档](docs/DEVELOPMENT.md)

</div>

---

## 功能

| | |
| --- | --- |
| 🎧 **双路收音** | 分别采集会议窗口的系统声音（对方）和麦克风（我），转录里清楚标出谁在说话 |
| ⚡ **多种识别引擎** | 本机 Whisper（自动中英文，说话时即可预览）、Apple Speech 流式（macOS 26）、云端流式（Soniox / OpenAI / Deepgram，自带 Key） |
| 🧠 **问题识别** | 区分陈述、未说完、提问、请求解释、追问；排除自问自答和口头禅；拿不准的句子一键回答 |
| ✍️ **流式回答** | 常驻 Codex 服务或任意 OpenAI 兼容 API（DeepSeek / 通义 / Kimi / Groq / Ollama…）逐字输出，显示首字耗时 |
| 📄 **资料与 Prompt** | 导入简历、JD、议程、课件（PDF 含扫描件 OCR、Word、PPT、Excel、图片），按问题检索相关片段并标注页码；场景模板与预设 |
| 🪄 **一键调整** | 更简短、展开、举例、STAR、英文版、澄清问题、我可以反问；会前“预测可能的问题” |
| 🪟 **提词器** | 半透明悬浮窗，可放在摄像头下方，鼠标穿透，历史翻页，一键隐藏全部窗口 |
| 📱 **手机伴侣** | 扫码即用，手机实时显示回答并可遥控，屏幕共享完全看不到 |
| 📸 **截图提问** | 窗口或区域截图，适合代码题、数学题、幻灯片 |
| ⌨️ **全局快捷键** | 全部可自定义，见下表 |

## 安装

1. 到 **[Releases](https://github.com/ooooooomygosh/InterviewHacker/releases)** 下载 `InterviewHacker-<版本>-macOS-arm64.dmg`，打开后把 InterviewHacker 拖进“应用程序”。
2. 第一次打开：应用未经 Apple 公证，macOS 会拦截。打开 **系统设置 → 隐私与安全性**，在页面底部点 **“仍要打开”**。
   也可以在终端执行：`xattr -dr com.apple.quarantine /Applications/InterviewHacker.app`
3. 按提示允许 **屏幕与系统音频录制** 和 **麦克风**。授权后如系统要求重启应用，照做即可。

> 需要 Apple Silicon（M 系列芯片）和 macOS 14 或更新版本。Apple Speech 流式识别需要 macOS 26。

### 更新（不用每次重新授权）

推荐用命令行更新（需要已登录的 [GitHub CLI](https://cli.github.com)）：

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

1. **设置 → 回答引擎**
   - 已安装并登录 ChatGPT / Codex 客户端：用默认的“Codex 常驻服务”，选模型，推理强度建议 `low`。
   - 或选“OpenAI 兼容 API”，点“常用”选服务商，填模型 ID 和 API Key（存入钥匙串）。
2. **设置 → 语音识别引擎**
   - 本机 Whisper：首次需下载模型（推荐 `large-v3-turbo-q5_0`；电脑较慢选 `small-q5_1`）。顶部显示的 RTF 明显小于 1 才跟得上说话。
   - 追求最快、中英混说：选云端流式 + Soniox，填 Key 并勾选数据说明。
3. **资料** 页：拖入相关文件，选场景（面试 / 会议 / 课程），点“填入模板”后按需修改回答要求；可保存为预设。
4. **助手** 页选择会议窗口作为音频来源，点 **开始辅助**。对方提问会自动出回答；随时按 **⌘⇧Return** 立即回答。
5. 按 **⌘⇧O** 打开提词器，在 设置 → 提词器 里选“摄像头下方”。

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
- **共享整个屏幕**：设置里的“尽力从屏幕共享中排除”对很多软件有效，但 macOS 没有能保证排除录屏的公开接口，macOS 15 起部分整屏共享仍可能拍到。用 **设置 → 共享可见性测试** 自己确认；最稳妥是开 **手机伴侣** 用手机看。

## 数据去向

| 数据 | 去向 |
| --- | --- |
| 原始音频 | 默认只在本机识别，不保存。选择云端识别并勾选同意后，实时上传到你选的服务商 |
| 转录、问题、检索到的资料片段、截图 | 发送给你选的回答引擎（Codex 或你配置的 API） |
| 导入的文件 | 在本机解析；只在保存预设时写入本机 Application Support |
| API Key | 系统钥匙串 |
| 手机伴侣 | 默认关闭；只同步当前问题和回答，链接含一次性随机配对码，局域网明文 HTTP |
| 会话记录 | 默认不保存；开启后保存在本机，可导出 Markdown 或删除 |

请遵守会议、课程、考试和面试方的规则，并在录音和转写前取得参与者同意。本软件不宣称“无法被检测”。

## 文档

- [开发与发布](docs/DEVELOPMENT.md)：项目结构、构建、打包、签名与公证、发布流程
- [第二轮改进说明](docs/IMPROVEMENTS-2026-10-14-round2.md)：流式回答、问题识别、云端识别、手机伴侣等设计与验收清单
- [第一轮代码审计](docs/AUDIT-2026-10-14.md) · [历史验证记录](docs/VALIDATION-2026-10-14.md) · [更新日志](CHANGELOG.md)

## 从源码构建

需要 Apple Silicon、Xcode（Swift 6）和 CMake：

```sh
swift test --disable-sandbox     # 运行测试
./scripts/build-app.sh           # 生成 dist/InterviewHacker.app 与 ZIP
open dist/InterviewHacker.app
```

详见 [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)。

## 致谢

语音识别运行时基于 [whisper.cpp](https://github.com/ggml-org/whisper.cpp)（MIT），模型来自 [OpenAI Whisper](https://github.com/openai/whisper)。第三方声明见 [`Resources/ThirdPartyNotices.txt`](Resources/ThirdPartyNotices.txt)。
